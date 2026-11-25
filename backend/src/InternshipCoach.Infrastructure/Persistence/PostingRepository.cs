using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Postings;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class PostingRepository(InternshipCoachDbContext db) : IPostingRepository
{
    public async Task AddAsync(Posting posting, CancellationToken cancellationToken)
    {
        // The user filter scopes this to the signed-in user's postings, so numbers run per user.
        posting.AssignNumber((await db.Postings.MaxAsync(p => (int?)p.Number, cancellationToken) ?? 0) + 1);
        db.Postings.Add(posting);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Posting>> ListAsync(PostingLanguage? language, string? query, CancellationToken cancellationToken)
    {
        var postings = db.Postings.Include(posting => posting.Requirements).AsQueryable();

        if (language is { } value)
        {
            postings = postings.Where(posting => posting.Language == value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var needle = query.Trim().ToLower();
            postings = postings.Where(posting =>
                posting.Company.ToLower().Contains(needle)
                || posting.Title.ToLower().Contains(needle)
                || posting.City.ToLower().Contains(needle));
        }

        return await postings.OrderByDescending(posting => posting.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Posting?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Postings.Include(posting => posting.Requirements).FirstOrDefaultAsync(posting => posting.Id == id, cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(Posting posting, CancellationToken cancellationToken)
    {
        db.Postings.Remove(posting);
        await db.SaveChangesAsync(cancellationToken);
    }
}
