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
                LogInfo("OnStartMap");
                if (handledCurrentMap)
                    return;

                handledCurrentMap = true;
                CodeOnNewGame();
            }
            catch (Exception ex)
            {
                LogInfo("OnStartMap failed:", ex);
            }
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            LogInfo("OnLoadSave");
            CancelPendingStartTroopProcessing();
            handledCurrentMap = true;
            CodeOnLoadGame();
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            LogInfo("OnUnloadMap");
            CancelPendingStartTroopProcessing();
            goodsAddedByCode.Clear();
            handledCurrentMap = false;
        }

        private void CodeOnNewGame()
        {
            ApplyStartResources();
            AddStartTroops();
        }

        private void CodeOnLoadGame()
        {
        }
    }
}
