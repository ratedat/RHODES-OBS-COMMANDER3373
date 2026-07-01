using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiEditDraftLog
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BuildJson(
        MaaRoiEditDraft draft,
        string? profileId,
        DateTimeOffset exportedAt)
    {
        var payload = new
        {
            schemaVersion = 1,
            kind = "maa-roi-edit-draft",
            profileId = NormalizeProfile(profileId),
            exportedAt = exportedAt.UtcDateTime.ToString("O"),
            draft,
        };

        return JsonSerializer.Serialize(payload, WriteOptions);
    }

    public static async Task<string> SaveAsync(
        MaaRoiEditDraft draft,
        string? profileId,
        string directory,
        DateTimeOffset? exportedAt = null)
    {
        Directory.CreateDirectory(directory);
        var timestamp = exportedAt ?? DateTimeOffset.UtcNow;
        var normalizedProfile = NormalizeProfile(profileId) ?? "all";
        var entry = string.IsNullOrWhiteSpace(draft.Entry) ? "roi" : draft.Entry;
        var file = Path.Combine(
            directory,
            $"roi-draft-{TimestampForFile(timestamp)}-{SanitizeFilePart(normalizedProfile)}-{SanitizeFilePart(entry)}.json");
        var json = BuildJson(draft, profileId, timestamp);
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
        var text = string.IsNullOrWhiteSpace(value) ? "roi" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text;
    }
}
