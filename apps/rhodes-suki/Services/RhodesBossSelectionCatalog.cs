using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesBossSelectionCatalog
{
    public static IReadOnlyList<SukiBossSectionEditor> LoadSections(
        string campaignId,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? selections = null)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
            return [];

        try
        {
            var dataRoot = RhodesRunCatalog.ResolveDataRoot();
            var path = Path.Combine(dataRoot, "campaigns.json");
            if (!File.Exists(path))
                return [];

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            var campaign = document.RootElement.EnumerateArray().FirstOrDefault(item =>
                JsonString(item, "id").Equals(campaignId, StringComparison.Ordinal));
            if (campaign.ValueKind != JsonValueKind.Object
                || !campaign.TryGetProperty("bossFlags", out var bossFlags)
                || bossFlags.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var sections = new List<JsonElement>();
            if (bossFlags.TryGetProperty("manualSections", out var manualSections)
                && manualSections.ValueKind == JsonValueKind.Array)
            {
                sections.AddRange(manualSections.EnumerateArray());
            }
            if (bossFlags.TryGetProperty("floor3", out var floor3)
                && floor3.ValueKind == JsonValueKind.Object
                && !sections.Any(section => JsonString(section, "field").Equals(JsonString(floor3, "field"), StringComparison.Ordinal)))
            {
                sections.Insert(0, floor3);
            }

            return sections
                .Where(section => section.ValueKind == JsonValueKind.Object)
                .Select(section => BuildSection(dataRoot, campaignId, section, selections))
                .Where(section => section is not null)
                .Cast<SukiBossSectionEditor>()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static SukiBossSectionEditor? BuildSection(
        string dataRoot,
        string campaignId,
        JsonElement section,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? selections)
    {
        var field = JsonString(section, "field");
        if (string.IsNullOrWhiteSpace(field)
            || !section.TryGetProperty("options", out var optionElements)
            || optionElements.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var selectedIds = selections is not null && selections.TryGetValue(field, out var selected)
            ? selected.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var allowsMultiple = JsonString(section, "mode").Equals("multi", StringComparison.OrdinalIgnoreCase)
            || JsonBool(section, "multiple");
        var options = optionElements.EnumerateArray()
            .Select(option => BuildOption(dataRoot, field, option, selectedIds))
            .Where(option => option is not null)
            .Cast<SukiBossOption>()
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.DisplayName, StringComparer.Ordinal)
            .ToList();
        if (!allowsMultiple)
            options.Insert(0, new SukiBossOption(field, "", "未選択", "", "", "", double.MinValue));

        return new SukiBossSectionEditor(
            campaignId,
            JsonString(section, "id", field),
            field,
            JsonString(section, "label", field),
            JsonString(section, "helper"),
            allowsMultiple,
            options);
    }

    private static SukiBossOption? BuildOption(
        string dataRoot,
        string field,
        JsonElement option,
        IReadOnlySet<string> selectedIds)
    {
        var id = JsonString(option, "id");
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return new SukiBossOption(
            field,
            id,
            JsonString(option, "stageName", JsonString(option, "bossName", id)),
            JsonString(option, "bossName"),
            JsonString(option, "optionLabel"),
            RhodesRunCatalog.ResolveLocalPath(dataRoot, FirstImagePath(option)),
            JsonDouble(option, "sortOrder", 99),
            selectedIds.Contains(id));
    }

    private static string FirstImagePath(JsonElement option)
    {
        if (option.TryGetProperty("image", out var image) && image.ValueKind == JsonValueKind.Object)
            return JsonString(image, "localPath");
        if (option.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
        {
            var first = images.EnumerateArray().FirstOrDefault(item => item.ValueKind == JsonValueKind.Object);
            return JsonString(first, "localPath");
        }
        return "";
    }

    private static string JsonString(JsonElement element, string propertyName, string fallback = "") =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;

    private static bool JsonBool(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.ValueKind is JsonValueKind.True or JsonValueKind.False
        && property.GetBoolean();

    private static double JsonDouble(JsonElement element, string propertyName, double fallback) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(propertyName, out var property)
        && property.TryGetDouble(out var value)
            ? value
            : fallback;
}
