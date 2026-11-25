using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Domain.Tests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("  Student@Example.COM ", "student@example.com")]
    [InlineData("a.b+tag@uni.be", "a.b+tag@uni.be")]
    public void Trims_and_lower_cases(string raw, string expected)
    {
        Assert.Equal(expected, Email.Create(raw).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("two@@example.com")]
    [InlineData("Name <a@b.com>")]
    public void Rejects_invalid_values(string? raw)
    {
        Assert.False(Email.TryCreate(raw, out _));
        Assert.Throws<DomainException>(() => Email.Create(raw));
    }

    [Fact]
    public void Equal_ignoring_case()
    {
        Assert.Equal(Email.Create("A@b.com"), Email.Create("a@B.com"));
    }
}
