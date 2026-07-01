using System.Net.Http.Json;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionScanApiResult(
    string ProfileId,
    string Status,
    string LogPath,
    IReadOnlyList<MaaCandidatePreview> Candidates,
    string Error)
{
    public bool HasCandidates => Candidates.Count > 0;

    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public static class RhodesRecognitionScanApiClient
{
    public static async Task<RhodesRecognitionScanApiResult> RunAsync(
        string baseUrl,
        string? profileId,
        string source = "adb",
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return new RhodesRecognitionScanApiResult("", "", "", [], "allプロファイルではADBスキャンAPIを実行できません。");

        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromMinutes(5) };
        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/api/recognition/scan",
                new
                {
                    profile = profileId,
                    source,
                },
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesRecognitionScanApiResult(profileId, "", "", [], $"{(int)response.StatusCode} {Shorten(json, 180)}");

            return ExtractResult(json);
        }
        catch (Exception ex)
        {
            return new RhodesRecognitionScanApiResult(profileId, "", "", [], Shorten(ex.Message, 180));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static RhodesRecognitionScanApiResult ExtractResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
            return new RhodesRecognitionScanApiResult("", "", "", [], "resultがありません。");

        var profileId = JsonString(result, "profileId");
        var status = JsonString(result, "status");
        var logPath = JsonString(result, "logPath");
        var candidates = RhodesMaaCandidateApiClient.ExtractCandidatePreviews(json);
        return new RhodesRecognitionScanApiResult(profileId, status, logPath, candidates, "");
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
