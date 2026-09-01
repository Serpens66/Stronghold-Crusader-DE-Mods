using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    /// <summary>Documented Vanilla values and supported UI ranges for gatehouse timing.</summary>
    public static class GatehouseTimingValues
    {
        /// <summary>Minimum supported human reopening delay in seconds.</summary>
        public const double MinimumHumanDelaySeconds = 0.0;
        /// <summary>Maximum supported human reopening delay in seconds.</summary>
        public const double MaximumHumanDelaySeconds = 30.0;
        /// <summary>Minimum supported AI reopening delay in seconds.</summary>
        public const double MinimumAiDelaySeconds = 0.0;
        /// <summary>Maximum supported AI reopening delay in seconds.</summary>
        public const double MaximumAiDelaySeconds = 120.0;
        /// <summary>Minimum supported closing distance in tiles.</summary>
        public const double MinimumDistanceTiles = 5.0;
        /// <summary>Maximum supported closing distance in tiles.</summary>
        public const double MaximumDistanceTiles = 50.0;
        /// <summary>Vanilla human reopening delay in seconds.</summary>
        public const double VanillaHumanDelaySeconds = 2.5;
        /// <summary>Vanilla AI reopening delay in seconds.</summary>
        public const double VanillaAiDelaySeconds = 30.0;
        /// <summary>Vanilla human closing distance in tiles.</summary>
        public const double VanillaHumanDistanceTiles = 17.5;
        /// <summary>Vanilla AI closing distance in tiles.</summary>
        public const double VanillaAiDistanceTiles = 25.0;
    }

    /// <summary>Immutable desired gatehouse timing and enemy-proximity settings.</summary>
    public readonly struct GatehouseTimingSettings
    {
        /// <summary>Creates gatehouse settings expressed in seconds and map tiles.</summary>
        public GatehouseTimingSettings(
            bool enabled,
            double humanReopenDelaySeconds,
            double aiReopenDelaySeconds,
            double humanCloseDistanceTiles,
            double aiCloseDistanceTiles)
        {
            Enabled = enabled;
            HumanReopenDelaySeconds = humanReopenDelaySeconds;
            AiReopenDelaySeconds = aiReopenDelaySeconds;
            HumanCloseDistanceTiles = humanCloseDistanceTiles;
            AiCloseDistanceTiles = aiCloseDistanceTiles;
        }

        /// <summary>
        /// Gets whether custom timing values should be applied. False restores only the four
        /// Vanilla timing values and does not change the independently owned distance origin.
        /// </summary>
        public bool Enabled { get; }
        /// <summary>Gets the human gate reopening delay in seconds.</summary>
        public double HumanReopenDelaySeconds { get; }
        /// <summary>Gets the AI gate reopening delay in seconds.</summary>
        public double AiReopenDelaySeconds { get; }
        /// <summary>Gets the human gate closing distance in tiles.</summary>
        public double HumanCloseDistanceTiles { get; }
        /// <summary>Gets the AI gate closing distance in tiles.</summary>
        public double AiCloseDistanceTiles { get; }
    }

    /// <summary>
    /// Applies validated, transactional gatehouse timing values. The intended future consumer is
    /// ExtraFeatures; this is documentation, not a runtime dependency.
    /// </summary>
    public interface IGatehouseTimingCapability
    {
        /// <summary>Attempts to apply and verify all four gatehouse timing values as one transaction.</summary>
        bool TryApply(GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic);
    }

    internal sealed class GatehouseBuildTarget
    {
        public static GatehouseBuildTarget Supported { get; } = new GatehouseBuildTarget(
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
            0xB73D0,
            2325,
            "F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8",
            0xB7B70,
            Hex("0F BF 8C 2A 0E 8B 7E 06 42 8D 2C FD 00 00 00 00 8B C1 44 8B C5 2B C5 44 2B C1 3B E9 42 8D 2C E5 00 00 00 00 44 0F 4E C0 48 8D 05 61 84 F4 FF 0F BF 8C 02 10 8B 7E 06 8B D5 2B D1 8B C1 2B C5 3B E9 0F 4E D0 44 3B C2 44 0F 4C C2"),
            Hex("44 0F BF 84 2A 0E 8B 7E 06 0F BF 8C 2A 10 8B 7E 06 0F BF 84 2B 0A CD 4C 06 44 01 F8 C1 E0 02 44 29 C0 99 31 D0 29 D0 41 89 C0 0F BF 84 2B 0C CD 4C 06 44 01 E0 C1 E0 02 29 C8 99 31 D0 29 D0 41 39 C0 44 0F 4C C0 90 90 90 90 90"),
            0xB7BBB,
            Hex("40 84 F6 75 10 41 81 F8 C8 00 00 00 7D 10 B8 B0 04 00 00 EB 69 41 81 F8 8C 00 00 00 7C 5B"),
            0xB7C32,
            Hex("EB 50 B8 64 00 00 00 48 8D 2D C0 83 F4 FF"),
            0xB7BC3,
            0xB7BCA,
            0xB7BD3,
            0xB7C35);

        public GatehouseBuildTarget(
            string buildHash,
            int functionRva,
            int functionSize,
            string functionHash,
            int distanceBlockRva,
            byte[] vanillaDistanceBlockBytes,
            byte[] centeredDistanceBlockBytes,
            int decisionBlockRva,
            byte[] decisionBlockBytes,
            int humanDelayBlockRva,
            byte[] humanDelayBlockBytes,
            int aiCloseDistanceRva,
            int aiReopenDelayRva,
            int humanCloseDistanceRva,
            int humanReopenDelayRva)
        {
            BuildHash = buildHash;
            FunctionRva = functionRva;
            FunctionSize = functionSize;
            FunctionHash = functionHash;
            DistanceBlockRva = distanceBlockRva;
            VanillaDistanceBlockBytes = vanillaDistanceBlockBytes;
            CenteredDistanceBlockBytes = centeredDistanceBlockBytes;
            DecisionBlockRva = decisionBlockRva;
            DecisionBlockBytes = decisionBlockBytes;
            HumanDelayBlockRva = humanDelayBlockRva;
            HumanDelayBlockBytes = humanDelayBlockBytes;
            AiCloseDistanceRva = aiCloseDistanceRva;
            AiReopenDelayRva = aiReopenDelayRva;
            HumanCloseDistanceRva = humanCloseDistanceRva;
            HumanReopenDelayRva = humanReopenDelayRva;
        }

        public string BuildHash { get; }
        public int FunctionRva { get; }
        public int FunctionSize { get; }
        public int FunctionEndRva => checked(FunctionRva + FunctionSize);
        public string FunctionHash { get; }
        public int DistanceBlockRva { get; }
        public byte[] VanillaDistanceBlockBytes { get; }
        public byte[] CenteredDistanceBlockBytes { get; }
        public int DecisionBlockRva { get; }
        public byte[] DecisionBlockBytes { get; }
        public int HumanDelayBlockRva { get; }
        public byte[] HumanDelayBlockBytes { get; }
        public int AiCloseDistanceRva { get; }
        public int AiReopenDelayRva { get; }
        public int HumanCloseDistanceRva { get; }
        public int HumanReopenDelayRva { get; }

        private static byte[] Hex(string text)
        {
            string[] tokens = text.Split(' ');
            var bytes = new byte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
                bytes[index] = Convert.ToByte(tokens[index], 16);
            return bytes;
        }
    }

    internal static class GatehouseCapabilityResolver
    {
        public static void Resolve(
            string binaryHash,
            long moduleBase,
            ReadOnlySpan<byte> memory,
            INativeMemory nativeMemory,
            NativeOwnershipRegistry ownership,
            object mutationSync,
            ManualLogSource log,
            GatehouseBuildTarget target,
            out GatehouseDistanceOriginService distanceOriginService,
            out NativeCapabilityDiagnostic distanceOriginDiagnostic,
            out GatehouseTimingService timingService,
            out NativeCapabilityDiagnostic timingDiagnostic)
        {
            distanceOriginService = null;
            timingService = null;
            NativeSection functionSection;

            if (!string.Equals(binaryHash, target.BuildHash, StringComparison.OrdinalIgnoreCase))
            {
                const string reason = "The installed CrusaderDE.dll hash is not present in the compiled target catalog.";
                distanceOriginDiagnostic = Diagnostic(
                    NativeCapabilityIds.GatehouseDistanceOrigin,
                    NativeCapabilityState.UnsupportedBuild,
                    binaryHash,
                    reason);
                timingDiagnostic = Diagnostic(
                    NativeCapabilityIds.GatehouseTiming,
                    NativeCapabilityState.UnsupportedBuild,
                    binaryHash,
                    reason);
                return;
            }

            try
            {
                if (moduleBase == 0 || memory.Length == 0)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Crusader native module is unavailable.");
                if (nativeMemory == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native memory adapter is unavailable.");
                if (ownership == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native ownership registry is unavailable.");
                if (mutationSync == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The gatehouse mutation coordinator is unavailable.");

                NativePeImage pe = NativePeImage.Parse(memory);
                functionSection = pe.RequireExecutableRange(target.FunctionRva, target.FunctionSize, "gatehouse handler function");

                string actualFunctionHash = SerpNativeApiRuntime.ComputeSha256(memory.Slice(target.FunctionRva, target.FunctionSize));
                if (!string.Equals(actualFunctionHash, target.FunctionHash, StringComparison.OrdinalIgnoreCase))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"The gatehouse handler function hash changed: expected={target.FunctionHash}, actual={actualFunctionHash}.");
            }
            catch (NativeResolutionException ex)
            {
                distanceOriginDiagnostic = Diagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, ex.State, binaryHash, ex.Message);
                timingDiagnostic = Diagnostic(NativeCapabilityIds.GatehouseTiming, ex.State, binaryHash, ex.Message);
                return;
            }
            catch (Exception ex)
            {
                distanceOriginDiagnostic = Diagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, NativeCapabilityState.Faulted, binaryHash, ex.Message);
                timingDiagnostic = Diagnostic(NativeCapabilityIds.GatehouseTiming, NativeCapabilityState.Faulted, binaryHash, ex.Message);
                return;
            }

            ResolveDistanceOrigin(
                binaryHash,
                moduleBase,
                memory,
                nativeMemory,
                ownership,
                mutationSync,
                log,
                target,
                functionSection,
                out distanceOriginService,
                out distanceOriginDiagnostic);
            ResolveTiming(
                binaryHash,
                moduleBase,
                memory,
                nativeMemory,
                ownership,
                mutationSync,
                log,
                target,
                functionSection,
                out timingService,
                out timingDiagnostic);
        }

        private static void ResolveDistanceOrigin(
            string binaryHash,
            long moduleBase,
            ReadOnlySpan<byte> memory,
            INativeMemory nativeMemory,
            NativeOwnershipRegistry ownership,
            object mutationSync,
            ManualLogSource log,
            GatehouseBuildTarget target,
            NativeSection functionSection,
            out GatehouseDistanceOriginService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            try
            {
                if (target.VanillaDistanceBlockBytes == null || target.CenteredDistanceBlockBytes == null ||
                    target.VanillaDistanceBlockBytes.Length != target.CenteredDistanceBlockBytes.Length)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The gatehouse distance patch catalog is incomplete or has mismatched block lengths.");
                RequireInsideFunction(target, target.DistanceBlockRva, target.VanillaDistanceBlockBytes.Length, "gatehouse distance block");
                RequireSameSection(functionSection, target.DistanceBlockRva, target.VanillaDistanceBlockBytes.Length, "gatehouse distance block");
                RequireBytes(memory, target.DistanceBlockRva, target.VanillaDistanceBlockBytes, "gatehouse distance block");

                var memoryTarget = new GatehouseDistanceOriginTarget(
                    moduleBase + target.DistanceBlockRva,
                    target.VanillaDistanceBlockBytes,
                    target.CenteredDistanceBlockBytes);
                service = new GatehouseDistanceOriginService(binaryHash, memoryTarget, nativeMemory, ownership, mutationSync, log);
                diagnostic = AvailableDiagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, binaryHash, target);
            }
            catch (NativeResolutionException ex)
            {
                diagnostic = Diagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                diagnostic = Diagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, NativeCapabilityState.Faulted, binaryHash, ex.Message);
            }
        }

        private static void ResolveTiming(
            string binaryHash,
            long moduleBase,
            ReadOnlySpan<byte> memory,
            INativeMemory nativeMemory,
            NativeOwnershipRegistry ownership,
            object mutationSync,
            ManualLogSource log,
            GatehouseBuildTarget target,
            NativeSection functionSection,
            out GatehouseTimingService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            try
            {
                RequireInsideFunction(target, target.DecisionBlockRva, target.DecisionBlockBytes.Length, "gatehouse decision block");
                RequireInsideFunction(target, target.HumanDelayBlockRva, target.HumanDelayBlockBytes.Length, "gatehouse human delay block");
                RequireInsideFunction(target, target.AiCloseDistanceRva, 4, "gatehouse AI close distance");
                RequireInsideFunction(target, target.AiReopenDelayRva, 4, "gatehouse AI reopen delay");
                RequireInsideFunction(target, target.HumanCloseDistanceRva, 4, "gatehouse human close distance");
                RequireInsideFunction(target, target.HumanReopenDelayRva, 4, "gatehouse human reopen delay");
                RequireSameSection(functionSection, target.DecisionBlockRva, target.DecisionBlockBytes.Length, "gatehouse decision block");
                RequireSameSection(functionSection, target.HumanDelayBlockRva, target.HumanDelayBlockBytes.Length, "gatehouse human delay block");
                RequireBytes(memory, target.DecisionBlockRva, target.DecisionBlockBytes, "gatehouse decision block");
                RequireBytes(memory, target.HumanDelayBlockRva, target.HumanDelayBlockBytes, "gatehouse human delay block");
                RequireInt32(memory, target.AiCloseDistanceRva, GatehouseTimingTarget.VanillaAiDistance, "gatehouse AI close distance");
                RequireInt32(memory, target.AiReopenDelayRva, GatehouseTimingTarget.VanillaAiDelay, "gatehouse AI reopen delay");
                RequireInt32(memory, target.HumanCloseDistanceRva, GatehouseTimingTarget.VanillaHumanDistance, "gatehouse human close distance");
                RequireInt32(memory, target.HumanReopenDelayRva, GatehouseTimingTarget.VanillaHumanDelay, "gatehouse human reopen delay");

                IReadOnlyList<NativeByteInvariant> invariants = CreateInstructionInvariants(moduleBase, target);
                var memoryTarget = new GatehouseTimingTarget(
                    moduleBase + target.AiCloseDistanceRva,
                    moduleBase + target.AiReopenDelayRva,
                    moduleBase + target.HumanCloseDistanceRva,
                    moduleBase + target.HumanReopenDelayRva,
                    invariants);
                service = new GatehouseTimingService(binaryHash, memoryTarget, nativeMemory, ownership, mutationSync, log);
                diagnostic = AvailableDiagnostic(NativeCapabilityIds.GatehouseTiming, binaryHash, target);
            }
            catch (NativeResolutionException ex)
            {
                diagnostic = Diagnostic(NativeCapabilityIds.GatehouseTiming, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                diagnostic = Diagnostic(NativeCapabilityIds.GatehouseTiming, NativeCapabilityState.Faulted, binaryHash, ex.Message);
            }
        }

        private static NativeCapabilityDiagnostic AvailableDiagnostic(
            string capabilityId,
            string binaryHash,
            GatehouseBuildTarget target) =>
            Diagnostic(
                capabilityId,
                NativeCapabilityState.Available,
                binaryHash,
                $"Validated gatehouse handler RVA 0x{target.FunctionRva:X}-0x{target.FunctionEndRva:X} with function SHA-256 {target.FunctionHash}.");

        private static NativeCapabilityDiagnostic Diagnostic(
            string capabilityId,
            NativeCapabilityState state,
            string binaryHash,
            string reason) =>
            new NativeCapabilityDiagnostic(capabilityId, state, binaryHash, reason);

        private static IReadOnlyList<NativeByteInvariant> CreateInstructionInvariants(long moduleBase, GatehouseBuildTarget target)
        {
            var result = new List<NativeByteInvariant>();
            AddBlockInvariants(result, moduleBase, target.DecisionBlockRva, target.DecisionBlockBytes,
                target.AiCloseDistanceRva, target.AiReopenDelayRva, target.HumanCloseDistanceRva);
            AddBlockInvariants(result, moduleBase, target.HumanDelayBlockRva, target.HumanDelayBlockBytes,
                target.HumanReopenDelayRva);
            return result;
        }

        private static void AddBlockInvariants(
            List<NativeByteInvariant> result,
            long moduleBase,
            int blockRva,
            byte[] bytes,
            params int[] immediateRvas)
        {
            for (int index = 0; index < bytes.Length; index++)
            {
                int rva = checked(blockRva + index);
                bool mutable = false;
                foreach (int immediateRva in immediateRvas)
                    if (rva >= immediateRva && rva < immediateRva + 4)
                        mutable = true;
                if (!mutable)
                    result.Add(new NativeByteInvariant(moduleBase + rva, bytes[index]));
            }
        }

        private static void RequireInsideFunction(GatehouseBuildTarget target, int rva, int length, string name)
        {
            if (rva < target.FunctionRva || length <= 0 || rva > target.FunctionEndRva - length)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, name + " lies outside the catalogued gatehouse function.");
        }

        private static void RequireSameSection(NativeSection section, int rva, int length, string name)
        {
            if (!section.Contains(rva, length))
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, name + " does not share the gatehouse function's executable section.");
        }

        private static void RequireBytes(ReadOnlySpan<byte> memory, int rva, byte[] expected, string target)
        {
            if (rva < 0 || rva > memory.Length - expected.Length)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, target + " lies outside the native image.");
            for (int index = 0; index < expected.Length; index++)
                if (memory[rva + index] != expected[index])
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"{target} changed at +0x{index:X}: expected=0x{expected[index]:X2}, actual=0x{memory[rva + index]:X2}.");
        }

        private static void RequireInt32(ReadOnlySpan<byte> memory, int rva, int expected, string target)
        {
            int actual = NativePeImage.ReadInt32(memory, rva);
            if (actual != expected)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"{target} changed: expected={expected}, actual={actual}.");
        }
    }

    internal readonly struct NativeByteInvariant
    {
        public NativeByteInvariant(long address, byte value)
        {
            Address = address;
            Value = value;
        }

        public long Address { get; }
        public byte Value { get; }
    }

    internal sealed class GatehouseTimingTarget
    {
        public const int VanillaAiDistance = 200;
        public const int VanillaAiDelay = 1200;
        public const int VanillaHumanDistance = 140;
        public const int VanillaHumanDelay = 100;

        public GatehouseTimingTarget(
            long aiDistance,
            long aiDelay,
            long humanDistance,
            long humanDelay,
            IReadOnlyList<NativeByteInvariant> instructionInvariants = null)
        {
            AiDistance = aiDistance;
            AiDelay = aiDelay;
            HumanDistance = humanDistance;
            HumanDelay = humanDelay;
            InstructionInvariants = instructionInvariants ?? Array.Empty<NativeByteInvariant>();
            Intervals = new[]
            {
                new NativeInterval(aiDistance, aiDistance + 4),
                new NativeInterval(aiDelay, aiDelay + 4),
                new NativeInterval(humanDistance, humanDistance + 4),
                new NativeInterval(humanDelay, humanDelay + 4)
            };
        }

        public long AiDistance { get; }
        public long AiDelay { get; }
        public long HumanDistance { get; }
        public long HumanDelay { get; }
        public IReadOnlyList<NativeInterval> Intervals { get; }
        public IReadOnlyList<NativeByteInvariant> InstructionInvariants { get; }
    }

    internal sealed class GatehouseTimingService
    {
        private const int TicksPerSecond = 40;
        private const int UnitsPerTile = 8;
        private readonly string binaryHash;
        private readonly GatehouseTimingTarget target;
        private readonly INativeMemory memory;
        private readonly NativeOwnershipRegistry ownership;
        private readonly object mutationSync;
        private readonly ManualLogSource log;
        private int expectedAiDistance = GatehouseTimingTarget.VanillaAiDistance;
        private int expectedAiDelay = GatehouseTimingTarget.VanillaAiDelay;
        private int expectedHumanDistance = GatehouseTimingTarget.VanillaHumanDistance;
        private int expectedHumanDelay = GatehouseTimingTarget.VanillaHumanDelay;

        public GatehouseTimingService(
            string binaryHash,
            GatehouseTimingTarget target,
            INativeMemory memory,
            NativeOwnershipRegistry ownership,
            object mutationSync,
            ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            this.mutationSync = mutationSync ?? throw new ArgumentNullException(nameof(mutationSync));
            this.log = log;
            VerifyExpected();
        }

        public IGatehouseTimingCapability Bind(string ownerGuid) => new OwnerCapability(this, ownerGuid);

        private bool TryApply(string ownerGuid, GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic)
        {
            int aiDistance;
            int aiDelay;
            int humanDistance;
            int humanDelay;
            try
            {
                if (settings.Enabled)
                {
                    ValidateRange(settings.HumanReopenDelaySeconds, 0.0, 30.0, nameof(settings.HumanReopenDelaySeconds));
                    ValidateRange(settings.AiReopenDelaySeconds, 0.0, 120.0, nameof(settings.AiReopenDelaySeconds));
                    ValidateRange(settings.HumanCloseDistanceTiles, 5.0, 50.0, nameof(settings.HumanCloseDistanceTiles));
                    ValidateRange(settings.AiCloseDistanceTiles, 5.0, 50.0, nameof(settings.AiCloseDistanceTiles));
                    humanDelay = ConvertNativeUInt16(settings.HumanReopenDelaySeconds, TicksPerSecond, nameof(settings.HumanReopenDelaySeconds));
                    aiDelay = ConvertNativeUInt16(settings.AiReopenDelaySeconds, TicksPerSecond, nameof(settings.AiReopenDelaySeconds));
                    humanDistance = ConvertNativeUInt16(settings.HumanCloseDistanceTiles, UnitsPerTile, nameof(settings.HumanCloseDistanceTiles));
                    aiDistance = ConvertNativeUInt16(settings.AiCloseDistanceTiles, UnitsPerTile, nameof(settings.AiCloseDistanceTiles));
                }
                else
                {
                    aiDistance = GatehouseTimingTarget.VanillaAiDistance;
                    aiDelay = GatehouseTimingTarget.VanillaAiDelay;
                    humanDistance = GatehouseTimingTarget.VanillaHumanDistance;
                    humanDelay = GatehouseTimingTarget.VanillaHumanDelay;
                }
            }
            catch (Exception ex)
            {
                diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, ex.Message);
                return false;
            }

            lock (mutationSync)
            {
                if (!ownership.TryReserve(
                        ownerGuid,
                        NativeCapabilityIds.GatehouseTiming,
                        NativeReservationMode.Exclusive,
                        target.Intervals,
                        out string conflictOwner))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.GatehouseTiming,
                        NativeCapabilityState.Conflict,
                        binaryHash,
                        "The gatehouse timing memory is already reserved by another owner.",
                        conflictOwner);
                    return false;
                }

                try
                {
                    VerifyExpected();
                    if (aiDistance == expectedAiDistance && aiDelay == expectedAiDelay &&
                        humanDistance == expectedHumanDistance && humanDelay == expectedHumanDelay)
                    {
                        diagnostic = Diagnostic(NativeCapabilityState.Available, "The requested gatehouse timing values are already active and were verified.");
                        return true;
                    }
                    WriteTransaction(aiDistance, aiDelay, humanDistance, humanDelay);
                    expectedAiDistance = aiDistance;
                    expectedAiDelay = aiDelay;
                    expectedHumanDistance = humanDistance;
                    expectedHumanDelay = humanDelay;
                    VerifyExpected();
                    string values = FormatValues(aiDistance, aiDelay, humanDistance, humanDelay);
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "Gatehouse timing values were applied and verified: " + values);
                    NativeApiLog.Info(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, enabled={settings.Enabled}, status=applied, {values}");
                    return true;
                }
                catch (Exception ex)
                {
                    diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, ex.Message);
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, status=failed, error={ex}");
                    return false;
                }
            }
        }

        private void WriteTransaction(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
        {
            int oldAiDistance = expectedAiDistance;
            int oldAiDelay = expectedAiDelay;
            int oldHumanDistance = expectedHumanDistance;
            int oldHumanDelay = expectedHumanDelay;
            GatehouseNativeMutation.Execute(
                memory,
                target.Intervals,
                VerifyExpected,
                () =>
                {
                    memory.WriteInt32(target.AiDistance, aiDistance);
                    memory.WriteInt32(target.AiDelay, aiDelay);
                    memory.WriteInt32(target.HumanDistance, humanDistance);
                    memory.WriteInt32(target.HumanDelay, humanDelay);
                    Verify(target.AiDistance, aiDistance, "AI distance");
                    Verify(target.AiDelay, aiDelay, "AI delay");
                    Verify(target.HumanDistance, humanDistance, "human distance");
                    Verify(target.HumanDelay, humanDelay, "human delay");
                },
                () =>
                {
                    memory.WriteInt32(target.AiDistance, oldAiDistance);
                    memory.WriteInt32(target.AiDelay, oldAiDelay);
                    memory.WriteInt32(target.HumanDistance, oldHumanDistance);
                    memory.WriteInt32(target.HumanDelay, oldHumanDelay);
                    Verify(target.AiDistance, oldAiDistance, "rolled-back AI distance");
                    Verify(target.AiDelay, oldAiDelay, "rolled-back AI delay");
                    Verify(target.HumanDistance, oldHumanDistance, "rolled-back human distance");
                    Verify(target.HumanDelay, oldHumanDelay, "rolled-back human delay");
                });
        }

        private void VerifyExpected()
        {
            foreach (NativeByteInvariant invariant in target.InstructionInvariants)
            {
                byte actual = memory.ReadByte(invariant.Address);
                if (actual != invariant.Value)
                    throw new InvalidOperationException($"Gatehouse instruction byte changed unexpectedly at target offset: expected=0x{invariant.Value:X2}, actual=0x{actual:X2}.");
            }
            Verify(target.AiDistance, expectedAiDistance, "AI distance");
            Verify(target.AiDelay, expectedAiDelay, "AI delay");
            Verify(target.HumanDistance, expectedHumanDistance, "human distance");
            Verify(target.HumanDelay, expectedHumanDelay, "human delay");
        }

        private void Verify(long address, int expected, string name)
        {
            int actual = memory.ReadInt32(address);
            if (actual != expected)
                throw new InvalidOperationException($"Gatehouse {name} changed unexpectedly: expected={expected}, actual={actual}.");
        }

        private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, state, binaryHash, reason);

        private static void ValidateRange(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name, $"{name} must be finite and between {minimum} and {maximum}.");
        }

        internal static int ConvertNativeUInt16(double value, int multiplier, string name)
        {
            int converted = checked((int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));
            if (converted < ushort.MinValue || converted > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(name, $"{name} converts to {converted}, outside the native UInt16 range.");
            return converted;
        }

        private static string FormatValues(int aiDistance, int aiDelay, int humanDistance, int humanDelay) =>
            $"humanClose={humanDistance / (double)UnitsPerTile:0.###}tiles/{humanDistance}units, " +
            $"humanReopen={humanDelay / (double)TicksPerSecond:0.###}s/{humanDelay}ticks, " +
            $"aiClose={aiDistance / (double)UnitsPerTile:0.###}tiles/{aiDistance}units, " +
            $"aiReopen={aiDelay / (double)TicksPerSecond:0.###}s/{aiDelay}ticks";

        private sealed class OwnerCapability : IGatehouseTimingCapability
        {
            private readonly GatehouseTimingService service;
            private readonly string ownerGuid;
            public OwnerCapability(GatehouseTimingService service, string ownerGuid) { this.service = service; this.ownerGuid = ownerGuid; }
            public bool TryApply(GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic) =>
                service.TryApply(ownerGuid, settings, out diagnostic);
        }
    }
}
