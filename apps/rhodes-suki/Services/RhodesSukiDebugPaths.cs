namespace RhodesSuki.Services;

public static class RhodesSukiDebugPaths
{
    public const string DebugLogDirectoryName = "RHODES OBS COMMANDER3373 Debug Logs";
    public const string RecognitionScansDirectoryName = "Recognition Scans";
    public const string RoiDraftsDirectoryName = "ROI Drafts";
    public const string RoiSessionsDirectoryName = "ROI Sessions";
    public const string BugReportsDirectoryName = "Bug Reports";

    public static string DebugLogDirectory => Path.Combine(AppContext.BaseDirectory, DebugLogDirectoryName);

    public static string RecognitionScansDirectory => Path.Combine(DebugLogDirectory, RecognitionScansDirectoryName);

    public static string RoiDraftsDirectory => Path.Combine(DebugLogDirectory, RoiDraftsDirectoryName);

    public static string RoiSessionsDirectory => Path.Combine(DebugLogDirectory, RoiSessionsDirectoryName);

    public static string BugReportsDirectory => Path.Combine(DebugLogDirectory, BugReportsDirectoryName);
}
