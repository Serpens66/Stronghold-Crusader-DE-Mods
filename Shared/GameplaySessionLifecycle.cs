using BepInEx.Logging;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;

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
        internal static IDisposable SubscribeStarted(
            ManualLogSource log,
            Action<GameplaySessionStartedContext> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

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
    }
}
