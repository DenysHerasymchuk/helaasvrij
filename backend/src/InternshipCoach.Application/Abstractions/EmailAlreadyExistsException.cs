namespace InternshipCoach.Application.Abstractions;

// Raised by the repository when a concurrent request wins the race on the unique email index.
public sealed class EmailAlreadyExistsException : Exception;
