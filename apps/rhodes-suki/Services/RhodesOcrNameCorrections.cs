using System.Text;
using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record RhodesOcrNameTarget(string Kind, string CampaignId, string Id, string Name);
public sealed record RhodesOcrNameCorrection(string RuleId, string TargetId, string CanonicalName);

public sealed class RhodesOcrNameCorrections
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly RhodesOcrNameCorrections Empty = new(new Dictionary<string, RhodesOcrNameCorrection>());
    private static (string Stamp, RhodesOcrNameCorrections Catalog)? _cached;
    private readonly IReadOnlyDictionary<string, RhodesOcrNameCorrection> _aliases;

    private RhodesOcrNameCorrections(IReadOnlyDictionary<string, RhodesOcrNameCorrection> aliases) => _aliases = aliases;

    public RhodesOcrNameCorrection? Resolve(string kind, string campaignId, string? text) =>
        _aliases.GetValueOrDefault(Key(kind, kind == "operator" ? "" : campaignId, Normalize(text)));

    public bool ContainsAliasFragment(string kind, string campaignId, string? text)
    {
        var normalized = Normalize(text);
        var scope = Key(kind, kind == "operator" ? "" : campaignId, "");
        if (normalized.Length == 0 || _aliases.ContainsKey(scope + normalized)) return false;
        return _aliases.Keys.Any(key => key.StartsWith(scope, StringComparison.Ordinal)
            && normalized.Contains(key[scope.Length..], StringComparison.Ordinal));
    }

    public static string Normalize(string? text) => string.Concat((text ?? "").Normalize(NormalizationForm.FormKC)
        .Where(ch => !char.IsWhiteSpace(ch) && ch is not ('「' or '」' or '『' or '』' or '【' or '】' or '[' or ']' or '(' or ')')))
        .ToLowerInvariant();

    public static RhodesOcrNameCorrections Load()
    {
        try
        {
            var dataRoot = RhodesRunCatalog.ResolveDataRoot();
            var correctionPath = Path.Combine(dataRoot, "recognition", "rhodes-name-corrections.json");
            var files = new[] { correctionPath, Path.Combine(dataRoot, "operators.json"),
                Path.Combine(dataRoot, "relics.json"), Path.Combine(dataRoot, "selectable-effects.json") };
            var stamp = string.Join("|", files.Select(path =>
            {
                var file = new FileInfo(path);
                return $"{path}:{(file.Exists ? file.Length : -1)}:{file.LastWriteTimeUtc.Ticks}";
            }));
            lock (Sync)
            {
                if (_cached is { } cached && cached.Stamp == stamp) return cached.Catalog;
                var catalog = File.Exists(correctionPath)
                    ? FromJson(File.ReadAllText(correctionPath),
                        ReadTargets(files[1], "operators", "operator")
                            .Concat(ReadTargets(files[2], "relics", "relic"))
                            .Concat(ReadTargets(files[3], "selectableEffects", "thought")))
                    : Empty;
                _cached = (stamp, catalog);
                return catalog;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return Empty;
        }
    }

    public static RhodesOcrNameCorrections FromJson(string json, IEnumerable<RhodesOcrNameTarget> targets)
    {
        try
        {
            var source = JsonSerializer.Deserialize<CorrectionDocument>(json, JsonOptions);
            if (source is not { SchemaVersion: 1, Normalization: "nfkc-compact-quotes-lower", Rules: { } rules })
                return Empty;
            var catalog = targets.ToArray();
            var byId = catalog.GroupBy(item => Key(item.Kind, item.CampaignId, item.Id), StringComparer.Ordinal)
                .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            var formalNames = catalog.Select(item => Key(item.Kind, item.CampaignId, Normalize(item.Name)))
                .ToHashSet(StringComparer.Ordinal);
            var aliases = new List<(string Key, RhodesOcrNameCorrection Correction)>();
            foreach (var rule in rules)
            {
                if (rule is null || string.IsNullOrWhiteSpace(rule.Id)
                    || rule.Kind is not ("operator" or "relic" or "thought")
                    || rule.Aliases is null || string.IsNullOrWhiteSpace(rule.TargetId)
                    || rule.CampaignId is null || rule.Kind == "operator" && rule.CampaignId != ""
                    || !byId.TryGetValue(Key(rule.Kind, rule.CampaignId, rule.TargetId), out var target)
                    || target.Name != rule.CanonicalName)
                    continue;
                foreach (var alias in rule.Aliases)
                {
                    var normalized = Normalize(alias);
                    var key = Key(rule.Kind, rule.CampaignId, normalized);
                    if (normalized.Length == 0 || formalNames.Contains(key)) continue;
                    aliases.Add((key, new RhodesOcrNameCorrection(rule.Id, target.Id, target.Name)));
                }
            }
            return new RhodesOcrNameCorrections(aliases.GroupBy(item => item.Key, StringComparer.Ordinal)
                .Where(group => group.Select(item => item.Correction.TargetId).Distinct(StringComparer.Ordinal).Count() == 1)
                .ToDictionary(group => group.Key, group => group.First().Correction, StringComparer.Ordinal));
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            return Empty;
        }
    }

    private static IEnumerable<RhodesOcrNameTarget> ReadTargets(string path, string arrayName, string kind)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty(arrayName, out var array) || array.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var item in array.EnumerateArray())
        {
            if (kind == "thought" && ReadString(item, "slot") != "thought") continue;
            var id = ReadString(item, "id");
            var name = ReadString(item, "name");
            if (id.Length == 0 || name.Length == 0) continue;
            yield return new RhodesOcrNameTarget(kind, kind == "operator" ? "" : ReadString(item, "campaignId"), id, name);
        }
    }

    private static string ReadString(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static string Key(string kind, string campaignId, string value) => $"{kind}\0{campaignId}\0{value}";
    private sealed record CorrectionDocument(int SchemaVersion, string? Normalization, CorrectionRule[]? Rules);
    private sealed record CorrectionRule(string? Id, string? Kind, string? CampaignId, string? TargetId, string? CanonicalName, string[]? Aliases);
}
