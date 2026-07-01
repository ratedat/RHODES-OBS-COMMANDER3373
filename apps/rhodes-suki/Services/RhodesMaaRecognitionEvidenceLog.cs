using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRecognitionEvidenceLog
{
    private const string Source = "suki-maa-native";
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(
        IEnumerable<MaaTaskRunResult> taskResults,
        IEnumerable<MaaCandidatePreview> candidates,
        string? profileId,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        string? requestId = null,
        string? scanId = null)
    {
        var resultList = taskResults.ToArray();
        var candidateList = candidates.ToArray();
        var id = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("D") : requestId.Trim();
        var effectiveScanId = string.IsNullOrWhiteSpace(scanId) ? id : scanId.Trim();
        var normalizedProfile = NormalizeProfile(profileId);
        var log = resultList.Select((result, index) => new
        {
            @event = "maa-task",
            at = completedAt.UtcDateTime.ToString("O"),
            index,
            entry = result.Entry,
            status = result.Status,
            result.Succeeded,
            result.Hit,
            result.Algorithm,
            detail = result.Detail,
        }).ToArray();
        var diagnostics = RhodesMaaTaskDiagnostics.Summarize(resultList);

        var payload = new
        {
            schemaVersion = 1,
            requestId = id,
            scanId = effectiveScanId,
            profileId = normalizedProfile,
            profileLabel = normalizedProfile,
            source = Source,
            status = "completed",
            reason = (string?)null,
            startedAt = startedAt.UtcDateTime.ToString("O"),
            completedAt = completedAt.UtcDateTime.ToString("O"),
            counts = new
            {
                candidates = candidateList.Length,
                suggestions = 0,
                autoApplied = 0,
                log = log.Length,
                resourceTasks = resultList.Length,
                failedResourceTasks = resultList.Count(result => !result.Succeeded),
            },
            candidates = candidateList,
            suggestions = Array.Empty<object>(),
            autoApplied = Array.Empty<object>(),
            log,
            evidence = new
            {
                kind = "maa-resource-task-results",
                diagnostics,
                taskResults = resultList,
            },
            error = (object?)null,
        };

        return JsonSerializer.Serialize(payload, WriteOptions);
    }

    public static async Task<string> SaveAsync(
        IEnumerable<MaaTaskRunResult> taskResults,
        IEnumerable<MaaCandidatePreview> candidates,
        string? profileId,
        string directory,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null)
    {
        Directory.CreateDirectory(directory);
        var completed = completedAt ?? DateTimeOffset.UtcNow;
        var started = startedAt ?? completed;
        var requestId = Guid.NewGuid().ToString("D");
        var normalizedProfile = NormalizeProfile(profileId) ?? "all";
        var file = Path.Combine(directory, $"recognition-{TimestampForFile(started)}-{SanitizeFilePart(normalizedProfile)}-{SanitizeFilePart(requestId)}.json");
        var json = BuildJson(taskResults, candidates, profileId, started, completed, requestId, requestId);
        await File.WriteAllTextAsync(file, $"{json}{Environment.NewLine}");
        return file;
    }

    private static string? NormalizeProfile(string? profileId)
    {
        return string.IsNullOrWhiteSpace(profileId) || profileId.Equals("all", StringComparison.Ordinal)
            ? null
            : profileId.Trim();
    }

    private static string TimestampForFile(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
    }

    private static string SanitizeFilePart(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "scan" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text;
    }
}
