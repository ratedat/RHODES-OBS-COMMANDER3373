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

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
