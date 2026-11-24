using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Domain.Tests.Postings;

public class ScoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private static Posting Make(params (string, Importance)[] reqs) =>
        Posting.Create(Guid.NewGuid(), "Acme", "Intern", "Gent", PostingLanguage.Nl, null, "text", null, reqs, false, new CreatedAt(Now));

    private static void Cover(Requirement r, Coverage c) =>
        r.RecordAnalysis(c, c == Coverage.None ? null : "quote", c == Coverage.None ? null : Guid.NewGuid(), null, null, null, Now);

    [Fact]
    public void Zero_requirements_score_zero()
    {
        Assert.Equal(0, Make().Score());
    }

    [Fact]
    public void Unanalysed_posting_scores_zero()
    {
        Assert.Equal(0, Make(("a", Importance.Required)).Score());
    }

    [Fact]
    public void Weights_and_coverage_follow_the_formula()
    {
        // required strong (2*1) + required partial (2*0.5) + nice none (0) = 3 of 5 = 60%
        var posting = Make(("a", Importance.Required), ("b", Importance.Required), ("c", Importance.Nice));
        Cover(posting.Requirements[0], Coverage.Strong);
        Cover(posting.Requirements[1], Coverage.Partial);
        Cover(posting.Requirements[2], Coverage.None);

        Assert.Equal(60, posting.Score());
    }

    [Fact]
    public void Score_rounds_half_up_to_a_whole_percent()
    {
        // 1 of 3 weights... nice partial (0.5) of 3 nice = 0.5/3 = 16.67 -> 17; one partial of 8 = 6.25 -> 6; 1 half of 4 nice... 
        var posting = Make(("a", Importance.Nice), ("b", Importance.Nice), ("c", Importance.Nice));
        Cover(posting.Requirements[0], Coverage.Partial);
        Assert.Equal(17, posting.Score());

        var half = Make(("a", Importance.Nice), ("b", Importance.Nice), ("c", Importance.Nice), ("d", Importance.Nice),
            ("e", Importance.Nice), ("f", Importance.Nice), ("g", Importance.Nice), ("h", Importance.Nice));
        Cover(half.Requirements[0], Coverage.Partial);
        Assert.Equal(6, half.Score()); // 6.25

        var exact = Make(("a", Importance.Nice), ("b", Importance.Nice), ("c", Importance.Nice), ("d", Importance.Nice),
            ("e", Importance.Nice), ("f", Importance.Nice), ("g", Importance.Nice), ("h", Importance.Nice), ("i", Importance.Nice),
            ("j", Importance.Nice), ("k", Importance.Nice), ("l", Importance.Nice), ("m", Importance.Nice), ("n", Importance.Nice),
            ("o", Importance.Nice), ("p", Importance.Nice), ("q", Importance.Nice), ("r", Importance.Nice), ("s", Importance.Nice),
            ("t", Importance.Nice));
        Cover(exact.Requirements[0], Coverage.Partial);
        Assert.Equal(3, exact.Score()); // 2.5 rounds up
    }

    [Fact]
    public void Same_input_gives_the_same_score()
    {
        var posting = Make(("a", Importance.Required), ("b", Importance.Nice));
        Cover(posting.Requirements[0], Coverage.Partial);
        Assert.Equal(posting.Score(), posting.Score());
    }

    [Fact]
    public void Low_score_or_few_required_covered_is_no_clear_fit()
    {
        var low = Make(("a", Importance.Required), ("b", Importance.Required), ("c", Importance.Required));
        Cover(low.Requirements[0], Coverage.Strong);
        Assert.True(low.NoClearFit()); // 33%

        var fine = Make(("a", Importance.Required), ("b", Importance.Required));
        Cover(fine.Requirements[0], Coverage.Strong);
        Cover(fine.Requirements[1], Coverage.Partial);
        Assert.False(fine.NoClearFit());

        // score 60% from nice items, but 0 of 2 required covered
        var niceOnly = Make(("r1", Importance.Required), ("r2", Importance.Required), ("n1", Importance.Nice), ("n2", Importance.Nice),
            ("n3", Importance.Nice), ("n4", Importance.Nice), ("n5", Importance.Nice), ("n6", Importance.Nice));
        foreach (var n in niceOnly.Requirements.Skip(2))
        {
            Cover(n, Coverage.Strong);
        }

        Assert.Equal(60, niceOnly.Score());
        Assert.True(niceOnly.NoClearFit());
    }

    [Fact]
    public void Partial_coverage_needs_evidence()
    {
        var posting = Make(("a", Importance.Required));
        Assert.Throws<DomainException>(() => posting.Requirements[0].RecordAnalysis(Coverage.Partial, null, null, null, null, null, Now));
    }

    [Fact]
    public void Snapshot_stores_the_current_score()
    {
        var posting = Make(("a", Importance.Required));
        Cover(posting.Requirements[0], Coverage.Strong);
        var snapshot = posting.RecordSnapshot("analyse", Now);

        Assert.Equal(100, snapshot.Score);
        Assert.Single(posting.Snapshots);
    }
}
