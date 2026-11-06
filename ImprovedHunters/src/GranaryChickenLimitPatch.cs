using BepInEx.Logging;
using System;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;

namespace ImprovedHunters
{
    /// <summary>
    /// Replaces Vanilla's food-based chicken target with the configured value immediately
    /// before its existing comparison with the per-player chicken counter.
    /// </summary>
    internal sealed unsafe class GranaryChickenLimitPatch : IDisposable
    {
        private const int ComparisonSequenceRva = 0xD2AB4;
        private const int ComparisonHookOffset = 11;
        private const int ExpectedSkipSpawnTargetRva = 0xD2BA7;
        private const int ExpectedSkipSpawnTargetOffset = ExpectedSkipSpawnTargetRva - ComparisonSequenceRva;
        private const int MinimumPlayerId = 1;
        private const int MaximumPlayerId = 8;

        private const string ComparisonSequencePattern =
            "83 3D ?? ?? ?? ?? 00 41 0F 45 C5 3B 87 48 20 00 00 " +
            "0F 8E ?? ?? ?? ?? 41 B9 22 00 00 00 C7 44 24 20 13 00 00 00";

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<bool> canManageChickens;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> comparisonHook = new HookHandle<X64InlineHook>();
        private bool featureAvailable = true;
        private bool hookConfirmed;
        private bool disposed;

        public GranaryChickenLimitPatch(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canManageChickens)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.canManageChickens = canManageChickens ?? throw new ArgumentNullException(nameof(canManageChickens));
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int sequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ComparisonSequencePattern,
                ComparisonSequenceRva,
                referenceHashMatches,
                "granary chicken target comparison",
                log).Rva;
            int hookRva = checked(sequenceRva + ComparisonHookOffset);
            int skipSpawnTargetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                sequenceRva + 19,
                sequenceRva + 23);
            if (skipSpawnTargetRva - sequenceRva != ExpectedSkipSpawnTargetOffset)
            {
                throw new InvalidOperationException(
                    $"The granary chicken comparison branches to an unexpected target: " +
                    $"expectedRelative=+0x{ExpectedSkipSpawnTargetOffset:X}, " +
                    $"actualRelative=+0x{skipSpawnTargetRva - sequenceRva:X}.");
            }

            try
            {
                transaction = HunterHookInfrastructure.CreateOwnedTransaction(region);
                HunterHookInfrastructure.AddContextHook(
                    transaction,
                    comparisonHook,
                    libraryBase + unchecked((ulong)hookRva),
                    ApplyConfiguredLimit,
                    registers: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult commitResult = transaction.Commit();

                if (!commitResult.IsCompleteSuccess || !comparisonHook.Success)
                    throw new InvalidOperationException("The granary chicken target comparison hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters granary chicken limit initialized: comparisonRva=0x{hookRva:X}, " +
                    $"skipSpawnRva=0x{skipSpawnTargetRva:X}, configured={settings.ChickensPerPlayer}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsAvailable => featureAvailable && !disposed && comparisonHook.Success;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            featureAvailable = false;
        }

        private void ApplyConfiguredLimit(NativePointer<X64SmartCPUContext> context)
        {
            if (!featureAvailable)
                return;

            int playerId;
            int target;
            try
            {
                if (!canManageChickens())
                    return;

                playerId = checked((int)context.Pointer->RBX);
                if (playerId < MinimumPlayerId || playerId > MaximumPlayerId)
                {
                    throw new InvalidOperationException(
                        $"The granary chicken comparison exposed invalid player ID {playerId}.");
                }

                target = GranaryChickenSpawnPolicy.ClampTarget(settings.ChickensPerPlayer);

                // The displaced instruction is still `cmp eax,[rdi+2048h]`, Vanilla's own count of
                // this player's chickens. Its flags decide both following branches: spawn one
                // below the target, remove one above it (the jge at the jle target reuses them).
                context.Pointer->RAX = unchecked((ulong)target);
            }
            catch (Exception exception)
            {
                DisableFeature(exception);
                return;
            }

            // Diagnostics cannot change a successfully prepared comparison.
            TryLogDecision(playerId, target);
        }

        private void TryLogDecision(int playerId, int target)
        {
            try
            {
                if (hookConfirmed)
                    return;

                hookConfirmed = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters chicken population hook confirmed: player={playerId}, target={target}.");
            }
            catch
            {
                // Never let diagnostics alter Vanilla or the independent limit decision.
            }
        }

        private void DisableFeature(Exception failure)
        {
            if (!featureAvailable)
                return;

            featureAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters chicken population setting is disabled for this process; " +
                $"Vanilla's food-based population remains active: {failure}");
        }
    }
}
