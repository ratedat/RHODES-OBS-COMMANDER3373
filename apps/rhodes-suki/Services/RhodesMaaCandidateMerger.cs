using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaCandidateMerger
{
    public static IReadOnlyList<MaaCandidatePreview> Merge(
        IEnumerable<MaaCandidatePreview> primaryCandidates,
        IEnumerable<MaaCandidatePreview> supplementalCandidates)
    {
        var supplemental = supplementalCandidates.ToArray();
        var resolvedAmiya = supplemental
            .Where(RhodesMaaAmiyaRoleResolver.IsRoleResolvedCandidate)
            .OrderByDescending(candidate => candidate.Confidence ?? 0)
            .FirstOrDefault();
        var primary = primaryCandidates.AsEnumerable();
        if (resolvedAmiya is not null)
        {
            primary = primary.Where(candidate =>
                !IsKind(candidate, "operator")
                || !RhodesMaaAmiyaRoleResolver.IsAmiyaOperatorId(CandidateId(candidate.OperatorId, candidate.Value)));
            supplemental = supplemental
                .Where(candidate =>
                    !IsKind(candidate, "operator")
                    || !RhodesMaaAmiyaRoleResolver.IsAmiyaOperatorId(CandidateId(candidate.OperatorId, candidate.Value))
                    || RhodesMaaAmiyaRoleResolver.IsRoleResolvedCandidate(candidate)
                        && CandidateId(candidate.OperatorId, candidate.Value).Equals(resolvedAmiya.OperatorId, StringComparison.Ordinal))
                .ToArray();
        }
        var localRelics = supplemental
            .Where(candidate => IsKind(candidate, "relic"))
            .ToArray();
        if (localRelics.Length > 0)
        {
            primary = primary.Where(candidate => !HasConflictingLocalRelic(candidate, localRelics));
        }
        var localThoughts = supplemental
            .Where(candidate => IsKind(candidate, "thought"))
            .ToArray();
        var merged = localThoughts.Length == 0
            ? primary.ToList()
            : primary
                .Where(candidate => !IsKind(candidate, "thought"))
                .Concat(localThoughts)
                .ToList();
        var hasPrimaryThought = merged.Any(candidate => IsKind(candidate, "thought"));

        foreach (var candidate in supplemental)
        {
            if (localThoughts.Length > 0 && IsKind(candidate, "thought"))
                continue;

            if (MergeOperatorCountEvidence(merged, candidate))
                continue;

            if (MergeRelicUsageEvidence(merged, candidate))
                continue;

            if (ShouldAdd(merged, candidate, hasPrimaryThought))
                merged.Add(candidate);
        }

        return merged;
    }

    private static bool MergeOperatorCountEvidence(
        IList<MaaCandidatePreview> existing,
        MaaCandidatePreview candidate)
    {
        if (!IsKind(candidate, "operator") || candidate.Count <= 0)
            return false;

        var id = CandidateId(candidate.OperatorId, candidate.Value);
        if (string.IsNullOrWhiteSpace(id))
            return false;

        for (var index = 0; index < existing.Count; index++)
        {
            var current = existing[index];
            if (!IsKind(current, "operator")
                || !CandidateId(current.OperatorId, current.Value).Equals(id, StringComparison.Ordinal))
            {
                continue;
            }

            existing[index] = current with { Count = Math.Max(current.Count, candidate.Count) };
            return true;
        }

        return false;
    }

    private static bool MergeRelicUsageEvidence(
        IList<MaaCandidatePreview> existing,
        MaaCandidatePreview candidate)
    {
        if (!IsKind(candidate, "relic") || string.IsNullOrWhiteSpace(candidate.StateId))
            return false;

        var id = CandidateId(candidate.RelicId, candidate.Value);
        if (string.IsNullOrWhiteSpace(id))
            return false;

        for (var index = 0; index < existing.Count; index++)
        {
            var current = existing[index];
            if (!IsKind(current, "relic")
                || !CandidateId(current.RelicId, current.Value).Equals(id, StringComparison.Ordinal))
            {
                continue;
            }

            existing[index] = current with
            {
                Label = string.IsNullOrWhiteSpace(current.Label) ? candidate.Label : current.Label,
                StateId = candidate.StateId,
            };
            return true;
        }

        return false;
    }

    private static bool HasConflictingLocalRelic(
        MaaCandidatePreview primaryCandidate,
        IReadOnlyList<MaaCandidatePreview> localRelics)
    {
        if (!IsKind(primaryCandidate, "relic"))
            return false;

        var primaryId = CandidateId(primaryCandidate.RelicId, primaryCandidate.Value);
        var evidence = NormalizeEvidence(primaryCandidate.RawText);
        if (string.IsNullOrWhiteSpace(primaryId)
            || string.IsNullOrWhiteSpace(evidence)
            || string.IsNullOrWhiteSpace(primaryCandidate.CampaignId))
        {
            return false;
        }

        return localRelics.Any(localCandidate =>
        {
            var localId = CandidateId(localCandidate.RelicId, localCandidate.Value);
            return !string.IsNullOrWhiteSpace(localId)
                && !localId.Equals(primaryId, StringComparison.Ordinal)
                && localCandidate.CampaignId.Equals(primaryCandidate.CampaignId, StringComparison.Ordinal)
                && NormalizeEvidence(localCandidate.RawText).Equals(evidence, StringComparison.Ordinal);
        });
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

    private static string NormalizeEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return string.Concat(value
            .Normalize(System.Text.NormalizationForm.FormKC)
            .Where(character => !char.IsWhiteSpace(character)));
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
                string.IsNullOrWhiteSpace(candidate.FieldId) ? "coins" : candidate.FieldId.Trim(),
                coinId,
                candidate.StatusId.Trim(),
            ]);
    }
}
