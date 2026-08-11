using System.Diagnostics;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMuMuCapabilityDetector
{
    private static readonly Version MinimumTouchVersion = new(6, 3, 2, 0);
    private static readonly string[] IpcRelativePaths =
    [
        Path.Combine("nx_main", "sdk", "external_renderer_ipc.dll"),
        Path.Combine("nx_device", "15.0", "shell", "sdk", "external_renderer_ipc.dll"),
        Path.Combine("nx_device", "12.0", "shell", "sdk", "external_renderer_ipc.dll"),
        Path.Combine("shell", "sdk", "external_renderer_ipc.dll"),
    ];

    public static async Task<RhodesMuMuCapabilitySnapshot> DetectAsync(
        SukiAdbConnectionSettings settings,
        string adbPath,
        string serial,
        IReadOnlyList<string>? processExecutablePaths = null,
        Func<IReadOnlyList<string>>? processExecutablePathProvider = null,
        Func<string, bool>? fileExists = null,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<RhodesAdbCommandResult>>? runCommandAsync = null,
        CancellationToken cancellationToken = default)
    {
        fileExists ??= File.Exists;
        runCommandAsync ??= RunCommandAsync;
        processExecutablePathProvider ??= EnumerateMuMuProcessPaths;

        var root = ResolveRoot(
            settings.EmulatorRoot,
            settings.EmulatorExecutablePath,
            adbPath,
            processExecutablePaths ?? [],
            fileExists);
        if (string.IsNullOrWhiteSpace(root) && processExecutablePaths is null)
        {
            processExecutablePaths = await Task.Run(processExecutablePathProvider, cancellationToken);
            root = ResolveRoot("", "", "", processExecutablePaths, fileExists);
        }
        if (string.IsNullOrWhiteSpace(root))
            return RhodesMuMuCapabilitySnapshot.NotDetected();

        var managerPath = Path.Combine(root, "nx_main", "MuMuManager.exe");
        var managerAvailable = fileExists(managerPath);
        var ipcPath = IpcRelativePaths
            .Select(relative => Path.Combine(root, relative))
            .FirstOrDefault(fileExists) ?? "";
        var screenshotAvailable = ipcPath.Length > 0;
        var versionText = "";
        Version? managerVersion = null;
        var versionDetail = managerAvailable ? "MuMuManager version未確認" : "MuMuManager.exeが見つかりません。";
        if (managerAvailable)
        {
            try
            {
                var result = await runCommandAsync(managerPath, ["version"], cancellationToken);
                if (result.Succeeded && TryParseManagerVersion(result.Output, out versionText, out managerVersion))
                    versionDetail = $"MuMuManager {versionText}";
                else
                    versionDetail = $"MuMuManager versionを解析できません: {Shorten(result.Detail, 120)}";
            }
            catch (Exception ex)
            {
                versionDetail = $"MuMuManager version取得失敗: {Shorten(ex.Message, 120)}";
            }
        }

        var touchAvailable = screenshotAvailable
            && managerVersion is not null
            && managerVersion >= MinimumTouchVersion;
        var instanceIndex = settings.MuMuBridgeConnectionEnabled
            ? settings.MuMuInstanceIndex
            : RhodesMaaAdbConnectionResolver.InferMuMuIndex(serial) ?? settings.MuMuInstanceIndex;
        var screenshotDetail = screenshotAvailable
            ? $"MuMu高速撮影を使用できます: {ipcPath}"
            : $"MuMu高速撮影は無効です: external_renderer_ipc.dllが見つかりません ({root})";
        var touchDetail = !screenshotAvailable
            ? "MuMu高速タッチは無効です: IPC DLLが見つかりません。"
            : touchAvailable
                ? $"MuMu高速タッチを使用できます: {versionDetail} / instance={instanceIndex}"
                : $"MuMu高速タッチは無効です: 6.3.2.0以上が必要です ({versionDetail})";

        return new RhodesMuMuCapabilitySnapshot(
            root,
            managerPath,
            ipcPath,
            versionText,
            screenshotAvailable,
            touchAvailable,
            Math.Clamp(instanceIndex, 0, 127),
            screenshotDetail,
            touchDetail);
    }

    internal static string ResolveRoot(
        string explicitRoot,
        string emulatorExecutablePath,
        string adbPath,
        IEnumerable<string> processExecutablePaths,
        Func<string, bool> fileExists)
    {
        var explicitCandidate = NormalizeDirectory(explicitRoot);
        if (explicitCandidate.Length > 0)
            return explicitCandidate;

        foreach (var sourcePath in new[] { emulatorExecutablePath, adbPath }.Concat(processExecutablePaths ?? []))
        {
            var candidate = InferRootFromFile(sourcePath, fileExists);
            if (candidate.Length > 0)
                return candidate;
        }
        return "";
    }

    private static string InferRootFromFile(string? filePath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(filePath) || filePath.Equals("adb", StringComparison.OrdinalIgnoreCase))
            return "";
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath.Trim());
        }
        catch
        {
            return "";
        }

        var directory = Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : new FileInfo(fullPath).Directory;
        var legacyRootCandidate = "";
        for (var depth = 0; directory is not null && depth < 7; depth++, directory = directory.Parent)
        {
            var root = directory.FullName;
            var hasManager = fileExists(Path.Combine(root, "nx_main", "MuMuManager.exe"));
            var hasModernIpc = IpcRelativePaths
                .Take(3)
                .Any(relative => fileExists(Path.Combine(root, relative)));
            if (hasManager || hasModernIpc)
            {
                return root;
            }

            var hasLegacyIpc = fileExists(Path.Combine(root, IpcRelativePaths[^1]));
            if (hasLegacyIpc && legacyRootCandidate.Length == 0)
                legacyRootCandidate = root;
        }
        return legacyRootCandidate;
    }

    private static string NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return "";
        }
    }

    private static bool TryParseManagerVersion(string json, out string versionText, out Version? version)
    {
        versionText = "";
        version = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("version", out var value))
                return false;
            versionText = value.GetString()?.Trim() ?? "";
            return Version.TryParse(versionText, out version);
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> EnumerateMuMuProcessPaths()
    {
        var result = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var processName = process.ProcessName;
                    if (!processName.Contains("MuMu", StringComparison.OrdinalIgnoreCase)
                        && !processName.Contains("Nemu", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var path = process.MainModule?.FileName ?? "";
                    if (path.Contains("MuMu", StringComparison.OrdinalIgnoreCase)
                        || path.Contains("Nemu", StringComparison.OrdinalIgnoreCase))
                        result.Add(path);
                }
                catch
                {
                    // 他ユーザーまたは昇格プロセスは読み取れないため候補外にする。
                }
            }
        }
        return result;
    }

    private static async Task<RhodesAdbCommandResult> RunCommandAsync(
        string executable,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        return new RhodesAdbCommandResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string Shorten(string? value, int maxLength)
    {
        var text = (value ?? "").Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
