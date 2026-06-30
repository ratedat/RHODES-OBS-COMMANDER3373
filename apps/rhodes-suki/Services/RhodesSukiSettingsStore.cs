using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesSukiSettingsStore
{
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
            return JsonSerializer.Deserialize<RhodesSukiSettings>(json, JsonOptions) ?? new RhodesSukiSettings();
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
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(settingsPath, $"{json}{Environment.NewLine}");
    }

    public static async Task SaveAsync(RhodesSukiSettings settings, string? path = null, CancellationToken cancellationToken = default)
    {
        var settingsPath = ResolvePath(path);
        EnsureDirectory(settingsPath);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
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
}
