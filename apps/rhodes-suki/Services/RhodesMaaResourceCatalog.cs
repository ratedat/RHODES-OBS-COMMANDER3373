using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaResourceCatalog
{
    private const string InterfaceSource = "interface.json";
    private const string ManualPipelineSource = "resource/base/pipeline/rhodes.json";
    private const string GeneratedPipelineSource = "resource/base/pipeline/rhodes-generated.json";

    private static readonly IReadOnlyDictionary<string, string> ProfileLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["all"] = "すべて",
        ["runStatusFull"] = "基礎情報",
        ["operatorsFull"] = "オペレーター",
        ["relicsFull"] = "秘宝",
        ["is4RevelationFull"] = "啓示",
        ["is5ThoughtFull"] = "思案",
        ["is5AgeFull"] = "時代",
        ["is2HallucinationsFull"] = "幻覚",
        ["is2PerformanceFull"] = "演目",
        ["is3KeyFull"] = "源石錐・鍵",
        ["is3LightHordeFull"] = "灯火・大群",
        ["is3RejectionFull"] = "拒絶反応",
        ["is6CoinsFull"] = "通宝",
        ["is6SeasonalHours"] = "歳時",
    };

    private static readonly IReadOnlyDictionary<string, int> ProfileOrder = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["runStatusFull"] = 10,
        ["operatorsFull"] = 20,
        ["relicsFull"] = 30,
        ["is4RevelationFull"] = 40,
        ["is5ThoughtFull"] = 50,
        ["is5AgeFull"] = 60,
        ["is2HallucinationsFull"] = 65,
        ["is2PerformanceFull"] = 66,
        ["is3KeyFull"] = 67,
        ["is3LightHordeFull"] = 68,
        ["is3RejectionFull"] = 69,
        ["is6CoinsFull"] = 70,
        ["is6SeasonalHours"] = 71,
    };

    public static IReadOnlyList<MaaResourceTaskPreview> DefaultTasks()
    {
        var interfaceTaskMetadata = InterfaceTaskMetadataByEntry();
        var tasks = new Dictionary<string, MaaResourceTaskPreview>(StringComparer.Ordinal);
        foreach (var task in ManualTasks(interfaceTaskMetadata).Concat(GeneratedTasks(interfaceTaskMetadata)))
        {
            tasks.TryAdd(task.Entry, task);
        }

        return tasks.Values.ToList();
    }

    public static string LoadRecognitionPayloadJson(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return "";

        foreach (var relativePath in new[] { ManualPipelineSource, GeneratedPipelineSource })
        {
            var payload = LoadRecognitionPayloadJson(relativePath, entry.Trim());
            if (!string.IsNullOrWhiteSpace(payload))
                return payload;
        }

        return "";
    }

    public static int LoadRecognitionScale(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return 1;

        foreach (var relativePath in new[] { ManualPipelineSource, GeneratedPipelineSource })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty(entry.Trim(), out var node)
                    && node.ValueKind == JsonValueKind.Object
                    && node.TryGetProperty("attach", out var attach)
                    && attach.ValueKind == JsonValueKind.Object
                    && attach.TryGetProperty("scale", out var scale)
                    && scale.TryGetInt32(out var value))
                {
                    return Math.Clamp(value, 1, 12);
                }
            }
            catch
            {
                // Invalid resource JSON is reported by ValidateContract; recognition stays unscaled here.
            }
        }

        return 1;
    }

    public static IReadOnlyList<string> LoadCompositeTemplateIds(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return [];

        foreach (var relativePath in new[] { ManualPipelineSource, GeneratedPipelineSource })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (!document.RootElement.TryGetProperty(entry.Trim(), out var node)
                    || node.ValueKind != JsonValueKind.Object
                    || !node.TryGetProperty("attach", out var attach)
                    || attach.ValueKind != JsonValueKind.Object
                    || !attach.TryGetProperty("templateIds", out var templateIds)
                    || templateIds.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                return templateIds.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToArray();
            }
            catch
            {
                // Contract validation reports malformed resources; candidate conversion remains empty.
            }
        }

        return [];
    }

    public static MaaTemplateOcrConfig? LoadTemplateOcrConfig(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return null;

        foreach (var relativePath in new[] { ManualPipelineSource, GeneratedPipelineSource })
        {
            var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (!document.RootElement.TryGetProperty(entry.Trim(), out var node)
                    || node.ValueKind != JsonValueKind.Object
                    || !node.TryGetProperty("attach", out var attach)
                    || attach.ValueKind != JsonValueKind.Object
                    || !attach.TryGetProperty("ocrOffset", out var offset)
                    || offset.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                return new MaaTemplateOcrConfig(
                    JsonString(attach, "idPrefix"),
                    JsonInt(offset, "x"),
                    JsonInt(offset, "y"),
                    JsonInt(offset, "width"),
                    JsonInt(offset, "height"),
                    Math.Clamp(JsonInt(attach, "scale", 1), 1, 12),
                    Math.Clamp(JsonInt(attach, "maxMatches", 1), 1, 64));
            }
            catch
            {
                // Invalid resource JSON is reported by ValidateContract.
            }
        }

        return null;
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
            foreach (var entry in pipelineEntries.Where(RhodesMaaRecognitionPolicy.IsAbandonedRunEntry))
                errors.Add($"pipeline contains abandoned run target {entry}");
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
                else if (!RhodesMaaRecognitionPolicy.IsPublishableEntry(entry))
                    errors.Add($"task {DisplayName(taskName)} publishes private or abandoned pipeline entry {entry}");
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

    public static MaaResourceExecutionPlan BuildExecutionPlan(
        IReadOnlyList<MaaResourceTaskPreview> tasks,
        MaaResourceProfilePreview? profile)
    {
        if (profile is null)
            return ExecutionPlanError("認識プロファイルが選択されていません。", state: MaaResourceExecutionPlan.UnselectedState);
        if (profile.Id == "all")
            return ExecutionPlanError("「すべて」は一覧表示用です。認識実行では具体的なプロファイルを選択してください。", profile, MaaResourceExecutionPlan.DisplayOnlyState);

        if (profile.TaskEntries is { Count: > 0 })
        {
            var taskByEntry = tasks
                .GroupBy(task => task.Entry, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var selected = new List<MaaResourceTaskPreview>();
            var missing = new List<string>();
            foreach (var entry in profile.TaskEntries.Where(entry => !string.IsNullOrWhiteSpace(entry)).Distinct(StringComparer.Ordinal))
            {
                if (taskByEntry.TryGetValue(entry, out var task))
                    selected.Add(task);
                else
                    missing.Add(entry);
            }

            if (missing.Count > 0)
                return ExecutionPlanError($"preset taskがResource catalogに存在しません: {string.Join(", ", missing)}", profile, MaaResourceExecutionPlan.MissingTaskState);
            if (selected.Count == 0)
                return ExecutionPlanError("選択プロファイルのpresetに実行可能なResource taskがありません。", profile, MaaResourceExecutionPlan.EmptyState);

            return new MaaResourceExecutionPlan(
                profile.Id,
                profile.Label,
                string.IsNullOrWhiteSpace(profile.Source) ? "profile preset" : profile.Source,
                profile.TaskEntries,
                selected,
                "",
                MaaResourceExecutionPlan.ReadyState);
        }

        var fallbackTasks = tasks
            .Where(task => TaskAppliesToProfile(task, profile.Id))
            .ToArray();
        return fallbackTasks.Length == 0
            ? ExecutionPlanError("選択プロファイルに実行可能なResource taskがありません。", profile, MaaResourceExecutionPlan.EmptyState)
            : new MaaResourceExecutionPlan(
                profile.Id,
                profile.Label,
                "resource profile ids",
                fallbackTasks.Select(task => task.Entry).ToArray(),
                fallbackTasks,
                "",
                MaaResourceExecutionPlan.ReadyState);
    }

    private static MaaResourceExecutionPlan ExecutionPlanError(
        string error,
        MaaResourceProfilePreview? profile = null,
        string state = MaaResourceExecutionPlan.ErrorState)
    {
        return new MaaResourceExecutionPlan(
            profile?.Id ?? "",
            profile?.Label ?? "",
            profile?.Source ?? "",
            profile?.TaskEntries ?? [],
            [],
            error,
            state);
    }

    private static IReadOnlyList<MaaResourceTaskPreview> ManualTasks(IReadOnlyDictionary<string, TaskMetadata> interfaceTaskMetadata)
    {
        return PipelineTasks(ManualPipelineSource, manual: true, interfaceTaskMetadata);
    }

    private static IReadOnlyList<MaaResourceTaskPreview> GeneratedTasks(IReadOnlyDictionary<string, TaskMetadata> interfaceTaskMetadata)
    {
        return PipelineTasks(GeneratedPipelineSource, manual: false, interfaceTaskMetadata);
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

    private static IReadOnlyDictionary<string, TaskMetadata> InterfaceTaskMetadataByEntry()
    {
        var path = Path.Combine(AppContext.BaseDirectory, InterfaceSource);
        if (!File.Exists(path))
            return new Dictionary<string, TaskMetadata>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("task", out var tasks) || tasks.ValueKind != JsonValueKind.Array)
                return new Dictionary<string, TaskMetadata>(StringComparer.Ordinal);

            var metadataByEntry = new Dictionary<string, TaskMetadata>(StringComparer.Ordinal);
            foreach (var task in tasks.EnumerateArray())
            {
                var entry = JsonString(task, "entry");
                if (string.IsNullOrWhiteSpace(entry))
                    continue;
                var label = JsonString(task, "label");
                metadataByEntry.TryAdd(entry, new TaskMetadata(
                    string.IsNullOrWhiteSpace(label) ? entry : label,
                    JsonString(task, "description"),
                    JsonStrings(task, "group")));
            }

            return metadataByEntry;
        }
        catch
        {
            return new Dictionary<string, TaskMetadata>(StringComparer.Ordinal);
        }
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

    private static IReadOnlyList<MaaResourceTaskPreview> PipelineTasks(
        string relativePath,
        bool manual,
        IReadOnlyDictionary<string, TaskMetadata> interfaceTaskMetadata)
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
                if (!RhodesMaaRecognitionPolicy.IsPublishableEntry(node.Name))
                    continue;

                var attach = node.Value.TryGetProperty("attach", out var attachValue) ? attachValue : default;
                interfaceTaskMetadata.TryGetValue(node.Name, out var interfaceMetadata);
                if (manual)
                {
                    var manualRecognition = JsonString(node.Value, "recognition");
                    var purpose = interfaceMetadata?.Purpose;
                    if (string.IsNullOrWhiteSpace(purpose))
                    {
                        purpose = string.Join(" / ", new[] { "RHODES手動定義のMAA Resourceタスクです。", manualRecognition }
                            .Where(part => !string.IsNullOrWhiteSpace(part)));
                    }
                    tasks.Add(new MaaResourceTaskPreview(
                        node.Name,
                        interfaceMetadata?.Label ?? node.Name,
                        purpose,
                        interfaceMetadata?.ProfileIds ?? [],
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
                    interfaceMetadata?.Label ?? $"生成: {generatedLabel}",
                    string.IsNullOrWhiteSpace(interfaceMetadata?.Purpose)
                        ? string.IsNullOrWhiteSpace(generatedPurpose) ? "生成済みMAA Resourceノードです。" : generatedPurpose
                        : interfaceMetadata.Purpose,
                    interfaceMetadata?.ProfileIds ?? profileIds,
                    source));
            }
        }
        catch
        {
            return [];
        }

        return tasks;
    }

    private static string LoadRecognitionPayloadJson(string relativePath, string entry)
    {
        var path = Path.Combine(AppContext.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
            return "";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(entry, out var node)
                || node.ValueKind != JsonValueKind.Object
                || !RhodesMaaRecognitionPolicy.IsPublishableEntry(entry))
            {
                return "";
            }

            var payload = JsonNode.Parse(node.GetRawText())?.AsObject();
            if (payload is null)
                return "";

            payload.Remove("action");
            payload.Remove("attach");
            return payload.ToJsonString();
        }
        catch
        {
            return "";
        }
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

    private static int JsonInt(JsonElement element, string propertyName, int fallback = 0)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value)
                ? value
                : fallback;
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
