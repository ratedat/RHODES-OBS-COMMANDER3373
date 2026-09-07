namespace RhodesSuki.Services;

public sealed record RhodesRecognitionPassSelection(
    IReadOnlyList<RhodesRecognitionScrollPass> Passes,
    bool UsedRememberedEndpoint,
    string RememberedEndpointDirection,
    string Reason);

public sealed class RhodesRecognitionEndpointCache
{
    private const int EndpointFingerprintDistance = 2;
    private readonly Dictionary<EndpointCacheKey, Dictionary<string, ulong>> _endpoints = [];

    public RhodesRecognitionPassSelection Select(
        string profileId,
        string campaignId,
        string connectionKind,
        int frameWidth,
        int frameHeight,
        ulong initialFingerprint,
        IReadOnlyList<RhodesRecognitionScrollPass> passes)
    {
        if (!SupportsRememberedEndpoint(profileId) || passes.Count < 2)
            return FullScan(passes, "profile-not-eligible");

        var key = CreateKey(profileId, campaignId, connectionKind, frameWidth, frameHeight);
        if (!_endpoints.TryGetValue(key, out var endpoints))
            return FullScan(passes, "endpoint-unknown");

        var matches = endpoints
            .Select(item => new
            {
                Direction = item.Key,
                Distance = RhodesRecognitionFrameFingerprint.Distance(initialFingerprint, item.Value),
            })
            .Where(item => item.Distance <= EndpointFingerprintDistance)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Direction, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
            return FullScan(passes, "viewport-not-matched");
        if (matches.Length != 1)
            return FullScan(passes, "ambiguous-endpoint");

        var matched = matches[0];

        var preferredDirection = OppositeDirection(matched.Direction);
        var preferredIndex = -1;
        for (var index = 0; index < passes.Count; index++)
        {
            if (!passes[index].Direction.Equals(preferredDirection, StringComparison.OrdinalIgnoreCase))
                continue;
            preferredIndex = index;
            break;
        }
        if (preferredIndex < 0)
            return FullScan(passes, "opposite-pass-missing");

        var preferred = passes[preferredIndex];
        if (profileId.Equals("is5ThoughtFull", StringComparison.Ordinal))
        {
            preferred = preferred with
            {
                CollectCandidates = true,
                MirrorPreviousPassScrolls = false,
            };
        }

        var reordered = new List<RhodesRecognitionScrollPass>(passes.Count) { preferred };
        for (var index = 0; index < passes.Count; index++)
        {
            if (index != preferredIndex)
                reordered.Add(passes[index]);
        }

        return new RhodesRecognitionPassSelection(
            reordered,
            true,
            matched.Direction,
            $"remembered-{matched.Direction}-distance-{matched.Distance}");
    }

    public void RecordEndpoint(
        string profileId,
        string campaignId,
        string connectionKind,
        int frameWidth,
        int frameHeight,
        string endpointDirection,
        ulong fingerprint)
    {
        if (!SupportsRememberedEndpoint(profileId)
            || string.IsNullOrWhiteSpace(endpointDirection)
            || string.IsNullOrWhiteSpace(OppositeDirection(endpointDirection)))
        {
            return;
        }

        var key = CreateKey(profileId, campaignId, connectionKind, frameWidth, frameHeight);
        if (!_endpoints.TryGetValue(key, out var endpoints))
        {
            endpoints = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            _endpoints[key] = endpoints;
        }

        endpoints[endpointDirection.Trim().ToLowerInvariant()] = fingerprint;
    }

    public void Clear() => _endpoints.Clear();

    private static RhodesRecognitionPassSelection FullScan(
        IReadOnlyList<RhodesRecognitionScrollPass> passes,
        string reason) =>
        new(passes.ToArray(), false, "", reason);

    private static bool SupportsRememberedEndpoint(string profileId) =>
        profileId is "operatorsFull" or "relicsFull" or "is5ThoughtFull";

    private static string OppositeDirection(string direction) => direction.Trim().ToLowerInvariant() switch
    {
        "left" => "right",
        "right" => "left",
        "up" => "down",
        "down" => "up",
        _ => "",
    };

    private static EndpointCacheKey CreateKey(
        string profileId,
        string campaignId,
        string connectionKind,
        int frameWidth,
        int frameHeight) =>
        new(
            profileId.Trim().ToLowerInvariant(),
            campaignId.Trim().ToLowerInvariant(),
            connectionKind.Trim().ToLowerInvariant(),
            Math.Max(0, frameWidth),
            Math.Max(0, frameHeight));

    private readonly record struct EndpointCacheKey(
        string ProfileId,
        string CampaignId,
        string ConnectionKind,
        int FrameWidth,
        int FrameHeight);
}
