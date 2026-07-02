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
                interfaceProfileById.TryGetValue(id, out var interfaceProfile);
                if (interfaceProfile is not null)
                {
                    label = interfaceProfile.Label;
                    description = interfaceProfile.Description;
                    source = interfaceProfile.Source;
                }
                var taskEntries = interfaceProfile?.TaskEntries;
                var taskCount = taskEntries is { Count: > 0 }
                    ? tasks.Count(task => taskEntries.Contains(task.Entry, StringComparer.Ordinal))
                    : tasks.Count(task => TaskAppliesToProfile(task, id));

                return new MaaResourceProfilePreview(
                    id,
                    label,
                    taskCount,
                    description,
                    source,
                    taskEntries);
            })
            .OrderBy(group => interfaceProfileById.TryGetValue(group.Id, out var profile) ? profile.Order : ProfileOrder.TryGetValue(group.Id, out var order) ? order : int.MaxValue)
            .ThenBy(group => group.Label, StringComparer.Ordinal)
            .ToList();

        groups.Insert(0, new MaaResourceProfilePreview("all", ProfileLabels["all"], tasks.Count, "すべてのResource taskを表示します。", "local aggregate"));
        return groups;
    }

    public static MaaResourceContractSnapshot ValidateContract()
    {
        var errors = new List<string>();
        var interfacePath = Path.Combine(AppContext.BaseDirectory, InterfaceSource);
        if (!File.Exists(interfacePath))
        {
            errors.Add($"interface.json not found: {interfacePath}");
            return new MaaResourceContractSnapshot(false, 0, 0, 0, errors);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(interfacePath));
            var root = document.RootElement;
            var pipelineEntries = PipelineEntries(errors);
            var controllers = NamedSet(root, "controller");
            var resources = NamedSet(root, "resource");
            var groups = NamedSet(root, "group");
            var tasks = ArrayItems(root, "task");
            var presets = ArrayItems(root, "preset");
            var taskNames = tasks
                .Select(task => JsonString(task, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);

            AddDuplicateErrors(errors, ArrayItems(root, "controller").Select(item => JsonString(item, "name")), "controller");
            AddDuplicateErrors(errors, ArrayItems(root, "resource").Select(item => JsonString(item, "name")), "resource");
            AddDuplicateErrors(errors, ArrayItems(root, "group").Select(item => JsonString(item, "name")), "group");
            AddDuplicateErrors(errors, tasks.Select(task => JsonString(task, "name")), "task name");
            AddDuplicateErrors(errors, tasks.Select(task => JsonString(task, "entry")), "task entry");
            AddDuplicateErrors(errors, presets.Select(preset => JsonString(preset, "name")), "preset");

            foreach (var resource in ArrayItems(root, "resource"))
            {
                var resourceName = JsonString(resource, "name");
                foreach (var controllerName in JsonStrings(resource, "controller"))
                {
                    if (!controllers.Contains(controllerName))
                        errors.Add($"resource {resourceName} references unknown controller {controllerName}");
                }
                foreach (var resourcePath in JsonStrings(resource, "path"))
                {
                    var path = Path.Combine(AppContext.BaseDirectory, resourcePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!Directory.Exists(path))
                        errors.Add($"resource {resourceName} path does not exist: {resourcePath}");
                }
            }

            foreach (var task in tasks)
            {
                var taskName = JsonString(task, "name");
                var entry = JsonString(task, "entry");
                if (string.IsNullOrWhiteSpace(taskName))
                    errors.Add("task is missing name");
                if (string.IsNullOrWhiteSpace(entry))
                    errors.Add($"task {DisplayName(taskName)} is missing entry");
                else if (!pipelineEntries.Contains(entry))
                    errors.Add($"task {DisplayName(taskName)} references unknown pipeline entry {entry}");

                foreach (var controllerName in JsonStrings(task, "controller"))
                {
                    if (!controllers.Contains(controllerName))
                        errors.Add($"task {DisplayName(taskName)} references unknown controller {controllerName}");
                }
                foreach (var resourceName in JsonStrings(task, "resource"))
                {
                    if (!resources.Contains(resourceName))
                        errors.Add($"task {DisplayName(taskName)} references unknown resource {resourceName}");
                }
                foreach (var groupName in JsonStrings(task, "group"))
                {
                    if (!groups.Contains(groupName))
                        errors.Add($"task {DisplayName(taskName)} references unknown group {groupName}");
                }
            }

            foreach (var preset in presets)
            {
                var presetName = JsonString(preset, "name");
                if (!groups.Contains(presetName))
                    errors.Add($"preset {DisplayName(presetName)} has no matching group");
                var presetTaskNames = PresetTaskNames(preset).ToHashSet(StringComparer.Ordinal);
                var groupedTaskNames = tasks
                    .Where(task => JsonStrings(task, "group").Contains(presetName, StringComparer.Ordinal))
                    .Select(task => JsonString(task, "name"))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var taskName in presetTaskNames)
                {
                    if (!taskNames.Contains(taskName))
                        errors.Add($"preset {presetName} references unknown task {taskName}");
                }
                if (groups.Contains(presetName) && !presetTaskNames.SetEquals(groupedTaskNames))
                {
                    var missing = groupedTaskNames.Except(presetTaskNames, StringComparer.Ordinal).ToArray();
                    var extra = presetTaskNames.Except(groupedTaskNames, StringComparer.Ordinal).ToArray();
                    if (missing.Length > 0)
                        errors.Add($"preset {presetName} is missing grouped tasks: {string.Join(", ", missing)}");
                    if (extra.Length > 0)
                        errors.Add($"preset {presetName} contains non-group tasks: {string.Join(", ", extra)}");
                }
            }

            return new MaaResourceContractSnapshot(
                errors.Count == 0,
                tasks.Count,
                groups.Count,
                presets.Count,
                errors);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return new MaaResourceContractSnapshot(false, 0, 0, 0, errors);
        }
    }

    public static bool TaskAppliesToProfile(MaaResourceTaskPreview task, MaaResourceProfilePreview? profile)
    {
        if (profile is null || profile.Id == "all")
            return true;
        return profile.TaskEntries is { Count: > 0 }
            ? profile.TaskEntries.Contains(task.Entry, StringComparer.Ordinal)
            : TaskAppliesToProfile(task, profile.Id);
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
            var taskEntryByName = InterfaceTaskEntryByName(document.RootElement);
            var presetEntriesById = InterfacePresetEntriesById(document.RootElement, taskEntryByName);
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
                    index++,
                    presetEntriesById.TryGetValue(id, out var entries) ? entries : []));
            }

            return profiles;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyDictionary<string, string> InterfaceTaskEntryByName(JsonElement root)
    {
        if (!root.TryGetProperty("task", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var entryByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var task in tasks.EnumerateArray())
        {
            var name = JsonString(task, "name");
            var entry = JsonString(task, "entry");
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(entry))
                entryByName.TryAdd(name, entry);
        }

        return entryByName;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> InterfacePresetEntriesById(
        JsonElement root,
        IReadOnlyDictionary<string, string> taskEntryByName)
    {
        if (!root.TryGetProperty("preset", out var presets) || presets.ValueKind != JsonValueKind.Array)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        var entriesById = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var preset in presets.EnumerateArray())
        {
            var id = JsonString(preset, "name");
            if (string.IsNullOrWhiteSpace(id) || !preset.TryGetProperty("task", out var presetTasks) || presetTasks.ValueKind != JsonValueKind.Array)
                continue;

            var entries = presetTasks.EnumerateArray()
                .Select(task => JsonString(task, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name) && taskEntryByName.ContainsKey(name))
                .Select(name => taskEntryByName[name])
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            entriesById.TryAdd(id, entries);
        }

        return entriesById;
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

    private static HashSet<string> PipelineEntries(ICollection<string> errors)
    {
        var entries = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relativePath in new[] { ManualPipelineSource, GeneratedPipelineSource })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                errors.Add($"pipeline not found: {relativePath}");
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var node in document.RootElement.EnumerateObject())
                    entries.Add(node.Name);
            }
            catch (Exception ex)
            {
                errors.Add($"{relativePath}: {ex.Message}");
            }
        }
        return entries;
    }

    private static IReadOnlyList<JsonElement> ArrayItems(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray().ToArray();
    }

    private static HashSet<string> NamedSet(JsonElement root, string propertyName)
    {
        return ArrayItems(root, propertyName)
            .Select(item => JsonString(item, "name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AddDuplicateErrors(ICollection<string> errors, IEnumerable<string> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            if (!seen.Add(value))
                errors.Add($"duplicate {label}: {value}");
        }
    }

    private static IEnumerable<string> PresetTaskNames(JsonElement preset)
    {
        if (preset.ValueKind != JsonValueKind.Object
            || !preset.TryGetProperty("task", out var tasks)
            || tasks.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var task in tasks.EnumerateArray())
        {
            var name = JsonString(task, "name");
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    private static string DisplayName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<unknown>" : value;
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
        int Order,
        IReadOnlyList<string> TaskEntries);
}
