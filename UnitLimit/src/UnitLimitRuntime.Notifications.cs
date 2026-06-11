using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private void DisplayLimitNotification(string message)
        {
            LimitNotification.Show(message);
            CancelLimitMessageTimer();
            limitMessageTimerHandle = GameTimeManagerAPI.Instance.GetTimerEngine().AddDelayedAction(
                LimitMessageDurationMilliseconds,
                OnLimitMessageTimerElapsed,
                null);
        }

        private void OnLimitMessageTimerElapsed()
        {
            LimitNotification.Hide();
        }

        private void HideLimitMessage()
        {
            CancelLimitMessageTimer();
            LimitNotification.Hide();
        }

        private void CancelLimitMessageTimer()
        {
            if (string.IsNullOrEmpty(limitMessageTimerHandle))
                return;

            try
            {
                GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAction(limitMessageTimerHandle);
            }
            catch (Exception ex)
            {
                LogInfo("Could not cancel limit message timer:", ex.Message);
            }

            limitMessageTimerHandle = null;
        }
    }
}
