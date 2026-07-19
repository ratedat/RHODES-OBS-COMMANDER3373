using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record RhodesDistributionProfile(string Channel)
{
    public const string FileName = "distribution-profile.json";

    public static RhodesDistributionProfile Validation { get; } = new("validation");

    public static RhodesDistributionProfile PublicDebug { get; } = new("public-debug");

    public bool IsPublicDebug => string.Equals(Channel, PublicDebug.Channel, StringComparison.OrdinalIgnoreCase);

    public static RhodesDistributionProfile LoadDefault()
    {
        return Load(AppContext.BaseDirectory);
    }

    public static RhodesDistributionProfile Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, FileName);
        if (!File.Exists(path))
            return Validation;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var channel = document.RootElement.TryGetProperty("channel", out var value)
                ? value.GetString()?.Trim()
                : null;
            return string.Equals(channel, PublicDebug.Channel, StringComparison.OrdinalIgnoreCase)
                ? PublicDebug
                : Validation;
        }
        catch (JsonException)
        {
            return Validation;
        }
        catch (IOException)
        {
            return Validation;
        }
    }
}
