using System.Text.RegularExpressions;

namespace RhodesSuki.Services;

public static class RhodesMaaRecognitionPolicy
{
    private static readonly HashSet<string> AbandonedRunFields = new(StringComparer.Ordinal)
    {
        "hope",
        "maxHope",
        "lifePoints",
        "shield",
        "commandLevel",
    };

    private static readonly HashSet<string> AbandonedRunIdTokens = new(StringComparer.Ordinal)
    {
        "hope",
        "maxhope",
        "life",
        "lifepoints",
        "shield",
        "command",
        "commandlevel",
    };

    public static bool IsRetainedRecognitionSource(string? id, string? candidateField = "")
    {
        return !IsAbandonedRunRecognitionId(id)
            && !AbandonedRunFields.Contains(candidateField ?? "");
    }

    public static bool IsPublishableEntry(string? entry)
    {
        return !string.IsNullOrWhiteSpace(entry)
            && !entry.EndsWith("Empty", StringComparison.Ordinal)
            && !IsAbandonedRunEntry(entry);
    }

    public static bool IsAbandonedRunEntry(string? entry)
    {
        return IdTokens(entry).Any(AbandonedRunIdTokens.Contains);
    }

    private static bool IsAbandonedRunRecognitionId(string? id)
    {
        var tokens = IdTokens(id);
        return tokens.Count > 0
            && tokens[0].Equals("run", StringComparison.Ordinal)
            && tokens.Skip(1).Any(AbandonedRunIdTokens.Contains);
    }

    private static IReadOnlyList<string> IdTokens(string? value)
    {
        var dotted = Regex.Replace(value ?? "", "([a-z])([A-Z])", "$1.$2");
        return Regex.Split(dotted.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }
}
