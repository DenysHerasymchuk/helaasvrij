using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources.Text;

public sealed class AddTextHandler(ISourceRepository sources, ICurrentUser currentUser, IClock clock)
{
    public const int MaxLength = 20_000;

    public async Task<SourceSummary> HandleAsync(AddTextCommand command, CancellationToken ct)
    {
        var text = command.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new SourceInputException("Paste some text.");
        }

        if (text.Length > MaxLength)
        {
            throw new SourceInputException($"Text is too long (max {MaxLength:N0} characters).");
        }

        var label = string.IsNullOrWhiteSpace(command.Label) ? "Pasted text" : command.Label;
        var source = Source.Create(currentUser.UserId, SourceKind.PastedText, label);
        source.AddDocument("pasted-text", text);
        source.MarkReady(clock.UtcNow);
        await sources.AddAsync(source, ct);
        return SourceSummary.From(source);
    }
}
