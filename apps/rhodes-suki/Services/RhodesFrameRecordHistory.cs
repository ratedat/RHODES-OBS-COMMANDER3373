using System.Text.Json;

namespace RhodesSuki.Services;

public static class RhodesFrameRecordHistory
{
    public static IReadOnlyList<RhodesFrameRecordHistoryItem> LoadRecent(
        string directory,
        IEnumerable<string>? extraMetadataPaths = null,
        int limit = 24)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(directory))
        {
            foreach (var path in Directory.EnumerateFiles(directory, "frame-*.json"))
            {
                if (!path.EndsWith("-state.json", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }
        }

        foreach (var path in extraMetadataPaths ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                paths.Add(path);
        }

        return paths
            .Select(TryLoad)
            .Where(item => item is not null)
            .Cast<RhodesFrameRecordHistoryItem>()
            .OrderByDescending(item => item.SortTimestamp)
            .ThenBy(item => item.MetadataPath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, limit))
            .ToArray();
    }

    private static RhodesFrameRecordHistoryItem? TryLoad(string metadataPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var capturedAt = JsonString(root, "capturedAt");
            var observedAt = ParseTimestamp(capturedAt)
                ?? new DateTimeOffset(File.GetLastWriteTimeUtc(metadataPath), TimeSpan.Zero);
            var imagePath = JsonString(root, "imagePath");
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                var basePath = metadataPath[..^Path.GetExtension(metadataPath).Length];
                imagePath = $"{basePath}.png";
            }

            var profileId = JsonString(root, "profileId");
            var profileLabel = JsonString(root, "profileLabel");
            var source = JsonString(root, "source");
            var runtimeSummary = JsonString(root, "runtimeSummary");
            var detail = string.Join(" / ", new[] { profileLabel, profileId, source, runtimeSummary }
                .Where(value => !string.IsNullOrWhiteSpace(value)));

            return new RhodesFrameRecordHistoryItem(
                FrameId: JsonString(root, "frameId"),
                CapturedAt: capturedAt,
                CapturedAtLabel: observedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ProfileId: profileId,
                ProfileLabel: profileLabel,
                Source: source,
                RuntimeSummary: runtimeSummary,
                ImagePath: imagePath,
                MetadataPath: metadataPath,
                StateSnapshotPath: JsonString(root, "stateSnapshotPath"),
                ImageBytes: JsonLong(root, "imageBytes"),
                Detail: string.IsNullOrWhiteSpace(detail) ? metadataPath : detail,
                SortTimestamp: observedAt);
        }
        catch
        {
            return null;
        }
    }

    private static string JsonString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static long JsonLong(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var result)
            ? result
            : 0;
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, out var result) ? result : null;
    }
}

public sealed record RhodesFrameRecordHistoryItem(
    string FrameId,
    string CapturedAt,
    string CapturedAtLabel,
    string ProfileId,
    string ProfileLabel,
    string Source,
    string RuntimeSummary,
    string ImagePath,
    string MetadataPath,
    string StateSnapshotPath,
    long ImageBytes,
    string Detail,
    DateTimeOffset SortTimestamp);
