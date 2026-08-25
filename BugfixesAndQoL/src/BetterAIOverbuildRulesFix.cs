// Feature: Improve AI broad obstruction cleanup while preventing reciprocal AI overbuild loops.
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
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class BetterAIOverbuildRulesFix : IDisposable
    {
        private const string MapperSelectionPattern =
            "83 EB 36 74 ?? 83 EB 19 74 ?? 83 EB 07 74 ?? 83 FB 01 74 ??";
        private const int MapperSelectionRva = 0x5CEAB;
        // sub/je/sub/je occupy exactly ten bytes and preserve the remaining Vanilla chain.
        private const int MapperSelectionHookSize = 10;

        private const string BroadBlockerLoadPattern =
            "49 69 C0 2C 03 00 00 0F B7 8C 38 2E 01 00 00";
        private const int BroadBlockerLoadRva = 0x5D016;
        // imul and movzx occupy exactly fifteen bytes; the next instruction starts at 0x5D025.
        private const int BroadBlockerLoadHookSize = 15;

        private const string NarrowBlockerLoadPattern =
            "49 69 C0 2C 03 00 00 48 0F BF 8C 38 2E 01 00 00";
        private const int NarrowBlockerLoadRva = 0x5D045;
        // imul and movsx occupy exactly sixteen bytes; the next instruction starts at 0x5D055.
        private const int NarrowBlockerLoadHookSize = 16;

        private const int PlacementPlayerIdStackOffset = 0x98;
        private const int PlacementOffsetXStackOffset = 0xA0;
        private const int PlacementOffsetYStackOffset = 0xA8;
        private const int PlacementMapperStackOffset = 0xB0;
        private const int PlacementOrientationStackOffset = 0xB8;
        private const int NativeProtectedSurrogateType = 40;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly BetterAIOverbuildConflictState conflictState =
            new BetterAIOverbuildConflictState();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> mapperSelectionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> broadBlockerProtectionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> narrowBlockerProtectionHook = new HookRef<X64InlineHook>();
        private bool callbackFailureLogged;
        private bool disposed;

        internal BetterAIOverbuildRulesFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "Better AI overbuild rules require the audited CrusaderDE.dll stack and building layouts.");

            Shared.NativeResolution mapperResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MapperSelectionPattern,
                MapperSelectionRva,
                referenceHashMatches,
                "AI overbuild mapper selection",
                log);
            Shared.NativeResolution broadBlockerResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                BroadBlockerLoadPattern,
                BroadBlockerLoadRva,
                referenceHashMatches,
                "AI broad-overbuild blocker protection",
                log);
            Shared.NativeResolution narrowBlockerResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                NarrowBlockerLoadPattern,
                NarrowBlockerLoadRva,
                referenceHashMatches,
                "AI narrow-overbuild blocker protection",
                log);

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref mapperSelectionHook,
                    libraryBase + unchecked((ulong)mapperResolution.Rva),
                    PromoteAddedMapper,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MapperSelectionHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref broadBlockerProtectionHook,
                    libraryBase + unchecked((ulong)broadBlockerResolution.Rva),
                    ProtectBroadForeignBlocker,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: BroadBlockerLoadHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.AddContextHook(
                    ref narrowBlockerProtectionHook,
                    libraryBase + unchecked((ulong)narrowBlockerResolution.Rva),
                    ProtectNarrowForeignBlocker,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: NarrowBlockerLoadHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();
                if (!mapperSelectionHook.Success || !broadBlockerProtectionHook.Success ||
                    !narrowBlockerProtectionHook.Success)
                {
                    throw new InvalidOperationException(
                        "The three AI overbuild hooks were not installed atomically.");
                }

                InitializeConflictTracking();

                ApplySetting();
            }
            catch
            {
                DisposeSubscriptions();
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
                throw;
            }
        }

        internal void ApplySetting()
        {
            if (disposed || !mapperSelectionHook.Success || !broadBlockerProtectionHook.Success ||
                !narrowBlockerProtectionHook.Success)
                return;

            bool enabled = IsEnabled;
            if (enabled)
            {
                if (!mapperSelectionHook.Value.IsActive)
                    mapperSelectionHook.Value.Enable();
                if (!broadBlockerProtectionHook.Value.IsActive)
                    broadBlockerProtectionHook.Value.Enable();
                if (!narrowBlockerProtectionHook.Value.IsActive)
                    narrowBlockerProtectionHook.Value.Enable();
            }
            else
            {
                ResetConflictState();
                if (mapperSelectionHook.Value.IsActive)
                    mapperSelectionHook.Value.Disable();
                if (broadBlockerProtectionHook.Value.IsActive)
                    broadBlockerProtectionHook.Value.Disable();
                if (narrowBlockerProtectionHook.Value.IsActive)
                    narrowBlockerProtectionHook.Value.Disable();
            }

        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            DisposeSubscriptions();
            conflictState.Reset();
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private void PromoteAddedMapper(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                eMappers mapper = (eMappers)unchecked((short)registers->RBX);
                if (!BetterAIOverbuildPolicy.IsAddedAlwaysBroadMapper(mapper))
                    return;

                // Mapper 54 is Vanilla's first unconditional branch. Only EBX is temporary here;
                // the original mapper remains on the stack for footprint creation and spawning.
                registers->RBX = unchecked((ushort)eMappers.MAPPER_HOVEL);
            }
            catch (Exception ex)
            {
                LogCallbackFailure("mapper promotion", ex);
            }
        }

        private void ProtectBroadForeignBlocker(NativePointer<X64SmartCPUContext> context) =>
            ProtectReciprocalForeignBlocker(context);

        private void ProtectNarrowForeignBlocker(NativePointer<X64SmartCPUContext> context) =>
            ProtectReciprocalForeignBlocker(context);

        private void ProtectReciprocalForeignBlocker(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int blockerId = unchecked((int)(uint)registers->R8);
                eStructs blockerStructureType = (eStructs)unchecked((ushort)registers->RCX);
                ReadPlacementContext(
                    registers,
                    out int placingPlayerId,
                    out int mapper,
                    out int currentBaseX,
                    out int currentBaseY,
                    out int originalOrientation,
                    out int pass);
                if (blockerId <= 0 || placingPlayerId < 1 || placingPlayerId > 8 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        blockerId, out GameBuilding* blocker) ||
                    (blocker->r_AliveState != AliveState.NeedsInit &&
                     blocker->r_AliveState != AliveState.IsAlive) ||
                    blocker->r_BuildingType != blockerStructureType)
                {
                    return;
                }

                int blockerOwnerId = blocker->r_PlayerIdOwner;
                bool blockerOwnerIsAi = blockerOwnerId >= 1 && blockerOwnerId <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(blockerOwnerId);
                bool isForeignAiBlocker = blockerOwnerIsAi &&
                    blockerOwnerId != placingPlayerId;
                int keepId = blockerOwnerIsAi
                    ? GamePlayerManagerAPI.Instance.GetPlayerKeepId(blockerOwnerId)
                    : -1;
                bool blockerHasKeep = keepId > 0;
                UnmanagedVector2<int> keepPosition = blockerHasKeep
                    ? GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(blockerOwnerId)
                    : default;
                bool hasProtectedReservationParent = isForeignAiBlocker &&
                    BetterAIOverbuildPolicy.IsReservedAreaStructure(blockerStructureType) &&
                    !BetterAIOverbuildPolicy.IsAlwaysProtectedReservedArea(blockerStructureType) &&
                    HasProtectedReservationParent(
                        placingPlayerId,
                        blocker,
                        blockerStructureType,
                        blockerHasKeep,
                        keepPosition.X,
                        keepPosition.Y);
                BetterAIOverbuildProtectionReason reason =
                    BetterAIOverbuildPolicy.ClassifyForeignBlocker(
                        placingPlayerId,
                        blockerOwnerId,
                        blockerOwnerIsAi,
                        blockerStructureType,
                        hasProtectedReservationParent,
                        blockerHasKeep,
                        blocker->r_TilePositionXBegin,
                        blocker->r_TilePositionYBegin,
                        keepPosition.X,
                        keepPosition.Y);

                bool hasTick = TryCaptureCurrentTick(out int tick);
                if (reason == BetterAIOverbuildProtectionReason.None &&
                    isForeignAiBlocker && hasTick)
                {
                    BetterAIOverbuildPlacementKey placementKey =
                        BetterAIOverbuildPlacementKey.FromNativePass(
                            placingPlayerId,
                            mapper,
                            currentBaseX,
                            currentBaseY,
                            originalOrientation,
                            pass);
                    var blockerKey = new BetterAIOverbuildBlockerKey(
                        blockerOwnerId,
                        (int)blockerStructureType,
                        blocker->r_TilePositionXBegin,
                        blocker->r_TilePositionYBegin);
                    var conflictKey = new BetterAIOverbuildConflictKey(placementKey, blockerKey);
                    bool protectRepeatedConflict = conflictState.ShouldProtect(
                        tick,
                        conflictKey,
                        blockerId,
                        blocker->r_GlobalId);
                    if (protectRepeatedConflict)
                    {
                        reason = BetterAIOverbuildProtectionReason.RepeatedConflict;
                    }
                    else
                    {
                        // Only the synchronous bulldoze event confirms that Vanilla acted on
                        // this classification. An ignored decision cannot seed a conflict.
                        conflictState.RegisterDelegatedDecision(
                            tick,
                            conflictKey,
                            blockerId,
                            blocker->r_GlobalId);
                    }
                }

                if (reason != BetterAIOverbuildProtectionReason.None)
                {
                    // Type 40 is rejected by both Vanilla classifier masks. This changes only
                    // the classifier register; the live building record is never modified.
                    registers->RCX = NativeProtectedSurrogateType;
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("foreign blocker protection", ex);
            }
        }

        private static bool HasProtectedReservationParent(
            int placingPlayerId,
            GameBuilding* reservedArea,
            eStructs reservedAreaType,
            bool ownerHasKeep,
            int keepX,
            int keepY)
        {
            bool found = false;
            uint bestGlobalId = 0;
            eStructs bestType = eStructs.STRUCT_NULL;
            int bestX = 0;
            int bestY = 0;
            long bestDistance = long.MaxValue;

            var buildingEnumerator = GameBuildingManagerAPI.Instance
                .QueryBuildings()
                .GetEnumerator();
            while (buildingEnumerator.MoveNext())
            {
                ref GameBuilding candidate = ref buildingEnumerator.Current;
                if ((candidate.r_AliveState != AliveState.NeedsInit &&
                     candidate.r_AliveState != AliveState.IsAlive) ||
                    candidate.r_PlayerIdOwner != reservedArea->r_PlayerIdOwner ||
                    !BetterAIOverbuildPolicy.IsReservationParentCandidate(
                        reservedAreaType,
                        candidate.r_BuildingType) ||
                    !BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                        reservedAreaType,
                        reservedArea->r_TilePositionXBegin,
                        reservedArea->r_TilePositionYBegin,
                        candidate.r_TilePositionXBegin,
                        candidate.r_TilePositionYBegin))
                {
                    continue;
                }

                long distance = BetterAIOverbuildPolicy.ManhattanDistance(
                    reservedArea->r_TilePositionXBegin,
                    reservedArea->r_TilePositionYBegin,
                    candidate.r_TilePositionXBegin,
                    candidate.r_TilePositionYBegin);
                if (found && (distance > bestDistance ||
                    (distance == bestDistance && candidate.r_GlobalId >= bestGlobalId)))
                {
                    continue;
                }

                found = true;
                bestGlobalId = candidate.r_GlobalId;
                bestType = candidate.r_BuildingType;
                bestX = candidate.r_TilePositionXBegin;
                bestY = candidate.r_TilePositionYBegin;
                bestDistance = distance;
            }

            if (!found)
                return false;

            BetterAIOverbuildProtectionReason protectionReason =
                BetterAIOverbuildPolicy.ClassifyForeignBlocker(
                    placingPlayerId,
                    reservedArea->r_PlayerIdOwner,
                    true,
                    bestType,
                    false,
                    ownerHasKeep,
                    bestX,
                    bestY,
                    keepX,
                    keepY);
            return protectionReason != BetterAIOverbuildProtectionReason.None;
        }

        private static void ReadPlacementContext(
            X64SmartCPUContext* registers,
            out int playerId,
            out int mapper,
            out int currentBaseX,
            out int currentBaseY,
            out int originalOrientation,
            out int pass)
        {
            playerId = *(int*)(registers->RSP + PlacementPlayerIdStackOffset);
            mapper = *(short*)(registers->RSP + PlacementMapperStackOffset);
            currentBaseX = *(int*)(registers->RSP + PlacementOffsetXStackOffset);
            currentBaseY = *(int*)(registers->RSP + PlacementOffsetYStackOffset);
            originalOrientation = *(int*)(registers->RSP + PlacementOrientationStackOffset);
            pass = unchecked((int)(uint)registers->R12);
        }

        private void InitializeConflictTracking()
        {
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingBulldoze));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingDelete));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => ResetConflictState()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => ResetConflictState()));
        }

        private void OnBuildingBulldoze(BuildingBulldozeEventArgs args)
        {
            if (!TryReadBuildingIdentity(args.BuildingId, out uint globalId))
                return;
            try
            {
                if (TryCaptureCurrentTick(out int tick))
                    conflictState.ObserveBulldoze(tick, args.BuildingId, globalId);
                else
                    conflictState.ObserveRemoval(args.BuildingId, globalId);
            }
            catch (Exception ex)
            {
                LogCallbackFailure("conflict bulldoze tracking", ex);
            }
        }

        private void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (!TryReadBuildingIdentity(args.BuildingId, out uint globalId))
                return;
            try
            {
                conflictState.ObserveRemoval(args.BuildingId, globalId);
            }
            catch (Exception ex)
            {
                LogCallbackFailure("conflict delete tracking", ex);
            }
        }

        private static bool TryReadBuildingIdentity(int buildingId, out uint globalId)
        {
            globalId = 0;
            if (buildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building))
            {
                return false;
            }

            globalId = building->r_GlobalId;
            return true;
        }

        private void ResetConflictState() => conflictState.Reset();

        private void DisposeSubscriptions()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
        }

        private static bool TryCaptureCurrentTick(out int tick)
        {
            try
            {
                tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
                return true;
            }
            catch
            {
                tick = int.MinValue;
                return false;
            }
        }

        private bool IsEnabled =>
            settings.EnableMod && settings.EnableAiFixes && settings.BetterAIOverbuildRules;

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;
            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Better AI overbuild rules failed during {operation}; the affected call remains Vanilla: {ex}");
        }

    }
}
