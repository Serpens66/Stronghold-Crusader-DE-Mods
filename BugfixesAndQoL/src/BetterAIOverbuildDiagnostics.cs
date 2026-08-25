// TEMPORARY INGAME DIAGNOSTICS - SAFE TO REMOVE.
// Removal: delete this file and BetterAIOverbuildDiagnosticState.cs, then remove their Compile
// entries and BETTER_AI_OVERBUILD_DIAGNOSTICS from BugfixesAndQoL.csproj. The marked #if
// call sites then compile away and may be deleted separately.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BugfixesAndQoL
{
    internal sealed unsafe class BetterAIOverbuildDiagnostics : IDisposable
    {
        private const string Prefix = "[TEMP BetterAIOverbuild]";
        private static BetterAIOverbuildDiagnostics instance;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly BetterAIOverbuildDiagnosticState state =
            new BetterAIOverbuildDiagnosticState();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private bool callbackFailureLogged;

        private BetterAIOverbuildDiagnostics(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingBulldoze));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap("map-unload")));
            LogInfo("temporary diagnostics initialized.");
        }

        internal static void Initialize(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (instance == null && log != null && settings != null)
                instance = new BetterAIOverbuildDiagnostics(log, settings);
        }

        internal static void Shutdown()
        {
            BetterAIOverbuildDiagnostics current = instance;
            instance = null;
            current?.Dispose();
        }

        internal static void NativeHooksInstalled(
            int mapperRva,
            int blockerRva,
            bool referenceHashMatches,
            bool enabled)
        {
            TryInvoke(current => current.LogInfo(
                $"native hooks installed: mapperRva=0x{mapperRva:X}, blockerRva=0x{blockerRva:X}, " +
                $"referenceHashMatches={referenceHashMatches}, enabled={enabled}."));
        }

        internal static void SettingApplied(bool enabled)
        {
            TryInvoke(current => current.LogInfo(
                $"setting applied: enabled={enabled}, hostFeatures={current.settings.EnableMod}, " +
                $"aiFixes={current.settings.EnableAiFixes}, option={current.settings.BetterAIOverbuildRules}."));
        }

        internal static void MapperPromoted(
            int tick,
            int playerId,
            int mapper,
            int targetX,
            int targetY)
        {
            TryInvoke(current =>
            {
                if (!current.state.RecordPromotion(tick, playerId, mapper, targetX, targetY))
                    return;
                current.LogInfo(
                    $"mapper-promoted: tick={tick}, player={playerId}, mapper={mapper}, " +
                    $"target=({targetX},{targetY}), reason=added-always-broad.");
            });
        }

        internal static void ForeignBlockerDecision(
            int tick,
            int placingPlayerId,
            int mapper,
            int targetX,
            int targetY,
            int pass,
            int blockerId,
            uint blockerGlobalId,
            int blockerOwnerId,
            int blockerStructureType,
            int blockerX,
            int blockerY,
            bool blockerHasKeep,
            int keepX,
            int keepY,
            long distance,
            BetterAIOverbuildProtectionReason protectionReason)
        {
            TryInvoke(current =>
            {
                bool isProtected = protectionReason != BetterAIOverbuildProtectionReason.None;
                if (!current.state.RecordDecision(
                    tick,
                    placingPlayerId,
                    mapper,
                    targetX,
                    targetY,
                    pass,
                    blockerId,
                    blockerGlobalId,
                    isProtected,
                    out _))
                {
                    return;
                }

                string reason = protectionReason == BetterAIOverbuildProtectionReason.AlwaysBroad
                    ? "protected-always-broad"
                    : protectionReason == BetterAIOverbuildProtectionReason.KeepRadius
                        ? "protected-keep-radius"
                        : "delegated-to-vanilla";
                string keep = blockerHasKeep ? $"({keepX},{keepY})" : "none";
                string distanceText = distance >= 0 ? distance.ToString() : "n/a";
                current.LogInfo(
                    $"foreign-blocker-decision: tick={tick}, pass={pass}, placingPlayer={placingPlayerId}, " +
                    $"mapper={mapper}, target=({targetX},{targetY}), blockerOwner={blockerOwnerId}, " +
                    $"blockerType={blockerStructureType}, blockerId={blockerId}, " +
                    $"blockerGlobalId={blockerGlobalId}, blockerAnchor=({blockerX},{blockerY}), " +
                    $"blockerKeep={keep}, manhattanDistance={distanceText}, decision={reason}.");
            });
        }

        internal static int CurrentTick()
        {
            try
            {
                return GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            }
            catch
            {
                return int.MinValue;
            }
        }

        public void Dispose()
        {
            EndMap("diagnostics-dispose");
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
        }

        private void OnMapStart(MapStartEventArgs args)
        {
            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    state.Reset();
                    callbackFailureLogged = false;
                    return;
                }

                if (args.Phase != EventHookPhase.Post)
                    return;

                LogInfo(
                    $"map-start: enabled={IsEnabled()}, alwaysBroadMappers=" +
                    "52,54,77,79,80,81,86,87, alwaysBroadStructures=" +
                    "10,1,26,2,19,11,8,9, keepManhattanRadius=20.");
            }
            catch (Exception ex)
            {
                LogCallbackFailure("map start", ex);
            }
        }

        private void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            try
            {
                if (args.BuildingId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        args.BuildingId, out GameBuilding* building))
                {
                    return;
                }

                int tick = CurrentTick();
                if (!state.ConfirmRemoval(
                    tick, args.BuildingId, building->r_GlobalId, out PendingRemoval pending))
                {
                    return;
                }

                LogInfo(
                    $"delegated-blocker-removed: tick={tick}, placingPlayer={pending.PlacingPlayerId}, " +
                    $"mapper={pending.Mapper}, target=({pending.TargetX},{pending.TargetY}), " +
                    $"pass={pending.Pass}, blockerId={pending.BlockerId}, " +
                    $"blockerGlobalId={pending.BlockerGlobalId}.");
            }
            catch (Exception ex)
            {
                LogCallbackFailure("bulldoze correlation", ex);
            }
        }

        private void EndMap(string reason)
        {
            try
            {
                BetterAIOverbuildDiagnosticSummary summary = state.SnapshotAndReset();
                string promotions = summary.PromotionCounts.Count == 0
                    ? "none"
                    : string.Join(",", summary.PromotionCounts
                        .OrderBy(pair => pair.Key)
                        .Select(pair => $"{pair.Key}:{pair.Value}"));
                LogInfo(
                    $"map-summary: reason={reason}, enabled={IsEnabled()}, promotions={promotions}, " +
                    $"protected={summary.ProtectedCount}, delegated={summary.DelegatedCount}, " +
                    $"confirmedRemovals={summary.ConfirmedRemovalCount}, " +
                    $"uncorrelatedDelegations={summary.UncorrelatedDelegationCount}, " +
                    $"duplicatesSuppressed={summary.DuplicateCount}.");
            }
            catch (Exception ex)
            {
                LogCallbackFailure("map summary", ex);
            }
        }

        private bool IsEnabled() =>
            settings.EnableMod && settings.EnableAiFixes && settings.BetterAIOverbuildRules;

        private void LogInfo(string message) =>
            Shared.DebugLogHelper.LogInfo(log, $"{Prefix} {message}");

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"{Prefix} diagnostics failed during {operation}; gameplay remains unaffected: {ex}");
        }

        private static void TryInvoke(Action<BetterAIOverbuildDiagnostics> action)
        {
            BetterAIOverbuildDiagnostics current = instance;
            if (current == null || action == null)
                return;
            try
            {
                action(current);
            }
            catch (Exception ex)
            {
                current.LogCallbackFailure("static observation", ex);
            }
        }
    }
}
