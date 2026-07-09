using System.IO.Compression;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace RhodesSuki.Services;

public static class RhodesBugReportBundle
{
    private const long DefaultMaxFileBytes = 50L * 1024L * 1024L;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly HashSet<string> AllowedDebugExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json",
        ".log",
        ".md",
        ".png",
        ".jpg",
        ".jpeg",
        ".txt",
    };

    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        RhodesSukiDebugPaths.BugReportsDirectoryName,
        ".cache",
        "cache",
        "dist",
        "node_modules",
        "outputs",
        "runtimes",
    };

    public static async Task<RhodesBugReportBundleResult> CreateAsync(
        RhodesBugReportBundleRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = request.Now ?? DateTimeOffset.UtcNow;
        var debugRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(request.DebugLogDirectory)
            ? RhodesSukiDebugPaths.DebugLogDirectory
            : request.DebugLogDirectory);
        var destinationDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(request.DestinationDirectory)
            ? RhodesSukiDebugPaths.BugReportsDirectory
            : request.DestinationDirectory);

        Directory.CreateDirectory(debugRoot);
        Directory.CreateDirectory(destinationDirectory);

        var zipPath = Path.Combine(
            destinationDirectory,
            $"RHODES-OBS-COMMANDER3373-bug-report-{now.UtcDateTime:yyyyMMdd-HHmmss}-{Environment.ProcessId}.zip");
        var tempPath = $"{zipPath}.tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        var included = new List<string>();
        var skipped = new List<RhodesBugReportSkippedEntry>();
        var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using (var stream = File.Create(tempPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                foreach (var filePath in EnumerateDebugFiles(debugRoot, skipped))
                {
                    var relativePath = Path.GetRelativePath(debugRoot, filePath);
                    await AddFileAsync(
                        archive,
                        filePath,
                        $"debug/{NormalizeEntryPath(relativePath)}",
                        request.MaxFileBytes,
                        included,
                        skipped,
                        addedPaths,
                        enforceDebugWhitelist: true,
                        cancellationToken);
                }

                await AddFileAsync(
                    archive,
                    request.StatePath,
                    "state/current-state.json",
                    request.MaxFileBytes,
                    included,
                    skipped,
                    addedPaths,
                    enforceDebugWhitelist: false,
                    cancellationToken);
                await AddFileAsync(
                    archive,
                    request.SettingsPath,
                    "state/suki-settings.json",
                    request.MaxFileBytes,
                    included,
                    skipped,
                    addedPaths,
                    enforceDebugWhitelist: false,
                    cancellationToken);
                await AddFileAsync(
                    archive,
                    request.LatestCapturePath,
                    string.IsNullOrWhiteSpace(request.LatestCapturePath)
                        ? "captures/latest.png"
                        : $"captures/latest-{Path.GetFileName(request.LatestCapturePath)}",
                    request.MaxFileBytes,
                    included,
                    skipped,
                    addedPaths,
                    enforceDebugWhitelist: true,
                    cancellationToken);
                await AddFileAsync(
                    archive,
                    request.LatestRecognitionLogPath,
                    string.IsNullOrWhiteSpace(request.LatestRecognitionLogPath)
                        ? "recognition/latest.json"
                        : $"recognition/latest-{Path.GetFileName(request.LatestRecognitionLogPath)}",
                    request.MaxFileBytes,
                    included,
                    skipped,
                    addedPaths,
                    enforceDebugWhitelist: true,
                    cancellationToken);

                await AddResourceDefinitionFilesAsync(
                    archive,
                    request.MaxFileBytes,
                    included,
                    skipped,
                    addedPaths,
                    cancellationToken);

                AddTextEntry(
                    archive,
                    "manifest.json",
                    BuildManifest(request, now, debugRoot, included, skipped));
                AddTextEntry(
                    archive,
                    "README.txt",
                    "公開デバッグ用バグ報告ZIPです。\n"
                    + "GLM/Ollama本体、モデル、巨大キャッシュ、dist類は含めていません。\n"
                    + "保存済み recognition-*.json とスクリーンショットから、ADBなしで候補・ROI・反映差分を再確認できます。\n");
            }

            File.Move(tempPath, zipPath, true);
            var info = new FileInfo(zipPath);
            return RhodesBugReportBundleResult.Succeeded(
                zipPath,
                info.Exists ? info.Length : 0,
                included,
                skipped);
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            return RhodesBugReportBundleResult.Failed(ex.Message, included, skipped);
        }
    }

    private static IEnumerable<string> EnumerateDebugFiles(
        string directory,
        ICollection<RhodesBugReportSkippedEntry> skipped)
    {
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch (Exception ex)
        {
            skipped.Add(new RhodesBugReportSkippedEntry(directory, $"enumerate-files-failed: {ex.Message}"));
            yield break;
        }

        foreach (var file in files)
            yield return file;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory);
        }
        catch (Exception ex)
        {
            skipped.Add(new RhodesBugReportSkippedEntry(directory, $"enumerate-directories-failed: {ex.Message}"));
            yield break;
        }

        foreach (var child in directories)
        {
            var name = Path.GetFileName(child);
            if (ShouldSkipDirectory(name))
            {
                skipped.Add(new RhodesBugReportSkippedEntry(child, "excluded-directory"));
                continue;
            }

            foreach (var file in EnumerateDebugFiles(child, skipped))
                yield return file;
        }
    }

    private static bool ShouldSkipDirectory(string directoryName)
    {
        if (ExcludedDirectoryNames.Contains(directoryName))
            return true;

        return directoryName.Contains("glm", StringComparison.OrdinalIgnoreCase)
            || directoryName.Contains("ollama", StringComparison.OrdinalIgnoreCase)
            || directoryName.Equals("models", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AddFileAsync(
        ZipArchive archive,
        string? filePath,
        string entryName,
        long maxFileBytes,
        ICollection<string> included,
        ICollection<RhodesBugReportSkippedEntry> skipped,
        ISet<string> addedPaths,
        bool enforceDebugWhitelist,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            skipped.Add(new RhodesBugReportSkippedEntry(fullPath, "missing"));
            return;
        }

        if (!addedPaths.Add(fullPath))
            return;

        var extension = Path.GetExtension(fullPath);
        if (enforceDebugWhitelist && !AllowedDebugExtensions.Contains(extension))
        {
            skipped.Add(new RhodesBugReportSkippedEntry(fullPath, $"excluded-extension:{extension}"));
            return;
        }

        var info = new FileInfo(fullPath);
        var maxBytes = maxFileBytes > 0 ? maxFileBytes : DefaultMaxFileBytes;
        if (info.Length > maxBytes)
        {
            skipped.Add(new RhodesBugReportSkippedEntry(fullPath, $"too-large:{info.Length}"));
            return;
        }

        var entry = archive.CreateEntry(NormalizeEntryPath(entryName), CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var fileStream = File.OpenRead(fullPath);
        await fileStream.CopyToAsync(entryStream, cancellationToken);
        included.Add(NormalizeEntryPath(entryName));
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(NormalizeEntryPath(entryName), CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }

    private static async Task AddResourceDefinitionFilesAsync(
        ZipArchive archive,
        long maxFileBytes,
        ICollection<string> included,
        ICollection<RhodesBugReportSkippedEntry> skipped,
        ISet<string> addedPaths,
        CancellationToken cancellationToken)
    {
        foreach (var file in DefaultResourceFiles())
        {
            await AddFileAsync(
                archive,
                file.Path,
                file.EntryName,
                maxFileBytes,
                included,
                skipped,
                addedPaths,
                enforceDebugWhitelist: false,
                cancellationToken);
        }
    }

    private static string BuildManifest(
        RhodesBugReportBundleRequest request,
        DateTimeOffset now,
        string debugRoot,
        IReadOnlyCollection<string> included,
        IReadOnlyCollection<RhodesBugReportSkippedEntry> skipped)
    {
        var manifest = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["generatedAt"] = now.UtcDateTime.ToString("O"),
            ["debugRoot"] = debugRoot,
            ["baseResolution"] = "1280x720 (16:9)",
            ["ocrDefault"] = "MAA-OCR",
            ["optionalOcr"] = "GLM-OCR/Ollama",
            ["distributionShell"] = "Avalonia/Suki",
            ["appInformationalVersion"] = typeof(RhodesBugReportBundle).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(RhodesBugReportBundle).Assembly.GetName().Version?.ToString()
                ?? "",
            ["dotnetRuntime"] = RuntimeInformation.FrameworkDescription,
            ["osArchitecture"] = RuntimeInformation.OSArchitecture.ToString(),
            ["currentCulture"] = CultureInfo.CurrentCulture.Name,
            ["publicDebugCampaign"] = RhodesPublicDebugPolicy.SarkazCampaignId,
            ["publicDebugProfiles"] = RhodesPublicDebugPolicy.ProfileIds,
            ["resourceHashes"] = BuildResourceHashes(),
            ["retainedRecognitionTargets"] = new[]
            {
                "originium-ingot",
                "difficulty",
                "squad",
                "is-special-values",
                "operators",
                "relics",
            },
            ["abandonedRunFields"] = RhodesMaaRecognitionPolicy.AbandonedRunFields.OrderBy(value => value).ToArray(),
            ["includedEntryCount"] = included.Count,
            ["skippedEntryCount"] = skipped.Count,
            ["includedEntries"] = included.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["skippedEntries"] = skipped.ToArray(),
        };

        foreach (var pair in request.Metadata)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
                manifest[$"context.{pair.Key}"] = pair.Value;
        }

        return $"{JsonSerializer.Serialize(manifest, JsonOptions)}{Environment.NewLine}";
    }

    private static SortedDictionary<string, string> BuildResourceHashes()
    {
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in DefaultResourceFiles())
        {
            hashes[file.EntryName] = File.Exists(file.Path)
                ? ComputeSha256(file.Path)
                : "missing";
        }

        return hashes;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<RhodesBugReportResourceFile> DefaultResourceFiles()
    {
        return
        [
            new(
                Path.Combine(AppContext.BaseDirectory, "interface.json"),
                "resource/interface.json"),
            new(
                Path.Combine(AppContext.BaseDirectory, "resource", "base", "pipeline", "rhodes.json"),
                "resource/pipeline/rhodes.json"),
            new(
                Path.Combine(AppContext.BaseDirectory, "resource", "base", "pipeline", "rhodes-generated.json"),
                "resource/pipeline/rhodes-generated.json"),
            new(
                Path.Combine(AppContext.BaseDirectory, "data", "recognition", "maa-tasks.json"),
                "resource/recognition/maa-tasks.json"),
            new(
                Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json"),
                "resource/recognition/scan-profiles.json"),
        ];
    }

    private static string NormalizeEntryPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}

public sealed record RhodesBugReportBundleRequest
{
    public string DebugLogDirectory { get; init; } = RhodesSukiDebugPaths.DebugLogDirectory;

    public string DestinationDirectory { get; init; } = RhodesSukiDebugPaths.BugReportsDirectory;

    public string? StatePath { get; init; }

    public string? SettingsPath { get; init; }

    public string? LatestCapturePath { get; init; }

    public string? LatestRecognitionLogPath { get; init; }

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    public long MaxFileBytes { get; init; } = 50L * 1024L * 1024L;

    public DateTimeOffset? Now { get; init; }
}

public sealed record RhodesBugReportBundleResult(
    bool Success,
    string ZipPath,
    long ZipBytes,
    IReadOnlyList<string> IncludedEntries,
    IReadOnlyList<RhodesBugReportSkippedEntry> SkippedEntries,
    string Message)
{
    public static RhodesBugReportBundleResult Succeeded(
        string zipPath,
        long zipBytes,
        IReadOnlyList<string> includedEntries,
        IReadOnlyList<RhodesBugReportSkippedEntry> skippedEntries)
    {
        return new RhodesBugReportBundleResult(
            true,
            zipPath,
            zipBytes,
            includedEntries,
            skippedEntries,
            $"バグ報告ZIPを作成しました: {zipPath}");
    }

    public static RhodesBugReportBundleResult Failed(
        string message,
        IReadOnlyList<string> includedEntries,
        IReadOnlyList<RhodesBugReportSkippedEntry> skippedEntries)
    {
        return new RhodesBugReportBundleResult(
            false,
            "",
            0,
            includedEntries,
            skippedEntries,
            string.IsNullOrWhiteSpace(message) ? "バグ報告ZIPの作成に失敗しました。" : message);
    }
}

public sealed record RhodesBugReportSkippedEntry(string Path, string Reason);

internal sealed record RhodesBugReportResourceFile(string Path, string EntryName);
