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
