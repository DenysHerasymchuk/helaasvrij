using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Infrastructure.Llm;

// Fallback transport: the user's own Claude Code CLI (`claude -p`, their subscription), run locally or via the host bridge.
// Never log the prompt, the reply or stderr: they can hold CV text.
public sealed class ClaudeCliLlmTransport(ClaudeCliOptions options, HttpClient? httpClient = null) : ILlmTransport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<HttpClient> DefaultHttp = new(() => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

    public static string ModelAlias(LlmModel model) => model == LlmModel.Smart ? "sonnet" : "haiku";

    /// <summary>Same flags as scripts/claude-bridge.mjs. `--opt=value` so a value starting with '-' is never read as a flag.</summary>
    public static IReadOnlyList<string> Arguments(LlmModel model, string system) =>
    [
        "-p", "--output-format=json", $"--model={ModelAlias(model)}", $"--system-prompt={system}",
        "--tools=", "--no-session-persistence", "--setting-sources=", "--strict-mcp-config"
    ];

    // shortcut: the CLI takes one prompt, so earlier turns are flattened into a transcript; switch to stream-json input if chat quality suffers.
    public static string Prompt(IReadOnlyList<ChatTurn> turns)
    {
        if (turns.Count == 1)
        {
            return turns[0].Content;
        }

        var sb = new StringBuilder("Conversation so far:\n\n");
        foreach (var turn in turns)
        {
            sb.Append(turn.Role == ChatRole.User ? "User: " : "Assistant: ").Append(turn.Content).Append("\n\n");
        }

        return sb.Append("Reply as the assistant to the last user message.").ToString();
    }

    public static LlmTransportResponse ParseCliOutput(string stdout)
    {
        try
        {
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            if (root.TryGetProperty("is_error", out var isError) && isError.ValueKind == JsonValueKind.True
                || !root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.String)
            {
                throw new LlmUnavailableException("The Claude CLI returned an error.");
            }

            int input = 0, output = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                input = Int(usage, "input_tokens") + Int(usage, "cache_creation_input_tokens") + Int(usage, "cache_read_input_tokens");
                output = Int(usage, "output_tokens");
            }

            return new LlmTransportResponse(result.GetString()!, input, output);
        }
        catch (JsonException ex)
        {
            throw new LlmUnavailableException("The Claude CLI returned unreadable output.", ex);
        }
    }

    private static int Int(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;

    public Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct) =>
        options.Mode == ClaudeCliMode.Bridge
            ? SendViaBridgeAsync(model, system, turns, maxTokens, ct)
            : RunLocalAsync(model, system, turns, maxTokens, ct);

    private async Task<LlmTransportResponse> SendViaBridgeAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.BridgeUrl) || string.IsNullOrWhiteSpace(options.Token))
        {
            throw new LlmUnavailableException("The Claude bridge is not configured (missing BridgeUrl or Token).");
        }

        var http = httpClient ?? DefaultHttp.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, options.BridgeUrl.TrimEnd('/') + "/complete")
        {
            Content = JsonContent.Create(new
            {
                model = ModelAlias(model),
                system,
                turns = turns.Select(t => new { role = t.Role == ChatRole.User ? "user" : "assistant", content = t.Content }),
                maxTokens
            }, options: Json)
        };
        request.Headers.Authorization = new("Bearer", options.Token);

        // The bridge runs one call at a time, so allow for waiting behind another call.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds * 2));
        try
        {
            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new LlmUnavailableException($"The Claude bridge failed ({(int)response.StatusCode}).");
            }

            var body = await response.Content.ReadFromJsonAsync<BridgeReply>(Json, timeout.Token);
            if (body?.Text is null)
            {
                throw new LlmUnavailableException("The Claude bridge returned no text.");
            }

            return new LlmTransportResponse(body.Text, body.InputTokens, body.OutputTokens);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or JsonException) && !ct.IsCancellationRequested)
        {
            throw new LlmUnavailableException("The Claude bridge could not be reached.", ex);
        }
    }

    private sealed record BridgeReply(string? Text, int InputTokens, int OutputTokens);

    private async Task<LlmTransportResponse> RunLocalAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        var start = new ProcessStartInfo(options.Command)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            // Neutral cwd so no project CLAUDE.md is pulled into the prompt.
            WorkingDirectory = Path.GetTempPath()
        };
        foreach (var arg in Arguments(model, system))
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["CLAUDE_CODE_MAX_OUTPUT_TOKENS"] = maxTokens.ToString();

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new LlmUnavailableException("The Claude CLI is not installed or not on PATH.", ex);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token); // drained so the pipe never blocks; never logged
            await process.StandardInput.WriteAsync(Prompt(turns).AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            await stderr;

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            {
                throw new LlmUnavailableException($"The Claude CLI exited with code {process.ExitCode}.");
            }

            return ParseCliOutput(output);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new LlmUnavailableException("The Claude CLI timed out.");
        }
        catch (IOException ex)
        {
            throw new LlmUnavailableException("The Claude CLI stopped unexpectedly.", ex);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
