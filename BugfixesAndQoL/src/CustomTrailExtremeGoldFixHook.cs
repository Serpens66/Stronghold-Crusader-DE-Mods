// Feature: Normalize the unusable Custom Trail extreme flag on read and save.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.IO;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CustomTrailExtremeGoldFixHook : IDisposable
    {
        private delegate FileHeader GetFileInfoDelegate(
            MapFileManager self,
            string filePath,
            string realFilePath,
            int folderType,
            bool loadRestartInfo);

        private delegate void SaveCustomTrailMapDelegate(
            EditorDirector self,
            string mapPath,
            string mapName,
            string trailPath,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook getFileInfoHook;
        private Hook saveCustomTrailMapHook;
        private GetFileInfoDelegate getFileInfoOriginal;
        private SaveCustomTrailMapDelegate saveCustomTrailMapOriginal;
        private bool disposed;

        public CustomTrailExtremeGoldFixHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            try
            {
                getFileInfoHook = new Hook(FindGetFileInfoMethod(), (GetFileInfoDelegate)GetFileInfoHook);
                getFileInfoOriginal = getFileInfoHook.GenerateTrampoline<GetFileInfoDelegate>();
                saveCustomTrailMapHook = new Hook(
                    FindSaveCustomTrailMapMethod(),
                    (SaveCustomTrailMapDelegate)SaveCustomTrailMapHook);
                saveCustomTrailMapOriginal = saveCustomTrailMapHook.GenerateTrampoline<SaveCustomTrailMapDelegate>();
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL Custom Trail starting-gold fix hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            saveCustomTrailMapHook?.Undo();
            saveCustomTrailMapHook?.Dispose();
            saveCustomTrailMapHook = null;
            getFileInfoHook?.Undo();
            getFileInfoHook?.Dispose();
            getFileInfoHook = null;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL Custom Trail starting-gold fix hooks disposed.");
        }

        private FileHeader GetFileInfoHook(
            MapFileManager self,
            string filePath,
            string realFilePath,
            int folderType,
            bool loadRestartInfo)
        {
            FileHeader result = getFileInfoOriginal(self, filePath, realFilePath, folderType, loadRestartInfo);
            if (!ShouldApply() || !loadRestartInfo || (!IsTrailPath(realFilePath) && !IsTrailPath(filePath)))
                return result;

            try
            {
                if (Normalize(result?.restartSkirmishInfo))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Ignored customisedExtremeTrail=true while reading Custom Trail [{realFilePath ?? filePath}].");
                }
            }
            catch (Exception ex)
            {
                // Reading the Vanilla result must remain successful even if this optional fix fails.
                Shared.DebugLogHelper.LogError(log, $"Custom Trail starting-gold read fix failed: {ex}");
            }

            return result;
        }

        private void SaveCustomTrailMapHook(
            EditorDirector self,
            string mapPath,
            string mapName,
            string trailPath,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            if (ShouldApply() && IsTrailPath(trailPath))
            {
                try
                {
                    if (Normalize(restartInfo))
                    {
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Writing Custom Trail [{trailPath}] with customisedExtremeTrail=false.");
                    }
                }
                catch (Exception ex)
                {
                    // A diagnostics failure must not prevent Vanilla from saving the mission.
                    Shared.DebugLogHelper.LogError(log, $"Custom Trail starting-gold save fix failed: {ex}");
                }
            }

            saveCustomTrailMapOriginal(self, mapPath, mapName, trailPath, restartInfo);
        }

        internal static bool Normalize(HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            if (restartInfo == null || !restartInfo.customisedExtremeTrail)
                return false;

            // The game cannot configure this value and ignores it during a normal Custom Trail launch.
            restartInfo.customisedExtremeTrail = false;
            return true;
        }

        private bool ShouldApply() =>
            settings.EnableClientFeatures && settings.EnableCustomTrailExtremeGoldFix;

        private static bool IsTrailPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return string.Equals(Path.GetExtension(path), ".trail", StringComparison.OrdinalIgnoreCase);
        }

        private static MethodInfo FindGetFileInfoMethod()
        {
            MethodInfo method = typeof(MapFileManager).GetMethod(
                nameof(MapFileManager.GetFileInfoFromFileName),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(bool) },
                null);

            return method ?? throw new MissingMethodException(
                typeof(MapFileManager).FullName,
                nameof(MapFileManager.GetFileInfoFromFileName));
        }

        private static MethodInfo FindSaveCustomTrailMapMethod()
        {
            MethodInfo method = typeof(EditorDirector).GetMethod(
                nameof(EditorDirector.SaveCustomTrailMap),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string),
                    typeof(HUD_IngameMenu.RestartSkirmishMapInfo),
                },
                null);

            return method ?? throw new MissingMethodException(
                typeof(EditorDirector).FullName,
                nameof(EditorDirector.SaveCustomTrailMap));
        }
    }
}
