using System;
namespace BugfixesAndQoL
{
    internal sealed class BugfixesAndQoLViewModel
    {
        internal event Action<string> SettingChanged;
        internal bool EnableClientFeatures { get; set; } = true;
        internal int ArabianIdleSoundVolumePercent { get; set; } = 25;
        internal void Notify(string property) => SettingChanged?.Invoke(property);
    }
    internal static class BugfixesAndQoLPlugin { internal const string PluginGuid = "BugfixesAndQoL_Serp"; }
}
namespace Shared
{
    internal static class DebugLogHelper
    {
        internal static int Failures, Messages;
        internal static void LogInfo(BepInEx.Logging.ManualLogSource log, object text) { Messages++; }
        internal static void LogError(BepInEx.Logging.ManualLogSource log, object text) { Failures++; }
    }
}
