using System.Text.RegularExpressions;
using System.Text.Json;

namespace RhodesSuki.Services;

public static class RhodesMaaRecognitionPolicy
{
    public const string TargetPolicySourcePath = "data/recognition/maa-recognition-target-policy.json";
    private static readonly Lazy<TargetPolicy> Policy = new(LoadTargetPolicy);

    public static bool IsRetainedRecognitionSource(string? id, string? candidateField = "")
    {
        return !IsAbandonedRunRecognitionId(id)
            && !Policy.Value.AbandonedRunFields.Contains(candidateField ?? "");
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
            && !Policy.Value.RetainedRunRecognitionIds.Contains(retainedId);
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

    private static TargetPolicy LoadTargetPolicy()
    {
        var path = ResolveTargetPolicyPath();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var runRecognition = root.TryGetProperty("runRecognition", out var value) ? value : default;
        return new TargetPolicy(
            ReadStringSet(runRecognition, "retainedIds"),
            ReadStringSet(runRecognition, "abandonedFields"));
    }

    private static string ResolveTargetPolicyPath()
    {
        foreach (var origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = TargetPolicySourcePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Aggregate(origin, Path.Combine);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException("MAA recognition target policy was not found.", TargetPolicySourcePath);
    }

    private static HashSet<string> ReadStringSet(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(propertyName, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed record TargetPolicy(
        HashSet<string> RetainedRunRecognitionIds,
        HashSet<string> AbandonedRunFields);
}
