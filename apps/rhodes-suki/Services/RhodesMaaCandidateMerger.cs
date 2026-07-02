using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaCandidateMerger
{
    public static IReadOnlyList<MaaCandidatePreview> Merge(
        IEnumerable<MaaCandidatePreview> primaryCandidates,
        IEnumerable<MaaCandidatePreview> supplementalCandidates)
    {
        var merged = primaryCandidates.ToList();
        var hasPrimaryThought = merged.Any(candidate => IsKind(candidate, "thought"));

        foreach (var candidate in supplementalCandidates)
        {
            if (ShouldAdd(merged, candidate, hasPrimaryThought))
                merged.Add(candidate);
        }

        return merged;
    }

    private static bool ShouldAdd(
        IReadOnlyList<MaaCandidatePreview> existing,
        MaaCandidatePreview candidate,
        bool hasPrimaryThought)
    {
        if (IsKind(candidate, "thought"))
            return !hasPrimaryThought;

        if (IsKind(candidate, "age"))
            return !existing.Any(item => IsKind(item, "age"));

        if (IsKind(candidate, "runStatus"))
        {
            var id = RunStatusKey(candidate);
            return !string.IsNullOrWhiteSpace(id)
                && !existing.Any(item => IsKind(item, "runStatus")
                    && RunStatusKey(item).Equals(id, StringComparison.Ordinal));
        }

        if (IsKind(candidate, "operator"))
        {
            var id = CandidateId(candidate.OperatorId, candidate.Value);
            return !string.IsNullOrWhiteSpace(id)
                && !existing.Any(item => IsKind(item, "operator")
                    && CandidateId(item.OperatorId, item.Value).Equals(id, StringComparison.Ordinal));
        }

        if (IsKind(candidate, "relic"))
        {
            var id = CandidateId(candidate.RelicId, candidate.Value);
            return !string.IsNullOrWhiteSpace(id)
                && !existing.Any(item => IsKind(item, "relic")
                    && CandidateId(item.RelicId, item.Value).Equals(id, StringComparison.Ordinal));
        }

        if (IsKind(candidate, "revelation"))
        {
            var id = RevelationKey(candidate);
            return !string.IsNullOrWhiteSpace(id)
                && !existing.Any(item => IsKind(item, "revelation")
                    && RevelationKey(item).Equals(id, StringComparison.Ordinal));
        }

        if (IsKind(candidate, "coin"))
        {
            var id = CoinKey(candidate);
            return !string.IsNullOrWhiteSpace(id)
                && !existing.Any(item => IsKind(item, "coin")
                    && CoinKey(item).Equals(id, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(candidate.RecognitionKey))
        {
            return !existing.Any(item => item.RecognitionKey.Equals(candidate.RecognitionKey, StringComparison.Ordinal));
        }

        return false;
    }

    private static bool IsKind(MaaCandidatePreview candidate, string kind)
    {
        return candidate.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase);
    }

    private static string CandidateId(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback.Trim() : primary.Trim();
    }

    private static string RunStatusKey(MaaCandidatePreview candidate)
    {
        var field = CandidateId(candidate.Field, candidate.Value);
        if (string.IsNullOrWhiteSpace(field))
            return "";

        return string.Join("\u001f", [candidate.CampaignId.Trim(), field]);
    }

    private static string RevelationKey(MaaCandidatePreview candidate)
    {
        var effectId = CandidateId(candidate.EffectId, candidate.Value);
        if (string.IsNullOrWhiteSpace(effectId))
            return "";

        var fieldId = CandidateId(candidate.FieldId, "revelation");
        return string.Join(
            "\u001f",
            [
                candidate.CampaignId.Trim(),
                fieldId.Equals("revelationBoard", StringComparison.Ordinal)
                    ? "revelation"
                    : fieldId,
                candidate.SlotKind.Trim(),
                effectId,
                candidate.StateId.Trim(),
            ]);
    }

    private static string CoinKey(MaaCandidatePreview candidate)
    {
        var coinId = CandidateId(candidate.CoinId, candidate.Value);
        if (string.IsNullOrWhiteSpace(coinId))
            return "";

        return string.Join(
            "\u001f",
            [
                candidate.CampaignId.Trim(),
                coinId,
                candidate.StatusId.Trim(),
                candidate.Face.Equals("back", StringComparison.OrdinalIgnoreCase) ? "back" : "front",
                Math.Max(1, candidate.Count).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ]);
    }
}
