using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Domain.Tests.Postings;

public class PostingTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    private static Posting Make(Guid? user = null, string company = "Acme", params (string, Importance)[] reqs) =>
        Posting.Create(user ?? Guid.NewGuid(), company, "Intern", "Gent", PostingLanguage.Nl, null, "text", null, reqs, false, Now);

    [Fact]
    public void Create_keeps_requirements_in_order_and_owned_by_the_user()
    {
        var user = Guid.NewGuid();
        var posting = Make(user, "Acme", ("A", Importance.Required), (" ", Importance.Nice), ("B", Importance.Nice));

        Assert.Equal(["A", "B"], posting.Requirements.Select(r => r.Text));
        Assert.All(posting.Requirements, r => Assert.Equal(user, r.UserId));
    }

    [Fact]
    public void Create_requires_company()
    {
        Assert.Throws<DomainException>(() => Make(company: " "));
    }

    [Fact]
    public void Create_requires_user()
    {
        Assert.Throws<DomainException>(() => Make(Guid.Empty));
    }

    [Fact]
    public void Language_codes_round_trip()
    {
        Assert.True(PostingLanguages.TryParse("FR", out var language));
        Assert.Equal("fr", language.ToCode());
        Assert.False(PostingLanguages.TryParse("de", out _));
    }

    [Fact]
    public void SetDates_stores_interview_in_utc()
    {
        var posting = Make();
        posting.SetDates(new DateOnly(2026, 12, 1), new DateTimeOffset(2026, 12, 3, 10, 0, 0, TimeSpan.FromHours(1)));

        Assert.Equal(TimeSpan.Zero, posting.InterviewAt!.Value.Offset);
        Assert.Equal(9, posting.InterviewAt.Value.Hour);
    }
}
