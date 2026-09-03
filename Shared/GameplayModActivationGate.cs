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
        private static bool hasAuthoritativeLoadEvidence;
        private static GameModeSnapshot authoritativeLoadSnapshot;
#if !SHARED_PRESET_TESTS
        private static IDisposable mapLoadSubscription;
        private static IDisposable loadSaveSubscription;
        private static IDisposable mapStartSubscription;
        private static IDisposable mapUnloadSubscription;
#endif

        internal static event Action<bool> StateChanged;

        internal static bool IsAllowed => isAllowed;
        internal static GameModeSnapshot Snapshot => snapshot;
        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;

        internal static void Initialize(
            ManualLogSource logger,
            string modGuid,
            string displayName,
            Func<bool> isConfiguredEnabled)
        {
            if (initialized)
                return;

            log = logger;
            profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));

#if !SHARED_PRESET_TESTS
            // Register before the mod's own handlers. Castle spawning and similar
            // native work already begins in OnStartMap(Pre).
            mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable
                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadMap({args.Phase})"));
            loadSaveSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadSave({args.Phase})"));
            mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                .Subscribe(args => UpdateStart(GameModeHelper.Capture(args), $"OnStartMap({args.Phase})"));
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Subscribe(args =>
                {
                    if (args.Phase == EventHookPhase.Pre)
                        Reset("OnUnloadMap(Pre)");
                });
#endif
            initialized = true;
            GameModeSnapshot current = GameModeHelper.Capture();
            if (current.Kind == GameModeKind.MapEditor)
                UpdateLoad(current, "initial-current-editor");
            else
                LogTransition("initialization");
        }

        private static void UpdateLoad(GameModeSnapshot next, string source)
        {
            if (hasAuthoritativeLoadEvidence)
                next = MergeWithAuthoritativeLoad(next);
            if (HasAuthoritativeLoadEvidence(next))
            {
                authoritativeLoadSnapshot = next;
                hasAuthoritativeLoadEvidence = true;
            }
            Update(next, source);
        }

        private static bool HasAuthoritativeLoadEvidence(GameModeSnapshot candidate) =>
            candidate.Kind == GameModeKind.MapEditor ||
            candidate.CampaignMapId > 0 ||
            candidate.EventTrailType >= 0 ||
            (candidate.Kind == GameModeKind.CoopTrail && candidate.CoopTrailId > 0) ||
            (candidate.Kind == GameModeKind.CustomTrail &&
             candidate.SkirmishGameType ==
                 (int)global::Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL) ||
            (candidate.IsMissionContent && candidate.IsCustomized);

        private static void UpdateStart(GameModeSnapshot next, string source)
        {
            if (hasAuthoritativeLoadEvidence)
                next = MergeWithAuthoritativeLoad(next);
            Update(next, source);
        }

        private static GameModeSnapshot MergeWithAuthoritativeLoad(GameModeSnapshot next)
        {
            if (authoritativeLoadSnapshot.Kind == GameModeKind.MapEditor)
                return authoritativeLoadSnapshot;
            if ((next.Kind == GameModeKind.CustomGame || next.Kind == GameModeKind.Unknown) &&
                authoritativeLoadSnapshot.IsMissionContent)
            {
                return authoritativeLoadSnapshot;
            }
            if (next.Kind == authoritativeLoadSnapshot.Kind &&
                authoritativeLoadSnapshot.IsCustomized && !next.IsCustomized)
            {
                return authoritativeLoadSnapshot;
            }
            return next;
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
                LogTransition(source);
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
            hasAuthoritativeLoadEvidence = false;
            authoritativeLoadSnapshot = default;
            if (changed)
                LogTransition(source);
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

        private static void LogTransition(string source)
        {
            bool configuredEnabled = ReadConfiguredEnabled();
            bool effectiveEnabled = configuredEnabled && IsAllowed;
            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
            string action = effectiveEnabled
                ? "enabled"
                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
            DebugLogHelper.LogInfo(
                log,
                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.");
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
        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => UpdateLoad(next, "test-load");
        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => UpdateStart(next, "test-start");
        internal static void ResetForTests()
        {
            profile = GameplayModModePolicy.GetProfile("ExtraFeatures_Serp", "Extra Features");
            configuredEnabledProvider = () => true;
            Reset("test-reset");
        }
#endif
    }
}
