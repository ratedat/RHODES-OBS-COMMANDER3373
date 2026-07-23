using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesSuiCatchWindDetailResolver
{
    public const string DetailEntry = "RhodesDynamic_is6.coin_detail.catch_wind";

    private const string WholeListEntry = "RhodesOcrRegion_is6_coin_list_text";
    private const string SlotEntryPrefix = "RhodesDynamic_is6.coin_list_text.slot";
    private static readonly OwnedSlot[] OwnedSlots =
    [
        new(0, 601, 211),
        new(1, 881, 211),
        new(2, 1143, 211),
        new(3, 463, 348),
        new(4, 742, 348),
        new(5, 1005, 348),
        new(6, 601, 486),
        new(7, 881, 486),
        new(8, 1143, 486),
    ];

    public static MaaDynamicOcrRequest BuildDetailRequest() => new(
        DetailEntry,
        8,
        385,
        300,
        120,
        1,
        1,
        OnlyRecognition: false);

    public static int? FindVisibleSlot(IEnumerable<MaaTaskRunResult> taskResults)
    {
        foreach (var result in taskResults.Where(result => result.Succeeded && result.Hit))
        {
            if (TrySlotFromEntry(result.Entry, out var entrySlot)
                && OcrTokens(result).Any(token => LooksLikeCatchWind(token.Text)))
            {
                return entrySlot;
            }

            if (!result.Entry.Equals(WholeListEntry, StringComparison.Ordinal))
                continue;

            foreach (var token in OcrTokens(result).Where(token => LooksLikeCatchWind(token.Text)))
            {
                if (token.Box is not null
                    && RhodesSuiCoinImageRecognizer.MatchOwnedSlot(token.Box) is int matchedSlot)
                {
                    return matchedSlot;
                }
            }
        }

        return null;
    }

    public static bool HasCatchWindMention(IEnumerable<MaaTaskRunResult> taskResults) =>
        taskResults
            .Where(result => result.Succeeded && result.Hit)
            .SelectMany(OcrTokens)
            .Any(token => LooksLikeCatchWind(token.Text));

    public static RhodesRecognitionNavigationStep BuildTapStep(int slotIndex)
    {
        var slot = OwnedSlots.FirstOrDefault(item => item.Index == slotIndex)
            ?? throw new ArgumentOutOfRangeException(nameof(slotIndex));
        const int inset = 42;
        return new RhodesRecognitionNavigationStep(
            "tap",
            "捕風の説明を開く",
            slot.CenterX - inset,
            slot.CenterY - inset,
            inset * 2,
            inset * 2,
            0);
    }

    public static RhodesSuiCoinImageDetection? ResolveDetection(
        MaaTaskRunResult detailResult,
        int slotIndex,
        IReadOnlyList<SukiSpecialEffectOption> coinOptions,
        IReadOnlyList<SukiSpecialEffectOption>? statusOptions = null,
        string imageStatusId = "")
    {
        if (!detailResult.Succeeded || !detailResult.Hit)
            return null;

        var tokens = OcrTokens(detailResult).ToArray();
        var direction = ResolveDirection(tokens.Select(token => token.Text));
        if (direction is null)
            return null;

        var optionSuffix = direction.Value switch
        {
            CatchWindDirection.Left => "is6_copper_e19",
            CatchWindDirection.Right => "is6_copper_e20",
            CatchWindDirection.Up => "is6_copper_e21",
            CatchWindDirection.Down => "is6_copper_e22",
            _ => "",
        };
        var option = coinOptions.FirstOrDefault(candidate =>
            candidate.Id.EndsWith(optionSuffix, StringComparison.Ordinal));
        if (option is null)
            return null;

        var tapStep = BuildTapStep(slotIndex);
        var score = tokens.Select(token => token.Score).DefaultIfEmpty(0.8).Max();
        var availableStatuses = statusOptions ?? [];
        var statusId = availableStatuses.Any(option => option.Id.Equals(imageStatusId, StringComparison.Ordinal))
            ? imageStatusId
            : ResolveStatusId(tokens.Select(token => token.Text), availableStatuses);
        return new RhodesSuiCoinImageDetection(
            option.Id,
            option.Name,
            Math.Clamp(score, 0.8, 1),
            slotIndex,
            new MaaRoi(tapStep.X, tapStep.Y, tapStep.Width, tapStep.Height),
            StatusId: statusId,
            VisualStrength: 1);
    }

    public static string FindVisibleStatusId(
        IEnumerable<MaaTaskRunResult> taskResults,
        int slotIndex)
    {
        return taskResults
            .SelectMany(result =>
                RhodesSuiCoinImageRecognizer.TryRead(result, out var fieldId, out var detections)
                && fieldId.Equals(RhodesSuiCoinImageRecognizer.OwnedFieldId, StringComparison.Ordinal)
                    ? detections
                    : [])
            .Where(detection => detection.SlotIndex == slotIndex
                && !string.IsNullOrWhiteSpace(detection.StatusId))
            .OrderByDescending(detection => detection.Score)
            .Select(detection => detection.StatusId)
            .FirstOrDefault() ?? "";
    }

    private static string ResolveStatusId(
        IEnumerable<string> texts,
        IReadOnlyList<SukiSpecialEffectOption> statusOptions)
    {
        var normalized = string.Concat(texts.Select(Normalize));
        return statusOptions
            .Select(option => (Option: option, Name: Normalize(option.Name)))
            .Where(item => item.Name.Length > 0 && normalized.Contains(item.Name, StringComparison.Ordinal))
            .OrderByDescending(item => item.Name.Length)
            .Select(item => item.Option.Id)
            .FirstOrDefault() ?? "";
    }

    private static CatchWindDirection? ResolveDirection(IEnumerable<string> texts)
    {
        var normalized = string.Concat(texts.Select(Normalize));
        if (normalized.Contains("左向き", StringComparison.Ordinal)
            || normalized.Contains("左向", StringComparison.Ordinal))
            return CatchWindDirection.Left;
        if (normalized.Contains("右向き", StringComparison.Ordinal)
            || normalized.Contains("右向", StringComparison.Ordinal))
            return CatchWindDirection.Right;
        if (normalized.Contains("上向き", StringComparison.Ordinal)
            || normalized.Contains("上向", StringComparison.Ordinal))
            return CatchWindDirection.Up;
        if (normalized.Contains("下向き", StringComparison.Ordinal)
            || normalized.Contains("下向", StringComparison.Ordinal))
            return CatchWindDirection.Down;
        return null;
    }

    private static bool LooksLikeCatchWind(string value) =>
        Normalize(value).Contains("捕風", StringComparison.Ordinal);

    private static string Normalize(string value) => string.Concat(
        (value ?? "").Where(character =>
            !char.IsWhiteSpace(character)
            && !char.IsPunctuation(character)
            && !char.IsSymbol(character)));

    private static bool TrySlotFromEntry(string entry, out int slotIndex)
    {
        slotIndex = -1;
        if (!entry.StartsWith(SlotEntryPrefix, StringComparison.Ordinal)
            || !int.TryParse(entry[SlotEntryPrefix.Length..], out var parsedSlot)
            || !OwnedSlots.Any(slot => slot.Index == parsedSlot))
        {
            return false;
        }

        slotIndex = parsedSlot;
        return true;
    }

    private static IReadOnlyList<OcrToken> OcrTokens(MaaTaskRunResult result)
    {
        var tokens = new List<OcrToken>();
        if (!string.IsNullOrWhiteSpace(result.RecognitionDetailJson))
        {
            try
            {
                using var document = JsonDocument.Parse(result.RecognitionDetailJson);
                CollectTokens(document.RootElement, tokens);
            }
            catch (JsonException)
            {
                // The summary fields below remain useful for malformed debug evidence.
            }
        }

        if (tokens.Count == 0 && !string.IsNullOrWhiteSpace(result.Detail))
            tokens.Add(new OcrToken(result.Detail, 0.8, null));
        return tokens;
    }

    private static void CollectTokens(JsonElement element, ICollection<OcrToken> tokens)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryString(element, "text", out var text)
                || TryString(element, "label", out text))
            {
                tokens.Add(new OcrToken(
                    text,
                    Number(element, "score", 0.8),
                    TryBox(element, out var box) ? box : null));
            }

            foreach (var property in element.EnumerateObject())
                CollectTokens(property.Value, tokens);
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectTokens(item, tokens);
        }
    }

    private static bool TryString(JsonElement element, string propertyName, out string value)
    {
        value = "";
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = property.GetString() ?? "");
    }

    private static double Number(JsonElement element, string propertyName, double fallback) =>
        element.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetDouble(out var value)
            ? value
            : fallback;

    private static bool TryBox(JsonElement element, out MaaRoi box)
    {
        box = new MaaRoi(0, 0, 0, 0);
        if (!element.TryGetProperty("box", out var property)
            || property.ValueKind != JsonValueKind.Array)
            return false;
        var values = property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Number)
            .Select(item => item.GetInt32())
            .Take(4)
            .ToArray();
        if (values.Length < 4)
            return false;
        box = new MaaRoi(values[0], values[1], values[2], values[3]);
        return true;
    }

    private sealed record OwnedSlot(int Index, int CenterX, int CenterY);
    private sealed record OcrToken(string Text, double Score, MaaRoi? Box);
    private enum CatchWindDirection { Left, Right, Up, Down }
}
