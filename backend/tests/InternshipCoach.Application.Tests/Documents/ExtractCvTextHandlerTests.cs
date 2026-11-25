using InternshipCoach.Application.Documents.ExtractCvText;
using InternshipCoach.Application.Sources;

namespace InternshipCoach.Application.Tests.Documents;

public class ExtractCvTextHandlerTests
{
    private sealed class FakeExtractor(string text = " Jan Peeters ") : ICvTextExtractor
    {
        public List<CvFileType> Calls { get; } = [];

        public string Extract(byte[] content, CvFileType type)
        {
            Calls.Add(type);
            return text;
        }
    }

    private static readonly byte[] Pdf = "%PDF-1.7 rest"u8.ToArray();
    private static readonly byte[] Zip = "PK\u0003\u0004rest"u8.ToArray();

    [Theory]
    [InlineData("cv.pdf", CvFileType.Pdf)]
    [InlineData("CV.PDF", CvFileType.Pdf)]
    [InlineData("cv.docx", CvFileType.Docx)]
    public void Pdf_and_docx_with_matching_magic_bytes_are_extracted(string name, CvFileType type)
    {
        var extractor = new FakeExtractor();

        var text = new ExtractCvTextHandler(extractor).Handle(name, type == CvFileType.Pdf ? Pdf : Zip);

        Assert.Equal("Jan Peeters", text);
        Assert.Equal(type, Assert.Single(extractor.Calls));
    }

    [Theory]
    [InlineData("cv.pdf", "zip")] // pdf extension, zip bytes
    [InlineData("cv.docx", "pdf")] // docx extension, pdf bytes
    [InlineData("cv.doc", "zip")] // old Word format
    [InlineData("cv.txt", "pdf")]
    [InlineData("cv.pdf", "text")]
    public void Wrong_extension_or_magic_bytes_are_rejected_before_extraction(string name, string bytes)
    {
        var extractor = new FakeExtractor();
        var content = bytes switch { "zip" => Zip, "pdf" => Pdf, _ => "hello"u8.ToArray() };

        Assert.Throws<SourceInputException>(() => new ExtractCvTextHandler(extractor).Handle(name, content));
        Assert.Empty(extractor.Calls);
    }

    [Fact]
    public void Files_over_5_mb_and_empty_files_are_rejected()
    {
        var extractor = new FakeExtractor();
        var big = new byte[ExtractCvTextHandler.MaxBytes + 1];
        Pdf.CopyTo(big, 0);

        Assert.Throws<SourceInputException>(() => new ExtractCvTextHandler(extractor).Handle("cv.pdf", big));
        Assert.Throws<SourceInputException>(() => new ExtractCvTextHandler(extractor).Handle("cv.pdf", []));
        Assert.Empty(extractor.Calls);
    }

    [Fact]
    public void A_file_without_text_is_rejected()
    {
        Assert.Throws<SourceInputException>(() => new ExtractCvTextHandler(new FakeExtractor("  \n ")).Handle("cv.pdf", Pdf));
    }
}
