using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionOperationStage(string Name, string ProfileId, long StartMs, long DurationMs);
public sealed record RhodesRecognitionOperationMetric(int Count, double DurationMs);
public sealed record RhodesRecognitionOperationSnapshot(
    int SchemaVersion, string OperationId, string[] ProfileIds, DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt, long DurationMs, string Status,
    RhodesRecognitionOperationStage[] Stages,
    IReadOnlyDictionary<string, RhodesRecognitionOperationMetric> Metrics,
    IReadOnlyDictionary<string, string> ResultSignatures);

/// <summary>One button operation, including navigation, awaited state persistence and evidence writes.</summary>
public sealed class RhodesRecognitionOperation
{
    private readonly TimeProvider _clock;
    private readonly long _start;
    private readonly DateTimeOffset _startedAt;
    private readonly string[] _profileIds;
    private readonly object _gate = new();
    private readonly List<RhodesRecognitionOperationStage> _stages = [];
    private readonly Dictionary<string, RhodesRecognitionOperationMetric> _metrics = [];
    private readonly Dictionary<string, string> _resultSignatures = [];
    private RhodesRecognitionOperationSnapshot? _completed;

    public RhodesRecognitionOperation(IEnumerable<string> profileIds, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        _start = _clock.GetTimestamp();
        _startedAt = _clock.GetUtcNow();
        _profileIds = profileIds.Distinct(StringComparer.Ordinal).ToArray();
    }

    public string Id { get; } = Guid.NewGuid().ToString("D");
    private long ElapsedMs => (long)_clock.GetElapsedTime(_start).TotalMilliseconds;

    public IDisposable Measure(string name, string? profileId = null) =>
        new StageScope(this, name, profileId ?? "", ElapsedMs);

    public void RecordResult(string profileId, IEnumerable<MaaCandidatePreview> candidates)
    {
        // Confidence and OCR spelling are evidence, not the resulting game state.
        var rows = candidates.Select(candidate => JsonSerializer.Serialize(new
        {
            candidate.Kind, candidate.Value, candidate.Field, candidate.OperatorId, candidate.RelicId,
            candidate.CampaignId, candidate.ThoughtId, candidate.AgeId, candidate.FieldId, candidate.SlotKind,
            candidate.EffectId, candidate.StateId, candidate.CoinId, candidate.StatusId, candidate.Face,
            candidate.Count, candidate.OperatorInstance, candidate.PromotionLevel,
        })).Order(StringComparer.Ordinal);
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
        lock (_gate)
            if (_completed is null) _resultSignatures[profileId] = signature;
    }

    public void RecordMetric(string name, double durationMs)
    {
        if (!double.IsFinite(durationMs) || durationMs < 0) return;
        lock (_gate)
        {
            if (_completed is not null) return;
            var previous = _metrics.GetValueOrDefault(name) ?? new(0, 0);
            _metrics[name] = new(previous.Count + 1, previous.DurationMs + durationMs);
        }
    }

    public RhodesRecognitionOperationSnapshot Complete(string status)
    {
        lock (_gate)
            return _completed ??= new(1, Id, _profileIds, _startedAt, _clock.GetUtcNow(),
                ElapsedMs, status, _stages.ToArray(), new Dictionary<string, RhodesRecognitionOperationMetric>(_metrics),
                new Dictionary<string, string>(_resultSignatures));
    }

    public async Task SaveAsync(string directory)
    {
        var snapshot = _completed ?? throw new InvalidOperationException("Complete the operation before saving it.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"operation-{snapshot.StartedAt:yyyyMMddTHHmmssfff}-{Id}.json");
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json + "\n");
            File.Move(temporaryPath, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private sealed class StageScope(RhodesRecognitionOperation owner, string name, string profileId, long started) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            lock (owner._gate)
                if (owner._completed is null)
                    owner._stages.Add(new(name, profileId, started, Math.Max(0, owner.ElapsedMs - started)));
        }
    }
}
