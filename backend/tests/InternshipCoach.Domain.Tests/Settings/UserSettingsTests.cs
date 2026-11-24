using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Domain.Tests.Settings;

public class UserSettingsTests
{
    [Fact]
    public void Create_normalises_language()
    {
        var settings = UserSettings.Create(Guid.NewGuid(), " NL ", 80, true);

        Assert.Equal("nl", settings.DefaultLanguage);
        Assert.Equal(80, settings.StrongMatchThreshold);
        Assert.True(settings.EmailAlerts);
    }

    [Theory]
    [InlineData("de", 50)]
    [InlineData(null, 50)]
    [InlineData("en", -1)]
    [InlineData("en", 101)]
    public void Create_rejects_invalid_values(string? language, int threshold) =>
        Assert.Throws<DomainException>(() => UserSettings.Create(Guid.NewGuid(), language, threshold, false));

    [Fact]
    public void Create_rejects_empty_user() =>
        Assert.Throws<DomainException>(() => UserSettings.Create(Guid.Empty, "en", 75, false));

    [Fact]
    public void Validate_reports_each_bad_field()
    {
        var errors = UserSettings.Validate("xx", 200);

        Assert.Contains("defaultLanguage", errors.Keys);
        Assert.Contains("strongMatchThreshold", errors.Keys);
    }
}
