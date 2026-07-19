using System.Text.RegularExpressions;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRecognitionPolicy
{
    public const string TargetPolicySourcePath = "data/recognition/maa-recognition-target-policy.json";
    private static readonly Lazy<TargetPolicy> Policy = new(LoadTargetPolicy);

    public static IReadOnlyCollection<string> RetainedRunRecognitionIds => Policy.Value.RetainedRunRecognitionIds;

    public static IReadOnlyCollection<string> RetainedCandidateKinds => Policy.Value.RetainedCandidateKinds;

    public static IReadOnlyCollection<string> RetainedRunStatusFields => Policy.Value.RetainedRunStatusFields;

    public static IReadOnlyCollection<string> AbandonedRunFields => Policy.Value.AbandonedRunFields;

    public static bool RequiresManualDifficulty(string? campaignId)
    {
        return campaignId is "is2_phantom" or "is3_mizuki" or "is4_sami";
    }

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

    public static bool IsRetainedCandidate(MaaCandidatePreview? candidate)
    {
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.Kind))
            return false;

        if (!Policy.Value.RetainedCandidateKinds.Contains(candidate.Kind))
            return false;

        if (!candidate.Kind.Equals("runStatus", StringComparison.Ordinal))
            return true;

        return Policy.Value.RetainedRunStatusFields.Contains(candidate.Field)
            && !Policy.Value.AbandonedRunFields.Contains(candidate.Field);
    }

    public static bool IsAbandonedRunEntry(string? entry)
    {
        return IsAbandonedRunRecognitionId(entry);
    }

    private static bool IsAbandonedRunRecognitionId(string? id)
    {
        var retainedId = RetainedRunRecognitionId(id);
        return !string.IsNullOrWhiteSpace(retainedId)
            && !Policy.Value.RetainedRunRecognitionIds.Any(item =>
                retainedId.Equals(item, StringComparison.Ordinal)
                || retainedId.StartsWith($"{item}.", StringComparison.Ordinal));
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
        var recognitionTargets = root.TryGetProperty("recognitionTargets", out var targetsValue) ? targetsValue : default;
        var runRecognition = root.TryGetProperty("runRecognition", out var value) ? value : default;
        return new TargetPolicy(
            ReadStringSet(recognitionTargets, "retainedCandidateKinds"),
            ReadStringSet(runRecognition, "retainedFields"),
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
        HashSet<string> RetainedCandidateKinds,
        HashSet<string> RetainedRunStatusFields,
        HashSet<string> RetainedRunRecognitionIds,
        HashSet<string> AbandonedRunFields);
}
