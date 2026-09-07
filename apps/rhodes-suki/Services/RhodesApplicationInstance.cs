using System.Security.Cryptography;
using System.Text;

namespace RhodesSuki.Services;

public static class RhodesApplicationInstance
{
    public static bool RunIfPrimary(string statePath, Action runApplication)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(runApplication);
        var identity = Path.GetFullPath(statePath);
        if (OperatingSystem.IsWindows())
            identity = identity.ToUpperInvariant();
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var name = $"{(OperatingSystem.IsWindows() ? @"Local\" : "")}RHODES-State-{key}";
        using var mutex = new Mutex(false, name);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // The previous process exited unexpectedly; this thread now owns the mutex.
                acquired = true;
            }
            if (!acquired)
                return false;

            runApplication();
            return true;
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }
}
