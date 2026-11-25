namespace InternshipCoach.Contracts.Profile;

public sealed record EvidenceResponse(string Quote, Guid SourceId, string SourceKind, string SourceLabel, string DocumentPath, string? Url);

public sealed record ProfileItemResponse(
    Guid Id, string Kind, string Name, string Quote, bool Confirmed, IReadOnlyList<EvidenceResponse> Evidence);

public sealed record ProfileResponse(IReadOnlyList<ProfileItemResponse> Items);

public sealed record ExtractResponse(int Created, int Merged, int Dropped, int Documents, int SkippedDocuments);
