namespace InternshipCoach.Application.Profile.Items;

public sealed class DeleteItemHandler(IProfileRepository repository)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct);
        if (item is null)
        {
            return false;
        }

        await repository.RemoveItemAsync(item, ct);
        return true;
    }
}
