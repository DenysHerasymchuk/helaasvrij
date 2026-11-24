using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Chat.Tools;

namespace InternshipCoach.Application.Chat.Send;

public sealed record SendChatCommand(IReadOnlyList<ChatTurn> Messages, string? Route);

public enum SendChatStatus
{
    Ok,
    Invalid
}

/// <summary>One tool the assistant ran (or proposed) for this message. Args is a short JSON string.</summary>
public sealed record ToolTraceEntry(string Name, string Args, bool Ok, string Summary, int DurationMs);

public sealed record SendChatResult(
    SendChatStatus Status, string? Reply = null, string? NavigateTo = null, string? Error = null,
    IReadOnlyList<ToolTraceEntry>? ToolTrace = null, PendingChatAction? PendingAction = null);
