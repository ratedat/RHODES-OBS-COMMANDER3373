using System.Net;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesApiStatusProbe
{
    public static async Task<SukiOptionalRuntimeStatus> ProbeAsync(
        string baseUrl,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        var normalized = NormalizeBaseUrl(baseUrl);
        try
        {
            var response = await client.GetAsync($"{normalized}/api/health", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                return ParseHealthJson(json);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return await ProbeStateFallbackAsync(client, normalized, cancellationToken);

            return new SukiOptionalRuntimeStatus("RHODES API", "HTTPエラー", $"{(int)response.StatusCode} {Shorten(json, 180)}", false, false);
        }
        catch (Exception ex)
        {
            return new SukiOptionalRuntimeStatus("RHODES API", "接続失敗", Shorten(ex.Message, 180), false, false);
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static SukiOptionalRuntimeStatus ParseHealthJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return new SukiOptionalRuntimeStatus("RHODES API", "応答異常", "health応答がobjectではありません。", false, false);

        var ok = JsonBool(root, "ok");
        var version = JsonString(root, "version");
        var state = root.TryGetProperty("state", out var stateElement) && stateElement.ValueKind == JsonValueKind.Object
            ? stateElement
            : default;
        var recognition = root.TryGetProperty("recognition", out var recognitionElement) && recognitionElement.ValueKind == JsonValueKind.Object
            ? recognitionElement
            : default;
        var campaignId = state.ValueKind == JsonValueKind.Object ? JsonString(state, "campaignId") : "";
        var operators = state.ValueKind == JsonValueKind.Object ? JsonInt(state, "operators") : 0;
        var relics = state.ValueKind == JsonValueKind.Object ? JsonInt(state, "relics") : 0;
        var pending = state.ValueKind == JsonValueKind.Object ? JsonInt(state, "pendingSuggestions") : 0;
        var active = recognition.ValueKind == JsonValueKind.Object && JsonBool(recognition, "active");

        var detail = string.Join(" / ", new[]
        {
            string.IsNullOrWhiteSpace(version) ? "version=unknown" : $"version={version}",
            string.IsNullOrWhiteSpace(campaignId) ? "campaign=unknown" : $"campaign={campaignId}",
            $"operators={operators}",
            $"relics={relics}",
            $"suggestions={pending}",
            active ? "scan=running" : "scan=idle",
        });
        return new SukiOptionalRuntimeStatus("RHODES API", ok ? "接続済み" : "異常", detail, ok, false);
    }

    public static SukiOptionalRuntimeStatus ParseStateJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return new SukiOptionalRuntimeStatus("RHODES API", "応答異常", "state応答がobjectではありません。", false, false);

        var run = root.TryGetProperty("run", out var runElement) && runElement.ValueKind == JsonValueKind.Object
            ? runElement
            : default;
        var campaignId = run.ValueKind == JsonValueKind.Object ? JsonString(run, "campaignId") : "";
        var operators = JsonArrayCount(root, "operators");
        var relics = JsonArrayCount(root, "relics");
        var updatedAt = JsonString(root, "updatedAt");
        var detail = string.Join(" / ", new[]
        {
            "state取得済み",
            string.IsNullOrWhiteSpace(campaignId) ? "campaign=unknown" : $"campaign={campaignId}",
            $"operators={operators}",
            $"relics={relics}",
            string.IsNullOrWhiteSpace(updatedAt) ? "" : $"updated={updatedAt}",
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return new SukiOptionalRuntimeStatus("RHODES API", "接続済み", detail, true, false);
    }

    public static async Task<SukiOptionalRuntimeStatus> ProbeMasterAsync(
        string baseUrl,
        int localCampaigns,
        int localOperators,
        int localRelics,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        var normalized = NormalizeBaseUrl(baseUrl);
        try
        {
            var response = await client.GetAsync($"{normalized}/api/master", cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new SukiOptionalRuntimeStatus("Master Data", "HTTPエラー", $"{(int)response.StatusCode} {Shorten(json, 180)}", false, false);

            return ParseMasterJson(json, localCampaigns, localOperators, localRelics);
        }
        catch (Exception ex)
        {
            return new SukiOptionalRuntimeStatus("Master Data", "接続失敗", Shorten(ex.Message, 180), false, false);
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static SukiOptionalRuntimeStatus ParseMasterJson(
        string json,
        int localCampaigns,
        int localOperators,
        int localRelics)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return new SukiOptionalRuntimeStatus("Master Data", "応答異常", "master応答がobjectではありません。", false, false);

        var campaigns = JsonArrayCount(root, "campaigns");
        var operators = JsonArrayCount(root, "operators");
        var relics = JsonArrayCount(root, "relics");
        var matched = campaigns == localCampaigns && operators == localOperators && relics == localRelics;
        var detail = string.Join(" / ", new[]
        {
            $"campaigns api={campaigns} local={localCampaigns}",
            $"operators api={operators} local={localOperators}",
            $"relics api={relics} local={localRelics}",
        });
        return new SukiOptionalRuntimeStatus("Master Data", matched ? "一致" : "差分あり", detail, true, false);
    }

    private static async Task<SukiOptionalRuntimeStatus> ProbeStateFallbackAsync(
        HttpClient client,
        string normalizedBaseUrl,
        CancellationToken cancellationToken)
    {
        var response = await client.GetAsync($"{normalizedBaseUrl}/api/state", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new SukiOptionalRuntimeStatus("RHODES API", "HTTPエラー", $"{(int)response.StatusCode} {Shorten(json, 180)}", false, false);

        return ParseStateJson(json);
    }

    private static string NormalizeBaseUrl(string value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:5173" : value.Trim();
        return text.TrimEnd('/');
    }

    private static string JsonString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int JsonInt(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static bool JsonBool(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static int JsonArrayCount(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
