using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources.Cv;

public sealed class UploadCvHandler(ISourceRepository sources, IPdfTextExtractor pdf, ICurrentUser currentUser, IClock clock)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    public async Task<SourceSummary> HandleAsync(UploadCvCommand command, CancellationToken ct)
    {
        if (command.Content.Length == 0)
        {
            throw new SourceInputException("The file is empty.");
        }

        if (command.Content.Length > MaxBytes)
        {
            throw new SourceInputException("The PDF is larger than 5 MB.");
        }

        if (!command.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !HasPdfHeader(command.Content))
        {
            throw new SourceInputException("Upload a PDF file.");
        }

        var pages = pdf.ExtractPages(command.Content);
        var source = Source.Create(currentUser.UserId, SourceKind.Cv, Path.GetFileName(command.FileName));
        for (var index = 0; index < pages.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(pages[index]))
            {
                source.AddDocument($"cv.pdf#page{index + 1}", pages[index].Trim());
            }
        }

        if (source.Documents.Count == 0)
        {
            throw new SourceInputException("No text found in this PDF (a scanned image?). Paste the text instead.");
        }

        source.MarkReady(clock.UtcNow);
        await sources.AddAsync(source, ct);
        return SourceSummary.From(source);
    }

    private static bool HasPdfHeader(byte[] content) =>
        content.Length >= 5 && content[0] == '%' && content[1] == 'P' && content[2] == 'D' && content[3] == 'F' && content[4] == '-';
}
