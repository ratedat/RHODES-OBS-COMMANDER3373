using System.Windows.Input;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.ViewModels;

public sealed class RhodesMaaDevelopmentToolsViewModel : ViewModelBase
{
    private readonly string _repositoryRoot;
    private readonly string _debugRoot;
    private string _materialsPath;
    private string _logAnalyzerPath = "";
    private string _evidenceExecutable = "maa-evidence";
    private string _status = "未確認。外部ツールはローカルに導入済みの場合だけ起動します。";
    private string _lastEvidencePath = "";
    private string _evidenceSummary = "MEK未実行";

    public RhodesMaaDevelopmentToolsViewModel(string repositoryRoot, string debugRoot)
    {
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
        _debugRoot = Path.GetFullPath(debugRoot);
        _materialsPath = Directory.Exists(_debugRoot) ? _debugRoot : _repositoryRoot;
        OpenMseCommand = new AsyncRelayCommand(() => OpenDevelopmentToolAsync("mse"));
        OpenMpeCommand = new AsyncRelayCommand(() => OpenDevelopmentToolAsync("mpe"));
        OpenLogAnalyzerCommand = new AsyncRelayCommand(OpenLogAnalyzerAsync);
        RunEvidenceKitCommand = new AsyncRelayCommand(RunEvidenceKitAsync);
        ProbeEvidenceKitCommand = new AsyncRelayCommand(ProbeEvidenceKitAsync);
    }

    public string RepositoryRoot => _repositoryRoot;

    public string MaterialsPath
    {
        get => _materialsPath;
        set => SetProperty(ref _materialsPath, value?.Trim() ?? "");
    }

    public string LogAnalyzerPath
    {
        get => _logAnalyzerPath;
        set => SetProperty(ref _logAnalyzerPath, value?.Trim() ?? "");
    }

    public string EvidenceExecutable
    {
        get => _evidenceExecutable;
        set => SetProperty(ref _evidenceExecutable, string.IsNullOrWhiteSpace(value) ? "maa-evidence" : value.Trim());
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string LastEvidencePath
    {
        get => _lastEvidencePath;
        private set => SetProperty(ref _lastEvidencePath, value);
    }

    public string EvidenceSummary
    {
        get => _evidenceSummary;
        private set => SetProperty(ref _evidenceSummary, value);
    }

    public ICommand OpenMseCommand { get; }

    public ICommand OpenMpeCommand { get; }

    public ICommand OpenLogAnalyzerCommand { get; }

    public ICommand RunEvidenceKitCommand { get; }

    public ICommand ProbeEvidenceKitCommand { get; }

    public void SetMaterialsPath(string path) => MaterialsPath = path;

    public void SetLogAnalyzerPath(string path) => LogAnalyzerPath = path;

    private async Task OpenDevelopmentToolAsync(string toolId)
    {
        var result = await RhodesExternalToolRunner.RunAsync(
            RhodesMaaDevelopmentTools.BuildOpenRequest(toolId, _repositoryRoot),
            waitForExit: false);
        Status = result.Succeeded
            ? $"{toolId.ToUpperInvariant()}をVS Codeで開きました。対応拡張が未導入の場合は通常エディター表示になります。"
            : result.Detail;
    }

    private async Task OpenLogAnalyzerAsync()
    {
        if (string.IsNullOrWhiteSpace(LogAnalyzerPath) || !File.Exists(LogAnalyzerPath))
        {
            Status = "MaaLogAnalyzerのローカル実行ファイルを指定してください。自動ダウンロードや外部送信は行いません。";
            return;
        }
        if (!File.Exists(MaterialsPath) && !Directory.Exists(MaterialsPath))
        {
            Status = "解析対象のログ・ZIP・フォルダが見つかりません。";
            return;
        }

        try
        {
            var result = await RhodesExternalToolRunner.RunAsync(
                RhodesMaaDevelopmentTools.BuildLogAnalyzerRequest(LogAnalyzerPath, MaterialsPath),
                waitForExit: false);
            Status = result.Succeeded ? "MaaLogAnalyzerをローカル起動しました。" : result.Detail;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
    }

    private async Task ProbeEvidenceKitAsync()
    {
        var request = new RhodesExternalToolRequest(
            EvidenceExecutable,
            ["--version"],
            UseShellExecute: false,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RhodesMaaEvidenceKit.AutoUpdateEnvironment] = "0",
                [RhodesMaaEvidenceKit.TelemetryEnvironment] = "0",
            });
        var result = await RhodesExternalToolRunner.RunAsync(request, waitForExit: true);
        Status = result.Succeeded
            ? $"MaaEvidenceKit {result.StandardOutput.Trim()} / 自動更新OFF / テレメトリOFF"
            : $"MaaEvidenceKit未確認: {result.Detail}";
    }

    private async Task RunEvidenceKitAsync()
    {
        if (!File.Exists(MaterialsPath) && !Directory.Exists(MaterialsPath))
        {
            Status = "MEK解析対象のログ・ZIP・フォルダが見つかりません。";
            return;
        }

        var outputDirectory = Path.Combine(_debugRoot, "maa-evidence");
        var outputPath = Path.Combine(outputDirectory, $"maa-evidence-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}.json");
        var (process, summary) = await RhodesMaaEvidenceKit.InspectAsync(
            EvidenceExecutable,
            MaterialsPath,
            outputPath);
        if (!process.Succeeded)
        {
            Status = $"MaaEvidenceKit失敗: {process.Detail} {process.StandardError}".Trim();
            EvidenceSummary = "MEK証跡を生成できませんでした。";
            return;
        }

        LastEvidencePath = outputPath;
        EvidenceSummary = summary.IsSupported
            ? $"artifact={summary.ArtifactCount} / evidence={summary.EvidenceCount} / missing={summary.MissingEvidenceCount} / warning={summary.WarningCount}"
            : summary.Detail;
        Status = $"MaaEvidenceKit完了（ローカルのみ）: {EvidenceSummary}";
    }
}
