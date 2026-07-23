using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionRetryDecision(bool ShouldRetry, string Reason)
{
    public static RhodesRecognitionRetryDecision NoRetry { get; } = new(false, "十分な信頼度です。");
}

public static class RhodesRecognitionRetryPolicy
{
    private static readonly IReadOnlyDictionary<string, double> RequiredRunFields =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["ingot"] = 0.78,
            ["difficulty"] = 0.78,
            ["squadId"] = 0.72,
            ["idea"] = 0.72,
        };

    public static RhodesRecognitionRetryDecision Evaluate(
        string profileId,
        IEnumerable<MaaTaskRunResult> taskResults,
        IEnumerable<MaaCandidatePreview> candidates,
        string campaignId = "")
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var recognized = candidates as MaaCandidatePreview[] ?? candidates.ToArray();
        if (profileId == "runStatusFull")
        {
            var requiredFields = RequiredRunFields.Where(required =>
                !(required.Key == "difficulty" && RhodesMaaRecognitionPolicy.RequiresManualDifficulty(campaignId))
                && !(required.Key == "idea"
                    && !string.IsNullOrWhiteSpace(campaignId)
                    && !campaignId.Equals("is5_sarkaz", StringComparison.Ordinal)));
            foreach (var required in requiredFields)
            {
                var candidate = recognized.FirstOrDefault(item => item.Field.Equals(required.Key, StringComparison.Ordinal));
                if (candidate is null)
                    return new RhodesRecognitionRetryDecision(true, $"{required.Key}候補がありません。");
                if ((candidate.Confidence ?? 0) < required.Value)
                    return new RhodesRecognitionRetryDecision(true, $"{required.Key}の信頼度が低いです ({(candidate.Confidence ?? 0):0.000})。");
            }
            return RhodesRecognitionRetryDecision.NoRetry;
        }

        if (profileId == "operatorsFull")
        {
            var operatorCandidates = recognized.Where(item => item.Kind == "operator").ToArray();
            var unresolvedOcr = results.Any(result =>
                result.Entry.StartsWith("operator.card.name.", StringComparison.Ordinal)
                && (!result.Succeeded || !result.Hit));
            if (operatorCandidates.Length == 0 || unresolvedOcr)
                return new RhodesRecognitionRetryDecision(true, "未解決のオペレーター名があります。");
            return LowConfidence(operatorCandidates, 0.70, "オペレーター");
        }

        if (profileId == "relicsFull")
        {
            var relicCandidates = recognized.Where(item => item.Kind == "relic").ToArray();
            if (relicCandidates.Length == 0)
                return new RhodesRecognitionRetryDecision(true, "秘宝候補がありません。");
            return LowConfidence(relicCandidates, 0.70, "秘宝");
        }

        if (profileId == "is5AgeFull")
        {
            var ageCandidates = recognized.Where(item => item.Kind == "age").ToArray();
            if (ageCandidates.Length == 0)
                return new RhodesRecognitionRetryDecision(true, "時代候補がありません。");
            return LowConfidence(ageCandidates, 0.75, "時代");
        }

        if (profileId == "is5ThoughtFull")
            return LowConfidence(recognized.Where(item => item.Kind == "thought"), 0.68, "思案");

        if (profileId == "is3LightHordeFull")
        {
            var hordeCalls = recognized.Where(item =>
                item.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && item.FieldId.Equals("hordeCalls", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(item.EffectId)).ToArray();
            if (hordeCalls.Length == 0)
                return new RhodesRecognitionRetryDecision(true, "大群の呼び声候補がありません。");
            return LowConfidence(hordeCalls, 0.72, "大群の呼び声");
        }

        if (profileId == "is3RejectionFull")
        {
            var reactions = recognized.Where(item =>
                item.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && item.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(item.EffectId)).ToArray();
            if (reactions.Length == 0)
                return new RhodesRecognitionRetryDecision(true, "拒絶反応候補がありません。");
            return LowConfidence(reactions, 0.72, "拒絶反応");
        }

        return RhodesRecognitionRetryDecision.NoRetry;
    }

    private static RhodesRecognitionRetryDecision LowConfidence(
        IEnumerable<MaaCandidatePreview> candidates,
        double threshold,
        string label)
    {
        var low = candidates.FirstOrDefault(candidate => (candidate.Confidence ?? 0) < threshold);
        return low is null
            ? RhodesRecognitionRetryDecision.NoRetry
            : new RhodesRecognitionRetryDecision(true, $"{label}候補の信頼度が低いです ({(low.Confidence ?? 0):0.000})。");
    }
}
