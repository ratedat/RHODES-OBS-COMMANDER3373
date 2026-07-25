using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesSuiCandleBearerManualSelection
{
    private const string CampaignId = "is6_sui";

    public static IReadOnlyList<MaaCandidatePreview> BuildCandidates(IEnumerable<SukiOperatorTargetOption> targets)
    {
        var candidates = new List<MaaCandidatePreview>
        {
            RhodesRecognitionCandidateApplier.CreateNoSuiCandleBearerTargetCandidate(),
        };

        foreach (var target in targets.Where(target => target.IsSelected))
        {
            candidates.Add(new MaaCandidatePreview(
                "sui",
                $"{target.Name} (持燭人・手動選択)",
                target.OperatorId,
                "手動入力",
                1.0,
                OperatorId: target.OperatorId,
                CampaignId: CampaignId,
                RecognitionKey: $"manual:sui:candle-bearer:{target.OperatorId}:{target.InstanceIndex}",
                FieldId: "candleBearer",
                OperatorInstance: target.InstanceIndex));
        }

        return candidates;
    }
}
