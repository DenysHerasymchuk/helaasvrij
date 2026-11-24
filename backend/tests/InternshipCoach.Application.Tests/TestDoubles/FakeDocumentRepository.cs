using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeDocumentRepository : IDocumentRepository
{
    public List<Document> Documents { get; } = [];

    public Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Document>>(Documents.OrderByDescending(d => d.CreatedAt.Value).ToList());

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        Documents.Add(document);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DeleteAsync(Document document, CancellationToken cancellationToken)
    {
        Documents.Remove(document);
        return Task.CompletedTask;
    }
}

public sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}
