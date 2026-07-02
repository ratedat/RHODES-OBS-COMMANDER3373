using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbPresetCatalog
{
    private static readonly IReadOnlyDictionary<string, string[]> DefaultSerialsByPreset = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["bluestacks"] = ["127.0.0.1:5555", "127.0.0.1:5556", "127.0.0.1:5565", "127.0.0.1:5575", "127.0.0.1:5585", "127.0.0.1:5595", "127.0.0.1:5554"],
        ["mumu"] = ["127.0.0.1:16384", "127.0.0.1:16416", "127.0.0.1:16448", "127.0.0.1:16480", "127.0.0.1:16512", "127.0.0.1:16544", "127.0.0.1:16576"],
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
            yield return Path.Combine(root, "Netease", "MuMu Player 12", "shell", "adb.exe");
            yield return Path.Combine(root, "Netease", "MuMu PlayerGlobal-12.0", "shell", "adb.exe");
            yield return Path.Combine(root, "MuMu Player 12", "shell", "adb.exe");
        }
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
}
