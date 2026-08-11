using System.Diagnostics;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbProcessService
{
    public static Task<RhodesAdbProcessActionResult> KillMatchingAsync(
        string selectedAdbPath,
        CancellationToken cancellationToken = default)
    {
        return KillMatchingAsync(
            selectedAdbPath,
            EnumerateAdbProcesses(),
            processId => KillProcessAsync(processId, cancellationToken));
    }

    public static async Task<RhodesAdbProcessActionResult> KillMatchingAsync(
        string selectedAdbPath,
        IEnumerable<RhodesAdbProcessInfo> processes,
        Func<int, Task<bool>> killProcessAsync)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(killProcessAsync);
        if (string.IsNullOrWhiteSpace(selectedAdbPath)
            || !Path.IsPathFullyQualified(selectedAdbPath.Trim()))
        {
            return new RhodesAdbProcessActionResult(false, 0, "選択中ADBの絶対パスを確認できないため、プロセスを終了しませんでした。");
        }

        var selected = NormalizeExecutablePath(selectedAdbPath);
        if (selected.Length == 0)
            return new RhodesAdbProcessActionResult(false, 0, "選択中ADBの絶対パスを確認できないため、プロセスを終了しませんでした。");

        var affected = 0;
        foreach (var process in processes)
        {
            var candidate = NormalizeExecutablePath(process.ExecutablePath);
            if (!candidate.Equals(selected, StringComparison.OrdinalIgnoreCase))
                continue;
            if (await killProcessAsync(process.ProcessId))
                affected++;
        }

        return new RhodesAdbProcessActionResult(
            true,
            affected,
            affected == 0
                ? "選択中ADBと完全一致する実行中プロセスはありませんでした。"
                : $"選択中ADBと完全一致するプロセスを{affected}件終了しました。");
    }

    private static IReadOnlyList<RhodesAdbProcessInfo> EnumerateAdbProcesses()
    {
        var result = new List<RhodesAdbProcessInfo>();
        foreach (var process in Process.GetProcessesByName("adb"))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                        result.Add(new RhodesAdbProcessInfo(process.Id, path));
                }
                catch
                {
                    // 実行ファイルパスを確認できないプロセスは安全のため対象外にする。
                }
            }
        }
        return result;
    }

    private static async Task<bool> KillProcessAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            process.Kill(entireProcessTree: false);
            await process.WaitForExitAsync(timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try
        {
            return Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return "";
        }
    }
}
