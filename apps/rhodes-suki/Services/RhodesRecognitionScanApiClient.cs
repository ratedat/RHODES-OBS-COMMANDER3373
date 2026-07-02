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

}
