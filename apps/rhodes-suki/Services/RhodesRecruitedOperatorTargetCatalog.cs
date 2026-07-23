using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecruitedOperatorTargetCatalog
{
    public static IReadOnlyList<SukiOperatorTargetOption> Build(
        IEnumerable<SukiChoiceItem> operators,
        IReadOnlyList<SukiOperatorTargetRef>? operatorTargets,
        IReadOnlyList<string>? legacyOperatorIds = null)
    {
        var selectedTargetKeys = operatorTargets?
            .Where(target => !string.IsNullOrWhiteSpace(target.OperatorId))
            .Select(target => target.TargetKey)
            .ToHashSet(StringComparer.Ordinal);
        var legacyIds = selectedTargetKeys is null
            ? (legacyOperatorIds ?? []).ToHashSet(StringComparer.Ordinal)
            : [];
        var result = new List<SukiOperatorTargetOption>();

        foreach (var item in operators.Where(item => item.IsSelected))
        {
            var count = item.SupportsMultipleCount
                ? Math.Max(1, item.EffectiveSelectionCount)
                : 1;
            for (var instance = 1; instance <= count; instance++)
            {
                var target = new SukiOperatorTargetRef(item.Id, instance);
                var name = count > 1
                    ? $"{item.Name} {instance}人目"
                    : item.Name;
                var isSelected = selectedTargetKeys?.Contains(target.TargetKey)
                    ?? (instance == 1 && legacyIds.Contains(item.Id));
                result.Add(new SukiOperatorTargetOption(
                    item.Id,
                    instance,
                    name,
                    item.ImagePath,
                    isSelected));
            }
        }

        return result;
    }
}
