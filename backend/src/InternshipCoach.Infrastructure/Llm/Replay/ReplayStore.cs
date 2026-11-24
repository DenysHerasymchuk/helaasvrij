using System.Text.Json;

namespace InternshipCoach.Infrastructure.Llm.Replay;

public sealed record ReplayEntry(string Kind, string Model, string Payload, int InputTokens, int OutputTokens);

/// <summary>JSON file of recorded responses keyed by <see cref="ReplayKey"/>. Loaded once, written atomically.</summary>
public sealed class ReplayStore
{
    private static readonly JsonSerializerOptions FileOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();
    private readonly Dictionary<string, ReplayEntry> _entries;

    public ReplayStore(string path)
    {
        _path = path;
        _entries = Load(path);
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    public bool TryGet(string key, out ReplayEntry entry)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(key, out entry!);
        }
    }

    public void Save(string key, ReplayEntry entry)
    {
        lock (_gate)
        {
            _entries[key] = entry;
            var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_entries, FileOptions));
            File.Move(temp, _path, overwrite: true);
        }
    }

    private static Dictionary<string, ReplayEntry> Load(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ReplayEntry>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException)
        {
            return []; // A broken recording behaves like an empty one; the live client answers instead.
        }
    }
}
