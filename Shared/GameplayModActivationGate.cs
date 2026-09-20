using BepInEx.Logging;
using System;
#if !SHARED_PRESET_TESTS
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
        private static GameModeSnapshot snapshot;
        private static volatile bool isAllowed;
        private static bool initialized;
        private static bool routineLoggingEnabled = true;
        internal static event Action<bool> StateChanged;

        internal static bool IsAllowed => isAllowed;
        internal static GameModeSnapshot Snapshot => snapshot;
        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;

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
            profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
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
            snapshot = next;
            isAllowed = GameplayModModePolicy.IsAllowed(profile, next, out _);
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
            isAllowed = false;
            snapshot = default;
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
            if (policyChanged)
                DebugLogHelper.LogInfo(log, message);
            else
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

#if SHARED_PRESET_TESTS
        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");
        internal static void ResetForTests()
        {
            profile = GameplayModModePolicy.GetProfile("ExtraFeatures_Serp", "Extra Features");
            configuredEnabledProvider = () => true;
            Reset("test-reset");
        }
#endif
    }
}
