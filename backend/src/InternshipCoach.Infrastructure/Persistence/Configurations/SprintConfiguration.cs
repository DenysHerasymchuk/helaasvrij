using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Sprints;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InternshipCoach.Infrastructure.Persistence.Configurations;

public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("sprints");
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(sprint => sprint.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(sprint => sprint.Name).HasColumnName("name").HasMaxLength(Sprint.NameMaxLength).IsRequired();
        builder.Property(sprint => sprint.Goal).HasColumnName("goal").HasMaxLength(Sprint.GoalMaxLength).IsRequired();
        builder.Property(sprint => sprint.StartsOn).HasColumnName("starts_on").IsRequired();
        builder.Property(sprint => sprint.EndsOn).HasColumnName("ends_on").IsRequired();
        builder.Property(sprint => sprint.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(sprint => sprint.CompletedAt).HasColumnName("completed_at");
        builder.Property(sprint => sprint.CreatedAt)
            .HasColumnName("created_at")
            .HasConversion(createdAt => createdAt.Value, value => new CreatedAt(value))
            .IsRequired();

        // A user has at most one active sprint; the database refuses a second one even if two requests race.
        builder.HasIndex(sprint => sprint.UserId)
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_sprints_one_active_per_user");
        builder.HasIndex(sprint => new { sprint.UserId, sprint.CreatedAt });
    }
}

public sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> builder)
    {
        builder.ToTable("application_events");
        builder.HasKey(applicationEvent => applicationEvent.Id);
        builder.Property(applicationEvent => applicationEvent.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(applicationEvent => applicationEvent.ApplicationId).HasColumnName("application_id").IsRequired();
        builder.Property(applicationEvent => applicationEvent.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(applicationEvent => applicationEvent.FromStage).HasColumnName("from_stage").HasConversion<string>().HasMaxLength(16);
        builder.Property(applicationEvent => applicationEvent.ToStage).HasColumnName("to_stage").HasConversion<string>().HasMaxLength(16);
        builder.Property(applicationEvent => applicationEvent.SprintId).HasColumnName("sprint_id");
        builder.Property(applicationEvent => applicationEvent.At).HasColumnName("at").IsRequired();

        builder.HasIndex(applicationEvent => new { applicationEvent.ApplicationId, applicationEvent.At });
        builder.HasIndex(applicationEvent => applicationEvent.SprintId);
    }
}
