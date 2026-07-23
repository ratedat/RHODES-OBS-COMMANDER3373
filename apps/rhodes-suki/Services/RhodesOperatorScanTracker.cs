using SkiaSharp;

namespace RhodesSuki.Services;

public readonly record struct RhodesOperatorCardFingerprint(
    ulong AverageHash,
    ulong DifferenceHash);

public sealed record RhodesOperatorOcrWorkItem(
    MaaDynamicOcrRequest Request,
    RhodesOperatorCardFingerprint Fingerprint);

public sealed record RhodesOperatorScanSelection(
    IReadOnlyList<RhodesOperatorOcrWorkItem> WorkItems,
    int VisibleCardCount,
    int StableViewportCount);

public sealed class RhodesOperatorScanTracker
{
    private const int CardMatchDistance = 1;
    private const int ViewportMatchDistance = 2;

    private readonly int _maxAttemptsPerCard;
    private readonly List<CardState> _cards = [];
    private IReadOnlyList<RhodesOperatorCardFingerprint> _previousViewport = [];
    private IReadOnlyList<CardState> _currentViewport = [];
    private int _stableViewportCount;

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
                card = new CardState(observation.Fingerprint);
                _cards.Add(card);
            }
            usedCards.Add(card);
            currentCards.Add(card);

            if (card.Resolved || card.Attempts >= _maxAttemptsPerCard)
                continue;
            card.Attempts++;
            workItems.Add(new RhodesOperatorOcrWorkItem(observation.Request, card.Fingerprint));
        }
        _currentViewport = currentCards;

        return new RhodesOperatorScanSelection(
            workItems,
            observations.Length,
            _stableViewportCount);
    }

    public int RecordResult(
        RhodesOperatorCardFingerprint fingerprint,
        bool resolved,
        string operatorId = "")
    {
        var card = _cards.FirstOrDefault(item => item.Fingerprint == fingerprint);
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
                           && item.DifferenceDistance <= 3)
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
                    && differenceDistance <= 4
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

            return new CardObservation(request, new RhodesOperatorCardFingerprint(
                AverageHash(cropped),
                DifferenceHash(cropped)));
        }).ToArray();
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

    private sealed class CardState(RhodesOperatorCardFingerprint fingerprint)
    {
        public RhodesOperatorCardFingerprint Fingerprint { get; } = fingerprint;
        public int Attempts { get; set; }
        public bool Resolved { get; set; }
        public string OperatorId { get; set; } = "";
        public int OperatorInstance { get; set; }
    }

    private sealed record CardObservation(
        MaaDynamicOcrRequest Request,
        RhodesOperatorCardFingerprint Fingerprint);
}
