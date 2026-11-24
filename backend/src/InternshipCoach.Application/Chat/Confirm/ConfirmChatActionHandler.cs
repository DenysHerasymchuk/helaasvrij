using InternshipCoach.Application.Chat.Tools;

namespace InternshipCoach.Application.Chat.Confirm;

public sealed record ConfirmChatActionCommand(string? Tool, IReadOnlyDictionary<string, string>? Args);

/// <summary>
/// Runs an action the assistant proposed, only after the student clicked Confirm. The tool must be on the action
/// list, and the toolbox re-checks ownership (user-scoped repositories) and the arguments before anything changes.
/// </summary>
public sealed class ConfirmChatActionHandler(ChatToolbox tools)
{
    public Task<ChatActionResult> HandleAsync(ConfirmChatActionCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Tool) || !ChatToolbox.IsActionTool(command.Tool))
        {
            return Task.FromResult(new ChatActionResult(ChatActionStatus.Invalid, "This action is not allowed."));
        }

        var args = (command.Args ?? new Dictionary<string, string>())
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.Ordinal);
        return tools.ExecuteAsync(command.Tool, args, ct);
    }
}
