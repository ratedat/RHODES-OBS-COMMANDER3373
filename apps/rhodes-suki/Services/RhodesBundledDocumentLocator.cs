namespace RhodesSuki.Services;

public static class RhodesBundledDocumentLocator
{
    private static readonly string AdbConnectionGuideRelativePath =
        Path.Combine("docs", "adb-connection-settings.html");

    public static string ResolveAdbConnectionGuidePath(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        return Path.GetFullPath(Path.Combine(baseDirectory, AdbConnectionGuideRelativePath));
    }

    public static string FindAdbConnectionGuidePath()
    {
        var packaged = ResolveAdbConnectionGuidePath(AppContext.BaseDirectory);
        if (File.Exists(packaged))
            return packaged;

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var source = Path.Combine(directory.FullName, "docs", "user", "adb-connection-settings.html");
            if (File.Exists(source))
                return source;
        }
        return packaged;
    }
}
