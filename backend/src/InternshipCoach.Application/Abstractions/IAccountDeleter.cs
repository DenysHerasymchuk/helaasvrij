namespace InternshipCoach.Application.Abstractions;

public interface IAccountDeleter
{
    /// <summary>Removes every row owned by the user (all user-owned tables) and the user itself, in one transaction.</summary>
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken);
}
