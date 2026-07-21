using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbPresetCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> DefaultSerialsByPreset = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["bluestacks"] = ["127.0.0.1:5555", "127.0.0.1:5556", "127.0.0.1:5565", "127.0.0.1:5575", "127.0.0.1:5585", "127.0.0.1:5595", "127.0.0.1:5554"],
        ["mumu"] = ["127.0.0.1:16384", "127.0.0.1:16416", "127.0.0.1:7555", "127.0.0.1:16448", "127.0.0.1:16480", "127.0.0.1:16512", "127.0.0.1:16544", "127.0.0.1:16576"],
        ["ldplayer"] = ["emulator-5554", "emulator-5556", "emulator-5558", "emulator-5560", "127.0.0.1:5555", "127.0.0.1:5557", "127.0.0.1:5559", "127.0.0.1:5561"],
        ["nox"] = ["127.0.0.1:62001", "127.0.0.1:59865"],
        ["xyaz"] = ["127.0.0.1:21503"],
        ["tencent"] = ["127.0.0.1:5555"],
        ["wsa"] = ["127.0.0.1:58526"],
        ["google-play-games-dev"] = ["127.0.0.1:6520"],
    };

    public static IReadOnlyList<MaaAdbPresetPreview> DefaultPresets()
    {
        return
        [
            new MaaAdbPresetPreview(
                "auto",
                "自動 / PATH adb",
                "PATH上のadbを使います。serialは空欄のまま接続済み端末を使います。",
                "adb",
                ""),
            new MaaAdbPresetPreview(
                "mumu",
                "MuMu Player",
                "MuMu Player 12向けです。多重起動時はMuMu側のADBポートを確認してください。",
                FirstExistingOrFallback(MuMuAdbPathCandidates(), "adb"),
                "127.0.0.1:16384"),
            new MaaAdbPresetPreview(
                "google-play-games-dev",
                "Google Play Games 開発者",
                "Google Play Games開発者エミュレーター向けです。Hyper-VとGoogleログインが必要です。",
                FirstExistingOrFallback(GooglePlayGamesAdbPathCandidates(), "adb"),
                "127.0.0.1:6520"),
            new MaaAdbPresetPreview(
                "avd",
                "Android Studio AVD",
                "Android SDK platform-toolsのadbを優先します。",
                FirstExistingOrFallback(AndroidSdkAdbPathCandidates(), "adb"),
                "emulator-5554"),
            new MaaAdbPresetPreview(
                "bluestacks",
                "BlueStacks",
                "BlueStacks_nxtのHD-Adb.exeを優先します。多重起動時は端末候補から選んでください。",
                FirstExistingOrFallback(BlueStacksAdbPathCandidates(), "adb"),
                "127.0.0.1:5555"),
            new MaaAdbPresetPreview(
                "ldplayer",
                "LDPlayer",
                "LDPlayer 9のadb.exeを優先します。",
                FirstExistingOrFallback(LdPlayerAdbPathCandidates(), "adb"),
                "emulator-5554"),
            new MaaAdbPresetPreview(
                "nox",
                "NoxPlayer",
                "Noxのnox_adb.exeを優先します。",
                FirstExistingOrFallback(NoxAdbPathCandidates(), "adb"),
                "127.0.0.1:62001"),
            new MaaAdbPresetPreview(
                "xyaz",
                "MEmu / 逍遥",
                "MEmu / Microvirt系のadb.exeを優先します。",
                FirstExistingOrFallback(MEmuAdbPathCandidates(), "adb"),
                "127.0.0.1:21503"),
            new MaaAdbPresetPreview(
                "wsa",
                "Windows Subsystem for Android",
                "WSAの既定ADBポートを使います。",
                "adb",
                "127.0.0.1:58526"),
            new MaaAdbPresetPreview(
                "custom",
                "手動",
                "ADBパスとserialを手動入力します。",
                "adb",
                ""),
        ];
    }

    public static IReadOnlyList<MaaAdbPathCandidatePreview> CandidatePaths(string adbPath = "", string selectedPresetId = "auto")
    {
        var candidates = new List<MaaAdbPathCandidatePreview>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PushCandidate(candidates, seen, adbPath, "settings", selectedPresetId);
        PushCandidate(candidates, seen, Environment.GetEnvironmentVariable("ARKNIGHTS_ADB_PATH"), "env", "custom");

        foreach (var candidate in RunningMuMuAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "process", "mumu");
        foreach (var candidate in RegisteredMuMuAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "registry", "mumu");
        foreach (var candidate in AndroidSdkAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "avd");
        foreach (var candidate in MuMuAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "mumu");
        foreach (var candidate in BlueStacksAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "bluestacks");
        foreach (var candidate in LdPlayerAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "ldplayer");
        foreach (var candidate in NoxAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "nox");
        foreach (var candidate in MEmuAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "xyaz");
        foreach (var candidate in TencentAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "tencent");
        foreach (var candidate in GooglePlayGamesAdbPathCandidates())
            PushCandidate(candidates, seen, candidate, "known-path", "google-play-games-dev");

        PushCandidate(candidates, seen, "adb", "path", "custom");
        return candidates;
    }

    public static IReadOnlyList<string> DefaultSerials(string presetId)
    {
        return DefaultSerialsByPreset.TryGetValue(presetId, out var serials)
            ? serials
            : [];
    }

    private static IEnumerable<string> MuMuAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
        {
            foreach (var relativeRoot in MuMuInstallDirectoryNames)
            {
                foreach (var candidate in MuMuAdbPathsFromInstallRoot(Path.Combine(root, relativeRoot)))
                    yield return candidate;
            }
        }
    }

    internal static IReadOnlyList<string> MuMuAdbPathsFromInstallRoot(string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return [];

        var root = installRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return
        [
            Path.Combine(root, "nx_main", "adb.exe"),
            Path.Combine(root, "shell", "adb.exe"),
            .. MuMuNxDeviceShellAdbPaths(root),
            Path.Combine(root, "nx_device", "12.0", "vmonitor", "bin", "adb_server.exe"),
            Path.Combine(root, "nx_device", "MuMu", "emulator", "nemu", "vmonitor", "bin", "adb_server.exe"),
        ];
    }

    private static IEnumerable<string> MuMuNxDeviceShellAdbPaths(string installRoot)
    {
        var nxDeviceRoot = Path.Combine(installRoot, "nx_device");
        if (!Directory.Exists(nxDeviceRoot))
        {
            yield return Path.Combine(nxDeviceRoot, "15.0", "shell", "adb.exe");
            yield return Path.Combine(nxDeviceRoot, "12.0", "shell", "adb.exe");
            yield break;
        }

        IEnumerable<string> versions;
        try
        {
            versions = Directory.EnumerateDirectories(nxDeviceRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name) && name.EndsWith(".0", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()!;
        }
        catch
        {
            versions = ["15.0", "12.0"];
        }

        foreach (var version in versions)
            yield return Path.Combine(nxDeviceRoot, version, "shell", "adb.exe");
    }

    internal static string ResolveMuMuInstallRootFromProcessPath(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return "";

        try
        {
            var executable = new FileInfo(processPath.Trim());
            var directory = executable.Directory;
            if (directory is null)
                return "";

            if (executable.Name.Equals("MuMuNxDevice.exe", StringComparison.OrdinalIgnoreCase))
                return directory.Parent?.Parent?.Parent?.FullName ?? "";
            if (executable.Name.Equals("MuMuPlayer.exe", StringComparison.OrdinalIgnoreCase))
                return directory.Parent?.FullName ?? "";
        }
        catch
        {
            return "";
        }

        return "";
    }

    internal static IReadOnlyList<string> MuMuAdbPathsFromProcessPath(string? processPath)
    {
        var installRoot = ResolveMuMuInstallRootFromProcessPath(processPath);
        if (string.IsNullOrWhiteSpace(installRoot) || string.IsNullOrWhiteSpace(processPath))
            return [];

        var executableName = Path.GetFileName(processPath.Trim());
        if (executableName.Equals("MuMuNxDevice.exe", StringComparison.OrdinalIgnoreCase))
        {
            var processDirectory = Path.GetDirectoryName(processPath.Trim()) ?? "";
            return
            [
                Path.Combine(processDirectory, "adb.exe"),
                Path.Combine(installRoot, "nx_main", "adb.exe"),
                Path.Combine(installRoot, "shell", "adb.exe"),
                .. MuMuNxDeviceShellAdbPaths(installRoot),
                Path.Combine(installRoot, "nx_device", "MuMu", "emulator", "nemu", "vmonitor", "bin", "adb_server.exe"),
            ];
        }

        if (executableName.Equals("MuMuPlayer.exe", StringComparison.OrdinalIgnoreCase))
            return [Path.Combine(installRoot, "shell", "adb.exe")];

        return [];
    }

    internal static string ResolveMuMuInstallRootFromUninstallString(string? uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return "";

        var match = Regex.Match(
            uninstallString.Trim(),
            "^\\\"?(.*?)[\\\\/]uninstall\\.exe\\\"?(?:\\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private static IEnumerable<string> RunningMuMuAdbPathCandidates()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        foreach (var processName in new[] { "MuMuNxDevice", "MuMuPlayer" })
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    string processPath;
                    try
                    {
                        processPath = process.MainModule?.FileName ?? "";
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var candidate in MuMuAdbPathsFromProcessPath(processPath))
                        yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<string> RegisteredMuMuAdbPathCandidates()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        foreach (var installRoot in RegisteredMuMuInstallRoots())
        {
            foreach (var candidate in MuMuAdbPathsFromInstallRoot(installRoot))
                yield return candidate;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> RegisteredMuMuInstallRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var keyPath in MuMuUninstallRegistryKeys)
                    {
                        using var key = baseKey.OpenSubKey(keyPath);
                        if (key is null)
                            continue;

                        var installRoot = (key.GetValue("InstallLocation") as string)?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(installRoot))
                            installRoot = ResolveMuMuInstallRootFromUninstallString(key.GetValue("UninstallString") as string);
                        if (!string.IsNullOrWhiteSpace(installRoot) && seen.Add(installRoot))
                            result.Add(installRoot);
                    }
                }
                catch
                {
                    // Registry access can be denied on managed PCs; known paths and process detection remain available.
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> BlueStacksAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
        {
            yield return Path.Combine(root, "BlueStacks_nxt", "HD-Adb.exe");
            yield return Path.Combine(root, "BlueStacks_nxt", "Engine", "ProgramFiles", "HD-Adb.exe");
        }
    }

    private static IEnumerable<string> LdPlayerAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
            yield return Path.Combine(root, "LDPlayer", "LDPlayer9", "adb.exe");
    }

    private static IEnumerable<string> NoxAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
        {
            yield return Path.Combine(root, "Nox", "bin", "nox_adb.exe");
            yield return Path.Combine(root, "Nox", "bin", "adb.exe");
        }
    }

    private static IEnumerable<string> MEmuAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
            yield return Path.Combine(root, "Microvirt", "MEmu", "adb.exe");
    }

    private static IEnumerable<string> TencentAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
            yield return Path.Combine(root, "Tencent", "Androws", "Application", "adb.exe");
    }

    private static IEnumerable<string> GooglePlayGamesAdbPathCandidates()
    {
        foreach (var root in ProgramInstallRoots())
        {
            yield return Path.Combine(root, "Google", "Play Games Developer Emulator", "current", "emulator", "adb.exe");
            yield return Path.Combine(root, "Google", "Play Games", "current", "emulator", "adb.exe");
        }

        foreach (var candidate in AndroidSdkAdbPathCandidates())
        {
            yield return candidate;
        }
    }

    private static IEnumerable<string> AndroidSdkAdbPathCandidates()
    {
        var androidHome = Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (!string.IsNullOrWhiteSpace(androidHome))
            yield return Path.Combine(androidHome, "platform-tools", "adb.exe");

        var androidSdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT");
        if (!string.IsNullOrWhiteSpace(androidSdkRoot))
            yield return Path.Combine(androidSdkRoot, "platform-tools", "adb.exe");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe");
    }

    private static IEnumerable<string> ProgramInstallRoots()
    {
        var roots = new List<string?>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };

        foreach (var driveRoot in FixedDriveRoots())
        {
            roots.Add(Path.Combine(driveRoot, "Program Files"));
            roots.Add(Path.Combine(driveRoot, "Program Files (x86)"));
        }

        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => root!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> FixedDriveRoots()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                .Select(drive => drive.RootDirectory.FullName)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string FirstExistingOrFallback(IEnumerable<string> candidates, string fallback)
    {
        foreach (var candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return fallback;
    }

    private static void PushCandidate(
        ICollection<MaaAdbPathCandidatePreview> candidates,
        ISet<string> seen,
        string? path,
        string source,
        string preset)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "" : path.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var key = normalized.Replace("\\.\\", "\\", StringComparison.Ordinal).Replace('\\', '/').ToLowerInvariant();
        if (!seen.Add(key))
            return;

        candidates.Add(new MaaAdbPathCandidatePreview(normalized, source, string.IsNullOrWhiteSpace(preset) ? "custom" : preset, false, false, ""));
    }

    private static readonly string[] MuMuInstallDirectoryNames =
    [
        Path.Combine("Netease", "MuMuPlayer"),
        Path.Combine("Netease", "MuMu Player"),
        Path.Combine("Netease", "MuMu Player 12"),
        Path.Combine("Netease", "MuMuPlayer-12.0"),
        Path.Combine("Netease", "MuMuPlayer-15.0"),
        Path.Combine("Netease", "MuMuPlayerGlobal-12.0"),
        Path.Combine("Netease", "MuMuPlayerGlobal-15.0"),
        Path.Combine("Netease", "MuMu PlayerGlobal-12.0"),
        Path.Combine("Netease", "YXArkNights-12.0"),
        "MuMuPlayer",
        "MuMu Player",
        "MuMu Player 12",
        "MuMuPlayerGlobal-12.0",
        "YXArkNights-12.0",
    ];

    private static readonly string[] MuMuUninstallRegistryKeys =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MuMuPlayer-15.0",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MuMuPlayer-12.0",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MuMuPlayer",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MuMuPlayerGlobal-15.0",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MuMuPlayerGlobal-12.0",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\YXArkNights-12.0",
    ];
}
