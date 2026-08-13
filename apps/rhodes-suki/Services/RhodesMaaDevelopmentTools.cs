using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaDevelopmentTools
{
    private static readonly IReadOnlyDictionary<string, string> NoEnvironment =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static RhodesExternalToolRequest BuildOpenRequest(string toolId, string repositoryRoot)
    {
        var root = Path.GetFullPath(repositoryRoot);
        var id = (toolId ?? "").Trim().ToLowerInvariant();
        var arguments = id switch
        {
            "mse" => new[] { "--reuse-window", root },
            "mpe" => new[]
            {
                "--reuse-window",
                Path.Combine(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json"),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(toolId), toolId, "対応していないMAA開発ツールです。"),
        };

        return new RhodesExternalToolRequest(
            "code",
            arguments,
            UseShellExecute: false,
            NoEnvironment,
            root);
    }

    public static RhodesExternalToolRequest BuildLogAnalyzerRequest(
        string executablePath,
        string materialsPath)
    {
        var rawExecutable = (executablePath ?? "").Trim();
        if (Uri.TryCreate(rawExecutable, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            throw new InvalidOperationException("MaaLogAnalyzerはローカル実行ファイルだけ指定できます。");
        }
        if (string.IsNullOrWhiteSpace(rawExecutable))
            throw new InvalidOperationException("MaaLogAnalyzerのローカル実行ファイルを指定してください。");

        var executable = Path.GetFullPath(rawExecutable);
        var materials = Path.GetFullPath(materialsPath);
        return new RhodesExternalToolRequest(
            executable,
            [materials],
            UseShellExecute: false,
            NoEnvironment,
            Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory);
    }
}
