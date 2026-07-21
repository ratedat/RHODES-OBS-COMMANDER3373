using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MaaFramework.Binding;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly RhodesMaaSession _session;
    private readonly RhodesDistributionProfile _distributionProfile;
    private readonly SemaphoreSlim _bossSelectionSaveLock = new(1, 1);
    private IReadOnlyList<MaaResourceTaskPreview> _allResourceTasks;
    private readonly IReadOnlyList<SukiChoiceItem> _allOperators = [];
    private readonly IReadOnlyList<SukiChoiceItem> _allRelics = [];
    private readonly IntegrationStatus _maaFrameworkStatus;
    private SukiRunStateSnapshot _runState;
    private byte[] _lastCapture = [];
    private string _adbPath = "adb";
    private string _adbSerial = "";
    private string _adbConfigJson = "{}";
    private string _workspaceTab = "run";
    private string _choiceTab = "operators";
    private string _operatorSearch = "";
    private string _operatorRarityFilter = "すべて";
    private string _operatorClassFilter = "すべて";
    private string _operatorBranchFilter = "すべて";
    private string _operatorSortMode = "レア度順";
    private string _relicSearch = "";
    private string _relicCategoryFilter = "すべて";
    private string _relicSortMode = "秘宝種別順";
    private int _operatorPaneColumns = 2;
    private int _relicPaneColumns = 2;
    private string _sessionState;
    private string _sessionDetail;
    private string _captureState = "未取得";
    private string _lastCapturePath = "";
    private string _lastFrameId = "";
    private string _lastFrameMetadataPath = "";
    private string _lastFrameStateSnapshotPath = "";
    private string _lastResourceTaskResultsPath = "";
    private string _lastRoiDraftPath = "";
    private string _lastRoiSessionPath = "";
    private string _lastBugReportBundlePath = "";
    private string _bugReportBundleStatus = "未作成";
    private string _bugReportImportPath = "";
    private string _bugReportImportStatus = "未取込";
    private string _importedBugReportFrameRecordsDirectory = "";
    private string _importedBugReportRecognitionScansDirectory = "";
    private string _roiRescanComparisonSummary = "再スキャン比較未実行";
    private string _roiRescanComparisonEvidenceSummary = "比較証跡未保存";
    private string _roiRescanEvidencePreviewTitle = "比較証跡未表示";
    private string _roiRescanEvidencePreviewText = "";
    private string _roiRescanEvidencePreviewJson = "";
    private string _lastRoiRescanBeforePath = "";
    private string _lastRoiRescanAfterPath = "";
    private MaaEvidencePreviewNode? _selectedRoiRescanEvidencePreviewNode;
    private bool _roiEvidenceShowCandidates = true;
    private bool _roiEvidenceShowTasks = true;
    private bool _roiEvidenceShowLog = true;
    private MaaRoiDraftApplyResult _roiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
    private MaaRoiBatchApplyResult _roiBatchApplyResult = MaaRoiBatchApplyResult.Failed("未確認");
    private MaaResourceGenerationResult _maaResourceGenerationResult = MaaResourceGenerationResult.Failed("未実行");
    private MaaResourceContractSnapshot _maaResourceContract = RhodesMaaResourceCatalog.ValidateContract();
    private string _rhodesApiUrl = "http://127.0.0.1:5173";
    private string _statusMessage = "MAAFramework の検証準備ができています。";
    private string _roiSessionRestoreNotice = "ROI調整セッション未読込";
    private string _lastCandidateApplySummary = "候補未反映";
    private SukiOptionalRuntimeStatus _rhodesApiStatus = new("RHODES API", "未確認", "状態同期または認識API実行で確認します。", false, false);
    private SukiOptionalRuntimeStatus _masterDataStatus = new("Master Data", "未確認", "/api/master未確認", false, false);
    private SukiOptionalRuntimeStatus _glmRuntimeStatus = new("GLM-OCR", "未確認", "状態確認を実行してください。", false, false);
    private SukiOptionalRuntimeStatus _ollamaRuntimeStatus = new("Ollama", "未確認", "状態確認を実行してください。", false, false);
    private SukiHypervisorStatus _hypervisorStatus = new("未確認", "Google Play Gamesや一部エミュレーターの前提確認", false, false, "info");
    private Bitmap? _lastCaptureImage;
    private int _capturePixelWidth;
    private int _capturePixelHeight;
    private bool _adbDiagnosticsDetectionAttempted;
    private bool? _adbDiagnosticsMaaReady;
    private bool? _adbDiagnosticsCaptureSucceeded;
    private string _adbDiagnosticsCaptureDetail = "";
    private DispatcherTimer? _operatorSearchDebounce;
    private DispatcherTimer? _relicSearchDebounce;
    private int _manualIngot;
    private int _manualDifficulty = 1;
    private SukiSquadOption? _selectedManualSquad;
    private SukiSquadOption? _selectedManualSquadRandomEffect;
    private bool _adbConnectionValidated;
    private int _manualIdea;
    private int _manualMizukiKey;
    private int _manualMizukiLight;
    private SukiSpecialEffectOption? _selectedManualAge;
    private SukiSpecialEffectOption? _selectedManualPerformance;
    private SukiSpecialEffectOption? _selectedManualMizukiRejection;
    private SukiThoughtCountEditor? _selectedThoughtDetail;
    private bool _isClearRunConfirmationVisible;
    private bool _isHudVisible;
    private bool _isHudClickThrough;
    private readonly RhodesNodeRuntimeManager _nodeRuntime;
    private readonly RhodesSidecarServerLauncher _sidecarServer;
    private RhodesNodeRuntimeStatus _nodeRuntimeStatus;
    private string _sidecarServerStatus = "未確認";
    private string _selectedRoiPreviewKey = "";
    private int[] _roiDragOrigin = [];
    private double _roiDragStartX;
    private double _roiDragStartY;
    private int[] _roiResizeOrigin = [];
    private string _roiResizeMode = "";
    private double _roiResizeStartX;
    private double _roiResizeStartY;
    private MaaRoiPreviewRow? _selectedRoiPreviewRow;
    private MaaRoiEditDraft _selectedRoiEditDraft = MaaRoiEditDraft.Empty;
    private MaaRoiRescanComparisonRow? _selectedRoiRescanComparisonRow;
    private MaaCandidatePreview? _selectedCandidateResult;
    private MaaOcrDetailRow? _selectedOcrDetailRow;
    private MaaTaskRunResult? _selectedResourceTaskResult;
    private RhodesRecognitionScanLogRow? _selectedRecognitionScanLogRow;
    private MaaAdbPresetPreview? _selectedAdbPreset;
    private MaaAdbPathCandidatePreview? _selectedAdbPathCandidate;
    private SukiAdbInputMethodOption? _selectedAdbInputMethod;
    private SukiAdbScreencapMethodOption? _selectedAdbScreencapMethod;
    private MaaResourceProfilePreview? _selectedResourceProfile;
    private MaaResourceExecutionPlan? _lastResourceExecutionPlan;
    private SukiOcrEngineOption? _selectedOcrEngine;
    private SukiCampaignPreview? _selectedCampaign;
    private bool _campaignSelectionSyncEnabled;
    private MaaTaskDiagnosticsSnapshot _resourceTaskDiagnostics = MaaTaskDiagnosticsSnapshot.Empty;
    private bool _operatorShowSelectedFirst;
    private bool _operatorHideExcluded;
    private bool _operatorSelectedOnly;
    private bool _relicShowSelectedFirst;
    private bool _relicHideExcluded;
    private bool _relicSelectedOnly;
    private bool _outputSeparateWindow = true;
    private bool _outputTournamentMode;
    private bool _outputTransparentBackground = true;
    private int _outputScrollSpeed = 13;
    private SukiOverlayLayoutPreview? _selectedOverlayLayoutItem;
    private bool _showRoiOverlay = true;
    private int _roiSnapStep = 1;
    private bool _isBusy;
    private string _adbDetectionSummary = "未検出";
    private string _adbDetectionDetail = "自動検出を実行するとADB候補と接続端末を表示します。";

    public MainWindowViewModel(
        IntegrationStatus maaStatus,
        RhodesMaaSession session,
        MaaSessionSnapshot sessionSnapshot)
    {
        _session = session;
        _maaFrameworkStatus = maaStatus;
        _nodeRuntime = new RhodesNodeRuntimeManager();
        _sidecarServer = new RhodesSidecarServerLauncher(_nodeRuntime);
        _nodeRuntimeStatus = _nodeRuntime.Probe();
        _sessionState = sessionSnapshot.State;
        _sessionDetail = sessionSnapshot.Detail;
        _allResourceTasks = RhodesMaaResourceCatalog.DefaultTasks();

        RuntimeStatuses =
        [
            maaStatus,
            new IntegrationStatus("MAA Resource", SessionState, SessionDetail, sessionSnapshot.IsReady),
            new IntegrationStatus(
                "MAA-OCR",
                MaaOcrStatusState(),
                MaaOcrStatusDetail(),
                IsMaaOcrReady()),
            new IntegrationStatus("GLM-OCR", "任意導入", "高精度検証用の別ランタイムとして維持", false),
        ];

        MigrationSteps =
        [
            "MAAFramework Resource/Controller/Tasker の最小起動を通す",
            "ADB接続とスクリーンショット取得をMAAFrameworkへ移管",
            "基本情報・オペレーター・秘宝OCRをMAAタスク化",
            "GLM-OCRを任意DLの高精度補助として接続",
            "SukiUI/Avalonia版を正規デスクトップとして固定",
        ];

        _distributionProfile = RhodesDistributionProfile.LoadDefault();
        var runCatalog = RhodesRunCatalog.LoadDefault();
        _runState = RhodesPublicDebugPolicy.ApplyCampaign(runCatalog.Current, _distributionProfile);
        WorkspaceNav = new ObservableCollection<SukiWorkspaceNavItem>(
            RhodesWorkspaceRegistry.ItemsFor(_distributionProfile));
        RunLayout = RhodesWorkspaceLayoutRegistry.For("run");
        SpecialLayout = RhodesWorkspaceLayoutRegistry.For("special");
        ChoicesLayout = RhodesWorkspaceLayoutRegistry.For("choices");
        OutputLayout = RhodesWorkspaceLayoutRegistry.For("output");
        DebugLayout = RhodesWorkspaceLayoutRegistry.For("debug");
        SpecialValuesSection = WorkspaceSection(SpecialLayout, "values");
        RunCampaignSection = WorkspaceSection(RunLayout, "campaign");
        OutputPartsSection = WorkspaceSection(OutputLayout, "parts");
        DebugMigrationSection = WorkspaceSection(DebugLayout, "migration");
        RuntimeLayout = RhodesRuntimeWorkspaceRegistry.Layout;
        RecognitionLayout = RhodesRecognitionWorkspaceRegistry.Layout;
        HeaderStatusChips = new ObservableCollection<SukiStatusChip>(BuildHeaderStatusChips());
        RunFieldPreviews = new ObservableCollection<SukiRunFieldPreview>(BuildRunFieldPreviews(_runState));
        CampaignPreviews = [];
        SpecialValuePreviews = [];
        RuntimeCapabilities = new ObservableCollection<SukiRuntimeCapabilityPreview>(BuildRuntimeCapabilities());
        InspectorRows = [];
        WorkspaceActions = [];
        OutputParts = new ObservableCollection<SukiOutputPartPreview>(RhodesOutputPartRegistry.BuildDefaultPreviews());
        foreach (var outputPart in OutputParts)
        {
            outputPart.PropertyChanged += (_, _) => RefreshInspectorRows();
        }
        OverlayLayoutItems = new ObservableCollection<SukiOverlayLayoutPreview>(RhodesOverlayLayoutCatalog.BuildPreviews());
        foreach (var layoutItem in OverlayLayoutItems)
            layoutItem.PropertyChanged += (_, _) => RefreshInspectorRows();
        SelectedOverlayLayoutItem = OverlayLayoutItems.FirstOrDefault();
        DebugLogLines =
        [
            "Suki shell ready.",
            "Debug logs: RHODES OBS COMMANDER3373 Debug Logs",
            "ADB capture and MAA task results are saved beside the packaged executable.",
        ];
        ProbePayloads = new ObservableCollection<MaaProbePayloadPreview>(Services.RhodesRecognitionProbe.DefaultPayloads());
        ProbeResults = [];
        AdbPresets = new ObservableCollection<MaaAdbPresetPreview>(RhodesAdbPresetCatalog.DefaultPresets());
        AdbPathCandidates = [];
        AdbDevices = [];
        OcrEngineOptions = new ObservableCollection<SukiOcrEngineOption>(SukiOcrEngineCatalog.Options);
        AdbInputMethodOptions = new ObservableCollection<SukiAdbInputMethodOption>(SukiAdbMethodCatalog.InputOptions);
        AdbScreencapMethodOptions = new ObservableCollection<SukiAdbScreencapMethodOption>(SukiAdbMethodCatalog.ScreencapOptions);
        SelectedAdbPreset = AdbPresets.FirstOrDefault(preset => preset.Id == "auto") ?? AdbPresets.FirstOrDefault();
        SelectedAdbInputMethod = SukiAdbMethodCatalog.FindInput(SukiAdbMethodCatalog.DefaultInputMethodId);
        SelectedAdbScreencapMethod = SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.DefaultScreencapMethodId);
        Campaigns = new ObservableCollection<SukiCampaignPreview>(
            RhodesPublicDebugPolicy.FilterCampaigns(runCatalog.Campaigns, _distributionProfile));
        _allOperators = runCatalog.Operators;
        _allRelics = runCatalog.Relics;
        FilteredOperators = [];
        FilteredRelics = [];
        FilteredOperatorRows = [];
        FilteredRelicRows = [];
        OperatorRarityOptions = [];
        OperatorClassOptions = [];
        OperatorBranchOptions = [];
        OperatorSortOptions = ["レア度順", "職業・職分順", "名前順"];
        RelicCategoryOptions = [];
        RelicSortOptions = ["秘宝種別順", "番号順", "名前順"];
        RunSelectionHighlights = [];
        PaneColumnOptions = [1, 2, 3, 4];
        _operatorShowSelectedFirst = runCatalog.Current.OperatorShowSelectedFirst;
        _operatorHideExcluded = runCatalog.Current.OperatorHideExcluded;
        _operatorSelectedOnly = runCatalog.Current.OperatorSelectedOnly;
        _relicShowSelectedFirst = runCatalog.Current.RelicShowSelectedFirst;
        _relicHideExcluded = runCatalog.Current.RelicHideExcluded;
        _relicSelectedOnly = runCatalog.Current.RelicSelectedOnly;
        _operatorPaneColumns = ClampPaneColumns(runCatalog.Current.OperatorGridColumns);
        _relicPaneColumns = ClampPaneColumns(runCatalog.Current.RelicGridColumns);
        _selectedOcrEngine = OcrEngineOptions.FirstOrDefault(option => option.Id == runCatalog.Current.OcrEngine)
            ?? OcrEngineOptions.FirstOrDefault();
        _selectedCampaign = Campaigns.FirstOrDefault(campaign => campaign.Id == _runState.CampaignId) ?? Campaigns.FirstOrDefault();
        ResourceProfiles = new ObservableCollection<MaaResourceProfilePreview>(
            RhodesPublicDebugPolicy.FilterProfiles(
                RhodesMaaResourceCatalog.ProfileGroups(_allResourceTasks),
                _distributionProfile));
        ResourceTasks = [];
        ResourceTaskResults = [];
        CandidateResults = [];
        OcrDetailRows = [];
        RoiDetailRows = [];
        RoiPreviewRows = [];
        SelectedRoiPreviewRows = [];
        RoiBatchDrafts = [];
        RoiAdjustmentSessions = [];
        RoiRescanComparisonRows = [];
        RoiRescanEvidencePreviewNodes = [];
        FrameRecordHistory = [];
        RecognitionScanHistory = [];
        RecognitionScanLogRows = [];
        BaseResolution = Services.RhodesMaaPaths.BaseResolution;
        ResourceRoot = sessionSnapshot.ResourceRoot;
        AgentBinaryRoot = sessionSnapshot.AgentBinaryRoot;

        ConnectCommand = new AsyncRelayCommand(ConnectAsync);
        ConnectAndCaptureCommand = new AsyncRelayCommand(ConnectAndCaptureAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        ApplyAdbPresetCommand = new AsyncRelayCommand(parameter => ApplyAdbPresetAsync(parameter as MaaAdbPresetPreview));
        ApplyAdbPathCandidateCommand = new AsyncRelayCommand(parameter => ApplyAdbPathCandidateAsync(parameter as MaaAdbPathCandidatePreview));
        RefreshAdbDevicesCommand = new AsyncRelayCommand(RefreshAdbDevicesAsync);
        ApplyAdbDeviceCommand = new AsyncRelayCommand(parameter => ApplyAdbDeviceAsync(parameter as MaaAdbDevicePreview));
        RunAdbConnectionTestCommand = new AsyncRelayCommand(RunAdbConnectionTestAsync);
        RefreshOptionalRuntimesCommand = new AsyncRelayCommand(RefreshOptionalRuntimesAsync);
        InstallGlmOcrCommand = new AsyncRelayCommand(() => RunOptionalRuntimeActionAsync("GLM-OCR導入", RhodesOptionalRuntimeProbe.InstallGlmAsync, status => _glmRuntimeStatus = status));
        UninstallGlmOcrCommand = new AsyncRelayCommand(() => RunOptionalRuntimeActionAsync("GLM-OCR削除", RhodesOptionalRuntimeProbe.UninstallGlmAsync, status => _glmRuntimeStatus = status));
        InstallOllamaCommand = new AsyncRelayCommand(() => RunOptionalRuntimeActionAsync("Ollama導入", RhodesOptionalRuntimeProbe.InstallOllamaAsync, status => _ollamaRuntimeStatus = status));
        StartOllamaCommand = new AsyncRelayCommand(() => RunOptionalRuntimeActionAsync("Ollama起動", RhodesOptionalRuntimeProbe.StartOllamaAsync, status => _ollamaRuntimeStatus = status));
        UninstallOllamaCommand = new AsyncRelayCommand(() => RunOptionalRuntimeActionAsync("Ollama削除", RhodesOptionalRuntimeProbe.UninstallOllamaAsync, status => _ollamaRuntimeStatus = status));
        CaptureCommand = new AsyncRelayCommand(CaptureAsync);
        RunAllProbesCommand = new AsyncRelayCommand(RunAllProbesAsync);
        RunSelectedProfileRecognitionCommand = new AsyncRelayCommand(RunSelectedProfileRecognitionAsync);
        RunSelectedProfileRecognitionAndApplyCommand = new AsyncRelayCommand(RunSelectedProfileRecognitionAndApplyAsync);
        RunProfileRecognitionAndApplyCommand = new AsyncRelayCommand(parameter => RunProfileRecognitionAndApplyAsync(parameter as string));
        RunCommonRecognitionAndApplyCommand = new AsyncRelayCommand(RunCommonRecognitionAndApplyAsync);
        RunAllOperationalRecognitionAndApplyCommand = new AsyncRelayCommand(RunAllOperationalRecognitionAndApplyAsync);
        RunCurrentSpecialRecognitionAndApplyCommand = new AsyncRelayCommand(RunCurrentSpecialRecognitionAndApplyAsync);
        RefreshFrameRecordHistoryCommand = new AsyncRelayCommand(RefreshFrameRecordHistoryAsync);
        LoadFrameRecordHistoryCommand = new AsyncRelayCommand(parameter => LoadFrameRecordHistoryAsync(parameter as RhodesFrameRecordHistoryItem));
        ReplayFrameRecordRecognitionCommand = new AsyncRelayCommand(parameter => ReplayFrameRecordRecognitionAsync(parameter as RhodesFrameRecordHistoryItem));
        RefreshRecognitionScanHistoryCommand = new AsyncRelayCommand(RefreshRecognitionScanHistoryAsync);
        LoadRecognitionScanHistoryCommand = new AsyncRelayCommand(parameter => LoadRecognitionScanHistoryAsync(parameter as RhodesRecognitionScanHistoryItem));
        OpenPreviewUrlCommand = new AsyncRelayCommand(OpenPreviewUrlAsync);
        ResetOverlayLayoutCommand = new AsyncRelayCommand(ResetOverlayLayoutAsync);
        AdjustSelectedOverlayLayoutCommand = new AsyncRelayCommand(AdjustSelectedOverlayLayoutAsync);
        RefreshNodeRuntimeCommand = new AsyncRelayCommand(RefreshNodeRuntimeAsync);
        InstallNodeRuntimeCommand = new AsyncRelayCommand(InstallNodeRuntimeAsync);
        UninstallNodeRuntimeCommand = new AsyncRelayCommand(UninstallNodeRuntimeAsync);
        RunAllResourceTasksCommand = new AsyncRelayCommand(RunAllResourceTasksAsync);
        ExportResourceTaskResultsCommand = new AsyncRelayCommand(ExportResourceTaskResultsAsync);
        ExportSelectedRoiDraftCommand = new AsyncRelayCommand(ExportSelectedRoiDraftAsync);
        ExportRoiAdjustmentSessionCommand = new AsyncRelayCommand(ExportRoiAdjustmentSessionAsync);
        CreateBugReportBundleCommand = new AsyncRelayCommand(CreateBugReportBundleAsync);
        RefreshRoiAdjustmentSessionsCommand = new AsyncRelayCommand(RefreshRoiAdjustmentSessionsAsync);
        LoadRoiAdjustmentSessionCommand = new AsyncRelayCommand(parameter => LoadRoiAdjustmentSessionAsync(parameter as MaaRoiAdjustmentSessionItem));
        SelectRoiPreviewCommand = new AsyncRelayCommand(parameter => SelectRoiPreviewAsync(parameter as MaaRoiPreviewRow));
        SetRoiSnapStepCommand = new AsyncRelayCommand(parameter => SetRoiSnapStepAsync(parameter as string));
        ResetSelectedRoiDraftCommand = new AsyncRelayCommand(ResetSelectedRoiDraftAsync);
        AdjustSelectedRoiDraftCommand = new AsyncRelayCommand(parameter => AdjustSelectedRoiDraftAsync(parameter as string));
        PreviewSelectedRoiDraftApplyCommand = new AsyncRelayCommand(PreviewSelectedRoiDraftApplyAsync);
        ApplySelectedRoiDraftCommand = new AsyncRelayCommand(ApplySelectedRoiDraftAsync);
        PreviewVisibleRoiDraftsApplyCommand = new AsyncRelayCommand(PreviewVisibleRoiDraftsApplyAsync);
        ApplyVisibleRoiDraftsCommand = new AsyncRelayCommand(ApplyVisibleRoiDraftsAsync);
        IncludeAllRoiBatchDraftsCommand = new AsyncRelayCommand(_ => SetAllRoiBatchDraftsIncludedAsync(true));
        ExcludeAllRoiBatchDraftsCommand = new AsyncRelayCommand(_ => SetAllRoiBatchDraftsIncludedAsync(false));
        RunRoiRescanComparisonCommand = new AsyncRelayCommand(RunRoiRescanComparisonAsync);
        OpenRoiRescanEvidenceCommand = new AsyncRelayCommand(OpenRoiRescanEvidenceAsync);
        PreviewRoiRescanEvidenceCommand = new AsyncRelayCommand(PreviewRoiRescanEvidenceAsync);
        RegenerateMaaResourceCommand = new AsyncRelayCommand(RegenerateMaaResourceAsync);
        SyncRunStateFromApiCommand = new AsyncRelayCommand(SyncRunStateFromApiAsync);
        ApplyManualRunValuesCommand = new AsyncRelayCommand(ApplyManualRunValuesAsync);
        ApplyManualSarkazValuesCommand = new AsyncRelayCommand(ApplyManualSarkazValuesAsync);
        ApplyManualPhantomValuesCommand = new AsyncRelayCommand(ApplyManualPhantomValuesAsync);
        ApplyManualMizukiValuesCommand = new AsyncRelayCommand(ApplyManualMizukiValuesAsync);
        ToggleManualHallucinationCommand = new AsyncRelayCommand(ToggleManualHallucinationAsync);
        ToggleManualMizukiHordeCallCommand = new AsyncRelayCommand(ToggleManualMizukiHordeCallAsync);
        ToggleManualMizukiOperatorTargetCommand = new AsyncRelayCommand(ToggleManualMizukiOperatorTargetAsync);
        ShowThoughtDetailCommand = new AsyncRelayCommand(parameter =>
        {
            SelectedThoughtDetail = parameter as SukiThoughtCountEditor;
            return Task.CompletedTask;
        });
        CloseThoughtDetailCommand = new AsyncRelayCommand(() =>
        {
            SelectedThoughtDetail = null;
            return Task.CompletedTask;
        });
        ShowClearRunConfirmationCommand = new AsyncRelayCommand(() => { IsClearRunConfirmationVisible = true; return Task.CompletedTask; });
        CancelClearRunCommand = new AsyncRelayCommand(() => { IsClearRunConfirmationVisible = false; return Task.CompletedTask; });
        ConfirmClearRunCommand = new AsyncRelayCommand(ConfirmClearRunAsync);
        ToggleHudVisibilityCommand = new AsyncRelayCommand(() => { IsHudVisible = !IsHudVisible; return Task.CompletedTask; });
        StartSidecarServerCommand = new AsyncRelayCommand(StartSidecarServerAsync);
        StopSidecarServerCommand = new AsyncRelayCommand(StopSidecarServerAsync);
        ConvertResourceTaskResultsCommand = new AsyncRelayCommand(ConvertResourceTaskResultsAsync);
        ApplyCandidateResultsCommand = new AsyncRelayCommand(ApplyCandidateResultsAsync);
        RunProbeCommand = new AsyncRelayCommand(parameter => RunProbeAsync(parameter as MaaProbePayloadPreview));
        RunResourceTaskCommand = new AsyncRelayCommand(parameter => RunResourceTaskAsync(parameter as MaaResourceTaskPreview));
        SetWorkspaceCommand = new AsyncRelayCommand(SetWorkspaceAsync);
        SetChoiceTabCommand = new AsyncRelayCommand(SetChoiceTabAsync);
        OpenRecognitionProfileCommand = new AsyncRelayCommand(OpenRecognitionProfileAsync);
        SetCurrentCampaignCommand = new AsyncRelayCommand(SetCurrentCampaignAsync);
        ToggleBossOptionCommand = new AsyncRelayCommand(ToggleBossOptionAsync);
        ToggleChoiceSelectedCommand = new AsyncRelayCommand(ToggleChoiceSelectedAsync);
        ToggleRelicUsedCommand = new AsyncRelayCommand(ToggleRelicUsedAsync);
        ToggleChoiceExcludedCommand = new AsyncRelayCommand(ToggleChoiceExcludedAsync);
        ClearVisibleChoicesCommand = new AsyncRelayCommand(ClearVisibleChoicesAsync);
        SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile => profile.Id == "runStatusFull") ?? ResourceProfiles.FirstOrDefault();
        RefreshOperatorFilterOptions();
        RefreshRelicFilterOptions();
        RefreshChoiceLists();
        RefreshCampaignPreviews();
        RefreshSpecialValuePreviews();
        RefreshFrameRecordHistory();
        RefreshRecognitionScanHistory();
        RefreshRoiAdjustmentSessions();
        RefreshWorkspaceActions();
        RefreshInspectorRows();
        LoadSettings();
        RefreshManualRunEditors();
        RefreshOverlayPartLinks();
        RefreshAdbDiagnosticSteps();
        _campaignSelectionSyncEnabled = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; } = "RHODES OBS COMMANDER3373";

    public string Subtitle { get; } = "Suki/Avalonia + MAAFramework workbench";

    public ObservableCollection<SukiWorkspaceNavItem> WorkspaceNav { get; }

    public SukiWorkspaceLayout RunLayout { get; }

    public SukiWorkspaceLayout SpecialLayout { get; }

    public SukiWorkspaceLayout ChoicesLayout { get; }

    public SukiWorkspaceLayout OutputLayout { get; }

    public SukiWorkspaceLayout DebugLayout { get; }

    public SukiWorkspaceSectionPreview SpecialValuesSection { get; }

    public SukiWorkspaceSectionPreview RunCampaignSection { get; }

    public SukiWorkspaceSectionPreview OutputPartsSection { get; }

    public SukiWorkspaceSectionPreview DebugMigrationSection { get; }

    public SukiRuntimeWorkspaceLayout RuntimeLayout { get; }

    public SukiRecognitionWorkspaceLayout RecognitionLayout { get; }

    public ObservableCollection<SukiStatusChip> HeaderStatusChips { get; }

    public ObservableCollection<SukiRunFieldPreview> RunFieldPreviews { get; }

    public ObservableCollection<SukiCampaignWorkspacePreview> CampaignPreviews { get; }

    public ObservableCollection<SukiSpecialValuePreview> SpecialValuePreviews { get; }

    public ObservableCollection<SukiRuntimeCapabilityPreview> RuntimeCapabilities { get; }

    public ObservableCollection<SukiInspectorRow> InspectorRows { get; }

    public ObservableCollection<SukiWorkspaceActionPreview> WorkspaceActions { get; }

    public string WorkspaceActionCountLabel => $"{WorkspaceActions.Count}件";

    public ObservableCollection<SukiOutputPartPreview> OutputParts { get; }

    public ObservableCollection<SukiOverlayLayoutPreview> OverlayLayoutItems { get; }

    public ObservableCollection<string> DebugLogLines { get; }

    public ObservableCollection<IntegrationStatus> RuntimeStatuses { get; }

    public ObservableCollection<string> MigrationSteps { get; }

    public ObservableCollection<MaaProbePayloadPreview> ProbePayloads { get; }

    public ObservableCollection<MaaProbeResult> ProbeResults { get; }

    public ObservableCollection<MaaAdbPresetPreview> AdbPresets { get; }

    public ObservableCollection<MaaAdbPathCandidatePreview> AdbPathCandidates { get; }

    public ObservableCollection<MaaAdbDevicePreview> AdbDevices { get; }

    public ObservableCollection<SukiAdbDiagnosticStepRow> AdbDiagnosticSteps { get; } = [];

    public ObservableCollection<SukiOcrEngineOption> OcrEngineOptions { get; }

    public ObservableCollection<SukiAdbInputMethodOption> AdbInputMethodOptions { get; }

    public ObservableCollection<SukiAdbScreencapMethodOption> AdbScreencapMethodOptions { get; }

    public ObservableCollection<SukiCampaignPreview> Campaigns { get; }

    public ObservableCollection<SukiChoiceItem> FilteredOperators { get; }

    public ObservableCollection<SukiChoiceItem> FilteredRelics { get; }

    public ObservableCollection<SukiChoiceRow> FilteredOperatorRows { get; }

    public ObservableCollection<SukiChoiceRow> FilteredRelicRows { get; }

    public ObservableCollection<string> OperatorRarityOptions { get; }

    public ObservableCollection<string> OperatorClassOptions { get; }

    public ObservableCollection<string> OperatorBranchOptions { get; }

    public ObservableCollection<string> OperatorSortOptions { get; }

    public ObservableCollection<string> RelicCategoryOptions { get; }

    public ObservableCollection<string> RelicSortOptions { get; }

    public ObservableCollection<SukiChoiceItem> RunSelectionHighlights { get; }

    public ObservableCollection<int> PaneColumnOptions { get; }

    public ObservableCollection<MaaResourceProfilePreview> ResourceProfiles { get; }

    public ObservableCollection<MaaResourceTaskPreview> ResourceTasks { get; }

    public MaaResourceExecutionPlan CurrentResourceExecutionPlan => RhodesMaaResourceCatalog.BuildExecutionPlan(_allResourceTasks, SelectedResourceProfile);

    public ObservableCollection<MaaTaskRunResult> ResourceTaskResults { get; }

    public ObservableCollection<MaaCandidatePreview> CandidateResults { get; }

    public ObservableCollection<MaaOcrDetailRow> OcrDetailRows { get; }

    public ObservableCollection<MaaRoiDetailRow> RoiDetailRows { get; }

    public ObservableCollection<MaaRoiPreviewRow> RoiPreviewRows { get; }

    public ObservableCollection<MaaRoiPreviewRow> SelectedRoiPreviewRows { get; }

    public ObservableCollection<MaaRoiBatchDraftPreview> RoiBatchDrafts { get; }

    public ObservableCollection<MaaRoiAdjustmentSessionItem> RoiAdjustmentSessions { get; }

    public ObservableCollection<MaaRoiRescanComparisonRow> RoiRescanComparisonRows { get; }

    public ObservableCollection<MaaEvidencePreviewNode> RoiRescanEvidencePreviewNodes { get; }

    public ObservableCollection<RhodesFrameRecordHistoryItem> FrameRecordHistory { get; }

    public ObservableCollection<RhodesRecognitionScanHistoryItem> RecognitionScanHistory { get; }

    public ObservableCollection<RhodesRecognitionScanLogRow> RecognitionScanLogRows { get; }

    public MaaTaskDiagnosticsSnapshot ResourceTaskDiagnostics
    {
        get => _resourceTaskDiagnostics;
        private set
        {
            if (!SetProperty(ref _resourceTaskDiagnostics, value))
                return;
            RefreshInspectorRows();
            OnPropertyChanged(nameof(RecognitionDebugSummary));
        }
    }

    public MaaResourceContractSnapshot MaaResourceContract
    {
        get => _maaResourceContract;
        private set
        {
            if (!SetProperty(ref _maaResourceContract, value))
                return;
            RefreshInspectorRows();
        }
    }

    public MaaBaseResolution BaseResolution { get; }

    public string ResourceRoot { get; }

    public string AgentBinaryRoot { get; }

    public string AdbPath
    {
        get => _adbPath;
        set
        {
            if (!SetProperty(ref _adbPath, value))
                return;
            OnPropertyChanged(nameof(AdbHeaderDetail));
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshInspectorRows();
        }
    }

    public string AdbSerial
    {
        get => _adbSerial;
        set
        {
            if (!SetProperty(ref _adbSerial, value))
                return;
            OnPropertyChanged(nameof(AdbHeaderTitle));
            OnPropertyChanged(nameof(AdbHeaderDetail));
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshInspectorRows();
        }
    }

    public string AdbConfigJson
    {
        get => _adbConfigJson;
        set
        {
            if (SetProperty(ref _adbConfigJson, string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim()))
                OnPropertyChanged(nameof(AdbDiagnosticCopyText));
        }
    }

    public string WorkspaceTab
    {
        get => _workspaceTab;
        private set
        {
            if (!SetProperty(ref _workspaceTab, value))
                return;
            OnPropertyChanged(nameof(IsRunWorkspaceVisible));
            OnPropertyChanged(nameof(IsSpecialWorkspaceVisible));
            OnPropertyChanged(nameof(IsChoicesWorkspaceVisible));
            OnPropertyChanged(nameof(IsRecognitionWorkspaceVisible));
            OnPropertyChanged(nameof(IsOutputWorkspaceVisible));
            OnPropertyChanged(nameof(IsRuntimeWorkspaceVisible));
            OnPropertyChanged(nameof(IsDebugWorkspaceVisible));
            OnPropertyChanged(nameof(IsGlobalConnectAndCaptureVisible));
            OnPropertyChanged(nameof(IsRoiToolsVisible));
            OnPropertyChanged(nameof(IsEvidencePanelsVisible));
            OnPropertyChanged(nameof(WorkspaceTitle));
            OnPropertyChanged(nameof(SelectedWorkspaceNavItem));
            RefreshWorkspaceActions();
            RefreshInspectorRows();
        }
    }

    /// <summary>左ナビのListBox選択と WorkspaceTab を同期する。</summary>
    public SukiWorkspaceNavItem? SelectedWorkspaceNavItem
    {
        get => WorkspaceNav.FirstOrDefault(item => string.Equals(item.Id, WorkspaceTab, StringComparison.Ordinal));
        set
        {
            if (value is not null)
                WorkspaceTab = value.Id;
        }
    }

    public SukiCampaignPreview? SelectedCampaign
    {
        get => _selectedCampaign;
        set
        {
            if (!SetProperty(ref _selectedCampaign, value))
                return;
            RefreshRelicFilterOptions();
            RefreshChoiceLists();
            RefreshCampaignPreviews();
            RefreshSpecialValuePreviews();
            RefreshHudRelics();
            RefreshManualRunEditors();
            RefreshInspectorRows();
            OnPropertyChanged(nameof(RunContextSummary));
            OnPropertyChanged(nameof(CampaignHeaderTitle));
            OnPropertyChanged(nameof(CampaignHeaderDetail));
            OnPropertyChanged(nameof(ManualDifficultyMaximum));
            OnPropertyChanged(nameof(IsSarkazCampaignSelected));
            OnPropertyChanged(nameof(IsPhantomCampaignSelected));
            OnPropertyChanged(nameof(IsMizukiCampaignSelected));
            OnPropertyChanged(nameof(IsManualDifficultyCampaign));
            OnPropertyChanged(nameof(ManualDifficultyGuidance));
            OnPropertyChanged(nameof(RunCommonRecognitionLabel));
            if (_campaignSelectionSyncEnabled
                && value is not null
                && !string.Equals(value.Id, _runState.CampaignId, StringComparison.Ordinal))
            {
                SetCurrentCampaignCommand.Execute(value);
            }
        }
    }

    /// <summary>共通値の手動入力: 源石錐。認識と同じ経路でstateへ反映する。</summary>
    public int ManualIngot
    {
        get => _manualIngot;
        set => SetProperty(ref _manualIngot, Math.Clamp(value, 0, 9999));
    }

    /// <summary>共通値の手動入力: 等級。反映時に多元化珍品tier(run.difficultyTierId)も導出される。</summary>
    public int ManualDifficulty
    {
        get => _manualDifficulty;
        set
        {
            if (!SetProperty(ref _manualDifficulty, Math.Clamp(value, 1, ManualDifficultyMaximum)))
                return;
            OnPropertyChanged(nameof(ManualDifficultyTierLabel));
        }
    }

    public int ManualDifficultyMaximum => RhodesRunCatalog.MaxDifficultyForCampaign(SelectedCampaign?.Id ?? _runState.CampaignId);

    /// <summary>入力中の等級に対応する多元化珍品tierのプレビュー (例: 多元化珍品: 創作的)。</summary>
    public string ManualDifficultyTierLabel =>
        RhodesDifficultyTierCatalog.DescribeTier(SelectedCampaign?.Id ?? _runState.CampaignId, ManualDifficulty);

    public bool IsManualDifficultyCampaign => RhodesMaaRecognitionPolicy.RequiresManualDifficulty(
        SelectedCampaign?.Id ?? _runState.CampaignId);

    public string ManualDifficultyGuidance => IsManualDifficultyCampaign
        ? "この統合戦略は等級を画面から取得できません。現在の等級を手動入力してください。"
        : "";

    public ObservableCollection<SukiSquadOption> ManualSquadOptions { get; } = [];

    public ObservableCollection<SukiSquadOption> ManualSquadRandomEffectOptions { get; } = [];

    public SukiSquadOption? SelectedManualSquad
    {
        get => _selectedManualSquad;
        set
        {
            if (!SetProperty(ref _selectedManualSquad, value))
                return;
            RefreshManualSquadRandomEffectOptions();
        }
    }

    public SukiSquadOption? SelectedManualSquadRandomEffect
    {
        get => _selectedManualSquadRandomEffect;
        set => SetProperty(ref _selectedManualSquadRandomEffect, value);
    }

    public bool IsManualSquadRandomEffectVisible => ManualSquadRandomEffectOptions.Count > 1;

    public int ManualIdea
    {
        get => _manualIdea;
        set => SetProperty(ref _manualIdea, Math.Clamp(value, 0, 999));
    }

    public int ManualMizukiKey
    {
        get => _manualMizukiKey;
        set => SetProperty(ref _manualMizukiKey, Math.Clamp(value, 0, 99));
    }

    public int ManualMizukiLight
    {
        get => _manualMizukiLight;
        set => SetProperty(ref _manualMizukiLight, Math.Clamp(value, 0, 100));
    }

    public ObservableCollection<SukiThoughtCountEditor> ManualThoughtEditors { get; } = [];

    public SukiThoughtCountEditor? SelectedThoughtDetail
    {
        get => _selectedThoughtDetail;
        set
        {
            if (!SetProperty(ref _selectedThoughtDetail, value))
                return;
            OnPropertyChanged(nameof(IsThoughtDetailVisible));
        }
    }

    public bool IsThoughtDetailVisible => SelectedThoughtDetail is not null;

    public ObservableCollection<SukiSpecialEffectOption> ManualAgeOptions { get; } = [];

    public ObservableCollection<SukiSpecialEffectOption> ManualPerformanceOptions { get; } = [];

    public ObservableCollection<SukiHallucinationOption> ManualHallucinationOptions { get; } = [];

    public ObservableCollection<SukiToggleEffectOption> ManualMizukiHordeCallOptions { get; } = [];

    public ObservableCollection<SukiOperatorTargetOption> ManualMizukiOperatorTargets { get; } = [];

    public ObservableCollection<SukiSpecialEffectOption> ManualMizukiRejectionOptions { get; } = [];

    public ObservableCollection<SukiHallucinationFusion> ActiveManualHallucinationFusions { get; } = [];

    public string ManualHallucinationSelectionSummary
    {
        get
        {
            var selected = ManualHallucinationOptions.Where(option => option.IsSelected).Select(option => option.Name).ToArray();
            return selected.Length == 0 ? "幻覚なし" : $"選択中 {selected.Length}件: {string.Join(" / ", selected)}";
        }
    }

    public SukiSpecialEffectOption? SelectedManualPerformance
    {
        get => _selectedManualPerformance;
        set => SetProperty(ref _selectedManualPerformance, value);
    }

    public SukiSpecialEffectOption? SelectedManualMizukiRejection
    {
        get => _selectedManualMizukiRejection;
        set => SetProperty(ref _selectedManualMizukiRejection, value);
    }

    public ObservableCollection<SukiBossSectionEditor> BossSections { get; } = [];

    public SukiSpecialEffectOption? SelectedManualAge
    {
        get => _selectedManualAge;
        set => SetProperty(ref _selectedManualAge, value);
    }

    public bool IsClearRunConfirmationVisible
    {
        get => _isClearRunConfirmationVisible;
        set => SetProperty(ref _isClearRunConfirmationVisible, value);
    }

    public bool IsRunWorkspaceVisible => WorkspaceTab == "run";

    public bool IsSpecialWorkspaceVisible => WorkspaceTab == "special";

    public bool IsSarkazCampaignSelected => string.Equals(
        SelectedCampaign?.Id ?? _runState.CampaignId,
        RhodesPublicDebugPolicy.SarkazCampaignId,
        StringComparison.Ordinal);

    public bool IsPhantomCampaignSelected => string.Equals(
        SelectedCampaign?.Id ?? _runState.CampaignId,
        "is2_phantom",
        StringComparison.Ordinal);

    public bool IsMizukiCampaignSelected => string.Equals(
        SelectedCampaign?.Id ?? _runState.CampaignId,
        "is3_mizuki",
        StringComparison.Ordinal);

    public string RunCommonRecognitionLabel => IsSarkazCampaignSelected
        ? "共通値・時代をADB取得・認識・反映"
        : IsPhantomCampaignSelected
            ? "共通値・幻覚・演目をADB取得・認識・反映"
            : IsMizukiCampaignSelected
                ? "共通値・源石錐・鍵・灯火・大群・拒絶反応・オペをADB取得・認識・反映"
                : "共通値をADB取得・認識・反映";

    public bool IsChoicesWorkspaceVisible => WorkspaceTab == "choices";

    public bool IsRecognitionWorkspaceVisible => WorkspaceTab == "recognition";

    public bool IsOutputWorkspaceVisible => WorkspaceTab == "output";

    public bool IsRuntimeWorkspaceVisible => WorkspaceTab == "runtime";

    public bool IsDebugWorkspaceVisible => WorkspaceTab == "debug";

    public bool IsGlobalConnectAndCaptureVisible => !IsRuntimeWorkspaceVisible;

    /// <summary>ROI編集ツール群は認識ワークスペースでのみ右ペインに出す。</summary>
    public bool IsRoiToolsVisible => IsRecognitionWorkspaceVisible;

    /// <summary>task結果・Probeなどの証跡パネルは認識/デバッグでのみ右ペインに出す。</summary>
    public bool IsEvidencePanelsVisible => IsRecognitionWorkspaceVisible || IsDebugWorkspaceVisible;

    public string WorkspaceTitle => RhodesWorkspaceRegistry.TitleFor(WorkspaceTab);

    public string ChoiceTab
    {
        get => _choiceTab;
        private set
        {
            if (!SetProperty(ref _choiceTab, value))
                return;
            OnPropertyChanged(nameof(IsOperatorsPanelVisible));
            OnPropertyChanged(nameof(IsRelicsPanelVisible));
            OnPropertyChanged(nameof(IsRecognitionPanelVisible));
            OnPropertyChanged(nameof(ChoicePanelTitle));
        }
    }

    public bool IsOperatorsPanelVisible => ChoiceTab == "operators";

    public bool IsRelicsPanelVisible => ChoiceTab == "relics";

    public bool IsRecognitionPanelVisible => ChoiceTab == "recognition";

    public string ChoicePanelTitle => ChoiceTab switch
    {
        "relics" => "秘宝",
        "recognition" => "認識タスク",
        _ => "オペレーター",
    };

    public string CampaignHeaderTitle => SelectedCampaign?.DisplayName ?? "IS未選択";

    public string CampaignHeaderDetail
    {
        get
        {
            var selectedOperators = _allOperators.Count(item => item.IsSelected);
            var selectedRelics = _allRelics.Count(item => item.CampaignId == SelectedCampaign?.Id && item.IsSelected);
            var isCurrentRunCampaign = string.Equals(SelectedCampaign?.Id, _runState.CampaignId, StringComparison.Ordinal);
            var squad = isCurrentRunCampaign && !string.IsNullOrWhiteSpace(_runState.Squad) ? _runState.Squad : "分隊未選択";
            var difficulty = isCurrentRunCampaign && !string.IsNullOrWhiteSpace(_runState.Difficulty) ? _runState.Difficulty : "等級未選択";
            return $"{squad} · 招集{selectedOperators}名 · 秘宝{selectedRelics}件 · {difficulty}";
        }
    }

    public string AdbHeaderTitle => string.IsNullOrWhiteSpace(AdbSerial) ? "ADB未選択" : AdbSerial;

    /// <summary>ヘッダー接続ピルのラベル。段階診断の結果から導出する。</summary>
    public string ConnectionStatusLabel => _adbDiagnosticsMaaReady switch
    {
        true when _adbDiagnosticsCaptureSucceeded == false => "接続OK / 撮影失敗",
        true => "接続OK",
        false => "接続失敗",
        null => "未接続",
    };

    public string ConnectionStatusDetail
    {
        get
        {
            var serial = string.IsNullOrWhiteSpace(AdbSerial) ? "serial未選択" : AdbSerial;
            return $"{serial} · {CapturePixelSizeLabel}";
        }
    }

    public string ConnectionStatusForeground => ConnectionStatusTone("#7BE2B6", "#F0D06A", "#F08A8A", "#AAB6B8");

    public string ConnectionStatusBackground => ConnectionStatusTone("#12241D", "#242012", "#241212", "#151D1E");

    public string ConnectionStatusBorder => ConnectionStatusTone("#2C5745", "#5C512C", "#5C2C2C", "#2B3638");

    private string ConnectionStatusTone(string ok, string warning, string failed, string unknown)
    {
        return _adbDiagnosticsMaaReady switch
        {
            true when _adbDiagnosticsCaptureSucceeded == false => warning,
            true => ok,
            false => failed,
            null => unknown,
        };
    }

    public string AdbHeaderDetail
    {
        get
        {
            var preset = SelectedAdbPreset?.Label ?? "手動";
            var serial = string.IsNullOrWhiteSpace(AdbSerial) ? "serial未選択" : AdbSerial;
            return $"{preset} · {serial} · {AdbMethodSummary} · {BaseResolution.AspectRatioLabel}";
        }
    }

    public string AdbMethodSummary
    {
        get
        {
            var screencap = SelectedAdbScreencapMethod?.Label ?? "標準撮影";
            var input = SelectedAdbInputMethod?.Label ?? "標準入力";
            return $"{screencap} / {input}";
        }
    }

    public string AdbMethodDetail
    {
        get
        {
            var screencap = SelectedAdbScreencapMethod?.Detail ?? "";
            var input = SelectedAdbInputMethod?.Detail ?? "";
            return string.Join(" / ", new[] { screencap, input }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }

    public string AdbDetectionSummary
    {
        get => _adbDetectionSummary;
        private set => SetProperty(ref _adbDetectionSummary, string.IsNullOrWhiteSpace(value) ? "未検出" : value);
    }

    public string AdbDetectionDetail
    {
        get => _adbDetectionDetail;
        private set => SetProperty(ref _adbDetectionDetail, string.IsNullOrWhiteSpace(value) ? "自動検出を実行してください。" : value);
    }

    public string AdbPathCandidateSummary => $"{AdbPathCandidates.Count}件";

    public bool HasAdbPathCandidates => AdbPathCandidates.Count > 0;

    public bool IsAdbPathCandidateListEmpty => !HasAdbPathCandidates;

    public string AdbPathCandidateEmptyMessage => "ADB候補はありません。自動検出、参照、または手動入力でADB実行ファイルを指定してください。";

    public string AdbDeviceSummary => $"{AdbDevices.Count}件";

    public bool HasAdbDevices => AdbDevices.Count > 0;

    public bool IsAdbDeviceListEmpty => !HasAdbDevices;

    public string AdbDeviceEmptyMessage => "接続端末はありません。エミュレーターを起動し、必要ならADB接続を有効化してから端末更新してください。";

    public string AdbDiagnosticCopyText => BuildAdbDiagnosticCopyText();

    public string RunContextSummary
    {
        get
        {
            var selectedOperators = _allOperators.Count(item => item.IsSelected);
            var selectedRelics = _allRelics.Count(item => item.CampaignId == SelectedCampaign?.Id && item.IsSelected);
            return $"{SelectedCampaign?.DisplayName ?? "IS未選択"} / 招集{selectedOperators}名 / 秘宝{selectedRelics}件";
        }
    }

    public string OperatorListSummary => RhodesChoiceCatalogRegistry.BuildSummary(
        "operator",
        _allOperators,
        FilteredOperators,
        OperatorChoiceFilterState());

    public string RelicListSummary => RhodesChoiceCatalogRegistry.BuildSummary(
        "relic",
        _allRelics,
        FilteredRelics,
        RelicChoiceFilterState());

    public string RunSelectionSummary
    {
        get
        {
            var operatorCount = _allOperators.Count(item => item.IsSelected);
            var relicCount = _allRelics.Count(item => item.CampaignId == SelectedCampaign?.Id && item.IsSelected);
            return $"オペレーター{operatorCount}名 · 秘宝{relicCount}件";
        }
    }

    public string OperatorSearch
    {
        get => _operatorSearch;
        set
        {
            if (!SetProperty(ref _operatorSearch, value ?? ""))
                return;
            RestartSearchDebounce(ref _operatorSearchDebounce, RefreshOperatorChoices);
        }
    }

    public string OperatorRarityFilter
    {
        get => _operatorRarityFilter;
        set
        {
            if (!SetProperty(ref _operatorRarityFilter, string.IsNullOrWhiteSpace(value) ? "すべて" : value))
                return;
            RefreshOperatorFilterOptions();
            RefreshOperatorChoices();
        }
    }

    public string OperatorClassFilter
    {
        get => _operatorClassFilter;
        set
        {
            if (!SetProperty(ref _operatorClassFilter, string.IsNullOrWhiteSpace(value) ? "すべて" : value))
                return;
            RefreshOperatorFilterOptions();
            RefreshOperatorChoices();
        }
    }

    public string OperatorBranchFilter
    {
        get => _operatorBranchFilter;
        set
        {
            if (!SetProperty(ref _operatorBranchFilter, string.IsNullOrWhiteSpace(value) ? "すべて" : value))
                return;
            RefreshOperatorChoices();
        }
    }

    public string OperatorSortMode
    {
        get => _operatorSortMode;
        set
        {
            if (!SetProperty(ref _operatorSortMode, string.IsNullOrWhiteSpace(value) ? "レア度順" : value))
                return;
            RefreshOperatorChoices();
        }
    }

    public bool OperatorShowSelectedFirst
    {
        get => _operatorShowSelectedFirst;
        set
        {
            if (!SetProperty(ref _operatorShowSelectedFirst, value))
                return;
            RefreshOperatorChoices();
            PersistChoiceStateInBackground();
        }
    }

    public bool OperatorHideExcluded
    {
        get => _operatorHideExcluded;
        set
        {
            if (!SetProperty(ref _operatorHideExcluded, value))
                return;
            RefreshOperatorChoices();
            PersistChoiceStateInBackground();
        }
    }

    public bool OperatorSelectedOnly
    {
        get => _operatorSelectedOnly;
        set
        {
            if (!SetProperty(ref _operatorSelectedOnly, value))
                return;
            RefreshOperatorChoices();
            PersistChoiceStateInBackground();
        }
    }

    public string RelicSearch
    {
        get => _relicSearch;
        set
        {
            if (!SetProperty(ref _relicSearch, value ?? ""))
                return;
            RestartSearchDebounce(ref _relicSearchDebounce, RefreshRelicChoices);
        }
    }

    public string RelicCategoryFilter
    {
        get => _relicCategoryFilter;
        set
        {
            if (!SetProperty(ref _relicCategoryFilter, string.IsNullOrWhiteSpace(value) ? "すべて" : value))
                return;
            RefreshRelicChoices();
        }
    }

    public string RelicSortMode
    {
        get => _relicSortMode;
        set
        {
            if (!SetProperty(ref _relicSortMode, string.IsNullOrWhiteSpace(value) ? "秘宝種別順" : value))
                return;
            RefreshRelicChoices();
        }
    }

    public bool RelicShowSelectedFirst
    {
        get => _relicShowSelectedFirst;
        set
        {
            if (!SetProperty(ref _relicShowSelectedFirst, value))
                return;
            RefreshRelicChoices();
            PersistChoiceStateInBackground();
        }
    }

    public bool RelicHideExcluded
    {
        get => _relicHideExcluded;
        set
        {
            if (!SetProperty(ref _relicHideExcluded, value))
                return;
            RefreshRelicChoices();
            PersistChoiceStateInBackground();
        }
    }

    public bool RelicSelectedOnly
    {
        get => _relicSelectedOnly;
        set
        {
            if (!SetProperty(ref _relicSelectedOnly, value))
                return;
            RefreshRelicChoices();
            PersistChoiceStateInBackground();
        }
    }

    public int OperatorPaneColumns
    {
        get => _operatorPaneColumns;
        set
        {
            if (!SetProperty(ref _operatorPaneColumns, ClampPaneColumns(value)))
                return;
            RefreshOperatorRows();
            RefreshInspectorRows();
            PersistChoiceStateInBackground();
        }
    }

    public int RelicPaneColumns
    {
        get => _relicPaneColumns;
        set
        {
            if (!SetProperty(ref _relicPaneColumns, ClampPaneColumns(value)))
                return;
            RefreshRelicRows();
            RefreshInspectorRows();
            PersistChoiceStateInBackground();
        }
    }

    public bool OutputSeparateWindow
    {
        get => _outputSeparateWindow;
        set
        {
            if (!SetProperty(ref _outputSeparateWindow, value))
                return;
            RefreshInspectorRows();
        }
    }

    public bool OutputTournamentMode
    {
        get => _outputTournamentMode;
        set
        {
            if (!SetProperty(ref _outputTournamentMode, value))
                return;
            RefreshInspectorRows();
        }
    }

    public bool OutputTransparentBackground
    {
        get => _outputTransparentBackground;
        set
        {
            if (!SetProperty(ref _outputTransparentBackground, value))
                return;
            RefreshInspectorRows();
        }
    }

    public int OutputScrollSpeed
    {
        get => _outputScrollSpeed;
        set
        {
            if (!SetProperty(ref _outputScrollSpeed, Math.Clamp(value, 0, 30)))
                return;
            RefreshInspectorRows();
        }
    }

    public SukiOverlayLayoutPreview? SelectedOverlayLayoutItem
    {
        get => _selectedOverlayLayoutItem;
        set
        {
            if (ReferenceEquals(_selectedOverlayLayoutItem, value))
                return;
            if (_selectedOverlayLayoutItem is not null)
                _selectedOverlayLayoutItem.IsSelected = false;
            _selectedOverlayLayoutItem = value;
            if (_selectedOverlayLayoutItem is not null)
                _selectedOverlayLayoutItem.IsSelected = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedOverlayLayoutItem));
        }
    }

    public bool HasSelectedOverlayLayoutItem => SelectedOverlayLayoutItem is not null;

    public string SessionState
    {
        get => _sessionState;
        private set
        {
            if (!SetProperty(ref _sessionState, value))
                return;
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public string SessionDetail
    {
        get => _sessionDetail;
        private set
        {
            if (!SetProperty(ref _sessionDetail, value))
                return;
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public string CaptureState
    {
        get => _captureState;
        private set
        {
            if (!SetProperty(ref _captureState, value))
                return;
            RefreshInspectorRows();
        }
    }

    public string LastCapturePath
    {
        get => _lastCapturePath;
        private set
        {
            if (!SetProperty(ref _lastCapturePath, value))
                return;
            RefreshInspectorRows();
        }
    }

    public string LastResourceTaskResultsPath
    {
        get => _lastResourceTaskResultsPath;
        private set
        {
            if (!SetProperty(ref _lastResourceTaskResultsPath, value ?? ""))
                return;
            RefreshRecognitionScanHistory();
        }
    }

    public string LastRoiDraftPath
    {
        get => _lastRoiDraftPath;
        private set
        {
            if (!SetProperty(ref _lastRoiDraftPath, value ?? ""))
                return;
            RefreshInspectorRows();
        }
    }

    public string LastRoiSessionPath
    {
        get => _lastRoiSessionPath;
        private set
        {
            if (!SetProperty(ref _lastRoiSessionPath, value))
                return;
            RefreshInspectorRows();
        }
    }

    public string LastBugReportBundlePath
    {
        get => _lastBugReportBundlePath;
        private set => SetProperty(ref _lastBugReportBundlePath, value ?? "");
    }

    public string BugReportBundleStatus
    {
        get => _bugReportBundleStatus;
        private set => SetProperty(ref _bugReportBundleStatus, string.IsNullOrWhiteSpace(value) ? "未作成" : value);
    }

    public string BugReportImportPath
    {
        get => _bugReportImportPath;
        private set => SetProperty(ref _bugReportImportPath, value ?? "");
    }

    public string BugReportImportStatus
    {
        get => _bugReportImportStatus;
        private set => SetProperty(ref _bugReportImportStatus, string.IsNullOrWhiteSpace(value) ? "未取込" : value);
    }

    public Bitmap? LastCaptureImage
    {
        get => _lastCaptureImage;
        private set => SetCaptureImage(value);
    }

    public bool IsLastCaptureImageEmpty => LastCaptureImage is null;

    public bool IsFrameRecordHistoryEmpty => FrameRecordHistory.Count == 0;

    public bool HasFrameRecordHistory => !IsFrameRecordHistoryEmpty;

    public string CapturePixelSizeLabel => _capturePixelWidth > 0 && _capturePixelHeight > 0
        ? $"{_capturePixelWidth}x{_capturePixelHeight}"
        : BaseResolution.AspectRatioLabel;

    public string RoiProjectionLabel => RoiPreviewRows.FirstOrDefault()?.ScaleLabel ?? $"base {BaseResolution.AspectRatioLabel}";

    public string RhodesApiUrl
    {
        get => _rhodesApiUrl;
        set
        {
            if (!SetProperty(ref _rhodesApiUrl, string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:5173" : value.TrimEnd('/')))
                return;
            _rhodesApiStatus = new SukiOptionalRuntimeStatus("RHODES API", "未確認", "API URLが変更されました。状態同期で確認してください。", false, false);
            OnPropertyChanged(nameof(OverlayUrl));
            OnPropertyChanged(nameof(CustomOverlayUrl));
            OnPropertyChanged(nameof(SidecarUrl));
            RefreshOverlayPartLinks();
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    /// <summary>OBSブラウザソースに設定するURL。</summary>
    public string OverlayUrl => $"{RhodesApiUrl}/overlay";

    public string CustomOverlayUrl => $"{RhodesApiUrl}/overlay?layout=custom";

    public string SidecarUrl => $"{RhodesApiUrl}/sidecar";

    public string NodeRuntimeState => _nodeRuntimeStatus.State;

    public string NodeRuntimeDetail => _nodeRuntimeStatus.Detail;

    /// <summary>部品単位でOBSへ追加するためのURL一覧。</summary>
    public ObservableCollection<SukiOverlayPartLink> OverlayPartLinks { get; } = [];

    private void RefreshOverlayPartLinks()
    {
        ReplaceCollection(OverlayPartLinks, RhodesOverlayPartLinkCatalog.Build(RhodesApiUrl));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string RoiSessionRestoreNotice
    {
        get => _roiSessionRestoreNotice;
        private set => SetProperty(ref _roiSessionRestoreNotice, string.IsNullOrWhiteSpace(value) ? "ROI調整セッション未読込" : value);
    }

    public string RoiRescanComparisonSummary
    {
        get => _roiRescanComparisonSummary;
        private set
        {
            if (!SetProperty(ref _roiRescanComparisonSummary, string.IsNullOrWhiteSpace(value) ? "再スキャン比較未実行" : value))
                return;
            RefreshInspectorRows();
        }
    }

    public string RoiRescanComparisonEvidenceSummary
    {
        get => _roiRescanComparisonEvidenceSummary;
        private set => SetProperty(ref _roiRescanComparisonEvidenceSummary, string.IsNullOrWhiteSpace(value) ? "比較証跡未保存" : value);
    }

    public string RoiRescanEvidencePreviewTitle
    {
        get => _roiRescanEvidencePreviewTitle;
        private set => SetProperty(ref _roiRescanEvidencePreviewTitle, string.IsNullOrWhiteSpace(value) ? "比較証跡未表示" : value);
    }

    public string RoiRescanEvidencePreviewText
    {
        get => _roiRescanEvidencePreviewText;
        private set => SetProperty(ref _roiRescanEvidencePreviewText, value ?? "");
    }

    public MaaEvidencePreviewNode? SelectedRoiRescanEvidencePreviewNode
    {
        get => _selectedRoiRescanEvidencePreviewNode;
        set
        {
            if (!SetProperty(ref _selectedRoiRescanEvidencePreviewNode, value) || value is null)
                return;
            RoiRescanEvidencePreviewText = string.IsNullOrWhiteSpace(value.PreviewText)
                ? value.Detail
                : value.PreviewText;
            SelectEvidencePreviewNode(value);
        }
    }

    public bool RoiEvidenceShowCandidates
    {
        get => _roiEvidenceShowCandidates;
        set
        {
            if (!SetProperty(ref _roiEvidenceShowCandidates, value))
                return;
            RefreshRoiRescanEvidencePreviewTree();
        }
    }

    public bool RoiEvidenceShowTasks
    {
        get => _roiEvidenceShowTasks;
        set
        {
            if (!SetProperty(ref _roiEvidenceShowTasks, value))
                return;
            RefreshRoiRescanEvidencePreviewTree();
        }
    }

    public bool RoiEvidenceShowLog
    {
        get => _roiEvidenceShowLog;
        set
        {
            if (!SetProperty(ref _roiEvidenceShowLog, value))
                return;
            RefreshRoiRescanEvidencePreviewTree();
        }
    }

    public string LastCandidateApplySummary
    {
        get => _lastCandidateApplySummary;
        private set
        {
            if (!SetProperty(ref _lastCandidateApplySummary, string.IsNullOrWhiteSpace(value) ? "候補未反映" : value))
                return;
            OnPropertyChanged(nameof(RecognitionDebugSummary));
        }
    }

    public string RecognitionDebugSummary
    {
        get
        {
            if (ResourceTaskResults.Count == 0)
                return "未実行 / task=0 / candidate=0 / state=未反映";

            var candidateState = CandidateResults.Count == 0
                ? "候補0"
                : LastCandidateApplySummary.Equals("候補未反映", StringComparison.Ordinal)
                    || LastCandidateApplySummary.Contains("履歴", StringComparison.Ordinal)
                    || LastCandidateApplySummary.Contains("再候補化", StringComparison.Ordinal)
                    ? "候補あり・反映確認待ち"
                    : "候補あり・反映処理済み";

            return string.Join(
                " / ",
                $"task={ResourceTaskDiagnostics.Succeeded}/{ResourceTaskDiagnostics.Total}",
                $"hit={ResourceTaskDiagnostics.Hit}",
                $"rawOCR={OcrDetailRows.Count}",
                $"ROI={RoiDetailRows.Count}",
                $"candidate={CandidateResults.Count}",
                $"state={candidateState}");
        }
    }

    public bool ShowRoiOverlay
    {
        get => _showRoiOverlay;
        set => SetProperty(ref _showRoiOverlay, value);
    }

    public int RoiSnapStep
    {
        get => _roiSnapStep;
        private set => SetProperty(ref _roiSnapStep, Math.Clamp(value, 1, 32));
    }

    public string RoiSnapStepLabel => $"step {RoiSnapStep}px";

    public MaaRoiPreviewRow? SelectedRoiPreviewRow
    {
        get => _selectedRoiPreviewRow;
        set
        {
            var nextKey = value?.Key ?? "";
            if (_selectedRoiPreviewKey == nextKey && Equals(_selectedRoiPreviewRow, value))
                return;

            _selectedRoiPreviewKey = nextKey;
            _selectedRoiPreviewRow = value;
            OnPropertyChanged();
            RefreshSelectedRoiPreviewRows();
        }
    }

    public MaaRoiEditDraft SelectedRoiEditDraft
    {
        get => _selectedRoiEditDraft;
        private set => SetProperty(ref _selectedRoiEditDraft, value);
    }

    public MaaRoiRescanComparisonRow? SelectedRoiRescanComparisonRow
    {
        get => _selectedRoiRescanComparisonRow;
        set
        {
            if (!SetProperty(ref _selectedRoiRescanComparisonRow, value))
                return;
            SelectCandidateForComparison(value);
        }
    }

    public MaaCandidatePreview? SelectedCandidateResult
    {
        get => _selectedCandidateResult;
        set
        {
            if (!SetProperty(ref _selectedCandidateResult, value))
                return;
            SelectEvidenceNodeForCandidate(value);
        }
    }

    public MaaRoiDraftApplyResult RoiDraftApplyResult
    {
        get => _roiDraftApplyResult;
        private set
        {
            if (!SetProperty(ref _roiDraftApplyResult, value))
                return;
            RefreshInspectorRows();
        }
    }

    public MaaRoiBatchApplyResult RoiBatchApplyResult
    {
        get => _roiBatchApplyResult;
        private set
        {
            if (!SetProperty(ref _roiBatchApplyResult, value))
                return;
            RefreshInspectorRows();
        }
    }

    public MaaResourceGenerationResult MaaResourceGenerationResult
    {
        get => _maaResourceGenerationResult;
        private set
        {
            if (!SetProperty(ref _maaResourceGenerationResult, value))
                return;
            RefreshInspectorRows();
        }
    }

    public MaaOcrDetailRow? SelectedOcrDetailRow
    {
        get => _selectedOcrDetailRow;
        set
        {
            if (Equals(_selectedOcrDetailRow, value))
                return;

            _selectedOcrDetailRow = value;
            OnPropertyChanged();
            SelectRoiForOcrDetail(value);
        }
    }

    public MaaTaskRunResult? SelectedResourceTaskResult
    {
        get => _selectedResourceTaskResult;
        set
        {
            if (Equals(_selectedResourceTaskResult, value))
                return;

            _selectedResourceTaskResult = value;
            OnPropertyChanged();
            SelectRoiForTaskResult(value);
            SelectEvidenceNodeForTaskEntry(value?.Entry ?? "");
        }
    }

    public RhodesRecognitionScanLogRow? SelectedRecognitionScanLogRow
    {
        get => _selectedRecognitionScanLogRow;
        set
        {
            if (Equals(_selectedRecognitionScanLogRow, value))
                return;

            _selectedRecognitionScanLogRow = value;
            OnPropertyChanged();
            SelectRoiForLogRow(value);
            SelectEvidenceNodeForTaskEntry(value?.Entry ?? "");
        }
    }

    public MaaAdbPresetPreview? SelectedAdbPreset
    {
        get => _selectedAdbPreset;
        set
        {
            if (!SetProperty(ref _selectedAdbPreset, value))
                return;
            OnPropertyChanged(nameof(AdbHeaderDetail));
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public MaaAdbPathCandidatePreview? SelectedAdbPathCandidate
    {
        get => _selectedAdbPathCandidate;
        set
        {
            if (!SetProperty(ref _selectedAdbPathCandidate, value))
                return;
            RefreshInspectorRows();
        }
    }

    public SukiAdbInputMethodOption? SelectedAdbInputMethod
    {
        get => _selectedAdbInputMethod;
        set
        {
            if (!SetProperty(ref _selectedAdbInputMethod, value))
                return;
            OnPropertyChanged(nameof(AdbHeaderDetail));
            OnPropertyChanged(nameof(AdbMethodSummary));
            OnPropertyChanged(nameof(AdbMethodDetail));
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public SukiAdbScreencapMethodOption? SelectedAdbScreencapMethod
    {
        get => _selectedAdbScreencapMethod;
        set
        {
            if (!SetProperty(ref _selectedAdbScreencapMethod, value))
                return;
            OnPropertyChanged(nameof(AdbHeaderDetail));
            OnPropertyChanged(nameof(AdbMethodSummary));
            OnPropertyChanged(nameof(AdbMethodDetail));
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public MaaResourceProfilePreview? SelectedResourceProfile
    {
        get => _selectedResourceProfile;
        set
        {
            if (!SetProperty(ref _selectedResourceProfile, value))
                return;
            RefreshResourceTasks();
            RefreshInspectorRows();
        }
    }

    public SukiOcrEngineOption? SelectedOcrEngine
    {
        get => _selectedOcrEngine;
        set
        {
            if (!SetProperty(ref _selectedOcrEngine, value))
                return;
            OnPropertyChanged(nameof(AdbDiagnosticCopyText));
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand ConnectCommand { get; }

    public ICommand ConnectAndCaptureCommand { get; }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ApplyAdbPresetCommand { get; }

    public ICommand ApplyAdbPathCandidateCommand { get; }

    public ICommand RefreshAdbDevicesCommand { get; }

    public ICommand ApplyAdbDeviceCommand { get; }

    public ICommand RunAdbConnectionTestCommand { get; }

    public ICommand RefreshOptionalRuntimesCommand { get; }

    public ICommand InstallGlmOcrCommand { get; }

    public ICommand UninstallGlmOcrCommand { get; }

    public ICommand InstallOllamaCommand { get; }

    public ICommand StartOllamaCommand { get; }

    public ICommand UninstallOllamaCommand { get; }

    public ICommand CaptureCommand { get; }

    public ICommand RunAllProbesCommand { get; }

    public ICommand RunSelectedProfileRecognitionCommand { get; }

    public ICommand RunSelectedProfileRecognitionAndApplyCommand { get; }

    public ICommand RunProfileRecognitionAndApplyCommand { get; }

    public ICommand RunCommonRecognitionAndApplyCommand { get; }

    public ICommand RunAllOperationalRecognitionAndApplyCommand { get; }

    public ICommand RunCurrentSpecialRecognitionAndApplyCommand { get; }

    public ICommand ToggleHudVisibilityCommand { get; }

    public ICommand RefreshFrameRecordHistoryCommand { get; }

    public ICommand LoadFrameRecordHistoryCommand { get; }

    public ICommand ReplayFrameRecordRecognitionCommand { get; }

    public ICommand RefreshRecognitionScanHistoryCommand { get; }

    public ICommand LoadRecognitionScanHistoryCommand { get; }

    public ICommand OpenPreviewUrlCommand { get; }

    public ICommand ResetOverlayLayoutCommand { get; }

    public ICommand AdjustSelectedOverlayLayoutCommand { get; }

    public ICommand RefreshNodeRuntimeCommand { get; }

    public ICommand InstallNodeRuntimeCommand { get; }

    public ICommand UninstallNodeRuntimeCommand { get; }

    public ICommand RunAllResourceTasksCommand { get; }

    public ICommand ExportResourceTaskResultsCommand { get; }

    public ICommand ExportSelectedRoiDraftCommand { get; }

    public ICommand ExportRoiAdjustmentSessionCommand { get; }

    public ICommand CreateBugReportBundleCommand { get; }

    public ICommand RefreshRoiAdjustmentSessionsCommand { get; }

    public ICommand LoadRoiAdjustmentSessionCommand { get; }

    public ICommand SelectRoiPreviewCommand { get; }

    public ICommand SetRoiSnapStepCommand { get; }

    public ICommand ResetSelectedRoiDraftCommand { get; }

    public ICommand AdjustSelectedRoiDraftCommand { get; }

    public ICommand PreviewSelectedRoiDraftApplyCommand { get; }

    public ICommand ApplySelectedRoiDraftCommand { get; }

    public ICommand PreviewVisibleRoiDraftsApplyCommand { get; }

    public ICommand ApplyVisibleRoiDraftsCommand { get; }

    public ICommand IncludeAllRoiBatchDraftsCommand { get; }

    public ICommand ExcludeAllRoiBatchDraftsCommand { get; }

    public ICommand RunRoiRescanComparisonCommand { get; }

    public ICommand OpenRoiRescanEvidenceCommand { get; }

    public ICommand PreviewRoiRescanEvidenceCommand { get; }

    public ICommand RegenerateMaaResourceCommand { get; }

    public ICommand SyncRunStateFromApiCommand { get; }

    public ICommand ApplyManualRunValuesCommand { get; }
    public ICommand ApplyManualSarkazValuesCommand { get; }
    public ICommand ApplyManualPhantomValuesCommand { get; }
    public ICommand ApplyManualMizukiValuesCommand { get; }
    public ICommand ToggleManualHallucinationCommand { get; }
    public ICommand ToggleManualMizukiHordeCallCommand { get; }
    public ICommand ToggleManualMizukiOperatorTargetCommand { get; }
    public ICommand ShowThoughtDetailCommand { get; }
    public ICommand CloseThoughtDetailCommand { get; }
    public ICommand ShowClearRunConfirmationCommand { get; }
    public ICommand CancelClearRunCommand { get; }
    public ICommand ConfirmClearRunCommand { get; }
    public ICommand ToggleBossOptionCommand { get; }

    public ICommand StartSidecarServerCommand { get; }

    public ICommand StopSidecarServerCommand { get; }

    public ICommand ConvertResourceTaskResultsCommand { get; }

    public ICommand ApplyCandidateResultsCommand { get; }

    public ICommand RunProbeCommand { get; }

    public ICommand RunResourceTaskCommand { get; }

    public ICommand SetWorkspaceCommand { get; }

    public ICommand SetChoiceTabCommand { get; }

    public ICommand OpenRecognitionProfileCommand { get; }

    public ICommand SetCurrentCampaignCommand { get; }

    public ICommand ToggleChoiceSelectedCommand { get; }

    public ICommand ToggleRelicUsedCommand { get; }

    public ICommand ToggleChoiceExcludedCommand { get; }

    public ICommand ClearVisibleChoicesCommand { get; }

    public void Dispose()
    {
        _lastCaptureImage?.Dispose();
        _sidecarServer.Dispose();
        _session.Dispose();
    }

    /// <summary>
    /// 本人用HUD小窓 (MAAのログ小窓と同型のデスクトップ最前面ウィンドウ) の表示状態。
    /// ウィンドウ生成・破棄はMainWindowのコードビハインドがこのプロパティを監視して行う。
    /// </summary>
    public bool IsHudVisible
    {
        get => _isHudVisible;
        set
        {
            if (!SetProperty(ref _isHudVisible, value))
                return;
            // 表示し直すたびにクリック透過を解除する。透過中はHUD自身を操作できないため、
            // ここが「詰み」からの復帰経路になる (ヘッダーのHUDトグルをOFF→ON)。
            if (value)
                IsHudClickThrough = false;
        }
    }

    /// <summary>HUDのクリック透過。ONの間はHUDがマウスを素通しし、下のゲームを操作できる。</summary>
    public bool IsHudClickThrough
    {
        get => _isHudClickThrough;
        set => SetProperty(ref _isHudClickThrough, value);
    }

    /// <summary>HUDの前回位置 (物理px)。-1は未保存。</summary>
    public int HudX { get; private set; } = -1;

    public int HudY { get; private set; } = -1;

    public void UpdateHudPosition(int x, int y)
    {
        HudX = x;
        HudY = y;
    }

    /// <summary>HUDに出す内容のチェック一覧 (出力ワークスペースで編集)。</summary>
    public ObservableCollection<SukiHudPartOption> HudPartOptions { get; } = [];

    /// <summary>HUDに表示するラン値チップ。HeaderStatusChipsをHUD部品設定でフィルタしたもの。</summary>
    public ObservableCollection<SukiStatusChip> HudChips { get; } = [];

    /// <summary>HUDに表示する所持秘宝。現在ISの選択済み秘宝だけを小窓向けに並べる。</summary>
    public ObservableCollection<SukiChoiceItem> HudRelics { get; } = [];

    public bool IsHudRelicsVisible => IsHudPartEnabled(RhodesHudPartCatalog.RelicsPartId) && HudRelics.Count > 0;

    public bool IsHudTierVisible => IsHudPartEnabled(RhodesHudPartCatalog.TierPartId);

    public bool IsHudApplySummaryVisible => IsHudPartEnabled(RhodesHudPartCatalog.ApplySummaryPartId);

    public bool IsHudConnectionVisible => IsHudPartEnabled(RhodesHudPartCatalog.ConnectionPartId);

    private bool IsHudPartEnabled(string partId)
    {
        var option = HudPartOptions.FirstOrDefault(item => item.Id.Equals(partId, StringComparison.Ordinal));
        return option?.IsEnabled ?? true;
    }

    private void InitializeHudPartOptions(string? enabledCsv)
    {
        ReplaceCollection(HudPartOptions, RhodesHudPartCatalog.Build(enabledCsv, OnHudPartToggled));
        OnHudPartToggled();
    }

    private void OnHudPartToggled()
    {
        RefreshHudChips();
        RefreshHudRelics();
        OnPropertyChanged(nameof(IsHudRelicsVisible));
        OnPropertyChanged(nameof(IsHudTierVisible));
        OnPropertyChanged(nameof(IsHudApplySummaryVisible));
        OnPropertyChanged(nameof(IsHudConnectionVisible));
    }

    private void RefreshHudChips()
    {
        ReplaceCollection(HudChips, HeaderStatusChips.Where(chip => IsHudPartEnabled(chip.Detail)));
    }

    private void RefreshHudRelics()
    {
        ReplaceCollection(
            HudRelics,
            _allRelics
                .Where(item => item.CampaignId == SelectedCampaign?.Id && item.IsSelected && !item.IsExcluded)
                .OrderBy(RhodesRelicUsagePolicy.OwnedDisplayPriority)
                .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.Name, StringComparer.Ordinal));
        OnPropertyChanged(nameof(IsHudRelicsVisible));
    }

    /// <summary>出力ワークスペースの配信サーバー状態表示。</summary>
    public string SidecarServerStatus
    {
        get => _sidecarServerStatus;
        private set => SetProperty(ref _sidecarServerStatus, string.IsNullOrWhiteSpace(value) ? "未確認" : value);
    }

    private Task RefreshNodeRuntimeAsync()
    {
        return RunBusyAsync(() =>
        {
            UpdateNodeRuntimeStatus(_nodeRuntime.Probe());
            StatusMessage = $"Node.js状態: {NodeRuntimeState} / {NodeRuntimeDetail}";
            return Task.CompletedTask;
        });
    }

    private Task InstallNodeRuntimeAsync()
    {
        return RunBusyAsync(async () =>
        {
            StatusMessage = $"Node.js v{RhodesNodeRuntimeManager.NodeVersion}をダウンロード・検証しています (約36MB)。";
            var result = await _nodeRuntime.InstallAsync();
            UpdateNodeRuntimeStatus(result.Status);
            StatusMessage = result.Message;
        });
    }

    private Task UninstallNodeRuntimeAsync()
    {
        return RunBusyAsync(async () =>
        {
            if (_sidecarServer.IsOwnedProcessRunning)
            {
                _sidecarServer.Stop();
                SidecarServerStatus = "停止";
            }

            var result = await _nodeRuntime.UninstallAsync();
            UpdateNodeRuntimeStatus(result.Status);
            StatusMessage = result.Message;
        });
    }

    private void UpdateNodeRuntimeStatus(RhodesNodeRuntimeStatus status)
    {
        _nodeRuntimeStatus = status;
        OnPropertyChanged(nameof(NodeRuntimeState));
        OnPropertyChanged(nameof(NodeRuntimeDetail));
    }

    private async Task StartSidecarServerAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "配信サーバーの状態を確認しています。";
            var before = await RhodesApiStatusProbe.ProbeAsync(RhodesApiUrl);
            if (before.Installed)
            {
                _rhodesApiStatus = before;
                SidecarServerStatus = $"稼働中 (外部起動) · {RhodesApiUrl}";
                StatusMessage = "配信サーバーは既に稼働しています。OBSブラウザソースにOverlayのURLを設定してください。";
                RefreshRuntimeCapabilities();
                return;
            }

            var launch = _sidecarServer.Start(RhodesApiUrl);
            UpdateNodeRuntimeStatus(_nodeRuntime.Probe());
            if (!launch.Succeeded)
            {
                SidecarServerStatus = "起動失敗";
                StatusMessage = launch.Message;
                return;
            }

            StatusMessage = launch.Message;
            await Task.Delay(TimeSpan.FromMilliseconds(1500));
            var after = await RhodesApiStatusProbe.ProbeAsync(RhodesApiUrl);
            _rhodesApiStatus = after;
            if (after.Installed)
            {
                SidecarServerStatus = $"稼働中 (Sukiが管理) · {RhodesApiUrl}";
                StatusMessage = $"配信サーバーが応答しました。Overlay: {RhodesApiUrl}/overlay / Sidecar: {RhodesApiUrl}/sidecar";
            }
            else
            {
                var detail = string.IsNullOrWhiteSpace(_sidecarServer.LastError) ? after.Detail : _sidecarServer.LastError;
                SidecarServerStatus = $"応答なし: {detail}";
                StatusMessage = $"配信サーバーを起動しましたが応答がありません: {detail}";
            }

            RefreshRuntimeCapabilities();
        });
    }

    private async Task StopSidecarServerAsync()
    {
        await RunBusyAsync(() =>
        {
            StatusMessage = _sidecarServer.Stop();
            SidecarServerStatus = _sidecarServer.IsOwnedProcessRunning ? SidecarServerStatus : "停止";
            return Task.CompletedTask;
        });
    }

    private IEnumerable<SukiStatusChip> BuildHeaderStatusChips()
    {
        return RhodesRunFieldRegistry.BuildHeaderStatusChips(_runState);
    }

    private static IEnumerable<SukiRunFieldPreview> BuildRunFieldPreviews(SukiRunStateSnapshot state)
    {
        return RhodesRunFieldRegistry.BuildRunFieldPreviews(state);
    }

    private IEnumerable<SukiRuntimeCapabilityPreview> BuildRuntimeCapabilities()
    {
        return RhodesRuntimeCapabilityRegistry.Build(new SukiRuntimeCapabilityContext(
            string.IsNullOrWhiteSpace(AdbSerial) ? "未選択" : "選択済み",
            $"{SelectedAdbPreset?.Label ?? "自動"} / {AdbHeaderTitle} / {AdbMethodSummary}",
            _maaFrameworkStatus,
            SelectedOcrEngine?.Label ?? MaaOcrStatusState(),
            $"{SelectedOcrEngine?.Id ?? SukiOcrEngineCatalog.DefaultId} / {MaaOcrStatusDetail()}",
            _glmRuntimeStatus,
            _ollamaRuntimeStatus,
            _hypervisorStatus));
    }

    private string MaaOcrStatusState()
    {
        if (_allResourceTasks.Count <= 0)
            return "未生成";

        return RhodesMaaPaths.MissingRecognitionResourceFiles().Count == 0 ? "準備済み" : "認識資産不足";
    }

    private string MaaOcrStatusDetail()
    {
        if (_allResourceTasks.Count <= 0)
            return "tools/generate-maa-resource.mjs でResourceを生成してください。";

        var profile = SelectedResourceProfile?.DisplayName ?? "プロファイル未選択";
        var resourceDetail = RhodesMaaPaths.RecognitionResourceStatusDetail();
        return $"{_allResourceTasks.Count} task / {profile} / {resourceDetail}";
    }

    private bool IsMaaOcrReady()
    {
        return _allResourceTasks.Count > 0 && RhodesMaaPaths.MissingRecognitionResourceFiles().Count == 0;
    }

    private bool EnsureMaaOcrReadyForRecognition()
    {
        if (IsMaaOcrReady())
            return true;

        var detail = _allResourceTasks.Count <= 0
            ? "MAA Resource task が未生成です。"
            : RhodesMaaPaths.RecognitionResourceStatusDetail();
        SessionState = "認識準備不足";
        SessionDetail = detail;
        StatusMessage = $"MAA-OCRを実行できません: {detail}";
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
        return false;
    }

    private void RefreshRuntimeCapabilities()
    {
        ReplaceCollection(RuntimeCapabilities, BuildRuntimeCapabilities());
        RefreshAdbDiagnosticSteps();
    }

    private void RefreshAdbDiagnosticSteps()
    {
        ReplaceCollection(AdbDiagnosticSteps, RhodesAdbDiagnosticsChecklist.Build(new RhodesAdbDiagnosticsInput(
            AdbPath,
            _adbDiagnosticsDetectionAttempted,
            AdbPathCandidates.Any(candidate => candidate.Available),
            AdbDevices.Count,
            AdbDevices.Count(device => device.IsUsable),
            AdbDevices.Count == 0 ? "端末なし" : string.Join(", ", AdbDevices.Select(device => device.Serial)),
            _adbDiagnosticsMaaReady,
            SessionDetail,
            _adbDiagnosticsCaptureSucceeded,
            _adbDiagnosticsCaptureDetail,
            _capturePixelWidth,
            _capturePixelHeight)));
        OnPropertyChanged(nameof(AdbDiagnosticCopyText));
        OnPropertyChanged(nameof(ConnectionStatusLabel));
        OnPropertyChanged(nameof(ConnectionStatusDetail));
        OnPropertyChanged(nameof(ConnectionStatusForeground));
        OnPropertyChanged(nameof(ConnectionStatusBackground));
        OnPropertyChanged(nameof(ConnectionStatusBorder));
    }

    public void MarkAdbDiagnosticsCopied()
    {
        StatusMessage = "ADB診断テキストをコピーしました。";
    }

    private string BuildAdbDiagnosticCopyText()
    {
        return RhodesAdbDiagnosticsChecklist.BuildCopyText(
            new RhodesAdbDiagnosticsCopyInput(
                SelectedCampaign?.DisplayName ?? "",
                SelectedAdbPreset?.DisplayName ?? SelectedAdbPreset?.Label ?? "",
                AdbPath,
                AdbSerial,
                SelectedAdbScreencapMethod?.Label ?? "",
                SelectedAdbScreencapMethod?.Id ?? "",
                SelectedAdbInputMethod?.Label ?? "",
                SelectedAdbInputMethod?.Id ?? "",
                SelectedOcrEngine?.Label ?? "",
                SelectedOcrEngine?.Id ?? "",
                CapturePixelSizeLabel,
                _adbDiagnosticsCaptureDetail,
                _adbDiagnosticsMaaReady is null ? "未確認" : _adbDiagnosticsMaaReady.Value ? "OK" : "失敗",
                SessionDetail,
                AdbDetectionSummary,
                AdbDetectionDetail),
            AdbDiagnosticSteps);
    }

    private void RefreshCampaignPreviews()
    {
        ReplaceCollection(
            CampaignPreviews,
            Campaigns.Select(campaign =>
            {
                var relicCount = _allRelics.Count(item => item.CampaignId == campaign.Id);
                var selectedRelics = _allRelics.Count(item => item.CampaignId == campaign.Id && item.IsSelected);
                return new SukiCampaignWorkspacePreview(
                    campaign.Id,
                    campaign.DisplayName,
                    $"{campaign.FullTitle} / 秘宝{selectedRelics}/{relicCount}",
                    string.Equals(campaign.Id, _runState.CampaignId, StringComparison.Ordinal),
                    string.Equals(campaign.Id, SelectedCampaign?.Id, StringComparison.Ordinal));
            }));
    }

    private void RefreshSpecialValuePreviews()
    {
        ReplaceCollection(SpecialValuePreviews, BuildSpecialValuePreviews());
    }

    private void RefreshRunStatePreviews()
    {
        ReplaceCollection(HeaderStatusChips, BuildHeaderStatusChips());
        RefreshHudChips();
        RefreshHudRelics();
        ReplaceCollection(RunFieldPreviews, BuildRunFieldPreviews(_runState));
        RefreshSpecialValuePreviews();
        RefreshCampaignPreviews();
        RefreshManualRunEditors();
        OnPropertyChanged(nameof(CampaignHeaderDetail));
        OnPropertyChanged(nameof(RunContextSummary));
        RefreshInspectorRows();
    }

    private void RefreshChoicesFromRunState(SukiRunStateSnapshot state)
    {
        foreach (var item in _allOperators)
            item.IsSelected = state.SelectedOperatorIds.Contains(item.Id);
        foreach (var item in _allRelics)
        {
            item.IsSelected = state.SelectedRelicIds.Contains(item.Id);
            item.IsUsed = item.IsSelected && state.UsedRelicIds.Contains(item.Id);
        }
        RefreshChoiceLists();
    }

    private void ReloadRunStateFromStore()
    {
        _runState = RhodesPublicDebugPolicy.ApplyCampaign(
            RhodesRunCatalog.LoadDefault().Current,
            _distributionProfile);
        var campaign = Campaigns.FirstOrDefault(item => string.Equals(item.Id, _runState.CampaignId, StringComparison.Ordinal));
        if (campaign is not null && !string.Equals(campaign.Id, SelectedCampaign?.Id, StringComparison.Ordinal))
            SelectedCampaign = campaign;
        var engine = OcrEngineOptions.FirstOrDefault(item => string.Equals(item.Id, _runState.OcrEngine, StringComparison.Ordinal));
        if (engine is not null)
            SelectedOcrEngine = engine;
        RefreshChoicesFromRunState(_runState);
        RefreshRunStatePreviews();
    }

    private IEnumerable<SukiSpecialValuePreview> BuildSpecialValuePreviews()
    {
        var campaignId = SelectedCampaign?.Id ?? _runState.CampaignId;
        if (campaignId == "is2_phantom")
        {
            yield return new SukiSpecialValuePreview(
                "演目",
                string.IsNullOrWhiteSpace(_runState.Performance) ? "演目なし" : _runState.Performance,
                "手動入力",
                "",
                string.IsNullOrWhiteSpace(_runState.PerformanceId) ? "取得値なし" : _runState.PerformanceId);
        }
        var specialFields = (_runState.SpecialFields ?? Array.Empty<SukiSpecialFieldState>())
            .Where(field => string.Equals(field.CampaignId, campaignId, StringComparison.Ordinal))
            .ToArray();
        if (specialFields.Length > 0)
        {
            foreach (var field in specialFields)
            {
                yield return new SukiSpecialValuePreview(field.Label, field.Value, field.Kind, field.ProfileId, field.Detail);
            }
            yield break;
        }

        yield return new SukiSpecialValuePreview("固有値", "未定義", "キャンペーン", "campaign.special", "このISの固有値定義を追加してください");
    }

    private void RefreshInspectorRows()
    {
        ReplaceCollection(InspectorRows, BuildInspectorRows());
    }

    private void RefreshWorkspaceActions()
    {
        ReplaceCollection(
            WorkspaceActions,
            RhodesWorkspaceActionRegistry.ForWorkspace(WorkspaceTab).Select(CreateWorkspaceActionPreview));
        OnPropertyChanged(nameof(WorkspaceActionCountLabel));
    }

    private SukiWorkspaceActionPreview CreateWorkspaceActionPreview(SukiWorkspaceActionDescriptor descriptor)
    {
        var commandSpec = RhodesWorkspaceActionRegistry.ParseCommandName(descriptor.CommandName);
        var command = commandSpec.CommandName switch
        {
            nameof(SyncRunStateFromApiCommand) => SyncRunStateFromApiCommand,
            nameof(OpenRecognitionProfileCommand) => OpenRecognitionProfileCommand,
            nameof(RunProfileRecognitionAndApplyCommand) => RunProfileRecognitionAndApplyCommand,
            nameof(ClearVisibleChoicesCommand) => ClearVisibleChoicesCommand,
            nameof(RunSelectedProfileRecognitionCommand) => RunSelectedProfileRecognitionCommand,
            nameof(RunSelectedProfileRecognitionAndApplyCommand) => RunSelectedProfileRecognitionAndApplyCommand,
            nameof(ApplyCandidateResultsCommand) => ApplyCandidateResultsCommand,
            nameof(RefreshRecognitionScanHistoryCommand) => RefreshRecognitionScanHistoryCommand,
            nameof(OpenPreviewUrlCommand) => OpenPreviewUrlCommand,
            nameof(SaveSettingsCommand) => SaveSettingsCommand,
            nameof(RefreshAdbDevicesCommand) => RefreshAdbDevicesCommand,
            nameof(RunAdbConnectionTestCommand) => RunAdbConnectionTestCommand,
            nameof(ConnectCommand) => ConnectCommand,
            nameof(ConnectAndCaptureCommand) => ConnectAndCaptureCommand,
            nameof(CaptureCommand) => CaptureCommand,
            nameof(RefreshOptionalRuntimesCommand) => RefreshOptionalRuntimesCommand,
            nameof(RegenerateMaaResourceCommand) => RegenerateMaaResourceCommand,
            _ => null,
        };

        return new SukiWorkspaceActionPreview(descriptor, command, commandSpec.CommandParameter);
    }

    private IEnumerable<SukiInspectorRow> BuildInspectorRows()
    {
        yield return new SukiInspectorRow("ワークスペース", WorkspaceTitle, WorkspaceTab);
        yield return new SukiInspectorRow("IS", CampaignHeaderTitle, CampaignHeaderDetail);
        yield return BuildProductSurfaceInspectorRow();
        yield return BuildWorkspaceActionInspectorRow();

        if (WorkspaceTab == "run")
        {
            yield return new SukiInspectorRow("共通取得値", $"{RunFieldPreviews.Count}項目", "runStatusFull");
            yield break;
        }

        if (WorkspaceTab == "special")
        {
            yield return new SukiInspectorRow("固有値", $"{SpecialValuePreviews.Count}項目", SelectedCampaign?.Id ?? "");
            yield break;
        }

        if (WorkspaceTab == "choices")
        {
            yield return new SukiInspectorRow("オペレーター", OperatorListSummary, "選択 / 除外 / 優先表示");
            yield return new SukiInspectorRow("秘宝", RelicListSummary, "IS別秘宝カタログ");
            yield break;
        }

        if (WorkspaceTab == "recognition")
        {
            var plan = CurrentResourceExecutionPlan;
            yield return new SukiInspectorRow("認識プロファイル", SelectedResourceProfile?.DisplayName ?? "-", SelectedResourceProfile?.ProfileSummary ?? "");
            yield return new SukiInspectorRow("実行計画", $"{plan.StateLabel} / {plan.Tasks.Count} task", plan.Summary);
            yield return new SukiInspectorRow(
                "履歴",
                $"{RecognitionScanHistory.Count}件",
                RecognitionScanHistory.FirstOrDefault()?.Detail ?? RhodesSukiDebugPaths.RecognitionScansDirectory);
            yield return new SukiInspectorRow("候補", $"{CandidateResults.Count}件", ResourceTaskDiagnostics.Summary);
            yield return new SukiInspectorRow("適用", LastCandidateApplySummary, "data/current-state.json");
            yield return new SukiInspectorRow(
                "ROIドラフト",
                string.IsNullOrWhiteSpace(LastRoiDraftPath) ? SelectedRoiEditDraft.StatusLabel : LastRoiDraftPath,
                $"{SelectedRoiEditDraft.Detail} / {RoiDraftApplyResult.Message}");
            yield return new SukiInspectorRow(
                "ROIセッション",
                string.IsNullOrWhiteSpace(LastRoiSessionPath) ? $"{RoiBatchDrafts.Count}候補" : LastRoiSessionPath,
                $"{RoiBatchApplyResult.Summary} / 保存{RoiAdjustmentSessions.Count}件");
            yield return new SukiInspectorRow("ROI比較", RoiRescanComparisonSummary, RoiRescanComparisonEvidenceSummary);
            yield return new SukiInspectorRow("MAA契約", MaaResourceContract.Summary, MaaResourceContract.Detail);
            yield return new SukiInspectorRow("MAA Resource", MaaResourceGenerationResult.Message, MaaResourceGenerationResult.OutputPath);
            yield return new SukiInspectorRow(
                "結果JSON",
                string.IsNullOrWhiteSpace(LastResourceTaskResultsPath) ? "-" : LastResourceTaskResultsPath,
                "RHODES OBS COMMANDER3373 Debug Logs");
            yield break;
        }

        if (WorkspaceTab == "output")
        {
            var visible = OutputParts.Count(part => part.Enabled);
            yield return new SukiInspectorRow("表示部品", $"{visible}/{OutputParts.Count}", $"scroll {OutputScrollSpeed}px/s");
            yield return new SukiInspectorRow("別ウィンドウ", OutputSeparateWindow ? "ON" : "OFF", "OBSサイドカー");
            yield return new SukiInspectorRow("大会向け", OutputTournamentMode ? "ON" : "OFF", "表示情報を絞る");
            yield break;
        }

        if (WorkspaceTab == "runtime")
        {
            yield return new SukiInspectorRow("ADB", AdbHeaderTitle, AdbHeaderDetail);
            yield return new SukiInspectorRow("撮影方式", SelectedAdbScreencapMethod?.Label ?? "標準撮影", SelectedAdbScreencapMethod?.Detail ?? "");
            yield return new SukiInspectorRow("入力方式", SelectedAdbInputMethod?.Label ?? "標準入力", SelectedAdbInputMethod?.Detail ?? "");
            yield return new SukiInspectorRow("端末", $"{AdbDevices.Count}件", SessionState);
            yield return new SukiInspectorRow("Hyper-V", _hypervisorStatus.State, _hypervisorStatus.Detail);
            yield return new SukiInspectorRow("OCRエンジン", SelectedOcrEngine?.Label ?? "MAA-OCR", SelectedOcrEngine?.Id ?? SukiOcrEngineCatalog.DefaultId);
            yield return new SukiInspectorRow("任意OCR", $"GLM={_glmRuntimeStatus.State} / Ollama={_ollamaRuntimeStatus.State}", "必要な検証環境だけで導入");
            yield break;
        }

        yield return new SukiInspectorRow("ログ", LastCapturePath, CaptureState);
        yield return new SukiInspectorRow("API", RhodesApiUrl, "候補化API");
    }

    private SukiInspectorRow BuildProductSurfaceInspectorRow()
    {
        var surfaces = RhodesProductSurfaceRegistry.ForWorkspace(WorkspaceTab);
        var categories = surfaces
            .Select(surface => surface.Category)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var outputCount = surfaces.Count(surface => surface.CanShowOnOutput);
        var detail = categories.Length == 0
            ? "未登録"
            : $"{string.Join(" / ", categories)} / OBS={outputCount}";
        return new SukiInspectorRow("サーフェス", $"{surfaces.Count}件", detail);
    }

    private SukiInspectorRow BuildWorkspaceActionInspectorRow()
    {
        var actions = WorkspaceActions;
        if (actions.Count == 0)
            return new SukiInspectorRow("操作", "未登録", "ワークスペース操作定義なし");

        var requiresMaaCount = actions.Count(action => action.RequiresMaaSession);
        var writesStateCount = actions.Count(action => action.WritesState);
        var workflows = actions
            .Select(action => action.Workflow)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        return new SukiInspectorRow(
            "操作",
            $"{actions.Count}件",
            $"MAA={requiresMaaCount} / state更新={writesStateCount} / {string.Join(" / ", workflows)}");
    }

    private async Task ConnectAsync()
    {
        await RunBusyAsync(async () =>
        {
            await EnsureMaaControllerReadyAsync(forceReconnect: true);
        });
    }

    private async Task ConnectAndCaptureAsync()
    {
        await RunBusyAsync(ConnectAndCaptureCoreAsync);
    }

    private void ApplyMaaSessionSnapshot(MaaSessionSnapshot snapshot, string statusMessage)
    {
        _adbDiagnosticsMaaReady = snapshot.IsReady;
        SessionState = snapshot.State;
        SessionDetail = snapshot.Detail;
        StatusMessage = statusMessage;
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
    }

    private void LoadSettings()
    {
        var settings = RhodesSukiSettingsStore.Load();
        AdbPath = string.IsNullOrWhiteSpace(settings.AdbPath) ? "adb" : settings.AdbPath;
        AdbSerial = settings.AdbSerial;
        AdbConfigJson = string.IsNullOrWhiteSpace(settings.AdbConfigJson) ? "{}" : settings.AdbConfigJson;
        RhodesApiUrl = string.IsNullOrWhiteSpace(settings.RhodesApiUrl) ? "http://127.0.0.1:5173" : settings.RhodesApiUrl;
        SelectedAdbPreset = AdbPresets.FirstOrDefault(preset => preset.Id == settings.SelectedAdbPresetId) ?? SelectedAdbPreset;
        SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile =>
            RhodesPublicDebugPolicy.IsProfileAllowed(settings.SelectedResourceProfileId, _distributionProfile)
            && profile.Id == settings.SelectedResourceProfileId) ?? SelectedResourceProfile;
        SelectedAdbInputMethod = SukiAdbMethodCatalog.FindInput(settings.AdbInputMethodId);
        SelectedAdbScreencapMethod = SukiAdbMethodCatalog.FindScreencap(settings.AdbScreencapMethodId);
        HudX = settings.HudX;
        HudY = settings.HudY;
        InitializeHudPartOptions(settings.HudVisibleParts);
        IsHudVisible = settings.HudVisible;
        _adbConnectionValidated = settings.AdbConnectionValidated;
        ApplyOverlayLayoutStates(settings.OverlayLayout);
    }

    private async Task SaveSettingsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var adbConfigJson = SukiAdbConfigJson.Normalize(AdbConfigJson);
            AdbConfigJson = adbConfigJson;
            await RhodesSukiSettingsStore.SaveAsync(BuildCurrentSettings(adbConfigJson));
            var apiError = await SaveAdbSettingsToApiStateAsync();
            StatusMessage = string.IsNullOrWhiteSpace(apiError)
                ? $"Suki設定とADB API設定を保存しました: {RhodesSukiSettingsStore.DefaultPath}"
                : $"Suki設定を保存しました。ADB API設定の反映は失敗: {apiError}";
        });
    }

    private RhodesSukiSettings BuildCurrentSettings(string? adbConfigJson = null)
    {
        return new RhodesSukiSettings(
            AdbPath,
            AdbSerial,
            string.IsNullOrWhiteSpace(adbConfigJson) ? SukiAdbConfigJson.Normalize(AdbConfigJson) : adbConfigJson,
            RhodesApiUrl,
            SelectedAdbPreset?.Id ?? "auto",
            SelectedResourceProfile?.Id ?? "runStatusFull",
            SelectedAdbInputMethod?.Id ?? SukiAdbMethodCatalog.DefaultInputMethodId,
            SelectedAdbScreencapMethod?.Id ?? SukiAdbMethodCatalog.DefaultScreencapMethodId,
            IsHudVisible,
            HudX,
            HudY,
            RhodesHudPartCatalog.SerializeEnabledIds(HudPartOptions),
            _adbConnectionValidated,
            OverlayLayoutItems.Select(item => item.ToState()).ToArray());
    }

    private async Task<string> SaveAdbSettingsToApiStateAsync()
    {
        var choiceOptions = BuildChoicePersistenceOptions();
        var result = await RhodesSukiStateSyncWorkflow.SyncSettingsAsync(
            new RhodesSukiStateSyncRequest(
                _allOperators,
                _allRelics,
                choiceOptions,
                new RhodesAdbApiSettings(
                    true,
                    SelectedAdbPreset?.Id ?? "auto",
                    AdbPath,
                    AdbSerial),
                BuildOutputPreferences(),
                SelectedOcrEngine?.Id ?? _runState.OcrEngine),
            cancellationToken => RhodesStateApiClient.FetchAsync(RhodesApiUrl, cancellationToken: cancellationToken),
            (stateJson, cancellationToken) => RhodesStateApiClient.SaveAsync(RhodesApiUrl, stateJson, cancellationToken: cancellationToken),
            (stateJson, _) => RhodesRunStateStore.ReplaceStateJsonAsync(stateJson));

        _rhodesApiStatus = result.ApiStatus;
        if (result.ShouldReloadRunState)
            ReloadRunStateFromStore();
        RefreshRuntimeCapabilities();
        return result.Error;
    }

    private async Task<string> SaveRunContextToApiStateAsync(string campaignId)
    {
        var result = await RhodesSukiStateSyncWorkflow.SyncRunContextAsync(
            campaignId,
            cancellationToken => RhodesStateApiClient.FetchAsync(RhodesApiUrl, cancellationToken: cancellationToken),
            (stateJson, cancellationToken) => RhodesStateApiClient.SaveAsync(RhodesApiUrl, stateJson, cancellationToken: cancellationToken),
            (stateJson, _) => RhodesRunStateStore.ReplaceStateJsonAsync(stateJson));

        _rhodesApiStatus = result.ApiStatus;
        RefreshRuntimeCapabilities();
        return result.Error;
    }

    private Task SetWorkspaceAsync(object? parameter)
    {
        var tab = parameter as string;
        WorkspaceTab = RhodesWorkspaceRegistry.Normalize(tab);
        StatusMessage = $"{WorkspaceTitle}を表示しています。";
        return Task.CompletedTask;
    }

    private Task SetChoiceTabAsync(object? parameter)
    {
        var tab = parameter as string;
        ChoiceTab = tab is "operators" or "relics" or "recognition" ? tab : "operators";
        WorkspaceTab = ChoiceTab == "recognition" ? "recognition" : "choices";
        StatusMessage = $"{ChoicePanelTitle}を表示しています。";
        return Task.CompletedTask;
    }

    private Task OpenRecognitionProfileAsync(object? parameter)
    {
        var profileId = parameter as string;
        if (!string.IsNullOrWhiteSpace(profileId))
        {
            var profile = ResourceProfiles.FirstOrDefault(item => string.Equals(item.Id, profileId, StringComparison.Ordinal));
            if (profile is not null)
            {
                SelectedResourceProfile = profile;
            }
            else
            {
                ChoiceTab = "recognition";
                WorkspaceTab = "recognition";
                StatusMessage = $"認識プロファイルが未定義です: {profileId}";
                return Task.CompletedTask;
            }
        }

        ChoiceTab = "recognition";
        WorkspaceTab = "recognition";
        StatusMessage = $"{SelectedResourceProfile?.DisplayName ?? "認識"}を表示しています。";
        return Task.CompletedTask;
    }

    private async Task SetCurrentCampaignAsync(object? parameter)
    {
        var campaignId = parameter switch
        {
            SukiCampaignWorkspacePreview preview => preview.Id,
            SukiCampaignPreview campaign => campaign.Id,
            string id => id,
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(campaignId))
            return;
        if (!RhodesPublicDebugPolicy.IsCampaignAllowed(campaignId, _distributionProfile))
        {
            StatusMessage = "公開デバッグではIS#5 サルカズの炉辺奇談のみを対象にします。";
            return;
        }

        if (string.Equals(campaignId, _runState.CampaignId, StringComparison.Ordinal))
        {
            StatusMessage = "このISは既に現在ランです。";
            return;
        }

        await RunBusyAsync(async () =>
        {
            var apiError = await SaveRunContextToApiStateAsync(campaignId);
            if (!string.IsNullOrWhiteSpace(apiError))
                await RhodesRunStateStore.SaveRunContextAsync(campaignId);

            ReloadRunStateFromStore();
            var campaign = Campaigns.FirstOrDefault(item => string.Equals(item.Id, _runState.CampaignId, StringComparison.Ordinal));
            RefreshRunStatePreviews();
            StatusMessage = string.IsNullOrWhiteSpace(apiError)
                ? $"{campaign?.DisplayName ?? campaignId} を現在ランに設定し、APIへ同期しました。"
                : $"{campaign?.DisplayName ?? campaignId} を現在ランに設定しました。API同期は失敗: {apiError}";
        });
    }

    private Task ToggleChoiceSelectedAsync(object? parameter)
    {
        if (parameter is not SukiChoiceItem item)
            return Task.CompletedTask;

        item.IsSelected = !item.IsSelected;
        if (item.IsSelected)
            item.IsExcluded = false;
        else if (string.Equals(item.Kind, "relic", StringComparison.Ordinal))
            item.IsUsed = false;
        if (string.Equals(item.Kind, "relic", StringComparison.Ordinal) && item.SupportsUsedFlag)
            RefreshRelicChoices();
        else
            RefreshChoiceAfterSelectionMutation(item.Kind);
        PersistChoiceStateInBackground();
        StatusMessage = $"{item.Name}: {(item.IsSelected ? "選択しました。" : "選択を解除しました。")}";
        return Task.CompletedTask;
    }

    private Task ToggleRelicUsedAsync(object? parameter)
    {
        if (parameter is not SukiChoiceItem { Kind: "relic", SupportsUsedFlag: true } item)
            return Task.CompletedTask;

        if (!item.IsSelected)
        {
            StatusMessage = $"{item.Name}: 所持中の秘宝だけ使用状態を変更できます。";
            return Task.CompletedTask;
        }

        item.IsUsed = !item.IsUsed;
        RefreshRelicSummaries();
        PersistChoiceStateInBackground();
        StatusMessage = $"{item.Name}: {(item.IsUsed ? "使用済みにしました。" : "未使用に戻しました。")}";
        return Task.CompletedTask;
    }

    private Task ToggleChoiceExcludedAsync(object? parameter)
    {
        if (parameter is not SukiChoiceItem item)
            return Task.CompletedTask;

        item.IsExcluded = !item.IsExcluded;
        if (item.IsExcluded)
        {
            item.IsSelected = false;
            if (string.Equals(item.Kind, "relic", StringComparison.Ordinal))
                item.IsUsed = false;
        }
        RefreshChoiceAfterExclusionMutation(item.Kind);
        PersistChoiceStateInBackground();
        StatusMessage = $"{item.Name}: {(item.IsExcluded ? "表示除外にしました。" : "表示除外を解除しました。")}";
        return Task.CompletedTask;
    }

    private Task ClearVisibleChoicesAsync()
    {
        if (ChoiceTab == "recognition")
        {
            StatusMessage = "認識タスクには手動選択がありません。";
            return Task.CompletedTask;
        }

        var target = ChoiceTab == "relics" ? FilteredRelics : FilteredOperators;
        foreach (var item in target)
        {
            item.IsSelected = false;
            if (string.Equals(item.Kind, "relic", StringComparison.Ordinal))
                item.IsUsed = false;
        }

        RefreshChoiceAfterBulkMutation(ChoiceTab == "relics" ? "relic" : "operator");
        PersistChoiceStateInBackground();
        StatusMessage = $"{ChoicePanelTitle}の表示中選択を解除しました。";
        return Task.CompletedTask;
    }

    private Task ApplyAdbPresetAsync(MaaAdbPresetPreview? preset)
    {
        if (preset is null)
        {
            StatusMessage = "ADBプリセットが選択されていません。";
            return Task.CompletedTask;
        }

        if (!string.IsNullOrWhiteSpace(preset.AdbPath))
            AdbPath = preset.AdbPath;

        SelectedAdbPreset = preset;
        AdbSerial = preset.Serial;
        ApplyAdbMethodsForPreset(preset.Id);
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
        StatusMessage = $"ADBプリセットを適用しました: {preset.DisplayName} / {AdbMethodSummary}";
        return Task.CompletedTask;
    }

    private Task ApplyAdbPathCandidateAsync(MaaAdbPathCandidatePreview? candidate)
    {
        if (candidate is null)
        {
            StatusMessage = "ADBパス候補が選択されていません。";
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(candidate.Path))
        {
            StatusMessage = "ADBパス候補にパスがありません。";
            return Task.CompletedTask;
        }

        AdbPath = candidate.Path;
        SelectedAdbPathCandidate = candidate;
        if (!string.IsNullOrWhiteSpace(candidate.Preset))
        {
            SelectedAdbPreset = AdbPresets.FirstOrDefault(preset =>
                preset.Id.Equals(candidate.Preset, StringComparison.OrdinalIgnoreCase)) ?? SelectedAdbPreset;
            ApplyAdbMethodsForPreset(candidate.Preset);
        }
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
        StatusMessage = $"ADBパスを適用しました: {candidate.Path} / {AdbMethodSummary}";
        return Task.CompletedTask;
    }

    public void SetManualAdbPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        AdbPath = path.Trim();
        SelectedAdbPreset = AdbPresets.FirstOrDefault(preset => preset.Id == "custom") ?? SelectedAdbPreset;
        ApplyAdbMethodsForPreset("custom");
        var manual = new MaaAdbPathCandidatePreview(AdbPath, "manual", "custom", File.Exists(AdbPath), File.Exists(AdbPath), "");
        UpsertAdbPathCandidate(manual);
        SelectedAdbPathCandidate = manual;
        AdbDetectionSummary = "手動ADBパスを選択";
        AdbDetectionDetail = AdbPath;
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
        StatusMessage = $"ADBパスを手動選択しました: {AdbPath}";
    }

    private async Task RefreshAdbDevicesAsync()
    {
        await RunBusyAsync(DetectAdbLocallyCoreAsync);
    }

    private async Task DetectAdbLocallyCoreAsync()
    {
        ReplaceAdbDevices([]);
        AdbDetectionSummary = "ADB自動検出中";
        AdbDetectionDetail = $"{SelectedAdbPreset?.Label ?? "手動"} / {AdbPath}";
        var snapshot = await RhodesSukiAdbDetectionWorkflow.DetectAsync(
            new RhodesAdbApiSettings(
                true,
                SelectedAdbPreset?.Id ?? "auto",
                AdbPath,
                AdbSerial));

        AdbPath = snapshot.AdbPath;
        AdbSerial = snapshot.AdbSerial;
        ReplaceAdbPathCandidates(snapshot.AdbCandidates);
        SelectedAdbPathCandidate = snapshot.SelectedAdbPathCandidate;
        ApplyDetectedAdbPreset(snapshot.SelectedAdbPathCandidate);
        ReplaceAdbDevices(snapshot.Devices);
        AdbDetectionSummary = snapshot.DetectionSummary;
        AdbDetectionDetail = snapshot.DetectionDetail;
        SessionState = snapshot.SessionState;
        SessionDetail = snapshot.SessionDetail;
        StatusMessage = snapshot.StatusMessage;
        _adbDiagnosticsDetectionAttempted = true;
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
    }

    private void ReplaceAdbPathCandidates(IEnumerable<MaaAdbPathCandidatePreview> candidates)
    {
        ReplaceCollection(AdbPathCandidates, RhodesAdbCandidateRegistry.Normalize(candidates));
        SelectedAdbPathCandidate = RhodesAdbCandidateRegistry.SelectDefault(AdbPathCandidates, AdbPath);
        OnPropertyChanged(nameof(AdbPathCandidateSummary));
        OnPropertyChanged(nameof(HasAdbPathCandidates));
        OnPropertyChanged(nameof(IsAdbPathCandidateListEmpty));
    }

    private void UpsertAdbPathCandidate(MaaAdbPathCandidatePreview candidate)
    {
        ReplaceAdbPathCandidates(RhodesAdbCandidateRegistry.Merge(AdbPathCandidates, [candidate]));
    }

    private void ReplaceAdbDevices(IEnumerable<MaaAdbDevicePreview> devices)
    {
        ReplaceCollection(AdbDevices, devices);
        OnPropertyChanged(nameof(AdbDeviceSummary));
        OnPropertyChanged(nameof(HasAdbDevices));
        OnPropertyChanged(nameof(IsAdbDeviceListEmpty));
    }

    private void ApplyAdbMethodsForPreset(string? presetId)
    {
        SelectedAdbInputMethod = SukiAdbMethodCatalog.FindInput(
            SukiAdbMethodCatalog.DefaultInputMethodIdForPreset(presetId));
        SelectedAdbScreencapMethod = SukiAdbMethodCatalog.FindScreencap(
            SukiAdbMethodCatalog.DefaultScreencapMethodIdForPreset(presetId));
    }

    private void ApplyDetectedAdbPreset(MaaAdbPathCandidatePreview? selectedCandidate)
    {
        var detectedPresetId = RhodesSukiAdbDetectionWorkflow.ResolveDetectedPresetId(
            SelectedAdbPreset?.Id,
            selectedCandidate);
        if (string.Equals(SelectedAdbPreset?.Id, detectedPresetId, StringComparison.OrdinalIgnoreCase))
            return;

        var detectedPreset = AdbPresets.FirstOrDefault(preset =>
            preset.Id.Equals(detectedPresetId, StringComparison.OrdinalIgnoreCase));
        if (detectedPreset is null)
            return;

        SelectedAdbPreset = detectedPreset;
        ApplyAdbMethodsForPreset(detectedPresetId);
    }

    private async Task RefreshOptionalRuntimesAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "ランタイム状態を確認しています。";
            var snapshot = await RhodesSukiRuntimeProbeWorkflow.ProbeAsync(
                cancellationToken => RhodesApiStatusProbe.ProbeAsync(RhodesApiUrl, cancellationToken: cancellationToken),
                cancellationToken => RhodesApiStatusProbe.ProbeMasterAsync(RhodesApiUrl, Campaigns.Count, _allOperators.Count, _allRelics.Count, cancellationToken: cancellationToken),
                cancellationToken => RhodesOptionalRuntimeProbe.ProbeAsync(RhodesApiUrl),
                cancellationToken => RhodesHypervisorProbe.ProbeAsync(RhodesApiUrl));
            _rhodesApiStatus = snapshot.Api;
            _masterDataStatus = snapshot.Master;
            _glmRuntimeStatus = snapshot.Glm;
            _ollamaRuntimeStatus = snapshot.Ollama;
            _hypervisorStatus = snapshot.Hypervisor;
            SidecarServerStatus = snapshot.Api.Installed
                ? (_sidecarServer.IsOwnedProcessRunning ? $"稼働中 (Sukiが管理) · {RhodesApiUrl}" : $"稼働中 (外部起動) · {RhodesApiUrl}")
                : "停止または未起動";
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
            StatusMessage = snapshot.StatusMessage;
        });
    }

    private async Task RunAdbConnectionTestAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "ADB接続テストを実行しています。";
            await DetectAdbLocallyCoreAsync();
            if (AdbPathCandidates.All(candidate => !candidate.Available))
            {
                ApplyAdbConnectionTestSnapshot(RhodesSukiAdbConnectionTestWorkflow.NoAvailableAdb(AdbDetectionDetail));
                return;
            }

            await ConnectAndCaptureCoreAsync();
        });
    }

    private async Task ConnectAndCaptureCoreAsync()
    {
        StatusMessage = "MAA Controller でADBへ接続しています。";
        var snapshot = await _session.InitializeAdbAsync(BuildSessionOptions());
        _adbDiagnosticsMaaReady = snapshot.IsReady;
        ApplyAdbConnectionTestSnapshot(RhodesSukiAdbConnectionTestWorkflow.FromController(snapshot));
        if (!snapshot.IsReady)
            return;

        StatusMessage = "ADB接続を確認しました。スクリーンショットを取得しています。";
        var capture = await CaptureCoreAsync();
        ApplyAdbConnectionTestSnapshot(
            RhodesSukiAdbConnectionTestWorkflow.FromCapture(capture, AdbSerial, CapturePixelSizeLabel, snapshot.Detail));
    }

    private void ApplyAdbConnectionTestSnapshot(RhodesSukiAdbConnectionTestSnapshot snapshot)
    {
        SessionState = snapshot.SessionState;
        SessionDetail = snapshot.SessionDetail;
        StatusMessage = snapshot.StatusMessage;
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
    }

    private async Task RunOptionalRuntimeActionAsync(
        string label,
        Func<string, HttpClient?, Task<SukiOptionalRuntimeActionResult>> action,
        Action<SukiOptionalRuntimeStatus> applyStatus)
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = $"{label}を実行しています。";
            var snapshot = await RhodesSukiOptionalRuntimeActionWorkflow.RunAsync(label, action, RhodesApiUrl);
            applyStatus(snapshot.RuntimeStatus);
            _rhodesApiStatus = snapshot.ApiStatus;
            RefreshRuntimeCapabilities();
            RefreshInspectorRows();
            StatusMessage = snapshot.StatusMessage;
        });
    }

    private Task ApplyAdbDeviceAsync(MaaAdbDevicePreview? device)
    {
        if (device is null)
        {
            StatusMessage = "ADB端末が選択されていません。";
            return Task.CompletedTask;
        }

        AdbSerial = device.Serial;
        RefreshRuntimeCapabilities();
        RefreshInspectorRows();
        StatusMessage = $"ADB serialを適用しました: {device.Serial}";
        return Task.CompletedTask;
    }

    private async Task CaptureAsync()
    {
        await RunBusyAsync(async () =>
        {
            var capture = await CaptureCoreAsync();
            if (capture?.Succeeded == true)
            {
                StatusMessage = "スクリーンショットを取得しました。";
            }
        });
    }

    private async Task RunAllProbesAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!await EnsureCaptureAsync())
                return;

            ProbeResults.Clear();
            foreach (var payload in ProbePayloads)
            {
                await RunProbeCoreAsync(payload);
            }
            RefreshInspectorRows();
        });
    }

    private async Task RunAllResourceTasksAsync()
    {
        await RunBusyAsync(async () => { await RunAllResourceTasksCoreAsync(); });
    }

    private async Task ExportResourceTaskResultsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var path = await SaveResourceTaskResultsAsync(
                ResourceTaskResults,
                SelectedResourceProfile?.Id,
                CandidateResults,
                executionPlan: CurrentResourceExecutionPlan);
            LastResourceTaskResultsPath = path;
            StatusMessage = $"MAA scan証跡を保存しました: {path}";
        });
    }

    private async Task ExportSelectedRoiDraftAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!SelectedRoiEditDraft.HasSelection)
            {
                StatusMessage = "ROI行を選択してください。";
                return;
            }

            LastRoiDraftPath = await RhodesMaaRoiEditDraftLog.SaveAsync(
                SelectedRoiEditDraft,
                SelectedResourceProfile?.Id,
                RhodesSukiDebugPaths.RoiDraftsDirectory);
            StatusMessage = $"ROIドラフトを保存しました: {LastRoiDraftPath}";
        });
    }

    private async Task ExportRoiAdjustmentSessionAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (RoiBatchDrafts.Count == 0)
            {
                StatusMessage = "保存できるROI調整候補がありません。";
                return;
            }

            LastRoiSessionPath = await RhodesMaaRoiAdjustmentSessionLog.SaveAsync(
                RoiBatchDrafts,
                SelectedResourceProfile?.Id,
                LastResourceTaskResultsPath,
                LastCapturePath,
                RoiBatchApplyResult,
                RhodesSukiDebugPaths.RoiSessionsDirectory,
                comparisonSummary: RoiRescanComparisonSummary,
                comparisonRows: RoiRescanComparisonRows,
                comparisonBeforeLogPath: _lastRoiRescanBeforePath,
                comparisonAfterLogPath: _lastRoiRescanAfterPath);
            RefreshRoiAdjustmentSessions();
            StatusMessage = $"ROI調整セッションを保存しました: {LastRoiSessionPath}";
        });
    }

    private async Task CreateBugReportBundleAsync()
    {
        await RunBusyAsync(async () =>
        {
            var adbConfigJson = SukiAdbConfigJson.Normalize(AdbConfigJson);
            AdbConfigJson = adbConfigJson;
            await RhodesSukiSettingsStore.SaveAsync(BuildCurrentSettings(adbConfigJson));

            var result = await RhodesBugReportBundle.CreateAsync(new RhodesBugReportBundleRequest
            {
                DebugLogDirectory = RhodesSukiDebugPaths.DebugLogDirectory,
                DestinationDirectory = RhodesSukiDebugPaths.BugReportsDirectory,
                StatePath = RhodesRunStateStore.ResolveDefaultStatePath(),
                SettingsPath = RhodesSukiSettingsStore.DefaultPath,
                LatestCapturePath = LastCapturePath,
                LatestRecognitionLogPath = LastResourceTaskResultsPath,
                Metadata = BuildBugReportMetadata(),
            });

            LastBugReportBundlePath = result.ZipPath;
            BugReportBundleStatus = result.Success
                ? $"{result.IncludedEntries.Count}件 / {result.ZipBytes:n0} bytes / {result.ZipPath}"
                : result.Message;
            StatusMessage = result.Message;
            DebugLogLines.Insert(0, result.Success
                ? $"Bug report ZIP: {result.ZipPath}"
                : $"Bug report ZIP failed: {result.Message}");
        });
    }

    private IReadOnlyDictionary<string, string> BuildBugReportMetadata()
    {
        var plan = CurrentResourceExecutionPlan;
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["appVersion"] = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "",
            ["os"] = RuntimeInformation.OSDescription,
            ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
            ["baseResolution"] = BaseResolution.AspectRatioLabel,
            ["campaignId"] = SelectedCampaign?.Id ?? "",
            ["campaignLabel"] = SelectedCampaign?.DisplayName ?? "",
            ["profileId"] = SelectedResourceProfile?.Id ?? "",
            ["profileLabel"] = SelectedResourceProfile?.DisplayName ?? "",
            ["executionPlan"] = plan.Summary,
            ["maaContract"] = MaaResourceContract.Summary,
            ["maaContractDetail"] = MaaResourceContract.Detail,
            ["resourceTaskDiagnostics"] = ResourceTaskDiagnostics.Summary,
            ["candidateCount"] = CandidateResults.Count.ToString(),
            ["appliedStateSummary"] = LastCandidateApplySummary,
            ["adbPath"] = AdbPath,
            ["adbSerial"] = AdbSerial,
            ["adbPreset"] = SelectedAdbPreset?.DisplayName ?? SelectedAdbPreset?.Label ?? "",
            ["adbDetectionSummary"] = AdbDetectionSummary,
            ["adbDetectionDetail"] = AdbDetectionDetail,
            ["adbCandidates"] = JsonSerializer.Serialize(AdbPathCandidates.Select(candidate => new
            {
                candidate.Path,
                candidate.Source,
                candidate.Preset,
                candidate.Exists,
                candidate.Available,
                candidate.Error,
            })),
            ["adbDevices"] = JsonSerializer.Serialize(AdbDevices.Select(device => new
            {
                device.Serial,
                device.State,
                device.Detail,
            })),
            ["screencapMethod"] = SelectedAdbScreencapMethod?.Label ?? "",
            ["inputMethod"] = SelectedAdbInputMethod?.Label ?? "",
            ["captureState"] = CaptureState,
            ["lastCapturePath"] = LastCapturePath,
            ["lastFrameId"] = _lastFrameId,
            ["lastFrameMetadataPath"] = _lastFrameMetadataPath,
            ["lastFrameStateSnapshotPath"] = _lastFrameStateSnapshotPath,
            ["lastRecognitionLogPath"] = LastResourceTaskResultsPath,
            ["lastRoiDraftPath"] = LastRoiDraftPath,
            ["lastRoiSessionPath"] = LastRoiSessionPath,
            ["ocrEngine"] = SelectedOcrEngine?.Label ?? "",
            ["rhodesApiUrl"] = RhodesApiUrl,
        };
    }

    public async Task ImportBugReportPathAsync(string path)
    {
        await RunBusyAsync(async () =>
        {
            var result = await RhodesBugReportImport.ImportAsync(path, RhodesSukiDebugPaths.BugReportsDirectory);
            BugReportImportPath = result.ImportedRoot;
            BugReportImportStatus = result.Message;
            if (!result.Success)
            {
                StatusMessage = result.Message;
                return;
            }

            _importedBugReportFrameRecordsDirectory = result.FrameRecordsDirectory;
            _importedBugReportRecognitionScansDirectory = result.RecognitionScansDirectory;
            RefreshFrameRecordHistory();
            RefreshRecognitionScanHistory();
            StatusMessage = result.Message;
            DebugLogLines.Insert(0, $"Bug report imported: {result.ImportedRoot}");
        });
    }

    private async Task RefreshRoiAdjustmentSessionsAsync()
    {
        await RunBusyAsync(() =>
        {
            RefreshRoiAdjustmentSessions();
            StatusMessage = $"ROI調整セッションを更新しました: {RoiAdjustmentSessions.Count}件";
            return Task.CompletedTask;
        });
    }

    private async Task LoadRoiAdjustmentSessionAsync(MaaRoiAdjustmentSessionItem? item)
    {
        if (item is null)
            return;

        await RunBusyAsync(() =>
        {
            var payload = RhodesMaaRoiAdjustmentSessionLog.Load(item.SessionPath);
            if (payload.SchemaVersion != 1 || payload.Kind != "maa-roi-adjustment-session")
            {
                StatusMessage = $"ROI調整セッションを読み込めません: {item.SessionPath}";
                return Task.CompletedTask;
            }

            if (!string.IsNullOrWhiteSpace(payload.ProfileId))
            {
                SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile => string.Equals(profile.Id, payload.ProfileId, StringComparison.Ordinal))
                    ?? SelectedResourceProfile;
            }

            LastRoiSessionPath = item.SessionPath;
            var restoredScan = false;
            if (!string.IsNullOrWhiteSpace(payload.ScanLogPath))
            {
                var scanPayload = RhodesRecognitionScanHistory.LoadPayload(payload.ScanLogPath);
                if (scanPayload.Succeeded)
                {
                    LoadRecognitionPayload(scanPayload, payload.ScanLogPath, "ROI調整セッションから読込");
                    restoredScan = true;
                    RoiSessionRestoreNotice = $"復元済み: 元スキャン候補{CandidateResults.Count}件 / task{ResourceTaskResults.Count}件 / log{RecognitionScanLogRows.Count}件";
                }
                else
                {
                    ClearRecognitionPayload(payload.ScanLogPath, "ROI調整セッションから読込");
                    RoiSessionRestoreNotice = $"元スキャンログを復元できません: {scanPayload.Error} / 認識履歴を更新するか、同じ画面を再スキャンしてセッションを保存し直してください。";
                }
            }
            else
            {
                ClearRecognitionPayload("", "ROI調整セッションから読込");
                RoiSessionRestoreNotice = "元スキャンログ未登録: 同じ画面を再スキャンしてからROI調整セッションを保存し直してください。";
            }

            if (!string.IsNullOrWhiteSpace(payload.CapturePath))
                TryLoadCapturePreviewFromPath(payload.CapturePath);
            RoiBatchApplyResult = payload.BatchResult ?? MaaRoiBatchApplyResult.Failed("未確認");
            ReplaceCollection(RoiBatchDrafts, payload.Drafts.Select(draft => draft.ToPreview()));
            ReplaceCollection(RoiRescanComparisonRows, payload.SafeComparisonRows);
            RoiRescanComparisonSummary = payload.SafeComparisonSummary;
            SetRoiRescanComparisonEvidence(payload.SafeComparisonBeforeLogPath, payload.SafeComparisonAfterLogPath);
            RefreshRoiAdjustmentSessions();
            StatusMessage = restoredScan
                ? $"ROI調整セッションを再開しました: 候補{CandidateResults.Count}件 / task{ResourceTaskResults.Count}件 / ROI候補{RoiBatchDrafts.Count}件 / 比較{RoiRescanComparisonRows.Count}件"
                : $"ROI調整セッションを再開しました: ROI候補{RoiBatchDrafts.Count}件 / 比較{RoiRescanComparisonRows.Count}件 / 元スキャン未復元";
            return Task.CompletedTask;
        });
    }

    private async Task RunRoiRescanComparisonAsync()
    {
        await RunBusyAsync(async () =>
        {
            var beforeTaskResults = ResourceTaskResults.ToArray();
            if (beforeTaskResults.Length == 0)
            {
                ClearRoiRescanComparison("比較元のResource task結果がありません。先に元スキャンまたは履歴読込を実行してください。");
                StatusMessage = RoiRescanComparisonSummary;
                return;
            }

            if (!ResourceTasks.Any())
            {
                ClearRoiRescanComparison("選択プロファイルにResource taskがありません。");
                StatusMessage = RoiRescanComparisonSummary;
                return;
            }

            var beforeCandidates = SnapshotCandidatesForComparison(beforeTaskResults, CandidateResults);
            var beforeSourceEntries = CandidateSourceEntries(CandidateApiProfileId(), beforeTaskResults);
            var beforeEvidencePath = await SaveResourceTaskResultsAsync(
                beforeTaskResults,
                SelectedResourceProfile?.Id,
                beforeCandidates,
                executionPlan: CurrentResourceExecutionPlan);
            if (!await RunAllResourceTasksCoreAsync())
                return;
            if (!await ConvertResourceTaskResultsCoreAsync())
                return;

            var afterTaskResults = ResourceTaskResults.ToArray();
            var afterCandidates = CandidateResults.ToArray();
            var afterSourceEntries = CandidateSourceEntries(CandidateApiProfileId(), afterTaskResults);
            var comparisonRows = CompareRoiRescanCandidates(
                beforeCandidates,
                afterCandidates,
                beforeSourceEntries,
                afterSourceEntries);
            ReplaceCollection(RoiRescanComparisonRows, comparisonRows);
            RoiRescanComparisonSummary = BuildRoiRescanComparisonSummary(beforeCandidates.Count, afterCandidates.Length, comparisonRows);
            SetRoiRescanComparisonEvidence(beforeEvidencePath, LastResourceTaskResultsPath);
            StatusMessage = RoiRescanComparisonSummary;
            RefreshInspectorRows();
        });
    }

    private Task OpenRoiRescanEvidenceAsync(object? parameter)
    {
        var side = parameter as string;
        var path = string.Equals(side, "after", StringComparison.OrdinalIgnoreCase)
            ? _lastRoiRescanAfterPath
            : _lastRoiRescanBeforePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            StatusMessage = string.Equals(side, "after", StringComparison.OrdinalIgnoreCase)
                ? "after比較証跡が見つかりません。"
                : "before比較証跡が見つかりません。";
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            StatusMessage = $"比較証跡を開きました: {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"比較証跡を開けませんでした: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    private async Task PreviewRoiRescanEvidenceAsync(object? parameter)
    {
        var side = parameter as string;
        var normalizedSide = string.Equals(side, "after", StringComparison.OrdinalIgnoreCase) ? "after" : "before";
        var path = normalizedSide == "after" ? _lastRoiRescanAfterPath : _lastRoiRescanBeforePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            RoiRescanEvidencePreviewTitle = $"{normalizedSide}比較証跡なし";
            RoiRescanEvidencePreviewText = "";
            _roiRescanEvidencePreviewJson = "";
            SelectedRoiRescanEvidencePreviewNode = null;
            ReplaceCollection(RoiRescanEvidencePreviewNodes, Array.Empty<MaaEvidencePreviewNode>());
            StatusMessage = $"{normalizedSide}比較証跡が見つかりません。";
            return;
        }

        var text = await File.ReadAllTextAsync(path);
        _roiRescanEvidencePreviewJson = text;
        RoiRescanEvidencePreviewTitle = $"{normalizedSide}: {path}";
        RoiRescanEvidencePreviewText = BuildRoiRescanEvidencePreview(text, SelectedRoiRescanComparisonRow);
        RefreshRoiRescanEvidencePreviewTree();
        StatusMessage = $"比較証跡をプレビューしました: {path}";
    }

    private void RefreshRoiRescanEvidencePreviewTree()
    {
        SelectedRoiRescanEvidencePreviewNode = null;
        IReadOnlyList<MaaEvidencePreviewNode> nodes = string.IsNullOrWhiteSpace(_roiRescanEvidencePreviewJson)
            ? []
            : BuildRoiRescanEvidencePreviewNodes(
                _roiRescanEvidencePreviewJson,
                SelectedRoiRescanComparisonRow,
                RoiEvidenceShowCandidates,
                RoiEvidenceShowTasks,
                RoiEvidenceShowLog);
        ReplaceCollection(
            RoiRescanEvidencePreviewNodes,
            nodes);

        var defaultNode = DefaultEvidencePreviewNode(nodes, SelectedRoiRescanComparisonRow);
        if (defaultNode is not null)
            SelectedRoiRescanEvidencePreviewNode = defaultNode;
    }

    private static string BuildRoiRescanEvidencePreview(string json, MaaRoiRescanComparisonRow? selectedRow)
    {
        if (selectedRow is null
            || (string.IsNullOrWhiteSpace(selectedRow.CandidateKey) && string.IsNullOrWhiteSpace(selectedRow.TaskEntry)))
        {
            return TruncateEvidencePreview(json);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var options = new JsonSerializerOptions { WriteIndented = true };
            var sections = new List<string>
            {
                $"filter: candidateKey={selectedRow.CandidateKey} / taskEntry={selectedRow.TaskEntry}",
            };

            var matchedCandidates = JsonArray(root, "candidates")
                .Where(candidate => CandidateJsonKey(candidate).Equals(selectedRow.CandidateKey, StringComparison.Ordinal))
                .ToArray();
            if (matchedCandidates.Length > 0)
            {
                sections.Add("candidates:");
                sections.Add(string.Join(
                    "\n",
                    matchedCandidates.Select(candidate => JsonSerializer.Serialize(candidate, options))));
            }

            var matchedTasks = EvidenceTaskResults(root)
                .Where(task => JsonString(task, "entry").Equals(selectedRow.TaskEntry, StringComparison.Ordinal))
                .ToArray();
            if (matchedTasks.Length > 0)
            {
                sections.Add("taskResults:");
                sections.Add(string.Join(
                    "\n",
                    matchedTasks.Select(task => JsonSerializer.Serialize(task, options))));
            }

            var matchedLogRows = JsonArray(root, "log")
                .Where(row => JsonString(row, "entry").Equals(selectedRow.TaskEntry, StringComparison.Ordinal))
                .ToArray();
            if (matchedLogRows.Length > 0)
            {
                sections.Add("log:");
                sections.Add(string.Join(
                    "\n",
                    matchedLogRows.Select(row => JsonSerializer.Serialize(row, options))));
            }

            if (sections.Count == 1)
                sections.Add("matched evidence: none");

            return TruncateEvidencePreview(string.Join("\n\n", sections));
        }
        catch (JsonException)
        {
            return TruncateEvidencePreview(json);
        }
    }

    private static IReadOnlyList<MaaEvidencePreviewNode> BuildRoiRescanEvidencePreviewNodes(
        string json,
        MaaRoiRescanComparisonRow? selectedRow,
        bool includeCandidates = true,
        bool includeTasks = true,
        bool includeLog = true)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var candidates = FilterEvidenceCandidates(JsonArray(root, "candidates"), selectedRow);
            var tasks = FilterEvidenceTasks(EvidenceTaskResults(root), selectedRow);
            var logs = FilterEvidenceTasks(JsonArray(root, "log"), selectedRow);
            var nodes = new List<MaaEvidencePreviewNode>
            {
                new MaaEvidencePreviewNode("Summary", "scan metadata", EvidenceSummary(root), NodeKind: "summary"),
            };
            var contractNode = EvidenceContractNode(root);
            if (contractNode is not null)
                nodes.Add(contractNode);
            if (includeCandidates)
                nodes.Add(EvidenceSection("Candidates", "candidate", candidates, CandidateEvidenceTitle, CandidateJsonKey, _ => ""));
            if (includeTasks)
                nodes.Add(EvidenceSection("Resource task results", "task", tasks, TaskEvidenceTitle, _ => "", item => JsonString(item, "entry")));
            if (includeLog)
                nodes.Add(EvidenceSection("Log", "log", logs, LogEvidenceTitle, _ => "", item => JsonString(item, "entry")));
            return nodes;
        }
        catch (JsonException)
        {
            return [new MaaEvidencePreviewNode("Raw JSON", "JSON parse failed", TruncateEvidencePreview(json), NodeKind: "raw")];
        }
    }

    private static IReadOnlyList<JsonElement> FilterEvidenceCandidates(
        IEnumerable<JsonElement> candidates,
        MaaRoiRescanComparisonRow? selectedRow)
    {
        var items = candidates.ToArray();
        return selectedRow is null || string.IsNullOrWhiteSpace(selectedRow.CandidateKey)
            ? items
            : items.Where(candidate => CandidateJsonKey(candidate).Equals(selectedRow.CandidateKey, StringComparison.Ordinal)).ToArray();
    }

    private static IReadOnlyList<JsonElement> FilterEvidenceTasks(
        IEnumerable<JsonElement> rows,
        MaaRoiRescanComparisonRow? selectedRow)
    {
        var items = rows.ToArray();
        return selectedRow is null || string.IsNullOrWhiteSpace(selectedRow.TaskEntry)
            ? items
            : items.Where(row => JsonString(row, "entry").Equals(selectedRow.TaskEntry, StringComparison.Ordinal)).ToArray();
    }

    private static MaaEvidencePreviewNode EvidenceSection(
        string title,
        string nodeKind,
        IReadOnlyList<JsonElement> items,
        Func<JsonElement, int, string> titleFactory,
        Func<JsonElement, string>? candidateKeyFactory = null,
        Func<JsonElement, string>? taskEntryFactory = null)
    {
        const int maxChildren = 80;
        var options = new JsonSerializerOptions { WriteIndented = true };
        var children = items
            .Take(maxChildren)
            .Select((item, index) => new MaaEvidencePreviewNode(
                titleFactory(item, index),
                EvidenceNodeDetail(item),
                JsonSerializer.Serialize(item, options),
                null,
                candidateKeyFactory?.Invoke(item) ?? "",
                taskEntryFactory?.Invoke(item) ?? "",
                nodeKind,
                true))
            .ToList();
        if (items.Count > maxChildren)
        {
            children.Add(new MaaEvidencePreviewNode(
                "truncated",
                $"{items.Count - maxChildren} items hidden",
                $"The preview tree shows the first {maxChildren} of {items.Count} items.",
                NodeKind: "truncated"));
        }

        return new MaaEvidencePreviewNode(
            $"{title} · {items.Count}",
            $"{items.Count} item(s)",
            "",
            children,
            NodeKind: "section",
            ShowDetailByDefault: false);
    }

    private static MaaEvidencePreviewNode? DefaultEvidencePreviewNode(
        IEnumerable<MaaEvidencePreviewNode> nodes,
        MaaRoiRescanComparisonRow? selectedRow)
    {
        var nodeList = nodes as IReadOnlyList<MaaEvidencePreviewNode> ?? nodes.ToArray();
        if (selectedRow is not null)
        {
            if (!string.IsNullOrWhiteSpace(selectedRow.CandidateKey))
            {
                var candidateNode = FindEvidenceNode(nodeList, node =>
                    !node.HasChildren
                    && node.CandidateKey.Equals(selectedRow.CandidateKey, StringComparison.Ordinal));
                if (candidateNode is not null)
                    return candidateNode;
            }

            if (!string.IsNullOrWhiteSpace(selectedRow.TaskEntry))
            {
                var taskNode = FindEvidenceNode(nodeList, node =>
                    !node.HasChildren
                    && node.TaskEntry.Equals(selectedRow.TaskEntry, StringComparison.Ordinal));
                if (taskNode is not null)
                    return taskNode;
            }
        }

        return FindEvidenceNode(nodeList, node => node.NodeKind.Equals("summary", StringComparison.Ordinal))
            ?? nodeList.FirstOrDefault();
    }

    private static string EvidenceSummary(JsonElement root)
    {
        var summary = new[]
        {
            $"profileId: {JsonString(root, "profileId")}",
            $"status: {JsonString(root, "status")}",
            $"source: {JsonString(root, "source")}",
            $"startedAt: {JsonString(root, "startedAt")}",
            $"completedAt: {JsonString(root, "completedAt")}",
            $"counts: {EvidenceCounts(root)}",
            $"executionPlan: {EvidenceExecutionPlan(root)}",
            $"contract: {EvidenceContract(root)}",
        };
        return string.Join(Environment.NewLine, summary);
    }

    private static string EvidenceExecutionPlan(JsonElement root)
    {
        var evidence = JsonObject(root, "evidence");
        var profile = JsonObject(evidence, "profile");
        var plan = JsonObject(profile, "executionPlan");
        if (plan.ValueKind != JsonValueKind.Object)
            return "-";

        var parts = new List<string>
        {
            FirstNonEmpty(JsonString(plan, "stateLabel"), JsonString(plan, "state")),
        };
        var source = JsonString(plan, "source");
        if (!string.IsNullOrWhiteSpace(source))
            parts.Add(source);

        var taskCount = JsonInt(plan, "taskCount");
        if (taskCount > 0)
            parts.Add($"tasks={taskCount}");

        if (JsonBool(plan, "canRun") == true)
            parts.Add("canRun=true");

        var error = JsonString(plan, "error");
        if (!string.IsNullOrWhiteSpace(error))
            parts.Add(error);

        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string EvidenceContract(JsonElement root)
    {
        var evidence = JsonObject(root, "evidence");
        var contract = JsonObject(evidence, "contract");
        if (contract.ValueKind != JsonValueKind.Object)
            return "-";

        var parts = new List<string>
        {
            FirstNonEmpty(JsonString(contract, "state"), JsonString(contract, "summary")),
        };

        var summary = JsonString(contract, "summary");
        if (!string.IsNullOrWhiteSpace(summary) && !summary.Equals(parts[0], StringComparison.Ordinal))
            parts.Add(summary);

        var detail = JsonString(contract, "detail");
        if (!string.IsNullOrWhiteSpace(detail))
            parts.Add(detail);

        var errorCount = JsonArray(contract, "errors").Count;
        if (errorCount > 0)
            parts.Add($"errors={errorCount}");

        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static MaaEvidencePreviewNode? EvidenceContractNode(JsonElement root)
    {
        var evidence = JsonObject(root, "evidence");
        var contract = JsonObject(evidence, "contract");
        if (contract.ValueKind != JsonValueKind.Object)
            return null;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var state = FirstNonEmpty(JsonString(contract, "state"), JsonString(contract, "summary"), "contract");
        return new MaaEvidencePreviewNode(
            $"Contract · {state}",
            EvidenceContract(root),
            JsonSerializer.Serialize(contract, options),
            NodeKind: "contract");
    }

    private static string EvidenceCounts(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("counts", out var counts)
            ? counts.GetRawText()
            : "{}";
    }

    private static string CandidateEvidenceTitle(JsonElement item, int index)
    {
        return FirstNonEmpty(JsonString(item, "label"), JsonString(item, "field"), JsonString(item, "kind"), $"candidate {index + 1}");
    }

    private static string TaskEvidenceTitle(JsonElement item, int index)
    {
        return FirstNonEmpty(JsonString(item, "entry"), $"task {index + 1}");
    }

    private static string LogEvidenceTitle(JsonElement item, int index)
    {
        return FirstNonEmpty(JsonString(item, "event"), JsonString(item, "entry"), $"log {index + 1}");
    }

    private static string EvidenceNodeDetail(JsonElement item)
    {
        var parts = new[]
        {
            JsonString(item, "kind"),
            JsonString(item, "entry"),
            JsonString(item, "status"),
            JsonString(item, "value"),
        }.Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(" / ", parts);
    }

    private static IReadOnlyList<JsonElement> JsonArray(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Array
            ? property.EnumerateArray().ToArray()
            : [];
    }

    private static JsonElement JsonObject(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Object
            ? property
            : default;
    }

    private static IReadOnlyList<JsonElement> EvidenceTaskResults(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("evidence", out var evidence)
            ? JsonArray(evidence, "taskResults")
            : [];
    }

    private static string CandidateJsonKey(JsonElement candidate)
    {
        var field = JsonString(candidate, "field");
        if (!string.IsNullOrWhiteSpace(field))
            return $"field:{field}:{JsonString(candidate, "campaignId")}:{JsonString(candidate, "fieldId")}:{JsonString(candidate, "slotKind")}";
        var operatorId = JsonString(candidate, "operatorId");
        if (!string.IsNullOrWhiteSpace(operatorId))
            return $"operator:{operatorId}";
        var relicId = JsonString(candidate, "relicId");
        if (!string.IsNullOrWhiteSpace(relicId))
            return $"relic:{JsonString(candidate, "campaignId")}:{relicId}";
        var thoughtId = JsonString(candidate, "thoughtId");
        if (!string.IsNullOrWhiteSpace(thoughtId))
            return $"thought:{JsonString(candidate, "campaignId")}:{thoughtId}";
        var ageId = JsonString(candidate, "ageId");
        if (!string.IsNullOrWhiteSpace(ageId))
            return $"age:{JsonString(candidate, "campaignId")}:{ageId}";
        var effectId = JsonString(candidate, "effectId");
        if (!string.IsNullOrWhiteSpace(effectId))
            return $"effect:{JsonString(candidate, "campaignId")}:{effectId}:{JsonString(candidate, "slotKind")}";
        var coinId = JsonString(candidate, "coinId");
        if (!string.IsNullOrWhiteSpace(coinId))
            return $"coin:{JsonString(candidate, "campaignId")}:{coinId}";
        var statusId = JsonString(candidate, "statusId");
        if (!string.IsNullOrWhiteSpace(statusId))
            return $"status:{JsonString(candidate, "campaignId")}:{statusId}";

        return FirstNonEmpty(JsonString(candidate, "recognitionKey"), JsonString(candidate, "campaignId"));
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static int JsonInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static bool? JsonBool(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            ? property.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;
    }

    private static string TruncateEvidencePreview(string text)
    {
        const int maxPreviewLength = 60000;
        return text.Length <= maxPreviewLength
            ? text
            : $"{text[..maxPreviewLength]}\n... truncated {text.Length - maxPreviewLength} chars ...";
    }

    private async Task PreviewSelectedRoiDraftApplyAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!SelectedRoiEditDraft.HasSelection)
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("ROI行を選択してください。");
                StatusMessage = RoiDraftApplyResult.Message;
                return;
            }

            var sourcePath = ResolveRoiDraftSourcePath(SelectedRoiEditDraft);
            if (!File.Exists(sourcePath))
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed($"ROI生成元JSONが見つかりません: {sourcePath}");
                StatusMessage = RoiDraftApplyResult.Message;
                return;
            }

            var json = await File.ReadAllTextAsync(sourcePath);
            var result = RhodesMaaRoiDraftSourceUpdater.ApplyToSourceJson(json, SelectedRoiEditDraft, out _);
            RoiDraftApplyResult = result with { SourcePath = result.Succeeded ? sourcePath : result.SourcePath };
            StatusMessage = result.Succeeded
                ? $"ROI適用プレビュー: {result.TargetId} {result.PreviousRoi} -> {result.UpdatedRoi}"
                : $"ROI適用プレビュー失敗: {result.Message}";
        });
    }

    private async Task SelectRoiPreviewAsync(MaaRoiPreviewRow? row)
    {
        if (row is null)
            return;

        await RunBusyAsync(() =>
        {
            SelectedRoiPreviewRow = row;
            StatusMessage = $"ROIを選択しました: {row.DisplayTitle} / {row.ProjectedRoiJson}";
            return Task.CompletedTask;
        });
    }

    public void BeginRoiDrag(MaaRoiPreviewRow? row, double pointerX, double pointerY)
    {
        if (row is null)
            return;

        SelectedRoiPreviewRow = row;
        if (!TryParseDraftRoi(SelectedRoiEditDraft.RoiJson, out var roi))
            return;

        _roiDragOrigin = roi;
        _roiDragStartX = pointerX;
        _roiDragStartY = pointerY;
        StatusMessage = $"ROIドラッグ開始: {row.DisplayTitle}";
    }

    public void UpdateRoiDrag(double pointerX, double pointerY)
    {
        if (_roiDragOrigin.Length != 4 || !SelectedRoiEditDraft.HasSelection)
            return;

        var adjusted = _roiDragOrigin.ToArray();
        adjusted[0] = Math.Max(0, adjusted[0] + SnapDelta(pointerX - _roiDragStartX));
        adjusted[1] = Math.Max(0, adjusted[1] + SnapDelta(pointerY - _roiDragStartY));
        SelectedRoiEditDraft = SelectedRoiEditDraft with { RoiJson = RoiJson(adjusted) };
        UpdateSelectedRoiOverlay(adjusted);
        UpdateRoiBatchDraftForSelected();
        RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
    }

    public void EndRoiDrag()
    {
        if (_roiDragOrigin.Length == 0)
            return;

        _roiDragOrigin = [];
        StatusMessage = $"ROIドラッグ終了: {SelectedRoiEditDraft.RoiJson}";
    }

    public void BeginRoiResize(MaaRoiPreviewRow? row, double pointerX, double pointerY, string? mode)
    {
        if (row is null)
            return;

        SelectedRoiPreviewRow = row;
        if (!TryParseDraftRoi(SelectedRoiEditDraft.RoiJson, out var roi))
            return;

        _roiResizeOrigin = roi;
        _roiResizeMode = string.IsNullOrWhiteSpace(mode) ? "bottomRight" : mode;
        _roiResizeStartX = pointerX;
        _roiResizeStartY = pointerY;
        StatusMessage = $"ROIリサイズ開始: {row.DisplayTitle} / {_roiResizeMode}";
    }

    public void UpdateRoiResize(double pointerX, double pointerY)
    {
        if (_roiResizeOrigin.Length != 4 || !SelectedRoiEditDraft.HasSelection)
            return;

        var adjusted = _roiResizeOrigin.ToArray();
        var dx = SnapDelta(pointerX - _roiResizeStartX);
        var dy = SnapDelta(pointerY - _roiResizeStartY);
        if (_roiResizeMode.Equals("topLeft", StringComparison.Ordinal) || _roiResizeMode.Equals("left", StringComparison.Ordinal))
        {
            var nextX = Math.Clamp(adjusted[0] + dx, 0, adjusted[0] + adjusted[2] - 1);
            var nextY = Math.Clamp(adjusted[1] + dy, 0, adjusted[1] + adjusted[3] - 1);
            adjusted[2] = Math.Max(1, adjusted[2] - (nextX - adjusted[0]));
            adjusted[0] = nextX;
            if (_roiResizeMode.Equals("topLeft", StringComparison.Ordinal))
            {
                adjusted[3] = Math.Max(1, adjusted[3] - (nextY - adjusted[1]));
                adjusted[1] = nextY;
            }
        }
        else if (_roiResizeMode.Equals("top", StringComparison.Ordinal))
        {
            var nextY = Math.Clamp(adjusted[1] + dy, 0, adjusted[1] + adjusted[3] - 1);
            adjusted[3] = Math.Max(1, adjusted[3] - (nextY - adjusted[1]));
            adjusted[1] = nextY;
        }
        else if (_roiResizeMode.Equals("right", StringComparison.Ordinal))
        {
            adjusted[2] = Math.Max(1, adjusted[2] + dx);
        }
        else if (_roiResizeMode.Equals("bottom", StringComparison.Ordinal))
        {
            adjusted[3] = Math.Max(1, adjusted[3] + dy);
        }
        else
        {
            adjusted[2] = Math.Max(1, adjusted[2] + dx);
            adjusted[3] = Math.Max(1, adjusted[3] + dy);
        }
        SelectedRoiEditDraft = SelectedRoiEditDraft with { RoiJson = RoiJson(adjusted) };
        UpdateSelectedRoiOverlay(adjusted);
        UpdateRoiBatchDraftForSelected();
        RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
    }

    public void EndRoiResize()
    {
        if (_roiResizeOrigin.Length == 0)
            return;

        _roiResizeOrigin = [];
        _roiResizeMode = "";
        StatusMessage = $"ROIリサイズ終了: {SelectedRoiEditDraft.RoiJson}";
    }

    public void CancelRoiInteraction()
    {
        if (_roiDragOrigin.Length == 4)
        {
            RestoreSelectedRoi(_roiDragOrigin, "ROIドラッグをキャンセルしました。");
            _roiDragOrigin = [];
            return;
        }

        if (_roiResizeOrigin.Length == 4)
        {
            RestoreSelectedRoi(_roiResizeOrigin, "ROIリサイズをキャンセルしました。");
            _roiResizeOrigin = [];
            _roiResizeMode = "";
        }
    }

    private async Task SetRoiSnapStepAsync(string? value)
    {
        await RunBusyAsync(() =>
        {
            if (int.TryParse(value, out var step))
                RoiSnapStep = step;
            OnPropertyChanged(nameof(RoiSnapStepLabel));
            StatusMessage = $"ROI調整ステップ: {RoiSnapStep}px";
            return Task.CompletedTask;
        });
    }

    private async Task ResetSelectedRoiDraftAsync()
    {
        await RunBusyAsync(() =>
        {
            if (_selectedRoiPreviewRow is null)
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("ROI行を選択してください。");
                StatusMessage = RoiDraftApplyResult.Message;
                return Task.CompletedTask;
            }

            if (TryParseDraftRoi(MaaRoiEditDraft.FromPreview(_selectedRoiPreviewRow).RoiJson, out var roi))
                RestoreSelectedRoi(roi, "ROIを元に戻しました。");
            return Task.CompletedTask;
        });
    }

    private async Task AdjustSelectedRoiDraftAsync(string? operation)
    {
        await RunBusyAsync(() =>
        {
            if (!SelectedRoiEditDraft.HasSelection)
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("ROI行を選択してください。");
                StatusMessage = RoiDraftApplyResult.Message;
                return Task.CompletedTask;
            }

            if (!TryParseDraftRoi(SelectedRoiEditDraft.RoiJson, out var roi))
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("ROI座標JSONを解析できません。");
                StatusMessage = RoiDraftApplyResult.Message;
                return Task.CompletedTask;
            }

            var adjusted = AdjustRoi(roi, operation, RoiSnapStep);
            SelectedRoiEditDraft = SelectedRoiEditDraft with { RoiJson = RoiJson(adjusted) };
            UpdateSelectedRoiOverlay(adjusted);
            UpdateRoiBatchDraftForSelected();
            RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
            StatusMessage = $"ROIを調整しました: {SelectedRoiEditDraft.RoiJson}";
            return Task.CompletedTask;
        });
    }

    private async Task ApplySelectedRoiDraftAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (!SelectedRoiEditDraft.HasSelection)
            {
                RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("ROI行を選択してください。");
                StatusMessage = RoiDraftApplyResult.Message;
                return;
            }

            var sourcePath = ResolveRoiDraftSourcePath(SelectedRoiEditDraft);
            var result = await RhodesMaaRoiDraftSourceUpdater.ApplyToSourceFileAsync(sourcePath, SelectedRoiEditDraft);
            RoiDraftApplyResult = result;
            StatusMessage = result.Succeeded
                ? $"ROIを適用しました: {result.TargetId} / backup={result.BackupPath}"
                : $"ROI適用失敗: {result.Message}";
        });
    }

    private async Task PreviewVisibleRoiDraftsApplyAsync()
    {
        await RunBusyAsync(async () =>
        {
            var drafts = VisibleResourceRoiDrafts();
            if (drafts.Count == 0)
            {
                RoiBatchApplyResult = MaaRoiBatchApplyResult.Failed("表示中のResource ROI候補がありません。");
                StatusMessage = RoiBatchApplyResult.Message;
                return;
            }

            var maaTasksPath = ResolveMaaTasksSourcePath();
            var scanProfilesPath = ResolveScanProfilesSourcePath();
            if (!File.Exists(maaTasksPath) || !File.Exists(scanProfilesPath))
            {
                RoiBatchApplyResult = MaaRoiBatchApplyResult.Failed($"ROI生成元JSONが見つかりません: maa={maaTasksPath} / scan={scanProfilesPath}");
                StatusMessage = RoiBatchApplyResult.Message;
                return;
            }

            var result = RhodesMaaRoiDraftBatchSourceUpdater.ApplyToSourceJsons(
                await File.ReadAllTextAsync(maaTasksPath),
                await File.ReadAllTextAsync(scanProfilesPath),
                drafts,
                out _,
                out _);
            RoiBatchApplyResult = result;
            UpdateRoiBatchDraftStates(result, result.Succeeded ? "確認済み" : "失敗");
            StatusMessage = result.Succeeded
                ? $"ROI一括適用プレビュー: {result.AppliedCount}件"
                : $"ROI一括適用プレビュー失敗: {result.Message}";
        });
    }

    private async Task ApplyVisibleRoiDraftsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var drafts = VisibleResourceRoiDrafts();
            if (drafts.Count == 0)
            {
                RoiBatchApplyResult = MaaRoiBatchApplyResult.Failed("表示中のResource ROI候補がありません。");
                StatusMessage = RoiBatchApplyResult.Message;
                return;
            }

            var result = await RhodesMaaRoiDraftBatchSourceUpdater.ApplyToSourceFilesAsync(
                ResolveMaaTasksSourcePath(),
                ResolveScanProfilesSourcePath(),
                drafts);
            RoiBatchApplyResult = result;
            UpdateRoiBatchDraftStates(result, result.Succeeded ? "適用済み" : "失敗");
            StatusMessage = result.Succeeded
                ? $"ROIを一括適用しました: {result.AppliedCount}件 / {result.BackupSummary}"
                : $"ROI一括適用失敗: {result.Message}";
        });
    }

    private Task SetAllRoiBatchDraftsIncludedAsync(bool isIncluded)
    {
        var next = RoiBatchDrafts
            .Select(item =>
            {
                var stateLabel = isIncluded && item.StateLabel == "対象外" ? "未確認" : item.StateLabel;
                return new MaaRoiBatchDraftPreview(
                    item.Draft,
                    isIncluded,
                    isIncluded ? stateLabel : "対象外",
                    isIncluded ? item.StateDetail : "");
            })
            .ToArray();
        ReplaceCollection(RoiBatchDrafts, next);
        RoiBatchApplyResult = MaaRoiBatchApplyResult.Failed("未確認");
        StatusMessage = isIncluded
            ? $"表示中ROI候補を全選択しました: {next.Length}件"
            : $"表示中ROI候補を全解除しました: {next.Length}件";
        return Task.CompletedTask;
    }

    private void UpdateRoiBatchDraftStates(MaaRoiBatchApplyResult result, string successStateLabel)
    {
        if (result.Results.Count == 0)
            return;

        var resultIndex = 0;
        var next = RoiBatchDrafts
            .Select(item =>
            {
                if (!item.IsIncluded)
                    return new MaaRoiBatchDraftPreview(item.Draft, false, "対象外", "");

                if (resultIndex >= result.Results.Count)
                    return new MaaRoiBatchDraftPreview(item.Draft, true, "未確認", item.StateDetail);

                var itemResult = result.Results[resultIndex++];
                return itemResult.Succeeded
                    ? new MaaRoiBatchDraftPreview(item.Draft, true, successStateLabel, itemResult.DiffSummary)
                    : new MaaRoiBatchDraftPreview(item.Draft, true, "失敗", itemResult.Message);
            })
            .ToArray();
        ReplaceCollection(RoiBatchDrafts, next);
    }

    private async Task RegenerateMaaResourceAsync()
    {
        await RunBusyAsync(async () =>
        {
            MaaResourceGenerationResult = await RhodesMaaGeneratedResourceBuilder.RegenerateFileAsync(
                ResolveMaaTasksSourcePath(),
                ResolveScanProfilesSourcePath(),
                ResolveGeneratedPipelinePath());
            if (!MaaResourceGenerationResult.Succeeded)
            {
                StatusMessage = $"MAA Resource再生成失敗: {MaaResourceGenerationResult.Message}";
                return;
            }

            var message = $"{MaaResourceGenerationResult.Message} / backup={MaaResourceGenerationResult.BackupPath}";
            ReloadResourceCatalog();
            var reloadSnapshot = await ReloadMaaResourceSessionIfReadyAsync();
            StatusMessage = reloadSnapshot is null
                ? $"{message} / 未接続のため次回接続時に反映"
                : reloadSnapshot.IsReady
                    ? $"{message} / Resource再読込済み"
                    : $"{message} / Resource再読込失敗: {reloadSnapshot.Detail}";
        });
    }

    private async Task<MaaSessionSnapshot?> ReloadMaaResourceSessionIfReadyAsync()
    {
        if (!string.Equals(SessionState, "接続済み", StringComparison.Ordinal))
            return null;

        var snapshot = await _session.InitializeAdbAsync(BuildSessionOptions());
        SessionState = snapshot.State;
        SessionDetail = snapshot.IsReady
            ? $"{snapshot.Detail} / Resource再読込済み"
            : $"{snapshot.Detail} / Resource再読込失敗";
        RefreshInspectorRows();
        return snapshot;
    }

    private async Task SyncRunStateFromApiAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "API状態を読み込んでいます。";
            var error = await SyncRunStateFromApiCoreAsync();
            StatusMessage = string.IsNullOrWhiteSpace(error)
                ? "API状態をSuki表示へ同期しました。"
                : $"API状態同期に失敗しました: {error}";
        });
    }

    private async Task ConvertResourceTaskResultsAsync()
    {
        await RunBusyAsync(async () => { await ConvertResourceTaskResultsCoreAsync(); });
    }

    private async Task ApplyCandidateResultsAsync()
    {
        await RunBusyAsync(ApplyCandidateResultsCoreAsync);
    }

    /// <summary>
    /// 候補反映の共通経路。認識候補も手動入力も同じreducer/API同期/ローカルfallbackを通す。
    /// </summary>
    private async Task<RhodesCandidateApplyWorkflowResult> ApplyCandidatesPipelineAsync(
        IReadOnlyList<MaaCandidatePreview> candidates)
    {
        var result = await RhodesRecognitionWorkflow.ApplyCandidatesAsync(
            candidates,
            cancellationToken => RhodesStateApiClient.FetchAsync(RhodesApiUrl, cancellationToken: cancellationToken),
            (stateJson, cancellationToken) => RhodesStateApiClient.SaveAsync(RhodesApiUrl, stateJson, cancellationToken: cancellationToken),
            (stateJson, _) => RhodesRunStateStore.ReplaceStateJsonAsync(stateJson),
            (localCandidates, _) => RhodesRunStateStore.SaveCandidatesAsync(localCandidates));

        if (result.ApiStatus is not null)
        {
            _rhodesApiStatus = result.ApiStatus;
            RefreshRuntimeCapabilities();
        }

        if (result.ShouldReloadRunState)
            ReloadRunStateFromStore();

        LastCandidateApplySummary = result.LastCandidateApplySummary;
        StatusMessage = result.StatusMessage;
        return result;
    }

    /// <summary>共通値(源石錐/等級/分隊)の手動入力を「手動候補」として反映する。</summary>
    private async Task ApplyManualRunValuesAsync()
    {
        await RunBusyAsync(async () =>
        {
            var campaignId = SelectedCampaign?.Id ?? _runState.CampaignId;
            var candidates = new List<MaaCandidatePreview>
            {
                new("runStatus", "源石錐 (手動入力)", ManualIngot.ToString(CultureInfo.InvariantCulture), "手動入力", 1.0, Field: "ingot", CampaignId: campaignId),
                new("runStatus", "等級 (手動入力)", ManualDifficulty.ToString(CultureInfo.InvariantCulture), "手動入力", 1.0, Field: "difficulty", CampaignId: campaignId),
            };
            if (!string.IsNullOrWhiteSpace(SelectedManualSquad?.Id))
                candidates.Add(new("runStatus", "分隊 (手動入力)", SelectedManualSquad.Id, "手動入力", 1.0, Field: "squadId", CampaignId: campaignId));
            if (!string.IsNullOrWhiteSpace(SelectedManualSquadRandomEffect?.Id))
                candidates.Add(new("runStatus", "分隊追加効果 (手動入力)", SelectedManualSquadRandomEffect.Id, "手動入力", 1.0, Field: "squadRandomEffectOptionId", CampaignId: campaignId));

            await ApplyCandidatesPipelineAsync(candidates);
            RefreshInspectorRows();
        });
    }

    private async Task ApplyManualSarkazValuesAsync()
    {
        await RunBusyAsync(async () =>
        {
            const string campaignId = RhodesPublicDebugPolicy.SarkazCampaignId;
            var candidates = new List<MaaCandidatePreview>
            {
                new("runStatus", "構想 (手動入力)", ManualIdea.ToString(CultureInfo.InvariantCulture), "手動入力", 1.0, Field: "idea", CampaignId: campaignId),
            };

            foreach (var thought in ManualThoughtEditors.Where(item => item.Count > 0))
            {
                for (var count = 0; count < thought.Count; count++)
                    candidates.Add(new("thought", $"{thought.Name} (手動入力)", thought.Id, "手動入力", 1.0, CampaignId: campaignId, ThoughtId: thought.Id));
            }

            if (SelectedManualAge is not null)
                candidates.Add(new("age", $"{SelectedManualAge.Name} (手動入力)", SelectedManualAge.Id, "手動入力", 1.0, CampaignId: campaignId, AgeId: SelectedManualAge.Id));

            await ApplyCandidatesPipelineAsync(candidates);
            RefreshInspectorRows();
        });
    }

    private async Task ApplyManualPhantomValuesAsync()
    {
        await RunBusyAsync(async () =>
        {
            const string campaignId = "is2_phantom";
            var hallucinations = ManualHallucinationOptions
                .Where(option => option.IsSelected)
                .Select(option => option.Name)
                .ToArray();
            var candidates = new MaaCandidatePreview[]
            {
                new(
                    "runStatus",
                    string.IsNullOrWhiteSpace(SelectedManualPerformance?.Id) ? "演目なし" : "演目 (手動入力)",
                    SelectedManualPerformance?.Id ?? "",
                    "手動入力",
                    1.0,
                    Field: "performanceId",
                    CampaignId: campaignId),
                new(
                    "runStatus",
                    hallucinations.Length == 0 ? "幻覚なし" : "幻覚 (手動入力)",
                    string.Join(" / ", hallucinations),
                    "手動入力",
                    1.0,
                    Field: "hallucinations",
                    CampaignId: campaignId),
            };

            await ApplyCandidatesPipelineAsync(candidates);
            RefreshInspectorRows();
        });
    }

    private async Task ApplyManualMizukiValuesAsync()
    {
        await RunBusyAsync(async () =>
        {
            const string campaignId = "is3_mizuki";
            var candidates = new List<MaaCandidatePreview>
            {
                new("mizuki", "鍵 (手動入力)", ManualMizukiKey.ToString(CultureInfo.InvariantCulture), "手動入力", 1.0, CampaignId: campaignId, FieldId: "key"),
                new("mizuki", "灯火 (手動入力)", ManualMizukiLight.ToString(CultureInfo.InvariantCulture), "手動入力", 1.0, CampaignId: campaignId, FieldId: "light"),
            };

            var hordeCalls = ManualMizukiHordeCallOptions.Where(option => option.IsSelected).ToArray();
            if (hordeCalls.Length == 0)
            {
                candidates.Add(RhodesRecognitionCandidateApplier.CreateNoMizukiHordeCallCandidate());
            }
            else
            {
                candidates.AddRange(hordeCalls.Select(option => new MaaCandidatePreview(
                    "mizuki",
                    $"{option.Name} (手動入力)",
                    option.Id,
                    "手動入力",
                    1.0,
                    CampaignId: campaignId,
                    FieldId: "hordeCalls",
                    EffectId: option.Id)));
            }

            if (string.IsNullOrWhiteSpace(SelectedManualMizukiRejection?.Id))
            {
                candidates.Add(RhodesRecognitionCandidateApplier.CreateNoMizukiRejectionCandidate());
            }
            else
            {
                candidates.Add(new MaaCandidatePreview(
                    "mizuki",
                    $"{SelectedManualMizukiRejection.Name} (手動入力)",
                    SelectedManualMizukiRejection.Id,
                    "手動入力",
                    1.0,
                    CampaignId: campaignId,
                    FieldId: "rejectionReaction",
                    EffectId: SelectedManualMizukiRejection.Id));
                candidates.AddRange(ManualMizukiOperatorTargets
                    .Where(option => option.IsSelected)
                    .Select(option => new MaaCandidatePreview(
                        "mizuki",
                        $"{option.Name} (拒絶反応対象)",
                        option.Id,
                        "手動入力",
                        1.0,
                        OperatorId: option.Id,
                        CampaignId: campaignId,
                        FieldId: "rejectionReaction")));
            }

            await ApplyCandidatesPipelineAsync(candidates);
            RefreshInspectorRows();
        });
    }

    private Task ToggleManualHallucinationAsync(object? parameter)
    {
        if (parameter is not SukiHallucinationOption option || !ManualHallucinationOptions.Contains(option))
            return Task.CompletedTask;

        option.IsSelected = !option.IsSelected;
        RefreshActiveManualHallucinationFusions();
        OnPropertyChanged(nameof(ManualHallucinationSelectionSummary));
        return Task.CompletedTask;
    }

    private Task ToggleManualMizukiHordeCallAsync(object? parameter)
    {
        if (parameter is SukiToggleEffectOption option && ManualMizukiHordeCallOptions.Contains(option))
            option.IsSelected = !option.IsSelected;
        return Task.CompletedTask;
    }

    private Task ToggleManualMizukiOperatorTargetAsync(object? parameter)
    {
        if (parameter is SukiOperatorTargetOption option && ManualMizukiOperatorTargets.Contains(option))
            option.IsSelected = !option.IsSelected;
        return Task.CompletedTask;
    }

    private async Task ConfirmClearRunAsync()
    {
        await RunBusyAsync(async () =>
        {
            var api = await RhodesStateApiClient.FetchAsync(RhodesApiUrl);
            if (api.Succeeded)
            {
                var clearedJson = RhodesStateApiClient.ClearCurrentRunInStateJson(api.StateJson);
                var saved = await RhodesStateApiClient.SaveAsync(RhodesApiUrl, clearedJson);
                if (saved.Succeeded)
                    await RhodesRunStateStore.ReplaceStateJsonAsync(clearedJson);
                else
                    await RhodesRunStateStore.ClearCurrentRunAsync();
            }
            else
            {
                await RhodesRunStateStore.ClearCurrentRunAsync();
            }
            IsClearRunConfirmationVisible = false;
            ReloadRunStateFromStore();
            StatusMessage = "現在の統合戦略ランをクリアしました。";
            LastCandidateApplySummary = "ラン状態を手動クリア";
        });
    }

    /// <summary>手動入力欄を現在stateへ同期し、分隊候補を現在ISで組み直す。</summary>
    private void RefreshManualRunEditors()
    {
        var campaignId = SelectedCampaign?.Id ?? _runState.CampaignId;
        ReplaceCollection(ManualSquadOptions, new[] { SukiSquadOption.KeepCurrent }
            .Concat(RhodesRunCatalog.LoadSquadOptions(campaignId)));
        ManualIngot = _runState.Ingot;
        if (int.TryParse(_runState.Difficulty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var difficulty))
            ManualDifficulty = difficulty;
        SelectedManualSquad = ManualSquadOptions.FirstOrDefault(option =>
                !string.IsNullOrWhiteSpace(option.Id)
            && option.Name.Equals(_runState.Squad, StringComparison.Ordinal))
            ?? SukiSquadOption.KeepCurrent;
        ManualIdea = _runState.Idea;
        RefreshManualSarkazEditors();
        RefreshManualPhantomEditors();
        RefreshManualMizukiEditors();
        RefreshManualSquadRandomEffectOptions();
        RefreshBossSections();
        OnPropertyChanged(nameof(ManualDifficultyMaximum));
        OnPropertyChanged(nameof(ManualDifficultyTierLabel));
        OnPropertyChanged(nameof(IsManualDifficultyCampaign));
        OnPropertyChanged(nameof(ManualDifficultyGuidance));
    }

    private void RefreshManualPhantomEditors()
    {
        var performances = new[] { new SukiSpecialEffectOption("", "演目なし") }
            .Concat(RhodesRunCatalog.LoadPerformanceOptions("is2_phantom"))
            .ToArray();
        ReplaceCollection(ManualPerformanceOptions, performances);
        SelectedManualPerformance = performances.FirstOrDefault(option =>
            option.Id.Equals(_runState.PerformanceId, StringComparison.Ordinal)) ?? performances[0];

        var hallucinationState = (_runState.SpecialFields ?? [])
            .FirstOrDefault(field => field.CampaignId == "is2_phantom" && field.FieldId == "hallucinations");
        var selectedNames = hallucinationState is null || hallucinationState.Detail == "取得値なし"
            ? new HashSet<string>(StringComparer.Ordinal)
            : RhodesHallucinationCatalog.NormalizeRecognizedNames([hallucinationState.Detail])
                .ToHashSet(StringComparer.Ordinal);
        ReplaceCollection(
            ManualHallucinationOptions,
            RhodesHallucinationCatalog.LoadDefault().Options.Select(option => new SukiHallucinationOption(
                option.Id,
                option.Name,
                option.MapLabel,
                option.Effect,
                option.FlavorText,
                option.Category,
                option.Aliases,
                selectedNames.Contains(option.Name))));
        RefreshActiveManualHallucinationFusions();
        OnPropertyChanged(nameof(ManualHallucinationSelectionSummary));
    }

    private void RefreshManualMizukiEditors()
    {
        const string campaignId = "is3_mizuki";
        var fields = (_runState.SpecialFields ?? [])
            .Where(field => field.CampaignId.Equals(campaignId, StringComparison.Ordinal))
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);

        ManualMizukiKey = fields.TryGetValue("key", out var key)
            && int.TryParse(key.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyValue)
                ? keyValue
                : 0;
        ManualMizukiLight = fields.TryGetValue("light", out var light)
            && int.TryParse(light.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lightValue)
                ? lightValue
                : 0;

        var selectedHordeIds = fields.TryGetValue("hordeCalls", out var horde)
            ? (horde.SelectedIds ?? []).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        ReplaceCollection(
            ManualMizukiHordeCallOptions,
            RhodesRunCatalog.LoadSpecialEffectOptions(campaignId, "hordeCall")
                .Select(option => new SukiToggleEffectOption(option, selectedHordeIds.Contains(option.Id))));

        var rejectionOptions = new[] { new SukiSpecialEffectOption("", "拒絶反応なし") }
            .Concat(RhodesRunCatalog.LoadSpecialEffectOptions(campaignId, "rejectionReaction"))
            .ToArray();
        ReplaceCollection(ManualMizukiRejectionOptions, rejectionOptions);
        var rejection = fields.GetValueOrDefault("rejectionReaction");
        SelectedManualMizukiRejection = rejectionOptions.FirstOrDefault(option => option.Id == rejection?.EffectId)
            ?? rejectionOptions[0];

        var selectedOperatorIds = (rejection?.OperatorIds ?? []).ToHashSet(StringComparer.Ordinal);
        ReplaceCollection(
            ManualMizukiOperatorTargets,
            _allOperators
                .Where(option => option.IsSelected)
                .Select(option => new SukiOperatorTargetOption(
                    option.Id,
                    option.Name,
                    option.ImagePath,
                    selectedOperatorIds.Contains(option.Id))));
    }

    private void RefreshActiveManualHallucinationFusions()
    {
        ReplaceCollection(
            ActiveManualHallucinationFusions,
            RhodesHallucinationCatalog.ResolveActiveFusions(
                ManualHallucinationOptions.Where(option => option.IsSelected).Select(option => option.Name)));
    }

    private void RefreshBossSections()
    {
        foreach (var section in BossSections)
            section.PropertyChanged -= BossSectionPropertyChanged;

        var campaignId = SelectedCampaign?.Id ?? _runState.CampaignId;
        ReplaceCollection(
            BossSections,
            RhodesBossSelectionCatalog.LoadSections(campaignId, _runState.BossSelections));

        foreach (var section in BossSections)
            section.PropertyChanged += BossSectionPropertyChanged;
    }

    private void BossSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SukiBossSectionEditor section)
            return;

        var shouldPersist = section.AllowsMultiple
            ? e.PropertyName == nameof(SukiBossSectionEditor.SelectedIds)
            : e.PropertyName == nameof(SukiBossSectionEditor.SelectedOption);
        if (!shouldPersist)
            return;

        _ = PersistBossSelectionAsync(
            section.CampaignId,
            section.Field,
            section.SelectedIds.ToArray(),
            section.AllowsMultiple);
    }

    private Task ToggleBossOptionAsync(object? parameter)
    {
        if (parameter is not SukiBossOption option)
            return Task.CompletedTask;

        BossSections.FirstOrDefault(section =>
                section.Field.Equals(option.Field, StringComparison.Ordinal))
            ?.Toggle(option);
        return Task.CompletedTask;
    }

    private async Task PersistBossSelectionAsync(
        string campaignId,
        string field,
        IReadOnlyList<string> selectedIds,
        bool allowsMultiple)
    {
        await _bossSelectionSaveLock.WaitAsync();
        try
        {
            var api = await RhodesStateApiClient.FetchAsync(RhodesApiUrl);
            if (api.Succeeded)
            {
                var updatedJson = RhodesStateApiClient.ApplyBossSelectionToStateJson(
                    api.StateJson,
                    campaignId,
                    field,
                    selectedIds,
                    allowsMultiple);
                var saved = await RhodesStateApiClient.SaveAsync(RhodesApiUrl, updatedJson);
                if (saved.Succeeded)
                {
                    await RhodesRunStateStore.ReplaceStateJsonAsync(updatedJson);
                    _rhodesApiStatus = new SukiOptionalRuntimeStatus(
                        "RHODES API",
                        "接続済み",
                        "ボス選択を同期しました。",
                        true,
                        false);
                }
                else
                {
                    await RhodesRunStateStore.SaveBossSelectionAsync(
                        campaignId,
                        field,
                        selectedIds,
                        allowsMultiple);
                    _rhodesApiStatus = new SukiOptionalRuntimeStatus(
                        "RHODES API",
                        "同期失敗",
                        saved.Error,
                        false,
                        false);
                }
            }
            else
            {
                await RhodesRunStateStore.SaveBossSelectionAsync(
                    campaignId,
                    field,
                    selectedIds,
                    allowsMultiple);
                _rhodesApiStatus = new SukiOptionalRuntimeStatus(
                    "RHODES API",
                    "未接続",
                    $"ローカルへ保存しました。{api.Error}",
                    false,
                    false);
            }

            ReloadRunStateFromStore();
            RefreshRuntimeCapabilities();
            StatusMessage = selectedIds.Count == 0
                ? "ボス選択を解除しました。"
                : $"ボス選択を保存しました: {string.Join(" / ", selectedIds)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"ボス選択を保存できませんでした: {ex.Message}";
        }
        finally
        {
            _bossSelectionSaveLock.Release();
        }
    }

    private void RefreshManualSarkazEditors()
    {
        var selectedThoughtId = SelectedThoughtDetail?.Id ?? "";
        var thoughtCounts = (_runState.SpecialFields ?? [])
            .FirstOrDefault(field => field.CampaignId == RhodesPublicDebugPolicy.SarkazCampaignId && field.FieldId == "thought")
            ?.Detail ?? "";
        var currentCounts = thoughtCounts.Split(" / ", StringSplitOptions.RemoveEmptyEntries)
            .Select(text => text.Split(" x", StringSplitOptions.None))
            .ToDictionary(parts => parts[0], parts => parts.Length > 1 && int.TryParse(parts[1], out var count) ? count : 1, StringComparer.Ordinal);

        ReplaceCollection(ManualThoughtEditors,
            RhodesRunCatalog.LoadSpecialEffectOptions(RhodesPublicDebugPolicy.SarkazCampaignId, "thought")
                .Select(option => new SukiThoughtCountEditor(option, currentCounts.GetValueOrDefault(option.Name))));
        SelectedThoughtDetail = ManualThoughtEditors.FirstOrDefault(item => item.Id == selectedThoughtId);

        var ages = new[] { new SukiSpecialEffectOption("", "時代なし") }
            .Concat(RhodesRunCatalog.LoadSpecialEffectOptions(RhodesPublicDebugPolicy.SarkazCampaignId, "age"))
            .ToArray();
        ReplaceCollection(ManualAgeOptions, ages);
        var currentAge = (_runState.SpecialFields ?? [])
            .FirstOrDefault(field => field.CampaignId == RhodesPublicDebugPolicy.SarkazCampaignId && field.FieldId == "age");
        SelectedManualAge = ages.FirstOrDefault(option => option.Name == currentAge?.Value) ?? ages[0];
    }

    private void RefreshManualSquadRandomEffectOptions()
    {
        var campaignId = SelectedCampaign?.Id ?? _runState.CampaignId;
        var squadId = ManualSquadRandomEffectSourceSquadId();
        var options = new[] { SukiSquadOption.KeepCurrent }
            .Concat(RhodesRunCatalog.LoadSquadRandomEffectOptions(campaignId, squadId))
            .ToArray();
        ReplaceCollection(ManualSquadRandomEffectOptions, options);
        SelectedManualSquadRandomEffect = options.FirstOrDefault(option =>
                !string.IsNullOrWhiteSpace(option.Id)
                && option.Name.Equals(_runState.SquadRandomEffect, StringComparison.Ordinal))
            ?? SukiSquadOption.KeepCurrent;
        OnPropertyChanged(nameof(IsManualSquadRandomEffectVisible));
    }

    private string ManualSquadRandomEffectSourceSquadId()
    {
        if (!string.IsNullOrWhiteSpace(SelectedManualSquad?.Id))
            return SelectedManualSquad.Id;

        return ManualSquadOptions.FirstOrDefault(option =>
                !string.IsNullOrWhiteSpace(option.Id)
                && option.Name.Equals(_runState.Squad, StringComparison.Ordinal))
            ?.Id ?? "";
    }

    private async Task ApplyCandidateResultsCoreAsync()
    {
        var result = await ApplyCandidatesPipelineAsync(CandidateResults.ToArray());
        if (ResourceTaskResults.Any())
        {
            var profileId = CandidateApiProfileId();
            var evidencePlan = CurrentEvidencePlan(profileId);
            LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
                ResourceTaskResults,
                profileId,
                CandidateResults,
                evidencePlan?.ProfileLabel,
                evidencePlan?.TaskEntries,
                evidencePlan,
                result.Summary,
                result.LocalFallbackUsed,
                result.ApiError);
            RefreshRecognitionScanHistory();
        }
        RefreshInspectorRows();
    }

    private async Task RunSelectedProfileRecognitionAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "選択プロファイルの認識を開始します。";
            if (await RunAllResourceTasksCoreAsync())
                await ConvertResourceTaskResultsCoreAsync();
        });
    }

    private async Task RunSelectedProfileRecognitionAndApplyAsync()
    {
        await RunBusyAsync(async () =>
        {
            StatusMessage = "選択プロファイルの認識と反映を開始します。";
            await RunSelectedProfileRecognitionAndApplyCoreAsync();
        });
    }

    private async Task RunProfileRecognitionAndApplyAsync(string? profileId)
    {
        await RunBusyAsync(async () =>
        {
            if (!TrySelectRecognitionProfile(profileId))
                return;

            StatusMessage = $"{SelectedResourceProfile?.DisplayName ?? "現在画面"}の撮影・認識・反映を開始します。";
            await RunSelectedProfileRecognitionAndApplyCoreAsync();
        });
    }

    private async Task RunCommonRecognitionAndApplyAsync()
    {
        string[] profileIds = CurrentCampaignId switch
        {
            "is5_sarkaz" => new[] { "runStatusFull", "is5AgeFull" },
            "is2_phantom" => ["runStatusFull", "is2HallucinationsFull", "is2PerformanceFull"],
            "is3_mizuki" => ["runStatusFull", "is3KeyFull", "is3LightHordeFull", "is3RejectionFull", "operatorsFull"],
            "is6_sui" => ["runStatusFull", "is6BaseFull"],
            _ => ["runStatusFull"],
        };
        await RunProfilesAndApplyAsync(profileIds, "共通値の撮影・認識・反映が完了しました。");
    }

    private async Task RunAllOperationalRecognitionAndApplyAsync()
    {
        string[] profileIds = CurrentCampaignId switch
        {
            "is5_sarkaz" => new[] { "runStatusFull", "is5ThoughtFull", "is5AgeFull", "operatorsFull", "relicsFull" },
            "is2_phantom" => ["runStatusFull", "is2HallucinationsFull", "is2PerformanceFull", "operatorsFull", "relicsFull"],
            "is3_mizuki" => ["runStatusFull", "is3KeyFull", "is3LightHordeFull", "is3RejectionFull", "operatorsFull", "relicsFull"],
            "is4_sami" => ["runStatusFull", "is4RevelationFull", "operatorsFull", "relicsFull"],
            "is6_sui" => ["runStatusFull", "is6BaseFull", "is6ActiveCoinsFull", "is6CoinsFull", "operatorsFull", "relicsFull"],
            _ => ["runStatusFull", "operatorsFull", "relicsFull"],
        };
        await RunProfilesAndApplyAsync(profileIds, "現在テーマの取得対象をすべて認識し、ラン状態へ反映しました。");
    }

    private async Task RunCurrentSpecialRecognitionAndApplyAsync()
    {
        string[] profileIds = CurrentCampaignId switch
        {
            "is5_sarkaz" => new[] { "is5ThoughtFull", "is5AgeFull" },
            "is2_phantom" => ["is2HallucinationsFull", "is2PerformanceFull"],
            "is3_mizuki" => ["is3KeyFull", "is3LightHordeFull", "is3RejectionFull"],
            "is4_sami" => ["is4RevelationFull"],
            "is6_sui" => ["is6ActiveCoinsFull", "is6CoinsFull"],
            _ => Array.Empty<string>(),
        };

        if (profileIds.Length == 0)
        {
            StatusMessage = "現在のテーマには自動取得する固有値がありません。";
            return;
        }

        await RunProfilesAndApplyAsync(profileIds, "現在テーマの固有値を認識し、ラン状態へ反映しました。");
    }

    private string CurrentCampaignId => SelectedCampaign?.Id ?? _runState.CampaignId;

    private async Task RunProfilesAndApplyAsync(IReadOnlyList<string> profileIds, string completionMessage)
    {
        await RunBusyAsync(async () =>
        {
            foreach (var profileId in profileIds)
            {
                if (!TrySelectRecognitionProfile(profileId))
                    return;

                StatusMessage = $"{SelectedResourceProfile?.DisplayName ?? profileId}の撮影・認識・反映を開始します。";
                if (!await RunSelectedProfileRecognitionAndApplyCoreAsync())
                    return;
            }

            TrySelectRecognitionProfile("runStatusFull");
            StatusMessage = completionMessage;
        });
    }

    private async Task<bool> RunSelectedProfileRecognitionAndApplyCoreAsync()
    {
        if (!await RunAllResourceTasksCoreAsync())
            return false;
        var converted = await ConvertResourceTaskResultsCoreAsync();
        EnsureAgeResetCandidateWhenUndetected();
        EnsureHallucinationResetCandidateWhenUndetected();
        EnsurePerformanceResetCandidateWhenUndetected();
        EnsureMizukiResetCandidatesWhenUndetected();
        if (!converted && CandidateResults.Count == 0)
            return false;
        await ApplyCandidateResultsCoreAsync();
        return true;
    }

    private void EnsureAgeResetCandidateWhenUndetected()
    {
        if (!string.Equals(SelectedResourceProfile?.Id, "is5AgeFull", StringComparison.Ordinal)
            || CandidateResults.Any(candidate => candidate.Kind.Equals("age", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CandidateResults.Add(RhodesRecognitionCandidateApplier.CreateNoAgeCandidate());
        StatusMessage = "時代を検出できなかったため、時代なしを反映します。";
    }

    private void EnsureHallucinationResetCandidateWhenUndetected()
    {
        if (!string.Equals(SelectedResourceProfile?.Id, "is2HallucinationsFull", StringComparison.Ordinal)
            || CandidateResults.Any(candidate => candidate.Field.Equals("hallucinations", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CandidateResults.Add(RhodesRecognitionCandidateApplier.CreateNoHallucinationCandidate());
        StatusMessage = "幻覚を検出できなかったため、幻覚なしを反映します。";
    }

    private void EnsurePerformanceResetCandidateWhenUndetected()
    {
        if (!string.Equals(SelectedResourceProfile?.Id, "is2PerformanceFull", StringComparison.Ordinal)
            || CandidateResults.Any(candidate => candidate.Field.Equals("performanceId", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CandidateResults.Add(RhodesRecognitionCandidateApplier.CreateNoPerformanceCandidate());
        StatusMessage = "演目を検出できなかったため、演目なしを反映します。";
    }

    private void EnsureMizukiResetCandidatesWhenUndetected()
    {
        if (string.Equals(SelectedResourceProfile?.Id, "is3LightHordeFull", StringComparison.Ordinal)
            && !CandidateResults.Any(candidate => candidate.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && candidate.FieldId.Equals("hordeCalls", StringComparison.Ordinal)))
        {
            CandidateResults.Add(RhodesRecognitionCandidateApplier.CreateNoMizukiHordeCallCandidate());
            StatusMessage = "大群の呼び声を検出できなかったため、選択なしを反映します。";
        }

        if (string.Equals(SelectedResourceProfile?.Id, "is3RejectionFull", StringComparison.Ordinal)
            && !CandidateResults.Any(candidate => candidate.Kind.Equals("mizuki", StringComparison.OrdinalIgnoreCase)
                && candidate.FieldId.Equals("rejectionReaction", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(candidate.EffectId)))
        {
            CandidateResults.Add(RhodesRecognitionCandidateApplier.CreateNoMizukiRejectionCandidate());
            StatusMessage = "拒絶反応を検出できなかったため、拒絶反応なしを反映します。";
        }
    }

    private bool TrySelectRecognitionProfile(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return SelectedResourceProfile is not null;

        if (!RhodesPublicDebugPolicy.IsProfileAllowed(profileId, _distributionProfile))
        {
            StatusMessage = $"公開デバッグ対象外の認識プロファイルです: {profileId}";
            return false;
        }

        var profile = ResourceProfiles.FirstOrDefault(item => string.Equals(item.Id, profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            StatusMessage = $"認識プロファイルが未定義です: {profileId}";
            return false;
        }

        SelectedResourceProfile = profile;
        _lastResourceExecutionPlan = null;
        return true;
    }

    private async Task RefreshRecognitionScanHistoryAsync()
    {
        await RunBusyAsync(() =>
        {
            RefreshRecognitionScanHistory();
            StatusMessage = $"認識履歴を更新しました: {RecognitionScanHistory.Count}件";
            return Task.CompletedTask;
        });
    }

    private async Task RefreshFrameRecordHistoryAsync()
    {
        await RunBusyAsync(() =>
        {
            RefreshFrameRecordHistory();
            StatusMessage = $"Frame Recordsを更新しました: {FrameRecordHistory.Count}件";
            return Task.CompletedTask;
        });
    }

    private void RefreshFrameRecordHistory()
    {
        ReplaceCollection(
            FrameRecordHistory,
            MergeFrameRecordHistory(
                RhodesFrameRecordHistory.LoadRecent(
                    RhodesSukiDebugPaths.FrameRecordsDirectory,
                    [_lastFrameMetadataPath]),
                string.IsNullOrWhiteSpace(_importedBugReportFrameRecordsDirectory)
                    ? []
                    : RhodesFrameRecordHistory.LoadRecent(_importedBugReportFrameRecordsDirectory, limit: 48)));
        OnPropertyChanged(nameof(IsFrameRecordHistoryEmpty));
        OnPropertyChanged(nameof(HasFrameRecordHistory));
        RefreshInspectorRows();
    }

    private static IReadOnlyList<RhodesFrameRecordHistoryItem> MergeFrameRecordHistory(
        IEnumerable<RhodesFrameRecordHistoryItem> primary,
        IEnumerable<RhodesFrameRecordHistoryItem> imported)
    {
        return primary
            .Concat(imported)
            .GroupBy(item => item.MetadataPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.SortTimestamp)
            .ThenBy(item => item.MetadataPath, StringComparer.OrdinalIgnoreCase)
            .Take(48)
            .ToArray();
    }

    private async Task LoadFrameRecordHistoryAsync(RhodesFrameRecordHistoryItem? item)
    {
        if (item is null)
            return;

        await RunBusyAsync(async () =>
        {
            if (await LoadFrameRecordImageCoreAsync(item))
                StatusMessage = $"Frame Recordを読み込みました: {item.FrameId}";
        });
    }

    private async Task ReplayFrameRecordRecognitionAsync(RhodesFrameRecordHistoryItem? item)
    {
        await RunBusyAsync(async () =>
        {
            if (item is not null)
            {
                if (!await LoadFrameRecordImageCoreAsync(item))
                    return;

                if (RhodesPublicDebugPolicy.IsProfileAllowed(item.ProfileId, _distributionProfile))
                {
                    SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile => profile.Id == item.ProfileId) ?? SelectedResourceProfile;
                    _lastResourceExecutionPlan = null;
                }
            }

            if (_lastCapture.Length == 0)
            {
                StatusMessage = "再実行するFrame Record画像がありません。";
                return;
            }

            if (!EnsureMaaOcrReadyForRecognition())
                return;
            if (!await EnsureMaaOfflineRecognitionReadyAsync())
                return;

            var plan = CurrentResourceExecutionPlan;
            if (!plan.CanRun)
            {
                StatusMessage = plan.Summary;
                return;
            }

            _lastResourceExecutionPlan = plan;
            ClearRoiRescanComparison();
            ResourceTaskResults.Clear();
            CandidateResults.Clear();
            RecognitionScanLogRows.Clear();
            LastCandidateApplySummary = "保存Frame再実行: 候補未反映";
            RefreshResourceTaskDiagnostics();
            RefreshInspectorRows();

            var runtimePlan = RhodesRecognitionRuntimePlan.PrepareInitial(plan);
            var execution = await RhodesRecognitionWorkflow.RunResourceTasksAsync(
                runtimePlan,
                (entry, cancellationToken) =>
                {
                    var payload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(entry);
                    return _session.RunResourceRecognitionAsync(entry, payload, _lastCapture, cancellationToken);
                },
                result =>
                {
                    ResourceTaskResults.Add(result);
                    RefreshResourceTaskDiagnostics();
                    StatusMessage = $"{result.Entry}: {result.Status}";
                });
            if (!execution.Succeeded)
            {
                StatusMessage = execution.Error;
                RefreshInspectorRows();
                return;
            }

            await RunTemplateOcrExpansionsAsync(execution.TaskResults, _lastCapture);
            await RunThoughtLoadOcrExpansionsAsync(execution.TaskResults, _lastCapture);

            RefreshInspectorRows();
            var localCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
                plan.ProfileId,
                ResourceTaskResults,
                SelectedCampaign?.Id ?? _runState.CampaignId);
            LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
                ResourceTaskResults,
                plan.ProfileId,
                localCandidates,
                plan.ProfileLabel,
                plan.TaskEntries,
                plan);
            await ConvertResourceTaskResultsCoreAsync();
            RefreshRecognitionScanHistory();
            StatusMessage = $"保存Frameを再認識しました: {plan.ProfileLabel} / task{ResourceTaskResults.Count} / 候補{CandidateResults.Count}";
        });
    }

    private async Task<bool> LoadFrameRecordImageCoreAsync(RhodesFrameRecordHistoryItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ImagePath) || !File.Exists(item.ImagePath))
        {
            StatusMessage = $"Frame Recordの画像が見つかりません: {item.ImagePath}";
            return false;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(item.ImagePath);
            _lastCapture = bytes;
            LastCaptureImage = CreateCaptureBitmap(bytes);
            LastCapturePath = item.ImagePath;
            _lastFrameId = item.FrameId;
            _lastFrameMetadataPath = item.MetadataPath;
            _lastFrameStateSnapshotPath = item.StateSnapshotPath;
            CaptureState = $"Frame Record読込: {item.FrameId} / {item.ProfileId}";
            RefreshFrameRecordHistory();
            RefreshInspectorRows();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Frame Recordの読込に失敗しました: {ex.Message}";
            return false;
        }
    }

    private async Task<bool> EnsureMaaOfflineRecognitionReadyAsync()
    {
        if (_session.IsTaskerReady)
            return true;

        StatusMessage = "MAA Resourceをオフライン認識用に初期化しています。";
        var snapshot = await _session.InitializeOfflineAsync(BuildSessionOptions());
        ApplyMaaSessionSnapshot(
            snapshot,
            snapshot.IsReady
                ? "MAA Resourceをオフライン認識用に初期化しました。"
                : "MAA Resourceのオフライン初期化に失敗しました。");
        return snapshot.IsReady;
    }

    private void RefreshRecognitionScanHistory()
    {
        ReplaceCollection(
            RecognitionScanHistory,
            MergeRecognitionScanHistory(
                RhodesRecognitionScanHistory.LoadRecent(
                    RhodesSukiDebugPaths.RecognitionScansDirectory,
                    [LastResourceTaskResultsPath]),
                string.IsNullOrWhiteSpace(_importedBugReportRecognitionScansDirectory)
                    ? []
                    : RhodesRecognitionScanHistory.LoadRecent(_importedBugReportRecognitionScansDirectory, limit: 48)));
        RefreshInspectorRows();
    }

    private static IReadOnlyList<RhodesRecognitionScanHistoryItem> MergeRecognitionScanHistory(
        IEnumerable<RhodesRecognitionScanHistoryItem> primary,
        IEnumerable<RhodesRecognitionScanHistoryItem> imported)
    {
        return primary
            .Concat(imported)
            .GroupBy(item => item.LogPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.SortTimestamp)
            .ThenBy(item => item.LogPath, StringComparer.OrdinalIgnoreCase)
            .Take(48)
            .ToArray();
    }

    private void RefreshRoiAdjustmentSessions()
    {
        ReplaceCollection(
            RoiAdjustmentSessions,
            RhodesMaaRoiAdjustmentSessionLog.LoadRecent(RhodesSukiDebugPaths.RoiSessionsDirectory));
        RefreshInspectorRows();
    }

    private async Task LoadRecognitionScanHistoryAsync(RhodesRecognitionScanHistoryItem? item)
    {
        if (item is null)
            return;

        await RunBusyAsync(() =>
        {
            var payload = RhodesRecognitionScanHistory.LoadPayload(item.LogPath);
            if (!payload.Succeeded)
            {
                StatusMessage = $"認識履歴の読込に失敗しました: {payload.Error}";
                return Task.CompletedTask;
            }

            LoadRecognitionPayload(payload, item.LogPath, "履歴から読込");
            StatusMessage = $"認識履歴を読み込みました: 候補{CandidateResults.Count}件 / task{ResourceTaskResults.Count}件 / log{RecognitionScanLogRows.Count}件";
            return Task.CompletedTask;
        });
    }

    private void LoadRecognitionPayload(
        RhodesRecognitionScanHistoryPayload payload,
        string logPath,
        string candidateApplySummary)
    {
        ClearRoiRescanComparison();
        if (!string.IsNullOrWhiteSpace(payload.ProfileId))
        {
            SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile =>
                profile.Id.Equals(payload.ProfileId, StringComparison.OrdinalIgnoreCase)) ?? SelectedResourceProfile;
            _lastResourceExecutionPlan = null;
        }

        var replayCandidates = payload.Candidates;
        if (replayCandidates.Count == 0 && payload.TaskResults.Count > 0)
        {
            replayCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
                payload.ProfileId,
                payload.TaskResults,
                SelectedCampaign?.Id ?? _runState.CampaignId);
            candidateApplySummary = $"保存ログ再候補化(local): {payload.ProfileId} / 候補{replayCandidates.Count}件";
        }

        CandidateResults.Clear();
        foreach (var candidate in replayCandidates)
        {
            CandidateResults.Add(candidate);
        }

        ResourceTaskResults.Clear();
        RecognitionScanLogRows.Clear();
        foreach (var taskResult in payload.TaskResults)
        {
            ResourceTaskResults.Add(taskResult);
        }

        foreach (var logRow in payload.LogRows)
        {
            RecognitionScanLogRows.Add(logRow);
        }
        TryLoadCapturePreviewFromPath(payload.FirstImagePath);
        _lastFrameId = payload.FrameId;
        _lastFrameMetadataPath = payload.FrameMetadataPath;
        _lastFrameStateSnapshotPath = payload.StateSnapshotPath;

        LastResourceTaskResultsPath = logPath;
        LastCandidateApplySummary = candidateApplySummary;
        RefreshResourceTaskDiagnostics();
        RefreshInspectorRows();
    }

    private void ClearRecognitionPayload(string logPath, string candidateApplySummary)
    {
        ClearRoiRescanComparison();
        CandidateResults.Clear();
        ResourceTaskResults.Clear();
        RecognitionScanLogRows.Clear();
        LastResourceTaskResultsPath = logPath;
        LastCandidateApplySummary = candidateApplySummary;
        RefreshResourceTaskDiagnostics();
        RefreshInspectorRows();
    }

    private Task OpenPreviewUrlAsync(object? parameter)
    {
        var path = parameter as string;
        var url = RhodesPreviewUrlBuilder.Build(RhodesApiUrl, path ?? "/sidecar");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            StatusMessage = $"プレビューを開きました: {url}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"プレビューを開けませんでした: {ex.Message}";
        }

        return Task.CompletedTask;
    }

    public void MarkObsUrlCopied(string? url)
    {
        StatusMessage = string.IsNullOrWhiteSpace(url)
            ? "OBS URLをコピーできませんでした。"
            : $"OBSへ貼り付けるURLをコピーしました: {url}";
    }

    public void SelectOverlayLayoutItem(SukiOverlayLayoutPreview? item)
    {
        if (item is not null)
            SelectedOverlayLayoutItem = item;
    }

    public void MoveOverlayLayoutItem(SukiOverlayLayoutPreview? item, double previewDeltaX, double previewDeltaY)
    {
        if (item is null)
            return;
        SelectedOverlayLayoutItem = item;
        item.X += (int)Math.Round(previewDeltaX * 2, MidpointRounding.AwayFromZero);
        item.Y += (int)Math.Round(previewDeltaY * 2, MidpointRounding.AwayFromZero);
        StatusMessage = $"{item.Label}を移動しました。保存して反映するとOBSへ反映されます。";
    }

    public void ResizeOverlayLayoutItem(SukiOverlayLayoutPreview? item, double previewDeltaX, double previewDeltaY)
    {
        if (item is null)
            return;
        SelectedOverlayLayoutItem = item;
        item.Width += (int)Math.Round(previewDeltaX * 2, MidpointRounding.AwayFromZero);
        item.Height += (int)Math.Round(previewDeltaY * 2, MidpointRounding.AwayFromZero);
        StatusMessage = $"{item.Label}のサイズを変更しました。保存して反映するとOBSへ反映されます。";
    }

    private Task ResetOverlayLayoutAsync()
    {
        ApplyOverlayLayoutStates(RhodesOverlayLayoutCatalog.BuildDefaultStates());
        StatusMessage = "Overlayレイアウトを初期配置へ戻しました。保存して反映するとOBSへ反映されます。";
        return Task.CompletedTask;
    }

    private Task AdjustSelectedOverlayLayoutAsync(object? parameter)
    {
        var item = SelectedOverlayLayoutItem;
        if (item is null)
            return Task.CompletedTask;

        switch (parameter as string)
        {
            case "left": item.X -= 8; break;
            case "right": item.X += 8; break;
            case "up": item.Y -= 8; break;
            case "down": item.Y += 8; break;
            case "narrower": item.Width -= 16; break;
            case "wider": item.Width += 16; break;
            case "shorter": item.Height -= 16; break;
            case "taller": item.Height += 16; break;
            case "back": item.ZIndex -= 1; break;
            case "front": item.ZIndex += 1; break;
        }
        StatusMessage = $"{item.Label}を調整しました。保存して反映するとOBSへ反映されます。";
        return Task.CompletedTask;
    }

    private void ApplyOverlayLayoutStates(IEnumerable<SukiOverlayLayoutState>? states)
    {
        var normalized = RhodesOverlayLayoutCatalog.Normalize(states);
        foreach (var state in normalized)
        {
            var item = OverlayLayoutItems.FirstOrDefault(candidate => candidate.Id == state.Id);
            if (item is null)
                continue;
            item.Enabled = state.Enabled;
            item.Width = state.Width;
            item.Height = state.Height;
            item.X = state.X;
            item.Y = state.Y;
            item.ZIndex = state.ZIndex;
        }
    }

    private async Task<string> SyncRunStateFromApiCoreAsync()
    {
        var result = await RhodesSukiStateSyncWorkflow.SyncFromApiAsync(
            cancellationToken => RhodesStateApiClient.FetchAsync(RhodesApiUrl, cancellationToken: cancellationToken),
            (stateJson, _) => RhodesRunStateStore.ReplaceStateJsonAsync(stateJson));

        _rhodesApiStatus = result.ApiStatus;
        RefreshRuntimeCapabilities();
        if (result.ShouldReloadRunState)
            ReloadRunStateFromStore();
        return result.Error;
    }

    private async Task<bool> RunAllResourceTasksCoreAsync()
    {
        var plan = CurrentResourceExecutionPlan;
        _lastResourceExecutionPlan = null;
        ClearRoiRescanComparison();
        ResourceTaskResults.Clear();
        CandidateResults.Clear();
        RecognitionScanLogRows.Clear();
        LastCandidateApplySummary = "候補未反映";
        RefreshResourceTaskDiagnostics();
        RefreshInspectorRows();
        if (!plan.CanRun)
        {
            StatusMessage = plan.Summary;
            return false;
        }
        if (!EnsureMaaOcrReadyForRecognition())
            return false;

        RhodesRecognitionNavigationPlan navigation;
        try
        {
            navigation = RhodesRecognitionNavigation.LoadDefault(plan.ProfileId);
        }
        catch (Exception ex)
        {
            StatusMessage = $"認識画面操作を読み込めません: {ex.Message}";
            return false;
        }

        var openResult = await RhodesRecognitionNavigation.ExecuteAsync(
            navigation.OpenSteps,
            _session.TapAsync);
        if (!openResult.Succeeded)
        {
            StatusMessage = $"認識画面を開けません: {openResult.Detail}";
            return false;
        }

        var shouldRestoreTarget = plan.ProfileId != "operatorsFull";
        _lastResourceExecutionPlan = plan;
        try
        {
            StatusMessage = $"MAA実行計画: {plan.Summary} / {openResult.Detail}";
            if (!await ForceCaptureAsync())
                return false;
            if (_lastCapture.Length == 0)
            {
                StatusMessage = "認識に渡すスクリーンショットがありません。";
                RefreshInspectorRows();
                return false;
            }

            var runtimePlan = RhodesRecognitionRuntimePlan.PrepareInitial(plan);
            var execution = await RhodesRecognitionWorkflow.RunResourceTasksAsync(
                runtimePlan,
                (entry, cancellationToken) =>
                {
                    var payload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(entry);
                    return _session.RunResourceRecognitionAsync(entry, payload, _lastCapture, cancellationToken);
                },
                result =>
                {
                    ResourceTaskResults.Add(result);
                    RefreshResourceTaskDiagnostics();
                    StatusMessage = $"{result.Entry}: {result.Status}";
                });
            if (!execution.Succeeded)
            {
                StatusMessage = execution.Error;
                RefreshInspectorRows();
                return false;
            }

            if (!RhodesRecognitionRuntimePlan.IsTargetScreenConfirmed(plan.ProfileId, execution.TaskResults))
            {
                if (ResourceTaskResults.Any())
                {
                    var failedScreenCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
                        plan.ProfileId,
                        ResourceTaskResults,
                        SelectedCampaign?.Id ?? _runState.CampaignId);
                    LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
                        ResourceTaskResults,
                        plan.ProfileId,
                        failedScreenCandidates,
                        plan.ProfileLabel,
                        plan.TaskEntries,
                        plan);
                }
                StatusMessage = plan.ProfileId switch
                {
                    "runStatusFull" => "分隊情報画面を確認できません。左下の分隊アイコンが表示されたマップ画面から再実行してください。",
                    "relicsFull" => "秘宝一覧を確認できません。マップ画面から再実行してください。戦闘・イベント詳細画面では秘宝認識を実行できません。",
                    "is5AgeFull" => "時代詳細画面を確認できません。時代が発生していない場合は候補なしになります。",
                    _ => "オペレーターカードの基準点を確認できません。隊員一覧が開いているか確認して再実行してください。",
                };
                RefreshInspectorRows();
                return false;
            }
            shouldRestoreTarget = true;

            var operatorScanTracker = plan.ProfileId == "operatorsFull"
                ? new RhodesOperatorScanTracker()
                : null;
            await RunTemplateOcrExpansionsAsync(
                execution.TaskResults,
                _lastCapture,
                operatorScanTracker: operatorScanTracker);
            await RunThoughtLoadOcrExpansionsAsync(execution.TaskResults, _lastCapture);
            await RetryLowConfidenceInitialFrameAsync(plan, runtimePlan, operatorScanTracker);
            await RunScrollRecognitionFramesAsync(plan, operatorScanTracker);
            RefreshInspectorRows();
            if (ResourceTaskResults.Any())
            {
                var localCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
                    plan.ProfileId,
                    ResourceTaskResults,
                    SelectedCampaign?.Id ?? _runState.CampaignId);
                LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
                    ResourceTaskResults,
                    plan.ProfileId,
                    localCandidates,
                    plan.ProfileLabel,
                    plan.TaskEntries,
                    plan);
                StatusMessage = $"MAA scan証跡を保存しました: {LastResourceTaskResultsPath}";
            }
            return ResourceTaskResults.Any();
        }
        finally
        {
            if (shouldRestoreTarget)
            {
                var restoreResult = await RhodesRecognitionNavigation.ExecuteAsync(
                    navigation.RestoreSteps,
                    _session.TapAsync);
                if (!restoreResult.Succeeded)
                    StatusMessage = $"認識後の画面復元に失敗しました: {restoreResult.Detail}";
            }
        }
    }

    private async Task RunTemplateOcrExpansionsAsync(
        IEnumerable<MaaTaskRunResult> templateResults,
        byte[] encodedImage,
        CancellationToken cancellationToken = default,
        RhodesOperatorScanTracker? operatorScanTracker = null)
    {
        if (string.Equals(CandidateApiProfileId(), "is6ActiveCoinsFull", StringComparison.Ordinal)
            && encodedImage.Length > 0)
        {
            var coinResult = RhodesSuiCoinImageRecognizer.Recognize(encodedImage);
            ResourceTaskResults.Add(coinResult);
            RefreshResourceTaskDiagnostics();
            StatusMessage = $"有効銭画像分類: {coinResult.Detail}";
        }

        foreach (var templateResult in templateResults.Where(result =>
                     result.Succeeded
                     && result.Hit
                     && result.Algorithm.Equals("TemplateMatch", StringComparison.OrdinalIgnoreCase)))
        {
            var config = RhodesMaaResourceCatalog.LoadTemplateOcrConfig(templateResult.Entry);
            if (config is null)
                continue;

            var requests = RhodesMaaTemplateOcrExpander.BuildRequests(config, templateResult.RecognitionDetailJson);
            if (operatorScanTracker is not null
                && config.IdPrefix.Equals("operator.card.name", StringComparison.Ordinal))
            {
                var selection = operatorScanTracker.Select(encodedImage, requests);
                StatusMessage = $"オペレーター表示{selection.VisibleCardCount}件 / OCR対象{selection.WorkItems.Count}件";
                foreach (var workItem in selection.WorkItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var request = workItem.Request;
                    var cardResults = new List<MaaTaskRunResult>();
                    var result = await _session.RunResourceRecognitionAsync(
                        request.Entry,
                        request.PayloadJson,
                        encodedImage,
                        cancellationToken,
                        request.Scale);
                    ResourceTaskResults.Add(result);
                    cardResults.Add(result);
                    var roleRequest = RhodesMaaAmiyaRoleResolver.BuildRequest(request, result);
                    if (roleRequest is not null)
                    {
                        var roleResult = await _session.RunResourceRecognitionAsync(
                            roleRequest.Entry,
                            roleRequest.PayloadJson,
                            encodedImage,
                            cancellationToken);
                        ResourceTaskResults.Add(roleResult);
                        cardResults.Add(roleResult);
                    }
                    var resolvedOperators = RhodesMaaLocalCandidateConverter.FromTaskResults(
                        "operatorsFull",
                        cardResults)
                        .Where(candidate => candidate.Kind.Equals("operator", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (IsMizukiCampaignSelected && resolvedOperators.Length == 1)
                    {
                        var rejectionDetection = RhodesMizukiRejectionCardDetector.Detect(encodedImage, request);
                        if (rejectionDetection.IsAffected)
                        {
                            ResourceTaskResults.Add(RhodesMizukiRejectionCardDetector.CreateTaskResult(
                                request,
                                resolvedOperators[0],
                                rejectionDetection));
                        }
                    }
                    var resolved = resolvedOperators.Length > 0;
                    operatorScanTracker.RecordResult(workItem.Fingerprint, resolved);
                    RefreshResourceTaskDiagnostics();
                    StatusMessage = $"{request.Entry}: {result.Status}";
                }
                continue;
            }

            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _session.RunResourceRecognitionAsync(
                    request.Entry,
                    request.PayloadJson,
                    encodedImage,
                    cancellationToken,
                    request.Scale);
                ResourceTaskResults.Add(result);
                RefreshResourceTaskDiagnostics();
                StatusMessage = $"{request.Entry}: {result.Status}";
            }
        }
    }

    private async Task RunThoughtLoadOcrExpansionsAsync(
        IEnumerable<MaaTaskRunResult> frameResults,
        byte[] encodedImage,
        CancellationToken cancellationToken = default)
    {
        var resolvedThoughtIds = ResourceTaskResults
            .Where(result => result.Succeeded
                && result.Hit
                && RhodesMaaThoughtLoadOcrExpander.TryReadDisplayedLoad(result.RecognitionDetailJson, out _))
            .Select(result => result.Entry)
            .Where(entry => entry.StartsWith("thought.card.load.", StringComparison.Ordinal))
            .Select(entry => entry["thought.card.load.".Length..])
            .ToHashSet(StringComparer.Ordinal);

        foreach (var thoughtListResult in frameResults.Where(result =>
                     result.Succeeded
                     && result.Hit
                     && result.Entry.Equals("RhodesOcrRegion_is5_thought_list_text", StringComparison.Ordinal)))
        {
            var requests = RhodesMaaThoughtLoadOcrExpander.BuildRequests(
                thoughtListResult.RecognitionDetailJson,
                resolvedThoughtIds);
            foreach (var request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _session.RunResourceRecognitionAsync(
                    request.Entry,
                    request.PayloadJson,
                    encodedImage,
                    cancellationToken,
                    request.Scale);
                ResourceTaskResults.Add(result);
                if (result.Succeeded
                    && result.Hit
                    && RhodesMaaThoughtLoadOcrExpander.TryReadDisplayedLoad(result.RecognitionDetailJson, out _))
                    resolvedThoughtIds.Add(request.Entry["thought.card.load.".Length..]);
                RefreshResourceTaskDiagnostics();
                StatusMessage = $"{request.Entry}: {result.Status}";
            }
        }
    }

    private async Task RetryLowConfidenceInitialFrameAsync(
        MaaResourceExecutionPlan plan,
        MaaResourceExecutionPlan runtimePlan,
        RhodesOperatorScanTracker? operatorScanTracker,
        CancellationToken cancellationToken = default)
    {
        var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
            plan.ProfileId,
            ResourceTaskResults,
            SelectedCampaign?.Id ?? _runState.CampaignId);
        var decision = RhodesRecognitionRetryPolicy.Evaluate(
            plan.ProfileId,
            ResourceTaskResults,
            candidates,
            SelectedCampaign?.Id ?? _runState.CampaignId);
        if (!decision.ShouldRetry)
            return;

        StatusMessage = $"低信頼フレームを1回だけ再撮影します: {decision.Reason}";
        await Task.Delay(80, cancellationToken);
        if (!await ForceCaptureAsync() || _lastCapture.Length == 0)
            return;

        var retryExecution = await RhodesRecognitionWorkflow.RunResourceTasksAsync(
            runtimePlan,
            (entry, token) =>
            {
                var payload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(entry);
                return _session.RunResourceRecognitionAsync(entry, payload, _lastCapture, token);
            },
            result =>
            {
                ResourceTaskResults.Add(result);
                RefreshResourceTaskDiagnostics();
            },
            cancellationToken);
        if (!retryExecution.Succeeded)
        {
            StatusMessage = $"低信頼フレームの再試行に失敗しました: {retryExecution.Error}";
            return;
        }

        await RunTemplateOcrExpansionsAsync(
            retryExecution.TaskResults,
            _lastCapture,
            cancellationToken,
            operatorScanTracker);
        await RunThoughtLoadOcrExpansionsAsync(retryExecution.TaskResults, _lastCapture, cancellationToken);
    }

    private async Task RunScrollRecognitionFramesAsync(
        MaaResourceExecutionPlan plan,
        RhodesOperatorScanTracker? operatorScanTracker = null,
        CancellationToken cancellationToken = default)
    {
        if (!RhodesRecognitionRuntimePlan.IsScrollProfile(plan.ProfileId))
            return;

        var passes = RhodesRecognitionScrollPlan.LoadDefault(plan.ProfileId);
        if (passes.Count == 0)
            return;

        var taskEntries = ScrollRecognitionTaskEntries(plan).ToArray();
        if (taskEntries.Length == 0)
            return;
        var initialCandidateCount = CurrentLocalCandidateCount(plan.ProfileId);
        var expectedCandidateCount = plan.ProfileId == "relicsFull"
            ? RhodesRelicOwnedCountReader.FromTaskResults(ResourceTaskResults)?.Count
            : null;
        if (RhodesRecognitionRuntimePlan.ShouldSkipScroll(plan.ProfileId, initialCandidateCount, expectedCandidateCount))
        {
            StatusMessage = $"秘宝候補{initialCandidateCount}/{expectedCandidateCount}件: 所持数と一致したため終了しました。";
            return;
        }
        var scanRegion = RhodesRecognitionScrollPlan.LoadScanRegionDefault(plan.ProfileId);

        var previousPassScrolls = 0;
        var reachedExpectedCandidateCount = false;
        foreach (var pass in passes)
        {
            var maxScrolls = pass.MirrorPreviousPassScrolls
                ? Math.Min(pass.MaxScrolls, previousPassScrolls)
                : pass.MaxScrolls;
            var executedScrolls = 0;
            var stableFrames = 0;
            var stableCandidates = 0;
            var previousFingerprint = RhodesRecognitionFrameFingerprint.Compute(_lastCapture, scanRegion);
            var previousCandidateCount = CurrentLocalCandidateCount(plan.ProfileId);

            for (var index = 0; index < maxScrolls; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var swipe = RhodesRecognitionScrollPlan.RandomSwipe(pass);
                var swipeStatus = await _session.SwipeAsync(
                    swipe.StartX,
                    swipe.StartY,
                    swipe.EndX,
                    swipe.EndY,
                    pass.DurationMs,
                    cancellationToken);
                if (swipeStatus != MaaJobStatus.Succeeded)
                {
                    StatusMessage = $"{pass.Label}: swipe失敗 ({swipeStatus})";
                    break;
                }

                executedScrolls++;
                if (pass.CaptureDelayMs > 0)
                    await Task.Delay(pass.CaptureDelayMs, cancellationToken);
                if (!await ForceCaptureAsync())
                    break;

                var fingerprint = RhodesRecognitionFrameFingerprint.Compute(_lastCapture, scanRegion);
                stableFrames = RhodesRecognitionFrameFingerprint.Distance(fingerprint, previousFingerprint) <= 2
                    ? stableFrames + 1
                    : 0;
                previousFingerprint = fingerprint;

                if (pass.CollectCandidates)
                {
                    await RunScrollFrameTasksAsync(
                        taskEntries,
                        _lastCapture,
                        cancellationToken,
                        operatorScanTracker);
                }

                var candidateCount = CurrentLocalCandidateCount(plan.ProfileId);
                stableCandidates = candidateCount == previousCandidateCount
                    ? stableCandidates + 1
                    : 0;
                previousCandidateCount = candidateCount;
                var candidateProgress = expectedCandidateCount.HasValue
                    ? $"{candidateCount}/{expectedCandidateCount.Value}"
                    : candidateCount.ToString();
                StatusMessage = $"{pass.Label}: {executedScrolls}/{maxScrolls} / 候補{candidateProgress}件";

                if (RhodesRecognitionRuntimePlan.HasReachedExpectedCandidateCount(
                        plan.ProfileId,
                        candidateCount,
                        expectedCandidateCount))
                {
                    reachedExpectedCandidateCount = true;
                    break;
                }

                if (executedScrolls >= pass.MinScrolls
                    && operatorScanTracker?.CanStopCurrentViewport == true)
                {
                    break;
                }
                if (RhodesRecognitionRuntimePlan.HasReachedScrollEnd(
                        executedScrolls,
                        pass.MinScrolls,
                        stableFrames,
                        pass.EndFingerprintStableCount,
                        stableCandidates,
                        pass.CandidateStableEndCount))
                {
                    break;
                }
            }

            previousPassScrolls = executedScrolls;
            if (reachedExpectedCandidateCount)
            {
                StatusMessage = $"秘宝候補{CurrentLocalCandidateCount(plan.ProfileId)}/{expectedCandidateCount}件: 所持数と一致したため終了しました。";
                break;
            }
            if (plan.ProfileId == "operatorsFull"
                && stableFrames > 0
                && operatorScanTracker?.CanStopScan == true)
            {
                StatusMessage = "表示済みオペレーターをすべて解決したためスキャンを終了しました。";
                break;
            }
        }
    }

    private IEnumerable<string> ScrollRecognitionTaskEntries(MaaResourceExecutionPlan plan)
    {
        if (plan.ProfileId == "operatorsFull")
        {
            return plan.TaskEntries.Where(entry =>
                RhodesMaaResourceCatalog.LoadTemplateOcrConfig(entry)?.IdPrefix == "operator.card.name");
        }

        if (plan.ProfileId == "relicsFull")
        {
            return plan.TaskEntries.Where(entry =>
                entry.Contains("relic_list_text", StringComparison.OrdinalIgnoreCase));
        }

        if (plan.ProfileId == "is5ThoughtFull")
        {
            return plan.TaskEntries.Where(entry =>
                entry.Contains("is5_thought_list_text", StringComparison.OrdinalIgnoreCase));
        }

        return [];
    }

    private async Task RunScrollFrameTasksAsync(
        IEnumerable<string> taskEntries,
        byte[] encodedImage,
        CancellationToken cancellationToken,
        RhodesOperatorScanTracker? operatorScanTracker)
    {
        var frameResults = new List<MaaTaskRunResult>();
        foreach (var entry in taskEntries)
        {
            var payload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(entry);
            var result = await _session.RunResourceRecognitionAsync(entry, payload, encodedImage, cancellationToken);
            ResourceTaskResults.Add(result);
            frameResults.Add(result);
            RefreshResourceTaskDiagnostics();
        }

        await RunTemplateOcrExpansionsAsync(
            frameResults,
            encodedImage,
            cancellationToken,
            operatorScanTracker);
        await RunThoughtLoadOcrExpansionsAsync(frameResults, encodedImage, cancellationToken);
    }

    private int CurrentLocalCandidateCount(string profileId) =>
        RhodesMaaLocalCandidateConverter.FromTaskResults(
            profileId,
            ResourceTaskResults,
            SelectedCampaign?.Id ?? _runState.CampaignId).Count;


    private async Task<bool> ConvertResourceTaskResultsCoreAsync()
    {
        if (!ResourceTaskResults.Any())
        {
            StatusMessage = "先にResource taskを実行してください。";
            return false;
        }

        var profileId = CandidateApiProfileId();
        var evidencePlan = CurrentEvidencePlan(profileId);
        var apiResult = await RhodesMaaCandidateApiClient.ConvertAsync(
            RhodesApiUrl,
            profileId,
            ResourceTaskResults);
        var conversion = RhodesRecognitionWorkflow.ConvertCandidates(
            profileId,
            ResourceTaskResults,
            apiResult,
            SelectedCampaign?.Id ?? _runState.CampaignId);

        CandidateResults.Clear();
        foreach (var candidate in conversion.Candidates)
            CandidateResults.Add(candidate);

        OnPropertyChanged(nameof(RecognitionDebugSummary));
        RefreshInspectorRows();
        LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
            ResourceTaskResults,
            profileId,
            CandidateResults,
            evidencePlan?.ProfileLabel,
            evidencePlan?.TaskEntries,
            evidencePlan);
        StatusMessage = conversion.StatusMessage;
        return CandidateResults.Count > 0;
    }

    private string? CandidateApiProfileId()
    {
        var profileId = _lastResourceExecutionPlan?.CanRun == true
            ? _lastResourceExecutionPlan.ProfileId
            : SelectedResourceProfile?.Id;
        return string.IsNullOrWhiteSpace(profileId) || profileId == "all" ? null : profileId;
    }

    private MaaResourceExecutionPlan? CurrentEvidencePlan(string? profileId)
    {
        return _lastResourceExecutionPlan?.CanRun == true
            && string.Equals(_lastResourceExecutionPlan.ProfileId, profileId, StringComparison.Ordinal)
            ? _lastResourceExecutionPlan
            : null;
    }

    private async Task RunProbeAsync(MaaProbePayloadPreview? payload)
    {
        if (payload is null)
            return;

        await RunBusyAsync(async () =>
        {
            if (!await EnsureCaptureAsync())
                return;

            await RunProbeCoreAsync(payload);
        });
    }

    private async Task RunResourceTaskAsync(MaaResourceTaskPreview? task)
    {
        if (task is null)
            return;

        await RunBusyAsync(async () =>
        {
            ClearRoiRescanComparison();
            _lastResourceExecutionPlan = null;
            if (!await ForceCaptureAsync())
                return;
            if (_lastCapture.Length == 0)
            {
                StatusMessage = "認識に渡すスクリーンショットがありません。";
                return;
            }

            var payload = RhodesMaaResourceCatalog.LoadRecognitionPayloadJson(task.Entry);
            var result = await _session.RunResourceRecognitionAsync(task.Entry, payload, _lastCapture);
            ResourceTaskResults.Add(result);
            RefreshResourceTaskDiagnostics();
            RefreshInspectorRows();
            LastResourceTaskResultsPath = await SaveResourceTaskResultsAsync(
                ResourceTaskResults,
                SelectedResourceProfile?.Id,
                RhodesMaaLocalCandidateConverter.FromTaskResults(
                    CandidateApiProfileId(),
                    ResourceTaskResults,
                    SelectedCampaign?.Id ?? _runState.CampaignId),
                executionPlan: CurrentResourceExecutionPlan);
            StatusMessage = $"{task.Entry}: {result.Status} / 証跡保存: {LastResourceTaskResultsPath}";
        });
    }

    private void RefreshResourceTaskDiagnostics()
    {
        ResourceTaskDiagnostics = RhodesMaaTaskDiagnostics.Summarize(ResourceTaskResults);
        ReplaceCollection(OcrDetailRows, RhodesMaaOcrDetailRows.FromTaskResults(ResourceTaskResults));
        ReplaceCollection(RoiDetailRows, RhodesMaaRoiDetailRows.FromTaskResults(ResourceTaskResults));
        RefreshRoiPreviewRows();
        OnPropertyChanged(nameof(RecognitionDebugSummary));
    }

    private void RefreshRoiPreviewRows()
    {
        ReplaceCollection(
            RoiPreviewRows,
            RhodesMaaRoiPreviewProjector.Project(
                RoiDetailRows,
                BaseResolution,
                _capturePixelWidth,
                _capturePixelHeight));
        var selected = string.IsNullOrWhiteSpace(_selectedRoiPreviewKey)
            ? null
            : RoiPreviewRows.FirstOrDefault(row => row.Key == _selectedRoiPreviewKey);
        if (!Equals(_selectedRoiPreviewRow, selected))
        {
            _selectedRoiPreviewRow = selected;
            OnPropertyChanged(nameof(SelectedRoiPreviewRow));
        }
        RefreshSelectedRoiPreviewRows();
        RefreshRoiBatchDrafts();
        OnPropertyChanged(nameof(RoiProjectionLabel));
    }

    private void RefreshRoiBatchDrafts()
    {
        var previous = RoiBatchDrafts.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var drafts = RoiPreviewRows
            .Where(row => row.IsResourceRoiCandidate)
            .Select(row => MaaRoiEditDraft.FromPreview(row))
            .Where(draft => draft.HasSelection && draft.IsResourceRoiCandidate)
            .Select(draft =>
            {
                var key = $"{draft.Entry}|{draft.Source}|{draft.RoiJson}";
                return previous.TryGetValue(key, out var item)
                    ? new MaaRoiBatchDraftPreview(draft, item.IsIncluded, item.StateLabel, item.StateDetail)
                    : new MaaRoiBatchDraftPreview(draft);
            })
            .ToArray();
        ReplaceCollection(RoiBatchDrafts, drafts);
    }

    private void RefreshSelectedRoiPreviewRows()
    {
        ReplaceCollection(
            SelectedRoiPreviewRows,
            _selectedRoiPreviewRow is null ? [] : [_selectedRoiPreviewRow]);
        SelectedRoiEditDraft = MaaRoiEditDraft.FromPreview(_selectedRoiPreviewRow);
        RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
    }

    private void UpdateSelectedRoiOverlay(int[] roi)
    {
        var source = _selectedRoiPreviewRow;
        if (source is null)
            return;

        ReplaceCollection(
            SelectedRoiPreviewRows,
            [source with
            {
                X = roi[0],
                Y = roi[1],
                Width = roi[2],
                Height = roi[3],
                Raw = SelectedRoiEditDraft.RoiJson,
            }]);
    }

    private void RestoreSelectedRoi(int[] roi, string message)
    {
        SelectedRoiEditDraft = SelectedRoiEditDraft with { RoiJson = RoiJson(roi) };
        UpdateSelectedRoiOverlay(roi);
        UpdateRoiBatchDraftForSelected("未確認", message);
        RoiDraftApplyResult = MaaRoiDraftApplyResult.Failed("未確認");
        StatusMessage = $"{message}: {SelectedRoiEditDraft.RoiJson}";
    }

    private void UpdateRoiBatchDraftForSelected(string stateLabel = "調整済み", string? stateDetail = null)
    {
        if (!SelectedRoiEditDraft.HasSelection || RoiBatchDrafts.Count == 0)
            return;

        var updated = RoiBatchDrafts
            .Select(item => item.Entry.Equals(SelectedRoiEditDraft.Entry, StringComparison.Ordinal)
                && item.Draft.Source.Equals(SelectedRoiEditDraft.Source, StringComparison.Ordinal)
                    ? new MaaRoiBatchDraftPreview(SelectedRoiEditDraft, item.IsIncluded, stateLabel, stateDetail ?? item.StateDetail)
                    : item)
            .ToArray();
        ReplaceCollection(RoiBatchDrafts, updated);
    }

    private void SelectRoiForOcrDetail(MaaOcrDetailRow? row)
    {
        var selected = RhodesMaaRoiSelectionMatcher.MatchForOcrDetail(RoiPreviewRows, row);
        if (selected is not null)
            SelectedRoiPreviewRow = selected;
    }

    private void SelectRoiForTaskResult(MaaTaskRunResult? result)
    {
        var selected = RhodesMaaRoiSelectionMatcher.MatchForTaskResult(RoiPreviewRows, result);
        if (selected is not null)
            SelectedRoiPreviewRow = selected;
    }

    private void SelectRoiForLogRow(RhodesRecognitionScanLogRow? row)
    {
        var selected = RhodesMaaRoiSelectionMatcher.MatchForLogRow(RoiPreviewRows, row);
        if (selected is not null)
            SelectedRoiPreviewRow = selected;
    }

    private static bool TryParseDraftRoi(string value, out int[] roi)
    {
        roi = [];
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 4)
                return false;

            roi = document.RootElement
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number) ? number : (int?)null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .ToArray();
            return roi.Length == 4 && roi[2] > 0 && roi[3] > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private int SnapDelta(double value)
    {
        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return RoiSnapStep <= 1
            ? rounded
            : (int)Math.Round(rounded / (double)RoiSnapStep, MidpointRounding.AwayFromZero) * RoiSnapStep;
    }

    private static int[] AdjustRoi(int[] roi, string? operation, int step)
    {
        var result = roi.ToArray();
        step = Math.Clamp(step, 1, 32);
        switch (operation)
        {
            case "left":
                result[0] = Math.Max(0, result[0] - step);
                break;
            case "right":
                result[0] += step;
                break;
            case "up":
                result[1] = Math.Max(0, result[1] - step);
                break;
            case "down":
                result[1] += step;
                break;
            case "wider":
                result[2] += step;
                break;
            case "narrower":
                result[2] = Math.Max(1, result[2] - step);
                break;
            case "taller":
                result[3] += step;
                break;
            case "shorter":
                result[3] = Math.Max(1, result[3] - step);
                break;
        }

        return result;
    }

    private static string RoiJson(int[] roi)
    {
        return $"[{roi[0]},{roi[1]},{roi[2]},{roi[3]}]";
    }

    private void RefreshResourceTasks()
    {
        ResourceTasks.Clear();
        foreach (var task in _allResourceTasks.Where(task => RhodesMaaResourceCatalog.TaskAppliesToProfile(task, SelectedResourceProfile)))
        {
            ResourceTasks.Add(task);
        }
        OnPropertyChanged(nameof(CurrentResourceExecutionPlan));
        RefreshInspectorRows();
    }

    private void ReloadResourceCatalog()
    {
        var selectedProfileId = SelectedResourceProfile?.Id;
        _allResourceTasks = RhodesMaaResourceCatalog.DefaultTasks();
        MaaResourceContract = RhodesMaaResourceCatalog.ValidateContract();
        ReplaceCollection(
            ResourceProfiles,
            RhodesPublicDebugPolicy.FilterProfiles(
                RhodesMaaResourceCatalog.ProfileGroups(_allResourceTasks),
                _distributionProfile));
        SelectedResourceProfile = ResourceProfiles.FirstOrDefault(profile => string.Equals(profile.Id, selectedProfileId, StringComparison.Ordinal))
            ?? ResourceProfiles.FirstOrDefault(profile => profile.Id == "runStatusFull")
            ?? ResourceProfiles.FirstOrDefault();
        RefreshResourceTasks();
        RefreshInspectorRows();
        OnPropertyChanged(nameof(MaaOcrStatusState));
        OnPropertyChanged(nameof(MaaOcrStatusDetail));
    }

    private void RefreshChoiceLists()
    {
        RefreshOperatorChoices();
        RefreshRelicChoices();
        RefreshCampaignPreviews();
        RefreshInspectorRows();
        OnPropertyChanged(nameof(RunContextSummary));
        OnPropertyChanged(nameof(CampaignHeaderDetail));
    }

    /// <summary>検索は1文字ごとに全再構築せず、入力が止まってから一度だけ反映する。</summary>
    private static void RestartSearchDebounce(ref DispatcherTimer? timer, Action refresh)
    {
        if (timer is null)
        {
            var created = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            created.Tick += (_, _) =>
            {
                created.Stop();
                refresh();
            };
            timer = created;
        }

        timer.Stop();
        timer.Start();
    }

    private void RefreshChoiceAfterSelectionMutation(string kind)
    {
        if (kind == "relic")
        {
            if (RhodesChoiceCatalogRegistry.RequiresFullRefreshAfterSelectionMutation(RelicChoiceFilterState()))
                RefreshRelicChoices();
            else
                RefreshRelicSummaries();
            return;
        }

        if (RhodesChoiceCatalogRegistry.RequiresFullRefreshAfterSelectionMutation(OperatorChoiceFilterState()))
            RefreshOperatorChoices();
        else
            RefreshOperatorSummaries();
    }

    private void RefreshChoiceAfterExclusionMutation(string kind)
    {
        if (kind == "relic")
        {
            if (RhodesChoiceCatalogRegistry.RequiresFullRefreshAfterExclusionMutation(RelicChoiceFilterState()))
                RefreshRelicChoices();
            else
                RefreshRelicSummaries();
            return;
        }

        if (RhodesChoiceCatalogRegistry.RequiresFullRefreshAfterExclusionMutation(OperatorChoiceFilterState()))
            RefreshOperatorChoices();
        else
            RefreshOperatorSummaries();
    }

    private void RefreshChoiceAfterBulkMutation(string kind)
    {
        if (kind == "relic")
        {
            if (RelicShowSelectedFirst || RelicSelectedOnly)
                RefreshRelicChoices();
            else
                RefreshRelicSummaries();
            return;
        }

        if (OperatorShowSelectedFirst || OperatorSelectedOnly)
            RefreshOperatorChoices();
        else
            RefreshOperatorSummaries();
    }

    private void PersistChoiceStateInBackground()
    {
        _ = PersistChoiceStateAsync();
    }

    private async Task<bool> PersistChoiceStateAsync()
    {
        try
        {
            await RhodesRunStateStore.SaveChoicesAsync(_allOperators, _allRelics, BuildChoicePersistenceOptions());
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"状態保存に失敗しました: {ex.Message}";
            TryWriteStateSaveErrorLog(ex);
            return false;
        }
    }

    /// <summary>保存失敗はStatusMessageだけだと原因調査ができないため、スタック付きでデバッグログへ残す。</summary>
    private static void TryWriteStateSaveErrorLog(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(RhodesSukiDebugPaths.DebugLogDirectory);
            var path = Path.Combine(RhodesSukiDebugPaths.DebugLogDirectory, "state-save-errors.log");
            File.AppendAllText(path, $"[{DateTimeOffset.UtcNow:O}]{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // ログ書き込み自体の失敗で保存経路を壊さない。
        }
    }

    private SukiChoicePersistenceOptions BuildChoicePersistenceOptions()
    {
        return new SukiChoicePersistenceOptions(
            OperatorShowSelectedFirst,
            OperatorHideExcluded,
            OperatorSelectedOnly,
            RelicShowSelectedFirst,
            RelicHideExcluded,
            RelicSelectedOnly,
            OperatorPaneColumns,
            RelicPaneColumns);
    }

    private SukiOutputPreferences BuildOutputPreferences()
    {
        return new SukiOutputPreferences(
            OutputSeparateWindow,
            OutputTournamentMode,
            OutputTransparentBackground,
            OutputScrollSpeed,
            OutputParts.Select(part => new SukiOutputPartState(
                part.Id,
                part.Enabled,
                part.ScrollEnabled,
                part.HideExcluded,
                part.Width,
                part.Height)).ToArray(),
            OverlayLayoutItems.Select(item => item.ToState()).ToArray());
    }

    private SukiChoiceCatalogFilterState OperatorChoiceFilterState()
    {
        return new SukiChoiceCatalogFilterState(
            SearchText: OperatorSearch,
            OperatorClass: OperatorClassFilter,
            OperatorBranch: OperatorBranchFilter,
            Rarity: OperatorRarityFilter,
            ShowSelectedFirst: OperatorShowSelectedFirst,
            HideExcluded: OperatorHideExcluded,
            SelectedOnly: OperatorSelectedOnly,
            PaneColumns: OperatorPaneColumns,
            SortMode: OperatorSortMode);
    }

    private SukiChoiceCatalogFilterState RelicChoiceFilterState()
    {
        return new SukiChoiceCatalogFilterState(
            SearchText: RelicSearch,
            Category: RelicCategoryFilter,
            CampaignId: SelectedCampaign?.Id ?? "",
            ShowSelectedFirst: RelicShowSelectedFirst,
            HideExcluded: RelicHideExcluded,
            SelectedOnly: RelicSelectedOnly,
            PaneColumns: RelicPaneColumns,
            SortMode: RelicSortMode);
    }

    private void RefreshOperatorSummaries()
    {
        OnPropertyChanged(nameof(OperatorListSummary));
        OnPropertyChanged(nameof(RunContextSummary));
        OnPropertyChanged(nameof(CampaignHeaderDetail));
        RefreshRunSelectionHighlights();
        RefreshInspectorRows();
    }

    private void RefreshRelicSummaries()
    {
        OnPropertyChanged(nameof(RelicListSummary));
        OnPropertyChanged(nameof(RunContextSummary));
        OnPropertyChanged(nameof(CampaignHeaderDetail));
        RefreshHudRelics();
        RefreshCampaignPreviews();
        RefreshRunSelectionHighlights();
        RefreshInspectorRows();
    }

    private void RefreshOperatorChoices()
    {
        var view = RhodesChoiceCatalogRegistry.BuildView("operator", _allOperators, OperatorChoiceFilterState());
        ReplaceCollection(FilteredOperators, view.FilteredItems);
        ReplaceCollection(FilteredOperatorRows, view.Rows);
        OnPropertyChanged(nameof(OperatorListSummary));
        OnPropertyChanged(nameof(RunContextSummary));
        OnPropertyChanged(nameof(CampaignHeaderDetail));
        RefreshRunSelectionHighlights();
        RefreshInspectorRows();
    }

    private void RefreshRelicChoices()
    {
        var view = RhodesChoiceCatalogRegistry.BuildView("relic", _allRelics, RelicChoiceFilterState());
        ReplaceCollection(FilteredRelics, view.FilteredItems);
        ReplaceCollection(FilteredRelicRows, view.Rows);
        OnPropertyChanged(nameof(RelicListSummary));
        OnPropertyChanged(nameof(RunContextSummary));
        OnPropertyChanged(nameof(CampaignHeaderDetail));
        RefreshHudRelics();
        RefreshCampaignPreviews();
        RefreshRunSelectionHighlights();
        RefreshInspectorRows();
    }

    private void RefreshRunSelectionHighlights()
    {
        var highlights = _allOperators
            .Where(item => item.IsSelected && !item.IsExcluded)
            .Take(4)
            .Concat(_allRelics
                .Where(item => item.CampaignId == CurrentCampaignId && item.IsSelected && !item.IsExcluded)
                .Take(2));
        ReplaceCollection(RunSelectionHighlights, highlights);
        OnPropertyChanged(nameof(RunSelectionSummary));
    }

    private void RefreshOperatorRows()
    {
        ReplaceCollection(FilteredOperatorRows, RhodesChoiceCatalogRegistry.BuildRows(FilteredOperators, OperatorChoiceFilterState()));
    }

    private void RefreshRelicRows()
    {
        ReplaceCollection(FilteredRelicRows, RhodesChoiceCatalogRegistry.BuildRows(FilteredRelics, RelicChoiceFilterState()));
    }

    private void RefreshOperatorFilterOptions()
    {
        var rarityBase = _allOperators.Where(item => !item.HiddenByDefault);
        ReplaceCollection(OperatorRarityOptions, new[] { "すべて" }.Concat(
            rarityBase.Select(item => item.Rarity)
                .Where(item => item > 0)
                .Distinct()
                .OrderByDescending(item => item)
                .Select(item => $"★{item}")));
        EnsureFilterValue(ref _operatorRarityFilter, OperatorRarityOptions, nameof(OperatorRarityFilter));

        var classBase = _allOperators.Where(item => !item.HiddenByDefault && RarityMatches(item));
        ReplaceCollection(OperatorClassOptions, new[] { "すべて" }.Concat(
            RhodesOperatorTaxonomy.SortClasses(classBase.Select(item => item.OperatorClass))));
        EnsureFilterValue(ref _operatorClassFilter, OperatorClassOptions, nameof(OperatorClassFilter));

        var branchBase = classBase.Where(item => OperatorClassFilter == "すべて" || item.OperatorClass == OperatorClassFilter);
        ReplaceCollection(OperatorBranchOptions, new[] { "すべて" }.Concat(
            RhodesOperatorTaxonomy.SortBranches(branchBase.Select(item => (item.OperatorBranch, item.OperatorClass)))));
        EnsureFilterValue(ref _operatorBranchFilter, OperatorBranchOptions, nameof(OperatorBranchFilter));
    }

    private void RefreshRelicFilterOptions()
    {
        ReplaceCollection(RelicCategoryOptions, new[] { "すべて" }.Concat(
            _allRelics.Where(item => item.CampaignId == SelectedCampaign?.Id)
                .Select(item => item.Category)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .Order(StringComparer.Ordinal)));
        EnsureFilterValue(ref _relicCategoryFilter, RelicCategoryOptions, nameof(RelicCategoryFilter));
    }

    private bool RarityMatches(SukiChoiceItem item)
    {
        return OperatorRarityFilter == "すべて" || item.Rarity.ToString() == OperatorRarityFilter.TrimStart('★');
    }

    private void EnsureFilterValue(ref string value, IEnumerable<string> options, string propertyName)
    {
        if (!options.Contains(value))
            value = "すべて";

        // 選択肢リストの再構築中、ComboBoxは一時的に選択をクリアしてnullを押し込む。
        // VM側は同値へ正規化するため変更通知が出ず、コンボの選択表示が空のまま固着する。
        // 値が変わっていなくても必ず再通知して選択表示を復元する。
        OnPropertyChanged(propertyName);
    }

    private static int ClampPaneColumns(int value)
    {
        return Math.Clamp(value, 1, 4);
    }

    private IReadOnlyList<MaaCandidatePreview> SnapshotCandidatesForComparison(
        IReadOnlyList<MaaTaskRunResult> taskResults,
        IEnumerable<MaaCandidatePreview> candidates)
    {
        var currentCandidates = candidates.ToArray();
        if (currentCandidates.Length > 0)
            return currentCandidates;

        return RhodesMaaLocalCandidateConverter.FromTaskResults(
            CandidateApiProfileId(),
            taskResults,
            SelectedCampaign?.Id ?? _runState.CampaignId);
    }

    private IReadOnlyDictionary<string, string> CandidateSourceEntries(
        string? profileId,
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var taskResult in taskResults)
        {
            var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
                profileId,
                [taskResult],
                SelectedCampaign?.Id ?? _runState.CampaignId);
            foreach (var candidate in candidates)
            {
                var key = CandidateComparisonKey(candidate);
                if (!string.IsNullOrWhiteSpace(key) && !entries.ContainsKey(key))
                    entries[key] = taskResult.Entry;
            }
        }

        return entries;
    }

    private void ClearRoiRescanComparison(string summary = "再スキャン比較未実行")
    {
        SelectedRoiRescanComparisonRow = null;
        ReplaceCollection(RoiRescanComparisonRows, Array.Empty<MaaRoiRescanComparisonRow>());
        RoiRescanComparisonSummary = summary;
        SetRoiRescanComparisonEvidence("", "");
        RoiRescanEvidencePreviewTitle = "比較証跡未表示";
        RoiRescanEvidencePreviewText = "";
        _roiRescanEvidencePreviewJson = "";
        SelectedRoiRescanEvidencePreviewNode = null;
        ReplaceCollection(RoiRescanEvidencePreviewNodes, Array.Empty<MaaEvidencePreviewNode>());
    }

    private void SetRoiRescanComparisonEvidence(string beforePath, string afterPath)
    {
        _lastRoiRescanBeforePath = beforePath ?? "";
        _lastRoiRescanAfterPath = afterPath ?? "";
        RoiRescanComparisonEvidenceSummary =
            string.IsNullOrWhiteSpace(_lastRoiRescanBeforePath) && string.IsNullOrWhiteSpace(_lastRoiRescanAfterPath)
                ? "比較証跡未保存"
                : $"before={_lastRoiRescanBeforePath} / after={_lastRoiRescanAfterPath}";
    }

    private static IReadOnlyList<MaaRoiRescanComparisonRow> CompareRoiRescanCandidates(
        IEnumerable<MaaCandidatePreview> beforeCandidates,
        IEnumerable<MaaCandidatePreview> afterCandidates,
        IReadOnlyDictionary<string, string>? beforeSourceEntries = null,
        IReadOnlyDictionary<string, string>? afterSourceEntries = null)
    {
        var before = CandidateComparisonMap(beforeCandidates);
        var after = CandidateComparisonMap(afterCandidates);
        var orderedKeys = before.Values
            .OrderBy(item => item.Order)
            .Select(item => item.Key)
            .Concat(after.Values.OrderBy(item => item.Order).Select(item => item.Key))
            .Distinct(StringComparer.Ordinal);
        var rows = new List<MaaRoiRescanComparisonRow>();
        foreach (var key in orderedKeys)
        {
            var hasBefore = before.TryGetValue(key, out var beforeItem);
            var hasAfter = after.TryGetValue(key, out var afterItem);
            if (!hasBefore && hasAfter && afterItem is not null)
            {
                rows.Add(new MaaRoiRescanComparisonRow(
                    "added",
                    afterItem.Label,
                    "-",
                    afterItem.DisplayValue,
                    afterItem.Detail,
                    key,
                    CandidateSourceEntry(key, beforeSourceEntries, afterSourceEntries)));
                continue;
            }

            if (hasBefore && !hasAfter && beforeItem is not null)
            {
                rows.Add(new MaaRoiRescanComparisonRow(
                    "removed",
                    beforeItem.Label,
                    beforeItem.DisplayValue,
                    "-",
                    beforeItem.Detail,
                    key,
                    CandidateSourceEntry(key, beforeSourceEntries, afterSourceEntries)));
                continue;
            }

            if (beforeItem is null || afterItem is null || beforeItem.Signature == afterItem.Signature)
                continue;

            rows.Add(new MaaRoiRescanComparisonRow(
                "changed",
                afterItem.Label,
                beforeItem.DisplayValue,
                afterItem.DisplayValue,
                $"{beforeItem.Detail} / after: {afterItem.Detail}",
                key,
                CandidateSourceEntry(key, beforeSourceEntries, afterSourceEntries)));
        }

        return rows;
    }

    private static string CandidateSourceEntry(
        string key,
        IReadOnlyDictionary<string, string>? beforeSourceEntries,
        IReadOnlyDictionary<string, string>? afterSourceEntries)
    {
        if (afterSourceEntries is not null && afterSourceEntries.TryGetValue(key, out var afterEntry))
            return afterEntry;
        return beforeSourceEntries is not null && beforeSourceEntries.TryGetValue(key, out var beforeEntry)
            ? beforeEntry
            : "";
    }

    private void SelectCandidateForComparison(MaaRoiRescanComparisonRow? row)
    {
        if (row is null)
            return;

        if (!string.IsNullOrWhiteSpace(row.TaskEntry))
        {
            var task = ResourceTaskResults.FirstOrDefault(result => result.Entry.Equals(row.TaskEntry, StringComparison.Ordinal));
            if (task is not null)
                SelectedResourceTaskResult = task;
        }

        if (string.IsNullOrWhiteSpace(row.CandidateKey))
        {
            StatusMessage = string.IsNullOrWhiteSpace(row.TaskEntry)
                ? $"比較差分を選択しました: {row.Label}"
                : $"比較差分のtaskを選択しました: {row.TaskEntry}";
            return;
        }

        var selected = CandidateResults.FirstOrDefault(candidate =>
            CandidateComparisonKey(candidate).Equals(row.CandidateKey, StringComparison.Ordinal));
        if (selected is null)
        {
            StatusMessage = string.IsNullOrWhiteSpace(row.TaskEntry)
                ? $"比較差分に対応する候補は現在の候補一覧にありません: {row.Label}"
                : $"比較差分のtaskを選択しました: {row.TaskEntry} / 候補は現在の一覧にありません";
            return;
        }

        SelectedCandidateResult = selected;
        StatusMessage = string.IsNullOrWhiteSpace(row.TaskEntry)
            ? $"比較差分の候補を選択しました: {selected.Label}"
            : $"比較差分の候補とtaskを選択しました: {selected.Label} / {row.TaskEntry}";
    }

    private void SelectEvidencePreviewNode(MaaEvidencePreviewNode node)
    {
        var selectedParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(node.CandidateKey))
        {
            var candidate = CandidateResults.FirstOrDefault(item =>
                CandidateComparisonKey(item).Equals(node.CandidateKey, StringComparison.Ordinal));
            if (candidate is not null)
            {
                SelectedCandidateResult = candidate;
                selectedParts.Add($"candidate:{candidate.Label}");
            }
        }

        if (!string.IsNullOrWhiteSpace(node.TaskEntry))
        {
            var task = ResourceTaskResults.FirstOrDefault(item => item.Entry.Equals(node.TaskEntry, StringComparison.Ordinal));
            if (task is not null)
            {
                SelectedResourceTaskResult = task;
                selectedParts.Add($"task:{task.Entry}");
            }

            var log = RecognitionScanLogRows.FirstOrDefault(item => item.Entry.Equals(node.TaskEntry, StringComparison.Ordinal));
            if (log is not null)
            {
                SelectedRecognitionScanLogRow = log;
                selectedParts.Add($"log:{log.DisplayName}");
            }
        }

        if (selectedParts.Count > 0)
            StatusMessage = $"証跡ノードを選択しました: {string.Join(" / ", selectedParts)}";
    }

    private void SelectEvidenceNodeForCandidate(MaaCandidatePreview? candidate)
    {
        if (candidate is null || RoiRescanEvidencePreviewNodes.Count == 0)
            return;

        var key = CandidateComparisonKey(candidate);
        if (string.IsNullOrWhiteSpace(key))
            return;

        var node = FindEvidenceNode(RoiRescanEvidencePreviewNodes, item =>
            item.CandidateKey.Equals(key, StringComparison.Ordinal));
        if (node is not null)
            SelectedRoiRescanEvidencePreviewNode = node;
    }

    private void SelectEvidenceNodeForTaskEntry(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry) || RoiRescanEvidencePreviewNodes.Count == 0)
            return;

        var node = FindEvidenceNode(RoiRescanEvidencePreviewNodes, item =>
            item.TaskEntry.Equals(entry, StringComparison.Ordinal));
        if (node is not null)
            SelectedRoiRescanEvidencePreviewNode = node;
    }

    private static MaaEvidencePreviewNode? FindEvidenceNode(
        IEnumerable<MaaEvidencePreviewNode> nodes,
        Func<MaaEvidencePreviewNode, bool> predicate)
    {
        foreach (var node in nodes)
        {
            if (predicate(node))
                return node;

            var child = FindEvidenceNode(node.SafeChildren, predicate);
            if (child is not null)
                return child;
        }

        return null;
    }

    private static string BuildRoiRescanComparisonSummary(
        int beforeCount,
        int afterCount,
        IReadOnlyList<MaaRoiRescanComparisonRow> rows)
    {
        var added = rows.Count(row => row.State == "added");
        var removed = rows.Count(row => row.State == "removed");
        var changed = rows.Count(row => row.State == "changed");
        return rows.Count == 0
            ? $"再スキャン比較: 差分なし (before {beforeCount}件 / after {afterCount}件)"
            : $"再スキャン比較: 追加{added} / 変化{changed} / 消失{removed} (before {beforeCount}件 / after {afterCount}件)";
    }

    private static IReadOnlyDictionary<string, CandidateComparisonItem> CandidateComparisonMap(
        IEnumerable<MaaCandidatePreview> candidates)
    {
        var map = new Dictionary<string, CandidateComparisonItem>(StringComparer.Ordinal);
        var order = 0;
        foreach (var candidate in candidates)
        {
            var key = CandidateComparisonKey(candidate);
            if (string.IsNullOrWhiteSpace(key))
                key = $"candidate:{order}:{candidate.Kind}:{candidate.Label}:{candidate.Value}";

            var item = new CandidateComparisonItem(
                key,
                order,
                CandidateComparisonLabel(candidate),
                CandidateComparisonSignature(candidate),
                CandidateComparisonDisplayValue(candidate),
                CandidateComparisonDetail(candidate),
                candidate.Confidence);
            if (!map.TryGetValue(key, out var existing) || (item.Confidence ?? 0) > (existing.Confidence ?? 0))
            {
                map[key] = item with { Order = existing?.Order ?? item.Order };
            }
            order++;
        }

        return map;
    }

    private static string CandidateComparisonKey(MaaCandidatePreview candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.Field))
            return $"field:{candidate.Field}:{candidate.CampaignId}:{candidate.FieldId}:{candidate.SlotKind}";
        if (!string.IsNullOrWhiteSpace(candidate.OperatorId))
            return $"operator:{candidate.OperatorId}";
        if (!string.IsNullOrWhiteSpace(candidate.RelicId))
            return $"relic:{candidate.CampaignId}:{candidate.RelicId}";
        if (!string.IsNullOrWhiteSpace(candidate.ThoughtId))
            return $"thought:{candidate.CampaignId}:{candidate.ThoughtId}";
        if (!string.IsNullOrWhiteSpace(candidate.AgeId))
            return $"age:{candidate.CampaignId}:{candidate.AgeId}";
        if (!string.IsNullOrWhiteSpace(candidate.EffectId))
            return $"effect:{candidate.CampaignId}:{candidate.EffectId}:{candidate.SlotKind}";
        if (!string.IsNullOrWhiteSpace(candidate.CoinId))
            return $"coin:{candidate.CampaignId}:{candidate.CoinId}";
        if (!string.IsNullOrWhiteSpace(candidate.StatusId))
            return $"status:{candidate.CampaignId}:{candidate.StatusId}";

        return FirstNonEmpty(candidate.RecognitionKey, candidate.Identity, candidate.CampaignId);
    }

    private static string CandidateComparisonSignature(MaaCandidatePreview candidate)
    {
        return string.Join(
            "|",
            candidate.Kind,
            candidate.Field,
            candidate.OperatorId,
            candidate.RelicId,
            candidate.ThoughtId,
            candidate.AgeId,
            candidate.EffectId,
            candidate.CoinId,
            candidate.StatusId,
            candidate.Value,
            candidate.Count,
            candidate.Face,
            candidate.CampaignId);
    }

    private static string CandidateComparisonLabel(MaaCandidatePreview candidate)
    {
        return FirstNonEmpty(candidate.Label, candidate.Field, candidate.Kind, candidate.Value, candidate.Identity);
    }

    private static string CandidateComparisonDisplayValue(MaaCandidatePreview candidate)
    {
        var label = CandidateComparisonLabel(candidate);
        if (candidate.Count > 0)
            return $"{label} x{candidate.Count}";
        if (!string.IsNullOrWhiteSpace(candidate.Field))
            return $"{label}={FirstNonEmpty(candidate.Value, candidate.RawText, "-")}";

        var id = FirstNonEmpty(
            candidate.OperatorId,
            candidate.RelicId,
            candidate.ThoughtId,
            candidate.AgeId,
            candidate.EffectId,
            candidate.CoinId,
            candidate.StatusId,
            candidate.Value);
        return string.IsNullOrWhiteSpace(id) || id == label
            ? label
            : $"{label} ({id})";
    }

    private static string CandidateComparisonDetail(MaaCandidatePreview candidate)
    {
        var parts = new[]
        {
            candidate.DebugDetail,
            string.IsNullOrWhiteSpace(candidate.RawText) ? "" : $"raw:{candidate.RawText}",
            candidate.Confidence.HasValue ? $"conf:{candidate.Confidence.Value:0.###}" : "",
        };
        return string.Join(" / ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static SukiWorkspaceSectionPreview WorkspaceSection(SukiWorkspaceLayout layout, string id)
    {
        return layout.Sections.First(section => string.Equals(section.Id, id, StringComparison.Ordinal));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        var items = source as IReadOnlyList<T> ?? source.ToArray();
        if (target.Count == items.Count && target.SequenceEqual(items))
            return;

        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private sealed record CandidateComparisonItem(
        string Key,
        int Order,
        string Label,
        string Signature,
        string DisplayValue,
        string Detail,
        double? Confidence);

    private async Task RunProbeCoreAsync(MaaProbePayloadPreview payload)
    {
        if (_session.Tasker is null)
        {
            ProbeResults.Add(new MaaProbeResult(payload.Name, "Invalid", false, "先にADB接続してください。"));
            return;
        }

        var result = await RhodesRecognitionProbe.RunRecognitionAsync(
            _session.Tasker,
            payload.Name,
            payload.Payload,
            _lastCapture);
        ProbeResults.Add(result);
        StatusMessage = $"{payload.Name}: {result.Status}";
    }

    private async Task<bool> EnsureMaaControllerReadyAsync(bool forceReconnect = false)
    {
        if (!forceReconnect && _session.IsControllerReady)
            return true;

        if (string.IsNullOrWhiteSpace(AdbSerial) || string.IsNullOrWhiteSpace(AdbPath))
            await DetectAdbLocallyCoreAsync();

        StatusMessage = "MAA Controller に接続しています。";
        var snapshot = await _session.InitializeAdbAsync(BuildSessionOptions());
        ApplyMaaSessionSnapshot(
            snapshot,
            snapshot.IsReady
                ? "MAA Controller に接続しました。"
                : "MAA Controller に接続できませんでした。設定を確認してください。");
        if (snapshot.IsReady)
        {
            _adbConnectionValidated = true;
            await RhodesSukiSettingsStore.SaveAsync(BuildCurrentSettings());
        }
        return snapshot.IsReady;
    }

    private async Task<bool> EnsureCaptureAsync()
    {
        if (!await EnsureMaaControllerReadyAsync())
            return false;

        if (_lastCapture.Length > 0)
            return true;

        var capture = await CaptureCoreAsync();
        return capture?.Succeeded == true;
    }

    private async Task<bool> ForceCaptureAsync()
    {
        if (!await EnsureMaaControllerReadyAsync())
            return false;

        var capture = await CaptureCoreAsync();
        return capture?.Succeeded == true;
    }

    private async Task<MaaCaptureResult?> CaptureCoreAsync()
    {
        var capture = await _session.CaptureEncodedAsync();
        CaptureState = capture.Succeeded ? $"取得済み: {capture.Detail}" : $"失敗: {capture.Detail}";
        _adbDiagnosticsCaptureSucceeded = capture.Succeeded;
        _adbDiagnosticsCaptureDetail = capture.Detail;
        if (!capture.Succeeded)
        {
            StatusMessage = capture.Detail;
            RefreshAdbDiagnosticSteps();
            return capture;
        }

        _lastCapture = capture.EncodedImage;
        LastCaptureImage = CreateCaptureBitmap(capture.EncodedImage);
        var frameRecord = await SaveCaptureFrameAsync(capture.EncodedImage);
        LastCapturePath = frameRecord.Succeeded ? frameRecord.ImagePath : await SaveLegacyCaptureAsync(capture.EncodedImage);
        _lastFrameId = frameRecord.FrameId;
        _lastFrameMetadataPath = frameRecord.MetadataPath;
        _lastFrameStateSnapshotPath = frameRecord.StateSnapshotPath;
        CaptureState = frameRecord.Succeeded
            ? $"{capture.Detail} / frame={frameRecord.FrameId} / {LastCapturePath}"
            : $"{capture.Detail} / frame保存失敗: {frameRecord.Error} / {LastCapturePath}";
        RefreshFrameRecordHistory();
        RefreshInspectorRows();
        return capture;
    }

    private static Bitmap CreateCaptureBitmap(byte[] encodedImage)
    {
        using var stream = new MemoryStream(encodedImage);
        return new Bitmap(stream);
    }

    private void TryLoadCapturePreviewFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            var bytes = File.ReadAllBytes(path);
            _lastCapture = bytes;
            LastCaptureImage = CreateCaptureBitmap(bytes);
            LastCapturePath = path;
            _lastFrameId = "";
            _lastFrameMetadataPath = "";
            _lastFrameStateSnapshotPath = "";
            CaptureState = $"履歴から読込: {path}";
        }
        catch
        {
            // 履歴の画像が消えていても、候補・ログの復元は継続する。
        }
    }

    private MaaSessionOptions BuildSessionOptions()
    {
        return RhodesMaaSession.DefaultAdbOptions(
            string.IsNullOrWhiteSpace(AdbPath) ? "adb" : AdbPath.Trim(),
            AdbSerial.Trim(),
            AdbConfigJson.Trim(),
            SelectedAdbInputMethod?.Value ?? AdbInputMethods.Default,
            SelectedAdbScreencapMethod?.Value ?? AdbScreencapMethods.Default,
            SelectedAdbPreset?.Id ?? "auto");
    }

    private async Task<RhodesFrameRecordResult> SaveCaptureFrameAsync(byte[] encodedImage)
    {
        var profile = SelectedResourceProfile;
        return await RhodesFrameRecordStore.SaveAsync(new RhodesFrameRecordRequest
        {
            EncodedImage = encodedImage,
            StatePath = RhodesRunStateStore.ResolveDefaultStatePath(),
            ProfileId = profile?.Id,
            ProfileLabel = profile?.Label ?? profile?.DisplayName,
            Source = "maa-native",
            AppVersion = typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "",
            RuntimeSummary = string.Join(
                " / ",
                new[]
                {
                    SelectedAdbPreset?.DisplayName ?? SelectedAdbPreset?.Label ?? "手動",
                    AdbSerial,
                    SelectedAdbScreencapMethod?.Label ?? "",
                    SelectedAdbInputMethod?.Label ?? "",
                }.Where(value => !string.IsNullOrWhiteSpace(value))),
        });
    }

    private static async Task<string> SaveLegacyCaptureAsync(byte[] encodedImage)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "RHODES OBS COMMANDER3373 Debug Logs", "maa-captures");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"suki-maa-capture-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.png");
        await File.WriteAllBytesAsync(path, encodedImage);
        return path;
    }

    private async Task<string> SaveResourceTaskResultsAsync(
        IEnumerable<MaaTaskRunResult> taskResults,
        string? profileId,
        IEnumerable<MaaCandidatePreview>? candidates = null,
        string? profileLabel = null,
        IEnumerable<string>? presetTaskEntries = null,
        MaaResourceExecutionPlan? executionPlan = null,
        SukiCandidateApplySummary? stateApplySummary = null,
        bool stateApplyLocalFallbackUsed = false,
        string? stateApplyApiError = null)
    {
        var selectedMatches = string.Equals(SelectedResourceProfile?.Id, profileId, StringComparison.Ordinal);
        return await RhodesMaaRecognitionEvidenceLog.SaveAsync(
            taskResults,
            candidates ?? [],
            profileId,
            RhodesSukiDebugPaths.RecognitionScansDirectory,
            capturePath: LastCapturePath,
            captureBytes: _lastCapture.Length,
            profileLabel: profileLabel ?? (selectedMatches ? SelectedResourceProfile?.Label : null),
            presetTaskEntries: presetTaskEntries ?? (selectedMatches ? SelectedResourceProfile?.TaskEntries : null),
            runtime: BuildRecognitionRuntimeEvidence(),
            executionPlan: executionPlan,
            contract: MaaResourceContract,
            frameId: _lastFrameId,
            frameMetadataPath: _lastFrameMetadataPath,
            stateSnapshotPath: _lastFrameStateSnapshotPath,
            stateApplySummary: stateApplySummary,
            stateApplyLocalFallbackUsed: stateApplyLocalFallbackUsed,
            stateApplyApiError: stateApplyApiError);
    }

    private MaaRecognitionRuntimeEvidence BuildRecognitionRuntimeEvidence()
    {
        var options = _session.EffectiveOptions ?? BuildSessionOptions();
        var preset = SelectedAdbPreset;
        var input = SelectedAdbInputMethod ?? SukiAdbMethodCatalog.FindInput(SukiAdbMethodCatalog.DefaultInputMethodId);
        var screencap = SelectedAdbScreencapMethod ?? SukiAdbMethodCatalog.FindScreencap(SukiAdbMethodCatalog.DefaultScreencapMethodId);
        var ocr = SelectedOcrEngine ?? SukiOcrEngineCatalog.Options.First(option => option.Id == SukiOcrEngineCatalog.DefaultId);
        return new MaaRecognitionRuntimeEvidence(
            preset?.Id ?? "manual",
            preset?.Label ?? "手動",
            options.AdbPath,
            options.AdbSerial,
            options.AdbConfigJson,
            input.Id,
            input.Label,
            input.Detail,
            options.InputMethod.ToString(),
            screencap.Id,
            screencap.Label,
            screencap.Detail,
            options.ScreencapMethod.ToString(),
            ocr.Id,
            ocr.Label,
            ocr.Detail,
            SessionState,
            SessionDetail,
            options.ResourceRoot,
            options.AgentBinaryRoot,
            BaseResolution.Width,
            BaseResolution.Height,
            BaseResolution.AspectRatioLabel,
            _session.IsControllerReady);
    }

    private static string ResolveMaaTasksSourcePath()
    {
        foreach (var origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(origin, RhodesMaaRoiDraftSourceUpdater.MaaTasksSourcePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(AppContext.BaseDirectory, RhodesMaaRoiDraftSourceUpdater.MaaTasksSourcePath);
    }

    private static string ResolveScanProfilesSourcePath()
    {
        foreach (var origin in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(origin, RhodesMaaGeneratedResourceBuilder.ScanProfilesSourcePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return Path.Combine(AppContext.BaseDirectory, RhodesMaaGeneratedResourceBuilder.ScanProfilesSourcePath);
    }

    private static string ResolveRoiDraftSourcePath(MaaRoiEditDraft draft)
    {
        return RhodesMaaRoiDraftSourceUpdater.UsesScanProfilesSource(draft)
            ? ResolveScanProfilesSourcePath()
            : ResolveMaaTasksSourcePath();
    }

    private IReadOnlyList<MaaRoiEditDraft> VisibleResourceRoiDrafts()
    {
        return RoiBatchDrafts
            .Where(item => item.IsIncluded)
            .Select(item => item.Draft)
            .ToArray();
    }

    private static string ResolveGeneratedPipelinePath()
    {
        return Path.Combine(AppContext.BaseDirectory, RhodesMaaGeneratedResourceBuilder.GeneratedPipelinePath);
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        try
        {
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void SetCaptureImage(Bitmap? value)
    {
        if (ReferenceEquals(_lastCaptureImage, value))
            return;

        var previous = _lastCaptureImage;
        _lastCaptureImage = value;
        _capturePixelWidth = value?.PixelSize.Width ?? 0;
        _capturePixelHeight = value?.PixelSize.Height ?? 0;
        OnPropertyChanged(nameof(LastCaptureImage));
        OnPropertyChanged(nameof(IsLastCaptureImageEmpty));
        OnPropertyChanged(nameof(CapturePixelSizeLabel));
        RefreshRoiPreviewRows();
        RefreshAdbDiagnosticSteps();
        previous?.Dispose();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
