using APIShared.ModSettings;
using System;

namespace Shared
{
    /// <summary>Serps gameplay-policy notice; APIShared only supplies the reusable presentation contract.</summary>
    internal static class DirectLaunchSettingsNotice
    {
        internal static void Configure(PresetLobbyModSettingsViewModel settings, bool blueprintsRemainAvailable = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.System_ConfigureDirectLaunchNotice(() => ResolveText(blueprintsRemainAvailable));
        }

        private static string ResolveText(bool blueprintsRemainAvailable)
        {
            bool german = SerpLocalization.GetActiveLocale().StartsWith("de", StringComparison.OrdinalIgnoreCase);
            if (blueprintsRemainAvailable)
                return german
                    ? "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert."
                    : "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.";
            return german
                ? "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert."
                : "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.";
        }
    }
}
