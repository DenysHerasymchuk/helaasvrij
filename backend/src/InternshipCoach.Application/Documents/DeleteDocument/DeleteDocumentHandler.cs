using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Documents.DeleteDocument;

public sealed class DeleteDocumentHandler(IDocumentRepository documents)
{
    /// <summary>False when the id does not exist or belongs to another user.</summary>
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct)
    {
        var document = await documents.FindAsync(id, ct);
        if (document is null)
        {
            return false;
        }

        await documents.DeleteAsync(document, ct);
        return true;
    }
}
