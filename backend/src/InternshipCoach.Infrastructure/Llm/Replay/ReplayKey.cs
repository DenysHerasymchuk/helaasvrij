using System.Security.Cryptography;
using System.Text;
using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Infrastructure.Llm.Replay;

/// <summary>Stable key for one LLM call: SHA-256 over (model id, system, user). The key reveals nothing about the text.</summary>
public static class ReplayKey
{
    public static string Compute(string model, string system, string user)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var part in new[] { model, system, user })
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            hash.AppendData(BitConverter.GetBytes(bytes.Length)); // length prefix: ("ab","c") differs from ("a","bc")
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static string Compute(LlmModel model, string system, string user) => Compute(LlmModels.Id(model), system, user);

    /// <summary>Text calls carry several turns; they are flattened into one stable string.</summary>
    public static string Flatten(IReadOnlyList<ChatTurn> turns) =>
        string.Join('\n', turns.Select(t => $"{t.Role}:{t.Content}"));
}
