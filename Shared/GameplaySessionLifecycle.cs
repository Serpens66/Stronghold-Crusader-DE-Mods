using BepInEx.Logging;
#if !SHARED_PRESET_TESTS
using R3;
#endif
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
#if SHARED_PRESET_TESTS
using System.Collections.Generic;
#endif

namespace Shared
{
    internal enum GameplaySessionStartKind
    {
        NewMap,
        LoadedSave,
    }

    internal sealed class GameplaySessionStartedContext
    {
        internal GameplaySessionStartKind Kind { get; }
        internal GameModeSnapshot Mode { get; }
        internal MapStartEventArgs MapStart { get; }
        internal LoadSaveGameEventArgs SaveLoad { get; }

        internal bool IsLoadedSave => Kind == GameplaySessionStartKind.LoadedSave;
        internal bool LoadingEditorMap => SaveLoad?.LoadingEditorMap == true;
        internal string SaveFileName => SaveLoad?.FileName;

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
    /// </summary>
    internal static class GameplaySessionLifecycle
    {
#if SHARED_PRESET_TESTS
        private static readonly List<Action<GameplaySessionStartedContext>> TestSubscribers =
            new List<Action<GameplaySessionStartedContext>>();
#endif

        internal static IDisposable SubscribeStarted(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

#if SHARED_PRESET_TESTS
            TestSubscribers.Add(callback);
            return new TestSubscription(callback);
#else
            IDisposable mapStartSubscription = null;
            IDisposable saveLoadSubscription = null;
            try
            {
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(args => Notify(log, callback, GameplaySessionStartedContext.FromNewMap(args)));
                saveLoadSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
                    .Where(IsSuccessfulSavePost)
                    .Subscribe(args => Notify(log, callback, GameplaySessionStartedContext.FromLoadedSave(args)));
                return new Subscription(mapStartSubscription, saveLoadSubscription);
            }
            catch
            {
                mapStartSubscription?.Dispose();
                saveLoadSubscription?.Dispose();
                throw;
            }
#endif
        }

        internal static bool IsSuccessfulSavePost(LoadSaveGameEventArgs args) =>
            args != null && args.Phase == EventHookPhase.Post && args.ReturnValue > 0;

        private static void Notify(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback,
            GameplaySessionStartedContext context)
        {
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
        }

#if SHARED_PRESET_TESTS
        internal static void System_TestRaiseNewMap(MapStartEventArgs args)
        {
            GameplaySessionStartedContext context = GameplaySessionStartedContext.FromNewMap(args);
            foreach (Action<GameplaySessionStartedContext> callback in TestSubscribers.ToArray())
                callback(context);
        }

        internal static void System_TestRaiseSave(LoadSaveGameEventArgs args)
        {
            if (!IsSuccessfulSavePost(args))
                return;
            GameplaySessionStartedContext context = GameplaySessionStartedContext.FromLoadedSave(args);
            foreach (Action<GameplaySessionStartedContext> callback in TestSubscribers.ToArray())
                callback(context);
        }

        internal static void System_TestReset() => TestSubscribers.Clear();

        private sealed class TestSubscription : IDisposable
        {
            private Action<GameplaySessionStartedContext> callback;

            internal TestSubscription(Action<GameplaySessionStartedContext> callback) =>
                this.callback = callback;

            public void Dispose()
            {
                Action<GameplaySessionStartedContext> removed = callback;
                callback = null;
                if (removed != null)
                    TestSubscribers.Remove(removed);
            }
        }
#else
        private sealed class Subscription : IDisposable
        {
            private IDisposable mapStartSubscription;
            private IDisposable saveLoadSubscription;

            internal Subscription(IDisposable mapStartSubscription, IDisposable saveLoadSubscription)
            {
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
            }
        }
#endif
    }
}
