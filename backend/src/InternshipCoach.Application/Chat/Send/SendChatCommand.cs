using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Application.Chat.Send;

public sealed record SendChatCommand(IReadOnlyList<ChatTurn> Messages, string? Route);

public enum SendChatStatus
{
    Ok,
    Invalid
}

public sealed record SendChatResult(SendChatStatus Status, string? Reply = null, string? NavigateTo = null, string? Error = null);
