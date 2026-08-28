// Feature: Let valid moat-digging orders traverse already completed friendly moats.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class MoatDiggingReachabilityFix : IDisposable
    {
        private const int DigMoatModePatternRva = 0x8D3C2;
        private const int CursorReachabilityPatternRva = 0x8F3A8;
        private const int CursorReachabilityHookOffset = 29;
        private const int CursorReachabilityHookLength = 12;

        private const string DigMoatModePattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? " +
            "85 C0 74 30 B8 01 00 00 00 44 8B E8 89 44 24 54";

        private const string CursorReachabilityPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? " +
            "85 C0 74 11 44 8B BC 24 C0 00 00 00";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HookTransaction transaction;
        private readonly int* digMoatMode;
        private readonly int* targetTileX;
        private readonly int* targetTileY;
        private HookRef<X64InlineHook> cursorReachabilityHook = new HookRef<X64InlineHook>();
        private IDisposable orderSubscription;
        private int replayDepth;
        private bool firstReplayLogged;
        private bool cursorFailureLogged;
        private bool disposed;

        public MoatDiggingReachabilityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DigMoatModePattern,
                DigMoatModePatternRva,
                referenceHashMatches,
                "DigMoat cursor mode",
                log: null);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityPattern,
                CursorReachabilityPatternRva,
                referenceHashMatches,
                "DigMoat cursor reachability check",
                log: null);

            int modeRva = ResolveGlobalRva(
                memory,
                modeResolution.Rva + 3,
                modeResolution.Rva + 7,
                "DigMoat cursor mode");
            int targetYRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 3,
                cursorResolution.Rva + 7,
                "cursor target Y");
            int targetXRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 17,
                cursorResolution.Rva + 21,
                "cursor target X");
            int hookRva = checked(cursorResolution.Rva + CursorReachabilityHookOffset);
            ValidateHookSpan(memory, hookRva);

            digMoatMode = (int*)(libraryBase + unchecked((ulong)modeRva));
            targetTileX = (int*)(libraryBase + unchecked((ulong)targetXRva));
            targetTileY = (int*)(libraryBase + unchecked((ulong)targetYRva));

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref cursorReachabilityHook,
                    libraryBase + unchecked((ulong)hookRva),
                    AllowFriendlyPlannedMoatCursor,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: CursorReachabilityHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!cursorReachabilityHook.Success)
                    throw new InvalidOperationException("The DigMoat cursor reachability hook was not installed.");

                orderSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(OnTribeIssueOrderWithTarget);

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL moat-digging reachability fix installed: " +
                    $"modeMethod={modeResolution.Method}, cursorMethod={cursorResolution.Method}, " +
                    $"modeRva=0x{modeRva:X}, targetXRva=0x{targetXRva:X}, " +
                    $"targetYRva=0x{targetYRva:X}, hookRva=0x{hookRva:X}.");
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Bugfixes and QoL moat-digging reachability fix is running on an unknown CrusaderDE.dll because both native cursor contracts and their RIP-relative globals were validated.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            orderSubscription?.Dispose();
            orderSubscription = null;
            transaction?.Unload();
            transaction?.Dispose();
        }

        private void OnTribeIssueOrderWithTarget(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!IsEnabled || replayDepth != 0 || args.Phase != EventHookPhase.Post ||
                args.AICommand != TribeAICommand.DigMoatTileId ||
                !TryGetTribeOwner(args.TribeId, out int playerId) ||
                !IsFriendlyPlannedMoat(playerId, args.TargetValue1, args.TargetValue2))
            {
                return;
            }

            replayDepth++;
            try
            {
                // Vanilla writes command 6 and the exact requested coordinates even when its
                // first movement attempt fails. Replaying synchronously lets that second attempt
                // use Vanilla's existing "already digging" permission to cross completed moats.
                bool issued = GameTribeManagerAPI.Instance.IssueTargettedCommand(
                    args.TribeId,
                    args.AICommand,
                    args.TargetValue1,
                    args.TargetValue2,
                    args.a6);
                if (!issued)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Bugfixes and QoL moat-digging replay was rejected: tribe={args.TribeId}, " +
                        $"target=({args.TargetValue1},{args.TargetValue2}).");
                }
                else if (!firstReplayLogged)
                {
                    firstReplayLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Bugfixes and QoL first moat-digging replay issued: tribe={args.TribeId}, " +
                        $"player={playerId}, target=({args.TargetValue1},{args.TargetValue2}).");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat-digging replay failed; later commands remain available: {ex}");
            }
            finally
            {
                replayDepth--;
            }
        }

        private void AllowFriendlyPlannedMoatCursor(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (unchecked((uint)registers->RAX) != 0 || !IsEnabled || *digMoatMode == 0)
                return;

            try
            {
                int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (IsFriendlyPlannedMoat(playerId, *targetTileX, *targetTileY))
                    registers->RAX = 1;
            }
            catch (Exception ex)
            {
                if (cursorFailureLogged)
                    return;

                cursorFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat cursor validation failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private bool IsEnabled =>
            !disposed && settings.EnableMod && settings.EnableMoatDiggingReachabilityFix;

        private static bool TryGetTribeOwner(int tribeId, out int playerId)
        {
            playerId = 0;
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null)
            {
                return false;
            }

            playerId = tribe->r_PlayerIdOwner;
            return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);
        }

        private static bool IsFriendlyPlannedMoat(int playerId, int tileX, int tileY)
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId))
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId) ||
                !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.PlannedMoat))
            {
                return false;
            }

            int moatOwnerId = tileApi.GetTilePlayerOwnerId(tileId);
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
                return false;

            return moatOwnerId == playerId || playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
        }

        private static int ResolveGlobalRva(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (resolvedRva < 0 || resolvedRva > memory.Length - sizeof(int))
                throw new InvalidOperationException($"The native {label} global is outside CrusaderDE.dll.");
            return resolvedRva;
        }

        private static void ValidateHookSpan(ReadOnlySpan<byte> memory, int hookRva)
        {
            byte[] expected =
            {
                0x85, 0xC0, 0x74, 0x11,
                0x44, 0x8B, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00
            };
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native DigMoat cursor hook span did not match the validated instructions.");
            }
        }
    }
}
