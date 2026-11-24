using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Documents.GetDocument;

public sealed class GetDocumentHandler(IDocumentRepository documents)
{
    /// <summary>Null when the id does not exist or belongs to another user.</summary>
    public Task<Document?> HandleAsync(Guid id, CancellationToken ct) => documents.FindAsync(id, ct);
}
