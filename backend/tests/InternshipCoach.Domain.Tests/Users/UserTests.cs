using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Domain.Tests.Users;

public class UserTests
{
    private static readonly Email Mail = Email.Create("s@uni.be");
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void CreateWithPassword_sets_fields_and_trims_name()
    {
        var user = User.CreateWithPassword(Mail, "  Sam  ", "hash", Now);

        Assert.Equal("Sam", user.DisplayName);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Null(user.GitHubId);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithPassword_rejects_blank_name(string name)
    {
        Assert.Throws<DomainException>(() => User.CreateWithPassword(Mail, name, "hash", Now));
    }

    [Fact]
    public void CreateWithPassword_rejects_missing_hash()
    {
        Assert.Throws<DomainException>(() => User.CreateWithPassword(Mail, "Sam", " ", Now));
    }

    [Fact]
    public void CreateFromGitHub_has_no_password_and_links_github()
    {
        var user = User.CreateFromGitHub(Mail, "Sam", 42, "sam", "https://a/b.png", Now);

        Assert.Null(user.PasswordHash);
        Assert.Equal(42, user.GitHubId);
        Assert.Equal("sam", user.GitHubLogin);
    }

    [Theory]
    [InlineData(0, "sam")]
    [InlineData(-1, "sam")]
    [InlineData(5, " ")]
    public void LinkGitHub_rejects_invalid_input(long id, string login)
    {
        var user = User.CreateWithPassword(Mail, "Sam", "hash", Now);

        Assert.Throws<DomainException>(() => user.LinkGitHub(id, login, null));
    }

    [Fact]
    public void LinkGitHub_keeps_the_password()
    {
        var user = User.CreateWithPassword(Mail, "Sam", "hash", Now);

        user.LinkGitHub(7, "sam", null);

        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(7, user.GitHubId);
    }
}
