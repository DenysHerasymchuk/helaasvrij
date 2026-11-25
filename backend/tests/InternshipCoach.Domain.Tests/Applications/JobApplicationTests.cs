using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Tests.Applications;

public class JobApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private static JobApplication New() => JobApplication.Create(Guid.NewGuid(), Guid.NewGuid(), new CreatedAt(Now));

    [Fact]
    public void Create_starts_saved_and_not_applied()
    {
        var application = New();

        Assert.Equal(ApplicationStage.Saved, application.Stage);
        Assert.Null(application.AppliedAt);
        Assert.Equal(Now, application.StageChangedAt);
    }

    [Fact]
    public void Create_requires_user_and_posting()
    {
        Assert.Throws<DomainException>(() => JobApplication.Create(Guid.Empty, Guid.NewGuid(), new CreatedAt(Now)));
        Assert.Throws<DomainException>(() => JobApplication.Create(Guid.NewGuid(), Guid.Empty, new CreatedAt(Now)));
    }

    [Fact]
    public void Rejected_from_saved_does_not_count_as_applied()
    {
        var application = New();

        application.MoveTo(ApplicationStage.Rejected, Now.AddDays(1));

        Assert.Null(application.AppliedAt);
        Assert.Equal(Now.AddDays(1), application.StageChangedAt);
    }

    [Fact]
    public void Moving_to_the_same_stage_changes_nothing()
    {
        var application = New();

        application.MoveTo(ApplicationStage.Saved, Now.AddDays(1));

        Assert.Equal(Now, application.StageChangedAt);
    }

    [Theory]
    [InlineData("saved", ApplicationStage.Saved)]
    [InlineData("Interview", ApplicationStage.Interview)]
    public void Stage_codes_parse_case_insensitively(string code, ApplicationStage expected)
    {
        Assert.True(ApplicationStages.TryParse(code, out var stage));
        Assert.Equal(expected, stage);
        Assert.False(ApplicationStages.TryParse("nope", out _));
    }
}
