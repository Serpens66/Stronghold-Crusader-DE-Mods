using BepInEx.Logging;
using CrusaderDE;
using Iced.Intel;
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
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal sealed unsafe class ElevatedMoatPatch
    {
        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> drawbridgeHeightFailureWriterHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivHeightGateHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivAudienceCaptureHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> moatCommandHeightGateHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> sharedHeightGateHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> aivCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> excavationCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> loweredDrawbridgeHeightWriteHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> directCompletedHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> completedDrawbridgeHeightWriteHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> drawbridgeSpecialRendererHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> drawbridgeAnimatedRendererArgumentsHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> unitDrawbridgeHeightCorrectionHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> plannedMoatCancellationHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> directRemovalHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> footprintRemovalHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> objectRemovalHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> moatWorkCompletionHeightHook =
            new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> areaRemovalHeightHook =
            new HookHandle<X64InlineHook>();
        private long successfulCorrections;
        private long completedHeightCorrections;
        private long restoredHeightCorrections;
        private int callbackFailureReported;
        private IntPtr aivAudienceAllowedFlag;
        private IntPtr featureActiveFlag;
        private IntPtr humanPlacementAllowedFlag;
        private int allowAIPlacement;
        private int allowHumanPlacement;
        private int featureActive;

        internal ElevatedMoatPatch(
            ManualLogSource log,
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches,
            bool allowAIPlacement,
            bool allowHumanPlacement)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!allowAIPlacement && !allowHumanPlacement)
                throw new ArgumentException("At least one elevated-moat placement audience must be enabled.");
            if (context == null || context.ModuleHandle == IntPtr.Zero || context.Memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.", nameof(context));
            if (!referenceHashMatches)
                throw new InvalidOperationException("Extra Features elevated-moat requires the audited CrusaderDE.dll hash.");
            if (!string.Equals(
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    ElevatedMoatNativeContract.ReferenceSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The shared native hash no longer matches the Extra Features elevated-moat audit contract.");
            }

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterPattern,
                ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterRva,
                true,
                "elevated-drawbridge height failure writer",
                log: null);
            if (resolution.Rva != ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterRva)
                throw new InvalidOperationException("The elevated-drawbridge writer resolved outside its audited RVA.");

            Shared.NativeResolution aivGateResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.AivHeightGatePattern,
                ElevatedMoatNativeContract.AivHeightGateRva,
                true,
                "AIV moat height gate",
                log: null);
            Shared.NativeResolution aivAudienceResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.AivAudienceCapturePattern,
                ElevatedMoatNativeContract.AivAudienceCaptureRva,
                true,
                "AIV moat player-audience capture",
                log: null);
            Shared.NativeResolution aivCreateResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                ElevatedMoatNativeContract.AivCreatePathPattern,
                ElevatedMoatNativeContract.AivCreatePathRva,
                true,
                "AIV moat creation path",
                log: null);
            Shared.NativeResolution moatCommandGateResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.MoatCommandHeightGatePattern,
                ElevatedMoatNativeContract.MoatCommandHeightGateRva,
                "MAPPER_MOAT/MAPPER_ANTIMOAT command height gate", log);
            Shared.NativeResolution sharedGateResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.SharedHeightGatePattern,
                ElevatedMoatNativeContract.SharedHeightGateRva, "shared moat height gate", log);
            Shared.NativeResolution aivHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.AivCompletedHeightPattern,
                ElevatedMoatNativeContract.AivCompletedHeightRva, "AIV completed-moat height", log);
            Shared.NativeResolution excavationHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.ExcavationCompletedHeightPattern,
                ElevatedMoatNativeContract.ExcavationCompletedHeightRva, "excavated-moat height", log);
            Shared.NativeResolution loweredDrawbridgeHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.LowerDrawbridgeHookPattern,
                ElevatedMoatNativeContract.LowerDrawbridgeHookRva,
                "lowered-drawbridge hook block", log);
            Shared.NativeResolution directHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DirectCompletedHeightPattern,
                ElevatedMoatNativeContract.DirectCompletedHeightRva, "direct completed-moat height", log);
            Shared.NativeResolution completedDrawbridgeHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.CompletedDrawbridgeHookPattern,
                ElevatedMoatNativeContract.CompletedDrawbridgeHookRva,
                "completed-drawbridge hook block", log);
            Shared.NativeResolution drawbridgeRendererResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookBytes,
                ElevatedMoatNativeContract.DrawbridgeSpecialRendererRva,
                "drawbridge special-renderer prologue", log);
            Shared.NativeResolution drawbridgeAnimatedRendererResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsBytes,
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsRva,
                "drawbridge animated-renderer arguments", log);
            Shared.NativeResolution unitDrawbridgeHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionBytes,
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionRva,
                "unit drawbridge vertical-correction block", log);
            Shared.NativeResolution plannedCancellationResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.PlannedMoatCancellationPattern,
                ElevatedMoatNativeContract.PlannedMoatCancellationRva,
                "planned moat cancellation", log);
            Shared.NativeResolution removalHeightResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.DirectRemovalHeightPattern,
                ElevatedMoatNativeContract.DirectRemovalHeightRva, "direct moat-removal height", log);
            Shared.NativeResolution footprintRemovalResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.FootprintRemovalHeightPattern,
                ElevatedMoatNativeContract.FootprintRemovalHeightRva,
                "footprint moat-removal height", log);
            Shared.NativeResolution objectRemovalResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.ObjectRemovalHeightPattern,
                ElevatedMoatNativeContract.ObjectRemovalHeightRva,
                "object moat-removal height", log);
            Shared.NativeResolution workCompletionResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.MoatWorkCompletionHeightPattern,
                ElevatedMoatNativeContract.MoatWorkCompletionHeightRva,
                "moat work-completion height", log);
            Shared.NativeResolution areaRemovalResolution = ResolveAudited(
                memory, ElevatedMoatNativeContract.AreaRemovalHeightPattern,
                ElevatedMoatNativeContract.AreaRemovalHeightRva,
                "area moat-removal height", log);
            if (aivGateResolution.Rva != ElevatedMoatNativeContract.AivHeightGateRva ||
                aivCreateResolution.Rva != ElevatedMoatNativeContract.AivCreatePathRva)
            {
                throw new InvalidOperationException("A new elevated-moat hook resolved outside its audited RVA.");
            }

            ElevatedMoatNativeContract.ValidateDrawbridgeHeightFailure(memory, resolution.Rva);
            ElevatedMoatNativeContract.ValidateAivHooks(
                memory,
                aivAudienceResolution.Rva,
                aivGateResolution.Rva,
                aivCreateResolution.Rva);
            ElevatedMoatNativeContract.ValidateAdaptiveHeightHooks(memory);
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)resolution.Rva),
                ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength))
            {
                if (probe.DisplacedByteCount != ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength)
                    throw new InvalidOperationException("Unexpected RedBird elevated-drawbridge writer span before installation.");
            }
            ProbeExactHookLength(
                imageBase,
                aivAudienceResolution.Rva,
                ElevatedMoatNativeContract.AivAudienceCaptureLength,
                "AIV moat player-audience capture");
            ProbeExactHookLength(
                imageBase,
                aivGateResolution.Rva,
                ElevatedMoatNativeContract.AivHeightGateLength,
                "AIV moat height gate");
            ProbeExactHookLength(imageBase, moatCommandGateResolution.Rva,
                ElevatedMoatNativeContract.MoatCommandHeightGateLength,
                "MAPPER_MOAT/MAPPER_ANTIMOAT command height gate");
            ProbeExactHookLength(imageBase, sharedGateResolution.Rva,
                ElevatedMoatNativeContract.SharedHeightGateLength, "shared moat height gate");
            ProbeExactHookLength(imageBase, aivHeightResolution.Rva,
                ElevatedMoatNativeContract.AivCompletedHeightLength, "AIV completed-moat height");
            ProbeExactHookLength(imageBase, excavationHeightResolution.Rva,
                ElevatedMoatNativeContract.ExcavationCompletedHeightLength, "excavated-moat height");
            ProbeExactHookLength(imageBase, loweredDrawbridgeHeightResolution.Rva,
                ElevatedMoatNativeContract.LowerDrawbridgeHookLength,
                "lowered-drawbridge hook block");
            ProbeExactHookLength(imageBase, directHeightResolution.Rva,
                ElevatedMoatNativeContract.DirectCompletedHeightLength, "direct completed-moat height");
            ProbeExactHookLength(imageBase, completedDrawbridgeHeightResolution.Rva,
                ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                "completed-drawbridge hook block");
            ProbeExactHookLength(imageBase, drawbridgeRendererResolution.Rva,
                ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength,
                "drawbridge special-renderer prologue");
            ProbeExactHookLength(imageBase, drawbridgeAnimatedRendererResolution.Rva,
                ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength,
                "drawbridge animated-renderer arguments");
            ProbeExactHookLength(imageBase, unitDrawbridgeHeightResolution.Rva,
                ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength,
                "unit drawbridge vertical-correction block");
            ProbeExactHookLength(imageBase, plannedCancellationResolution.Rva,
                ElevatedMoatNativeContract.PlannedMoatCancellationLength,
                "planned moat cancellation");
            ProbeExactHookLength(imageBase, removalHeightResolution.Rva,
                ElevatedMoatNativeContract.DirectRemovalHeightLength, "direct moat-removal height");
            ProbeExactHookLength(imageBase, footprintRemovalResolution.Rva,
                ElevatedMoatNativeContract.FootprintRemovalHeightLength,
                "footprint moat-removal height");
            ProbeExactHookLength(imageBase, objectRemovalResolution.Rva,
                ElevatedMoatNativeContract.ObjectRemovalHeightLength,
                "object moat-removal height");
            ProbeExactHookLength(imageBase, workCompletionResolution.Rva,
                ElevatedMoatNativeContract.MoatWorkCompletionHeightLength,
                "moat work-completion height");
            ProbeExactHookLength(imageBase, areaRemovalResolution.Rva,
                ElevatedMoatNativeContract.AreaRemovalHeightLength,
                "area moat-removal height");

            HookTransaction pending = null;
            try
            {
                aivAudienceAllowedFlag = Marshal.AllocHGlobal(1);
                featureActiveFlag = Marshal.AllocHGlobal(1);
                humanPlacementAllowedFlag = Marshal.AllocHGlobal(1);
                WriteNativeFlag(aivAudienceAllowedFlag, false);
                WriteNativeFlag(featureActiveFlag, false);
                WriteNativeFlag(humanPlacementAllowedFlag, false);
                UpdateSettings(allowAIPlacement, allowHumanPlacement);
                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddContextHook(
                    drawbridgeHeightFailureWriterHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)resolution.Rva)),
                    SuppressDrawbridgeHeightFailureWriter,
                    new ContextHookOptions
                    {
                        // The managed callback must preserve every live GPR around this validator path.
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.Suppress
                    });
                pending.AddContextHook(
                    aivAudienceCaptureHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)aivAudienceResolution.Rva)),
                    CaptureAivPlacementAudience,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = ElevatedMoatNativeContract.AivAudienceCaptureLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.AfterCallback
                    });
                pending.AddInline(
                    aivHeightGateHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)aivGateResolution.Rva)),
                    (assembler, instructions, returnAddress) => GenerateAivHeightBypass(
                        assembler,
                        instructions,
                        returnAddress,
                        imageBase + ElevatedMoatNativeContract.AivCreatePathRva,
                        unchecked((ulong)aivAudienceAllowedFlag.ToInt64())),
                    hookSize: ElevatedMoatNativeContract.AivHeightGateLength);
                pending.AddInline(
                    moatCommandHeightGateHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)moatCommandGateResolution.Rva)),
                    (assembler, instructions, returnAddress) => GenerateMoatCommandHeightBypass(
                        assembler,
                        instructions,
                        returnAddress,
                        unchecked((ulong)featureActiveFlag.ToInt64()),
                        unchecked((ulong)humanPlacementAllowedFlag.ToInt64())),
                    hookSize: ElevatedMoatNativeContract.MoatCommandHeightGateLength);
                pending.AddInline(
                    sharedHeightGateHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)sharedGateResolution.Rva)),
                    (assembler, instructions, returnAddress) => GenerateSharedHeightBypass(
                        assembler,
                        instructions,
                        returnAddress,
                        unchecked((ulong)featureActiveFlag.ToInt64()),
                        unchecked((ulong)humanPlacementAllowedFlag.ToInt64())),
                    hookSize: ElevatedMoatNativeContract.SharedHeightGateLength);
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
                pending.AddInline(
                    loweredDrawbridgeHeightWriteHook,
                    HookTarget.FromAddress(
                        imageBase + unchecked((ulong)loweredDrawbridgeHeightResolution.Rva)),
                    (assembler, instructions, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateLowered(
                        assembler,
                        instructions,
                        returnAddress,
                        unchecked((ulong)featureActiveFlag.ToInt64()),
                        imageBase),
                    hookSize: ElevatedMoatNativeContract.LowerDrawbridgeHookLength);
                pending.AddContextHook(
                    directCompletedHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)directHeightResolution.Rva)),
                    ApplyDirectCompletedHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.DirectCompletedHeightLength));
                pending.AddInline(
                    completedDrawbridgeHeightWriteHook,
                    HookTarget.FromAddress(
                        imageBase + unchecked((ulong)completedDrawbridgeHeightResolution.Rva)),
                    (assembler, instructions, returnAddress) => ElevatedMoatDrawbridgeHooks.GenerateCompleted(
                        assembler,
                        instructions,
                        returnAddress,
                        unchecked((ulong)featureActiveFlag.ToInt64()),
                        imageBase + ElevatedMoatNativeContract.DrawbridgeStateUpdateRva),
                    hookSize: ElevatedMoatNativeContract.CompletedDrawbridgeHookLength);
                pending.AddInline(
                    drawbridgeSpecialRendererHook,
                    HookTarget.FromAddress(
                        imageBase + unchecked((ulong)drawbridgeRendererResolution.Rva)),
                    (assembler, instructions, returnAddress) =>
                        ElevatedMoatDrawbridgeHooks.GenerateSpecialRenderer(
                            assembler,
                            instructions,
                            returnAddress,
                            unchecked((ulong)featureActiveFlag.ToInt64()),
                            imageBase + ElevatedMoatNativeContract.TileHeightGridRva,
                            imageBase + ElevatedMoatNativeContract.CurrentRenderedTileHeightRva),
                    hookSize: ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength);
                pending.AddInline(
                    drawbridgeAnimatedRendererArgumentsHook,
                    HookTarget.FromAddress(
                        imageBase + unchecked((ulong)drawbridgeAnimatedRendererResolution.Rva)),
                    (assembler, instructions, returnAddress) =>
                        ElevatedMoatDrawbridgeHooks.GenerateAnimatedRendererArguments(
                            assembler,
                            instructions,
                            returnAddress,
                            unchecked((ulong)featureActiveFlag.ToInt64()),
                            imageBase + ElevatedMoatNativeContract.TileHeightGridRva,
                            imageBase + ElevatedMoatNativeContract.CurrentRenderedTileHeightRva),
                    hookSize: ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength);
                pending.AddInline(
                    unitDrawbridgeHeightCorrectionHook,
                    HookTarget.FromAddress(
                        imageBase + unchecked((ulong)unitDrawbridgeHeightResolution.Rva)),
                    (assembler, instructions, returnAddress) =>
                        ElevatedMoatDrawbridgeHooks.GenerateUnitHeightCorrection(
                            assembler,
                            instructions,
                            returnAddress,
                            unchecked((ulong)featureActiveFlag.ToInt64())),
                    hookSize: ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength);
                pending.AddContextHook(
                    plannedMoatCancellationHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)plannedCancellationResolution.Rva)),
                    RestorePlannedMoatCancellationHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.PlannedMoatCancellationLength));
                pending.AddContextHook(
                    directRemovalHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)removalHeightResolution.Rva)),
                    RestoreDirectRemovalHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.DirectRemovalHeightLength));
                pending.AddContextHook(
                    footprintRemovalHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)footprintRemovalResolution.Rva)),
                    RestoreFootprintRemovalHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.FootprintRemovalHeightLength));
                pending.AddContextHook(
                    objectRemovalHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)objectRemovalResolution.Rva)),
                    RestoreObjectRemovalHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.ObjectRemovalHeightLength));
                pending.AddContextHook(
                    moatWorkCompletionHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)workCompletionResolution.Rva)),
                    RestoreMoatWorkCompletionHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.MoatWorkCompletionHeightLength));
                pending.AddContextHook(
                    areaRemovalHeightHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)areaRemovalResolution.Rva)),
                    RestoreAreaRemovalHeight,
                    CorrectingContextOptions(ElevatedMoatNativeContract.AreaRemovalHeightLength));

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess ||
                    !drawbridgeHeightFailureWriterHook.Success ||
                    !aivAudienceCaptureHook.Success ||
                    !aivHeightGateHook.Success ||
                    !moatCommandHeightGateHook.Success ||
                    !sharedHeightGateHook.Success ||
                    !aivCompletedHeightHook.Success ||
                    !excavationCompletedHeightHook.Success ||
                    !loweredDrawbridgeHeightWriteHook.Success ||
                    !directCompletedHeightHook.Success ||
                    !completedDrawbridgeHeightWriteHook.Success ||
                    !drawbridgeSpecialRendererHook.Success ||
                    !drawbridgeAnimatedRendererArgumentsHook.Success ||
                    !unitDrawbridgeHeightCorrectionHook.Success ||
                    !plannedMoatCancellationHook.Success ||
                    !directRemovalHeightHook.Success ||
                    !footprintRemovalHeightHook.Success ||
                    !objectRemovalHeightHook.Success ||
                    !moatWorkCompletionHeightHook.Success ||
                    !areaRemovalHeightHook.Success)
                    throw new InvalidOperationException($"The elevated-moat hook transaction was incomplete: {result}.");
                if (drawbridgeHeightFailureWriterHook.Hook.DisplacedByteCount !=
                    ElevatedMoatNativeContract.DrawbridgeHeightFailureWriterLength)
                {
                    throw new InvalidOperationException(
                        "Unexpected installed elevated-drawbridge overwrite length; rolling back.");
                }
                RequireInstalledHookLength(
                    aivAudienceCaptureHook,
                    ElevatedMoatNativeContract.AivAudienceCaptureLength,
                    "AIV moat player-audience capture");
                RequireInstalledHookLength(
                    aivHeightGateHook,
                    ElevatedMoatNativeContract.AivHeightGateLength,
                    "AIV moat height gate");
                RequireInstalledHookLength(moatCommandHeightGateHook,
                    ElevatedMoatNativeContract.MoatCommandHeightGateLength,
                    "MAPPER_MOAT/MAPPER_ANTIMOAT command height gate");
                RequireInstalledHookLength(sharedHeightGateHook,
                    ElevatedMoatNativeContract.SharedHeightGateLength, "shared moat height gate");
                RequireInstalledHookLength(aivCompletedHeightHook,
                    ElevatedMoatNativeContract.AivCompletedHeightLength, "AIV completed-moat height");
                RequireInstalledHookLength(excavationCompletedHeightHook,
                    ElevatedMoatNativeContract.ExcavationCompletedHeightLength, "excavated-moat height");
                RequireInstalledHookLength(loweredDrawbridgeHeightWriteHook,
                    ElevatedMoatNativeContract.LowerDrawbridgeHookLength,
                    "lowered-drawbridge hook block");
                RequireInstalledHookLength(directCompletedHeightHook,
                    ElevatedMoatNativeContract.DirectCompletedHeightLength, "direct completed-moat height");
                RequireInstalledHookLength(completedDrawbridgeHeightWriteHook,
                    ElevatedMoatNativeContract.CompletedDrawbridgeHookLength,
                    "completed-drawbridge hook block");
                RequireInstalledHookLength(drawbridgeSpecialRendererHook,
                    ElevatedMoatNativeContract.DrawbridgeSpecialRendererHookLength,
                    "drawbridge special-renderer prologue");
                RequireInstalledHookLength(drawbridgeAnimatedRendererArgumentsHook,
                    ElevatedMoatNativeContract.DrawbridgeAnimatedRendererArgumentsLength,
                    "drawbridge animated-renderer arguments");
                RequireInstalledHookLength(unitDrawbridgeHeightCorrectionHook,
                    ElevatedMoatNativeContract.UnitDrawbridgeHeightCorrectionLength,
                    "unit drawbridge vertical-correction block");
                RequireInstalledHookLength(plannedMoatCancellationHook,
                    ElevatedMoatNativeContract.PlannedMoatCancellationLength,
                    "planned moat cancellation");
                RequireInstalledHookLength(directRemovalHeightHook,
                    ElevatedMoatNativeContract.DirectRemovalHeightLength, "direct moat-removal height");
                RequireInstalledHookLength(footprintRemovalHeightHook,
                    ElevatedMoatNativeContract.FootprintRemovalHeightLength,
                    "footprint moat-removal height");
                RequireInstalledHookLength(objectRemovalHeightHook,
                    ElevatedMoatNativeContract.ObjectRemovalHeightLength,
                    "object moat-removal height");
                RequireInstalledHookLength(moatWorkCompletionHeightHook,
                    ElevatedMoatNativeContract.MoatWorkCompletionHeightLength,
                    "moat work-completion height");
                RequireInstalledHookLength(areaRemovalHeightHook,
                    ElevatedMoatNativeContract.AreaRemovalHeightLength,
                    "area moat-removal height");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Extra Features elevated-moat active; AI={allowAIPlacement}, human={allowHumanPlacement}, editor=true, " +
                    $"adaptiveDepth={ElevatedMoatNativeContract.MoatDepth}, " +
                    $"healthReportInterval={ElevatedMoatHealthReporting.ReportInterval} corrections.");

                transaction = pending;
                pending = null;
            }
            catch
            {
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
                FreeUnpublishedFlags();
                throw;
            }
        }

        internal void UpdateSettings(bool allowAIPlacement, bool allowHumanPlacement)
        {
            bool active = allowAIPlacement || allowHumanPlacement;
            WriteNativeFlag(featureActiveFlag, false);
            Volatile.Write(ref featureActive, 0);
            WriteNativeFlag(aivAudienceAllowedFlag, false);

            Volatile.Write(ref this.allowAIPlacement, allowAIPlacement ? 1 : 0);
            Volatile.Write(ref this.allowHumanPlacement, allowHumanPlacement ? 1 : 0);
            WriteNativeFlag(humanPlacementAllowedFlag, allowHumanPlacement);

            if (active)
            {
                Volatile.Write(ref featureActive, 1);
                WriteNativeFlag(featureActiveFlag, true);
            }
        }

        private void FreeUnpublishedFlags()
        {
            FreeFlag(ref aivAudienceAllowedFlag);
            FreeFlag(ref featureActiveFlag);
            FreeFlag(ref humanPlacementAllowedFlag);
        }

        private static void FreeFlag(ref IntPtr flag)
        {
            if (flag == IntPtr.Zero)
                return;
            Marshal.FreeHGlobal(flag);
            flag = IntPtr.Zero;
        }

        private static void WriteNativeFlag(IntPtr flag, bool value)
        {
            if (flag != IntPtr.Zero)
                Volatile.Write(ref *((byte*)flag), value ? (byte)1 : (byte)0);
        }

        private static Shared.NativeResolution ResolveAudited(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string description,
            ManualLogSource log)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, expectedRva, true, description, log: null);
            if (resolution.Rva != expectedRva)
                throw new InvalidOperationException($"The {description} resolved outside its audited RVA.");
            return resolution;
        }

        private static Shared.NativeResolution ResolveAudited(
            ReadOnlySpan<byte> memory,
            byte[] pattern,
            int expectedRva,
            string description,
            ManualLogSource log)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                expectedRva,
                true,
                description,
                log: null,
                Shared.NativePatternSearchScope.ExecutableSections);
            if (resolution.Rva != expectedRva)
                throw new InvalidOperationException($"The {description} resolved outside its audited RVA.");
            return resolution;
        }

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

        private void ApplyAivCompletedHeight(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!IsActive)
                    return;
                ulong tileBase = context.Pointer->RSI;
                ApplyCompletedHeight(
                    tileBase + ElevatedMoatNativeContract.TileHeightGridOffset,
                    tileBase + ElevatedMoatNativeContract.TileDefaultHeightGridOffset,
                    "aiv",
                    $"{unchecked((int)context.Pointer->R15)},{unchecked((int)context.Pointer->R14)}");
            }
            catch (Exception exception)
            {
                LogCallbackFailure("AIV completed-moat height", exception);
            }
        }

        private void ApplyExcavationCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RBX, unchecked((int)context.Pointer->RDI), "excavation");

        private void ApplyDirectCompletedHeight(NativePointer<X64SmartCPUContext> context) =>
            ApplyCompletedHeightFromManager(
                context, context.Pointer->RDI, unchecked((long)context.Pointer->RBX), "direct");

        private void RestorePlannedMoatCancellationHeight(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!IsActive)
                    return;
                ulong managerAddress = context.Pointer->RDI;
                long tileId = unchecked((long)context.Pointer->RBX);
                if (!IsValidTileId(tileId))
                    throw new InvalidOperationException($"Invalid planned moat-cancellation tile ID {tileId}.");

                RestoreOriginalHeight(managerAddress, tileId, "planned-cancellation");
            }
            catch (Exception exception)
            {
                LogCallbackFailure("planned moat-cancellation height", exception);
            }
        }

        private void RestoreDirectRemovalHeight(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                if (!IsActive)
                    return;
                ulong managerAddress = context.Pointer->RDI;
                long tileId = unchecked((long)context.Pointer->RBX);
                if (!IsValidTileId(tileId))
                    throw new InvalidOperationException($"Invalid moat-removal tile ID {tileId}.");

                RestoreOriginalHeight(managerAddress, tileId, "direct-removal");
            }
            catch (Exception exception)
            {
                LogCallbackFailure("direct moat-removal height", exception);
            }
        }

        private void RestoreFootprintRemovalHeight(NativePointer<X64SmartCPUContext> context) =>
            RestoreRemovalHeightFromManager(
                context.Pointer->RBX, unchecked((long)context.Pointer->RSI), "footprint-removal");

        private void RestoreObjectRemovalHeight(NativePointer<X64SmartCPUContext> context) =>
            RestoreRemovalHeightFromManager(
                context.Pointer->RBX, unchecked((long)context.Pointer->RSI), "object-removal");

        private void RestoreMoatWorkCompletionHeight(NativePointer<X64SmartCPUContext> context) =>
            RestoreRemovalHeightFromManager(
                context.Pointer->RSI, unchecked((long)context.Pointer->RAX), "work-completion");

        private void RestoreAreaRemovalHeight(NativePointer<X64SmartCPUContext> context) =>
            RestoreRemovalHeightFromManager(
                context.Pointer->RCX, unchecked((long)context.Pointer->R14), "area-removal");

        private void RestoreRemovalHeightFromManager(ulong managerAddress, long tileId, string source)
        {
            try
            {
                if (!IsActive)
                    return;
                if (!IsValidTileId(tileId))
                    throw new InvalidOperationException($"Invalid {source} moat tile ID {tileId}.");

                RestoreOriginalHeight(managerAddress, tileId, source);
            }
            catch (Exception exception)
            {
                LogCallbackFailure($"{source} moat height", exception);
            }
        }

        private void RestoreOriginalHeight(ulong managerAddress, long tileId, string source)
        {
            byte* current = (byte*)(managerAddress + (ulong)tileId +
                ElevatedMoatNativeContract.TileHeightGridOffset);
            byte defaultHeight = *((byte*)(managerAddress + (ulong)tileId +
                ElevatedMoatNativeContract.TileDefaultHeightGridOffset));
            *current = ElevatedMoatNativeContract.CalculateRestoredHeight(defaultHeight);
            if (*current != defaultHeight)
                throw new InvalidOperationException($"{source} did not restore tile {tileId} to its default height.");

            RecordSuccessfulCorrection(restored: true);
        }

        private void ApplyCompletedHeightFromManager(
            NativePointer<X64SmartCPUContext> context,
            ulong managerAddress,
            long tileId,
            string source)
        {
            try
            {
                if (!IsActive)
                    return;
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
                LogCallbackFailure($"{source} completed-moat height", exception);
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
            if (*current != completedHeight)
            {
                throw new InvalidOperationException(
                    $"{source} did not apply the calculated height to tile {tile}; " +
                    $"defaultHeight={defaultHeight}, vanillaHeight={vanillaHeight}.");
            }

            RecordSuccessfulCorrection(restored: false);
        }

        private bool IsActive => Volatile.Read(ref featureActive) != 0;

        private void RecordSuccessfulCorrection(bool restored)
        {
            if (restored)
                Interlocked.Increment(ref restoredHeightCorrections);
            else
                Interlocked.Increment(ref completedHeightCorrections);

            long total = Interlocked.Increment(ref successfulCorrections);
            if (!ElevatedMoatHealthReporting.ShouldReport(total))
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Extra Features elevated-moat healthy: corrections={total}, " +
                $"completed={Interlocked.Read(ref completedHeightCorrections)}, " +
                $"restored={Interlocked.Read(ref restoredHeightCorrections)}, errors=0.");
        }

        private static bool IsValidTileId(long tileId) =>
            tileId >= 0 && tileId < GameTileManagerAPI.MAX_WIDTH * GameTileManagerAPI.MAX_HEIGHT;

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

        private static void GenerateMoatCommandHeightBypass(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong humanPlacementAllowedFlagAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[1].Length != 6 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].MemoryBase != Register.RBX ||
                overwrittenInstructions[0].MemoryIndex != Register.RDI ||
                overwrittenInstructions[0].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.TileHeightGridOffset ||
                overwrittenInstructions[0].Immediate8 !=
                    ElevatedMoatNativeContract.MaximumVanillaTerrainHeight ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Ja ||
                returnAddress != overwrittenInstructions[1].NextIP)
            {
                throw new InvalidOperationException("The moat-command height-gate instruction contract differs.");
            }

            Label vanillaHeightGate = assembler.CreateLabel("moatCommandVanillaHeightGate");
            Label bypassHeightGate = assembler.CreateLabel("moatCommandBypassHeightGate");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaHeightGate);
            assembler.cmp(r14d, (int)eMappers.MAPPER_ANTIMOAT);
            assembler.je(bypassHeightGate);
            assembler.cmp(r14d, (int)eMappers.MAPPER_MOAT);
            assembler.jne(vanillaHeightGate);
            EmitEnabledFlagBranch(assembler, humanPlacementAllowedFlagAddress, vanillaHeightGate);
            assembler.jmp(bypassHeightGate);

            // Preserve Vanilla for every unexpected caller. Both continuations recalculate
            // flags immediately, and this block does not alter any live GPR or XMM state.
            assembler.Label(ref vanillaHeightGate);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref bypassHeightGate);
            assembler.AddUnrestrictedJmp(returnAddress);
        }

        private static void GenerateSharedHeightBypass(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong humanPlacementAllowedFlagAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[1].Length != 6 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[0].MemoryBase != Register.RBX ||
                overwrittenInstructions[0].MemoryIndex != Register.RDI ||
                overwrittenInstructions[0].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.TileHeightGridOffset ||
                overwrittenInstructions[0].Immediate8 !=
                    ElevatedMoatNativeContract.MaximumVanillaTerrainHeight ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Ja ||
                returnAddress != overwrittenInstructions[1].NextIP)
            {
                throw new InvalidOperationException("The shared moat height-gate instruction contract differs.");
            }

            Label vanillaHeightGate = assembler.CreateLabel("sharedMoatVanillaHeightGate");
            Label bypassHeightGate = assembler.CreateLabel("sharedMoatBypassHeightGate");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaHeightGate);
            // Native modes 1 and 3 cancel/remove moats. Mode 2 is direct editor placement.
            // Mode 0 creates a planned moat and therefore follows the human checkbox.
            assembler.cmp(r15d, (int)ElevatedMoatNativeMode.CancelPlan);
            assembler.je(bypassHeightGate);
            assembler.cmp(r15d, (int)ElevatedMoatNativeMode.DirectCreate);
            assembler.je(bypassHeightGate);
            assembler.cmp(r15d, (int)ElevatedMoatNativeMode.DirectRemove);
            assembler.je(bypassHeightGate);
            assembler.cmp(r15d, (int)ElevatedMoatNativeMode.Plan);
            assembler.jne(vanillaHeightGate);
            EmitEnabledFlagBranch(assembler, humanPlacementAllowedFlagAddress, vanillaHeightGate);
            assembler.jmp(bypassHeightGate);

            assembler.Label(ref vanillaHeightGate);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref bypassHeightGate);
            assembler.AddUnrestrictedJmp(returnAddress);
        }

        private static void EmitEnabledFlagBranch(
            Assembler assembler,
            ulong flagAddress,
            Label disabledTarget)
        {
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.pop(rax);
            assembler.jne(disabledTarget);
        }

        private static void GenerateAivHeightBypass(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong createPathAddress,
            ulong audienceAllowedFlagAddress)
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

            Label vanillaHeightGate = assembler.CreateLabel("aivVanillaHeightGate");
            // Preserve RAX and the stack exactly while reading the result captured at this
            // function's entry for the actual player ID (including CastlePlanner humans).
            assembler.push(rax);
            assembler.mov(rax, audienceAllowedFlagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.pop(rax);
            assembler.jne(vanillaHeightGate);

            // The low-height branch is Vanilla's normal moat creation path. Jumping there
            // bypasses only the height alternative after all preceding tile checks passed.
            assembler.AddUnrestrictedJmp(createPathAddress);

            assembler.Label(ref vanillaHeightGate);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
            assembler.AddUnrestrictedJmp(returnAddress);
        }

        private void CaptureAivPlacementAudience(NativePointer<X64SmartCPUContext> context)
        {
            // At FUN_180059730 entry, RDX is param_2: the actual 1-based player ID.
            bool allowed = false;
            try
            {
                int playerId = unchecked((int)context.Pointer->RDX);
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (players.IsPlayerIdValid(playerId))
                    allowed = players.IsAIPlayer(playerId)
                        ? Volatile.Read(ref allowAIPlacement) != 0
                        : Volatile.Read(ref allowHumanPlacement) != 0;
            }
            catch
            {
                // Fail closed: the inline gate below keeps Vanilla's height check.
            }

            WriteNativeFlag(aivAudienceAllowedFlag, allowed && IsActive);
        }

        private void SuppressDrawbridgeHeightFailureWriter(NativePointer<X64SmartCPUContext> context)
        {
            // RBP is the audited player ID in this validator. Invalid IDs belong to the editor
            // or another direct context and remain enabled whenever this patch is installed.
            int playerId = unchecked((int)context.Pointer->RBP);
            bool allowed;
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                bool editor = GameData.Instance?.mapType == Enums.GameModes.MAP_EDITOR;
                allowed = IsActive && (editor || !players.IsPlayerIdValid(playerId) ||
                    (players.IsAIPlayer(playerId)
                        ? Volatile.Read(ref allowAIPlacement) != 0
                        : Volatile.Read(ref allowHumanPlacement) != 0));
            }
            catch
            {
                allowed = false;
            }

            if (allowed)
                return;

            ulong validator = context.Pointer->RBX;
            *((int*)(validator + ElevatedMoatNativeContract.PlacementBlockedOffset)) =
                ElevatedMoatNativeContract.PlacementBlockedValue;
            *((int*)(validator + ElevatedMoatNativeContract.PlacementFailureReasonOffset)) =
                ElevatedMoatNativeContract.PlacementFailureReason;
        }

        private void LogCallbackFailure(string stage, Exception exception)
        {
            if (Interlocked.Exchange(ref callbackFailureReported, 1) != 0)
                return;

            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features elevated-moat callback failed at {stage}; later duplicate failures are suppressed: {exception}");
        }

    }
}
