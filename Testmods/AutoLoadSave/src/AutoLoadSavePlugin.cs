using System;
using System.Globalization;
using System.IO;
using System.Threading;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using UnityEngine;

namespace AutoLoadSave
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.12.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class AutoLoadSavePlugin : BaseUnityPlugin
    {
        public const string Guid = "AutoLoadSave_Serp", Name = "AutoLoadSave", Version = "0.1.0";
        private static ManualLogSource log;
        private static LoadRequest request;
        private static IMissionLifecycleCapability missions;
        private static bool registered, loading;
        private static volatile bool confirmed;
        private static int tickReported;
        private static int lastFrame = -1;
        private static string resolvedPath;

        private void Awake()
        {
            if (registered) return;
            registered = true;
            log = Logger;
            request = new LoadRequest(Config.Bind("General", "SavePath", "",
                "Single-player .sav file: absolute path or relative to the game's Saves directory. " +
                "Empty disables autoload. Read once at startup; one attempt per process, including failures.").Value);
            LogInfo("Initialized; SavePath=" + (request.Consumed ? "<empty>; autoload disabled." : request.ConfiguredPath));
            if (request.Consumed) return;
            try
            {
                var api = ApiShared.ForMod(Guid);
                if (!api.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                    !missions.TryRegisterObserver("autoload-result", OnStarted, null, null, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic.Reason);
                // Static publisher survives SHCDE destroying the early plugin component.
                Application.onBeforeRender += BeforeRender;
                LogInfo("Waiting for the fully initialized main menu.");
            }
            catch (Exception ex) { Error("Autoload initialization failed; no save will be loaded. " + ex); }
        }

        private static void BeforeRender()
        {
            if (request.Consumed || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (!MainViewModel.viewModelLoaded || FatControler.currentScene != Enums.SceneIDS.FrontEnd) return;
                var view = MainViewModel.Instance;
                bool ready = view.Show_Frontend_MainMenu && !view.Show_IntroSequence &&
                    view.HUDmain != null && view.HUDIngameMenu != null &&
                    Director.instance != null && EditorDirector.instance != null;
                if (!request.TryBegin(ready)) return;
                LogInfo("Main menu ready after startup cleanup; beginning the single load attempt.");
                resolvedPath = request.ResolvePath(ConfigSettings.GetSavesPath());
                // Folder type 0 is Vanilla's user-file catalog selector, not a game/player ID.
                var header = MapFileManager.Instance.GetFileInfoFromFileName(resolvedPath, resolvedPath, 0, true);
                if (header == null) throw new InvalidDataException("Vanilla rejected the savegame header: " + resolvedPath);
                LogInfo("Loading " + resolvedPath);
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                loading = true;
                try
                {
                    // Match FrontendMenus' LoadSinglePlayerGame confirmation, including its preparation.
                    Platform_Multiplayer.Instance.gameMembers = null;
                    view.InitNewScene(Enums.SceneIDS.MainGame);
                    Director.instance.SetPausedState(false);
                    EditorDirector.instance.stopGameSim();
                    EditorDirector.instance.loadSaveGame(resolvedPath, header.standAlone_filename, header);
                    view.InitObjectiveGoodsPanelDelayed();
                }
                finally { loading = false; }
                if (!confirmed) Error("Load returned without a completed save mission; no retry. " + resolvedPath);
            }
            catch (Exception ex)
            {
                // Even failures while checking readiness must not log/retry every frame.
                request.TryBegin(true);
                Error("Automatic loading failed; no retry. " + ex);
            }
        }

        private static void OnStarted(MissionLifecycleNotification notification)
        {
            if (!loading || notification.IsReplay || !notification.Context.IsSave ||
                !string.Equals(notification.Context.FilePath, resolvedPath, StringComparison.OrdinalIgnoreCase)) return;
            confirmed = true;
            LogInfo("Save loaded; managed initialization completed. Session=" + notification.Context.SessionId +
                "; path=" + resolvedPath + ". AutoLoadSave performs no further actions.");
        }

        private static void OnTick(int tick)
        {
            if (!confirmed || Interlocked.Exchange(ref tickReported, 1) != 0) return;
            LogInfo("Simulation tick observed after save initialization: " + tick);
        }

        private static string Stamp(string message) => DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + message;
        private static void LogInfo(string message) => log.LogInfo(Stamp(message));
        private static void Error(string message) => log.LogError(Stamp(message));
    }
}
