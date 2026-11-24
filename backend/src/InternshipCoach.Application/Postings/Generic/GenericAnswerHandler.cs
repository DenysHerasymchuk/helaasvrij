using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Generic;

/// <summary>
/// The "generic chatbot" answer shown next to the gap matrix. Generated once with the Cheap model from the posting
/// text only (no CV, profile or sources), then stored on the posting. Demo postings carry a stored answer from the seeder.
/// </summary>
public sealed class GenericAnswerHandler(IPostingRepository postings, ILlmClient llm, ICurrentUser currentUser, IClock clock)
{
    public const string Feature = "generic-answer";
    private const int MaxTokens = 4000;

    private const string System =
        "You are a general-purpose chatbot. A student pasted an internship posting in <untrusted_data name=\"posting\"> " +
        "and asks: \"What do I need to get this internship?\" Answer helpfully in English in at most 250 words, plain text, " +
        "short paragraphs or a simple list, no markdown headings.";

    public async Task<Posting?> HandleAsync(Guid postingId, CancellationToken ct)
    {
        var posting = await postings.FindAsync(postingId, ct);
        if (posting is null || posting.GenericAnswer is not null)
        {
            return posting;
        }

        var reply = await llm.CompleteTextAsync(
            new LlmCallContext(currentUser.UserId, Feature), LlmModel.Cheap, System,
            [new ChatTurn(ChatRole.User, UntrustedInput.Wrap("posting", posting.RawText))], MaxTokens, ct);

        posting.SetGenericAnswer(reply.Text, clock.UtcNow);
        await postings.SaveAsync(ct);
        return posting;
    }
}
