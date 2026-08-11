namespace RhodesSuki.Models;

public sealed record SukiAdbConnectionSettings(
    int SchemaVersion = 1,
    bool AutoDetect = true,
    bool AlwaysAutoDetect = false,
    string EmulatorRoot = "",
    string EmulatorExecutablePath = "",
    bool MuMuScreenshotEnhancementEnabled = false,
    bool MuMuTouchEnhancementEnabled = false,
    bool MuMuBridgeConnectionEnabled = false,
    int MuMuInstanceIndex = 0,
    string GamePackage = "com.YoStarJP.Arknights",
    int GameCloneIndex = 0,
    string InputFallbackMethodId = "minitouch",
    string ScreencapFallbackMethodId = "raw-gzip",
    int ReconnectAttempts = 3,
    int ReconnectDelayMs = 1000,
    bool RestartAdbServerOnFailure = false,
    bool HardRestartAdbProcessOnFailure = false,
    bool RestartEmulatorOnFailure = false,
    bool KillAdbOnExit = false,
    bool UseManagedAdb = false,
    bool LightweightAdb = false)
{
    public const int CurrentSchemaVersion = 1;
}
