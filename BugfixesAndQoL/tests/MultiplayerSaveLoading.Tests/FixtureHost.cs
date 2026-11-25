namespace BugfixesAndQoL
{
    // External plugin/settings boundary only. Production VM ownership is exercised
    // separately by Modsettings.Tests against the complete mainmod DLL.
    internal static class BugfixesAndQoLPlugin
    { internal const string PluginGuid = "BugfixesAndQoL_Serp"; }
    internal sealed class BugfixesAndQoLViewModel
    { public bool EnableIngameMultiplayerSaveLoading { get; set; } = true; }
}
