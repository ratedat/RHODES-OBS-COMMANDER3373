using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaRoiDetailRows
{
    private static readonly string[] RoiPropertyNames = ["roi", "rect", "box"];
    private static readonly string[] ResultArrayNames = ["filtered", "filtered_results", "filteredResults", "best", "best_result", "bestResult", "all", "all_results", "allResults"];

    public static IReadOnlyList<MaaRoiDetailRow> FromTaskResults(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var rows = new List<MaaRoiDetailRow>();
        foreach (var taskResult in taskResults)
        {
            AddRows(rows, taskResult);
        }
        return rows;
    }

    private static void AddRows(List<MaaRoiDetailRow> rows, MaaTaskRunResult taskResult)
    {
        using var document = ParseRecognitionDetail(taskResult.RecognitionDetailJson);
        if (document is null)
            return;

        var detail = UnwrapResult(document.RootElement);
        AddRectRows(rows, taskResult.Entry, "detail", detail);
        foreach (var (source, item) in ResultItems(detail))
        {
            AddRectRows(rows, taskResult.Entry, source, item);
        }
    }

    private static JsonDocument? ParseRecognitionDetail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var jsonStart = text.IndexOf('{');
        if (jsonStart < 0)
            return null;

        try
        {
            return JsonDocument.Parse(text[jsonStart..]);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement UnwrapResult(JsonElement detail)
    {
        return detail.ValueKind == JsonValueKind.Object
            && detail.TryGetProperty("result", out var nested)
            && nested.ValueKind == JsonValueKind.Object
            ? nested
            : detail;
    }

    private static IEnumerable<(string Source, JsonElement Item)> ResultItems(JsonElement detail)
    {
        if (detail.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var propertyName in ResultArrayNames)
        {
            if (!detail.TryGetProperty(propertyName, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.EnumerateArray())
                    yield return (ResultSource(propertyName), item);
                continue;
            }

            if (property.ValueKind == JsonValueKind.Object)
                yield return (ResultSource(propertyName), property);
        }
    }

    private static string ResultSource(string propertyName)
    {
        if (propertyName.Contains("filtered", StringComparison.OrdinalIgnoreCase)) return "filtered";
        if (propertyName.Contains("best", StringComparison.OrdinalIgnoreCase)) return "best";
        if (propertyName.Contains("all", StringComparison.OrdinalIgnoreCase)) return "all";
        return propertyName;
    }

    private static void AddRectRows(List<MaaRoiDetailRow> rows, string entry, string source, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return;

        foreach (var propertyName in RoiPropertyNames)
        {
            if (!item.TryGetProperty(propertyName, out var property) || !TryRect(property, out var rect))
                continue;

            rows.Add(new MaaRoiDetailRow(entry, $"{source}.{propertyName}", rect.X, rect.Y, rect.Width, rect.Height, property.GetRawText()));
        }
    }

    private static bool TryRect(JsonElement element, out MaaRoi rect)
    {
        rect = new MaaRoi(0, 0, 0, 0);
        if (TryFlatArrayRect(element, out rect) || TryObjectRect(element, out rect) || TryPointBoxRect(element, out rect))
            return rect.Width > 0 && rect.Height > 0;

        return false;
    }

    private static bool TryFlatArrayRect(JsonElement element, out MaaRoi rect)
    {
        rect = new MaaRoi(0, 0, 0, 0);
        if (element.ValueKind != JsonValueKind.Array)
            return false;

        var values = element.EnumerateArray()
            .Select(Number)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (values.Length != 4)
            return false;

        rect = new MaaRoi(values[0], values[1], values[2], values[3]);
        return true;
    }

    private static bool TryObjectRect(JsonElement element, out MaaRoi rect)
    {
        rect = new MaaRoi(0, 0, 0, 0);
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        var x = NumberProperty(element, "x") ?? NumberProperty(element, "left");
        var y = NumberProperty(element, "y") ?? NumberProperty(element, "top");
        var width = NumberProperty(element, "width") ?? NumberProperty(element, "w");
        var height = NumberProperty(element, "height") ?? NumberProperty(element, "h");
        if (!x.HasValue || !y.HasValue || !width.HasValue || !height.HasValue)
            return false;

        rect = new MaaRoi(x.Value, y.Value, width.Value, height.Value);
        return true;
    }

    private static bool TryPointBoxRect(JsonElement element, out MaaRoi rect)
    {
        rect = new MaaRoi(0, 0, 0, 0);
        if (element.ValueKind != JsonValueKind.Array)
            return false;

        var points = new List<(int X, int Y)>();
        foreach (var point in element.EnumerateArray())
        {
            if (point.ValueKind == JsonValueKind.Array)
            {
                var values = point.EnumerateArray().Select(Number).Where(value => value.HasValue).Select(value => value!.Value).ToArray();
                if (values.Length >= 2) points.Add((values[0], values[1]));
            }
            else if (point.ValueKind == JsonValueKind.Object)
            {
                var x = NumberProperty(point, "x");
                var y = NumberProperty(point, "y");
                if (x.HasValue && y.HasValue) points.Add((x.Value, y.Value));
            }
        }

        if (points.Count < 2)
            return false;

        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minY = points.Min(point => point.Y);
        var maxY = points.Max(point => point.Y);
        rect = new MaaRoi(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private static int? NumberProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            ? Number(value)
            : null;
    }

    private static int? Number(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number))
            return number;

        return element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out number)
            ? number
            : null;
    }
}
