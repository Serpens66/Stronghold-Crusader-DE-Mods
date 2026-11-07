using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CrusaderDE;
using SHCDESE.API;
using UnityEngine;

namespace UiAcceptance
{
    // Game flow: menus, saves, scenes, pause/speed, waits, screenshots, settings, quitting.
    internal static class GameSteps
    {
        internal static void Register()
        {
            Runner.Register("WaitMainMenu", s => WaitMainMenu());
            Runner.Register("LoadSave", LoadSave);
            Runner.Register("QuitToMenu", QuitToMenu);
            Runner.Register("MainMenu", s => Do(() => MainViewModel.Instance.ButtonMainMenuFunction(s.Arg(0))));
            Runner.Register("Pause", Pause);
            Runner.Register("Speed", s => Do(() => Director.instance.SetEngineFrameRate(s.Int(0))));
            Runner.Register("Wait", s => new Wait[] { new Delay(s.Int(0)) });
            Runner.Register("WaitTicks", WaitTicks);
            Runner.Register("WaitMarker", WaitMarker);
            Runner.Register("Screenshot", Screenshot);
            Runner.Register("ModSettings.SelectTab", SelectModSettingsTab);
            Runner.Register("ExpectElement", ExpectElement);
            Runner.Register("InvokeCommand", InvokeCommand);
            Runner.Register("SetModSetting", SetModSetting);
            Runner.Register("RandomEvents", RandomEventsSwitch);
            Runner.Register("ExpectModSetting", ExpectModSetting);
            Runner.Register("Log", s => Do(() => AcceptLog.Info("NOTE " + string.Join(" ", s.Args))));
            Runner.Register("Describe", s => Do(Describe));
            Runner.Register("DumpSettings", s => Do(() => DumpSettings(s.Arg(0))));
            Runner.Register("ExpectGameState", ExpectGameState);
            Runner.Register("WaitMission", WaitMission);
            Runner.Register("WaitMissionEnd", WaitMissionEnd);
            Runner.Register("WaitMissionTicks", WaitMissionTicks);
            Runner.Register("ViewCommand", ViewCommand);
            Runner.Register("Mark", s => Do(() => marks[s.Arg(0)] = AcceptLog.Position));
            Runner.Register("ExpectSince", ExpectSince);
            Runner.Register("ExpectSequence", ExpectSequence);
        }

        // A mission started by someone else (MP client: the host starts it).
        private static IEnumerable<Wait> WaitMission(Step step)
        {
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            if (step.Arg(0, "new") == "any" && before != 0 && Interlocked.Read(ref UiAcceptancePlugin.EndedSession) != before &&
                FatControler.currentScene != Enums.SceneIDS.FrontEnd) yield break;
            yield return new Until("mission start", () => InMission(before));
            sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
        }

        private static IEnumerable<Wait> WaitMissionEnd(Step step)
        {
            long session = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            yield return new Until("end of mission " + session, () => Interlocked.Read(ref UiAcceptancePlugin.EndedSession) == session);
        }

        // Waits until N simulation ticks after the last WaitMission/LoadSave start; identical on
        // every MP peer, so host and client steps meet without an own network channel.
        internal static int sessionTicks;
        private static IEnumerable<Wait> WaitMissionTicks(Step step)
        {
            int target = step.Int(0);
            yield return new Until(target + " ticks into the mission", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) - sessionTicks >= target);
        }

        // args: public field of GameData.lastGameState, expected value — waits until it matches.
        private static IEnumerable<Wait> ExpectGameState(Step step)
        {
            object value = null;
            yield return new Until("lastGameState." + step.Arg(0) + " == " + step.Arg(1), () =>
            {
                object state = GameData.Instance?.lastGameState;
                if (state == null) return false;
                value = Read(state.GetType(), state, step.Arg(0), BindingFlags.Instance);
                return string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), step.Arg(1), StringComparison.Ordinal);
            });
            AcceptLog.Info("GAMESTATE " + step.Arg(0) + "=" + value);
        }

        // args: public MainViewModel command property, parameter — the same path a XAML button uses.
        private static IEnumerable<Wait> ViewCommand(Step step)
        {
            object command = Read(typeof(MainViewModel), MainViewModel.Instance, step.Arg(0), BindingFlags.Instance);
            var input = command as System.Windows.Input.ICommand ?? throw new StepFailure(step.Arg(0) + " is not a command");
            input.Execute(step.Arg(1, null));
            yield return new Delay(500);
        }

        // Logs every public bool/int/string property of a settings model (the effective values a run starts with).
        private static void DumpSettings(string path)
        {
            object model = ResolveStatic(path);
            var values = new List<string>();
            foreach (PropertyInfo property in model.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
                if (property.CanRead && property.GetIndexParameters().Length == 0 &&
                    (property.PropertyType == typeof(bool) || property.PropertyType == typeof(int) || property.PropertyType == typeof(string)))
                    try { values.Add(property.Name + "=" + property.GetValue(model, null)); } catch { }
            AcceptLog.Info("SETTINGS " + path + " " + string.Join(" ", values));
        }

        private static readonly Dictionary<string, long> marks = new Dictionary<string, long>(StringComparer.Ordinal);
        private static long MarkPosition(string name) =>
            marks.TryGetValue(name, out long position) ? position : throw new StepFailure("unknown mark " + name);

        // args: mark, regex, min, [max] — counts log lines since a Mark step.
        private static IEnumerable<Wait> ExpectSince(Step step)
        {
            var pattern = new Regex(step.Arg(1), RegexOptions.CultureInvariant);
            int min = step.Int(2), max = step.Int(3, int.MaxValue);
            int count = AcceptLog.Count(MarkPosition(step.Arg(0)), pattern);
            Runner.Require(count >= min && count <= max, "/" + step.Arg(1) + "/ since " + step.Arg(0) + " count=" + count + " expected " + min + ".." + max);
            yield break;
        }

        // args: mark, regex with one numeric group, count — exactly count matches, strictly increasing
        // (FIFO and exactly-once for queued operations).
        private static IEnumerable<Wait> ExpectSequence(Step step)
        {
            var pattern = new Regex(step.Arg(1), RegexOptions.CultureInvariant);
            int expected = step.Int(2);
            List<long> values = null;
            yield return new Until(expected + " matches of /" + step.Arg(1) + "/", () => (values = AcceptLog.Numbers(MarkPosition(step.Arg(0)), pattern)).Count >= expected);
            AcceptLog.Info("SEQUENCE " + string.Join(",", values));
            Runner.Require(values.Count == expected, "count=" + values.Count + " expected " + expected);
            for (int i = 1; i < values.Count; i++)
                Runner.Require(values[i] == values[i - 1] + 1, "not strictly consecutive at " + i + ": " + string.Join(",", values));
        }

        // Discovery: local player, spectator/game type and per-player lord, gold and units.
        private static void Describe()
        {
            var state = GameData.Instance?.lastGameState;
            var players = GamePlayerManagerAPI.Instance;
            AcceptLog.Info("DESCRIBE local=" + players.GetLocalPlayerId() + " spectatorMode=" + state?.spectatorMode +
                " gameType=" + state?.game_type + " paused=" + players.IsLocalPaused() + " screen=" + Screen.width + "x" + Screen.height +
                " mode=" + (UiAcceptancePlugin.LastContext != null ? UiAcceptancePlugin.LastContext.Mode.ToDiagnosticString() : "<none>"));
            var units = new int[9];
            foreach (int id in APIShared.UnitAccess.GetAllReallyAliveUnits())
            {
                int owner = OwnerOf(id);
                if (owner >= 0 && owner < units.Length) units[owner]++;
            }
            for (int p = 1; p <= 8; p++)
                if (players.IsPlayerIdValid(p))
                    AcceptLog.Info("DESCRIBE player=" + p + " ai=" + players.IsAIPlayer(p) + " lord=" + players.GetLordUnitId(p) +
                        " gold=" + players.GetPlayerGold(p) + " units=" + units[p]);
        }

        private static unsafe int OwnerOf(int id) =>
            APIShared.UnitAccess.TryGetById(id, out SHCDESE.Interop.GameUnit* unit, out _) ? unit->r_ControllableForPlayerId : -1;

        internal static IEnumerable<Wait> Do(Action action) { action(); yield break; }

        // In-game popups use Show_HUD_Confirmation; front-end ones the MP/SM/Sands variants.
        internal static bool ConfirmationVisible()
        {
            var view = MainViewModel.Instance;
            return view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM || view.Show_HUD_ConfirmationSands;
        }

        internal static bool MenuReady()
        {
            if (!MainViewModel.viewModelLoaded || FatControler.currentScene != Enums.SceneIDS.FrontEnd) return false;
            var view = MainViewModel.Instance;
            return view.Show_Frontend_MainMenu && !view.Show_IntroSequence && view.HUDmain != null &&
                view.HUDIngameMenu != null && Director.instance != null && EditorDirector.instance != null;
        }

        // InitNewScene(MainGame) later becomes ActualMainGame (or MapEditor); only the front end is excluded.
        internal static bool InMission(long after) =>
            Interlocked.Read(ref UiAcceptancePlugin.StartedSession) != 0 &&
            Interlocked.Read(ref UiAcceptancePlugin.StartedSession) != after &&
            FatControler.currentScene != Enums.SceneIDS.FrontEnd;

        private static IEnumerable<Wait> WaitMainMenu()
        {
            yield return new Until("main menu ready after startup cleanup", MenuReady);
        }

        // Vanilla's FrontendMenus load confirmation sequence (same as AutoLoadSave), repeatable.
        internal static IEnumerable<Wait> LoadSave(Step step)
        {
            yield return new Until("main menu", MenuReady);
            string path = step.Arg(0);
            if (!Path.IsPathRooted(path)) path = Path.Combine(ConfigSettings.GetSavesPath(), path);
            Runner.Require(File.Exists(path), "save not found: " + path);
            var header = MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0, true);
            Runner.Require(header != null, "Vanilla rejected the save header: " + path);
            long before = Interlocked.Read(ref UiAcceptancePlugin.StartedSession);
            var view = MainViewModel.Instance;
            Platform_Multiplayer.Instance.gameMembers = null;
            view.InitNewScene(Enums.SceneIDS.MainGame);
            Director.instance.SetPausedState(false);
            EditorDirector.instance.stopGameSim();
            EditorDirector.instance.loadSaveGame(path, header.standAlone_filename, header);
            view.InitObjectiveGoodsPanelDelayed();
            yield return new Until("mission start of " + Path.GetFileName(path), () => InMission(before));
            sessionTicks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            int ticks = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            yield return new Until("first simulation ticks", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) > ticks + 20);
        }

        // Ingame menu "quit to main menu" plus its Vanilla confirmation, through the public commands.
        private static IEnumerable<Wait> QuitToMenu(Step step)
        {
            var view = MainViewModel.Instance;
            // Vanilla reopens the skirmish lobby only when restart info exists; "menu" clears it so the
            // quit ends in the main menu (the lobby navigation below stays as a fallback).
            if (step.Arg(0, "menu") == "menu" && view.HUDIngameMenu.restartSkirmishMapInfo != null)
            {
                view.HUDIngameMenu.restartSkirmishMapInfo = null;
                AcceptLog.Info("QUIT cleared skirmish restart info");
            }
            view.HUDmain.InGameOptions(null, null);
            yield return new Delay(300);
            view.ButtonIngameMenuCommand.Execute("6");
            yield return new Delay(300);
            view.ButtonConfirmationCommand.Execute("1");
            yield return new Until("front end", () => FatControler.currentScene == Enums.SceneIDS.FrontEnd);
            if (step.Arg(0, "menu") != "menu") yield break;
            // Vanilla reopens the skirmish lobby after quitting a skirmish. Leave it through its own
            // commands: lobby Back (asks for confirmation), confirm, then FrontendMenus "BackMain".
            yield return new Delay(1500);
            for (int press = 0; press < 10 && !MenuReady(); press++)
            {
                var main = MainViewModel.Instance;
                var lobby = main.FRONTMultiplayer;
                string action;
                if (ConfirmationVisible()) { action = "confirm"; main.ButtonConfirmationCommand.Execute("1"); }
                else if (lobby != null && lobby.panelActive && main.Show_MPGameCreation) { action = "lobby-back"; lobby.ButtonClicked("Back"); }
                else { action = "back-main"; main.FrontEndMenu.ButtonClicked("BackMain"); }
                AcceptLog.Info("QUIT_NAVIGATION " + press + " " + action + " mainMenu=" + main.Show_Frontend_MainMenu +
                    " intro=" + main.Show_IntroSequence + " lobby=" + (lobby != null && lobby.panelActive));
                yield return new Delay(1000);
            }
            yield return new Until("main menu ready", MenuReady);
        }

        private static IEnumerable<Wait> Pause(Step step)
        {
            bool wanted = step.Arg(0) == "on";
            if (GamePlayerManagerAPI.Instance.IsLocalPaused() != wanted)
            {
                // Same native action as Vanilla's pause key; its return value is no proof (PerfProbe).
                int state = wanted ? 1 : 0;
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, state, state);
            }
            yield return new Until("pause=" + wanted, () => GamePlayerManagerAPI.Instance.IsLocalPaused() == wanted);
        }

        private static IEnumerable<Wait> WaitTicks(Step step)
        {
            int start = Volatile.Read(ref UiAcceptancePlugin.Ticks), count = step.Int(0);
            yield return new Until(count + " simulation ticks", () => Volatile.Read(ref UiAcceptancePlugin.Ticks) >= start + count);
        }

        private static IEnumerable<Wait> WaitMarker(Step step)
        {
            var pattern = new Regex(step.Arg(0), RegexOptions.CultureInvariant);
            long from = step.Arg(1, "step") == "any" ? 0 : Runner.StepLog;
            yield return new Until("marker /" + step.Arg(0) + "/", () => AcceptLog.Count(from, pattern) > 0);
        }

        internal static string ScreenshotDirectory => Path.Combine(BepInEx.Paths.BepInExRootPath, "UiAcceptance_shots");

        private static IEnumerable<Wait> Screenshot(Step step)
        {
            Directory.CreateDirectory(ScreenshotDirectory);
            string file = Path.Combine(ScreenshotDirectory, AcceptLog.RunId + "_" + step.Arg(0) + "_" + Screen.width + "x" + Screen.height + ".png");
            ScreenCapture.CaptureScreenshot(file);
            AcceptLog.Info("SCREENSHOT " + file);
            yield return new Delay(500); // Written asynchronously at the end of a frame.
        }

        // Mod settings only change outside missions (workspace rule). The setting object is reached
        // through a public static member of the consumer, e.g. Spectator.SpectatorPlugin.Settings.
        // Selects a tab of the mod settings hub (open it with NoesisClick SE_ModOptionsToggle on a lobby screen).
        // args: tab name as registered, usually the mod GUID.
        private static IEnumerable<Wait> SelectModSettingsTab(Step step)
        {
            var hub = SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel;
            foreach (var tab in hub.ModTabs)
                if (string.Equals(tab.Name, step.Arg(0), StringComparison.Ordinal)) hub.SelectedTab = tab;
            Runner.Require(hub.SelectedTab != null && hub.SelectedTab.Name == step.Arg(0), "no settings tab " + step.Arg(0));
            AcceptLog.Info("SETTINGS_TAB " + hub.SelectedTab.Name);
            yield break;
        }

        // A XAML patch of a mod really reached the live visual tree. args: element name.
        private static IEnumerable<Wait> ExpectElement(Step step)
        {
            object element = null;
            yield return new Until("element " + step.Arg(0), () => (element = SHCDESE.API.GameXAMLManagerAPI.Instance.FindGlobalElement(step.Arg(0))) != null);
            AcceptLog.Info("ELEMENT " + step.Arg(0) + " type=" + element.GetType().Name);
        }

        // Runs a command a view model exposes. args: Type::staticField.CommandProperty, optional parameter.
        private static IEnumerable<Wait> InvokeCommand(Step step)
        {
            string path = step.Arg(0);
            int dot = path.LastIndexOf('.');
            object model = ResolveStatic(path.Substring(0, dot));
            PropertyInfo property = model.GetType().GetProperty(path.Substring(dot + 1)) ?? throw new StepFailure("no command " + path);
            var command = (System.Windows.Input.ICommand)property.GetValue(model, null);
            command.Execute(step.Args.Length > 1 ? step.Arg(1) : null);
            AcceptLog.Info("COMMAND " + path);
            yield break;
        }

        private static IEnumerable<Wait> SetModSetting(Step step)
        {
            Runner.Require(FatControler.currentScene == Enums.SceneIDS.FrontEnd, "settings change only outside missions");
            object model = ResolveStatic(step.Arg(0));
            PropertyInfo property = model.GetType().GetProperty(step.Arg(1)) ?? throw new StepFailure("no property " + step.Arg(1));
            object value = Convert.ChangeType(step.Arg(2), property.PropertyType, CultureInfo.InvariantCulture);
            property.SetValue(model, value, null);
            Runner.Require(Equals(property.GetValue(model, null), value), step.Arg(1) + " did not accept " + step.Arg(2));
            AcceptLog.Info("SETTING " + step.Arg(0) + "." + step.Arg(1) + "=" + value);
            yield break;
        }

        // Scripted fixtures must not meet wolves, fires or plague. The mod keeps its settings on the plugin
        // instance. args: on | off; its absence is fine, there are no events then.
        private static IEnumerable<Wait> RandomEventsSwitch(Step step)
        {
            Runner.Require(FatControler.currentScene == Enums.SceneIDS.FrontEnd, "settings change only outside missions");
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("RandomEvents_Serp", out BepInEx.PluginInfo info))
            {
                AcceptLog.Info("RANDOM_EVENTS not installed");
                yield break;
            }

            // The object stays usable after BepInEx's manager is destroyed; only its Unity side is gone.
            object plugin = info.Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin, null);
            PropertyInfo enable = settings.GetType().GetProperty("EnableMod");
            bool value = step.Arg(0) == "on";
            enable.SetValue(settings, value, null);
            Runner.Require((bool)enable.GetValue(settings, null) == value, "RandomEvents did not accept " + step.Arg(0));
            AcceptLog.Info("RANDOM_EVENTS " + step.Arg(0));
        }

        private static IEnumerable<Wait> ExpectModSetting(Step step)
        {
            object model = ResolveStatic(step.Arg(0));
            object value = model.GetType().GetProperty(step.Arg(1))?.GetValue(model, null);
            Runner.Require(string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), step.Arg(2), StringComparison.OrdinalIgnoreCase),
                step.Arg(1) + "=" + value + ", expected " + step.Arg(2));
            yield break;
        }

        // "Namespace.Type::StaticMember.InstanceMember..." (properties or fields, public or not;
        // test tool), e.g. Spectator.SpectatorPlugin::processRoot.Settings.
        internal static object ResolveStatic(string path)
        {
            int split = path.IndexOf("::", StringComparison.Ordinal);
            Runner.Require(split > 0, "expected Type::Member, got " + path);
            string typeName = path.Substring(0, split);
            string[] members = path.Substring(split + 2).Split('.');
            Type type = FindType(typeName) ?? throw new StepFailure("type not loaded: " + typeName);
            object value = Read(type, null, members[0], BindingFlags.Static);
            for (int i = 1; i < members.Length && value != null; i++)
                value = Read(value.GetType(), value, members[i], BindingFlags.Instance);
            return value ?? throw new StepFailure(path + " is null or missing");
        }

        internal static object Read(Type type, object owner, string member, BindingFlags scope)
        {
            BindingFlags flags = scope | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(member, flags | BindingFlags.DeclaredOnly);
                if (property != null) return property.GetValue(owner, null);
                FieldInfo field = current.GetField(member, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(owner);
            }
            throw new StepFailure(type.FullName + "." + member + " not found");
        }

        internal static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        // BepInEx flushes LogOutput.log on a timer; flush explicitly, then quit on the Unity thread
        // two seconds later (PerfProbe pattern).
        internal static IEnumerable<Wait> QuitSequence()
        {
            AcceptLog.Info("Quitting after scenario.");
            yield return new Delay(2000);
            try
            {
                foreach (BepInEx.Logging.ILogListener listener in BepInEx.Logging.Logger.Listeners)
                    if (listener is BepInEx.Logging.DiskLogListener disk) disk.LogWriter?.Flush();
            }
            catch (Exception error) { AcceptLog.Warn("disk log flush failed: " + error.Message); }
            Application.Quit();
        }
    }
}
