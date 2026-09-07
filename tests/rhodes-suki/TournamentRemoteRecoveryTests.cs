using System.Text.Json.Nodes;
using RhodesSuki.Services;

namespace RhodesSuki.Tests;

internal static class TournamentRemoteRecoveryTests
{
    public static void StartupRecoveryPreservesRemoteStateUntilClear()
    {
        var state = JsonNode.Parse(
            """
            {
              "version": 1,
              "mode": "tournament",
              "run": { "campaignId": "is5_sarkaz", "ingot": 37 },
              "operators": ["gummy"],
              "relics": ["is5_sarkaz_relic_001"],
              "tournament": { "recoverOnStartup": true }
            }
            """)!.AsObject();

        RhodesRunStateStore.ApplyStartupReset(
            state,
            DateTimeOffset.Parse("2026-09-08T00:00:00Z"));

        Equal("is5_sarkaz", state["run"]!["campaignId"]!.GetValue<string>(), "remote campaign survives startup");
        Equal(37, state["run"]!["ingot"]!.GetValue<int>(), "remote run value survives startup");
        Equal("gummy", state["operators"]![0]!.GetValue<string>(), "remote operator survives startup");
        Equal(true, state["tournament"]!["recoverOnStartup"]!.GetValue<bool>(), "recovery marker survives startup");

        var apiCleared = JsonNode.Parse(RhodesStateApiClient.ClearCurrentRunInStateJson(state.ToJsonString()))!.AsObject();
        Equal(false, apiCleared["tournament"]!["recoverOnStartup"]!.GetValue<bool>(), "API run clear releases startup recovery");

        var tempRoot = Path.Combine(Path.GetTempPath(), $"rhodes-remote-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var statePath = Path.Combine(tempRoot, "current-state.json");
            File.WriteAllText(statePath, state.ToJsonString());
            RhodesRunStateStore.ClearCurrentRunAsync(statePath).GetAwaiter().GetResult();
            var localCleared = JsonNode.Parse(File.ReadAllText(statePath))!.AsObject();
            Equal(false, localCleared["tournament"]!["recoverOnStartup"]!.GetValue<bool>(), "local run clear releases startup recovery");
        }
        finally
        {
            Directory.Delete(tempRoot, true);
        }
    }

    public static void RemotePollingUsesDisplayOnlyState()
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root, "apps", "rhodes-suki", "ViewModels", "MainWindowViewModel.cs");
        var source = File.ReadAllText(path);
        var methodStart = source.IndexOf("private async Task ImportTournamentRemoteStateAsync", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void ApplyTournamentQuickResult", methodStart, StringComparison.Ordinal);
        if (methodStart < 0 || methodEnd <= methodStart)
            throw new InvalidOperationException("Tournament remote import method was not found.");

        var method = source[methodStart..methodEnd];
        Equal(true, method.Contains("FetchAsync(RhodesApiUrl", StringComparison.Ordinal), "remote import fetches the API snapshot");
        Equal(true, method.Contains("ReloadRunStateFromJson", StringComparison.Ordinal), "remote import projects the fetched snapshot into the UI");
        Equal(false, method.Contains("SyncRunStateFromApiCoreAsync", StringComparison.Ordinal), "remote import does not use the disk replacing manual sync path");
        Equal(false, method.Contains("ReplaceStateJsonAsync", StringComparison.Ordinal), "remote import does not write an older GET snapshot back to disk");

        var restoreStart = source.IndexOf("private void RefreshChoicesFromRunState", StringComparison.Ordinal);
        var restoreEnd = source.IndexOf("private void ReloadRunStateFromStore", restoreStart, StringComparison.Ordinal);
        var restore = source[restoreStart..restoreEnd];
        Equal(true, restore.Contains("_isRestoringChoiceState = true", StringComparison.Ordinal), "choice projection enters restore mode");
        Equal(true, restore.Contains("_isRestoringChoiceState = false", StringComparison.Ordinal), "choice projection leaves restore mode");
    }

    public static void TrackerUsesAppliedSequence()
    {
        var tracker = new RhodesTournamentRemoteStateTracker();
        var acknowledged = RhodesTournamentRemoteApiClient.ParseStatusJson(
            """{ "active": true, "sessionId": "session-a", "cursor": 0, "appliedSequence": 0 }""");
        tracker.MarkImported(acknowledged);

        var savedBeforeAck = RhodesTournamentRemoteApiClient.ParseStatusJson(
            """{ "active": true, "sessionId": "session-a", "cursor": 0, "appliedSequence": 1 }""");
        var property = savedBeforeAck.GetType().GetProperty("AppliedSequence");
        Equal(true, property is not null, "status exposes the saved operation sequence");
        Equal(1L, property!.GetValue(savedBeforeAck), "status parses appliedSequence");
        Equal(true, tracker.ShouldImport(savedBeforeAck), "saved operation imports before ACK advances cursor");

        tracker.MarkImported(savedBeforeAck);
        var acknowledgedLater = RhodesTournamentRemoteApiClient.ParseStatusJson(
            """{ "active": true, "sessionId": "session-a", "cursor": 1, "appliedSequence": 1 }""");
        Equal(false, tracker.ShouldImport(acknowledgedLater), "later ACK does not re-import the same saved operation");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var origin in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var current = new DirectoryInfo(origin); current is not null; current = current.Parent)
            {
                if (File.Exists(Path.Combine(current.FullName, "apps", "rhodes-suki", "RhodesSuki.csproj")))
                    return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("RHODES repository root was not found.");
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }
}
