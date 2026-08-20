using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSuiSeasonalHourTemporalSelection(
    int FrameIndex,
    IReadOnlyList<string> StableEffectIds,
    int ConflictingGroupCount,
    double SelectedConfidence,
    int ReadableFrameCount)
{
    public int StableSeasonalHourCount => StableEffectIds.Count;

    public bool IsBestEffort => FrameIndex >= 0 && StableSeasonalHourCount == 0;

    public bool IsAmbiguous => FrameIndex < 0 && ReadableFrameCount > 1;

    public string Summary => FrameIndex < 0
        ? IsAmbiguous
            ? $"歳時候補が{ReadableFrameCount}Frameで一致しません（揺れ={ConflictingGroupCount}組）。"
            : "歳時を読めるFrameがありません。"
        : IsBestEffort
            ? $"判読できた歳時Frame {FrameIndex + 1}を補助採用 / 時間一致は未確認"
            : $"歳時Frame {FrameIndex + 1}を採用 / 安定={StableSeasonalHourCount}件 / 揺れ={ConflictingGroupCount}組";
}

public static class RhodesSuiSeasonalHourTemporalConsensus
{
    private static readonly HashSet<string> RankSuffixes = new(StringComparer.Ordinal)
    {
        "mourou",
        "meiryou",
        "nyuukotsu",
        "awakening",
    };

    public static RhodesSuiSeasonalHourTemporalSelection SelectBest(
        IReadOnlyList<IReadOnlyList<MaaCandidatePreview>> frames)
    {
        if (frames.Count == 0)
            return new RhodesSuiSeasonalHourTemporalSelection(-1, [], 0, 0, 0);

        var frameCandidates = frames
            .Select((candidates, frameIndex) => new
            {
                FrameIndex = frameIndex,
                Candidates = candidates
                    .Where(IsSeasonalHour)
                    .GroupBy(candidate => candidate.EffectId, StringComparer.Ordinal)
                    .Select(group => group.OrderByDescending(candidate => candidate.Confidence ?? 0).First())
                    .ToArray(),
            })
            .ToArray();
        var allEvidence = frameCandidates
            .SelectMany(frame => frame.Candidates.Select(candidate => new
            {
                frame.FrameIndex,
                Candidate = candidate,
                ParentId = ParentEffectId(candidate.EffectId),
            }))
            .ToArray();

        var stableEffectIds = new List<string>();
        var conflictingGroupCount = 0;
        foreach (var parentGroup in allEvidence.GroupBy(evidence => evidence.ParentId, StringComparer.Ordinal))
        {
            var variants = parentGroup
                .GroupBy(evidence => evidence.Candidate.EffectId, StringComparer.Ordinal)
                .Select(group => new
                {
                    EffectId = group.Key,
                    FrameCount = group.Select(evidence => evidence.FrameIndex).Distinct().Count(),
                    Confidence = group.Max(evidence => evidence.Candidate.Confidence ?? 0),
                })
                .OrderByDescending(variant => variant.FrameCount)
                .ThenByDescending(variant => variant.Confidence)
                .ToArray();
            if (variants.Length > 1)
                conflictingGroupCount++;
            if (variants.Length == 0 || variants[0].FrameCount < 2)
                continue;
            if (variants.Length > 1 && variants[0].FrameCount == variants[1].FrameCount)
                continue;
            stableEffectIds.Add(variants[0].EffectId);
        }

        var stableSet = stableEffectIds.ToHashSet(StringComparer.Ordinal);
        var readableFrames = frameCandidates
            .Where(frame => frame.Candidates.Length > 0)
            .ToArray();
        if (stableSet.Count == 0)
        {
            var bestEffortConfidence = readableFrames
                .SelectMany(frame => frame.Candidates)
                .Select(candidate => candidate.Confidence ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            if (readableFrames.Length != 1)
            {
                return new RhodesSuiSeasonalHourTemporalSelection(
                    -1,
                    [],
                    conflictingGroupCount,
                    bestEffortConfidence,
                    readableFrames.Length);
            }

            return new RhodesSuiSeasonalHourTemporalSelection(
                readableFrames[0].FrameIndex,
                [],
                conflictingGroupCount,
                bestEffortConfidence,
                readableFrames.Length);
        }

        var selected = frameCandidates
            .Select(frame => new
            {
                frame.FrameIndex,
                StableCount = frame.Candidates.Count(candidate => stableSet.Contains(candidate.EffectId)),
                StableConfidence = frame.Candidates
                    .Where(candidate => stableSet.Contains(candidate.EffectId))
                    .Sum(candidate => candidate.Confidence ?? 0),
                BestEffortConfidence = frame.Candidates.Select(candidate => candidate.Confidence ?? 0).DefaultIfEmpty(0).Max(),
            })
            .OrderByDescending(frame => frame.StableCount)
            .ThenByDescending(frame => frame.StableConfidence)
            .ThenByDescending(frame => frame.BestEffortConfidence)
            .ThenBy(frame => frame.FrameIndex)
            .First();

        return new RhodesSuiSeasonalHourTemporalSelection(
            selected.FrameIndex,
            stableEffectIds.OrderBy(effectId => effectId, StringComparer.Ordinal).ToArray(),
            conflictingGroupCount,
            Math.Max(selected.StableConfidence, selected.BestEffortConfidence),
            readableFrames.Length);
    }

    public static bool RequiresDetailFallback(IReadOnlyList<MaaCandidatePreview> candidates)
    {
        var seasonalHours = candidates
            .Where(IsSeasonalHour)
            .ToArray();
        if (seasonalHours.Length == 0)
            return true;

        return seasonalHours.Any(candidate =>
            candidate.EffectId.Contains("_is6sst11_", StringComparison.Ordinal)
            && !candidate.EffectId.EndsWith("_awakening", StringComparison.Ordinal));
    }

    private static bool IsSeasonalHour(MaaCandidatePreview candidate) =>
        candidate.FieldId.Equals("seasonalHours", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(candidate.EffectId);

    private static string ParentEffectId(string effectId)
    {
        var separator = effectId.LastIndexOf('_');
        if (separator <= 0 || separator >= effectId.Length - 1)
            return effectId;
        return RankSuffixes.Contains(effectId[(separator + 1)..])
            ? effectId[..separator]
            : effectId;
    }
}
