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

    private static readonly DateOnly Feb = new(2027, 2, 1);
    private static readonly DateOnly May = new(2027, 5, 31);

    [Theory]
    [InlineData("2027-02-15", "2027-05-15", StageFit.Fits)]
    [InlineData("2027-01-15", "2027-05-15", StageFit.Conflict)]
    [InlineData("2027-02-15", "2027-06-30", StageFit.Conflict)]
    [InlineData("2027-07-01", null, StageFit.Conflict)]
    [InlineData(null, "2027-04-30", StageFit.Fits)]
    [InlineData(null, null, StageFit.Unknown)]
    public void CheckStage_needs_every_known_posting_date_inside_the_student_period(string? start, string? end, StageFit expected)
    {
        var posting = Make();
        posting.SetStagePeriod(start is null ? null : DateOnly.Parse(start), end is null ? null : DateOnly.Parse(end));

        Assert.Equal(expected, posting.CheckStage(Feb, May));
    }

    [Fact]
    public void CheckStage_is_unknown_without_a_student_period_and_open_sides_accept_anything()
    {
        var posting = Make();
        posting.SetStagePeriod(new DateOnly(2027, 1, 10), new DateOnly(2027, 3, 1));

        Assert.Equal(StageFit.Unknown, posting.CheckStage(null, null));
        Assert.Equal(StageFit.Fits, posting.CheckStage(null, May));
        Assert.Equal(StageFit.Conflict, posting.CheckStage(Feb, null));
    }

    [Fact]
    public void SetStagePeriod_rejects_an_end_before_the_start()
    {
        Assert.Throws<DomainException>(() => Make().SetStagePeriod(May, Feb));
    }
}
