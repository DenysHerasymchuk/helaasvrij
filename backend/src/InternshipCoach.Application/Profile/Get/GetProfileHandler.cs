namespace InternshipCoach.Application.Profile.Get;

public sealed class GetProfileHandler(IProfileRepository repository)
{
    public async Task<ProfileView> HandleAsync(CancellationToken ct)
    {
        var basics = await repository.FindBasicsAsync(ct);
        var items = await repository.ListItemsAsync(ct);
        return new ProfileView(BasicsView.From(basics), await ProfileViewBuilder.BuildItemsAsync(repository, items, ct));
    }
}

/// <summary>Only the school and stage period, for checking postings against it.</summary>
public sealed class GetBasicsHandler(IProfileRepository repository)
{
    public async Task<BasicsView> HandleAsync(CancellationToken ct) => BasicsView.From(await repository.FindBasicsAsync(ct));
}
