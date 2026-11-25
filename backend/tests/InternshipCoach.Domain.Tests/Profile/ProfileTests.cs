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
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, " ", doc, "q", Now));
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", doc, " ", Now));
        Assert.Throws<DomainException>(() => ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", Guid.Empty, "q", Now));
    }

    [Fact]
    public void Item_starts_unconfirmed_with_its_first_quote_as_evidence()
    {
        var item = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "  Docker  ", Guid.NewGuid(), "Docker compose", Now);

        Assert.False(item.Confirmed);
        Assert.Equal("Docker", item.Name);
        Assert.Equal("docker", item.NormalizedName);
        Assert.Single(item.Evidence);
    }

    [Fact]
    public void Same_quote_from_the_same_document_is_not_added_twice()
    {
        var doc = Guid.NewGuid();
        var item = ProfileItem.Create(Guid.NewGuid(), ProfileItemKind.Skill, "Docker", doc, "Docker", Now);

        Assert.False(item.AddEvidence(doc, "Docker"));
        Assert.True(item.AddEvidence(doc, "Dockerfile"));
        Assert.True(item.AddEvidence(Guid.NewGuid(), "Docker"));
        Assert.Equal(3, item.Evidence.Count);
    }
}
