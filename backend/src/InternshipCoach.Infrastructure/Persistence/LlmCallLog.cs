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

    public Task<int> CountSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct)
    {
        var sinceUtc = since.ToUniversalTime();
        return db.LlmCalls.CountAsync(call => call.UserId == userId && call.CreatedAt.Value >= sinceUtc, ct);
    }
}
