using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InternshipCoach.Application.Documents.ExtractCvText;
using InternshipCoach.Application.Sources;
using InternshipCoach.Infrastructure.Documents;
using InternshipCoach.Infrastructure.Sources;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace InternshipCoach.Infrastructure.Tests;

public class CvDocumentTests
{
    private static readonly ExtractCvTextHandler Handler = new(new CvTextExtractor(new PdfPigTextExtractor()));

    private static byte[] BuildPdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            builder.AddPage(595, 842).AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    private static byte[] BuildDocx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            document.AddMainDocumentPart().Document = new Document(new Body(paragraphs.Select(p => new Paragraph(new Run(new Text(p))))));
        }

        return stream.ToArray();
    }

    [Fact]
    public void Pdf_cv_text_is_extracted_from_all_pages()
    {
        var text = Handler.Handle("cv.pdf", BuildPdf("Lotte Peeters Java developer", "Skills Docker Postgres"));

        Assert.Contains("Lotte Peeters Java developer", text);
        Assert.Contains("Skills Docker Postgres", text);
    }

    [Fact]
    public void Docx_cv_text_is_extracted_one_line_per_paragraph()
    {
        var text = Handler.Handle("cv.docx", BuildDocx("Lotte Peeters", "Built a weather app in React."));

        Assert.Equal("Lotte Peeters\nBuilt a weather app in React.", text);
    }

    [Fact]
    public void A_zip_that_is_not_a_word_file_is_an_input_error() =>
        Assert.Throws<SourceInputException>(() => Handler.Handle("cv.docx", "PK\u0003\u0004 not really a zip"u8.ToArray()));

    [Fact]
    public void Docx_export_has_headings_bullets_and_paragraphs()
    {
        const string markdown = "# Lotte Peeters\n\nJava student.\n\n## Projects\n\n- Weather app in React\n- Bank API in .NET\n\n## Skills\n\nDocker, C#\n";

        var bytes = new DocxRenderer().Render(markdown);

        using var document = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var paragraphs = document.MainDocumentPart!.Document!.Body!.Elements<Paragraph>().ToList();
        string? Style(Paragraph p) => p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;

        Assert.Equal("Lotte Peeters", Assert.Single(paragraphs, p => Style(p) == "Heading1").InnerText);
        Assert.Equal(["Projects", "Skills"], paragraphs.Where(p => Style(p) == "Heading2").Select(p => p.InnerText));
        Assert.Equal(["Weather app in React", "Bank API in .NET"],
            paragraphs.Where(p => p.ParagraphProperties?.NumberingProperties is not null).Select(p => p.InnerText));
        Assert.Contains(paragraphs, p => p.InnerText == "Docker, C#" && p.ParagraphProperties is null);
        Assert.NotNull(document.MainDocumentPart.StyleDefinitionsPart?.Styles);
        Assert.NotNull(document.MainDocumentPart.NumberingDefinitionsPart?.Numbering);
    }
}
