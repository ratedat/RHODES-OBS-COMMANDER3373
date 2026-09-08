using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

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

    public static void PlansEveryEligibleCardAfterMarkerValidation()
    {
        using var bitmap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Black);
        DrawStackBadge(bitmap, 952, 400, [1]);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

        var twoCards = new MaaTaskRunResult(
            "RhodesOcrRegion_relic_list_text", "Succeeded", true, "",
            """
            {"filtered":[
                {"text":"「知識の橋」","score":0.99,"box":[952,248,104,23]},
                {"text":"「知識の橋」","score":0.98,"box":[952,400,104,23]}
            ]}
            """,
            "OCR", true);

        var requests = RhodesRelicStackOcrPlanner.BuildRequests([twoCards], encoded.ToArray(), "is5_sarkaz");
        Equal(1, requests.Count, "a missing marker on the first card cannot suppress a later eligible card");
        Equal(true, requests[0].Y > 460, "the request remains attached to the later card");
        Equal(5, requests[0].Width, "a narrow one keeps the minimum source width needed by OCR");

        DrawStackBadge(bitmap, 952, 248, [5]);
        using var bothImage = SKImage.FromBitmap(bitmap);
        using var bothEncoded = bothImage.Encode(SKEncodedImageFormat.Png, 100);
        requests = RhodesRelicStackOcrPlanner.BuildRequests([twoCards], bothEncoded.ToArray(), "is5_sarkaz");
        Equal(2, requests.Count, "two distinct visible cards of one relic remain independently measurable");
        Equal(2, requests.Select(request => request.Entry).Distinct(StringComparer.Ordinal).Count(),
            "per-card requests carry distinct diagnostic identities");
    }

    public static void SeparatesDigitsFromArrowAndCardArtwork()
    {
        using var bitmap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(24, 26, 30));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.DrawRect(new SKRect(480, 430, 527, 480), new SKPaint { Color = new SKColor(245, 194, 74) });
            canvas.DrawRect(new SKRect(574, 446, 586, 474), new SKPaint { Color = SKColors.White });
        }
        DrawStackBadge(bitmap, 579, 380, [4, 14]);
        var title = NameRows(("「知識の橋」", 579, 380));
        var requests = RhodesRelicStackOcrPlanner.BuildRequests([title], Encode(bitmap), "is5_sarkaz");
        Equal(1, requests.Count, "the common arrow and its right-side digits establish one badge");
        var request = requests[0];
        Equal(true, request.X <= 532 && request.X + request.Width >= 553,
            "the shifted title keeps both digits of the measured 10");
        Equal(true, request.X > 525,
            "the arrow and left-side artwork are excluded from the OCR image");
        Equal(true, request.X + request.Width < 574,
            "unrelated right-side artwork is excluded from the OCR image");
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            request.Entry, out _, out var primaryCapture, out var primaryCard, out var primaryVariant),
            "the primary stack entry is parseable");
        Equal("gray", primaryVariant, "the antialiased neutral image is the primary recognition path");
        var binaryFallback = RhodesRelicStackOcrPlanner.BuildFallbackRequest(request);
        Equal(true, binaryFallback is not null, "the primary request has one deterministic binary fallback");
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            binaryFallback!.Entry, out _, out var fallbackCapture, out var fallbackCard, out var fallbackVariant),
            "the fallback stack entry is parseable");
        Equal(primaryCapture, fallbackCapture, "fallback stays in the same capture");
        Equal(primaryCard, fallbackCard, "fallback stays attached to the same card");
        Equal("binary", fallbackVariant, "fallback uses the strict bright-pixel mask");
        var grayRescue = RhodesRelicStackOcrPlanner.BuildFallbackRequest(binaryFallback);
        Equal(true, grayRescue is not null, "two unreadable paths have one bounded gray rescue");
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            grayRescue!.Entry, out _, out var rescueCapture, out var rescueCard, out var rescueVariant),
            "the rescue stack entry is parseable");
        Equal(primaryCapture, rescueCapture, "rescue stays in the same capture");
        Equal(primaryCard, rescueCard, "rescue stays attached to the same card");
        Equal("gray0", rescueVariant, "rescue keeps gray luminance without expanding above the ROI");
        var finalRescue = RhodesRelicStackOcrPlanner.BuildFallbackRequest(grayRescue);
        Equal(true, finalRescue is not null, "three unreadable paths have one geometry-selected final rescue");
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            finalRescue!.Entry, out _, out var finalCapture, out var finalCard, out var finalVariant),
            "the final rescue stack entry is parseable");
        Equal(primaryCapture, finalCapture, "final rescue stays in the same capture");
        Equal(primaryCard, finalCard, "final rescue stays attached to the same card");
        Equal("gray0-pad2", finalVariant, "a wide token keeps luminance and adds vertical black padding");
        Equal<MaaDynamicOcrRequest?>(null, RhodesRelicStackOcrPlanner.BuildFallbackRequest(finalRescue),
            "the fourth path cannot recursively create another retry");

        var narrowGray = grayRescue with { Width = 5, Height = 15 };
        var narrowFinal = RhodesRelicStackOcrPlanner.BuildFallbackRequest(narrowGray);
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            narrowFinal!.Entry, out _, out _, out _, out var narrowVariant),
            "the narrow final rescue stack entry is parseable");
        Equal("binary-pad2", narrowVariant, "a narrow token uses the strict mask with vertical black padding");
        var squareFinal = RhodesRelicStackOcrPlanner.BuildFallbackRequest(
            grayRescue with { Width = 15, Height = 15 });
        Equal(true, RhodesRelicStackOcrPlanner.TryParseEntry(
            squareFinal!.Entry, out _, out _, out _, out var squareVariant),
            "the square final rescue stack entry is parseable");
        Equal("gray0-pad2", squareVariant, "equal width and height use the luminance-preserving boundary");

        var grayPrimary = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(bitmap), "OCR", request.PayloadJson, request.Scale, request.Entry);
        using var grayPrimaryImage = SKBitmap.Decode(grayPrimary.EncodedImage);
        Equal((request.Height + 1) * request.Scale, grayPrimaryImage.Height,
            "the gray primary keeps one source row above the isolated digits");
        Equal(request.Width * request.Scale + 2 * grayPrimaryImage.Height, grayPrimaryImage.Width,
            "the gray primary margins track its expanded height");

        var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(bitmap), "OCR", binaryFallback.PayloadJson, binaryFallback.Scale, binaryFallback.Entry);
        using var separated = SKBitmap.Decode(prepared.EncodedImage);
        Equal(binaryFallback.Width * binaryFallback.Scale + 2 * binaryFallback.Height * binaryFallback.Scale, separated.Width,
            "the isolated digit crop has horizontal recognition margins");
        Equal(binaryFallback.Height * binaryFallback.Scale, separated.Height, "the isolated digit crop is scaled");
        Equal(SKColors.Black, separated.GetPixel(0, separated.Height / 2), "the recognition margin is black");
        Equal(true, separated.Pixels.Any(pixel => pixel == SKColors.White), "digit strokes remain visible");
        Equal(true, separated.Pixels.All(pixel => pixel == SKColors.Black || pixel == SKColors.White),
            "the stack OCR image contains only separated foreground and background");

        using var threeDigit = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        threeDigit.Erase(SKColors.Black);
        DrawStackBadge(threeDigit, 952, 248, [4, 10, 10]);
        requests = RhodesRelicStackOcrPlanner.BuildRequests(
            [NameRows(("生還者の契約", 952, 248))], Encode(threeDigit), "is5_sarkaz");
        Equal(1, requests.Count, "a three-digit stack remains eligible");
        Equal(true, requests[0].Width >= 32, "all three digit components remain in one OCR crop");

        using var wideDigitGap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        wideDigitGap.Erase(SKColors.Black);
        DrawStackBadge(wideDigitGap, 952, 248, []);
        DrawDigitBlocks(wideDigitGap, 906, 318, [4, 10], 10);
        requests = RhodesRelicStackOcrPlanner.BuildRequests(
            [NameRows(("Friston.P", 952, 248))], Encode(wideDigitGap), "is5_sarkaz");
        Equal(1, requests.Count, "a measured eleven-pixel column gap remains one displayed number");
        Equal(true, requests[0].X + requests[0].Width >= 931,
            "the second digit after the wider gap is retained");

        using var misleadingDigit = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        misleadingDigit.Erase(SKColors.Black);
        DrawStackBadge(misleadingDigit, 952, 248, []);
        DrawSparseDigitChevron(misleadingDigit, 906, 315);
        DrawDigitBlocks(misleadingDigit, 923, 315, [5]);
        requests = RhodesRelicStackOcrPlanner.BuildRequests(
            [NameRows(("「知識の橋」", 952, 248))], Encode(misleadingDigit), "is5_sarkaz");
        Equal(1, requests.Count, "a sparse digit shaped like a chevron is not mistaken for the dense badge marker");

        using var noArrow = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Premul);
        noArrow.Erase(SKColors.Black);
        DrawDigitBlocks(noArrow, 904, 317, [5, 10]);
        using (var canvas = new SKCanvas(noArrow))
            canvas.DrawRect(new SKRect(880, 310, 899, 334), new SKPaint { Color = new SKColor(235, 190, 70) });
        Equal(0, RhodesRelicStackOcrPlanner.BuildRequests(
                [NameRows(("「知識の橋」", 952, 248))], Encode(noArrow), "is5_sarkaz").Count,
            "digits or bright artwork without the common arrow do not invent a displayed stack");

        using var graySource = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        graySource.Erase(new SKColor(24, 26, 30));
        graySource.SetPixel(1, 1, new SKColor(210, 210, 210));
        var grayPrepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(graySource),
            "OCR",
            """{"roi":[0,0,4,4],"only_rec":true}""",
            2,
            $"relic.stack.{RelicId}.capture.gray.card.left.variant.gray");
        using var gray = SKBitmap.Decode(grayPrepared.EncodedImage);
        Equal(new SKColor(210, 210, 210), gray.GetPixel(10, 2),
            "the fallback image keeps neutral antialiasing luminance");
        var grayZeroPrepared = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(graySource),
            "OCR",
            """{"roi":[0,0,4,4],"only_rec":true}""",
            2,
            $"relic.stack.{RelicId}.capture.gray.card.left.variant.gray0");
        using var grayZero = SKBitmap.Decode(grayZeroPrepared.EncodedImage);
        Equal(new SKColor(210, 210, 210), grayZero.GetPixel(10, 2),
            "the bounded rescue also preserves neutral antialiasing luminance");

        var binaryPadded = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(graySource),
            "OCR",
            """{"roi":[0,0,4,4],"only_rec":true}""",
            2,
            $"relic.stack.{RelicId}.capture.gray.card.left.variant.binary-pad2");
        using var binaryPaddedImage = SKBitmap.Decode(binaryPadded.EncodedImage);
        Equal(24, binaryPaddedImage.Width, "padded binary keeps the original horizontal margins");
        Equal(16, binaryPaddedImage.Height, "padded binary adds two source pixels above and below");
        Equal(SKColors.Black, binaryPaddedImage.GetPixel(10, 2), "padded binary top margin stays black");
        Equal(SKColors.White, binaryPaddedImage.GetPixel(10, 6), "padded binary content keeps the strict mask");

        var grayPadded = RhodesMaaRecognitionImagePreprocessor.Prepare(
            Encode(graySource),
            "OCR",
            """{"roi":[0,0,4,4],"only_rec":true}""",
            2,
            $"relic.stack.{RelicId}.capture.gray.card.left.variant.gray0-pad2");
        using var grayPaddedImage = SKBitmap.Decode(grayPadded.EncodedImage);
        Equal(24, grayPaddedImage.Width, "padded gray keeps the original horizontal margins");
        Equal(16, grayPaddedImage.Height, "padded gray adds two source pixels above and below");
        Equal(new SKColor(210, 210, 210), grayPaddedImage.GetPixel(10, 6),
            "padded gray content preserves neutral antialiasing luminance");
    }

    public static void KeepsCardEvidenceWithoutForcingZeroOrConflictingCardsIntoState()
    {
        var explicitZeroEntry = $"relic.stack.{RelicId}.capture.frame-a.card.left";
        var unreadableEntry = $"relic.stack.{RelicId}.capture.frame-a.card.right";
        var observations = RhodesRelicStackObservationReader.Read([
            CardStackResult(explicitZeroEntry, "0"),
            CardStackResult(unreadableEntry, "?"),
        ]);
        Equal(2, observations.Count, "each planned card remains independently inspectable");
        Equal(RhodesRelicStackObservationStatus.ExplicitZero, observations[0].Status,
            "an OCR-recognized zero is distinct from a missing result");
        Equal<int?>(0, observations[0].Count, "explicit zero retains its diagnostic value");
        Equal(RhodesRelicStackObservationStatus.Unreadable, observations[1].Status,
            "a marker with no numeric OCR stays unreadable");
        Equal(0, RhodesRelicStackObservationReader.Read([]).Count,
            "no planned stack badge remains absence, not an inferred zero");

        var lowConfidence = RhodesRelicStackObservationReader.Read([
            CardStackResultWithScore($"relic.stack.{RelicId}.capture.frame-low.card.left", "5", 0.47),
        ]).Single();
        Equal(RhodesRelicStackObservationStatus.LowConfidence, lowConfidence.Status,
            "a weak single-glyph guess remains diagnostic rather than authoritative");
        Equal(0, Convert([
            NameResult(),
            CardStackResultWithScore($"relic.stack.{RelicId}.capture.frame-low.card.left", "5", 0.47),
        ]).Count, "a weak single-glyph guess cannot overwrite scalar run state");

        var conflicting = Convert([
            NameResult(),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-b.card.left", "4"),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-b.card.right", "9"),
        ]);
        Equal(0, conflicting.Count,
            "different card values in one capture cannot be collapsed into the scalar run state");

        var agreeing = Convert([
            NameResult(),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-c.card.left", "4"),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-c.card.right", "×4"),
        ]);
        Equal(4, agreeing.Count, "agreeing cards contribute one capture-level vote");

        var fallback = Convert([
            NameResult(),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-d.card.left.variant.binary", "^0"),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-d.card.left.variant.gray", "10"),
        ]);
        Equal(10, fallback.Count,
            "one confirmed fallback can recover the same card after the primary image stays non-authoritative");

        var conflictingFallbacks = Convert([
            NameResult(),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-e.card.left.variant.binary", "4"),
            CardStackResult($"relic.stack.{RelicId}.capture.frame-e.card.left.variant.gray", "9"),
        ]);
        Equal(0, conflictingFallbacks.Count,
            "two authoritative values for one card remain ambiguous");

        var filteredObject = new MaaTaskRunResult(
            $"relic.stack.{RelicId}.capture.frame-object-a.card.left", "Succeeded", true, "",
            """{"filtered":{"text":"4","score":"legacy-invalid"}}""", "OCR", true);
        Equal(4, Convert([NameResult(), filteredObject]).Count,
            "a single filtered OCR object with nonnumeric optional metadata remains compatible");
        var allObject = new MaaTaskRunResult(
            $"relic.stack.{RelicId}.capture.frame-object-b.card.left", "Succeeded", true, "",
            """{"all":{"text":"9","score":0.99}}""", "OCR", true);
        Equal(9, Convert([NameResult(), allObject]).Count,
            "a single all-results OCR object remains compatible");
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

    private static MaaTaskRunResult CardStackResult(string entry, params string[] texts) => new(
        entry, "Succeeded", true, "",
        JsonSerializer.Serialize(new { filtered = texts.Select(text => new { text, score = 0.99 }) }),
        "OCR", true);

    private static MaaTaskRunResult CardStackResultWithScore(string entry, string text, double score) => new(
        entry, "Succeeded", true, "",
        JsonSerializer.Serialize(new { filtered = new[] { new { text, score } } }),
        "OCR", true);

    private static MaaTaskRunResult NameRows(params (string Text, int X, int Y)[] rows) => new(
        "RhodesOcrRegion_relic_list_text", "Succeeded", true, "",
        JsonSerializer.Serialize(new
        {
            filtered = rows.Select(row => new
            {
                text = row.Text,
                score = 0.99,
                box = new[] { row.X, row.Y, 104, 23 },
            }),
        }),
        "OCR", true);

    private static void DrawStackBadge(SKBitmap bitmap, int titleX, int titleY, int[] digitWidths)
    {
        var arrowLeft = titleX - 67;
        var arrowTop = titleY + 70;
        for (var y = 0; y < 6; y++)
        {
            var halfWidth = y + 1;
            for (var x = 6 - halfWidth + 1; x <= 7 + halfWidth - 1; x++)
                bitmap.SetPixel(arrowLeft + x, arrowTop + y, SKColors.White);
        }
        for (var y = 6; y < 8; y++)
        for (var x = 0; x < 14; x++)
            bitmap.SetPixel(arrowLeft + x, arrowTop + y, SKColors.White);
        for (var y = 8; y < 13; y++)
        {
            var offset = y - 7;
            for (var x = 6 - offset; x <= 8 - offset; x++)
                bitmap.SetPixel(arrowLeft + x, arrowTop + y, SKColors.White);
            for (var x = 5 + offset; x <= 7 + offset; x++)
                bitmap.SetPixel(arrowLeft + x, arrowTop + y, SKColors.White);
        }

        DrawDigitBlocks(bitmap, titleX - 46, titleY + 70, digitWidths);
    }

    private static void DrawDigitBlocks(
        SKBitmap bitmap,
        int left,
        int top,
        int[] widths,
        int advancePadding = 7)
    {
        foreach (var width in widths)
        {
            for (var y = top; y < top + 16; y++)
            for (var x = left; x < left + width; x++)
                bitmap.SetPixel(x, y, SKColors.White);
            left += width + advancePadding;
        }
    }

    private static void DrawSparseDigitChevron(SKBitmap bitmap, int left, int top)
    {
        for (var y = 0; y < 8; y++)
        {
            bitmap.SetPixel(left + 4 - Math.Min(4, y), top + y, SKColors.White);
            bitmap.SetPixel(left + 5 + Math.Min(4, y), top + y, SKColors.White);
        }
        for (var y = 8; y < 16; y++)
        {
            bitmap.SetPixel(left + Math.Min(4, y - 7), top + y, SKColors.White);
            bitmap.SetPixel(left + 9 - Math.Min(4, y - 7), top + y, SKColors.White);
        }
    }

    private static byte[] Encode(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected {expected}, actual {actual}");
    }
}
