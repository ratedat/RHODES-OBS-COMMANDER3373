using SkiaSharp;

namespace RhodesSuki.Services;

internal static class RhodesRelicStackImageSeparator
{
    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int SearchOffsetX = -104;
    private const int SearchOffsetY = 54;
    private const int SearchWidth = 108;
    private const int SearchHeight = 48;
    private const int LeftPadding = 2;
    private const int RightPadding = 1;
    private const int VerticalPadding = 2;

    public static bool TryLocateDigits(SKBitmap bitmap, int titleX, int titleY, out SKRectI digitBounds)
    {
        digitBounds = SKRectI.Empty;
        if (bitmap.Width <= 0 || bitmap.Height <= 0)
            return false;

        var xScale = bitmap.Width / (double)BaseWidth;
        var yScale = bitmap.Height / (double)BaseHeight;
        var search = ToImageRect(
            new SKRectI(
                titleX + SearchOffsetX,
                titleY + SearchOffsetY,
                titleX + SearchOffsetX + SearchWidth,
                titleY + SearchOffsetY + SearchHeight),
            bitmap,
            xScale,
            yScale);
        if (search.Width <= 0 || search.Height <= 0)
            return false;

        var matches = FindComponents(bitmap, search, xScale, yScale)
            .Where(component => LooksLikeArrow(component, titleX, titleY))
            .Select(arrow => TryLocateDigitInk(bitmap, arrow, titleX, xScale, yScale, out var digits)
                ? new BadgeMatch(arrow, digits)
                : null)
            .Where(match => match is not null)
            .Cast<BadgeMatch>()
            .ToArray();
        if (matches.Length != 1)
            return false;

        var left = Math.Max(matches[0].Arrow.Bounds.Right + 3, matches[0].Digits.Left - LeftPadding);
        var top = Math.Max(matches[0].Arrow.Bounds.Top, matches[0].Digits.Top - VerticalPadding);
        var right = Math.Min(
            BaseWidth,
            Math.Max(matches[0].Digits.Right + RightPadding, left + 5));
        var bottom = Math.Min(
            Math.Min(BaseHeight, matches[0].Arrow.Bounds.Bottom + 4),
            matches[0].Digits.Bottom + VerticalPadding);
        if (right <= left || bottom <= top)
            return false;
        digitBounds = new SKRectI(left, top, right, bottom);
        return true;
    }

    public static bool IsDigitForeground(SKColor color)
    {
        var minimum = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        return minimum >= 180 && IsNeutral(color);
    }

    public static bool IsNeutral(SKColor color)
    {
        var maximum = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        var minimum = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
        return maximum - minimum <= 42;
    }

    private static IReadOnlyList<Component> FindComponents(
        SKBitmap bitmap,
        SKRectI search,
        double xScale,
        double yScale)
    {
        var width = search.Width;
        var height = search.Height;
        var foreground = new bool[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            foreground[y * width + x] = IsDigitForeground(bitmap.GetPixel(search.Left + x, search.Top + y));

        var visited = new bool[foreground.Length];
        var components = new List<Component>();
        for (var seed = 0; seed < foreground.Length; seed++)
        {
            if (!foreground[seed] || visited[seed])
                continue;

            var queue = new Queue<int>();
            queue.Enqueue(seed);
            visited[seed] = true;
            var left = width;
            var top = height;
            var right = -1;
            var bottom = -1;
            var area = 0;
            var rows = new Dictionary<int, (int Left, int Right)>();
            var points = new List<(int X, int Y)>();
            while (queue.Count > 0)
            {
                var index = queue.Dequeue();
                var x = index % width;
                var y = index / width;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
                area++;
                points.Add((x, y));
                if (!rows.TryGetValue(y, out var span))
                    rows[y] = (x, x);
                else
                    rows[y] = (Math.Min(span.Left, x), Math.Max(span.Right, x));

                for (var offsetY = -1; offsetY <= 1; offsetY++)
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                        continue;
                    var nextX = x + offsetX;
                    var nextY = y + offsetY;
                    if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height)
                        continue;
                    var next = nextY * width + nextX;
                    if (!foreground[next] || visited[next])
                        continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            var imageBounds = new SKRectI(
                search.Left + left,
                search.Top + top,
                search.Left + right + 1,
                search.Top + bottom + 1);
            var baseBounds = new SKRectI(
                (int)Math.Floor(imageBounds.Left / xScale),
                (int)Math.Floor(imageBounds.Top / yScale),
                (int)Math.Ceiling(imageBounds.Right / xScale),
                (int)Math.Ceiling(imageBounds.Bottom / yScale));
            var split = top + Math.Max(1, (bottom - top + 1) * 3 / 5);
            var topSpan = rows.Where(row => row.Key < split)
                .Select(row => row.Value.Right - row.Value.Left + 1)
                .DefaultIfEmpty(0)
                .Max() / xScale;
            var bottomSpan = rows.Where(row => row.Key >= split)
                .Select(row => row.Value.Right - row.Value.Left + 1)
                .DefaultIfEmpty(0)
                .Max() / xScale;
            var centerX = (left + right) / 2.0;
            var centerRadius = Math.Max(1.0, xScale * 1.5);
            var upperLimit = top + Math.Max(1, (bottom - top + 1) * 3 / 5);
            var upperCenterFilled = points.Any(point => point.Y < upperLimit
                && Math.Abs(point.X - centerX) <= centerRadius);
            var lowerSplit = points.Where(point => point.Y >= upperLimit)
                .GroupBy(point => point.Y)
                .Any(row => row.Any(point => point.X < centerX - centerRadius)
                    && row.Any(point => point.X > centerX + centerRadius)
                    && row.All(point => Math.Abs(point.X - centerX) > centerRadius));
            components.Add(new Component(
                baseBounds,
                area / (xScale * yScale),
                topSpan,
                bottomSpan,
                upperCenterFilled && lowerSplit));
        }
        return components;
    }

    private static bool LooksLikeArrow(Component component, int titleX, int titleY)
    {
        var bounds = component.Bounds;
        return bounds.Left >= titleX - 100
            && bounds.Right <= titleX - 35
            && bounds.Top >= titleY + 55
            && bounds.Top <= titleY + 84
            && bounds.Width is >= 10 and <= 32
            && bounds.Height is >= 10 and <= 32
            && component.Area >= 60
            && component.Area / (bounds.Width * bounds.Height) >= 0.42
            && (component.HasChevronPattern || component.TopSpan >= component.BottomSpan + 3);
    }

    private static bool TryLocateDigitInk(
        SKBitmap bitmap,
        Component arrow,
        int titleX,
        double xScale,
        double yScale,
        out SKRectI bounds)
    {
        bounds = SKRectI.Empty;
        var requested = new SKRectI(
            arrow.Bounds.Right + 3,
            arrow.Bounds.Top,
            Math.Min(titleX + 2, arrow.Bounds.Right + 50),
            arrow.Bounds.Bottom + 4);
        var search = ToImageRect(requested, bitmap, xScale, yScale);
        if (search.Width <= 0 || search.Height <= 0)
            return false;

        var columns = new List<int>();
        for (var x = search.Left; x < search.Right; x++)
        {
            var count = 0;
            for (var y = search.Top; y < search.Bottom; y++)
                if (IsDigitForeground(bitmap.GetPixel(x, y)))
                    count++;
            if (count >= Math.Max(2, (int)Math.Floor(yScale * 2)))
                columns.Add(x);
        }
        if (columns.Count == 0)
            return false;

        var first = columns[0];
        var last = first;
        foreach (var column in columns.Skip(1))
        {
            if ((column - last) / xScale > 12)
                break;
            last = column;
        }

        var top = search.Bottom;
        var bottom = -1;
        var area = 0;
        for (var y = search.Top; y < search.Bottom; y++)
        for (var x = first; x <= last; x++)
        {
            if (!IsDigitForeground(bitmap.GetPixel(x, y)))
                continue;
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
            area++;
        }
        if (bottom < top || area / (xScale * yScale) < 8)
            return false;

        bounds = new SKRectI(
            (int)Math.Floor(first / xScale),
            (int)Math.Floor(top / yScale),
            (int)Math.Ceiling((last + 1) / xScale),
            (int)Math.Ceiling((bottom + 1) / yScale));
        return bounds.Width is >= 1 and <= 45 && bounds.Height is >= 7 and <= 28;
    }

    private static SKRectI ToImageRect(
        SKRectI requested,
        SKBitmap bitmap,
        double xScale,
        double yScale) => new(
            Math.Clamp((int)Math.Floor(requested.Left * xScale), 0, bitmap.Width),
            Math.Clamp((int)Math.Floor(requested.Top * yScale), 0, bitmap.Height),
            Math.Clamp((int)Math.Ceiling(requested.Right * xScale), 0, bitmap.Width),
            Math.Clamp((int)Math.Ceiling(requested.Bottom * yScale), 0, bitmap.Height));

    private sealed record Component(
        SKRectI Bounds,
        double Area,
        double TopSpan,
        double BottomSpan,
        bool HasChevronPattern);

    private sealed record BadgeMatch(Component Arrow, SKRectI Digits);
}
