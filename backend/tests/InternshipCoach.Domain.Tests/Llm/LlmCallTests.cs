using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;

namespace InternshipCoach.Domain.Tests.Llm;

public class LlmCallTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_stores_counts_and_no_text()
    {
        var call = LlmCall.Create(Guid.NewGuid(), "profile", "anthropic/claude-sonnet-5-5", 120, 80, 950, true, Now);

        Assert.Equal(120, call.InputTokens);
        Assert.Equal(80, call.OutputTokens);
        Assert.True(call.Success);
    }

    [Fact]
    public void Create_rejects_empty_user()
    {
        Assert.Throws<DomainException>(() => LlmCall.Create(Guid.Empty, "f", "m", 1, 1, 1, true, Now));
    }

    [Fact]
    public void Create_rejects_negative_tokens()
    {
        Assert.Throws<DomainException>(() => LlmCall.Create(Guid.NewGuid(), "f", "m", -1, 1, 1, true, Now));
    }

    [Fact]
    public void Create_rejects_blank_feature_or_model()
    {
        Assert.Throws<DomainException>(() => LlmCall.Create(Guid.NewGuid(), " ", "m", 1, 1, 1, true, Now));
        Assert.Throws<DomainException>(() => LlmCall.Create(Guid.NewGuid(), "f", "", 1, 1, 1, true, Now));
    }
}
