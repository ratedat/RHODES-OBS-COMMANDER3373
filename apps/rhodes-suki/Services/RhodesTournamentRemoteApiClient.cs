using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhodesSuki.Services;

public sealed class RhodesTimestampJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? "";
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return "";
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var milliseconds))
        {
            try
            {
                return DateTimeOffset
                    .FromUnixTimeMilliseconds(milliseconds)
                    .ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new JsonException("Remote expiry timestamp is outside the supported range.", exception);
            }
        }

        throw new JsonException("Remote expiry timestamp must be an ISO string or Unix milliseconds.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

public sealed record RhodesTournamentRemoteStatus(
    bool Active = false,
    string RelayUrl = "",
    string SessionId = "",
    string EditorCode = "",
    string InputUrl = "",
    string PlayerLabel = "",
    [property: JsonConverter(typeof(RhodesTimestampJsonConverter))] string ExpiresAt = "",
    long Cursor = 0,
    string StartedAt = "",
    string LastSyncedAt = "",
    string LastOperationAt = "",
    string LastError = "");

public sealed record RhodesTournamentRemoteResult(
    RhodesTournamentRemoteStatus Status,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public sealed record RhodesTournamentQuickPublishStatus(
    bool Installed = false,
    string Version = "",
    string RuntimePath = "",
    bool Active = false,
    bool Starting = false,
    string PublicUrl = "",
    string LocalRelayUrl = "",
    string LastError = "",
    string Stage = "",
    string Diagnostic = "",
    RhodesTournamentRemoteStatus? Remote = null);

public sealed record RhodesTournamentQuickPublishResult(
    RhodesTournamentQuickPublishStatus Status,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public static class RhodesTournamentRemoteApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Task<RhodesTournamentRemoteResult> StatusAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Get, baseUrl, "/api/tournament/remote/status", null, client, cancellationToken);
    }

    public static Task<RhodesTournamentRemoteResult> StartAsync(
        string baseUrl,
        string relayUrl,
        string playerLabel,
        string adminToken,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            relayUrl = relayUrl.Trim(),
            playerLabel = string.IsNullOrWhiteSpace(playerLabel) ? "Player" : playerLabel.Trim(),
            adminToken,
        };
        return SendAsync(HttpMethod.Post, baseUrl, "/api/tournament/remote/start", payload, client, cancellationToken);
    }

    public static Task<RhodesTournamentRemoteResult> SyncAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Post, baseUrl, "/api/tournament/remote/sync", new { }, client, cancellationToken);
    }

    public static Task<RhodesTournamentRemoteResult> StopAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Post, baseUrl, "/api/tournament/remote/stop", new { }, client, cancellationToken);
    }

    public static Task<RhodesTournamentQuickPublishResult> QuickStatusAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendQuickAsync(HttpMethod.Get, baseUrl, "/api/tournament/quick/status", null, client, cancellationToken);
    }

    public static Task<RhodesTournamentQuickPublishResult> QuickInstallAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendQuickAsync(HttpMethod.Post, baseUrl, "/api/tournament/quick/install", new { }, client, cancellationToken);
    }

    public static Task<RhodesTournamentQuickPublishResult> QuickStartAsync(
        string baseUrl,
        string playerLabel,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendQuickAsync(
            HttpMethod.Post,
            baseUrl,
            "/api/tournament/quick/start",
            new
            {
                playerLabel = string.IsNullOrWhiteSpace(playerLabel) ? "Player" : playerLabel.Trim(),
            },
            client,
            cancellationToken);
    }

    public static Task<RhodesTournamentQuickPublishResult> QuickStopAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendQuickAsync(HttpMethod.Post, baseUrl, "/api/tournament/quick/stop", new { }, client, cancellationToken);
    }

    public static Task<RhodesTournamentQuickPublishResult> QuickUninstallAsync(
        string baseUrl,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        return SendQuickAsync(HttpMethod.Post, baseUrl, "/api/tournament/quick/uninstall", new { }, client, cancellationToken);
    }

    public static RhodesTournamentRemoteStatus ParseStatusJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("status", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            root = nested;
        }

        return JsonSerializer.Deserialize<RhodesTournamentRemoteStatus>(root.GetRawText(), JsonOptions)
            ?? new RhodesTournamentRemoteStatus();
    }

    public static RhodesTournamentQuickPublishStatus ParseQuickStatusJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("status", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
        {
            root = nested;
        }

        return JsonSerializer.Deserialize<RhodesTournamentQuickPublishStatus>(root.GetRawText(), JsonOptions)
            ?? new RhodesTournamentQuickPublishStatus();
    }

    private static async Task<RhodesTournamentRemoteResult> SendAsync(
        HttpMethod method,
        string baseUrl,
        string path,
        object? payload,
        HttpClient? client,
        CancellationToken cancellationToken)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        try
        {
            using var request = new HttpRequestMessage(method, $"{normalizedBaseUrl}{path}");
            if (payload is not null)
                request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new RhodesTournamentRemoteResult(
                    new RhodesTournamentRemoteStatus(),
                    $"HTTP {(int)response.StatusCode}: {ReadError(json)}");
            }

            return new RhodesTournamentRemoteResult(ParseStatusJson(json), "");
        }
        catch (Exception ex)
        {
            return new RhodesTournamentRemoteResult(
                new RhodesTournamentRemoteStatus(),
                ex.Message);
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    private static async Task<RhodesTournamentQuickPublishResult> SendQuickAsync(
        HttpMethod method,
        string baseUrl,
        string path,
        object? payload,
        HttpClient? client,
        CancellationToken cancellationToken)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        try
        {
            using var request = new HttpRequestMessage(method, $"{normalizedBaseUrl}{path}");
            if (payload is not null)
                request.Content = JsonContent.Create(payload);

            using var response = await client.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new RhodesTournamentQuickPublishResult(
                    new RhodesTournamentQuickPublishStatus(),
                    $"HTTP {(int)response.StatusCode}: {ReadError(json)}");
            }

            return new RhodesTournamentQuickPublishResult(ParseQuickStatusJson(json), "");
        }
        catch (Exception ex)
        {
            return new RhodesTournamentQuickPublishResult(
                new RhodesTournamentQuickPublishStatus(),
                ex.Message);
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    private static string NormalizeBaseUrl(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:5173" : value.Trim();
        return text.TrimEnd('/');
    }

    private static string ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() ?? "remote input request failed";
            }
        }
        catch
        {
            // Fall through to a compact raw response.
        }

        var text = string.IsNullOrWhiteSpace(json) ? "remote input request failed" : json.Trim().ReplaceLineEndings(" ");
        return text.Length <= 200 ? text : $"{text[..200]}...";
    }
}
