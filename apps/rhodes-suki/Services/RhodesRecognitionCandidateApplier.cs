using System.Globalization;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionCandidateApplier
{
    private const string Is5CampaignId = "is5_sarkaz";

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

    private static SukiCandidateApplySummary Apply(
        JsonObject state,
        IEnumerable<MaaCandidatePreview> candidates,
        DateTimeOffset now,
        bool runStatusOnly)
    {
        var candidateList = candidates.ToList();
        var applied = new List<string>();
        var handledIndexes = runStatusOnly
            ? new HashSet<int>()
            : ApplyIs5SpecialCandidates(state, candidateList, applied);
        var ignored = 0;
        for (var index = 0; index < candidateList.Count; index++)
        {
            if (handledIndexes.Contains(index))
                continue;

            var candidate = candidateList[index];
            if (!ApplyCandidate(state, candidate, applied, runStatusOnly))
            {
                ignored++;
            }
        }

        if (applied.Count > 0)
            state["updatedAt"] = now.UtcDateTime.ToString("O");

        return new SukiCandidateApplySummary(applied.Count, ignored, applied);
    }

    private static bool ApplyCandidate(
        JsonObject state,
        MaaCandidatePreview candidate,
        ICollection<string> applied,
        bool runStatusOnly)
    {
        if (candidate.Kind.Equals("runStatus", StringComparison.OrdinalIgnoreCase))
            return ApplyRunStatusCandidate(state, candidate, applied);

        if (runStatusOnly)
            return false;

        if (candidate.Kind.Equals("operator", StringComparison.OrdinalIgnoreCase))
            return ApplyStringSetCandidate(state, "operators", candidate.OperatorId, candidate.Value, applied, "operator");

        if (candidate.Kind.Equals("relic", StringComparison.OrdinalIgnoreCase))
            return ApplyRelicCandidate(state, candidate, applied);

        return false;
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

    private static bool ApplyRunStatusCandidate(JsonObject state, MaaCandidatePreview candidate, ICollection<string> applied)
    {
        var run = EnsureObject(state, "run");
        var field = candidate.Field.Trim();
        switch (field)
        {
            case "hope":
                return ApplyInt(run, field, candidate.Value, 0, 999, applied);
            case "maxHope":
                return ApplyInt(run, field, candidate.Value, 0, 999, applied);
            case "ingot":
                return ApplyInt(run, field, candidate.Value, 0, 9999, applied);
            case "lifePoints":
                return ApplyInt(run, field, candidate.Value, 0, 999, applied);
            case "shield":
                return ApplyInt(run, field, candidate.Value, 0, 999, applied);
            case "commandLevel":
                return ApplyInt(run, field, candidate.Value, 1, 99, applied);
            case "difficulty":
                return ApplyInt(run, field, candidate.Value, 1, 99, applied);
            case "squadId":
                return ApplyString(run, field, candidate.Value, applied, clearSquad: true);
            case "squadRandomEffectOptionId":
                return ApplyString(run, field, candidate.Value, applied);
            case "idea":
                return ApplyIdea(run, candidate, applied);
            default:
                return false;
        }
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
        campaign["age"] = best.AgeId;

        var handled = new HashSet<int>();
        foreach (var item in valid)
        {
            handled.Add(item.Index);
            applied.Add($"age:{item.AgeId}");
        }
        return handled;
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

        return ApplyStringSetCandidate(state, "relics", candidate.RelicId, candidate.Value, applied, "relic");
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

        var currentCampaignId = JsonString(run, "campaignId");
        if (!string.IsNullOrWhiteSpace(currentCampaignId)
            && !currentCampaignId.Equals(Is5CampaignId, StringComparison.Ordinal))
        {
            return null;
        }

        run["campaignId"] ??= Is5CampaignId;
        var special = EnsureObject(run, "special");
        return EnsureObject(special, Is5CampaignId);
    }

    private static bool CandidateCampaignIsIs5(MaaCandidatePreview candidate)
    {
        return string.IsNullOrWhiteSpace(candidate.CampaignId)
            || candidate.CampaignId.Equals(Is5CampaignId, StringComparison.Ordinal);
    }

    private static string CandidateId(string primaryValue, string fallbackValue)
    {
        var value = string.IsNullOrWhiteSpace(primaryValue) ? fallbackValue : primaryValue;
        return value.Trim();
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
}
