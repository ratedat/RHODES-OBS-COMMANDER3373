using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRuntimeWorkspaceRegistry
{
    public static SukiRuntimeWorkspaceLayout Layout => RhodesWorkspaceLayoutRegistry.Runtime;
}
