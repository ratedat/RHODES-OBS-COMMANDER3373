using System.Globalization;
using System.Text.Json;

namespace RhodesSuki.Services;

public static class RhodesMaaThoughtLoadOcrExpander
{
    private const int LoadOffsetX = -55;
    private const int LoadOffsetY = 60;
    private const int LoadWidth = 48;
    private const int LoadHeight = 42;
    private const int LoadScale = 4;

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(
        string? recognitionDetailJson,
        IReadOnlyCollection<string>? excludedThoughtIds = null)
    {
        if (string.IsNullOrWhiteSpace(recognitionDetailJson))
            return [];

        try
        {
            using var document = JsonDocument.Parse(recognitionDetailJson);
            var rows = ResultArray(document.RootElement, "filtered");
            if (rows.Count == 0)
                rows = ResultArray(document.RootElement, "all");

            var byName = RhodesRunCatalog.LoadSpecialEffectOptions("is5_sarkaz", "thought")
                .GroupBy(option => Normalize(option.Name), StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
            var excluded = excludedThoughtIds?.ToHashSet(StringComparer.Ordinal) ?? [];
            var requested = new HashSet<string>(StringComparer.Ordinal);
            var requests = new List<MaaDynamicOcrRequest>();
            foreach (var row in rows)
            {
                if (!TryText(row, out var text)
                    || !byName.TryGetValue(Normalize(text), out var thought)
                    || excluded.Contains(thought.Id)
                    || !requested.Add(thought.Id)
                    || !TryBox(row, out var boxX, out var boxY))
                {
                    continue;
                }

                var x = boxX + LoadOffsetX;
                var y = boxY + LoadOffsetY;
                if (x < 0 || y < 0 || x + LoadWidth > 1280 || y + LoadHeight > 720)
                    continue;

                requests.Add(new MaaDynamicOcrRequest(
                    $"thought.card.load.{thought.Id}",
                    x,
                    y,
                    LoadWidth,
                    LoadHeight,
                    LoadScale,
                    Score(row)));
            }
            return requests;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool TryReadDisplayedLoad(string? recognitionDetailJson, out int load)
    {
        load = 0;
        if (string.IsNullOrWhiteSpace(recognitionDetailJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(recognitionDetailJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("result", out var nested)
                && nested.ValueKind == JsonValueKind.Object)
            {
                root = nested;
            }

            foreach (var row in ResultRows(root))
            {
                if (!TryText(row, out var text))
                    continue;

                var digits = NormalizeDigits(text);
                if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out load)
                    && load is >= 0 and <= 99)
                {
                    return true;
                }
            }
            load = 0;
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<JsonElement> ResultArray(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().ToArray()
            : [];

    private static IReadOnlyList<JsonElement> ResultRows(JsonElement root)
    {
        var filtered = ResultArray(root, "filtered");
        if (filtered.Count > 0)
            return filtered;

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("best", out var best)
            && best.ValueKind == JsonValueKind.Object)
        {
            return [best];
        }
        return ResultArray(root, "all");
    }

    private static bool TryText(JsonElement row, out string text)
    {
        text = "";
        if (row.ValueKind != JsonValueKind.Object
            || !row.TryGetProperty("text", out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        text = property.GetString() ?? "";
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool TryBox(JsonElement row, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (row.ValueKind != JsonValueKind.Object
            || !row.TryGetProperty("box", out var box)
            || box.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var values = box.EnumerateArray().Take(2).ToArray();
        return values.Length == 2
            && values[0].TryGetInt32(out x)
            && values[1].TryGetInt32(out y);
    }

    private static double Score(JsonElement row) =>
        row.ValueKind == JsonValueKind.Object
        && row.TryGetProperty("score", out var score)
        && score.TryGetDouble(out var value)
            ? value
            : 0;

    private static string Normalize(string value) =>
        string.Concat((value ?? "").Normalize().Where(ch => !char.IsWhiteSpace(ch)));

    private static string NormalizeDigits(string value)
    {
        var digits = new List<char>();
        foreach (var ch in value)
        {
            if (ch is >= '0' and <= '9')
                digits.Add(ch);
            else if (ch is >= '０' and <= '９')
                digits.Add((char)('0' + ch - '０'));
            else if (ch is 'I' or 'i' or 'L' or 'l' or '|' or '一' or '丨')
                digits.Add('1');
            else if (ch is 'O' or 'o' or 'Ｏ' or 'ｏ')
                digits.Add('0');
        }
        return new string(digits.ToArray());
    }
}
