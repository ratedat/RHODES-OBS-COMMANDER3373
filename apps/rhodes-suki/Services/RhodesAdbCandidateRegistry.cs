using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbCandidateRegistry
{
    public static IReadOnlyList<MaaAdbPathCandidatePreview> Normalize(
        IEnumerable<MaaAdbPathCandidatePreview> candidates,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var ordered = new Dictionary<string, MaaAdbPathCandidatePreview>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Path))
                continue;

            var path = candidate.Path.Trim();
            var normalized = candidate with
            {
                Path = path,
                Exists = candidate.Exists || IsPathAdb(path) || fileExists(path),
                Available = candidate.Available,
            };
            if (!ShouldShow(normalized))
                continue;

            if (!ordered.TryGetValue(path, out var existing) || CandidatePriority(normalized) > CandidatePriority(existing))
                ordered[path] = normalized;
        }

        return ordered.Values
            .OrderByDescending(candidate => candidate.Available)
            .ThenByDescending(candidate => candidate.Exists)
            .ThenBy(candidate => candidate.Preset)
            .ThenBy(candidate => candidate.Path)
            .ToArray();
    }

    public static IReadOnlyList<MaaAdbPathCandidatePreview> Merge(
        IEnumerable<MaaAdbPathCandidatePreview> first,
        IEnumerable<MaaAdbPathCandidatePreview> second,
        Func<string, bool>? fileExists = null)
    {
        return Normalize(first.Concat(second), fileExists);
    }

    public static MaaAdbPathCandidatePreview? SelectDefault(
        IEnumerable<MaaAdbPathCandidatePreview> candidates,
        string currentPath)
    {
        var list = candidates as IReadOnlyList<MaaAdbPathCandidatePreview> ?? candidates.ToArray();
        return list.FirstOrDefault(candidate => PathsEqual(candidate.Path, currentPath))
            ?? list.FirstOrDefault(candidate => candidate.IsSelectable);
    }

    public static bool PathsEqual(string left, string right)
    {
        return left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldShow(MaaAdbPathCandidatePreview candidate)
    {
        if (candidate.Available || candidate.Exists || !string.IsNullOrWhiteSpace(candidate.Error))
            return true;

        return candidate.Source.Equals("settings", StringComparison.OrdinalIgnoreCase)
            || candidate.Source.Equals("manual", StringComparison.OrdinalIgnoreCase)
            || candidate.Source.Equals("env", StringComparison.OrdinalIgnoreCase)
            || candidate.Source.Equals("path", StringComparison.OrdinalIgnoreCase);
    }

    private static int CandidatePriority(MaaAdbPathCandidatePreview candidate)
    {
        return (candidate.Available ? 4 : 0)
            + (candidate.Exists ? 2 : 0)
            + (string.IsNullOrWhiteSpace(candidate.Error) ? 1 : 0);
    }

    private static bool IsPathAdb(string path)
    {
        return path.Equals("adb", StringComparison.OrdinalIgnoreCase)
            || path.Equals("adb.exe", StringComparison.OrdinalIgnoreCase);
    }
}
