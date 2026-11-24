using System.Net;
using System.Text;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Infrastructure.Llm;

namespace InternshipCoach.Infrastructure.Tests;

public class Route33LlmTransportTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private const string OkBody = """
        {"id":"msg_1","type":"message","role":"assistant","model":"anthropic/claude-sonnet-5-5",
         "content":[{"type":"text","text":"{\"a\":1}"}],"stop_reason":"end_turn","stop_sequence":null,
         "usage":{"input_tokens":12,"output_tokens":7}}
        """;

    private static Route33LlmTransport Create(StubHandler handler, string? key = "test-key") =>
        new(new Route33Options { ApiKey = key, BaseUrl = "https://gateway.test" }, new HttpClient(handler));

    [Fact]
    public async Task Sends_bearer_auth_to_base_url_and_reads_text_and_usage()
    {
        var handler = new StubHandler(HttpStatusCode.OK, OkBody);

        var response = await Create(handler).SendAsync(
            LlmModel.Smart, "system text", [new ChatTurn(ChatRole.User, "hello")], 4000, CancellationToken.None);

        Assert.Equal("""{"a":1}""", response.Text);
        Assert.Equal(12, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);
        Assert.Equal("https://gateway.test/v1/messages", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("test-key", handler.Request.Headers.Authorization?.Parameter);
        Assert.Contains("anthropic/claude-sonnet-5-5", handler.RequestBody);
        Assert.DoesNotContain("tool_choice", handler.RequestBody);
        Assert.DoesNotContain("\"tools\"", handler.RequestBody);
    }

    [Fact]
    public async Task Gateway_error_becomes_llm_unavailable_without_leaking_details()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"type":"error","error":{"type":"invalid_request_error","message":"secret detail"}}""");

        var ex = await Assert.ThrowsAsync<LlmUnavailableException>(() => Create(handler).SendAsync(
            LlmModel.Cheap, "s", [new ChatTurn(ChatRole.User, "u")], 100, CancellationToken.None));

        Assert.DoesNotContain("secret detail", ex.Message);
    }

    [Fact]
    public async Task Missing_key_is_reported_when_called_not_at_startup()
    {
        var transport = Create(new StubHandler(HttpStatusCode.OK, OkBody), key: null);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => transport.SendAsync(
            LlmModel.Cheap, "s", [new ChatTurn(ChatRole.User, "u")], 100, CancellationToken.None));
    }
}
