// Feature: Scale the native lifetime of all plague-cloud projectiles.
using System;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using System.Runtime.InteropServices;
using System.Threading;
using SHCDESE.Interop;

namespace ExtraFeatures
{
    internal sealed unsafe class PlagueDurationPatch : IDisposable
    {
        public const double MinimumMultiplier = 0.5;
        public const double MaximumMultiplier = 1000.0;

        private const int VanillaLifetime = 800;
        private const int LifetimeImmediateOffset = 9;
        private const int LifetimeComparisonOffset = 18;
        private const int LifetimeComparisonMinimumHookSize = 9;
        private const int LifetimeComparisonDisplacedBytes = 17;
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
        private readonly HookHandle<X64InlineHook> lifetimeComparisonHook = new HookHandle<X64InlineHook>();
        private AiFlagDiseaseTracker aiFlagDiseaseTracker;
        private int expectedLifetime = VanillaLifetime;
        private bool conditionalExceptionAvailable;
        private bool conditionalCallbackFailureLogged;
        private bool disposed;

        public PlagueDurationPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
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

            TryInitializeAiFlagException(libraryHandle, region, memory, referenceHashMatches, matchOffset);
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
                Volatile.Write(ref expectedLifetime, VanillaLifetime);
                conditionalExceptionAvailable = false;
            }
            finally
            {
                disposed = true;
            }
        }

        private void TryInitializeAiFlagException(
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            int lifetimePatternRva)
        {
            HookTransaction candidate = null;
            bool hookPublished = false;
            try
            {
                ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
                candidate = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
                ExtraFeaturesHookInfrastructure.AddContextHook(
                    candidate,
                    lifetimeComparisonHook,
                    libraryBase + unchecked((ulong)(lifetimePatternRva + LifetimeComparisonOffset)),
                    ApplyConditionalLifetime,
                    registers: X64SmartCPUContextRegs.Volatile | X64SmartCPUContextRegs.RBX,
                    hookSize: LifetimeComparisonMinimumHookSize,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult commitResult = candidate.Commit();
                if (!commitResult.IsCompleteSuccess || !lifetimeComparisonHook.Success)
                    throw new InvalidOperationException("The conditional plague-lifetime hook was not installed.");

                if (!lifetimeComparisonHook.IsInstalled)
                    throw new InvalidOperationException("The permanent plague-lifetime hook is not active.");
                if (lifetimeComparisonHook.Hook.DisplacedByteCount != LifetimeComparisonDisplacedBytes)
                    throw new InvalidOperationException("The plague-lifetime hook displaced an unexpected native span.");

                conditionalExceptionAvailable = true;
                if (referenceHashMatches)
                {
                    try
                    {
                        aiFlagDiseaseTracker = new AiFlagDiseaseTracker(
                            log,
                            libraryHandle,
                            region,
                            memory,
                            referenceHashMatches);
                    }
                    catch (Exception ex)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            "Extra Features could not initialize AI flag Disease tracking; " +
                            $"the permanent lifetime hook remains active without that exception: {ex}");
                    }
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features AI flag and Cesspit Disease lifetime exceptions initialized: " +
                    $"comparisonRva=0x{lifetimePatternRva + LifetimeComparisonOffset:X}, vanilla={VanillaLifetime}.");
                conditionalTransaction = candidate;
                hookPublished = true;
            }
            catch (Exception ex)
            {
                conditionalExceptionAvailable = false;
                RollbackUnpublishedLifetimeComparisonHook(candidate, hookPublished);
                aiFlagDiseaseTracker?.Dispose();
                aiFlagDiseaseTracker = null;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Extra Features could not initialize the AI flag and Cesspit Disease lifetime exceptions; " +
                    $"the configured global plague duration remains active: {ex}");
            }
        }

        private static void RollbackUnpublishedLifetimeComparisonHook(
            HookTransaction candidate,
            bool hookPublished)
        {
            if (!hookPublished)
                candidate?.Dispose();
        }

        private void ApplyConditionalLifetime(NativePointer<X64SmartCPUContext> context)
        {
            if (!conditionalExceptionAvailable)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers->R8 == 0)
                    throw new InvalidOperationException("The Disease update supplied a null projectile-array base.");

                int lifetime = Volatile.Read(ref expectedLifetime);
                if (aiFlagDiseaseTracker != null)
                {
                    GameProjectile* projectile = (GameProjectile*)(registers->R8 + registers->RBX);
                    if (aiFlagDiseaseTracker.IsTracked(projectile))
                        lifetime = VanillaLifetime;
                }
                registers->RAX = unchecked((uint)lifetime);
            }
            catch (Exception ex)
            {
                conditionalExceptionAvailable = false;
                if (conditionalCallbackFailureLogged)
                    return;

                conditionalCallbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Extra Features plague lifetime callback was disabled for this process; " +
                    $"the physically installed hook now preserves Vanilla behavior: {ex}");
            }
        }

        public bool TryRegisterVanillaFlagDisease(int projectileId) =>
            conditionalExceptionAvailable &&
            aiFlagDiseaseTracker != null &&
            aiFlagDiseaseTracker.TryTrackExternalDiseaseFlag(projectileId);

        private void SetLifetime(int desiredLifetime)
        {
            if (desiredLifetime == Volatile.Read(ref expectedLifetime))
                return;
            if (!conditionalExceptionAvailable || !lifetimeComparisonHook.IsInstalled)
                throw new InvalidOperationException("The permanent plague-lifetime hook is unavailable.");

            Volatile.Write(ref expectedLifetime, desiredLifetime);
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
