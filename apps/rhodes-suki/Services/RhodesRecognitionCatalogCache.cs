using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionCatalogCache
{
    private static readonly object Sync = new();
    private static CacheEntry? _cached;
    private static long _diagnosticLoadCount;

    public static long DiagnosticLoadCount => Interlocked.Read(ref _diagnosticLoadCount);

    public static RhodesRecognitionCatalogSnapshot Load()
    {
        var dataRoot = Path.GetFullPath(RhodesRunCatalog.ResolveDataRoot());
        var operatorsStamp = FileStamp.Read(Path.Combine(dataRoot, "operators.json"));
        var relicsStamp = FileStamp.Read(Path.Combine(dataRoot, "relics.json"));

        lock (Sync)
        {
            if (_cached is not null
                && _cached.DataRoot.Equals(dataRoot, StringComparison.OrdinalIgnoreCase)
                && _cached.OperatorsStamp == operatorsStamp
                && _cached.RelicsStamp == relicsStamp)
            {
                return _cached.Snapshot;
            }

            var catalog = RhodesRunCatalog.LoadDefault(dataRootOverride: dataRoot);
            var snapshot = new RhodesRecognitionCatalogSnapshot(
                catalog.Operators.ToArray(),
                catalog.Relics.ToArray());
            _cached = new CacheEntry(dataRoot, operatorsStamp, relicsStamp, snapshot);
            Interlocked.Increment(ref _diagnosticLoadCount);
            return snapshot;
        }
    }

    public static void Invalidate()
    {
        lock (Sync)
            _cached = null;
    }

    private sealed record CacheEntry(
        string DataRoot,
        FileStamp OperatorsStamp,
        FileStamp RelicsStamp,
        RhodesRecognitionCatalogSnapshot Snapshot);

    private readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc)
    {
        public static FileStamp Read(string path)
        {
            var info = new FileInfo(path);
            return new FileStamp(info.Length, info.LastWriteTimeUtc);
        }
    }
}

public sealed record RhodesRecognitionCatalogSnapshot(
    IReadOnlyList<SukiChoiceItem> Operators,
    IReadOnlyList<SukiChoiceItem> Relics);
