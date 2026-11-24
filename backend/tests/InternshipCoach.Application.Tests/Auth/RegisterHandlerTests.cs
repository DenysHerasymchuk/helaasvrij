using InternshipCoach.Application.Auth;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Tests.TestDoubles;

namespace InternshipCoach.Application.Tests.Auth;

public class RegisterHandlerTests
{
    private readonly FakeUserRepository _users = new();
    private readonly RegisterHandler _handler;

    public RegisterHandlerTests()
    {
        _handler = new RegisterHandler(_users, new FakePasswordHasher(), new FakeClock(DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task Creates_user_with_hashed_password_and_normalised_email()
    {
        var result = await _handler.HandleAsync(new RegisterCommand("Sam", " Sam@Uni.be ", "password1"), default);

        Assert.True(result.Succeeded);
        var stored = Assert.Single(_users.Users);
        Assert.Equal("sam@uni.be", stored.Email.Value);
        Assert.Equal("hashed:password1", stored.PasswordHash);
    }

    [Fact]
    public async Task Rejects_duplicate_email_ignoring_case()
    {
        await _handler.HandleAsync(new RegisterCommand("Sam", "sam@uni.be", "password1"), default);

        var result = await _handler.HandleAsync(new RegisterCommand("Other", "SAM@uni.be", "password2"), default);

        Assert.Equal(AuthStatus.EmailTaken, result.Status);
        Assert.Single(_users.Users);
    }

    [Theory]
    [InlineData("", "a@b.com", "password1", "name")]
    [InlineData("Sam", "not-an-email", "password1", "email")]
    [InlineData("Sam", "a@b.com", "short", "password")]
    public async Task Returns_field_errors_for_invalid_input(string name, string email, string password, string field)
    {
        var result = await _handler.HandleAsync(new RegisterCommand(name, email, password), default);

        Assert.Equal(AuthStatus.Invalid, result.Status);
        Assert.Contains(field, result.Errors!.Keys);
        Assert.Empty(_users.Users);
    }
}
