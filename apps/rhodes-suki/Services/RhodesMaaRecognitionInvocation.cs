using System.Text.Json.Nodes;

namespace RhodesSuki.Services;

public sealed record MaaRecognitionInvocation(string Type, string ParametersJson);

public static class RhodesMaaRecognitionInvocation
{
    public static bool TryParse(
        string? payloadJson,
        out MaaRecognitionInvocation invocation,
        out string error)
    {
        invocation = new MaaRecognitionInvocation("", "{}");
        error = "";
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            error = "recognition payload が空です。";
            return false;
        }

        try
        {
            var parameters = JsonNode.Parse(payloadJson)?.AsObject();
            var type = parameters?["recognition"]?.GetValue<string>()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(type))
            {
                error = "recognition payload に認識方式がありません。";
                return false;
            }

            parameters!.Remove("recognition");
            invocation = new MaaRecognitionInvocation(type, parameters.ToJsonString());
            return true;
        }
        catch (Exception ex)
        {
            error = $"recognition payload を解析できません: {ex.Message}";
            return false;
        }
    }
}
