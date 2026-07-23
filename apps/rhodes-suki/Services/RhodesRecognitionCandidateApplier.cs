using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionCandidateApplier
{
    public const string NoAgeId = "__none__";
    public const string NoMizukiSelectionId = "__none__";
    private const string Is3CampaignId = "is3_mizuki";
    private const string Is4CampaignId = "is4_sami";
    private const string Is5CampaignId = "is5_sarkaz";
    private const string Is6CampaignId = "is6_sui";
    private static readonly HashSet<string> KnownCampaignIds =
    [
        "is2_phantom",
        "is3_mizuki",
        Is4CampaignId,
        Is5CampaignId,
        Is6CampaignId,
    ];

    public static SukiCandidateApplySummary ApplyRunStatus(
        JsonObject state,
        IEnumerable<MaaCandidatePreview> candidates,
        DateTimeOffset now)
    {
        return Apply(state, candidates, now, runStatusOnly: true);
    }

    public static SukiCandidateApplySummary Apply(
        JsonObject state,
        IEnumerable<MaaCandidatePreview> candidates,
        DateTimeOffset now)
    {
        return Apply(state, candidates, now, runStatusOnly: false);
    }

    public static MaaCandidatePreview CreateNoAgeCandidate() => new(
        "age",
        "時代なし",
        NoAgeId,
        "時代未検出",
        1.0,
        CampaignId: Is5CampaignId,
        RecognitionKey: "maa-local:age:none",
        AgeId: NoAgeId);

    public static MaaCandidatePreview CreateNoHallucinationCandidate() => new(
        "runStatus",
        "幻覚なし",
        "",
        "幻覚未検出",
        1.0,
        Field: "hallucinations",
        CampaignId: "is2_phantom",
        RecognitionKey: "maa-local:hallucinations:none");

    public static MaaCandidatePreview CreateNoPerformanceCandidate() => new(
        "runStatus",
        "演目なし",
        "",
        "演目未検出",
        1.0,
        Field: "performanceId",
        CampaignId: "is2_phantom",
        RecognitionKey: "maa-local:performance:none");

    public static MaaCandidatePreview CreateNoMizukiHordeCallCandidate() => new(
        "mizuki",
        "大群の呼び声なし",
        NoMizukiSelectionId,
        "大群の呼び声未検出",
        1.0,
        CampaignId: Is3CampaignId,
        RecognitionKey: "maa-local:mizuki:horde:none",
        FieldId: "hordeCalls",
        EffectId: NoMizukiSelectionId);

    public static MaaCandidatePreview CreateNoMizukiRejectionCandidate() => new(
        "mizuki",
        "拒絶反応なし",
        NoMizukiSelectionId,
        "拒絶反応未検出",
        1.0,
        CampaignId: Is3CampaignId,
        RecognitionKey: "maa-local:mizuki:rejection:none",
        FieldId: "rejectionReaction",
        EffectId: NoMizukiSelectionId);

    public static MaaCandidatePreview CreateNoMizukiRejectionTargetCandidate() => new(
        "mizuki",
        "拒絶反応対象なし",
        NoMizukiSelectionId,
        "拒絶反応対象を空にする",
        1.0,
        CampaignId: Is3CampaignId,
        RecognitionKey: "manual:mizuki:rejection-targets:none",
        FieldId: "rejectionReactionTargets");

    public static MaaCandidatePreview CreateNoMizukiEvolutionTargetCandidate() => new(
        "mizuki",
        "進化対象なし",
        NoMizukiSelectionId,
        "進化対象を空にする",
        1.0,
        CampaignId: Is3CampaignId,
        RecognitionKey: "manual:mizuki:evolution-targets:none",
        FieldId: "operatorEvolutionTargets");

    public static MaaCandidatePreview CreateNoSuiCoinCandidate(string fieldId)
    {
        var normalizedFieldId = fieldId.Equals("activeCoins", StringComparison.Ordinal)
            ? "activeCoins"
            : "coins";
        return new MaaCandidatePreview(
            "coin",
            normalizedFieldId.Equals("activeCoins", StringComparison.Ordinal) ? "有効銭なし" : "保有銭なし",
            "",
            "手動入力",
            1.0,
            CampaignId: Is6CampaignId,
            RecognitionKey: $"manual:sui:{normalizedFieldId}:clear",
            FieldId: normalizedFieldId,
            SlotKind: "clear");
    }

    public static MaaCandidatePreview CreateNoSuiSupportMartialCandidate() => new(
        "sui",
        "支武なし",
        "",
        "手動入力",
        1.0,
        CampaignId: Is6CampaignId,
        RecognitionKey: "manual:sui:supportMartial:clear",
        FieldId: "supportMartial",
        SlotKind: "clear");

    public static MaaCandidatePreview CreateNoSuiSeasonalHourCandidate() => new(
        "sui",
        "歳時なし",
        "",
        "手動入力",
        1.0,
        CampaignId: Is6CampaignId,
        RecognitionKey: "manual:sui:seasonalHours:clear",
        FieldId: "seasonalHours",
        SlotKind: "clear");

    public static MaaCandidatePreview CreateNoSuiSeasonalHourTargetCandidate() => new(
        "sui",
        "戌絵対象職分なし",
        "",
        "手動入力",
        1.0,
        CampaignId: Is6CampaignId,
        RecognitionKey: "manual:sui:seasonalHourTargets:clear",
        FieldId: "seasonalHourTargets",
        SlotKind: "clear");

    private static SukiCandidateApplySummary Apply(
        JsonObject state,
        IEnumerable<MaaCandidatePreview> candidates,
        DateTimeOffset now,
        bool runStatusOnly)
    {
        var pruned = RhodesRunStateStore.PruneAbandonedRunValues(state);
        var normalizedOcrEngine = RhodesRunStateStore.NormalizeOcrEnginePreference(state);
        var prepared = PrepareCandidatesForApply(candidates);
        var candidateList = prepared.Active.Select(item => item.Candidate).ToArray();
        var applied = new List<string>();
        var outcomes = new List<SukiCandidateApplyOutcome>();
        var handledIndexes = ApplyCampaignCandidates(state, candidateList, applied);
        if (!runStatusOnly)
            handledIndexes.UnionWith(ApplyIs3SpecialCandidates(state, candidateList, applied));
        if (!runStatusOnly)
            handledIndexes.UnionWith(ApplyIs5SpecialCandidates(state, candidateList, applied));
        if (!runStatusOnly)
            handledIndexes.UnionWith(ApplyIs6CoinCandidates(state, candidateList, applied));
        if (!runStatusOnly)
            handledIndexes.UnionWith(ApplyIs6SeasonalHourCandidates(state, candidateList, applied));
        if (!runStatusOnly)
            handledIndexes.UnionWith(ApplyIs6SupportMartialCandidates(state, candidateList, applied));
        foreach (var index in handledIndexes.OrderBy(value => value))
            outcomes.Add(Outcome(prepared.Active[index].Index, candidateList[index], "applied", AppliedFieldForCandidate(candidateList[index]), ""));
        var ignored = prepared.IgnoredDuplicates.Count;
        foreach (var duplicate in prepared.IgnoredDuplicates)
            outcomes.Add(Outcome(duplicate.Index, duplicate.Candidate, "ignored", "", "lower-confidence-duplicate"));
        for (var index = 0; index < candidateList.Length; index++)
        {
            if (handledIndexes.Contains(index))
                continue;

            var candidate = candidateList[index];
            if (ApplyCandidate(state, candidate, applied, runStatusOnly))
            {
                outcomes.Add(Outcome(prepared.Active[index].Index, candidate, "applied", AppliedFieldForCandidate(candidate), ""));
            }
            else
            {
                ignored++;
                outcomes.Add(Outcome(prepared.Active[index].Index, candidate, "ignored", "", IgnoredReason(state, candidate, runStatusOnly)));
            }
        }

        if (applied.Count > 0 || pruned || normalizedOcrEngine)
            state["updatedAt"] = now.UtcDateTime.ToString("O");

        return new SukiCandidateApplySummary(
            applied.Count,
            ignored,
            applied,
            outcomes.OrderBy(item => item.Index).ToArray());
    }

    private static SukiCandidateApplyOutcome Outcome(
        int index,
        MaaCandidatePreview candidate,
        string outcome,
        string appliedField,
        string ignoredReason)
    {
        return new SukiCandidateApplyOutcome(
            index,
            candidate.Kind,
            candidate.Label,
            candidate.Value,
            candidate.Identity,
            outcome,
            appliedField,
            ignoredReason);
    }

    private static string AppliedFieldForCandidate(MaaCandidatePreview candidate)
    {
        if (CandidateIsKind(candidate, "runStatus"))
            return string.IsNullOrWhiteSpace(candidate.Field) ? "run" : candidate.Field.Trim();
        if (CandidateIsKind(candidate, "operator"))
            return $"operator:{CandidateId(candidate.OperatorId, candidate.Value)}";
        if (CandidateIsKind(candidate, "relic"))
            return $"relic:{CandidateId(candidate.RelicId, candidate.Value)}";
        if (CandidateIsKind(candidate, "thought"))
            return $"thought:{CandidateId(candidate.ThoughtId, candidate.Value)}";
        if (CandidateIsKind(candidate, "age"))
            return $"age:{CandidateId(candidate.AgeId, candidate.Value)}";
        if (CandidateIsKind(candidate, "mizuki"))
        {
            var fieldId = string.IsNullOrWhiteSpace(candidate.FieldId) ? "special" : candidate.FieldId.Trim();
            var value = CandidateId(
                candidate.OperatorId,
                CandidateId(candidate.EffectId, candidate.Value));
            return $"mizuki:{fieldId}:{value}";
        }
        if (CandidateIsKind(candidate, "revelation"))
            return $"revelation:{CandidateId(candidate.EffectId, candidate.Value)}";
        if (CandidateIsKind(candidate, "coin"))
            return $"coin:{CandidateId(candidate.CoinId, candidate.Value)}";
        if (CandidateIsKind(candidate, "sui"))
        {
            var fieldId = string.IsNullOrWhiteSpace(candidate.FieldId) ? "special" : candidate.FieldId.Trim();
            return $"sui:{fieldId}:{CandidateId(candidate.EffectId, candidate.Value)}";
        }
        return candidate.Identity;
    }

    private static string IgnoredReason(JsonObject state, MaaCandidatePreview candidate, bool runStatusOnly)
    {
        if (runStatusOnly && !CandidateIsKind(candidate, "runStatus"))
            return "run-status-only";
        if (string.IsNullOrWhiteSpace(candidate.Kind))
            return "missing-kind";
        if (CandidateIsKind(candidate, "runStatus"))
            return RunStatusIgnoredReason(state, candidate);
        if (CandidateIsKind(candidate, "operator"))
            return StringSetIgnoredReason(state, "operators", candidate.OperatorId, candidate.Value, "operator");
        if (CandidateIsKind(candidate, "relic"))
            return RelicIgnoredReason(state, candidate);
        if (CandidateIsKind(candidate, "thought") || CandidateIsKind(candidate, "age"))
            return Is5SpecialIgnoredReason(candidate);
        if (CandidateIsKind(candidate, "mizuki"))
            return MizukiSpecialIgnoredReason(state, candidate);
        if (CandidateIsKind(candidate, "revelation"))
            return "not-is4-or-invalid-revelation-slot";
        if (CandidateIsKind(candidate, "coin"))
            return "not-is6-or-missing-coin-id";
        if (CandidateIsKind(candidate, "sui"))
            return "not-is6-or-missing-sui-special-value";
        return "unsupported-kind";
    }

    private static string RunStatusIgnoredReason(JsonObject state, MaaCandidatePreview candidate)
    {
        var field = candidate.Field.Trim();
        if (string.IsNullOrWhiteSpace(field))
            return "missing-run-field";
        if (RhodesMaaRecognitionPolicy.AbandonedRunFields.Contains(field))
            return "abandoned-run-field";

        var run = state["run"] as JsonObject;
        if (run is not null
            && !field.Equals("campaignId", StringComparison.Ordinal)
            && !CandidateCampaignMatchesCurrentRun(run, candidate))
        {
            return "campaign-mismatch";
        }

        return field switch
        {
            "ingot" or "difficulty" or "idea" or "ticket" => int.TryParse(candidate.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? "unsupported-or-invalid-run-field"
                : "invalid-run-value",
            "squadId" or "squadRandomEffectOptionId" => string.IsNullOrWhiteSpace(candidate.Value)
                ? "missing-run-value"
                : "unsupported-or-invalid-run-field",
            "campaignId" => KnownCampaignIds.Contains(CandidateId(candidate.Value, candidate.CampaignId))
                ? "unsupported-or-invalid-run-field"
                : "unknown-campaign-id",
            _ => "unsupported-run-field",
        };
    }

    private static string StringSetIgnoredReason(JsonObject state, string propertyName, string primaryValue, string fallbackValue, string noun)
    {
        var value = CandidateId(primaryValue, fallbackValue);
        if (string.IsNullOrWhiteSpace(value))
            return $"missing-{noun}-id";
        if (StringSetContains(state, propertyName, value))
            return $"duplicate-{noun}";
        return $"unsupported-{noun}";
    }

    private static string RelicIgnoredReason(JsonObject state, MaaCandidatePreview candidate)
    {
        var run = state["run"] as JsonObject;
        var currentCampaignId = run is null ? "" : JsonString(run, "campaignId");
        if (!string.IsNullOrWhiteSpace(candidate.CampaignId)
            && !string.IsNullOrWhiteSpace(currentCampaignId)
            && !candidate.CampaignId.Equals(currentCampaignId, StringComparison.Ordinal))
        {
            return "campaign-mismatch";
        }

        return StringSetIgnoredReason(state, "relics", candidate.RelicId, candidate.Value, "relic");
    }

    private static string Is5SpecialIgnoredReason(MaaCandidatePreview candidate)
    {
        if (!CandidateCampaignIsIs5(candidate))
            return "campaign-mismatch";

        var id = CandidateIsKind(candidate, "thought")
            ? CandidateId(candidate.ThoughtId, candidate.Value)
            : CandidateId(candidate.AgeId, candidate.Value);
        return string.IsNullOrWhiteSpace(id) ? "missing-special-id" : "unsupported-is5-special";
    }

    private static string MizukiSpecialIgnoredReason(JsonObject state, MaaCandidatePreview candidate)
    {
        if (!CandidateCampaignIs(candidate, Is3CampaignId))
            return "campaign-mismatch";

        var run = state["run"] as JsonObject;
        if (run is not null && !CandidateCampaignMatchesCurrentRun(run, candidate))
            return "campaign-mismatch";

        if ((candidate.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                || candidate.FieldId.Equals("operatorEvolution", StringComparison.Ordinal))
            && !string.IsNullOrWhiteSpace(candidate.OperatorId)
            && !StringSetContains(state, "operators", candidate.OperatorId))
        {
            return "operator-not-recruited";
        }

        return candidate.FieldId switch
        {
            "key" or "light" => "invalid-mizuki-number",
            "hordeCalls" => "missing-horde-call-id",
            "rejectionReaction" => "missing-or-conflicting-rejection-data",
            "operatorEvolution" or "operatorEvolutionTargets" => "missing-or-conflicting-evolution-data",
            _ => "unsupported-mizuki-field",
        };
    }

    private static bool StringSetContains(JsonObject state, string propertyName, string value)
    {
        if (state[propertyName] is not JsonArray existing)
            return false;

        foreach (var item in existing)
        {
            if (item is JsonValue existingValue
                && existingValue.TryGetValue<string>(out var text)
                && text.Equals(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static CandidatePreparation PrepareCandidatesForApply(IEnumerable<MaaCandidatePreview> candidates)
    {
        var active = new List<CandidateApplyEntry>();
        var ignoredDuplicates = new List<CandidateApplyEntry>();
        var bestRunStatusByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (candidate, originalIndex) in candidates.Select((candidate, index) => (candidate, index)))
        {
            if (!CandidateIsKind(candidate, "runStatus"))
            {
                active.Add(new CandidateApplyEntry(originalIndex, candidate));
                continue;
            }

            var key = RunStatusApplyKey(candidate);
            if (string.IsNullOrWhiteSpace(key))
            {
                active.Add(new CandidateApplyEntry(originalIndex, candidate));
                continue;
            }

            if (!bestRunStatusByKey.TryGetValue(key, out var existingIndex))
            {
                bestRunStatusByKey[key] = active.Count;
                active.Add(new CandidateApplyEntry(originalIndex, candidate));
                continue;
            }

            var existing = active[existingIndex];
            if ((candidate.Confidence ?? 0) > (existing.Candidate.Confidence ?? 0))
            {
                ignoredDuplicates.Add(existing);
                active[existingIndex] = new CandidateApplyEntry(originalIndex, candidate);
            }
            else
            {
                ignoredDuplicates.Add(new CandidateApplyEntry(originalIndex, candidate));
            }
        }

        return new CandidatePreparation(active, ignoredDuplicates);
    }

    private static HashSet<int> ApplyCampaignCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var handled = new HashSet<int>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!CandidateIsKind(candidate, "runStatus") || !candidate.Field.Equals("campaignId", StringComparison.Ordinal))
                continue;

            if (ApplyCampaignContextCandidate(state, candidate, applied))
                handled.Add(index);
        }
        return handled;
    }

    private static bool ApplyCandidate(
        JsonObject state,
        MaaCandidatePreview candidate,
        ICollection<string> applied,
        bool runStatusOnly)
    {
        if (CandidateIsKind(candidate, "runStatus"))
            return ApplyRunStatusCandidate(state, candidate, applied);

        if (runStatusOnly)
            return false;

        if (CandidateIsKind(candidate, "operator"))
            return ApplyOperatorCandidate(state, candidate, applied);

        if (CandidateIsKind(candidate, "relic"))
            return ApplyRelicCandidate(state, candidate, applied);

        if (CandidateIsKind(candidate, "revelation"))
            return ApplyRevelationCandidate(state, candidate, applied);

        if (CandidateIsKind(candidate, "coin"))
            return ApplyCoinCandidate(state, candidate, applied);

        return false;
    }

    private static bool ApplyOperatorCandidate(
        JsonObject state,
        MaaCandidatePreview candidate,
        ICollection<string> applied)
    {
        var operatorId = CandidateId(candidate.OperatorId, candidate.Value);
        if (string.IsNullOrWhiteSpace(operatorId))
            return false;

        if (!RhodesMaaAmiyaRoleResolver.IsRoleResolvedCandidate(candidate))
        {
            var operatorChanged = ApplyStringSetCandidate(state, "operators", operatorId, string.Empty, applied, "operator");
            var countChanged = ApplyReserveOperatorCount(state, operatorId, candidate.Count, applied);
            return operatorChanged || countChanged;
        }

        var changed = false;
        var values = new HashSet<string>(StringComparer.Ordinal);
        var array = new JsonArray();
        if (state["operators"] is JsonArray existing)
        {
            foreach (var item in existing)
            {
                var text = item?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (RhodesMaaAmiyaRoleResolver.IsAmiyaOperatorId(text)
                    && !text.Equals(operatorId, StringComparison.Ordinal))
                {
                    changed = true;
                    continue;
                }

                if (!values.Add(text))
                {
                    changed = true;
                    continue;
                }

                array.Add(text);
            }
        }

        if (values.Add(operatorId))
        {
            array.Add(operatorId);
            changed = true;
        }

        if (!changed)
            return false;

        state["operators"] = array;
        applied.Add($"operator:{operatorId}");
        return true;
    }

    private static bool ApplyReserveOperatorCount(
        JsonObject state,
        string operatorId,
        int count,
        ICollection<string> applied)
    {
        if (!operatorId.StartsWith("reserve_", StringComparison.OrdinalIgnoreCase) || count <= 0)
            return false;

        var counts = EnsureObject(state, "operatorCounts");
        if (count <= 1)
        {
            if (!counts.Remove(operatorId))
                return false;

            applied.Add($"operator-count:{operatorId}:1");
            return true;
        }

        if (counts[operatorId]?.GetValue<int>() == count)
            return false;

        counts[operatorId] = Math.Clamp(count, 2, 99);
        applied.Add($"operator-count:{operatorId}:{Math.Clamp(count, 2, 99)}");
        return true;
    }

    private static HashSet<int> ApplyIs5SpecialCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var handled = new HashSet<int>();
        handled.UnionWith(ApplyThoughtCandidates(state, candidates, applied));
        handled.UnionWith(ApplyAgeCandidates(state, candidates, applied));
        return handled;
    }

    private static HashSet<int> ApplyIs3SpecialCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        if (!JsonString(run, "campaignId").Equals(Is3CampaignId, StringComparison.Ordinal))
            return [];

        var handled = new HashSet<int>();
        var campaign = EnsureCampaignSpecialFromRun(run, Is3CampaignId);
        if (campaign is null)
            return handled;

        foreach (var field in new[] { "key", "light" })
        {
            var best = candidates
                .Select((candidate, index) => (Candidate: candidate, Index: index))
                .Where(item => CandidateIsKind(item.Candidate, "mizuki")
                    && item.Candidate.FieldId.Equals(field, StringComparison.Ordinal)
                    && CandidateCampaignIs(item.Candidate, Is3CampaignId))
                .Select(item => (item.Candidate, item.Index, Value: ParseMizukiNumber(item.Candidate.Value, field)))
                .Where(item => item.Value is not null)
                .OrderByDescending(item => item.Candidate.Confidence ?? 0)
                .ThenBy(item => item.Index)
                .FirstOrDefault();
            if (best.Value is null)
                continue;

            campaign[field] = best.Value.Value;
            handled.Add(best.Index);
            applied.Add($"mizuki:{field}:{best.Value.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        var horde = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index, EffectId: CandidateId(candidate.EffectId, candidate.Value)))
            .Where(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("hordeCalls", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && !string.IsNullOrWhiteSpace(item.EffectId))
            .ToArray();
        if (horde.Length > 0)
        {
            var hordeCalls = new JsonArray();
            foreach (var effectId in horde.Select(item => item.EffectId)
                .Where(effectId => !effectId.Equals(NoMizukiSelectionId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal))
            {
                hordeCalls.Add(effectId);
            }
            campaign["hordeCalls"] = hordeCalls;
            foreach (var item in horde)
            {
                handled.Add(item.Index);
                applied.Add($"mizuki:hordeCalls:{item.EffectId}");
            }
        }

        var recognizedOperatorIds = candidates
            .Where(candidate => CandidateIsKind(candidate, "operator"))
            .Select(candidate => CandidateId(candidate.OperatorId, candidate.Value))
            .Where(operatorId => !string.IsNullOrWhiteSpace(operatorId))
            .ToHashSet(StringComparer.Ordinal);
        var recognizedOperatorCounts = candidates
            .Where(candidate => CandidateIsKind(candidate, "operator"))
            .Select(candidate => (
                OperatorId: CandidateId(candidate.OperatorId, candidate.Value),
                Count: Math.Max(1, candidate.Count)))
            .Where(item => !string.IsNullOrWhiteSpace(item.OperatorId))
            .GroupBy(item => item.OperatorId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(item => item.Count),
                StringComparer.Ordinal);
        var targetRows = candidates
            .Select((candidate, index) => (
                Candidate: candidate,
                Index: index,
                OperatorId: candidate.OperatorId.Trim(),
                OperatorInstance: Math.Max(1, candidate.OperatorInstance)))
            .Where(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && !string.IsNullOrWhiteSpace(item.OperatorId)
                && (StringSetContains(state, "operators", item.OperatorId)
                    || recognizedOperatorIds.Contains(item.OperatorId))
                && item.OperatorInstance <= Math.Max(
                    RecruitedOperatorCount(state, item.OperatorId),
                    recognizedOperatorCounts.GetValueOrDefault(item.OperatorId, 0)))
            .DistinctBy(
                item => $"{item.OperatorId}#{item.OperatorInstance}",
                StringComparer.Ordinal)
            .ToArray();
        var clearTargetRow = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .FirstOrDefault(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("rejectionReactionTargets", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && item.Candidate.Value.Equals(NoMizukiSelectionId, StringComparison.Ordinal));

        var evolutionTargetRows = candidates
            .Select((candidate, index) => (
                Candidate: candidate,
                Index: index,
                OperatorId: candidate.OperatorId.Trim(),
                OperatorInstance: Math.Max(1, candidate.OperatorInstance)))
            .Where(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("operatorEvolution", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && !string.IsNullOrWhiteSpace(item.OperatorId)
                && (StringSetContains(state, "operators", item.OperatorId)
                    || recognizedOperatorIds.Contains(item.OperatorId))
                && item.OperatorInstance <= Math.Max(
                    RecruitedOperatorCount(state, item.OperatorId),
                    recognizedOperatorCounts.GetValueOrDefault(item.OperatorId, 0)))
            .DistinctBy(
                item => $"{item.OperatorId}#{item.OperatorInstance}",
                StringComparer.Ordinal)
            .ToArray();
        var clearEvolutionTargetRow = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .FirstOrDefault(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("operatorEvolutionTargets", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && item.Candidate.Value.Equals(NoMizukiSelectionId, StringComparison.Ordinal));
        if (evolutionTargetRows.Length > 0)
        {
            var operatorIds = new JsonArray();
            var operatorTargets = new JsonArray();
            foreach (var operatorId in evolutionTargetRows.Select(item => item.OperatorId).Distinct(StringComparer.Ordinal))
                operatorIds.Add(operatorId);
            foreach (var target in evolutionTargetRows)
            {
                operatorTargets.Add(new JsonObject
                {
                    ["operatorId"] = target.OperatorId,
                    ["instance"] = target.OperatorInstance,
                });
                handled.Add(target.Index);
                applied.Add($"mizuki:operatorEvolution:operator:{target.OperatorId}#{target.OperatorInstance}");
            }
            campaign["operatorEvolution"] = new JsonObject
            {
                ["operatorIds"] = operatorIds,
                ["operatorTargets"] = operatorTargets,
            };
            if (clearEvolutionTargetRow.Candidate is not null)
                handled.Add(clearEvolutionTargetRow.Index);
        }
        else if (clearEvolutionTargetRow.Candidate is not null)
        {
            campaign.Remove("operatorEvolution");
            handled.Add(clearEvolutionTargetRow.Index);
            applied.Add("mizuki:operatorEvolution:none");
        }

        var rejection = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index, EffectId: CandidateId(candidate.EffectId, candidate.Value)))
            .Where(item => CandidateIsKind(item.Candidate, "mizuki")
                && item.Candidate.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                && CandidateCampaignIs(item.Candidate, Is3CampaignId)
                && !string.IsNullOrWhiteSpace(item.Candidate.EffectId)
                && !string.IsNullOrWhiteSpace(item.EffectId))
            .OrderByDescending(item => item.Candidate.Confidence ?? 0)
            .ThenBy(item => item.Index)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(rejection.EffectId))
        {
            if (rejection.EffectId.Equals(NoMizukiSelectionId, StringComparison.Ordinal))
            {
                campaign.Remove("rejectionReaction");
                handled.Add(rejection.Index);
                applied.Add("mizuki:rejectionReaction:none");
                return handled;
            }

            var operatorIds = new JsonArray();
            var operatorTargets = new JsonArray();
            if (targetRows.Length > 0)
            {
                foreach (var operatorId in targetRows.Select(item => item.OperatorId).Distinct(StringComparer.Ordinal))
                    operatorIds.Add(operatorId);
                foreach (var target in targetRows)
                {
                    operatorTargets.Add(new JsonObject
                    {
                        ["operatorId"] = target.OperatorId,
                        ["instance"] = target.OperatorInstance,
                    });
                }
            }
            else if (clearTargetRow.Candidate is null
                && campaign["rejectionReaction"] is JsonObject existingRejection
                && JsonString(existingRejection, "effectId").Equals(rejection.EffectId, StringComparison.Ordinal)
                && existingRejection["operatorIds"] is JsonArray existingOperatorIds)
            {
                foreach (var operatorId in existingOperatorIds
                    .Select(item => item?.GetValue<string>() ?? "")
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal))
                {
                    operatorIds.Add(operatorId);
                }
                if (existingRejection["operatorTargets"] is JsonArray existingOperatorTargets)
                {
                    foreach (var target in existingOperatorTargets)
                        operatorTargets.Add(target?.DeepClone());
                }
                else
                {
                    foreach (var operatorId in operatorIds
                        .Select(item => item?.GetValue<string>() ?? "")
                        .Where(item => !string.IsNullOrWhiteSpace(item)))
                    {
                        operatorTargets.Add(new JsonObject
                        {
                            ["operatorId"] = operatorId,
                            ["instance"] = 1,
                        });
                    }
                }
            }
            campaign["rejectionReaction"] = new JsonObject
            {
                ["effectId"] = rejection.EffectId,
                ["operatorIds"] = operatorIds,
                ["operatorTargets"] = operatorTargets,
            };
            handled.Add(rejection.Index);
            applied.Add($"mizuki:rejectionReaction:{rejection.EffectId}");
            foreach (var target in targetRows)
            {
                handled.Add(target.Index);
                applied.Add($"mizuki:rejectionReaction:operator:{target.OperatorId}#{target.OperatorInstance}");
            }
            if (clearTargetRow.Candidate is not null)
            {
                handled.Add(clearTargetRow.Index);
                applied.Add("mizuki:rejectionReaction:operators:none");
            }
        }
        else if ((targetRows.Length > 0 || clearTargetRow.Candidate is not null)
            && campaign["rejectionReaction"] is JsonObject existingRejection
            && !string.IsNullOrWhiteSpace(JsonString(existingRejection, "effectId")))
        {
            var operatorIds = new JsonArray();
            var operatorTargets = new JsonArray();
            foreach (var operatorId in targetRows.Select(item => item.OperatorId).Distinct(StringComparer.Ordinal))
                operatorIds.Add(operatorId);
            foreach (var target in targetRows)
            {
                operatorTargets.Add(new JsonObject
                {
                    ["operatorId"] = target.OperatorId,
                    ["instance"] = target.OperatorInstance,
                });
            }
            existingRejection["operatorIds"] = operatorIds;
            existingRejection["operatorTargets"] = operatorTargets;
            foreach (var target in targetRows)
            {
                handled.Add(target.Index);
                applied.Add($"mizuki:rejectionReaction:operator:{target.OperatorId}#{target.OperatorInstance}");
            }
            if (clearTargetRow.Candidate is not null)
            {
                handled.Add(clearTargetRow.Index);
                applied.Add("mizuki:rejectionReaction:operators:none");
            }
        }

        return handled;
    }

    private static int RecruitedOperatorCount(JsonObject state, string operatorId)
    {
        if (!StringSetContains(state, "operators", operatorId))
            return 0;
        if (state["operatorCounts"] is JsonObject counts
            && counts[operatorId] is JsonValue value
            && value.TryGetValue<int>(out var count))
        {
            return Math.Max(1, count);
        }
        return 1;
    }

    private static HashSet<int> ApplyIs6CoinCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        if (!JsonString(run, "campaignId").Equals(Is6CampaignId, StringComparison.Ordinal))
            return [];

        var campaign = EnsureCampaignSpecialFromRun(run, Is6CampaignId);
        if (campaign is null)
            return [];

        var handled = new HashSet<int>();
        foreach (var fieldId in new[] { "activeCoins", "coins" })
        {
            var clearRows = candidates
                .Select((candidate, index) => (Candidate: candidate, Index: index))
                .Where(item => CandidateIsKind(item.Candidate, "coin")
                    && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                    && item.Candidate.FieldId.Equals(fieldId, StringComparison.Ordinal)
                    && item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var rows = candidates
                .Select((candidate, index) => (
                    Candidate: candidate,
                    Index: index,
                    CoinId: CandidateId(candidate.CoinId, candidate.Value),
                    FieldId: string.IsNullOrWhiteSpace(candidate.FieldId) ? "coins" : candidate.FieldId.Trim()))
                .Where(item => CandidateIsKind(item.Candidate, "coin")
                    && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                    && item.FieldId.Equals(fieldId, StringComparison.Ordinal)
                    && !item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(item.CoinId))
                .ToArray();
            if (rows.Length == 0 && clearRows.Length == 0)
                continue;

            var grouped = rows
                .GroupBy(
                    item => CoinEntryKey(item.CoinId, string.IsNullOrWhiteSpace(item.Candidate.StatusId) ? null : item.Candidate.StatusId.Trim()),
                    StringComparer.Ordinal)
                .Select(group => new
                {
                    group.First().CoinId,
                    StatusId = string.IsNullOrWhiteSpace(group.First().Candidate.StatusId) ? null : group.First().Candidate.StatusId.Trim(),
                    Count = Math.Clamp(group.Sum(item => Math.Clamp(item.Candidate.Count <= 0 ? 1 : item.Candidate.Count, 1, 99)), 1, 99),
                })
                .ToArray();

            var entries = new JsonArray();
            foreach (var entry in grouped)
            {
                var node = new JsonObject
                {
                    ["coinId"] = entry.CoinId,
                    ["count"] = entry.Count,
                };
                if (!string.IsNullOrWhiteSpace(entry.StatusId))
                    node["statusId"] = entry.StatusId;
                entries.Add(node);
            }
            campaign[fieldId] = entries;

            foreach (var clearRow in clearRows)
            {
                handled.Add(clearRow.Index);
                applied.Add($"coin:{fieldId}:clear");
            }

            foreach (var row in rows)
            {
                handled.Add(row.Index);
                applied.Add($"coin:{fieldId}:{row.CoinId}");
            }
        }

        return handled;
    }

    private static HashSet<int> ApplyIs6SeasonalHourCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        if (!JsonString(run, "campaignId").Equals(Is6CampaignId, StringComparison.Ordinal))
            return [];

        var campaign = EnsureCampaignSpecialFromRun(run, Is6CampaignId);
        if (campaign is null)
            return [];

        var clearRows = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("seasonalHours", StringComparison.Ordinal)
                && item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = candidates
            .Select((candidate, index) => (
                Candidate: candidate,
                Index: index,
                EffectId: CandidateId(candidate.EffectId, candidate.Value)))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("seasonalHours", StringComparison.Ordinal)
                && !item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.EffectId))
            .ToArray();
        var targetClearRows = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("seasonalHourTargets", StringComparison.Ordinal)
                && item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var targetRows = candidates
            .Select((candidate, index) => (
                Candidate: candidate,
                Index: index,
                Target: CandidateId(candidate.EffectId, candidate.Value)))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("seasonalHourTargets", StringComparison.Ordinal)
                && !item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.Target))
            .ToArray();
        if (clearRows.Length == 0 && rows.Length == 0 && targetClearRows.Length == 0 && targetRows.Length == 0)
            return [];

        if (clearRows.Length > 0 || rows.Length > 0)
        {
            var seasonalHours = new JsonArray();
            foreach (var effectId in rows.Select(item => item.EffectId).Distinct(StringComparer.Ordinal))
                seasonalHours.Add(effectId);
            campaign["seasonalHours"] = seasonalHours;
        }

        var recognizedDogPainting = rows.Any(row => row.EffectId.Contains("is6sst11", StringComparison.Ordinal));
        var recognizedTargetedDogPainting = rows.Any(row =>
            row.EffectId.Contains("is6sst11", StringComparison.Ordinal)
            && !row.EffectId.EndsWith("_awakening", StringComparison.Ordinal));
        var shouldReplaceTargets = targetClearRows.Length > 0
            || targetRows.Length > 0
            || ((clearRows.Length > 0 || rows.Length > 0) && !recognizedDogPainting);
        if (shouldReplaceTargets)
        {
            var targets = new JsonArray();
            if (recognizedTargetedDogPainting || targetClearRows.Length > 0)
            {
                foreach (var target in targetRows.Select(item => item.Target).Distinct(StringComparer.Ordinal))
                    targets.Add(target);
            }
            campaign["seasonalHourTargets"] = targets;
        }

        var handled = new HashSet<int>();
        foreach (var clearRow in clearRows)
        {
            handled.Add(clearRow.Index);
            applied.Add("sui:seasonalHours:clear");
        }
        foreach (var row in rows)
        {
            handled.Add(row.Index);
            applied.Add($"sui:seasonalHours:{row.EffectId}");
        }
        foreach (var clearRow in targetClearRows)
        {
            handled.Add(clearRow.Index);
            applied.Add("sui:seasonalHourTargets:clear");
        }
        foreach (var row in targetRows)
        {
            handled.Add(row.Index);
            applied.Add($"sui:seasonalHourTargets:{row.Target}");
        }
        return handled;
    }

    private static HashSet<int> ApplyIs6SupportMartialCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        if (!JsonString(run, "campaignId").Equals(Is6CampaignId, StringComparison.Ordinal))
            return [];

        var campaign = EnsureCampaignSpecialFromRun(run, Is6CampaignId);
        if (campaign is null)
            return [];

        var clearRows = candidates
            .Select((candidate, index) => (Candidate: candidate, Index: index))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("supportMartial", StringComparison.Ordinal)
                && item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var rows = candidates
            .Select((candidate, index) => (
                Candidate: candidate,
                Index: index,
                Value: CandidateId(candidate.EffectId, candidate.Value)))
            .Where(item => CandidateIsKind(item.Candidate, "sui")
                && CandidateCampaignIs(item.Candidate, Is6CampaignId)
                && item.Candidate.FieldId.Equals("supportMartial", StringComparison.Ordinal)
                && !item.Candidate.SlotKind.Equals("clear", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.Value))
            .ToArray();
        if (clearRows.Length == 0 && rows.Length == 0)
            return [];

        var values = new JsonArray();
        foreach (var value in rows.Select(item => item.Value).Distinct(StringComparer.Ordinal))
            values.Add(value);
        campaign["supportMartial"] = values;

        var handled = new HashSet<int>();
        foreach (var clearRow in clearRows)
        {
            handled.Add(clearRow.Index);
            applied.Add("sui:supportMartial:clear");
        }
        foreach (var row in rows)
        {
            handled.Add(row.Index);
            applied.Add($"sui:supportMartial:{row.Value}");
        }
        return handled;
    }

    private static int? ParseMizukiNumber(string value, string field)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return null;
        var maximum = field.Equals("light", StringComparison.Ordinal) ? 100 : 99;
        return number is >= 0 && number <= maximum ? number : null;
    }

    private static bool ApplyRunStatusCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        RhodesRunStateStore.PruneAbandonedRunValuesFromRun(run);
        var field = candidate.Field.Trim();
        if (!field.Equals("campaignId", StringComparison.Ordinal) && !CandidateCampaignMatchesCurrentRun(run, candidate))
            return false;

        switch (field)
        {
            case "ingot":
                return ApplyInt(run, field, candidate.Value, 0, 9999, applied);
            case "difficulty":
                if (!ApplyInt(run, field, candidate.Value, 1, 99, applied))
                    return false;
                ApplyDifficultyTier(run);
                ApplySuiSeasonalHourDifficulty(run);
                return true;
            case "squadId":
                return ApplyString(run, field, candidate.Value, applied, clearSquad: true);
            case "squadRandomEffectOptionId":
                return ApplyString(run, field, candidate.Value, applied);
            case "performanceId":
                return ApplyNullableString(run, field, candidate.Value, applied);
            case "hallucinations":
                return ApplyPhantomHallucinations(run, candidate, applied);
            case "campaignId":
                return ApplyCampaignContextCandidate(state, candidate, applied);
            case "idea":
                return ApplyIdea(run, candidate, applied);
            case "ticket":
                return ApplySuiTicket(run, candidate, applied);
            default:
                return false;
        }
    }

    /// <summary>
    /// 等級は多元化珍品(No.001〜020の効果バリアント)のtierと結びつく。
    /// Web側 app/domain/difficulty.js の applyDifficultyTier と同じ規則で run.difficultyTierId を導出し、
    /// API未接続のローカル反映でも珍品バリアント解決が追従するようにする。
    /// </summary>
    private static void ApplyDifficultyTier(JsonObject run)
    {
        var campaignId = JsonString(run, "campaignId");
        if (!RhodesDifficultyTierCatalog.HasTiers(campaignId))
            return;

        if (run["difficulty"] is JsonValue value && value.TryGetValue<int>(out var difficulty))
            run["difficultyTierId"] = RhodesDifficultyTierCatalog.ResolveTierId(campaignId, difficulty);
    }

    private static void ApplySuiSeasonalHourDifficulty(JsonObject run)
    {
        if (!JsonString(run, "campaignId").Equals("is6_sui", StringComparison.Ordinal)
            || run["difficulty"] is not JsonValue difficultyValue
            || !difficultyValue.TryGetValue<int>(out var difficulty)
            || run["special"] is not JsonObject special
            || special["is6_sui"] is not JsonObject sui
            || sui["seasonalHours"] is not JsonArray seasonalHours)
        {
            return;
        }

        var targetRank = RhodesRunCatalog.ResolveSuiSeasonalHourVariantRank(difficulty);
        var normalized = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in seasonalHours)
        {
            var id = item?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!id.EndsWith("_awakening", StringComparison.Ordinal))
            {
                foreach (var rank in new[] { "mourou", "meiryou", "nyuukotsu" })
                {
                    var suffix = $"_{rank}";
                    if (!id.EndsWith(suffix, StringComparison.Ordinal))
                        continue;
                    id = $"{id[..^suffix.Length]}_{targetRank}";
                    break;
                }
            }

            if (seen.Add(id))
                normalized.Add(id);
        }

        sui["seasonalHours"] = normalized;
    }

    private static bool ApplyInt(JsonObject run, string field, string value, int min, int max, ICollection<string> applied)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return false;

        run[field] = Math.Clamp(number, min, max);
        applied.Add(field);
        return true;
    }

    private static bool ApplyString(JsonObject run, string field, string value, ICollection<string> applied, bool clearSquad = false)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        run[field] = text;
        if (clearSquad)
        {
            run["squad"] = null;
            run["squadRandomEffectOptionId"] = null;
        }
        applied.Add(field);
        return true;
    }

    private static bool ApplyNullableString(JsonObject run, string field, string value, ICollection<string> applied)
    {
        var text = value.Trim();
        if (string.IsNullOrWhiteSpace(text))
            run.Remove(field);
        else
            run[field] = text;
        applied.Add(field);
        return true;
    }

    private static bool ApplyPhantomHallucinations(
        JsonObject run,
        MaaCandidatePreview candidate,
        ICollection<string> applied)
    {
        if (!JsonString(run, "campaignId").Equals("is2_phantom", StringComparison.Ordinal))
            return false;

        var campaign = EnsureObject(EnsureObject(run, "special"), "is2_phantom");
        var values = RhodesHallucinationCatalog.NormalizeRecognizedNames([candidate.Value]);
        if (values.Count == 0)
            campaign.Remove("hallucinations");
        else
            campaign["hallucinations"] = new JsonArray(values.Select(value => JsonValue.Create(value)).ToArray());
        applied.Add("hallucinations");
        return true;
    }

    private static bool ApplyCampaignContextCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        var campaignId = CandidateId(candidate.Value, candidate.CampaignId);
        if (string.IsNullOrWhiteSpace(campaignId) || !KnownCampaignIds.Contains(campaignId))
            return false;

        var run = EnsureObject(state, "run");
        var previousCampaignId = JsonString(run, "campaignId");
        run["campaignId"] = campaignId;
        if (!string.Equals(previousCampaignId, campaignId, StringComparison.Ordinal))
            ResetRunValues(run);

        applied.Add("campaignId");
        return true;
    }

    private static bool ApplyIdea(JsonObject run, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        if (!int.TryParse(candidate.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            return false;

        var campaign = EnsureIs5SpecialFromRun(run, candidate);
        if (campaign is null)
            return false;

        campaign["idea"] = Math.Min(999, value);
        applied.Add("idea");
        return true;
    }

    private static bool ApplySuiTicket(JsonObject run, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        if (!JsonString(run, "campaignId").Equals(Is6CampaignId, StringComparison.Ordinal)
            || !int.TryParse(candidate.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        var campaign = EnsureObject(EnsureObject(run, "special"), Is6CampaignId);
        campaign["ticket"] = Math.Clamp(value, 0, 999);
        applied.Add("ticket");
        return true;
    }

    private static IReadOnlyCollection<int> ApplyThoughtCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var valid = new List<(int Index, string ThoughtId)>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.Kind.Equals("thought", StringComparison.OrdinalIgnoreCase))
                continue;

            var thoughtId = CandidateId(candidate.ThoughtId, candidate.Value);
            if (string.IsNullOrWhiteSpace(thoughtId) || !CandidateCampaignIsIs5(candidate))
                continue;

            valid.Add((index, thoughtId));
        }

        if (valid.Count == 0)
            return [];

        var run = EnsureObject(state, "run");
        var campaign = EnsureIs5SpecialFromRun(run, valid.Select(item => candidates[item.Index]));
        if (campaign is null)
            return [];

        var ids = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in valid)
        {
            if (!counts.ContainsKey(item.ThoughtId))
                ids.Add(item.ThoughtId);
            counts[item.ThoughtId] = counts.GetValueOrDefault(item.ThoughtId) + 1;
        }

        var thought = new JsonArray();
        foreach (var thoughtId in ids)
        {
            thought.Add(new JsonObject
            {
                ["effectId"] = thoughtId,
                ["count"] = counts[thoughtId],
                ["stateId"] = null,
            });
        }
        campaign["thought"] = thought;
        campaign["thoughtOverlayVisible"] = true;

        var handled = new HashSet<int>();
        foreach (var item in valid)
        {
            handled.Add(item.Index);
            applied.Add($"thought:{item.ThoughtId}");
        }
        return handled;
    }

    private static IReadOnlyCollection<int> ApplyAgeCandidates(
        JsonObject state,
        IReadOnlyList<MaaCandidatePreview> candidates,
        ICollection<string> applied)
    {
        var valid = new List<(int Index, MaaCandidatePreview Candidate, string AgeId)>();
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!candidate.Kind.Equals("age", StringComparison.OrdinalIgnoreCase))
                continue;

            var ageId = CandidateId(candidate.AgeId, candidate.Value);
            if (string.IsNullOrWhiteSpace(ageId) || !CandidateCampaignIsIs5(candidate))
                continue;

            valid.Add((index, candidate, ageId));
        }

        if (valid.Count == 0)
            return [];

        var run = EnsureObject(state, "run");
        var campaign = EnsureIs5SpecialFromRun(run, valid.Select(item => item.Candidate));
        if (campaign is null)
            return [];

        var best = valid
            .OrderByDescending(item => item.Candidate.Confidence ?? 0)
            .ThenBy(item => item.Index)
            .First();
        campaign["age"] = best.AgeId.Equals(NoAgeId, StringComparison.Ordinal)
            ? null
            : ResolveAgeVariantId(best.AgeId, RunDifficulty(run));

        var handled = new HashSet<int>();
        foreach (var item in valid)
        {
            handled.Add(item.Index);
            applied.Add($"age:{item.AgeId}");
        }
        return handled;
    }

    private static int RunDifficulty(JsonObject run)
    {
        if (run["difficulty"] is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;
        }
        return 1;
    }

    private static string ResolveAgeVariantId(string ageId, int difficulty)
    {
        var suffix = difficulty >= 12 ? "prime" : difficulty >= 6 ? "expansion" : "formation";
        return Regex.Replace(ageId, "_(formation|expansion|prime)$", $"_{suffix}", RegexOptions.CultureInvariant);
    }

    private static bool ApplyRelicCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        var currentCampaignId = JsonString(run, "campaignId");
        if (!string.IsNullOrWhiteSpace(candidate.CampaignId)
            && !string.IsNullOrWhiteSpace(currentCampaignId)
            && !candidate.CampaignId.Equals(currentCampaignId, StringComparison.Ordinal))
        {
            return false;
        }

        var relicId = CandidateId(candidate.RelicId, candidate.Value);
        var ownedChanged = ApplyStringSetCandidate(state, "relics", relicId, "", applied, "relic");
        var usageChanged = ApplyRelicUsageState(state, candidate, relicId, applied);
        return ownedChanged || usageChanged;
    }

    private static bool ApplyRelicUsageState(
        JsonObject state,
        MaaCandidatePreview candidate,
        string relicId,
        ICollection<string> applied)
    {
        var usageState = candidate.StateId.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(relicId)
            || usageState is not ("used" or "unused")
            || !RhodesRelicUsagePolicy.SupportsUsedFlag(candidate.Label))
        {
            return false;
        }

        var orderedIds = new List<string>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        if (state["usedRelicIds"] is JsonArray existing)
        {
            foreach (var item in existing)
            {
                var value = item?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(value) || !usedIds.Add(value))
                    continue;
                orderedIds.Add(value);
            }
        }

        var changed = usageState == "used"
            ? usedIds.Add(relicId)
            : usedIds.Remove(relicId);
        if (!changed)
            return false;

        if (usageState == "used")
            orderedIds.Add(relicId);
        else
            orderedIds.RemoveAll(id => id.Equals(relicId, StringComparison.Ordinal));

        var array = new JsonArray();
        foreach (var id in orderedIds)
            array.Add(id);
        state["usedRelicIds"] = array;
        applied.Add($"relic-usage:{relicId}:{usageState}");
        return true;
    }

    private static bool ApplyRevelationCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        if (!CandidateCampaignIs(candidate, Is4CampaignId))
            return false;

        var effectId = CandidateId(candidate.EffectId, candidate.Value);
        if (string.IsNullOrWhiteSpace(effectId))
            return false;

        var fieldId = NormalizeRevelationFieldId(candidate.FieldId);
        var slotKind = candidate.SlotKind.Trim().ToLowerInvariant();
        if (slotKind is not ("cause" or "causeid" or "structure" or "structureid" or "rhetoric" or "rhetoricid"))
            return false;

        var run = EnsureObject(state, "run");
        var campaign = EnsureCampaignSpecialFromRun(run, Is4CampaignId);
        if (campaign is null)
            return false;

        var board = EnsureObject(campaign, fieldId);
        if (slotKind is "cause" or "causeid")
        {
            board["causeId"] = effectId;
            applied.Add($"revelation:cause:{effectId}");
            return true;
        }

        if (slotKind is "structure" or "structureid")
        {
            board["structureId"] = effectId;
            applied.Add($"revelation:structure:{effectId}");
            return true;
        }

        board["rhetorics"] = MergeCountedEntries(
            board["rhetorics"] as JsonArray,
            effectId,
            Math.Clamp(candidate.Count <= 0 ? 1 : candidate.Count, 1, 99));
        applied.Add($"revelation:rhetoric:{effectId}");
        return true;
    }

    private static bool ApplyCoinCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        if (!CandidateCampaignIs(candidate, Is6CampaignId))
            return false;

        var coinId = CandidateId(candidate.CoinId, candidate.Value);
        if (string.IsNullOrWhiteSpace(coinId))
            return false;

        var run = EnsureObject(state, "run");
        var campaign = EnsureCampaignSpecialFromRun(run, Is6CampaignId);
        if (campaign is null)
            return false;

        var fieldId = string.IsNullOrWhiteSpace(candidate.FieldId) ? "coins" : candidate.FieldId.Trim();
        var entries = MergeCoinEntries(
            campaign[fieldId] as JsonArray,
            coinId,
            string.IsNullOrWhiteSpace(candidate.StatusId) ? null : candidate.StatusId.Trim(),
            Math.Clamp(candidate.Count <= 0 ? 1 : candidate.Count, 1, 99));
        campaign[fieldId] = entries;
        applied.Add($"coin:{coinId}");
        return true;
    }

    private static bool ApplyStringSetCandidate(
        JsonObject state,
        string propertyName,
        string primaryValue,
        string fallbackValue,
        ICollection<string> applied,
        string appliedPrefix)
    {
        var value = string.IsNullOrWhiteSpace(primaryValue) ? fallbackValue.Trim() : primaryValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var values = new HashSet<string>(StringComparer.Ordinal);
        var array = new JsonArray();
        if (state[propertyName] is JsonArray existing)
        {
            foreach (var item in existing)
            {
                var text = item?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(text) || !values.Add(text))
                    continue;
                array.Add(text);
            }
        }

        if (!values.Add(value))
            return false;

        array.Add(value);
        state[propertyName] = array;
        applied.Add($"{appliedPrefix}:{value}");
        return true;
    }

    private static JsonObject EnsureObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject existing)
            return existing;

        var created = new JsonObject();
        parent[propertyName] = created;
        return created;
    }

    private static JsonObject? EnsureIs5SpecialFromRun(JsonObject run, MaaCandidatePreview candidate)
    {
        return EnsureIs5SpecialFromRun(run, [candidate]);
    }

    private static JsonObject? EnsureIs5SpecialFromRun(JsonObject run, IEnumerable<MaaCandidatePreview> candidates)
    {
        if (candidates.Any(candidate => !CandidateCampaignIsIs5(candidate)))
            return null;

        return EnsureCampaignSpecialFromRun(run, Is5CampaignId);
    }

    private static JsonObject? EnsureCampaignSpecialFromRun(JsonObject run, string campaignId)
    {
        var currentCampaignId = JsonString(run, "campaignId");
        if (!string.IsNullOrWhiteSpace(currentCampaignId)
            && !currentCampaignId.Equals(campaignId, StringComparison.Ordinal))
        {
            return null;
        }

        run["campaignId"] ??= campaignId;
        var special = EnsureObject(run, "special");
        return EnsureObject(special, campaignId);
    }

    private static bool CandidateCampaignIsIs5(MaaCandidatePreview candidate)
    {
        return CandidateCampaignIs(candidate, Is5CampaignId);
    }

    private static bool CandidateCampaignIs(MaaCandidatePreview candidate, string campaignId)
    {
        return string.IsNullOrWhiteSpace(candidate.CampaignId)
            || candidate.CampaignId.Equals(campaignId, StringComparison.Ordinal);
    }

    private static bool CandidateCampaignMatchesCurrentRun(JsonObject run, MaaCandidatePreview candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.CampaignId))
            return true;

        var currentCampaignId = JsonString(run, "campaignId");
        return string.IsNullOrWhiteSpace(currentCampaignId)
            || candidate.CampaignId.Equals(currentCampaignId, StringComparison.Ordinal);
    }

    private static bool CandidateIsKind(MaaCandidatePreview candidate, string kind)
    {
        return candidate.Kind.Equals(kind, StringComparison.OrdinalIgnoreCase);
    }

    private static string CandidateId(string primaryValue, string fallbackValue)
    {
        var value = string.IsNullOrWhiteSpace(primaryValue) ? fallbackValue : primaryValue;
        return value.Trim();
    }

    private static string RunStatusApplyKey(MaaCandidatePreview candidate)
    {
        var field = CandidateId(candidate.Field, candidate.Value);
        if (string.IsNullOrWhiteSpace(field))
            return "";

        return string.Join("\u001f", [candidate.CampaignId.Trim(), field]);
    }

    private static string NormalizeRevelationFieldId(string fieldId)
    {
        var value = fieldId.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Equals("revelationBoard", StringComparison.Ordinal)
            ? "revelation"
            : value;
    }

    private static JsonArray MergeCountedEntries(JsonArray? existing, string effectId, int count)
    {
        var entries = new Dictionary<string, int>(StringComparer.Ordinal);
        if (existing is not null)
        {
            foreach (var item in existing)
            {
                if (item is not JsonObject entry)
                    continue;

                var id = JsonString(entry, "effectId");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var existingCount = JsonInt(entry, "count");
                entries[id] = Math.Clamp(entries.GetValueOrDefault(id) + Math.Max(1, existingCount), 1, 99);
            }
        }

        entries[effectId] = Math.Clamp(entries.GetValueOrDefault(effectId) + count, 1, 99);
        var result = new JsonArray();
        foreach (var entry in entries)
        {
            result.Add(new JsonObject
            {
                ["effectId"] = entry.Key,
                ["count"] = entry.Value,
            });
        }
        return result;
    }

    private static JsonArray MergeCoinEntries(JsonArray? existing, string coinId, string? statusId, int count)
    {
        var entries = new Dictionary<string, (string CoinId, string? StatusId, int Count)>(StringComparer.Ordinal);
        if (existing is not null)
        {
            foreach (var item in existing)
            {
                if (item is not JsonObject entry)
                    continue;

                var id = JsonString(entry, "coinId");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var existingStatusId = JsonString(entry, "statusId");
                var key = CoinEntryKey(id, existingStatusId);
                var existingCount = Math.Max(1, JsonInt(entry, "count"));
                entries[key] = entries.TryGetValue(key, out var current)
                    ? current with { Count = Math.Clamp(current.Count + existingCount, 1, 99) }
                    : (id, string.IsNullOrWhiteSpace(existingStatusId) ? null : existingStatusId, Math.Clamp(existingCount, 1, 99));
            }
        }

        var targetKey = CoinEntryKey(coinId, statusId);
        entries[targetKey] = entries.TryGetValue(targetKey, out var target)
            ? target with { Count = Math.Clamp(target.Count + count, 1, 99) }
            : (coinId, statusId, count);

        var result = new JsonArray();
        foreach (var entry in entries.Values)
        {
            result.Add(new JsonObject
            {
                ["coinId"] = entry.CoinId,
                ["count"] = entry.Count,
                ["statusId"] = entry.StatusId,
            });
        }
        return result;
    }

    private static string CoinEntryKey(string coinId, string? statusId)
    {
        return $"{coinId}\u001f{statusId ?? ""}";
    }

    private static void ResetRunValues(JsonObject run)
    {
        // difficultyTierId は difficulty からの導出値なので、等級と一緒に破棄する。
        foreach (var propertyName in new[]
            {
                "squad",
                "squadId",
                "squadRandomEffectOptionId",
                "performanceId",
                "difficulty",
                "difficultyTierId",
                "ingot",
                "idea",
                "special",
            }
            .Concat(RhodesMaaRecognitionPolicy.AbandonedRunFields))
        {
            run.Remove(propertyName);
        }
    }

    private static int JsonInt(JsonObject parent, string propertyName)
    {
        if (parent.TryGetPropertyValue(propertyName, out var node) && node is JsonValue value
            && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return 0;
    }

    private static string JsonString(JsonObject parent, string propertyName)
    {
        if (parent.TryGetPropertyValue(propertyName, out var node) && node is JsonValue value
            && value.TryGetValue<string>(out var text))
        {
            return text;
        }

        return "";
    }

    private sealed record CandidatePreparation(
        IReadOnlyList<CandidateApplyEntry> Active,
        IReadOnlyList<CandidateApplyEntry> IgnoredDuplicates);

    private sealed record CandidateApplyEntry(int Index, MaaCandidatePreview Candidate);
}
