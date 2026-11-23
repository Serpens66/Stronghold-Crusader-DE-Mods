using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;

namespace UiAcceptance
{
    internal static partial class PairedResyncSteps
    {
        private static long runOrigin;

        internal static void Register()
        {
            Runner.Register("Paired.Begin", s => GameSteps.Do(() => runOrigin = AcceptLog.Position));
            Runner.Register("Paired.HostSettings", HostSettings);
            Runner.Register("Paired.AddTradingAi", AddTradingAi);
            Runner.Register("Paired.ArmRepair", ArmRepair);
            Runner.Register("Paired.Restart", Restart);
            Runner.Register("Paired.ClientEsc", ClientEsc);
            Runner.Register("Paired.RestartCount", RestartCount);
            Runner.Register("Paired.ExpectMarket", ExpectMarket);
            RegisterMoat();
        }

        private static IEnumerable<Wait> HostSettings(Step step)
        {
            Runner.Require(Runner.Active.Role == "host" && FatControler.currentScene == Enums.SceneIDS.FrontEnd,
                "test settings require the host front end");
            Runner.Require(BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out var mod),
                "BugfixesAndQoL is missing");
            var settings = mod.Instance.GetType().GetProperty("Settings")?.GetValue(mod.Instance);
            Runner.Require(settings != null, "BugfixesAndQoL settings are unavailable");
            foreach (string name in new[] { "EnableMod", "EnableMoatTerrainRestoration", "EnableSingleBuildingPause" })
            {
                var property = settings.GetType().GetProperty(name);
                Runner.Require(property?.PropertyType == typeof(bool) && property.CanWrite, "setting contract differs: " + name);
                property.SetValue(settings, true);
                Runner.Require((bool)property.GetValue(settings), "setting was not applied: " + name);
            }
            yield break;
        }

        private static IEnumerable<Wait> AddTradingAi(Step step)
        {
            var lobby = MainViewModel.Instance.FRONTMultiplayer;
            Runner.Require(lobby?.currentLobby?.isHost == true, "AI setup requires the host lobby");
            int before = lobby.currentLobby.members.Count;
            lobby.SkirmishAIAddClick(((int)AILords.SK_RICHARD).ToString(CultureInfo.InvariantCulture));
            yield return new Until("Richard joined as the trading AI", () => lobby.currentLobby.members.Count == before + 1);
        }

        private static IEnumerable<Wait> ArmRepair(Step step)
        {
            RequireRole(host: false);
            string check = step.Arg(0);
            var entry = GameSteps.FindType("MakeDesync.Plugin")?.GetMethod("TryQueueAcceptance",
                BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(int) }, null);
            Runner.Require(entry?.ReturnType == typeof(bool), "MakeDesync automation entry point is missing");
            int tile = check == "moat" ? SHCDESE.API.GameTileManagerAPI.Instance.GetTileId(MoatX, MoatY) : -1;
            Runner.Require((bool)entry.Invoke(null, new object[] { check, tile }), "MakeDesync declined " + check);
            string success = check == "soil" ? "MAKE_DESYNC_MOAT_FILL .*restored=True" :
                "MAKE_DESYNC_RESYNC_COMPLETED mode=" + (check == "units" ? "vanilla" : "moat") + " .*restored=True";
            var result = new Regex(success, RegexOptions.CultureInvariant);
            yield return new Until("successful " + check, () => AcceptLog.Count(Runner.StepLog, result) == 1);
        }

        private static IEnumerable<Wait> ExpectMarket(Step step)
        {
            int repairs = step.Int(0);
            var start = new Regex("MAKE_DESYNC_MARKET_BOUNDARY stage=start .*pass=True");
            var complete = new Regex("MAKE_DESYNC_MARKET_BOUNDARY stage=complete .*pass=True");
            yield return new Until("verified market boundaries for " + repairs + " repairs",
                () => AcceptLog.Count(runOrigin, start) >= repairs && AcceptLog.Count(runOrigin, complete) >= repairs);
        }

        private static IEnumerable<Wait> ClientEsc(Step step)
        {
            RequireRole(host: false);
            var view = MainViewModel.Instance;
            view.HUDmain.InGameOptions(null, null);
            yield return new Until("client ESC menu", () => view.Show_HUD_IngameMenu);
            Button button = RestartButton();
            Runner.Require(!button.IsEnabled, "the client can use the original Restart Mission button");
            view.ButtonIngameMenuCommand.Execute("8");
            yield return new Until("client ESC menu dismissed", () => !view.Show_HUD_IngameMenu);
        }

        private static IEnumerable<Wait> Restart(Step step)
        {
            RequireRole(host: true);
            bool yes = step.Arg(0) == "yes";
            long oldSession = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            var view = MainViewModel.Instance;
            view.HUDmain.InGameOptions(null, null);
            yield return new Until("host ESC menu", () => view.Show_HUD_IngameMenu);
            yield return new Until("original restart command available", () => RestartButton().IsEnabled);
            Button button = RestartButton();
            button.Command.Execute(button.CommandParameter);
            yield return new Until("original restart confirmation", GameSteps.ConfirmationVisible);
            view.ButtonConfirmationCommand.Execute(yes ? "1" : "2");
            if (!yes)
            {
                yield return new Until("restart confirmation dismissed", () => !GameSteps.ConfirmationVisible());
                if (view.Show_HUD_IngameMenu) view.ButtonIngameMenuCommand.Execute("8");
                int tick = Volatile.Read(ref UiAcceptancePlugin.Ticks);
                yield return new Until("old world continues after cancellation", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) >= tick + 15);
                Runner.Require(oldSession == Interlocked.Read(ref UiAcceptancePlugin.StartedSession), "cancel replaced the mission");
                yield break;
            }
            var completed = new Regex("MP_RESTART_TEST .*RESTART_COMPLETE");
            yield return new Until("restart operation completed", () =>
                Interlocked.Read(ref UiAcceptancePlugin.StartedSession) != oldSession && AcceptLog.Count(Runner.StepLog, completed) > 0);
            int started = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            yield return new Until("restarted world advances", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) >= started + 30);
        }

        private static IEnumerable<Wait> RestartCount(Step step)
        {
            RequireRole(host: false);
            int count = step.Int(0);
            var completed = new Regex("MP_RESTART_TEST .*RESTART_COMPLETE");
            yield return new Until(count + " restart completion markers", () => AcceptLog.Count(runOrigin, completed) >= count);
            int tick = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            yield return new Until("client restarted world advances", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) >= tick + 30);
        }

        private static Button RestartButton()
        {
            var button = MainViewModel.Instance.HUDIngameMenu.FindName("ButtonRestartMission") as Button;
            Runner.Require(button != null && button.IsVisible && button.Command != null &&
                string.Equals(button.CommandParameter as string, "5", StringComparison.Ordinal), "Vanilla restart button binding differs");
            return button;
        }

        private static void RequireRole(bool host)
        {
            var mission = UiAcceptancePlugin.Missions?.Current;
            Runner.Require(mission?.Mode.IsRealMultiplayer == true && mission.IsHost == host,
                "paired test is outside its intended multiplayer role");
        }
    }
}
