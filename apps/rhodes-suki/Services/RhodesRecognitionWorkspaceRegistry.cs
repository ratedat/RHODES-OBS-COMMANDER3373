using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionWorkspaceRegistry
{
    public static SukiRecognitionWorkspaceLayout Layout => RhodesWorkspaceLayoutRegistry.Recognition;
}
