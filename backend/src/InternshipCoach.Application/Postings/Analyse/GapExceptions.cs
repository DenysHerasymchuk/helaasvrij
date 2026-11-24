namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>The posting cannot be analysed yet (no confirmed profile items). Maps to HTTP 409.</summary>
public sealed class GapInputException(string message) : Exception(message);
