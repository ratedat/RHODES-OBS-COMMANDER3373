using System.Text.Json.Nodes;
using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record MaaAdbConnectionResolution(
    MaaSessionOptions Options,
    bool DeviceResolved,
    string Detail);

public static class RhodesMaaAdbConnectionResolver
{
    public static async Task<MaaAdbConnectionResolution> ResolveToolkitAsync(
        MaaSessionOptions requested,
        Func<string, CancellationToken, Task<IReadOnlyList<AdbDeviceInfo>>> findDevicesAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(findDevicesAsync);
        var devices = await findDevicesAsync(requested.AdbPath, cancellationToken);
        return ResolveToolkitDevice(requested, devices);
    }

    public static MaaSessionOptions ApplyPresetExtras(MaaSessionOptions options)
    {
        if (!UsesEmulatorExtras(options))
            return options with { AdbConfigJson = SukiAdbConfigJson.Normalize(options.AdbConfigJson) };

        var preset = NormalizePreset(options.ConnectionPreset);
        var config = ParseObject(options.AdbConfigJson);
        var emulator = preset switch
        {
            "mumu" => "mumu",
            "ldplayer" => "ld",
            _ => "",
        };
        if (emulator.Length == 0)
            return options with { AdbConfigJson = config.ToJsonString() };

        var extras = GetOrCreateObject(config, "extras");
        var emulatorConfig = GetOrCreateObject(extras, emulator);
        SetDefault(emulatorConfig, "enable", JsonValue.Create(true));

        var root = ResolveEmulatorRoot(options.AdbPath, preset);
        if (!string.IsNullOrWhiteSpace(root))
            SetDefault(emulatorConfig, "path", JsonValue.Create(root));

        var index = preset == "mumu"
            ? InferMuMuIndex(options.AdbSerial)
            : InferLdPlayerIndex(options.AdbSerial);
        if (index is not null)
            SetDefault(emulatorConfig, "index", JsonValue.Create(index.Value));

        return options with { AdbConfigJson = config.ToJsonString() };
    }

    public static MaaAdbConnectionResolution ResolveToolkitDevice(
        MaaSessionOptions requested,
        IEnumerable<AdbDeviceInfo> devices)
    {
        var candidates = devices.ToArray();
        var selected = string.IsNullOrWhiteSpace(requested.AdbSerial)
            ? candidates.Length == 1 ? candidates[0] : null
            : candidates.FirstOrDefault(device => device.AdbSerial.Equals(requested.AdbSerial, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            var reason = candidates.Length == 0
                ? "MaaToolkitでADB端末が見つかりませんでした。"
                : string.IsNullOrWhiteSpace(requested.AdbSerial)
                    ? $"MaaToolkitで{candidates.Length}端末を検出しました。使用するserialを選択してください。"
                    : $"MaaToolkitにserial {requested.AdbSerial} がありません。";
            return new MaaAdbConnectionResolution(requested, false, reason);
        }

        var resolved = requested with
        {
            AdbPath = string.IsNullOrWhiteSpace(selected.AdbPath) ? requested.AdbPath : selected.AdbPath,
            AdbSerial = selected.AdbSerial,
            AdbConfigJson = MergeJson(selected.Config, requested.AdbConfigJson),
            InputMethod = requested.InputMethod == AdbInputMethods.Default ? selected.InputMethods : requested.InputMethod,
            ScreencapMethod = requested.ScreencapMethod == AdbScreencapMethods.Default ? selected.ScreencapMethods : requested.ScreencapMethod,
        };
        resolved = ApplyPresetExtras(resolved);
        return new MaaAdbConnectionResolution(
            resolved,
            true,
            $"MaaToolkit: {selected.Name} / {selected.AdbSerial} / {resolved.ScreencapMethod} / {resolved.InputMethod}");
    }

    internal static int? InferMuMuIndex(string? serial)
    {
        if (!TryParseSerialPort(serial, out var port, out var emulatorStyle))
            return null;
        if (emulatorStyle)
            return port >= 5554 && (port - 5554) % 2 == 0 ? (port - 5554) / 2 : null;
        if (port == 7555)
            return 0;
        if (port >= 16384 && (port - 16384) % 4 == 0)
        {
            var k = (port - 16384) / 4;
            return ((k & 7) << 5) | (k >> 3);
        }
        return port >= 5555 && (port - 5555) % 2 == 0 ? (port - 5555) / 2 : null;
    }

    internal static int? InferLdPlayerIndex(string? serial)
    {
        if (!TryParseSerialPort(serial, out var port, out var emulatorStyle))
            return null;
        var basePort = emulatorStyle ? 5554 : 5555;
        return port >= basePort && (port - basePort) % 2 == 0 ? (port - basePort) / 2 : null;
    }

    private static bool UsesEmulatorExtras(MaaSessionOptions options)
    {
        return options.InputMethod.HasFlag(AdbInputMethods.EmulatorExtras)
            || options.ScreencapMethod.HasFlag(AdbScreencapMethods.EmulatorExtras);
    }

    private static string ResolveEmulatorRoot(string adbPath, string preset)
    {
        if (string.IsNullOrWhiteSpace(adbPath) || adbPath.Equals("adb", StringComparison.OrdinalIgnoreCase))
            return "";

        var directory = Path.GetDirectoryName(adbPath.Trim());
        if (string.IsNullOrWhiteSpace(directory))
            return "";
        if (preset == "mumu" && Path.GetFileName(directory).Equals("shell", StringComparison.OrdinalIgnoreCase))
            directory = Directory.GetParent(directory)?.FullName ?? directory;
        return directory;
    }

    private static bool TryParseSerialPort(string? serial, out int port, out bool emulatorStyle)
    {
        port = 0;
        emulatorStyle = false;
        var value = serial?.Trim() ?? "";
        const string emulatorPrefix = "emulator-";
        if (value.StartsWith(emulatorPrefix, StringComparison.OrdinalIgnoreCase))
        {
            emulatorStyle = true;
            return int.TryParse(value[emulatorPrefix.Length..], out port);
        }

        var colon = value.LastIndexOf(':');
        return colon >= 0 && colon + 1 < value.Length && int.TryParse(value[(colon + 1)..], out port);
    }

    private static string MergeJson(string? baseJson, string? overrideJson)
    {
        var result = ParseObject(baseJson);
        MergeInto(result, ParseObject(overrideJson));
        return result.ToJsonString();
    }

    private static void MergeInto(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject sourceObject && target[key] is JsonObject targetObject)
            {
                MergeInto(targetObject, sourceObject);
                continue;
            }
            target[key] = value?.DeepClone();
        }
    }

    private static JsonObject ParseObject(string? json)
    {
        return JsonNode.Parse(SukiAdbConfigJson.Normalize(json))!.AsObject();
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
            return existing;
        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static void SetDefault(JsonObject target, string propertyName, JsonNode? value)
    {
        if (target[propertyName] is null)
            target[propertyName] = value;
    }

    private static string NormalizePreset(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant();
    }
}
