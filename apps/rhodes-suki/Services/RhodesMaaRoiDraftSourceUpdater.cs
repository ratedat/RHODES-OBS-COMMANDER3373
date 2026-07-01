using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiDraftSourceUpdater
{
    public const string MaaTasksSourcePath = "data/recognition/maa-tasks.json";
    public const string ScanProfilesSourcePath = "data/recognition/scan-profiles.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
    };

    public static MaaRoiDraftApplyResult ApplyToMaaTasksJson(
        string json,
        MaaRoiEditDraft draft,
        out string updatedJson)
    {
        updatedJson = json;
        if (!draft.HasSelection)
            return MaaRoiDraftApplyResult.Failed("ROIドラフトが未選択です。");

        if (!draft.IsResourceRoiCandidate)
            return MaaRoiDraftApplyResult.Failed("Resource ROI候補ではないため適用できません。");

        if (!TryParseRoi(draft.RoiJson, out var roi))
            return MaaRoiDraftApplyResult.Failed("ROI座標JSONを解析できません。");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return MaaRoiDraftApplyResult.Failed($"MAA task JSONを解析できません: {ex.Message}");
        }

        if (root is not JsonObject rootObject || rootObject["ocrRegions"] is not JsonArray regions)
            return MaaRoiDraftApplyResult.Failed("ocrRegionsが見つかりません。");

        foreach (var regionNode in regions.OfType<JsonObject>())
        {
            var id = regionNode["id"]?.GetValue<string>() ?? "";
            if (!draft.Entry.Equals(NodeName("RhodesOcrRegion", id), StringComparison.Ordinal))
                continue;

            var previous = RoiToJson(regionNode["roi"] as JsonArray);
            regionNode["roi"] = new JsonArray(roi[0], roi[1], roi[2], roi[3]);
            updatedJson = $"{rootObject.ToJsonString(WriteOptions)}{Environment.NewLine}";
            return new MaaRoiDraftApplyResult(
                true,
                "maa-tasks.jsonのocrRegions ROIを更新できます。",
                MaaTasksSourcePath,
                id,
                previous,
                draft.RoiJson);
        }

        return MaaRoiDraftApplyResult.Failed($"entryに対応するocrRegionsが見つかりません: {draft.Entry}");
    }

    public static MaaRoiDraftApplyResult ApplyToScanProfilesJson(
        string json,
        MaaRoiEditDraft draft,
        out string updatedJson)
    {
        updatedJson = json;
        if (!draft.HasSelection)
            return MaaRoiDraftApplyResult.Failed("ROIドラフトが未選択です。");

        if (!draft.IsResourceRoiCandidate)
            return MaaRoiDraftApplyResult.Failed("Resource ROI候補ではないため適用できません。");

        if (!TryParseRoi(draft.RoiJson, out var roi))
            return MaaRoiDraftApplyResult.Failed("ROI座標JSONを解析できません。");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            return MaaRoiDraftApplyResult.Failed($"scan-profiles JSONを解析できません: {ex.Message}");
        }

        if (root is not JsonObject rootObject || rootObject["profiles"] is not JsonArray profiles)
            return MaaRoiDraftApplyResult.Failed("profilesが見つかりません。");

        foreach (var profileNode in profiles.OfType<JsonObject>())
        {
            var profileId = profileNode["id"]?.GetValue<string>() ?? "";
            if (profileNode["templateOcrRegions"] is not JsonArray regions)
                continue;

            var index = 0;
            foreach (var regionNode in regions.OfType<JsonObject>())
            {
                var idPrefix = regionNode["idPrefix"]?.GetValue<string>() ?? "";
                var suffix = string.IsNullOrWhiteSpace(idPrefix) ? index.ToString() : idPrefix;
                if (!draft.Entry.Equals(NodeName("RhodesTemplate", $"{profileId}.{suffix}"), StringComparison.Ordinal))
                {
                    index++;
                    continue;
                }

                var previous = RoiToJson(regionNode["searchRoi"] as JsonObject);
                regionNode["searchRoi"] = RoiObject(roi);
                updatedJson = $"{rootObject.ToJsonString(WriteOptions)}{Environment.NewLine}";
                return new MaaRoiDraftApplyResult(
                    true,
                    "scan-profiles.jsonのtemplateOcrRegions searchRoiを更新できます。",
                    ScanProfilesSourcePath,
                    $"{profileId}.{suffix}",
                    previous,
                    draft.RoiJson);
            }
        }

        return MaaRoiDraftApplyResult.Failed($"entryに対応するtemplateOcrRegionsが見つかりません: {draft.Entry}");
    }

    public static MaaRoiDraftApplyResult ApplyToSourceJson(
        string json,
        MaaRoiEditDraft draft,
        out string updatedJson)
    {
        return UsesScanProfilesSource(draft)
            ? ApplyToScanProfilesJson(json, draft, out updatedJson)
            : ApplyToMaaTasksJson(json, draft, out updatedJson);
    }

    public static async Task<MaaRoiDraftApplyResult> ApplyToMaaTasksFileAsync(
        string sourcePath,
        MaaRoiEditDraft draft)
    {
        if (!File.Exists(sourcePath))
            return MaaRoiDraftApplyResult.Failed($"maa-tasks.jsonが見つかりません: {sourcePath}");

        var json = await File.ReadAllTextAsync(sourcePath);
        var result = ApplyToMaaTasksJson(json, draft, out var updatedJson);
        if (!result.Succeeded)
            return result with { SourcePath = sourcePath };

        var backupPath = $"{sourcePath}.bak-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}";
        File.Copy(sourcePath, backupPath, overwrite: false);
        await File.WriteAllTextAsync(sourcePath, updatedJson);
        return result with
        {
            SourcePath = sourcePath,
            BackupPath = backupPath,
            Message = "maa-tasks.jsonへROIを適用しました。Resource再生成を実行してください。",
        };
    }

    public static async Task<MaaRoiDraftApplyResult> ApplyToScanProfilesFileAsync(
        string sourcePath,
        MaaRoiEditDraft draft)
    {
        if (!File.Exists(sourcePath))
            return MaaRoiDraftApplyResult.Failed($"scan-profiles.jsonが見つかりません: {sourcePath}");

        var json = await File.ReadAllTextAsync(sourcePath);
        var result = ApplyToScanProfilesJson(json, draft, out var updatedJson);
        if (!result.Succeeded)
            return result with { SourcePath = sourcePath };

        var backupPath = $"{sourcePath}.bak-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}";
        File.Copy(sourcePath, backupPath, overwrite: false);
        await File.WriteAllTextAsync(sourcePath, updatedJson);
        return result with
        {
            SourcePath = sourcePath,
            BackupPath = backupPath,
            Message = "scan-profiles.jsonへROIを適用しました。Resource再生成を実行してください。",
        };
    }

    public static async Task<MaaRoiDraftApplyResult> ApplyToSourceFileAsync(
        string sourcePath,
        MaaRoiEditDraft draft)
    {
        return UsesScanProfilesSource(draft)
            ? await ApplyToScanProfilesFileAsync(sourcePath, draft)
            : await ApplyToMaaTasksFileAsync(sourcePath, draft);
    }

    public static bool UsesScanProfilesSource(MaaRoiEditDraft draft)
    {
        return draft.Entry.StartsWith("RhodesTemplate_", StringComparison.Ordinal);
    }

    private static bool TryParseRoi(string value, out int[] roi)
    {
        roi = [];
        try
        {
            var node = JsonNode.Parse(value);
            if (node is not JsonArray array || array.Count != 4)
                return false;

            var values = array
                .Select(item => item?.GetValue<int>())
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
            if (values.Length != 4 || values[2] <= 0 || values[3] <= 0)
                return false;

            roi = values;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string RoiToJson(JsonArray? array)
    {
        if (array is null)
            return "";

        var values = array
            .Select(item => item?.GetValue<int>())
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
        return values.Length == 4 ? $"[{values[0]},{values[1]},{values[2]},{values[3]}]" : "";
    }

    private static string RoiToJson(JsonObject? obj)
    {
        if (obj is null)
            return "";

        var values = new[]
        {
            IntValue(obj, "x"),
            IntValue(obj, "y"),
            IntValue(obj, "width"),
            IntValue(obj, "height"),
        };
        return values.All(item => item.HasValue)
            ? $"[{values[0]},{values[1]},{values[2]},{values[3]}]"
            : "";
    }

    private static JsonObject RoiObject(int[] roi)
    {
        return new JsonObject
        {
            ["x"] = roi[0],
            ["y"] = roi[1],
            ["width"] = roi[2],
            ["height"] = roi[3],
        };
    }

    private static int? IntValue(JsonObject obj, string propertyName)
    {
        return obj[propertyName]?.GetValue<int>();
    }

    private static string NodeName(string prefix, string id)
    {
        var safe = Regex.Replace(id, "[^A-Za-z0-9]+", "_").Trim('_');
        return $"{prefix}_{safe}";
    }
}
