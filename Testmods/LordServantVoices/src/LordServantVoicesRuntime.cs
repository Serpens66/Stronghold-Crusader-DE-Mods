using APIShared.Presentation;
using BepInEx.Logging;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LordServantVoices
{
    // Replaces only the generic Servant voice ("all_*.wav") that Vanilla plays for Rat, Snake,
    // Pig and Wolf with lord-specific Servant recordings, plus the matching message text.
    // The video stays untouched.
    internal static class LordServantVoicesRuntime
    {
        private const string RegistrationId = "lord-servant-voices";
        private const string ServantFilePrefix = "all_";
        private const string VoiceRoot = "Voices/";
        private const string English = "en-US";
        private const string German = "de-DE";

        private sealed class Target
        {
            internal readonly string VanillaFileName;
            internal readonly LordMessageAudio EnglishAudio;
            internal readonly LordMessageAudio GermanAudio;
            internal readonly LordMessageText EnglishText;
            internal readonly LordMessageText GermanText;
            internal Target(string vanillaFileName, LordMessageAudio englishAudio, LordMessageAudio germanAudio,
                LordMessageText englishText, LordMessageText germanText)
            {
                VanillaFileName = vanillaFileName; EnglishAudio = englishAudio; GermanAudio = germanAudio;
                EnglishText = englishText; GermanText = germanText;
            }
        }

        internal static ManualLogSource Log;
        // Written once on Unity before registration; Pre only reads it on Unity.
        private static Dictionary<(AILords, AILordMessageType), Target> targets;
        private static LordMessageRegistration registration;
        private static int initialized;
        private static int firstReplacementLogged;

        internal static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            try
            {
                if (!LordMessageEvents.Available)
                {
                    Shared.DebugLogHelper.LogError(Log, "LORD_SERVANT_VOICES_DISABLED: Lord message events unavailable: " + LordMessageEvents.Diagnostic);
                    return;
                }
                if (!VanillaLordMessages.Available)
                {
                    Shared.DebugLogHelper.LogError(Log, "LORD_SERVANT_VOICES_DISABLED: Vanilla Lord message catalog unavailable: " + VanillaLordMessages.Diagnostic);
                    return;
                }

                var prepared = new Dictionary<(AILords, AILordMessageType), Target>();
                foreach (VoiceEntry entry in LordServantVoicesCatalog.Entries)
                {
                    if (!VanillaLordMessages.TryGet(entry.Lord, entry.Type, out VanillaLordMessage vanilla, out string reason))
                    {
                        Shared.DebugLogHelper.LogError(Log, $"LORD_SERVANT_VOICES_ENTRY_SKIPPED: {entry.Lord}/{entry.Type}: {reason}");
                        continue;
                    }
                    string vanillaFile = FileName(vanilla.AudioPath);
                    // Only the shared Servant voice is replaced; anything else means the audited table changed.
                    if (!vanillaFile.StartsWith(ServantFilePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        Shared.DebugLogHelper.LogError(Log, $"LORD_SERVANT_VOICES_ENTRY_SKIPPED: {entry.Lord}/{entry.Type} plays '{vanilla.AudioPath}', not a Servant voice.");
                        continue;
                    }
                    LordMessageAudio english = Prepare(entry.File, English);
                    LordMessageAudio german = Prepare(entry.File, German);
                    if (english == null && german == null) continue;
                    prepared[(entry.Lord, entry.Type)] = new Target(vanillaFile, english, german,
                        LordMessageText.FromText(entry.EnglishText), LordMessageText.FromText(entry.GermanText));
                }

                targets = prepared;
                if (!LordMessageEvents.TryRegister(LordServantVoicesPlugin.PluginGuid, RegistrationId, BeforeMessage, null,
                        out registration, out string registerReason))
                {
                    Shared.DebugLogHelper.LogError(Log, "LORD_SERVANT_VOICES_DISABLED: registration failed: " + registerReason);
                    return;
                }
                Shared.DebugLogHelper.LogInfo(Log, $"LORD_SERVANT_VOICES_READY: entries={prepared.Count}/{LordServantVoicesCatalog.Entries.Length}");
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(Log, "LORD_SERVANT_VOICES_DISABLED: " + error);
            }
        }

        private static LordMessageAudio Prepare(string file, string language)
        {
            string path = VoiceRoot + language + "/" + file + ".ogg";
            LordMessageAudio audio = LordMessageAudio.FromModAsset(LordServantVoicesPlugin.PluginGuid, path);
            if (audio.IsReady) return audio;
            Shared.DebugLogHelper.LogError(Log, "LORD_SERVANT_VOICES_RESOURCE_MISSING: " + path);
            return null;
        }

        private static void BeforeMessage(LordMessagePreEventArgs args)
        {
            LordMessageContext context = args.Context;
            if (context.VanillaLord == null || args.Audio.Kind != LordMessageSourceKind.Original) return;
            if (!targets.TryGetValue((context.VanillaLord.Value, context.MessageType), out Target target)) return;
            // Keep Extender/Custom-Lord or other mods' audio: replace only the untouched Servant file.
            if (!string.Equals(FileName(context.OriginalAudioPath), target.VanillaFileName, StringComparison.OrdinalIgnoreCase)) return;
            LordMessageAudio audio = SpokenLanguage() switch
            {
                English => target.EnglishAudio,
                German => target.GermanAudio,
                _ => null,
            };
            if (audio == null || !args.TryReplaceAudio(audio)) return;

            // The Vanilla subtitle is the spoken Servant line, so it is swapped together with the voice.
            // Like Vanilla, the subtitle follows the game's text language; for other text languages
            // the translated Vanilla line stays. A text already replaced by another mod is kept.
            LordMessageText text = TextLanguage() switch
            {
                English => target.EnglishText,
                German => target.GermanText,
                _ => null,
            };
            if (text != null && args.Text.Kind == LordMessageSourceKind.Original) args.TryReplaceText(text);

            if (Interlocked.Exchange(ref firstReplacementLogged, 1) == 0)
                Shared.DebugLogHelper.LogInfo(Log, $"LORD_SERVANT_VOICES_FIRST_REPLACEMENT: {context.VanillaLord}/{context.MessageType} -> {audio.Path}, text={(text != null)}");
        }

        // Mirrors the game's speech folder choice (MyAudioManager): English speech, or the localized
        // speech of the selected language. Only English and German recordings exist; other spoken
        // languages keep the Vanilla Servant so the voice language stays consistent.
        private static string SpokenLanguage()
        {
            if (ConfigSettings.Settings_EnglishSpeech || FatControler.UsesEnglishSpeechFolder()) return English;
            return FatControler.german ? German : null;
        }

        // The game's text language flags (FatControler), independent of the English-speech option.
        private static string TextLanguage()
        {
            if (FatControler.english) return English;
            return FatControler.german ? German : null;
        }

        private static string FileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            int slash = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
    }
}
