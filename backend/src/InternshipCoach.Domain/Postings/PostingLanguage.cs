namespace InternshipCoach.Domain.Postings;

public enum PostingLanguage
{
    Nl,
    Fr,
    En
}

public static class PostingLanguages
{
    public static bool TryParse(string? value, out PostingLanguage language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "nl":
                language = PostingLanguage.Nl;
                return true;
            case "fr":
                language = PostingLanguage.Fr;
                return true;
            case "en":
                language = PostingLanguage.En;
                return true;
            default:
                language = default;
                return false;
        }
    }

    public static string ToCode(this PostingLanguage language) => language.ToString().ToLowerInvariant();
}
