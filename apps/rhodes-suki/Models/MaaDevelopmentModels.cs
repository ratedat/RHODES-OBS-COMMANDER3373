namespace RhodesSuki.Models;

public sealed record RhodesExternalToolRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    bool UseShellExecute,
    IReadOnlyDictionary<string, string> Environment,
    string WorkingDirectory = "");

public sealed record RhodesExternalToolResult(
    bool Succeeded,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    string Detail);

public sealed record RhodesMaaEvidenceSummary(
    bool IsSupported,
    int ArtifactCount,
    int EvidenceCount,
    int MissingEvidenceCount,
    int WarningCount,
    string SchemaVersion,
    string Detail);

public sealed record RhodesRecognitionLabRequest(
    string Mode,
    string ImagePath,
    MaaRoi Roi,
    string Expected = "",
    double Threshold = 0.7,
    bool OnlyRecognition = true,
    string Template = "",
    int TemplateMethod = 5,
    int ColorMethod = 40,
    string ColorLower = "0,0,0",
    string ColorUpper = "255,255,255",
    int ColorCount = 1,
    bool ColorConnected = false);

public sealed record RhodesRecognitionLabPlan(
    bool IsValid,
    string Mode,
    string ImagePath,
    MaaRoi Roi,
    bool UsesMaaRecognition,
    string PayloadJson,
    string Error);

public sealed record RhodesRecognitionLabResult(
    bool Succeeded,
    string Mode,
    string Detail,
    string ResultJson,
    long ElapsedMilliseconds,
    string LogPath = "");
