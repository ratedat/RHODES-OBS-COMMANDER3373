using System.Diagnostics;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbRecoveryService
{
    public static async Task<RhodesAdbRecoveryResult> ConnectAsync(
        SukiAdbConnectionSettings settings,
        string adbPath,
        Func<CancellationToken, Task<MaaSessionSnapshot>> connectAsync,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<RhodesAdbCommandResult>>? runAdbCommandAsync = null,
        Func<string, CancellationToken, Task<RhodesAdbProcessActionResult>>? killAdbProcessAsync = null,
        Func<string, CancellationToken, Task<bool>>? startEmulatorAsync = null,
        bool destructiveActionsConfirmed = false,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectAsync);
        runAdbCommandAsync ??= RhodesAdbCommandRunner.RunAsync;
        killAdbProcessAsync ??= RhodesAdbProcessService.KillMatchingAsync;
        startEmulatorAsync ??= StartEmulatorAsync;
        delayAsync ??= Task.Delay;
        settings = RhodesSukiSettingsStore.NormalizeAdbConnection(settings);
        var stages = new List<RhodesAdbRecoveryStage>();

        var snapshot = await connectAsync(cancellationToken);
        stages.Add(new RhodesAdbRecoveryStage("initial", snapshot.IsReady, snapshot.Detail));
        if (snapshot.IsReady)
            return new RhodesAdbRecoveryResult(true, snapshot, stages);

        for (var attempt = 1; attempt <= settings.ReconnectAttempts; attempt++)
        {
            if (settings.ReconnectDelayMs > 0)
                await delayAsync(TimeSpan.FromMilliseconds(settings.ReconnectDelayMs), cancellationToken);
            snapshot = await connectAsync(cancellationToken);
            stages.Add(new RhodesAdbRecoveryStage($"reconnect-{attempt}", snapshot.IsReady, snapshot.Detail));
            if (snapshot.IsReady)
                return new RhodesAdbRecoveryResult(true, snapshot, stages);
        }

        if (settings.RestartAdbServerOnFailure)
        {
            var killed = await runAdbCommandAsync(adbPath, ["kill-server"], cancellationToken);
            var started = await runAdbCommandAsync(adbPath, ["start-server"], cancellationToken);
            var stageSucceeded = killed.Succeeded && started.Succeeded;
            stages.Add(new RhodesAdbRecoveryStage(
                "adb-server-restart",
                stageSucceeded,
                $"kill-server={killed.ExitCode}; start-server={started.ExitCode}; {killed.Detail} {started.Detail}".Trim()));
            snapshot = await connectAsync(cancellationToken);
            stages.Add(new RhodesAdbRecoveryStage("after-adb-server-restart", snapshot.IsReady, snapshot.Detail));
            if (snapshot.IsReady)
                return new RhodesAdbRecoveryResult(true, snapshot, stages);
        }

        if (settings.HardRestartAdbProcessOnFailure)
        {
            if (!destructiveActionsConfirmed)
            {
                stages.Add(new RhodesAdbRecoveryStage(
                    "hard-restart-skipped",
                    false,
                    "ADBプロセス終了は、この接続操作で明示確認されていないため実行しませんでした。"));
            }
            else
            {
                var action = await killAdbProcessAsync(adbPath, cancellationToken);
                var start = action.Succeeded
                    ? await runAdbCommandAsync(adbPath, ["start-server"], cancellationToken)
                    : new RhodesAdbCommandResult(-1, "", "ADBプロセス終了に失敗したためstart-serverを省略");
                stages.Add(new RhodesAdbRecoveryStage(
                    "hard-restart",
                    action.Succeeded && start.Succeeded,
                    $"{action.Detail} start-server={start.ExitCode}"));
                snapshot = await connectAsync(cancellationToken);
                stages.Add(new RhodesAdbRecoveryStage("after-hard-restart", snapshot.IsReady, snapshot.Detail));
                if (snapshot.IsReady)
                    return new RhodesAdbRecoveryResult(true, snapshot, stages);
            }
        }

        if (settings.RestartEmulatorOnFailure)
        {
            if (string.IsNullOrWhiteSpace(settings.EmulatorExecutablePath))
            {
                stages.Add(new RhodesAdbRecoveryStage("emulator-start", false, "エミュレーター実行ファイルが未設定です。"));
            }
            else
            {
                var started = await startEmulatorAsync(settings.EmulatorExecutablePath, cancellationToken);
                stages.Add(new RhodesAdbRecoveryStage(
                    "emulator-start",
                    started,
                    started
                        ? $"エミュレーター本体を起動しました: {settings.EmulatorExecutablePath}"
                        : $"エミュレーター本体を起動できませんでした: {settings.EmulatorExecutablePath}"));
                if (started)
                {
                    if (settings.ReconnectDelayMs > 0)
                        await delayAsync(TimeSpan.FromMilliseconds(settings.ReconnectDelayMs), cancellationToken);
                    snapshot = await connectAsync(cancellationToken);
                    stages.Add(new RhodesAdbRecoveryStage("after-emulator-start", snapshot.IsReady, snapshot.Detail));
                    if (snapshot.IsReady)
                        return new RhodesAdbRecoveryResult(true, snapshot, stages);
                }
            }
        }

        return new RhodesAdbRecoveryResult(false, snapshot, stages);
    }

    private static Task<bool> StartEmulatorAsync(string executablePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!IsSupportedEmulatorExecutable(executablePath))
                return Task.FromResult(false);
            var fullPath = Path.GetFullPath(executablePath.Trim());
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                WorkingDirectory = Path.GetDirectoryName(fullPath) ?? "",
                UseShellExecute = true,
            });
            return Task.FromResult(process is not null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    internal static bool IsSupportedEmulatorExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        try
        {
            var trimmed = executablePath.Trim();
            return Path.IsPathFullyQualified(trimmed)
                && Path.GetExtension(trimmed).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                && File.Exists(Path.GetFullPath(trimmed));
        }
        catch
        {
            return false;
        }
    }
}

public static class RhodesAdbCommandRunner
{
    public static async Task<RhodesAdbCommandResult> RunAsync(
        string adbPath,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath.Trim(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        return new RhodesAdbCommandResult(process.ExitCode, await outputTask, await errorTask);
    }
}
