// Feature: Prevent a horse-only recruitment failure from reusing a stale missing-good id.
using BepInEx.Logging;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiRecruitmentHorseDemandFix : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64ManagedFunctionDetourAOB<RecruitEuropeanUnitDelegate>> recruitHook =
            new HookRef<X64ManagedFunctionDetourAOB<RecruitEuropeanUnitDelegate>>();
        private bool disposed;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RecruitEuropeanUnitDelegate(
            IntPtr unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly);

        public AiRecruitmentHorseDemandFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // The entry signature proves the result-code field at +0x650, but the
            // companion missing-good field at +0x654 is written only in later branches.
            // Do not write that fixed manager layout on an unknown native build.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The AI recruitment horse-demand fix remains inactive because its fixed recruitment-result layout is not validated for this CrusaderDE.dll.");
            }

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitPattern,
                AiRecruitmentHorseDemandNativeDefinition.RecruitEuropeanUnitRva,
                referenceHashMatches,
                "European troop recruitment",
                log);

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddDetour(
                    ref recruitHook,
                    libraryBase + unchecked((ulong)resolution.Rva),
                    RecruitEuropeanUnit);
                transaction.Commit();

                if (!recruitHook.Success)
                    throw new InvalidOperationException("The European troop recruitment hook was not installed.");

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Bugfixes and QoL AI recruitment horse-demand hook installed: " +
                    $"method={resolution.Method}, rva=0x{resolution.Rva:X}, enabled={IsEnabled}.");

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
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
        }

        private int RecruitEuropeanUnit(
            IntPtr unitManager,
            int unitType,
            int spawnContext,
            int playerId,
            int validationOnly)
        {
            if (unitManager != IntPtr.Zero && IsEnabled)
            {
                // Vanilla clears the error code but not this companion output. A horse-only
                // failure would otherwise leave the AI reading an earlier weapon id.
                WriteManagerInt(
                    unitManager,
                    AiRecruitmentHorseDemandNativeDefinition.MissingGoodIdOffset,
                    0);
            }

            return recruitHook.Value.Hook.Trampoline(
                unitManager,
                unitType,
                spawnContext,
                playerId,
                validationOnly);
        }

        private bool IsEnabled => settings.EnableMod && settings.EnableAiFixes;

        private static void WriteManagerInt(IntPtr unitManager, int offset, int value) =>
            *(int*)((byte*)unitManager.ToPointer() + offset) = value;
    }
}
