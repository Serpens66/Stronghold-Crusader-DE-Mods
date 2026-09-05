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
    /// Normalizes Vanilla's chicken target immediately before its existing
    /// comparison with the per-player granary chicken counter.
    /// </summary>
    internal sealed unsafe class GranaryChickenLimitPatch : IDisposable
    {
        private const int ComparisonSequenceRva = 0xD2AB4;
        private const int ComparisonHookOffset = 11;
        private const int ExpectedSkipSpawnTargetRva = 0xD2BA7;
        private const int ExpectedSkipSpawnTargetOffset = ExpectedSkipSpawnTargetRva - ComparisonSequenceRva;
        private const int MinimumPlayerId = 1;
        private const int MaximumPlayerId = 8;
        private const int MaxDecisionLogs = 80;

        private const string ComparisonSequencePattern =
            "83 3D ?? ?? ?? ?? 00 41 0F 45 C5 3B 87 48 20 00 00 " +
            "0F 8E ?? ?? ?? ?? 41 B9 22 00 00 00 C7 44 24 20 13 00 00 00";

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Func<int, int> getLiveChickenCount;
        private readonly Func<bool> canManageChickens;
        private readonly int[] lastLoggedCounts = new int[MaximumPlayerId + 1];
        private readonly int[] lastLoggedLimits = new int[MaximumPlayerId + 1];
        private readonly bool[] lastLoggedDecisions = new bool[MaximumPlayerId + 1];
        private readonly bool[] hasLoggedDecision = new bool[MaximumPlayerId + 1];
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> comparisonHook = new HookHandle<X64InlineHook>();
        private bool featureAvailable = true;
        private bool hookConfirmed;
        private int decisionLogs;
        private bool disposed;

        public GranaryChickenLimitPatch(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<int, int> getLiveChickenCount,
            Func<bool> canManageChickens)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.getLiveChickenCount = getLiveChickenCount ?? throw new ArgumentNullException(nameof(getLiveChickenCount));
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
                    $"skipSpawnRva=0x{skipSpawnTargetRva:X}, configured={settings.MaxNeutralChickensPerPlayer}.");
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
            transaction?.Dispose();
            transaction = null;
        }

        private void ApplyConfiguredLimit(NativePointer<X64SmartCPUContext> context)
        {
            if (!featureAvailable)
                return;

            int playerId;
            int liveCount;
            int configuredLimit;
            bool allowSpawn;
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

                liveCount = getLiveChickenCount(playerId);
                configuredLimit = GranaryChickenSpawnPolicy.ClampMaximum(settings.MaxNeutralChickensPerPlayer);
                if (!GranaryChickenSpawnPolicy.TryGetNormalizedVanillaTarget(
                    managementEnabled: true,
                    liveChickenCount: liveCount,
                    configuredMaximum: configuredLimit,
                    out int normalizedTarget))
                {
                    return;
                }
                allowSpawn = normalizedTarget != 0;

                // The displaced instruction is still `cmp eax,[rdi+2048h]`.
                // INT_MAX always permits its following signed jle to fall through;
                // zero always takes the skip branch because Vanilla's count is nonnegative.
                context.Pointer->RAX = unchecked((ulong)normalizedTarget);
            }
            catch (Exception exception)
            {
                DisableFeature(exception);
                return;
            }

            // Diagnostics cannot change a successfully prepared comparison.
            TryLogDecision(playerId, liveCount, configuredLimit, allowSpawn);
        }

        private void TryLogDecision(int playerId, int liveCount, int configuredLimit, bool allowSpawn)
        {
            try
            {
                if (!hookConfirmed)
                {
                    hookConfirmed = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters granary chicken limit hook confirmed: player={playerId}, " +
                        $"live={liveCount}, limit={configuredLimit}, allowNextSpawn={allowSpawn}.");
                }

                LogChangedDecision(playerId, liveCount, configuredLimit, allowSpawn);
            }
            catch
            {
                // Never let diagnostics alter Vanilla or the independent limit decision.
            }
        }

        private void LogChangedDecision(int playerId, int liveCount, int configuredLimit, bool allowSpawn)
        {
            if (decisionLogs >= MaxDecisionLogs ||
                hasLoggedDecision[playerId] &&
                lastLoggedCounts[playerId] == liveCount &&
                lastLoggedLimits[playerId] == configuredLimit &&
                lastLoggedDecisions[playerId] == allowSpawn)
            {
                return;
            }

            hasLoggedDecision[playerId] = true;
            lastLoggedCounts[playerId] = liveCount;
            lastLoggedLimits[playerId] = configuredLimit;
            lastLoggedDecisions[playerId] = allowSpawn;
            decisionLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters granary chicken limit decision: player={playerId}, live={liveCount}, " +
                $"limit={configuredLimit}, allowNextSpawn={allowSpawn} ({decisionLogs}/{MaxDecisionLogs}).");
        }

        private void DisableFeature(Exception failure)
        {
            if (!featureAvailable)
                return;

            featureAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters granary chicken limit is disabled for this process; " +
                $"Vanilla spawning remains active and no further chickens will be neutralized: {failure}");
        }
    }
}
