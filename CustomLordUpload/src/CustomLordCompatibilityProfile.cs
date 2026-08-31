using System;
using System.Collections.Generic;

namespace CustomLordUpload
{
    internal static class CustomLordCompatibilityProfile
    {
        // COMPATIBILITY: These values are not exposed as public APIs. Recheck them against the
        // current Vanilla loader and Script Extender media loader after either dependency updates.
        internal const int AvatarWidth = 144;
        internal const int AvatarHeight = 144;
        internal const long AvatarMaximumExclusiveBytes = 80000;
        internal const short WavePcmFormat = 1;
        internal const int WaveSampleRate = 44100;
        internal const short WaveBitsPerSample = 16;

        internal static readonly HashSet<string> RootMediaExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".wav", ".ogg", ".webm", ".mp4", ".jpg", ".jpeg", ".tga"
            };

        internal static readonly HashSet<string> DevelopmentExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".7z", ".aup", ".aup3", ".bak", ".cs", ".csproj", ".dll", ".exe", ".pdb",
                ".psd", ".rar", ".sln", ".tmp", ".zip"
            };

        internal static readonly HashSet<string> DevelopmentDirectoryNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", ".svn", "_LegacyMediaSource", "bin", "node_modules", "obj"
            };

        internal static Dictionary<string, int> CreateFallbackMessageTypes()
        {
            // COMPATIBILITY: Used only when the public Script Extender enum cannot be reflected.
            string[] names =
            {
                "IncomingMessage", "WillAttack", "TauntSiege2", "TauntSiege3", "TauntSiege4",
                "AngerSiegeFailed", "AngerFortressDamaged", "PleadDeath", "PleadOutsideWalls",
                "NervousInsideWalls", "Counterattack", "Unk11", "Won", "Unk13", "RequestGoods",
                "ReceivedGoods", "DefeatedAgain", "AllyNotificationCongratulations",
                "AllyNotificationHasDefeatedEnemy", "AllyNotificationRequestReinforcements",
                "AllyNotificationMerryChristmas", "Unk21", "Unk22",
                "AllyNotificationWillSiegeEnemySoon", "AllyNotificationCannotAttackEnemy",
                "AllyNotificationWillNotAttackToday", "AllyNotificationCannotNotHelp",
                "AllyNotificationWillNotHelp", "AllyNotificationWillNotSendRequestedGoods",
                "AllyNotificationHasSentRequestedGoods", "AllyNotificationConfidentInVictory",
                "AllyNotificationConfidentInLosing", "AllyNotificationSentReinforcements",
                "AllyNotificationAgree"
            };

            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < names.Length; index++)
                result[names[index]] = index;
            return result;
        }

        internal static HashSet<string> CreateFallbackLordInfoFields()
        {
            // COMPATIBILITY: Used only when the public Script Extender LordInfo type cannot be reflected.
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "LocalizedDisplayName", "LocalizedTitles", "LocalizedDescription",
                "LocalizedDifficultyRating", "LocalizedFavouriteTroops", "LocalizedCastles",
                "LocalizedPlayStyle", "LocalizedFavouriteSaying", "FacePath", "JoinAudioPath",
                "LeaveAudioPath", "Messages", "IncomingMessage"
            };
        }
    }
}
