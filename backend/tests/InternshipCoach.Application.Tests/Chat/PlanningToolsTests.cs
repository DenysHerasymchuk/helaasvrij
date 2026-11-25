using InternshipCoach.Application.Chat.Confirm;
using InternshipCoach.Application.Chat.Tools;
using InternshipCoach.Domain.Applications;

namespace InternshipCoach.Application.Tests.Chat;

public class PlanningToolsTests
{
    private static Dictionary<string, string> Args(params (string Key, object Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value.ToString()!);

    private static Task<ChatActionResult> Confirm(ChatFixture f, string tool, params (string Key, object Value)[] pairs) =>
        f.Confirm.HandleAsync(new ConfirmChatActionCommand(tool, Args(pairs)), default);

    private static async Task<ChatActionResult> StartSprint(ChatFixture f, string name = "Sprint 1") =>
        await Confirm(f, "start_sprint", ("name", name), ("goal", "Apply to five"), ("days", 7));

    [Fact]
    public async Task Catalog_names_are_unique_and_every_action_is_handled()
    {
        var all = ChatToolbox.ReadTools.Concat(ChatToolbox.ActionTools).Select(t => t.Name).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());

        var f = new ChatFixture();
        foreach (var action in ChatToolbox.ActionTools)
        {
            // Empty arguments must fail on the argument, never as "not allowed" (that would mean the tool has no handler).
            var result = await Confirm(f, action.Name);
            Assert.NotEqual("This action is not allowed.", result.Message);
        }
    }

    [Fact]
    public async Task Active_sprint_is_empty_until_one_is_started_then_lists_its_positions()
    {
        var f = new ChatFixture();
        var none = await f.Tools.RunReadAsync("get_active_sprint", Args(), default);
        Assert.Equal("no running sprint", none.Summary);

        var posting = f.AddPosting("Barco");
        Assert.Equal(ChatActionStatus.Ok, (await StartSprint(f)).Status);
        Assert.Equal(ChatActionStatus.Ok, (await Confirm(f, "add_to_sprint", ("postingId", posting.Id))).Status);

        var active = await f.Tools.RunReadAsync("get_active_sprint", Args(), default);
        Assert.Contains("Sprint 1", active.Summary);
        Assert.Contains("1 position", active.Summary);
    }

    [Fact]
    public async Task Proposing_changes_nothing_and_confirm_does()
    {
        var f = new ChatFixture();
        var pending = await f.Tools.ProposeAsync("start_sprint", Args(("name", "Sprint 1")), default);

        Assert.Contains("Sprint 1", pending.Summary);
        Assert.Empty(f.Sprints.Items);

        await Confirm(f, "start_sprint", ("name", "Sprint 1"));
        Assert.Single(f.Sprints.Items);
    }

    [Fact]
    public async Task A_second_sprint_is_refused_while_one_runs()
    {
        var f = new ChatFixture();
        await StartSprint(f);

        await Assert.ThrowsAsync<ChatToolException>(() => f.Tools.ProposeAsync("start_sprint", Args(("name", "Sprint 2")), default));
        var again = await StartSprint(f, "Sprint 2");
        Assert.Equal(ChatActionStatus.Invalid, again.Status);
        Assert.Single(f.Sprints.Items);
    }

    [Fact]
    public async Task Adding_to_a_sprint_needs_a_running_sprint_and_my_own_position()
    {
        var f = new ChatFixture();
        var posting = f.AddPosting("Barco");

        Assert.Equal(ChatActionStatus.Invalid, (await Confirm(f, "add_to_sprint", ("postingId", posting.Id))).Status);

        await StartSprint(f);
        var foreign = await Confirm(f, "add_to_sprint", ("postingId", Guid.NewGuid()));
        Assert.Equal(ChatActionStatus.NotFound, foreign.Status);
    }

    [Fact]
    public async Task Backlog_lists_positions_that_are_in_no_sprint()
    {
        var f = new ChatFixture();
        var inSprint = f.AddPosting("Barco");
        f.AddPosting("Axxes");
        await StartSprint(f);
        await Confirm(f, "add_to_sprint", ("postingId", inSprint.Id));

        var backlog = await f.Tools.RunReadAsync("list_backlog", Args(), default);

        Assert.Equal("1 position", backlog.Summary);
    }

    [Fact]
    public async Task Completing_a_sprint_sends_open_positions_back_to_the_backlog()
    {
        var f = new ChatFixture();
        var posting = f.AddPosting("Barco");
        await StartSprint(f);
        await Confirm(f, "add_to_sprint", ("postingId", posting.Id));

        var done = await Confirm(f, "complete_sprint");

        Assert.Equal(ChatActionStatus.Ok, done.Status);
        Assert.Contains("1 open positions went back", done.Message);
        Assert.Null(f.Applications.Items.Single().SprintId);
    }

    [Fact]
    public async Task Notes_need_a_card_and_are_replaced_on_confirm()
    {
        var f = new ChatFixture();
        var posting = f.AddPosting("Barco");

        await Assert.ThrowsAsync<ChatToolException>(() => f.Tools.ProposeAsync("set_notes", Args(("postingId", posting.Id)), default));
        Assert.Equal(ChatActionStatus.Invalid, (await Confirm(f, "set_notes", ("postingId", posting.Id), ("notes", "Call on Monday"))).Status);

        var application = f.AddToBoard(posting);
        var ok = await Confirm(f, "set_notes", ("postingId", posting.Id), ("notes", "Call on Monday"));

        Assert.Equal(ChatActionStatus.Ok, ok.Status);
        Assert.Equal("Call on Monday", application.Notes);
    }

    [Fact]
    public async Task Gap_tools_do_not_touch_another_users_requirement()
    {
        var f = new ChatFixture();
        f.AddPosting("Barco");

        var tick = await Confirm(f, "tick_gap", ("requirementId", Guid.NewGuid()));
        var untick = await Confirm(f, "untick_gap", ("requirementId", Guid.NewGuid()));

        Assert.Equal(ChatActionStatus.NotFound, tick.Status);
        Assert.Equal(ChatActionStatus.NotFound, untick.Status);
    }

    [Fact]
    public async Task Activity_shows_a_stage_move_newest_first()
    {
        var f = new ChatFixture();
        var posting = f.AddPosting("Barco");
        f.AddToBoard(posting);
        await Confirm(f, "set_application_stage", ("postingId", posting.Id), ("stage", "applied"));

        var activity = await f.Tools.RunReadAsync("get_activity", Args(("take", 5)), default);

        Assert.Equal("2 events", activity.Summary); // Added, then StageChanged
        Assert.Equal(ApplicationStage.Applied, f.Applications.Items.Single().Stage);
    }
}
