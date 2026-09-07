using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.Tests;

public static class RelicStackRecognitionTests
{
    private const string RelicId = "is5_sarkaz_relic_204";

    public static void RejectsMalformedStackCountNoise()
    {
        // Artificial malformed text must never establish a numeric stack count.
        var results = new[]
        {
            NameResult(),
            StackResult("★g"),
            StackResult("Q", "…3"),
            StackResult("xg"),
            StackResult("~#"),
        };
        var candidate = Convert(results);
        Equal(0, candidate.Count, "malformed glyphs do not establish a stack count");

        var state = JsonNode.Parse("""
            { "run": { "campaignId": "is5_sarkaz" },
              "relics": ["is5_sarkaz_relic_204"],
              "relicStackCounts": { "is5_sarkaz_relic_204": 1 } }
            """)!.AsObject();
        RhodesRecognitionCandidateApplier.Apply(state, [candidate], DateTimeOffset.UnixEpoch);
        Equal(1, state["relicStackCounts"]![RelicId]!.GetValue<int>(),
            "unreadable stack OCR preserves the user's confirmed manual value");

        var latest = Convert([NameResult(), StackResult("?"), StackResult("-*"), StackResult("?"), StackResult(".")]);
        Equal(0, latest.Count, "unreadable results stay unknown, not an inferred one");

        var progressed = Convert([NameResult(), StackResult("3-"), StackResult("★!"), StackResult("3-"), StackResult("3-")]);
        Equal(0, progressed.Count, "three agreeing malformed numbers cannot establish the confirmed one stack");
        RhodesRecognitionCandidateApplier.Apply(state, [progressed], DateTimeOffset.UnixEpoch);
        Equal(1, state["relicStackCounts"]![RelicId]!.GetValue<int>(),
            "repeated high-confidence malformed OCR cannot overwrite the manual one");
    }

    public static void RejectsAmbiguousNumbersBeforeLimits()
    {
        foreach (var rows in new[]
        {
            new[] { "…2" }, new[] { "4 14" }, new[] { "4 4" }, new[] { "4/10" },
            new[] { "4.0" }, new[] { "value4" }, new[] { "4", "14" },
            new[] { "4", "9" }, new[] { "4", "0" }, new[] { "4", "2147483648" },
        })
        {
            Equal(0, Convert([NameResult(), StackResult(rows)]).Count,
                $"ambiguous OCR is rejected before filtering limits: {string.Join("|", rows)}");
        }
    }

    public static void KeepsWholeBadgeCountsAndOneVotePerCapture()
    {
        foreach (var text in new[] { "1", "×1", "x1", " 1 ", "^1", "-1" })
            Equal(1, Convert([NameResult(), StackResult(text)]).Count, $"whole badge format {text}");

        Equal(0, Convert([NameResult(), StackResult("4", "4"), StackResult("9")]).Count,
            "duplicate OCR rows in one capture cannot outweigh a conflicting capture");
        Equal(4, Convert([NameResult(), StackResult("4"), StackResult("×4"), StackResult("9")]).Count,
            "independent agreeing captures retain the existing majority behavior");
    }

    private static MaaCandidatePreview Convert(IEnumerable<MaaTaskRunResult> results) =>
        RhodesMaaLocalCandidateConverter.FromTaskResults("relicsFull", results, "is5_sarkaz")
            .Single(candidate => candidate.RelicId == RelicId);

    private static MaaTaskRunResult NameResult() => new(
        "RhodesOcrRegion_relic_list_text", "Succeeded", true, "",
        """{"filtered":[{"text":"「知識の橋」","score":0.99,"box":[966,421,93,21]}]}""",
        "OCR", true);

    private static MaaTaskRunResult StackResult(params string[] texts) => new(
        $"relic.stack.{RelicId}", "Succeeded", true, "",
        JsonSerializer.Serialize(new { filtered = texts.Select(text => new { text, score = 0.99 }) }),
        "OCR", true);

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}
