using System.IO.Compression;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesCoinStabilityFrameInput(
    string FrameId,
    string ProfileId,
    int PassIndex,
    byte[] EncodedImage,
    string Source,
    string SourceKind,
    IReadOnlyList<MaaTaskRunResult> EvidenceTasks,
    IReadOnlyList<RhodesSuiCoinImageDetection> SavedDetections);

public static class RhodesCoinStabilityCorpus
{
    private static readonly HashSet<string> SupportedProfiles =
    [
        "is6ActiveCoinsFull",
        "is6CoinsFull",
    ];

    public static IReadOnlyList<RhodesCoinStabilityFrameInput> Discover(
        IEnumerable<string> roots)
    {
        var frames = new List<FrameCandidate>();
        var evidence = new List<EvidenceCandidate>();

        foreach (var root in roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(root)
                && Path.GetExtension(root).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                DiscoverArchive(root, frames, evidence);
                continue;
            }

            if (!Directory.Exists(root))
                continue;

            DiscoverFiles(root, frames, evidence);
            foreach (var archivePath in EnumerateFiles(root, "*.zip"))
                DiscoverArchive(archivePath, frames, evidence);
        }

        var evidenceByKey = evidence
            .GroupBy(item => FrameKey(item.FrameId, item.ProfileId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.CompletedAt)
                    .ThenBy(item => item.SourceKind.Equals("file", StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                    .First(),
                StringComparer.Ordinal);

        return frames
            .GroupBy(item => FrameKey(item.FrameId, item.ProfileId), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(item => item.SourceKind.Equals("file", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(item => item.Source, StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderBy(item => item.FrameId, StringComparer.Ordinal)
            .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
            .Select(frame =>
            {
                evidenceByKey.TryGetValue(FrameKey(frame.FrameId, frame.ProfileId), out var saved);
                return new RhodesCoinStabilityFrameInput(
                    frame.FrameId,
                    frame.ProfileId,
                    0,
                    frame.EncodedImage,
                    frame.Source,
                    frame.SourceKind,
                    saved?.Tasks ?? [],
                    saved?.SavedDetections ?? []);
            })
            .ToArray();
    }

    private static void DiscoverFiles(
        string root,
        ICollection<FrameCandidate> frames,
        ICollection<EvidenceCandidate> evidence)
    {
        foreach (var jsonPath in EnumerateFiles(root, "*.json"))
        {
            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(File.ReadAllBytes(jsonPath));
                if (TryReadFrameIdentity(document.RootElement, out var frameId, out var profileId)
                    && TryReadFrameImage(jsonPath, document.RootElement, frameId, out var image))
                {
                    frames.Add(new FrameCandidate(
                        frameId,
                        profileId,
                        image,
                        jsonPath,
                        "file"));
                }

                if (TryReadEvidence(document.RootElement, jsonPath, "file", out var savedEvidence))
                    evidence.Add(savedEvidence);
            }
            catch (JsonException)
            {
                // A report bundle can contain unrelated JSON. It is not part of the coin corpus.
            }
            catch (IOException)
            {
                // A file may disappear while a running debugger rotates its history.
            }
            finally
            {
                document?.Dispose();
            }
        }
    }

    private static void DiscoverArchive(
        string archivePath,
        ICollection<FrameCandidate> frames,
        ICollection<EvidenceCandidate> evidence)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entries = archive.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name))
                .ToArray();
            var entryByPath = entries
                .GroupBy(
                    entry => NormalizeEntryPath(entry.FullName),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries.Where(entry =>
                entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                JsonDocument? document = null;
                try
                {
                    using var stream = entry.Open();
                    document = JsonDocument.Parse(stream);
                    var source = $"{archivePath}#{entry.FullName}";
                    if (TryReadFrameIdentity(document.RootElement, out var frameId, out var profileId)
                        && TryReadArchiveFrameImage(
                            entry,
                            entryByPath,
                            document.RootElement,
                            frameId,
                            out var image))
                    {
                        frames.Add(new FrameCandidate(
                            frameId,
                            profileId,
                            image,
                            source,
                            "zip"));
                    }

                    if (TryReadEvidence(document.RootElement, source, "zip", out var savedEvidence))
                        evidence.Add(savedEvidence);
                }
                catch (JsonException)
                {
                    // Ignore unrelated or truncated JSON inside received reports.
                }
                catch (IOException)
                {
                    // Ignore one unreadable entry while retaining the rest of the report.
                }
                finally
                {
                    document?.Dispose();
                }
            }
        }
        catch (InvalidDataException)
        {
            // Not every ZIP under outputs is a RHODES report bundle.
        }
        catch (IOException)
        {
            // A report can still be in the process of being written.
        }
        catch (UnauthorizedAccessException)
        {
            // Discovery is best-effort and must not block analysis of the remaining roots.
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(
                root,
                pattern,
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false,
                });
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool TryReadFrameIdentity(
        JsonElement root,
        out string frameId,
        out string profileId)
    {
        frameId = StringProperty(root, "frameId");
        profileId = StringProperty(root, "profileId");
        return frameId.Length > 0 && SupportedProfiles.Contains(profileId);
    }

    private static bool TryReadFrameImage(
        string metadataPath,
        JsonElement metadata,
        string frameId,
        out byte[] image)
    {
        image = [];
        var candidates = new[]
        {
            Path.ChangeExtension(metadataPath, ".png"),
            Path.Combine(Path.GetDirectoryName(metadataPath) ?? "", $"frame-{frameId}.png"),
            StringProperty(metadata, "imagePath"),
        };
        var imagePath = candidates.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(imagePath))
            return false;

        try
        {
            image = File.ReadAllBytes(imagePath);
            return image.Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryReadArchiveFrameImage(
        ZipArchiveEntry metadataEntry,
        IReadOnlyDictionary<string, ZipArchiveEntry> entryByPath,
        JsonElement metadata,
        string frameId,
        out byte[] image)
    {
        image = [];
        var directory = NormalizeEntryPath(
            Path.GetDirectoryName(metadataEntry.FullName)?.Replace('\\', '/') ?? "");
        var baseName = Path.GetFileNameWithoutExtension(metadataEntry.Name);
        var metadataImageName = Path.GetFileName(StringProperty(metadata, "imagePath"));
        var candidatePaths = new[]
        {
            JoinEntryPath(directory, $"{baseName}.png"),
            JoinEntryPath(directory, $"frame-{frameId}.png"),
            JoinEntryPath(directory, metadataImageName),
        };
        var imageEntry = candidatePaths
            .Where(path => path.Length > 0)
            .Select(path => entryByPath.TryGetValue(path, out var entry) ? entry : null)
            .FirstOrDefault(entry => entry is not null);
        if (imageEntry is null)
            return false;

        using var source = imageEntry.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        image = buffer.ToArray();
        return image.Length > 0;
    }

    private static bool TryReadEvidence(
        JsonElement root,
        string source,
        string sourceKind,
        out EvidenceCandidate evidence)
    {
        evidence = default!;
        var profileId = StringProperty(root, "profileId");
        if (!SupportedProfiles.Contains(profileId)
            || !root.TryGetProperty("evidence", out var evidenceNode)
            || evidenceNode.ValueKind != JsonValueKind.Object
            || !evidenceNode.TryGetProperty("capture", out var capture)
            || capture.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var frameId = StringProperty(capture, "frameId");
        if (frameId.Length == 0
            || !evidenceNode.TryGetProperty("taskResults", out var taskResults)
            || taskResults.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var tasks = FirstPassTasks(profileId, taskResults);
        var savedDetections = tasks
            .Where(task => task.Entry.Equals(ExpectedImageEntry(profileId), StringComparison.Ordinal))
            .Select(task => RhodesSuiCoinImageRecognizer.TryRead(task, out _, out var detections)
                ? detections
                : [])
            .FirstOrDefault() ?? [];
        var completedAt = DateTimeOffset.TryParse(
            StringProperty(root, "completedAt"),
            out var parsedCompletedAt)
                ? parsedCompletedAt
                : DateTimeOffset.MinValue;
        evidence = new EvidenceCandidate(
            frameId,
            profileId,
            source,
            sourceKind,
            completedAt,
            tasks,
            savedDetections);
        return true;
    }

    private static IReadOnlyList<MaaTaskRunResult> FirstPassTasks(
        string profileId,
        JsonElement taskResults)
    {
        var expectedImageEntry = ExpectedImageEntry(profileId);
        var tasks = new List<MaaTaskRunResult>();
        foreach (var task in taskResults.EnumerateArray())
        {
            if (task.ValueKind != JsonValueKind.Object)
                continue;

            var parsed = new MaaTaskRunResult(
                StringProperty(task, "entry"),
                StringProperty(task, "status"),
                BooleanProperty(task, "succeeded"),
                StringProperty(task, "detail"),
                StringProperty(task, "recognitionDetailJson"),
                StringProperty(task, "algorithm"),
                BooleanProperty(task, "hit"));
            tasks.Add(parsed);
            if (parsed.Entry.Equals(expectedImageEntry, StringComparison.Ordinal))
                break;
        }
        return tasks;
    }

    private static string ExpectedImageEntry(string profileId) =>
        profileId.Equals("is6ActiveCoinsFull", StringComparison.Ordinal)
            ? RhodesSuiCoinImageRecognizer.ActiveEntry
            : RhodesSuiCoinImageRecognizer.OwnedEntry;

    private static string StringProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static bool BooleanProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();

    private static string FrameKey(string frameId, string profileId) =>
        $"{frameId}\u001f{profileId}";

    private static string NormalizeEntryPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string JoinEntryPath(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        return NormalizeEntryPath(
            string.IsNullOrWhiteSpace(directory) ? name : $"{directory}/{name}");
    }

    private sealed record FrameCandidate(
        string FrameId,
        string ProfileId,
        byte[] EncodedImage,
        string Source,
        string SourceKind);

    private sealed record EvidenceCandidate(
        string FrameId,
        string ProfileId,
        string Source,
        string SourceKind,
        DateTimeOffset CompletedAt,
        IReadOnlyList<MaaTaskRunResult> Tasks,
        IReadOnlyList<RhodesSuiCoinImageDetection> SavedDetections);
}
