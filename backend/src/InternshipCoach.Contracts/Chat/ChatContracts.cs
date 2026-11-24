namespace InternshipCoach.Contracts.Chat;

/// <summary>Role is "user" or "assistant".</summary>
public sealed record ChatMessageDto(string Role, string Content);

public sealed record ChatRequest(IReadOnlyList<ChatMessageDto>? Messages, string? Route);

/// <summary>Status is "ok" or "error". Args is a short JSON string of the arguments.</summary>
public sealed record ChatToolTraceDto(string Name, string Args, string Status, string Summary, int DurationMs);

/// <summary>An action the assistant proposed; it runs only when the client posts it to /api/chat/actions/confirm.</summary>
public sealed record ChatPendingActionDto(string Tool, IReadOnlyDictionary<string, string> Args, string Summary);

/// <summary>NavigateTo is a whitelisted app route or null.</summary>
public sealed record ChatResponse(
    string Reply, string? NavigateTo, IReadOnlyList<ChatToolTraceDto> ToolTrace, ChatPendingActionDto? PendingAction);

public sealed record ConfirmChatActionRequest(string? Tool, IReadOnlyDictionary<string, string>? Args);

/// <summary>NavigateTo is the page where the result shows, or null.</summary>
public sealed record ConfirmChatActionResponse(string Message, string? NavigateTo);
