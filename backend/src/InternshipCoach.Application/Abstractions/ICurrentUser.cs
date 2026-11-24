namespace InternshipCoach.Application.Abstractions;

public interface ICurrentUser
{
    /// <summary>Id of the signed-in user. Throws <see cref="UnauthorizedAccessException" /> when nobody is signed in.</summary>
    Guid UserId { get; }
}
