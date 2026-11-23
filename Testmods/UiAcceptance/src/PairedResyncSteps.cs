using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CrusaderDE;
using Noesis;
using SHCDESE.Interop;
using Steamworks;

namespace UiAcceptance
{
    internal static partial class PairedResyncSteps
    {
        private static long runOrigin;
        private const string TradingLord = "SerpMarketRest4";

        internal static void Register()
        {
            Runner.Register("Paired.Begin", s => GameSteps.Do(() => runOrigin = AcceptLog.Position));
            Runner.Register("Paired.HostSettings", HostSettings);
            Runner.Register("Paired.AddTradingAi", AddTradingAi);
            Runner.Register("Paired.VerifyTradingAi", VerifyTradingAi);
            Runner.Register("Paired.NoPeaceTime", NoPeaceTime);
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
            var session = lobby.currentLobby;
            string lord = step.Arg(0);
            var before = session.members.ToArray();
            Runner.Require(before.Length == 2 && session.CountHumanPlayers() == 2, "expected two humans before adding the test AI");

            lobby.SkirmishAIAddClick("98"); // Open the built-in custom-lord picker.
            var list = lobby.FindName("CustomLordList") as ListView;
            Runner.Require(list?.ItemsSource != null, "custom lord picker is unavailable");
            var rows = list.ItemsSource.Cast<object>().OfType<FileRow>()
                .Where(row => string.Equals(row.lord?.lordName, lord, StringComparison.Ordinal)).ToArray();
            Runner.Require(lord == TradingLord && rows.Length == 1 && !rows[0].lord.workshop &&
                rows[0].lord.aivs.Count == 1 && rows[0].lord.configs.Count == 1,
                "expected one local trading lord with exactly one AIV and one AIC: " + lord);
            list.SelectedItem = rows[0];
            lobby.ButtonClicked("AddCustomLord");
            yield return new Until("the custom trading AI joined", () => session.members.Count == before.Length + 1);
            var ai = session.members.Single(member => !before.Contains(member));
            Runner.Require(ai.customLordName == lord && session.CountAIPlayers() == 1, "unexpected AI after adding " + lord);

            var host = before.Single(member => member.IsSelf());
            if (session.getTeam(ai) != session.getTeam(host))
            {
                lobby.ButtonClicked("ShowTeams");
                try
                {
                    var order = GameSteps.Read(lobby.GetType(), lobby, "orderTeamMembers",
                        BindingFlags.Instance | BindingFlags.NonPublic) as Platform_Multiplayer.MPLobbyMember[];
                    Runner.Require(order != null, "the team picker did not expose its face order");
                    int aiFace = Array.IndexOf(order, ai);
                    int hostFace = Array.IndexOf(order, host);
                    Runner.Require(aiFace >= 0 && hostFace >= 0, "host or AI is absent from the team picker");
                    lobby.ButtonClicked("TeamFace" + (aiFace + 1).ToString(CultureInfo.InvariantCulture));
                    lobby.ButtonClicked("TeamFace" + (hostFace + 1).ToString(CultureInfo.InvariantCulture));
                }
                finally
                {
                    MainViewModel.Instance.Show_SkirmishTeamsPanel = false;
                }
            }
            var client = before.Single(member => !member.IsSelf());
            Runner.Require(session.getTeam(host) > 0 && session.getTeam(ai) == session.getTeam(host) &&
                session.getTeam(client) != session.getTeam(host), "expected the host and trading AI to face the client");
        }

        private static IEnumerable<Wait> ArmRepair(Step step)
        {
            RequireRole(host: false);
            string check = step.Arg(0);
            if (check != "units") { RequireTerrainAvailable(); RequireMoat(); }
            var entry = GameSteps.FindType("MakeDesync.Plugin")?.GetMethod("TryQueueAcceptance",
                BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(int) }, null);
            Runner.Require(entry?.ReturnType == typeof(bool), "MakeDesync automation entry point is missing");
            int tile = check == "moat" ? SHCDESE.API.GameTileManagerAPI.Instance.GetTileId(moat.X, moat.Y) : -1;
            Runner.Require((bool)entry.Invoke(null, new object[] { check, tile }), "MakeDesync declined " + check);
            string success = check == "soil" ? "MAKE_DESYNC_MOAT_FILL .*restored=True" :
                "MAKE_DESYNC_RESYNC_COMPLETED mode=" + (check == "units" ? "vanilla" : "moat") + " .*restored=True";
            var result = new Regex(success, RegexOptions.CultureInvariant);
            yield return new Until("successful " + check, () =>
            {
                if (check != "units") RequireTerrainAvailable();
                return AcceptLog.Count(Runner.StepLog, result) == 1;
            });
        }

        private static IEnumerable<Wait> VerifyTradingAi(Step step)
        {
            yield return new Until("the agreed custom trading AI is available", () =>
                CheckTradingProfile(MainViewModel.Instance.FRONTMultiplayer));
        }

        private static bool CheckTradingProfile(FRONT_Multiplayer lobby)
        {
            var session = lobby?.currentLobby;
            if (session == null || !session.id.IsValid() || session.CountHumanPlayers() != 2 || session.CountAIPlayers() != 1)
                return false;
            ulong hostId = SteamMatchmaking.GetLobbyOwner(session.id).m_SteamID;
            var members = session.members.Where(member => !member.dummyToBeKicked).ToArray();
            var host = members.SingleOrDefault(member => member.GetSteamID() == hostId);
            var ai = members.SingleOrDefault(member => member.SkirmishMember && !member.SkirmishHumanMember);
            if (host == null || ai == null || session.getTeam(host) <= 0 || session.getTeam(ai) != session.getTeam(host))
                return false;
            int aiIndex = session.getThisPlayerFromSteamID(ai.GetSteamID()) - 1;
            string[] profiles = { null, session.AIVDataPlayer2, session.AIVDataPlayer3, session.AIVDataPlayer4,
                session.AIVDataPlayer5, session.AIVDataPlayer6, session.AIVDataPlayer7, session.AIVDataPlayer8 };
            if (aiIndex <= 0 || aiIndex >= profiles.Length || string.IsNullOrEmpty(profiles[aiIndex])) return false;
            string payload = profiles[aiIndex];
            var received = new FRONT_Multiplayer.MPAIVInfo();
            received.decode(payload);
            if (received.lordName != TradingLord || received.builtIn || received.builtInLord ||
                received.community || received.historical || received.rotation != 0 ||
                received.aivs.Count != 1 || received.aivs[0].data?.Length is not > 0 || received.lordConfig == null) return false;
            var aic = received.lordConfig.lordData;
            if (aic.buy_weapons != 5 || aic.poleturners_make != -999 ||
                aic.economy_protection_type != (int)eChimps.CHIMP_TYPE_SPEARMAN) return false;

            using var sha = SHA256.Create();
            string digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "");
            if (session.isHost)
            {
                if (lobby.AIVs[aiIndex].encode() != payload) return false;
            }
            else
            {
                string path = CheckpointPath("host-setup");
                string[] expected = System.IO.File.ReadAllLines(path);
                if (expected.Length != 2 || expected[0] != AcceptLog.RunId || expected[1] != digest) return false;
            }
            AcceptLog.Info($"PAIRED_AI_PROFILE run={AcceptLog.RunId} player={aiIndex + 1} lord={TradingLord} digest={digest}");
            return true;
        }

        private static IEnumerable<Wait> NoPeaceTime(Step step)
        {
            var lobby = MainViewModel.Instance.FRONTMultiplayer;
            var setup = GameSteps.Read(lobby.GetType(), lobby, "MPsetupData",
                BindingFlags.Instance | BindingFlags.NonPublic) as EngineInterface.MultiplayerSetupData;
            Runner.Require(setup != null && setup.peacetime == 0, "the host did not apply zero minutes of peace time");
            yield break;
        }

        private static IEnumerable<Wait> ExpectMarket(Step step)
        {
            int repairs = step.Int(0);
            var start = new Regex("MAKE_DESYNC_MARKET_BOUNDARY stage=start .*pass=True");
            var complete = new Regex("MAKE_DESYNC_MARKET_BOUNDARY stage=complete .*pass=True");
            yield return new Until("verified market boundaries for " + repairs + " repairs", () =>
                AcceptLog.Count(runOrigin, start) >= repairs && AcceptLog.Count(runOrigin, complete) >= repairs);
            if (step.Arg(1, "") == "partial")
            {
                var partial = new Regex("MAKE_DESYNC_MARKET_BOUNDARY stage=start .*partialBefore=[1-9][0-9]* .*pass=True");
                Runner.Require(AcceptLog.Count(runOrigin, partial) > 0, "no repair started with a partially fulfilled five-item market order");
            }
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
