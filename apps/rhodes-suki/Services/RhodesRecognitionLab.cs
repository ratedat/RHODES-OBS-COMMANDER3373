using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionLab
{
    public const int BaseWidth = 1280;
    public const int BaseHeight = 720;

    public static RhodesRecognitionLabPlan BuildPlan(RhodesRecognitionLabRequest request)
    {
        var mode = (request.Mode ?? "").Trim().ToLowerInvariant();
        var imagePath = (request.ImagePath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(imagePath))
            return Invalid(request, mode, "画像が指定されていません。");
        if (!IsValidRoi(request.Roi))
            return Invalid(request, mode, "ROIは1280x720の範囲内で指定してください。");
        if (request.Threshold is < 0 or > 1)
            return Invalid(request, mode, "thresholdは0から1の範囲で指定してください。");

        if (mode is "sui-active-coins" or "sui-owned-coins" or "sui-owned-status")
        {
            return new RhodesRecognitionLabPlan(
                true,
                mode,
                imagePath,
                request.Roi,
                UsesMaaRecognition: false,
                PayloadJson: "",
                Error: "");
        }

        JsonObject payload;
        switch (mode)
        {
            case "ocr":
                payload = new JsonObject
                {
                    ["recognition"] = "OCR",
                    ["roi"] = JsonSerializer.SerializeToNode(request.Roi.ToArray()),
                    ["only_rec"] = request.OnlyRecognition,
                    ["threshold"] = request.Threshold,
                };
                var expected = request.Expected
                    .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (expected.Length > 0)
                    payload["expected"] = JsonSerializer.SerializeToNode(expected);
                break;

            case "template-match":
                if (string.IsNullOrWhiteSpace(request.Template))
                    return Invalid(request, mode, "TemplateMatchにはtemplateが必要です。");
                payload = new JsonObject
                {
                    ["recognition"] = "TemplateMatch",
                    ["roi"] = JsonSerializer.SerializeToNode(request.Roi.ToArray()),
                    ["template"] = request.Template.Trim(),
                    ["threshold"] = request.Threshold,
                    ["method"] = request.TemplateMethod,
                };
                break;

            case "color-match":
                if (!TryParseColor(request.ColorLower, out var lower)
                    || !TryParseColor(request.ColorUpper, out var upper))
                {
                    return Invalid(request, mode, "ColorMatchのlower/upperは0～255の3要素で指定してください。");
                }
                if (request.ColorCount < 1)
                    return Invalid(request, mode, "ColorMatchのcountは1以上で指定してください。");
                payload = new JsonObject
                {
                    ["recognition"] = "ColorMatch",
                    ["roi"] = JsonSerializer.SerializeToNode(request.Roi.ToArray()),
                    ["method"] = request.ColorMethod,
                    ["lower"] = JsonSerializer.SerializeToNode(lower),
                    ["upper"] = JsonSerializer.SerializeToNode(upper),
                    ["count"] = request.ColorCount,
                    ["connected"] = request.ColorConnected,
                };
                break;

            default:
                return Invalid(request, mode, $"未対応の認識モードです: {mode}");
        }

        return new RhodesRecognitionLabPlan(
            true,
            mode,
            imagePath,
            request.Roi,
            UsesMaaRecognition: true,
            payload.ToJsonString(),
            Error: "");
    }

    private static RhodesRecognitionLabPlan Invalid(
        RhodesRecognitionLabRequest request,
        string mode,
        string error) =>
        new(false, mode, request.ImagePath ?? "", request.Roi, false, "", error);

    private static bool IsValidRoi(MaaRoi roi) =>
        roi.X >= 0
        && roi.Y >= 0
        && roi.Width > 0
        && roi.Height > 0
        && roi.X + roi.Width <= BaseWidth
        && roi.Y + roi.Height <= BaseHeight;

    private static bool TryParseColor(string value, out int[] color)
    {
        color = [];
        var parts = (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
            return false;
        var parsed = new int[3];
        for (var index = 0; index < parsed.Length; index++)
        {
            if (!int.TryParse(parts[index], out parsed[index]) || parsed[index] is < 0 or > 255)
                return false;
        }
        color = parsed;
        return true;
    }
}
