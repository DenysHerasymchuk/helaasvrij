using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Documents.UpdateDocument;

/// <summary>Document is null and Error is null: not found (or another user's).</summary>
public sealed record UpdateDocumentResult(Document? Document, string? Error);

public sealed class UpdateDocumentHandler(IDocumentRepository documents, IClock clock)
{
    public async Task<UpdateDocumentResult> HandleAsync(UpdateDocumentCommand command, CancellationToken ct)
    {
        var document = await documents.FindAsync(command.Id, ct);
        if (document is null)
        {
            return new UpdateDocumentResult(null, null);
        }

        try
        {
            document.Edit(command.Title, command.Content, clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return new UpdateDocumentResult(null, ex.Message);
        }

        await documents.UpdateAsync(document, ct);
        return new UpdateDocumentResult(document, null);
    }
}
