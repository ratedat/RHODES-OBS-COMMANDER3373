using System.Globalization;
using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record MaaCompositeTemplateHit(int Index, double Score);

public static class RhodesMaaCompositeTemplateResult
{
    public static bool TryReadFirstHit(string detailJson, out MaaCompositeTemplateHit hit)
    {
        hit = new MaaCompositeTemplateHit(-1, 0);
        using var document = Parse(detailJson);
        if (document is null)
            return false;

        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("result", out var nested))
        {
            root = nested;
        }
        if (root.ValueKind != JsonValueKind.Array)
            return false;

        var index = 0;
        foreach (var child in root.EnumerateArray())
        {
            if (HasNonEmptyBox(child) || TryReadBestScore(child, out _))
            {
                TryReadBestScore(child, out var score);
                hit = new MaaCompositeTemplateHit(index, score);
                return true;
            }
            index++;
        }

        return false;
    }

    public static double ReadBestScore(string detailJson)
    {
        using var document = Parse(detailJson);
        if (document is null)
            return 0;
        return TryReadBestScore(document.RootElement, out var score) ? score : 0;
    }

    private static bool TryReadBestScore(JsonElement element, out double score)
    {
        score = 0;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (element.TryGetProperty("detail", out var detail)
            && TryReadBestScore(detail, out score))
        {
            return true;
        }

        foreach (var name in new[] { "best", "best_result", "bestResult" })
        {
            if (element.TryGetProperty(name, out var best)
                && best.ValueKind == JsonValueKind.Object
                && TryNumber(best, "score", out score))
            {
                return true;
            }
        }

        foreach (var name in new[] { "filtered", "filtered_results", "filteredResults" })
        {
            if (!element.TryGetProperty(name, out var filtered)
                || filtered.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var item in filtered.EnumerateArray())
            {
                if (TryNumber(item, "score", out score))
                    return true;
            }
        }

        return TryNumber(element, "score", out score);
    }

    private static bool HasNonEmptyBox(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("box", out var box)
            || box.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var values = box.EnumerateArray().Take(4).ToArray();
        return values.Length == 4
            && values[2].TryGetDouble(out var width)
            && values[3].TryGetDouble(out var height)
            && width > 0
            && height > 0;
    }

    private static bool TryNumber(JsonElement element, string name, out double number)
    {
        number = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var property))
            return false;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out number))
            return true;
        return property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static JsonDocument? Parse(string detailJson)
    {
        if (string.IsNullOrWhiteSpace(detailJson))
            return null;
        var objectStart = detailJson.IndexOf('{');
        var arrayStart = detailJson.IndexOf('[');
        var start = objectStart < 0
            ? arrayStart
            : arrayStart < 0 ? objectStart : Math.Min(objectStart, arrayStart);
        if (start < 0)
            return null;
        try
        {
            return JsonDocument.Parse(detailJson[start..]);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
