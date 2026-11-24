using System.Text.Json;
using System.Text.RegularExpressions;
using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Evaluation;

/// <summary>
/// Fake LLM transport for offline runs: a keyword-overlap labeller, not a model. It reads the numbered requirements,
/// evidence and repos out of the prompt the real analyser builds and answers in the real JSON shape. Deterministic.
/// Its agreement number only proves the pipeline and gold set work; real accuracy needs R33_API_KEY.
/// </summary>
public sealed partial class OfflineTransport : ILlmTransport
{
    private static readonly HashSet<string> Stop = new(
        ["the", "and", "with", "for", "can", "has", "have", "van", "het", "een", "met", "voor", "der", "des", "les",
         "avec", "knowledge", "experience", "kennis", "ervaring", "good", "goede", "basic", "familiar", "works", "knows",
         "writes", "skills", "ability", "able", "strong", "notions", "maitrise", "connaissance", "experience"]);

    public List<string> RawReplies { get; } = [];

    public Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        var user = turns[^1].Content;
        var reply = user.Contains("name=\"gaps\"") ? ActionReply(user) : LabelReply(user);
        RawReplies.Add(reply);
        return Task.FromResult(new LlmTransportResponse(reply, 100, 50));
    }

    private static string LabelReply(string user)
    {
        var requirements = Block(user, "requirements");
        var quotes = Block(user, "evidence")
            .Select(l => QuoteLine().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
        var repos = Block(user, "repos");

        var rows = new List<object>();
        foreach (var line in requirements)
        {
            var m = ReqLine().Match(line);
            if (!m.Success)
            {
                continue;
            }

            var words = Words(m.Groups[2].Value);
            string? best = null;
            var bestHits = 0;
            foreach (var q in quotes)
            {
                var qWords = Words(q);
                var hits = words.Count(w => qWords.Contains(w));
                if (hits > bestHits)
                {
                    best = q;
                    bestHits = hits;
                }
            }

            var coverage = bestHits >= 2 || (words.Count > 0 && bestHits * 2 >= words.Count) ? "strong"
                : bestHits >= 1 ? "partial" : "none";
            var repo = repos.Count > 0 ? repos[(int.Parse(m.Groups[1].Value) - 1) % repos.Count] : null;
            rows.Add(new
            {
                req = int.Parse(m.Groups[1].Value),
                coverage,
                quote = coverage == "none" ? null : best,
                action = coverage == "strong" || repo is null ? null : $"Add a small feature to {repo} that shows this skill",
                project = coverage == "strong" ? null : repo
            });
        }

        return JsonSerializer.Serialize(new { rows });
    }

    private static string ActionReply(string user)
    {
        var repos = Block(user, "repos");
        var actions = Block(user, "gaps").Select((_, i) => new
        {
            req = i + 1,
            action = $"Add a small feature to {repos[0]} that closes this gap",
            project = repos[0]
        });
        return JsonSerializer.Serialize(new { actions });
    }

    private static List<string> Block(string user, string name)
    {
        var m = Regex.Match(user, $"<untrusted_data name=\"{name}\">\\n(.*?)\\n</untrusted_data>", RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value.Split('\n').ToList() : [];
    }

    private static HashSet<string> Words(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}#+.]+")
            .Select(m => m.Value.Trim('.'))
            .Where(w => w.Length >= 3 && !Stop.Contains(w))
            .Select(w => w.EndsWith('s') && w.Length > 3 ? w[..^1] : w)
            .ToHashSet();

    [GeneratedRegex(@"^(\d+)\. \[(?:required|nice)\] (.*)$")]
    private static partial Regex ReqLine();

    [GeneratedRegex("^\\d+\\. \\(.*?\\) \"(.*)\"$")]
    private static partial Regex QuoteLine();
}
