using APIShared.GameModes;
using BepInEx.Logging;
using System;
#if !API_SHARED_PRESET_TESTS
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
#endif

namespace Shared
{
    /// <summary>
    /// Caches the current map policy for one mod assembly. Shared sources are linked
    /// into every mod, so one mod can never accidentally change another mod's state.
    /// </summary>
    internal static class GameplayModActivationGate
    {
        private static ManualLogSource log;
        private static GameplayModActivationProfile profile;
        private static Func<bool> configuredEnabledProvider;
        private static GameplayModeGate gate;
        private static GameModeSnapshot snapshot => gate?.Snapshot ?? default;
        private static bool isAllowed => gate?.IsAllowed ?? false;
        private static bool initialized;
        private static bool routineLoggingEnabled = true;
        // Feature decisions only change with the snapshot. Each slot packs (generation << 1) | allowed;
        // generation 0 never matches, so an untouched slot is always recomputed.
        private static int featureGeneration = 1;
        private static readonly int[] featureDecisionCache = new int[FeatureCacheSize];
        private const int FeatureCacheSize = 32;
        internal static event Action<bool> StateChanged;

        internal static bool IsAllowed => isAllowed;
        internal static GameModeSnapshot Snapshot => snapshot;
        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;

        /// <summary>
        /// Same decision as GameplayFeatureModePolicy.IsAllowed(modGuid, featureId, Snapshot), cached until
        /// the next mode update. Invalid identities are never cached, so their health report is unchanged.
        /// </summary>
        internal static bool IsFeatureAllowed(string modGuid, GameplayFeatureId featureId)
        {
            int index = (int)featureId;
            int generation = System.Threading.Volatile.Read(ref featureGeneration);
            if (index >= 0 && index < FeatureCacheSize)
            {
                int cached = System.Threading.Volatile.Read(ref featureDecisionCache[index]);
                if ((cached >> 1) == generation)
                    return (cached & 1) != 0;
            }

            bool allowed = GameplayFeatureModePolicy.IsAllowed(modGuid, featureId, snapshot);
            if (index >= 0 && index < FeatureCacheSize && GameplayFeatureModePolicy.IsKnownIdentity(modGuid, featureId))
                System.Threading.Volatile.Write(ref featureDecisionCache[index], (generation << 1) | (allowed ? 1 : 0));
            return allowed;
        }

        // Called after every gate.Update so a reader that sees the new generation also sees the new snapshot.
        private static void InvalidateFeatureDecisions() =>
            System.Threading.Interlocked.Increment(ref featureGeneration);

        internal static void Initialize(
            ManualLogSource logger,
            string modGuid,
            string displayName,
            Func<bool> isConfiguredEnabled,
            bool logRoutineActivity = true)
        {
            if (initialized)
                return;

            log = logger;
            profile = SerpsModProfiles.GetProfile(modGuid, displayName);
            gate = new GameplayModeGate(profile);
            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
            routineLoggingEnabled = logRoutineActivity;

            MissionEvents.SetOwner(modGuid);
            MissionEvents.SetGate(e =>
            {
                if (e.Kind == APIShared.MissionLifecycleKind.End) Reset("MissionEnd");
                else Update(e.Context.Mode, $"Mission{e.Kind}({e.Phase}, session={e.Context.SessionId})");
            });
            initialized = true;
            LogTransition("initialization", policyChanged: false);
        }

        private static void Update(GameModeSnapshot next, string source)
        {
            bool previousAllowed = isAllowed;
            bool changed = next.Kind != snapshot.Kind ||
                next.LaunchVariant != snapshot.LaunchVariant ||
                next.CustomizedTrailId != snapshot.CustomizedTrailId ||
                next.CustomizedMissionId != snapshot.CustomizedMissionId ||
                next.IsRealMultiplayer != snapshot.IsRealMultiplayer ||
                next.HasConflictingCustomizedOrigin != snapshot.HasConflictingCustomizedOrigin;
            gate.Update(next);
            InvalidateFeatureDecisions();
            if (changed)
                LogTransition(source, previousAllowed != isAllowed);
            if (previousAllowed != isAllowed)
                NotifyStateChanged(isAllowed);
        }

        private static void Reset(string source)
        {
            bool changed = snapshot.Kind != GameModeKind.Unknown ||
                snapshot.LaunchVariant != GameModeLaunchVariant.Standard;
            bool previousAllowed = isAllowed;
            gate.Update(default(GameModeSnapshot));
            InvalidateFeatureDecisions();
            if (changed)
                LogTransition(source, previousAllowed != isAllowed);
            if (previousAllowed)
                NotifyStateChanged(false);
        }

        private static void NotifyStateChanged(bool allowed)
        {
            Delegate[] handlers = StateChanged?.GetInvocationList();
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers)
            {
                try { ((Action<bool>)handler)(allowed); }
                catch (Exception ex)
                {
                    DebugLogHelper.LogError(
                        log,
                        $"[{profile.DisplayName}] gameplay-mod gate listener failed closed: {ex}");
                }
            }
        }

        private static void LogTransition(string source, bool policyChanged)
        {
            bool configuredEnabled = ReadConfiguredEnabled();
            if (!routineLoggingEnabled)
                return;

            bool effectiveEnabled = configuredEnabled && IsAllowed;
            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
            string action = effectiveEnabled
                ? "enabled"
                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
            string message =
                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.";
            DebugLogHelper.LogDebug(log, message);
            GameplayFeatureModePolicy.LogDecisions(log, profile.ModGuid, snapshot, source);
        }

        private static bool ReadConfiguredEnabled()
        {
            try { return configuredEnabledProvider?.Invoke() == true; }
            catch (Exception ex)
            {
                DebugLogHelper.LogError(log, $"[{profile.DisplayName}] EnableMod provider failed closed: {ex}");
                return false;
            }
        }

#if API_SHARED_PRESET_TESTS
        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");
        internal static void ResetForTests()
        {
            profile = SerpsModProfiles.GetProfile("ExtraFeatures_Serp", "Extra Features");
            if (gate == null) gate = new GameplayModeGate(profile);
            configuredEnabledProvider = () => true;
            Reset("test-reset");
        }
#endif
    }
}
