using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaPaths
{
    private static readonly string[] RequiredRecognitionResourceFiles =
    [
        "model/ocr/det.onnx",
        "model/ocr/rec.onnx",
        "model/ocr/keys.txt",
        "image/third_party/maa/resource/template/Roguelike/base/RoguelikeRecruitOcrFlag.png",
    ];

    public static MaaBaseResolution BaseResolution { get; } = new(1280, 720);

    public static string AppBaseDirectory => AppContext.BaseDirectory;

    public static string DefaultResourceRoot => Path.Combine(AppBaseDirectory, "resource", "base");

    public static string DefaultAgentBinaryRoot
    {
        get
        {
            var libsPath = Path.Combine(AppBaseDirectory, "libs", "MaaAgentBinary");
            if (Directory.Exists(libsPath)) return libsPath;
            return Path.Combine(AppBaseDirectory, "MaaAgentBinary");
        }
    }

    public static IReadOnlyList<string> MissingRecognitionResourceFiles(string? resourceRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(resourceRoot) ? DefaultResourceRoot : resourceRoot.Trim();
        return RequiredRecognitionResourceFiles
            .Where(relative => !File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
    }

    public static string RecognitionResourceStatusDetail(string? resourceRoot = null)
    {
        var root = string.IsNullOrWhiteSpace(resourceRoot) ? DefaultResourceRoot : resourceRoot.Trim();
        var missing = MissingRecognitionResourceFiles(root);
        if (missing.Count == 0)
            return $"MAA-OCR asset OK: {root}";

        return $"missing={missing.Count}: {string.Join(", ", missing.Take(4))}; path={root}";
    }
}
