using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;

namespace InternshipCoach.Application.Applications;

public sealed class ListApplicationsHandler(IApplicationRepository applications)
{
    public Task<IReadOnlyList<ApplicationCard>> HandleAsync(CancellationToken ct) => applications.ListAsync(ct);
}

public enum ApplicationOutcome
{
    Ok,
    NotFound,
    Invalid
}

public sealed record ApplicationResult(ApplicationOutcome Outcome, ApplicationCard? Card = null, string? Error = null);

public sealed record CreateApplicationCommand(Guid PostingId);

public sealed class CreateApplicationHandler(
    IApplicationRepository applications, IPostingRepository postings, ICurrentUser currentUser, IClock clock)
{
    public async Task<ApplicationResult> HandleAsync(CreateApplicationCommand command, CancellationToken ct)
    {
        // The posting lookup is filtered by user, so another user's posting id reads as "not found".
        if (await postings.FindAsync(command.PostingId, ct) is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        // Idempotent: adding a posting that is already on the board returns its existing card.
        if (await applications.FindIdForPostingAsync(command.PostingId, ct) is { } existing)
        {
            return new ApplicationResult(ApplicationOutcome.Ok, await applications.GetCardAsync(existing, ct));
        }

        var application = JobApplication.Create(currentUser.UserId, command.PostingId, new CreatedAt(clock.UtcNow));
        await applications.AddAsync(application, ct);
        return new ApplicationResult(ApplicationOutcome.Ok, await applications.GetCardAsync(application.Id, ct));
    }
}

/// <param name="Stage">Null = leave the stage as it is.</param>
/// <param name="Notes">Null = leave the notes as they are.</param>
public sealed record UpdateApplicationCommand(Guid Id, string? Stage, string? Notes);

public sealed class UpdateApplicationHandler(IApplicationRepository applications, IClock clock)
{
    public async Task<ApplicationResult> HandleAsync(UpdateApplicationCommand command, CancellationToken ct)
    {
        var application = await applications.FindAsync(command.Id, ct);
        if (application is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        ApplicationStage? stage = null;
        if (command.Stage is not null)
        {
            if (!ApplicationStages.TryParse(command.Stage, out var parsed))
            {
                return new ApplicationResult(ApplicationOutcome.Invalid, Error: "Stage must be saved, applied, interview, offer or rejected.");
            }

            stage = parsed;
        }

        try
        {
            if (stage is { } value)
            {
                application.MoveTo(value, clock.UtcNow);
            }

            if (command.Notes is not null)
            {
                application.SetNotes(command.Notes);
            }
        }
        catch (DomainException ex)
        {
            return new ApplicationResult(ApplicationOutcome.Invalid, Error: ex.Message);
        }

        await applications.SaveAsync(ct);
        return new ApplicationResult(ApplicationOutcome.Ok, await applications.GetCardAsync(application.Id, ct));
    }
}

public sealed record ApplicationMonth(string Month, int Applied, int Interview, int Offer);

public sealed class GetApplicationStatsHandler(IApplicationRepository applications, IClock clock)
{
    public const int DefaultMonths = 6;
    public const int MaxMonths = 24;

    /// <summary>
    /// Oldest month first, empty months included. Applied counts applications by the month the student applied;
    /// interview and offer count applications now in that stage by the month they got there.
    /// </summary>
    public async Task<IReadOnlyList<ApplicationMonth>> HandleAsync(int? months, CancellationToken ct)
    {
        var count = Math.Clamp(months ?? DefaultMonths, 1, MaxMonths);
        var now = clock.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(count - 1));
        var cards = await applications.ListAsync(ct);

        var result = new List<ApplicationMonth>(count);
        for (var i = 0; i < count; i++)
        {
            var start = first.AddMonths(i);
            var end = start.AddMonths(1);
            bool In(DateTimeOffset? at) => at is { } value && value.UtcDateTime >= start && value.UtcDateTime < end;

            result.Add(new ApplicationMonth(
                start.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
                cards.Count(c => In(c.Application.AppliedAt)),
                cards.Count(c => c.Application.Stage == ApplicationStage.Interview && In(c.Application.StageChangedAt)),
                cards.Count(c => c.Application.Stage == ApplicationStage.Offer && In(c.Application.StageChangedAt))));
        }

        return result;
    }
}
