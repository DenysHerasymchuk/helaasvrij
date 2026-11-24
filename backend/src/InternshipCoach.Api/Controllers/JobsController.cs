using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

// Shared Adzuna snapshot for all users: at most 3 Adzuna calls per day, searches filter the stored file.
// shortcut: JSON file on a volume instead of a DB table; move to a JobPosting table when postings (prompt 5) land.
[ApiController]
[Authorize]
[Route("api/jobs")]
public sealed class JobsController(IHttpClientFactory http, IConfiguration config) : ControllerBase
{
    private static readonly string[] Queries = ["stage", "internship", "stagiair"];
    private static readonly SemaphoreSlim Lock = new(1, 1);

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? what, [FromQuery] string? where, CancellationToken ct)
    {
        var jobs = await LoadAsync(ct);
        if (jobs is null) return Problem("Job search is not set up or Adzuna did not answer. Try again later.", statusCode: 503);

        var words = (what ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("stage" or "internship" or "stagiair" or "intern")).ToArray();
        var city = where?.Trim().ToLowerInvariant();
        return Ok(jobs
            .Where(j => words.All(w => $"{j.Title} {j.Description} {j.Company}".ToLowerInvariant().Contains(w)))
            .Where(j => string.IsNullOrEmpty(city) || (j.Location ?? "").ToLowerInvariant().Contains(city))
            .Take(50));
    }

    private async Task<List<JobResponse>?> LoadAsync(CancellationToken ct)
    {
        var path = config["Jobs:StorePath"] ?? "/home/app/jobs.json";
        await Lock.WaitAsync(ct);
        try
        {
            if (System.IO.File.Exists(path) && System.IO.File.GetLastWriteTimeUtc(path) > DateTime.UtcNow.AddDays(-1))
                return JsonSerializer.Deserialize<List<JobResponse>>(await System.IO.File.ReadAllTextAsync(path, ct));

            var fresh = await FetchAsync(ct);
            if (fresh is null)
                return System.IO.File.Exists(path) ? JsonSerializer.Deserialize<List<JobResponse>>(await System.IO.File.ReadAllTextAsync(path, ct)) : null;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await System.IO.File.WriteAllTextAsync(path, JsonSerializer.Serialize(fresh), ct);
            return fresh;
        }
        finally { Lock.Release(); }
    }

    private async Task<List<JobResponse>?> FetchAsync(CancellationToken ct)
    {
        var appId = config["Adzuna:AppId"] ?? config["ADZUNA_APP_ID"];
        var appKey = config["Adzuna:AppKey"] ?? config["ADZUNA_APP_KEY"];
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appKey)) return null;

        var all = new Dictionary<string, JobResponse>();
        foreach (var q in Queries)
        {
            var url = $"https://api.adzuna.com/v1/api/jobs/be/search/1?app_id={Uri.EscapeDataString(appId)}&app_key={Uri.EscapeDataString(appKey)}&results_per_page=50&what={q}";
            using var res = await http.CreateClient().GetAsync(url, ct);
            if (!res.IsSuccessStatusCode) return all.Count > 0 ? all.Values.ToList() : null;
            using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            foreach (var r in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                var id = r.GetProperty("id").GetString() ?? "";
                all[id] = new JobResponse(id, r.GetProperty("title").GetString() ?? "",
                    r.TryGetProperty("company", out var c) && c.TryGetProperty("display_name", out var cn) ? cn.GetString() : null,
                    r.TryGetProperty("location", out var l) && l.TryGetProperty("display_name", out var ln) ? ln.GetString() : null,
                    r.TryGetProperty("description", out var d) ? d.GetString() : null,
                    r.TryGetProperty("created", out var cr) ? cr.GetString() : null,
                    r.GetProperty("redirect_url").GetString() ?? "");
            }
        }
        return all.Values.OrderByDescending(j => j.Created).ToList();
    }
}

public sealed record JobResponse(string Id, string Title, string? Company, string? Location, string? Description, string? Created, string Url);
