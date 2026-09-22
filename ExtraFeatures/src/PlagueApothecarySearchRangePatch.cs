// Feature: Customize Vanilla's maximum plague-search distance from an apothecary building.
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtraFeatures
{
    internal sealed unsafe class PlagueApothecarySearchRangePatch
    {
        public const int VanillaMaximumDistance = 30;
        public const int MinimumDistance = 20;
        public const int MaximumDistance = 200;

        internal const int HookRva = 0x9F866;
        internal const int HookDisplacedBytes = 14;
        internal const int DistanceCalculationRva = 0x79C0;
        internal const int RejectRva = 0x9F8CD;
        internal const int ReturnRva = 0x9F874;

        private const int PatternRva = 0x9F85F;
        private const int HookOffsetInPattern = HookRva - PatternRva;
        private const int CompareOffsetInHook = 5;
        private const int CompareDisplacementOffset = 2;
        private const int CompareInstructionLength = 7;
        private const string HookPattern =
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " +
            "83 3D ?? ?? ?? ?? 1E 7F ?? 0F BF 4B 1C 48 8D 15 ?? ?? ?? ??";

        private static readonly object InstallationSync = new object();
        private static PlagueApothecarySearchRangePatch rootedPublishedInstance;

        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook> distanceComparisonHook =
            new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private IntPtr effectiveMaximumAddress;
        private bool published;

        private PlagueApothecarySearchRangePatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int patternRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                HookPattern,
                PatternRva,
                referenceHashMatches,
                "apothecary plague-search range block",
                log).Rva;
            int hookRva = checked(patternRva + HookOffsetInPattern);
            if (hookRva != HookRva || hookRva + HookDisplacedBytes != ReturnRva)
                throw new InvalidOperationException(
                    "The apothecary plague-search hook boundary changed.");

            long moduleBase = libraryHandle.ToInt64();
            int compareRva = checked(hookRva + CompareOffsetInHook);
            int displacement = ReadInt32LittleEndian(
                memory,
                compareRva + CompareDisplacementOffset);
            long distanceResult = checked(
                moduleBase + compareRva + CompareInstructionLength + displacement);
            long moduleEnd = checked(moduleBase + memory.Length);
            if (distanceResult < moduleBase || distanceResult + sizeof(int) > moduleEnd)
                throw new InvalidOperationException(
                    "The native plague-distance result lies outside the game module.");

            HookTransaction pendingTransaction = null;
            try
            {
                effectiveMaximumAddress = Marshal.AllocHGlobal(sizeof(int));
                Marshal.WriteInt32(effectiveMaximumAddress, VanillaMaximumDistance);

                ulong imageBase = unchecked((ulong)moduleBase);
                ulong distanceResultAddress = unchecked((ulong)distanceResult);
                ulong maximumAddress = unchecked((ulong)effectiveMaximumAddress.ToInt64());
                pendingTransaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
                pendingTransaction.AddInline(
                    distanceComparisonHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)hookRva)),
                    (assembler, instructions, returnAddress) =>
                        PlagueApothecarySearchRangeEmitter.Emit(
                            assembler,
                            instructions,
                            distanceResultAddress,
                            maximumAddress,
                            imageBase + DistanceCalculationRva,
                            imageBase + RejectRva),
                    hookSize: HookDisplacedBytes);
                CommitResult commitResult = pendingTransaction.Commit();
                if (!commitResult.IsCompleteSuccess ||
                    !distanceComparisonHook.Success ||
                    !distanceComparisonHook.IsInstalled ||
                    distanceComparisonHook.Hook.DisplacedByteCount != HookDisplacedBytes)
                {
                    throw new InvalidOperationException(
                        $"The apothecary plague-search hook displaced " +
                        $"{distanceComparisonHook.Hook?.DisplacedByteCount ?? -1} bytes; " +
                        $"expected {HookDisplacedBytes}.");
                }

                transaction = pendingTransaction;
                pendingTransaction = null;
            }
            catch
            {
                if (pendingTransaction != null)
                {
                    try { pendingTransaction.Dispose(); } catch { }
                }
                RollbackUnpublishedCandidate();
                throw;
            }
        }

        internal static PlagueApothecarySearchRangePatch Install(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            lock (InstallationSync)
            {
                if (rootedPublishedInstance != null)
                    return rootedPublishedInstance;

                var candidate = new PlagueApothecarySearchRangePatch(
                    log,
                    libraryHandle,
                    region,
                    memory,
                    referenceHashMatches);
                candidate.published = true;
                rootedPublishedInstance = candidate;

                try
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Extra Features apothecary plague-search hook installed disabled: " +
                        $"startRva=0x{HookRva:X}, displaced={HookDisplacedBytes}, " +
                        $"returnRva=0x{ReturnRva:X}, rejectRva=0x{RejectRva:X}, " +
                        "incomingInteriorTargets=0.");
                    Shared.CrashBreadcrumbDiagnostics.Record(
                        "ApothecaryPlagueSearchHookInstalled",
                        HookRva,
                        HookDisplacedBytes);
                }
                catch { }
                return candidate;
            }
        }

        internal void SetEffectiveMaximum(bool enabled, int configuredMaximum)
        {
            int boundedMaximum = Math.Max(
                MinimumDistance,
                Math.Min(MaximumDistance, configuredMaximum));
            int effectiveMaximum = enabled ? boundedMaximum : VanillaMaximumDistance;
            int previous = Interlocked.Exchange(
                ref *(int*)effectiveMaximumAddress.ToPointer(),
                effectiveMaximum);
            if (previous == effectiveMaximum)
                return;

            Shared.CrashBreadcrumbDiagnostics.Record(
                "ApothecaryPlagueSearchMaximum",
                effectiveMaximum);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Extra Features apothecary plague-search maximum is now " +
                $"{effectiveMaximum} ({(enabled ? "configured" : "Vanilla")}).");
        }

        private void RollbackUnpublishedCandidate()
        {
            if (published)
                return;

            if (transaction != null)
            {
                try { transaction.Dispose(); } catch { }
                transaction = null;
            }
            if (effectiveMaximumAddress != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(effectiveMaximumAddress);
                effectiveMaximumAddress = IntPtr.Zero;
            }
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
