using System;
using System.Collections.Generic;

namespace ExtendedData
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

        internal static readonly HashSet<string> AllowedOverrideExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".tga", ".ogg", ".wav", ".webm", ".mp4"
            };

        internal static readonly HashSet<string> GenericOverrideAssetNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "about2siege", "add_player", "ally_need_help", "angry", "angry_castle_damaged",
                "angry_siege_lost", "attack", "avatar", "boast_of_kill", "cant_attack",
                "cant_help", "clip", "congrats_on_kill", "default", "defeat", "die_ally",
                "face", "happy", "icon", "intro", "introduction", "join", "kick_player",
                "kill_npc", "kill_player", "leave", "line", "message", "nerv_pre_siege",
                "nerv_weak", "neutral", "not_sending_goods", "portrait", "request_goods",
                "sad", "sent_goods", "sound", "speech", "taunt1", "taunt2", "taunt3",
                "taunt4", "team_losing", "team_winning", "thank_goods", "victory_good",
                "victory_harass", "video", "voice", "will_attack_enemy", "will_send_troops",
                "wont_attack", "wont_help"
            };

        internal static readonly string[] NumberedGenericOverrideAssetNames =
        {
            "attack", "clip", "intro", "introduction", "join", "leave", "line",
            "message", "sound", "speech", "video", "voice"
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
