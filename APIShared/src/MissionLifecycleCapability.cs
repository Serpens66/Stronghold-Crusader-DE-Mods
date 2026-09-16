using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.EventAPI;
using Shared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace APIShared
{
    internal sealed class MissionLifecycleService
    {
        private delegate void CreateMap(EditorDirector self, int size, Enums.GameModes mode, bool siege, bool multiplayer);
        private delegate bool EditorLoad(EditorDirector self, string file, string name);
        private delegate EngineInterface.LoadMapReturnData NewMap(int size, int type, bool siege, bool multiplayer);
        private delegate void Campaign(EditorDirector self, int id, int difficulty);
        private delegate void Trail(EditorDirector self, int type, int id, int difficulty);
        private delegate bool CustomTrail(EditorDirector self, string name, int id, int difficulty);
        private delegate void Save(EditorDirector self, string file, string name, FileHeader header);
        private delegate void Standalone(HUD_IngameMenu.RestartMapInfo info);
        private delegate void Skirmish(FRONT_Multiplayer self, HUD_IngameMenu.RestartSkirmishMapInfo info);
        private delegate void Tutorial(HUD_Tutorial self);
        private delegate void MultiplayerNew(Platform_Multiplayer self, EngineInterface.MultiplayerSetupData setup, FileHeader map, int trail, int mission);
        private delegate void MultiplayerSave(Platform_Multiplayer self, EngineInterface.MultiplayerSetupData setup, FileHeader map);
        private delegate EngineInterface.MultiplayerSetupData MultiplayerInit(bool skirmish, byte[] restart, int coopTrail, int coopMission, bool test, bool custom, bool extreme);
        private delegate bool Message(Platform_Multiplayer self, Platform_Multiplayer.MPData data, Platform_Multiplayer.MPGameMember member, bool thread);
        private delegate void PostLoad(EditorDirector self, EngineInterface.LoadMapReturnData data, bool thread, bool save);
        private delegate EngineInterface.LoadMapReturnData NativeLoad(int campaign, string file, bool dummy, bool save, int trail, int mission, bool classic);
        private delegate EngineInterface.LoadMapReturnData SaveFile(string file);
        private delegate EngineInterface.LoadMapReturnData EditorFile(string file, bool editor);
        private delegate void Screen(MainViewModel self, Enums.SceneIDS scene);
        private delegate void Scene(FatControler self, Enums.SceneIDS scene);
        private delegate void Exit(FatControler self);

        private readonly ManualLogSource log;
        private readonly int unityThread;
        private readonly MissionLifecycleState state;
        private readonly List<Hook> hooks = new List<Hook>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private Operation executing;
        private Operation waiting;
        private static MissionLifecycleService published;
        internal static GameModeSnapshot Snapshot => published?.state.Context?.Mode ?? default;
        internal static bool HasContext => published?.state.Context != null;
        internal static MissionContext ActiveContext => published?.state.Context;

        private CreateMap create; private EditorLoad editorLoad; private NewMap newMap;
        private Campaign campaign; private Trail trail; private CustomTrail customTrail;
        private Save save; private Standalone standalone; private Skirmish skirmish, restartSkirmish; private Tutorial tutorial;
        private MultiplayerNew multiplayerNew; private MultiplayerSave multiplayerSave; private Message message;
        private MultiplayerInit multiplayerInit;
        private PostLoad postLoad; private NativeLoad nativeLoad; private SaveFile saveFile; private EditorFile editorFile;
        private Screen screen; private Scene scene; private Exit exit;

        private sealed class Operation
        {
            internal long Id;
            internal MissionStartKind Kind;
            internal GameModeKind Intent;
            internal string File, Name;
            internal int Campaign, Trail = -1;
            internal int? CoopTrail;
            internal int? MissionIndex;
            internal int? Size, Difficulty;
            internal bool SiegeThatEditor;
            internal bool Multiplayer, Restart, NativeFailed, NativeSucceeded, ManagedSucceeded;
            internal EngineInterface.LoadMapReturnData? Data;
        }

        private MissionLifecycleService(ManualLogSource log)
        {
            this.log = log;
            unityThread = Thread.CurrentThread.ManagedThreadId;
            state = new MissionLifecycleState(ex => Report(ex));
            state.Register("APIShared_Serp", "diagnostics", e => NativeApiLog.Info(log,
                $"Mission{e.Kind}: session={e.Context.SessionId}, source={e.Context.StartKind}, mode={e.Context.Mode.Kind}, " +
                $"phase={e.Phase}, reason={e.EndReason}, started={e.HasStarted}, replay={e.IsReplay}."));
        }

        internal static bool TryCreate(ManualLogSource log, out MissionLifecycleService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            var candidate = new MissionLifecycleService(log);
            service = null;
            try
            {
                candidate.Prepare();
                foreach (var hook in candidate.hooks) hook.Apply();
                candidate.Subscribe();
                published = service = candidate;
                diagnostic = Status(NativeCapabilityState.Available, "Process-wide mission lifecycle installed.");
                candidate.Safe(() => NativeApiLog.Info(log, "Process-wide mission lifecycle installed."));
                return true;
            }
            catch (Exception ex)
            {
                foreach (var subscription in candidate.subscriptions) { try { subscription.Dispose(); } catch { } }
                for (int i = candidate.hooks.Count - 1; i >= 0; i--)
                { try { candidate.hooks[i].Undo(); } catch { } try { candidate.hooks[i].Dispose(); } catch { } }
                diagnostic = Status(NativeCapabilityState.Faulted, ex.Message);
                candidate.Report(ex);
                return false;
            }
        }

        private T Hook<T>(Type type, string name, T callback) where T : Delegate
        {
            var signature = typeof(T).GetMethod("Invoke").GetParameters();
            bool instance = signature.Length > 0 && signature[0].ParameterType == type;
            var parameters = new Type[signature.Length - (instance ? 1 : 0)];
            for (int i = 0; i < parameters.Length; i++) parameters[i] = signature[i + (instance ? 1 : 0)].ParameterType;
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance, null, parameters, null)
                ?? throw new MissingMethodException(type.FullName, name);
            var hook = new Hook(method, callback, new HookConfig { ManualApply = true, ID = "APIShared.MissionLifecycle." + name });
            hooks.Add(hook);
            return hook.GenerateTrampoline<T>();
        }

        private void Prepare()
        {
            create = Hook<CreateMap>(typeof(EditorDirector), nameof(EditorDirector.createNewMap), (self, size, mode, siege, multi) =>
                Run(new Operation { Kind = MissionStartKind.EditorCreated, Intent = GameModeKind.MapEditor, Size = size, SiegeThatEditor = siege },
                    () => { create(self, size, mode, siege, multi); MarkManaged(); }));
            editorLoad = Hook<EditorLoad>(typeof(EditorDirector), nameof(EditorDirector.loadMapIntoEditor), (self, file, name) =>
                Run(Seed(MissionStartKind.EditorLoaded, GameModeKind.MapEditor, file, name), () =>
                { bool ok = editorLoad(self, file, name); if (ok) MarkManaged(); return ok; }));
            newMap = Hook<NewMap>(typeof(EngineInterface), nameof(EngineInterface.newMapEditor), (size, type, siege, multi) =>
                CaptureNative(() => newMap(size, type, siege, multi)));
            campaign = Hook<Campaign>(typeof(EditorDirector), nameof(EditorDirector.LoadCampaignMap), (self, id, difficulty) =>
                Run(new Operation { Kind = MissionStartKind.NewGame, Intent = GameModeKind.Campaign, Campaign = id, Difficulty = difficulty }, () => campaign(self, id, difficulty)));
            trail = Hook<Trail>(typeof(EditorDirector), nameof(EditorDirector.LoadSkirmishMap), (self, type, id, difficulty) =>
                Run(new Operation { Kind = MissionStartKind.NewGame, Intent = GameModeKind.VanillaTrail, Trail = type, MissionIndex = id, Difficulty = difficulty }, () => trail(self, type, id, difficulty)));
            customTrail = Hook<CustomTrail>(typeof(EditorDirector), nameof(EditorDirector.LoadCustomTrailMap), (self, name, id, difficulty) =>
                Run(new Operation { Kind = MissionStartKind.NewGame, Intent = GameModeKind.CustomTrail, Name = name, MissionIndex = id - 1, Difficulty = difficulty }, () => customTrail(self, name, id, difficulty)));
            save = Hook<Save>(typeof(EditorDirector), nameof(EditorDirector.loadSaveGame), (self, file, name, header) =>
                Run(Seed(MissionStartKind.LoadedSave, GameModeKind.Unknown, file, name), () => save(self, file, name, header)));
            standalone = Hook<Standalone>(typeof(FRONT_StandaloneMission), nameof(FRONT_StandaloneMission.StartMap), info =>
                Run(Seed(MissionStartKind.NewGame, GameModeKind.StandaloneMission), () => standalone(info)));
            skirmish = Hook<Skirmish>(typeof(FRONT_Multiplayer), "StartSkirmishGame", (self, info) =>
                Run(Seed(MissionStartKind.NewGame, GameModeKind.CustomGame), () => skirmish(self, info)));
            restartSkirmish = Hook<Skirmish>(typeof(FRONT_Multiplayer), nameof(FRONT_Multiplayer.RestartSkirmishGame), (self, info) =>
                Run(new Operation { Kind = MissionStartKind.NewGame, Intent = GameModeKind.CustomGame, Restart = true }, () => restartSkirmish(self, info)));
            tutorial = Hook<Tutorial>(typeof(HUD_Tutorial), nameof(HUD_Tutorial.StartTutorial), self =>
                Run(Seed(MissionStartKind.NewGame, GameModeKind.Tutorial), () => tutorial(self)));
            multiplayerNew = Hook<MultiplayerNew>(typeof(Platform_Multiplayer), nameof(Platform_Multiplayer.StartGame), (self, setup, map, coop, mission) =>
                Run(new Operation { Kind = MissionStartKind.NewGame, Intent = coop > 0 ? GameModeKind.CoopTrail : GameModeKind.CustomGame, Multiplayer = true },
                    () => multiplayerNew(self, setup, map, coop, mission)));
            multiplayerSave = Hook<MultiplayerSave>(typeof(Platform_Multiplayer), nameof(Platform_Multiplayer.StartSave), (self, setup, map) =>
                Run(new Operation { Kind = MissionStartKind.LoadedSave, Intent = GameModeKind.Unknown, Multiplayer = true }, () => multiplayerSave(self, setup, map)));
            multiplayerInit = Hook<MultiplayerInit>(typeof(EngineInterface), nameof(EngineInterface.initMultiplayerGame), (skirmishGame, restart, coop, mission, test, custom, extreme) =>
            {
                Safe(() =>
                {
                    if (executing == null) return;
                    executing.CoopTrail = coop;
                    if (coop > 0) { executing.Intent = GameModeKind.CoopTrail; executing.MissionIndex = mission; }
                    else if (custom && executing.Intent == GameModeKind.CustomGame) executing.Intent = GameModeKind.CustomTrail;
                });
                return multiplayerInit(skirmishGame, restart, coop, mission, test, custom, extreme);
            });
            message = Hook<Message>(typeof(Platform_Multiplayer), "processMessage", ProcessMessage);
            postLoad = Hook<PostLoad>(typeof(EditorDirector), nameof(EditorDirector.postLoading), (self, data, thread, fromSave) =>
            { postLoad(self, data, thread, fromSave); Safe(() => { if (executing != null && data.errorCode == 1) executing.ManagedSucceeded = true; }); });
            nativeLoad = Hook<NativeLoad>(typeof(EngineInterface), "loadMap", (id, file, dummy, fromSave, type, mission, classic) =>
            {
                Safe(() => { if (executing != null) { executing.File = file; executing.Campaign = id > 0 ? id : 0; executing.Trail = type; } });
                return CaptureNative(() => nativeLoad(id, file, dummy, fromSave, type, mission, classic));
            });
            saveFile = Hook<SaveFile>(typeof(EngineInterface), nameof(EngineInterface.LoadSaveFile), file => CaptureNative(() => saveFile(file)));
            editorFile = Hook<EditorFile>(typeof(EngineInterface), nameof(EngineInterface.LoadMapFile), (file, editor) => CaptureNative(() => editorFile(file, editor)));
            screen = Hook<Screen>(typeof(MainViewModel), nameof(MainViewModel.GoToScreen), (self, target) =>
            { try { screen(self, target); } finally { Safe(() => { if (self.CurrentScreenNo != Enums.SceneIDS.ActualMainGame) End(MissionEndReason.SceneChanged); }); } });
            scene = Hook<Scene>(typeof(FatControler), nameof(FatControler.NewScene), (self, target) =>
            { try { scene(self, target); } finally { Safe(() => { if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame) End(MissionEndReason.SceneChanged); }); } });
            exit = Hook<Exit>(typeof(FatControler), nameof(FatControler.ExitApp), self =>
            { Safe(() => End(MissionEndReason.ApplicationExit)); exit(self); });
        }

        private void Subscribe()
        {
            // These callbacks must NEVER escape into SE: its detour catch blocks can call Vanilla twice.
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(e => Safe(() =>
            { if (e.Phase == EventHookPhase.Pre && executing == null) End(MissionEndReason.Unloaded); })));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(e => Safe(() =>
            {
                var op = executing;
                if (op == null || !state.IsPending(op.Id)) return;
                if (e.bMultiplayerSave != 0) op.Kind = MissionStartKind.LoadedSave;
                if (e.CampaignMapId > 0) op.Campaign = e.CampaignMapId;
                if (e.Phase == EventHookPhase.Pre)
                    state.Checkpoint(op.Id, Capture(op), MissionInitializationPhase.BeforeNativeStart);
                else if (unchecked((uint)e.ReturnValue) == uint.MaxValue)
                    state.Checkpoint(op.Id, Capture(op), MissionInitializationPhase.AfterNativeStart);
                else op.NativeFailed = true;
            })));
        }

        private static Operation Seed(MissionStartKind kind, GameModeKind intent, string file = null, string name = null) =>
            new Operation { Kind = kind, Intent = intent, File = file, Name = name };

        private MissionContext Capture(Operation op)
        {
            GameModeSnapshot mode;
            try { mode = GameModeHelper.CaptureMission(op.Multiplayer, op.Kind == MissionStartKind.LoadedSave, op.Campaign, op.Trail,
                op.Intent == GameModeKind.MapEditor, op.Data, op.Intent, op.CoopTrail); }
            catch (Exception ex) { Report(ex); mode = default; } // Unknown evidence never opens a mode gate.
            var data = op.Data;
            int? player = data.HasValue && data.Value.playerID >= 1 && data.Value.playerID <= 8 ? data.Value.playerID : (int?)null;
            bool? host = null;
            try
            {
                if (op.ManagedSucceeded && EditorDirector.instance != null)
                {
                    int assigned = EditorDirector.instance.gameLocalPlayerID;
                    player = assigned >= 1 && assigned <= 8 ? assigned : (int?)null;
                }
                if (op.Multiplayer) host = Platform_Multiplayer.Instance.IsGameMemberHost();
            }
            catch (Exception ex) { Report(ex); }
            int? mission = op.MissionIndex;
            if (!mission.HasValue && data.HasValue)
            {
                if (mode.Kind == GameModeKind.CoopTrail) mission = data.Value.coopMissionID;
                else if (mode.Kind == GameModeKind.Campaign) mission = data.Value.mission_level;
                else if (data.Value.skirmishTrail >= 0 &&
                    (mode.Kind == GameModeKind.VanillaTrail || mode.Kind == GameModeKind.SandsOfTime)) mission = data.Value.skirmishTrailLevel;
            }
            if (mission < 0) mission = null;
            return new MissionContext(op.Id, op.Kind, mode, op.File, op.Name, op.Restart,
                data.HasValue && data.Value.mapSize > 0 ? data.Value.mapSize : op.Size,
                data.HasValue && data.Value.difficulty_level >= 0 ? data.Value.difficulty_level : op.Difficulty, player,
                mission, host,
                data.HasValue ? MapType(data.Value.siege_or_invasion) : MissionMapType.Unknown,
                data.HasValue ? data.Value.multiplayerMap != 0 : (bool?)null, op.SiegeThatEditor);
        }

        // LoadMapReturnData uses the native serialized subtype order, not Enums.GameModes values.
        internal static MissionMapType MapType(int nativeSubtype)
        {
            switch (nativeSubtype)
            {
                case 0: return MissionMapType.Siege;
                case 1: return MissionMapType.Invasion;
                case 2: return MissionMapType.Economy;
                case 3: return MissionMapType.FreeBuild;
                default: return MissionMapType.Unknown;
            }
        }

        private void Run(Operation op, Action original) => Run(op, () => { original(); return true; });
        private T Run<T>(Operation op, Func<T> original)
        {
            if (executing != null) return original(); // Nested custom-trail -> skirmish belongs to the outer attempt.
            executing = op;
            waiting = null;
            bool normal = false;
            Safe(() => op.Id = state.Begin(Capture(op)));
            try { T result = original(); normal = true; return result; }
            finally
            {
                executing = null;
                Safe(() =>
                {
                    var context = Capture(op);
                    if (state.Finish(op.Id, context, normal, op.NativeSucceeded && !op.NativeFailed,
                        op.ManagedSucceeded, op.Multiplayer && context.IsHost == false)) waiting = op;
                });
            }
        }

        private bool ProcessMessage(Platform_Multiplayer self, Platform_Multiplayer.MPData data,
            Platform_Multiplayer.MPGameMember member, bool thread)
        {
            var op = waiting;
            if (thread || op == null || !state.IsPending(op.Id) || executing != null) return message(self, data, member, thread);
            executing = op;
            bool normal = false;
            try { bool result = message(self, data, member, thread); normal = true; return result; }
            finally
            {
                executing = null;
                Safe(() =>
                {
                    if (!normal) { waiting = null; state.End(MissionEndReason.Exception, op.Id); }
                    else if (op.ManagedSucceeded) { waiting = null; state.Ready(op.Id, Capture(op)); }
                });
            }
        }

        private EngineInterface.LoadMapReturnData CaptureNative(Func<EngineInterface.LoadMapReturnData> original)
        {
            var op = executing;
            Safe(() => { if (op != null) state.Checkpoint(op.Id, Capture(op), MissionInitializationPhase.BeforeLoad); });
            var result = original();
            Safe(() =>
            {
                if (op == null || !state.IsPending(op.Id)) return; // Noninteractive export is not a mission.
                op.Data = result;
                op.NativeSucceeded = result.errorCode == 1;
                if (op.NativeSucceeded && !op.NativeFailed)
                    state.Checkpoint(op.Id, Capture(op), MissionInitializationPhase.NativeLoaded);
            });
            return result;
        }
        private void MarkManaged() => Safe(() => { if (executing != null) executing.ManagedSucceeded = true; });
        private void End(MissionEndReason reason) { waiting = null; state.End(reason); }
        private void Safe(Action action) { try { action(); } catch (Exception ex) { Report(ex); } }
        private void Report(Exception ex) { try { NativeApiLog.Error(log, "Mission lifecycle callback failed: " + ex); } catch { } }
        private static NativeCapabilityDiagnostic Status(NativeCapabilityState state, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.MissionLifecycle, state, string.Empty, reason);
        internal IMissionLifecycleCapability Bind(string owner) => new Binding(this, owner);
        private sealed class Binding : IMissionLifecycleCapability
        {
            private readonly MissionLifecycleService service;
            private readonly string owner;
            internal Binding(MissionLifecycleService service, string owner) { this.service = service; this.owner = owner; }
            public MissionContext Current => service.state.Current;
            public bool TryRegisterObserver(string id, Action<MissionLifecycleNotification> onStart,
                Action<MissionLifecycleNotification> onEnd, Action<MissionLifecycleNotification> onInitialization,
                out NativeCapabilityDiagnostic diagnostic)
            {
                bool ok = Thread.CurrentThread.ManagedThreadId == service.unityThread &&
                    (onStart != null || onEnd != null || onInitialization != null) && service.state.Register(owner, id, e =>
                    { if (e.Kind == MissionLifecycleKind.Start) onStart?.Invoke(e); else if (e.Kind == MissionLifecycleKind.End) onEnd?.Invoke(e); else onInitialization?.Invoke(e); });
                diagnostic = Status(ok ? NativeCapabilityState.Available : NativeCapabilityState.ValidationFailed,
                    ok ? "Mission observer registered." : "Registration requires the Unity thread and a unique owner-local ID.");
                return ok;
            }
        }
    }
}
