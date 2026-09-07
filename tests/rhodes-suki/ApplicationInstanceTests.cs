using RhodesSuki.Services;

public static class ApplicationInstanceTests
{
    public static void DuplicateStartPreservesCurrentRun()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rhodes-instance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "current-state.json");
        const string state = "{\"run\":{\"campaignId\":\"is5_sarkaz\",\"ingot\":42},\"operators\":[\"test-operator\"]}";
        File.WriteAllText(statePath, state);
        try
        {
            WhileRunning(statePath, () =>
            {
                var started = RhodesApplicationInstance.RunIfPrimary(statePath,
                    () => RhodesRunStateStore.PrepareForStartupAsync(statePath).GetAwaiter().GetResult());
                Check(!started, "duplicate startup must not initialize the saved run");
                Check(File.ReadAllText(statePath) == state, "the active run must remain byte-for-byte intact");
                var alias = Path.Combine(root, ".", "current-state.json");
                if (OperatingSystem.IsWindows()) alias = alias.ToUpperInvariant();
                Check(!RhodesApplicationInstance.RunIfPrimary(alias, () => throw new Exception("alias initialized")),
                    "equivalent paths must share the same application lease");
            });
            Check(RhodesApplicationInstance.RunIfPrimary(statePath, () => { }), "a completed application releases its lease");
        }
        finally { Directory.Delete(root, true); }
    }

    public static void SeparateStateFilesRemainIndependent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"rhodes-instance-{Guid.NewGuid():N}");
        WhileRunning(Path.Combine(root, "player-a.json"), () =>
        {
            var ran = false;
            Check(RhodesApplicationInstance.RunIfPrimary(Path.Combine(root, "player-b.json"), () => ran = true),
                "a separate state file may run independently");
            Check(ran, "the independent application must run");
        });
    }

    public static void FailedStartupReleasesInstance()
    {
        var statePath = Path.Combine(Path.GetTempPath(), $"rhodes-failed-instance-{Guid.NewGuid():N}.json");
        try
        {
            RhodesApplicationInstance.RunIfPrimary(statePath, () => throw new InvalidOperationException("test startup failure"));
            throw new Exception("startup error was swallowed");
        }
        catch (InvalidOperationException error) when (error.Message == "test startup failure") { }
        WhileRunning(statePath, () => { });
    }

    private static void WhileRunning(string statePath, Action attemptAnotherStart)
    {
        using var started = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        Exception? failure = null;
        var owner = new Thread(() =>
        {
            try
            {
                Check(RhodesApplicationInstance.RunIfPrimary(statePath, () =>
                {
                    started.Set();
                    Check(finish.Wait(TimeSpan.FromSeconds(10)), "test application timed out");
                }), "the first application acquires the lease");
            }
            catch (Exception error) { failure = error; started.Set(); }
        });
        owner.Start();
        try
        {
            Check(started.Wait(TimeSpan.FromSeconds(10)), "test application did not start");
            if (failure is not null) throw failure;
            attemptAnotherStart();
        }
        finally
        {
            finish.Set();
            Check(owner.Join(TimeSpan.FromSeconds(10)), "test application did not finish");
        }
        if (failure is not null) throw failure;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
