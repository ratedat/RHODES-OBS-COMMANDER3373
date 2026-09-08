using System.Text.Json;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.Tests;

public static class NameCorrectionMasterTests
{
    public static void ResolvesCuratedNamesWithoutChangingRawText()
    {
        foreach (var (kind, profile, entry, raw, expected) in new[]
        {
            ("thought", "is5ThoughtFull", "RhodesOcrRegion_is5_thought_list_text", "啓豪", "is5_sarkaz_selectable_thought_insp_09"),
            ("thought", "is5ThoughtFull", "RhodesOcrRegion_is5_thought_list_text", "開墨", "is5_sarkaz_selectable_thought_insp_11"),
            ("thought", "is5ThoughtFull", "RhodesOcrRegion_is5_thought_list_text", "功各の石柱", "is5_sarkaz_selectable_thought_legacy_05"),
            ("thought", "is5ThoughtFull", "RhodesOcrRegion_is5_thought_list_text", "土を被つた食器", "is5_sarkaz_selectable_thought_legacy_11"),
            ("relic", "relicsFull", "RhodesOcrRegion_relic_list_text", "『帰還j", "is5_sarkaz_relic_126"),
            ("operator", "operatorsFull", "operator.card.name.0", "12-", "12f"),
        })
        {
            var candidate = RhodesMaaLocalCandidateConverter.FromTaskResults(profile, [Ocr(entry, raw)], "is5_sarkaz").Single();
            Check(candidate.Kind == kind && (candidate.ThoughtId == expected || candidate.RelicId == expected || candidate.OperatorId == expected), $"correction: {raw}");
            Check(candidate.RawText == raw, "raw OCR survives ID correction");
        }
    }

    public static void PreservesFormalOperatorIdentityIncludingSingleCharacterNames()
    {
        foreach (var op in RhodesRecognitionCatalogCache.Load().Operators)
        {
            if (RhodesMaaAmiyaRoleResolver.IsLiteralAmiyaText(op.Name)) continue;
            var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults("operatorsFull", [Ocr("operator.card.name.0", op.Name)]);
            Check(string.Join("|", candidates.Select(item => item.OperatorId)) == op.Id, $"formal operator {op.Name}: expected {op.Id}");
        }
        foreach (var (raw, id) in new[] { ("W", "w"), ("w", "w"), ("ア", "aak") })
            Check(RhodesMaaLocalCandidateConverter.FromTaskResults("operatorsFull", [Ocr("operator.card.name.0", raw)]).Single().OperatorId == id,
                "single character exact name");
        foreach (var raw in new[] { "攻撃 W 上昇", "効果 ア 上昇", "Wの効果", "アの効果", "Z" })
            Check(!RhodesMaaLocalCandidateConverter.FromTaskResults("operatorsFull", [Ocr("operator.card.name.0", raw)])
                .Any(item => item.OperatorId is "w" or "aak"), "single characters in prose are not names");
    }

    public static void RejectsInvalidTargetsAndAliasConflicts()
    {
        var targets = new[]
        {
            new RhodesOcrNameTarget("thought", "is5_sarkaz", "first", "啓蒙"),
            new RhodesOcrNameTarget("thought", "is5_sarkaz", "second", "開墾"),
        };
        var catalog = RhodesOcrNameCorrections.FromJson(
            """
            {"schemaVersion":1,"normalization":"nfkc-compact-quotes-lower","rules":[
                {"id":"first","kind":"thought","campaignId":"is5_sarkaz","targetId":"first","canonicalName":"啓蒙","aliases":["啓豪","競合","開墾"]},
                {"id":"second","kind":"thought","campaignId":"is5_sarkaz","targetId":"second","canonicalName":"開墾","aliases":["競合"]},
                {"id":"missing","kind":"thought","campaignId":"is5_sarkaz","targetId":"unknown","canonicalName":"不存在","aliases":["未登録"]}
            ]}
            """, targets);
        Check(catalog.Resolve("thought", "is5_sarkaz", " 「啓 豪」 ")?.TargetId == "first", "normalization matches the declared contract");
        Check(catalog.Resolve("thought", "is5_sarkaz", "競合") is null, "conflicting alias does not pick the first rule");
        Check(catalog.Resolve("thought", "is5_sarkaz", "開墾") is null, "another formal name cannot be reassigned");
        Check(catalog.Resolve("thought", "is5_sarkaz", "未登録") is null, "unknown target cannot enter the dictionary");
        Check(catalog.Resolve("thought", "is4_sami", "啓豪") is null, "campaign scope");
        Check(catalog.Resolve("relic", "is5_sarkaz", "啓豪") is null, "kind scope");
        Check(catalog.Resolve("thought", "is5_sarkaz", "啓豪を所持") is null, "whole alias only");
        foreach (var raw in new[] { "帰還jを", "帰還j を所持" })
        {
            Check(RhodesMaaLocalCandidateConverter.FromTaskResults("relicsFull",
                [Ocr("RhodesOcrRegion_relic_list_text", raw)], "is5_sarkaz").Count == 0,
                "an alias in prose cannot fall through to relic fuzzy matching");
            Check(RhodesMaaLocalCandidateConverter.ResolveRelicName(raw, "is5_sarkaz") is null,
                "standalone relic resolver uses the same prose boundary");
        }
        Check(RhodesMaaLocalCandidateConverter.ResolveRelicName("帰還j", "is5_sarkaz")?.Id == "is5_sarkaz_relic_126",
            "standalone relic resolver uses the shared master");
        Check(RhodesOperatorOcrNormalizer.Normalize("フメイー") != RhodesOperatorOcrNormalizer.Normalize("メイ"),
            "master normalization does not remove an undeclared trailing character");
        foreach (var raw in new[] { "フメイー", "12-ー", "フメイ 補足" })
            Check(RhodesMaaLocalCandidateConverter.FromTaskResults("operatorsFull", [Ocr("operator.card.name.0", raw)]).Count == 0,
                "operator aliases require the complete row");
    }

    public static void CorrectsThoughtRowsBeforeAdditionalOcr()
    {
        var source = Ocr("RhodesOcrRegion_is5_thought_list_text", "啓豪");
        var calls = 0;
        var result = RhodesMaaThoughtNameOcrExpander.RefineAsync(source, (request, _) =>
        {
            calls++;
            return Task.FromResult(Ocr(request.Entry, "未確定"));
        }).GetAwaiter().GetResult();
        Check(calls == 0 && result.Attempts.Count == 0, "known corrections avoid extra OCR");
        using var detail = JsonDocument.Parse(result.Frame.RecognitionDetailJson);
        var row = detail.RootElement.GetProperty("filtered")[0];
        Check(row.GetProperty("text").GetString() == "啓蒙", "corrected row reaches downstream readers");
        Check(row.GetProperty("rhodes_name_correction").GetProperty("original_text").GetString() == "啓豪", "master correction retains raw evidence");
        Check(RhodesMaaLocalCandidateConverter.FromTaskResults("is5ThoughtFull", [result.Frame]).Single().RawText == "啓豪",
            "the final candidate also exposes the original OCR text");
        var request = RhodesMaaThoughtLoadOcrExpander.BuildRequests(result.Frame.RecognitionDetailJson).Single();
        Check(request.Entry.EndsWith("insp_09", StringComparison.Ordinal) && request.X == 445 && request.Y == 200,
            "corrected thought load uses the original card coordinates");
    }

    private static MaaTaskRunResult Ocr(string entry, string text) => new(entry, "Succeeded", true, "synthetic OCR",
        JsonSerializer.Serialize(new { filtered = new[] { new { text, score = 0.91, box = new[] { 500, 140, 140, 20 } } } }), "OCR", true);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
