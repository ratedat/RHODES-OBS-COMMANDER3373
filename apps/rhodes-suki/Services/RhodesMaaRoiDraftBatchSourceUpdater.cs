using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiDraftBatchSourceUpdater
{
    public static async Task<MaaRoiBatchApplyResult> ApplyToSourceFilesAsync(
        string maaTasksPath,
        string scanProfilesPath,
        IEnumerable<MaaRoiEditDraft> drafts)
    {
        if (!File.Exists(maaTasksPath))
            return MaaRoiBatchApplyResult.Failed($"maa-tasks.jsonが見つかりません: {maaTasksPath}");
        if (!File.Exists(scanProfilesPath))
            return MaaRoiBatchApplyResult.Failed($"scan-profiles.jsonが見つかりません: {scanProfilesPath}");

        var maaTasksJson = await File.ReadAllTextAsync(maaTasksPath);
        var scanProfilesJson = await File.ReadAllTextAsync(scanProfilesPath);
        var result = ApplyToSourceJsons(
            maaTasksJson,
            scanProfilesJson,
            drafts,
            out var updatedMaaTasksJson,
            out var updatedScanProfilesJson);
        if (!result.Succeeded)
            return result;

        var maaBackupPath = "";
        var scanBackupPath = "";
        if (!string.Equals(maaTasksJson, updatedMaaTasksJson, StringComparison.Ordinal))
        {
            maaBackupPath = BackupPath(maaTasksPath);
            File.Copy(maaTasksPath, maaBackupPath, overwrite: false);
            await File.WriteAllTextAsync(maaTasksPath, updatedMaaTasksJson);
        }

        if (!string.Equals(scanProfilesJson, updatedScanProfilesJson, StringComparison.Ordinal))
        {
            scanBackupPath = BackupPath(scanProfilesPath);
            File.Copy(scanProfilesPath, scanBackupPath, overwrite: false);
            await File.WriteAllTextAsync(scanProfilesPath, updatedScanProfilesJson);
        }

        return result with
        {
            Message = $"ROIドラフトを{result.AppliedCount}件適用しました。Resource再生成を実行してください。",
            MaaTasksBackupPath = maaBackupPath,
            ScanProfilesBackupPath = scanBackupPath,
        };
    }

    public static MaaRoiBatchApplyResult ApplyToSourceJsons(
        string maaTasksJson,
        string scanProfilesJson,
        IEnumerable<MaaRoiEditDraft> drafts,
        out string updatedMaaTasksJson,
        out string updatedScanProfilesJson)
    {
        updatedMaaTasksJson = maaTasksJson;
        updatedScanProfilesJson = scanProfilesJson;

        var selectedDrafts = drafts
            .Where(draft => draft.HasSelection)
            .ToArray();
        if (selectedDrafts.Length == 0)
            return MaaRoiBatchApplyResult.Failed("ROIドラフトがありません。");

        var currentMaaTasksJson = maaTasksJson;
        var currentScanProfilesJson = scanProfilesJson;
        var results = new List<MaaRoiDraftApplyResult>();

        foreach (var draft in selectedDrafts)
        {
            if (RhodesMaaRoiDraftSourceUpdater.UsesScanProfilesSource(draft))
            {
                var result = RhodesMaaRoiDraftSourceUpdater.ApplyToScanProfilesJson(currentScanProfilesJson, draft, out var nextScanProfilesJson);
                results.Add(result);
                if (!result.Succeeded)
                    return MaaRoiBatchApplyResult.Failed(result.Message, results);

                currentScanProfilesJson = nextScanProfilesJson;
            }
            else
            {
                var result = RhodesMaaRoiDraftSourceUpdater.ApplyToMaaTasksJson(currentMaaTasksJson, draft, out var nextMaaTasksJson);
                results.Add(result);
                if (!result.Succeeded)
                    return MaaRoiBatchApplyResult.Failed(result.Message, results);

                currentMaaTasksJson = nextMaaTasksJson;
            }
        }

        updatedMaaTasksJson = currentMaaTasksJson;
        updatedScanProfilesJson = currentScanProfilesJson;
        return new MaaRoiBatchApplyResult(
            true,
            $"ROIドラフトを{results.Count}件適用できます。",
            results.Count,
            results);
    }

    private static string BackupPath(string sourcePath)
    {
        return $"{sourcePath}.bak-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}";
    }
}
