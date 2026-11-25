namespace InternshipCoach.Application.Sources;

public interface IPdfTextExtractor
{
    /// <summary>Text of each page in order (empty string for a page without text). Throws <see cref="SourceInputException" /> for an unreadable PDF.</summary>
    IReadOnlyList<string> ExtractPages(byte[] pdf);
}
