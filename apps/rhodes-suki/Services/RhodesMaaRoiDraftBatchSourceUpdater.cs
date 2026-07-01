using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiDraftBatchSourceUpdater
{
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
}
