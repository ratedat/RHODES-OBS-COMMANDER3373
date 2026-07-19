using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRunStateStore
{
    public const string StartupCampaignId = "is2_phantom";

    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    // TypeInfoResolver必須: JsonArray.Add<T>で作られたノード(JsonValueCustomized)は、
    // リゾルバ無しのoptionsをToJsonStringへ渡すとMakeReadOnlyで例外になる。
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static string ResolveDefaultStatePath()
    {
        var dataRoot = RhodesRunCatalog.ResolveDataRoot();
        return RhodesRunCatalog.ResolveStatePath(dataRoot);
    }

    public static async Task SaveChoicesAsync(
        IEnumerable<SukiChoiceItem> operators,
        IEnumerable<SukiChoiceItem> relics,
        SukiChoicePersistenceOptions options,
        string? statePath = null,
        DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            ApplyChoices(state, operators, relics, options, now ?? DateTimeOffset.UtcNow);
            await WriteJsonAtomicAsync(path, state);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task SaveRunContextAsync(
        string campaignId,
        string? statePath = null,
        DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            ApplyRunContext(state, campaignId, now ?? DateTimeOffset.UtcNow);
            await WriteJsonAtomicAsync(path, state);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task SaveBossSelectionAsync(
        string campaignId,
        string field,
        IEnumerable<string> selectedIds,
        bool allowsMultiple,
        string? statePath = null,
        DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            ApplyBossSelection(state, campaignId, field, selectedIds, allowsMultiple, now ?? DateTimeOffset.UtcNow);
            await WriteJsonAtomicAsync(path, state);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task<SukiCandidateApplySummary> SaveCandidatesAsync(
        IEnumerable<MaaCandidatePreview> candidates,
        string? statePath = null,
        DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            var before = state.ToJsonString();
            var summary = RhodesRecognitionCandidateApplier.Apply(state, candidates, now ?? DateTimeOffset.UtcNow);
            if (summary.AppliedCount > 0 || !string.Equals(before, state.ToJsonString(), StringComparison.Ordinal))
                await WriteJsonAtomicAsync(path, state);
            return summary;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task ReplaceStateJsonAsync(string stateJson, string? statePath = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson) as JsonObject
            ?? new JsonObject { ["version"] = 1 };

        await WriteLock.WaitAsync();
        try
        {
            node["version"] ??= 1;
            PruneAbandonedRunValues(node);
            NormalizeOcrEnginePreference(node);
            await WriteJsonAtomicAsync(path, node);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task ClearCurrentRunAsync(string? statePath = null, DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            var run = EnsureObject(state, "run");
            var campaignId = JsonString(run, "campaignId");
            ResetRunValues(run);
            ClearBossSelectionsForCampaign(state, campaignId);
            state["bossFlags"] = new JsonArray();
            state["operators"] = new JsonArray();
            state["relics"] = new JsonArray();
            state["usedRelicIds"] = new JsonArray();
            state["updatedAt"] = (now ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O");
            await WriteJsonAtomicAsync(path, state);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static async Task PrepareForStartupAsync(
        string? statePath = null,
        DateTimeOffset? now = null)
    {
        var path = string.IsNullOrWhiteSpace(statePath) ? ResolveDefaultStatePath() : statePath;
        await WriteLock.WaitAsync();
        try
        {
            var state = await LoadStateNodeAsync(path);
            ApplyStartupReset(state, now ?? DateTimeOffset.UtcNow);
            await WriteJsonAtomicAsync(path, state);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static JsonObject ApplyStartupReset(JsonObject state, DateTimeOffset now)
    {
        var adb = state["adb"]?.DeepClone();
        var preferences = state["preferences"]?.DeepClone();
        var theme = state["theme"]?.DeepClone();

        state.Clear();
        state["version"] = 1;
        if (theme is not null)
            state["theme"] = theme;
        state["mode"] = "casual";
        state["run"] = new JsonObject
        {
            ["campaignId"] = StartupCampaignId,
        };
        state["operators"] = new JsonArray();
        state["relics"] = new JsonArray();
        state["usedRelicIds"] = new JsonArray();
        state["bossFlags"] = new JsonArray();
        state["bossSelections"] = new JsonObject();
        state["pendingSuggestions"] = new JsonArray();
        state["tournament"] = new JsonObject
        {
            ["pendingState"] = null,
            ["lastSubmissionAt"] = null,
            ["submittedBy"] = null,
        };
        if (adb is not null)
            state["adb"] = adb;
        if (preferences is not null)
            state["preferences"] = preferences;
        state["updatedAt"] = now.UtcDateTime.ToString("O");
        return state;
    }

    public static JsonObject ApplyChoices(
        JsonObject state,
        IEnumerable<SukiChoiceItem> operators,
        IEnumerable<SukiChoiceItem> relics,
        SukiChoicePersistenceOptions options,
        DateTimeOffset now)
    {
        state["version"] ??= 1;
        PruneAbandonedRunValues(state);
        NormalizeOcrEnginePreference(state);
        state["operators"] = ToJsonArray(operators.Where(item => item.IsSelected).Select(item => item.Id));
        state["relics"] = ToJsonArray(relics.Where(item => item.IsSelected).Select(item => item.Id));
        state["usedRelicIds"] = ToJsonArray(relics
            .Where(item => item.IsSelected && item.SupportsUsedFlag && item.IsUsed)
            .Select(item => item.Id));
        state["updatedAt"] = now.UtcDateTime.ToString("O");

        var preferences = EnsureObject(state, "preferences");
        preferences["operatorExcludedIds"] = ToJsonArray(operators.Where(item => item.IsExcluded).Select(item => item.Id));
        preferences["relicExcludedIds"] = ToJsonArray(relics.Where(item => item.IsExcluded).Select(item => item.Id));
        preferences["operatorShowSelectedFirst"] = options.OperatorShowSelectedFirst;
        preferences["operatorHideExcluded"] = options.OperatorHideExcluded;
        preferences["operatorSelectedOnly"] = options.OperatorSelectedOnly;
        preferences["relicShowSelectedFirst"] = options.RelicShowSelectedFirst;
        preferences["relicHideExcluded"] = options.RelicHideExcluded;
        preferences["relicSelectedOnly"] = options.RelicSelectedOnly;
        preferences["operatorGridColumns"] = Math.Clamp(options.OperatorGridColumns, 1, 4);
        preferences["relicGridColumns"] = Math.Clamp(options.RelicGridColumns, 1, 4);

        return state;
    }

    public static JsonObject ApplyBossSelection(
        JsonObject state,
        string campaignId,
        string field,
        IEnumerable<string> selectedIds,
        bool allowsMultiple,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("campaignId is required.", nameof(campaignId));
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("field is required.", nameof(field));

        var values = selectedIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var allSelections = EnsureObject(state, "bossSelections");
        var campaignSelections = EnsureObject(allSelections, campaignId.Trim());
        if (allowsMultiple)
            campaignSelections[field.Trim()] = ToJsonArray(values);
        else
            campaignSelections[field.Trim()] = values.FirstOrDefault();
        state["version"] ??= 1;
        state["updatedAt"] = now.UtcDateTime.ToString("O");
        return state;
    }

    public static bool ClearBossSelectionsForCampaign(JsonObject state, string campaignId)
    {
        if (string.IsNullOrWhiteSpace(campaignId)
            || state["bossSelections"] is not JsonObject allSelections
            || allSelections[campaignId] is not JsonObject campaignSelections)
        {
            return false;
        }

        var changed = campaignSelections.Count > 0;
        campaignSelections.Clear();
        return changed;
    }

    public static JsonObject ApplyRunContext(JsonObject state, string campaignId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            throw new ArgumentException("campaignId is required.", nameof(campaignId));

        state["version"] ??= 1;
        state["updatedAt"] = now.UtcDateTime.ToString("O");
        NormalizeOcrEnginePreference(state);

        var run = EnsureObject(state, "run");
        var previousCampaignId = JsonString(run, "campaignId");
        var normalizedCampaignId = campaignId.Trim();
        run["campaignId"] = normalizedCampaignId;
        if (!string.Equals(previousCampaignId, normalizedCampaignId, StringComparison.Ordinal))
            ResetRunValues(run);
        else
            PruneAbandonedRunValuesFromRun(run);

        return state;
    }

    public static bool PruneAbandonedRunValues(JsonObject state)
    {
        if (state["run"] is JsonObject run)
            return PruneAbandonedRunValuesFromRun(run);
        return false;
    }

    public static bool PruneAbandonedRunValuesFromRun(JsonObject run)
    {
        var removed = false;
        foreach (var propertyName in RhodesMaaRecognitionPolicy.AbandonedRunFields)
        {
            removed |= run.Remove(propertyName);
        }
        return removed;
    }

    public static bool NormalizeOcrEnginePreference(JsonObject state)
    {
        var preferences = EnsureObject(state, "preferences");
        var previous = JsonString(preferences, "ocrEngine");
        var normalized = SukiOcrEngineCatalog.Normalize(previous);
        preferences["ocrEngine"] = normalized;
        return !string.Equals(previous, normalized, StringComparison.Ordinal);
    }

    private static async Task<JsonObject> LoadStateNodeAsync(string path)
    {
        if (!File.Exists(path))
            return new JsonObject { ["version"] = 1 };

        await using var stream = File.OpenRead(path);
        var node = await JsonNode.ParseAsync(stream);
        return node as JsonObject ?? new JsonObject { ["version"] = 1 };
    }

    private static async Task WriteJsonAtomicAsync(string path, JsonObject state)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tempPath = $"{path}.{Environment.ProcessId}.tmp";
        await File.WriteAllTextAsync(tempPath, $"{state.ToJsonString(WriteOptions)}{Environment.NewLine}");
        File.Move(tempPath, path, true);
    }

    private static JsonObject EnsureObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
            return existing;

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static void ResetRunValues(JsonObject run)
    {
        // difficultyTierId は difficulty からの導出値なので、等級と一緒に破棄する。
        foreach (var propertyName in new[]
            {
                "squad",
                "squadId",
                "squadRandomEffectOptionId",
                "performanceId",
                "difficulty",
                "difficultyTierId",
                "ingot",
                "idea",
                "special",
            }
            .Concat(RhodesMaaRecognitionPolicy.AbandonedRunFields))
        {
            run.Remove(propertyName);
        }
    }

    private static string JsonString(JsonObject parent, string propertyName)
    {
        if (parent.TryGetPropertyValue(propertyName, out var node) && node is JsonValue value
            && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return "";
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var array = new JsonArray();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                continue;

            array.Add(value);
        }

        return array;
    }
}

public sealed record SukiChoicePersistenceOptions(
    bool OperatorShowSelectedFirst,
    bool OperatorHideExcluded,
    bool OperatorSelectedOnly,
    bool RelicShowSelectedFirst,
    bool RelicHideExcluded,
    bool RelicSelectedOnly,
    int OperatorGridColumns,
    int RelicGridColumns);
