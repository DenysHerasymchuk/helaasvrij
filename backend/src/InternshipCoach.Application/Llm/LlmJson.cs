using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;

namespace InternshipCoach.Application.Llm;

public static class LlmJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    /// <summary>Removes a surrounding markdown code fence and any text outside the outermost JSON value.</summary>
    public static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var closing = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closing >= 0)
            {
                trimmed = trimmed[..closing];
            }

            trimmed = trimmed.Trim();
        }

        var start = trimmed.IndexOfAny(['{', '[']);
        if (start > 0)
        {
            var end = trimmed.LastIndexOf(trimmed[start] == '{' ? '}' : ']');
            if (end > start)
            {
                trimmed = trimmed[start..(end + 1)];
            }
        }

        return trimmed;
    }

    /// <summary>Returns null when valid, otherwise a short description of the first problems.</summary>
    public static string? Validate(string json, string jsonSchema)
    {
        JsonElement instance;
        try
        {
            instance = JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return $"Not valid JSON: {ex.Message}";
        }

        var schema = JsonSchema.FromText(jsonSchema);
        var result = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List });

        if (result.IsValid)
        {
            return null;
        }

        var errors = (result.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Take(5)
            .ToList();

        return errors.Count > 0 ? string.Join("; ", errors) : "Does not match the JSON schema.";
    }
}
