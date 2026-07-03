using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using RhodesSuki.Models;
using System.Runtime.InteropServices;

namespace RhodesSuki.Services;

public sealed class RhodesMaaSession : IDisposable
{
    private static int NativeRuntimePrepared;
    private MaaResource? _resource;
    private MaaController? _controller;
    private MaaTasker? _tasker;

    public MaaTasker? Tasker => _tasker;

    public bool IsControllerReady => _tasker?.Controller is { IsConnected: true };

    public static MaaSessionOptions DefaultAdbOptions(
        string adbPath = "adb",
        string adbSerial = "",
        string adbConfigJson = "{}",
        AdbInputMethods inputMethod = AdbInputMethods.Default,
        AdbScreencapMethods screencapMethod = AdbScreencapMethods.Default)
    {
        return new MaaSessionOptions(
            RhodesMaaPaths.DefaultResourceRoot,
            RhodesMaaPaths.DefaultAgentBinaryRoot,
            adbPath,
            adbSerial,
            SukiAdbConfigJson.Normalize(adbConfigJson),
            inputMethod,
            screencapMethod);
    }

    public static MaaSessionSnapshot ProbeDefaultPaths()
    {
        var options = DefaultAdbOptions();
        var resourceExists = Directory.Exists(options.ResourceRoot);
        var agentExists = Directory.Exists(options.AgentBinaryRoot);
        var missingResources = resourceExists
            ? RhodesMaaPaths.MissingRecognitionResourceFiles(options.ResourceRoot)
            : Array.Empty<string>();
        var state = !resourceExists
            ? "Resource未配置"
            : missingResources.Count > 0
                ? "認識資産不足"
                : "Resource検出";
        var detail = missingResources.Count > 0
            ? RhodesMaaPaths.RecognitionResourceStatusDetail(options.ResourceRoot)
            : agentExists
            ? "MAA Resource と AgentBinary の探索パスを確認しました。"
            : "MAA Resource は作成済みです。AgentBinary は NuGet publish 出力で確認します。";

        return new MaaSessionSnapshot(
            state,
            detail,
            options.ResourceRoot,
            options.AgentBinaryRoot,
            resourceExists,
            agentExists,
            resourceExists && missingResources.Count == 0);
    }

    public async Task<MaaSessionSnapshot> InitializeAdbAsync(MaaSessionOptions options, CancellationToken cancellationToken = default)
    {
        DisposeCurrent();

        var runtimeStatus = MaaFrameworkRuntimeProbe.ProbeAppBaseDirectory(AppContext.BaseDirectory);
        if (!runtimeStatus.IsReady)
            return Snapshot($"MAAFramework {runtimeStatus.State}", runtimeStatus.Detail, options, false);

        EnsureNativeRuntimeDirectory();

        if (!Directory.Exists(options.ResourceRoot))
        {
            return Snapshot("Resource未配置", "MAA Resource root が存在しません。", options, false);
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _resource = new MaaResource(options.ResourceRoot);
                _controller = new MaaAdbController(
                    options.AdbPath,
                    options.AdbSerial,
                    options.ScreencapMethod,
                    options.InputMethod,
                    options.AdbConfigJson,
                    options.AgentBinaryRoot);

                _tasker = new MaaTasker
                {
                    Resource = _resource,
                    Controller = _controller,
                    DisposeOptions = DisposeOptions.All,
                };

                _tasker.Global.SetOption_SaveOnError(false);
                _tasker.Global.SetOption_DebugMode(true);

                var linkStatus = _tasker.Controller?.LinkStart().Wait();
                var ok = linkStatus == MaaJobStatus.Succeeded;
                return Snapshot(
                    ok ? "接続済み" : "接続失敗",
                    $"MAA Controller LinkStart: {linkStatus}",
                    options,
                    ok);
            }
            catch (Exception ex)
            {
                DisposeCurrent();
                return Snapshot("初期化失敗", ex.Message, options, false);
            }
        }, cancellationToken);
    }

    public MaaJobStatus Capture()
    {
        if (_tasker?.Controller is not { IsConnected: true } controller)
            return MaaJobStatus.Invalid;
        return controller.Screencap().Wait();
    }

    public async Task<MaaCaptureResult> CaptureEncodedAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tasker?.Controller is not { IsConnected: true } controller)
            {
                return new MaaCaptureResult(
                    MaaJobStatus.Invalid.ToString(),
                    false,
                    "MAA Controller が接続されていません。",
                    []);
            }

            using var buffer = new MaaImageBuffer();
            var status = controller.Screencap().Wait();
            if (status != MaaJobStatus.Succeeded)
            {
                return new MaaCaptureResult(status.ToString(), false, "Screencap が失敗しました。", []);
            }

            if (!controller.GetCachedImage(buffer))
            {
                return new MaaCaptureResult(status.ToString(), false, "Cached image を取得できませんでした。", []);
            }

            if (!buffer.TryGetEncodedData(out byte[]? encodedImage) || encodedImage is null || encodedImage.Length == 0)
            {
                return new MaaCaptureResult(status.ToString(), false, "Encoded image を取得できませんでした。", []);
            }

            return new MaaCaptureResult(status.ToString(), true, $"{encodedImage.Length:N0} bytes", encodedImage);
        }, cancellationToken);
    }

    public async Task<MaaTaskRunResult> RunResourceTaskAsync(
        string entry,
        string pipelineOverrideJson = "{}",
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tasker is null)
            {
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "MAA Tasker が初期化されていません。");
            }

            if (string.IsNullOrWhiteSpace(entry))
            {
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "entry が空です。");
            }

            var job = _tasker.AppendTask(entry.Trim(), string.IsNullOrWhiteSpace(pipelineOverrideJson) ? "{}" : pipelineOverrideJson);
            var status = job.Wait();
            var detail = BuildTaskDetail(_tasker, job.Id, $"TaskId={job.Id}");
            return new MaaTaskRunResult(
                entry,
                status.ToString(),
                status == MaaJobStatus.Succeeded,
                detail.Summary,
                detail.RecognitionDetailJson,
                detail.Algorithm,
                detail.Hit);
        }, cancellationToken);
    }

    internal static MaaTaskDetailSnapshot BuildTaskDetail(MaaTasker tasker, long taskId, string fallback)
    {
        try
        {
            tasker.GetTaskDetail(taskId, out var entry, out var nodeIdList, out var statusJson);
            if (nodeIdList.Count() == 0)
                return new MaaTaskDetailSnapshot($"{fallback}; entry={entry}; detail={statusJson}", "", "", false);

            tasker.GetNodeDetail(
                nodeIdList[0],
                out var nodeName,
                out var recognitionId,
                out var actionId,
                out var actionCompleted);

            using var hitBox = new MaaRectBuffer();
            tasker.GetRecognitionDetail(
                recognitionId,
                out var recognitionNode,
                out var algorithm,
                out var hit,
                hitBox,
                out var recognitionDetailJson,
                null,
                null);

            var summary = $"TaskId={taskId}; entry={entry}; node={nodeName}; recognition={recognitionNode}; algorithm={algorithm}; hit={hit}; actionId={actionId}; actionCompleted={actionCompleted}";
            return new MaaTaskDetailSnapshot(summary, recognitionDetailJson, algorithm, hit);
        }
        catch (Exception ex)
        {
            return new MaaTaskDetailSnapshot($"{fallback}; detail unavailable: {ex.Message}", "", "", false);
        }
    }

    public void Dispose()
    {
        DisposeCurrent();
    }

    private static MaaSessionSnapshot Snapshot(string state, string detail, MaaSessionOptions options, bool ready)
    {
        return new MaaSessionSnapshot(
            state,
            detail,
            options.ResourceRoot,
            options.AgentBinaryRoot,
            Directory.Exists(options.ResourceRoot),
            Directory.Exists(options.AgentBinaryRoot),
            ready);
    }

    private void DisposeCurrent()
    {
        _tasker?.Dispose();
        _tasker = null;
        _controller = null;
        _resource = null;
    }

    private static void EnsureNativeRuntimeDirectory()
    {
        if (Interlocked.Exchange(ref NativeRuntimePrepared, 1) == 1)
            return;

        var nativeDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            CurrentRuntimeIdentifier(),
            "native");
        if (!Directory.Exists(nativeDirectory))
            return;

        if (OperatingSystem.IsWindows())
            _ = SetDllDirectory(nativeDirectory);

        foreach (var fileName in WindowsCoreNativeFiles())
        {
            var fullPath = Path.Combine(nativeDirectory, fileName);
            if (File.Exists(fullPath))
                _ = NativeLibrary.Load(fullPath);
        }
    }

    private static string CurrentRuntimeIdentifier()
    {
        var os = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "linux";
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };
        return $"{os}-{arch}";
    }

    private static IReadOnlyList<string> WindowsCoreNativeFiles()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        return
        [
            "MaaUtils.dll",
            "MaaToolkit.dll",
            "MaaFramework.dll",
            "MaaAdbControlUnit.dll",
        ];
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);
}
