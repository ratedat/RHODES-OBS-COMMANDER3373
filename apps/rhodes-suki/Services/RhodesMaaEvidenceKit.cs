using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaEvidenceKit
{
    public const string SchemaVersion = "maa-evidence/v1";
    public const string AutoUpdateEnvironment = "MAA_EVIDENCE_AUTO_UPDATE";
    public const string TelemetryEnvironment = "MAA_EVIDENCE_TELEMETRY";

    public static RhodesExternalToolRequest BuildInspectRequest(
        string executable,
        string materialsPath,
        string outputPath)
    {
        var materials = Path.GetFullPath(materialsPath);
        var output = Path.GetFullPath(outputPath);
        return new RhodesExternalToolRequest(
            string.IsNullOrWhiteSpace(executable) ? "maa-evidence" : executable.Trim(),
            ["inspect", materials, "--format", "json", "--output", output],
            UseShellExecute: false,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [AutoUpdateEnvironment] = "0",
                [TelemetryEnvironment] = "0",
            },
            Path.GetDirectoryName(materials) ?? Environment.CurrentDirectory);
    }

    public static RhodesMaaEvidenceSummary ParseSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new RhodesMaaEvidenceSummary(false, 0, 0, 0, 0, "", "MEK JSONが空です。");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var schema = String(root, "schemaVersion");
            var supported = string.Equals(schema, SchemaVersion, StringComparison.Ordinal);
            return new RhodesMaaEvidenceSummary(
                supported,
                ArrayCount(root, "artifacts"),
                ArrayCount(root, "evidence"),
                ArrayCount(root, "missingEvidence"),
                ArrayCount(root, "warnings"),
                schema,
                supported ? "MEK証跡を読み込みました。" : $"未対応のMEK schemaです: {schema}");
        }
        catch (Exception ex)
        {
            return new RhodesMaaEvidenceSummary(false, 0, 0, 0, 0, "", $"MEK JSONを解析できません: {ex.Message}");
        }
    }

    public static async Task<(RhodesExternalToolResult Process, RhodesMaaEvidenceSummary Summary)> InspectAsync(
        string executable,
        string materialsPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var process = await RhodesExternalToolRunner.RunAsync(
            BuildInspectRequest(executable, materialsPath, outputPath),
            waitForExit: true,
            cancellationToken);
        if (!process.Succeeded || !File.Exists(outputPath))
            return (process, new RhodesMaaEvidenceSummary(false, 0, 0, 0, 0, "", process.Detail));
        return (process, ParseSummary(await File.ReadAllTextAsync(outputPath, cancellationToken)));
    }

    private static int ArrayCount(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;

    private static string String(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
}
