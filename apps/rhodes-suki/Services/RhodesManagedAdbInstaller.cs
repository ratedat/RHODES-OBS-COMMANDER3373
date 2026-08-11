using System.IO.Compression;
using System.Security.Cryptography;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed class RhodesManagedAdbInstaller
{
    public const string PlatformToolsVersion = "37.0.1";
    public const string RuntimeDirectoryName = "adb-runtime";
    public const string DistributionDirectoryName = "platform-tools";
    public const string ArchiveFileName = "platform-tools-latest-windows.zip";

    // Google公式固定バージョンZIPの値。更新時はURLとSHA-256を同時に更新する。
    public const string ArchiveSha256 = "45f4d63113e895ebde0c90f194099a4676b6ac653bd28d54314a9e022bbc1a99";

    private const long MaxArchiveBytes = 64L * 1024L * 1024L;
    private const long MaxExpandedBytes = 256L * 1024L * 1024L;
    private const int MaxArchiveEntries = 10_000;
    private static readonly Uri ArchiveUri = new($"https://dl.google.com/android/repository/{ArchiveFileName}");
    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public RhodesManagedAdbInstaller(string? baseDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory);
    }

    public string ManagedRuntimeRoot => Path.Combine(_baseDirectory, RuntimeDirectoryName);

    public string ManagedInstallRoot => Path.Combine(ManagedRuntimeRoot, $"platform-tools-{PlatformToolsVersion}");

    public string ManagedAdbExecutablePath => Path.Combine(ManagedInstallRoot, "adb.exe");

    public async Task<RhodesManagedAdbInstallResult> InstallAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return Failure("管理ADBはWindows配布でのみ利用できます。");
        if (ArchiveSha256.Length != 64)
            return Failure("管理ADBの固定SHA-256が未設定のため、安全のため導入を拒否しました。");

        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        try
        {
            using var response = await httpClient.GetAsync(ArchiveUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxArchiveBytes)
                return Failure("Platform Tools ZIPが許容サイズを超えています。");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await InstallArchiveAsync(stream, ArchiveSha256, cancellationToken);
        }
        catch (Exception ex)
        {
            return Failure($"管理ADBのダウンロードに失敗しました: {ex.Message}");
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    public async Task<RhodesManagedAdbInstallResult> InstallArchiveAsync(
        Stream archiveStream,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken);
        var archivePath = "";
        var stagingRoot = "";
        try
        {
            EnsureManagedRootIsOwned();
            Directory.CreateDirectory(ManagedRuntimeRoot);
            archivePath = Path.Combine(ManagedRuntimeRoot, $".download-{Guid.NewGuid():N}.zip");
            stagingRoot = Path.Combine(ManagedRuntimeRoot, $".install-{Guid.NewGuid():N}");
            await CopyArchiveWithLimitAsync(archiveStream, archivePath, cancellationToken);
            if (!ArchiveHashMatches(archivePath, expectedSha256))
                return Failure("Platform Tools ZIPのSHA-256が一致しないため導入を中止しました。");

            Directory.CreateDirectory(stagingRoot);
            ExtractArchiveSafely(archivePath, stagingRoot);
            var stagedRoot = Path.Combine(stagingRoot, DistributionDirectoryName);
            var stagedAdb = Path.Combine(stagedRoot, "adb.exe");
            if (!File.Exists(stagedAdb))
                return Failure("Platform Tools ZIPにplatform-tools/adb.exeが見つかりません。");

            if (Directory.Exists(ManagedInstallRoot))
                Directory.Delete(ManagedInstallRoot, recursive: true);
            Directory.Move(stagedRoot, ManagedInstallRoot);
            return new RhodesManagedAdbInstallResult(
                true,
                ManagedAdbExecutablePath,
                $"Google Platform Tools {PlatformToolsVersion}をRHODES管理領域へ導入しました。エミュレーター同梱ADBは変更していません。");
        }
        catch (Exception ex)
        {
            return Failure($"管理ADBの導入に失敗しました: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingRoot);
            _installLock.Release();
        }
    }

    private RhodesManagedAdbInstallResult Failure(string detail) => new(false, "", detail);

    private void EnsureManagedRootIsOwned()
    {
        var expected = Path.GetFullPath(Path.Combine(_baseDirectory, RuntimeDirectoryName));
        var actual = Path.GetFullPath(ManagedRuntimeRoot);
        var parentPrefix = _baseDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
            || !actual.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("RHODES管理領域外へADBを導入できません。");
        }
    }

    private static async Task CopyArchiveWithLimitAsync(Stream source, string destinationPath, CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            total += read;
            if (total > MaxArchiveBytes)
                throw new InvalidDataException("Platform Tools ZIPが許容サイズを超えています。");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static bool ArchiveHashMatches(string archivePath, string expectedSha256)
    {
        var normalized = (expectedSha256 ?? "").Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            return false;
        using var stream = File.OpenRead(archivePath);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(stream),
            Convert.FromHexString(normalized));
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationRoot)
    {
        var prefix = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException("Platform Tools ZIPの項目数が許容値を超えています。");
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            expandedBytes += entry.Length;
            if (expandedBytes > MaxExpandedBytes)
                throw new InvalidDataException("Platform Tools ZIPの展開サイズが許容値を超えています。");
            var normalizedName = entry.FullName
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedName))
                throw new InvalidDataException("Platform Tools ZIPに絶対パスが含まれています。");
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedName));
            if (!destinationPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Platform Tools ZIPに展開先外のパスが含まれています。");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using var input = entry.Open();
            using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (path.Length > 0 && File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (path.Length > 0 && Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch { }
    }
}
