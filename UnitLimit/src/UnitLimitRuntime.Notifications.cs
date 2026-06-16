using CrusaderDE;
using SHCDESE.API;

namespace UnitLimit
{
    public sealed partial class UnitLimitRuntime
    {
        private void PlayRecruitsNeededSpeech()
        {
            try
            {
                string speechFileName = GetRandomMissingRecruitsSpeechFileName();
                LogDebug("UnitLimit missing recruits speech:", speechFileName);

                SFXManager.instance?.playSpeech(
                    1,
                    speechFileName,
                    1f);
            }
            catch (System.Exception ex)
            {
                LogDebug("Could not play UnitLimit missing recruits speech:", ex.Message);
            }
        }

        private static string GetRandomMissingRecruitsSpeechFileName()
        {
            lock (MissingRecruitsSpeechRandom)
            {
                return MissingRecruitsSpeechFileNames[MissingRecruitsSpeechRandom.Next(MissingRecruitsSpeechFileNames.Length)];
            }
        }

        private void DisplayLimitNotification(string message)
        {
            SiegeLimitNotification.Hide();
            LimitNotification.Show(message);
            StartLimitMessageTimer();
        }

        private void DisplaySiegeLimitNotification(string message)
        {
            LimitNotification.Hide();
            SiegeLimitNotification.Show(message);
            StartLimitMessageTimer();
        }

        private void StartLimitMessageTimer()
        {
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
            SiegeLimitNotification.Hide();
        }

        private void HideLimitMessage()
        {
            CancelLimitMessageTimer();
            LimitNotification.Hide();
            SiegeLimitNotification.Hide();
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
                LogDebug("Could not cancel limit message timer:", ex.Message);
            }

            limitMessageTimerHandle = null;
        }
    }
}
