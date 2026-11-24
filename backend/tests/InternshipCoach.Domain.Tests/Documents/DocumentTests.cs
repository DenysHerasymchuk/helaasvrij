using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Domain.Tests.Documents;

public class DocumentTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    private static Document Valid() => Document.Create(Guid.NewGuid(), DocumentKind.Cv, "nl", " My CV ", "# CV", Now);

    [Fact]
    public void Create_trims_title_and_sets_both_timestamps()
    {
        var document = Valid();

        Assert.Equal("My CV", document.Title);
        Assert.Equal("nl", document.Language);
        Assert.Equal(Now.Value, document.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("de")]
    [InlineData("NL")]
    public void Create_rejects_language_outside_nl_fr_en(string? language)
    {
        Assert.Throws<DomainException>(() => Document.Create(Guid.NewGuid(), DocumentKind.Cv, language, "t", "c", Now));
    }

    [Fact]
    public void Create_rejects_empty_user()
    {
        Assert.Throws<DomainException>(() => Document.Create(Guid.Empty, DocumentKind.Cv, "en", "t", "c", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_title(string title)
    {
        Assert.Throws<DomainException>(() => Document.Create(Guid.NewGuid(), DocumentKind.Cv, "en", title, "c", Now));
    }

    [Fact]
    public void Create_rejects_title_over_200_chars_and_blank_content()
    {
        Assert.Throws<DomainException>(() => Document.Create(Guid.NewGuid(), DocumentKind.Cv, "en", new string('a', 201), "c", Now));
        Assert.Throws<DomainException>(() => Document.Create(Guid.NewGuid(), DocumentKind.Cv, "en", "t", " ", Now));
    }

    [Fact]
    public void Edit_updates_title_content_and_updated_at()
    {
        var document = Valid();
        var later = Now.Value.AddHours(1);

        document.Edit("New", "Body", later);

        Assert.Equal("New", document.Title);
        Assert.Equal("Body", document.Content);
        Assert.Equal(later, document.UpdatedAt);
        Assert.Equal(Now.Value, document.CreatedAt.Value);
    }

    [Fact]
    public void Edit_rejects_empty_content_and_keeps_old_values()
    {
        var document = Valid();

        Assert.Throws<DomainException>(() => document.Edit("New", "", Now.Value));
        Assert.Equal("My CV", document.Title);
    }
}
