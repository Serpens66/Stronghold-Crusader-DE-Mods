using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.EventAPI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace APIShared
{
    internal sealed class EditorMapLifecycleService
    {
        private delegate void CreateMap(EditorDirector self, int size, Enums.GameModes mode, bool siege, bool multiplayer);
        private delegate bool LoadMap(EditorDirector self, string file, string name);
        private delegate EngineInterface.LoadMapReturnData NewMap(int size, int type, bool siege, bool multiplayer);
        private delegate void GoToScreen(MainViewModel self, Enums.SceneIDS scene);
        private readonly ManualLogSource log;
        private readonly int unityThreadId;
        private readonly EditorMapLifecycleState state;
        private readonly List<Hook> hooks = new List<Hook>();
        private readonly Stack<Creation> creations = new Stack<Creation>();
        private CreateMap createOriginal;
        private LoadMap loadOriginal;
        private NewMap newOriginal;
        private GoToScreen screenOriginal;
        private IDisposable unload;

        private EditorMapLifecycleService(ManualLogSource log)
        {
            this.log = log;
            unityThreadId = Thread.CurrentThread.ManagedThreadId;
            state = new EditorMapLifecycleState(ex => NativeApiLog.Error(log, $"Editor lifecycle observer failed: {ex}"));
            state.Register("APIShared_Serp", "diagnostics", e => NativeApiLog.Info(log,
                $"EditorMap{e.Kind}: session={e.SessionId}, origin={e.Origin}, reason={e.EndReason}, replay={e.IsReplay}."));
        }

        internal static bool TryCreate(ManualLogSource log, out EditorMapLifecycleService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            var candidate = new EditorMapLifecycleService(log);
            service = null;
            try
            {
                candidate.Prepare();
                foreach (var hook in candidate.hooks) hook.Apply();
                candidate.unload = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(e => e.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => candidate.state.ObserveNativeUnload());
                service = candidate;
                diagnostic = Status(NativeCapabilityState.Available, "Process-wide editor lifecycle installed.");
                NativeApiLog.Info(log, diagnostic.Reason);
                return true;
            }
            catch (Exception ex)
            {
                candidate.unload?.Dispose();
                for (int i = candidate.hooks.Count - 1; i >= 0; i--)
                {
                    try { candidate.hooks[i].Undo(); } catch { }
                    try { candidate.hooks[i].Dispose(); } catch { }
                }
                diagnostic = Status(NativeCapabilityState.Faulted, ex.Message);
                NativeApiLog.Error(log, $"Editor lifecycle installation rolled back before publication: {ex}");
                return false;
            }
        }

        private void Prepare()
        {
            createOriginal = Prepare<CreateMap>(typeof(EditorDirector), nameof(EditorDirector.createNewMap),
                new[] { typeof(int), typeof(Enums.GameModes), typeof(bool), typeof(bool) }, (CreateMap)Create);
            loadOriginal = Prepare<LoadMap>(typeof(EditorDirector), nameof(EditorDirector.loadMapIntoEditor),
                new[] { typeof(string), typeof(string) }, (LoadMap)Load);
            newOriginal = Prepare<NewMap>(typeof(EngineInterface), nameof(EngineInterface.newMapEditor),
                new[] { typeof(int), typeof(int), typeof(bool), typeof(bool) }, (NewMap)New);
            screenOriginal = Prepare<GoToScreen>(typeof(MainViewModel), nameof(MainViewModel.GoToScreen),
                new[] { typeof(Enums.SceneIDS) }, (GoToScreen)Screen);
        }

        private T Prepare<T>(Type type, string name, Type[] parameters, Delegate callback) where T : Delegate
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static, null, parameters, null)
                ?? throw new MissingMethodException(type.FullName, name);
            var hook = new Hook(method, callback, new HookConfig { ManualApply = true, ID = "APIShared.EditorLifecycle." + name });
            hooks.Add(hook);
            return hook.GenerateTrampoline<T>();
        }

        private void Create(EditorDirector self, int size, Enums.GameModes mode, bool siege, bool multiplayer)
        {
            var creation = new Creation();
            creations.Push(creation);
            try
            {
                state.Run(EditorMapOrigin.Created, null, () =>
                {
                    createOriginal(self, size, mode, siege, multiplayer);
                    return creation.NativeSucceeded;
                });
            }
            finally
            {
                creations.Pop();
            }
        }

        private EngineInterface.LoadMapReturnData New(int size, int type, bool siege, bool multiplayer)
        {
            var result = newOriginal(size, type, siege, multiplayer);
            if (creations.Count != 0) creations.Peek().NativeSucceeded = result.errorCode == 1;
            return result;
        }

        private bool Load(EditorDirector self, string file, string name)
        {
            return state.Run(EditorMapOrigin.Loaded, file, () => loadOriginal(self, file, name));
        }

        private void Screen(MainViewModel self, Enums.SceneIDS scene)
        {
            try { screenOriginal(self, scene); }
            finally
            {
                // Also retire a changed screen if later Vanilla initialization throws.
                // InitNewScene(MapEditor) is translated to ActualMainGame before GoToScreen.
                state.ObserveScreen(self.CurrentScreenNo == Enums.SceneIDS.ActualMainGame, self.IsMapEditorMode);
            }
        }

        internal IEditorMapLifecycleCapability Bind(string owner) => new Binding(this, owner);
        private static NativeCapabilityDiagnostic Status(NativeCapabilityState status, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.EditorMapLifecycle, status, string.Empty, reason);
        private sealed class Creation { internal bool NativeSucceeded; }
        private sealed class Binding : IEditorMapLifecycleCapability
        {
            private readonly EditorMapLifecycleService service;
            private readonly string owner;
            internal Binding(EditorMapLifecycleService service, string owner) { this.service = service; this.owner = owner; }
            public EditorMapLifecycleNotification Current => service.state.Current;
            public bool TryRegisterObserver(string registrationId, Action<EditorMapLifecycleNotification> observer,
                out NativeCapabilityDiagnostic diagnostic)
            {
                bool ok = Thread.CurrentThread.ManagedThreadId == service.unityThreadId &&
                    service.state.Register(owner, registrationId, observer);
                diagnostic = Status(ok ? NativeCapabilityState.Available : NativeCapabilityState.ValidationFailed,
                    ok ? "Editor observer registered." : "Registration requires the Unity thread, a callback and a unique owner-local ID.");
                return ok;
            }
        }
    }
}
