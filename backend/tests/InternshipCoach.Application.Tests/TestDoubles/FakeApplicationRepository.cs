using InternshipCoach.Application.Applications;
using InternshipCoach.Domain.Applications;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeApplicationRepository(FakePostingRepository postings) : IApplicationRepository
{
    public List<JobApplication> Items { get; } = [];

    public int Saves { get; private set; }

    private ApplicationCard? ToCard(JobApplication application)
    {
        var posting = postings.Items.FirstOrDefault(p => p.Id == application.PostingId);
        return posting is null ? null : new ApplicationCard(application, posting.Company, posting.Title, posting.City, posting.Deadline, posting.Key);
    }

    public Task<IReadOnlyList<JobApplication>> ListForSprintAsync(Guid sprintId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<JobApplication>>(Items.Where(a => a.SprintId == sprintId).ToList());

    public Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ApplicationEvent>>(Items.SelectMany(a => a.Events).OrderBy(e => e.At).ToList());

    public Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(Guid applicationId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ApplicationEvent>>(
            Items.Where(a => a.Id == applicationId).SelectMany(a => a.Events).OrderBy(e => e.At).ToList());

    public Task<IReadOnlyList<ApplicationCard>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ApplicationCard>>(Items.Select(ToCard).OfType<ApplicationCard>()
            .OrderByDescending(c => c.Application.StageChangedAt).ToList());

    public Task<ApplicationCard?> GetCardAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.Where(a => a.Id == id).Select(ToCard).FirstOrDefault());

    public Task<JobApplication?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<Guid?> FindIdForPostingAsync(Guid postingId, CancellationToken ct) =>
        Task.FromResult(Items.Where(a => a.PostingId == postingId).Select(a => (Guid?)a.Id).FirstOrDefault());

    public Task AddAsync(JobApplication application, CancellationToken ct)
    {
        Items.Add(application);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
