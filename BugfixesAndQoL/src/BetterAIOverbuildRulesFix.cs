// Feature: Improve AI broad obstruction cleanup while preventing reciprocal AI overbuild loops.
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
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

        private const string BlockerLoadPattern =
            "49 69 C0 2C 03 00 00 0F B7 8C 38 2E 01 00 00";
        private const int BlockerLoadRva = 0x5D016;
        // imul and movzx occupy exactly fifteen bytes; the next instruction starts at 0x5D025.
        private const int BlockerLoadHookSize = 15;

        private const int PlacementPlayerIdStackOffset = 0x98;
        private const int PlacementOffsetXStackOffset = 0xA0;
        private const int PlacementOffsetYStackOffset = 0xA8;
        private const int PlacementMapperStackOffset = 0xB0;
        private const int PlacementOriginXOffset = 0x204E760;
        private const int PlacementOriginYOffset = 0x204E764;
        private const int NativeProtectedSurrogateType = 40;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> mapperSelectionHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> blockerProtectionHook = new HookRef<X64InlineHook>();
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
            Shared.NativeResolution blockerResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                BlockerLoadPattern,
                BlockerLoadRva,
                referenceHashMatches,
                "AI overbuild blocker protection",
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
                    ref blockerProtectionHook,
                    libraryBase + unchecked((ulong)blockerResolution.Rva),
                    ProtectReciprocalForeignBlocker,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: BlockerLoadHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();
                if (!mapperSelectionHook.Success || !blockerProtectionHook.Success)
                    throw new InvalidOperationException("The two AI overbuild hooks were not installed atomically.");

#if BETTER_AI_OVERBUILD_DIAGNOSTICS
                // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_BEGIN
                BetterAIOverbuildDiagnostics.NativeHooksInstalled(
                    mapperResolution.Rva,
                    blockerResolution.Rva,
                    referenceHashMatches,
                    IsEnabled);
                // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_END
#endif
                ApplySetting();
            }
            catch
            {
                transaction?.Unload();
                transaction?.Dispose();
                transaction = null;
                throw;
            }
        }

        internal void ApplySetting()
        {
            if (disposed || !mapperSelectionHook.Success || !blockerProtectionHook.Success)
                return;

            if (IsEnabled)
            {
                if (!mapperSelectionHook.Value.IsActive)
                    mapperSelectionHook.Value.Enable();
                if (!blockerProtectionHook.Value.IsActive)
                    blockerProtectionHook.Value.Enable();
            }
            else
            {
                if (mapperSelectionHook.Value.IsActive)
                    mapperSelectionHook.Value.Disable();
                if (blockerProtectionHook.Value.IsActive)
                    blockerProtectionHook.Value.Disable();
            }

#if BETTER_AI_OVERBUILD_DIAGNOSTICS
            // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_BEGIN
            BetterAIOverbuildDiagnostics.SettingApplied(IsEnabled);
            // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_END
#endif
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
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
                int mapper = unchecked((int)(uint)registers->RBX);
                if (!BetterAIOverbuildPolicy.IsAddedAlwaysBroadMapper(mapper))
                    return;

#if BETTER_AI_OVERBUILD_DIAGNOSTICS
                // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_BEGIN
                ReadPlacementContext(
                    registers,
                    out int playerId,
                    out _,
                    out int targetX,
                    out int targetY);
                BetterAIOverbuildDiagnostics.MapperPromoted(
                    BetterAIOverbuildDiagnostics.CurrentTick(),
                    playerId,
                    mapper,
                    targetX,
                    targetY);
                // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_END
#endif

                // Mapper 54 is Vanilla's first unconditional branch. Only EBX is temporary here;
                // the original mapper remains on the stack for footprint creation and spawning.
                registers->RBX = BetterAIOverbuildPolicy.MapperHovel;
            }
            catch (Exception ex)
            {
                LogCallbackFailure("mapper promotion", ex);
            }
        }

        private void ProtectReciprocalForeignBlocker(NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int blockerId = unchecked((int)(uint)registers->R8);
                int blockerStructureType = unchecked((ushort)registers->RCX);
                ReadPlacementContext(
                    registers,
                    out int placingPlayerId,
                    out int mapper,
                    out int targetX,
                    out int targetY);
                if (blockerId <= 0 || placingPlayerId < 1 || placingPlayerId > 8 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        blockerId, out GameBuilding* blocker) ||
                    (blocker->r_AliveState != AliveState.NeedsInit &&
                     blocker->r_AliveState != AliveState.IsAlive) ||
                    blocker->r_BuildingType != (eStructs)blockerStructureType)
                {
                    return;
                }

                int blockerOwnerId = blocker->r_PlayerIdOwner;
                bool blockerOwnerIsAi = blockerOwnerId >= 1 && blockerOwnerId <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(blockerOwnerId);
                int keepId = blockerOwnerIsAi
                    ? GamePlayerManagerAPI.Instance.GetPlayerKeepId(blockerOwnerId)
                    : -1;
                bool blockerHasKeep = keepId > 0;
                UnmanagedVector2<int> keepPosition = blockerHasKeep
                    ? GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(blockerOwnerId)
                    : default;
                BetterAIOverbuildProtectionReason reason =
                    BetterAIOverbuildPolicy.ClassifyForeignBlocker(
                        placingPlayerId,
                        blockerOwnerId,
                        blockerOwnerIsAi,
                        blockerStructureType,
                        blockerHasKeep,
                        blocker->r_TilePositionXBegin,
                        blocker->r_TilePositionYBegin,
                        keepPosition.X,
                        keepPosition.Y,
                        out long distance);

                if (blockerOwnerIsAi && blockerOwnerId != placingPlayerId)
                {
#if BETTER_AI_OVERBUILD_DIAGNOSTICS
                    // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_BEGIN
                    BetterAIOverbuildDiagnostics.ForeignBlockerDecision(
                        BetterAIOverbuildDiagnostics.CurrentTick(),
                        placingPlayerId,
                        mapper,
                        targetX,
                        targetY,
                        unchecked((int)(uint)registers->R12),
                        blockerId,
                        blocker->r_GlobalId,
                        blockerOwnerId,
                        blockerStructureType,
                        blocker->r_TilePositionXBegin,
                        blocker->r_TilePositionYBegin,
                        blockerHasKeep,
                        keepPosition.X,
                        keepPosition.Y,
                        distance,
                        reason);
                    // TEMP_BETTER_AI_OVERBUILD_DIAGNOSTICS_END
#endif
                }

                if (reason != BetterAIOverbuildProtectionReason.None)
                {
                    // Type 40 is protected by Vanilla's existing broad mask. This changes only
                    // the classifier register; the live building record is never modified.
                    registers->RCX = NativeProtectedSurrogateType;
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("foreign blocker protection", ex);
            }
        }

        private static void ReadPlacementContext(
            X64SmartCPUContext* registers,
            out int playerId,
            out int mapper,
            out int targetX,
            out int targetY)
        {
            playerId = *(int*)(registers->RSP + PlacementPlayerIdStackOffset);
            mapper = *(short*)(registers->RSP + PlacementMapperStackOffset);
            int offsetX = *(int*)(registers->RSP + PlacementOffsetXStackOffset);
            int offsetY = *(int*)(registers->RSP + PlacementOffsetYStackOffset);
            targetX = checked(*(int*)(registers->R14 + PlacementOriginXOffset) + offsetX);
            targetY = checked(*(int*)(registers->R14 + PlacementOriginYOffset) + offsetY);
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
