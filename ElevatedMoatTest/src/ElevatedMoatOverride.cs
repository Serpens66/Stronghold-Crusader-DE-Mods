using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using System;
using System.Threading;

namespace ElevatedMoatTest
{
    internal sealed unsafe class ElevatedMoatOverride : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly IDisposable placementValidationSubscription;
        private readonly HookHandle<X64InlineHook> heightFailureWriterHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> tileValidationResultHook =
            new HookHandle<X64InlineHook>();
        private int firstInterventionLogged;
        private int callbackFailureLogged;
        private int eventFailureLogged;
        private int diagnosticLogCount;
        private string lastDiagnosticFingerprint;
        private int repeatedDiagnosticCount;
        private readonly object diagnosticLock = new object();
        private long validationSequence;

        private const int MaximumDiagnosticLogs = 200;

        [ThreadStatic]
        private static MoatValidationTrace currentMoatTrace;

        internal ElevatedMoatOverride(
            ManualLogSource log,
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (context == null || context.ModuleHandle == IntPtr.Zero || context.Memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.", nameof(context));
            if (!referenceHashMatches)
                throw new InvalidOperationException("Elevated Moat Test requires the audited CrusaderDE.dll hash.");
            if (!string.Equals(
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    ElevatedMoatNativeContract.ReferenceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The shared native hash no longer matches the Elevated Moat Test audit contract.");
            }

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.HeightWriterPattern,
                ElevatedMoatNativeContract.HeightWriterRva,
                true,
                "elevated-moat height failure writer",
                log);
            if (resolution.Rva != ElevatedMoatNativeContract.HeightWriterRva)
                throw new InvalidOperationException("The elevated-moat writer resolved outside its audited RVA.");

            Shared.NativeResolution tileResultResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.TileValidationResultPattern,
                ElevatedMoatNativeContract.TileValidationResultRva,
                true,
                "moat tile-validation result",
                log);
            if (tileResultResolution.Rva != ElevatedMoatNativeContract.TileValidationResultRva)
                throw new InvalidOperationException("The tile-validation result resolved outside its audited RVA.");

            ElevatedMoatNativeContract.Validate(memory, resolution.Rva);
            ElevatedMoatNativeContract.ValidateTileValidationResultHook(memory, tileResultResolution.Rva);
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)resolution.Rva),
                ElevatedMoatNativeContract.HeightWriterLength))
            {
                if (probe.DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength)
                    throw new InvalidOperationException("Unexpected RedBird elevated-moat writer span before installation.");
            }
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)tileResultResolution.Rva),
                ElevatedMoatNativeContract.TileValidationResultLength))
            {
                if (probe.DisplacedByteCount != ElevatedMoatNativeContract.TileValidationResultLength)
                    throw new InvalidOperationException("Unexpected RedBird tile-validation result span before installation.");
            }

            HookTransaction pending = null;
            IDisposable pendingSubscription = null;
            try
            {
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddContextHook(
                    heightFailureWriterHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)resolution.Rva)),
                    SuppressHeightFailureWriter,
                    new ContextHookOptions
                    {
                        // The managed callback must preserve every live GPR around this validator path.
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.HeightWriterLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.Suppress
                    });
                pending.AddContextHook(
                    tileValidationResultHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)tileResultResolution.Rva)),
                    ObserveTileValidationResult,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.TileValidationResultLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        // Observe EAX before TEST/JZ and then execute the original result block.
                        Placement = OverwrittenInstructionPlacement.AfterCallback
                    });

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess ||
                    !heightFailureWriterHook.Success ||
                    !tileValidationResultHook.Success)
                    throw new InvalidOperationException($"The elevated-moat hook transaction was incomplete: {result}.");
                if (heightFailureWriterHook.Hook.DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength)
                    throw new InvalidOperationException("Unexpected installed elevated-moat overwrite length; rolling back.");
                if (tileValidationResultHook.Hook.DisplacedByteCount !=
                    ElevatedMoatNativeContract.TileValidationResultLength)
                {
                    throw new InvalidOperationException(
                        "Unexpected installed tile-validation result overwrite length; rolling back.");
                }

                pendingSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable
                    .Subscribe(ObservePlacementValidation);

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Elevated Moat Test active for all players with diagnostics: " +
                    $"heightWriterRva=0x{resolution.Rva:X}, heightHookLength={heightFailureWriterHook.Hook.DisplacedByteCount}, " +
                    $"tileResultRva=0x{tileResultResolution.Rva:X}, tileResultHookLength={tileValidationResultHook.Hook.DisplacedByteCount}, " +
                    $"mapperMoat={ElevatedMoatNativeContract.MapperMoat}, " +
                    $"vanillaMaximumHeight={ElevatedMoatNativeContract.MaximumVanillaTerrainHeight}.");

                transaction = pending;
                placementValidationSubscription = pendingSubscription;
                pending = null;
                pendingSubscription = null;
            }
            catch
            {
                pendingSubscription?.Dispose();
                if (pending != null)
                {
                    try
                    {
                        pending.Dispose();
                    }
                    catch
                    {
                    }
                }
                throw;
            }
        }

        public void Dispose()
        {
            placementValidationSubscription?.Dispose();
            transaction?.Dispose();
        }

        private void SuppressHeightFailureWriter(NativePointer<X64SmartCPUContext> context)
        {
            // Suppression itself is performed by RedBird. This observer never writes native state,
            // so validation failures already set by earlier Vanilla rules remain untouched.
            try
            {
                int playerId = unchecked((int)context.Pointer->RBP);
                int maximumHeight = *(int*)(context.Pointer->RBX +
                    ElevatedMoatNativeContract.MaximumFootprintHeightOffset);
                MoatValidationTrace trace = currentMoatTrace;
                if (trace != null)
                {
                    trace.ManagerAddress = context.Pointer->RBX;
                    trace.HeightSuppressed = true;
                    trace.MaximumHeightAtSuppression = maximumHeight;
                    trace.HeightSuppressionCount++;
                }
                if (Interlocked.Exchange(ref firstInterventionLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Elevated moat height block suppressed for the first time: " +
                        $"playerId={playerId}, maximumHeight={maximumHeight}.");
                }
            }
            catch (Exception exception)
            {
                if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Elevated Moat Test first-hit diagnostics failed; suppression remains active: {exception}");
                }
            }
        }

        private void ObserveTileValidationResult(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                MoatValidationTrace trace = currentMoatTrace;
                if (trace == null)
                    return;

                ulong managerAddress = context.Pointer->RBX;
                trace.ManagerAddress = managerAddress;
                trace.TileValidationCount++;
                int result = unchecked((int)context.Pointer->RAX);
                if (result == 0)
                    return;

                int tileX = trace.TileX + *(int*)(managerAddress +
                    ElevatedMoatNativeContract.FootprintTileXOffset);
                int tileY = trace.TileY + *(int*)(managerAddress +
                    ElevatedMoatNativeContract.FootprintTileYOffset);
                trace.TileRejectionCount++;
                if (!trace.HasFirstTileRejection)
                {
                    trace.HasFirstTileRejection = true;
                    trace.FirstRejectedTileX = tileX;
                    trace.FirstRejectedTileY = tileY;
                    trace.FirstTileRejectionResult = result;
                }
                trace.LastRejectedTileX = tileX;
                trace.LastRejectedTileY = tileY;
                trace.LastTileRejectionResult = result;
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("tile-validation result", exception);
            }
        }

        private void ObservePlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            if (args == null || args.Mappers != eMappers.MAPPER_MOAT)
                return;

            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    currentMoatTrace = new MoatValidationTrace
                    {
                        Sequence = Interlocked.Increment(ref validationSequence),
                        PlayerId = args.PlayerId,
                        TileX = args.TileX,
                        TileY = args.TileY,
                        Unknown1 = args.Unknown1,
                        Unknown2 = args.Unknown2
                    };
                    return;
                }

                if (args.Phase != EventHookPhase.Post)
                    return;

                MoatValidationTrace trace = currentMoatTrace ?? new MoatValidationTrace
                {
                    Sequence = Interlocked.Increment(ref validationSequence),
                    PlayerId = args.PlayerId,
                    TileX = args.TileX,
                    TileY = args.TileY,
                    Unknown1 = args.Unknown1,
                    Unknown2 = args.Unknown2
                };
                LogCompletedValidation(trace);
                currentMoatTrace = null;
            }
            catch (Exception exception)
            {
                currentMoatTrace = null;
                if (Interlocked.Exchange(ref eventFailureLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Elevated Moat Test placement-event diagnostics failed: {exception}");
                }
            }
        }

        private void LogCompletedValidation(MoatValidationTrace trace)
        {
            int status = -1;
            int reason = -1;
            int minimumHeight = -1;
            int maximumHeight = -1;
            int effectiveMinimumHeight = -1;
            if (trace.ManagerAddress != 0)
            {
                status = *(int*)(trace.ManagerAddress + ElevatedMoatNativeContract.PlacementBlockedOffset);
                reason = *(int*)(trace.ManagerAddress + ElevatedMoatNativeContract.PlacementFailureReasonOffset);
                minimumHeight = *(int*)(trace.ManagerAddress + ElevatedMoatNativeContract.MinimumFootprintHeightOffset);
                maximumHeight = *(int*)(trace.ManagerAddress + ElevatedMoatNativeContract.MaximumFootprintHeightOffset);
                effectiveMinimumHeight = *(int*)(trace.ManagerAddress +
                    ElevatedMoatNativeContract.EffectiveMinimumFootprintHeightOffset);
            }

            string playerKind = "unknown";
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                playerKind = players.IsPlayerIdValid(trace.PlayerId)
                    ? (players.IsAIPlayer(trace.PlayerId) ? "ai" : "human")
                    : "invalid";
            }
            catch
            {
            }

            string fingerprint =
                $"{trace.PlayerId}|{trace.TileX}|{trace.TileY}|{trace.Unknown1}|{trace.Unknown2}|" +
                $"{trace.HeightSuppressed}|{status}|{reason}|{minimumHeight}|{maximumHeight}|" +
                $"{trace.TileValidationCount}|{trace.TileRejectionCount}|" +
                $"{trace.FirstRejectedTileX}|{trace.FirstRejectedTileY}|{trace.FirstTileRejectionResult}";
            int repeatedBeforeThis;
            lock (diagnosticLock)
            {
                if (string.Equals(lastDiagnosticFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    repeatedDiagnosticCount++;
                    return;
                }
                if (diagnosticLogCount >= MaximumDiagnosticLogs)
                    return;

                repeatedBeforeThis = repeatedDiagnosticCount;
                repeatedDiagnosticCount = 0;
                lastDiagnosticFingerprint = fingerprint;
                diagnosticLogCount++;
            }

            Shared.DebugLogHelper.LogWarning(
                log,
                $"MOAT_VALIDATION: sequence={trace.Sequence}, playerId={trace.PlayerId}, playerKind={playerKind}, " +
                $"originTile={trace.TileX},{trace.TileY}, unknown1={trace.Unknown1}, unknown2={trace.Unknown2}, " +
                $"heightSuppressed={trace.HeightSuppressed}, suppressionCount={trace.HeightSuppressionCount}, " +
                $"heightAtSuppression={trace.MaximumHeightAtSuppression}, " +
                $"heightRange={minimumHeight}..{maximumHeight}, effectiveMinimumHeight={effectiveMinimumHeight}, " +
                $"finalStatus={status}, finalReason={reason}, tileChecks={trace.TileValidationCount}, " +
                $"tileRejections={trace.TileRejectionCount}, " +
                $"firstRejectedTile={trace.FirstRejectedTileX},{trace.FirstRejectedTileY}, " +
                $"firstTileResult={trace.FirstTileRejectionResult}, " +
                $"lastRejectedTile={trace.LastRejectedTileX},{trace.LastRejectedTileY}, " +
                $"lastTileResult={trace.LastTileRejectionResult}, repeatedPrevious={repeatedBeforeThis}, " +
                $"diagnosticLog={diagnosticLogCount}/{MaximumDiagnosticLogs}.");
        }

        private void LogCallbackFailureOnce(string stage, Exception exception)
        {
            if (Interlocked.Exchange(ref callbackFailureLogged, 1) == 0)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Elevated Moat Test {stage} callback failed; gameplay state remains untouched: {exception}");
            }
        }

        private sealed class MoatValidationTrace
        {
            internal long Sequence;
            internal int PlayerId;
            internal int TileX;
            internal int TileY;
            internal int Unknown1;
            internal byte Unknown2;
            internal ulong ManagerAddress;
            internal bool HeightSuppressed;
            internal int HeightSuppressionCount;
            internal int MaximumHeightAtSuppression = -1;
            internal int TileValidationCount;
            internal int TileRejectionCount;
            internal bool HasFirstTileRejection;
            internal int FirstRejectedTileX = -1;
            internal int FirstRejectedTileY = -1;
            internal int FirstTileRejectionResult = -1;
            internal int LastRejectedTileX = -1;
            internal int LastRejectedTileY = -1;
            internal int LastTileRejectionResult = -1;
        }
    }
}
