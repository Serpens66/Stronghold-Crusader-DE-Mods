using System;
using System.Collections.Generic;
using SpectatorEditorBuildTest;
namespace APIShared { public static class PlayerPerspectiveAPI { public static int Viewed = 2; public static int GetViewedPlayerId() { return Viewed; } } }
namespace BepInEx.Bootstrap
{
    public sealed class Info { public object Instance = new BugfixesAndQoL.Plugin(); }
    public static class Chainloader { public static readonly Dictionary<string, Info> PluginInfos = new Dictionary<string, Info>(); }
}
namespace BugfixesAndQoL
{
    public sealed class Plugin { }
    internal static class SpectatorPerspectiveRuntime
    {
        internal static bool Active = true, Reject, Throw;
        internal static int Calls, Reports, Portrait;
        internal static bool IsActiveSpectator() { return Active; }
        private static void SelectPlayer(int player)
        {
            Calls++;
            if (Throw) throw new Exception("fixture");
            if (Reject) return;
            APIShared.PlayerPerspectiveAPI.Viewed = Portrait = player;
            Reports++;
        }
    }
}
namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private sealed class Log { internal void LogWarning(string text) { Warnings++; } }
        private static readonly Log log = new Log();
        internal static int Warnings, Owner = -1, Changes;
        internal static bool Session = true;
        private static bool IsSpectatorSession() { return Session; }
        private static bool IsSelectableAi(int id) { return id >= 2 && id <= 8; }
        private static void ApplyHumanAi(int id) { if (id != Owner) { Owner = id; Changes++; } }
        internal static bool Sync() { SyncSpectatorSelection(); return spectatorSelectionLinked; }
        internal static bool Select(int id) { return spectatorSelection.TrySelect(id); }
    }
}
internal static class Tests
{
    private static int checks;
    private static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
    private static int Main()
    {
        try
        {
            Check(!SpectatorEditorBuildTestPlugin.Sync(), "provider absent");
            BepInEx.Bootstrap.Chainloader.PluginInfos.Add("BugfixesAndQoL_Serp", new BepInEx.Bootstrap.Info());
            Check(SpectatorEditorBuildTestPlugin.Sync() && SpectatorEditorBuildTestPlugin.Owner == 2, "initial perspective adopted");
            int changes = SpectatorEditorBuildTestPlugin.Changes;
            for (int i = 0; i < 100; i++) SpectatorEditorBuildTestPlugin.Sync();
            Check(SpectatorEditorBuildTestPlugin.Changes == changes && BugfixesAndQoL.SpectatorPerspectiveRuntime.Calls == 0, "idle no changes or feedback");
            APIShared.PlayerPerspectiveAPI.Viewed = 3;
            Check(SpectatorEditorBuildTestPlugin.Sync() && SpectatorEditorBuildTestPlugin.Owner == 3, "spectator to testmod");
            Check(SpectatorEditorBuildTestPlugin.Select(4), "testmod to spectator accepted");
            SpectatorEditorBuildTestPlugin.Sync();
            Check(SpectatorEditorBuildTestPlugin.Owner == 4 && BugfixesAndQoL.SpectatorPerspectiveRuntime.Portrait == 4 && BugfixesAndQoL.SpectatorPerspectiveRuntime.Reports == 1, "provider updates all consumers");
            Check(SpectatorEditorBuildTestPlugin.Select(4) && BugfixesAndQoL.SpectatorPerspectiveRuntime.Calls == 1, "same selection no duplicate provider invocation");
            int queuedRecipient = SpectatorEditorBuildTestPlugin.Owner;
            APIShared.PlayerPerspectiveAPI.Viewed = 5;
            SpectatorEditorBuildTestPlugin.Sync();
            Check(queuedRecipient == 4 && SpectatorEditorBuildTestPlugin.Owner == 5, "captured recipient stays fixed");
            foreach (int invalid in new[] { 1, 0, -1, 9 })
            {
                APIShared.PlayerPerspectiveAPI.Viewed = invalid;
                Check(SpectatorEditorBuildTestPlugin.Sync() && SpectatorEditorBuildTestPlugin.Owner == -1, "invalid or human viewpoint does not fall back");
            }
            APIShared.PlayerPerspectiveAPI.Viewed = 6;
            SpectatorEditorBuildTestPlugin.Owner = -1; // HUD recreation
            Check(SpectatorEditorBuildTestPlugin.Sync() && SpectatorEditorBuildTestPlugin.Owner == 6, "HUD recreated");
            SpectatorEditorBuildTestPlugin.Session = false;
            Check(!SpectatorEditorBuildTestPlugin.Sync(), "normal game/editor/multiplayer excluded by session gate");
            SpectatorEditorBuildTestPlugin.Session = true;
            BugfixesAndQoL.SpectatorPerspectiveRuntime.Active = false;
            Check(!SpectatorEditorBuildTestPlugin.Sync(), "disabled provider standalone");
            BugfixesAndQoL.SpectatorPerspectiveRuntime.Active = true;
            APIShared.PlayerPerspectiveAPI.Viewed = 7;
            Check(SpectatorEditorBuildTestPlugin.Sync() && SpectatorEditorBuildTestPlugin.Owner == 7, "new session restored perspective");
            BugfixesAndQoL.SpectatorPerspectiveRuntime.Reject = true;
            Check(!SpectatorEditorBuildTestPlugin.Select(8) && APIShared.PlayerPerspectiveAPI.Viewed == 7, "rejection does not change selection");
            Check(!SpectatorEditorBuildTestPlugin.Select(0), "invalid request rejected");
            BugfixesAndQoL.SpectatorPerspectiveRuntime.Throw = true;
            Check(!SpectatorEditorBuildTestPlugin.Select(8), "provider exception isolated");
            for (int i = 0; i < 100; i++) SpectatorEditorBuildTestPlugin.Sync();
            Check(SpectatorEditorBuildTestPlugin.Warnings == 1, "fault logs once");
            int warnings = 0;
            var incompatible = new SpectatorSelectionAdapter(typeof(Tests), () => 2, message => warnings++);
            int ignored;
            Check(!incompatible.TryRead(out ignored) && warnings == 1, "incompatible signatures disabled once");
            Console.WriteLine("PASS: " + checks + " spectator selection integration checks.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
