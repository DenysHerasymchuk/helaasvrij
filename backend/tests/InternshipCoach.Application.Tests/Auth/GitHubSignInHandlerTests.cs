using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Tests.TestDoubles;

namespace InternshipCoach.Application.Tests.Auth;

public class GitHubSignInHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakeClock _clock = new(DateTimeOffset.UnixEpoch);
    private readonly GitHubSignInHandler _handler;

    public GitHubSignInHandlerTests()
    {
        _handler = new GitHubSignInHandler(_users, _clock);
    }

    private async Task RegisterAsync(string email) =>
        await new RegisterHandler(_users, new FakePasswordHasher(), _clock)
            .HandleAsync(new RegisterCommand("Sam", email, "password1"), default);

    [Fact]
    public async Task Links_to_existing_user_when_github_email_is_verified()
    {
        await RegisterAsync("sam@uni.be");

        var result = await _handler.HandleAsync(new GitHubSignInCommand(42, "sam", "Sam", "SAM@uni.be", null), default);

        Assert.True(result.Succeeded);
        var user = Assert.Single(_users.Users);
        Assert.Equal(42, user.GitHubId);
        Assert.Equal("hashed:password1", user.PasswordHash);
    }

    [Fact]
    public async Task Does_not_link_when_the_email_is_not_verified()
    {
        await RegisterAsync("sam@uni.be");

        var result = await _handler.HandleAsync(new GitHubSignInCommand(42, "attacker", null, null, null), default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, _users.Users.Count);
        Assert.Null(_users.Users[0].GitHubId);
        Assert.Equal(42, result.User!.GitHubId);
        Assert.NotEqual("sam@uni.be", result.User.Email.Value);
    }

    [Fact]
    public async Task Creates_a_new_user_from_a_verified_email()
    {
        var result = await _handler.HandleAsync(new GitHubSignInCommand(7, "lee", "Lee Ng", "lee@uni.be", "https://a/b.png"), default);

        Assert.Equal("lee@uni.be", result.User!.Email.Value);
        Assert.Equal("Lee Ng", result.User.DisplayName);
        Assert.Null(result.User.PasswordHash);
    }

    [Fact]
    public async Task Second_sign_in_returns_the_same_user()
    {
        var first = await _handler.HandleAsync(new GitHubSignInCommand(7, "lee", null, "lee@uni.be", null), default);
        var second = await _handler.HandleAsync(new GitHubSignInCommand(7, "lee", null, null, null), default);

        Assert.Equal(first.User!.Id, second.User!.Id);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Rejects_a_profile_without_id_or_login()
    {
        var result = await _handler.HandleAsync(new GitHubSignInCommand(0, "", null, null, null), default);

        Assert.False(result.Succeeded);
    }
}
