using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesHypervisorProbe
{
    private static readonly Uri HypervisorStatusPath = new("/api/system/hypervisor", UriKind.Relative);

    public static async Task<SukiHypervisorStatus> ProbeAsync(string apiUrl, HttpClient? client = null)
    {
        var ownsClient = client is null;
        client ??= new HttpClient();
        try
        {
            client.BaseAddress = NormalizeBaseUri(apiUrl);
            var json = await client.GetStringAsync(HypervisorStatusPath);
            return ParseStatusJson(json);
        }
        catch (Exception ex)
        {
            return new SukiHypervisorStatus("確認失敗", ex.Message, false, false, "warning");
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static SukiHypervisorStatus ParseStatusJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var supported = JsonBool(root, "supported", true);
        var available = JsonBool(root, "available");
        var requiresBiosChange = JsonBool(root, "requiresBiosChange");
        var severity = JsonString(root, "severity");
        var message = JsonString(root, "message");

        if (!supported)
        {
            return new SukiHypervisorStatus(
                "非対応",
                string.IsNullOrWhiteSpace(message) ? "Hyper-V診断はWindows環境でのみ確認できます。" : message,
                false,
                false,
                FirstNonEmpty(severity, "info"));
        }

        if (available)
        {
            return new SukiHypervisorStatus(
                "有効",
                string.IsNullOrWhiteSpace(message) ? "Hyper-V/Windows Hypervisorは有効です。" : message,
                true,
                false,
                FirstNonEmpty(severity, "ok"));
        }

        if (requiresBiosChange)
        {
            return new SukiHypervisorStatus(
                "BIOS要確認",
                string.IsNullOrWhiteSpace(message) ? "BIOS/UEFIでIntel VT-xまたはAMD-V/SVMを有効にしてください。" : message,
                false,
                true,
                FirstNonEmpty(severity, "error"));
        }

        return new SukiHypervisorStatus(
            string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase) ? "要修正" : "Windows機能要確認",
            string.IsNullOrWhiteSpace(message)
                ? "Windowsの機能でHyper-V、仮想マシンプラットフォーム、Windows Hypervisor Platformを有効化してください。"
                : message,
            false,
            false,
            FirstNonEmpty(severity, "warning"));
    }

    private static Uri NormalizeBaseUri(string apiUrl)
    {
        var value = string.IsNullOrWhiteSpace(apiUrl) ? "http://127.0.0.1:5173" : apiUrl.TrimEnd('/');
        return Uri.TryCreate($"{value}/", UriKind.Absolute, out var uri)
            ? uri
            : new Uri("http://127.0.0.1:5173/");
    }

    private static string JsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static bool JsonBool(JsonElement root, string propertyName, bool defaultValue = false)
    {
        if (!root.TryGetProperty(propertyName, out var value))
            return defaultValue;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) ? parsed : defaultValue,
            _ => defaultValue,
        };
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    }
}
