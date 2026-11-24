using System.Net;
using System.Text.RegularExpressions;

namespace InternshipCoach.Application.Postings.Fetching;

/// <summary>Turns an HTML page into readable plain text. Not a sanitizer: the result is only ever treated as data.</summary>
public static partial class HtmlText
{
    [GeneratedRegex(@"<script\b.*?</script\s*>|<style\b.*?</style\s*>|<noscript\b.*?</noscript\s*>|<svg\b.*?</svg\s*>|<head\b.*?</head\s*>|<template\b.*?</template\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<\s*(br|/p|/div|/li|/h[1-6]|/tr|/section|/article|p|li)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Breaks();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.NonBacktracking)]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+", RegexOptions.NonBacktracking)]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\s*\n\s*(\n\s*)*", RegexOptions.NonBacktracking)]
    private static partial Regex Lines();

    public static string ToText(string html)
    {
        var text = Hidden().Replace(html, " ");
        text = Comments().Replace(text, " ");
        text = Breaks().Replace(text, "\n");
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Spaces().Replace(text, " ");
        text = Lines().Replace(text, "\n");
        return text.Trim();
    }
}
