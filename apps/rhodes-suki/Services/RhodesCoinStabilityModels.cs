using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhodesSuki.Services;

public sealed record RhodesCoinStabilityStatusExpectation(
    string Kind,
    string StatusId = "");

public sealed record RhodesCoinStabilitySlotExpectation(
    int SlotIndex,
    bool Present,
    string CoinId,
    RhodesCoinStabilityStatusExpectation Status);

public sealed record RhodesCoinStabilityFrameExpectation(
    string FrameId,
    string ProfileId,
    int PassIndex,
    IReadOnlyList<RhodesCoinStabilitySlotExpectation> Slots);

public sealed record RhodesCoinStabilityManifest(
    int SchemaVersion,
    IReadOnlyList<RhodesCoinStabilityFrameExpectation> Frames)
{
    private static readonly HashSet<string> SupportedProfiles =
    [
        "is6ActiveCoinsFull",
        "is6CoinsFull",
    ];

    private static readonly HashSet<string> SupportedStatusKinds =
    [
        "known",
        "none",
        "unknown",
    ];

    public static RhodesCoinStabilityManifest Parse(string json)
    {
        RhodesCoinStabilityManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RhodesCoinStabilityManifest>(
                json,
                RhodesCoinStabilityJson.Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"銭ゴールデンmanifestを解析できません: {ex.Message}", ex);
        }

        if (manifest is null)
            throw new InvalidOperationException("銭ゴールデンmanifestが空です。");

        manifest.Validate();
        return manifest;
    }

    public static RhodesCoinStabilityManifest Load(string path) =>
        Parse(File.ReadAllText(path, Encoding.UTF8));

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidOperationException($"未対応のmanifest schemaVersionです: {SchemaVersion}");

        var frameKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var frame in Frames ?? [])
        {
            if (string.IsNullOrWhiteSpace(frame.FrameId))
                throw new InvalidOperationException("manifest frameIdは必須です。");
            if (!SupportedProfiles.Contains(frame.ProfileId))
                throw new InvalidOperationException($"未対応の銭profileIdです: {frame.ProfileId}");
            if (frame.PassIndex < 0)
                throw new InvalidOperationException($"passIndexは0以上で指定してください: {frame.FrameId}");

            var frameKey = $"{frame.FrameId}\u001f{frame.ProfileId}\u001f{frame.PassIndex}";
            if (!frameKeys.Add(frameKey))
                throw new InvalidOperationException($"manifest内でframe/passが重複しています: {frameKey}");

            var slotIndices = new HashSet<int>();
            var maximumSlot = frame.ProfileId.Equals("is6ActiveCoinsFull", StringComparison.Ordinal) ? 2 : 8;
            foreach (var slot in frame.Slots ?? [])
            {
                if (slot.SlotIndex < 0 || slot.SlotIndex > maximumSlot)
                    throw new InvalidOperationException(
                        $"slotIndexがprofileの範囲外です: {frame.FrameId} slot={slot.SlotIndex}");
                if (!slotIndices.Add(slot.SlotIndex))
                    throw new InvalidOperationException(
                        $"manifest内でslotIndexが重複しています: {frame.FrameId} slot={slot.SlotIndex}");
                if (slot.Present && string.IsNullOrWhiteSpace(slot.CoinId))
                    throw new InvalidOperationException(
                        $"present=trueのslotにはcoinIdが必要です: {frame.FrameId} slot={slot.SlotIndex}");
                if (!slot.Present && !string.IsNullOrWhiteSpace(slot.CoinId))
                    throw new InvalidOperationException(
                        $"present=falseのslotにcoinIdは指定できません: {frame.FrameId} slot={slot.SlotIndex}");
                if (slot.Status is null || !SupportedStatusKinds.Contains(slot.Status.Kind))
                    throw new InvalidOperationException(
                        $"status.kindはknown/none/unknownで指定してください: {frame.FrameId} slot={slot.SlotIndex}");
                if (slot.Status.Kind.Equals("known", StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(slot.Status.StatusId))
                {
                    throw new InvalidOperationException(
                        $"status.kind=knownにはstatusIdが必要です: {frame.FrameId} slot={slot.SlotIndex}");
                }
                if (!slot.Status.Kind.Equals("known", StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(slot.Status.StatusId))
                {
                    throw new InvalidOperationException(
                        $"status.kind={slot.Status.Kind}にstatusIdは指定できません: {frame.FrameId} slot={slot.SlotIndex}");
                }
            }
        }
    }
}

public sealed record RhodesCoinStabilitySlotObservation(
    string FrameId,
    string ProfileId,
    int PassIndex,
    int SlotIndex,
    bool Present,
    string CoinId,
    string StatusId,
    double Score,
    double RunnerUpScore,
    double VisualStrength,
    double StatusScore,
    string PredictedStatusId,
    int[] Roi,
    string ImageSha256,
    string Source,
    string EvidenceSource,
    string SavedCoinId = "",
    string SavedStatusId = "",
    double CoinVisualStrength = 0)
{
    public static RhodesCoinStabilitySlotObservation Create(
        string frameId,
        string profileId,
        int passIndex,
        int slotIndex,
        string coinId,
        string statusId) =>
        new(
            frameId,
            profileId,
            passIndex,
            slotIndex,
            !string.IsNullOrWhiteSpace(coinId),
            coinId,
            statusId,
            0,
            0,
            0,
            0,
            "",
            [],
            "",
            "",
            "test");
}

public sealed record RhodesCoinStabilityError(
    string FrameId,
    string ProfileId,
    int PassIndex,
    int SlotIndex,
    string ErrorClass,
    string ExpectedCoinId,
    string ExpectedStatusId,
    string ActualCoinId,
    string ActualStatusId,
    string Detail);

public sealed record RhodesCoinStabilityCandidateDiff(
    string FrameId,
    string ProfileId,
    int PassIndex,
    IReadOnlyList<string> Expected,
    IReadOnlyList<string> Observed,
    IReadOnlyList<string> Saved,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Unexpected,
    int CandidateSplitCount);

public sealed record RhodesCoinStabilityTiming(
    double DecodeMilliseconds,
    double AnchorMilliseconds,
    double CoinMatchingMilliseconds,
    double StatusMatchingMilliseconds,
    double OcrMilliseconds,
    long CoinComparisonCount,
    long StatusColorComparisonCount,
    long StatusShapeComparisonCount,
    long OverlayComparisonCount,
    int OcrTaskCount);

public sealed class RhodesCoinRecognitionDiagnostics
{
    public double DecodeMilliseconds { get; private set; }
    public double AnchorMilliseconds { get; private set; }
    public double CoinMatchingMilliseconds { get; private set; }
    public double StatusMatchingMilliseconds { get; private set; }
    public double OcrMilliseconds { get; private set; }
    public long CoinComparisonCount { get; private set; }
    public long StatusColorComparisonCount { get; private set; }
    public long StatusShapeComparisonCount { get; private set; }
    public long OverlayComparisonCount { get; private set; }
    public int OcrTaskCount { get; private set; }

    public void AddDecode(TimeSpan elapsed) => DecodeMilliseconds += elapsed.TotalMilliseconds;
    public void AddAnchor(TimeSpan elapsed) => AnchorMilliseconds += elapsed.TotalMilliseconds;
    public void AddCoinMatching(TimeSpan elapsed) => CoinMatchingMilliseconds += elapsed.TotalMilliseconds;
    public void AddStatusMatching(TimeSpan elapsed) => StatusMatchingMilliseconds += elapsed.TotalMilliseconds;
    public void AddOcr(TimeSpan elapsed) => OcrMilliseconds += elapsed.TotalMilliseconds;
    public void IncrementCoinComparisons() => CoinComparisonCount++;
    public void IncrementStatusColorComparisons() => StatusColorComparisonCount++;
    public void IncrementStatusShapeComparisons() => StatusShapeComparisonCount++;
    public void IncrementOverlayComparisons() => OverlayComparisonCount++;
    public void IncrementOcrTasks() => OcrTaskCount++;

    public RhodesCoinStabilityTiming Snapshot() =>
        new(
            DecodeMilliseconds,
            AnchorMilliseconds,
            CoinMatchingMilliseconds,
            StatusMatchingMilliseconds,
            OcrMilliseconds,
            CoinComparisonCount,
            StatusColorComparisonCount,
            StatusShapeComparisonCount,
            OverlayComparisonCount,
            OcrTaskCount);
}

public sealed record RhodesCoinStabilityConfusionCell(
    string Expected,
    string Actual,
    int Count);

public sealed record RhodesCoinStabilityConfusionMatrix(
    int Denominator,
    IReadOnlyList<string> Labels,
    IReadOnlyList<RhodesCoinStabilityConfusionCell> Cells);

public sealed record RhodesCoinStabilityThresholds(
    double Score,
    double Margin,
    double Overlay,
    double Presence);

public sealed record RhodesCoinStabilitySweepPoint(
    RhodesCoinStabilityThresholds Thresholds,
    int Denominator,
    int Correct,
    int FalsePositive,
    int FalseNegative,
    int WrongClass,
    int Missing,
    bool MeetsFalsePositiveConstraint,
    double Accuracy);

public sealed record RhodesCoinStabilityRunOptions(
    bool RunSweep = false);

public sealed record RhodesCoinStabilitySummary(
    int FrameCount,
    int ObservationCount,
    int LabeledSlotCount,
    int CorrectCoinCount,
    int StatusDenominator,
    int CorrectStatusCount,
    int ErrorCount,
    IReadOnlyDictionary<string, int> ErrorsByClass,
    int CandidateSplitCount,
    int DuplicateCountErrorCount,
    double CoinAccuracy,
    double StatusAccuracy,
    string ResultHash,
    RhodesCoinStabilityTiming? Timing = null);

public sealed record RhodesCoinStabilityRunMetadata(
    int SchemaVersion,
    string Commit,
    string MaaVersion,
    string TemplateSha256,
    string MasterSha256,
    string ManifestSha256,
    string ResultHash,
    int FrameCount,
    long ElapsedMilliseconds,
    IReadOnlyList<string> FrameRoots);

public sealed record RhodesCoinStabilityRunResult(
    IReadOnlyList<RhodesCoinStabilitySlotObservation> Observations,
    IReadOnlyList<RhodesCoinStabilityError> Errors,
    IReadOnlyList<RhodesCoinStabilityCandidateDiff> CandidateDiffs,
    RhodesCoinStabilitySummary Summary,
    RhodesCoinStabilityRunMetadata Metadata,
    RhodesCoinStabilityConfusionMatrix? StatusConfusionMatrix = null,
    IReadOnlyList<RhodesCoinStabilitySweepPoint>? ThresholdSweep = null);

public static class RhodesCoinStabilityEvaluator
{
    private static readonly double[] DefaultScoreThresholds = [0.70, 0.72, 0.74, 0.76];
    private static readonly double[] DefaultMarginThresholds = [0, 0.01, 0.02, 0.03];
    private static readonly double[] DefaultOverlayThresholds = [0.025, 0.035, 0.045, 0.055];
    private static readonly double[] DefaultPresenceThresholds = [0.48, 0.60, 0.70];

    public static IReadOnlyList<RhodesCoinStabilityError> Compare(
        RhodesCoinStabilityFrameExpectation expected,
        IEnumerable<RhodesCoinStabilitySlotObservation> observations)
    {
        var actualBySlot = observations
            .Where(item => item.FrameId.Equals(expected.FrameId, StringComparison.Ordinal)
                && item.ProfileId.Equals(expected.ProfileId, StringComparison.Ordinal)
                && item.PassIndex == expected.PassIndex)
            .GroupBy(item => item.SlotIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                    .First());
        var expectedPresentSlots = expected.Slots
            .Where(slot => slot.Present)
            .ToDictionary(slot => slot.SlotIndex);
        var expectedCoinSlots = expectedPresentSlots.Values
            .Where(slot => !string.IsNullOrWhiteSpace(slot.CoinId))
            .GroupBy(slot => slot.CoinId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(slot => slot.SlotIndex).ToHashSet(),
                StringComparer.Ordinal);
        var errors = new List<RhodesCoinStabilityError>();

        foreach (var slot in expected.Slots.OrderBy(item => item.SlotIndex))
        {
            actualBySlot.TryGetValue(slot.SlotIndex, out var actual);
            var actualPresent = actual is { Present: true };
            if (slot.Present && !actualPresent)
            {
                errors.Add(Error(expected, slot, actual, "slot_missed", "expected coin slot was not detected"));
                continue;
            }

            if (!slot.Present && actualPresent)
            {
                errors.Add(Error(expected, slot, actual, "association_error", "empty slot received a coin"));
                continue;
            }

            if (!slot.Present || actual is null)
                continue;

            if (!slot.CoinId.Equals(actual.CoinId, StringComparison.Ordinal))
            {
                var errorClass = expectedCoinSlots.TryGetValue(actual.CoinId, out var expectedSlots)
                    && !expectedSlots.Contains(slot.SlotIndex)
                        ? "association_error"
                        : "coin_name_error";
                errors.Add(Error(expected, slot, actual, errorClass, "coin identity differs"));
                continue;
            }

            if (slot.Status.Kind.Equals("unknown", StringComparison.Ordinal))
                continue;

            if (slot.Status.Kind.Equals("none", StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(actual.StatusId))
                    errors.Add(Error(expected, slot, actual, "status_false_positive", "status was detected on a plain coin"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(actual.StatusId))
            {
                errors.Add(Error(expected, slot, actual, "status_false_negative", "known status was missed"));
            }
            else if (!slot.Status.StatusId.Equals(actual.StatusId, StringComparison.Ordinal))
            {
                errors.Add(Error(expected, slot, actual, "status_wrong_class", "detected status class differs"));
            }
        }

        if (expected.Slots.Count > 0)
        {
            var expectedCounts = expected.Slots
                .Where(slot => slot.Present)
                .GroupBy(slot => $"{slot.CoinId}\u001f{ExpectedStatusKey(slot.Status)}", StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var labeledIndices = expected.Slots.Select(slot => slot.SlotIndex).ToHashSet();
            var actualCounts = actualBySlot.Values
                .Where(slot => slot.Present && labeledIndices.Contains(slot.SlotIndex))
                .GroupBy(slot => $"{slot.CoinId}\u001f{slot.StatusId}", StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            if (!MultisetEqual(expectedCounts, actualCounts, expected.Slots))
            {
                errors.Add(new RhodesCoinStabilityError(
                    expected.FrameId,
                    expected.ProfileId,
                    expected.PassIndex,
                    -1,
                    "duplicate_count_error",
                    StringMultiset(expectedCounts),
                    "",
                    StringMultiset(actualCounts),
                    "",
                    "labeled slot multiset differs"));
            }
        }

        return errors
            .OrderBy(error => error.FrameId, StringComparer.Ordinal)
            .ThenBy(error => error.PassIndex)
            .ThenBy(error => error.SlotIndex)
            .ThenBy(error => error.ErrorClass, StringComparer.Ordinal)
            .ToArray();
    }

    public static RhodesCoinStabilityConfusionMatrix BuildStatusConfusionMatrix(
        RhodesCoinStabilityManifest manifest,
        IEnumerable<RhodesCoinStabilitySlotObservation> observations)
    {
        var actualBySlot = BuildObservationIndex(observations);
        var counts = new Dictionary<(string Expected, string Actual), int>();
        var labels = new HashSet<string>(StringComparer.Ordinal)
        {
            "none",
            "missing",
        };
        var denominator = 0;
        foreach (var frame in manifest.Frames)
        {
            foreach (var slot in frame.Slots)
            {
                if (!slot.Present || slot.Status.Kind.Equals("unknown", StringComparison.Ordinal))
                    continue;

                denominator++;
                var expected = slot.Status.Kind.Equals("known", StringComparison.Ordinal)
                    ? slot.Status.StatusId
                    : "none";
                var actual = ResolveActualStatus(frame, slot, actualBySlot);
                labels.Add(expected);
                labels.Add(actual);
                counts[(expected, actual)] = counts.GetValueOrDefault((expected, actual)) + 1;
            }
        }

        return new RhodesCoinStabilityConfusionMatrix(
            denominator,
            labels.OrderBy(label => label, StringComparer.Ordinal).ToArray(),
            counts
                .OrderBy(pair => pair.Key.Expected, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Actual, StringComparer.Ordinal)
                .Select(pair => new RhodesCoinStabilityConfusionCell(
                    pair.Key.Expected,
                    pair.Key.Actual,
                    pair.Value))
                .ToArray());
    }

    public static IReadOnlyList<RhodesCoinStabilitySweepPoint> RunThresholdSweep(
        RhodesCoinStabilityManifest manifest,
        IEnumerable<RhodesCoinStabilitySlotObservation> observations,
        IReadOnlyList<double>? scoreThresholds = null,
        IReadOnlyList<double>? marginThresholds = null,
        IReadOnlyList<double>? overlayThresholds = null,
        IReadOnlyList<double>? presenceThresholds = null)
    {
        var actualBySlot = BuildObservationIndex(observations);
        var result = new List<RhodesCoinStabilitySweepPoint>();
        foreach (var score in scoreThresholds ?? DefaultScoreThresholds)
        {
            foreach (var margin in marginThresholds ?? DefaultMarginThresholds)
            {
                foreach (var overlay in overlayThresholds ?? DefaultOverlayThresholds)
                {
                    foreach (var presence in presenceThresholds ?? DefaultPresenceThresholds)
                    {
                        var thresholds = new RhodesCoinStabilityThresholds(score, margin, overlay, presence);
                        result.Add(EvaluateThresholds(manifest, actualBySlot, thresholds));
                    }
                }
            }
        }

        return result
            .OrderByDescending(point => point.MeetsFalsePositiveConstraint)
            .ThenByDescending(point => point.Accuracy)
            .ThenBy(point => point.Thresholds.Score)
            .ThenBy(point => point.Thresholds.Margin)
            .ThenBy(point => point.Thresholds.Overlay)
            .ThenBy(point => point.Thresholds.Presence)
            .ToArray();
    }

    public static string ComputeResultHash(
        IEnumerable<RhodesCoinStabilitySlotObservation> observations,
        IEnumerable<RhodesCoinStabilityError> errors)
    {
        var canonical = new
        {
            observations = observations
                .OrderBy(item => item.FrameId, StringComparer.Ordinal)
                .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
                .ThenBy(item => item.PassIndex)
                .ThenBy(item => item.SlotIndex)
                .Select(item => new
                {
                    item.FrameId,
                    item.ProfileId,
                    item.PassIndex,
                    item.SlotIndex,
                    item.Present,
                    item.CoinId,
                    item.StatusId,
                    item.Score,
                    item.RunnerUpScore,
                    item.VisualStrength,
                    item.StatusScore,
                    item.PredictedStatusId,
                    item.Roi,
                    item.ImageSha256,
                    item.EvidenceSource,
                    item.SavedCoinId,
                    item.SavedStatusId,
                    item.CoinVisualStrength,
                }),
            errors = errors
                .OrderBy(item => item.FrameId, StringComparer.Ordinal)
                .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
                .ThenBy(item => item.PassIndex)
                .ThenBy(item => item.SlotIndex)
                .ThenBy(item => item.ErrorClass, StringComparer.Ordinal),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, RhodesCoinStabilityJson.Options);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static RhodesCoinStabilitySweepPoint EvaluateThresholds(
        RhodesCoinStabilityManifest manifest,
        IReadOnlyDictionary<string, RhodesCoinStabilitySlotObservation> actualBySlot,
        RhodesCoinStabilityThresholds thresholds)
    {
        var denominator = 0;
        var correct = 0;
        var falsePositive = 0;
        var falseNegative = 0;
        var wrongClass = 0;
        var missing = 0;
        foreach (var frame in manifest.Frames)
        {
            foreach (var slot in frame.Slots)
            {
                if (!slot.Present || slot.Status.Kind.Equals("unknown", StringComparison.Ordinal))
                    continue;

                denominator++;
                var expected = slot.Status.Kind.Equals("known", StringComparison.Ordinal)
                    ? slot.Status.StatusId
                    : "none";
                var actual = ResolveSweptStatus(frame, slot, actualBySlot, thresholds);
                if (actual.Equals(expected, StringComparison.Ordinal))
                {
                    correct++;
                }
                else if (actual.Equals("missing", StringComparison.Ordinal))
                {
                    missing++;
                    if (!expected.Equals("none", StringComparison.Ordinal))
                        falseNegative++;
                }
                else if (expected.Equals("none", StringComparison.Ordinal))
                {
                    falsePositive++;
                }
                else if (actual.Equals("none", StringComparison.Ordinal))
                {
                    falseNegative++;
                }
                else
                {
                    wrongClass++;
                }
            }
        }

        return new RhodesCoinStabilitySweepPoint(
            thresholds,
            denominator,
            correct,
            falsePositive,
            falseNegative,
            wrongClass,
            missing,
            falsePositive == 0,
            denominator == 0 ? 0 : (double)correct / denominator);
    }

    private static string ResolveActualStatus(
        RhodesCoinStabilityFrameExpectation frame,
        RhodesCoinStabilitySlotExpectation slot,
        IReadOnlyDictionary<string, RhodesCoinStabilitySlotObservation> actualBySlot)
    {
        if (!actualBySlot.TryGetValue(ObservationKey(frame, slot.SlotIndex), out var actual)
            || !actual.Present
            || !actual.CoinId.Equals(slot.CoinId, StringComparison.Ordinal))
        {
            return "missing";
        }

        return string.IsNullOrWhiteSpace(actual.StatusId) ? "none" : actual.StatusId;
    }

    private static string ResolveSweptStatus(
        RhodesCoinStabilityFrameExpectation frame,
        RhodesCoinStabilitySlotExpectation slot,
        IReadOnlyDictionary<string, RhodesCoinStabilitySlotObservation> actualBySlot,
        RhodesCoinStabilityThresholds thresholds)
    {
        if (!actualBySlot.TryGetValue(ObservationKey(frame, slot.SlotIndex), out var actual)
            || !actual.Present
            || !actual.CoinId.Equals(slot.CoinId, StringComparison.Ordinal)
            || actual.CoinVisualStrength < thresholds.Presence)
        {
            return "missing";
        }

        if (string.IsNullOrWhiteSpace(actual.PredictedStatusId)
            || actual.StatusScore < thresholds.Score
            || actual.StatusScore - actual.RunnerUpScore < thresholds.Margin
            || actual.VisualStrength < thresholds.Overlay)
        {
            return "none";
        }
        return actual.PredictedStatusId;
    }

    private static IReadOnlyDictionary<string, RhodesCoinStabilitySlotObservation> BuildObservationIndex(
        IEnumerable<RhodesCoinStabilitySlotObservation> observations) =>
        observations
            .GroupBy(
                item => ObservationKey(item.FrameId, item.ProfileId, item.PassIndex, item.SlotIndex),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                    .First(),
                StringComparer.Ordinal);

    private static string ObservationKey(
        RhodesCoinStabilityFrameExpectation frame,
        int slotIndex) =>
        ObservationKey(frame.FrameId, frame.ProfileId, frame.PassIndex, slotIndex);

    private static string ObservationKey(
        string frameId,
        string profileId,
        int passIndex,
        int slotIndex) =>
        $"{frameId}\u001f{profileId}\u001f{passIndex}\u001f{slotIndex}";

    private static RhodesCoinStabilityError Error(
        RhodesCoinStabilityFrameExpectation frame,
        RhodesCoinStabilitySlotExpectation expected,
        RhodesCoinStabilitySlotObservation? actual,
        string errorClass,
        string detail) =>
        new(
            frame.FrameId,
            frame.ProfileId,
            frame.PassIndex,
            expected.SlotIndex,
            errorClass,
            expected.CoinId,
            expected.Status.StatusId,
            actual?.CoinId ?? "",
            actual?.StatusId ?? "",
            detail);

    private static string ExpectedStatusKey(RhodesCoinStabilityStatusExpectation status) =>
        status.Kind.Equals("known", StringComparison.Ordinal)
            ? status.StatusId
            : status.Kind.Equals("none", StringComparison.Ordinal)
                ? ""
                : "*";

    private static bool MultisetEqual(
        IReadOnlyDictionary<string, int> expected,
        IReadOnlyDictionary<string, int> actual,
        IReadOnlyList<RhodesCoinStabilitySlotExpectation> slots)
    {
        if (slots.Any(slot => slot.Present && slot.Status.Kind.Equals("unknown", StringComparison.Ordinal)))
        {
            var expectedCoinCounts = slots
                .Where(slot => slot.Present)
                .GroupBy(slot => slot.CoinId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var actualCoinCounts = actual
                .GroupBy(pair => pair.Key.Split('\u001f')[0], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value), StringComparer.Ordinal);
            return expectedCoinCounts.Count == actualCoinCounts.Count
                && expectedCoinCounts.All(pair =>
                    actualCoinCounts.TryGetValue(pair.Key, out var count) && count == pair.Value);
        }

        return expected.Count == actual.Count
            && expected.All(pair => actual.TryGetValue(pair.Key, out var count) && count == pair.Value);
    }

    private static string StringMultiset(IReadOnlyDictionary<string, int> multiset) =>
        string.Join(
            "|",
            multiset
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key.Replace('\u001f', ':')}x{pair.Value}"));
}

internal static class RhodesCoinStabilityJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    };
}
