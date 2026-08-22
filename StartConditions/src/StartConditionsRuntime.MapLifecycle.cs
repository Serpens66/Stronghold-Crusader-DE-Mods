using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        private void OnStartMap(MapStartEventArgs args)
        {
            try
            {
                LogDebug("OnStartMap");
                if (handledCurrentMap)
                    return;

                handledCurrentMap = true;
                CodeOnNewGame();
            }
            catch (Exception ex)
            {
                LogDebug("OnStartMap failed:", ex);
            }
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogDebug("OnLoadSave");
            CancelPendingKeepReadiness();
            CancelPendingStartTroopProcessing();
            handledCurrentMap = true;
            CodeOnLoadGame();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogDebug("OnUnloadMap");
            CancelPendingKeepReadiness();
            CancelPendingStartTroopProcessing();
            handledCurrentMap = false;
            activeSettings = settings;
            activePlayerIds = Array.Empty<int>();
        }

        private void CodeOnNewGame()
        {
            CancelPendingKeepReadiness();
            CancelPendingStartTroopProcessing();
            // Capture once so readiness and delayed troop processing use the same mission settings.
            activeSettings = StartConditionsIntegration.GetEffectiveSettings(settings);
            activePlayerIds = Array.Empty<int>();
            pendingKeepReadiness = Shared.ActivePlayerKeepReadiness.Wait(
                OnActivePlayerKeepsReady,
                KeepReadinessTimeout,
                message => LogError(message),
                "Start Conditions could not apply start resources or troops within 30 seconds because not every active player had a ready Keep.",
                OnKeepReadinessCompleted);
            LogDebug("Waiting up to 30 seconds for the synchronized active-player roster and all Keeps.");
        }

        private void OnActivePlayerKeepsReady(Shared.ActivePlayerKeepSnapshot snapshot)
        {
            activePlayerIds = (int[])snapshot.PlayerIds.Clone();
            LogInfo(
                "Start Conditions Keep readiness succeeded; applying start conditions for players",
                $"[{string.Join(",", activePlayerIds)}]",
                "keeps",
                $"[{string.Join(",", snapshot.KeepBuildingIds)}]");
            TryRunFeature("start resources", ApplyStartResources);
            TryRunFeature("start troops", AddStartTroops);
        }

        private void OnKeepReadinessCompleted(Shared.ActivePlayerKeepWaitResult result)
        {
            pendingKeepReadiness = null;
            if (result.Status == Shared.ActivePlayerKeepWaitStatus.CallbackFailed)
                LogError("Start Conditions stopped after its Keep-readiness callback failed.");
        }

        private void CancelPendingKeepReadiness()
        {
            Shared.ActivePlayerKeepWaitHandle pending = pendingKeepReadiness;
            pendingKeepReadiness = null;
            pending?.Dispose();
        }

        private void CodeOnLoadGame()
        {
        }
    }
}
