namespace InternshipCoach.Application.Sources;

/// <summary>The caller sent something we refuse (bad file, bad URL, bad username). Maps to HTTP 400.</summary>
public sealed class SourceInputException(string message) : Exception(message);

/// <summary>An outside service (GitHub, a website) failed or refused. The message is safe to show to the user.</summary>
public sealed class SourceFetchException(string message) : Exception(message);
