using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace LordMessageReplacementTest
{
    // This policy deliberately recognises only the audited generic messenger sources.
    // An authored SE resource with the same speaker/category is not a placeholder.
    internal static class ReplacementPolicy
    {
        internal static bool IsTargetLord(AILords lord) => lord == AILords.SK_RAT ||
            lord == AILords.SK_SNAKE || lord == AILords.SK_PIG || lord == AILords.SK_WOLF;

        internal static bool IsAlliedCategory(AILordMessageType type) =>
            (type >= AILordMessageType.RequestGoods && type <= AILordMessageType.MerryChristmas) ||
            (type >= AILordMessageType.About2Siege && type <= AILordMessageType.WillAttackEnemy);

        internal static bool ShouldReplace(AILords lord, AILordMessageType type,
            string vanillaAudio, string vanillaVideo, string actualAudio, string actualVideo)
        {
            if (!IsTargetLord(lord) || !IsAlliedCategory(type) ||
                !SamePath(vanillaAudio, actualAudio) || !SamePath(vanillaVideo, actualVideo)) return false;
            string audio = FileName(vanillaAudio);
            string video = FileName(vanillaVideo);
            return audio.StartsWith("all_", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(video, "bad_soldier_taunt.webm", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(video, "bad_soldier_nevous.webm", StringComparison.OrdinalIgnoreCase));
        }

        private static bool SamePath(string expected, string actual) => !string.IsNullOrEmpty(expected) &&
            !string.IsNullOrEmpty(actual) && string.Equals(expected.Replace('\\', '/'),
                actual.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);

        private static string FileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            return path.Substring(separator + 1);
        }
    }
}
