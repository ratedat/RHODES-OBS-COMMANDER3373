using System.Globalization;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaOcrDetailRows
{
    public static IReadOnlyList<MaaOcrDetailRow> FromTaskResults(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var rows = new List<MaaOcrDetailRow>();
        foreach (var taskResult in taskResults)
        {
            AddRows(rows, taskResult);
        }
        return rows;
    }

    private static void AddRows(List<MaaOcrDetailRow> rows, MaaTaskRunResult taskResult)
    {
        using var document = ParseRecognitionDetail(taskResult.RecognitionDetailJson);
        if (document is null)
            return;

        var detail = UnwrapResult(document.RootElement);
        AddResultRows(rows, taskResult, detail, "filtered", "filtered", "filtered_results", "filteredResults");
        AddResultRows(rows, taskResult, detail, "best", "best", "best_result", "bestResult");
        AddResultRows(rows, taskResult, detail, "all", "all", "all_results", "allResults");
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

    private static void AddResultRows(
        List<MaaOcrDetailRow> rows,
        MaaTaskRunResult taskResult,
        JsonElement detail,
        string source,
        params string[] propertyNames)
    {
        foreach (var item in ResultItems(detail, propertyNames))
        {
            var text = JsonString(item, "text").Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            rows.Add(new MaaOcrDetailRow(
                taskResult.Entry,
                text,
                JsonNumber(item, "score") ?? JsonNumber(item, "confidence") ?? JsonNumber(item, "prob") ?? JsonNumber(item, "similarity"),
                source,
                taskResult.Algorithm));
        }
    }

    private static IEnumerable<JsonElement> ResultItems(JsonElement detail, params string[] propertyNames)
    {
        if (detail.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var propertyName in propertyNames)
        {
            if (!detail.TryGetProperty(propertyName, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.EnumerateArray())
                    yield return item;
                yield break;
            }

            if (property.ValueKind == JsonValueKind.Object)
            {
                yield return property;
                yield break;
            }
        }
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static double? JsonNumber(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String
            && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }
}
