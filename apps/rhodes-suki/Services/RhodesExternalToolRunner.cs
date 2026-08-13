using System.Diagnostics;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesExternalToolRunner
{
    public static async Task<RhodesExternalToolResult> RunAsync(
        RhodesExternalToolRequest request,
        bool waitForExit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return new RhodesExternalToolResult(false, -1, "", "", "実行ファイルが指定されていません。");

        var launch = ResolveKnownLocalLaunch(request);
        var extension = Path.GetExtension(launch.FileName);
        if (!launch.UseShellExecute
            && extension is ".cmd" or ".bat" or ".ps1")
        {
            return new RhodesExternalToolResult(
                false,
                -1,
                "",
                "",
                "シェルスクリプトは直接起動しません。exeまたは既定のmaa-evidenceコマンドを指定してください。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = launch.FileName,
            UseShellExecute = launch.UseShellExecute,
            WorkingDirectory = string.IsNullOrWhiteSpace(launch.WorkingDirectory)
                ? Environment.CurrentDirectory
                : launch.WorkingDirectory,
            CreateNoWindow = !launch.UseShellExecute,
            RedirectStandardOutput = waitForExit && !launch.UseShellExecute,
            RedirectStandardError = waitForExit && !launch.UseShellExecute,
        };
        foreach (var argument in launch.Arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (var (name, value) in launch.Environment)
            startInfo.Environment[name] = value;

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
                return new RhodesExternalToolResult(false, -1, "", "", $"起動できませんでした: {launch.FileName}");
            if (!waitForExit)
                return new RhodesExternalToolResult(true, 0, "", "", $"起動しました: {launch.FileName}");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            return new RhodesExternalToolResult(
                process.ExitCode == 0,
                process.ExitCode,
                stdout,
                stderr,
                process.ExitCode == 0 ? "完了しました。" : $"終了コード {process.ExitCode} で失敗しました。");
        }
        catch (Exception ex)
        {
            return new RhodesExternalToolResult(false, -1, "", ex.Message, $"起動に失敗しました: {ex.Message}");
        }
    }

    private static RhodesExternalToolRequest ResolveKnownLocalLaunch(RhodesExternalToolRequest request)
    {
        if (!OperatingSystem.IsWindows() || Path.IsPathRooted(request.FileName))
            return request;

        if (request.FileName.Equals("code", StringComparison.OrdinalIgnoreCase))
        {
            var code = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe"),
            }.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(code))
                return request with { FileName = code };
        }

        if (request.FileName.Equals("maa-evidence", StringComparison.OrdinalIgnoreCase))
        {
            var npmRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm");
            var entry = Path.Combine(npmRoot, "node_modules", "maa-evidence-kit", "dist", "cli", "main.js");
            var node = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
                Path.Combine(AppContext.BaseDirectory, "nodejs-runtime", "node.exe"),
            }.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(node) && File.Exists(entry))
            {
                return request with
                {
                    FileName = node,
                    Arguments = new[] { entry }.Concat(request.Arguments).ToArray(),
                };
            }
        }

        return request;
    }
}
