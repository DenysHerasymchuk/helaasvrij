namespace InternshipCoach.Application.Profile.Get;

public sealed class GetProfileHandler(IProfileRepository repository)
{
    public async Task<ProfileView> HandleAsync(CancellationToken ct)
    {
        var items = await repository.ListItemsAsync(ct);
        return new ProfileView(await ProfileViewBuilder.BuildItemsAsync(repository, items, ct));
    }
}
