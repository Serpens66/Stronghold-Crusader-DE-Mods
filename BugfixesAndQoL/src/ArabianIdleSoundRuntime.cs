using System;
using System.Threading;
using APIShared.Audio;
using BepInEx.Logging;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    // APIShared owns the permanent hooks; this process-rooted consumer owns only
    // its local setting and registration. Startup component destruction is irrelevant.
    internal static class ArabianIdleSoundRuntime
    {
        private const int SwordDrawEffectId = 180;
        // APIShared catalog key of the confirmed Arabian idle sword-draw call site.
        private const string SwordDrawSourceKey = "Unit75.Source04";
        private static readonly Action<SoundPreEventArgs> callback = Apply;
        private static SoundRegistration registration;
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource persistentLog;
        private static int configured, activePercent = -1, confirmed;

        internal static void Configure(ManualLogSource log, BugfixesAndQoLViewModel model)
        {
            if (Interlocked.Exchange(ref configured, 1) != 0) return;
            persistentLog = log;
            settings = model;
            settings.SettingChanged += OnSettingChanged;
            try
            {
                // Only the confirmed draw source: every other soldier sound stays in native code.
                if (!SoundSourceSelection.TryCreate(new[] { SwordDrawSourceKey }, null, out var sources, out string reason) ||
                    !SoundEvents.TryRegister(BugfixesAndQoLPlugin.PluginGuid, "ArabianIdleSoundVolume",
                        callback, null, sources, out registration, out reason))
                    throw new InvalidOperationException(reason);
                RefreshActivation();
                try
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogInfo(persistentLog,
                            "ARABIAN_IDLE_VOLUME_REGISTERED: percent=" + settings.ArabianIdleSoundVolumePercent +
                            "; enabled=" + registration.Enabled + "; context=" + SoundEvents.ContextDiagnostic);
                }
                catch { }
            }
            catch (Exception error)
            {
                Volatile.Write(ref activePercent, -1);
                registration?.SetEnabled(false);
                try { Shared.DebugLogHelper.LogError(persistentLog, "ARABIAN_IDLE_VOLUME_DISABLED: " + error); }
                catch { }
            }
        }

        private static void OnSettingChanged(string property)
        {
            if (property == nameof(settings.EnableClientFeatures) ||
                property == nameof(settings.ArabianIdleSoundVolumePercent))
                RefreshActivation();
        }

        private static void RefreshActivation()
        {
            int percent = Math.Max(0, Math.Min(100, settings.ArabianIdleSoundVolumePercent));
            bool enabled = registration != null && settings.EnableClientFeatures && percent < 100;
            // Publish one complete callback state before changing logical demand.
            Volatile.Write(ref activePercent, enabled ? percent : -1);
            registration?.SetEnabled(enabled);
        }

        internal static void Apply(SoundPreEventArgs args)
        {
            int percent = Volatile.Read(ref activePercent);
            if (percent < 0 || !Matches(args.Context) || args.SkipOriginalFunction) return;
            if (percent == 0) args.SkipOriginalFunction = true;
            else args.MultiplyVolume(percent / 100f);
            if (Volatile.Read(ref confirmed) == 0 && Interlocked.Exchange(ref confirmed, 1) == 0)
                try
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogInfo(persistentLog,
                            "ARABIAN_IDLE_VOLUME_APPLIED: permanent callback after startup; percent=" + percent +
                            "; source=" + args.Context.SourceKey + "; request changed, audibility not confirmed.");
                }
                catch { } // Diagnostics must never affect playback or callback rollback.
        }

        internal static bool Matches(SoundContext context) =>
            context.Situation == SoundSituation.ArabianIdleSwordDraw &&
            context.SituationConfirmed &&
            context.UnitType == (int)eChimps.CHIMP_TYPE_ARAB_SWORDSMAN &&
            context.OriginalSoundId == SwordDrawEffectId;
    }
}
