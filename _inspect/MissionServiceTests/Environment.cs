// Boundary doubles only. The service, state machine and public contracts are production sources.
using System.Reflection;
namespace BepInEx.Logging { public class ManualLogSource { } }
namespace R3
{
    public class Stream<T>
    {
        readonly List<Action<T>> observers = new();
        public IDisposable Subscribe(Action<T> observer) { observers.Add(observer); return new Token(() => observers.Remove(observer)); }
        public void Send(T value) { foreach (var observer in observers.ToArray()) observer(value); }
    }
    class Token(Action end) : IDisposable { public void Dispose() => end(); }
}
namespace SHCDESE.EventAPI
{
    public enum EventHookPhase { Pre, Post }
    public class MapEvent { public EventHookPhase Phase; public byte bMultiplayerSave; public int CampaignMapId; public long ReturnValue; }
    public class Channel { public R3.Stream<MapEvent> Observable = new(); }
    public static class MapLoaderR3EventHooks { public static Channel OnUnloadMap = new(), OnStartMap = new(); }
}
namespace MonoMod.RuntimeDetour
{
    public class HookConfig { public bool ManualApply; public string ID; }
    public class Hook : IDisposable
    {
        public static readonly List<Hook> All = new();
        public static int FailApplyAt;
        readonly MethodInfo method;
        public readonly Delegate Callback;
        public bool Applied, Disposed;
        public Hook(MethodInfo method, Delegate callback, HookConfig config) { this.method = method; Callback = callback; All.Add(this); }
        public T GenerateTrampoline<T>() where T : Delegate => (T)method.CreateDelegate(typeof(T));
        public void Apply() { if (FailApplyAt == All.IndexOf(this) + 1) throw new Exception("apply"); Applied = true; }
        public void Undo() => Applied = false;
        public void Dispose() => Disposed = true;
    }
}
namespace Shared
{
    public enum GameModeKind { Unknown, MapEditor, Campaign, VanillaTrail, CustomTrail, StandaloneMission, CustomGame, Tutorial, CoopTrail, SandsOfTime }
    public readonly struct GameModeSnapshot(GameModeKind kind) { public GameModeKind Kind => kind; public bool IsMissionContent => kind == GameModeKind.Campaign || kind == GameModeKind.VanillaTrail; }
    public static class GameModeHelper
    {
        public static GameModeSnapshot CaptureMission(bool multiplayer, bool save, int campaign, int trail, bool editor, EngineInterface.LoadMapReturnData? data, GameModeKind intent, int? coopTrail = null) => new(intent);
    }
}
namespace APIShared
{
    public enum NativeCapabilityState { Available, Faulted, ValidationFailed }
    public class NativeCapabilityDiagnostic(string id, NativeCapabilityState state, string unused, string reason) { public string Reason => reason; }
    public static class NativeCapabilityIds { public const string MissionLifecycle = "mission"; }
    public static class NativeApiLog
    {
        public static bool Fail;
        public static void Info(BepInEx.Logging.ManualLogSource log, string message) { if (Fail) throw new Exception("logger"); }
        public static void Error(BepInEx.Logging.ManualLogSource log, string message) { if (Fail) throw new Exception("logger"); }
    }
}
public static class Enums { public enum GameModes { FreeBuild } public enum SceneIDS { ActualMainGame, FrontEnd, Options } }
public class FileHeader { }
public class EngineInterface
{
    public struct MultiplayerSetupData { }
    public struct LoadMapReturnData
    {
        public int errorCode, playerID, mapSize, difficulty_level, skirmishTrail, skirmishTrailLevel, mission_level, siege_or_invasion, multiplayerMap, coopMissionID;
    }
    public static LoadMapReturnData newMapEditor(int size, int type, bool siege, bool multiplayer) => default;
    public static MultiplayerSetupData initMultiplayerGame(bool skirmish, byte[] restart, int coopTrail, int coopMission, bool test, bool custom, bool extreme) => default;
    public static LoadMapReturnData loadMap(int campaign, string file, bool dummy, bool save, int trail, int mission, bool classic) => default;
    public static LoadMapReturnData LoadSaveFile(string file) => default;
    public static LoadMapReturnData LoadMapFile(string file, bool editor) => default;
}
public class EditorDirector
{
    public static EditorDirector instance = new(); public int gameLocalPlayerID = 1;
    public static Func<bool> LoadBody;
    public void createNewMap(int size, Enums.GameModes mode, bool siege, bool multiplayer) { }
    public bool loadMapIntoEditor(string file, string name) => LoadBody?.Invoke() == true;
    public void LoadCampaignMap(int id, int difficulty) { }
    public void LoadSkirmishMap(int type, int id, int difficulty) { }
    public bool LoadCustomTrailMap(string name, int id, int difficulty) => false;
    public void loadSaveGame(string file, string name, FileHeader header) { }
    public void postLoading(EngineInterface.LoadMapReturnData data, bool thread, bool save) { }
}
namespace CrusaderDE
{
    public class HUD_IngameMenu { public class RestartMapInfo { } public class RestartSkirmishMapInfo { } }
    public class FRONT_StandaloneMission { public static void StartMap(HUD_IngameMenu.RestartMapInfo info) { } }
    public class FRONT_Multiplayer
    {
        public void StartSkirmishGame(HUD_IngameMenu.RestartSkirmishMapInfo info) { }
        public void RestartSkirmishGame(HUD_IngameMenu.RestartSkirmishMapInfo info) { }
    }
    public class HUD_Tutorial { public void StartTutorial() { } }
    public class MainViewModel { public Enums.SceneIDS CurrentScreenNo = Enums.SceneIDS.ActualMainGame; public void GoToScreen(Enums.SceneIDS target) { if (target != Enums.SceneIDS.Options) CurrentScreenNo = target; } }
}
public class Platform_Multiplayer
{
    public static Platform_Multiplayer Instance = new(); public bool Host;
    public class MPData { } public class MPGameMember { }
    public bool IsGameMemberHost() => Host;
    public void StartGame(EngineInterface.MultiplayerSetupData setup, FileHeader map, int trail, int mission) { }
    public void StartSave(EngineInterface.MultiplayerSetupData setup, FileHeader map) { }
    public bool processMessage(MPData data, MPGameMember member, bool thread) { Program.MessageBody?.Invoke(); return true; }
}
public class FatControler
{
    public static Enums.SceneIDS currentScene = Enums.SceneIDS.ActualMainGame;
    public void NewScene(Enums.SceneIDS target) { currentScene = target; }
    public void ExitApp() { }
}
