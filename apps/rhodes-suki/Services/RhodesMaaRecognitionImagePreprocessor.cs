using System.Text.Json.Nodes;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record MaaPreparedRecognitionInput(byte[] EncodedImage, string ParametersJson);

public static class RhodesMaaRecognitionImagePreprocessor
{
    public static MaaPreparedRecognitionInput Prepare(
        byte[] encodedImage,
        string recognitionType,
        string parametersJson,
        int scale,
        string entry = "")
    {
        var operatorNameEntry = entry.StartsWith("operator.card.name.", StringComparison.Ordinal);
        if (!string.Equals(recognitionType, "OCR", StringComparison.Ordinal)
            || scale <= 1 && !operatorNameEntry
            || encodedImage.Length == 0)
        {
            return new MaaPreparedRecognitionInput(encodedImage, parametersJson);
        }

        JsonObject? parameters;
        try
        {
            parameters = JsonNode.Parse(parametersJson)?.AsObject();
        }
        catch
        {
            return new MaaPreparedRecognitionInput(encodedImage, parametersJson);
        }

        if (parameters?["roi"] is not JsonArray roi
            || roi.Count < 4
            || !TryInt(roi[0], out var x)
            || !TryInt(roi[1], out var y)
            || !TryInt(roi[2], out var width)
            || !TryInt(roi[3], out var height)
            || width <= 0
            || height <= 0)
        {
            return new MaaPreparedRecognitionInput(encodedImage, parametersJson);
        }

        using var source = SKBitmap.Decode(encodedImage);
        if (source is null)
            return new MaaPreparedRecognitionInput(encodedImage, parametersJson);

        var left = Math.Clamp(x, 0, source.Width);
        var top = Math.Clamp(y, 0, source.Height);
        var right = Math.Clamp(x + width, left, source.Width);
        var bottom = Math.Clamp(y + height, top, source.Height);
        var cropWidth = right - left;
        var cropHeight = bottom - top;
        if (cropWidth <= 0 || cropHeight <= 0)
            return new MaaPreparedRecognitionInput(encodedImage, parametersJson);

        if (operatorNameEntry)
        {
            return PrepareMaaOperatorName(
                source,
                left,
                top,
                cropWidth,
                cropHeight,
                Math.Clamp(scale, 1, 12),
                parameters);
        }

        var targetWidth = checked(cropWidth * Math.Clamp(scale, 1, 12));
        var targetHeight = checked(cropHeight * Math.Clamp(scale, 1, 12));
        using var scaled = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawBitmap(
                source,
                new SKRect(left, top, right, bottom),
                new SKRect(0, 0, targetWidth, targetHeight),
                null);
        }

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        parameters["roi"] = new JsonArray(0, 0, targetWidth, targetHeight);
        return new MaaPreparedRecognitionInput(data.ToArray(), parameters.ToJsonString());
    }

    private static MaaPreparedRecognitionInput PrepareMaaOperatorName(
        SKBitmap source,
        int left,
        int top,
        int width,
        int height,
        int scale,
        JsonObject parameters)
    {
        const int lowerThreshold = 170;
        var foreground = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                foreground[y * width + x] =
                    Luminance(source.GetPixel(left + x, top + y)) >= lowerThreshold;
            }
        }

        RemoveLongTopHorizontalRuns(foreground, width, height);
        RemoveRightBorderForeground(foreground, width, height);

        var foregroundLeft = width;
        var foregroundTop = height;
        var foregroundRight = -1;
        var foregroundBottom = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!foreground[y * width + x])
                    continue;
                foregroundLeft = Math.Min(foregroundLeft, x);
                foregroundTop = Math.Min(foregroundTop, y);
                foregroundRight = Math.Max(foregroundRight, x);
                foregroundBottom = Math.Max(foregroundBottom, y);
            }
        }

        if (foregroundRight < foregroundLeft || foregroundBottom < foregroundTop)
        {
            return CropAndScale(source, left, top, width, height, scale, parameters);
        }

        const int expansion = 2;
        var expandedLeft = Math.Max(0, foregroundLeft - expansion);
        var expandedTop = Math.Max(0, foregroundTop - expansion);
        var expandedRight = Math.Min(width - 1, foregroundRight + expansion);
        var expandedBottom = Math.Min(height - 1, foregroundBottom + expansion);
        return CropAndScale(
            source,
            left + expandedLeft,
            top + expandedTop,
            expandedRight - expandedLeft + 1,
            expandedBottom - expandedTop + 1,
            scale,
            parameters);
    }

    private static MaaPreparedRecognitionInput CropAndScale(
        SKBitmap source,
        int left,
        int top,
        int width,
        int height,
        int scale,
        JsonObject parameters)
    {
        using var cropped = new SKBitmap(width, height, source.ColorType, source.AlphaType);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(
                source,
                new SKRect(left, top, left + width, top + height),
                new SKRect(0, 0, width, height),
                null);
        }
        return EncodePrepared(cropped, scale, parameters);
    }

    private static MaaPreparedRecognitionInput EncodePrepared(
        SKBitmap source,
        int scale,
        JsonObject parameters)
    {
        var targetWidth = checked(source.Width * scale);
        var targetHeight = checked(source.Height * scale);
        using var scaled = new SKBitmap(targetWidth, targetHeight, source.ColorType, source.AlphaType);
        using (var canvas = new SKCanvas(scaled))
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawBitmap(
                source,
                new SKRect(0, 0, source.Width, source.Height),
                new SKRect(0, 0, targetWidth, targetHeight),
                null);
        }

        using var image = SKImage.FromBitmap(scaled);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        parameters["roi"] = new JsonArray(0, 0, targetWidth, targetHeight);
        return new MaaPreparedRecognitionInput(data.ToArray(), parameters.ToJsonString());
    }

    private static int Luminance(SKColor color) =>
        (299 * color.Red + 587 * color.Green + 114 * color.Blue + 500) / 1000;

    private static void RemoveRightBorderForeground(bool[] foreground, int width, int height)
    {
        var visited = new bool[foreground.Length];
        var minimumBackgroundArea = Math.Max(16, width * height / 20);
        for (var y = 0; y < height; y++)
        {
            var seed = y * width + width - 1;
            if (!foreground[seed] || visited[seed])
                continue;

            var queue = new Queue<int>();
            var component = new List<int>();
            queue.Enqueue(seed);
            visited[seed] = true;
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                component.Add(index);
                var x = index % width;
                var currentY = index / width;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                            continue;
                        var nextX = x + offsetX;
                        var nextY = currentY + offsetY;
                        if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height)
                            continue;
                        var next = nextY * width + nextX;
                        if (!foreground[next] || visited[next])
                            continue;
                        visited[next] = true;
                        queue.Enqueue(next);
                    }
                }
            }

            if (component.Count < minimumBackgroundArea)
                continue;
            foreach (var index in component)
                foreground[index] = false;
        }
    }

    private static void RemoveLongTopHorizontalRuns(bool[] foreground, int width, int height)
    {
        var minimumRun = Math.Max(4, width / 8);
        var rowLimit = Math.Max(1, height / 2);
        for (var y = 0; y < rowLimit; y++)
        {
            var x = 0;
            while (x < width)
            {
                while (x < width && !foreground[y * width + x])
                    x++;
                var start = x;
                while (x < width && foreground[y * width + x])
                    x++;
                if (x - start < minimumRun)
                    continue;
                for (var clearX = start; clearX < x; clearX++)
                    foreground[y * width + clearX] = false;
            }
        }
    }

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }
}
