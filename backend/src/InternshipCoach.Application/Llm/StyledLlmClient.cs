using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Writing;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Llm;

/// <summary>Adds the user's chosen writing skills to the system prompt of calls that write text for them (documents, interview).</summary>
public sealed class StyledLlmClient(LlmClient inner, ISettingsRepository settings) : ILlmClient
{
    public async Task<LlmResult<T>> CompleteJsonAsync<T>(
        LlmCallContext context, LlmModel model, string system, string user, string jsonSchema, int maxTokens, CancellationToken ct) =>
        await inner.CompleteJsonAsync<T>(context, model, await WithSkillsAsync(context, system, ct), user, jsonSchema, maxTokens, ct);

    public async Task<LlmText> CompleteTextAsync(
        LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct) =>
        await inner.CompleteTextAsync(context, model, await WithSkillsAsync(context, system, ct), turns, maxTokens, ct);

    private async Task<string> WithSkillsAsync(LlmCallContext context, string system, CancellationToken ct)
    {
        if (WritingSkills.AreaOf(context.Feature) is not { } area)
        {
            return system;
        }

        var saved = await settings.FindAsync(context.UserId, ct);
        var ids = area == WritingArea.Documents
            ? saved?.DocumentSkillIds ?? WritingSkills.DefaultDocuments
            : saved?.InterviewSkillIds ?? WritingSkills.DefaultInterview;
        var brief = WritingSkills.Brief(ids);
        return brief is null ? system : $"{system}\n\n{brief}";
    }
}
