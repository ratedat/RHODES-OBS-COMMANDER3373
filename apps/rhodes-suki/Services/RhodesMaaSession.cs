using MaaFramework.Binding;
using MaaFramework.Binding.Buffers;
using MaaFramework.Binding.Custom;
using RhodesSuki.Models;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace RhodesSuki.Services;

public sealed class RhodesMaaSession : IDisposable
{
    private static int NativeRuntimePrepared;
    private static readonly SemaphoreSlim ToolkitConfigGate = new(1, 1);
    private static bool ToolkitConfigInitialized;
    private MaaResource? _resource;
    private MaaController? _controller;
    private MaaTasker? _tasker;
    private RhodesOfflineMaaControllerApi? _offlineControllerApi;

    public MaaTasker? Tasker => _tasker;

    public bool IsControllerReady => _tasker?.Controller is { IsConnected: true };

    public bool IsTaskerReady => _tasker is not null;

    public MaaSessionControllerKind ControllerKind { get; private set; }

    public IntPtr ActiveWin32WindowHandle { get; private set; }

    public MaaPcConnectionPlan? ActivePcConnectionPlan { get; private set; }

    public MaaSessionOptions? EffectiveOptions { get; private set; }

    public static MaaSessionOptions DefaultAdbOptions(
        string adbPath = "adb",
        string adbSerial = "",
        string adbConfigJson = "{}",
        AdbInputMethods inputMethod = AdbInputMethods.Default,
        AdbScreencapMethods screencapMethod = AdbScreencapMethods.Default,
        string connectionPreset = "auto")
    {
        return new MaaSessionOptions(
            RhodesMaaPaths.DefaultResourceRoot,
            RhodesMaaPaths.DefaultAgentBinaryRoot,
            adbPath,
            adbSerial,
            SukiAdbConfigJson.Normalize(adbConfigJson),
            inputMethod,
            screencapMethod,
            connectionPreset);
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

        var toolkitDetail = "";
        try
        {
            var toolkitResolution = await RhodesMaaAdbConnectionResolver.ResolveToolkitAsync(
                options,
                FindToolkitDevicesAsync,
                cancellationToken);
            toolkitDetail = toolkitResolution.Detail;
            options = toolkitResolution.DeviceResolved
                ? toolkitResolution.Options
                : RhodesMaaAdbConnectionResolver.ApplyPresetExtras(options);
        }
        catch (Exception ex)
        {
            toolkitDetail = $"MaaToolkit検出失敗: {ex.Message}";
            options = RhodesMaaAdbConnectionResolver.ApplyPresetExtras(options);
        }

        if (string.IsNullOrWhiteSpace(options.AdbSerial))
        {
            return Snapshot(
                "端末未選択",
                string.IsNullOrWhiteSpace(toolkitDetail) ? "ADB serialを選択してください。" : toolkitDetail,
                options,
                false);
        }

        EffectiveOptions = options;

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _resource = CreateResource(options);
                _controller = new MaaAdbController(
                    options.AdbPath,
                    options.AdbSerial,
                    options.ScreencapMethod,
                    options.InputMethod,
                    options.AdbConfigJson,
                    options.AgentBinaryRoot,
                    LinkOption.None,
                    CheckStatusOption.None);

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
                ControllerKind = ok ? MaaSessionControllerKind.Adb : MaaSessionControllerKind.None;
                return Snapshot(
                    ok ? "接続済み" : "接続失敗",
                    $"MAA Controller LinkStart: {linkStatus} / {toolkitDetail}",
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

    public async Task<MaaSessionSnapshot> InitializeOfflineAsync(MaaSessionOptions options, CancellationToken cancellationToken = default)
    {
        DisposeCurrent();

        var runtimeStatus = MaaFrameworkRuntimeProbe.ProbeAppBaseDirectory(AppContext.BaseDirectory);
        if (!runtimeStatus.IsReady)
            return Snapshot($"MAAFramework {runtimeStatus.State}", runtimeStatus.Detail, options, false);

        EnsureNativeRuntimeDirectory();

        if (!Directory.Exists(options.ResourceRoot))
            return Snapshot("Resource未配置", "MAA Resource root が存在しません。", options, false);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _resource = CreateResource(options);
                _offlineControllerApi = new RhodesOfflineMaaControllerApi();
                _controller = new MaaCustomController(
                    _offlineControllerApi,
                    LinkOption.Start,
                    CheckStatusOption.ThrowIfNotSucceeded);
                _tasker = new MaaTasker
                {
                    Resource = _resource,
                    Controller = _controller,
                    DisposeOptions = DisposeOptions.All,
                };

                _tasker.Global.SetOption_SaveOnError(false);
                _tasker.Global.SetOption_DebugMode(true);

                if (!_tasker.IsInitialized)
                {
                    DisposeCurrent();
                    return Snapshot(
                        "オフライン初期化失敗",
                        "MAA Tasker をResourceとオフラインControllerで初期化できませんでした。",
                        options,
                        false);
                }

                ControllerKind = MaaSessionControllerKind.Offline;

                return Snapshot(
                    "オフライン認識",
                    "MAA Resource をADBなし再実行用に初期化しました。",
                    options,
                    true);
            }
            catch (Exception ex)
            {
                DisposeCurrent();
                return Snapshot("オフライン初期化失敗", ex.Message, options, false);
            }
        }, cancellationToken);
    }

    public async Task<MaaSessionSnapshot> InitializeWin32Async(
        MaaSessionOptions options,
        IntPtr windowHandle,
        string? screencapMethodId,
        CancellationToken cancellationToken = default)
    {
        return await InitializeWin32Async(
            options,
            windowHandle,
            screencapMethodId,
            null,
            null,
            cancellationToken);
    }

    public async Task<MaaSessionSnapshot> InitializeWin32Async(
        MaaSessionOptions options,
        IntPtr windowHandle,
        string? screencapMethodId,
        string? mouseMethodId,
        string? keyboardMethodId,
        CancellationToken cancellationToken = default)
    {
        DisposeCurrent();

        if (!OperatingSystem.IsWindows())
            return Snapshot("PC撮影非対応", "Win32 ControllerはWindowsでのみ使用できます。", options, false);
        if (!IsCurrentProcessElevated())
        {
            return Snapshot(
                "PC操作権限不足",
                "PCクライアント操作には管理者権限が必要です。RHODESを管理者として起動し直してください。",
                options,
                false);
        }

        var runtimeStatus = MaaFrameworkRuntimeProbe.ProbeAppBaseDirectory(AppContext.BaseDirectory);
        if (!runtimeStatus.IsReady)
            return Snapshot($"MAAFramework {runtimeStatus.State}", runtimeStatus.Detail, options, false);

        EnsureNativeRuntimeDirectory();

        if (!Directory.Exists(options.ResourceRoot))
            return Snapshot("Resource未配置", "MAA Resource root が存在しません。", options, false);
        if (windowHandle == IntPtr.Zero)
            return Snapshot("PCウィンドウ未選択", "撮影するゲームウィンドウを選択してください。", options, false);

        EffectiveOptions = options;
        var plan = RhodesMaaPcConnectionPolicy.Resolve(
            screencapMethodId,
            mouseMethodId,
            keyboardMethodId);
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _resource = CreateResource(options);
                _controller = new MaaWin32Controller(
                    windowHandle,
                    plan.ScreencapMethod,
                    plan.MouseMethod,
                    plan.KeyboardMethod,
                    LinkOption.None,
                    CheckStatusOption.None);
                if (!_controller.SetOption_ScreenshotTargetLongSide(plan.TargetWidth)
                    || !_controller.SetOption_ScreenshotTargetShortSide(plan.TargetHeight))
                {
                    throw new InvalidOperationException("PC撮影の1280x720正規化設定に失敗しました。");
                }

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
                ControllerKind = ok ? MaaSessionControllerKind.Win32 : MaaSessionControllerKind.None;
                ActiveWin32WindowHandle = ok ? windowHandle : IntPtr.Zero;
                ActivePcConnectionPlan = ok ? plan : null;
                return Snapshot(
                    ok ? "PCウィンドウ接続済み" : "PCウィンドウ接続失敗",
                    $"MAA Win32 Controller LinkStart: {linkStatus} / capture={plan.ScreencapMethod} / mouse={plan.MouseMethod} / keyboard={plan.KeyboardMethod} / 1280x720",
                    options,
                    ok);
            }
            catch (Exception ex)
            {
                DisposeCurrent();
                return Snapshot("PCウィンドウ初期化失敗", ex.Message, options, false);
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

    public async Task<MaaJobStatus> TapAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tasker?.Controller is not { IsConnected: true } controller)
                return MaaJobStatus.Invalid;
            return controller.Click(x, y).Wait();
        }, cancellationToken);
    }

    public async Task<MaaJobStatus> SwipeAsync(
        int startX,
        int startY,
        int endX,
        int endY,
        int durationMs,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tasker?.Controller is not { IsConnected: true } controller)
                return MaaJobStatus.Invalid;
            return controller.Swipe(
                startX,
                startY,
                endX,
                endY,
                Math.Max(1, durationMs),
                0,
                1).Wait();
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

    public async Task<MaaTaskRunResult> RunResourceRecognitionAsync(
        string entry,
        string recognitionPayloadJson,
        byte[] encodedImage,
        CancellationToken cancellationToken = default,
        int? scaleOverride = null)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_tasker is null)
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "MAA Tasker が初期化されていません。");
            if (string.IsNullOrWhiteSpace(entry))
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "entry が空です。");
            if (string.IsNullOrWhiteSpace(recognitionPayloadJson))
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "recognition payload が空です。");
            if (encodedImage.Length == 0)
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, "保存Frame画像が空です。");
            if (!RhodesMaaRecognitionInvocation.TryParse(recognitionPayloadJson, out var invocation, out var parseError))
                return new MaaTaskRunResult(entry, MaaJobStatus.Invalid.ToString(), false, parseError);

            var scale = scaleOverride is > 0
                ? Math.Clamp(scaleOverride.Value, 1, 12)
                : RhodesMaaResourceCatalog.LoadRecognitionScale(entry);
            var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
                encodedImage,
                invocation.Type,
                invocation.ParametersJson,
                scale,
                entry);
            using var image = new MaaImageBuffer();
            image.TrySetEncodedData(prepared.EncodedImage);
            var job = _tasker.AppendRecognition(invocation.Type, prepared.ParametersJson, image);
            var status = job.Wait();
            var detail = BuildTaskDetail(_tasker, job.Id, $"ReplayRecognition={entry}; type={invocation.Type}; scale={scale}");
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
            ready,
            options);
    }

    private static MaaResource CreateResource(MaaSessionOptions options)
    {
        var resource = new MaaResource();
        try
        {
            var inferenceConfigured = options.InferenceProvider switch
            {
                InferenceExecutionProvider.Auto => true,
                InferenceExecutionProvider.CPU => resource.SetInference_UseCpu(),
                InferenceExecutionProvider.DirectML => resource.SetInference_UseDirectML(options.InferenceDeviceId),
                _ => false,
            };
            if (!inferenceConfigured)
                throw new InvalidOperationException($"MAA推論設定を適用できませんでした: {options.InferenceProvider}");

            var bundleStatus = resource.AppendBundle(options.ResourceRoot).Wait();
            if (bundleStatus != MaaJobStatus.Succeeded)
                throw new InvalidOperationException($"MAA Resource bundleの読込に失敗しました: {bundleStatus}");
            return resource;
        }
        catch
        {
            resource.Dispose();
            throw;
        }
    }

    private void DisposeCurrent()
    {
        _tasker?.Dispose();
        _tasker = null;
        _controller = null;
        _resource = null;
        _offlineControllerApi = null;
        EffectiveOptions = null;
        ControllerKind = MaaSessionControllerKind.None;
        ActiveWin32WindowHandle = IntPtr.Zero;
        ActivePcConnectionPlan = null;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static async Task<IReadOnlyList<AdbDeviceInfo>> FindToolkitDevicesAsync(
        string adbPath,
        CancellationToken cancellationToken)
    {
        await ToolkitConfigGate.WaitAsync(cancellationToken);
        try
        {
            if (!ToolkitConfigInitialized)
            {
                var userPath = Path.Combine(AppContext.BaseDirectory, "user-data", "maa-toolkit");
                Directory.CreateDirectory(userPath);
                if (!MaaToolkit.Shared.Config.InitOption(userPath, "{}"))
                    throw new InvalidOperationException("MaaToolkit Config.InitOptionに失敗しました。");
                ToolkitConfigInitialized = true;
            }
        }
        finally
        {
            ToolkitConfigGate.Release();
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var devices = await MaaToolkit.Shared.AdbDevice.FindAsync(adbPath);
        cancellationToken.ThrowIfCancellationRequested();
        return devices.ToArray();
    }

    public static void PrepareNativeRuntime()
    {
        EnsureNativeRuntimeDirectory();
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
            "MaaWin32ControlUnit.dll",
        ];
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);
}
