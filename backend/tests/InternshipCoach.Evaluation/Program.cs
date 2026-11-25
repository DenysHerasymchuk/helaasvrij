using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Common;
using InternshipCoach.Evaluation;
using InternshipCoach.Infrastructure.Llm;

// Gold-set evaluation. Offline by default (keyword fake transport); real route33 only when R33_API_KEY is set.
// Usage: dotnet run --project backend/tests/InternshipCoach.Evaluation [-- --runs 5 --cases path.json --offline]
var runs = 5;
var casesPath = Path.Combine(AppContext.BaseDirectory, "Data", "gold-set.json");
var forceOffline = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--runs": runs = int.Parse(args[++i]); break;
        case "--cases": casesPath = args[++i]; break;
        case "--offline": forceOffline = true; break;
    }
}

var apiKey = Environment.GetEnvironmentVariable("R33_API_KEY");
var live = !forceOffline && !string.IsNullOrWhiteSpace(apiKey);
var gold = GoldSet.Load(casesPath);
Console.WriteLine($"Mode: {(live ? "LIVE route33 (uses budget)" : "OFFLINE fake transport (keyword labeller, not a model)")}");
Console.WriteLine($"Cases: {gold.Cases.Count}, runs per case: {runs}\n");

var user = new FixedUser();
var clock = new FixedClock();
var totalReqs = 0;
var agree = 0;
var rawBadQuotes = 0;
var keptBadQuotes = 0;
var maxSpread = 0;

Console.WriteLine($"{"case",-24} {"agree",-8} {"expected",-9} {"scores over runs",-28} spread");
foreach (var c in gold.Cases)
{
    var persona = gold.Personas[c.Persona];
    var evidence = GoldSet.BuildEvidence(persona);
    var scores = new List<int>();
    var caseAgree = 0;

    for (var run = 0; run < runs; run++)
    {
        var offline = new OfflineTransport();
        ILlmTransport transport = live ? new Route33LlmTransport(new Route33Options { ApiKey = apiKey }) : offline;
        var llm = new LlmClient(transport, new MemoryLog(), clock, new LlmLimits(10_000));
        var analyser = new GapAnalyser(llm, user);

        var posting = Posting.Create(user.UserId, c.Company, c.Title, "Brussels", c.Language, null,
            string.Join('\n', c.Requirements.Select(r => r.Text)), null,
            c.Requirements.Select(r => (r.Text, r.Importance)), isDemo: true, new CreatedAt(clock.UtcNow));

        var verdicts = await analyser.AnalyseAsync(posting.Requirements, c.Language, evidence, CancellationToken.None);
        for (var i = 0; i < posting.Requirements.Count; i++)
        {
            var v = verdicts[i];
            posting.Requirements[i].RecordAnalysis(v.Coverage, v.Quote, v.DocumentId, v.Action, v.Project, null, clock.UtcNow);
            totalReqs++;
            if (v.Coverage == c.Requirements[i].Expected)
            {
                agree++;
                caseAgree++;
            }

            // Quotes kept by code must be verbatim in their document (always true by construction; checked anyway).
            if (v.Coverage != Coverage.None && !(v.DocumentId is { } d && evidence.DocumentTexts.TryGetValue(d, out var t) && QuoteCheck.IsVerbatim(v.Quote, t)))
            {
                keptBadQuotes++;
            }
        }

        scores.Add(posting.Score());
        rawBadQuotes += offline.RawReplies.Count == 0 ? 0 : CountBadRawQuotes(offline.RawReplies, evidence);
    }

    var spread = scores.Max() - scores.Min();
    maxSpread = Math.Max(maxSpread, spread);
    var expectedPosting = Posting.Create(user.UserId, c.Company, c.Title, null, c.Language, null, "x", null,
        c.Requirements.Select(r => (r.Text, r.Importance)), true, new CreatedAt(clock.UtcNow));
    for (var i = 0; i < c.Requirements.Count; i++)
    {
        var e = c.Requirements[i].Expected;
        expectedPosting.Requirements[i].RecordAnalysis(e, e == Coverage.None ? null : "q", e == Coverage.None ? null : Guid.NewGuid(), null, null, null, clock.UtcNow);
    }

    Console.WriteLine($"{c.Id,-24} {caseAgree * 100 / (runs * c.Requirements.Count),3}%     {expectedPosting.Score(),3}%      {string.Join(", ", scores),-28} {spread}");
}

Console.WriteLine();
Console.WriteLine($"Agreement with gold labels: {agree}/{totalReqs} ({(totalReqs == 0 ? 0 : agree * 100 / totalReqs)}%)");
Console.WriteLine($"Quote-check failures in raw model replies (before code filtering): {rawBadQuotes}{(live ? " (not measured live)" : "")}");
Console.WriteLine($"Quote-check failures in kept evidence (target 0): {keptBadQuotes}");
Console.WriteLine($"Largest score spread over {runs} runs: {maxSpread} points{(live ? "" : " (offline fake is deterministic, so 0 only proves the code path)")}");
return keptBadQuotes == 0 ? 0 : 1;

static int CountBadRawQuotes(List<string> replies, GapEvidence evidence)
{
    var text = string.Join(' ', evidence.DocumentTexts.Values);
    var bad = 0;
    foreach (var reply in replies)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(reply);
        if (!doc.RootElement.TryGetProperty("rows", out var rows))
        {
            continue;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (row.TryGetProperty("quote", out var q) && q.ValueKind == System.Text.Json.JsonValueKind.String
                && !QuoteCheck.IsVerbatim(q.GetString(), text))
            {
                bad++;
            }
        }
    }

    return bad;
}

internal sealed class FixedUser : ICurrentUser
{
    public Guid UserId { get; } = Guid.Parse("00000000-0000-0000-0000-00000000e7a1");
}

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; } = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);
}

internal sealed class MemoryLog : ILlmCallLog
{
    public Task AddAsync(LlmCallRecord record, CancellationToken ct) => Task.CompletedTask;

    public Task<int> CountSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct) => Task.FromResult(0);
}
