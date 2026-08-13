using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Input;
using MaaFramework.Binding;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.ViewModels;

public sealed record RhodesRecognitionLabModeOption(string Id, string Label, string Detail);

public sealed class RhodesRecognitionLabViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions LogJsonOptions = new() { WriteIndented = true };
    private readonly RhodesMaaSession _session;
    private readonly Func<byte[]> _latestCaptureProvider;
    private readonly Func<string> _latestCapturePathProvider;
    private readonly string _logRoot;
    private RhodesRecognitionLabModeOption? _selectedMode;
    private string _imagePath = "";
    private int _roiX;
    private int _roiY;
    private int _roiWidth = 1280;
    private int _roiHeight = 720;
    private string _expected = "";
    private double _threshold = 0.7;
    private bool _onlyRecognition = true;
    private string _template = "";
    private int _templateMethod = 5;
    private int _colorMethod = 40;
    private string _colorLower = "0,80,100";
    private string _colorUpper = "20,255,255";
    private int _colorCount = 12;
    private bool _colorConnected = true;
    private string _status = "未実行。結果は候補表示とログ保存だけで、ラン状態へ自動反映しません。";
    private string _resultJson = "";
    private string _lastLogPath = "";

    public RhodesRecognitionLabViewModel(
        RhodesMaaSession session,
        Func<byte[]> latestCaptureProvider,
        Func<string> latestCapturePathProvider,
        string logRoot)
    {
        _session = session;
        _latestCaptureProvider = latestCaptureProvider;
        _latestCapturePathProvider = latestCapturePathProvider;
        _logRoot = Path.GetFullPath(logRoot);
        ModeOptions = new ObservableCollection<RhodesRecognitionLabModeOption>
        {
            new("ocr", "MAA-OCR", "任意ROIをOCRし、expected・threshold・only_recを比較します。"),
            new("template-match", "MAA TemplateMatch", "resource/base/image内のtemplateを任意ROIで比較します。"),
            new("color-match", "MAA ColorMatch", "色域・method・count・connectedを任意ROIで比較します。"),
            new("sui-active-coins", "歳・有効銭", "現行の画像分類と必要な行OCRを、状態へ反映せず確認します。"),
            new("sui-owned-coins", "歳・保有銭", "現行の銭画像分類、欠落名OCR、状態認識をまとめて確認します。"),
            new("sui-owned-status", "歳・銭状態", "保有銭の状態アイコン認識を重点確認します。"),
        };
        _selectedMode = ModeOptions[0];
        RunCommand = new AsyncRelayCommand(RunAsync);
        UseLatestCaptureCommand = new AsyncRelayCommand(() =>
        {
            UseLatestCapture();
            return Task.CompletedTask;
        });
    }

    public ObservableCollection<RhodesRecognitionLabModeOption> ModeOptions { get; }

    public RhodesRecognitionLabModeOption? SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!SetProperty(ref _selectedMode, value))
                return;
            OnPropertyChanged(nameof(IsOcrMode));
            OnPropertyChanged(nameof(IsTemplateMode));
            OnPropertyChanged(nameof(IsColorMode));
            OnPropertyChanged(nameof(SelectedModeDetail));
        }
    }

    public string SelectedModeDetail => SelectedMode?.Detail ?? "";

    public bool IsOcrMode => SelectedMode?.Id == "ocr";

    public bool IsTemplateMode => SelectedMode?.Id == "template-match";

    public bool IsColorMode => SelectedMode?.Id == "color-match";

    public string ImagePath
    {
        get => _imagePath;
        set => SetProperty(ref _imagePath, value?.Trim() ?? "");
    }

    public int RoiX { get => _roiX; set => SetProperty(ref _roiX, value); }

    public int RoiY { get => _roiY; set => SetProperty(ref _roiY, value); }

    public int RoiWidth { get => _roiWidth; set => SetProperty(ref _roiWidth, value); }

    public int RoiHeight { get => _roiHeight; set => SetProperty(ref _roiHeight, value); }

    public string Expected { get => _expected; set => SetProperty(ref _expected, value ?? ""); }

    public double Threshold { get => _threshold; set => SetProperty(ref _threshold, value); }

    public bool OnlyRecognition { get => _onlyRecognition; set => SetProperty(ref _onlyRecognition, value); }

    public string Template { get => _template; set => SetProperty(ref _template, value ?? ""); }

    public int TemplateMethod { get => _templateMethod; set => SetProperty(ref _templateMethod, value); }

    public int ColorMethod { get => _colorMethod; set => SetProperty(ref _colorMethod, value); }

    public string ColorLower { get => _colorLower; set => SetProperty(ref _colorLower, value ?? ""); }

    public string ColorUpper { get => _colorUpper; set => SetProperty(ref _colorUpper, value ?? ""); }

    public int ColorCount { get => _colorCount; set => SetProperty(ref _colorCount, value); }

    public bool ColorConnected { get => _colorConnected; set => SetProperty(ref _colorConnected, value); }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public string ResultJson { get => _resultJson; private set => SetProperty(ref _resultJson, value); }

    public string LastLogPath { get => _lastLogPath; private set => SetProperty(ref _lastLogPath, value); }

    public ICommand RunCommand { get; }

    public ICommand UseLatestCaptureCommand { get; }

    public void SetImagePath(string path) => ImagePath = path;

    public void UseLatestCapture()
    {
        var path = _latestCapturePathProvider();
        ImagePath = string.IsNullOrWhiteSpace(path) ? "<メモリ上の最新Frame>" : path;
    }

    private async Task RunAsync()
    {
        var watch = Stopwatch.StartNew();
        var request = BuildRequest();
        var plan = RhodesRecognitionLab.BuildPlan(request);
        if (!plan.IsValid)
        {
            Status = plan.Error;
            ResultJson = "";
            return;
        }

        try
        {
            var bytes = await LoadImageAsync();
            if (bytes.Length == 0)
                throw new InvalidOperationException("認識対象Frameを読み込めません。画像を選ぶか、先に撮影してください。");

            var execution = plan.UsesMaaRecognition
                ? await RunMaaRecognitionAsync(plan, bytes)
                : await RunSuiRecognitionAsync(plan, bytes);
            watch.Stop();
            var result = new RhodesRecognitionLabResult(
                execution.Succeeded,
                plan.Mode,
                execution.Detail,
                execution.Json,
                watch.ElapsedMilliseconds);
            ResultJson = execution.Json;
            LastLogPath = await WriteLogAsync(request, plan, result, bytes);
            Status = $"{(execution.Succeeded ? "完了" : "失敗")} / {watch.ElapsedMilliseconds}ms / 状態へ未反映 / log={LastLogPath}";
        }
        catch (Exception ex)
        {
            watch.Stop();
            var result = new RhodesRecognitionLabResult(false, plan.Mode, ex.Message, "", watch.ElapsedMilliseconds);
            LastLogPath = await WriteLogAsync(request, plan, result, []);
            Status = $"認識ラボ失敗: {ex.Message} / log={LastLogPath}";
            ResultJson = "";
        }
    }

    private RhodesRecognitionLabRequest BuildRequest() => new(
        SelectedMode?.Id ?? "",
        string.IsNullOrWhiteSpace(ImagePath) ? "<最新Frame>" : ImagePath,
        new MaaRoi(RoiX, RoiY, RoiWidth, RoiHeight),
        Expected,
        Threshold,
        OnlyRecognition,
        Template,
        TemplateMethod,
        ColorMethod,
        ColorLower,
        ColorUpper,
        ColorCount,
        ColorConnected);

    private async Task<byte[]> LoadImageAsync()
    {
        if (!string.IsNullOrWhiteSpace(ImagePath) && File.Exists(ImagePath))
            return await File.ReadAllBytesAsync(ImagePath);
        return _latestCaptureProvider();
    }

    private async Task<(bool Succeeded, string Detail, string Json)> RunMaaRecognitionAsync(
        RhodesRecognitionLabPlan plan,
        byte[] bytes)
    {
        var entry = $"RhodesRecognitionLab_{plan.Mode.Replace('-', '_')}";
        var result = await _session.RunResourceRecognitionAsync(entry, plan.PayloadJson, bytes);
        var json = JsonSerializer.Serialize(new
        {
            result.Entry,
            result.Status,
            result.Succeeded,
            result.Detail,
            result.Algorithm,
            result.Hit,
            recognitionDetail = ParseJsonOrString(result.RecognitionDetailJson),
        }, LogJsonOptions);
        return (result.Succeeded, result.Detail, json);
    }

    private async Task<(bool Succeeded, string Detail, string Json)> RunSuiRecognitionAsync(
        RhodesRecognitionLabPlan plan,
        byte[] bytes)
    {
        if (plan.Mode == "sui-active-coins")
        {
            var inspections = RhodesSuiCoinImageRecognizer.InspectActive(bytes);
            var native = RhodesSuiCoinImageRecognizer.Recognize(bytes);
            var ocrResults = await RunDynamicOcrAsync(
                RhodesSuiCoinImageRecognizer.PlanActivePanelOcrRequests(inspections),
                bytes);
            return (
                native.Succeeded,
                $"有効銭: inspected={inspections.Count} / rowOcr={ocrResults.Count}",
                JsonSerializer.Serialize(new
                {
                    native = ParseJsonOrString(native.RecognitionDetailJson),
                    inspections,
                    rowOcr = ocrResults,
                }, LogJsonOptions));
        }

        var ownedInspections = RhodesSuiCoinImageRecognizer.InspectOwned(bytes);
        var owned = RhodesSuiCoinImageRecognizer.RecognizeOwnedWithOcrFallback(bytes);
        var ocrResultsOwned = await RunDynamicOcrAsync(owned.NameOcrRequests, bytes);
        var status = RhodesSuiCoinStatusRecognizer.RecognizeOwned(
            bytes,
            ocrResultsOwned.Prepend(owned.ImageResult),
            imageInspections: ownedInspections);
        var resultJson = JsonSerializer.Serialize(new
        {
            owned = ParseJsonOrString(owned.ImageResult.RecognitionDetailJson),
            inspections = ownedInspections,
            missingNameOcr = ocrResultsOwned,
            status = ParseJsonOrString(status.RecognitionDetailJson),
        }, LogJsonOptions);
        return (
            owned.ImageResult.Succeeded && status.Succeeded,
            $"保有銭: inspected={ownedInspections.Count} / nameOcr={ocrResultsOwned.Count} / statusHit={status.Hit}",
            resultJson);
    }

    private async Task<IReadOnlyList<MaaTaskRunResult>> RunDynamicOcrAsync(
        IReadOnlyList<MaaDynamicOcrRequest> requests,
        byte[] bytes)
    {
        var results = new List<MaaTaskRunResult>(requests.Count);
        foreach (var request in requests)
        {
            results.Add(await _session.RunResourceRecognitionAsync(
                request.Entry,
                request.PayloadJson,
                bytes,
                scaleOverride: request.Scale));
        }
        return results;
    }

    private async Task<string> WriteLogAsync(
        RhodesRecognitionLabRequest request,
        RhodesRecognitionLabPlan plan,
        RhodesRecognitionLabResult result,
        byte[] imageBytes)
    {
        Directory.CreateDirectory(_logRoot);
        var path = Path.Combine(_logRoot, $"recognition-lab-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.json");
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            kind = "rhodes-recognition-lab",
            createdAt = DateTimeOffset.Now,
            versions = new
            {
                application = typeof(RhodesRecognitionLabViewModel).Assembly.GetName().Version?.ToString() ?? "",
                maaBinding = typeof(MaaResource).Assembly.GetName().Version?.ToString() ?? "",
            },
            image = new
            {
                path = request.ImagePath,
                bytes = imageBytes.Length,
                sha256 = imageBytes.Length == 0
                    ? ""
                    : Convert.ToHexString(SHA256.HashData(imageBytes)).ToLowerInvariant(),
            },
            safety = new
            {
                recognitionOnly = true,
                stateApplied = false,
                inputActionIssued = false,
            },
            request,
            plan,
            result,
        }, LogJsonOptions);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private static object? ParseJsonOrString(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return json;
        }
    }
}
