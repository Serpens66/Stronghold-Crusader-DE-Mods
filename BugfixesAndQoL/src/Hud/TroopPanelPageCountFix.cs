using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using System;
using System.Reflection;
using System.Threading;

namespace BugfixesAndQoL
{
    // Vanilla's HUD_Troops.SetupSelectedTroops only raises its private page count (pages = 2/3/4 at the
    // 9th/18th/27th selected type) and never lowers it. After one large selection, every later selection
    // keeps the page arrows and TogglePages can open empty pages. Resetting the count to Vanilla's initial
    // value 1 before each run lets the unchanged original recompute it (baseline HUD_Troops.cs, FBCB9319).
    // APIShared hooks the same method with a MonoMod Hook around the original and never reads or writes
    // pages; this Harmony prefix chains with it. Fixes mod and Script Extender do not patch this method.
    // The prefix stays installed for the process lifetime; the setting only switches it logically.
    internal static class TroopPanelPageCountFix
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.TroopPanelPageCount");
        private static AccessTools.FieldRef<HUD_Troops, int> pagesField;
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static int attempted;
        private static int enabled;

        internal static void Install(ManualLogSource logger, BugfixesAndQoLViewModel viewModel)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            log = logger;
            try
            {
                settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                FieldInfo pages = AccessTools.DeclaredField(typeof(HUD_Troops), "pages");
                if (pages == null || pages.IsStatic || pages.FieldType != typeof(int))
                    throw new MissingFieldException("HUD_Troops.pages differs from the audit.");
                pagesField = AccessTools.FieldRefAccess<HUD_Troops, int>(pages);
                MethodInfo setup = AccessTools.DeclaredMethod(typeof(HUD_Troops), "SetupSelectedTroops", Type.EmptyTypes);
                if (setup == null || setup.IsStatic || setup.ReturnType != typeof(void))
                    throw new MissingMethodException("HUD_Troops.SetupSelectedTroops signature differs from the audit.");
                harmony.Patch(setup, prefix: new HarmonyMethod(typeof(TroopPanelPageCountFix), nameof(BeforeSetupSelectedTroops)));
                settings.SettingChanged += OnSettingChanged;
                RefreshEnabled();
                Shared.DebugLogHelper.LogInfo(log, "TROOP_PANEL_PAGE_COUNT_FIX_INSTALLED: permanent HUD_Troops prefix; enabled=" +
                    (Volatile.Read(ref enabled) != 0));
            }
            catch (Exception exception)
            {
                Volatile.Write(ref enabled, 0);
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/TroopPanelPageCountFix/install",
                    "TROOP_PANEL_PAGE_COUNT_FIX_DISABLED: Vanilla behavior retained; " + exception);
            }
        }

        private static void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableTroopPanelPageCountFix))
                RefreshEnabled();
        }

        private static void RefreshEnabled() => Volatile.Write(ref enabled,
            pagesField != null && settings.EnableMod && settings.EnableClientFeatures &&
            settings.EnableTroopPanelPageCountFix ? 1 : 0);

        private static void BeforeSetupSelectedTroops(HUD_Troops __instance)
        {
            if (Volatile.Read(ref enabled) == 0) return;
            try { pagesField(__instance) = 1; }
            catch (Exception exception)
            {
                // Never let the fix break Vanilla's HUD refresh; the original still runs with its own count.
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/TroopPanelPageCountFix/prefix",
                    "TROOP_PANEL_PAGE_COUNT_FIX_FAILED: " + exception);
            }
        }
    }
}
