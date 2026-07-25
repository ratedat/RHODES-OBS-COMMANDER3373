using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RhodesSuki.Services;

public static class RhodesCoinStabilityReportWriter
{
    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public static void Write(
        string outputDirectory,
        RhodesCoinStabilityRunResult result)
    {
        Directory.CreateDirectory(outputDirectory);

        var observationsPath = Path.Combine(outputDirectory, "observations.jsonl");
        using (var writer = new StreamWriter(
            observationsPath,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            foreach (var observation in result.Observations
                .OrderBy(item => item.FrameId, StringComparer.Ordinal)
                .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
                .ThenBy(item => item.PassIndex)
                .ThenBy(item => item.SlotIndex))
            {
                writer.WriteLine(JsonSerializer.Serialize(observation, JsonLineOptions));
            }
        }

        WriteErrorsCsv(Path.Combine(outputDirectory, "errors.csv"), result.Errors);
        WriteJson(Path.Combine(outputDirectory, "summary.json"), result.Summary);
        WriteJson(Path.Combine(outputDirectory, "candidate-diff.json"), result.CandidateDiffs);
        WriteJson(Path.Combine(outputDirectory, "run-metadata.json"), result.Metadata);
        WriteJson(
            Path.Combine(outputDirectory, "confusion-matrix.json"),
            result.StatusConfusionMatrix ?? new RhodesCoinStabilityConfusionMatrix(0, [], []));
        WriteJson(
            Path.Combine(outputDirectory, "threshold-sweep.json"),
            result.ThresholdSweep ?? []);
    }

    private static void WriteErrorsCsv(
        string path,
        IEnumerable<RhodesCoinStabilityError> errors)
    {
        var lines = new List<string>
        {
            CsvRow(
                "frameId",
                "profileId",
                "passIndex",
                "slotIndex",
                "errorClass",
                "expectedCoinId",
                "expectedStatusId",
                "actualCoinId",
                "actualStatusId",
                "detail"),
        };
        lines.AddRange(errors
            .OrderBy(item => item.FrameId, StringComparer.Ordinal)
            .ThenBy(item => item.ProfileId, StringComparer.Ordinal)
            .ThenBy(item => item.PassIndex)
            .ThenBy(item => item.SlotIndex)
            .ThenBy(item => item.ErrorClass, StringComparer.Ordinal)
            .Select(error => CsvRow(
                error.FrameId,
                error.ProfileId,
                error.PassIndex.ToString(),
                error.SlotIndex.ToString(),
                error.ErrorClass,
                error.ExpectedCoinId,
                error.ExpectedStatusId,
                error.ActualCoinId,
                error.ActualStatusId,
                error.Detail)));
        File.WriteAllText(
            path,
            string.Join("\r\n", lines) + "\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string CsvRow(params string[] values) =>
        string.Join(",", values.Select(CsvValue));

    private static string CsvValue(string value)
    {
        if (!value.Contains(',')
            && !value.Contains('"')
            && !value.Contains('\r')
            && !value.Contains('\n'))
        {
            return value;
        }
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, RhodesCoinStabilityJson.Options);
        File.WriteAllText(
            path,
            json + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
