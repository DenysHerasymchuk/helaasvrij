using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class LlmCallLog(InternshipCoachDbContext db) : ILlmCallLog
{
    public async Task AddAsync(LlmCallRecord record, CancellationToken ct)
    {
        db.LlmCalls.Add(LlmCall.Create(
            record.UserId, record.Feature, record.Model, record.InputTokens, record.OutputTokens,
            record.DurationMs, record.Success, new CreatedAt(record.CreatedAt)));
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CountSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct)
    {
        var sinceUtc = since.ToUniversalTime();
        // CreatedAt is a converted value object, so Postgres cannot translate `.Value`; filter the dates in memory.
        // shortcut: loads all of a user's call dates, fine at hackathon scale; move to a raw DateTimeOffset column if logs grow.
        var dates = await db.LlmCalls.Where(call => call.UserId == userId).Select(call => call.CreatedAt).ToListAsync(ct);
        return dates.Count(createdAt => createdAt.Value >= sinceUtc);
    }
}
