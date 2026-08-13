using MaaFramework.Binding;
using RhodesSuki.Services;

try
{
    var resourceRoot = Path.GetFullPath(
        args.Length > 0
            ? args[0]
            : Path.Combine(Environment.CurrentDirectory, "apps", "rhodes-suki", "resource", "base"));
    if (!Directory.Exists(resourceRoot))
        throw new DirectoryNotFoundException($"MAA Resourceが見つかりません: {resourceRoot}");

    RhodesMaaSession.PrepareNativeRuntime();
    using var resource = new MaaResource();
    if (!resource.SetInference_UseCpu())
        throw new InvalidOperationException("MAA CPU推論設定を適用できませんでした。");

    var status = resource.AppendBundle(resourceRoot).Wait();
    if (status != MaaJobStatus.Succeeded)
        throw new InvalidOperationException($"MAA Resource bundleの読込に失敗しました: {status}");

    var hash = resource.Hash?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(hash))
        throw new InvalidOperationException("MaaResource.Hashが空です。");

    Console.WriteLine($"resourceHash={hash}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"maa-resource-hash: {ex.Message}");
    Environment.ExitCode = 1;
}
