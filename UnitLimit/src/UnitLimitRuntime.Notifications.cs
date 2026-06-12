using SHCDESE.API;

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
            limitMessageTimerHandle = null;
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
            catch (System.Exception ex)
            {
                LogInfo("Could not cancel limit message timer:", ex.Message);
            }

            limitMessageTimerHandle = null;
        }
    }
}
