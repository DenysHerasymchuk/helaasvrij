namespace InternshipCoach.Application.Interview;

/// <summary>
/// The same rule as the gap analysis (<c>GapAnalyser.TryNameRepo</c>): the project must be one of the student's
/// repos and the text must mention it. Kept here so the interview feature does not depend on gap code that is still moving.
/// </summary>
public static class RepoNaming
{
    public static bool TryNameRepo(string? text, string? project, IReadOnlyList<string> repos, out string repo)
    {
        repo = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(project))
        {
            return false;
        }

        var match = repos.FirstOrDefault(r => string.Equals(r, project.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null || !text.Contains(match, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        repo = match;
        return true;
    }
}
