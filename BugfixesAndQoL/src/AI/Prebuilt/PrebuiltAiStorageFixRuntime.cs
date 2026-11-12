using APIShared;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    // The permanent lifecycle/tick publishers retain this runtime after the plugin startup cleanup.
    internal sealed unsafe class PrebuiltAiStorageFixRuntime
    {
        private const int PrebuiltReferenceRva = 0x95FF8;
        private const string PrebuiltReferencePattern =
            "8B 0D ?? ?? ?? ?? 8D 46 FF 0F AB C1 41 B8 64 00 00 00 " +
            "89 0D ?? ?? ?? ?? 8B D6 49 8B CD E8 ?? ?? ?? ??";
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IntPtr prebuiltPlayersBits;
        private List<IDisposable> subscriptions;
        private PlayerWork[] activeWork = Array.Empty<PlayerWork>();
        private long sessionId;
        private bool processing;

        internal PrebuiltAiStorageFixRuntime(ManualLogSource log,
            BugfixesAndQoLViewModel settings, CrusaderLibraryLoadContext context)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (context == null) throw new ArgumentNullException(nameof(context));
            ReadOnlySpan<byte> memory = context.Memory;
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, PrebuiltReferenceRva, PrebuiltReferencePattern))
                throw new InvalidOperationException("The prebuilt-player reference differs from the audited native build.");
            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(memory,
                PrebuiltReferenceRva + 2, PrebuiltReferenceRva + 6);
            if (targetRva < 0 || targetRva > memory.Length - sizeof(int))
                throw new InvalidOperationException("The prebuilt-player bitfield is outside the native image.");
            prebuiltPlayersBits = IntPtr.Add(context.ModuleHandle, targetRva);
            ValidateInteropLayout();
        }

        internal void Install()
        {
            if (subscriptions != null) return;
            var candidates = new List<IDisposable>();
            try
            {
                candidates.Add(Shared.MissionEvents.NativeStart.Subscribe(notification =>
                {
                    if (notification.IsBeforeInitialization) ResetMapState();
                }));
                candidates.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, OnSessionStarted));
                candidates.Add(Shared.MissionEvents.Ended.Subscribe(_ => ResetMapState()));
                candidates.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post).Subscribe(OnBuildingSpawn));
                if (!APIShared.Simulation.SimulationTickEvents.TryRegister(
                    BugfixesAndQoLPlugin.PluginGuid, "prebuilt-ai-storage", OnTick, out string reason, 100))
                    throw new InvalidOperationException("Native simulation ticks are unavailable: " + reason);
                subscriptions = candidates;
            }
            catch
            {
                foreach (IDisposable candidate in candidates) candidate.Dispose();
                throw;
            }
        }

        private void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            ResetMapState();
            if (session.Kind != Shared.GameplaySessionStartKind.NewMap || session.IsLoadedSave ||
                session.IsEditor || session.IsReplay || !settings.EnableMod || !settings.EnablePrebuiltAiStorageFix)
                return;
            try
            {
                int prebuiltBits = Marshal.ReadInt32(prebuiltPlayersBits);
                var plans = new List<PlayerWork>();
                for (int playerId = AivSystem.FIRST_LIVE_VILLAGE_SLOT;
                    playerId <= AivSystem.LAST_LIVE_VILLAGE_SLOT; playerId++)
                {
                    if ((prebuiltBits & (1 << (playerId - 1))) == 0 ||
                        !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                        continue;
                    try
                    {
                        PlayerWork plan = CapturePlan(playerId, session.SessionId);
                        if (plan != null) plans.Add(plan);
                    }
                    catch (Exception ex)
                    {
                        // One unsupported custom layout or unavailable per-player policy
                        // must not discard already validated queues for the other Lords.
                        Shared.DebugLogHelper.LogErrorLimited(log, "prebuilt-ai-storage-player-capture",
                            $"Prebuilt AI storage capture skipped player={playerId}; Vanilla remains active: {ex}");
                    }
                }
                sessionId = session.SessionId;
                activeWork = plans.ToArray();
                if (plans.Count != 0)
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Prebuilt AI storage queues armed: session={sessionId}, players={plans.Count}.");
            }
            catch (Exception ex)
            {
                ResetMapState();
                Shared.DebugLogHelper.LogErrorLimited(log, "prebuilt-ai-storage-start",
                    $"Prebuilt AI storage startup capture failed; Vanilla remains active: {ex}");
            }
        }

        private PlayerWork CapturePlan(int playerId, long capturedSession)
        {
            GameAIVManagerAPI aiv = GameAIVManagerAPI.Instance;
            if (!aiv.TryGetVillageSlotByPlayerId(playerId, out int slot) ||
                !aiv.TryGetVillageBySlot(slot, out AivVillageState* village))
                return null;
            int maximum = village->MaximumBuildStep;
            if (maximum < 0 || maximum >= AivVillageState.BUILD_STEP_CAPACITY)
                throw new InvalidOperationException($"Player {playerId} has an invalid prepared AIV frame bound {maximum}.");
            bool allowStockpile = ReadFixesGoodsyardPolicy(playerId);
            var placements = new List<Shared.StoragePlacement>();
            for (int frameIndex = 0; frameIndex <= maximum; frameIndex++)
            {
                if (!aiv.TryGetBuildStep(slot, frameIndex, out AivBuildStep* step) ||
                    step->State == AivBuildStepState.Inactive || !IsStorage(step->BuildingType) ||
                    (!allowStockpile && step->BuildingType == eMappers.MAPPER_STORES))
                    continue;
                if (step->TileCount != 1 || !GameTileManagerAPI.Instance.IsValidTileId(step->MapTileIdOrBufferIndex))
                    throw new InvalidOperationException($"Player {playerId} storage frame {frameIndex} has an invalid single-placement payload.");
                var tile = GameTileManagerAPI.Instance.GetTileVectorFromId(step->MapTileIdOrBufferIndex);
                placements.Add(new Shared.StoragePlacement(frameIndex, step->BuildingType,
                    tile.X, tile.Y, BuildingScales.GetScale(step->BuildingType)));
            }
            if (placements.Count == 0) return null;
            var backend = new AiStorageBackend(this, playerId, capturedSession, slot, village, placements);
            // Snapshot original identities before simulation can destroy/reuse a planned section.
            // Exclude every frame at a completed-but-missing section's origin, including
            // failed duplicates. The backend retains the full plan for completion bookkeeping.
            List<Shared.StoragePlacement> startupPlan = PrebuiltAiStorageCompletionPolicy.FilterStartupPlan(
                placements, backend.Inspect, frameIndex =>
                {
                    if (!aiv.TryGetBuildStep(slot, frameIndex, out AivBuildStep* step))
                        throw new InvalidOperationException($"Player {playerId} storage frame {frameIndex} disappeared during startup capture.");
                    return step->State;
                });
            return startupPlan.Count == 0 ? null : new PlayerWork(
                new Shared.DeferredStorageQueue(playerId, startupPlan), backend);
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            PlayerWork[] work = activeWork;
            for (int index = 0; index < work.Length; index++)
                if (!work[index].Finished) work[index].Backend.ObserveSpawn(args);
        }

        private void OnTick(int nativeTick)
        {
            PlayerWork[] work = activeWork;
            if (work.Length == 0 || processing) return;
            if (!settings.EnableMod || !settings.EnablePrebuiltAiStorageFix)
            {
                ResetMapState();
                return;
            }
            processing = true;
            try
            {
                bool remaining = false;
                for (int index = 0; index < work.Length; index++)
                {
                    PlayerWork plan = work[index];
                    if (plan.Finished) continue;
                    try
                    {
                        Shared.StorageQueueResult result = plan.Queue.Process(nativeTick, plan.Backend, out string reason);
                        if (result == Shared.StorageQueueResult.Waiting)
                        {
                            remaining = true;
                            continue;
                        }
                        plan.Finished = true;
                        if (result == Shared.StorageQueueResult.Completed)
                            Shared.DebugLogHelper.LogInfo(log,
                                $"Prebuilt AI storage queue completed: player={plan.Queue.PlayerId}, tick={nativeTick}, created={plan.Backend.CreatedCount}.");
                        else
                            Shared.DebugLogHelper.LogWarningLimited(log, "prebuilt-ai-storage-stop",
                                $"Prebuilt AI storage queue stopped: player={plan.Queue.PlayerId}, tick={nativeTick}, result={result}, reason={reason}.");
                    }
                    catch (Exception ex)
                    {
                        plan.Finished = true;
                        Shared.DebugLogHelper.LogErrorLimited(log, "prebuilt-ai-storage-tick",
                            $"Prebuilt AI storage queue aborted: player={plan.Queue.PlayerId}, tick={nativeTick}: {ex}");
                    }
                }
                if (!remaining && ReferenceEquals(activeWork, work)) activeWork = Array.Empty<PlayerWork>();
            }
            finally { processing = false; }
        }

        private void ResetMapState()
        {
            sessionId = 0;
            activeWork = Array.Empty<PlayerWork>();
        }

        private static bool IsStorage(eMappers mapper) => mapper == eMappers.MAPPER_STORES ||
            mapper == eMappers.MAPPER_GRANARY || mapper == eMappers.MAPPER_ARMOURY;

        private static bool ReadFixesGoodsyardPolicy(int playerId)
        {
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out PluginInfo plugin) ||
                plugin == null || ReferenceEquals(plugin.Instance, null)) return true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            Type type = plugin.Instance.GetType();
            object config = type.GetField("ModularGoodsyardPlacement", flags)?.GetValue(plugin.Instance);
            object enabled = config?.GetType().GetProperty("Value", flags)?.GetValue(config, null);
            if (!(enabled is bool isEnabled))
                throw new InvalidOperationException("The installed Fixes goodsyard option cannot be read safely.");
            if (!isEnabled) return true;
            object viewModel = type.GetProperty("LobbySettingsViewModel", flags)?.GetValue(plugin.Instance, null);
            bool[] values = viewModel?.GetType().GetProperty("PlaceGoodsyardData", flags)?.GetValue(viewModel, null) as bool[];
            if (values == null || playerId < 1 || playerId >= values.Length)
                throw new InvalidOperationException("The installed Fixes per-player goodsyard policy is unavailable.");
            return values[playerId];
        }

        private static void ValidateInteropLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                (int)Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.p_UnknownRelevant17)) != 0x2C8 ||
                Marshal.SizeOf(typeof(AivVillageState)) != 0x6D98 ||
                (int)Marshal.OffsetOf(typeof(AivVillageState), nameof(AivVillageState.MaximumBuildStep)) != 0x20 ||
                Marshal.SizeOf(typeof(AivBuildStep)) != 0x0C ||
                (int)Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.State)) != 0 ||
                (int)Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.RebuildDelay)) != 1 ||
                (int)Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.BuildingType)) != 2 ||
                (int)Marshal.OffsetOf(typeof(AivBuildStep), nameof(AivBuildStep.MapTileIdOrBufferIndex)) != 8)
                throw new InvalidOperationException("Installed storage/AIV interop differs from the audited native layout.");
        }

        private sealed class PlayerWork
        {
            internal readonly Shared.DeferredStorageQueue Queue;
            internal readonly AiStorageBackend Backend;
            internal bool Finished;
            internal PlayerWork(Shared.DeferredStorageQueue queue, AiStorageBackend backend)
            { Queue = queue; Backend = backend; }
        }

        private sealed class AiStorageBackend : Shared.StoragePlacementBackend
        {
            private readonly PrebuiltAiStorageFixRuntime runtime;
            private readonly int playerId, villageSlot, variant, maximum, originX, originY;
            private readonly AivRotation rotation;
            private readonly long capturedSession;
            private readonly List<Shared.StoragePlacement> placements;
            internal int CreatedCount { get; private set; }

            internal AiStorageBackend(PrebuiltAiStorageFixRuntime runtime, int playerId, long capturedSession,
                int villageSlot, AivVillageState* village, List<Shared.StoragePlacement> placements) : base(playerId)
            {
                this.runtime = runtime;
                this.playerId = playerId;
                this.capturedSession = capturedSession;
                this.villageSlot = villageSlot;
                this.placements = placements;
                variant = village->SelectedVariantIndex;
                maximum = village->MaximumBuildStep;
                originX = village->LayoutOriginX;
                originY = village->LayoutOriginY;
                rotation = village->Rotation;
            }

            public override bool IsCurrent(Shared.StoragePlacement placement, out string reason)
            {
                GameAIVManagerAPI aiv = GameAIVManagerAPI.Instance;
                if (runtime.sessionId != capturedSession ||
                    (Marshal.ReadInt32(runtime.prebuiltPlayersBits) & (1 << (playerId - 1))) == 0 ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId) ||
                    !aiv.TryGetVillageSlotByPlayerId(playerId, out int currentSlot) || currentSlot != villageSlot ||
                    !aiv.TryGetVillageBySlot(villageSlot, out AivVillageState* village) ||
                    village->SelectedVariantIndex != variant || village->MaximumBuildStep != maximum ||
                    village->LayoutOriginX != originX || village->LayoutOriginY != originY || village->Rotation != rotation ||
                    !aiv.TryGetBuildStep(villageSlot, placement.SourceIndex, out AivBuildStep* step) ||
                    !PrebuiltAiStorageCompletionPolicy.IsMatchingStep(in *step, placement.Mapper,
                        GameTileManagerAPI.Instance.GetTileId(placement.X, placement.Y)))
                {
                    reason = "startup-session-or-selected-AIV-changed";
                    return false;
                }
                reason = string.Empty;
                return true;
            }

            protected override void OnCreated(Shared.StoragePlacement placement, Shared.StorageCreatedSection section)
            {
                if (!IsCurrent(placement, out string reason) ||
                    !APIShared.BuildingAccess.TryGetById(section.LastBuildingId, out GameBuilding* last, out _) ||
                    last->r_GlobalId != section.LastGlobalId || last->r_PlayerIdOwner != playerId ||
                    last->r_BuildingType != placement.Mapper.ConvertToEStructs() ||
                    (last->r_AliveState != AliveState.NeedsInit && last->r_AliveState != AliveState.IsAlive))
                    throw new InvalidOperationException("The created storage section changed before AIV completion: " + reason);
                // 0x51790's successful ordinary-building path marks the constructor's final
                // record, then State=Built and RebuildDelay=10. For a yard this is subrecord four.
                // 0x31000 only adjusts workshop goods and has no effect on these three types.
                ValidateCompletionGroup(placement);
                PrebuiltAiStorageCompletionPolicy.MarkCreatedBuilding(ref *last);
                MarkCompletionGroup(placement);
                CreatedCount++;
            }

            public override void OnReady(Shared.StoragePlacement placement, bool created)
            {
                if (created) return;
                // Vanilla may already have created this section, including another AIV frame
                // at the same origin. Preserve its record flags, but retire every matching retry.
                ValidateCompletionGroup(placement);
                MarkCompletionGroup(placement);
            }

            private void ValidateCompletionGroup(Shared.StoragePlacement placement)
            {
                foreach (Shared.StoragePlacement source in placements)
                {
                    if (source.Mapper != placement.Mapper || source.X != placement.X || source.Y != placement.Y) continue;
                    if (!IsCurrent(source, out string reason))
                        throw new InvalidOperationException("Storage AIV frame changed before completion: " + reason);
                }
            }

            private void MarkCompletionGroup(Shared.StoragePlacement placement)
            {
                foreach (Shared.StoragePlacement source in placements)
                {
                    if (source.Mapper != placement.Mapper || source.X != placement.X || source.Y != placement.Y) continue;
                    GameAIVManagerAPI.Instance.TryGetBuildStep(villageSlot, source.SourceIndex, out AivBuildStep* step);
                    PrebuiltAiStorageCompletionPolicy.MarkBuilt(ref *step);
                }
            }
        }
    }
}
