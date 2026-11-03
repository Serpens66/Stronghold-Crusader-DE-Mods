using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            Runner.Register("Editor.Start", EditorStart);
            Runner.Register("Mp.Host", Host);
            Runner.Register("Mp.WaitMembers", WaitMembers);
            Runner.Register("Mp.Join", Join);
            Runner.Register("Mp.Ready", Ready);
            Runner.Register("Mp.Leave", s => GameSteps.Do(() => Platform_Multiplayer.Instance.LeaveLobby()));
            Runner.Register("Mp.Save", s => GameSteps.Do(() => EngineInterface.TriggerMPSave(s.Arg(0) + ".msv")));
            Runner.Register("Mp.LoadSave", MpLoadSave);
            Runner.Register("Mp.ExpectRealMultiplayer", ExpectRealMultiplayer);
            Runner.Register("Mp.Exit", s => GameSteps.Do(() => Director.instance.ExitAppFromMP()));
            Runner.Register("Mp.Follow", Follow);
        }

        // Client: follows the host lobby named in the join file from any state — statistics screen,
        // a foreign or own lobby (e.g. BugfixesAndQoL's return-to-lobby after a game), sub-menus — and
        // returns once inside that lobby ("lobby") or once its mission started ("mission"). A lobby ID
        // change while waiting is followed as well. Every action uses Vanilla's own commands, 1 per second.
        private static IEnumerable<Wait> Follow(Step step)
        {
            bool untilMission = step.Arg(0, "lobby") == "mission";
            string joinFile = Path.Combine(BepInEx.Paths.ConfigPath, "UiAcceptance", "join-lobby.txt");
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            var pace = System.Diagnostics.Stopwatch.StartNew();
            ulong target = 0, joining = 0;
            yield return new Until(untilMission ? "mission of the followed lobby" : "followed host lobby", () =>
            {
                if (untilMission && GameSteps.InMission(before)) return true;
                if (pace.ElapsedMilliseconds < 1000) return false;
                pace.Restart();
                try { if (File.Exists(joinFile) && ulong.TryParse(File.ReadAllText(joinFile).Trim(), out ulong id)) target = id; }
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
            if (untilMission) GameSteps.sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            AcceptLog.Info("MP_FOLLOW done target=" + target);
        }

        private static FRONT_Multiplayer Lobby => MainViewModel.Instance.FRONTMultiplayer;
        private static bool LobbyReady() => Lobby != null && Lobby.currentLobby != null && Lobby.panelActive && MainViewModel.Instance.Show_MPGameCreation;

        private static IEnumerable<Wait> Do(Action<FRONT_Multiplayer> action, int repeat)
        {
            yield return new Until("lobby ready", LobbyReady);
            for (int i = 0; i < repeat; i++) { action(Lobby); yield return new Delay(250); }
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
            Lobby.ButtonClicked(step.Arg(0, "DoHost"));
            yield return new Until("hosted lobby", () => LobbyReady() && Lobby.currentLobby.isHost && !FRONT_Multiplayer.skirmishGame);
            AcceptLog.Info("MP_HOST lobby=" + Lobby.currentLobby.id.m_SteamID);
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
            if (!Self().ready) Lobby.ButtonClicked("Ready");
            yield return new Until("own ready state", () => Self()?.ready == true);
            if (Lobby.currentLobby.isHost)
                yield return new Until("all human members ready", () => Lobby.currentLobby.members.TrueForAll(m => !IsHuman(m) || m.ready || m.dummyToBeKicked));
        }

        // Vanilla's lobby Load: SendSaveCRCs, HostLoadGame(header.fileName) of an entry from
        // MapFileManager.GetMPSaves, then the private startGameTime that makes the host start loading.
        private static IEnumerable<Wait> MpLoadSave(Step step)
        {
            yield return new Until("hosted lobby", () => LobbyReady() && Lobby.currentLobby.isHost);
            string wanted = Path.GetFileNameWithoutExtension(step.Arg(0));
            FileHeader header = MapFileManager.Instance.GetMPSaves(0, true).Find(h =>
                string.Equals(Path.GetFileNameWithoutExtension(h.fileName ?? ""), wanted, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(h.filePath ?? ""), wanted, StringComparison.OrdinalIgnoreCase));
            Runner.Require(header != null, "MP save " + step.Arg(0) + " not in Vanilla's MP save list");
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

        // Workspace rule: a run counts as real MP only with several human participants in the snapshot.
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
