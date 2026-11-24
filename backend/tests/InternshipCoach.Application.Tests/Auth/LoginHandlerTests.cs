using InternshipCoach.Application.Auth;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Tests.TestDoubles;

namespace InternshipCoach.Application.Tests.Auth;

public class LoginHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly LoginHandler _handler;

    public LoginHandlerTests()
    {
        var hasher = new FakePasswordHasher();
        _handler = new LoginHandler(_users, hasher);
        new RegisterHandler(_users, hasher, new FakeClock(DateTimeOffset.UnixEpoch))
            .HandleAsync(new RegisterCommand("Sam", "sam@uni.be", "password1"), default).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Signs_in_with_correct_password_and_any_email_case()
    {
        var result = await _handler.HandleAsync(new LoginCommand("SAM@uni.be", "password1"), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Unknown_email_and_wrong_password_give_the_same_failure()
    {
        var unknown = await _handler.HandleAsync(new LoginCommand("nobody@uni.be", "password1"), default);
        var wrong = await _handler.HandleAsync(new LoginCommand("sam@uni.be", "wrong-password"), default);

        Assert.Equal(unknown, wrong);
        Assert.Equal(AuthStatus.InvalidCredentials, unknown.Status);
    }

    [Fact]
    public async Task Invalid_email_format_gives_the_same_failure()
    {
        var result = await _handler.HandleAsync(new LoginCommand("garbage", "password1"), default);

        Assert.Equal(AuthResult.InvalidCredentials(), result);
    }
}
