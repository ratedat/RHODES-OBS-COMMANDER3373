using RhodesSuki.Services;

try
{
    var options = CommandLineOptions.Parse(args);
    var repositoryRoot = FindRepositoryRoot(
        Path.GetDirectoryName(Path.GetFullPath(options.ManifestPath))
        ?? Environment.CurrentDirectory);
    var manifestPath = ResolvePath(repositoryRoot, options.ManifestPath);
    var outputPath = ResolvePath(repositoryRoot, options.OutputPath);
    var frameRoots = options.FrameRoots
        .Select(path => ResolvePath(repositoryRoot, path))
        .ToArray();
    var manifest = RhodesCoinStabilityManifest.Load(manifestPath);
    var frames = RhodesCoinStabilityCorpus.Discover(frameRoots);
    if (frames.Count == 0)
    {
        throw new InvalidOperationException(
            "is6CoinsFull / is6ActiveCoinsFull の保存Frameを見つけられませんでした。");
    }

    var result = RhodesCoinStabilityRunner.Run(
        manifest,
        frames,
        repositoryRoot,
        frameRoots,
        new RhodesCoinStabilityRunOptions(options.Sweep));
    RhodesCoinStabilityReportWriter.Write(outputPath, result);

    Console.WriteLine($"frames={result.Summary.FrameCount}");
    Console.WriteLine($"observations={result.Summary.ObservationCount}");
    Console.WriteLine($"labeledSlots={result.Summary.LabeledSlotCount}");
    Console.WriteLine($"coinAccuracy={result.Summary.CoinAccuracy:P2}");
    Console.WriteLine($"statusAccuracy={result.Summary.StatusAccuracy:P2}");
    Console.WriteLine($"candidateSplits={result.Summary.CandidateSplitCount}");
    Console.WriteLine($"errors={result.Summary.ErrorCount}");
    Console.WriteLine($"resultHash={result.Summary.ResultHash}");
    Console.WriteLine($"elapsedMs={result.Metadata.ElapsedMilliseconds}");
    Console.WriteLine($"decodeMs={result.Summary.Timing?.DecodeMilliseconds:F2}");
    Console.WriteLine($"anchorMs={result.Summary.Timing?.AnchorMilliseconds:F2}");
    Console.WriteLine($"coinMatchingMs={result.Summary.Timing?.CoinMatchingMilliseconds:F2}");
    Console.WriteLine($"statusMatchingMs={result.Summary.Timing?.StatusMatchingMilliseconds:F2}");
    Console.WriteLine($"ocrMs={result.Summary.Timing?.OcrMilliseconds:F2}");
    Console.WriteLine($"coinComparisons={result.Summary.Timing?.CoinComparisonCount ?? 0}");
    Console.WriteLine($"statusColorComparisons={result.Summary.Timing?.StatusColorComparisonCount ?? 0}");
    Console.WriteLine($"statusShapeComparisons={result.Summary.Timing?.StatusShapeComparisonCount ?? 0}");
    Console.WriteLine($"overlayComparisons={result.Summary.Timing?.OverlayComparisonCount ?? 0}");
    Console.WriteLine($"ocrTasks={result.Summary.Timing?.OcrTaskCount ?? 0}");
    Console.WriteLine($"sweepPoints={result.ThresholdSweep?.Count ?? 0}");
    Console.WriteLine(
        $"eligibleSweepPoints={result.ThresholdSweep?.Count(point => point.MeetsFalsePositiveConstraint) ?? 0}");
    Console.WriteLine($"output={outputPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"coin-stability: {ex.Message}");
    Environment.ExitCode = 1;
}

static string FindRepositoryRoot(string startPath)
{
    var current = new DirectoryInfo(startPath);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "data", "selectable-effects.json"))
            && Directory.Exists(Path.Combine(current.FullName, "apps", "rhodes-suki")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }

    throw new InvalidOperationException(
        $"RHODESリポジトリルートを特定できません: {startPath}");
}

static string ResolvePath(string repositoryRoot, string path) =>
    Path.GetFullPath(
        Path.IsPathRooted(path)
            ? path
            : Path.Combine(repositoryRoot, path));

internal sealed record CommandLineOptions(
    string ManifestPath,
    IReadOnlyList<string> FrameRoots,
    string OutputPath,
    bool Sweep)
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var manifest = "";
        var frames = new List<string>();
        var output = "";
        var sweep = false;
        for (var index = 0; index < args.Count; index++)
        {
            var option = args[index];
            if (option.Equals("--manifest", StringComparison.Ordinal))
            {
                manifest = ReadValue(args, ref index, option);
            }
            else if (option.Equals("--frames", StringComparison.Ordinal))
            {
                frames.Add(ReadValue(args, ref index, option));
            }
            else if (option.Equals("--out", StringComparison.Ordinal))
            {
                output = ReadValue(args, ref index, option);
            }
            else if (option.Equals("--sweep", StringComparison.Ordinal))
            {
                sweep = true;
            }
            else
            {
                throw new InvalidOperationException($"未対応の引数です: {option}");
            }
        }

        if (string.IsNullOrWhiteSpace(manifest))
            throw new InvalidOperationException("--manifest は必須です。");
        if (frames.Count == 0)
            throw new InvalidOperationException("--frames は1件以上必要です。");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("--out は必須です。");
        return new CommandLineOptions(manifest, frames, output, sweep);
    }

    private static string ReadValue(
        IReadOnlyList<string> args,
        ref int index,
        string option)
    {
        if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new InvalidOperationException($"{option} の値がありません。");
        index++;
        return args[index];
    }
}
