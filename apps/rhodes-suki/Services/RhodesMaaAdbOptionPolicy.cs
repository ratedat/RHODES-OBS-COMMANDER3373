using System.Text.Json.Nodes;
using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaAdbOptionPolicy
{
    public static RhodesMaaAdbOptionResolution Resolve(
        MaaSessionOptions requested,
        SukiAdbConnectionSettings settings,
        RhodesMuMuCapabilitySnapshot capability)
    {
        var preset = requested.ConnectionPreset?.Trim() ?? "";
        if (!preset.Equals("mumu", StringComparison.OrdinalIgnoreCase))
        {
            return new RhodesMaaAdbOptionResolution(
                requested with { AdbConfigJson = SukiAdbConfigJson.Normalize(requested.AdbConfigJson) },
                false,
                false,
                "MuMu以外の接続では高速撮影設定を適用しません。",
                "MuMu以外の接続では高速タッチ設定を適用しません。");
        }

        var screenshotActive = settings.MuMuScreenshotEnhancementEnabled
            && capability.ScreenshotEnhancementAvailable;
        var touchActive = settings.MuMuTouchEnhancementEnabled
            && capability.TouchEnhancementAvailable;
        var screencapFallback = SukiAdbMethodCatalog.FindScreencap(settings.ScreencapFallbackMethodId).Value;
        var inputFallback = SukiAdbMethodCatalog.FindInput(settings.InputFallbackMethodId).Value;
        var screencap = screenshotActive
            ? AdbScreencapMethods.EmulatorExtras | screencapFallback
            : screencapFallback;
        var input = touchActive
            ? AdbInputMethods.EmulatorExtras | inputFallback
            : inputFallback;

        var config = JsonNode.Parse(SukiAdbConfigJson.Normalize(requested.AdbConfigJson))!.AsObject();
        var extras = GetOrCreateObject(config, "extras");
        var mumu = GetOrCreateObject(extras, "mumu");
        mumu["enable"] = screenshotActive || touchActive;
        mumu["path"] = capability.EmulatorRoot;
        if (!string.IsNullOrWhiteSpace(capability.IpcLibraryPath))
            mumu["lib"] = capability.IpcLibraryPath;
        else
            mumu.Remove("lib");
        mumu["index"] = settings.MuMuBridgeConnectionEnabled
            ? settings.MuMuInstanceIndex
            : capability.InstanceIndex;
        mumu["app_package"] = settings.GamePackage;
        mumu["app_cloned_index"] = settings.GameCloneIndex;

        var screenshotDetail = !settings.MuMuScreenshotEnhancementEnabled
            ? $"MuMu高速撮影OFF。{SukiAdbMethodCatalog.FindScreencap(settings.ScreencapFallbackMethodId).Label}を使用します。"
            : screenshotActive
                ? capability.ScreenshotDetail
                : $"{capability.ScreenshotDetail} フォールバック: {SukiAdbMethodCatalog.FindScreencap(settings.ScreencapFallbackMethodId).Label}";
        var touchDetail = !settings.MuMuTouchEnhancementEnabled
            ? $"MuMu高速タッチOFF。{SukiAdbMethodCatalog.FindInput(settings.InputFallbackMethodId).Label}を使用します。"
            : touchActive
                ? capability.TouchDetail
                : $"{capability.TouchDetail} フォールバック: {SukiAdbMethodCatalog.FindInput(settings.InputFallbackMethodId).Label}";

        return new RhodesMaaAdbOptionResolution(
            requested with
            {
                AdbConfigJson = config.ToJsonString(),
                ScreencapMethod = screencap,
                InputMethod = input,
            },
            screenshotActive,
            touchActive,
            screenshotDetail,
            touchDetail);
    }

    private static JsonObject GetOrCreateObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
            return existing;
        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }
}
