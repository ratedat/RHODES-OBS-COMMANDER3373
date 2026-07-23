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
    double TemplateScore,
    bool OnlyRecognition = true)
{
    public string PayloadJson => JsonSerializer.Serialize(new
    {
        recognition = "OCR",
        roi = new[] { X, Y, Width, Height },
        only_rec = OnlyRecognition,
        threshold = 0.3,
    });
}

public static class RhodesMaaTemplateOcrExpander
{
    private const string OperatorCardNamePrefix = "operator.card.name";
    private const double OperatorGridSupplementMinimumScore = 0.60;
    private const int OperatorGridTolerance = 3;
    private const int MinimumVisibleOperatorNameWidth = 96;

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
            var boxes = SelectBoxes(root, config);

            var requests = new List<MaaDynamicOcrRequest>();
            foreach (var item in boxes.Take(config.MaxMatches))
            {
                if (!TryBox(item, out var boxX, out var boxY))
                    continue;
                var x = boxX + config.OffsetX;
                var y = boxY + config.OffsetY;
                var width = VisibleWidth(config, x);
                if (x < 0 || y < 0 || width <= 0 || y + config.Height > 720)
                    continue;
                requests.Add(new MaaDynamicOcrRequest(
                    $"{config.IdPrefix}.{requests.Count}",
                    x,
                    y,
                    width,
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

    private static IReadOnlyList<JsonElement> SelectBoxes(
        JsonElement root,
        MaaTemplateOcrConfig config)
    {
        var filtered = Array(root, "filtered");
        var all = Array(root, "all");
        if (filtered.Count == 0)
            return all;
        if (!IsOperatorCardName(config) || all.Count == 0 || filtered.Count >= config.MaxMatches)
            return filtered;

        var selected = filtered.Take(config.MaxMatches).ToList();
        var selectedPoints = selected
            .Select(item => TryBox(item, out var x, out var y) ? new BoxPoint(x, y) : (BoxPoint?)null)
            .Where(point => point.HasValue)
            .Select(point => point!.Value)
            .ToArray();

        foreach (var item in all)
        {
            if (selected.Count >= config.MaxMatches
                || Score(item) < OperatorGridSupplementMinimumScore
                || !TryBox(item, out var x, out var y)
                || selectedPoints.Any(point => IsSameGridPoint(point, x, y)))
            {
                continue;
            }

            var columnMatches = selectedPoints.Count(point => Math.Abs(point.X - x) <= OperatorGridTolerance);
            var rowMatches = selectedPoints.Any(point => Math.Abs(point.Y - y) <= OperatorGridTolerance);
            if (columnMatches >= 2 && rowMatches)
                selected.Add(item);
        }

        return selected;
    }

    private static int VisibleWidth(MaaTemplateOcrConfig config, int x)
    {
        if (x < 0 || x >= 1280)
            return 0;
        if (x + config.Width <= 1280)
            return config.Width;
        if (!IsOperatorCardName(config))
            return 0;

        var visibleWidth = 1280 - x;
        return visibleWidth >= MinimumVisibleOperatorNameWidth ? visibleWidth : 0;
    }

    private static bool IsOperatorCardName(MaaTemplateOcrConfig config)
    {
        return config.IdPrefix.Equals(OperatorCardNamePrefix, StringComparison.Ordinal);
    }

    private static bool IsSameGridPoint(BoxPoint point, int x, int y)
    {
        return Math.Abs(point.X - x) <= OperatorGridTolerance
            && Math.Abs(point.Y - y) <= OperatorGridTolerance;
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

    private readonly record struct BoxPoint(int X, int Y);
}
