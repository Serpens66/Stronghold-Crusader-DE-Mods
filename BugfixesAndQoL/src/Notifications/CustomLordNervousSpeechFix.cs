using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BugfixesAndQoL
{
    // Vanilla maps two default custom-lord speech keys to files that do not exist
    // (all_plead_02/03.wav). playSpeech then finds no speech folder and stays silent while
    // the video message plays. The shipped recordings for these keys are all_nervous_01/02.wav
    // (same slots as xx_nervous_01/02 in the native per-lord tables of the built-in lords).
    // Only exact vanilla values are replaced, so foreign edits of the dictionary are kept.
    internal static class CustomLordNervousSpeechFix
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.CustomLordNervousSpeech");
        private static readonly string[] Keys = { "SK_NERV_PRE_SIEGE", "SK_NERV_WEAK" };
        private static readonly string[] VanillaPaths = { "fx\\speech\\all_plead_02.wav", "fx\\speech\\all_plead_03.wav" };
        private static readonly string[] FixedPaths = { "fx\\speech\\all_nervous_01.wav", "fx\\speech\\all_nervous_02.wav" };
        private static AccessTools.FieldRef<SFXManager, Dictionary<string, string>> speechField;
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static int attempted;
        private static int installed;
        private static int appliedLogged;

        internal static void Install(ManualLogSource logger, BugfixesAndQoLViewModel viewModel)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            log = logger;
            try
            {
                settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                if (AccessTools.DeclaredField(typeof(SFXManager), "customLordDefaultSpeech")?.FieldType != typeof(Dictionary<string, string>))
                    throw new MissingFieldException("SFXManager.customLordDefaultSpeech differs from the audit.");
                speechField = AccessTools.FieldRefAccess<SFXManager, Dictionary<string, string>>("customLordDefaultSpeech");
                var init = AccessTools.DeclaredMethod(typeof(SFXManager), nameof(SFXManager.init), Type.EmptyTypes);
                if (init == null || init.IsStatic || init.ReturnType != typeof(void))
                    throw new MissingMethodException("SFXManager.init signature differs from the audit.");
                // Covers a SFXManager created after this install; an existing one is updated below.
                harmony.Patch(init, postfix: new HarmonyMethod(typeof(CustomLordNervousSpeechFix), nameof(AfterInit)));
                Volatile.Write(ref installed, 1);
                settings.SettingChanged += OnSettingChanged;
                Refresh(SFXManager.instance);
                Shared.DebugLogHelper.LogInfo(log, "CUSTOM_LORD_NERVOUS_SPEECH_INSTALLED: enabled=" + IsEnabled());
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/CustomLordNervousSpeechFix/install",
                    "CUSTOM_LORD_NERVOUS_SPEECH_DISABLED: Vanilla behavior retained; " + exception);
            }
        }

        private static bool IsEnabled() => settings.EnableMod && settings.EnableClientFeatures;

        private static void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableMod) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures))
                Refresh(SFXManager.instance);
        }

        private static void AfterInit(SFXManager __instance) => Refresh(__instance);

        private static void Refresh(SFXManager manager)
        {
            if (manager == null || Volatile.Read(ref installed) == 0) return;
            try
            {
                Dictionary<string, string> speech = speechField(manager);
                if (speech == null) return;
                bool enabled = IsEnabled();
                int changed = 0;
                for (int i = 0; i < Keys.Length; i++)
                {
                    string from = enabled ? VanillaPaths[i] : FixedPaths[i];
                    string to = enabled ? FixedPaths[i] : VanillaPaths[i];
                    if (speech.TryGetValue(Keys[i], out string current) &&
                        string.Equals(current, from, StringComparison.OrdinalIgnoreCase))
                    {
                        speech[Keys[i]] = to;
                        changed++;
                    }
                }
                if (changed > 0 && Interlocked.Exchange(ref appliedLogged, 1) == 0)
                    Shared.DebugLogHelper.LogInfo(log, "CUSTOM_LORD_NERVOUS_SPEECH_APPLIED: enabled=" + enabled + ", entries=" + changed);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/CustomLordNervousSpeechFix/refresh",
                    "CUSTOM_LORD_NERVOUS_SPEECH_REFRESH_FAILED: " + exception);
            }
        }
    }
}
