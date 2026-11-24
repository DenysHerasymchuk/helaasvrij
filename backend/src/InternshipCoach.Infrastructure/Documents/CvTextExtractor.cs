using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InternshipCoach.Application.Documents.ExtractCvText;
using InternshipCoach.Application.Sources;

namespace InternshipCoach.Infrastructure.Documents;

// PDF goes through the same PdfPig extractor as the CV source; DOCX is read with OpenXml, one line per paragraph.
public sealed class CvTextExtractor(IPdfTextExtractor pdf) : ICvTextExtractor
{
    public string Extract(byte[] content, CvFileType type) =>
        type == CvFileType.Pdf ? string.Join("\n\n", pdf.ExtractPages(content).Select(page => page.Trim())) : ExtractDocx(content);

    private static string ExtractDocx(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var document = WordprocessingDocument.Open(stream, false);
            var body = document.MainDocumentPart?.Document?.Body;
            return body is null ? string.Empty : string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText.Trim()));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new SourceInputException("Could not read this Word file. Try saving it again as .docx, or paste the text.");
        }
    }
}
