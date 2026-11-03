using System;
using System.Collections.Generic;

// SDK/game boundary doubles: the production defeat consumer is compiled unchanged.
// The fixture verifies activation and session/thread handoff, not native rendering/network delivery.
namespace BepInEx.Logging { public sealed class ManualLogSource { } }
namespace APIShared.GameModes
{
    public struct GameModeSnapshot
    {
        public bool IsRealMultiplayer, IsSingleplayerSkirmishMode;
        public string ToDiagnosticString() => IsRealMultiplayer ? "MP" : "SP";
    }
    public static class GameModeHelper { public static bool Editor; public static bool IsMapEditor() => Editor; }
}
namespace APIShared
{
    public sealed class NativeCapabilityDiagnostic { public string Reason; }
    public sealed class MissionLifecycleContext { public long SessionId; public GameModes.GameModeSnapshot Mode; }
    public sealed class MissionLifecycleNotification { public MissionLifecycleContext Context; }
    public interface IPlayerDefeatCapability { }
    public interface IPlayerDefeatStateCapability : IPlayerDefeatCapability
    {
        bool TryRegisterStateObserver(string id, Action<PlayerDefeatSnapshot> callback, out NativeCapabilityDiagnostic diagnostic);
    }
    public sealed class PlayerDefeatSnapshot
    {
        public long SessionId; public int PlayerId; public bool Readable = true, IsEliminated;
    }
    public sealed class DefeatBoundary : IPlayerDefeatStateCapability
    {
        public Action<PlayerDefeatSnapshot> Observer;
        public PlayerDefeatSnapshot Replay;
        public bool TryRegisterStateObserver(string id, Action<PlayerDefeatSnapshot> callback, out NativeCapabilityDiagnostic diagnostic)
        { Observer = callback; diagnostic = null; if (Replay != null) callback(Replay); return true; }
    }
    public sealed class LifecycleBoundary
    {
        public Action<MissionLifecycleNotification> Started, Ended;
        public MissionLifecycleNotification Replay;
        public bool TryRegisterObserver(string id, Action<MissionLifecycleNotification> start, Action<MissionLifecycleNotification> end,
            object initialization, out NativeCapabilityDiagnostic diagnostic)
        { Started = start; Ended = end; diagnostic = null; if (Replay != null) start(Replay); return true; }
    }
    public sealed class ApiBoundary
    {
        public readonly LifecycleBoundary Lifecycle = new LifecycleBoundary();
        public readonly DefeatBoundary Defeat = new DefeatBoundary();
        public bool TryGetMissionLifecycle(string owner, out LifecycleBoundary lifecycle, out NativeCapabilityDiagnostic diagnostic)
        { lifecycle = Lifecycle; diagnostic = null; return true; }
        public bool TryGetPlayerDefeat(string owner, out IPlayerDefeatCapability defeat, out NativeCapabilityDiagnostic diagnostic)
        { defeat = Defeat; diagnostic = null; return true; }
    }
    public static class ApiShared { public static ApiBoundary Current; }
    public readonly struct ObserverCommandRequest
    {
        public ObserverCommandRequest(int observer, int viewed, CrusaderDE.Enums.GameActionCommand command, int structure, int state, int value2)
        { ObserverPlayerId = observer; ViewedPlayerId = viewed; Command = command; }
        public int ObserverPlayerId { get; }
        public int ViewedPlayerId { get; }
        public CrusaderDE.Enums.GameActionCommand Command { get; }
    }
    public static class ObserverCommandExceptions
    {
        public static Func<ObserverCommandRequest, bool> Allow;
        public static ObserverCommandRequest Last;
        public static bool IsAllowed(ObserverCommandRequest request) { Last = request; return Allow != null && Allow(request); }
    }
    public static class PlayerPerspectiveAPI
    {
        public static int Native = 3;
        public static int GetControlledPlayerId() => CrusaderDE.EditorDirector.instance.ActivePlayerID;
        public static int GetRawNativeViewPlayerId() => Native;
    }
}
namespace APIShared.Presentation
{
    public enum MissionOverOperation { State, Text }
    public sealed class MissionOverArgs { public int Result; }
    public static class MissionOverEvents
    {
        public static readonly List<Action<MissionOverArgs>> Before = new List<Action<MissionOverArgs>>();
        public static bool TryRegister(MissionOverOperation operation, string owner, string id,
            Action<MissionOverArgs> before, object after, out string reason)
        { Before.Add(before); reason = null; return true; }
    }
}
namespace APIShared.Commands
{
    public sealed class GameActionPreEventArgs
    {
        public CrusaderDE.Enums.GameActionCommand Command;
        public int StructureId, ActionState, Value2;
        public bool SkipOriginalFunction;
    }
    public static class GameActionEvents
    {
        public static readonly List<KeyValuePair<int, Action<GameActionPreEventArgs>>> Before = new List<KeyValuePair<int, Action<GameActionPreEventArgs>>>();
        public static bool TryRegister(string owner, string id, Action<GameActionPreEventArgs> before,
            object after, out string reason, int order = 0)
        { Before.Add(new KeyValuePair<int, Action<GameActionPreEventArgs>>(order, before)); Before.Sort((a,b) => a.Key.CompareTo(b.Key)); reason = null; return true; }
        public static bool Allowed(CrusaderDE.Enums.GameActionCommand command)
        {
            var args = new GameActionPreEventArgs { Command = command };
            foreach (var entry in Before) entry.Value(args);
            return !args.SkipOriginalFunction;
        }
    }
}
namespace CrusaderDE
{
    public static class Enums
    {
        // Exact mirror of the installed game enum; Test-CompiledContracts.ps1 enforces equality.
        public enum GameActionCommand
        {
            GateHouseState = 1001, RepairBuilding = 1002, ToggleSleep = 1003, SetTaxRate = 1004, SetRationing = 1005,
            SetFoodEaten = 1006, MakeTroop = 1007, DrawbridgeState = 1008, OpenDogCage = 1009, Troops_Stop = 1010,
            Troops_Disband = 1011, Troops_AttackHere = 1012, Troops_Cow = 1013, EngBuild_Catapult = 1014,
            EngBuild_Trebuchet = 1015, EngBuild_SiegeTower = 1016, EngBuild_Shield = 1017,
            EngBuild_BatteringRam = 1018, Troops_Patrol = 1019, Troops_ChangeStance = 1020, Troops_DeSelectType = 1021,
            Troops_DeSelectExceptType = 1022, SetNextWeaponMade = 1023, BuyGoods = 1024, SellGoods = 1025,
            CycleBookmarks = 1026, SetCurrentTradedGood = 1027, RadarClicked = 1028, AlignSprites = 1029,
            ResizeRadarMapBuffer = 1030, FreeBuild_Event = 1031, FreeBuild_InvasionCharSet = 1032,
            FreeBuild_InvasionCount = 1033, FreeBuild_InvasionStart = 1034, ActionPointClicked = 1035, Undo = 1036,
            SetStartingTeam = 1037, Set_AI_Patrolling = 1038, Fix_Tribes = 1039, CloseTroopsPanel = 1040,
            AmmoRecharge = 1041, AI_Target_Special = 1042, CentreMarker = 1043, SH1Cheats = 1044,
            HideObjectiveProgress = 1045, SetRadarMapRotationType = 1046, SelectBuildingType = 1047,
            Game_Paused = 1048, RotateBuilding = 1049, Autotrade_Pause = 1050, Autotrade_OnOff = 1051,
            Autotrade_SetBuy = 1052, Autotrade_SetSell = 1053, Autotrade_Apply = 1054, SetOutpostState = 1055,
            SetOutpostSize = 1056, SetOutpostDelay = 1057, GenieSpeech = 1058, LordType = 1059,
            EngBuild_ArabBallista = 1060, Ally_Orders = 1061, Ally_CancelGoodsReq = 1062, Ally_SendGoods = 1063,
            Ally_RequestGoods = 1064, Ally_ConfirmOrders = 1065, Ally_CancelOrders = 1066, ShowPlannedMoat = 1067,
            ExtremePower = 1068, SkirmishInsult = 1069, MPConfig = 1070, ResetTime = 1071, SandsCheatAllowed = 1072,
            SpectatorMode = 1073, Scenario_Set_Starting_Month = 2000, Scenario_Set_Starting_Year = 2001,
            Scenario_Set_Trading = 2002, Scenario_Set_Special = 2003, Scenario_Set_StartingGoods = 2004,
            Scenario_Set_Starting_Popularity = 2005, Scenario_Set_Starting_Special_Gold = 2006,
            Scenario_Set_Starting_Special_Rations = 2007, Scenario_Set_Starting_Special_Tax = 2008,
            Scenario_Set_BuildingAvailable = 2009, Scenario_Set_TroopAvailable = 2010,
            Scenario_Set_SwordAvailable = 2011, Scenario_Set_XbowAvailable = 2012, Scenario_Set_PikeAvailable = 2013,
            EditorWipeGoods = 2015, RespawnLord = 2016, WipeAnimals = 2017, HoldTime = 2018, SetMarkerState = 2019,
            FastGoodsFeedin = 2020, Scenario_Set_MaceAvailable = 2021, Scenario_Set_bowAvailable = 2022,
            Scenario_Set_SpearAvailable = 2023, Scenario_Set_MercTroopAvailable = 2024,
            Scenario_Set_BedouinTroopAvailable = 2025, Balanced = 2026
        }
        public enum SceneIDS { ActualMainGame }
    }
    public static class FatControler { public static Enums.SceneIDS currentScene; }
    public sealed class Director { public static Director instance = new Director(); public bool SimRunning = true; }
    public sealed class EditorDirector { public static EditorDirector instance = new EditorDirector(); public int ActivePlayerID = 3; }
    public sealed class GameData { public static GameData Instance = new GameData(); public EngineInterface.PlayState lastGameState = new EngineInterface.PlayState(); }
    public static class EngineInterface
    {
        public sealed class PlayState { public int spectatorMode; }
        public static int Calls;
        public static void GameAction(Enums.GameActionCommand command, int id, int state) { Calls++; }
    }
    public sealed class Platform_Multiplayer
    {
        public sealed class MPGameMember { public bool kicked, skirmishAI; public int playerID = 3; public ulong steamID = 1234; }
        public static Platform_Multiplayer Instance = new Platform_Multiplayer();
        public MPGameMember Member = new MPGameMember();
        public MPGameMember getPlayer(int id) => Member;
    }
}
namespace Steamworks
{
    public struct CSteamID { public ulong m_SteamID; }
    public static class SteamUser { public static CSteamID GetSteamID() => new CSteamID { m_SteamID = 1234 }; }
}
namespace Shared
{
    public static class DebugLogHelper
    {
        public static void LogErrorLimited(BepInEx.Logging.ManualLogSource log, string key, string text) { }
        public static void LogDebugLimited(BepInEx.Logging.ManualLogSource log, string key, string text) { }
        public static readonly List<string> Messages = new List<string>();
        public static void LogInfo(BepInEx.Logging.ManualLogSource log, string text) { Messages.Add(text); }
        public static void LogWarning(BepInEx.Logging.ManualLogSource log, string text) { Messages.Add(text); }
    }
}
namespace Spectator
{
    public static class SpectatorPlugin { public const string PluginGuid = "consumer.fixture"; }
    internal sealed class SpectatorViewModel
    {
        public SpectatorFeatureActivation Activation = new SpectatorFeatureActivation(true, true, true, true, true, true);
    }
    internal static class SpectatorPerspectiveRuntime
    {
        public static bool Original;
        public static int Confirmed;
        public static bool AllyAllowed, AllyThrows;
        public static bool IsOriginalSpectator() => Original;
        public static void OnEliminatedSpectatorConfirmed() { Confirmed++; }
        public static int DefeatChanges;
        public static void OnDefeatStateChanged() { DefeatChanges++; }
        public static bool CanIssueAllyAction(CrusaderDE.Enums.GameActionCommand command, int structure, int state, int value)
        { if (AllyThrows) throw new InvalidOperationException("No current native ally snapshot."); return AllyAllowed; }
    }
    internal static partial class SpectatorRuntime
    {
        public sealed class StatisticsBoundary { public int Invalidated; public void InvalidateMenu() { Invalidated++; } }
        public static StatisticsBoundary Statistics = new StatisticsBoundary();
        public static readonly Queue<Action> Pending = new Queue<Action>();
        public static int Invalidated;
        public static int HudClosed;
        public static bool HudCloseThrows;
        public static void PostPresentation(Action action) => Pending.Enqueue(action);
        public static void InvalidateToolbar() { Invalidated++; }
        public static void PrepareEliminatedObserverHud()
        {
            if (HudCloseThrows) throw new InvalidOperationException("HUD is being replaced.");
            if (SpectatorDefeat.IsEliminatedSpectator) throw new InvalidOperationException("Observer role granted before retiring the participant HUD.");
            if (SpectatorPerspectiveRuntime.Confirmed != 0) throw new InvalidOperationException("Old interactive HUD closed after admitting foreign view.");
            HudClosed++;
        }
        public static void Drain() { while (Pending.Count > 0) Pending.Dequeue()(); }
    }
    internal static class SpectatorDefeatRuntimeTests
    {
        private static readonly APIShared.GameModes.GameModeSnapshot Single = new APIShared.GameModes.GameModeSnapshot { IsSingleplayerSkirmishMode = true };
        private static readonly APIShared.GameModes.GameModeSnapshot Network = new APIShared.GameModes.GameModeSnapshot { IsRealMultiplayer = true };

        private static APIShared.MissionLifecycleNotification Session(long id, APIShared.GameModes.GameModeSnapshot mode) =>
            new APIShared.MissionLifecycleNotification { Context = new APIShared.MissionLifecycleContext { SessionId = id, Mode = mode } };
        private static APIShared.PlayerDefeatSnapshot State(long session, int player, bool eliminated, bool readable = true) =>
            new APIShared.PlayerDefeatSnapshot { SessionId = session, PlayerId = player, IsEliminated = eliminated, Readable = readable };

        private static SpectatorViewModel Begin(APIShared.GameModes.GameModeSnapshot mode, bool hostEnabled = true, bool loadedLoss = false)
        {
            APIShared.ApiShared.Current = new APIShared.ApiBoundary();
            APIShared.Presentation.MissionOverEvents.Before.Clear();
            APIShared.Commands.GameActionEvents.Before.Clear();
            SpectatorRuntime.Pending.Clear(); SpectatorRuntime.Invalidated = 0;
            SpectatorRuntime.HudClosed = 0; SpectatorRuntime.HudCloseThrows = false;
            SpectatorRuntime.Statistics = new SpectatorRuntime.StatisticsBoundary();
            SpectatorPerspectiveRuntime.Original = false; SpectatorPerspectiveRuntime.Confirmed = 0;
            SpectatorPerspectiveRuntime.AllyAllowed = SpectatorPerspectiveRuntime.AllyThrows = false;
            CrusaderDE.GameData.Instance.lastGameState = new CrusaderDE.EngineInterface.PlayState();
            CrusaderDE.EditorDirector.instance.ActivePlayerID = 3;
            CrusaderDE.Platform_Multiplayer.Instance.Member = new CrusaderDE.Platform_Multiplayer.MPGameMember();
            CrusaderDE.Director.instance.SimRunning = true;
            APIShared.PlayerPerspectiveAPI.Native = 3; CrusaderDE.EngineInterface.Calls = 0;
            APIShared.GameModes.GameModeHelper.Editor = false;
            APIShared.ObserverCommandExceptions.Allow = null; Shared.DebugLogHelper.Messages.Clear();
            var settings = new SpectatorViewModel { Activation = new SpectatorFeatureActivation(hostEnabled, true, true, true, true, true) };
            if (loadedLoss)
            {
                APIShared.ApiShared.Current.Lifecycle.Replay = Session(17, mode);
                APIShared.ApiShared.Current.Defeat.Replay = State(17, 3, true);
            }
            new SpectatorDefeat(new BepInEx.Logging.ManualLogSource(), settings).Initialize();
            if (!loadedLoss) APIShared.ApiShared.Current.Lifecycle.Started(Session(17, mode));
            return settings;
        }

        internal static void Run(Action<bool, string> check)
        {
            Begin(Single);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, false));
            SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 0 && !SpectatorDefeat.IsEliminatedParticipant,
                "a lordless/ordinary baseline with no confirmed elimination grants no spectator mode");
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            check(SpectatorRuntime.Statistics.Invalidated == 0 && SpectatorRuntime.Invalidated == 0,
                "a simulation state callback queues UI invalidation instead of changing bound UI");
            var result = new APIShared.Presentation.MissionOverArgs { Result = 1 };
            APIShared.Presentation.MissionOverEvents.Before[0](result);
            check(result.Result == 2, "confirmed local elimination marks defeat presentation before the later render confirmation");
            SpectatorRuntime.Drain(); SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 1 && !SpectatorDefeat.IsEliminatedSpectator,
                "local spectator action runs once and waits for its native flag to appear in PlayState");
            CrusaderDE.GameData.Instance.lastGameState.spectatorMode = 1;
            SpectatorDefeat.ObservePresentedState();
            check(SpectatorDefeat.IsEliminatedSpectator && SpectatorPerspectiveRuntime.Confirmed == 1 &&
                APIShared.PlayerPerspectiveAPI.GetControlledPlayerId() == 3, "confirmation grants the observer role without replacing participant identity");
            check(SpectatorRuntime.HudClosed == 1,
                "interactive participant HUD is retired before admitting the first spectator viewpoint");
            CheckObserverCommands(check);
            SpectatorPerspectiveRuntime.AllyAllowed = true;
            check(APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.Ally_Orders),
                "offline ally actions retain the explicitly authorized spectator policy");
            SpectatorPerspectiveRuntime.AllyThrows = true;
            check(!APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.Ally_Orders),
                "a failing ally predicate keeps its veto rather than rolling back into Vanilla");
            SpectatorPerspectiveRuntime.AllyThrows = false;
            bool selectedTestPower = false;
            APIShared.Commands.GameActionEvents.TryRegister("test", "authorized-offline-power", command => {
                if (command.Command == CrusaderDE.Enums.GameActionCommand.ExtremePower)
                { selectedTestPower = true; command.SkipOriginalFunction = true; }
            }, null, out _, order: 0);
            check(!APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.ExtremePower) && selectedTestPower,
                "the earlier authorized offline power selector can consume its command without executing the native gameplay action");
            APIShared.PlayerPerspectiveAPI.Native = 7;
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            check(SpectatorDefeat.IsEliminatedSpectator && CrusaderDE.EngineInterface.Calls == 1 &&
                SpectatorPerspectiveRuntime.Confirmed == 1, "duplicate notifications and later foreign views do not repeat promotion or identity confirmation");
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, false, readable: false));
            check(SpectatorDefeat.IsEliminatedSpectator,
                "a transient unreadable snapshot does not restore active-player input for a confirmed observer");

            Begin(Single, hostEnabled: false);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            check(SpectatorDefeat.IsEliminatedParticipant && !SpectatorDefeat.IsEliminatedSpectator &&
                CrusaderDE.EngineInterface.Calls == 0, "host-disabled elimination keeps statistics eligibility without granting observer/tool rights");

            Begin(Single, loadedLoss: true);
            SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 1, "a replayed current loss after loading activates without manufacturing a new lord-death event");

            Begin(Network);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 1, "a multiplayer client switches locally under the synchronized host setting without requiring local host status");
            CrusaderDE.GameData.Instance.lastGameState.spectatorMode = 1;
            SpectatorDefeat.ObservePresentedState();
            check(SpectatorDefeat.IsEliminatedSpectator && APIShared.PlayerPerspectiveAPI.GetControlledPlayerId() == 3,
                "network observer confirmation retains the real participant ID");
            CheckObserverCommands(check);
            SpectatorPerspectiveRuntime.AllyAllowed = true;
            check(!APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.Ally_Orders),
                "the central guard independently denies multiplayer ally mutations");

            Begin(Network);
            CrusaderDE.Platform_Multiplayer.Instance.Member.steamID = 9999;
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 0, "a mismatched local Steam participant cannot be promoted");

            Begin(Single);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            APIShared.ApiShared.Current.Lifecycle.Ended(Session(17, Single));
            APIShared.ApiShared.Current.Lifecycle.Started(Session(18, Single));
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorRuntime.Drain(); SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 0 && !SpectatorDefeat.IsEliminatedParticipant &&
                !SpectatorDefeat.IsEliminatedSpectator, "map replacement retires the old role, queued UI work and stale defeat state");
            APIShared.ApiShared.Current.Defeat.Observer(State(18, 2, true));
            APIShared.ApiShared.Current.Defeat.Observer(State(18, 3, true, readable: false));
            SpectatorDefeat.ObservePresentedState();
            check(CrusaderDE.EngineInterface.Calls == 0, "foreign or unreadable elimination states cannot grant local viewing rights");

            Begin(Single);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            APIShared.PlayerPerspectiveAPI.Native = 6;
            CrusaderDE.GameData.Instance.lastGameState.spectatorMode = 1;
            SpectatorDefeat.ObservePresentedState();
            check(!SpectatorDefeat.IsEliminatedSpectator, "unexpected native identity before confirmation fails closed");
            check(APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.SetTaxRate),
                "an unconfirmed ordinary participant retains Vanilla command semantics");

            Begin(Single);
            APIShared.ApiShared.Current.Defeat.Observer(State(17, 3, true));
            SpectatorDefeat.ObservePresentedState();
            CrusaderDE.GameData.Instance.lastGameState.spectatorMode = 1;
            SpectatorRuntime.HudCloseThrows = true;
            bool closeFailed = false;
            try { SpectatorDefeat.ObservePresentedState(); } catch (InvalidOperationException) { closeFailed = true; }
            check(closeFailed && !SpectatorDefeat.IsEliminatedSpectator && SpectatorPerspectiveRuntime.Confirmed == 0,
                "a failed old-HUD retirement grants no observer viewpoint");
            SpectatorRuntime.HudCloseThrows = false;
            SpectatorDefeat.ObservePresentedState();
            check(SpectatorDefeat.IsEliminatedSpectator && SpectatorRuntime.HudClosed == 1 && CrusaderDE.EngineInterface.Calls == 1,
                "the existing render path retries HUD retirement without repeating the spectator-mode action");
            Console.WriteLine("PASS: production eliminated-player consumer, local/MP activation, native confirmation, baseline replay and session retirement.");
        }

        private static void CheckObserverCommands(Action<bool, string> check)
        {
            // Every audited game value has exactly one class (the enum double mirrors the game enum).
            foreach (CrusaderDE.Enums.GameActionCommand command in Enum.GetValues(typeof(CrusaderDE.Enums.GameActionCommand)))
                check(SpectatorObserverActionPolicy.IsViewedPlayerAction(command) != SpectatorObserverActionPolicy.IsAuditedAllowed(command),
                    "observer policy classifies the audited command exactly once: " + command);
            foreach (var command in new[] {
                CrusaderDE.Enums.GameActionCommand.SetTaxRate, CrusaderDE.Enums.GameActionCommand.SetRationing,
                CrusaderDE.Enums.GameActionCommand.SetFoodEaten, CrusaderDE.Enums.GameActionCommand.BuyGoods,
                CrusaderDE.Enums.GameActionCommand.SellGoods, CrusaderDE.Enums.GameActionCommand.MakeTroop,
                CrusaderDE.Enums.GameActionCommand.ToggleSleep, CrusaderDE.Enums.GameActionCommand.RepairBuilding,
                CrusaderDE.Enums.GameActionCommand.Troops_Disband, CrusaderDE.Enums.GameActionCommand.Troops_Stop,
                CrusaderDE.Enums.GameActionCommand.ExtremePower, CrusaderDE.Enums.GameActionCommand.SelectBuildingType,
                (CrusaderDE.Enums.GameActionCommand)int.MaxValue })
                check(!APIShared.Commands.GameActionEvents.Allowed(command), "local observer command acting as the viewed player (or unaudited) blocked: " + command);
            foreach (var command in new[] {
                CrusaderDE.Enums.GameActionCommand.CycleBookmarks, CrusaderDE.Enums.GameActionCommand.RadarClicked,
                CrusaderDE.Enums.GameActionCommand.AlignSprites, CrusaderDE.Enums.GameActionCommand.ResizeRadarMapBuffer,
                CrusaderDE.Enums.GameActionCommand.CentreMarker, CrusaderDE.Enums.GameActionCommand.HideObjectiveProgress,
                CrusaderDE.Enums.GameActionCommand.SetRadarMapRotationType, CrusaderDE.Enums.GameActionCommand.Game_Paused,
                CrusaderDE.Enums.GameActionCommand.ShowPlannedMoat, CrusaderDE.Enums.GameActionCommand.SpectatorMode,
                // ESC and the Reports button; the observer's selection was cleared with its own view.
                CrusaderDE.Enums.GameActionCommand.CloseTroopsPanel,
                CrusaderDE.Enums.GameActionCommand.Scenario_Set_StartingGoods, CrusaderDE.Enums.GameActionCommand.Undo })
                check(APIShared.Commands.GameActionEvents.Allowed(command), "menu/presentation/global observer command preserved: " + command);
            check(Shared.DebugLogHelper.Messages.Exists(m => m.Contains("SPECTATOR_OBSERVER_UNAUDITED_GAMEACTION_BLOCKED")) &&
                Shared.DebugLogHelper.Messages.FindAll(m => m.Contains("command=SetTaxRate ")).Count <= 1,
                "blocked commands are reported once per command, unaudited ones distinctly");

            // Synchronized simulation/Chore calls (another thread) run on every peer and are never vetoed.
            bool simulationAllowed = false;
            var simulation = new System.Threading.Thread(() =>
                simulationAllowed = APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.FreeBuild_Event));
            simulation.Start(); simulation.Join();
            check(simulationAllowed, "a simulation-thread GameAction (e.g. a Chore callback) is never vetoed for an observer");

            // Cross-mod exception: decided per observer and request, seeing the viewed player.
            APIShared.ObserverCommandExceptions.Allow = r => r.Command == CrusaderDE.Enums.GameActionCommand.SetTaxRate && r.ObserverPlayerId == 3;
            check(APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.SetTaxRate) &&
                APIShared.ObserverCommandExceptions.Last.ViewedPlayerId == APIShared.PlayerPerspectiveAPI.Native,
                "a registered exception lets the observer issue that command");
            check(!APIShared.Commands.GameActionEvents.Allowed(CrusaderDE.Enums.GameActionCommand.BuyGoods),
                "an exception for one command does not open others");
            APIShared.ObserverCommandExceptions.Allow = null;
        }
    }
}
