using BepInEx.Logging;
using Iced.Intel;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
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
using static Iced.Intel.AssemblerRegisters;

namespace ElevatedMoatTest
{
    internal sealed unsafe class ElevatedMoatOverride : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly IDisposable placementValidationSubscription;
        private readonly IDisposable buildStructureSubscription;
        private readonly HookHandle<X64InlineHook> heightFailureWriterHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> tileValidationResultHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivHeightGateHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivCreatePathHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> humanMoatWriterResultHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> sharedHeightGateHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> excavationCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> rebuildCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> directCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> genericCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> directRemovalHeightHook =
            new HookHandle<X64InlineHook>();
        private int firstInterventionLogged;
        private int callbackFailureLogged;
        private int eventFailureLogged;
        private int diagnosticLogCount;
        private string lastDiagnosticFingerprint;
        private int repeatedDiagnosticCount;
        private readonly object diagnosticLock = new object();
        private long validationSequence;
        private long buildSequence;
        private int aivAttemptLogCount;
        private int nativeActionLogCount;
        private int adaptiveHeightLogCount;

        private const int MaximumDiagnosticLogs = 200;
        private const int MaximumNativeActionLogs = 100;
        private const int MaximumAdaptiveHeightLogs = 100;

        [ThreadStatic]
        private static MoatValidationTrace currentMoatTrace;

        [ThreadStatic]
        private static MoatBuildTrace currentMoatBuildTrace;

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

            Shared.NativeResolution aivGateResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.AivHeightGatePattern,
                ElevatedMoatNativeContract.AivHeightGateRva,
                true,
                "AIV moat height gate",
                log);
            Shared.NativeResolution aivCreateResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.AivCreatePathPattern,
                ElevatedMoatNativeContract.AivCreatePathRva,
                true,
                "AIV moat creation path",
                log);
            Shared.NativeResolution humanResultResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.HumanMoatWriterResultPattern,
                ElevatedMoatNativeContract.HumanMoatWriterResultRva,
                true,
                "human moat writer result",
                log);
            Shared.NativeResolution sharedGateResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.SharedHeightGatePattern,
                ElevatedMoatNativeContract.SharedHeightGateRva, "shared moat height gate", log);
            Shared.NativeResolution aivHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.AivCompletedHeightPattern,
                ElevatedMoatNativeContract.AivCompletedHeightRva, "AIV completed-moat height", log);
            Shared.NativeResolution excavationHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.ExcavationCompletedHeightPattern,
                ElevatedMoatNativeContract.ExcavationCompletedHeightRva, "excavated-moat height", log);
            Shared.NativeResolution rebuildHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.RebuildCompletedHeightPattern,
                ElevatedMoatNativeContract.RebuildCompletedHeightRva, "rebuilt-moat height", log);
            Shared.NativeResolution directHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DirectCompletedHeightPattern,
                ElevatedMoatNativeContract.DirectCompletedHeightRva, "direct completed-moat height", log);
            Shared.NativeResolution genericHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.GenericCompletedHeightPattern,
                ElevatedMoatNativeContract.GenericCompletedHeightRva, "generic completed-moat height", log);
            Shared.NativeResolution removalHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DirectRemovalHeightPattern,
                ElevatedMoatNativeContract.DirectRemovalHeightRva, "direct moat-removal height", log);
            if (aivGateResolution.Rva != ElevatedMoatNativeContract.AivHeightGateRva ||
                aivCreateResolution.Rva != ElevatedMoatNativeContract.AivCreatePathRva ||
                humanResultResolution.Rva != ElevatedMoatNativeContract.HumanMoatWriterResultRva)
            {
                throw new InvalidOperationException("A new elevated-moat hook resolved outside its audited RVA.");
            }

            ElevatedMoatNativeContract.Validate(memory, resolution.Rva);
            ElevatedMoatNativeContract.ValidateTileValidationResultHook(memory, tileResultResolution.Rva);
            ElevatedMoatNativeContract.ValidateAivHooks(
                memory,
                aivGateResolution.Rva,
                aivCreateResolution.Rva);
            ElevatedMoatNativeContract.ValidateHumanMoatWriterResultHook(
                memory,
                humanResultResolution.Rva);
            ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(memory);
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
            ProbeExactHookLength(
                imageBase,
                aivGateResolution.Rva,
                ElevatedMoatNativeContract.AivHeightGateLength,
                "AIV moat height gate");
            ProbeExactHookLength(
                imageBase,
                aivCreateResolution.Rva,
                ElevatedMoatNativeContract.AivCreatePathLength,
                "AIV moat creation path");
            ProbeExactHookLength(
                imageBase,
                humanResultResolution.Rva,
                ElevatedMoatNativeContract.HumanMoatWriterResultLength,
                "human moat writer result");
            ProbeExactHookLength(imageBase, sharedGateResolution.Rva,
                ElevatedMoatNativeContract.SharedHeightGateLength, "shared moat height gate");
            ProbeExactHookLength(imageBase, aivHeightResolution.Rva,
                ElevatedMoatNativeContract.AivCompletedHeightLength, "AIV completed-moat height");
            ProbeExactHookLength(imageBase, excavationHeightResolution.Rva,
                ElevatedMoatNativeContract.ExcavationCompletedHeightLength, "excavated-moat height");
            ProbeExactHookLength(imageBase, rebuildHeightResolution.Rva,
                ElevatedMoatNativeContract.RebuildCompletedHeightLength, "rebuilt-moat height");
            ProbeExactHookLength(imageBase, directHeightResolution.Rva,
                ElevatedMoatNativeContract.DirectCompletedHeightLength, "direct completed-moat height");
            ProbeExactHookLength(imageBase, genericHeightResolution.Rva,
                ElevatedMoatNativeContract.GenericCompletedHeightLength, "generic completed-moat height");
            ProbeExactHookLength(imageBase, removalHeightResolution.Rva,
                ElevatedMoatNativeContract.DirectRemovalHeightLength, "direct moat-removal height");

            HookTransaction pending = null;
            IDisposable pendingSubscription = null;
            IDisposable pendingBuildSubscription = null;
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
                pending.AddInline(
                    aivHeightGateHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)aivGateResolution.Rva)),
                    (assembler, instructions, returnAddress) => GenerateAivHeightBypass(
                        assembler,
                        instructions,
                        returnAddress,
                        imageBase + ElevatedMoatNativeContract.AivCreatePathRva),
                    hookSize: ElevatedMoatNativeContract.AivHeightGateLength);
                pending.AddContextHook(
                    aivCreatePathHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)aivCreateResolution.Rva)),
                    ObserveAivMoatCreateAttempt,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.AivCreatePathLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.AfterCallback
                    });
                pending.AddContextHook(
                    humanMoatWriterResultHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)humanResultResolution.Rva)),
                    ObserveHumanMoatWriterResult,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.HumanMoatWriterResultLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        // RAX still contains FUN_180059210's result before the displaced AND/XOR.
                        Placement = OverwrittenInstructionPlacement.AfterCallback
                    });
                pending.AddContextHook(
                    sharedHeightGateHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)sharedGateResolution.Rva)),
                    SuppressSharedHeightGate,
                    SuppressingContextOptions(ElevatedMoatNativeContract.SharedHeightGateLength));
                pending.AddContextHook(
                    aivCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)aivHeightResolution.Rva)),
                    ApplyAivCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.AivCompletedHeightLength));
                pending.AddContextHook(
                    excavationCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)excavationHeightResolution.Rva)),
                    ApplyExcavationCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.ExcavationCompletedHeightLength));
                pending.AddContextHook(
                    rebuildCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)rebuildHeightResolution.Rva)),
                    ApplyRebuildCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.RebuildCompletedHeightLength));
                pending.AddContextHook(
                    directCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)directHeightResolution.Rva)),
                    ApplyDirectCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.DirectCompletedHeightLength));
                pending.AddContextHook(
                    genericCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)genericHeightResolution.Rva)),
                    ApplyGenericCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.GenericCompletedHeightLength));
                pending.AddContextHook(
                    directRemovalHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)removalHeightResolution.Rva)),
                    RestoreDirectRemovalHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.DirectRemovalHeightLength));

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess ||
                    !heightFailureWriterHook.Success ||
                    !tileValidationResultHook.Success ||
                    !aivHeightGateHook.Success ||
                    !aivCreatePathHook.Success ||
                    !humanMoatWriterResultHook.Success ||
                    !sharedHeightGateHook.Success ||
                    !aivCompletedHeightHook.Success ||
                    !excavationCompletedHeightHook.Success ||
                    !rebuildCompletedHeightHook.Success ||
                    !directCompletedHeightHook.Success ||
                    !genericCompletedHeightHook.Success ||
                    !directRemovalHeightHook.Success)
                    throw new InvalidOperationException($"The elevated-moat hook transaction was incomplete: {result}.");
                if (heightFailureWriterHook.Hook.DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength)
                    throw new InvalidOperationException("Unexpected installed elevated-moat overwrite length; rolling back.");
                if (tileValidationResultHook.Hook.DisplacedByteCount !=
                    ElevatedMoatNativeContract.TileValidationResultLength)
                {
                    throw new InvalidOperationException(
                        "Unexpected installed tile-validation result overwrite length; rolling back.");
                }
                RequireInstalledHookLength(
                    aivHeightGateHook,
                    ElevatedMoatNativeContract.AivHeightGateLength,
                    "AIV moat height gate");
                RequireInstalledHookLength(
                    aivCreatePathHook,
                    ElevatedMoatNativeContract.AivCreatePathLength,
                    "AIV moat creation path");
                RequireInstalledHookLength(
                    humanMoatWriterResultHook,
                    ElevatedMoatNativeContract.HumanMoatWriterResultLength,
                    "human moat writer result");
                RequireInstalledHookLength(sharedHeightGateHook,
                    ElevatedMoatNativeContract.SharedHeightGateLength, "shared moat height gate");
                RequireInstalledHookLength(aivCompletedHeightHook,
                    ElevatedMoatNativeContract.AivCompletedHeightLength, "AIV completed-moat height");
                RequireInstalledHookLength(excavationCompletedHeightHook,
                    ElevatedMoatNativeContract.ExcavationCompletedHeightLength, "excavated-moat height");
                RequireInstalledHookLength(rebuildCompletedHeightHook,
                    ElevatedMoatNativeContract.RebuildCompletedHeightLength, "rebuilt-moat height");
                RequireInstalledHookLength(directCompletedHeightHook,
                    ElevatedMoatNativeContract.DirectCompletedHeightLength, "direct completed-moat height");
                RequireInstalledHookLength(genericCompletedHeightHook,
                    ElevatedMoatNativeContract.GenericCompletedHeightLength, "generic completed-moat height");
                RequireInstalledHookLength(directRemovalHeightHook,
                    ElevatedMoatNativeContract.DirectRemovalHeightLength, "direct moat-removal height");

                pendingSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable
                    .Subscribe(ObservePlacementValidation);
                pendingBuildSubscription = BuildingR3EventHooks.OnBuildStructure.Observable
                    .Subscribe(ObserveBuildStructure);

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Elevated Moat Test active for all players with diagnostics: " +
                    $"heightWriterRva=0x{resolution.Rva:X}, heightHookLength={heightFailureWriterHook.Hook.DisplacedByteCount}, " +
                    $"tileResultRva=0x{tileResultResolution.Rva:X}, tileResultHookLength={tileValidationResultHook.Hook.DisplacedByteCount}, " +
                    $"aivGateRva=0x{aivGateResolution.Rva:X}, aivGateHookLength={aivHeightGateHook.Hook.DisplacedByteCount}, " +
                    $"aivCreateRva=0x{aivCreateResolution.Rva:X}, aivCreateHookLength={aivCreatePathHook.Hook.DisplacedByteCount}, " +
                    $"humanResultRva=0x{humanResultResolution.Rva:X}, humanResultHookLength={humanMoatWriterResultHook.Hook.DisplacedByteCount}, " +
                    $"sharedGateRva=0x{sharedGateResolution.Rva:X}, adaptiveDepth={ElevatedMoatNativeContract.MoatDepth}, " +
                    $"mapperMoat={ElevatedMoatNativeContract.MapperMoat}, " +
                    $"vanillaMaximumHeight={ElevatedMoatNativeContract.MaximumVanillaTerrainHeight}.");

                transaction = pending;
                placementValidationSubscription = pendingSubscription;
                buildStructureSubscription = pendingBuildSubscription;
                pending = null;
                pendingSubscription = null;
                pendingBuildSubscription = null;
            }
            catch
            {
                pendingSubscription?.Dispose();
                pendingBuildSubscription?.Dispose();
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
            buildStructureSubscription?.Dispose();
            placementValidationSubscription?.Dispose();
            transaction?.Dispose();
        }

        private static Shared.NativeResolution ResolveAudited(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string description,
            ManualLogSource log)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, expectedRva, true, description, log);
            if (resolution.Rva != expectedRva)
                throw new InvalidOperationException($"The {description} resolved outside its audited RVA.");
            return resolution;
        }

        private static ContextHookOptions SuppressingContextOptions(int hookSize) =>
            new ContextHookOptions
            {
                Registers = X64SmartCPUContextRegs.All,
                HookSize = hookSize,
                ErrorMode = CallbackErrorMode.LogAndContinue,
                Placement = OverwrittenInstructionPlacement.Suppress
            };

        private static ContextHookOptions CorrectingContextOptions(int hookSize) =>
            new ContextHookOptions
            {
                Registers = X64SmartCPUContextRegs.All,
                HookSize = hookSize,
                ErrorMode = CallbackErrorMode.LogAndContinue,
                // RedBird executes the complete audited Vanilla block first. The callback then
                // replaces only its fixed height result before native processing continues.
                Placement = OverwrittenInstructionPlacement.BeforeCallback
            };

        private void SuppressSharedHeightGate(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                ulong managerAddress = context.Pointer->RDI;
                long tileId = unchecked((long)context.Pointer->RBX);
                if (!IsValidTileId(tileId))
                    return;

                byte currentHeight = *((byte*)(managerAddress + (ulong)tileId +
                    ElevatedMoatNativeContract.TileHeightGridOffset));
                byte defaultHeight = *((byte*)(managerAddress + (ulong)tileId +
                    ElevatedMoatNativeContract.TileDefaultHeightGridOffset));
                if (currentHeight <= ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    return;

                int logNumber = Interlocked.Increment(ref nativeActionLogCount);
                if (logNumber > MaximumNativeActionLogs)
                    return;

                int mode = unchecked((int)context.Pointer->R15);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"MOAT_NATIVE_ACTION: tileId={tileId}, mode={GetNativeMoatMode(mode)}, " +
                    $"currentHeight={currentHeight}, defaultHeight={defaultHeight}, " +
                    $"diagnosticLog={logNumber}/{MaximumNativeActionLogs}.");
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("shared moat height gate", exception);
            }
        }

        private void ApplyAivCompletedHeight(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                ulong tileBase = context.Pointer->RSI;
                ApplyCompletedHeight(
                    tileBase + ElevatedMoatNativeContract.TileHeightGridOffset,
                    tileBase + ElevatedMoatNativeContract.TileDefaultHeightGridOffset,
                    "aiv",
                    $"{unchecked((int)context.Pointer->R15)},{unchecked((int)context.Pointer->R14)}");
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("AIV completed-moat height", exception);
            }
        }

        private void ApplyExcavationCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RBX, unchecked((int)context.Pointer->RDI), "excavation");

        private void ApplyRebuildCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RBX, unchecked((int)context.Pointer->RSI), "rebuild");

        private void ApplyDirectCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RDI, unchecked((long)context.Pointer->RBX), "direct");

        private void ApplyGenericCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RBX, unchecked((long)context.Pointer->R14), "generic");

        private void RestoreDirectRemovalHeight(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                ulong managerAddress = context.Pointer->RDI;
                long tileId = unchecked((long)context.Pointer->RBX);
                if (!IsValidTileId(tileId))
                    throw new InvalidOperationException($"Invalid moat-removal tile ID {tileId}.");

                byte* current = (byte*)(managerAddress + (ulong)tileId +
                    ElevatedMoatNativeContract.TileHeightGridOffset);
                byte defaultHeight = *((byte*)(managerAddress + (ulong)tileId +
                    ElevatedMoatNativeContract.TileDefaultHeightGridOffset));
                byte vanillaHeight = *current;
                *current = defaultHeight;
                LogAdaptiveHeight("removal", tileId.ToString(), defaultHeight, vanillaHeight, defaultHeight);
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("direct moat-removal height", exception);
            }
        }

        private void ApplyCompletedHeightFromManager(
            NativePointer<X64SmartCPUContext> context,
            ulong managerAddress,
            long tileId,
            string source)
        {
            try
            {
                if (!IsValidTileId(tileId))
                    throw new InvalidOperationException($"Invalid {source} moat tile ID {tileId}.");

                ApplyCompletedHeight(
                    managerAddress + (ulong)tileId + ElevatedMoatNativeContract.TileHeightGridOffset,
                    managerAddress + (ulong)tileId + ElevatedMoatNativeContract.TileDefaultHeightGridOffset,
                    source,
                    tileId.ToString());
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce($"{source} completed-moat height", exception);
            }
        }

        private void ApplyCompletedHeight(
            ulong heightAddress,
            ulong defaultHeightAddress,
            string source,
            string tile)
        {
            byte* current = (byte*)heightAddress;
            byte vanillaHeight = *current;
            byte defaultHeight = *((byte*)defaultHeightAddress);
            byte completedHeight = ElevatedMoatNativeContract.CalculateCompletedHeight(defaultHeight);
            *current = completedHeight;
            LogAdaptiveHeight(source, tile, defaultHeight, vanillaHeight, completedHeight);
        }

        private void LogAdaptiveHeight(
            string source,
            string tile,
            byte defaultHeight,
            byte vanillaHeight,
            byte appliedHeight)
        {
            if (defaultHeight <= ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                return;

            int logNumber = Interlocked.Increment(ref adaptiveHeightLogCount);
            if (logNumber > MaximumAdaptiveHeightLogs)
                return;

            Shared.DebugLogHelper.LogWarning(
                log,
                $"MOAT_HEIGHT_APPLIED: source={source}, tile={tile}, defaultHeight={defaultHeight}, " +
                $"vanillaHeight={vanillaHeight}, appliedHeight={appliedHeight}, " +
                $"diagnosticLog={logNumber}/{MaximumAdaptiveHeightLogs}.");
        }

        private static bool IsValidTileId(long tileId) => tileId >= 0 && tileId < 64000;

        private static string GetNativeMoatMode(int mode)
        {
            switch (mode)
            {
                case 0: return "plan";
                case 1: return "remove-plan";
                case 2: return "direct-create";
                case 3: return "direct-remove";
                default: return $"unknown-{mode}";
            }
        }

        private static void ProbeExactHookLength(
            ulong imageBase,
            int hookRva,
            int expectedLength,
            string description)
        {
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)hookRva),
                expectedLength))
            {
                if (probe.DisplacedByteCount != expectedLength)
                {
                    throw new InvalidOperationException(
                        $"Unexpected RedBird {description} span before installation: " +
                        $"expected={expectedLength}, actual={probe.DisplacedByteCount}.");
                }
            }
        }

        private static void RequireInstalledHookLength(
            HookHandle<X64InlineHook> handle,
            int expectedLength,
            string description)
        {
            if (handle.Hook.DisplacedByteCount != expectedLength)
            {
                throw new InvalidOperationException(
                    $"Unexpected installed {description} overwrite length; rolling back.");
            }
        }

        private static void GenerateAivHeightBypass(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong createPathAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 7 ||
                overwrittenInstructions[1].Length != 6 ||
                overwrittenInstructions[2].Length != 9 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].MemoryBase != Register.RSI ||
                overwrittenInstructions[0].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.TileHeightGridOffset ||
                overwrittenInstructions[0].Immediate8 !=
                    ElevatedMoatNativeContract.MaximumVanillaTerrainHeight ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Jbe ||
                overwrittenInstructions[1].NearBranchTarget != createPathAddress ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Movzx ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException("The AIV moat height gate instruction contract differs.");
            }

            // The low-height branch is Vanilla's normal moat creation path. Jumping there
            // bypasses only the height alternative after all preceding tile checks passed.
            assembler.AddUnrestrictedJmp(createPathAddress);
        }

        private void ObserveAivMoatCreateAttempt(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                int height = *((byte*)(context.Pointer->RSI +
                    ElevatedMoatNativeContract.TileHeightGridOffset));
                if (height <= ElevatedMoatNativeContract.MaximumVanillaTerrainHeight)
                    return;

                int logNumber = Interlocked.Increment(ref aivAttemptLogCount);
                if (logNumber > MaximumDiagnosticLogs)
                    return;

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"AIV_MOAT_CREATE_ATTEMPT: playerId={unchecked((int)context.Pointer->R12)}, " +
                    $"tile={unchecked((int)context.Pointer->R15)},{unchecked((int)context.Pointer->R14)}, " +
                    $"height={height}, diagnosticLog={logNumber}/{MaximumDiagnosticLogs}.");
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("AIV moat creation", exception);
            }
        }

        private void ObserveHumanMoatWriterResult(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                MoatBuildTrace trace = currentMoatBuildTrace;
                if (trace == null)
                    return;

                long result = unchecked((long)context.Pointer->RAX);
                trace.NativeWriterCallCount++;
                if (trace.NativeWriterCallCount == 1)
                    trace.FirstNativeSlotResult = result;
                trace.LastNativeSlotResult = result;
            }
            catch (Exception exception)
            {
                LogCallbackFailureOnce("human moat writer result", exception);
            }
        }

        private void ObserveBuildStructure(BuildStructureEventArgs args)
        {
            if (args == null || args.Mappers != eMappers.MAPPER_MOAT)
                return;

            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    currentMoatBuildTrace = new MoatBuildTrace
                    {
                        Sequence = Interlocked.Increment(ref buildSequence),
                        PlayerId = args.PlayerId,
                        TileX = args.TileX,
                        TileY = args.TileY,
                        Height = TryGetTileHeight(args.TileX, args.TileY)
                    };
                    return;
                }

                if (args.Phase != EventHookPhase.Post)
                    return;

                MoatBuildTrace trace = currentMoatBuildTrace ?? new MoatBuildTrace
                {
                    Sequence = Interlocked.Increment(ref buildSequence),
                    PlayerId = args.PlayerId,
                    TileX = args.TileX,
                    TileY = args.TileY,
                    Height = TryGetTileHeight(args.TileX, args.TileY)
                };
                currentMoatBuildTrace = null;

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"MOAT_BUILD: sequence={trace.Sequence}, playerId={trace.PlayerId}, " +
                    $"playerKind={GetPlayerKind(trace.PlayerId)}, tile={trace.TileX},{trace.TileY}, " +
                    $"height={trace.Height}, builderReached=true, nativeWriterCalls={trace.NativeWriterCallCount}, " +
                    $"firstSlotResult={trace.FirstNativeSlotResult}, lastSlotResult={trace.LastNativeSlotResult}.");
            }
            catch (Exception exception)
            {
                currentMoatBuildTrace = null;
                if (Interlocked.Exchange(ref eventFailureLogged, 1) == 0)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Elevated Moat Test build-event diagnostics failed: {exception}");
                }
            }
        }

        private static int TryGetTileHeight(int tileX, int tileY)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                return tiles.GetTileHeight(tiles.GetTileId(tileX, tileY));
            }
            catch
            {
                return -1;
            }
        }

        private static string GetPlayerKind(int playerId)
        {
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                return players.IsPlayerIdValid(playerId)
                    ? (players.IsAIPlayer(playerId) ? "ai" : "human")
                    : "invalid";
            }
            catch
            {
                return "unknown";
            }
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

            string playerKind = GetPlayerKind(trace.PlayerId);
            string heightClass = maximumHeight < 0
                ? "unknown"
                : minimumHeight <= ElevatedMoatNativeContract.MaximumVanillaTerrainHeight &&
                    maximumHeight > ElevatedMoatNativeContract.MaximumVanillaTerrainHeight
                    ? "mixed"
                    : maximumHeight > ElevatedMoatNativeContract.MaximumVanillaTerrainHeight
                        ? "elevated"
                        : "low";

            string fingerprint =
                $"{playerKind}|{heightClass}|{trace.HeightSuppressed}|{status}|{reason}|" +
                $"{trace.TileValidationCount}|{trace.TileRejectionCount}|" +
                $"{trace.FirstTileRejectionResult}|{trace.LastTileRejectionResult}";
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

        private sealed class MoatBuildTrace
        {
            internal long Sequence;
            internal int PlayerId;
            internal int TileX;
            internal int TileY;
            internal int Height = -1;
            internal int NativeWriterCallCount;
            internal long FirstNativeSlotResult = -1;
            internal long LastNativeSlotResult = -1;
        }
    }
}
