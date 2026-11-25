using System.Text;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Documents.TailorCv;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Profile;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Documents.CreateCv;

public sealed record CreateCvResult(Document? Document, int DroppedCount, string? Error)
{
    public const string NoConfirmedItems = "Read your sources on your profile first";

    public bool Succeeded => Document is not null;

    public static CreateCvResult Invalid(string message) => new(null, 0, message);
}

// Writes a first CV from the student's confirmed profile items only. Every bullet names the item it came from;
// bullets with an unknown item or a number that item does not contain are dropped and counted.
public sealed class CreateCvHandler(
    ILlmClient llm, IDocumentRepository documents, IProfileRepository profile, ISettingsRepository settings,
    IUserRepository users, ICurrentUser currentUser, IClock clock)
{
    public const string Feature = "cv-create";

    private const string System = """
        You write a first CV for a student from their confirmed profile items.
        Rules:
        - Use only facts stated in the items (name, quotes). Never add employers, schools, dates, grades, numbers, tools or skills that are not there.
        - Every bullet has "itemId": the id of the one item it is based on, copied exactly. One item can give one or two bullets.
        - Skill bullets are just the skill name. Language bullets are just the language name.
        - Project, experience and education bullets are one short line each.
        - "summary" is two or three sentences, built only from the items.
        """;

    private const string Schema = """
        {
          "type": "object",
          "required": ["summary", "bullets"],
          "properties": {
            "summary": { "type": "string" },
            "bullets": {
              "type": "array",
              "maxItems": 80,
              "items": {
                "type": "object",
                "required": ["itemId", "text"],
                "properties": { "itemId": { "type": "string" }, "text": { "type": "string" } }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> LanguageNames = new() { ["nl"] = "Dutch", ["fr"] = "French", ["en"] = "English" };
    private static readonly Dictionary<string, string> TitleNames = new() { ["nl"] = "Nederlands", ["fr"] = "Français", ["en"] = "English" };

    // Section order on the CV, with headings per language.
    private static readonly (ProfileItemKind Kind, Dictionary<string, string> Heading)[] Sections =
    [
        (ProfileItemKind.Skill, new() { ["nl"] = "Vaardigheden", ["fr"] = "Compétences", ["en"] = "Skills" }),
        (ProfileItemKind.Project, new() { ["nl"] = "Projecten", ["fr"] = "Projets", ["en"] = "Projects" }),
        (ProfileItemKind.Experience, new() { ["nl"] = "Ervaring", ["fr"] = "Expérience", ["en"] = "Experience" }),
        (ProfileItemKind.Education, new() { ["nl"] = "Opleiding", ["fr"] = "Formation", ["en"] = "Education" }),
        (ProfileItemKind.Language, new() { ["nl"] = "Talen", ["fr"] = "Langues", ["en"] = "Languages" })
    ];

    public async Task<CreateCvResult> HandleAsync(CreateCvCommand command, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        if (command.Language is not null && !Document.IsLanguage(command.Language))
        {
            return CreateCvResult.Invalid("Choose Dutch, French or English.");
        }

        // The repository already filters by user; the UserId check keeps another user's item out even if it did not.
        var items = (await profile.ListItemsAsync(ct)).Where(i => i.Confirmed && i.UserId == userId).ToDictionary(i => i.Id);
        if (items.Count == 0)
        {
            return CreateCvResult.Invalid(CreateCvResult.NoConfirmedItems);
        }

        var language = command.Language ?? (await settings.FindAsync(userId, ct))?.DefaultLanguage ?? UserSettings.DefaultLanguageCode;

        var itemsText = new StringBuilder();
        foreach (var item in items.Values)
        {
            itemsText.Append($"id: {item.Id}\nkind: {item.Kind}\nname: {item.Name}\n");
            itemsText.Append("quotes:\n");
            foreach (var evidence in item.Evidence)
            {
                itemsText.Append($"- {TailorCvHandler.Line(evidence.Quote)}\n");
            }

            itemsText.Append('\n');
        }

        var user = $"Write the CV in {LanguageNames[language]}.\n\n{UntrustedInput.Wrap("profile_items", itemsText.ToString())}";
        var reply = await llm.CompleteJsonAsync<DraftCv>(
            new LlmCallContext(userId, Feature), LlmModel.Smart, System, user, Schema, LlmModels.SmartMinMaxTokens, ct);

        var itemNumbers = items.ToDictionary(pair => pair.Key, pair => TailorCvHandler.Numbers(ItemText(pair.Value)));
        var dropped = 0;
        var kept = new List<(ProfileItem Item, string Text)>();
        foreach (var bullet in reply.Value.Bullets ?? [])
        {
            var text = TailorCvHandler.Line(bullet.Text);
            if (Guid.TryParse(bullet.ItemId, out var id) && items.TryGetValue(id, out var item) && text.Length > 0
                && TailorCvHandler.Numbers(text).IsSubsetOf(itemNumbers[id]))
            {
                kept.Add((item, text));
            }
            else
            {
                dropped++;
            }
        }

        if (kept.Count == 0)
        {
            return CreateCvResult.Invalid("No line of the result could be matched to your profile. Try again.");
        }

        var summary = TailorCvHandler.Line(reply.Value.Summary);
        var allowed = new HashSet<string>(itemNumbers.Values.SelectMany(n => n));
        if (summary.Length > 0 && !TailorCvHandler.Numbers(summary).IsSubsetOf(allowed))
        {
            summary = string.Empty;
            dropped++;
        }

        var name = TailorCvHandler.Line((await users.FindByIdAsync(userId, ct))?.DisplayName);
        var markdown = new StringBuilder($"# {(name.Length == 0 ? "CV" : name)}\n");
        if (summary.Length > 0)
        {
            markdown.Append($"\n{summary}\n");
        }

        foreach (var (kind, heading) in Sections)
        {
            var lines = kept.Where(k => k.Item.Kind == kind).Select(k => k.Text).Distinct().ToList();
            if (lines.Count == 0)
            {
                continue;
            }

            markdown.Append($"\n## {heading[language]}\n\n");
            if (kind == ProfileItemKind.Skill)
            {
                markdown.Append($"{string.Join(", ", lines)}\n");
            }
            else
            {
                lines.ForEach(line => markdown.Append($"- {line}\n"));
            }
        }

        var document = Document.Create(userId, DocumentKind.Cv, language, $"CV ({TitleNames[language]})", markdown.ToString(), new CreatedAt(clock.UtcNow));
        await documents.AddAsync(document, ct);
        return new CreateCvResult(document, dropped, null);
    }

    private static string ItemText(ProfileItem item) => $"{item.Name} {item.Quote} {string.Join(' ', item.Evidence.Select(e => e.Quote))}";

    private sealed record DraftCv(string? Summary, List<DraftBullet>? Bullets);

    private sealed record DraftBullet(string? ItemId, string? Text);
}
