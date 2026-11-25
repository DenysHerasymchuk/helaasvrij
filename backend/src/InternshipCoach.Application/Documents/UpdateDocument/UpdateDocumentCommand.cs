namespace InternshipCoach.Application.Documents.UpdateDocument;

public sealed record UpdateDocumentCommand(Guid Id, string? Title, string? Content);
