namespace InternshipCoach.Application.Documents.ExportDocx;

public interface IDocxRenderer
{
    /// <summary>A Word file from document markdown: #, ## and ### headings, - or * bullets, other lines as paragraphs.</summary>
    byte[] Render(string markdown);
}
