using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Abstractions;

// Reads see only the signed-in user's documents (global query filter).
public interface IDocumentRepository
{
    Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Document document, CancellationToken cancellationToken);

    Task UpdateAsync(Document document, CancellationToken cancellationToken);

    Task DeleteAsync(Document document, CancellationToken cancellationToken);
}
