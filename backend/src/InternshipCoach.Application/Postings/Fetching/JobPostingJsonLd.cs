using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace InternshipCoach.Application.Postings.Fetching;

public sealed record JobPostingData(string? Title, string? Company, string? City, DateOnly? Deadline, string Description);

/// <summary>Reads a schema.org JobPosting from the JSON-LD blocks of a page.</summary>
public static partial class JobPostingJsonLd
{
    [GeneratedRegex(@"<script\b[^>]*type\s*=\s*[""']?application/ld\+json[""']?[^>]*>(.*?)</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Blocks();

    public static JobPostingData? TryRead(string html)
    {
        foreach (Match block in Blocks().Matches(html))
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(WebUtility.HtmlDecode(block.Groups[1].Value).Trim());
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                var posting = Find(document.RootElement, 0);
                if (posting is { } found)
                {
                    var data = Read(found);
                    if (data is not null)
                    {
                        return data;
                    }
                }
            }
        }

        return null;
    }

    private static JsonElement? Find(JsonElement element, int depth)
    {
        if (depth > 4)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (Find(item, depth + 1) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (HasType(element, "JobPosting"))
        {
            return element;
        }

        return element.TryGetProperty("@graph", out var graph) ? Find(graph, depth + 1) : null;
    }

    private static bool HasType(JsonElement element, string type)
    {
        if (!element.TryGetProperty("@type", out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.Equals(value.GetString(), type, StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Any(v => v.ValueKind == JsonValueKind.String
                                                                   && string.Equals(v.GetString(), type, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static JobPostingData? Read(JsonElement posting)
    {
        var rawDescription = Text(posting, "description");
        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            return null;
        }

        // Descriptions are HTML, often entity-encoded: decode, then strip the tags.
        var description = HtmlText.ToText(WebUtility.HtmlDecode(rawDescription));
        if (description.Length == 0)
        {
            return null;
        }

        var company = posting.TryGetProperty("hiringOrganization", out var org)
            ? org.ValueKind == JsonValueKind.String ? org.GetString() : Text(org, "name")
            : null;

        return new JobPostingData(Text(posting, "title"), company, City(posting), Deadline(posting), description);
    }

    private static string? City(JsonElement posting)
    {
        if (!posting.TryGetProperty("jobLocation", out var location))
        {
            return null;
        }

        if (location.ValueKind == JsonValueKind.Array)
        {
            location = location.EnumerateArray().FirstOrDefault();
        }

        if (location.ValueKind != JsonValueKind.Object || !location.TryGetProperty("address", out var address))
        {
            return null;
        }

        return address.ValueKind == JsonValueKind.String ? address.GetString() : Text(address, "addressLocality");
    }

    private static DateOnly? Deadline(JsonElement posting)
    {
        var text = Text(posting, "validThrough");
        return text is { Length: >= 10 } && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", out var date) ? date : null;
    }

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
