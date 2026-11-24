using InternshipCoach.Application.Account.Delete;
using InternshipCoach.Application.Settings.Get;
using InternshipCoach.Application.Settings.Update;
using InternshipCoach.Application.Tests.TestDoubles;

namespace InternshipCoach.Application.Tests.Settings;

public class SettingsHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Get_returns_defaults_when_nothing_saved()
    {
        var result = await new GetSettingsHandler(new FakeSettingsRepository(), new FakeCurrentUser(UserId))
            .HandleAsync(CancellationToken.None);

        Assert.Equal("en", result.DefaultLanguage);
        Assert.Equal(75, result.StrongMatchThreshold);
        Assert.False(result.EmailAlerts);
    }

    [Fact]
    public async Task Update_creates_then_updates_one_row()
    {
        var repository = new FakeSettingsRepository();
        var handler = new UpdateSettingsHandler(repository, new FakeCurrentUser(UserId));

        await handler.HandleAsync(new UpdateSettingsCommand("nl", 60, true), CancellationToken.None);
        var second = await handler.HandleAsync(new UpdateSettingsCommand("fr", 90, false), CancellationToken.None);

        var row = Assert.Single(repository.Rows);
        Assert.Equal(UserId, row.UserId);
        Assert.Equal("fr", second.DefaultLanguage);
        Assert.Equal(90, row.StrongMatchThreshold);
    }

    [Fact]
    public async Task Update_returns_errors_and_saves_nothing_when_invalid()
    {
        var repository = new FakeSettingsRepository();

        var result = await new UpdateSettingsHandler(repository, new FakeCurrentUser(UserId))
            .HandleAsync(new UpdateSettingsCommand("xx", 500, false), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("defaultLanguage", result.Errors!.Keys);
        Assert.Empty(repository.Rows);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("delete")]
    public async Task Delete_does_nothing_without_exact_confirmation(string? confirm)
    {
        var deleter = new FakeAccountDeleter();

        var deleted = await new DeleteAccountHandler(deleter, new FakeCurrentUser(UserId)).HandleAsync(confirm, CancellationToken.None);

        Assert.False(deleted);
        Assert.Empty(deleter.Deleted);
    }

    [Fact]
    public async Task Delete_removes_the_current_user_when_confirmed()
    {
        var deleter = new FakeAccountDeleter();

        var deleted = await new DeleteAccountHandler(deleter, new FakeCurrentUser(UserId)).HandleAsync("DELETE", CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal(UserId, Assert.Single(deleter.Deleted));
    }
}
