using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Profile;

namespace InternshipCoach.Application.Profile.Basics;

public sealed record UpdateBasicsCommand(string? School, DateOnly? StageStart, DateOnly? StageEnd);

public sealed record BasicsResult(BasicsView? Basics, IReadOnlyDictionary<string, string>? Errors)
{
    public bool Succeeded => Errors is null;
}

public sealed class UpdateBasicsHandler(IProfileRepository repository, ICurrentUser currentUser)
{
    public async Task<BasicsResult> HandleAsync(UpdateBasicsCommand command, CancellationToken ct)
    {
        var errors = ProfileBasics.Validate(command.School, command.StageStart, command.StageEnd);
        if (errors.Count > 0)
        {
            return new BasicsResult(null, errors);
        }

        var existing = await repository.FindBasicsAsync(ct);
        if (existing is null)
        {
            existing = ProfileBasics.Create(currentUser.UserId, command.School, command.StageStart, command.StageEnd);
            await repository.AddBasicsAsync(existing, ct);
        }
        else
        {
            existing.Update(command.School, command.StageStart, command.StageEnd);
            await repository.SaveAsync(ct);
        }

        return new BasicsResult(BasicsView.From(existing), null);
    }
}
