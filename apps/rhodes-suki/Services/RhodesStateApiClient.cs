using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesStateApiResult(
    string StateJson,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public sealed record RhodesCandidateStateApplyResult(
    string StateJson,
    SukiCandidateApplySummary Summary);

public static class RhodesStateApiClient
{
    // TypeInfoResolver必須: JsonArray.Add<T>由来のノードをToJsonStringする際の例外を防ぐ。
    private static readonly JsonSerializerOptions IndentedWriteOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    public static async Task<RhodesStateApiResult> FetchAsync(
        string baseUrl,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        try
        {
            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/state", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesStateApiResult("", $"{(int)response.StatusCode} {Shorten(json, 180)}");

            return new RhodesStateApiResult(json, "");
        }
        catch (Exception ex)
        {
            return new RhodesStateApiResult("", Shorten(ex.Message, 180));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static async Task<RhodesStateApiResult> SaveAsync(
        string baseUrl,
        string stateJson,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        try
        {
            using var content = new StringContent(stateJson, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{baseUrl.TrimEnd('/')}/api/state", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesStateApiResult("", $"{(int)response.StatusCode} {Shorten(json, 180)}");

            return new RhodesStateApiResult(json, "");
        }
        catch (Exception ex)
        {
            return new RhodesStateApiResult("", Shorten(ex.Message, 180));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static string ApplyAdbSettingsToStateJson(string stateJson, RhodesAdbApiSettings settings)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        var adb = root["adb"] as JsonObject;
        if (adb is null)
        {
            adb = [];
            root["adb"] = adb;
        }
        adb["autoDetect"] = settings.AutoDetect;
        adb["connectionPreset"] = string.IsNullOrWhiteSpace(settings.ConnectionPreset) ? "auto" : settings.ConnectionPreset;
        adb["adbPath"] = settings.AdbPath ?? "";
        adb["serial"] = settings.Serial ?? "";
        adb["restartServerOnFailure"] = true;
        adb["restartProcessOnFailure"] = true;
        adb["reconnectAttempts"] = 5;
        adb["reconnectDelayMs"] = 1000;
        RhodesRunStateStore.PruneAbandonedRunValues(root);
        RhodesRunStateStore.NormalizeOcrEnginePreference(root);
        root["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        return root.ToJsonString(IndentedWriteOptions);
    }

    public static string ApplyRunContextToStateJson(
        string stateJson,
        string campaignId,
        DateTimeOffset? now = null)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        RhodesRunStateStore.ApplyRunContext(root, campaignId, now ?? DateTimeOffset.UtcNow);
        return root.ToJsonString(IndentedWriteOptions);
    }

    public static string ApplyChoicesToStateJson(
        string stateJson,
        IEnumerable<SukiChoiceItem> operators,
        IEnumerable<SukiChoiceItem> relics,
        SukiChoicePersistenceOptions choiceOptions,
        DateTimeOffset? now = null)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        RhodesRunStateStore.ApplyChoices(root, operators, relics, choiceOptions, now ?? DateTimeOffset.UtcNow);
        return root.ToJsonString();
    }

    public static RhodesCandidateStateApplyResult ApplyCandidatesToStateJson(
        string stateJson,
        IEnumerable<MaaCandidatePreview> candidates,
        DateTimeOffset? now = null)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        var summary = RhodesRecognitionCandidateApplier.Apply(root, candidates, now ?? DateTimeOffset.UtcNow);
        return new RhodesCandidateStateApplyResult(root.ToJsonString(), summary);
    }

    public static string ApplySukiPreferencesToStateJson(
        string stateJson,
        SukiChoicePersistenceOptions choiceOptions,
        SukiOutputPreferences outputPreferences,
        string ocrEngine = SukiOcrEngineCatalog.DefaultId)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        var preferences = root["preferences"] as JsonObject;
        if (preferences is null)
        {
            preferences = [];
            root["preferences"] = preferences;
        }

        preferences["ocrEngine"] = SukiOcrEngineCatalog.Normalize(ocrEngine);
        preferences["operatorShowSelectedFirst"] = choiceOptions.OperatorShowSelectedFirst;
        preferences["operatorHideExcluded"] = choiceOptions.OperatorHideExcluded;
        preferences["operatorSelectedOnly"] = choiceOptions.OperatorSelectedOnly;
        preferences["relicShowSelectedFirst"] = choiceOptions.RelicShowSelectedFirst;
        preferences["relicHideExcluded"] = choiceOptions.RelicHideExcluded;
        preferences["relicSelectedOnly"] = choiceOptions.RelicSelectedOnly;
        preferences["operatorGridColumns"] = Math.Clamp(choiceOptions.OperatorGridColumns, 1, 6);
        preferences["relicGridColumns"] = Math.Clamp(choiceOptions.RelicGridColumns, 1, 6);

        var scrollSpeed = Math.Clamp(outputPreferences.ScrollSpeed, 0, 30);
        foreach (var field in new[]
        {
            "compactRelicScrollSpeed",
            "verticalRelicScrollSpeed",
            "verticalOperatorScrollSpeed",
            "horizontalRelicScrollSpeed",
            "horizontalOperatorScrollSpeed",
        })
        {
            preferences[field] = scrollSpeed;
        }

        preferences["sukiOutputSeparateWindow"] = outputPreferences.SeparateWindow;
        preferences["sukiOutputTransparentBackground"] = outputPreferences.TransparentBackground;
        preferences["sukiOutputBackgroundTransparency"] = Math.Clamp(outputPreferences.BackgroundTransparency, 0, 100);
        preferences["sukiOutputShowPartTitles"] = outputPreferences.ShowPartTitles;
        preferences["sukiOutputParts"] = ToOutputPartsJson(outputPreferences.Parts);
        preferences["sukiOverlayLayout"] = ToOverlayLayoutJson(
            RhodesOverlayLayoutCatalog.Normalize(outputPreferences.OverlayLayout));
        ApplySarkazSpecialOverlayPreference(root, outputPreferences.Parts);

        var currentMode = JsonString(root, "mode");
        if (outputPreferences.TournamentMode)
            root["mode"] = "tournament";
        else if (string.Equals(currentMode, "tournament", StringComparison.OrdinalIgnoreCase))
            root["mode"] = "casual";

        RhodesRunStateStore.PruneAbandonedRunValues(root);
        root["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        return root.ToJsonString(IndentedWriteOptions);
    }

    private static void ApplySarkazSpecialOverlayPreference(
        JsonObject root,
        IReadOnlyList<SukiOutputPartState> outputParts)
    {
        var specialPart = outputParts.FirstOrDefault(part =>
            part.Id.Equals("special", StringComparison.OrdinalIgnoreCase));
        if (specialPart is null)
            return;

        var run = root["run"] as JsonObject;
        if (run is null || !JsonString(run, "campaignId").Equals(RhodesPublicDebugPolicy.SarkazCampaignId, StringComparison.Ordinal))
            return;

        var special = run["special"] as JsonObject;
        if (special is null)
        {
            special = [];
            run["special"] = special;
        }

        var campaign = special[RhodesPublicDebugPolicy.SarkazCampaignId] as JsonObject;
        if (campaign is null)
        {
            campaign = [];
            special[RhodesPublicDebugPolicy.SarkazCampaignId] = campaign;
        }

        campaign["thoughtOverlayVisible"] = specialPart.Enabled;
    }

    public static string ClearCurrentRunInStateJson(string stateJson)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson) as JsonObject
            ?? new JsonObject { ["version"] = 1 };
        var run = root["run"] as JsonObject ?? new JsonObject();
        root["run"] = run;
        foreach (var propertyName in new[]
        {
            "squad", "squadId", "squadRandomEffect", "squadRandomEffectOptionId",
            "difficulty", "difficultyTierId", "ingot", "idea", "special"
        }.Concat(RhodesMaaRecognitionPolicy.AbandonedRunFields))
        {
            run.Remove(propertyName);
        }
        RhodesRunStateStore.ClearBossSelectionsForCampaign(root, JsonString(run, "campaignId"));
        root["bossFlags"] = new JsonArray();
        root["operators"] = new JsonArray();
        root["operatorCounts"] = new JsonObject();
        root["relics"] = new JsonArray();
        root["usedRelicIds"] = new JsonArray();
        root["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        return root.ToJsonString(IndentedWriteOptions);
    }

    public static string ApplyBossSelectionToStateJson(
        string stateJson,
        string campaignId,
        string field,
        IEnumerable<string> selectedIds,
        bool allowsMultiple)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson) as JsonObject
            ?? new JsonObject { ["version"] = 1 };
        RhodesRunStateStore.ApplyBossSelection(
            root,
            campaignId,
            field,
            selectedIds,
            allowsMultiple,
            DateTimeOffset.UtcNow);
        return root.ToJsonString(IndentedWriteOptions);
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }

    private static JsonArray ToOutputPartsJson(IEnumerable<SukiOutputPartState> parts)
    {
        var array = new JsonArray();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.Id))
                continue;

            array.Add(new JsonObject
            {
                ["id"] = part.Id,
                ["enabled"] = part.Enabled,
                ["scrollEnabled"] = part.ScrollEnabled,
                ["hideExcluded"] = part.HideExcluded,
                ["width"] = Math.Max(1, part.Width),
                ["height"] = Math.Max(1, part.Height),
            });
        }

        return array;
    }

    private static JsonArray ToOverlayLayoutJson(IEnumerable<SukiOverlayLayoutState> parts)
    {
        var array = new JsonArray();
        foreach (var part in parts)
        {
            array.Add(new JsonObject
            {
                ["id"] = part.Id,
                ["enabled"] = part.Enabled,
                ["x"] = part.X,
                ["y"] = part.Y,
                ["width"] = part.Width,
                ["height"] = part.Height,
                ["zIndex"] = part.ZIndex,
            });
        }

        return array;
    }

    private static string JsonString(JsonObject parent, string propertyName)
    {
        return parent.TryGetPropertyValue(propertyName, out var node)
            && node is JsonValue value
            && value.TryGetValue<string>(out var text)
            ? text
            : "";
    }
}
