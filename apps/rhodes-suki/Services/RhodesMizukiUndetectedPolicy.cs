using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMizukiUndetectedPolicy
{
    public static string? GetPreservationWarning(
        string? profileId,
        IEnumerable<MaaCandidatePreview> candidates)
    {
        var recognized = candidates as MaaCandidatePreview[] ?? candidates.ToArray();
        if (string.Equals(profileId, "is3LightHordeFull", StringComparison.Ordinal)
            && !recognized.Any(candidate =>
                candidate.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && candidate.FieldId.Equals("hordeCalls", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(candidate.EffectId)))
        {
            return "大群の呼び声を検出できなかったため、前回値を保持します。解除は特殊値画面から行ってください。";
        }

        if (string.Equals(profileId, "is3RejectionFull", StringComparison.Ordinal)
            && !recognized.Any(candidate =>
                candidate.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && candidate.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(candidate.EffectId)))
        {
            return "拒絶反応を検出できなかったため、前回値と対象者を保持します。解除は特殊値画面から行ってください。";
        }

        return null;
    }
}
