using System.IO.Compression;

namespace RhodesSuki.Services;

public static class RhodesBugReportImport
{
    public static async Task<RhodesBugReportImportResult> ImportAsync(
        string sourcePath,
        string destinationRoot = "",
        DateTimeOffset? now = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            return RhodesBugReportImportResult.Failed("取込元が空です。");

        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
            return RhodesBugReportImportResult.Failed($"取込元が見つかりません: {fullSourcePath}");

        try
        {
            var importRoot = Directory.Exists(fullSourcePath)
                ? fullSourcePath
                : await ExtractZipAsync(fullSourcePath, destinationRoot, now ?? DateTimeOffset.UtcNow, cancellationToken);
            var debugRoot = ResolveDebugRoot(importRoot);
            var frameRecordsDirectory = ResolveNamedDirectory(importRoot, debugRoot, RhodesSukiDebugPaths.FrameRecordsDirectoryName);
            var recognitionScansDirectory = ResolveNamedDirectory(importRoot, debugRoot, RhodesSukiDebugPaths.RecognitionScansDirectoryName);
            var manifestPath = ResolveManifestPath(importRoot, debugRoot);
            var frameCount = Directory.Exists(frameRecordsDirectory)
                ? RhodesFrameRecordHistory.LoadRecent(frameRecordsDirectory, limit: 200).Count
                : 0;

            if (frameCount <= 0)
            {
                return RhodesBugReportImportResult.Failed(
                    $"Frame Recordsが見つかりません: {frameRecordsDirectory}",
                    importRoot,
                    debugRoot,
                    frameRecordsDirectory,
                    recognitionScansDirectory,
                    manifestPath,
                    0);
            }

            return new RhodesBugReportImportResult(
                true,
                fullSourcePath,
                importRoot,
                debugRoot,
                frameRecordsDirectory,
                recognitionScansDirectory,
                manifestPath,
                frameCount,
                "",
                $"バグ報告を取り込みました: Frame {frameCount}件 / {frameRecordsDirectory}");
        }
        catch (Exception ex)
        {
            return RhodesBugReportImportResult.Failed(ex.Message);
        }
    }

    private static async Task<string> ExtractZipAsync(
        string zipPath,
        string destinationRoot,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(destinationRoot)
            ? RhodesSukiDebugPaths.BugReportsDirectory
            : destinationRoot);
        var importedRoot = Path.Combine(root, "imported");
        Directory.CreateDirectory(importedRoot);

        var name = SanitizeFilePart(Path.GetFileNameWithoutExtension(zipPath));
        var destination = Path.Combine(importedRoot, $"{now.UtcDateTime:yyyyMMdd-HHmmss}-{name}");
        Directory.CreateDirectory(destination);

        await using var stream = File.OpenRead(zipPath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            var targetPath = Path.GetFullPath(Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!targetPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"ZIP entryが展開先外を指しています: {entry.FullName}");

            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? destination);
            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(targetPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);
        }

        return destination;
    }

    private static string ResolveDebugRoot(string importRoot)
    {
        var candidates = new[]
        {
            Path.Combine(importRoot, "debug"),
            Path.Combine(importRoot, RhodesSukiDebugPaths.DebugLogDirectoryName),
            importRoot,
        };

        return candidates.FirstOrDefault(path =>
            Directory.Exists(Path.Combine(path, RhodesSukiDebugPaths.FrameRecordsDirectoryName))
            || Directory.Exists(Path.Combine(path, RhodesSukiDebugPaths.RecognitionScansDirectoryName)))
            ?? importRoot;
    }

    private static string ResolveNamedDirectory(string importRoot, string debugRoot, string directoryName)
    {
        var candidates = new[]
        {
            Path.Combine(debugRoot, directoryName),
            Path.Combine(importRoot, "debug", directoryName),
            Path.Combine(importRoot, RhodesSukiDebugPaths.DebugLogDirectoryName, directoryName),
            string.Equals(Path.GetFileName(importRoot), directoryName, StringComparison.OrdinalIgnoreCase)
                ? importRoot
                : Path.Combine(importRoot, directoryName),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static string ResolveManifestPath(string importRoot, string debugRoot)
    {
        var candidates = new[]
        {
            Path.Combine(importRoot, "manifest.json"),
            Path.Combine(debugRoot, "manifest.json"),
        };
        return candidates.FirstOrDefault(File.Exists) ?? "";
    }

    private static string SanitizeFilePart(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "bug-report" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
            text = text.Replace(invalid, '-');
        return text;
    }
}

public sealed record RhodesBugReportImportResult(
    bool Success,
    string SourcePath,
    string ImportedRoot,
    string DebugRoot,
    string FrameRecordsDirectory,
    string RecognitionScansDirectory,
    string ManifestPath,
    int FrameCount,
    string Error,
    string Message)
{
    public static RhodesBugReportImportResult Failed(
        string error,
        string importedRoot = "",
        string debugRoot = "",
        string frameRecordsDirectory = "",
        string recognitionScansDirectory = "",
        string manifestPath = "",
        int frameCount = 0)
    {
        return new RhodesBugReportImportResult(
            false,
            "",
            importedRoot,
            debugRoot,
            frameRecordsDirectory,
            recognitionScansDirectory,
            manifestPath,
            frameCount,
            error,
            string.IsNullOrWhiteSpace(error) ? "バグ報告の取込に失敗しました。" : error);
    }
}
