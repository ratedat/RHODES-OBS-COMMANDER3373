using MaaFramework.Binding;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RhodesSuki.Models;

public sealed record MaaBaseResolution(int Width, int Height)
{
    public string AspectRatioLabel => $"{Width}x{Height} (16:9)";
}

public sealed record MaaSessionOptions(
    string ResourceRoot,
    string AgentBinaryRoot,
    string AdbPath,
    string AdbSerial,
    string AdbConfigJson,
    AdbInputMethods InputMethod,
    AdbScreencapMethods ScreencapMethod,
    string ConnectionPreset = "auto");

public static class SukiAdbConfigJson
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "{}";

        try
        {
            var node = JsonNode.Parse(value);
            if (node is not JsonObject)
                throw new InvalidOperationException("ADB ConfigはJSON objectで入力してください。");

            return node.ToJsonString();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"ADB Config JSONが不正です: {ex.Message}", ex);
        }
    }
}

public sealed record MaaAdbPresetPreview(
    string Id,
    string Label,
    string Description,
    string AdbPath,
    string Serial)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Serial) ? Label : $"{Label} ({Serial})";

    public string PathSummary => string.IsNullOrWhiteSpace(AdbPath) ? "adb" : AdbPath;
}

public sealed record MaaAdbDevicePreview(
    string Serial,
    string State,
    string Detail)
{
    public bool IsUsable => State.Equals("device", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(Detail)
        ? $"{State} {Serial}"
        : $"{State} {Serial} {Detail}";
}

public sealed record MaaAdbPathCandidatePreview(
    string Path,
    string Source,
    string Preset,
    bool Exists,
    bool Available,
    string Error)
{
    public bool IsSelectable => !string.IsNullOrWhiteSpace(Path) && (Available || Exists);

    public string StateLabel => Available
        ? "使用可"
        : Exists
            ? "存在"
            : "未検出";

    public string PathSummary => string.IsNullOrWhiteSpace(Path) ? "adb" : Path;

    public string SourceLabel => string.IsNullOrWhiteSpace(Source) ? "manual" : Source;

    public string DisplayName => string.IsNullOrWhiteSpace(Preset)
        ? $"{SourceLabel} / {PathSummary}"
        : $"{Preset} / {PathSummary}";

    public string Detail => string.Join(
        " / ",
        new[]
        {
            string.IsNullOrWhiteSpace(Source) ? "" : $"source:{Source}",
            string.IsNullOrWhiteSpace(Preset) ? "" : $"preset:{Preset}",
            string.IsNullOrWhiteSpace(Error) ? "" : Error,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record RhodesAdbApiSettings(
    bool AutoDetect,
    string ConnectionPreset,
    string AdbPath,
    string Serial);

public sealed record SukiOcrEngineOption(
    string Id,
    string Label,
    string Detail)
{
    public string DisplayName => Label;
}

public static class SukiOcrEngineCatalog
{
    public const string DefaultId = "maa-ocr";

    private static readonly SukiOcrEngineOption[] BuiltInOptions =
    [
        new("maa-ocr", "MAA-OCR", "MAAFramework系OCRを使います。"),
        new("glm-ocr", "GLM-OCR 任意検証", "任意導入GLM-OCRを使います。"),
    ];

    private static readonly HashSet<string> ValidIds = BuiltInOptions
        .Select(option => option.Id)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<SukiOcrEngineOption> Options => BuiltInOptions;

    public static string Normalize(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? DefaultId : value.Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "auto" or "profile" or "maa" or "maa-onnx" or "onnx" => "maa-ocr",
            "glm" or "windows-glm" or "glm-windows" or "glm-hybrid" or "hybrid-glm" => "glm-ocr",
            "hybrid" or "maa-hybrid" or "onnx-hybrid" or "paddle" or "windows" or "windows-paddle" or "paddle-windows" => "maa-ocr",
            _ => normalized
        };
        return ValidIds.Contains(normalized) ? normalized : DefaultId;
    }
}

public sealed record SukiAdbInputMethodOption(
    string Id,
    string Label,
    string Detail,
    AdbInputMethods Value)
{
    public string DisplayName => Label;
}

public sealed record SukiAdbScreencapMethodOption(
    string Id,
    string Label,
    string Detail,
    AdbScreencapMethods Value)
{
    public string DisplayName => Label;
}

public static class SukiAdbMethodCatalog
{
    public const string DefaultInputMethodId = "default";
    public const string DefaultScreencapMethodId = "default";
    public const string FastEmulatorMethodId = "emulator-fast";

    private static readonly SukiAdbInputMethodOption[] BuiltInInputOptions =
    [
        new(
            DefaultInputMethodId,
            "標準入力",
            "MAA既定。互換性優先でEmulatorExtrasは使いません。",
            AdbInputMethods.Default),
        new(
            FastEmulatorMethodId,
            "MuMu高速入力",
            "MuMu 12向け。EmulatorExtrasを優先し、失敗時はMaaTouch/MiniTouch/ADB shellへ戻します。",
            AdbInputMethods.EmulatorExtras
                | AdbInputMethods.Maatouch
                | AdbInputMethods.MinitouchAndAdbKey
                | AdbInputMethods.AdbShell),
        new(
            "adb-shell",
            "互換ADB入力",
            "低速ですが最も通りやすいADB shell入力だけを使います。",
            AdbInputMethods.AdbShell),
    ];

    private static readonly SukiAdbScreencapMethodOption[] BuiltInScreencapOptions =
    [
        new(
            DefaultScreencapMethodId,
            "標準撮影",
            "MAA既定。ロスレスで高互換の方式から自動選択します。",
            AdbScreencapMethods.Default),
        new(
            FastEmulatorMethodId,
            "MuMu/LD高速撮影",
            "EmulatorExtrasを最優先し、失敗時はRawWithGzip/Encode系へ戻します。ロスレスを維持します。",
            AdbScreencapMethods.EmulatorExtras
                | AdbScreencapMethods.RawWithGzip
                | AdbScreencapMethods.Encode
                | AdbScreencapMethods.EncodeToFileAndPull),
        new(
            "raw-gzip",
            "Raw/Gzip優先",
            "RawWithGzipを優先します。EmulatorExtrasが不安定な環境向けのロスレス設定です。",
            AdbScreencapMethods.RawWithGzip
                | AdbScreencapMethods.Encode
                | AdbScreencapMethods.EncodeToFileAndPull),
        new(
            "compat",
            "互換撮影",
            "Encode系だけを使います。低速ですが互換性を優先します。",
            AdbScreencapMethods.Encode
                | AdbScreencapMethods.EncodeToFileAndPull),
    ];

    private static readonly HashSet<string> ValidInputIds = BuiltInInputOptions
        .Select(option => option.Id)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> ValidScreencapIds = BuiltInScreencapOptions
        .Select(option => option.Id)
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<SukiAdbInputMethodOption> InputOptions => BuiltInInputOptions;

    public static IReadOnlyList<SukiAdbScreencapMethodOption> ScreencapOptions => BuiltInScreencapOptions;

    public static SukiAdbInputMethodOption FindInput(string? id)
    {
        var normalized = NormalizeInput(id);
        return BuiltInInputOptions.First(option => option.Id == normalized);
    }

    public static SukiAdbScreencapMethodOption FindScreencap(string? id)
    {
        var normalized = NormalizeScreencap(id);
        return BuiltInScreencapOptions.First(option => option.Id == normalized);
    }

    public static string NormalizeInput(string? id)
    {
        var normalized = NormalizeId(id, DefaultInputMethodId);
        normalized = normalized switch
        {
            "mumu" or "mumu-fast" or "ldplayer" or "ld-fast" or "emulator" or "emulator-extras" => FastEmulatorMethodId,
            "shell" or "adb" => "adb-shell",
            _ => normalized,
        };
        return ValidInputIds.Contains(normalized) ? normalized : DefaultInputMethodId;
    }

    public static string NormalizeScreencap(string? id)
    {
        var normalized = NormalizeId(id, DefaultScreencapMethodId);
        normalized = normalized switch
        {
            "mumu" or "mumu-fast" or "ldplayer" or "ld-fast" or "emulator" or "emulator-extras" => FastEmulatorMethodId,
            "raw" or "gzip" or "raw-with-gzip" => "raw-gzip",
            "encode" or "safe" => "compat",
            _ => normalized,
        };
        return ValidScreencapIds.Contains(normalized) ? normalized : DefaultScreencapMethodId;
    }

    public static string DefaultInputMethodIdForPreset(string? presetId)
    {
        var normalized = NormalizeId(presetId, "auto");
        return normalized is "mumu" ? FastEmulatorMethodId : DefaultInputMethodId;
    }

    public static string DefaultScreencapMethodIdForPreset(string? presetId)
    {
        var normalized = NormalizeId(presetId, "auto");
        return normalized is "mumu" or "ldplayer" ? FastEmulatorMethodId : DefaultScreencapMethodId;
    }

    private static string NormalizeId(string? id, string fallback)
    {
        return string.IsNullOrWhiteSpace(id)
            ? fallback
            : id.Trim().ToLowerInvariant();
    }
}

public sealed record SukiOutputPreferences(
    bool SeparateWindow,
    bool TournamentMode,
    bool TransparentBackground,
    int ScrollSpeed,
    IReadOnlyList<SukiOutputPartState> Parts,
    IReadOnlyList<SukiOverlayLayoutState>? OverlayLayout = null,
    int BackgroundTransparency = 100,
    bool ShowPartTitles = true);

public sealed record SukiOutputPartState(
    string Id,
    bool Enabled,
    bool ScrollEnabled,
    bool HideExcluded,
    int Width,
    int Height);

public sealed record SukiOverlayLayoutState(
    string Id,
    bool Enabled,
    int X,
    int Y,
    int Width,
    int Height,
    int ZIndex);

public sealed record RhodesSukiSettings(
    string AdbPath = "adb",
    string AdbSerial = "",
    string AdbConfigJson = "{}",
    string RhodesApiUrl = "http://127.0.0.1:5173",
    string SelectedAdbPresetId = "auto",
    string SelectedResourceProfileId = "runStatusFull",
    string AdbInputMethodId = SukiAdbMethodCatalog.DefaultInputMethodId,
    string AdbScreencapMethodId = SukiAdbMethodCatalog.DefaultScreencapMethodId,
    bool HudVisible = false,
    int HudX = -1,
    int HudY = -1,
    string HudVisibleParts = "",
    bool AdbConnectionValidated = false,
    IReadOnlyList<SukiOverlayLayoutState>? OverlayLayout = null);

public sealed record MaaSessionSnapshot(
    string State,
    string Detail,
    string ResourceRoot,
    string AgentBinaryRoot,
    bool ResourceRootExists,
    bool AgentBinaryRootExists,
    bool IsReady,
    MaaSessionOptions? EffectiveOptions = null);

public sealed record MaaRecognitionRuntimeEvidence(
    string AdbPresetId,
    string AdbPresetLabel,
    string AdbPath,
    string AdbSerial,
    string AdbConfigJson,
    string AdbInputMethodId,
    string AdbInputMethodLabel,
    string AdbInputMethodDetail,
    string AdbInputMethodValue,
    string AdbScreencapMethodId,
    string AdbScreencapMethodLabel,
    string AdbScreencapMethodDetail,
    string AdbScreencapMethodValue,
    string OcrEngineId,
    string OcrEngineLabel,
    string OcrEngineDetail,
    string SessionState,
    string SessionDetail,
    string ResourceRoot,
    string AgentBinaryRoot,
    int BaseWidth,
    int BaseHeight,
    string BaseResolution,
    bool IsControllerReady);

public sealed record MaaFrameworkRuntimeProbeFacts(
    string BindingAssemblyName,
    string BindingAssemblyVersion,
    string RuntimeIdentifier,
    string NativeRuntimeDirectory,
    IReadOnlyList<string> MissingNativeFiles,
    bool VisualCppRuntimeCheckRequired,
    IReadOnlyList<string> MissingVisualCppRuntimeFiles);

public sealed record MaaRoi(int X, int Y, int Width, int Height)
{
    public int[] ToArray() => [X, Y, Width, Height];
}

public sealed record MaaProbePayloadPreview(
    string Name,
    string Purpose,
    string Payload);

public sealed record MaaProbeResult(
    string Name,
    string Status,
    bool Succeeded,
    string Detail,
    string RecognitionDetailJson = "",
    string Algorithm = "",
    bool Hit = false);

public sealed record MaaCaptureResult(
    string Status,
    bool Succeeded,
    string Detail,
    byte[] EncodedImage);

public sealed record MaaResourceTaskPreview(
    string Entry,
    string Label,
    string Purpose,
    IReadOnlyList<string>? ProfileIds = null,
    string Source = "")
{
    public string ProfileSummary => ProfileIds is { Count: > 0 } ? $"profiles: {string.Join(", ", ProfileIds)}" : "profiles: manual";

    public string SourceSummary => string.IsNullOrWhiteSpace(Source) ? "source: manual" : $"source: {Source}";
}

public sealed record MaaResourceProfilePreview(
    string Id,
    string Label,
    int TaskCount,
    string Description = "",
    string Source = "",
    IReadOnlyList<string>? TaskEntries = null)
{
    public string DisplayName => $"{Label} ({TaskCount})";

    public string ProfileSummary => Id == "all" ? "profiles: all" : $"profile: {Id}";

    public string SourceSummary => string.IsNullOrWhiteSpace(Source) ? "source: local profile ids" : $"source: {Source}";
}

public sealed record MaaResourceExecutionPlan(
    string ProfileId,
    string ProfileLabel,
    string Source,
    IReadOnlyList<string> TaskEntries,
    IReadOnlyList<MaaResourceTaskPreview> Tasks,
    string Error,
    string State = "ready")
{
    public const string ReadyState = "ready";
    public const string UnselectedState = "unselected";
    public const string DisplayOnlyState = "display-only";
    public const string MissingTaskState = "missing-task";
    public const string EmptyState = "empty";
    public const string ErrorState = "error";

    public bool CanRun => string.Equals(State, ReadyState, StringComparison.Ordinal)
        && string.IsNullOrWhiteSpace(Error)
        && Tasks.Count > 0;

    public bool IsDisplayOnly => string.Equals(State, DisplayOnlyState, StringComparison.Ordinal);

    public string StateLabel => State switch
    {
        ReadyState => "実行可能",
        UnselectedState => "未選択",
        DisplayOnlyState => "一覧用",
        MissingTaskState => "task欠落",
        EmptyState => "空",
        _ => "エラー",
    };

    public string Summary => CanRun
        ? $"{StateLabel}: {ProfileLabel} / tasks={Tasks.Count} / {Source}"
        : string.IsNullOrWhiteSpace(Error) ? "実行対象がありません。" : Error;
}

public sealed record MaaResourceContractSnapshot(
    bool IsValid,
    int TaskCount,
    int GroupCount,
    int PresetCount,
    IReadOnlyList<string> Errors)
{
    public string State => IsValid ? "OK" : "NG";

    public string Summary => IsValid
        ? $"OK task={TaskCount} group={GroupCount} preset={PresetCount}"
        : $"NG errors={Errors.Count} task={TaskCount} group={GroupCount} preset={PresetCount}";

    public string Detail => Errors.Count == 0
        ? "interface.json / resource/base/pipeline"
        : string.Join(" / ", Errors.Take(3));
}

public sealed record MaaTaskRunResult(
    string Entry,
    string Status,
    bool Succeeded,
    string Detail,
    string RecognitionDetailJson = "",
    string Algorithm = "",
    bool Hit = false);

public sealed record MaaOcrDetailRow(
    string Entry,
    string Text,
    double? Score,
    string Source,
    string Algorithm)
{
    public string ScoreLabel => Score.HasValue ? Score.Value.ToString("0.###") : "-";

    public string Detail => $"{Source} / {Algorithm}";
}

public sealed record MaaRoiDetailRow(
    string Entry,
    string Source,
    int X,
    int Y,
    int Width,
    int Height,
    string Raw)
{
    public string BoundsLabel => $"{X},{Y} {Width}x{Height}";

    public string Kind => RoiKind(Source);

    public bool IsResourceRoiCandidate => Kind.Equals("roi", StringComparison.OrdinalIgnoreCase);

    public string EditKindLabel => IsResourceRoiCandidate
        ? "Resource ROI候補"
        : Kind.Equals("box", StringComparison.OrdinalIgnoreCase)
            ? "OCR文字枠"
            : "診断枠";

    public string RoiJson => $"[{X},{Y},{Width},{Height}]";

    private static string RoiKind(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "";

        var dot = source.LastIndexOf('.');
        return dot >= 0 && dot + 1 < source.Length ? source[(dot + 1)..] : source;
    }
}

public sealed record MaaRoiPreviewRow(
    string Entry,
    string Source,
    double X,
    double Y,
    double Width,
    double Height,
    string Raw,
    string ScaleLabel)
{
    public string BoundsLabel => $"{X:0.#},{Y:0.#} {Width:0.#}x{Height:0.#}";

    public double TopLeftHandleX => Math.Max(0, X - 12);

    public double TopLeftHandleY => Math.Max(0, Y - 12);

    public double LeftHandleX => Math.Max(0, X - 10);

    public double LeftHandleY => Math.Max(0, Y + (Height / 2) - 10);

    public double TopHandleX => Math.Max(0, X + (Width / 2) - 10);

    public double TopHandleY => Math.Max(0, Y - 10);

    public double RightHandleX => Math.Max(0, X + Width - 10);

    public double RightHandleY => Math.Max(0, Y + (Height / 2) - 10);

    public double BottomHandleX => Math.Max(0, X + (Width / 2) - 10);

    public double BottomHandleY => Math.Max(0, Y + Height - 10);

    public double ResizeHandleX => Math.Max(0, X + Width - 12);

    public double ResizeHandleY => Math.Max(0, Y + Height - 12);

    public string Kind => RoiKind(Source);

    public bool IsResourceRoiCandidate => Kind.Equals("roi", StringComparison.OrdinalIgnoreCase);

    public string EditKindLabel => IsResourceRoiCandidate
        ? "Resource ROI候補"
        : Kind.Equals("box", StringComparison.OrdinalIgnoreCase)
            ? "OCR文字枠"
            : "診断枠";

    public string DisplayTitle => $"{Entry} / {Source}";

    public string ProjectedRoiJson => $"[{RoundCoordinate(X)},{RoundCoordinate(Y)},{RoundCoordinate(Width)},{RoundCoordinate(Height)}]";

    public string Key => $"{Entry}|{Source}|{Raw}|{X:0.###},{Y:0.###},{Width:0.###},{Height:0.###}";

    private static int RoundCoordinate(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static string RoiKind(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "";

        var dot = source.LastIndexOf('.');
        return dot >= 0 && dot + 1 < source.Length ? source[(dot + 1)..] : source;
    }
}

public sealed record MaaRoiEditDraft(
    string Entry,
    string Source,
    string RoiJson,
    bool IsResourceRoiCandidate)
{
    public static MaaRoiEditDraft Empty { get; } = new("", "", "-", false);

    public bool HasSelection => !string.IsNullOrWhiteSpace(Entry);

    public string StatusLabel => HasSelection
        ? IsResourceRoiCandidate ? "編集候補" : "診断用"
        : "未選択";

    public string Detail => HasSelection
        ? $"{Entry} / {Source}"
        : "ROI行を選択してください";

    public static MaaRoiEditDraft FromPreview(MaaRoiPreviewRow? row)
    {
        return row is null
            ? Empty
            : new MaaRoiEditDraft(row.Entry, row.Source, row.ProjectedRoiJson, row.IsResourceRoiCandidate);
    }
}

public sealed class MaaRoiBatchDraftPreview
{
    public MaaRoiBatchDraftPreview(
        MaaRoiEditDraft draft,
        bool isIncluded = true,
        string stateLabel = "未確認",
        string stateDetail = "")
    {
        Draft = draft;
        IsIncluded = isIncluded;
        StateLabel = string.IsNullOrWhiteSpace(stateLabel) ? "未確認" : stateLabel;
        StateDetail = stateDetail;
    }

    public MaaRoiEditDraft Draft { get; }

    public bool IsIncluded { get; set; }

    public string StateLabel { get; }

    public string StateDetail { get; }

    public string Entry => Draft.Entry;

    public string Detail => Draft.Detail;

    public string RoiJson => Draft.RoiJson;

    public string Key => $"{Draft.Entry}|{Draft.Source}|{Draft.RoiJson}";
}

public sealed record MaaRoiAdjustmentSessionDraft(
    string Entry,
    string Source,
    string RoiJson,
    bool IsResourceRoiCandidate,
    bool IsIncluded,
    string StateLabel,
    string StateDetail)
{
    public static MaaRoiAdjustmentSessionDraft FromPreview(MaaRoiBatchDraftPreview preview)
    {
        return new MaaRoiAdjustmentSessionDraft(
            preview.Draft.Entry,
            preview.Draft.Source,
            preview.Draft.RoiJson,
            preview.Draft.IsResourceRoiCandidate,
            preview.IsIncluded,
            preview.StateLabel,
            preview.StateDetail);
    }

    public MaaRoiEditDraft ToEditDraft()
    {
        return new MaaRoiEditDraft(Entry, Source, RoiJson, IsResourceRoiCandidate);
    }

    public MaaRoiBatchDraftPreview ToPreview()
    {
        return new MaaRoiBatchDraftPreview(ToEditDraft(), IsIncluded, StateLabel, StateDetail);
    }
}

public sealed record MaaRoiAdjustmentSessionPayload(
    int SchemaVersion,
    string Kind,
    string? ProfileId,
    string ScanLogPath,
    string CapturePath,
    string CreatedAt,
    IReadOnlyList<MaaRoiAdjustmentSessionDraft> Drafts,
    MaaRoiBatchApplyResult? BatchResult,
    string? ComparisonSummary,
    IReadOnlyList<MaaRoiRescanComparisonRow>? ComparisonRows,
    string? ComparisonBeforeLogPath,
    string? ComparisonAfterLogPath)
{
    public IReadOnlyList<MaaRoiRescanComparisonRow> SafeComparisonRows => ComparisonRows ?? [];

    public int DraftCount => Drafts.Count;

    public int IncludedCount => Drafts.Count(draft => draft.IsIncluded);

    public int ComparisonCount => SafeComparisonRows.Count;

    public string SafeComparisonSummary => string.IsNullOrWhiteSpace(ComparisonSummary) ? "再スキャン比較未実行" : ComparisonSummary;

    public string SafeComparisonBeforeLogPath => ComparisonBeforeLogPath ?? "";

    public string SafeComparisonAfterLogPath => ComparisonAfterLogPath ?? "";
}

public sealed record MaaRoiAdjustmentSessionItem(
    string ProfileId,
    string CreatedAt,
    int DraftCount,
    int IncludedCount,
    int ComparisonCount,
    string ScanLogPath,
    string SessionPath,
    DateTimeOffset SortTimestamp)
{
    public string Title => string.IsNullOrWhiteSpace(ProfileId) ? "ROI調整セッション" : $"ROI調整: {ProfileId}";

    public string Detail => ComparisonCount > 0
        ? $"{DraftCount}候補 / 対象{IncludedCount}件 / 比較{ComparisonCount}件 / {CreatedAt}"
        : $"{DraftCount}候補 / 対象{IncludedCount}件 / {CreatedAt}";
}

public sealed record MaaRoiDraftApplyResult(
    bool Succeeded,
    string Message,
    string SourcePath,
    string TargetId,
    string PreviousRoi,
    string UpdatedRoi)
{
    public string BackupPath { get; init; } = "";

    public bool HasDiff => Succeeded
        && !string.IsNullOrWhiteSpace(TargetId)
        && !string.IsNullOrWhiteSpace(UpdatedRoi);

    public string TargetSummary => HasDiff
        ? $"対象: {TargetId} / {SourcePath}"
        : "対象: -";

    public string DiffSummary => HasDiff
        ? $"差分: {PreviousRoi} -> {UpdatedRoi}"
        : "差分: -";

    public string BackupSummary => string.IsNullOrWhiteSpace(BackupPath)
        ? "backup: -"
        : $"backup: {BackupPath}";

    public static MaaRoiDraftApplyResult Failed(string message)
    {
        return new MaaRoiDraftApplyResult(false, message, "", "", "", "");
    }
}

public sealed record MaaRoiBatchApplyResult(
    bool Succeeded,
    string Message,
    int AppliedCount,
    IReadOnlyList<MaaRoiDraftApplyResult> Results)
{
    public string MaaTasksBackupPath { get; init; } = "";

    public string ScanProfilesBackupPath { get; init; } = "";

    public string Summary => Succeeded
        ? $"ROI一括適用: {AppliedCount}件"
        : $"ROI一括適用失敗: {Message}";

    public string BackupSummary
    {
        get
        {
            var backups = new[]
            {
                string.IsNullOrWhiteSpace(MaaTasksBackupPath) ? "" : $"maa={MaaTasksBackupPath}",
                string.IsNullOrWhiteSpace(ScanProfilesBackupPath) ? "" : $"scan={ScanProfilesBackupPath}",
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
            return backups.Length == 0 ? "backup: -" : $"backup: {string.Join(" / ", backups)}";
        }
    }

    public static MaaRoiBatchApplyResult Failed(string message, IReadOnlyList<MaaRoiDraftApplyResult>? results = null)
    {
        return new MaaRoiBatchApplyResult(false, message, results?.Count(item => item.Succeeded) ?? 0, results ?? []);
    }
}

public sealed record MaaResourceGenerationResult(
    bool Succeeded,
    string Message,
    string OutputPath,
    string BackupPath,
    int NodeCount)
{
    public static MaaResourceGenerationResult Failed(string message)
    {
        return new MaaResourceGenerationResult(false, message, "", "", 0);
    }
}

public sealed record MaaTaskDetailSnapshot(
    string Summary,
    string RecognitionDetailJson,
    string Algorithm,
    bool Hit);

public sealed record MaaTaskDiagnosticsSnapshot(
    int Total,
    int Succeeded,
    int Hit,
    int Failed,
    int OcrCandidateCount,
    int TemplateCandidateCount,
    string Summary,
    IReadOnlyList<string> Lines)
{
    public static MaaTaskDiagnosticsSnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        "MAA task未実行",
        ["MAA Resource taskを実行すると診断サマリを表示します。"]);
}

public sealed record SukiOptionalRuntimeStatus(
    string Label,
    string State,
    string Detail,
    bool Installed,
    bool Installing);

public sealed record SukiOptionalRuntimeProbeSnapshot(
    SukiOptionalRuntimeStatus Glm,
    SukiOptionalRuntimeStatus Ollama);

public sealed record SukiOptionalRuntimeActionResult(
    SukiOptionalRuntimeStatus Status,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public sealed record SukiHypervisorStatus(
    string State,
    string Detail,
    bool Available,
    bool RequiresBiosChange,
    string Severity);

public sealed record RhodesRecognitionScanHistoryItem(
    string ProfileId,
    string ProfileLabel,
    string Source,
    string Status,
    string StartedAt,
    string CompletedAt,
    int CandidateCount,
    int LogCount,
    int ResourceTaskCount,
    int PresetTaskCount,
    string ExecutionPlanState,
    string ExecutionPlanStateLabel,
    string ExecutionPlanSource,
    int ExecutionPlanTaskCount,
    string ExecutionPlanError,
    string ContractState,
    string ContractSummary,
    string ContractDetail,
    int ContractErrorCount,
    string LogPath,
    string Error,
    DateTimeOffset SortTimestamp)
{
    public string DisplayProfile => string.IsNullOrWhiteSpace(ProfileLabel)
        ? string.IsNullOrWhiteSpace(ProfileId) ? "profile不明" : ProfileId
        : ProfileLabel;

    public string SourceLabel => string.IsNullOrWhiteSpace(Source) ? "source不明" : Source;

    public string StatusLabel => string.IsNullOrWhiteSpace(Status) ? "status不明" : Status;

    public string TimestampLabel => SortTimestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public string ExecutionPlanSummary
    {
        get
        {
            var parts = new List<string>
            {
                string.IsNullOrWhiteSpace(ExecutionPlanStateLabel) ? ExecutionPlanState : ExecutionPlanStateLabel,
                ExecutionPlanSource,
            };
            if (ExecutionPlanTaskCount > 0)
                parts.Add($"tasks={ExecutionPlanTaskCount}");
            if (!string.IsNullOrWhiteSpace(ExecutionPlanError))
                parts.Add(ExecutionPlanError);
            return string.Join("/", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string ContractStatusSummary
    {
        get
        {
            var parts = new List<string>
            {
                ContractState,
                ContractSummary,
            };
            if (ContractErrorCount > 0)
                parts.Add($"errors={ContractErrorCount}");
            return string.Join("/", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }
    }

    public string Summary => ResourceTaskCount > 0
        ? string.Join(" / ", new[]
            {
                SourceLabel,
                $"candidates={CandidateCount}",
                $"tasks={ResourceTaskCount}",
                $"preset={PresetTaskCount}",
                string.IsNullOrWhiteSpace(ExecutionPlanSummary) ? "" : $"plan={ExecutionPlanSummary}",
                string.IsNullOrWhiteSpace(ContractStatusSummary) ? "" : $"contract={ContractStatusSummary}",
                $"log={LogCount}",
            }.Where(part => !string.IsNullOrWhiteSpace(part)))
        : $"{SourceLabel} / candidates={CandidateCount} / log={LogCount}";

    public string Detail => string.IsNullOrWhiteSpace(Error)
        ? string.IsNullOrWhiteSpace(ContractDetail) ? LogPath : $"{ContractDetail} / {LogPath}"
        : $"{Error} / {LogPath}";
}

public sealed record RhodesRecognitionScanHistoryPayload(
    IReadOnlyList<MaaCandidatePreview> Candidates,
    IReadOnlyList<MaaTaskRunResult> TaskResults,
    IReadOnlyList<RhodesRecognitionScanLogRow> LogRows,
    string Error,
    string ProfileId = "",
    string ProfileLabel = "",
    string Source = "",
    string Status = "",
    string FrameId = "",
    string FrameMetadataPath = "",
    string StateSnapshotPath = "")
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);

    public string FirstImagePath => LogRows.FirstOrDefault(row => row.HasImagePath)?.Path ?? "";
}

public sealed record RhodesRecognitionScanLogRow(
    string Event,
    string At,
    string Entry,
    string Stage,
    string Label,
    string Detail,
    string Path)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Event) ? "event不明" : Event;

    public string Context => string.Join(" / ", new[] { Entry, Stage, Label }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public string Summary => string.IsNullOrWhiteSpace(Context) ? Detail : $"{Context} / {Detail}";

    public bool HasImagePath => Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
        || Path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
        || Path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
}

public sealed record MaaCandidatePreview(
    string Kind,
    string Label,
    string Value,
    string RawText,
    double? Confidence,
    string Field = "",
    string OperatorId = "",
    string RelicId = "",
    string CampaignId = "",
    string RecognitionKey = "",
    string ThoughtId = "",
    string AgeId = "",
    string FieldId = "",
    string SlotKind = "",
    string EffectId = "",
    string StateId = "",
    string CoinId = "",
    string StatusId = "",
    string Face = "",
    int Count = 0,
    int OperatorInstance = 0)
{
    public string Identity => FirstNonEmpty(Field, OperatorId, RelicId, ThoughtId, AgeId, EffectId, CoinId, RecognitionKey, CampaignId);

    public string DebugDetail
    {
        get
        {
            var parts = new[]
            {
                Part("field", Field),
                Part("operator", OperatorId),
                OperatorInstance > 0 ? $"operatorInstance:{OperatorInstance}" : "",
                Part("relic", RelicId),
                Part("thought", ThoughtId),
                Part("age", AgeId),
                Part("effect", EffectId),
                Part("coin", CoinId),
                Part("status", StatusId),
                Part("state", StateId),
                Part("slot", SlotKind),
                Part("fieldId", FieldId),
                Part("campaign", CampaignId),
                Part("key", RecognitionKey),
            }.Where(part => !string.IsNullOrWhiteSpace(part));
            return string.Join(" · ", parts);
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }

    private static string Part(string label, string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : $"{label}:{value}";
    }
}

public sealed record MaaRoiRescanComparisonRow(
    string State,
    string Label,
    string BeforeValue,
    string AfterValue,
    string Detail,
    string CandidateKey = "",
    string TaskEntry = "")
{
    public string StateLabel => State switch
    {
        "added" => "追加",
        "removed" => "消失",
        "changed" => "変化",
        _ => State,
    };

    public string ValueDiff => $"{BeforeValue} -> {AfterValue}";
}

public sealed record MaaEvidencePreviewNode(
    string Title,
    string Detail,
    string PreviewText,
    IReadOnlyList<MaaEvidencePreviewNode>? Children = null,
    string CandidateKey = "",
    string TaskEntry = "",
    string NodeKind = "item",
    bool ShowDetailByDefault = true)
{
    public IReadOnlyList<MaaEvidencePreviewNode> SafeChildren => Children ?? [];

    public bool HasChildren => SafeChildren.Count > 0;

    public string CountLabel => HasChildren ? SafeChildren.Count.ToString() : "";

    public bool HasVisibleDetail => ShowDetailByDefault && !string.IsNullOrWhiteSpace(Detail);
}

public sealed record SukiCandidateApplySummary(
    int AppliedCount,
    int IgnoredCount,
    IReadOnlyList<string> AppliedFields,
    IReadOnlyList<SukiCandidateApplyOutcome>? OutcomeItems = null)
{
    public IReadOnlyList<SukiCandidateApplyOutcome> Outcomes => OutcomeItems ?? [];

    public static SukiCandidateApplySummary Empty { get; } = new(0, 0, [], []);
}

public sealed record SukiCandidateApplyOutcome(
    int Index,
    string Kind,
    string Label,
    string Value,
    string Identity,
    string Outcome,
    string AppliedField,
    string IgnoredReason);
