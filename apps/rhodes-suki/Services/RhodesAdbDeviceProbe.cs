using System.Diagnostics;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbDeviceProbe
{
    public static async Task<IReadOnlyList<MaaAdbDevicePreview>> ListDevicesAsync(string adbPath, CancellationToken cancellationToken = default)
    {
        var result = await RunAdbAsync(string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath.Trim(), cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.Error) ? $"adb devices failed: {result.ExitCode}" : result.Error.Trim());

        return ParseDevices(result.Output);
    }

    public static IReadOnlyList<MaaAdbDevicePreview> ParseDevices(string output)
    {
        var devices = new List<MaaAdbDevicePreview>();
        foreach (var rawLine in (output ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase) || line.StartsWith("* daemon", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            devices.Add(new MaaAdbDevicePreview(
                parts[0],
                parts[1],
                string.Join(" ", parts.Skip(2))));
        }

        return devices;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunAdbAsync(string adbPath, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        process.StartInfo.ArgumentList.Add("devices");
        process.StartInfo.ArgumentList.Add("-l");

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, await outputTask, await errorTask);
    }
}
