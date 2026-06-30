using System.Globalization;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaResultPreview
{
    public static IReadOnlyList<MaaCandidatePreview> FromTaskResults(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var previews = new List<MaaCandidatePreview>();
        foreach (var taskResult in taskResults)
        {
            AddTaskResultPreviews(previews, taskResult);
        }
        return previews;
    }

    private static void AddTaskResultPreviews(List<MaaCandidatePreview> previews, MaaTaskRunResult taskResult)
    {
        var added = false;
        using var document = ParseRecognitionDetail(taskResult.RecognitionDetailJson);
        if (document is not null)
        {
            foreach (var item in PrimaryResults(document.RootElement))
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var text = JsonString(item, "text");
                var score = JsonNumber(item, "score") ?? JsonNumber(item, "confidence") ?? JsonNumber(item, "prob") ?? JsonNumber(item, "similarity");
                var algorithm = taskResult.Algorithm.ToLowerInvariant();
                var isOcr = algorithm.Contains("ocr", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(text);
                var isTemplate = algorithm.Contains("template", StringComparison.Ordinal)
                    || (!isOcr && (score.HasValue || HasAnyProperty(item, "box", "rect", "roi")));

                if (isOcr && !string.IsNullOrWhiteSpace(text))
                {
                    previews.Add(new MaaCandidatePreview(
                        "ocr",
                        taskResult.Entry,
                        text.Trim(),
                        string.IsNullOrWhiteSpace(taskResult.Detail) ? taskResult.Entry : taskResult.Detail,
                        score));
                    added = true;
                    continue;
                }

                if (isTemplate)
                {
                    previews.Add(new MaaCandidatePreview(
                        "template",
                        taskResult.Entry,
                        TemplateValue(item, taskResult.Hit),
                        taskResult.Algorithm,
                        score));
                    added = true;
                }
            }
        }

        if (!added && taskResult.Succeeded && taskResult.Hit)
        {
            previews.Add(new MaaCandidatePreview(
                "maa",
                taskResult.Entry,
                "hit",
                string.IsNullOrWhiteSpace(taskResult.Detail) ? taskResult.Algorithm : taskResult.Detail,
                null));
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

    private static IReadOnlyList<JsonElement> PrimaryResults(JsonElement detail)
    {
        if (detail.ValueKind == JsonValueKind.Object
            && detail.TryGetProperty("result", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            detail = nested;
        }

        var filtered = ResultArray(detail, "filtered", "filtered_results", "filteredResults");
        if (filtered.Count > 0)
            return filtered;

        var best = FirstObject(detail, "best", "best_result", "bestResult");
        if (best.HasValue)
            return [best.Value];

        return ResultArray(detail, "all", "all_results", "allResults");
    }

    private static List<JsonElement> ResultArray(JsonElement detail, params string[] names)
    {
        var results = new List<JsonElement>();
        if (detail.ValueKind != JsonValueKind.Object)
            return results;

        foreach (var name in names)
        {
            if (!detail.TryGetProperty(name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Array)
            {
                results.AddRange(property.EnumerateArray());
                return results;
            }

            if (property.ValueKind == JsonValueKind.Object)
            {
                results.Add(property);
                return results;
            }
        }
        return results;
    }

    private static JsonElement? FirstObject(JsonElement detail, params string[] names)
    {
        if (detail.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (detail.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Object)
                return property;
        }
        return null;
    }

    private static string TemplateValue(JsonElement item, bool taskHit)
    {
        var count = JsonNumber(item, "count");
        if (count.HasValue)
            return count.Value.ToString(CultureInfo.InvariantCulture);

        var score = JsonNumber(item, "score") ?? JsonNumber(item, "confidence") ?? JsonNumber(item, "similarity");
        if (score.HasValue)
            return "hit";

        return taskHit ? "hit" : "matched";
    }

    private static bool HasAnyProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        return names.Any(name => element.TryGetProperty(name, out _));
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
