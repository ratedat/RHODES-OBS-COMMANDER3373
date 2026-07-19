using System.IO.Compression;
using System.Security.Cryptography;

namespace RhodesSuki.Services;

/// <summary>
/// 配信サーバー専用のNode.jsを、アプリ実行フォルダ配下だけで導入・削除する。
/// MSIや管理者権限は使わず、Node.js公式ZIPを固定SHA-256で検証して展開する。
/// </summary>
public sealed class RhodesNodeRuntimeManager
{
    public const string NodeVersion = "24.18.0";
    public const string RuntimeDirectoryName = "nodejs-runtime";
    public const string DistributionDirectoryName = $"node-v{NodeVersion}-win-x64";
    public const string ArchiveFileName = $"{DistributionDirectoryName}.zip";
    public const string ArchiveSha256 = "0ae68406b42d7725661da979b1403ec9926da205c6770827f33aac9d8f26e821";

    private const long MaxArchiveBytes = 96L * 1024L * 1024L;
    private const long MaxExpandedBytes = 768L * 1024L * 1024L;
    private const int MaxArchiveEntries = 30_000;
    private static readonly Uri ArchiveUri = new($"https://nodejs.org/dist/v{NodeVersion}/{ArchiveFileName}");

    private readonly string _baseDirectory;
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public RhodesNodeRuntimeManager(string? baseDirectory = null)
    {
        _baseDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : baseDirectory);
    }

    public string ManagedRuntimeRoot => Path.Combine(_baseDirectory, RuntimeDirectoryName);

    public string ManagedInstallRoot => Path.Combine(ManagedRuntimeRoot, DistributionDirectoryName);

    public string ManagedNodeExecutablePath => Path.Combine(ManagedInstallRoot, "node.exe");

    public RhodesNodeRuntimeStatus Probe()
    {
        if (File.Exists(ManagedNodeExecutablePath))
        {
            return new RhodesNodeRuntimeStatus(
                "導入済み",
                $"Node.js v{NodeVersion} 管理版 / {ManagedNodeExecutablePath}",
                true,
                true,
                ManagedNodeExecutablePath);
        }

        var pathExecutable = FindNodeOnPath();
        if (!string.IsNullOrWhiteSpace(pathExecutable))
        {
            return new RhodesNodeRuntimeStatus(
                "PATH利用可",
                $"外部Node.jsを利用できます / {pathExecutable}",
                true,
                false,
                pathExecutable);
        }

        return new RhodesNodeRuntimeStatus(
            "未導入",
            $"配信サーバーにはNode.jsが必要です。管理版v{NodeVersion}をこの画面から導入できます。",
            false,
            false,
            "");
    }

    public string ResolveNodeExecutable()
    {
        return Probe().ExecutablePath;
    }

    public async Task<RhodesNodeRuntimeActionResult> InstallAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return Failure("管理版Node.jsはWindows x64配布でのみ利用できます。");

        var ownsClient = httpClient is null;
        httpClient ??= new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        try
        {
            using var response = await httpClient.GetAsync(
                ArchiveUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaxArchiveBytes)
                return Failure("Node.js配布ZIPが許容サイズを超えています。導入を中止しました。");

            await using var archiveStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await InstallArchiveAsync(archiveStream, ArchiveSha256, cancellationToken);
        }
        catch (Exception ex)
        {
            return Failure($"Node.jsのダウンロードに失敗しました: {ex.Message}");
        }
        finally
        {
            if (ownsClient)
                httpClient.Dispose();
        }
    }

    /// <summary>
    /// 検証済みアーカイブの導入境界。テストではネットワークを使わず、この境界へZIPを渡す。
    /// </summary>
    public async Task<RhodesNodeRuntimeActionResult> InstallArchiveAsync(
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
                return Failure("Node.js配布ZIPのSHA-256が一致しないため、導入を中止しました。");

            Directory.CreateDirectory(stagingRoot);
            ExtractArchiveSafely(archivePath, stagingRoot);
            var stagedDistribution = Path.Combine(stagingRoot, DistributionDirectoryName);
            var stagedExecutable = Path.Combine(stagedDistribution, "node.exe");
            if (!File.Exists(stagedExecutable))
                return Failure("Node.js配布ZIPにnode.exeが見つかりません。導入を中止しました。");

            if (Directory.Exists(ManagedInstallRoot))
                Directory.Delete(ManagedInstallRoot, recursive: true);
            Directory.Move(stagedDistribution, ManagedInstallRoot);

            var status = Probe();
            return new RhodesNodeRuntimeActionResult(
                true,
                status,
                $"Node.js v{NodeVersion}を導入しました。配信サーバーを起動できます。");
        }
        catch (Exception ex)
        {
            return Failure($"Node.jsの導入に失敗しました: {ex.Message}");
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingRoot);
            _installLock.Release();
        }
    }

    public async Task<RhodesNodeRuntimeActionResult> UninstallAsync(CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken);
        try
        {
            EnsureManagedRootIsOwned();
            if (Directory.Exists(ManagedRuntimeRoot))
                Directory.Delete(ManagedRuntimeRoot, recursive: true);

            var status = Probe();
            return new RhodesNodeRuntimeActionResult(
                true,
                status,
                status.IsAvailable
                    ? "管理版Node.jsを削除しました。引き続きPATH上のNode.jsを利用できます。"
                    : "管理版Node.jsを削除しました。");
        }
        catch (Exception ex)
        {
            return Failure($"管理版Node.jsを削除できませんでした: {ex.Message}");
        }
        finally
        {
            _installLock.Release();
        }
    }

    private RhodesNodeRuntimeActionResult Failure(string message)
    {
        return new RhodesNodeRuntimeActionResult(false, Probe(), message);
    }

    private void EnsureManagedRootIsOwned()
    {
        var expected = Path.GetFullPath(Path.Combine(_baseDirectory, RuntimeDirectoryName));
        var actual = Path.GetFullPath(ManagedRuntimeRoot);
        var expectedParent = Path.GetFullPath(_baseDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
            || !actual.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("管理対象外のNode.jsフォルダは操作できません。");
        }
    }

    private static async Task CopyArchiveWithLimitAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
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
                throw new InvalidDataException("Node.js配布ZIPが許容サイズを超えています。");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static bool ArchiveHashMatches(string archivePath, string expectedSha256)
    {
        var normalized = (expectedSha256 ?? "").Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            return false;

        using var stream = File.OpenRead(archivePath);
        var actual = SHA256.HashData(stream);
        var expected = Convert.FromHexString(normalized);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationRoot)
    {
        var destinationPrefix = Path.GetFullPath(destinationRoot).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxArchiveEntries)
            throw new InvalidDataException("Node.js配布ZIPの項目数が許容値を超えています。");

        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            expandedBytes += entry.Length;
            if (expandedBytes > MaxExpandedBytes)
                throw new InvalidDataException("Node.js配布ZIPの展開サイズが許容値を超えています。");

            var normalizedName = entry.FullName
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedName))
                throw new InvalidDataException("Node.js配布ZIPに絶対パスが含まれています。");

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedName));
            if (!destinationPath.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Node.js配布ZIPに展開先外のパスが含まれています。");

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

    private static string FindNodeOnPath()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = segment.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            try
            {
                var candidate = Path.Combine(directory, OperatingSystem.IsWindows() ? "node.exe" : "node");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch
            {
                // 不正なPATH断片は無視し、次候補を確認する。
            }
        }

        return "";
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 元の失敗理由を優先する。
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // 元の失敗理由を優先する。
        }
    }
}

public sealed record RhodesNodeRuntimeStatus(
    string State,
    string Detail,
    bool IsAvailable,
    bool IsManaged,
    string ExecutablePath);

public sealed record RhodesNodeRuntimeActionResult(
    bool Succeeded,
    RhodesNodeRuntimeStatus Status,
    string Message);
