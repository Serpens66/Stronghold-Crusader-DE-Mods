using BepInEx.Logging;
#if !SHARED_PRESET_TESTS
using R3;
#endif
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
#if !SHARED_PRESET_TESTS
using APIShared;
using System.Linq;
using BepInEx;
#endif

namespace Shared
{
    internal enum GameplaySessionStartKind
    {
        NewMap,
        LoadedSave,
        EditorCreated,
        EditorLoaded,
    }

    internal sealed class GameplaySessionStartedContext
    {
        internal GameplaySessionStartKind Kind { get; }
        internal GameModeSnapshot Mode { get; }
        internal MapStartEventArgs MapStart { get; }
        internal LoadSaveGameEventArgs SaveLoad { get; }

        internal bool IsLoadedSave => Kind == GameplaySessionStartKind.LoadedSave;
        internal bool IsEditor => Kind == GameplaySessionStartKind.EditorCreated || Kind == GameplaySessionStartKind.EditorLoaded;
        internal bool LoadingEditorMap => Kind == GameplaySessionStartKind.EditorLoaded;
        internal long EditorSessionId { get; private set; }
        internal bool IsReplay { get; private set; }
        private string editorFilePath;
        internal string SaveFileName => IsEditor ? editorFilePath : SaveLoad?.FileName;

        internal static GameplaySessionStartedContext FromEditor(long id, bool loaded, string path, bool replay) =>
            new GameplaySessionStartedContext(loaded ? GameplaySessionStartKind.EditorLoaded : GameplaySessionStartKind.EditorCreated,
                GameModeHelper.CaptureEditorSession(), null, null)
            { EditorSessionId = id, editorFilePath = path, IsReplay = replay };

        internal GameplaySessionStartedContext AsEditorReplay() =>
            new GameplaySessionStartedContext(Kind, Mode, null, null)
            { EditorSessionId = EditorSessionId, editorFilePath = editorFilePath, IsReplay = true };

        private GameplaySessionStartedContext(
            GameplaySessionStartKind kind,
            GameModeSnapshot mode,
            MapStartEventArgs mapStart,
            LoadSaveGameEventArgs saveLoad)
        {
            Kind = kind;
            Mode = mode;
            MapStart = mapStart;
            SaveLoad = saveLoad;
        }

        internal static GameplaySessionStartedContext FromNewMap(MapStartEventArgs args) =>
            new GameplaySessionStartedContext(
                GameplaySessionStartKind.NewMap,
                GameModeHelper.Capture(args),
                args,
                null);

        internal static GameplaySessionStartedContext FromLoadedSave(LoadSaveGameEventArgs args) =>
            new GameplaySessionStartedContext(
                GameplaySessionStartKind.LoadedSave,
                GameModeHelper.Capture(args),
                null,
                args);
    }

    /// <summary>
    /// Normalizes the point at which native gameplay state is available after either a new-map
    /// start or a successful save load. Save-load Pre and the nested unload notifications are
    /// deliberately ignored; existing ModSaveData handlers have restored their state before the
    /// final Post notification reaches subscriptions installed by a mod runtime.
    /// Editor sessions instead come exclusively from APIShared after managed initialization;
    /// their activation gate runs before feature subscribers, including synchronous replay.
    /// </summary>
    internal static class GameplaySessionLifecycle
    {
        private static readonly List<EditorSubscriber> editorSubscribers = new List<EditorSubscriber>();
        private static GameplaySessionStartedContext activeEditor;
        private static Action<GameplaySessionStartedContext> editorGate;
        private static Action editorGateEnded;
#if !SHARED_PRESET_TESTS
        private static bool editorConnected;
        private static void EnsureEditorConnected()
        {
            if (editorConnected) return;
            string owner = typeof(GameplaySessionLifecycle).Assembly.GetTypes()
                .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
                .Select(a => a.GUID).First();
            if (!ApiShared.Current.TryGetEditorMapLifecycle(owner, out var capability, out var diagnostic))
                throw new InvalidOperationException("Editor lifecycle unavailable: " + diagnostic?.Reason);
            // Mark before registration: a synchronous replay can initialize another feature.
            editorConnected = true;
            if (!capability.TryRegisterObserver("Shared.GameplaySessionLifecycle", OnEditorNotification, out diagnostic))
            {
                editorConnected = false;
                throw new InvalidOperationException("Editor lifecycle registration failed: " + diagnostic?.Reason);
            }
        }

        private static void OnEditorNotification(EditorMapLifecycleNotification e)
        {
            if (e.Kind == EditorMapLifecycleKind.Ended) { EndEditor(); return; }
            StartEditor(GameplaySessionStartedContext.FromEditor(e.SessionId,
                e.Origin == EditorMapOrigin.Loaded, e.FilePath, e.IsReplay));
        }
#endif

        internal static void SetEditorGate(Action<GameplaySessionStartedContext> ready, Action ended)
        {
            editorGate = ready;
            editorGateEnded = ended;
            if (activeEditor != null) ready(activeEditor.AsEditorReplay());
        }

        private static void StartEditor(GameplaySessionStartedContext context)
        {
            activeEditor = context;
            // A gate failure prevents feature callbacks; never initialize behind a stale mode gate.
            try { editorGate?.Invoke(context); }
            catch
            {
                activeEditor = null;
                editorGateEnded?.Invoke();
                throw;
            }
            foreach (var entry in editorSubscribers.ToArray()) entry.Deliver(context);
        }

        private static void EndEditor()
        {
            activeEditor = null;
            editorGateEnded?.Invoke();
            foreach (var entry in editorSubscribers.ToArray()) entry.End();
        }

        private sealed class EditorSubscriber : IDisposable
        {
            private readonly ManualLogSource log;
            private readonly Action<GameplaySessionStartedContext> callback;
            private readonly Action ended;
            private long lastSession;
            internal EditorSubscriber(ManualLogSource log, Action<GameplaySessionStartedContext> callback, Action ended)
            { this.log = log; this.callback = callback; this.ended = ended; }
            internal void End()
            {
                try { ended?.Invoke(); }
                catch (Exception ex) { DebugLogHelper.LogError(log, $"Editor end subscriber failed: {ex}"); }
            }
            internal void Deliver(GameplaySessionStartedContext context)
            {
                if (lastSession == context.EditorSessionId) return;
                lastSession = context.EditorSessionId;
                Notify(log, callback, context);
            }
            public void Dispose() { editorSubscribers.Remove(this); }
        }
        [ThreadStatic]
        private static GameplaySessionStartedContext currentNotification;

#if SHARED_PRESET_TESTS
        private static readonly List<Action<GameplaySessionStartedContext>> TestSubscribers =
            new List<Action<GameplaySessionStartedContext>>();
#endif

        internal static IDisposable SubscribeStarted(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback,
            Action onEditorEnded = null)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var editor = new EditorSubscriber(log, callback, onEditorEnded);
            editorSubscribers.Add(editor);

#if SHARED_PRESET_TESTS
            TestSubscribers.Add(callback);
            var subscription = new TestSubscription(callback, editor);
            if (activeEditor != null) editor.Deliver(activeEditor.AsEditorReplay());
            ReplayCurrentNotification(log, callback);
            return subscription;
#else
            IDisposable mapStartSubscription = null;
            IDisposable saveLoadSubscription = null;
            try
            {
                EnsureEditorConnected();
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => NotifyNewMap(log, callback, args));
                saveLoadSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(args => IsSuccessfulSavePost(args) && !args.LoadingEditorMap)
                    .Subscribe(args => NotifyLoadedSave(log, callback, args));
                var subscription = new Subscription(mapStartSubscription, saveLoadSubscription, editor);
                if (activeEditor != null) editor.Deliver(activeEditor.AsEditorReplay());
                ReplayCurrentNotification(log, callback);
                return subscription;
            }
            catch
            {
                mapStartSubscription?.Dispose();
                saveLoadSubscription?.Dispose();
                editor.Dispose();
                throw;
            }
#endif
        }

        internal static bool IsSuccessfulSavePost(LoadSaveGameEventArgs args) =>
            args != null && args.Phase == EventHookPhase.Post && args.ReturnValue > 0;

        private static void NotifyNewMap(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback,
            MapStartEventArgs args)
        {
            try
            {
                Notify(log, callback, GameplaySessionStartedContext.FromNewMap(args));
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Shared gameplay-session context capture failed: kind={GameplaySessionStartKind.NewMap}, error={ex}");
            }
        }

        private static void NotifyLoadedSave(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback,
            LoadSaveGameEventArgs args)
        {
            try
            {
                Notify(log, callback, GameplaySessionStartedContext.FromLoadedSave(args));
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Shared gameplay-session context capture failed: kind={GameplaySessionStartKind.LoadedSave}, error={ex}");
            }
        }

        private static void ReplayCurrentNotification(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback)
        {
            GameplaySessionStartedContext context = currentNotification;
            if (context != null && !context.IsEditor)
                Notify(log, callback, context);
        }

        private static void Notify(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback,
            GameplaySessionStartedContext context)
        {
            GameplaySessionStartedContext previousNotification = currentNotification;
            currentNotification = context;
            try
            {
                callback(context);
            }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(
                    log,
                    $"Shared gameplay-session subscriber failed: kind={context.Kind}, error={ex}");
            }
            finally
            {
                currentNotification = previousNotification;
            }
        }

#if SHARED_PRESET_TESTS
        internal static void System_TestRaiseEditor(long id, bool loaded = false) =>
            StartEditor(GameplaySessionStartedContext.FromEditor(id, loaded, loaded ? "test.map" : null, false));
        internal static void System_TestEndEditor() => EndEditor();
        internal static void System_TestRaiseNewMap(MapStartEventArgs args)
        {
            foreach (Action<GameplaySessionStartedContext> callback in TestSubscribers.ToArray())
                NotifyNewMap(null, callback, args);
        }

        internal static void System_TestRaiseSave(LoadSaveGameEventArgs args)
        {
            if (!IsSuccessfulSavePost(args) || args.LoadingEditorMap)
                return;
            foreach (Action<GameplaySessionStartedContext> callback in TestSubscribers.ToArray())
                NotifyLoadedSave(null, callback, args);
        }

        internal static void System_TestReset()
        {
            TestSubscribers.Clear();
            currentNotification = null;
            editorSubscribers.Clear();
            activeEditor = null;
            editorGate = null;
            editorGateEnded = null;
        }

        private sealed class TestSubscription : IDisposable
        {
            private Action<GameplaySessionStartedContext> callback;
            private readonly IDisposable editor;

            internal TestSubscription(Action<GameplaySessionStartedContext> callback, IDisposable editor)
            { this.callback = callback; this.editor = editor; }

            public void Dispose()
            {
                Action<GameplaySessionStartedContext> removed = callback;
                callback = null;
                editor.Dispose();
                if (removed != null)
                    TestSubscribers.Remove(removed);
            }
        }
#else
        private sealed class Subscription : IDisposable
        {
            private IDisposable mapStartSubscription;
            private IDisposable saveLoadSubscription;
            private readonly IDisposable editor;

            internal Subscription(IDisposable mapStartSubscription, IDisposable saveLoadSubscription, IDisposable editor)
            {
                this.editor = editor;
                this.mapStartSubscription = mapStartSubscription;
                this.saveLoadSubscription = saveLoadSubscription;
            }

            public void Dispose()
            {
                IDisposable mapStart = mapStartSubscription;
                IDisposable saveLoad = saveLoadSubscription;
                mapStartSubscription = null;
                saveLoadSubscription = null;
                mapStart?.Dispose();
                saveLoad?.Dispose();
                editor.Dispose();
            }
        }
#endif
    }
}
