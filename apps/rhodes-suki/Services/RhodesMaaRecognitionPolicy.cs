using System.Text.RegularExpressions;

namespace RhodesSuki.Services;

public static class RhodesMaaRecognitionPolicy
{
    private static readonly HashSet<string> RetainedRunRecognitionIds = new(StringComparer.Ordinal)
    {
        "run.squad.info.panel",
        "run.status.idea.icon",
        "run.status.ingot.icon",
        "run.operator.list",
        "run.sarkaz.age.detail",
        "run.map.footer",
        "run.map.footer.relic",
        "run.ingot",
        "run.idea",
        "run.idea.current",
        "run.squad.card",
        "run.squad.name",
        "run.difficulty.grade",
        "run.difficulty.block",
    };

    private static readonly HashSet<string> AbandonedRunFields = new(StringComparer.Ordinal)
    {
        "hope",
        "maxHope",
        "lifePoints",
        "shield",
        "commandLevel",
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
        return IsAbandonedRunRecognitionId(entry);
    }

    private static bool IsAbandonedRunRecognitionId(string? id)
    {
        var retainedId = RetainedRunRecognitionId(id);
        return !string.IsNullOrWhiteSpace(retainedId)
            && !RetainedRunRecognitionIds.Contains(retainedId);
    }

    private static IReadOnlyList<string> IdTokens(string? value)
    {
        var dotted = Regex.Replace(value ?? "", "([a-z])([A-Z])", "$1.$2");
        return Regex.Split(dotted.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    private static string RetainedRunRecognitionId(string? value)
    {
        var tokens = IdTokens(value);
        var runIndex = -1;
        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index].Equals("run", StringComparison.Ordinal))
                runIndex = index;
        }

        return runIndex < 0 ? "" : string.Join(".", tokens.Skip(runIndex));
    }
}
