using System.Reflection;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Interview;
using InternshipCoach.Domain.Llm;
using InternshipCoach.Domain.Settings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Domain.Sprints;
using InternshipCoach.Domain.Tokens;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Users;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public class InternshipCoachDbContext : DbContext, IDataProtectionKeyContext
{
    public const string SchemaName = "coach";

    private static readonly MethodInfo ApplyUserFilterMethod = typeof(InternshipCoachDbContext)
        .GetMethod(nameof(ApplyUserFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Null at design time (migrations) and when no user is signed in; the filter then matches nothing.
    private readonly ICurrentUser? _currentUser;

    public InternshipCoachDbContext(DbContextOptions<InternshipCoachDbContext> options, ICurrentUser? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    protected InternshipCoachDbContext(DbContextOptions options, ICurrentUser? currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<LlmCall> LlmCalls => Set<LlmCall>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<Source> Sources => Set<Source>();

    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<ProfileItem> ProfileItems => Set<ProfileItem>();

    public DbSet<ProfileEvidence> ProfileEvidence => Set<ProfileEvidence>();


    public DbSet<Posting> Postings => Set<Posting>();

    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<InterviewQuestion> InterviewQuestions => Set<InterviewQuestion>();

    public DbSet<InterviewAnswer> InterviewAnswers => Set<InterviewAnswer>();

    // Shared and anonymous (not IUserOwned): no query filter, never deleted with an account.
    public DbSet<CompanyQuestion> CompanyQuestions => Set<CompanyQuestion>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();

    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    public DbSet<ScoreSnapshot> ScoreSnapshots => Set<ScoreSnapshot>();

    // Cookie-encryption keys live in the database so sessions survive an api container restart.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    // Read by the query filter on every query (EF parameterises it per context instance).
    private Guid FilterUserId => _currentUser?.UserId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InternshipCoachDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(IUserOwned).IsAssignableFrom(type.ClrType) && type.BaseType is null)
                     .ToList())
        {
            ApplyUserFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyUserFilter<T>(ModelBuilder modelBuilder) where T : class, IUserOwned =>
        modelBuilder.Entity<T>().HasQueryFilter(entity => entity.UserId == FilterUserId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureOwnedByCurrentUser();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureOwnedByCurrentUser();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Defence in depth: the filter hides other users' rows on read, this stops writing for another user.
    private void EnsureOwnedByCurrentUser()
    {
        var changed = ChangeTracker.Entries<IUserOwned>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (changed.Count == 0)
        {
            return;
        }

        var userId = _currentUser?.UserId
            ?? throw new InvalidOperationException("Cannot save user-owned data without a current user.");

        if (changed.Any(entry => entry.Entity.UserId != userId))
        {
            throw new InvalidOperationException("Cannot save data owned by another user.");
        }
    }
}
