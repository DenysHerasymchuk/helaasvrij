using System.Net;
using System.Text;
using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Infrastructure.Llm;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternshipCoach.Infrastructure.Tests.Llm;

public class ClaudeCliFallbackTests
{
    private static readonly ChatTurn[] Turns = [new(ChatRole.User, "hello")];

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FakeTransport(string text) : ILlmTransport
    {
        public int Calls { get; private set; }

        public Task<LlmTransportResponse> SendAsync(LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new LlmTransportResponse(text, 1, 1));
        }
    }

    private const string Route33Ok = """
        {"id":"msg_1","type":"message","role":"assistant","model":"anthropic/claude-sonnet-5-5",
         "content":[{"type":"text","text":"from-route33"}],"stop_reason":"end_turn","stop_sequence":null,
         "usage":{"input_tokens":12,"output_tokens":7}}
        """;

    private static Route33LlmTransport Route33(StubHandler handler, string? key = "k") =>
        new(new Route33Options { ApiKey = key, BaseUrl = "https://gateway.test" }, new HttpClient(handler));

    private static FallbackLlmTransport Fallback(ILlmTransport primary, ILlmTransport cli) =>
        new(primary, cli, NullLogger<FallbackLlmTransport>.Instance);

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Falls_back_to_cli_when_route33_fails(HttpStatusCode status)
    {
        var cli = new FakeTransport("from-cli");
        var transport = Fallback(Route33(new StubHandler(status, """{"type":"error","error":{"type":"x","message":"m"}}""")), cli);

        var response = await transport.SendAsync(LlmModel.Smart, "s", Turns, 4000, CancellationToken.None);

        Assert.Equal("from-cli", response.Text);
        Assert.Equal(1, cli.Calls);
    }

    [Fact]
    public async Task Falls_back_when_route33_key_is_missing()
    {
        var cli = new FakeTransport("from-cli");
        var transport = Fallback(Route33(new StubHandler(HttpStatusCode.OK, Route33Ok), key: null), cli);

        Assert.Equal("from-cli", (await transport.SendAsync(LlmModel.Cheap, "s", Turns, 100, CancellationToken.None)).Text);
    }

    [Fact]
    public async Task Does_not_call_cli_when_route33_answers()
    {
        var cli = new FakeTransport("from-cli");
        var transport = Fallback(Route33(new StubHandler(HttpStatusCode.OK, Route33Ok)), cli);

        var response = await transport.SendAsync(LlmModel.Smart, "s", Turns, 4000, CancellationToken.None);

        Assert.Equal("from-route33", response.Text);
        Assert.Equal(0, cli.Calls);
    }

    [Fact]
    public void Parses_cli_json_output_fixture()
    {
        var stdout = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Llm", "claude-cli-output.json"));

        var response = ClaudeCliLlmTransport.ParseCliOutput(stdout);

        Assert.Equal("""{"ok":true}""", response.Text);
        Assert.Equal(2 + 629 + 10, response.InputTokens);
        Assert.Equal(4, response.OutputTokens);
    }

    [Theory]
    [InlineData("""{"type":"result","is_error":true,"result":"Credit balance is too low"}""")]
    [InlineData("""{"type":"result","is_error":false}""")]
    [InlineData("not json")]
    public void Cli_error_or_garbage_becomes_llm_unavailable(string stdout)
    {
        Assert.Throws<LlmUnavailableException>(() => ClaudeCliLlmTransport.ParseCliOutput(stdout));
    }

    [Fact]
    public void Cli_arguments_disable_tools_and_pick_model_alias()
    {
        var args = ClaudeCliLlmTransport.Arguments(LlmModel.Cheap, "-x");

        Assert.Contains("--model=haiku", args);
        Assert.Contains("--tools=", args);
        Assert.Contains("--system-prompt=-x", args);
        Assert.Equal("sonnet", ClaudeCliLlmTransport.ModelAlias(LlmModel.Smart));
    }

    [Fact]
    public async Task Bridge_mode_posts_turns_with_bearer_token()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"text":"hi","inputTokens":5,"outputTokens":2}""");
        var transport = new ClaudeCliLlmTransport(
            new ClaudeCliOptions { Mode = ClaudeCliMode.Bridge, BridgeUrl = "http://host.docker.internal:8787/", Token = "tok" },
            new HttpClient(handler));

        var response = await transport.SendAsync(
            LlmModel.Smart, "sys", [new(ChatRole.User, "q"), new(ChatRole.Assistant, "a"), new(ChatRole.User, "q2")], 4000, CancellationToken.None);

        Assert.Equal(new LlmTransportResponse("hi", 5, 2), response);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://host.docker.internal:8787/complete", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("tok", handler.Request.Headers.Authorization?.Parameter);

        using var body = JsonDocument.Parse(handler.RequestBody!);
        Assert.Equal("sonnet", body.RootElement.GetProperty("model").GetString());
        Assert.Equal("sys", body.RootElement.GetProperty("system").GetString());
        Assert.Equal(4000, body.RootElement.GetProperty("maxTokens").GetInt32());
        var turns = body.RootElement.GetProperty("turns");
        Assert.Equal(3, turns.GetArrayLength());
        Assert.Equal("assistant", turns[1].GetProperty("role").GetString());
        Assert.Equal("q2", turns[2].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Bridge_error_status_becomes_llm_unavailable()
    {
        var transport = new ClaudeCliLlmTransport(
            new ClaudeCliOptions { Mode = ClaudeCliMode.Bridge, BridgeUrl = "http://bridge", Token = "tok" },
            new HttpClient(new StubHandler(HttpStatusCode.Unauthorized, """{"error":"unauthorized"}""")));

        await Assert.ThrowsAsync<LlmUnavailableException>(() =>
            transport.SendAsync(LlmModel.Cheap, "s", Turns, 100, CancellationToken.None));
    }

    [Fact]
    public async Task Bridge_without_token_is_unavailable_and_sends_nothing()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var transport = new ClaudeCliLlmTransport(
            new ClaudeCliOptions { Mode = ClaudeCliMode.Bridge, BridgeUrl = "http://bridge" }, new HttpClient(handler));

        await Assert.ThrowsAsync<LlmUnavailableException>(() =>
            transport.SendAsync(LlmModel.Cheap, "s", Turns, 100, CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }
}
