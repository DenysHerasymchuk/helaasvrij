namespace InternshipCoach.Contracts.Chat;

/// <summary>Role is "user" or "assistant".</summary>
public sealed record ChatMessageDto(string Role, string Content);

public sealed record ChatRequest(IReadOnlyList<ChatMessageDto>? Messages, string? Route);

/// <summary>NavigateTo is a whitelisted app route or null.</summary>
public sealed record ChatResponse(string Reply, string? NavigateTo);
