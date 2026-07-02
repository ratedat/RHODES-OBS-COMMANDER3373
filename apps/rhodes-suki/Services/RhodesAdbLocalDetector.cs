using System.Diagnostics;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesAdbCommandResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;

    public string Detail => string.IsNullOrWhiteSpace(Error) ? Output.Trim() : Error.Trim();
}

public sealed record RhodesAdbConnectPreview(string Address, bool Succeeded, string Detail);

public sealed record RhodesAdbLocalDetectionResult(
    string SelectedAdbPath,
    string RuntimeAdbPath,
    string RuntimeSerial,
    IReadOnlyList<MaaAdbPathCandidatePreview> AdbCandidates,
    IReadOnlyList<MaaAdbDevicePreview> Devices,
    RhodesAdbConnectPreview? Connect,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);
}

public static class RhodesAdbLocalDetector
{
    public static async Task<RhodesAdbLocalDetectionResult> DetectAsync(
        RhodesAdbApiSettings settings,
        Func<string, bool>? fileExists = null,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<RhodesAdbCommandResult>>? runCommand = null,
        CancellationToken cancellationToken = default)
    {
        fileExists ??= File.Exists;
        runCommand ??= RunAdbCommandAsync;

        var normalizedPreset = string.IsNullOrWhiteSpace(settings.ConnectionPreset) ? "auto" : settings.ConnectionPreset.Trim();
        var candidates = RhodesAdbPresetCatalog.CandidatePaths(settings.AdbPath, normalizedPreset);
        var checkedCandidates = new List<MaaAdbPathCandidatePreview>();
        foreach (var candidate in candidates)
        {
            var isPathAdb = candidate.Path.Equals("adb", StringComparison.OrdinalIgnoreCase);
            var exists = isPathAdb || fileExists(candidate.Path);
            var available = false;
            var candidateError = "";
            if (exists)
            {
                try
                {
                    var version = await runCommand(candidate.Path, ["version"], cancellationToken);
                    available = version.Succeeded;
                    candidateError = version.Succeeded ? "" : Shorten(version.Detail, 160);
                }
                catch (Exception ex)
                {
                    candidateError = Shorten(ex.Message, 160);
                }
            }

            checkedCandidates.Add(candidate with
            {
                Exists = exists,
                Available = available,
                Error = candidateError,
            });
        }

        var ordered = SortCandidates(checkedCandidates, settings).ToArray();
        var selected = ordered.FirstOrDefault(candidate => candidate.Available);
        if (selected is null)
        {
            return new RhodesAdbLocalDetectionResult(
                "",
                string.IsNullOrWhiteSpace(settings.AdbPath) ? "adb" : settings.AdbPath.Trim(),
                settings.Serial.Trim(),
                ordered,
                [],
                null,
                "利用可能なADB実行ファイルが見つかりません。ADB候補または手動選択を確認してください。");
        }

        var effectivePreset = EffectivePreset(normalizedPreset, selected.Preset);
        var serialCandidates = BuildSerialCandidates(settings.Serial, effectivePreset);
        var connect = await TryConnectTcpSerialsAsync(selected.Path, serialCandidates, runCommand, cancellationToken);
        IReadOnlyList<MaaAdbDevicePreview> devices = [];
        string devicesError = "";
        try
        {
            var devicesResult = await runCommand(selected.Path, ["devices", "-l"], cancellationToken);
            if (devicesResult.Succeeded)
                devices = RhodesAdbDeviceProbe.ParseDevices(devicesResult.Output);
            else
                devicesError = Shorten(devicesResult.Detail, 180);
        }
        catch (Exception ex)
        {
            devicesError = Shorten(ex.Message, 180);
        }

        var serial = FirstNonEmpty(
            settings.Serial,
            SelectDetectedSerial(devices, serialCandidates),
            devices.FirstOrDefault(device => device.IsUsable)?.Serial ?? "");
        var error = string.IsNullOrWhiteSpace(devicesError)
            ? ""
            : $"ADB devices取得失敗: {devicesError}";
        return new RhodesAdbLocalDetectionResult(
            selected.Path,
            selected.Path,
            serial,
            ordered,
            devices,
            connect,
            error);
    }

    internal static IReadOnlyList<string> BuildSerialCandidates(string configuredSerial, string presetId)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        PushSerial(result, seen, configuredSerial);
        foreach (var serial in RhodesAdbPresetCatalog.DefaultSerials(presetId))
            PushSerial(result, seen, serial);
        return result;
    }

    internal static IReadOnlyList<MaaAdbPathCandidatePreview> SortCandidates(
        IEnumerable<MaaAdbPathCandidatePreview> candidates,
        RhodesAdbApiSettings settings)
    {
        var explicitPath = NormalizePathKey(settings.AdbPath);
        var preset = string.IsNullOrWhiteSpace(settings.ConnectionPreset) ? "auto" : settings.ConnectionPreset.Trim();
        return candidates
            .OrderBy(candidate => CandidateScore(candidate, explicitPath, preset))
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int CandidateScore(MaaAdbPathCandidatePreview candidate, string explicitPath, string preset)
    {
        var score = candidate.Available ? 0 : 1000;
        var candidatePath = NormalizePathKey(candidate.Path);
        if (!string.IsNullOrWhiteSpace(explicitPath) && candidatePath == explicitPath)
            score -= 120;
        if (candidate.Source.Equals("settings", StringComparison.OrdinalIgnoreCase))
            score -= 90;
        if (candidate.Source.Equals("env", StringComparison.OrdinalIgnoreCase))
            score -= 80;
        if (!preset.Equals("auto", StringComparison.OrdinalIgnoreCase))
            score += candidate.Preset.Equals(preset, StringComparison.OrdinalIgnoreCase) ? -60 : 35;
        else
        {
            if (candidate.Preset.Equals("mumu", StringComparison.OrdinalIgnoreCase))
                score -= 45;
            if (candidate.Preset.Equals("bluestacks", StringComparison.OrdinalIgnoreCase))
                score += 35;
            if (candidate.Preset.Equals("ldplayer", StringComparison.OrdinalIgnoreCase))
                score += 20;
        }
        if (candidatePath == "adb")
            score += 60;
        return score;
    }

    private static async Task<RhodesAdbConnectPreview?> TryConnectTcpSerialsAsync(
        string adbPath,
        IReadOnlyList<string> serialCandidates,
        Func<string, IReadOnlyList<string>, CancellationToken, Task<RhodesAdbCommandResult>> runCommand,
        CancellationToken cancellationToken)
    {
        RhodesAdbConnectPreview? last = null;
        foreach (var serial in serialCandidates.Where(IsTcpSerial))
        {
            try
            {
                var result = await runCommand(adbPath, ["connect", serial], cancellationToken);
                last = new RhodesAdbConnectPreview(serial, result.Succeeded, Shorten(result.Detail, 180));
                if (result.Succeeded)
                    return last;
            }
            catch (Exception ex)
            {
                last = new RhodesAdbConnectPreview(serial, false, Shorten(ex.Message, 180));
            }
        }

        return last;
    }

    private static string EffectivePreset(string preset, string selectedPreset)
    {
        if (!preset.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return preset;
        return selectedPreset is "auto" or "custom" or ""
            ? preset
            : selectedPreset;
    }

    private static string SelectDetectedSerial(IEnumerable<MaaAdbDevicePreview> devices, IReadOnlyList<string> serialCandidates)
    {
        var usable = devices.Where(device => device.IsUsable).ToArray();
        foreach (var serial in serialCandidates)
        {
            var match = usable.FirstOrDefault(device => device.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Serial;
        }
        return "";
    }

    private static bool IsTcpSerial(string value)
    {
        var text = value.Trim();
        var colon = text.LastIndexOf(':');
        return colon > 0
            && colon + 1 < text.Length
            && text[..colon].Contains('.')
            && int.TryParse(text[(colon + 1)..], out _);
    }

    private static void PushSerial(ICollection<string> result, ISet<string> seen, string? value)
    {
        var serial = string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        if (serial.Length > 0 && seen.Add(serial))
            result.Add(serial);
    }

    private static async Task<RhodesAdbCommandResult> RunAdbCommandAsync(
        string adbPath,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(adbPath) ? "adb" : adbPath.Trim(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        return new RhodesAdbCommandResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string NormalizePathKey(string value)
    {
        return (value ?? "").Trim().Replace("\\.\\", "\\", StringComparison.Ordinal).Replace('\\', '/').ToLowerInvariant();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static string Shorten(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var text = value.Trim().ReplaceLineEndings(" ");
        return text.Length <= maxLength ? text : $"{text[..maxLength]}...";
    }
}
