using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSuiActiveCoinViewportRow(
    string CoinId,
    string Label,
    string StatusId,
    double Confidence,
    int SourcePosition);

public sealed record RhodesSuiActiveCoinScanSnapshot(
    int? ExpectedCount,
    IReadOnlyList<RhodesSuiActiveCoinViewportRow> Rows,
    bool IsComplete,
    bool IsAmbiguous,
    int ViewportCount);

public sealed class RhodesSuiActiveCoinScanTracker
{
    private readonly List<RhodesSuiActiveCoinViewportRow> _rows = [];
    private int? _expectedCount;
    private bool _isAmbiguous;
    private int _viewportCount;

    public void SetExpectedCount(int? expectedCount)
    {
        if (expectedCount is not (>= 1 and <= 99))
            return;

        _expectedCount = expectedCount;
        if (_rows.Count > _expectedCount.Value)
            _isAmbiguous = true;
    }

    public RhodesSuiActiveCoinScanSnapshot RecordViewport(
        IEnumerable<RhodesSuiActiveCoinViewportRow> rows,
        bool viewportMoved)
    {
        var incoming = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.CoinId))
            .ToArray();
        _viewportCount++;
        if (incoming.Length == 0)
        {
            _isAmbiguous = true;
            return Snapshot();
        }

        if (_rows.Count == 0)
        {
            if (_expectedCount is not null && incoming.Length > _expectedCount.Value)
            {
                _isAmbiguous = true;
                return Snapshot();
            }

            _rows.AddRange(incoming);
            return Snapshot();
        }

        var overlap = SelectOverlap(incoming, viewportMoved);
        if (overlap < 0)
        {
            _isAmbiguous = true;
            return Snapshot();
        }

        for (var index = 0; index < overlap; index++)
        {
            var existingIndex = _rows.Count - overlap + index;
            _rows[existingIndex] = PreferEvidence(_rows[existingIndex], incoming[index]);
        }
        _rows.AddRange(incoming.Skip(overlap));
        return Snapshot();
    }

    public MaaTaskRunResult CreateConsolidatedResult()
    {
        var detections = _rows
            .Select((row, index) => new RhodesSuiCoinImageDetection(
                row.CoinId,
                row.Label,
                row.Confidence,
                index,
                new MaaRoi(0, row.SourcePosition, 1, 1),
                row.StatusId))
            .ToArray();
        return RhodesSuiCoinImageRecognizer.CreateActiveConsolidatedResult(detections);
    }

    private int SelectOverlap(
        IReadOnlyList<RhodesSuiActiveCoinViewportRow> incoming,
        bool viewportMoved)
    {
        var maximum = Math.Min(_rows.Count, incoming.Count);
        var fallback = -1;
        for (var overlap = maximum; overlap >= 0; overlap--)
        {
            if (!MatchesOverlap(incoming, overlap))
                continue;

            var mergedCount = _rows.Count + incoming.Count - overlap;
            if (_expectedCount is not null && mergedCount > _expectedCount.Value)
                continue;

            fallback = fallback < 0 ? overlap : fallback;
            var needsProgress = viewportMoved
                && _expectedCount is not null
                && _rows.Count < _expectedCount.Value;
            if (needsProgress && mergedCount <= _rows.Count)
                continue;
            return overlap;
        }
        return fallback;
    }

    private bool MatchesOverlap(
        IReadOnlyList<RhodesSuiActiveCoinViewportRow> incoming,
        int overlap)
    {
        for (var index = 0; index < overlap; index++)
        {
            if (!_rows[_rows.Count - overlap + index].CoinId.Equals(
                    incoming[index].CoinId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static RhodesSuiActiveCoinViewportRow PreferEvidence(
        RhodesSuiActiveCoinViewportRow existing,
        RhodesSuiActiveCoinViewportRow incoming)
    {
        var preferred = incoming.Confidence > existing.Confidence ? incoming : existing;
        var statusId = !string.IsNullOrWhiteSpace(incoming.StatusId)
            ? incoming.StatusId
            : existing.StatusId;
        return preferred with { StatusId = statusId };
    }

    private RhodesSuiActiveCoinScanSnapshot Snapshot() => new(
        _expectedCount,
        _rows.ToArray(),
        _expectedCount is not null && _rows.Count == _expectedCount.Value,
        _isAmbiguous,
        _viewportCount);
}
