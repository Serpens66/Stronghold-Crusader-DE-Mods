using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CrusaderDE;
using Steamworks;

namespace UiAcceptance
{
    // Skirmish/editor setup through Vanilla's public lobby and menu commands, plus real multiplayer
    // host/client flow. All gameplay changes use Vanilla or mod commands; no own network packets.
    internal static class LobbySteps
    {
        internal static void Register()
        {
            Runner.Register("Skirmish.Open", SkirmishOpen);
            Runner.Register("Lobby.SelectMap", SelectMap);
            Runner.Register("Lobby.AddAI", s => Do(view => view.SkirmishAIAddClick(s.Arg(0)), s.Int(1, 1)));
            Runner.Register("Lobby.Button", s => Do(view => view.ButtonClicked(s.Arg(0)), 1));
            Runner.Register("Lobby.Play", Play);
            Runner.Register("Lobby.PeaceTime", PeaceTime);
            Runner.Register("Lobby.AutoTrade", AutoTrade);
            Runner.Register("Lobby.GameSpeed", GameSpeed);
            Runner.Register("Lobby.AddCustomAi", AddCustomAi);
            Runner.Register("Lobby.StartPositions", StartPositions);
            Runner.Register("Lobby.Prebuild", Prebuild);
            Runner.Register("Editor.Start", EditorStart);
            Runner.Register("Mp.Host", Host);
            Runner.Register("Mp.WaitMembers", WaitMembers);
            Runner.Register("Mp.Join", Join);
            Runner.Register("Mp.Ready", Ready);
            Runner.Register("Mp.Leave", s => GameSteps.Do(() => Platform_Multiplayer.Instance.LeaveLobby()));
            Runner.Register("Mp.Save", s => GameSteps.Do(() => EngineInterface.TriggerMPSave(s.Arg(0) + ".msv")));
            Runner.Register("Mp.SaveConfirmed", SaveConfirmed);
            Runner.Register("Mp.AwaitSaveFile", AwaitSaveFile);
            Runner.Register("Mp.LoadSave", MpLoadSave);
            Runner.Register("Mp.ExpectRealMultiplayer", ExpectRealMultiplayer);
            Runner.Register("Mp.Exit", s => GameSteps.Do(() => Director.instance.ExitAppFromMP()));
            Runner.Register("Mp.Follow", Follow);
            Runner.Register("Mp.SetSpeed", SetMultiplayerSpeed);
            Runner.Register("Mp.ExpectSpeed", ExpectMultiplayerSpeed);
        }

        // Client: follows the host lobby named in the join file from any state — statistics screen,
        // a foreign or own lobby (e.g. BugfixesAndQoL's return-to-lobby after a game), sub-menus — and
        // returns once inside that lobby ("lobby") or once its mission started ("mission"). A lobby ID
        // change while waiting is followed as well. Every action uses Vanilla's own commands, 1 per second.
        // Optional second argument "fresh": the join file still names the lobby of the previous mission until
        // the runner publishes the host's next lobby, so that ID is ignored instead of being joined again.
        private static ulong lastMissionLobby;
        private static IEnumerable<Wait> Follow(Step step)
        {
            bool untilMission = step.Arg(0, "lobby") == "mission";
            bool fresh = step.Arg(1, "") == "fresh";
            ulong stale = fresh ? lastMissionLobby : 0;
            string joinFile = Path.Combine(BepInEx.Paths.ConfigPath, "UiAcceptance", "join-lobby.txt");
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            var pace = System.Diagnostics.Stopwatch.StartNew();
            ulong target = 0, joining = 0;
            if (stale != 0) AcceptLog.Info("MP_FOLLOW ignoring previous mission lobby " + stale);
            yield return new Until(untilMission ? "mission of the followed lobby" : "followed host lobby", () =>
            {
                if (untilMission && GameSteps.InMission(before)) return true;
                if (pace.ElapsedMilliseconds < 1000) return false;
                pace.Restart();
                try { if (File.Exists(joinFile) && ulong.TryParse(File.ReadAllText(joinFile).Trim(), out ulong id)) target = id == stale ? 0 : id; }
                catch (IOException) { }
                if (target == 0) return false;
                var main = MainViewModel.Instance;
                bool inLobby = LobbyReady();
                if (inLobby && Lobby.currentLobby.id.m_SteamID == target && !Lobby.currentLobby.isHost)
                { joining = 0; return !untilMission; }
                string action;
                if (main.Show_HUD_MissionOver && main.HUDMissionOver != null) { action = "leave-statistics"; main.HUDMissionOver.ButtonClicked("Continue"); }
                else if (GameSteps.ConfirmationVisible()) { action = "confirm"; main.ButtonConfirmationCommand.Execute("1"); }
                else if (inLobby) { action = "leave-lobby " + Lobby.currentLobby.id.m_SteamID; Lobby.ButtonClicked("Back"); }
                else if (GameSteps.MenuReady())
                {
                    if (joining == target) return false; // invite in progress
                    action = "join " + target; joining = target;
                    Platform_Multiplayer.Instance.SetInviteLobbyID(target);
                    Platform_Multiplayer.Instance.ResumeInvite();
                }
                else if (FatControler.currentScene == Enums.SceneIDS.FrontEnd) { action = "back-main"; main.FrontEndMenu.ButtonClicked("BackMain"); }
                else return false; // in a mission of another session: wait for its end
                AcceptLog.Info("MP_FOLLOW " + action);
                return false;
            });
            if (untilMission)
            {
                GameSteps.sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
                // The mission can start before the first join-file read; then the joined lobby names it.
                ulong missionLobby = target != 0 ? target : Lobby?.currentLobby?.id.m_SteamID ?? 0;
                if (missionLobby != 0) lastMissionLobby = missionLobby;
            }
            AcceptLog.Info("MP_FOLLOW done target=" + target);
        }

        private static FRONT_Multiplayer Lobby => MainViewModel.Instance.FRONTMultiplayer;
        private static bool LobbyReady() => Lobby != null && Lobby.currentLobby != null && Lobby.panelActive && MainViewModel.Instance.Show_MPGameCreation;

        private static IEnumerable<Wait> Do(Action<FRONT_Multiplayer> action, int repeat)
        {
            yield return new Until("lobby ready", LobbyReady);
            for (int i = 0; i < repeat; i++) { action(Lobby); yield return new Delay(250); }
        }

        // args: the keep place of each player in lobby order (0 = the human host, then the AI lords as added), e.g.
        // "2 5". The lobby hands out free places at random when a player joins; comparable runs need the same places.
        // Vanilla's own swap through the radar shields writes the same array and then calls UpdateHostInfo.
        private static IEnumerable<Wait> StartPositions(Step step)
        {
            yield return new Until("lobby ready", LobbyReady);
            var setup = (EngineInterface.MultiplayerSetupData)typeof(FRONT_Multiplayer)
                .GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Lobby);
            Runner.Require(setup != null, "no lobby setup data");
            int[] order = setup.start_keep_location_order;
            int[,] places = GameData.Instance.Keep_Locations;
            for (int place = 0; place < order.Length; place++)
                order[place] = -10;
            for (int player = 0; player < step.Args.Length; player++)
            {
                int place = step.Int(player);
                Runner.Require(place >= 0 && place < order.Length && places[place, 0] >= 0, "no keep place " + place + " on this map");
                order[place] = player;
            }
            typeof(FRONT_Multiplayer).GetMethod("UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Lobby, new object[] { false });
            for (int place = 0; place < order.Length; place++)
                AcceptLog.Info("LOBBY_KEEP place=" + place + " at=" + places[place, 0] + "," + places[place, 1] + " player=" + order[place]);
            yield return new Delay(250);
        }

        // Through Vanilla's settings panel; its Apply button resets the value if the option is hidden.
        private static IEnumerable<Wait> PeaceTime(Step step)
        {
            yield return new Until("lobby ready", LobbyReady);
            Lobby.ButtonClicked("Setup");
            yield return new Delay(250);
            Runner.Require(MainViewModel.Instance.Show_MPPeacetime, "peace time is not offered in this lobby");
            FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value = step.Int(0);
            yield return new Delay(250);
            Lobby.ButtonClicked("ApplySettings");
            yield return new Delay(250);
        }

        private static IEnumerable<Wait> AutoTrade(Step step)
        {
            string choice = step.Arg(0);
            Runner.Require(choice == "on" || choice == "off", "AutoTrade expects on or off.");
            int expected = choice == "on" ? 1 : 0;
            yield return new Until("lobby ready", LobbyReady);
            Runner.Require(Lobby.currentLobby.isHost, "Only the host applies lobby settings.");
            Lobby.ButtonClicked("Setup");
            yield return new Delay(250);
            var draft = GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "MPTEMPsetupData", BindingFlags.Instance)
                as EngineInterface.MultiplayerSetupData;
            Runner.Require(draft != null, "Vanilla AutoTrade setup is unavailable.");
            if (draft.allow_autotrading != expected)
                Lobby.ButtonClicked("Settings_Autotrading");
            Runner.Require(draft.allow_autotrading == expected, "Vanilla did not change the draft AutoTrade setting.");
            Lobby.ButtonClicked("ApplySettings");
            yield return new Delay(250);
            var committed = GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "MPsetupData", BindingFlags.Instance)
                as EngineInterface.MultiplayerSetupData;
            Runner.Require(committed != null && committed.allow_autotrading == expected,
                "The confirmed Vanilla setup did not retain AutoTrade " + choice + ".");
            AcceptLog.Info("LOBBY_AUTOTRADE applied=" + choice);
        }

        // Starting speed of an MP lobby through the same settings panel. Vanilla stores slider*5 in
        // MPTEMPsetupData (MP_Settings_GameSpeed_Slider_ValueChanged), ApplySettings copies it into the
        // private MPsetupData, and StartGame calls SetEngineFrameRate(MPsetupData.starting_gamespeed).
        // FBCB9319 XAML: slider 2..16, so the lobby offers 10..80; SetEngineFrameRate also resets values above 90
        // to 40 for non-spectators. Higher MP start speeds are therefore rejected instead of forced.
        private static IEnumerable<Wait> GameSpeed(Step step)
        {
            int speed = step.Int(0);
            Runner.Require(speed > 0 && speed % 5 == 0, "the lobby speed slider stores multiples of five: " + speed);
            yield return new Until("lobby ready", LobbyReady);
            Runner.Require(Lobby.currentLobby.isHost, "only the host applies lobby settings");
            Lobby.ButtonClicked("Setup");
            yield return new Delay(250);
            var slider = FRONT_Multiplayer_Setup.Instance?.RefMP_Settings_GameSpeed_Slider;
            Runner.Require(slider != null, "Vanilla speed slider is unavailable");
            float value = speed / 5;
            Runner.Require(value >= slider.Minimum && value <= slider.Maximum,
                "speed " + speed + " is outside the Vanilla slider range " + slider.Minimum * 5 + ".." + slider.Maximum * 5);
            slider.Value = value;
            // Vanilla's own handler reads the slider itself; calling it again after the bound event is idempotent.
            Lobby.MP_Settings_GameSpeed_Slider_ValueChanged(null, null);
            yield return new Delay(250);
            Lobby.ButtonClicked("ApplySettings");
            yield return new Delay(250);
            var setup = GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "MPsetupData", BindingFlags.Instance) as EngineInterface.MultiplayerSetupData;
            Runner.Require(setup != null, "no lobby setup data");
            AcceptLog.Info("LOBBY_GAMESPEED requested=" + speed + " applied=" + setup.starting_gamespeed + " slider=" + slider.Value);
            Runner.Require(setup.starting_gamespeed == speed, "Vanilla applied starting speed " + setup.starting_gamespeed + ", expected " + speed);
        }

        // Adds a local custom AI lord by its exact name through Vanilla's custom-lord picker (lobby AI slot "98").
        // args: lord name. The AI keeps the team Vanilla gives a new AI slot; the step requires that no human
        // shares that team, so the AI plays on its own side regardless of the number of humans.
        private static IEnumerable<Wait> AddCustomAi(Step step)
        {
            yield return new Until("lobby ready", LobbyReady);
            Runner.Require(Lobby.currentLobby.isHost, "AI setup requires the host lobby");
            var session = Lobby.currentLobby;
            var added = new Platform_Multiplayer.MPLobbyMember[1];
            foreach (Wait wait in AddCustomLord(Lobby, step.Arg(0), rows => rows.Length == 1 && !rows[0].lord.workshop,
                "expected exactly one local (non-Workshop) custom lord named " + step.Arg(0), "the custom AI " + step.Arg(0) + " joined", added))
                yield return wait;
            var ai = added[0];
            int team = session.getTeam(ai);
            var humans = session.members.FindAll(m => IsHuman(m) && !m.dummyToBeKicked);
            AcceptLog.Info("LOBBY_CUSTOM_AI lord=" + ai.customLordName + " team=" + team + " humans=" + humans.Count +
                " humanTeams=" + string.Join(",", humans.ConvertAll(m => session.getTeam(m).ToString(System.Globalization.CultureInfo.InvariantCulture))));
            Runner.Require(team <= 0 || humans.TrueForAll(m => session.getTeam(m) != team),
                "the custom AI shares team " + team + " with a human");
        }

        // Shared custom-lord addition (also used by Paired.AddTradingAi). The caller validates the lobby;
        // rowContract judges the matching picker rows before anything is added. added[0] receives the new member.
        internal static IEnumerable<Wait> AddCustomLord(FRONT_Multiplayer lobby, string lord,
            Func<FileRow[], bool> rowContract, string contractFailure, string joined, Platform_Multiplayer.MPLobbyMember[] added)
        {
            var session = lobby.currentLobby;
            var before = session.members.ToArray();
            lobby.SkirmishAIAddClick("98"); // Open the built-in custom-lord picker.
            var list = lobby.FindName("CustomLordList") as Noesis.ListView;
            Runner.Require(list?.ItemsSource != null, "custom lord picker is unavailable");
            var rows = list.ItemsSource.Cast<object>().OfType<FileRow>()
                .Where(row => string.Equals(row.lord?.lordName, lord, StringComparison.Ordinal)).ToArray();
            Runner.Require(rows.Length > 0 && rowContract(rows), contractFailure);
            list.SelectedItem = rows[0];
            lobby.ButtonClicked("AddCustomLord");
            yield return new Until(joined, () => session.members.Count == before.Length + 1);
            var ai = session.members.Single(member => !before.Contains(member));
            Runner.Require(ai.customLordName == lord, "unexpected AI after adding " + lord);
            added[0] = ai;
        }

        private static IEnumerable<Wait> Prebuild(Step step)
        {
            yield return new Until("skirmish lobby ready", () => LobbyReady() && FRONT_Multiplayer.skirmishGame);
            var setup = (EngineInterface.MultiplayerSetupData)typeof(FRONT_Multiplayer)
                .GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Lobby);
            Runner.Require(setup != null, "no lobby setup data");
            if (setup.advopt_pre_build == 0)
                Lobby.ButtonClicked("Settings_PreBuild");
            Runner.Require(setup.advopt_pre_build == 1, "Vanilla prebuilt castle setting did not enable");
            AcceptLog.Info("LOBBY_PREBUILD enabled=True");
        }

        private static IEnumerable<Wait> SkirmishOpen(Step step)
        {
            yield return new Until("main menu", GameSteps.MenuReady);
            MainViewModel.Instance.ButtonMainMenuFunction("Custom");
            yield return new Until("skirmish lobby", () => LobbyReady() && FRONT_Multiplayer.skirmishGame);
            yield return new Delay(500);
        }

        // Map by file name (case-insensitive) through Vanilla's private map list, as AivLobbyPresetTest does.
        private static IEnumerable<Wait> SelectMap(Step step)
        {
            yield return new Until("lobby ready", LobbyReady);
            var list = (Noesis.ListView)GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "RefFileLists", BindingFlags.Instance);
            object target = null;
            foreach (object row in (IEnumerable)list.ItemsSource)
                if (row is FileRow file && file.fileHeader != null &&
                    string.Equals(Path.GetFileName(file.fileHeader.filePath), step.Arg(0), StringComparison.OrdinalIgnoreCase))
                { target = row; break; }
            Runner.Require(target != null, "map " + step.Arg(0) + " not in the lobby list");
            list.SelectedItem = target;
            yield return new Delay(500);
            var header = (FileHeader)GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "selectedMPHeader", BindingFlags.Instance);
            Runner.Require(header != null && string.Equals(Path.GetFileName(header.filePath), step.Arg(0), StringComparison.OrdinalIgnoreCase),
                "Vanilla did not select " + step.Arg(0));
        }

        private static IEnumerable<Wait> Play(Step step)
        {
            yield return new Until("lobby ready", LobbyReady);
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            Lobby.ButtonClicked("Play");
            yield return new Until("mission start", () => GameSteps.InMission(before));
            GameSteps.sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            // A new skirmish may start paused (e.g. CastlePlanner's castle selection); ticks are a separate step.
            AcceptLog.Info("PLAY started paused=" + SHCDESE.API.GamePlayerManagerAPI.Instance.IsLocalPaused());
        }

        private static IEnumerable<Wait> EditorStart(Step step)
        {
            yield return new Until("main menu", GameSteps.MenuReady);
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            MainViewModel.Instance.ButtonMainMenuFunction("MapEditor");
            yield return new Delay(1000);
            MainViewModel.Instance.MapEditorSetupFunction("Start");
            yield return new Until("editor mission start", () => Interlocked.Read(ref UiAcceptancePlugin.StartedSession) != before &&
                UiAcceptancePlugin.LastContext != null && UiAcceptancePlugin.LastContext.IsEditor);
        }

        // ---- real multiplayer ----
        private static IEnumerable<Wait> Host(Step step)
        {
            yield return new Until("main menu", GameSteps.MenuReady);
            MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");
            yield return new Delay(1500);
            Lobby.ButtonClicked("Host");
            yield return new Delay(800);
            if (step.Arg(1, "") == "private")
            {
                var privacy = typeof(FRONT_Multiplayer).GetField("MPLobbyMode", BindingFlags.Instance | BindingFlags.NonPublic);
                Runner.Require(privacy != null && privacy.FieldType == typeof(int), "Vanilla lobby privacy field differs");
                if ((int)privacy.GetValue(Lobby) == 0) Lobby.ButtonClicked("TogglePublic");
                Runner.Require((int)privacy.GetValue(Lobby) == 4, "Vanilla did not select a private lobby");
            }
            Lobby.ButtonClicked(step.Arg(0, "DoHost"));
            yield return new Until("hosted lobby", () => LobbyReady() && Lobby.currentLobby.isHost && !FRONT_Multiplayer.skirmishGame);
            AcceptLog.Info("MP_HOST lobby=" + Lobby.currentLobby.id.m_SteamID + " run=" + AcceptLog.RunId);
        }

        private static IEnumerable<Wait> WaitMembers(Step step)
        {
            int count = step.Int(0);
            yield return new Until(count + " human lobby members", () => LobbyReady() && HumanMembers() >= count);
            AcceptLog.Info("MP_MEMBERS humans=" + HumanMembers() + " all=" + Lobby.currentLobby.members.Count);
        }

        // The client finds a Steam friend that is in a lobby of this game and joins it through the
        // same public invite path BugfixesAndQoL uses (SetInviteLobbyID + ResumeInvite).
        private static IEnumerable<Wait> Join(Step step)
        {
            yield return new Until("main menu", GameSteps.MenuReady);
            CSteamID lobby = CSteamID.Nil;
            string friendName = step.Arg(0, "");
            // Fallback channel: the test runner writes the host's lobby ID into this file on the client
            // (Steam friend status does not expose private lobbies). Read at most every 500 ms.
            string joinFile = Path.Combine(BepInEx.Paths.ConfigPath, "UiAcceptance", "join-lobby.txt");
            var poll = System.Diagnostics.Stopwatch.StartNew();
            string announced = null;
            yield return new Until("a friend's lobby of this game or " + joinFile, () =>
            {
                if (poll.ElapsedMilliseconds >= 500)
                {
                    poll.Restart();
                    try
                    {
                        if (File.Exists(joinFile) && ulong.TryParse(File.ReadAllText(joinFile).Trim(), out ulong id) && id != 0 && id.ToString() != announced)
                        { announced = id.ToString(); lobby = new CSteamID(id); return true; }
                    }
                    catch (IOException) { }
                }
                int friends = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
                for (int i = 0; i < friends; i++)
                {
                    CSteamID friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                    if (friendName.Length > 0 && !string.Equals(SteamFriends.GetFriendPersonaName(friend), friendName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (SteamFriends.GetFriendGamePlayed(friend, out FriendGameInfo_t game) && game.m_gameID.AppID().m_AppId == 3024040 &&
                        game.m_steamIDLobby.IsValid()) { lobby = game.m_steamIDLobby; return true; }
                }
                return false;
            });
            AcceptLog.Info("MP_JOIN lobby=" + lobby.m_SteamID);
            Platform_Multiplayer.Instance.SetInviteLobbyID(lobby.m_SteamID);
            Platform_Multiplayer.Instance.ResumeInvite();
            yield return new Until("joined lobby", () => LobbyReady() && !Lobby.currentLobby.isHost);
        }

        // AI slots in an MP lobby are skirmish members without a human player and never report ready.
        private static bool IsHuman(Platform_Multiplayer.MPLobbyMember member) => !member.SkirmishMember || member.SkirmishHumanMember;
        private static int HumanMembers() => Lobby.currentLobby.members.FindAll(m => IsHuman(m) && !m.dummyToBeKicked).Count;

        private static Platform_Multiplayer.MPLobbyMember Self()
        {
            CSteamID self = SteamUser.GetSteamID();
            foreach (var member in Lobby.currentLobby.members) if (member.id == self) return member;
            return null;
        }

        // Ready toggles in Vanilla; press only while the own member is not ready. Host: also waits
        // until every other member is ready before returning.
        private static IEnumerable<Wait> Ready(Step step)
        {
            yield return new Until("own lobby member", () => LobbyReady() && Self() != null);
            yield return new Until("map valid", () => Lobby.currentLobby.isHost || (bool)GameSteps.Read(typeof(FRONT_Multiplayer), Lobby, "MPMapValid", BindingFlags.Instance));
            if (step.Arg(0, "") == "after-peers")
            {
                Runner.Require(Lobby.currentLobby.isHost, "waiting for peers before Ready requires the host");
                yield return new Until("remote human ready", () => LobbyReady() &&
                    Lobby.currentLobby.members.Exists(m => IsHuman(m) && !m.IsSelf() && !m.dummyToBeKicked) &&
                    Lobby.currentLobby.members.TrueForAll(m => !IsHuman(m) || m.IsSelf() || m.ready || m.dummyToBeKicked));
            }
            // A mod launch guard (e.g. APIShared's production-rule agreement) can still be waiting and
            // reject the first click; repeat it until Ready sticks. Ready is a toggle, so never click
            // while already ready, and leave the state time to update between clicks.
            DateTime nextClick = DateTime.MinValue;
            int clicks = 0;
            yield return new Until("own ready state", () =>
            {
                var self = Self();
                if (self?.ready == true) return true;
                if (self != null && DateTime.UtcNow >= nextClick)
                {
                    nextClick = DateTime.UtcNow.AddSeconds(5);
                    clicks++;
                    Lobby.ButtonClicked("Ready");
                }
                return false;
            });
            if (clicks > 1) AcceptLog.Info("MP_READY clicks=" + clicks);
            if (Lobby.currentLobby.isHost)
                yield return new Until("all human members ready", () => Lobby.currentLobby.members.TrueForAll(m => !IsHuman(m) || m.ready || m.dummyToBeKicked));
        }

        // Vanilla's lobby Load: SendSaveCRCs, HostLoadGame(header.fileName) of an entry from
        // MapFileManager.GetMPSaves, then the private startGameTime that makes the host start loading.
        private static IEnumerable<Wait> MpLoadSave(Step step)
        {
            yield return new Until("hosted lobby", () => LobbyReady() && Lobby.currentLobby.isHost);
            string wanted = SaveName(step);
            FileHeader header = MapFileManager.Instance.GetMPSaves(0, true).Find(h =>
                string.Equals(Path.GetFileNameWithoutExtension(h.fileName ?? ""), wanted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(h.filePath ?? ""), wanted, StringComparison.OrdinalIgnoreCase));
            Runner.Require(header != null, "MP save " + wanted + " not in Vanilla's MP save list");
            Platform_Multiplayer.Instance.SendSaveCRCs(false);
            yield return new Delay(1000);
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            AcceptLog.Info("MP_LOAD fileName=" + header.fileName);
            Platform_Multiplayer.Instance.HostLoadGame(header.fileName);
            FieldInfo start = typeof(FRONT_Multiplayer).GetField("startGameTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new StepFailure("FRONT_Multiplayer.startGameTime missing");
            start.SetValue(Lobby, DateTime.UtcNow.AddMilliseconds(500.0));
            yield return new Until("loaded MP mission", () => GameSteps.InMission(before));
            GameSteps.sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
        }

        // Without an argument the run's own save name: SerpAcceptance-<RunId>(.msv), the only MP save a run may write.
        private static string SaveName(Step step)
        {
            string name = Path.GetFileNameWithoutExtension(step.Arg(0, "SerpAcceptance-" + AcceptLog.RunId + ".msv"));
            Runner.Require(name.Length > 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0, "invalid MP save name: " + name);
            return name;
        }

        // Host: Vanilla's MP save (every peer writes its own copy at the same simulation point), then waits
        // until the local file is newer than the step start and its size has been stable for one second.
        private static IEnumerable<Wait> SaveConfirmed(Step step)
        {
            var mission = UiAcceptancePlugin.Missions?.Current;
            Runner.Require(mission?.Mode.IsRealMultiplayer == true && mission.IsHost == true, "an MP save requires the host of a real multiplayer mission");
            string name = SaveName(step);
            string path = Path.Combine(ConfigSettings.GetSavesPath(), name + ".msv");
            var previous = new FileInfo(path);
            DateTime after = previous.Exists ? previous.LastWriteTimeUtc : DateTime.UtcNow.AddSeconds(-2);
            EngineInterface.TriggerMPSave(name + ".msv");
            APIShared.Simulation.SimulationTickEvents.TryGetCurrentTick(out int tick);
            AcceptLog.Info("MP_SAVE_TRIGGERED name=" + name + " path=" + path + " mapTick=" + tick + " replaces=" + previous.Exists);
            foreach (Wait wait in AwaitStableFile(path, after, previous.Exists)) yield return wait;
        }

        // Client: the same save written by its own game, newer than the current mission start.
        private static IEnumerable<Wait> AwaitSaveFile(Step step)
        {
            var mission = UiAcceptancePlugin.Missions?.Current;
            Runner.Require(mission?.Mode.IsRealMultiplayer == true, "the MP save is only written in a real multiplayer mission");
            string path = Path.Combine(ConfigSettings.GetSavesPath(), SaveName(step) + ".msv");
            var started = new DateTime(Interlocked.Read(ref UiAcceptancePlugin.StartedUtcTicks), DateTimeKind.Utc);
            foreach (Wait wait in AwaitStableFile(path, started.AddSeconds(-2), false)) yield return wait;
        }

        private static IEnumerable<Wait> AwaitStableFile(string path, DateTime after, bool strictlyNewer)
        {
            long length = -1;
            DateTime written = DateTime.MinValue;
            var stable = System.Diagnostics.Stopwatch.StartNew();
            yield return new Until("stable MP save " + path, () =>
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || (strictlyNewer ? file.LastWriteTimeUtc <= after : file.LastWriteTimeUtc < after))
                {
                    length = -1;
                    return false;
                }
                if (file.Length != length || file.LastWriteTimeUtc != written)
                {
                    length = file.Length;
                    written = file.LastWriteTimeUtc;
                    stable.Restart();
                    return false;
                }
                return stable.ElapsedMilliseconds >= 1000;
            });
            AcceptLog.Info("MP_SAVE_FILE path=" + path + " bytes=" + length + " writtenUtc=" + written.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        }

        // Workspace rule: a run counts as real MP only with several human participants in the snapshot.
        // Vanilla's lobby slider stops at 80; faster MP speeds go through BugfixesAndQoL's synchronized
        // time-control Chore (limited by its host setting MultiplayerGameSpeedMaximum, at most 200).
        // args: speed. Host only; every peer then checks the result with Mp.ExpectSpeed.
        private static IEnumerable<Wait> SetMultiplayerSpeed(Step step)
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            Runner.Require(BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out BepInEx.PluginInfo info),
                "BugfixesAndQoL is not installed");
            // The object stays usable after BepInEx's manager is destroyed; only its Unity side is gone.
            object plugin = info.Instance;
            object runtime = plugin?.GetType().GetField("runtime", instance)?.GetValue(plugin);
            object speed = runtime?.GetType().GetField("multiplayerGameSpeedRuntime", instance)?.GetValue(runtime);
            MethodInfo queue = speed?.GetType().GetMethod("TryQueueChange", instance, null,
                new[] { typeof(int), typeof(int), typeof(string), typeof(int) }, null);
            Runner.Require(queue != null && queue.ReturnType == typeof(bool), "BugfixesAndQoL multiplayer speed contract differs");
            const int setAction = 3;
            int target = step.Int(0);
            // A request right at the mission start can be queued and still never execute; ask once the
            // simulation runs and repeat until the synchronized speed arrived on this peer.
            yield return new Until("running simulation", () => Director.instance != null && Director.instance.SimRunning);
            DateTime nextRequest = DateTime.MinValue;
            yield return new Until("engine speed " + target, () =>
            {
                if (Director.instance.EngineFrameRate == target) return true;
                if (DateTime.UtcNow >= nextRequest)
                {
                    nextRequest = DateTime.UtcNow.AddSeconds(5);
                    bool queued = (bool)queue.Invoke(speed, new object[] { setAction, target, "ui-acceptance", 0 });
                    AcceptLog.Info("MP_SPEED_REQUEST target=" + target + " queued=" + queued + " current=" + Director.instance.EngineFrameRate);
                }
                return false;
            });
            AcceptLog.Info("MP_SPEED speed=" + Director.instance.EngineFrameRate);
        }

        // args: speed. Waits until the synchronized engine speed arrived on this peer.
        private static IEnumerable<Wait> ExpectMultiplayerSpeed(Step step)
        {
            int target = step.Int(0), seen = int.MinValue;
            yield return new Until("engine speed " + target, () =>
            {
                if (Director.instance == null) return false;
                int current = (int)Director.instance.EngineFrameRate;
                if (current != seen) { seen = current; AcceptLog.Info("MP_SPEED_SEEN speed=" + current); }
                return current == target;
            });
            AcceptLog.Info("MP_SPEED speed=" + Director.instance.EngineFrameRate);
        }

        private static IEnumerable<Wait> ExpectRealMultiplayer(Step step)
        {
            yield return new Until("mission context", () => UiAcceptancePlugin.LastContext != null);
            string mode = UiAcceptancePlugin.LastContext.Mode.ToDiagnosticString();
            AcceptLog.Info("MP_MODE " + mode);
            var match = System.Text.RegularExpressions.Regex.Match(mode, @"realNetworkGameMembers=(\d+)");
            Runner.Require(mode.Contains("realMultiplayer=True") || mode.Contains("realMultiplayer=true"), "not real multiplayer: " + mode);
            Runner.Require(match.Success && int.Parse(match.Groups[1].Value) >= 2, "fewer than two network game members: " + mode);
        }
    }
}
