namespace InternshipCoach.Application.Chat.Tools;

/// <summary>One argument of a tool. All arguments travel as strings; <paramref name="Values"/> lists the allowed words.</summary>
public sealed record ToolParam(string Name, string Description, bool Required = true, IReadOnlyList<string>? Values = null);

/// <summary>One tool in the catalog that chat and the MCP server both use.</summary>
public sealed record ToolSpec(string Name, string Description, IReadOnlyList<ToolParam> Params, bool IsAction = false)
{
    /// <summary>The arguments as the chat prompt shows them, for example {"postingId":"&lt;id&gt;"}.</summary>
    public string ArgsExample => "{" + string.Join(",", Params.Select(p =>
        $"\"{p.Name}\":\"{(p.Values is null ? $"<{p.Description}>" : string.Join("|", p.Values))}\"")) + "}";

    public static ToolSpec Read(string name, string description, params ToolParam[] parameters) => new(name, description, parameters);

    public static ToolSpec Action(string name, string description, params ToolParam[] parameters) => new(name, description, parameters, true);
}
