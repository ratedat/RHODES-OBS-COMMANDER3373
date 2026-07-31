using System.Net;
using System.Net.Sockets;

namespace RhodesSuki.Services;

public sealed record RhodesSidecarCompatibilityResult(
    bool Supported,
    string Detail);

public static class RhodesSidecarApiCompatibility
{
    public static async Task<RhodesSidecarCompatibilityResult> ProbeQuickPublishAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RhodesTournamentRemoteApiClient.QuickStatusAsync(
            baseUrl,
            client,
            cancellationToken);

        return new RhodesSidecarCompatibilityResult(
            result.Succeeded,
            result.Succeeded ? "簡易公開API対応" : result.Error);
    }

    public static string FindAvailableLoopbackUrl(string currentUrl, int maxAttempts = 32)
    {
        var uri = Uri.TryCreate(currentUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri("http://127.0.0.1:5173");
        var scheme = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeHttps
            : Uri.UriSchemeHttp;
        var startPort = uri.Port is >= 1024 and < 65535 ? uri.Port + 1 : 5173;

        for (var offset = 0; offset < Math.Max(1, maxAttempts); offset++)
        {
            var port = startPort + offset;
            if (port > 65535)
                port = 5173 + (port - 65536);

            if (IsLoopbackPortAvailable(port))
                return $"{scheme}://127.0.0.1:{port}";
        }

        throw new InvalidOperationException(
            $"{maxAttempts}個の候補を確認しましたが、配信サーバー用の空きポートが見つかりません。");
    }

    private static bool IsLoopbackPortAvailable(int port)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
