using SkiaSharp;

namespace RhodesSuki.Services;

public readonly record struct RhodesOperatorCardFingerprint(
    ulong AverageHash,
    ulong DifferenceHash);

public sealed record RhodesOperatorOcrWorkItem(
    MaaDynamicOcrRequest Request,
    RhodesOperatorCardFingerprint Fingerprint,
    long TrackingId);

public sealed record RhodesOperatorScanSelection(
    IReadOnlyList<RhodesOperatorOcrWorkItem> WorkItems,
    int VisibleCardCount,
    int StableViewportCount);

public sealed class RhodesOperatorScanTracker
{
    private const int CardMatchDistance = 3;
    private const int CardDifferenceMatchDistance = 6;
    private const int ViewportMatchDistance = 4;
    private const int ViewportDifferenceMatchDistance = 8;

    private readonly int _maxAttemptsPerCard;
    private readonly List<CardState> _cards = [];
    private IReadOnlyList<RhodesOperatorCardFingerprint> _previousViewport = [];
    private IReadOnlyList<CardState> _currentViewport = [];
    private int _stableViewportCount;
    private long _nextTrackingId = 1;

    public RhodesOperatorScanTracker(int maxAttemptsPerCard = 3)
    {
        _maxAttemptsPerCard = Math.Max(1, maxAttemptsPerCard);
    }

    public bool CanStopCurrentViewport =>
        _stableViewportCount > 0
        && _currentViewport.Count > 0
        && _currentViewport.All(card => card.Resolved || card.Attempts >= _maxAttemptsPerCard);

    public bool CanStopScan =>
        _cards.Count > 0
        && _cards.All(card => card.Resolved || card.Attempts >= _maxAttemptsPerCard);

    public RhodesOperatorScanSelection Select(
        byte[] encodedImage,
        IReadOnlyList<MaaDynamicOcrRequest> requests)
    {
        var observations = Fingerprints(encodedImage, requests);
        var viewport = observations.Select(item => item.Fingerprint).ToArray();
        _stableViewportCount = EquivalentViewport(_previousViewport, viewport)
            ? _stableViewportCount + 1
            : 0;
        _previousViewport = viewport;

        var usedCards = new HashSet<CardState>();
        var currentCards = new List<CardState>(observations.Length);
        var workItems = new List<RhodesOperatorOcrWorkItem>(observations.Length);
        foreach (var observation in observations)
        {
            var card = FindCard(observation.Fingerprint, usedCards);
            if (card is null)
            {
                card = new CardState(_nextTrackingId++, observation.Fingerprint);
                _cards.Add(card);
            }
            else
            {
                // Compare the next frame against the most recent appearance. Horizontal card
                // scrolling otherwise accumulates small hash drift until one card looks new.
                card.Fingerprint = observation.Fingerprint;
            }
            usedCards.Add(card);
            currentCards.Add(card);

            if (card.Resolved || card.Attempts >= _maxAttemptsPerCard)
                continue;
            card.Attempts++;
            workItems.Add(new RhodesOperatorOcrWorkItem(
                observation.Request,
                card.Fingerprint,
                card.TrackingId));
        }
        _currentViewport = currentCards;

        return new RhodesOperatorScanSelection(
            workItems,
            observations.Length,
            _stableViewportCount);
    }

    public int RecordResult(
        long trackingId,
        bool resolved,
        string operatorId = "")
    {
        var card = _cards.FirstOrDefault(item => item.TrackingId == trackingId);
        if (card is null)
            return 0;
        if (resolved)
        {
            card.Resolved = true;
            if (!string.IsNullOrWhiteSpace(operatorId) && card.OperatorInstance <= 0)
            {
                card.OperatorId = operatorId.Trim();
                var cardIndex = _cards.IndexOf(card);
                card.OperatorInstance = _cards
                    .Take(cardIndex)
                    .Count(item => item.OperatorId.Equals(card.OperatorId, StringComparison.Ordinal))
                    + 1;
            }
        }
        return Math.Max(1, card.OperatorInstance);
    }

    private CardState? FindCard(
        RhodesOperatorCardFingerprint fingerprint,
        IReadOnlySet<CardState> excluded)
    {
        return _cards
            .Where(card => !excluded.Contains(card))
            .Select(card => new
            {
                Card = card,
                AverageDistance = RhodesRecognitionFrameFingerprint.Distance(
                    card.Fingerprint.AverageHash,
                    fingerprint.AverageHash),
                DifferenceDistance = RhodesRecognitionFrameFingerprint.Distance(
                    card.Fingerprint.DifferenceHash,
                    fingerprint.DifferenceHash),
            })
            .Where(item => item.AverageDistance <= CardMatchDistance
                           && item.DifferenceDistance <= CardDifferenceMatchDistance)
            .OrderBy(item => item.AverageDistance)
            .ThenBy(item => item.DifferenceDistance)
            .Select(item => item.Card)
            .FirstOrDefault();
    }

    private static bool EquivalentViewport(
        IReadOnlyList<RhodesOperatorCardFingerprint> left,
        IReadOnlyList<RhodesOperatorCardFingerprint> right)
    {
        if (left.Count == 0 || left.Count != right.Count)
            return false;

        var used = new bool[right.Count];
        foreach (var fingerprint in left)
        {
            var matchIndex = -1;
            var matchAverageDistance = int.MaxValue;
            var matchDifferenceDistance = int.MaxValue;
            for (var index = 0; index < right.Count; index++)
            {
                if (used[index])
                    continue;
                var averageDistance = RhodesRecognitionFrameFingerprint.Distance(
                    fingerprint.AverageHash,
                    right[index].AverageHash);
                var differenceDistance = RhodesRecognitionFrameFingerprint.Distance(
                    fingerprint.DifferenceHash,
                    right[index].DifferenceHash);
                if (averageDistance <= ViewportMatchDistance
                    && differenceDistance <= ViewportDifferenceMatchDistance
                    && (averageDistance < matchAverageDistance
                        || averageDistance == matchAverageDistance
                        && differenceDistance < matchDifferenceDistance))
                {
                    matchIndex = index;
                    matchAverageDistance = averageDistance;
                    matchDifferenceDistance = differenceDistance;
                }
            }
            if (matchIndex < 0)
                return false;
            used[matchIndex] = true;
        }
        return true;
    }

    private static CardObservation[] Fingerprints(
        byte[] encodedImage,
        IReadOnlyList<MaaDynamicOcrRequest> requests)
    {
        // Requests are the 188x29 name strips derived from CODENAME anchors; portraits and skins stay outside them.
        using var source = SKBitmap.Decode(encodedImage)
            ?? throw new InvalidOperationException("オペレーターFrame画像をデコードできません。");
        var scaleX = source.Width / 1280d;
        var scaleY = source.Height / 720d;
        return requests.Select(request =>
        {
            var left = Math.Clamp((int)Math.Round(request.X * scaleX), 0, source.Width - 1);
            var top = Math.Clamp((int)Math.Round(request.Y * scaleY), 0, source.Height - 1);
            var width = Math.Clamp((int)Math.Round(request.Width * scaleX), 1, source.Width - left);
            var height = Math.Clamp((int)Math.Round(request.Height * scaleY), 1, source.Height - top);
            using var cropped = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(cropped))
            {
                canvas.DrawBitmap(
                    source,
                    new SKRectI(left, top, left + width, top + height),
                    new SKRect(0, 0, width, height));
            }
            using var normalized = NormalizeFingerprintSource(cropped);

            return new CardObservation(request, new RhodesOperatorCardFingerprint(
                AverageHash(normalized),
                DifferenceHash(normalized)));
        }).ToArray();
    }

    private static SKBitmap NormalizeFingerprintSource(SKBitmap source)
    {
        var minX = source.Width;
        var minY = source.Height;
        var maxX = -1;
        var maxY = -1;
        var foregroundPixels = 0;
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var color = source.GetPixel(x, y);
                var luminance = Luminance(color);
                var chroma = Math.Max(color.Red, Math.Max(color.Green, color.Blue))
                             - Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                if (luminance < 92 || luminance < 135 && chroma < 18)
                    continue;
                foregroundPixels++;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        if (foregroundPixels < 8 || maxX < minX || maxY < minY)
            return source.Copy();

        minX = Math.Max(0, minX - 2);
        minY = Math.Max(0, minY - 2);
        maxX = Math.Min(source.Width - 1, maxX + 2);
        maxY = Math.Min(source.Height - 1, maxY + 2);
        var normalized = new SKBitmap(maxX - minX + 1, maxY - minY + 1);
        using var canvas = new SKCanvas(normalized);
        canvas.DrawBitmap(
            source,
            new SKRectI(minX, minY, maxX + 1, maxY + 1),
            new SKRect(0, 0, normalized.Width, normalized.Height));
        return normalized;
    }

    private static ulong AverageHash(SKBitmap source)
    {
        using var reduced = source.Resize(new SKImageInfo(8, 8), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("オペレーター名画像を縮小できません。");
        Span<int> luminance = stackalloc int[64];
        var total = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var value = Luminance(reduced.GetPixel(x, y));
                luminance[y * 8 + x] = value;
                total += value;
            }
        }

        var average = total / 64;
        ulong result = 0;
        for (var index = 0; index < luminance.Length; index++)
        {
            if (luminance[index] >= average)
                result |= 1UL << index;
        }
        return result;
    }

    private static ulong DifferenceHash(SKBitmap source)
    {
        using var reduced = source.Resize(new SKImageInfo(9, 8), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("オペレーター名画像を縮小できません。");
        ulong result = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (Luminance(reduced.GetPixel(x, y)) > Luminance(reduced.GetPixel(x + 1, y)))
                    result |= 1UL << (y * 8 + x);
            }
        }
        return result;
    }

    private static int Luminance(SKColor color) =>
        (color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000;

    private sealed class CardState(
        long trackingId,
        RhodesOperatorCardFingerprint fingerprint)
    {
        public long TrackingId { get; } = trackingId;
        public RhodesOperatorCardFingerprint Fingerprint { get; set; } = fingerprint;
        public int Attempts { get; set; }
        public bool Resolved { get; set; }
        public string OperatorId { get; set; } = "";
        public int OperatorInstance { get; set; }
    }

    private sealed record CardObservation(
        MaaDynamicOcrRequest Request,
        RhodesOperatorCardFingerprint Fingerprint);
}
