namespace InternshipCoach.Application.Profile.Items;

public sealed class SetItemConfirmedHandler(IProfileRepository repository)
{
    /// <summary>Returns false when the item does not exist or belongs to someone else.</summary>
    public async Task<bool> HandleAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        var item = await repository.FindItemAsync(id, ct);
        if (item is null)
        {
            return false;
        }

        item.SetConfirmed(confirmed);
        await repository.SaveAsync(ct);
        return true;
    }
}

public sealed class ConfirmAllItemsHandler(IProfileRepository repository)
{
    /// <summary>Confirms every item that is not confirmed yet and returns how many changed.</summary>
    public async Task<int> HandleAsync(CancellationToken ct)
    {
        var pending = (await repository.ListItemsAsync(ct)).Where(item => !item.Confirmed).ToList();
        foreach (var item in pending)
        {
            item.SetConfirmed(true);
        }

        if (pending.Count > 0)
        {
            await repository.SaveAsync(ct);
        }

        return pending.Count;
    }
}

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
