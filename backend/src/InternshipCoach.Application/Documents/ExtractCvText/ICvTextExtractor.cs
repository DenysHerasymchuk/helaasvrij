namespace InternshipCoach.Application.Documents.ExtractCvText;

public enum CvFileType
{
    Pdf,
    Docx
}

public interface ICvTextExtractor
{
    /// <summary>Plain text of the file. Throws <see cref="Sources.SourceInputException" /> for an unreadable file.</summary>
    string Extract(byte[] content, CvFileType type);
}
