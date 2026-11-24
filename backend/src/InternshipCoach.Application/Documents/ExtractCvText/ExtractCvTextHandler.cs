using InternshipCoach.Application.Sources;

namespace InternshipCoach.Application.Documents.ExtractCvText;

// Turns an uploaded CV into text the student checks before tailoring. The file is never stored.
public sealed class ExtractCvTextHandler(ICvTextExtractor extractor)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <summary>Throws <see cref="SourceInputException" /> for a wrong, empty, too large or unreadable file.</summary>
    public string Handle(string fileName, byte[] content)
    {
        if (content.Length == 0)
        {
            throw new SourceInputException("The file is empty.");
        }

        if (content.Length > MaxBytes)
        {
            throw new SourceInputException("The file is larger than 5 MB.");
        }

        CvFileType? type = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" when content.AsSpan().StartsWith("%PDF"u8) => CvFileType.Pdf,
            ".docx" when content.AsSpan().StartsWith("PK\u0003\u0004"u8) => CvFileType.Docx,
            _ => null
        };

        if (type is null)
        {
            throw new SourceInputException("Upload a PDF or Word (.docx) file.");
        }

        var text = extractor.Extract(content, type.Value).Trim();
        if (text.Length == 0)
        {
            throw new SourceInputException("No text found in this file (a scanned image?). Paste the text instead.");
        }

        return text;
    }
}
