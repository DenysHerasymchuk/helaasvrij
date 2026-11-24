using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Profile;

namespace InternshipCoach.Domain.Tests.Profile;

public class ProfileTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Item_needs_a_name_a_quote_and_a_document()
    {
        var doc = Guid.NewGuid();
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, " ", null, doc, "q", Now));
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", null, doc, " ", Now));
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", null, Guid.Empty, "q", Now));
    }

    [Fact]
    public void Item_starts_unconfirmed_with_its_first_quote_as_evidence()
    {
        var item = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "  Docker  ", null, Guid.NewGuid(), "Docker compose", Now);

        Assert.False(item.Confirmed);
        Assert.Equal("Docker", item.Name);
        Assert.Equal("docker", item.NormalizedName);
        Assert.Single(item.Evidence);
    }

    [Fact]
    public void Level_is_kept_only_for_languages_and_only_when_it_is_cefr()
    {
        var doc = Guid.NewGuid();
        var dutch = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Language, "Dutch", "c1", doc, "Dutch: C1", Now);
        var bad = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Language, "French", "fluent", doc, "French: fluent", Now);
        var skill = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Java", "B2", doc, "Java", Now);

        Assert.Equal("C1", dutch.Level);
        Assert.Null(bad.Level);
        Assert.Null(skill.Level);
    }

    [Fact]
    public void Same_quote_from_the_same_document_is_not_added_twice()
    {
        var doc = Guid.NewGuid();
        var item = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", null, doc, "Docker", Now);

        Assert.False(item.AddEvidence(doc, "Docker", null));
        Assert.True(item.AddEvidence(doc, "Dockerfile", null));
        Assert.True(item.AddEvidence(Guid.NewGuid(), "Docker", null));
        Assert.Equal(3, item.Evidence.Count);
    }

    [Fact]
    public void Basics_reject_an_end_before_the_start_and_a_long_school()
    {
        var user = Guid.NewGuid();
        Assert.Throws<DomainException>(() => ProfileBasics.Create(user, "School", new DateOnly(2027, 3, 1), new DateOnly(2027, 2, 1)));
        Assert.Throws<DomainException>(() => ProfileBasics.Create(user, new string('x', 201), null, null));

        var ok = ProfileBasics.Create(user, "  Howest ", new DateOnly(2027, 2, 1), new DateOnly(2027, 5, 31));
        Assert.Equal("Howest", ok.School);
    }
}
