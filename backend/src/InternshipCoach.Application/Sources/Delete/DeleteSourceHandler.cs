namespace InternshipCoach.Application.Sources.Delete;

public sealed class DeleteSourceHandler(ISourceRepository sources)
{
    /// <summary>Returns false when the source does not exist or belongs to someone else.</summary>
    public async Task<bool> HandleAsync(Guid id, CancellationToken ct)
    {
        var source = await sources.FindAsync(id, ct);
        if (source is null)
        {
            return false;
        }

        await sources.RemoveAsync(source, ct);
        return true;
    }
}
