using System.Net.Http.Json;
using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesMaaCandidateApiResult(
    IReadOnlyList<MaaCandidatePreview> Candidates,
    string Error)
{
    public bool HasCandidates => Candidates.Count > 0;
}

public static class RhodesMaaCandidateApiClient
{
    public static async Task<RhodesMaaCandidateApiResult> ConvertAsync(
        string baseUrl,
        string? profileId,
        IEnumerable<MaaTaskRunResult> taskResults,
        TimeSpan? timeout = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return new RhodesMaaCandidateApiResult([], "allプロファイルでは候補化APIをスキップしました。");

        var ownsClient = client is null;
        client ??= new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(5) };
        try
        {
            var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/api/recognition/maa-resource",
                new
                {
                    profile = profileId,
                    source = "maa-framework",
                    taskResults = taskResults.ToArray(),
                },
                cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RhodesMaaCandidateApiResult([], $"{(int)response.StatusCode} {Shorten(json, 160)}");

            return new RhodesMaaCandidateApiResult(ExtractCandidatePreviews(json), "");
        }
        catch (Exception ex)
        {
            return new RhodesMaaCandidateApiResult([], Shorten(ex.Message, 160));
        }
        finally
        {
            if (ownsClient)
                client.Dispose();
        }
    }

    public static IReadOnlyList<MaaCandidatePreview> ExtractCandidatePreviews(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("result", out var result)
            || !result.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var previews = new List<MaaCandidatePreview>();
        foreach (var candidate in candidates.EnumerateArray())
        {
            var kind = JsonString(candidate, "kind");
            var label = JsonString(candidate, "label");
            var rawText = JsonString(candidate, "rawText");
            var field = JsonString(candidate, "field");
            var name = JsonString(candidate, "name");
            var value = JsonValueText(candidate, "value");
            var confidence = JsonNumber(candidate, "confidence");
            previews.Add(new MaaCandidatePreview(
                kind,
                string.IsNullOrWhiteSpace(label) ? field : label,
                string.IsNullOrWhiteSpace(value) ? name : value,
                rawText,
                confidence,
                field,
                JsonString(candidate, "operatorId"),
                JsonString(candidate, "relicId"),
                JsonString(candidate, "campaignId"),
                JsonString(candidate, "recognitionKey"),
                JsonString(candidate, "thoughtId"),
                JsonString(candidate, "ageId")));
        }
        return previews;
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
    }

    private static string JsonValueText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return "";
        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => property.GetRawText(),
        };
    }

    private static double? JsonNumber(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out var value)
            ? value
            : null;
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
