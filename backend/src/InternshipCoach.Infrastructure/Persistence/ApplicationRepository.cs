using InternshipCoach.Application.Applications;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Postings;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class ApplicationRepository(InternshipCoachDbContext db) : IApplicationRepository
{
    // Both sets carry the user filter, so the join only ever sees the signed-in user's rows.
    private IQueryable<Row> Rows(Guid? id = null) =>
        from application in db.Applications.AsNoTracking()
        where id == null || application.Id == id
        join posting in db.Postings.AsNoTracking() on application.PostingId equals posting.Id
        select new Row(application, posting.Company, posting.Title, posting.City, posting.Deadline, posting.Number);

    private sealed record Row(JobApplication Application, string Company, string Title, string City, DateOnly? Deadline, int Number)
    {
        public ApplicationCard ToCard() => new(Application, Company, Title, City, Deadline, PositionKey.Of(Company, Number));
    }

    public async Task<IReadOnlyList<ApplicationCard>> ListAsync(CancellationToken cancellationToken) =>
        (await Rows().ToListAsync(cancellationToken)).Select(row => row.ToCard())
        .OrderByDescending(card => card.Application.StageChangedAt).ToList();

    public async Task<ApplicationCard?> GetCardAsync(Guid id, CancellationToken cancellationToken) =>
        (await Rows(id).FirstOrDefaultAsync(cancellationToken))?.ToCard();

    public Task<JobApplication?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Applications.FirstOrDefaultAsync(application => application.Id == id, cancellationToken);

    public Task<Guid?> FindIdForPostingAsync(Guid postingId, CancellationToken cancellationToken) =>
        db.Applications.Where(application => application.PostingId == postingId)
            .Select(application => (Guid?)application.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<JobApplication>> ListForSprintAsync(Guid sprintId, CancellationToken cancellationToken) =>
        await db.Applications.Where(application => application.SprintId == sprintId).ToListAsync(cancellationToken);

    // Events have no user column of their own: joining the filtered applications scopes them to the signed-in user.
    public async Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(CancellationToken cancellationToken) =>
        await (from applicationEvent in db.ApplicationEvents.AsNoTracking()
               join application in db.Applications.AsNoTracking() on applicationEvent.ApplicationId equals application.Id
               select applicationEvent).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(Guid applicationId, CancellationToken cancellationToken) =>
        await (from applicationEvent in db.ApplicationEvents.AsNoTracking()
               join application in db.Applications.AsNoTracking() on applicationEvent.ApplicationId equals application.Id
               where application.Id == applicationId
               orderby applicationEvent.At
               select applicationEvent).ToListAsync(cancellationToken);

    public async Task AddAsync(JobApplication application, CancellationToken cancellationToken)
    {
        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
