using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record MaaDynamicTemplateRequest(string Entry, string PayloadJson);

public static class RhodesMaaAmiyaRoleResolver
{
    private const string NameEntryPrefix = "operator.card.name.";
    private const string RoleEntryPrefix = "operator.card.amiya-role.";
    private const string RoleRecognitionKeyPrefix = "maa-local:operator-role:";
    private const int RoleOffsetX = -235;
    private const int RoleOffsetY = -90;
    private const int RoleRoiSize = 64;

    private static readonly string LiteralAmiya = RhodesOperatorOcrNormalizer.Normalize("アーミヤ");
    private static readonly string[] OperatorIds = ["amiya", "amiya3", "amiya2"];
    private static readonly string[] Templates =
    [
        "run/AmiyaRoleCaster.png",
        "run/AmiyaRoleMedic.png",
        "run/AmiyaRoleWarrior.png",
    ];

    public static MaaDynamicTemplateRequest? BuildRequest(
        MaaDynamicOcrRequest nameRequest,
        MaaTaskRunResult nameResult)
    {
        if (!nameRequest.Entry.StartsWith(NameEntryPrefix, StringComparison.Ordinal)
            || !ContainsLiteralAmiya(nameResult))
        {
            return null;
        }

        var suffix = nameRequest.Entry[NameEntryPrefix.Length..];
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        var x = nameRequest.X + RoleOffsetX;
        var y = nameRequest.Y + RoleOffsetY;
        if (x < 0 || y < 0 || x + RoleRoiSize > 1280 || y + RoleRoiSize > 720)
            return null;

        var alternatives = Templates.Select(template => new
        {
            recognition = "TemplateMatch",
            roi = new[] { x, y, RoleRoiSize, RoleRoiSize },
            template,
            threshold = 0.72,
            method = 5,
            order_by = "Score",
        });
        return new MaaDynamicTemplateRequest(
            $"{RoleEntryPrefix}{suffix}",
            JsonSerializer.Serialize(new
            {
                recognition = "Or",
                any_of = alternatives,
            }));
    }

    public static string? ResolveOperatorId(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        int nameResultIndex)
    {
        if (nameResultIndex < 0 || nameResultIndex >= taskResults.Count)
            return null;

        var nameEntry = taskResults[nameResultIndex].Entry;
        if (!nameEntry.StartsWith(NameEntryPrefix, StringComparison.Ordinal))
            return null;

        var roleEntry = $"{RoleEntryPrefix}{nameEntry[NameEntryPrefix.Length..]}";
        for (var index = nameResultIndex + 1; index < taskResults.Count; index++)
        {
            var result = taskResults[index];
            if (result.Entry.StartsWith(NameEntryPrefix, StringComparison.Ordinal))
                break;
            if (!result.Entry.Equals(roleEntry, StringComparison.Ordinal)
                || !result.Succeeded
                || !result.Hit
                || !RhodesMaaCompositeTemplateResult.TryReadFirstHit(result.RecognitionDetailJson, out var hit)
                || hit.Index < 0
                || hit.Index >= OperatorIds.Length)
            {
                continue;
            }

            return OperatorIds[hit.Index];
        }

        return null;
    }

    public static bool ContainsLiteralAmiya(MaaTaskRunResult result) =>
        result.Succeeded
        && result.Hit
        && RhodesMaaOcrDetailRows.FromTaskResults([result])
            .Any(row => RhodesOperatorOcrNormalizer.Normalize(row.Text).Equals(LiteralAmiya, StringComparison.Ordinal));

    public static bool IsAmiyaOperatorId(string? operatorId) =>
        !string.IsNullOrWhiteSpace(operatorId)
        && OperatorIds.Contains(operatorId.Trim(), StringComparer.Ordinal);

    public static string RoleRecognitionKey(string operatorId) =>
        $"{RoleRecognitionKeyPrefix}{operatorId.Trim()}";

    public static bool IsRoleResolvedCandidate(MaaCandidatePreview candidate) =>
        candidate.Kind.Equals("operator", StringComparison.OrdinalIgnoreCase)
        && IsAmiyaOperatorId(candidate.OperatorId)
        && candidate.RecognitionKey.StartsWith(RoleRecognitionKeyPrefix, StringComparison.Ordinal);
}
