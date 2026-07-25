using System.Diagnostics;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesSuiCoinStatusProbe(
    int SlotIndex,
    string StatusId,
    double Score,
    double RunnerUpScore,
    MaaRoi Roi,
    bool IsConfident = false,
    double OverlayDifference = 0,
    bool IsStatusPresent = false,
    double BodyDifference = 0,
    double EdgeDensity = 0,
    double ChromaDensity = 0,
    double ResidualEdgeScore = 0,
    double ResidualMomentScore = 0,
    double ResidualGlobalAspectScore = 0,
    double ResidualCoverageScore = 0,
    double ResidualAspectRatio = 0,
    string RunnerUpStatusId = "");

public static class RhodesSuiCoinStatusRecognizer
{
    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int FeatureSize = 64;
    private const int CoinListRoiX = 120;
    private const int CoinListRoiY = 96;
    private const double CoinListOcrScale = 2;
    private const double CoinCenterOffsetFromText = 62;
    private const int ExpectedStatusOffsetX = 12;
    private const int ExpectedStatusOffsetY = -42;
    private const int StatusSearchLeft = 24;
    private const int StatusSearchRight = 24;
    private const int StatusSearchUp = 56;
    private const int StatusSearchDown = 18;
    private const int FastStatusSearchHorizontalRadius = 6;
    private const int FastStatusSearchVerticalRadius = 8;
    private const double MinimumInspectionVisualStrength = 0.48;
    private const double MinimumScore = 0.74;
    private const double StrongMarginMinimumScore = 0.72;
    private const double StrongMargin = 0.02;
    private const double AmbiguousGild5MinimumScore = 0.74;
    private const double MinimumMargin = 0.01;
    private const double ResidualMinimumMargin = 0.008;
    private const double SpatialTieScoreWindow = 0.01;
    private const double MinimumSpatialTieDistanceGap = 0.1;
    private const double MinimumOverlayDifference = 0.035;
    private const double MinimumOverlayBodyDifference = 0.04;
    private const double MinimumStructuralEdgeDensity = 0.10;
    private const double LowChromaMinimumScore = 0.73;
    private const double LowChromaShapeRescueMinimumScore = 0.67;
    private const double LowChromaShapeRescueMinimumMargin = 0.02;
    private const double LowChromaShapeRescueMinimumMomentScore = 0.82;
    private const double VerticalStatusMaximumAspectRatio = 0.58;
    private const double VerticalStatusMinimumScore = 0.60;
    private const double VerticalStatusMinimumCoverageScore = 0.85;
    private const double VerticalStatusMinimumGlobalAspectScore = 0.64;
    private const double VerticalStatusMaximumLeaderGap = 0.10;
    private const double BroadImageRescueMinimumLead = 0.04;
    private const double ResidualBroadConfirmationMaximumMargin = 0.012;
    private const double ResidualBroadConfirmationMaximumScore = 0.77;
    private const double RaisedStatusImageMinimumScore = 0.69;
    private const double ClearAbsenceMaximumOverlayDifference = 0.03;
    private const double ClearAbsenceMaximumEdgeDensity = 0.04;
    private const double ClearAbsenceMaximumChromaDensity = 0.06;
    private const double ResidualEdgeThreshold = 0.08;
    private const double ResidualChromaThreshold = 0.10;
    private const int MaximumColorCandidatesPerStatus = 12;
    private const double ColorScoreWeight = 0.25;
    private const double ShapeScoreWeight = 0.75;
    private const byte ShapeAlphaFloor = 160;
    private static readonly int[] StatusTemplateWidths = [28, 32, 36, 40, 44, 48];
    private static readonly HashSet<int> FastStatusTemplateWidths = [28, 32, 36];
    private static readonly int[] CoinTemplateSizes = [100, 106, 112];
    private static readonly Lazy<IReadOnlyList<StatusTemplate>> DefaultStatusTemplates =
        new(() => BuildStatusTemplates(RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus")));
    private static readonly Lazy<IReadOnlyDictionary<string, CoinBaselineTemplate>> DefaultCoinTemplates =
        new(() => BuildCoinTemplates(RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin")));

    public static MaaTaskRunResult RecognizeOwned(
        byte[] encodedImage,
        IEnumerable<MaaTaskRunResult> frameTaskResults,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null,
        IReadOnlyList<SukiSpecialEffectOption>? statusOptions = null,
        IReadOnlyList<RhodesSuiCoinImageDetection>? imageInspections = null,
        RhodesCoinRecognitionDiagnostics? diagnostics = null)
    {
        if (encodedImage.Length == 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        var decodeWatch = Stopwatch.StartNew();
        using var decoded = SKBitmap.Decode(encodedImage);
        decodeWatch.Stop();
        diagnostics?.AddDecode(decodeWatch.Elapsed);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        using var normalized = NormalizeFrame(decoded);
        var templates = statusOptions is null
            ? DefaultStatusTemplates.Value
            : BuildStatusTemplates(statusOptions);
        var coinTemplates = coinOptions is null
            ? DefaultCoinTemplates.Value
            : BuildCoinTemplates(coinOptions);
        var matches = ResolveAnchoredMatches(frameTaskResults, imageInspections, diagnostics);
        if (matches.Length == 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        var detections = new List<RhodesSuiCoinImageDetection>(matches.Length);
        var matchingWatch = Stopwatch.StartNew();
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var centerX = match.CenterX;
            var centerY = match.CenterY;
            var presence = coinTemplates.TryGetValue(match.CoinId, out var coinTemplate)
                ? MeasureStatusPresence(normalized, coinTemplate, centerX, centerY, diagnostics)
                : null;
            var overlayDifference = presence?.OverlayDifference ?? 0;
            StatusMatch? status = null;
            var usedBroadSearch = false;
            if (!IsClearlyStatusAbsent(presence))
            {
                status = presence is not null
                    ? BestResidualStatusMatch(normalized, presence, templates, centerX, centerY, diagnostics)
                    : BestStatusMatchFast(normalized, templates, centerX, centerY, diagnostics);
                if ((
                        !IsAcceptedStatus(status, presence, allowLowChroma: false)
                        && ShouldUseBroadStatusSearch(presence)
                    )
                    || NeedsBroadStatusConfirmation(status, presence))
                {
                    status = presence is null
                        ? BestStatusMatchBroad(normalized, templates, centerX, centerY, diagnostics)
                        : BestCombinedBroadStatusMatch(
                            normalized,
                            presence,
                            templates,
                            centerX,
                            centerY,
                            status,
                            diagnostics);
                    usedBroadSearch = true;
                }
            }
            var statusAccepted = IsAcceptedStatus(status, presence, allowLowChroma: usedBroadSearch);

            var statusId = statusAccepted
                ? status!.Template.Option.Id
                : "";
            var evidenceRoi = statusId.Length > 0
                ? status!.Roi
                : CoinRoi(centerX, centerY);
            detections.Add(new RhodesSuiCoinImageDetection(
                match.CoinId,
                match.Label,
                statusId.Length > 0 ? Math.Min(match.Confidence, status!.Score) : match.Confidence,
                match.SlotIndex >= 0 ? match.SlotIndex : index,
                evidenceRoi,
                statusId,
                status?.RunnerUpScore ?? 0,
                overlayDifference,
                status?.Score ?? 0,
                status?.Template.Option.Id ?? ""));
        }
        matchingWatch.Stop();
        diagnostics?.AddStatusMatching(matchingWatch.Elapsed);

        return RhodesSuiCoinImageRecognizer.CreateOwnedResult(detections);
    }

    public static IReadOnlyList<RhodesSuiCoinStatusProbe> ProbeOwnedStatusSlots(
        byte[] encodedImage,
        IReadOnlyList<RhodesSuiCoinImageDetection> imageInspections,
        IReadOnlyList<SukiSpecialEffectOption>? statusOptions = null,
        RhodesCoinRecognitionDiagnostics? diagnostics = null) =>
        InspectOwnedStatusSlots(encodedImage, imageInspections, statusOptions, diagnostics)
            .Where(probe =>
                probe.IsStatusPresent
                || (
                    IsStatusScoreAccepted(probe.StatusId, probe.Score, probe.RunnerUpScore)
                    && probe.IsConfident
                ))
            .ToArray();

    public static IReadOnlyList<RhodesSuiCoinStatusProbe> InspectOwnedStatusSlots(
        byte[] encodedImage,
        IReadOnlyList<RhodesSuiCoinImageDetection> imageInspections,
        IReadOnlyList<SukiSpecialEffectOption>? statusOptions = null,
        RhodesCoinRecognitionDiagnostics? diagnostics = null)
    {
        if (encodedImage.Length == 0 || imageInspections.Count == 0)
            return [];

        var decodeWatch = Stopwatch.StartNew();
        using var decoded = SKBitmap.Decode(encodedImage);
        decodeWatch.Stop();
        diagnostics?.AddDecode(decodeWatch.Elapsed);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0)
            return [];

        using var normalized = NormalizeFrame(decoded);
        var templates = statusOptions is null
            ? DefaultStatusTemplates.Value
            : BuildStatusTemplates(statusOptions);
        var coinTemplates = DefaultCoinTemplates.Value;
        if (templates.Count == 0)
            return [];

        var matchingWatch = Stopwatch.StartNew();
        var probes = imageInspections
            .Where(inspection =>
                inspection.SlotIndex >= 0
                && inspection.VisualStrength >= MinimumInspectionVisualStrength)
            .GroupBy(inspection => inspection.SlotIndex)
            .Select(group => group.OrderByDescending(inspection => inspection.VisualStrength).First())
            .Select(inspection =>
            {
                if (!RhodesSuiCoinImageRecognizer.TryGetOwnedSlotCenter(
                        inspection.SlotIndex,
                        out var centerX,
                        out var centerY))
                {
                    return null;
                }

                var presence = coinTemplates.TryGetValue(inspection.CoinId, out var coinTemplate)
                    ? MeasureStatusPresence(normalized, coinTemplate, centerX, centerY, diagnostics)
                    : null;
                var overlayDifference = presence?.OverlayDifference ?? 0;
                var status = presence is not null
                    ? BestResidualStatusMatch(
                        normalized,
                        presence,
                        templates,
                        centerX,
                        centerY,
                        diagnostics)
                    : BestStatusMatchFast(
                        normalized,
                        templates,
                        centerX,
                        centerY,
                        diagnostics);
                var usedBroadSearch = false;
                if ((
                        !IsAcceptedStatus(status, presence, allowLowChroma: false)
                        && ShouldUseBroadStatusSearch(presence)
                    )
                    || NeedsBroadStatusConfirmation(status, presence))
                {
                    status = presence is null
                        ? BestStatusMatchBroad(
                            normalized,
                            templates,
                            centerX,
                            centerY,
                            diagnostics)
                        : BestCombinedBroadStatusMatch(
                            normalized,
                            presence,
                            templates,
                            centerX,
                            centerY,
                            status,
                            diagnostics);
                    usedBroadSearch = true;
                }
                if (status is null)
                    return null;

                return new RhodesSuiCoinStatusProbe(
                    inspection.SlotIndex,
                    status.Template.Option.Id,
                    status.Score,
                    status.RunnerUpScore,
                    status.Roi,
                    status.IsConfident,
                    overlayDifference,
                    presence is not null
                    && IsResidualStatusPresent(status, presence, allowLowChroma: usedBroadSearch),
                    presence?.BodyDifference ?? 0,
                    presence?.EdgeDensity ?? 0,
                    presence?.ChromaDensity ?? 0,
                    status.ResidualEdgeScore,
                    status.ResidualMomentScore,
                    status.ResidualGlobalAspectScore,
                    status.ResidualCoverageScore,
                    status.ResidualAspectRatio,
                    status.RunnerUpStatusId);
            })
            .Where(probe => probe is not null)
            .Cast<RhodesSuiCoinStatusProbe>()
            .OrderBy(probe => probe.SlotIndex)
            .ToArray();
        matchingWatch.Stop();
        diagnostics?.AddStatusMatching(matchingWatch.Elapsed);
        return probes;
    }

    private static AnchoredCoinMatch[] ResolveAnchoredMatches(
        IEnumerable<MaaTaskRunResult> frameTaskResults,
        IReadOnlyList<RhodesSuiCoinImageDetection>? imageInspections,
        RhodesCoinRecognitionDiagnostics? diagnostics)
    {
        var anchorWatch = Stopwatch.StartNew();
        var ocrElapsed = TimeSpan.Zero;
        const string wholeListEntry = "RhodesOcrRegion_is6_coin_list_text";
        var inspectionBySlot = (imageInspections ?? [])
            .Where(inspection => inspection.SlotIndex >= 0)
            .GroupBy(inspection => inspection.SlotIndex)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(inspection => inspection.VisualStrength).First());
        var anchored = new List<AnchoredCoinMatch>();
        foreach (var result in frameTaskResults)
        {
            var ocrWatch = Stopwatch.StartNew();
            var matches = RhodesMaaLocalCandidateConverter.ResolveSuiCoinOcrMatches(result);
            ocrWatch.Stop();
            ocrElapsed += ocrWatch.Elapsed;
            diagnostics?.AddOcr(ocrWatch.Elapsed);
            diagnostics?.IncrementOcrTasks();
            if (matches.Count == 0)
                continue;

            if (result.Entry.Equals(wholeListEntry, StringComparison.Ordinal))
            {
                foreach (var match in matches)
                {
                    anchored.Add(new AnchoredCoinMatch(
                        match.CoinId,
                        match.Label,
                        match.Confidence,
                        RhodesSuiCoinImageRecognizer.MatchOwnedSlot(match.OcrBox) ?? -1,
                        ScreenTextCenterX(match.OcrBox),
                        ScreenTextCenterY(match.OcrBox) - CoinCenterOffsetFromText));
                }
                continue;
            }

            if (!TryParseFocusedSlot(result.Entry, out var slotIndex)
                || !RhodesSuiCoinImageRecognizer.TryGetOwnedSlotCenter(
                    slotIndex,
                    out var centerX,
                    out var centerY))
            {
                continue;
            }

            anchored.AddRange(matches.Select(match => new AnchoredCoinMatch(
                match.CoinId,
                match.Label,
                match.Confidence,
                slotIndex,
                centerX,
                centerY)));
        }

        for (var index = 0; index < anchored.Count; index++)
        {
            var match = anchored[index];
            if (match.SlotIndex < 0
                || !inspectionBySlot.TryGetValue(match.SlotIndex, out var inspection)
                || !string.Equals(inspection.CoinId, match.CoinId, StringComparison.Ordinal)
                || inspection.VisualStrength < MinimumInspectionVisualStrength
                || inspection.Roi.Width <= 0
                || inspection.Roi.Height <= 0)
            {
                continue;
            }

            anchored[index] = match with
            {
                CenterX = inspection.Roi.X + (inspection.Roi.Width / 2d),
                CenterY = inspection.Roi.Y + (inspection.Roi.Height / 2d),
            };
        }

        var ocrResolvedSlots = anchored
            .Where(match => match.SlotIndex >= 0)
            .Select(match => match.SlotIndex)
            .ToHashSet();
        foreach (var inspection in imageInspections ?? [])
        {
            if (ocrResolvedSlots.Contains(inspection.SlotIndex))
            {
                continue;
            }

            if (!RhodesSuiCoinImageRecognizer.IsConfidentOwnedMatch(inspection))
                continue;

            anchored.Add(new AnchoredCoinMatch(
                inspection.CoinId,
                inspection.Label,
                inspection.Score,
                inspection.SlotIndex,
                inspection.Roi.X + (inspection.Roi.Width / 2d),
                inspection.Roi.Y + (inspection.Roi.Height / 2d)));
        }

        var resolved = anchored
            .GroupBy(
                match => (
                    match.CoinId,
                    match.SlotIndex,
                    CenterX: (int)Math.Round(match.CenterX),
                    CenterY: (int)Math.Round(match.CenterY)))
            .Select(group => group
                .OrderByDescending(match => match.Confidence)
                .First())
            .OrderBy(match => match.CenterY)
            .ThenBy(match => match.CenterX)
            .ToArray();
        anchorWatch.Stop();
        diagnostics?.AddAnchor(anchorWatch.Elapsed - ocrElapsed);
        return resolved;
    }

    private static bool TryParseFocusedSlot(string entry, out int slotIndex)
    {
        slotIndex = -1;
        if (!entry.StartsWith(RhodesSuiCoinImageRecognizer.OwnedNameEntryPrefix, StringComparison.Ordinal))
            return false;

        return int.TryParse(
            entry.AsSpan(RhodesSuiCoinImageRecognizer.OwnedNameEntryPrefix.Length),
            out slotIndex);
    }

    private static StatusMatch? BestStatusMatchFast(
        SKBitmap frame,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY,
        RhodesCoinRecognitionDiagnostics? diagnostics)
        => BestStatusMatch(
            frame,
            templates,
            coinCenterX,
            coinCenterY,
            ExpectedStatusOffsetY,
            FastStatusSearchHorizontalRadius,
            FastStatusSearchHorizontalRadius,
            FastStatusSearchVerticalRadius,
            FastStatusSearchVerticalRadius,
            template => FastStatusTemplateWidths.Contains(template.Width),
            diagnostics);

    private static StatusMatch? BestStatusMatchBroad(
        SKBitmap frame,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY,
        RhodesCoinRecognitionDiagnostics? diagnostics) =>
        BestStatusMatch(
            frame,
            templates,
            coinCenterX,
            coinCenterY,
            ExpectedStatusOffsetY,
            StatusSearchLeft,
            StatusSearchRight,
            StatusSearchUp,
            StatusSearchDown,
            null,
            diagnostics);

    private static StatusMatch? BestCombinedBroadStatusMatch(
        SKBitmap frame,
        StatusPresenceEvidence evidence,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY,
        StatusMatch? retainedStatus,
        RhodesCoinRecognitionDiagnostics? diagnostics)
    {
        var residual = BestResidualStatusMatch(
            frame,
            evidence,
            templates,
            coinCenterX,
            coinCenterY,
            diagnostics,
            broad: true);
        var fallback = IsAcceptedStatus(
                retainedStatus,
                evidence,
                allowLowChroma: false)
            ? retainedStatus
            : residual;
        if (fallback is null
            || (
                residual is not null
                && IsVerticalStatusCandidate(residual)
            ))
        {
            return residual ?? fallback;
        }

        var image = BestStatusMatchBroad(
            frame,
            templates,
            coinCenterX,
            coinCenterY,
            diagnostics);
        var imageExtendsAboveResidual = image is not null
            && image.Roi.Y < evidence.CoinRoi.Y - 4;
        return image is not null
            && image.IsConfident
            && image.Score >= (
                imageExtendsAboveResidual
                    ? RaisedStatusImageMinimumScore
                    : MinimumScore
            )
            && (
                imageExtendsAboveResidual
                || image.Score - fallback.Score >= BroadImageRescueMinimumLead
            )
                ? image
                : fallback;
    }

    private static StatusMatch? BestResidualStatusMatch(
        SKBitmap frame,
        StatusPresenceEvidence evidence,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY,
        RhodesCoinRecognitionDiagnostics? diagnostics,
        bool broad = false)
    {
        if (templates.Count == 0)
            return null;

        var expectedX = (int)Math.Round(coinCenterX) + ExpectedStatusOffsetX;
        var expectedY = (int)Math.Round(coinCenterY) + ExpectedStatusOffsetY;
        // The coin name has already anchored the card center. Broad mode expands
        // template sizes, not the screen search area.
        var horizontalRadius = FastStatusSearchHorizontalRadius;
        var verticalUp = FastStatusSearchVerticalRadius;
        var verticalDown = FastStatusSearchVerticalRadius;
        var residualAspectRatio = ResidualGlobalAspectRatio(evidence);
        var ranked = new List<StatusMatch>();
        foreach (var statusGroup in templates.GroupBy(template => template.Option.Id, StringComparer.Ordinal))
        {
            StatusMatch? best = null;
            foreach (var template in statusGroup)
            {
                if (!broad && !FastStatusTemplateWidths.Contains(template.Width))
                    continue;

                for (var y = expectedY - verticalUp; y <= expectedY + verticalDown; y += 2)
                {
                    for (var x = expectedX - horizontalRadius; x <= expectedX + horizontalRadius; x += 2)
                    {
                        var roi = new MaaRoi(x, y, template.Width, template.Height);
                        if (!IsInside(frame, roi))
                            continue;

                        diagnostics?.IncrementStatusShapeComparisons();
                        var residualShapeScore = ResidualShapeSimilarity(evidence, roi, template);
                        var residualColorScore = ResidualColorSimilarity(evidence, roi, template);
                        var residualEdgeScore = ResidualEdgeSimilarity(evidence, roi, template);
                        var residualMomentScore = ResidualMomentSimilarity(evidence, roi, template);
                        var residualGlobalAspectScore = ResidualGlobalAspectSimilarity(evidence, template);
                        var rawCoreScore = RawCoreSimilarity(frame, roi, template);
                        var projectionScore = RawProjectionSimilarity(frame, roi, template);
                        var momentScore = RawMomentSimilarity(frame, roi, template);
                        var hueScore = RawHueSimilarity(frame, roi, template);
                        var coverageScore = broad
                            ? ResidualCoverageSimilarity(evidence, roi)
                            : 1;
                        if (residualShapeScore <= 0
                            || residualColorScore <= 0
                            || residualEdgeScore <= 0
                            || residualMomentScore <= 0
                            || residualGlobalAspectScore <= 0
                            || rawCoreScore <= 0
                            || projectionScore <= 0
                            || momentScore <= 0
                            || hueScore <= 0
                            || coverageScore <= 0)
                            continue;

                        diagnostics?.IncrementStatusColorComparisons();
                        var score = broad
                            ? (0.12 * residualColorScore)
                                + (0.08 * residualShapeScore)
                                + (0.10 * residualEdgeScore)
                                + (0.15 * residualMomentScore)
                                + (0.10 * residualGlobalAspectScore)
                                + (0.10 * rawCoreScore)
                                + (0.10 * projectionScore)
                                + (0.15 * momentScore)
                                + (0.05 * hueScore)
                                + (0.05 * coverageScore)
                            : (0.15 * residualColorScore)
                                + (0.10 * residualShapeScore)
                                + (0.15 * rawCoreScore)
                                + (0.20 * projectionScore)
                                + (0.20 * momentScore)
                                + (0.20 * hueScore);
                        var distance = Math.Sqrt(
                            Math.Pow(roi.X - expectedX, 2)
                            + Math.Pow(roi.Y - expectedY, 2));
                        var candidate = new StatusMatch(
                            template,
                            score,
                            0,
                            roi,
                            distance,
                            false,
                            residualEdgeScore,
                            residualMomentScore,
                            residualGlobalAspectScore,
                            coverageScore,
                            residualAspectRatio);
                        if (best is null
                            || candidate.Score > best.Score
                            || (
                                Math.Abs(candidate.Score - best.Score) <= 0.000001
                                && candidate.DistanceToExpected < best.DistanceToExpected
                            ))
                        {
                            best = candidate;
                        }
                    }
                }
            }

            if (best is not null)
                ranked.Add(best);
        }

        var ordered = ranked
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.DistanceToExpected)
            .ToArray();
        if (ordered.Length == 0)
            return null;

        var winner = ordered[0];
        if (broad && residualAspectRatio is > 0 and <= VerticalStatusMaximumAspectRatio)
        {
            var verticalStatus = ordered.FirstOrDefault(IsVerticalStatusCandidate);
            if (verticalStatus is not null
                && winner.Score - verticalStatus.Score <= VerticalStatusMaximumLeaderGap)
            {
                winner = verticalStatus;
            }
        }

        var runnerUp = ordered.FirstOrDefault(match =>
            !match.Template.Option.Id.Equals(winner.Template.Option.Id, StringComparison.Ordinal));
        var hasVerticalStructure = IsVerticalStatusCandidate(winner);
        return winner with
        {
            RunnerUpScore = runnerUp?.Score ?? 0,
            RunnerUpStatusId = runnerUp?.Template.Option.Id ?? "",
            IsConfident = hasVerticalStructure
                || runnerUp is null
                || winner.Score - runnerUp.Score >= ResidualMinimumMargin,
        };
    }

    private static bool IsVerticalStatusCandidate(StatusMatch status) =>
        status.Template.Option.Id.EndsWith("is6_gild5", StringComparison.Ordinal)
        && status.ResidualAspectRatio is > 0 and <= VerticalStatusMaximumAspectRatio
        && status.Score >= VerticalStatusMinimumScore
        && status.ResidualCoverageScore >= VerticalStatusMinimumCoverageScore
        && status.ResidualGlobalAspectScore >= VerticalStatusMinimumGlobalAspectScore;

    private static double ResidualCoverageSimilarity(
        StatusPresenceEvidence evidence,
        MaaRoi statusRoi)
    {
        var coinRoi = evidence.CoinRoi;
        double total = 0;
        double covered = 0;
        for (var featureY = 0; featureY < FeatureSize; featureY++)
        {
            for (var featureX = 0; featureX < FeatureSize; featureX++)
            {
                if (!IsStatusResidualPixel(featureX, featureY))
                    continue;

                var index = (featureY * FeatureSize) + featureX;
                var weight = Math.Max(0, evidence.ResidualMagnitude[index] - 0.03);
                if (weight <= 0)
                    continue;

                var screenX = coinRoi.X + ((featureX + 0.5) * coinRoi.Width / FeatureSize);
                var screenY = coinRoi.Y + ((featureY + 0.5) * coinRoi.Height / FeatureSize);
                total += weight;
                if (screenX >= statusRoi.X
                    && screenX < statusRoi.X + statusRoi.Width
                    && screenY >= statusRoi.Y
                    && screenY < statusRoi.Y + statusRoi.Height)
                {
                    covered += weight;
                }
            }
        }

        return total <= 0.000001
            ? 0
            : Math.Clamp(covered / total, 0, 1);
    }

    private static double ResidualShapeSimilarity(
        StatusPresenceEvidence evidence,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var coinRoi = evidence.CoinRoi;
        var left = Math.Max(statusRoi.X, coinRoi.X);
        var top = Math.Max(statusRoi.Y, coinRoi.Y);
        var right = Math.Min(statusRoi.X + statusRoi.Width, coinRoi.X + coinRoi.Width);
        var bottom = Math.Min(statusRoi.Y + statusRoi.Height, coinRoi.Y + coinRoi.Height);
        if (right - left < 8 || bottom - top < 8)
            return 0;

        var expectedValues = new List<double>();
        var actualValues = new List<double>();
        double foregroundSignal = 0;
        double foregroundWeight = 0;
        double backgroundSignal = 0;
        double backgroundWeight = 0;
        for (var featureY = 0; featureY < FeatureSize; featureY++)
        {
            var screenY = coinRoi.Y + ((featureY + 0.5) * coinRoi.Height / FeatureSize);
            if (screenY < top || screenY >= bottom)
                continue;

            var templateY = Math.Clamp(
                (int)Math.Floor((screenY - statusRoi.Y) * template.Height / statusRoi.Height),
                0,
                template.Height - 1);
            for (var featureX = 0; featureX < FeatureSize; featureX++)
            {
                var screenX = coinRoi.X + ((featureX + 0.5) * coinRoi.Width / FeatureSize);
                if (screenX < left || screenX >= right)
                    continue;

                var templateX = Math.Clamp(
                    (int)Math.Floor((screenX - statusRoi.X) * template.Width / statusRoi.Width),
                    0,
                    template.Width - 1);
                var alpha = ShapeMaskValue(template.AlphaMask[(templateY * template.Width) + templateX]);
                var residual = evidence.ResidualMagnitude[(featureY * FeatureSize) + featureX];
                expectedValues.Add(alpha);
                actualValues.Add(residual);
                if (alpha >= 0.25)
                {
                    foregroundSignal += alpha * residual;
                    foregroundWeight += alpha;
                }
                else
                {
                    backgroundSignal += (1 - alpha) * residual;
                    backgroundWeight += 1 - alpha;
                }
            }
        }
        if (expectedValues.Count < 24 || foregroundWeight <= 0)
            return 0;

        var correlation = NormalizedCorrelation(expectedValues, actualValues);
        var foregroundMean = foregroundSignal / foregroundWeight;
        var backgroundMean = backgroundWeight <= 0 ? 0 : backgroundSignal / backgroundWeight;
        var contrast = Math.Clamp((foregroundMean - backgroundMean) / 0.16, 0, 1);
        var strength = Math.Clamp(foregroundMean / 0.22, 0, 1);
        return (0.60 * correlation) + (0.25 * contrast) + (0.15 * strength);
    }

    private static double ResidualColorSimilarity(
        StatusPresenceEvidence evidence,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        double dot = 0;
        double expectedNorm = 0;
        double actualNorm = 0;
        var coinRoi = evidence.CoinRoi;
        foreach (var pixel in template.Pixels)
        {
            var screenX = statusRoi.X + pixel.X + 0.5;
            var screenY = statusRoi.Y + pixel.Y + 0.5;
            if (screenX < coinRoi.X
                || screenX >= coinRoi.X + coinRoi.Width
                || screenY < coinRoi.Y
                || screenY >= coinRoi.Y + coinRoi.Height)
            {
                continue;
            }

            var featureX = Math.Clamp(
                (int)Math.Floor((screenX - coinRoi.X) * FeatureSize / coinRoi.Width),
                0,
                FeatureSize - 1);
            var featureY = Math.Clamp(
                (int)Math.Floor((screenY - coinRoi.Y) * FeatureSize / coinRoi.Height),
                0,
                FeatureSize - 1);
            var index = (featureY * FeatureSize) + featureX;
            var weight = Math.Sqrt(pixel.Weight);
            var expectedRed = weight * pixel.Red / 255d;
            var expectedGreen = weight * pixel.Green / 255d;
            var expectedBlue = weight * pixel.Blue / 255d;
            var actualRed = weight * Math.Max(0, evidence.ResidualRed[index]);
            var actualGreen = weight * Math.Max(0, evidence.ResidualGreen[index]);
            var actualBlue = weight * Math.Max(0, evidence.ResidualBlue[index]);
            dot += (expectedRed * actualRed)
                + (expectedGreen * actualGreen)
                + (expectedBlue * actualBlue);
            expectedNorm += (expectedRed * expectedRed)
                + (expectedGreen * expectedGreen)
                + (expectedBlue * expectedBlue);
            actualNorm += (actualRed * actualRed)
                + (actualGreen * actualGreen)
                + (actualBlue * actualBlue);
        }

        var denominator = Math.Sqrt(expectedNorm * actualNorm);
        if (denominator <= 0.000001)
            return 0;
        return Math.Clamp(dot / denominator, 0, 1);
    }

    private static double ResidualEdgeSimilarity(
        StatusPresenceEvidence evidence,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var coinRoi = evidence.CoinRoi;
        var left = Math.Max(statusRoi.X, coinRoi.X);
        var top = Math.Max(statusRoi.Y, coinRoi.Y);
        var right = Math.Min(statusRoi.X + statusRoi.Width, coinRoi.X + coinRoi.Width);
        var bottom = Math.Min(statusRoi.Y + statusRoi.Height, coinRoi.Y + coinRoi.Height);
        if (right - left < 8 || bottom - top < 8)
            return 0;

        double dot = 0;
        double expectedNorm = 0;
        double actualNorm = 0;
        var sampleCount = 0;
        for (var featureY = 1; featureY < FeatureSize - 1; featureY++)
        {
            var screenY = coinRoi.Y + ((featureY + 0.5) * coinRoi.Height / FeatureSize);
            if (screenY < top || screenY >= bottom)
                continue;

            for (var featureX = 1; featureX < FeatureSize - 1; featureX++)
            {
                var screenX = coinRoi.X + ((featureX + 0.5) * coinRoi.Width / FeatureSize);
                if (screenX < left || screenX >= right)
                    continue;

                var templateX = Math.Clamp(
                    (int)Math.Floor((screenX - statusRoi.X) * template.Width / statusRoi.Width),
                    0,
                    template.Width - 1);
                var templateY = Math.Clamp(
                    (int)Math.Floor((screenY - statusRoi.Y) * template.Height / statusRoi.Height),
                    0,
                    template.Height - 1);
                var templateLeft = ShapeMaskValue(
                    template.AlphaMask[
                        (templateY * template.Width) + Math.Max(0, templateX - 1)]);
                var templateRight = ShapeMaskValue(
                    template.AlphaMask[
                        (templateY * template.Width) + Math.Min(template.Width - 1, templateX + 1)]);
                var templateTop = ShapeMaskValue(
                    template.AlphaMask[
                        (Math.Max(0, templateY - 1) * template.Width) + templateX]);
                var templateBottom = ShapeMaskValue(
                    template.AlphaMask[
                        (Math.Min(template.Height - 1, templateY + 1) * template.Width) + templateX]);
                var expectedEdge = Math.Sqrt(
                    Math.Pow(templateRight - templateLeft, 2)
                    + Math.Pow(templateBottom - templateTop, 2));

                var featureIndex = (featureY * FeatureSize) + featureX;
                var residualLeft = evidence.ResidualMagnitude[featureIndex - 1];
                var residualRight = evidence.ResidualMagnitude[featureIndex + 1];
                var residualTop = evidence.ResidualMagnitude[featureIndex - FeatureSize];
                var residualBottom = evidence.ResidualMagnitude[featureIndex + FeatureSize];
                var actualEdge = Math.Sqrt(
                    Math.Pow(residualRight - residualLeft, 2)
                    + Math.Pow(residualBottom - residualTop, 2));
                dot += expectedEdge * actualEdge;
                expectedNorm += expectedEdge * expectedEdge;
                actualNorm += actualEdge * actualEdge;
                sampleCount++;
            }
        }

        if (sampleCount < 24)
            return 0;
        var denominator = Math.Sqrt(expectedNorm * actualNorm);
        if (denominator <= 0.000001)
            return 0;
        return Math.Clamp(dot / denominator, 0, 1);
    }

    private static double ResidualMomentSimilarity(
        StatusPresenceEvidence evidence,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var expected = MeasureCoreMoments(
            template.Width,
            template.Height,
            (x, y) => ShapeMaskValue(template.AlphaMask[(y * template.Width) + x]));
        var coinRoi = evidence.CoinRoi;
        var actual = MeasureCoreMoments(
            template.Width,
            template.Height,
            (x, y) =>
            {
                var screenX = statusRoi.X + ((x + 0.5) * statusRoi.Width / template.Width);
                var screenY = statusRoi.Y + ((y + 0.5) * statusRoi.Height / template.Height);
                if (screenX < coinRoi.X
                    || screenX >= coinRoi.X + coinRoi.Width
                    || screenY < coinRoi.Y
                    || screenY >= coinRoi.Y + coinRoi.Height)
                {
                    return 0;
                }

                var featureX = Math.Clamp(
                    (int)Math.Floor((screenX - coinRoi.X) * FeatureSize / coinRoi.Width),
                    0,
                    FeatureSize - 1);
                var featureY = Math.Clamp(
                    (int)Math.Floor((screenY - coinRoi.Y) * FeatureSize / coinRoi.Height),
                    0,
                    FeatureSize - 1);
                return Math.Clamp(
                    (evidence.ResidualMagnitude[(featureY * FeatureSize) + featureX] - 0.025) / 0.20,
                    0,
                    1);
            });
        if (expected.Weight <= 0 || actual.Weight <= 0)
            return 0;

        var aspectScore = Math.Exp(-Math.Abs(Math.Log(
            Math.Max(0.05, actual.AspectRatio) / Math.Max(0.05, expected.AspectRatio))));
        var centerDistance = Math.Sqrt(
            Math.Pow(actual.CenterX - expected.CenterX, 2)
            + Math.Pow(actual.CenterY - expected.CenterY, 2));
        var centerScore = Math.Exp(-centerDistance * 5);
        return (0.80 * aspectScore) + (0.20 * centerScore);
    }

    private static double ResidualGlobalAspectSimilarity(
        StatusPresenceEvidence evidence,
        StatusTemplate template)
    {
        var expected = MeasurePixelMoments(
            template.Width,
            template.Height,
            (x, y) => ShapeMaskValue(template.AlphaMask[(y * template.Width) + x]));
        var actual = MeasurePixelMoments(
            FeatureSize,
            FeatureSize,
            (x, y) =>
            {
                if (!IsStatusResidualPixel(x, y))
                    return 0;
                return Math.Clamp(
                    (evidence.ResidualMagnitude[(y * FeatureSize) + x] - 0.035) / 0.20,
                    0,
                    1);
            });
        if (expected.Weight <= 0 || actual.Weight <= 0)
            return 0;

        return Math.Exp(-Math.Abs(Math.Log(
            Math.Max(0.02, actual.AspectRatio) / Math.Max(0.02, expected.AspectRatio))));
    }

    private static double ResidualGlobalAspectRatio(StatusPresenceEvidence evidence)
    {
        var actual = MeasurePixelMoments(
            FeatureSize,
            FeatureSize,
            (x, y) =>
            {
                if (!IsStatusResidualPixel(x, y))
                    return 0;
                return Math.Clamp(
                    (evidence.ResidualMagnitude[(y * FeatureSize) + x] - 0.035) / 0.20,
                    0,
                    1);
            });
        return actual.Weight <= 0 ? 0 : actual.AspectRatio;
    }

    private static CoreMoments MeasurePixelMoments(
        int width,
        int height,
        Func<int, int, double> signalAt)
    {
        double weight = 0;
        double centerX = 0;
        double centerY = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var signal = Math.Max(0, signalAt(x, y) - 0.12);
                if (signal <= 0)
                    continue;
                weight += signal;
                centerX += signal * (x + 0.5);
                centerY += signal * (y + 0.5);
            }
        }
        if (weight <= 0)
            return new CoreMoments(0, 0, 0, 0);

        centerX /= weight;
        centerY /= weight;
        double varianceX = 0;
        double varianceY = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var signal = Math.Max(0, signalAt(x, y) - 0.12);
                if (signal <= 0)
                    continue;
                varianceX += signal * Math.Pow((x + 0.5) - centerX, 2);
                varianceY += signal * Math.Pow((y + 0.5) - centerY, 2);
            }
        }
        varianceX /= weight;
        varianceY /= weight;
        return new CoreMoments(
            weight,
            centerX,
            centerY,
            varianceX / Math.Max(0.000001, varianceY));
    }

    private static double RawCoreSimilarity(
        SKBitmap frame,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var expectedValues = new List<double>(template.Width * template.Height);
        var actualValues = new List<double>(template.Width * template.Height);
        double foregroundSignal = 0;
        double foregroundWeight = 0;
        double backgroundSignal = 0;
        double backgroundWeight = 0;
        for (var y = 0; y < template.Height; y++)
        {
            for (var x = 0; x < template.Width; x++)
            {
                var index = (y * template.Width) + x;
                var templatePixel = template.ColorPixels[index];
                var alpha = templatePixel.Alpha / 255d;
                var templateCore = alpha * Math.Clamp(
                    (Math.Min(templatePixel.Red, Math.Min(templatePixel.Green, templatePixel.Blue)) - 72d) / 183d,
                    0,
                    1);
                var actual = frame.GetPixel(statusRoi.X + x, statusRoi.Y + y);
                var actualCore = Math.Clamp(
                    (Math.Min(actual.Red, Math.Min(actual.Green, actual.Blue)) - 112d) / 143d,
                    0,
                    1);
                expectedValues.Add(templateCore);
                actualValues.Add(actualCore);

                if (templateCore >= 0.20)
                {
                    foregroundSignal += templateCore * actualCore;
                    foregroundWeight += templateCore;
                }
                else
                {
                    var weight = 1 - templateCore;
                    backgroundSignal += weight * actualCore;
                    backgroundWeight += weight;
                }
            }
        }
        if (foregroundWeight <= 0)
            return 0;

        var correlation = NormalizedCorrelation(expectedValues, actualValues);
        var foregroundMean = foregroundSignal / foregroundWeight;
        var backgroundMean = backgroundWeight <= 0 ? 0 : backgroundSignal / backgroundWeight;
        var contrast = Math.Clamp((foregroundMean - backgroundMean) / 0.45, 0, 1);
        var strength = Math.Clamp(foregroundMean / 0.65, 0, 1);
        return (0.70 * correlation) + (0.20 * contrast) + (0.10 * strength);
    }

    private static double RawProjectionSimilarity(
        SKBitmap frame,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        const int projectionBins = 16;
        var expectedRows = new double[projectionBins];
        var expectedColumns = new double[projectionBins];
        var actualRows = new double[projectionBins];
        var actualColumns = new double[projectionBins];
        for (var y = 0; y < template.Height; y++)
        {
            var row = Math.Min(projectionBins - 1, y * projectionBins / template.Height);
            for (var x = 0; x < template.Width; x++)
            {
                var column = Math.Min(projectionBins - 1, x * projectionBins / template.Width);
                var index = (y * template.Width) + x;
                var templatePixel = template.ColorPixels[index];
                var expectedCore = (templatePixel.Alpha / 255d) * Math.Clamp(
                    (Math.Min(templatePixel.Red, Math.Min(templatePixel.Green, templatePixel.Blue)) - 72d) / 183d,
                    0,
                    1);
                var actual = frame.GetPixel(statusRoi.X + x, statusRoi.Y + y);
                var actualCore = Math.Clamp(
                    (Math.Min(actual.Red, Math.Min(actual.Green, actual.Blue)) - 112d) / 143d,
                    0,
                    1);
                expectedRows[row] += expectedCore;
                expectedColumns[column] += expectedCore;
                actualRows[row] += actualCore;
                actualColumns[column] += actualCore;
            }
        }

        var rowScore = NormalizedCorrelation(expectedRows, actualRows);
        var columnScore = NormalizedCorrelation(expectedColumns, actualColumns);
        return (rowScore + columnScore) / 2;
    }

    private static double RawMomentSimilarity(
        SKBitmap frame,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var expected = MeasureCoreMoments(
            template.Width,
            template.Height,
            (x, y) =>
            {
                var pixel = template.ColorPixels[(y * template.Width) + x];
                var alpha = pixel.Alpha / 255d;
                return alpha * Math.Clamp(
                    (Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue)) - 96d) / 159d,
                    0,
                    1);
            });
        var actual = MeasureCoreMoments(
            template.Width,
            template.Height,
            (x, y) =>
            {
                var pixel = frame.GetPixel(statusRoi.X + x, statusRoi.Y + y);
                return Math.Clamp(
                    (Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue)) - 144d) / 111d,
                    0,
                    1);
            });
        if (expected.Weight <= 0 || actual.Weight <= 0)
            return 0;

        var aspectScore = Math.Exp(-Math.Abs(Math.Log(
            Math.Max(0.05, actual.AspectRatio) / Math.Max(0.05, expected.AspectRatio))));
        var centerDistance = Math.Sqrt(
            Math.Pow(actual.CenterX - expected.CenterX, 2)
            + Math.Pow(actual.CenterY - expected.CenterY, 2));
        var centerScore = Math.Exp(-centerDistance * 5);
        return (0.75 * aspectScore) + (0.25 * centerScore);
    }

    private static CoreMoments MeasureCoreMoments(
        int width,
        int height,
        Func<int, int, double> signalAt)
    {
        double weight = 0;
        double centerX = 0;
        double centerY = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var signal = Math.Max(0, signalAt(x, y) - 0.12);
                if (signal <= 0)
                    continue;
                weight += signal;
                centerX += signal * ((x + 0.5) / width);
                centerY += signal * ((y + 0.5) / height);
            }
        }
        if (weight <= 0)
            return new CoreMoments(0, 0, 0, 0);

        centerX /= weight;
        centerY /= weight;
        double varianceX = 0;
        double varianceY = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var signal = Math.Max(0, signalAt(x, y) - 0.12);
                if (signal <= 0)
                    continue;
                varianceX += signal * Math.Pow(((x + 0.5) / width) - centerX, 2);
                varianceY += signal * Math.Pow(((y + 0.5) / height) - centerY, 2);
            }
        }
        varianceX /= weight;
        varianceY /= weight;
        return new CoreMoments(
            weight,
            centerX,
            centerY,
            varianceX / Math.Max(0.000001, varianceY));
    }

    private static double RawHueSimilarity(
        SKBitmap frame,
        MaaRoi statusRoi,
        StatusTemplate template)
    {
        var expected = MeasureDominantHue(
            template.Width,
            template.Height,
            (x, y) =>
            {
                var pixel = template.ColorPixels[(y * template.Width) + x];
                return (pixel.Red, pixel.Green, pixel.Blue, pixel.Alpha / 255d);
            });
        var actual = MeasureDominantHue(
            template.Width,
            template.Height,
            (x, y) =>
            {
                var pixel = frame.GetPixel(statusRoi.X + x, statusRoi.Y + y);
                return (pixel.Red, pixel.Green, pixel.Blue, 1d);
            });
        var denominator = Math.Sqrt(
            ((expected.Red * expected.Red)
                + (expected.Green * expected.Green)
                + (expected.Blue * expected.Blue))
            * ((actual.Red * actual.Red)
                + (actual.Green * actual.Green)
                + (actual.Blue * actual.Blue)));
        if (denominator <= 0.000001)
            return 0.5;
        var cosine = ((expected.Red * actual.Red)
            + (expected.Green * actual.Green)
            + (expected.Blue * actual.Blue)) / denominator;
        return Math.Clamp((cosine + 1) / 2, 0, 1);
    }

    private static HueVector MeasureDominantHue(
        int width,
        int height,
        Func<int, int, (byte Red, byte Green, byte Blue, double Alpha)> pixelAt)
    {
        double red = 0;
        double green = 0;
        double blue = 0;
        double totalWeight = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = pixelAt(x, y);
                var maximum = Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue));
                var minimum = Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue));
                var chroma = (maximum - minimum) / 255d;
                var brightness = maximum / 255d;
                var weight = pixel.Alpha * chroma * brightness;
                if (weight <= 0.02)
                    continue;
                var mean = (pixel.Red + pixel.Green + pixel.Blue) / 3d;
                red += weight * (pixel.Red - mean);
                green += weight * (pixel.Green - mean);
                blue += weight * (pixel.Blue - mean);
                totalWeight += weight;
            }
        }
        return totalWeight <= 0
            ? new HueVector(0, 0, 0)
            : new HueVector(red / totalWeight, green / totalWeight, blue / totalWeight);
    }

    private static double NormalizedCorrelation(
        IReadOnlyList<double> expected,
        IReadOnlyList<double> actual)
    {
        var expectedMean = expected.Average();
        var actualMean = actual.Average();
        double covariance = 0;
        double expectedVariance = 0;
        double actualVariance = 0;
        for (var index = 0; index < expected.Count; index++)
        {
            var expectedDelta = expected[index] - expectedMean;
            var actualDelta = actual[index] - actualMean;
            covariance += expectedDelta * actualDelta;
            expectedVariance += expectedDelta * expectedDelta;
            actualVariance += actualDelta * actualDelta;
        }

        var denominator = Math.Sqrt(expectedVariance * actualVariance);
        if (denominator <= 0.000001)
            return 0;
        return Math.Clamp(((covariance / denominator) + 1) / 2, 0, 1);
    }

    private static bool IsResidualStatusPresent(
        StatusMatch status,
        StatusPresenceEvidence evidence,
        bool allowLowChroma)
    {
        if (allowLowChroma
            && IsVerticalStatusCandidate(status)
            && evidence.OverlayDifference >= 0.06
            && evidence.EdgeDensity >= 0.12
            && HasStructuralStatusEvidence(evidence))
        {
            return true;
        }

        if (evidence.OverlayDifference >= MinimumOverlayDifference
            && evidence.ChromaDensity >= 0.04
            && HasStructuralStatusEvidence(evidence)
            && status.Score >= 0.61
            && status.IsConfident)
        {
            return true;
        }

        return allowLowChroma
            && evidence.ChromaDensity < 0.04
            && evidence.OverlayDifference >= 0.04
            && HasStructuralStatusEvidence(evidence)
            && status.IsConfident
            && (
                (
                    status.Score >= LowChromaMinimumScore
                    && status.Score - status.RunnerUpScore >= ResidualMinimumMargin
                )
                || (
                    status.Score >= LowChromaShapeRescueMinimumScore
                    && status.Score - status.RunnerUpScore >= LowChromaShapeRescueMinimumMargin
                    && status.ResidualMomentScore >= LowChromaShapeRescueMinimumMomentScore
                )
            );
    }

    private static bool HasStructuralStatusEvidence(StatusPresenceEvidence evidence) =>
        evidence.OverlayDifference - evidence.BodyDifference >= MinimumOverlayBodyDifference
        || evidence.EdgeDensity >= MinimumStructuralEdgeDensity;

    private static StatusMatch? BestStatusMatch(
        SKBitmap frame,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY,
        int statusOffsetY,
        int searchLeft,
        int searchRight,
        int searchUp,
        int searchDown,
        Func<StatusTemplate, bool>? templateFilter,
        RhodesCoinRecognitionDiagnostics? diagnostics)
    {
        if (templates.Count == 0)
            return null;

        var expectedX = (int)Math.Round(coinCenterX) + ExpectedStatusOffsetX;
        var expectedY = (int)Math.Round(coinCenterY) + statusOffsetY;
        var candidatesByStatus = new Dictionary<string, List<StatusCandidate>>(StringComparer.Ordinal);
        foreach (var template in templates)
        {
            if (templateFilter is not null && !templateFilter(template))
                continue;

            for (var y = expectedY - searchUp; y <= expectedY + searchDown; y += 2)
            {
                for (var x = expectedX - searchLeft; x <= expectedX + searchRight; x += 2)
                {
                    var roi = new MaaRoi(x, y, template.Width, template.Height);
                    if (!IsInside(frame, roi))
                        continue;

                    diagnostics?.IncrementStatusColorComparisons();
                    var colorScore = ColorSimilarity(frame, roi, template);
                    if (!candidatesByStatus.TryGetValue(template.Option.Id, out var candidates))
                        candidatesByStatus[template.Option.Id] = candidates = [];
                    AddColorCandidate(candidates, new StatusCandidate(template, colorScore, roi));
                }
            }

        }

        var ranked = candidatesByStatus.Values
            .Select(candidates => candidates
                .Select(candidate =>
                {
                    diagnostics?.IncrementStatusShapeComparisons();
                    var shapeScore = ShapeSimilarity(frame, candidate.Roi, candidate.Template);
                    var score = (ColorScoreWeight * candidate.ColorScore)
                        + (ShapeScoreWeight * shapeScore);
                    var distanceToExpected = Math.Sqrt(
                        Math.Pow(candidate.Roi.X - expectedX, 2)
                        + Math.Pow(candidate.Roi.Y - expectedY, 2));
                    return new StatusMatch(
                        candidate.Template,
                        score,
                        0,
                        candidate.Roi,
                        distanceToExpected,
                        false);
                })
                .OrderByDescending(match => match.Score)
                .First())
            .OrderByDescending(match => match.Score)
            .ToArray();
        if (ranked.Length == 0)
            return null;

        var rawWinner = ranked[0];
        var rawRunnerUp = ranked.ElementAtOrDefault(1);
        var rawMargin = rawWinner.Score - (rawRunnerUp?.Score ?? 0);
        if (rawRunnerUp is null || rawMargin >= MinimumMargin)
        {
            return rawWinner with
            {
                RunnerUpScore = rawRunnerUp?.Score ?? 0,
                RunnerUpStatusId = rawRunnerUp?.Template.Option.Id ?? "",
                IsConfident = true,
            };
        }

        var spatiallyRanked = ranked
            .Where(match => rawWinner.Score - match.Score <= SpatialTieScoreWindow)
            .OrderBy(match => match.DistanceToExpected)
            .ThenByDescending(match => match.Score)
            .ToArray();
        var spatialWinner = spatiallyRanked[0];
        var spatialRunnerUp = spatiallyRanked.ElementAtOrDefault(1);
        var spatialDistanceGap = (spatialRunnerUp?.DistanceToExpected ?? double.MaxValue)
            - spatialWinner.DistanceToExpected;
        return spatialWinner with
        {
            RunnerUpScore = rawWinner.Template.Option.Id == spatialWinner.Template.Option.Id
                ? rawRunnerUp?.Score ?? 0
                : rawWinner.Score,
            RunnerUpStatusId = rawWinner.Template.Option.Id == spatialWinner.Template.Option.Id
                ? rawRunnerUp?.Template.Option.Id ?? ""
                : rawWinner.Template.Option.Id,
            IsConfident = spatialDistanceGap >= MinimumSpatialTieDistanceGap,
        };
    }

    private static StatusMatch? StrongerStatusMatch(StatusMatch? left, StatusMatch? right)
    {
        if (left is null)
            return right;
        if (right is null)
            return left;

        var winner = left.Score >= right.Score ? left : right;
        var other = ReferenceEquals(winner, left) ? right : left;
        var runnerUpScore = winner.Template.Option.Id.Equals(other.Template.Option.Id, StringComparison.Ordinal)
            ? Math.Max(winner.RunnerUpScore, other.RunnerUpScore)
            : Math.Max(winner.RunnerUpScore, other.Score);
        return winner with
        {
            RunnerUpScore = runnerUpScore,
            RunnerUpStatusId = winner.Template.Option.Id.Equals(other.Template.Option.Id, StringComparison.Ordinal)
                ? winner.RunnerUpStatusId
                : other.Template.Option.Id,
            IsConfident = winner.Score - runnerUpScore >= MinimumMargin,
        };
    }

    private static void AddColorCandidate(
        List<StatusCandidate> candidates,
        StatusCandidate candidate)
    {
        candidates.Add(candidate);
        if (candidates.Count <= MaximumColorCandidatesPerStatus)
            return;

        var weakestIndex = 0;
        for (var index = 1; index < candidates.Count; index++)
        {
            if (candidates[index].ColorScore < candidates[weakestIndex].ColorScore)
                weakestIndex = index;
        }
        candidates.RemoveAt(weakestIndex);
    }

    private static double ColorSimilarity(SKBitmap frame, MaaRoi roi, StatusTemplate template)
    {
        double difference = 0;
        double weight = 0;
        foreach (var pixel in template.Pixels)
        {
            var actual = frame.GetPixel(roi.X + pixel.X, roi.Y + pixel.Y);
            difference += pixel.Weight * (
                Math.Abs(actual.Red - pixel.Red)
                + Math.Abs(actual.Green - pixel.Green)
                + Math.Abs(actual.Blue - pixel.Blue));
            weight += pixel.Weight;
        }
        return weight <= 0 ? 0 : 1 - (difference / (weight * 3 * 255));
    }

    private static double ShapeSimilarity(SKBitmap frame, MaaRoi roi, StatusTemplate template)
    {
        var count = template.AlphaMask.Count;
        if (count == 0)
            return 0;

        double expectedSum = 0;
        double actualSum = 0;
        for (var index = 0; index < count; index++)
        {
            var x = index % template.Width;
            var y = index / template.Width;
            expectedSum += ShapeMaskValue(template.AlphaMask[index]);
            actualSum += Luminance(frame.GetPixel(roi.X + x, roi.Y + y)) / 255d;
        }

        var expectedMean = expectedSum / count;
        var actualMean = actualSum / count;
        double covariance = 0;
        double expectedVariance = 0;
        double actualVariance = 0;
        for (var index = 0; index < count; index++)
        {
            var x = index % template.Width;
            var y = index / template.Width;
            var expected = ShapeMaskValue(template.AlphaMask[index]) - expectedMean;
            var actual = (Luminance(frame.GetPixel(roi.X + x, roi.Y + y)) / 255d) - actualMean;
            covariance += expected * actual;
            expectedVariance += expected * expected;
            actualVariance += actual * actual;
        }

        var denominator = Math.Sqrt(expectedVariance * actualVariance);
        if (denominator <= 0.000001)
            return 0;

        return Math.Clamp(((covariance / denominator) + 1) / 2, 0, 1);
    }

    private static double ShapeMaskValue(byte alpha) =>
        alpha <= ShapeAlphaFloor
            ? 0
            : (alpha - ShapeAlphaFloor) / (double)(byte.MaxValue - ShapeAlphaFloor);

    private static double Luminance(SKColor color) =>
        (0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue);

    private static StatusPresenceEvidence? MeasureStatusPresence(
        SKBitmap frame,
        CoinBaselineTemplate template,
        double centerX,
        double centerY,
        RhodesCoinRecognitionDiagnostics? diagnostics)
    {
        double bestBodyDifference = double.MaxValue;
        StatusPresenceEvidence? bestEvidence = null;
        foreach (var size in CoinTemplateSizes)
        {
            for (var yOffset = -4; yOffset <= 4; yOffset += 4)
            {
                for (var xOffset = -4; xOffset <= 4; xOffset += 4)
                {
                    var roi = new MaaRoi(
                        (int)Math.Round(centerX) - (size / 2) + xOffset,
                        (int)Math.Round(centerY) - (size / 2) + yOffset,
                        size,
                        size);
                    if (!IsInside(frame, roi))
                        continue;

                    diagnostics?.IncrementOverlayComparisons();
                    var evidence = CompareCoinRegions(frame, roi, template);
                    if (evidence.BodyDifference < bestBodyDifference)
                    {
                        bestBodyDifference = evidence.BodyDifference;
                        bestEvidence = evidence;
                    }
                }
            }
        }

        return bestEvidence;
    }

    private static StatusPresenceEvidence CompareCoinRegions(
        SKBitmap frame,
        MaaRoi roi,
        CoinBaselineTemplate template)
    {
        using var crop = new SKBitmap(roi.Width, roi.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                frame,
                new SKRect(roi.X, roi.Y, roi.X + roi.Width, roi.Y + roi.Height),
                new SKRect(0, 0, roi.Width, roi.Height));
        }
        using var resized = Resize(crop, FeatureSize, FeatureSize);
        var actualPixels = new SKColor[FeatureSize * FeatureSize];
        for (var y = 0; y < FeatureSize; y++)
        {
            for (var x = 0; x < FeatureSize; x++)
                actualPixels[(y * FeatureSize) + x] = resized.GetPixel(x, y);
        }
        var redTransform = FitCoinChannel(template, actualPixels, 0);
        var greenTransform = FitCoinChannel(template, actualPixels, 1);
        var blueTransform = FitCoinChannel(template, actualPixels, 2);
        var redBackground = FitBackgroundPlane(template, actualPixels, 0);
        var greenBackground = FitBackgroundPlane(template, actualPixels, 1);
        var blueBackground = FitBackgroundPlane(template, actualPixels, 2);

        double bodyDifference = 0;
        double bodyWeight = 0;
        double overlayDifference = 0;
        double overlayWeight = 0;
        var residualMagnitude = new double[FeatureSize * FeatureSize];
        var residualChroma = new double[FeatureSize * FeatureSize];
        var residualRed = new double[FeatureSize * FeatureSize];
        var residualGreen = new double[FeatureSize * FeatureSize];
        var residualBlue = new double[FeatureSize * FeatureSize];
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            var actual = actualPixels[index];
            var alpha = expected.Alpha / 255d;
            var expectedRed = (alpha * redTransform.Predict(expected.Red))
                + ((1 - alpha) * redBackground.Predict(x, y));
            var expectedGreen = (alpha * greenTransform.Predict(expected.Green))
                + ((1 - alpha) * greenBackground.Predict(x, y));
            var expectedBlue = (alpha * blueTransform.Predict(expected.Blue))
                + ((1 - alpha) * blueBackground.Predict(x, y));
            var redResidual = actual.Red - expectedRed;
            var greenResidual = actual.Green - expectedGreen;
            var blueResidual = actual.Blue - expectedBlue;
            var difference = Math.Abs(redResidual)
                + Math.Abs(greenResidual)
                + Math.Abs(blueResidual);
            var isStatusOverlayPixel = IsStatusOverlayPixel(x, y);
            var isStatusResidualPixel = IsStatusResidualPixel(x, y);
            if (isStatusResidualPixel)
            {
                residualMagnitude[index] = difference / (3 * 255d);
                residualRed[index] = redResidual / 255d;
                residualGreen[index] = greenResidual / 255d;
                residualBlue[index] = blueResidual / 255d;
                residualChroma[index] = (
                    Math.Max(redResidual, Math.Max(greenResidual, blueResidual))
                    - Math.Min(redResidual, Math.Min(greenResidual, blueResidual))) / (2 * 255d);
            }

            if (isStatusOverlayPixel)
            {
                overlayDifference += difference;
                overlayWeight++;
            }
            else if (!isStatusResidualPixel)
            {
                bodyDifference += difference;
                bodyWeight++;
            }
        }

        double edgeCount = 0;
        double edgeComparisons = 0;
        double chromaCount = 0;
        for (var y = 0; y < FeatureSize; y++)
        {
            for (var x = 0; x < FeatureSize; x++)
            {
                if (!IsStatusOverlayPixel(x, y))
                    continue;

                var index = (y * FeatureSize) + x;
                if (residualChroma[index] >= ResidualChromaThreshold)
                    chromaCount++;
                if (x > (int)Math.Round(FeatureSize * (18d / 32)))
                {
                    edgeComparisons++;
                    if (Math.Abs(residualMagnitude[index] - residualMagnitude[index - 1]) >= ResidualEdgeThreshold)
                        edgeCount++;
                }
                if (y > 0 && IsStatusOverlayPixel(x, y - 1))
                {
                    edgeComparisons++;
                    if (Math.Abs(residualMagnitude[index] - residualMagnitude[index - FeatureSize]) >= ResidualEdgeThreshold)
                        edgeCount++;
                }
            }
        }

        return new StatusPresenceEvidence(
            bodyWeight <= 0 ? 1 : bodyDifference / (bodyWeight * 3 * 255),
            overlayWeight <= 0 ? 0 : Math.Clamp(overlayDifference / (overlayWeight * 3 * 255), 0, 1),
            edgeComparisons <= 0 ? 0 : edgeCount / edgeComparisons,
            overlayWeight <= 0 ? 0 : chromaCount / overlayWeight,
            roi,
            residualMagnitude,
            residualRed,
            residualGreen,
            residualBlue);
    }

    private static bool IsStatusOverlayPixel(int x, int y) =>
        x >= (int)Math.Round(FeatureSize * (18d / 32))
        && y <= (int)Math.Round(FeatureSize * (17d / 32));

    private static bool IsStatusResidualPixel(int x, int y) =>
        x >= (int)Math.Round(FeatureSize * (18d / 32))
        && y <= (int)Math.Round(FeatureSize * (23d / 32));

    private static double MinimumStatusScore(string statusId) =>
        statusId.EndsWith("is6_gild5", StringComparison.Ordinal)
            ? AmbiguousGild5MinimumScore
            : MinimumScore;

    private static bool IsStatusScoreAccepted(StatusMatch status)
        => IsStatusScoreAccepted(
            status.Template.Option.Id,
            status.Score,
            status.RunnerUpScore);

    private static bool IsStatusScoreAccepted(
        string statusId,
        double score,
        double runnerUpScore)
    {
        if (score >= MinimumStatusScore(statusId))
            return true;

        return !statusId.EndsWith("is6_gild5", StringComparison.Ordinal)
            && score >= StrongMarginMinimumScore
            && score - runnerUpScore >= StrongMargin;
    }

    private static bool IsClearlyStatusAbsent(StatusPresenceEvidence? evidence) =>
        evidence is not null
        && (
            evidence.OverlayDifference <= ClearAbsenceMaximumOverlayDifference
            || (
                evidence.OverlayDifference < MinimumOverlayDifference
                && evidence.EdgeDensity <= ClearAbsenceMaximumEdgeDensity
                && evidence.ChromaDensity <= ClearAbsenceMaximumChromaDensity
            )
        );

    private static bool ShouldUseBroadStatusSearch(StatusPresenceEvidence? evidence) =>
        evidence is null
        || evidence.ChromaDensity >= 0.04
        || (
            evidence.OverlayDifference >= 0.04
            && evidence.EdgeDensity >= 0.10
        );

    private static bool NeedsBroadStatusConfirmation(
        StatusMatch? status,
        StatusPresenceEvidence? evidence) =>
        status is not null
        && evidence is not null
        && status.IsConfident
        && HasStructuralStatusEvidence(evidence)
        && status.Score <= ResidualBroadConfirmationMaximumScore
        && status.Score - status.RunnerUpScore
            <= ResidualBroadConfirmationMaximumMargin;

    private static bool IsAcceptedStatus(
        StatusMatch? status,
        StatusPresenceEvidence? evidence,
        bool allowLowChroma)
    {
        if (status is null)
            return false;
        if (evidence is not null)
            return IsResidualStatusPresent(status, evidence, allowLowChroma);
        return IsStatusScoreAccepted(status)
            && status.Score >= status.RunnerUpScore
            && status.IsConfident;
    }

    private static LinearFit FitCoinChannel(
        CoinBaselineTemplate template,
        IReadOnlyList<SKColor> actual,
        int channel)
    {
        double count = 0;
        double expectedSum = 0;
        double actualSum = 0;
        double expectedSquareSum = 0;
        double productSum = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            if (IsStatusResidualPixel(x, y) || expected.Alpha < 160)
                continue;

            var expectedValue = Channel(expected, channel);
            var actualValue = Channel(actual[index], channel);
            count++;
            expectedSum += expectedValue;
            actualSum += actualValue;
            expectedSquareSum += expectedValue * expectedValue;
            productSum += expectedValue * actualValue;
        }
        if (count <= 0)
            return new LinearFit(1, 0);

        var denominator = (count * expectedSquareSum) - (expectedSum * expectedSum);
        var scale = Math.Abs(denominator) < 0.001
            ? 1
            : ((count * productSum) - (expectedSum * actualSum)) / denominator;
        var offset = (actualSum - (scale * expectedSum)) / count;
        return new LinearFit(Math.Clamp(scale, -1, 2), Math.Clamp(offset, -255, 255));
    }

    private static PlaneFit FitBackgroundPlane(
        CoinBaselineTemplate template,
        IReadOnlyList<SKColor> actual,
        int channel)
    {
        double xx = 0;
        double xy = 0;
        double xSum = 0;
        double yy = 0;
        double ySum = 0;
        double count = 0;
        double xv = 0;
        double yv = 0;
        double valueSum = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            if (IsStatusResidualPixel(x, y) || expected.Alpha >= 32)
                continue;

            var value = Channel(actual[index], channel);
            xx += x * x;
            xy += x * y;
            xSum += x;
            yy += y * y;
            ySum += y;
            count++;
            xv += x * value;
            yv += y * value;
            valueSum += value;
        }
        if (count < 3)
            return new PlaneFit(0, 0, count <= 0 ? 0 : valueSum / count);

        var matrix = new[,]
        {
            { xx, xy, xSum, xv },
            { xy, yy, ySum, yv },
            { xSum, ySum, count, valueSum },
        };
        for (var column = 0; column < 3; column++)
        {
            var pivot = Enumerable.Range(column, 3 - column)
                .OrderByDescending(row => Math.Abs(matrix[row, column]))
                .First();
            if (Math.Abs(matrix[pivot, column]) < 0.0001)
                return new PlaneFit(0, 0, valueSum / count);
            if (pivot != column)
            {
                for (var item = column; item < 4; item++)
                    (matrix[column, item], matrix[pivot, item]) = (matrix[pivot, item], matrix[column, item]);
            }
            var divisor = matrix[column, column];
            for (var item = column; item < 4; item++)
                matrix[column, item] /= divisor;
            for (var row = 0; row < 3; row++)
            {
                if (row == column)
                    continue;
                var factor = matrix[row, column];
                for (var item = column; item < 4; item++)
                    matrix[row, item] -= factor * matrix[column, item];
            }
        }
        return new PlaneFit(matrix[0, 3], matrix[1, 3], matrix[2, 3]);
    }

    private static double Channel(BaselinePixel pixel, int channel) => channel switch
    {
        0 => pixel.Red,
        1 => pixel.Green,
        _ => pixel.Blue,
    };

    private static double Channel(SKColor pixel, int channel) => channel switch
    {
        0 => pixel.Red,
        1 => pixel.Green,
        _ => pixel.Blue,
    };

    private static IReadOnlyList<StatusTemplate> BuildStatusTemplates(
        IEnumerable<SukiSpecialEffectOption> options)
    {
        var templates = new List<StatusTemplate>();
        foreach (var option in options.Where(option => !string.IsNullOrWhiteSpace(option.ImagePath)))
        {
            using var source = SKBitmap.Decode(option.ImagePath);
            if (source is null || source.Width <= 0 || source.Height <= 0)
                continue;

            foreach (var width in StatusTemplateWidths)
            {
                foreach (var heightScale in StatusHeightScales(option.Id))
                {
                    var height = Math.Max(
                        1,
                        (int)Math.Round(
                            width
                            * (source.Height / (double)source.Width)
                            * heightScale));
                    using var resized = Resize(source, width, height);
                    var pixels = new List<StatusPixel>();
                    var alphaMask = new byte[width * height];
                    var colorPixels = new StatusColorPixel[width * height];
                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            var color = resized.GetPixel(x, y);
                            var index = (y * width) + x;
                            alphaMask[index] = color.Alpha;
                            colorPixels[index] = new StatusColorPixel(
                                color.Red,
                                color.Green,
                                color.Blue,
                                color.Alpha);
                            if (color.Alpha < 176)
                                continue;

                            var alpha = color.Alpha / 255d;
                            pixels.Add(new StatusPixel(x, y, color.Red, color.Green, color.Blue, alpha * alpha));
                        }
                    }
                    if (pixels.Count >= 24)
                        templates.Add(new StatusTemplate(option, width, height, pixels, alphaMask, colorPixels));
                }
            }
        }
        return templates;
    }

    private static IReadOnlyList<double> StatusHeightScales(string statusId) =>
        statusId.EndsWith("is6_gild5", StringComparison.Ordinal)
            ? [1, 1.4, 1.9]
            : [1];

    private static IReadOnlyDictionary<string, CoinBaselineTemplate> BuildCoinTemplates(
        IEnumerable<SukiSpecialEffectOption> options)
    {
        var templates = new Dictionary<string, CoinBaselineTemplate>(StringComparer.Ordinal);
        foreach (var option in options.Where(option => !string.IsNullOrWhiteSpace(option.ImagePath)))
        {
            using var source = SKBitmap.Decode(option.ImagePath);
            if (source is null || source.Width <= 0 || source.Height <= 0)
                continue;

            using var resized = Resize(source, FeatureSize, FeatureSize);
            var pixels = new List<BaselinePixel>(FeatureSize * FeatureSize);
            for (var y = 0; y < FeatureSize; y++)
            {
                for (var x = 0; x < FeatureSize; x++)
                {
                    var color = resized.GetPixel(x, y);
                    pixels.Add(new BaselinePixel(color.Red, color.Green, color.Blue, color.Alpha));
                }
            }
            templates[option.Id] = new CoinBaselineTemplate(pixels);
        }
        return templates;
    }

    private static double ScreenTextCenterX(MaaRoi box) =>
        CoinListRoiX + ((box.X + (box.Width / 2d)) / CoinListOcrScale);

    private static double ScreenTextCenterY(MaaRoi box) =>
        CoinListRoiY + ((box.Y + (box.Height / 2d)) / CoinListOcrScale);

    private static MaaRoi CoinRoi(double centerX, double centerY) =>
        new(
            Math.Clamp((int)Math.Round(centerX) - 53, 0, BaseWidth - 106),
            Math.Clamp((int)Math.Round(centerY) - 53, 0, BaseHeight - 106),
            106,
            106);

    private static bool IsInside(SKBitmap frame, MaaRoi roi) =>
        roi.X >= 0
        && roi.Y >= 0
        && roi.X + roi.Width <= frame.Width
        && roi.Y + roi.Height <= frame.Height;

    private static SKBitmap NormalizeFrame(SKBitmap source)
    {
        if (source.Width == BaseWidth && source.Height == BaseHeight)
            return source.Copy();
        return Resize(source, BaseWidth, BaseHeight);
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        return source.Resize(
                new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
    }

    private sealed record StatusTemplate(
        SukiSpecialEffectOption Option,
        int Width,
        int Height,
        IReadOnlyList<StatusPixel> Pixels,
        IReadOnlyList<byte> AlphaMask,
        IReadOnlyList<StatusColorPixel> ColorPixels);

    private sealed record StatusColorPixel(
        byte Red,
        byte Green,
        byte Blue,
        byte Alpha);

    private sealed record StatusPixel(
        int X,
        int Y,
        byte Red,
        byte Green,
        byte Blue,
        double Weight);

    private sealed record CoreMoments(
        double Weight,
        double CenterX,
        double CenterY,
        double AspectRatio);

    private sealed record HueVector(
        double Red,
        double Green,
        double Blue);

    private sealed record StatusMatch(
        StatusTemplate Template,
        double Score,
        double RunnerUpScore,
        MaaRoi Roi,
        double DistanceToExpected,
        bool IsConfident,
        double ResidualEdgeScore = 0,
        double ResidualMomentScore = 0,
        double ResidualGlobalAspectScore = 0,
        double ResidualCoverageScore = 0,
        double ResidualAspectRatio = 0,
        string RunnerUpStatusId = "");

    private sealed record StatusCandidate(
        StatusTemplate Template,
        double ColorScore,
        MaaRoi Roi);

    private sealed record AnchoredCoinMatch(
        string CoinId,
        string Label,
        double Confidence,
        int SlotIndex,
        double CenterX,
        double CenterY);

    private sealed record CoinBaselineTemplate(IReadOnlyList<BaselinePixel> Pixels);

    private sealed record BaselinePixel(byte Red, byte Green, byte Blue, byte Alpha);

    private sealed record StatusPresenceEvidence(
        double BodyDifference,
        double OverlayDifference,
        double EdgeDensity,
        double ChromaDensity,
        MaaRoi CoinRoi,
        IReadOnlyList<double> ResidualMagnitude,
        IReadOnlyList<double> ResidualRed,
        IReadOnlyList<double> ResidualGreen,
        IReadOnlyList<double> ResidualBlue);

    private sealed record LinearFit(double Scale, double Offset)
    {
        public double Predict(double value) => Math.Clamp((Scale * value) + Offset, 0, 255);
    }

    private sealed record PlaneFit(double X, double Y, double Offset)
    {
        public double Predict(double x, double y) => Math.Clamp((X * x) + (Y * y) + Offset, 0, 255);
    }
}
