using BepInEx.Configuration;

namespace Spectator
{
    // Presentation preference only: deliberately independent of mission and lobby presets.
    internal static class ToolbarThemePreference
    {
        private static ConfigEntry<bool> classic;
        internal static bool Classic => classic?.Value == true;
        internal static void Initialize(ConfigFile config) => classic = config.Bind("Interface", "ClassicFloatingTheme", false,
            "Use the classic dark Spectator design while the toolbar is floating. Docked tools always use parchment.");
        internal static void Toggle()
        {
            if (classic == null) return;
            classic.Value = !classic.Value;
            classic.ConfigFile.Save();
        }
    }

}
