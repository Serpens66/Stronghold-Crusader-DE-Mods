// Feature: Scale the native lifetime of all plague-cloud projectiles.
using System;
using System.Diagnostics;
using BepInEx.Logging;
using System.Runtime.InteropServices;
using SHCDESE.Interop;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;
using Zhuqiaomon.Windows;

namespace ExtraFeatures
{
    internal sealed unsafe class PlagueDurationPatch : IDisposable
    {
        public const double MinimumMultiplier = 0.5;
        public const double MaximumMultiplier = 1000.0;

        private const int VanillaLifetime = 800;
        private const int LifetimeImmediateOffset = 9;
        private const int LifetimeComparisonOffset = 18;
        private const int LifetimePatternRva = 0x9A164;

        // Disease update signature at reference RVA 0x9A164; the lifetime opcode
        // starts at RVA 0x9A16C and its immediate at RVA 0x9A16D for CrusaderDE.dll SHA-256
        // FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
        // The lifetime immediate is wildcarded so a moved function can still be found,
        // while the surrounding age comparison and fade transition remain validated.
        private const string LifetimePattern =
            "41 0F BF 44 18 18 03 D0 B8 ?? ?? ?? ?? 41 89 54 18 14 " +
            "66 41 39 84 18 D0 00 00 00 7C 06 66 45 89 4C 18 28 " +
            "41 0F B7 44 18 14 49 8D 0C 18 66 83 C0 10 66 41 89 44 18 34";

        private readonly IntPtr lifetimeAddress;
        private readonly ManualLogSource log;
        private HookTransaction conditionalTransaction;
        private HookRef<X64InlineHook> lifetimeComparisonHook = new HookRef<X64InlineHook>();
        private AiFlagDiseaseTracker aiFlagDiseaseTracker;
        private int expectedLifetime = VanillaLifetime;
        private bool conditionalExceptionAvailable;
        private bool conditionalCallbackFailureLogged;
        private bool disposed;

        public PlagueDurationPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int matchOffset = Shared.NativePatternResolver.ResolveUnique(
                memory,
                LifetimePattern,
                LifetimePatternRva,
                referenceHashMatches,
                "plague lifetime instruction",
                log).Rva;
            int immediateOffset = checked(matchOffset + LifetimeImmediateOffset);
            if (immediateOffset < 0 || immediateOffset + sizeof(int) > memory.Length)
                throw new InvalidOperationException("The plague lifetime immediate lies outside the game module.");

            lifetimeAddress = IntPtr.Add(libraryHandle, immediateOffset);
            int currentLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (currentLifetime != VanillaLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime has an unexpected native value: expected={VanillaLifetime}, actual={currentLifetime}.");
            }

            TryInitializeAiFlagException(libraryHandle, memory, referenceHashMatches, matchOffset);
        }

        public void Apply(double multiplier, bool enabled)
        {
            ThrowIfDisposed();
            int desiredLifetime = enabled
                ? checked((int)Math.Round(VanillaLifetime * ClampMultiplier(multiplier), MidpointRounding.AwayFromZero))
                : VanillaLifetime;
            SetLifetime(desiredLifetime);
        }

        public void RestoreVanilla()
        {
            ThrowIfDisposed();
            SetLifetime(VanillaLifetime);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try
            {
                conditionalExceptionAvailable = false;
                conditionalTransaction?.Unload();
                conditionalTransaction?.Dispose();
                conditionalTransaction = null;
                aiFlagDiseaseTracker?.Dispose();
                aiFlagDiseaseTracker = null;
                RestoreVanilla();
            }
            finally
            {
                disposed = true;
            }
        }

        private void TryInitializeAiFlagException(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            int lifetimePatternRva)
        {
            try
            {
                aiFlagDiseaseTracker = new AiFlagDiseaseTracker(
                    log,
                    libraryHandle,
                    memory,
                    referenceHashMatches);

                ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
                conditionalTransaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                conditionalTransaction.AddContextHook(
                    ref lifetimeComparisonHook,
                    libraryBase + unchecked((ulong)(lifetimePatternRva + LifetimeComparisonOffset)),
                    ApplyConditionalLifetime,
                    regs: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                conditionalTransaction.Commit();
                if (!lifetimeComparisonHook.Success)
                    throw new InvalidOperationException("The conditional plague-lifetime hook was not installed.");

                conditionalExceptionAvailable = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features AI flag Disease lifetime exception initialized: " +
                    $"comparisonRva=0x{lifetimePatternRva + LifetimeComparisonOffset:X}, vanilla={VanillaLifetime}.");
            }
            catch (Exception ex)
            {
                conditionalExceptionAvailable = false;
                conditionalTransaction?.Unload();
                conditionalTransaction?.Dispose();
                conditionalTransaction = null;
                aiFlagDiseaseTracker?.Dispose();
                aiFlagDiseaseTracker = null;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Extra Features could not initialize the AI flag Disease lifetime exception; " +
                    $"the configured global plague duration remains active: {ex}");
            }
        }

        private void ApplyConditionalLifetime(NativePointer<X64SmartCPUContext> context)
        {
            if (!conditionalExceptionAvailable || aiFlagDiseaseTracker == null)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers->R8 == 0)
                    throw new InvalidOperationException("The Disease update supplied a null projectile-array base.");

                GameProjectile* projectile = (GameProjectile*)(registers->R8 + registers->RBX);
                if (aiFlagDiseaseTracker.IsTracked(projectile))
                    registers->RAX = VanillaLifetime;
            }
            catch (Exception ex)
            {
                conditionalExceptionAvailable = false;
                if (conditionalCallbackFailureLogged)
                    return;

                conditionalCallbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Extra Features AI flag Disease lifetime exception was disabled for this process; " +
                    $"the configured global plague duration remains active: {ex}");
            }
        }

        private void SetLifetime(int desiredLifetime)
        {
            if (desiredLifetime == expectedLifetime)
                return;

            int currentLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (currentLifetime != expectedLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime bytes changed unexpectedly: expected={expectedLifetime}, actual={currentLifetime}.");
            }

            UIntPtr size = (UIntPtr)sizeof(int);
            if (!Kernel32.VirtualProtect(
                    lifetimeAddress,
                    size,
                    Kernel32.MemoryPermissions.PAGE_EXECUTE_READWRITE,
                    out Kernel32.MemoryPermissions oldProtection))
            {
                throw new InvalidOperationException("VirtualProtect failed for the plague lifetime patch.");
            }

            bool valueWritten = false;
            try
            {
                Marshal.WriteInt32(lifetimeAddress, desiredLifetime);
                valueWritten = true;
            }
            finally
            {
                if (valueWritten)
                    expectedLifetime = desiredLifetime;

                if (!Kernel32.VirtualProtect(lifetimeAddress, size, oldProtection, out _))
                    throw new InvalidOperationException("Restoring memory protection failed for the plague lifetime patch.");
            }

            if (!MinWinAPI.FlushInstructionCache(Process.GetCurrentProcess().Handle, lifetimeAddress, size))
                throw new InvalidOperationException("Flushing the instruction cache failed for the plague lifetime patch.");

            int verifiedLifetime = Marshal.ReadInt32(lifetimeAddress);
            if (verifiedLifetime != desiredLifetime)
            {
                throw new InvalidOperationException(
                    $"The plague lifetime patch verification failed: expected={desiredLifetime}, actual={verifiedLifetime}.");
            }
        }

        private static double ClampMultiplier(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            return Math.Max(MinimumMultiplier, Math.Min(MaximumMultiplier, value));
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PlagueDurationPatch));
        }
    }
}
