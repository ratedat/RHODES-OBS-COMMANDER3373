using System.Text.Json;
using RhodesSuki.Services;
using RhodesSuki.Models;

public static class RecognitionOperationTests
{
    public static void DetailNavigationStaysOutsideWaiter()
    {
        Check(!RhodesRecognitionCapturePolicy.CanRecognizeDuringSettle("is6CoinsFull", true), "catch-wind detail navigation cannot replace the waiter's accepted frame");
        Check(!RhodesRecognitionCapturePolicy.CanRecognizeDuringSettle("unknown-future-profile", true), "new workflows require explicit capture-only review");
        Check(!RhodesRecognitionCapturePolicy.CanRecognizeDuringSettle("operatorsFull", false), "normalization passes do not recognize");
        Check(RhodesRecognitionCapturePolicy.CanRecognizeDuringSettle("operatorsFull", true), "pure frame recognition runs once within settle");
    }

    public static void SignaturesIncludeMutableState()
    {
        var candidate = new MaaCandidatePreview("operator", "アーミヤ", "amiya", "アーミヤ", 0.9,
            OperatorId: "amiya", Count: 1, PromotionLevel: 1);
        string Signature(MaaCandidatePreview item)
        {
            var operation = new RhodesRecognitionOperation(["operatorsFull"]);
            operation.RecordResult("operatorsFull", [item]);
            return operation.Complete("completed").ResultSignatures["operatorsFull"];
        }
        Check(Signature(candidate) == Signature(candidate with { RawText = "OCR noise", Confidence = 0.8 }), "OCR spelling alone is not game-state change");
        Check(Signature(candidate) != Signature(candidate with { PromotionLevel = 2 }), "same-count promotion is visible");
        Check(Signature(candidate) != Signature(candidate with { OperatorId = "amiya_guard" }), "same-count class change is visible");
        Check(Signature(candidate) != Signature(candidate with { Count = 2 }), "stack/count change is visible");
    }

    public static void TracksEndToEndAndFailures()
    {
        var clock = new TestClock();
        var operation = new RhodesRecognitionOperation(["operatorsFull"], clock);
        using (operation.Measure("recognition", "operatorsFull"))
            clock.Advance(60);
        using (operation.Measure("restore", "operatorsFull"))
            clock.Advance(20);
        using (operation.Measure("apply-and-save", "operatorsFull"))
            clock.Advance(30);
        var snapshot = operation.Complete("failed");
        Check(snapshot.DurationMs == 110, "end-to-end includes restore and awaited state save");
        Check(snapshot.Status == "failed", "failed operations are not successful measurements");
        Check(snapshot.Stages.Sum(stage => stage.DurationMs) == 110, "stage timings preserved");
        clock.Advance(50);
        Check(operation.Complete("completed") == snapshot, "completion cannot rewrite failed evidence or extend its clock");
    }

    public static void CorrelatesEvidenceWithoutReplacingSnapshots()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"rhodes-operation-{Guid.NewGuid():N}");
        try
        {
            var first = RhodesMaaRecognitionEvidenceLog.SaveAsync([], [], "operatorsFull", directory,
                scanId: "shared-operation").GetAwaiter().GetResult();
            var second = RhodesMaaRecognitionEvidenceLog.SaveAsync([], [], "operatorsFull", directory,
                scanId: "shared-operation").GetAwaiter().GetResult();
            using var a = JsonDocument.Parse(File.ReadAllText(first));
            using var b = JsonDocument.Parse(File.ReadAllText(second));
            Check(first != second, "each evidence snapshot is preserved");
            Check(a.RootElement.GetProperty("scanId").GetString() == "shared-operation", "capture snapshot correlation");
            Check(b.RootElement.GetProperty("scanId").GetString() == "shared-operation", "state application snapshot correlation");
            Check(a.RootElement.GetProperty("requestId").GetString() != b.RootElement.GetProperty("requestId").GetString(), "snapshot IDs remain unique");
            var operation = new RhodesRecognitionOperation(["operatorsFull"]);
            operation.Complete("completed");
            operation.SaveAsync(directory).GetAwaiter().GetResult();
            var operationFile = Directory.GetFiles(directory, "operation-*.json").Single();
            var original = File.ReadAllText(operationFile);
            var failed = false;
            try { operation.SaveAsync(directory).GetAwaiter().GetResult(); }
            catch (IOException) { failed = true; }
            Check(failed, "existing operation evidence cannot be overwritten");
            Check(File.ReadAllText(operationFile) == original, "failed publish preserves complete evidence");
            Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "failed publish cleans its temporary file");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class TestClock : TimeProvider
    {
        private long _milliseconds;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _milliseconds;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddMilliseconds(_milliseconds);
        public void Advance(long milliseconds) => _milliseconds += milliseconds;
    }
}
