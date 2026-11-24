namespace InternshipCoach.Contracts.Profile;

public sealed record EvidenceResponse(string Quote, Guid SourceId, string SourceKind, string SourceLabel, string DocumentPath, string? Url);

public sealed record ProfileItemResponse(
    Guid Id, string Kind, string Name, string? Level, string Quote, bool Confirmed, IReadOnlyList<EvidenceResponse> Evidence);

public sealed record BasicsResponse(string? School, DateOnly? StageStart, DateOnly? StageEnd);

public sealed record ProfileResponse(BasicsResponse Basics, IReadOnlyList<ProfileItemResponse> Items);

public sealed record ExtractResponse(int Created, int Merged, int Dropped, int Documents, int SkippedDocuments);

public sealed record SetConfirmedRequest(bool Confirmed);

public sealed record SetLevelRequest(string? Level);

public sealed record UpdateBasicsRequest(string? School, DateOnly? StageStart, DateOnly? StageEnd);

public sealed record ConfirmAllResponse(int Confirmed);
