namespace InternshipCoach.Domain.Common;

/// <summary>Marks an entity that belongs to one user. The data layer filters and guards it by <see cref="UserId" />.</summary>
public interface IUserOwned
{
    Guid UserId { get; }
}
