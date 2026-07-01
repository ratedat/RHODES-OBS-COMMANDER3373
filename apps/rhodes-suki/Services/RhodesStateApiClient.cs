using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesStateApiResult(
    string StateJson,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public static class RhodesStateApiClient
{
    public static async Task<RhodesStateApiResult> FetchAsync(
        string baseUrl,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        try
        {
            var response = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/state", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesStateApiResult("", $"{(int)response.StatusCode} {Shorten(json, 180)}");

            return new RhodesStateApiResult(json, "");
        }
        catch (Exception ex)
        {
            return new RhodesStateApiResult("", Shorten(ex.Message, 180));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static async Task<RhodesStateApiResult> SaveAsync(
        string baseUrl,
        string stateJson,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        try
        {
            using var content = new StringContent(stateJson, Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{baseUrl.TrimEnd('/')}/api/state", content, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesStateApiResult("", $"{(int)response.StatusCode} {Shorten(json, 180)}");

            return new RhodesStateApiResult(json, "");
        }
        catch (Exception ex)
        {
            return new RhodesStateApiResult("", Shorten(ex.Message, 180));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static string ApplyAdbSettingsToStateJson(string stateJson, RhodesAdbApiSettings settings)
    {
        var root = JsonNode.Parse(string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson)?.AsObject() ?? [];
        var adb = root["adb"] as JsonObject;
        if (adb is null)
        {
            adb = [];
            root["adb"] = adb;
        }
        adb["autoDetect"] = settings.AutoDetect;
        adb["connectionPreset"] = string.IsNullOrWhiteSpace(settings.ConnectionPreset) ? "auto" : settings.ConnectionPreset;
        adb["adbPath"] = settings.AdbPath ?? "";
        adb["serial"] = settings.Serial ?? "";
        adb["restartServerOnFailure"] = true;
        adb["restartProcessOnFailure"] = true;
        adb["reconnectAttempts"] = 5;
        adb["reconnectDelayMs"] = 1000;
        root["updatedAt"] = DateTimeOffset.UtcNow.ToString("O");
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
