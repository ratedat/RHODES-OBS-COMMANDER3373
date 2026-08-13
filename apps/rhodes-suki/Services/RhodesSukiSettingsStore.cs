using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesSukiSettingsStore
{
    public const int CurrentSchemaVersion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "user-data", "suki-settings.json");

    public static RhodesSukiSettings Load(string? path = null)
    {
        var settingsPath = ResolvePath(path);
        if (!File.Exists(settingsPath))
            return new RhodesSukiSettings();

        try
        {
            var json = File.ReadAllText(settingsPath);
            return Normalize(JsonSerializer.Deserialize<RhodesSukiSettings>(json, JsonOptions) ?? new RhodesSukiSettings());
        }
        catch
        {
            return new RhodesSukiSettings();
        }
    }

    public static void Save(RhodesSukiSettings settings, string? path = null)
    {
        var settingsPath = ResolvePath(path);
        EnsureDirectory(settingsPath);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        File.WriteAllText(settingsPath, $"{json}{Environment.NewLine}");
    }

    public static async Task SaveAsync(RhodesSukiSettings settings, string? path = null, CancellationToken cancellationToken = default)
    {
        var settingsPath = ResolvePath(path);
        EnsureDirectory(settingsPath);
        var json = JsonSerializer.Serialize(Normalize(settings), JsonOptions);
        await File.WriteAllTextAsync(settingsPath, $"{json}{Environment.NewLine}", cancellationToken);
    }

    private static string ResolvePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? DefaultPath : path;
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    internal static RhodesSukiSettings Normalize(RhodesSukiSettings settings)
    {
        if (settings.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"未対応のSuki設定schemaVersionです: {settings.SchemaVersion} > {CurrentSchemaVersion}");
        }

        var outputPreferences = settings.OutputPreferences;
        if (outputPreferences is not null)
            outputPreferences = RhodesOutputProfileService.Normalize(outputPreferences);

        var adbConnection = settings.AdbConnection ?? CreateAdbConnectionFromLegacySettings(settings);
        var maaRuntime = settings.MaaRuntime ?? new SukiMaaRuntimeSettings();

        var normalized = settings with
        {
            SchemaVersion = CurrentSchemaVersion,
            AdbConnection = NormalizeAdbConnection(adbConnection),
            MaaRuntime = NormalizeMaaRuntime(maaRuntime),
            OutputPreferences = outputPreferences,
            TournamentRelayUrl = settings.TournamentRelayUrl?.Trim() ?? "",
            TournamentPlayerLabel = string.IsNullOrWhiteSpace(settings.TournamentPlayerLabel)
                ? "Player"
                : settings.TournamentPlayerLabel.Trim(),
        };
        var hasBareAdbPath = string.IsNullOrWhiteSpace(settings.AdbPath)
            || settings.AdbPath.Trim().Equals("adb", StringComparison.OrdinalIgnoreCase)
            || settings.AdbPath.Trim().Equals("adb.exe", StringComparison.OrdinalIgnoreCase);
        if (settings.SelectedAdbPresetId.Equals("custom", StringComparison.OrdinalIgnoreCase)
            && hasBareAdbPath
            && string.IsNullOrWhiteSpace(settings.AdbSerial))
        {
            normalized = normalized with { SelectedAdbPresetId = "auto", AdbPath = "adb" };
        }

        return normalized;
    }

    internal static SukiAdbConnectionSettings NormalizeAdbConnection(SukiAdbConnectionSettings settings)
    {
        if (settings.SchemaVersion > SukiAdbConnectionSettings.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"未対応のADB接続設定schemaVersionです: {settings.SchemaVersion} > {SukiAdbConnectionSettings.CurrentSchemaVersion}");
        }

        var inputFallback = settings.InputFallbackMethodId?.Trim() switch
        {
            "default" => "default",
            "maatouch" => "maatouch",
            "minitouch" => "minitouch",
            "adb-shell" => "adb-shell",
            _ => "minitouch",
        };
        var screencapFallback = settings.ScreencapFallbackMethodId?.Trim() switch
        {
            "default" => "default",
            "raw-gzip" => "raw-gzip",
            "compat" => "compat",
            _ => "raw-gzip",
        };

        return settings with
        {
            SchemaVersion = SukiAdbConnectionSettings.CurrentSchemaVersion,
            EmulatorRoot = settings.EmulatorRoot?.Trim() ?? "",
            EmulatorExecutablePath = settings.EmulatorExecutablePath?.Trim() ?? "",
            MuMuInstanceIndex = Math.Clamp(settings.MuMuInstanceIndex, 0, 127),
            LdPlayerInstanceIndex = Math.Clamp(settings.LdPlayerInstanceIndex, 0, 127),
            GamePackage = SukiAdbGamePackageCatalog.NormalizePackage(settings.GamePackage),
            GameCloneIndex = Math.Clamp(settings.GameCloneIndex, 0, 99),
            InputFallbackMethodId = inputFallback,
            ScreencapFallbackMethodId = screencapFallback,
            ReconnectAttempts = Math.Clamp(settings.ReconnectAttempts, 1, 5),
            ReconnectDelayMs = Math.Clamp(settings.ReconnectDelayMs, 0, 10_000),
            LightweightAdb = false,
        };
    }

    internal static SukiMaaRuntimeSettings NormalizeMaaRuntime(SukiMaaRuntimeSettings settings)
    {
        if (settings.SchemaVersion > SukiMaaRuntimeSettings.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"未対応のMAAランタイム設定schemaVersionです: {settings.SchemaVersion} > {SukiMaaRuntimeSettings.CurrentSchemaVersion}");
        }

        var screencapMethodId = SukiWin32ScreencapCatalog.Normalize(settings.Win32ScreencapMethodId);
        if (settings.SchemaVersion < 2
            && screencapMethodId.Equals("background", StringComparison.Ordinal))
        {
            screencapMethodId = SukiWin32ScreencapCatalog.DefaultId;
        }

        return settings with
        {
            SchemaVersion = SukiMaaRuntimeSettings.CurrentSchemaVersion,
            ConnectionTargetId = SukiMaaConnectionTargetCatalog.Normalize(settings.ConnectionTargetId),
            InferenceProviderId = SukiMaaInferenceCatalog.Normalize(settings.InferenceProviderId),
            InferenceDeviceId = Math.Clamp(settings.InferenceDeviceId, 0, 15),
            PreferredWindowTitle = string.IsNullOrWhiteSpace(settings.PreferredWindowTitle)
                ? "アークナイツ"
                : settings.PreferredWindowTitle.Trim(),
            PreferredWindowClass = settings.PreferredWindowClass?.Trim() ?? "",
            Win32ScreencapMethodId = screencapMethodId,
            Win32MouseMethodId = SukiWin32InputCatalog.NormalizeMouse(settings.Win32MouseMethodId),
            Win32KeyboardMethodId = SukiWin32InputCatalog.NormalizeKeyboard(settings.Win32KeyboardMethodId),
        };
    }

    private static SukiAdbConnectionSettings CreateAdbConnectionFromLegacySettings(RhodesSukiSettings settings)
    {
        var usesFastInput = settings.AdbInputMethodId.Equals(
            SukiAdbMethodCatalog.FastEmulatorMethodId,
            StringComparison.OrdinalIgnoreCase);
        var usesFastScreencap = settings.AdbScreencapMethodId.Equals(
            SukiAdbMethodCatalog.FastEmulatorMethodId,
            StringComparison.OrdinalIgnoreCase);

        return new SukiAdbConnectionSettings(
            AutoDetect: !settings.SelectedAdbPresetId.Equals("custom", StringComparison.OrdinalIgnoreCase),
            MuMuScreenshotEnhancementEnabled: usesFastScreencap,
            MuMuTouchEnhancementEnabled: usesFastInput,
            InputFallbackMethodId: "minitouch",
            ScreencapFallbackMethodId: "raw-gzip");
    }
}
