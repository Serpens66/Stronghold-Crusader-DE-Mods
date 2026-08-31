using BepInEx.Logging;
using SHCDESE.API;
using System;
using System.Threading;

namespace CustomCustomTrail
{
    internal sealed class TrailWorkshopPatchVerifier
    {
        private const string AssetPath = "Assets/GUI/XAMLResources/FRONT_EditorSetup.xaml";
        private const string Marker = "CustomCustomTrailUploadOptionsHost";
        private readonly ManualLogSource log;
        private int successLogged;
        private int lastMismatch = int.MinValue;

        internal TrailWorkshopPatchVerifier(ManualLogSource log)
        {
            this.log = log;
            GameAssetManagerAPI.Instance.OnTextFileAssetProcess += Verify;
        }

        private void Verify(string relativePath, ref string text)
        {
            if (!string.Equals(relativePath.Replace('\\', '/'), AssetPath, StringComparison.OrdinalIgnoreCase))
                return;
            int count = CountOccurrences(text ?? string.Empty, Marker);
            if (count == 1)
            {
                if (Interlocked.Exchange(ref successLogged, 1) == 0)
                    Shared.DebugLogHelper.LogInfo(log, "Custom Trail upload checkbox XAML patch matched exactly once.");
                return;
            }
            if (Interlocked.Exchange(ref lastMismatch, count) != count)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Custom Trail upload checkbox XAML patch verification found " + count +
                    " markers; expected exactly one.");
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
