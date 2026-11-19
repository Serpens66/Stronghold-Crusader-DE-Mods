using System;

namespace BugfixesAndQoL
{
    // Only settings and logging are doubles. The patch, Harmony, SafeTranspiler and HUD IL are real.
    internal sealed class BugfixesAndQoLViewModel
    {
        internal event Action<string> SettingChanged;
        internal bool EnableMod { get; set; } = true;
        internal bool EnableClientFeatures { get; set; } = true;
        internal bool EnableTroopPanelPageCountFix { get; set; } = true;
        internal void Notify(string property) => SettingChanged?.Invoke(property);
    }
    internal static class BugfixesAndQoLPlugin { internal const string PluginGuid = "BugfixesAndQoL_Serp"; }
}
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, object text) { }
        internal static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, object text) { }
    }
}
