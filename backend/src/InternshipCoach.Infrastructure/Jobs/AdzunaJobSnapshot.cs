using System.Globalization;
using System.Text.Json;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Fetching;
using Microsoft.Extensions.Configuration;

namespace InternshipCoach.Infrastructure.Jobs;

// Shared Adzuna snapshot for all users: at most 3 Adzuna calls per day, searches filter the stored file.
// shortcut: JSON file on a volume instead of a DB table; move to a table if the list must be queried in SQL.
public sealed class AdzunaJobSnapshot(IConfiguration config, IClock clock, HttpClient? http = null) : IJobSnapshot
{
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

    // Bump when Job gains fields: an older file is refetched once, then rewritten with this version.
    public const int SchemaVersion = 2;

    private static readonly string[] Queries = ["stage", "internship", "stagiair"];
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private DateTimeOffset _lastFailedFetch = DateTimeOffset.MinValue;

    private sealed record Stored(int SchemaVersion, List<Job> Jobs);

    public async Task<IReadOnlyList<Job>?> LoadAsync(CancellationToken ct)
    {
        var path = config["Jobs:StorePath"] ?? "/home/app/jobs.json";
        await Lock.WaitAsync(ct);
        try
        {
            var (jobs, version) = File.Exists(path) ? Read(await File.ReadAllTextAsync(path, ct)) : (null, 0);
            var now = clock.UtcNow;
            var fresh = jobs is not null && version == SchemaVersion && File.GetLastWriteTimeUtc(path) > now.UtcDateTime.AddDays(-1);
            // A failed fetch is not retried for an hour, so a broken key or an outage cannot burn the daily quota.
            if (fresh || now - _lastFailedFetch < TimeSpan.FromHours(1)) return jobs;

            var fetched = await FetchAsync(ct);
            if (fetched is null)
            {
                _lastFailedFetch = now;
                return jobs;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Stored(SchemaVersion, fetched)), ct);
            return fetched;
        }
        finally { Lock.Release(); }
    }

    /// <summary>Reads the stored file. The first format was a bare array (version 1, without salary or contract fields).</summary>
    public static (List<Job>? Jobs, int Version) Read(string json)
    {
        try
        {
            if (json.TrimStart().StartsWith('[')) return (JsonSerializer.Deserialize<List<Job>>(json), 1);
            var stored = JsonSerializer.Deserialize<Stored>(json);
            return (stored?.Jobs, stored?.SchemaVersion ?? 0);
        }
        catch (JsonException)
        {
            return (null, 0);
        }
    }

    /// <summary>Maps one Adzuna search response. Adzuna wraps matched words in &lt;strong&gt;, so text fields are stripped.</summary>
    public static IEnumerable<Job> Parse(JsonElement root)
    {
        foreach (var r in root.GetProperty("results").EnumerateArray())
        {
            yield return new Job(
                Str(r, "id") ?? "",
                Text(Str(r, "title")) ?? "",
                Text(Nested(r, "company", "display_name")),
                Text(Nested(r, "location", "display_name")),
                Text(Str(r, "description")),
                Str(r, "created"),
                Str(r, "redirect_url") ?? "",
                Num(r, "salary_min"),
                Num(r, "salary_max"),
                Str(r, "salary_is_predicted") == "1",
                Str(r, "contract_type"),
                Str(r, "contract_time"),
                Text(Nested(r, "category", "label")));
        }
    }

    private async Task<List<Job>?> FetchAsync(CancellationToken ct)
    {
        var appId = config["Adzuna:AppId"] ?? config["ADZUNA_APP_ID"];
        var appKey = config["Adzuna:AppKey"] ?? config["ADZUNA_APP_KEY"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appKey)) return null;

        var all = new Dictionary<string, Job>();
        foreach (var q in Queries)
        {
            var url = $"https://api.adzuna.com/v1/api/jobs/be/search/1?app_id={Uri.EscapeDataString(appId)}&app_key={Uri.EscapeDataString(appKey)}&results_per_page=50&what={q}";
            try
            {
                using var res = await _http.GetAsync(url, ct);
                if (!res.IsSuccessStatusCode) break;
                using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                foreach (var job in Parse(doc.RootElement).Where(j => j.Id.Length > 0)) all[job.Id] = job;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                break; // keep what earlier queries returned; never log the url (it holds the key)
            }
        }
        return all.Count > 0 ? all.Values.OrderByDescending(j => j.Created).ToList() : null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null
        } : null;

    private static string? Nested(JsonElement e, string outer, string inner) =>
        e.TryGetProperty(outer, out var o) && o.ValueKind == JsonValueKind.Object ? Str(o, inner) : null;

    private static decimal? Num(JsonElement e, string name) =>
        decimal.TryParse(Str(e, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : HtmlText.ToText(value);
}
