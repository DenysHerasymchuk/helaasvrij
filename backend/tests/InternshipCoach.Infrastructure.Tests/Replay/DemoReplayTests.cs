using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Infrastructure.Llm.Replay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InternshipCoach.Infrastructure.Tests.Replay;

public sealed class DemoReplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "replay-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "nested", "replay.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    private sealed record Pair(string Name, int N);

    private sealed class CountingClient : ILlmClient
    {
        public int Calls { get; private set; }

        public Task<LlmResult<T>> CompleteJsonAsync<T>(
            LlmCallContext context, LlmModel model, string system, string user, string jsonSchema, int maxTokens, CancellationToken ct)
        {
            Calls++;
            var value = (T)(object)new Pair("live", Calls);
            return Task.FromResult(new LlmResult<T>(value, 7, 3, 1));
        }

        public Task<LlmText> CompleteTextAsync(
            LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new LlmText($"reply {Calls}", 5, 2));
        }
    }

    private static readonly LlmCallContext Ctx = new(Guid.NewGuid(), "test");

    [Fact]
    public void Key_is_stable_and_depends_on_every_part()
    {
        var key = ReplayKey.Compute("m", "sys", "user");
        Assert.Equal(key, ReplayKey.Compute("m", "sys", "user"));
        Assert.Equal(64, key.Length);
        Assert.NotEqual(key, ReplayKey.Compute("m2", "sys", "user"));
        Assert.NotEqual(key, ReplayKey.Compute("m", "sys2", "user"));
        Assert.NotEqual(key, ReplayKey.Compute("m", "sys", "user2"));
        Assert.NotEqual(ReplayKey.Compute("m", "ab", "c"), ReplayKey.Compute("m", "a", "bc"));
        Assert.DoesNotContain("user", key);
    }

    [Fact]
    public void Key_uses_the_model_id()
    {
        Assert.Equal(ReplayKey.Compute(LlmModels.SmartId, "s", "u"), ReplayKey.Compute(LlmModel.Smart, "s", "u"));
        Assert.NotEqual(ReplayKey.Compute(LlmModel.Smart, "s", "u"), ReplayKey.Compute(LlmModel.Cheap, "s", "u"));
    }

    [Fact]
    public async Task Recorded_json_call_replays_without_calling_the_inner_client()
    {
        var inner = new CountingClient();
        var options = new DemoReplayOptions { Replay = true, Record = true, Path = FilePath };
        var recorder = new ReplayLlmClient(inner, new ReplayStore(FilePath), options);

        var first = await recorder.CompleteJsonAsync<Pair>(Ctx, LlmModel.Smart, "sys", "user", "{}", 100, default);
        Assert.Equal(1, inner.Calls);
        Assert.True(File.Exists(FilePath));

        // A fresh store and client read the file, as after an app restart. Recording is off now.
        var offline = new CountingClient();
        var replayer = new ReplayLlmClient(offline, new ReplayStore(FilePath), new DemoReplayOptions { Replay = true, Path = FilePath });
        var second = await replayer.CompleteJsonAsync<Pair>(Ctx, LlmModel.Smart, "sys", "user", "{}", 100, default);

        Assert.Equal(0, offline.Calls);
        Assert.Equal(first.Value, second.Value);
        Assert.Equal(7, second.InputTokens);
    }

    [Fact]
    public async Task Recorded_text_call_replays_and_different_input_misses()
    {
        var inner = new CountingClient();
        var options = new DemoReplayOptions { Replay = true, Record = true, Path = FilePath };
        var client = new ReplayLlmClient(inner, new ReplayStore(FilePath), options);
        IReadOnlyList<ChatTurn> turns = [new ChatTurn(ChatRole.User, "hello")];

        var first = await client.CompleteTextAsync(Ctx, LlmModel.Smart, "sys", turns, 100, default);
        var again = await client.CompleteTextAsync(Ctx, LlmModel.Smart, "sys", turns, 100, default);
        Assert.Equal(1, inner.Calls);
        Assert.Equal(first.Text, again.Text);

        await client.CompleteTextAsync(Ctx, LlmModel.Smart, "sys", [new ChatTurn(ChatRole.User, "other")], 100, default);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Miss_without_recording_goes_live_and_writes_nothing()
    {
        var inner = new CountingClient();
        var client = new ReplayLlmClient(inner, new ReplayStore(FilePath), new DemoReplayOptions { Replay = true, Path = FilePath });

        await client.CompleteJsonAsync<Pair>(Ctx, LlmModel.Cheap, "sys", "user", "{}", 100, default);

        Assert.Equal(1, inner.Calls);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void Recording_file_does_not_contain_the_prompt_text()
    {
        var store = new ReplayStore(FilePath);
        var key = ReplayKey.Compute("m", "system text", "SECRET CV TEXT");
        store.Save(key, new ReplayEntry("text", "m", "answer", 1, 1));

        var file = File.ReadAllText(FilePath);
        Assert.DoesNotContain("SECRET CV TEXT", file);
        Assert.Contains(key, file);
    }

    private static IConfiguration Config(params (string, string?)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Item1, v.Item2))).Build();

    [Fact]
    public void Flag_off_leaves_the_registration_alone()
    {
        var services = new ServiceCollection();
        services.AddScoped<ILlmClient, LlmClient>();

        services.AddDemoReplay(Config());

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ILlmClient));
        Assert.Equal(typeof(LlmClient), descriptor.ImplementationType);
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ReplayStore));
    }

    [Fact]
    public void Flag_on_wraps_the_client()
    {
        var services = new ServiceCollection();
        services.AddScoped<ILlmClient, LlmClient>();

        services.AddDemoReplay(Config(("Demo:Replay", "true"), ("Demo:Path", FilePath)));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ILlmClient));
        Assert.NotNull(descriptor.ImplementationFactory);
        Assert.Contains(services, d => d.ServiceType == typeof(LlmClient));
        Assert.Contains(services, d => d.ServiceType == typeof(ReplayStore));
    }
}
