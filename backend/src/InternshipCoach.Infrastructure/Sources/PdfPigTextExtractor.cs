using InternshipCoach.Application.Sources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace InternshipCoach.Infrastructure.Sources;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    private const int MaxPages = 30;

    public IReadOnlyList<string> ExtractPages(byte[] pdf)
    {
        try
        {
            using var document = PdfDocument.Open(pdf);
            return document.GetPages().Take(MaxPages).Select(page => ContentOrderTextExtractor.GetText(page)).ToList();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new SourceInputException("Could not read this PDF. Try exporting it again, or paste the text.");
        }
    }
}
