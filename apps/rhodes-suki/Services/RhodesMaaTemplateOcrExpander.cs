using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record MaaTemplateOcrConfig(
    string IdPrefix,
    int OffsetX,
    int OffsetY,
    int Width,
    int Height,
    int Scale,
    int MaxMatches);

public sealed record MaaDynamicOcrRequest(
    string Entry,
    int X,
    int Y,
    int Width,
    int Height,
    int Scale,
    double TemplateScore)
{
    public string PayloadJson => JsonSerializer.Serialize(new
    {
        recognition = "OCR",
        roi = new[] { X, Y, Width, Height },
        only_rec = true,
        threshold = 0.3,
    });
}

public static class RhodesMaaTemplateOcrExpander
{
    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(
        MaaTemplateOcrConfig config,
        string? recognitionDetailJson)
    {
        if (string.IsNullOrWhiteSpace(config.IdPrefix)
            || config.Width <= 0
            || config.Height <= 0
            || string.IsNullOrWhiteSpace(recognitionDetailJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(recognitionDetailJson);
            var root = document.RootElement;
            var boxes = Array(root, "filtered");
            if (boxes.Count == 0)
                boxes = Array(root, "all");

            var requests = new List<MaaDynamicOcrRequest>();
            foreach (var item in boxes.Take(config.MaxMatches))
            {
                if (!TryBox(item, out var boxX, out var boxY))
                    continue;
                var x = boxX + config.OffsetX;
                var y = boxY + config.OffsetY;
                if (x < 0 || y < 0 || x + config.Width > 1280 || y + config.Height > 720)
                    continue;
                requests.Add(new MaaDynamicOcrRequest(
                    $"{config.IdPrefix}.{requests.Count}",
                    x,
                    y,
                    config.Width,
                    config.Height,
                    config.Scale,
                    Score(item)));
            }
            return requests;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<JsonElement> Array(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Array
                ? property.EnumerateArray().ToArray()
                : [];
    }

    private static bool TryBox(JsonElement item, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("box", out var box)
            || box.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var values = box.EnumerateArray().Take(4).ToArray();
        return values.Length == 4
            && values[0].TryGetInt32(out x)
            && values[1].TryGetInt32(out y);
    }

    private static double Score(JsonElement item)
    {
        return item.ValueKind == JsonValueKind.Object
            && item.TryGetProperty("score", out var score)
            && score.TryGetDouble(out var value)
                ? value
                : 0;
    }
}
