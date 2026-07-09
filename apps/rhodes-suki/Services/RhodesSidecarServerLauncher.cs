using System.Diagnostics;

namespace RhodesSuki.Services;

/// <summary>
/// 配信サーバー (app/server.mjs = overlay / sidecar / state API) をSukiから起動・停止する。
/// 旧Electron版はサーバーを内蔵起動していたが、Suki版では出力ワークスペースから明示的に管理する。
/// アプリ終了時は、このアプリが起動したプロセスのみ停止する (外部起動のサーバーには触れない)。
/// </summary>
public sealed class RhodesSidecarServerLauncher : IDisposable
{
    private Process? _process;
    private string _lastError = "";

    public bool IsOwnedProcessRunning => _process is { HasExited: false };

    public string LastError => _lastError;

    /// <summary>
    /// app/server.mjs の絶対パスを解決する。見つからなければ空文字。
    /// 開発ビルドでは data がbin配下の同梱コピーに解決されるため、
    /// dataRootの親だけでなく、実行フォルダから上方向にも探索する。
    /// </summary>
    public static string ResolveServerScriptPath()
    {
        foreach (var start in CandidateRoots().Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var directory = start;
            for (var depth = 0; depth < 8 && !string.IsNullOrWhiteSpace(directory); depth++)
            {
                var script = Path.Combine(directory, "app", "server.mjs");
                if (File.Exists(script))
                    return script;

                directory = Path.GetDirectoryName(directory) ?? "";
            }
        }

        return "";
    }

    private static IEnumerable<string> CandidateRoots()
    {
        string dataParent = "";
        try
        {
            dataParent = Path.GetDirectoryName(RhodesRunCatalog.ResolveDataRoot()) ?? "";
        }
        catch
        {
            // dataが解決できなくても他候補で探索を続ける。
        }

        yield return dataParent;
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;
    }

    public RhodesSidecarLaunchResult Start(string apiUrl)
    {
        if (IsOwnedProcessRunning)
            return new RhodesSidecarLaunchResult(true, "配信サーバーは起動済みです (このアプリが管理中)。");

        var script = ResolveServerScriptPath();
        if (string.IsNullOrWhiteSpace(script))
        {
            return new RhodesSidecarLaunchResult(
                false,
                "app/server.mjs が見つかりません。リポジトリ配置で実行するか、`npm run dev` で手動起動してください。");
        }

        var port = ResolvePort(apiUrl);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "node",
                WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(script)) ?? "",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add(script);
            startInfo.ArgumentList.Add("--port");
            startInfo.ArgumentList.Add(port.ToString());

            var process = Process.Start(startInfo);
            if (process is null)
                return new RhodesSidecarLaunchResult(false, "nodeプロセスを開始できませんでした。");

            // パイプ詰まり防止のため出力は読み捨て、直近のstderrだけ診断用に保持する。
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    _lastError = args.Data;
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _process = process;
            return new RhodesSidecarLaunchResult(true, $"配信サーバーを起動しました (port {port})。");
        }
        catch (Exception ex)
        {
            return new RhodesSidecarLaunchResult(
                false,
                $"起動失敗: {ex.Message} / Node.js が必要です。手動起動: npm run dev");
        }
    }

    public string Stop()
    {
        if (_process is not { } process)
            return "このアプリが起動した配信サーバーはありません (外部起動分は停止しません)。";

        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // 既に終了している場合は成功扱い。
        }

        process.Dispose();
        _process = null;
        return "配信サーバーを停止しました。";
    }

    private static int ResolvePort(string apiUrl)
    {
        return Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) && uri.Port > 0 ? uri.Port : 5173;
    }

    public void Dispose()
    {
        Stop();
    }
}

public sealed record RhodesSidecarLaunchResult(bool Succeeded, string Message);
