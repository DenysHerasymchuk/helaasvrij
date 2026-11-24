using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class DocumentRepository(InternshipCoachDbContext db) : IDocumentRepository
{
    public Task<Document?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Documents.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

    // shortcut: sorts in memory (CreatedAt is a converted value object); add paging if a user ever has hundreds of documents.
    public async Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken) =>
        (await db.Documents.ToListAsync(cancellationToken)).OrderByDescending(document => document.CreatedAt.Value).ToList();

    public async Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task UpdateAsync(Document document, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(Document document, CancellationToken cancellationToken)
    {
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
    }
}
