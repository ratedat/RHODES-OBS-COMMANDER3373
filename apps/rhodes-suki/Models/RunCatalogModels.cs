using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace RhodesSuki.Models;

public sealed record SukiCampaignPreview(
    string Id,
    int Number,
    string Title,
    string FullTitle,
    IReadOnlyList<SukiCampaignSpecialField> SpecialFields)
{
    public string DisplayName => $"IS#{Number} {Title}";
}

public sealed record SukiCampaignSpecialField(
    string Id,
    string Label,
    string Type,
    string EffectSlot,
    string UnitLabel);

public sealed record SukiWorkspaceNavItem(
    string Id,
    string Label,
    string Subtitle,
    string Description);

public sealed record SukiStatusChip(
    string Label,
    string Value,
    string Detail);

public sealed record SukiRunFieldPreview(
    string Label,
    string Value,
    string Source,
    string RecognitionTaskId,
    string Detail);

public sealed record SukiSpecialValuePreview(
    string Label,
    string Value,
    string Kind,
    string ProfileId,
    string Detail);

public sealed record SukiSpecialEffectOption(
    string Id,
    string Name,
    string GroupLabel = "",
    string Effect = "",
    string FlavorText = "",
    string Category = "",
    int Price = 0,
    string ThoughtRank = "",
    string ThoughtLoad = "",
    string ImagePath = "",
    string ParentKey = "",
    string ParentName = "",
    string VariantRank = "",
    string VariantLabel = "")
{
    public string DetailMeta => string.Join(
        " / ",
        new[]
        {
            GroupLabel,
            ThoughtRank,
            ThoughtLoad,
            Price > 0 ? $"消費構想 {Price}" : "",
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public override string ToString() => Name;
}

public sealed class SukiSeasonalHourEditor : INotifyPropertyChanged
{
    private SukiSpecialEffectOption _selectedOption;

    public SukiSeasonalHourEditor(
        string parentKey,
        string parentName,
        IReadOnlyList<SukiSpecialEffectOption> options,
        string selectedId = "")
    {
        ParentKey = parentKey;
        ParentName = parentName;
        Options = options;
        _selectedOption = options.FirstOrDefault(option => option.Id.Equals(selectedId, StringComparison.Ordinal))
            ?? options.First();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ParentKey { get; }
    public string ParentName { get; }
    public IReadOnlyList<SukiSpecialEffectOption> Options { get; }

    public SukiSpecialEffectOption SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (value is null || ReferenceEquals(_selectedOption, value))
                return;
            _selectedOption = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedId)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedEffect)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDogPaintingTargetSelectionVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DogPaintingTargetLimit)));
        }
    }

    public string SelectedId => SelectedOption.Id;
    public string SelectedEffect => SelectedOption.Effect;
    public bool IsDogPaintingTargetSelectionVisible => ParentKey.Equals("is6sst11", StringComparison.Ordinal)
        && DogPaintingTargetLimit > 0;
    public int DogPaintingTargetLimit => SelectedOption.VariantRank switch
    {
        "mourou" => 1,
        "meiryou" => 2,
        "nyuukotsu" => 3,
        _ => 0,
    };
}

public sealed class SukiHallucinationOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public SukiHallucinationOption(
        string id,
        string name,
        string mapLabel,
        string effect,
        string flavorText,
        string category,
        IReadOnlyList<string> aliases,
        bool isSelected = false)
    {
        Id = id;
        Name = name;
        MapLabel = mapLabel;
        Effect = effect;
        FlavorText = flavorText;
        Category = category;
        Aliases = aliases;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Name { get; }
    public string MapLabel { get; }
    public string Effect { get; }
    public string FlavorText { get; }
    public string Category { get; }
    public IReadOnlyList<string> Aliases { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class SukiToggleEffectOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public SukiToggleEffectOption(SukiSpecialEffectOption option, bool isSelected = false)
    {
        Option = option;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public SukiSpecialEffectOption Option { get; }
    public string Id => Option.Id;
    public string Name => Option.Name;
    public string Effect => Option.Effect;
    public string ImagePath => Option.ImagePath;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed class SukiOperatorTargetOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public SukiOperatorTargetOption(
        string operatorId,
        int instanceIndex,
        string name,
        string imagePath,
        bool isSelected = false)
    {
        OperatorId = operatorId;
        InstanceIndex = Math.Max(1, instanceIndex);
        Name = name;
        ImagePath = imagePath;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => OperatorId;
    public string OperatorId { get; }
    public int InstanceIndex { get; }
    public string TargetKey => $"{OperatorId}#{InstanceIndex}";
    public string Name { get; }
    public string ImagePath { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed record SukiOperatorTargetRef(
    string OperatorId,
    int InstanceIndex = 1)
{
    public int NormalizedInstanceIndex => Math.Max(1, InstanceIndex);
    public string TargetKey => $"{OperatorId}#{NormalizedInstanceIndex}";
}

public sealed record SukiHallucinationFusion(
    string Id,
    IReadOnlyList<string> RequiredIds,
    string Name,
    string Effect);

public sealed record SukiHallucinationCatalogSnapshot(
    IReadOnlyList<SukiHallucinationOption> Options,
    IReadOnlyList<SukiHallucinationFusion> Fusions,
    string SourceTitle,
    string SourceUrl,
    string SourceCheckedAt);

public sealed class SukiBossOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public SukiBossOption(
        string field,
        string id,
        string stageName,
        string bossName,
        string optionLabel,
        string requiredNote,
        string imagePath,
        double sortOrder,
        bool isSelected = false)
    {
        Field = field;
        Id = id;
        StageName = stageName;
        BossName = bossName;
        OptionLabel = optionLabel;
        RequiredNote = requiredNote;
        ImagePath = imagePath;
        SortOrder = sortOrder;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Field { get; }

    public string Id { get; }

    public string StageName { get; }

    public string BossName { get; }

    public string OptionLabel { get; }

    public string RequiredNote { get; }

    public string ImagePath { get; }

    public double SortOrder { get; }

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Id))
                return "未選択";
            var name = string.IsNullOrWhiteSpace(BossName) ? StageName : $"{StageName} / {BossName}";
            return string.IsNullOrWhiteSpace(RequiredNote) ? name : $"{name} — {RequiredNote}";
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public override string ToString() => DisplayName;
}

public sealed class SukiBossSectionEditor : INotifyPropertyChanged
{
    private SukiBossOption? _selectedOption;

    public SukiBossSectionEditor(
        string campaignId,
        string id,
        string field,
        string label,
        string helper,
        bool allowsMultiple,
        IReadOnlyList<SukiBossOption> options)
    {
        CampaignId = campaignId;
        Id = id;
        Field = field;
        Label = label;
        Helper = helper;
        AllowsMultiple = allowsMultiple;
        Options = options;
        _selectedOption = allowsMultiple ? null : options.FirstOrDefault(option => option.IsSelected)
            ?? options.FirstOrDefault(option => string.IsNullOrWhiteSpace(option.Id));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string CampaignId { get; }

    public string Id { get; }

    public string Field { get; }

    public string Label { get; }

    public string Helper { get; }

    public bool AllowsMultiple { get; }

    public bool IsSingleSelection => !AllowsMultiple;

    public IReadOnlyList<SukiBossOption> Options { get; }

    public SukiBossOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (AllowsMultiple || ReferenceEquals(_selectedOption, value))
                return;
            _selectedOption = value;
            foreach (var option in Options)
                option.IsSelected = ReferenceEquals(option, value) && !string.IsNullOrWhiteSpace(option.Id);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIds)));
        }
    }

    public IReadOnlyList<string> SelectedIds => Options
        .Where(option => option.IsSelected && !string.IsNullOrWhiteSpace(option.Id))
        .Select(option => option.Id)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public void Toggle(SukiBossOption option)
    {
        if (!AllowsMultiple || !Options.Contains(option))
            return;
        option.IsSelected = !option.IsSelected;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIds)));
    }
}

public sealed class SukiThoughtCountEditor : INotifyPropertyChanged
{
    private int _count;

    public SukiThoughtCountEditor(string id, string name, int count = 0)
        : this(new SukiSpecialEffectOption(id, name), count)
    {
    }

    public SukiThoughtCountEditor(SukiSpecialEffectOption option, int count = 0)
    {
        Option = option;
        _count = Math.Max(0, count);
    }

    public SukiSpecialEffectOption Option { get; }
    public string Id => Option.Id;
    public string Name => Option.Name;
    public string DetailMeta => Option.DetailMeta;
    public string Effect => Option.Effect;
    public string FlavorText => Option.FlavorText;
    public string ImagePath => Option.ImagePath;

    public int Count
    {
        get => _count;
        set
        {
            var normalized = Math.Clamp(value, 0, 99);
            if (_count == normalized)
                return;
            _count = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record SukiCoinLoadoutEntry(
    string CoinId,
    string StatusId,
    int Count);

public sealed class SukiCoinLoadoutEditor : INotifyPropertyChanged
{
    private int _count;
    private SukiSpecialEffectOption _selectedStatus;

    public SukiCoinLoadoutEditor(
        SukiSpecialEffectOption coin,
        IReadOnlyList<SukiSpecialEffectOption> statusOptions,
        string statusId = "",
        int count = 1)
    {
        Coin = coin;
        StatusOptions = statusOptions;
        _selectedStatus = statusOptions.FirstOrDefault(option => option.Id.Equals(statusId, StringComparison.Ordinal))
            ?? statusOptions.First();
        _count = Math.Clamp(count, 1, 99);
    }

    public SukiSpecialEffectOption Coin { get; }
    public string CoinId => Coin.Id;
    public string Name => Coin.Name;
    public string Effect => Coin.Effect;
    public string Category => Coin.Category;
    public string ImagePath => Coin.ImagePath;
    public IReadOnlyList<SukiSpecialEffectOption> StatusOptions { get; }

    public SukiSpecialEffectOption SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (ReferenceEquals(_selectedStatus, value) || value is null)
                return;
            _selectedStatus = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedStatus)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusId)));
        }
    }

    public string StatusId => SelectedStatus.Id;

    public int Count
    {
        get => _count;
        set
        {
            var normalized = Math.Clamp(value, 1, 99);
            if (_count == normalized)
                return;
            _count = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record SukiSpecialFieldState(
    string CampaignId,
    string FieldId,
    string Label,
    string Type,
    string Value,
    string Kind,
    string ProfileId,
    string Detail,
    string EffectId = "",
    IReadOnlyList<string>? SelectedIds = null,
    IReadOnlyList<string>? OperatorIds = null,
    IReadOnlyList<SukiCoinLoadoutEntry>? CoinEntries = null,
    IReadOnlyList<SukiOperatorTargetRef>? OperatorTargets = null);

public sealed record SukiCampaignWorkspacePreview(
    string Id,
    string DisplayName,
    string Detail,
    bool IsCurrentRun,
    bool IsSelected)
{
    public string SelectedLabel => IsSelected ? "表示中" : "";

    public string CurrentRunLabel => IsCurrentRun ? "ラン元" : "";

    public string CurrentRunActionLabel => IsCurrentRun ? "現在ラン" : "現在ランに設定";

    public bool CanSetCurrentRun => !IsCurrentRun;
}

public sealed record SukiRuntimeCapabilityPreview(
    string Id,
    string Name,
    string Tag,
    string State,
    string Detail,
    string PrimaryAction,
    bool IsOptional)
{
    public string InstallLabel => IsOptional ? "任意DL" : "必須";
}

public sealed record SukiRuntimeCapabilityContext(
    string AdbState,
    string AdbDetail,
    IntegrationStatus MaaFrameworkStatus,
    string MaaOcrState,
    string MaaOcrDetail,
    SukiOptionalRuntimeStatus GlmStatus,
    SukiOptionalRuntimeStatus OllamaStatus,
    SukiHypervisorStatus HypervisorStatus);

public sealed record SukiWorkspaceSectionPreview(
    string Id,
    string Title,
    string Detail);

public sealed record SukiProductSurfaceDescriptor(
    string Id,
    string Category,
    string WorkspaceId,
    string StatePath,
    string Provenance,
    string InspectorKind,
    string ReviewPolicy,
    bool CanShowOnOutput,
    int DisplayPriority);

public sealed record SukiWorkspaceLayout(
    string WorkspaceId,
    SukiWorkspaceSectionPreview Header,
    IReadOnlyList<SukiWorkspaceSectionPreview> Sections);

public sealed record SukiWorkspaceActionDescriptor(
    string Id,
    string WorkspaceId,
    string SectionId,
    string Label,
    string CommandName,
    string Workflow,
    string Detail,
    bool RequiresMaaSession,
    bool WritesState,
    int DisplayPriority)
{
    public string MaaRequirementLabel => RequiresMaaSession ? "MAA" : "local/API";

    public string StateWriteLabel => WritesState ? "state更新" : "read-only";
}

public sealed record SukiWorkspaceActionCommandSpec(
    string CommandName,
    string? CommandParameter);

public sealed record SukiWorkspaceActionPreview(
    SukiWorkspaceActionDescriptor Descriptor,
    ICommand? Command,
    object? CommandParameter)
{
    public string Id => Descriptor.Id;

    public string WorkspaceId => Descriptor.WorkspaceId;

    public string SectionId => Descriptor.SectionId;

    public string Label => Descriptor.Label;

    public string CommandName => Descriptor.CommandName;

    public string Workflow => Descriptor.Workflow;

    public string Detail => Descriptor.Detail;

    public bool RequiresMaaSession => Descriptor.RequiresMaaSession;

    public bool WritesState => Descriptor.WritesState;

    public string MaaRequirementLabel => Descriptor.MaaRequirementLabel;

    public string StateWriteLabel => Descriptor.StateWriteLabel;

    public bool IsExecutable => Command is not null;

    public string ActionButtonLabel => IsExecutable ? "実行" : "未接続";
}

public sealed record SukiRuntimeWorkspaceLayout(
    SukiWorkspaceSectionPreview Header,
    SukiWorkspaceSectionPreview Connection,
    SukiWorkspaceSectionPreview Detection,
    SukiWorkspaceSectionPreview Diagnostics,
    SukiWorkspaceSectionPreview OptionalRuntime)
{
    public IReadOnlyList<SukiWorkspaceSectionPreview> Sections =>
    [
        Connection,
        Detection,
        Diagnostics,
        OptionalRuntime
    ];
}

public sealed record SukiRecognitionWorkspaceLayout(
    SukiWorkspaceSectionPreview Header,
    SukiWorkspaceSectionPreview Profile,
    SukiWorkspaceSectionPreview Execution,
    SukiWorkspaceSectionPreview Review,
    SukiWorkspaceSectionPreview Evidence)
{
    public IReadOnlyList<SukiWorkspaceSectionPreview> Sections =>
    [
        Profile,
        Execution,
        Review,
        Evidence
    ];
}

public sealed record SukiInspectorRow(
    string Label,
    string Value,
    string Detail);

public sealed record SukiOutputPartDescriptor(
    string Id,
    string Label,
    string BindingPath,
    string Detail,
    bool DefaultEnabled,
    bool DefaultScrollEnabled,
    bool DefaultHideExcluded,
    int DefaultWidth,
    int DefaultHeight)
{
    public SukiOutputPartPreview ToPreview()
    {
        return new SukiOutputPartPreview(
            Id,
            Label,
            BindingPath,
            Detail,
            DefaultEnabled,
            DefaultScrollEnabled,
            DefaultHideExcluded,
            DefaultWidth,
            DefaultHeight);
    }
}

public sealed class SukiOutputPartPreview : INotifyPropertyChanged
{
    private bool _enabled;
    private bool _scrollEnabled;
    private bool _hideExcluded;
    private int _width;
    private int _height;

    public SukiOutputPartPreview(
        string id,
        string label,
        string bindingPath,
        string detail,
        bool enabled,
        bool scrollEnabled,
        bool hideExcluded,
        int width,
        int height)
    {
        Id = id;
        Label = label;
        BindingPath = bindingPath;
        Detail = detail;
        _enabled = enabled;
        _scrollEnabled = scrollEnabled;
        _hideExcluded = hideExcluded;
        _width = width;
        _height = height;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Label { get; }

    public string BindingPath { get; }

    public string Detail { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            OnPropertyChanged();
        }
    }

    public bool ScrollEnabled
    {
        get => _scrollEnabled;
        set
        {
            if (_scrollEnabled == value)
                return;
            _scrollEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool HideExcluded
    {
        get => _hideExcluded;
        set
        {
            if (_hideExcluded == value)
                return;
            _hideExcluded = value;
            OnPropertyChanged();
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            if (_width == value)
                return;
            _width = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (_height == value)
                return;
            _height = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeLabel));
        }
    }

    public string SizeLabel => $"{Width}x{Height}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record SukiRunStateSnapshot(
    string CampaignId,
    IReadOnlySet<string> SelectedOperatorIds,
    IReadOnlySet<string> SelectedRelicIds,
    IReadOnlySet<string> ExcludedOperatorIds,
    IReadOnlySet<string> ExcludedRelicIds,
    bool OperatorShowSelectedFirst,
    bool OperatorHideExcluded,
    bool OperatorSelectedOnly,
    bool RelicShowSelectedFirst,
    bool RelicHideExcluded,
    bool RelicSelectedOnly,
    int OperatorGridColumns = 2,
    int RelicGridColumns = 2,
    string Squad = "",
    string SquadRandomEffect = "",
    string Difficulty = "",
    int Ingot = 0,
    int Idea = 0,
    IReadOnlyList<SukiSpecialFieldState>? SpecialFields = null,
    string OcrEngine = "maa-ocr",
    IReadOnlyDictionary<string, IReadOnlyList<string>>? BossSelections = null,
    string PerformanceId = "",
    string Performance = "")
{
    public IReadOnlySet<string> UsedRelicIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> OperatorCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}

public sealed record RhodesRunCatalogSnapshot(
    IReadOnlyList<SukiCampaignPreview> Campaigns,
    IReadOnlyList<SukiChoiceItem> Operators,
    IReadOnlyList<SukiChoiceItem> Relics,
    SukiRunStateSnapshot Current);

public sealed record SukiChoiceFilterOptions(
    string SearchText = "",
    string Category = "",
    string OperatorClass = "",
    string OperatorBranch = "",
    string Rarity = "",
    string CampaignId = "",
    bool ShowSelectedFirst = false,
    bool HideExcluded = false,
    bool SelectedOnly = false,
    bool IncludeHidden = false,
    string SortMode = "");

public sealed record SukiChoiceRow(
    int Columns,
    IReadOnlyList<SukiChoiceItem> Items);

public sealed record SukiChoiceCatalogDescriptor(
    string Id,
    string Kind,
    string Label,
    bool IsCampaignScoped,
    string SelectedSummaryLabel,
    string TotalSummaryLabel);

public sealed record SukiChoiceCatalogFilterState(
    string SearchText = "",
    string Category = "",
    string OperatorClass = "",
    string OperatorBranch = "",
    string Rarity = "",
    string CampaignId = "",
    bool ShowSelectedFirst = false,
    bool HideExcluded = false,
    bool SelectedOnly = false,
    int PaneColumns = 2,
    string SortMode = "");

public sealed record SukiChoiceCatalogView(
    SukiChoiceCatalogDescriptor Descriptor,
    SukiChoiceCatalogFilterState FilterState,
    IReadOnlyList<SukiChoiceItem> FilteredItems,
    IReadOnlyList<SukiChoiceRow> Rows,
    string Summary);

public sealed class SukiChoiceItem : INotifyPropertyChanged
{
    public const int MaximumSelectionCount = 99;

    private bool _isSelected;
    private bool _isExcluded;
    private bool _isUsed;
    private bool _isRejectionReactionTarget;
    private bool _isEvolutionTarget;
    private int _selectionCount = 1;

    public SukiChoiceItem(
        string kind,
        string id,
        string name,
        string heading,
        string operatorClass,
        string operatorBranch,
        string campaignId,
        string category,
        int rarity,
        int sortOrder,
        bool hiddenByDefault,
        string detail = "",
        string searchText = "",
        string imagePath = "",
        bool supportsUsedFlag = false)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Heading = heading;
        OperatorClass = operatorClass;
        OperatorBranch = operatorBranch;
        CampaignId = campaignId;
        Category = category;
        Rarity = rarity;
        SortOrder = sortOrder;
        HiddenByDefault = hiddenByDefault;
        Detail = detail;
        ImagePath = imagePath;
        SupportsUsedFlag = supportsUsedFlag;
        SearchText = string.IsNullOrWhiteSpace(searchText)
            ? $"{id} {name} {heading} {operatorClass} {operatorBranch} {campaignId} {category} {detail}"
            : searchText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Kind { get; }

    public string Id { get; }

    public string Name { get; }

    public string Heading { get; }

    public string Detail { get; }

    public string ImagePath { get; }

    public string OperatorClass { get; }

    public string OperatorBranch { get; }

    public string CampaignId { get; }

    public string Category { get; }

    public int Rarity { get; }

    public int SortOrder { get; }

    public bool HiddenByDefault { get; }

    public bool SupportsUsedFlag { get; }

    public string SearchText { get; }

    public bool IsRejectionReactionTarget
    {
        get => _isRejectionReactionTarget;
        set
        {
            if (_isRejectionReactionTarget == value)
                return;
            _isRejectionReactionTarget = value;
            OnPropertyChanged();
        }
    }

    public bool IsEvolutionTarget
    {
        get => _isEvolutionTarget;
        set
        {
            if (_isEvolutionTarget == value)
                return;
            _isEvolutionTarget = value;
            OnPropertyChanged();
        }
    }

    public bool SupportsMultipleCount =>
        string.Equals(Kind, "operator", StringComparison.Ordinal)
        && (Id.StartsWith("reserve_", StringComparison.OrdinalIgnoreCase)
            || Name.StartsWith("予備隊員", StringComparison.Ordinal));

    public int SelectionCount
    {
        get => SupportsMultipleCount ? _selectionCount : 1;
        set
        {
            var normalized = SupportsMultipleCount
                ? Math.Clamp(value, 1, MaximumSelectionCount)
                : 1;
            if (_selectionCount == normalized)
                return;
            _selectionCount = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveSelectionCount));
            OnPropertyChanged(nameof(StateLabel));
        }
    }

    public int EffectiveSelectionCount => IsSelected ? SelectionCount : 0;

    public bool IsSelectionCountVisible => SupportsMultipleCount && IsSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            var resetSelectionCount = !value && _selectionCount != 1;
            _isSelected = value;
            if (!value)
                _selectionCount = 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionButtonLabel));
            OnPropertyChanged(nameof(StateLabel));
            OnPropertyChanged(nameof(IsUsageToggleVisible));
            if (resetSelectionCount)
                OnPropertyChanged(nameof(SelectionCount));
            OnPropertyChanged(nameof(EffectiveSelectionCount));
            OnPropertyChanged(nameof(IsSelectionCountVisible));
        }
    }

    public bool IsUsed
    {
        get => _isUsed;
        set
        {
            var normalized = SupportsUsedFlag && value;
            if (_isUsed == normalized)
                return;
            _isUsed = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(UsageButtonLabel));
            OnPropertyChanged(nameof(StateLabel));
        }
    }

    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (_isExcluded == value)
                return;
            _isExcluded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExclusionButtonLabel));
            OnPropertyChanged(nameof(StateLabel));
        }
    }

    public string SelectionButtonLabel => IsSelected ? "選択解除" : "選択";

    public string ExclusionButtonLabel => IsExcluded ? "除外解除" : "表示除外";

    public bool IsUsageToggleVisible => SupportsUsedFlag && IsSelected;

    public string UsageButtonLabel => IsUsed ? "使用済" : "未使用";

    public string StateLabel
    {
        get
        {
            if (IsSelected && IsExcluded)
                return "選択 / 除外";
            if (IsSelected)
            {
                if (SupportsMultipleCount && SelectionCount > 1)
                    return $"選択中 / {SelectionCount}名";
                return IsUsed ? "選択中 / 使用済" : "選択中";
            }
            if (IsExcluded)
                return "除外";
            if (HiddenByDefault)
                return "未実装";
            return "";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
