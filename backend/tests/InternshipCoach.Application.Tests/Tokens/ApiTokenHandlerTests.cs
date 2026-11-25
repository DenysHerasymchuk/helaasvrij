using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Application.Tokens;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Tokens;

namespace InternshipCoach.Application.Tests.Tokens;

public sealed class ApiTokenHandlerTests
{
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 25, 9, 0, 0, TimeSpan.Zero));
    private readonly FakeApiTokenRepository _repo;

    public ApiTokenHandlerTests() => _repo = new FakeApiTokenRepository(_user.UserId);

    private CreateApiTokenHandler Create() => new(_repo, _user, _clock);

    [Fact]
    public async Task Create_returns_the_secret_once_and_stores_only_its_hash()
    {
        var created = await Create().HandleAsync("MCP on my laptop", CancellationToken.None);

        Assert.StartsWith(ApiToken.Prefix, created.Secret);
        var stored = Assert.Single(_repo.Items);
        Assert.Equal(ApiToken.HashOf(created.Secret), stored.Hash);
        Assert.DoesNotContain(created.Secret, stored.Hash + stored.Hint + stored.Name);
        Assert.Equal(created.Secret[^4..], created.Token.Hint);
    }

    [Fact]
    public async Task Resolve_finds_the_owner_until_the_token_is_revoked()
    {
        var created = await Create().HandleAsync("tool", CancellationToken.None);
        var hash = ApiToken.HashOf(created.Secret);

        Assert.Equal(_user.UserId, await _repo.ResolveAsync(hash, _clock.UtcNow, CancellationToken.None));

        Assert.True(await new RevokeApiTokenHandler(_repo, _clock).HandleAsync(created.Token.Id, CancellationToken.None));
        Assert.Null(await _repo.ResolveAsync(hash, _clock.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task Revoke_of_another_users_token_is_not_found()
    {
        var other = ApiToken.Create(Guid.NewGuid(), "theirs", new CreatedAt(_clock.UtcNow), out _);
        _repo.Items.Add(other);

        Assert.False(await new RevokeApiTokenHandler(_repo, _clock).HandleAsync(other.Id, CancellationToken.None));
        Assert.True(other.IsActive);
    }

    [Fact]
    public async Task List_shows_only_my_tokens_and_never_the_secret()
    {
        await Create().HandleAsync("mine", CancellationToken.None);
        _repo.Items.Add(ApiToken.Create(Guid.NewGuid(), "theirs", new CreatedAt(_clock.UtcNow), out _));

        var list = await new ListApiTokensHandler(_repo).HandleAsync(CancellationToken.None);

        Assert.Equal(["mine"], list.Select(t => t.Name));
    }

    [Fact]
    public async Task Create_needs_a_name_and_stops_at_ten_active_tokens()
    {
        await Assert.ThrowsAsync<DomainException>(() => Create().HandleAsync(" ", CancellationToken.None));

        for (var i = 0; i < ApiToken.MaxActivePerUser; i++)
        {
            await Create().HandleAsync($"t{i}", CancellationToken.None);
        }

        await Assert.ThrowsAsync<DomainException>(() => Create().HandleAsync("one too many", CancellationToken.None));
    }
}
