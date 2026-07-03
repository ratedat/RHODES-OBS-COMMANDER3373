using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSukiRuntimeProbeSnapshot(
    SukiOptionalRuntimeStatus Api,
    SukiOptionalRuntimeStatus Master,
    SukiOptionalRuntimeStatus Glm,
    SukiOptionalRuntimeStatus Ollama,
    SukiHypervisorStatus Hypervisor,
    string StatusMessage);

public static class RhodesSukiRuntimeProbeWorkflow
{
    public static async Task<RhodesSukiRuntimeProbeSnapshot> ProbeAsync(
        Func<CancellationToken, Task<SukiOptionalRuntimeStatus>> probeApiAsync,
        Func<CancellationToken, Task<SukiOptionalRuntimeStatus>> probeMasterAsync,
        Func<CancellationToken, Task<SukiOptionalRuntimeProbeSnapshot>> probeOptionalRuntimesAsync,
        Func<CancellationToken, Task<SukiHypervisorStatus>> probeHypervisorAsync,
        CancellationToken cancellationToken = default)
    {
        var apiTask = probeApiAsync(cancellationToken);
        var masterTask = probeMasterAsync(cancellationToken);
        var optionalTask = probeOptionalRuntimesAsync(cancellationToken);
        var hypervisorTask = probeHypervisorAsync(cancellationToken);

        await Task.WhenAll(apiTask, masterTask, optionalTask, hypervisorTask);

        var optional = optionalTask.Result;
        var api = apiTask.Result;
        var master = masterTask.Result;
        var hypervisor = hypervisorTask.Result;
        return new RhodesSukiRuntimeProbeSnapshot(
            api,
            master,
            optional.Glm,
            optional.Ollama,
            hypervisor,
            $"ランタイム状態: API={api.State}, Master={master.State}, GLM={optional.Glm.State}, Ollama={optional.Ollama.State}, Hyper-V={hypervisor.State}");
    }
}
