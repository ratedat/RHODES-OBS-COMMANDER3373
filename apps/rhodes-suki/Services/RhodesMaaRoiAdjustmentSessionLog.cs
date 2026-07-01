using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiAdjustmentSessionLog
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string BuildJson(
        IEnumerable<MaaRoiBatchDraftPreview> drafts,
        string? profileId,
        string? scanLogPath,
        string? capturePath,
        MaaRoiBatchApplyResult? batchResult,
        DateTimeOffset createdAt)
    {
        var payload = new MaaRoiAdjustmentSessionPayload(
            1,
            "maa-roi-adjustment-session",
            NormalizeProfile(profileId),
            scanLogPath?.Trim() ?? "",
            capturePath?.Trim() ?? "",
            createdAt.UtcDateTime.ToString("O"),
            drafts.Select(MaaRoiAdjustmentSessionDraft.FromPreview).ToArray(),
            batchResult);

        return JsonSerializer.Serialize(payload, WriteOptions);
    }

    public static async Task<string> SaveAsync(
        IEnumerable<MaaRoiBatchDraftPreview> drafts,
        string? profileId,
        string? scanLogPath,
        string? capturePath,
        MaaRoiBatchApplyResult? batchResult,
        string directory,
        DateTimeOffset? createdAt = null)
    {
        Directory.CreateDirectory(directory);
        var timestamp = createdAt ?? DateTimeOffset.UtcNow;
        var normalizedProfile = NormalizeProfile(profileId) ?? "all";
        var file = Path.Combine(
            directory,
            $"roi-session-{TimestampForFile(timestamp)}-{SanitizeFilePart(normalizedProfile)}.json");
        var json = BuildJson(drafts, profileId, scanLogPath, capturePath, batchResult, timestamp);
        await File.WriteAllTextAsync(file, $"{json}{Environment.NewLine}");
        return file;
    }

    public static MaaRoiAdjustmentSessionPayload Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Empty();

        try
        {
            return JsonSerializer.Deserialize<MaaRoiAdjustmentSessionPayload>(File.ReadAllText(path), ReadOptions) ?? Empty();
        }
        catch
        {
            return Empty();
        }
    }

    private static MaaRoiAdjustmentSessionPayload Empty()
    {
        return new MaaRoiAdjustmentSessionPayload(0, "", null, "", "", "", [], null);
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
        var text = string.IsNullOrWhiteSpace(value) ? "roi-session" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text;
    }
}
