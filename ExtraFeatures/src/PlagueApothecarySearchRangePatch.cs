// Feature: Customize Vanilla's maximum plague-search distance from an apothecary building.
using BepInEx.Logging;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ExtraFeatures
{
    internal sealed unsafe class PlagueApothecarySearchRangePatch : IDisposable
    {
        public const int VanillaMaximumDistance = 30;
        public const int MinimumDistance = 20;
        public const int MaximumDistance = 200;

        private const int CompareDisplacementOffset = 2;
        private const int CompareInstructionLength = 7;

        // c_game_projectile_disease_find_nearest_for_healer, reference RVA 0x9F81B.
        // This is the comparison immediately after Vanilla calculates Manhattan
        // distance from the healer's assigned building to a disease projectile.
        private const string BuildingDistanceComparisonPattern =
            "83 3D ?? ?? ?? ?? 1E 7F ?? 0F BF 4B 1C 48 8D 15 ?? ?? ?? ?? " +
            "44 0F BF 4B 1A 49 69 C4 90 04 00 00";
        private const int BuildingDistanceComparisonRva = 0x9F81B;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> distanceComparisonHook = new HookRef<X64InlineHook>();
        private IntPtr distanceResultAddress;
        private bool featureAvailable = true;
        private bool disposed;

        public PlagueApothecarySearchRangePatch(
            ManualLogSource log,
            ExtraFeaturesViewModel settings,
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int matchOffset = Shared.NativePatternResolver.ResolveUnique(
                memory,
                BuildingDistanceComparisonPattern,
                BuildingDistanceComparisonRva,
                referenceHashMatches,
                "apothecary plague-search range comparison",
                log).Rva;
            long moduleBase = libraryHandle.ToInt64();
            int displacement = ReadInt32LittleEndian(
                memory,
                matchOffset + CompareDisplacementOffset);
            long resolvedAddress = checked(
                moduleBase + matchOffset + CompareInstructionLength + displacement);
            long moduleEnd = checked(moduleBase + memory.Length);
            if (resolvedAddress < moduleBase || resolvedAddress + sizeof(int) > moduleEnd)
                throw new InvalidOperationException("The native plague-distance result lies outside the game module.");

            distanceResultAddress = new IntPtr(resolvedAddress);
            try
            {
                transaction = new HookTransaction(
                    memory,
                    unchecked((ulong)moduleBase),
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref distanceComparisonHook,
                    unchecked((ulong)(moduleBase + matchOffset)),
                    ApplyConfiguredDistanceComparison,
                    regs: X64SmartCPUContextRegs.Volatile,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!distanceComparisonHook.Success)
                    throw new InvalidOperationException("The apothecary plague-search range hook was not installed.");

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features apothecary plague-search range initialized: " +
                    $"configured={settings.ApothecaryPlagueSearchDistance}, vanilla={VanillaMaximumDistance}, " +
                    $"allowed={MinimumDistance}-{MaximumDistance}.");
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
            featureAvailable = false;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            distanceResultAddress = IntPtr.Zero;
        }

        private void ApplyConfiguredDistanceComparison(NativePointer<X64SmartCPUContext> _)
        {
            if (!featureAvailable || !settings.EnableMod)
                return;

            int configuredMaximum = settings.ApothecaryPlagueSearchDistance;
            if (configuredMaximum == VanillaMaximumDistance)
                return;

            try
            {
                int actualDistance = Marshal.ReadInt32(distanceResultAddress);
                if (actualDistance < 0)
                    throw new InvalidOperationException($"Vanilla returned a negative plague distance: {actualDistance}.");

                // The displaced Vanilla instruction still compares against 30.
                // Normalize only its scratch result to 30/31 so the branch represents
                // the configured inclusive limit without replacing Vanilla's selector.
                int normalizedForVanilla = actualDistance <= configuredMaximum
                    ? VanillaMaximumDistance
                    : VanillaMaximumDistance + 1;
                Marshal.WriteInt32(distanceResultAddress, normalizedForVanilla);
            }
            catch (Exception ex)
            {
                DisableFeature(ex);
            }
        }

        private void DisableFeature(Exception failure)
        {
            if (!featureAvailable)
                return;

            featureAvailable = false;
            Shared.DebugLogHelper.LogError(
                log,
                $"Extra Features apothecary plague-search range is disabled for this process; " +
                $"Vanilla distance 30 and all other features remain active: {failure}");
        }

        private static int ReadInt32LittleEndian(ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset + sizeof(int) > bytes.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24);
        }
    }
}
