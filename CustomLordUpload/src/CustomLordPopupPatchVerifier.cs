using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Threading;

namespace CustomLordUpload
{
    internal sealed class CustomLordPopupPatchVerifier
    {
        private const string PopupPath = "Assets/GUI/XAMLResources/HUD_ConfirmationPopup.xaml";
        private const string Marker = "CustomLordUploadWarningScrollViewer";
        private readonly ManualLogSource log;
        private readonly object mismatchLock = new object();
        private int successfulVerificationLogged;
        private int lastLoggedMismatchCount = int.MinValue;

        internal CustomLordPopupPatchVerifier(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            // COMPATIBILITY: Recheck the public text-processing event and popup asset path after
            // Script Extender or game-XAML updates. The plugin instance cannot own this subscription.
            GameAssetManagerAPI.Instance.OnTextFileAssetProcess += VerifyPatchedPopup;
        }

        private void VerifyPatchedPopup(string relativePath, ref string text)
        {
            if (!string.Equals(
                    relativePath.Replace('\\', '/'),
                    PopupPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int count = CountOccurrences(text ?? string.Empty, Marker);
            if (count == 1)
            {
                if (Interlocked.Exchange(ref successfulVerificationLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "Custom Lord upload popup XAML patch matched exactly once.");
                }
            }
            else
            {
                lock (mismatchLock)
                {
                    if (lastLoggedMismatchCount == count)
                        return;
                    lastLoggedMismatchCount = count;
                }
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Custom Lord upload popup XAML patch verification found " + count +
                    " markers; expected exactly one. Review the XAML patch for this game/Extender version.");
            }
        }

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }
            return count;
        }
    }
}
