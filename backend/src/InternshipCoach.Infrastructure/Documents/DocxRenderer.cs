using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InternshipCoach.Application.Documents.ExportDocx;

namespace InternshipCoach.Infrastructure.Documents;

// shortcut: inline markdown (**bold**, links) is written as plain text; add run parsing if students start using it.
public sealed class DocxRenderer : IDocxRenderer
{
    private const int BulletNumberingId = 1;

    public byte[] Render(string markdown)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(
                HeadingStyle("Heading1", "heading 1", 36),
                HeadingStyle("Heading2", "heading 2", 28),
                HeadingStyle("Heading3", "heading 3", 24));
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = new Numbering(
                new AbstractNum(
                    new Level(
                        new NumberingFormat { Val = NumberFormatValues.Bullet },
                        new LevelText { Val = "•" },
                        new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }))
                    { LevelIndex = 0 })
                { AbstractNumberId = 0 },
                new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumberingId });

            var body = new Body();
            foreach (var raw in markdown.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0)
                {
                    body.Append(ToParagraph(line));
                }
            }

            main.Document = new Document(body);
        }

        return stream.ToArray();
    }

    private static Paragraph ToParagraph(string line)
    {
        var level = line.TakeWhile(c => c == '#').Count();
        if (level is >= 1 and <= 3 && line.Length > level && line[level] == ' ')
        {
            return Paragraph(line[(level + 1)..], new ParagraphProperties(new ParagraphStyleId { Val = $"Heading{level}" }));
        }

        if (line.StartsWith("- ") || line.StartsWith("* "))
        {
            return Paragraph(line[2..], new ParagraphProperties(
                new NumberingProperties(new NumberingLevelReference { Val = 0 }, new NumberingId { Val = BulletNumberingId })));
        }

        return Paragraph(line, null);
    }

    private static Paragraph Paragraph(string text, ParagraphProperties? properties)
    {
        var paragraph = new Paragraph();
        if (properties is not null)
        {
            paragraph.Append(properties);
        }

        paragraph.Append(new Run(new Text(text.Trim()) { Space = SpaceProcessingModeValues.Preserve }));
        return paragraph;
    }

    private static Style HeadingStyle(string id, string name, int halfPoints) =>
        new(
            new StyleName { Val = name },
            new NextParagraphStyle { Val = "Normal" },
            new StyleParagraphProperties(new SpacingBetweenLines { Before = "240", After = "120" }, new KeepNext()),
            new StyleRunProperties(new Bold(), new FontSize { Val = halfPoints.ToString() }))
        { Type = StyleValues.Paragraph, StyleId = id };
}
