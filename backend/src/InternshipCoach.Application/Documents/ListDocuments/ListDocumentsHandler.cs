using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Documents.ListDocuments;

public sealed class ListDocumentsHandler(IDocumentRepository documents)
{
    public Task<IReadOnlyList<Document>> HandleAsync(CancellationToken ct) => documents.ListAsync(ct);
}
