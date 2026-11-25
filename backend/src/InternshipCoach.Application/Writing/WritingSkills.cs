namespace InternshipCoach.Application.Writing;

/// <summary>A writing skill: a short brief added to the system prompt when the AI writes for the user.</summary>
public sealed record WritingSkill(string Id, string Name, string Description, string Instructions);

/// <summary>The places the AI writes for the user. Each has its own list of skills in the settings.</summary>
public enum WritingArea
{
    /// <summary>CV, tailored CV, cover letter, recruiter message.</summary>
    Documents,

    /// <summary>Interview questions and the feedback on answers.</summary>
    Interview
}

public static class WritingSkills
{
    public const int MaxPerArea = 4;

    public static readonly IReadOnlyList<WritingSkill> All =
    [
        new("humanizer", "Humanizer", "Reads like a person wrote it: plain words, no stock AI phrases.",
            "Write like a person, not a model. Use plain words, vary sentence length and prefer a specific detail to a big claim. " +
            "Avoid stock AI phrasing (delve, leverage, tapestry, \"not just X but Y\"), forced lists of three, filler openings and closings, and em dashes."),
        new("teacher", "Teacher", "Explains why, names the idea and gives a next step.",
            "Write as a patient teacher. Each question or piece of feedback should teach: say why it matters, name the idea behind it, " +
            "give one concrete next step and a short example. Build questions from simple to harder and keep an encouraging tone."),
        new("concise", "Concise", "Short sentences, nothing that does not carry a fact.",
            "Be brief. Cut every word that does not carry a fact, and prefer short sentences."),
        new("formal", "Formal", "A professional register, no slang or contractions.",
            "Use a formal, professional register. No slang, no contractions.")
    ];

    public static readonly IReadOnlyList<string> DefaultDocuments = ["humanizer"];
    public static readonly IReadOnlyList<string> DefaultInterview = ["humanizer", "teacher"];

    public static bool Exists(string id) => All.Any(s => s.Id == id);

    public static WritingArea? AreaOf(string feature) => feature switch
    {
        "cv-create" or "cv-tailor" or "cover-letter" or "recruiter-message" => WritingArea.Documents,
        "interview-questions" or "interview-feedback" => WritingArea.Interview,
        _ => null
    };

    /// <summary>The text added to a system prompt for these skills, or null for none.</summary>
    public static string? Brief(IEnumerable<string> ids)
    {
        var chosen = ids.Select(id => All.FirstOrDefault(s => s.Id == id)).OfType<WritingSkill>().ToList();
        if (chosen.Count == 0)
        {
            return null;
        }

        return "Writing skills. They only change how the text you write is worded. Never change facts, numbers, names, quoted evidence or the JSON structure.\n" +
               string.Join("\n", chosen.Select(s => $"- {s.Name}: {s.Instructions}"));
    }
}
