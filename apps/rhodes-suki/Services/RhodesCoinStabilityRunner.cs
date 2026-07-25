using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesCoinStabilityRunner
{
    private const string ActiveProfileId = "is6ActiveCoinsFull";
    private const string OwnedProfileId = "is6CoinsFull";

    public static RhodesCoinStabilityRunResult Run(
        RhodesCoinStabilityManifest manifest,
        IReadOnlyList<RhodesCoinStabilityFrameInput> frames,
        string repositoryRoot,
        IReadOnlyList<string> frameRoots,
        RhodesCoinStabilityRunOptions? options = null)
    {
        manifest.Validate();
        options ??= new RhodesCoinStabilityRunOptions();
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new RhodesCoinRecognitionDiagnostics();
        var coinOptions = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin");
        var statusOptions = RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus");
        var observations = frames
            .OrderBy(frame => frame.FrameId, StringComparer.Ordinal)
            .ThenBy(frame => frame.ProfileId, StringComparer.Ordinal)
            .SelectMany(frame => Observe(frame, coinOptions, statusOptions, diagnostics))
            .ToArray();

        var errors = new List<RhodesCoinStabilityError>();
        var candidateDiffs = new List<RhodesCoinStabilityCandidateDiff>();
        foreach (var expected in manifest.Frames
                     .OrderBy(frame => frame.FrameId, StringComparer.Ordinal)
                     .ThenBy(frame => frame.ProfileId, StringComparer.Ordinal)
                     .ThenBy(frame => frame.PassIndex))
        {
            var frameObservations = observations
                .Where(item => item.FrameId.Equals(expected.FrameId, StringComparison.Ordinal)
                    && item.ProfileId.Equals(expected.ProfileId, StringComparison.Ordinal)
                    && item.PassIndex == expected.PassIndex)
                .ToArray();
            if (expected.Slots.Any(slot => slot.Present)
                && !frameObservations.Any(item => item.Present))
            {
                errors.Add(new RhodesCoinStabilityError(
                    expected.FrameId,
                    expected.ProfileId,
                    expected.PassIndex,
                    -1,
                    "panel_phase_error",
                    "coin panel",
                    "",
                    "no detected slots",
                    "",
                    "expected a populated coin panel but replay found no coin"));
            }

            errors.AddRange(RhodesCoinStabilityEvaluator.Compare(expected, frameObservations));
        }

        foreach (var frame in frames
                     .OrderBy(item => item.FrameId, StringComparer.Ordinal)
                     .ThenBy(item => item.ProfileId, StringComparer.Ordinal))
        {
            var expected = manifest.Frames.FirstOrDefault(item =>
                item.FrameId.Equals(frame.FrameId, StringComparison.Ordinal)
                && item.ProfileId.Equals(frame.ProfileId, StringComparison.Ordinal)
                && item.PassIndex == frame.PassIndex);
            var observed = observations
                .Where(item => item.FrameId.Equals(frame.FrameId, StringComparison.Ordinal)
                    && item.ProfileId.Equals(frame.ProfileId, StringComparison.Ordinal)
                    && item.PassIndex == frame.PassIndex
                    && item.Present)
                .ToArray();
            var diff = BuildCandidateDiff(frame, expected, observed);
            candidateDiffs.Add(diff);
            errors.AddRange(BuildCandidateSplitErrors(frame, observed));
        }

        var orderedErrors = errors
            .OrderBy(error => error.FrameId, StringComparer.Ordinal)
            .ThenBy(error => error.ProfileId, StringComparer.Ordinal)
            .ThenBy(error => error.PassIndex)
            .ThenBy(error => error.SlotIndex)
            .ThenBy(error => error.ErrorClass, StringComparer.Ordinal)
            .ToArray();
        var resultHash = RhodesCoinStabilityEvaluator.ComputeResultHash(observations, orderedErrors);
        var timing = diagnostics.Snapshot();
        var summary = BuildSummary(
            manifest,
            observations,
            orderedErrors,
            candidateDiffs,
            resultHash,
            timing);
        var statusConfusionMatrix =
            RhodesCoinStabilityEvaluator.BuildStatusConfusionMatrix(manifest, observations);
        var thresholdSweep = options.RunSweep
            ? RhodesCoinStabilityEvaluator.RunThresholdSweep(manifest, observations)
            : [];
        stopwatch.Stop();

        var normalizedRoot = Path.GetFullPath(repositoryRoot);
        var metadata = new RhodesCoinStabilityRunMetadata(
            1,
            ReadCommit(normalizedRoot),
            typeof(MaaToolkit).Assembly.GetName().Version?.ToString() ?? "unknown",
            HashTemplateSet(coinOptions.Concat(statusOptions)),
            HashFile(Path.Combine(normalizedRoot, "data", "selectable-effects.json")),
            HashBytes(Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(manifest, RhodesCoinStabilityJson.Options))),
            resultHash,
            frames.Count,
            stopwatch.ElapsedMilliseconds,
            frameRoots
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        return new RhodesCoinStabilityRunResult(
            observations,
            orderedErrors,
            candidateDiffs,
            summary,
            metadata,
            statusConfusionMatrix,
            thresholdSweep);
    }

    private static IReadOnlyList<RhodesCoinStabilitySlotObservation> Observe(
        RhodesCoinStabilityFrameInput frame,
        IReadOnlyList<SukiSpecialEffectOption> coinOptions,
        IReadOnlyList<SukiSpecialEffectOption> statusOptions,
        RhodesCoinRecognitionDiagnostics diagnostics)
    {
        var imageSha = HashBytes(frame.EncodedImage);
        var inspections = frame.ProfileId.Equals(ActiveProfileId, StringComparison.Ordinal)
            ? RhodesSuiCoinImageRecognizer.InspectActive(frame.EncodedImage, coinOptions, diagnostics)
            : RhodesSuiCoinImageRecognizer.InspectOwned(frame.EncodedImage, coinOptions, diagnostics);
        IReadOnlyList<RhodesSuiCoinImageDetection> detections;
        if (frame.ProfileId.Equals(ActiveProfileId, StringComparison.Ordinal))
        {
            detections = RhodesSuiCoinImageRecognizer.Detect(
                frame.EncodedImage,
                coinOptions,
                diagnostics);
        }
        else if (frame.ProfileId.Equals(OwnedProfileId, StringComparison.Ordinal))
        {
            var statusResult = RhodesSuiCoinStatusRecognizer.RecognizeOwned(
                frame.EncodedImage,
                frame.EvidenceTasks,
                coinOptions,
                statusOptions,
                inspections,
                diagnostics);
            detections = RhodesSuiCoinImageRecognizer.TryRead(statusResult, out _, out var parsed)
                ? parsed
                : [];
        }
        else
        {
            detections = [];
        }

        var detectedBySlot = detections
            .Where(item => item.SlotIndex >= 0)
            .GroupBy(item => item.SlotIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                    .First());
        var inspectedBySlot = inspections
            .Where(item => item.SlotIndex >= 0)
            .GroupBy(item => item.SlotIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                    .First());
        var statusProbeBySlot = frame.ProfileId.Equals(OwnedProfileId, StringComparison.Ordinal)
            ? RhodesSuiCoinStatusRecognizer.InspectOwnedStatusSlots(
                    frame.EncodedImage,
                    inspections,
                    statusOptions,
                    diagnostics)
                .ToDictionary(probe => probe.SlotIndex)
            : [];
        var savedBySlot = frame.SavedDetections
            .Where(item => item.SlotIndex >= 0)
            .GroupBy(item => item.SlotIndex)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Score)
                    .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                    .First());
        var maximumSlot = frame.ProfileId.Equals(ActiveProfileId, StringComparison.Ordinal) ? 2 : 8;
        var evidenceSource = frame.EvidenceTasks.Count > 0
            ? "image+recognition-scan"
            : "image-only";
        var result = new List<RhodesCoinStabilitySlotObservation>(maximumSlot + 1);
        for (var slotIndex = 0; slotIndex <= maximumSlot; slotIndex++)
        {
            detectedBySlot.TryGetValue(slotIndex, out var detected);
            inspectedBySlot.TryGetValue(slotIndex, out var inspected);
            statusProbeBySlot.TryGetValue(slotIndex, out var statusProbe);
            savedBySlot.TryGetValue(slotIndex, out var saved);
            var metrics = detected ?? inspected;
            var detectedStatus = detected is not null && detected.StatusScore > 0
                ? detected
                : null;
            result.Add(new RhodesCoinStabilitySlotObservation(
                frame.FrameId,
                frame.ProfileId,
                frame.PassIndex,
                slotIndex,
                detected is not null,
                detected?.CoinId ?? "",
                detected?.StatusId ?? "",
                metrics?.Score ?? 0,
                detectedStatus?.RunnerUpScore ?? statusProbe?.RunnerUpScore ?? detected?.RunnerUpScore ?? inspected?.RunnerUpScore ?? 0,
                detected?.VisualStrength ?? 0,
                detectedStatus?.StatusScore ?? statusProbe?.Score ?? 0,
                detectedStatus?.PredictedStatusId ?? statusProbe?.StatusId ?? "",
                (detectedStatus?.Roi ?? statusProbe?.Roi ?? detected?.Roi ?? inspected?.Roi)?.ToArray() ?? [],
                imageSha,
                frame.Source,
                evidenceSource,
                saved?.CoinId ?? "",
                saved?.StatusId ?? "",
                inspected?.VisualStrength ?? 0));
        }
        return result;
    }

    private static RhodesCoinStabilityCandidateDiff BuildCandidateDiff(
        RhodesCoinStabilityFrameInput frame,
        RhodesCoinStabilityFrameExpectation? expected,
        IReadOnlyList<RhodesCoinStabilitySlotObservation> observed)
    {
        var expectedValues = expected?.Slots
            .Where(slot => slot.Present)
            .OrderBy(slot => slot.SlotIndex)
            .Select(slot => CandidateValue(
                slot.SlotIndex,
                slot.CoinId,
                slot.Status.Kind.Equals("known", StringComparison.Ordinal)
                    ? slot.Status.StatusId
                    : slot.Status.Kind.Equals("unknown", StringComparison.Ordinal)
                        ? "*"
                        : ""))
            .ToArray() ?? [];
        var observedValues = observed
            .OrderBy(slot => slot.SlotIndex)
            .Select(slot => CandidateValue(slot.SlotIndex, slot.CoinId, slot.StatusId))
            .ToArray();
        var savedValues = frame.SavedDetections
            .OrderBy(slot => slot.SlotIndex)
            .ThenBy(slot => slot.CoinId, StringComparer.Ordinal)
            .Select(slot => CandidateValue(slot.SlotIndex, slot.CoinId, slot.StatusId))
            .ToArray();
        var missing = expected is null
            ? []
            : MultisetDifference(expectedValues, observedValues, wildcardStatus: true);
        var unexpected = expected is null
            ? []
            : MultisetDifference(observedValues, expectedValues, wildcardStatus: true);
        return new RhodesCoinStabilityCandidateDiff(
            frame.FrameId,
            frame.ProfileId,
            frame.PassIndex,
            expectedValues,
            observedValues,
            savedValues,
            missing,
            unexpected,
            CountCandidateSplits(frame.SavedDetections, observed));
    }

    private static IReadOnlyList<RhodesCoinStabilityError> BuildCandidateSplitErrors(
        RhodesCoinStabilityFrameInput frame,
        IReadOnlyList<RhodesCoinStabilitySlotObservation> observed)
    {
        if (frame.SavedDetections.Count == 0)
            return [];

        var observedBySlot = observed.ToDictionary(item => item.SlotIndex);
        return frame.SavedDetections
            .GroupBy(item => item.SlotIndex)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                .First())
            .Where(saved =>
                !observedBySlot.TryGetValue(saved.SlotIndex, out var current)
                || !saved.CoinId.Equals(current.CoinId, StringComparison.Ordinal)
                || !saved.StatusId.Equals(current.StatusId, StringComparison.Ordinal))
            .Select(saved =>
            {
                observedBySlot.TryGetValue(saved.SlotIndex, out var current);
                return new RhodesCoinStabilityError(
                    frame.FrameId,
                    frame.ProfileId,
                    frame.PassIndex,
                    saved.SlotIndex,
                    "candidate_split",
                    saved.CoinId,
                    saved.StatusId,
                    current?.CoinId ?? "",
                    current?.StatusId ?? "",
                    "saved candidate and deterministic replay differ");
            })
            .ToArray();
    }

    private static RhodesCoinStabilitySummary BuildSummary(
        RhodesCoinStabilityManifest manifest,
        IReadOnlyList<RhodesCoinStabilitySlotObservation> observations,
        IReadOnlyList<RhodesCoinStabilityError> errors,
        IReadOnlyList<RhodesCoinStabilityCandidateDiff> candidateDiffs,
        string resultHash,
        RhodesCoinStabilityTiming timing)
    {
        var observationsByFrame = observations
            .GroupBy(item => $"{item.FrameId}\u001f{item.ProfileId}\u001f{item.PassIndex}", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(item => item.SlotIndex),
                StringComparer.Ordinal);
        var labeledSlotCount = 0;
        var correctCoinCount = 0;
        var statusDenominator = 0;
        var correctStatusCount = 0;
        foreach (var frame in manifest.Frames)
        {
            observationsByFrame.TryGetValue(
                $"{frame.FrameId}\u001f{frame.ProfileId}\u001f{frame.PassIndex}",
                out var bySlot);
            foreach (var slot in frame.Slots)
            {
                labeledSlotCount++;
                RhodesCoinStabilitySlotObservation? actual = null;
                if (bySlot is not null)
                    bySlot.TryGetValue(slot.SlotIndex, out actual);
                var coinCorrect = slot.Present
                    ? actual is { Present: true }
                        && slot.CoinId.Equals(actual.CoinId, StringComparison.Ordinal)
                    : actual is null || !actual.Present;
                if (coinCorrect)
                    correctCoinCount++;

                if (!slot.Present || slot.Status.Kind.Equals("unknown", StringComparison.Ordinal))
                    continue;
                statusDenominator++;
                var statusCandidateMatchesCoin = actual is { Present: true }
                    && slot.CoinId.Equals(actual.CoinId, StringComparison.Ordinal);
                var statusCorrect = slot.Status.Kind.Equals("none", StringComparison.Ordinal)
                    ? statusCandidateMatchesCoin && string.IsNullOrWhiteSpace(actual!.StatusId)
                    : statusCandidateMatchesCoin
                        && slot.Status.StatusId.Equals(actual!.StatusId, StringComparison.Ordinal);
                if (statusCorrect)
                    correctStatusCount++;
            }
        }

        var byClass = errors
            .GroupBy(error => error.ErrorClass, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new RhodesCoinStabilitySummary(
            observations
                .Select(item => $"{item.FrameId}\u001f{item.ProfileId}\u001f{item.PassIndex}")
                .Distinct(StringComparer.Ordinal)
                .Count(),
            observations.Count,
            labeledSlotCount,
            correctCoinCount,
            statusDenominator,
            correctStatusCount,
            errors.Count,
            byClass,
            candidateDiffs.Sum(diff => diff.CandidateSplitCount),
            byClass.GetValueOrDefault("duplicate_count_error"),
            labeledSlotCount == 0 ? 0 : (double)correctCoinCount / labeledSlotCount,
            statusDenominator == 0 ? 0 : (double)correctStatusCount / statusDenominator,
            resultHash,
            timing);
    }

    private static int CountCandidateSplits(
        IReadOnlyList<RhodesSuiCoinImageDetection> saved,
        IReadOnlyList<RhodesCoinStabilitySlotObservation> observed)
    {
        var observedBySlot = observed.ToDictionary(item => item.SlotIndex);
        return saved
            .GroupBy(item => item.SlotIndex)
            .Select(group => group
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.CoinId, StringComparer.Ordinal)
                .First())
            .Count(item =>
                !observedBySlot.TryGetValue(item.SlotIndex, out var current)
                || !item.CoinId.Equals(current.CoinId, StringComparison.Ordinal)
                || !item.StatusId.Equals(current.StatusId, StringComparison.Ordinal));
    }

    private static string CandidateValue(int slotIndex, string coinId, string statusId) =>
        $"{slotIndex}:{coinId}:{statusId}";

    private static IReadOnlyList<string> MultisetDifference(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        bool wildcardStatus)
    {
        var remaining = right.ToList();
        var result = new List<string>();
        foreach (var value in left)
        {
            var index = remaining.FindIndex(candidate =>
                CandidateEquals(value, candidate, wildcardStatus));
            if (index < 0)
                result.Add(value);
            else
                remaining.RemoveAt(index);
        }
        return result;
    }

    private static bool CandidateEquals(string left, string right, bool wildcardStatus)
    {
        if (left.Equals(right, StringComparison.Ordinal))
            return true;
        if (!wildcardStatus)
            return false;
        var leftParts = left.Split(':', 3);
        var rightParts = right.Split(':', 3);
        return leftParts.Length == 3
            && rightParts.Length == 3
            && leftParts[0].Equals(rightParts[0], StringComparison.Ordinal)
            && leftParts[1].Equals(rightParts[1], StringComparison.Ordinal)
            && (leftParts[2].Equals("*", StringComparison.Ordinal)
                || rightParts[2].Equals("*", StringComparison.Ordinal));
    }

    private static string ReadCommit(string repositoryRoot)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse HEAD",
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
                return "unknown";
            var value = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 && value.Length > 0 ? value : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string HashTemplateSet(IEnumerable<SukiSpecialEffectOption> options)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var option in options
                     .OrderBy(item => item.Id, StringComparer.Ordinal)
                     .ThenBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase))
        {
            var identity = Encoding.UTF8.GetBytes($"{option.Id}\0");
            hash.AppendData(identity);
            if (File.Exists(option.ImagePath))
                hash.AppendData(File.ReadAllBytes(option.ImagePath));
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string HashFile(string path) =>
        File.Exists(path) ? HashBytes(File.ReadAllBytes(path)) : "missing";

    private static string HashBytes(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
