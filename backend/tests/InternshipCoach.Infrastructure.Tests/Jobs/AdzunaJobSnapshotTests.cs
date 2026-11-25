using System.Net;
using System.Text.Json;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;

namespace InternshipCoach.Infrastructure.Tests.Jobs;

// Never calls Adzuna: responses come from a saved fixture through a fake handler.
public class AdzunaJobSnapshotTests : IDisposable
{
    private static readonly string Fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Jobs", "adzuna-search.json"));
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"jobs-{Guid.NewGuid():N}.json");

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Fixture) });
        }
    }

    private (AdzunaJobSnapshot Snapshot, FixtureHandler Handler) Create()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jobs:StorePath"] = _path, ["Adzuna:AppId"] = "test-id", ["Adzuna:AppKey"] = "test-key"
        }).Build();
        var handler = new FixtureHandler();
        return (new AdzunaJobSnapshot(config, new Clock(), new HttpClient(handler)), handler);
    }

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Parse_maps_every_field_and_strips_adzuna_highlight_tags()
    {
        using var doc = JsonDocument.Parse(Fixture);
        var jobs = AdzunaJobSnapshot.Parse(doc.RootElement).ToList();

        var job = jobs[0];
        Assert.Equal("4931028811", job.Id);
        Assert.Equal("Stage .NET developer", job.Title);
        Assert.Equal("Acme Software NV", job.Company);
        Assert.Equal("Gent, Oost-Vlaanderen", job.Location);
        Assert.StartsWith("Voor onze afdeling in Gent zoeken we een stagiair met", job.Description);
        Assert.Contains("pluspunt & je", job.Description);
        Assert.Equal(1800m, job.SalaryMin);
        Assert.Equal(2400.5m, job.SalaryMax);
        Assert.True(job.SalaryIsPredicted);
        Assert.Equal("contract", job.ContractType);
        Assert.Equal("full_time", job.ContractTime);
        Assert.Equal("IT Jobs", job.Category);

        var bare = jobs[1];
        Assert.Null(bare.Company);
        Assert.Null(bare.SalaryMin);
        Assert.False(bare.SalaryIsPredicted);
        Assert.Null(bare.ContractType);
    }

    [Fact]
    public async Task A_fresh_snapshot_is_served_without_calling_adzuna()
    {
        var (seed, _) = Create();
        await seed.LoadAsync(default);

        var (snapshot, handler) = Create();
        var jobs = await snapshot.LoadAsync(default);

        Assert.Equal(2, jobs!.Count);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task An_old_format_snapshot_is_refetched_once_then_served_from_the_file()
    {
        // Version 1: a bare array without the salary and contract fields, written today.
        await File.WriteAllTextAsync(_path, """[{"Id":"1","Title":"Old","Url":"https://www.adzuna.be/land/ad/1"}]""");
        var (snapshot, handler) = Create();

        var first = await snapshot.LoadAsync(default);
        var second = await snapshot.LoadAsync(default);

        Assert.Equal(3, handler.Calls); // stage, internship, stagiair
        Assert.Equal("IT Jobs", first![0].Category);
        Assert.Equal(2, second!.Count);
        Assert.Equal(AdzunaJobSnapshot.SchemaVersion, AdzunaJobSnapshot.Read(await File.ReadAllTextAsync(_path)).Version);
    }
}
