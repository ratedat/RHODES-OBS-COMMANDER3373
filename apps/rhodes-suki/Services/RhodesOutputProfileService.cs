using System.Text.Json;
using System.Text.RegularExpressions;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static partial class RhodesOutputProfileService
{
    public const string ProfileKind = "rhodes-output-profile";
    public const int ProfileSchemaVersion = 1;
    public const int OutputSchemaVersion = 2;
    public const int MaxCustomCssLength = 65_536;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static SukiOutputPreferences Normalize(SukiOutputPreferences preferences)
    {
        var integratedAppearance = NormalizeAppearance(preferences.IntegratedAppearance);
        var individualAppearance = NormalizeAppearance(preferences.IndividualAppearance ?? integratedAppearance);
        var parts = (preferences.Parts ?? [])
            .Where(part => !string.IsNullOrWhiteSpace(part.Id))
            .Select(part => part with
            {
                Id = part.Id.Trim(),
                Width = Math.Max(1, part.Width),
                Height = Math.Max(1, part.Height),
                BackgroundOpacity = Math.Clamp(part.BackgroundOpacity, 0, 100),
            })
            .GroupBy(part => part.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();

        return preferences with
        {
            SchemaVersion = OutputSchemaVersion,
            BackgroundOpacity = Math.Clamp(preferences.BackgroundOpacity, 0, 100),
            ScrollSpeed = Math.Clamp(preferences.ScrollSpeed, 0, 30),
            Parts = parts,
            IntegratedAppearance = integratedAppearance,
            IndividualAppearance = individualAppearance,
            IndividualTournamentMode = preferences.IndividualTournamentMode
                ?? preferences.TournamentMode,
            IndividualBackgroundEnabled = preferences.IndividualBackgroundEnabled
                ?? preferences.BackgroundEnabled,
            IndividualBackgroundOpacity = Math.Clamp(
                preferences.IndividualBackgroundOpacity
                    ?? preferences.BackgroundOpacity,
                0,
                100),
            IndividualShowPartTitles = preferences.IndividualShowPartTitles
                ?? preferences.ShowPartTitles,
            IndividualScrollSpeed = Math.Clamp(preferences.IndividualScrollSpeed ?? preferences.ScrollSpeed, 0, 30),
        };
    }

    public static SukiOutputAppearance NormalizeAppearance(SukiOutputAppearance? appearance)
    {
        appearance ??= new SukiOutputAppearance();
        return appearance with
        {
            FontColor = NormalizeColor(appearance.FontColor, "#F2EFE6"),
            BackgroundColor = NormalizeColor(appearance.BackgroundColor, "#080B0C"),
            BorderColor = NormalizeColor(appearance.BorderColor, "#2B3638"),
            AccentColor = NormalizeColor(appearance.AccentColor, "#55D6BE"),
            FontSizePercent = Math.Clamp(appearance.FontSizePercent, 60, 200),
            CustomCss = NormalizeCustomCss(appearance.CustomCss),
        };
    }

    public static async Task ExportAsync(
        string path,
        SukiOutputPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        ValidateCustomCss(preferences.IntegratedAppearance?.CustomCss);
        ValidateCustomCss(preferences.IndividualAppearance?.CustomCss);
        var profile = new SukiOutputProfile(
            ProfileKind,
            ProfileSchemaVersion,
            DateTimeOffset.UtcNow,
            Normalize(preferences));
        EnsureDirectory(path);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(path, $"{json}{Environment.NewLine}", cancellationToken);
    }

    public static async Task<SukiOutputPreferences> ImportAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("出力プロファイルのJSONルートはobjectである必要があります。");

        if (document.RootElement.TryGetProperty("kind", out _)
            || document.RootElement.TryGetProperty("Kind", out _))
        {
            var profile = JsonSerializer.Deserialize<SukiOutputProfile>(json, JsonOptions)
                ?? throw new InvalidDataException("出力プロファイルを読み込めませんでした。");
            if (!profile.Kind.Equals(ProfileKind, StringComparison.Ordinal))
                throw new InvalidDataException($"未対応のプロファイル種別です: {profile.Kind}");
            if (profile.SchemaVersion > ProfileSchemaVersion)
                throw new InvalidDataException($"新しい出力プロファイルです。アプリを更新してください: schemaVersion={profile.SchemaVersion}");
            EnsureSupportedOutputSchema(profile.OutputPreferences);
            ValidateCustomCss(profile.OutputPreferences.IntegratedAppearance?.CustomCss);
            ValidateCustomCss(profile.OutputPreferences.IndividualAppearance?.CustomCss);
            return Normalize(profile.OutputPreferences);
        }

        var preferences = JsonSerializer.Deserialize<SukiOutputPreferences>(json, JsonOptions)
            ?? throw new InvalidDataException("出力設定を読み込めませんでした。");
        EnsureSupportedOutputSchema(preferences);
        ValidateCustomCss(preferences.IntegratedAppearance?.CustomCss);
        ValidateCustomCss(preferences.IndividualAppearance?.CustomCss);
        return Normalize(preferences);
    }

    private static string NormalizeColor(string? value, string fallback)
    {
        var trimmed = value?.Trim() ?? "";
        return CssHexColorRegex().IsMatch(trimmed) ? trimmed.ToUpperInvariant() : fallback;
    }

    private static string NormalizeCustomCss(string? value)
    {
        var css = (value ?? "").Replace("\0", "", StringComparison.Ordinal);
        if (css.Length > MaxCustomCssLength)
            css = css[..MaxCustomCssLength];
        return ForbiddenCssRegex().IsMatch(css) ? "" : css;
    }

    private static void ValidateCustomCss(string? value)
    {
        if (ForbiddenCssRegex().IsMatch(value ?? ""))
            throw new InvalidDataException("ユーザーCSSでは javascript: URLを使用できません。");
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static void EnsureSupportedOutputSchema(SukiOutputPreferences preferences)
    {
        if (preferences.SchemaVersion > OutputSchemaVersion)
            throw new InvalidDataException(
                $"新しい出力設定です。アプリを更新してください: outputSchemaVersion={preferences.SchemaVersion}");
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")]
    private static partial Regex CssHexColorRegex();

    [GeneratedRegex("javascript\\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenCssRegex();
}
