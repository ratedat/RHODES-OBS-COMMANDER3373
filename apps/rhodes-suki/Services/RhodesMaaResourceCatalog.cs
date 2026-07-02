using System.Text;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaResourceCatalog
{
    private const string InterfaceSource = "interface.json";
    private const string ManualPipelineSource = "resource/base/pipeline/rhodes.json";
    private const string GeneratedPipelineSource = "resource/base/pipeline/rhodes-generated.json";

    private static readonly IReadOnlyDictionary<string, TaskMetadata> ManualTaskMetadata = new Dictionary<string, TaskMetadata>(StringComparer.Ordinal)
    {
        ["RhodesProbe"] = new("Probe", "MAAFramework接続確認用のDirectHitタスクです。", []),
        ["RhodesRunStatusIdeaIcon"] = new("基本情報: 構想アイコン", "構想値の基準点になるアイコンTemplateMatchをMAAで実行します。", ["runStatusFull"]),
        ["RhodesRunStatusIngotIcon"] = new("基本情報: 源石錐アイコン", "源石錐の基準点になるアイコンTemplateMatchをMAAで実行します。", ["runStatusFull"]),
        ["RhodesOperatorCodenameFlag"] = new("オペレーター: CODENAME", "招集カード内のCODENAME目印をMAA TemplateMatchで検出します。", ["operatorsFull"]),
        ["RhodesOperatorNameOcr"] = new("オペレーター: 名前OCR", "招集カード領域をMAA-OCRで読ませます。", ["operatorsFull"]),
        ["RhodesRelicButton"] = new("画面判定: 秘宝ボタン", "マップ下部の秘宝ボタンをMAA TemplateMatchで検出します。", ["relicsFull"]),
        ["RhodesOperatorButton"] = new("画面判定: 隊員ボタン", "マップ下部の隊員ボタンをMAA TemplateMatchで検出します。", ["operatorsFull"]),
        ["RhodesThoughtButton"] = new("画面判定: 思案ボタン", "マップ下部の思案ボタンをMAA TemplateMatchで検出します。", ["is5ThoughtFull"]),
    };

    private static readonly HashSet<string> AbandonedRunIdTokens = new(StringComparer.Ordinal)
    {
        "hope",
        "maxhope",
        "life",
        "lifepoints",
        "shield",
        "command",
        "commandlevel",
    };

    private static readonly IReadOnlyDictionary<string, string> ProfileLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["all"] = "すべて",
        ["runStatusFull"] = "基礎情報",
        ["operatorsFull"] = "オペレーター",
        ["relicsFull"] = "秘宝",
        ["is4RevelationFull"] = "啓示",
        ["is5ThoughtFull"] = "思案",
        ["is5AgeFull"] = "時代",
        ["is6CoinsFull"] = "通宝",
    };

    private static readonly IReadOnlyDictionary<string, int> ProfileOrder = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["runStatusFull"] = 10,
        ["operatorsFull"] = 20,
        ["relicsFull"] = 30,
        ["is4RevelationFull"] = 40,
        ["is5ThoughtFull"] = 50,
        ["is5AgeFull"] = 60,
        ["is6CoinsFull"] = 70,
    };

    public static IReadOnlyList<MaaResourceTaskPreview> DefaultTasks()
    {
        var tasks = new Dictionary<string, MaaResourceTaskPreview>(StringComparer.Ordinal);
        foreach (var task in ManualTasks().Concat(GeneratedTasks()))
        {
            tasks.TryAdd(task.Entry, task);
        }

        return tasks.Values.ToList();
    }

    public static IReadOnlyList<MaaResourceProfilePreview> ProfileGroups(IReadOnlyList<MaaResourceTaskPreview> tasks)
    {
        var interfaceProfiles = InterfaceProfiles();
        var interfaceProfileById = new Dictionary<string, ProfileMetadata>(StringComparer.Ordinal);
        foreach (var profile in interfaceProfiles)
            interfaceProfileById.TryAdd(profile.Id, profile);
        var groups = tasks
            .SelectMany(task => task.ProfileIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id =>
            {
                var source = "";
                var description = "";
                var label = ProfileLabels.TryGetValue(id, out var fallbackLabel) ? fallbackLabel : id;
                if (interfaceProfileById.TryGetValue(id, out var profile))
                {
                    label = profile.Label;
                    description = profile.Description;
                    source = profile.Source;
                }

                return new MaaResourceProfilePreview(
                    id,
                    label,
                    tasks.Count(task => TaskAppliesToProfile(task, id)),
                    description,
                    source);
            })
            .OrderBy(group => interfaceProfileById.TryGetValue(group.Id, out var profile) ? profile.Order : ProfileOrder.TryGetValue(group.Id, out var order) ? order : int.MaxValue)
            .ThenBy(group => group.Label, StringComparer.Ordinal)
            .ToList();

        groups.Insert(0, new MaaResourceProfilePreview("all", ProfileLabels["all"], tasks.Count, "すべてのResource taskを表示します。", "local aggregate"));
        return groups;
    }

    public static bool TaskAppliesToProfile(MaaResourceTaskPreview task, string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || profileId == "all")
            return true;
        return task.ProfileIds?.Contains(profileId, StringComparer.Ordinal) == true;
    }

    private static IReadOnlyList<MaaResourceTaskPreview> ManualTasks()
    {
        return PipelineTasks(ManualPipelineSource, manual: true);
    }

    private static IReadOnlyList<MaaResourceTaskPreview> GeneratedTasks()
    {
        return PipelineTasks(GeneratedPipelineSource, manual: false);
    }

    private static IReadOnlyList<ProfileMetadata> InterfaceProfiles()
    {
        var path = Path.Combine(AppContext.BaseDirectory, InterfaceSource);
        if (!File.Exists(path))
            return [];

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("group", out var groups) || groups.ValueKind != JsonValueKind.Array)
                return [];

            var profiles = new List<ProfileMetadata>();
            var index = 0;
            foreach (var group in groups.EnumerateArray())
            {
                var id = JsonString(group, "name");
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var label = JsonString(group, "label");
                profiles.Add(new ProfileMetadata(
                    id,
                    string.IsNullOrWhiteSpace(label) ? id : label,
                    JsonString(group, "description"),
                    "interface.json group/preset",
                    index++));
            }

            return profiles;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<MaaResourceTaskPreview> PipelineTasks(string relativePath, bool manual)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return [];

        var tasks = new List<MaaResourceTaskPreview>();
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var node in document.RootElement.EnumerateObject())
            {
                if (!IsPublishableEntry(node.Name))
                    continue;

                var attach = node.Value.TryGetProperty("attach", out var attachValue) ? attachValue : default;
                if (manual)
                {
                    var metadata = ManualTaskMetadata.TryGetValue(node.Name, out var value)
                        ? value
                        : new TaskMetadata(node.Name, "RHODES手動定義のMAA Resourceタスクです。", []);
                    var manualRecognition = JsonString(node.Value, "recognition");
                    tasks.Add(new MaaResourceTaskPreview(
                        node.Name,
                        metadata.Label,
                        string.Join(" / ", new[] { metadata.Purpose, manualRecognition }.Where(part => !string.IsNullOrWhiteSpace(part))),
                        metadata.ProfileIds,
                        relativePath));
                    continue;
                }

                var label = JsonString(attach, "label");
                var source = JsonString(attach, "source");
                var id = JsonString(attach, "id");
                var recognition = JsonString(node.Value, "recognition");
                var profileIds = JsonStrings(attach, "profileIds");
                var profileId = JsonString(attach, "profileId");
                if (!string.IsNullOrWhiteSpace(profileId) && !profileIds.Contains(profileId, StringComparer.Ordinal))
                    profileIds = [.. profileIds, profileId];
                var generatedLabel = string.IsNullOrWhiteSpace(label) ? node.Name : label;
                var generatedPurpose = string.Join(
                    " / ",
                    new[] { recognition, source, id }.Where(part => !string.IsNullOrWhiteSpace(part)));

                tasks.Add(new MaaResourceTaskPreview(
                    node.Name,
                    $"生成: {generatedLabel}",
                    string.IsNullOrWhiteSpace(generatedPurpose) ? "生成済みMAA Resourceノードです。" : generatedPurpose,
                    profileIds,
                    source));
            }
        }
        catch
        {
            return [];
        }

        return tasks;
    }

    private static bool IsPublishableEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || entry.EndsWith("Empty", StringComparison.Ordinal))
            return false;
        var normalized = new StringBuilder(entry.Length + 8);
        for (var index = 0; index < entry.Length; index++)
        {
            var character = entry[index];
            if (index > 0 && char.IsUpper(character) && char.IsLower(entry[index - 1]))
                normalized.Append('_');
            normalized.Append(char.ToLowerInvariant(character));
        }
        var tokens = normalized.ToString().Split(['_', '.', '-'], StringSplitOptions.RemoveEmptyEntries);
        return !tokens.Any(token => AbandonedRunIdTokens.Contains(token));
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static IReadOnlyList<string> JsonStrings(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return [];
        if (property.ValueKind == JsonValueKind.String)
            return [property.GetString() ?? ""];
        if (property.ValueKind != JsonValueKind.Array)
            return [];
        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record TaskMetadata(
        string Label,
        string Purpose,
        IReadOnlyList<string> ProfileIds);

    private sealed record ProfileMetadata(
        string Id,
        string Label,
        string Description,
        string Source,
        int Order);
}
