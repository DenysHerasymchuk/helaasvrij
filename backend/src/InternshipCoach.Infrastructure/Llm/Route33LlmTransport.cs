using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Infrastructure.Llm;

// Official Anthropic C# SDK pointed at route33 (Anthropic-compatible, Bearer auth, base URL without /v1).
// No tools and no forced tool_choice (route33 rejects it); the JSON schema travels in the system prompt.
public sealed class Route33LlmTransport : ILlmTransport
{
    private readonly AnthropicClient? _client;

    public Route33LlmTransport(Route33Options options, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return; // Reported lazily so the app still starts without a key.
        }

        var baseUrl = options.BaseUrl.TrimEnd('/');
        _client = httpClient is null
            ? new AnthropicClient
            {
                ApiKey = null,
                AuthToken = options.ApiKey,
                BaseUrl = baseUrl,
                MaxRetries = 1,
                Timeout = TimeSpan.FromSeconds(120)
            }
            : new AnthropicClient
            {
                ApiKey = null,
                AuthToken = options.ApiKey,
                BaseUrl = baseUrl,
                MaxRetries = 0,
                HttpClient = httpClient
            };
    }

    public async Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        if (_client is null)
        {
            throw new LlmUnavailableException("The AI service is not configured (missing Route33 API key).");
        }

        var request = new MessageCreateParams
        {
            Model = LlmModels.Id(model),
            MaxTokens = maxTokens,
            System = system,
            Messages = turns
                .Select(turn => turn.Role == ChatRole.User
                    ? new MessageParam(new UserMessageParam { Content = turn.Content })
                    : new MessageParam(new AssistantMessageParam { Content = turn.Content }))
                .ToList()
        };

        try
        {
            var message = await _client.Messages.Create(request, cancellationToken: ct);
            var text = string.Concat(message.Content.Select(block => block.Value is TextBlock t ? t.Text : string.Empty));
            return new LlmTransportResponse(text, (int)message.Usage.InputTokens, (int)message.Usage.OutputTokens);
        }
        catch (Exception ex) when ((ex is AnthropicException or HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            // Message only: SDK exceptions can carry request details, never forward them.
            throw new LlmUnavailableException("The AI service call failed.", ex);
        }
    }
}
