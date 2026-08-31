using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Threading;

namespace CustomLordUpload
{
    internal sealed class CustomLordPopupPatchVerifier
    {
        private const string PopupPath = "Assets/GUI/XAMLResources/HUD_ConfirmationPopup.xaml";
        private const string UploadPagePath = "Assets/GUI/XAMLResources/FRONT_EditorSetup.xaml";
        private const string PopupMarker = "CustomLordUploadWarningScrollViewer";
        private const string UploadPageMarker = "CustomLordUploadOptionsHost";
        private readonly ManualLogSource log;
        private readonly object mismatchLock = new object();
        private int popupVerificationLogged;
        private int uploadPageVerificationLogged;
        private int lastPopupMismatchCount = int.MinValue;
        private int lastUploadPageMismatchCount = int.MinValue;

        internal CustomLordPopupPatchVerifier(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            // COMPATIBILITY: Recheck the public text-processing event and popup asset path after
            // Script Extender or game-XAML updates. The plugin instance cannot own this subscription.
            GameAssetManagerAPI.Instance.OnTextFileAssetProcess += VerifyPatchedPopup;
        }

        private void VerifyPatchedPopup(string relativePath, ref string text)
        {
            string normalizedPath = relativePath.Replace('\\', '/');
            string marker;
            string description;
            if (string.Equals(normalizedPath, PopupPath, StringComparison.OrdinalIgnoreCase))
            {
                marker = PopupMarker;
                description = "Custom Lord upload popup";
            }
            else if (string.Equals(normalizedPath, UploadPagePath, StringComparison.OrdinalIgnoreCase))
            {
                marker = UploadPageMarker;
                description = "Custom Lord upload checkbox";
            }
            else
                return;

            int count = CountOccurrences(text ?? string.Empty, marker);
            if (count == 1)
            {
                bool firstSuccess = string.Equals(marker, PopupMarker, StringComparison.Ordinal)
                    ? Interlocked.Exchange(ref popupVerificationLogged, 1) == 0
                    : Interlocked.Exchange(ref uploadPageVerificationLogged, 1) == 0;
                if (firstSuccess)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        description + " XAML patch matched exactly once.");
                }
            }
            else
            {
                lock (mismatchLock)
                {
                    bool popup = string.Equals(marker, PopupMarker, StringComparison.Ordinal);
                    int previous = popup ? lastPopupMismatchCount : lastUploadPageMismatchCount;
                    if (previous == count)
                        return;
                    if (popup)
                        lastPopupMismatchCount = count;
                    else
                        lastUploadPageMismatchCount = count;
                }
                Shared.DebugLogHelper.LogWarning(
                    log,
                    description + " XAML patch verification found " + count +
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
