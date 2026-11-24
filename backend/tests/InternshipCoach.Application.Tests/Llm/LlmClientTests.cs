using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Tests.TestDoubles;

namespace InternshipCoach.Application.Tests.Llm;

public class LlmClientTests
{
    private const string Schema = """
        {"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}
        """;

    private static readonly LlmCallContext Context = new(Guid.NewGuid(), "test");
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    public sealed record Skill(string Name);

    private static (LlmClient Client, FakeLlmTransport Transport, FakeLlmCallLog Log) Create(int cap = 200, params string[] replies)
    {
        var transport = new FakeLlmTransport(replies);
        var log = new FakeLlmCallLog();
        return (new LlmClient(transport, log, Clock, new LlmLimits(cap)), transport, log);
    }

    private static Task<LlmResult<Skill>> Json(LlmClient client, LlmModel model = LlmModel.Cheap, int maxTokens = 500) =>
        client.CompleteJsonAsync<Skill>(Context, model, "sys", "user", Schema, maxTokens, CancellationToken.None);

    [Fact]
    public async Task Valid_json_in_a_code_fence_is_parsed_on_first_attempt()
    {
        var (client, transport, log) = Create(replies: "```json\n{\"name\":\"docker\"}\n```");

        var result = await Json(client);

        Assert.Equal("docker", result.Value.Name);
        Assert.Equal(1, result.Attempts);
        Assert.Single(transport.Requests);
        Assert.Single(log.Records);
        Assert.True(log.Records[0].Success);
    }

    [Fact]
    public async Task Schema_goes_in_system_prompt_with_untrusted_notice()
    {
        var (client, transport, _) = Create(replies: """{"name":"a"}""");

        await Json(client);

        Assert.Contains(Schema, transport.Requests[0].System);
        Assert.Contains("untrusted_data", transport.Requests[0].System);
    }

    [Fact]
    public async Task Invalid_json_is_retried_once_with_the_error_appended()
    {
        var (client, transport, log) = Create(replies: ["not json at all", """{"name":"ok"}"""]);

        var result = await Json(client);

        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal(3, transport.Requests[1].Turns.Count);
        Assert.Contains("rejected", transport.Requests[1].Turns[2].Content);
        Assert.Equal([false, true], log.Records.Select(r => r.Success));
        Assert.Equal(20, result.InputTokens);
    }

    [Fact]
    public async Task Schema_failure_twice_throws_typed_error_after_two_attempts()
    {
        var (client, transport, _) = Create(replies: ["""{"wrong":1}""", """{"wrong":2}"""]);

        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => Json(client));

        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task Empty_reply_is_retried_once()
    {
        var (client, transport, _) = Create(replies: ["", """{"name":"x"}"""]);

        var result = await Json(client);

        Assert.Equal(2, result.Attempts);
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task Smart_model_gets_at_least_4000_max_tokens()
    {
        var (client, transport, log) = Create(replies: """{"name":"x"}""");

        await Json(client, LlmModel.Smart, maxTokens: 500);

        Assert.Equal(4000, transport.Requests[0].MaxTokens);
        Assert.Equal(LlmModel.Smart, transport.Requests[0].Model);
        Assert.Equal(LlmModels.SmartId, log.Records[0].Model);
    }

    [Fact]
    public async Task Log_records_model_and_tokens_but_no_text()
    {
        var (client, _, log) = Create(replies: """{"name":"secret cv line"}""");

        await Json(client);

        var record = log.Records.Single();
        Assert.Equal("test", record.Feature);
        Assert.Equal(LlmModels.CheapId, record.Model);
        Assert.Equal(10, record.InputTokens);
        Assert.Equal(5, record.OutputTokens);
        Assert.DoesNotContain(typeof(LlmCallRecord).GetProperties(), p => p.PropertyType == typeof(string) && p.Name is "Prompt" or "Response" or "Text");
    }

    [Fact]
    public async Task Daily_cap_blocks_the_call_before_it_is_sent()
    {
        var (client, transport, log) = Create(cap: 3, replies: """{"name":"x"}""");
        log.PreExisting = 3;

        await Assert.ThrowsAsync<LlmDailyCapExceededException>(() => Json(client));

        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Text_call_retries_once_on_empty_then_returns_text()
    {
        var (client, transport, _) = Create(replies: ["  ", "  hello  "]);

        var result = await client.CompleteTextAsync(
            Context, LlmModel.Smart, "sys", [new ChatTurn(ChatRole.User, "hi")], 100, CancellationToken.None);

        Assert.Equal("hello", result.Text);
        Assert.Equal(2, transport.Requests.Count);
    }

    [Fact]
    public async Task Text_call_gives_up_after_two_empty_replies()
    {
        var (client, _, _) = Create(replies: ["", ""]);

        await Assert.ThrowsAsync<LlmInvalidResponseException>(() => client.CompleteTextAsync(
            Context, LlmModel.Cheap, "sys", [new ChatTurn(ChatRole.User, "hi")], 100, CancellationToken.None));
    }
}
