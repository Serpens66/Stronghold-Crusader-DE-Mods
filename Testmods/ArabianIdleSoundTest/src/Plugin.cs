using BepInEx;
using APIShared.ModSettings;
using BepInEx.Logging;
using APIShared.Audio;
using System;
using System.Threading;

namespace ArabianIdleSoundTest
{
    internal enum PlaybackProbe { Volume25, ReplaceVanilla, Suppress, PanLeft, CustomClip }

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.8.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ArabianIdleSoundTest_Serp", Name = "Arabian Idle Sound Test", Version = "0.1.0";
        private static SoundTestSettings settings;
        private static int activeProbe, activeUnitType = -1, activeCooldown, cooldownLogged;
        private static SoundRepeatLimiter limiter = new SoundRepeatLimiter(0);
        private static readonly APIShared.ModApiClient Api = APIShared.ApiShared.ForMod(Guid);
        private static int activeSituation = (int)SoundSituation.ArabianIdleSwordDraw;
        private static SoundClipHandle testClip;
        private static readonly System.Collections.Generic.HashSet<string> observedSources = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        private static ManualLogSource log;
        private static SoundRegistration registration;
        private static int confirmed;
        private void Awake()
        {
            log = Logger;
            settings = new SoundTestSettings();
            settings.SettingChanged += PublishSettings;
            try
            {
                LobbyModSettingsPresetRegistration.Register(this, log, Name, settings,
                    "ScriptExtenderUI/ArabianIdleSoundTestSettings.xaml");
            }
            catch (Exception ex)
            { Shared.DebugLogHelper.LogError(log, "Ingame sound settings registration failed: " + ex); return; }
            if (!SoundEvents.TryRegister(Guid, "IdleSwordDraw", Apply, Completed, out var created, out var reason))
            { Shared.DebugLogHelper.LogError(log, "Sound registration failed: " + reason); return; }
            Volatile.Write(ref registration, created);
            if (!SoundClips.TryLoadModClip(Guid, "Triad", "Sounds/APISharedTriad.wav", value => {
                Volatile.Write(ref testClip, value);
                Shared.DebugLogHelper.LogInfo(log, "AUDIO_TEST_CLIP: " + value.Diagnostic + "; identity=" + value.OwnerGuid + "/" + value.RegistrationId + "; expected=three rising tones (440/660/880 Hz).");
            }, out var loaded, out var clipReason)) Shared.DebugLogHelper.LogError(log, "Test clip registration failed: " + clipReason);
            else Volatile.Write(ref testClip, loaded);
            if (!Api.TryGetMissionLifecycle(out var missions, out var lifecycleReason) ||
                !missions.TryRegisterObserver("SoundRepeatWindow", _ => ResetRepeatWindow(), _ => ResetRepeatWindow(), _ => ResetRepeatWindow(), out lifecycleReason))
                Shared.DebugLogHelper.LogWarning(log, "Audio test session-reset registration unavailable: " + lifecycleReason.Reason);
            PublishSettings();
            Shared.DebugLogHelper.LogInfo(log, "APIShared sound rule registered; probe=" + (PlaybackProbe)Volatile.Read(ref activeProbe) + "; context=" + SoundEvents.ContextDiagnostic);
        }
        private static void PublishSettings() {
            Volatile.Write(ref activeSituation, (int)SoundTestSettings.Situations[settings.SituationIndex]);
            Volatile.Write(ref activeUnitType, SoundTestSettings.UnitTypes[settings.UnitTypeIndex]);
            Volatile.Write(ref activeCooldown, settings.CooldownMilliseconds);
            Configure(settings.EnableMod, (PlaybackProbe)settings.ProbeIndex);
        }
        // Settings and preset applications publish only logical state; hooks stay installed.
        internal static void Configure(bool enabled, PlaybackProbe probe)
        {
            if (probe < PlaybackProbe.Volume25 || probe > PlaybackProbe.CustomClip) throw new ArgumentOutOfRangeException(nameof(probe));
            Volatile.Write(ref activeProbe, (int)probe);
            Volatile.Read(ref registration)?.SetEnabled(enabled);
            ResetRepeatWindow();
            Interlocked.Exchange(ref confirmed, 0);
            lock (observedSources) observedSources.Clear();
        }
        internal static void ResetRepeatWindow()
        {
            Volatile.Write(ref limiter, new SoundRepeatLimiter(Volatile.Read(ref activeCooldown)));
            Interlocked.Exchange(ref cooldownLogged, 0);
        }
        internal static bool Matches(SoundContext context)
        {
            int type = Volatile.Read(ref activeUnitType);
            return context.Situation != SoundSituation.Unknown && context.Situation == (SoundSituation)Volatile.Read(ref activeSituation) &&
                (type == -1 || context.UnitType == type);
        }
        internal static void Apply(SoundPreEventArgs args) => ApplyAt(args, Volatile.Read(ref activeCooldown) == 0 ? 0 : System.Diagnostics.Stopwatch.GetTimestamp());
        internal static void ApplyAt(SoundPreEventArgs args, long timestamp)
        {
            if (!Matches(args.Context)) return;
            var probe = (PlaybackProbe)Volatile.Read(ref activeProbe);
            args.State = probe;
            // Another contributor's veto never consumes our repeat window.
            if (args.SkipOriginalFunction) return;
            if (!Volatile.Read(ref limiter).TryAcquire(timestamp)) {
                args.SkipOriginalFunction = true;
                if (Interlocked.Exchange(ref cooldownLogged, 1) == 0)
                    try { Shared.DebugLogHelper.LogInfo(log, "AUDIO_TEST_COOLDOWN: suppressed repeated matching request; milliseconds=" + Volatile.Read(ref activeCooldown) + ",unitFilter=" + Volatile.Read(ref activeUnitType) + ",situation=" + args.Context.Situation + "; interval measures Pre admission, not audible output."); } catch { }
                return;
            }
            switch (probe)
            {
                case PlaybackProbe.Volume25: args.MultiplyVolume(0.25f); break;
                case PlaybackProbe.ReplaceVanilla: args.ReplaceSound(179); break;
                case PlaybackProbe.Suppress: args.SkipOriginalFunction = true; break;
                case PlaybackProbe.PanLeft: args.SetPan(-1f); break;
                case PlaybackProbe.CustomClip: args.TryReplaceClip(Volatile.Read(ref testClip)); break;
            }
        }
        private static void Completed(SoundPostEventArgs args)
        {
            if (!(args.State is PlaybackProbe probe)) return;
            string key = args.Context.SourceKey ?? "Unknown";
            lock (observedSources) if (!observedSources.Add(key)) return;
            Interlocked.Exchange(ref confirmed, 1);
            Shared.DebugLogHelper.LogInfo(log, "AUDIO_TEST_DISPATCH: probe=" + probe + ",source=" + key + ",situation=" + args.Context.Situation +
                ",evidence=" + args.Context.SituationEvidence + ",unitType=" + args.Context.UnitType + ",suppressed=" + args.WasSuppressed +
                ",target=" + args.Target + ",volumeBefore=" + args.Context.OriginalVolume + ",volumeAfter=" + args.Volume + ",pan=" + args.Pan + "; submission does not confirm audible output.");
        }
    }
}
