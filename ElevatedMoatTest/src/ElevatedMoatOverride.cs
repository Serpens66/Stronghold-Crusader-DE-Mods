using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace ElevatedMoatTest
{
    internal sealed unsafe class ElevatedMoatOverride : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> heightFailureWriterHook =
            new HookHandle<X64InlineHook>();
        private int firstInterventionLogged;
        private int callbackFailureLogged;

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

            ElevatedMoatNativeContract.Validate(memory, resolution.Rva);
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)resolution.Rva),
                ElevatedMoatNativeContract.HeightWriterLength))
            {
                if (probe.DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength)
                    throw new InvalidOperationException("Unexpected RedBird elevated-moat writer span before installation.");
            }

            HookTransaction pending = null;
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

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !heightFailureWriterHook.Success)
                    throw new InvalidOperationException($"The elevated-moat hook transaction was incomplete: {result}.");
                if (heightFailureWriterHook.Hook.DisplacedByteCount != ElevatedMoatNativeContract.HeightWriterLength)
                    throw new InvalidOperationException("Unexpected installed elevated-moat overwrite length; rolling back.");

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
                throw;
            }

            Shared.DebugLogHelper.LogWarning(
                log,
                $"Elevated Moat Test active for all players: writerRva=0x{resolution.Rva:X}, " +
                $"hookLength={heightFailureWriterHook.Hook.DisplacedByteCount}, mapperMoat={ElevatedMoatNativeContract.MapperMoat}, " +
                $"vanillaMaximumHeight={ElevatedMoatNativeContract.MaximumVanillaTerrainHeight}.");
        }

        public void Dispose()
        {
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
    }
}
