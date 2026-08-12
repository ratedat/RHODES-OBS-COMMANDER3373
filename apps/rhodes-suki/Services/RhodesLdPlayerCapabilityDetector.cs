using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesLdPlayerCapabilityDetector
{
    public static RhodesLdPlayerCapabilitySnapshot Detect(
        SukiAdbConnectionSettings settings,
        string adbPath,
        string serial,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var root = ResolveRoot(settings.EmulatorRoot, settings.EmulatorExecutablePath, adbPath, fileExists);
        if (string.IsNullOrWhiteSpace(root))
            return RhodesLdPlayerCapabilitySnapshot.NotDetected();

        var consolePath = Path.Combine(root, "ldconsole.exe");
        var captureLibraryPath = Path.Combine(root, "ldopengl64.dll");
        var consoleAvailable = fileExists(consolePath);
        var captureLibraryAvailable = fileExists(captureLibraryPath);
        var available = consoleAvailable && captureLibraryAvailable;
        var instanceIndex = RhodesMaaAdbConnectionResolver.InferLdPlayerIndex(serial)
            ?? settings.LdPlayerInstanceIndex;
        var detail = available
            ? $"LDPlayer高速撮影を使用できます: {captureLibraryPath} / instance={instanceIndex}。接続時のPIDはMaaToolkitが取得します。"
            : BuildUnavailableDetail(root, consoleAvailable, captureLibraryAvailable);

        return new RhodesLdPlayerCapabilitySnapshot(
            root,
            consolePath,
            captureLibraryPath,
            available,
            Math.Clamp(instanceIndex, 0, 127),
            detail);
    }

    internal static string ResolveRoot(
        string explicitRoot,
        string emulatorExecutablePath,
        string adbPath,
        Func<string, bool> fileExists)
    {
        var configured = NormalizeDirectory(explicitRoot);
        if (configured.Length > 0 && HasLdRuntime(configured, fileExists))
            return configured;

        foreach (var sourcePath in new[] { emulatorExecutablePath, adbPath })
        {
            var root = InferRootFromFile(sourcePath, fileExists);
            if (root.Length > 0)
                return root;
        }
        return configured.Length > 0 ? configured : InferDirectoryFromFile(adbPath);
    }

    private static string InferRootFromFile(string? filePath, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || filePath.Equals("adb", StringComparison.OrdinalIgnoreCase)
            || filePath.Equals("adb.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath.Trim());
        }
        catch
        {
            return "";
        }

        var directory = new FileInfo(fullPath).Directory;
        for (var depth = 0; directory is not null && depth < 5; depth++, directory = directory.Parent)
        {
            var root = directory.FullName;
            if (HasLdRuntime(root, fileExists))
                return root;
        }
        return "";
    }

    private static bool HasLdRuntime(string root, Func<string, bool> fileExists) =>
        fileExists(Path.Combine(root, "ldconsole.exe"))
        || fileExists(Path.Combine(root, "ldopengl64.dll"));

    private static string InferDirectoryFromFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || filePath.Equals("adb", StringComparison.OrdinalIgnoreCase)
            || filePath.Equals("adb.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }
        try
        {
            return new FileInfo(Path.GetFullPath(filePath.Trim())).Directory?.FullName ?? "";
        }
        catch
        {
            return "";
        }
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

    private static string BuildUnavailableDetail(string root, bool consoleAvailable, bool captureLibraryAvailable)
    {
        var missing = new List<string>();
        if (!consoleAvailable)
            missing.Add("ldconsole.exe");
        if (!captureLibraryAvailable)
            missing.Add("ldopengl64.dll");
        return $"LDPlayer高速撮影は無効です: {string.Join(" / ", missing)} が見つかりません ({root})。";
    }
}
