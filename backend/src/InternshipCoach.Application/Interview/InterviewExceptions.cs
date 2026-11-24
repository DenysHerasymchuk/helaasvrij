namespace InternshipCoach.Application.Interview;

/// <summary>The questions cannot be made or the answer cannot be rated yet (no analysis, no repos, too many tries). Maps to HTTP 409.</summary>
public sealed class InterviewInputException(string message) : Exception(message);

/// <summary>The model gave no usable question that names one of the student's repos. Maps to HTTP 502.</summary>
public sealed class InterviewGenerationException(string message) : Exception(message);
