using System.Text.Json;

namespace RhodesSuki.Services;

public static class RhodesFrameRecordStore
{
    private const int SchemaVersion = 1;
    private const int DefaultRetentionLimit = 40;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static async Task<RhodesFrameRecordResult> SaveAsync(
        RhodesFrameRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EncodedImage.Length == 0)
            return RhodesFrameRecordResult.Failed("encoded image is empty");

        var now = request.Now ?? DateTimeOffset.UtcNow;
        var frameDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(request.FrameDirectory)
            ? RhodesSukiDebugPaths.FrameRecordsDirectory
            : request.FrameDirectory);
        Directory.CreateDirectory(frameDirectory);

        var frameId = string.IsNullOrWhiteSpace(request.FrameId)
            ? $"{now.UtcDateTime:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}"
            : SanitizeFilePart(request.FrameId);
        var imagePath = Path.Combine(frameDirectory, $"frame-{frameId}.png");
        var metadataPath = Path.Combine(frameDirectory, $"frame-{frameId}.json");
        var stateSnapshotPath = Path.Combine(frameDirectory, $"frame-{frameId}-state.json");

        try
        {
            await File.WriteAllBytesAsync(imagePath, request.EncodedImage, cancellationToken);
            var stateSnapshotSaved = await TryCopyStateSnapshotAsync(request.StatePath, stateSnapshotPath, cancellationToken);
            if (!stateSnapshotSaved)
                stateSnapshotPath = "";

            var metadata = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = SchemaVersion,
                ["frameId"] = frameId,
                ["capturedAt"] = now.UtcDateTime.ToString("O"),
                ["profileId"] = request.ProfileId ?? "",
                ["profileLabel"] = request.ProfileLabel ?? "",
                ["source"] = request.Source ?? "",
                ["appVersion"] = request.AppVersion ?? "",
                ["runtimeSummary"] = request.RuntimeSummary ?? "",
                ["imagePath"] = imagePath,
                ["imageBytes"] = request.EncodedImage.Length,
                ["statePath"] = request.StatePath ?? "",
                ["stateSnapshotPath"] = stateSnapshotPath,
            };

            await File.WriteAllTextAsync(
                metadataPath,
                $"{JsonSerializer.Serialize(metadata, JsonOptions)}{Environment.NewLine}",
                cancellationToken);

            Prune(frameDirectory, Math.Max(1, request.RetentionLimit ?? DefaultRetentionLimit));
            return new RhodesFrameRecordResult(
                true,
                frameId,
                imagePath,
                metadataPath,
                stateSnapshotPath,
                "",
                request.EncodedImage.Length);
        }
        catch (Exception ex)
        {
            return RhodesFrameRecordResult.Failed(ex.Message);
        }
    }

    private static async Task<bool> TryCopyStateSnapshotAsync(
        string? statePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(statePath) || !File.Exists(statePath))
            return false;

        await using var source = File.OpenRead(statePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
        return true;
    }

    private static void Prune(string frameDirectory, int retentionLimit)
    {
        var metadataFiles = Directory.EnumerateFiles(frameDirectory, "frame-*.json")
            .Where(path => !path.EndsWith("-state.json", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ThenByDescending(info => info.Name, StringComparer.Ordinal)
            .Skip(retentionLimit)
            .ToArray();

        foreach (var metadata in metadataFiles)
            DeleteFrame(metadata.FullName);
    }

    private static void DeleteFrame(string metadataPath)
    {
        var basePath = metadataPath[..^Path.GetExtension(metadataPath).Length];
        foreach (var path in new[] { metadataPath, $"{basePath}.png", $"{basePath}-state.json" })
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Debug retention cleanup must not fail the capture path.
            }
        }
    }

    private static string SanitizeFilePart(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text;
    }
}

public sealed record RhodesFrameRecordRequest
{
    public string FrameDirectory { get; init; } = RhodesSukiDebugPaths.FrameRecordsDirectory;

    public byte[] EncodedImage { get; init; } = [];

    public string? StatePath { get; init; }

    public string? FrameId { get; init; }

    public string? ProfileId { get; init; }

    public string? ProfileLabel { get; init; }

    public string? Source { get; init; }

    public string? AppVersion { get; init; }

    public string? RuntimeSummary { get; init; }

    public DateTimeOffset? Now { get; init; }

    public int? RetentionLimit { get; init; }
}

public sealed record RhodesFrameRecordResult(
    bool Succeeded,
    string FrameId,
    string ImagePath,
    string MetadataPath,
    string StateSnapshotPath,
    string Error,
    long ImageBytes)
{
    public static RhodesFrameRecordResult Failed(string error)
    {
        return new RhodesFrameRecordResult(false, "", "", "", "", error, 0);
    }
}
