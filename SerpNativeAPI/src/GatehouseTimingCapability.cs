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
        /// Gets whether custom timing values should be applied. False restores the four Vanilla
        /// timing values while retaining the capability-wide centered distance origin.
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
    /// Applies a validated, transactional gatehouse configuration and activates the centered
    /// distance origin on the first successful application.
    /// </summary>
    public interface IGatehouseTimingCapability
    {
        /// <summary>Attempts to apply the centered distance block and all gatehouse values as one transaction.</summary>
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
            ManualLogSource log,
            GatehouseBuildTarget target,
            out GatehouseTimingService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            try
            {
                if (!string.Equals(binaryHash, target.BuildHash, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.GatehouseTiming,
                        NativeCapabilityState.UnsupportedBuild,
                        binaryHash,
                        "The installed CrusaderDE.dll hash is not present in the compiled target catalog.");
                    return;
                }
                if (moduleBase == 0 || memory.Length == 0)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Crusader native module is unavailable.");
                if (nativeMemory == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native memory adapter is unavailable.");

                NativePeImage pe = NativePeImage.Parse(memory);
                NativeSection functionSection = pe.RequireExecutableRange(target.FunctionRva, target.FunctionSize, "gatehouse handler function");
                if (target.VanillaDistanceBlockBytes == null || target.CenteredDistanceBlockBytes == null ||
                    target.VanillaDistanceBlockBytes.Length != target.CenteredDistanceBlockBytes.Length)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The gatehouse distance patch catalog is incomplete or has mismatched block lengths.");
                RequireInsideFunction(target, target.DistanceBlockRva, target.VanillaDistanceBlockBytes.Length, "gatehouse distance block");
                RequireInsideFunction(target, target.DecisionBlockRva, target.DecisionBlockBytes.Length, "gatehouse decision block");
                RequireInsideFunction(target, target.HumanDelayBlockRva, target.HumanDelayBlockBytes.Length, "gatehouse human delay block");
                RequireInsideFunction(target, target.AiCloseDistanceRva, 4, "gatehouse AI close distance");
                RequireInsideFunction(target, target.AiReopenDelayRva, 4, "gatehouse AI reopen delay");
                RequireInsideFunction(target, target.HumanCloseDistanceRva, 4, "gatehouse human close distance");
                RequireInsideFunction(target, target.HumanReopenDelayRva, 4, "gatehouse human reopen delay");
                RequireSameSection(functionSection, target, "gatehouse targets");

                string actualFunctionHash = SerpNativeApiRuntime.ComputeSha256(memory.Slice(target.FunctionRva, target.FunctionSize));
                if (!string.Equals(actualFunctionHash, target.FunctionHash, StringComparison.OrdinalIgnoreCase))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"The gatehouse handler function hash changed: expected={target.FunctionHash}, actual={actualFunctionHash}.");
                RequireBytes(memory, target.DistanceBlockRva, target.VanillaDistanceBlockBytes, "gatehouse distance block");
                RequireBytes(memory, target.DecisionBlockRva, target.DecisionBlockBytes, "gatehouse decision block");
                RequireBytes(memory, target.HumanDelayBlockRva, target.HumanDelayBlockBytes, "gatehouse human delay block");
                RequireInt32(memory, target.AiCloseDistanceRva, GatehouseTimingTarget.VanillaAiDistance, "gatehouse AI close distance");
                RequireInt32(memory, target.AiReopenDelayRva, GatehouseTimingTarget.VanillaAiDelay, "gatehouse AI reopen delay");
                RequireInt32(memory, target.HumanCloseDistanceRva, GatehouseTimingTarget.VanillaHumanDistance, "gatehouse human close distance");
                RequireInt32(memory, target.HumanReopenDelayRva, GatehouseTimingTarget.VanillaHumanDelay, "gatehouse human reopen delay");

                IReadOnlyList<NativeByteInvariant> invariants = CreateInstructionInvariants(moduleBase, target);
                var memoryTarget = new GatehouseTimingTarget(
                    moduleBase + target.DistanceBlockRva,
                    target.VanillaDistanceBlockBytes,
                    target.CenteredDistanceBlockBytes,
                    moduleBase + target.AiCloseDistanceRva,
                    moduleBase + target.AiReopenDelayRva,
                    moduleBase + target.HumanCloseDistanceRva,
                    moduleBase + target.HumanReopenDelayRva,
                    invariants);
                service = new GatehouseTimingService(binaryHash, memoryTarget, nativeMemory, ownership, log);
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.GatehouseTiming,
                    NativeCapabilityState.Available,
                    binaryHash,
                    $"Validated gatehouse handler RVA 0x{target.FunctionRva:X}-0x{target.FunctionEndRva:X} with function SHA-256 {target.FunctionHash}.");
            }
            catch (NativeResolutionException ex)
            {
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, NativeCapabilityState.Faulted, binaryHash, ex.Message);
            }
        }

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

        private static void RequireSameSection(NativeSection section, GatehouseBuildTarget target, string name)
        {
            int[] starts = { target.DistanceBlockRva, target.DecisionBlockRva, target.HumanDelayBlockRva, target.AiCloseDistanceRva,
                target.AiReopenDelayRva, target.HumanCloseDistanceRva, target.HumanReopenDelayRva };
            foreach (int start in starts)
                if (!section.Contains(start, 4))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, name + " do not share the gatehouse function's executable section.");
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
            long distanceBlock,
            byte[] vanillaDistanceBytes,
            byte[] centeredDistanceBytes,
            long aiDistance,
            long aiDelay,
            long humanDistance,
            long humanDelay,
            IReadOnlyList<NativeByteInvariant> instructionInvariants = null)
        {
            if (vanillaDistanceBytes == null || centeredDistanceBytes == null || vanillaDistanceBytes.Length == 0 ||
                vanillaDistanceBytes.Length != centeredDistanceBytes.Length)
                throw new ArgumentException("Gatehouse distance blocks must be non-empty and have equal lengths.");
            DistanceBlock = distanceBlock;
            VanillaDistanceBytes = (byte[])vanillaDistanceBytes.Clone();
            CenteredDistanceBytes = (byte[])centeredDistanceBytes.Clone();
            AiDistance = aiDistance;
            AiDelay = aiDelay;
            HumanDistance = humanDistance;
            HumanDelay = humanDelay;
            InstructionInvariants = instructionInvariants ?? Array.Empty<NativeByteInvariant>();
            Intervals = new[]
            {
                new NativeInterval(distanceBlock, checked(distanceBlock + vanillaDistanceBytes.Length)),
                new NativeInterval(aiDistance, aiDistance + 4),
                new NativeInterval(aiDelay, aiDelay + 4),
                new NativeInterval(humanDistance, humanDistance + 4),
                new NativeInterval(humanDelay, humanDelay + 4)
            };
        }

        public long DistanceBlock { get; }
        public byte[] VanillaDistanceBytes { get; }
        public byte[] CenteredDistanceBytes { get; }
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
        private readonly object sync = new object();
        private readonly string binaryHash;
        private readonly GatehouseTimingTarget target;
        private readonly INativeMemory memory;
        private readonly NativeOwnershipRegistry ownership;
        private readonly ManualLogSource log;
        private int expectedAiDistance = GatehouseTimingTarget.VanillaAiDistance;
        private int expectedAiDelay = GatehouseTimingTarget.VanillaAiDelay;
        private int expectedHumanDistance = GatehouseTimingTarget.VanillaHumanDistance;
        private int expectedHumanDelay = GatehouseTimingTarget.VanillaHumanDelay;
        private bool midpointPatchActive;

        public GatehouseTimingService(
            string binaryHash,
            GatehouseTimingTarget target,
            INativeMemory memory,
            NativeOwnershipRegistry ownership,
            ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.target = target;
            this.memory = memory;
            this.ownership = ownership;
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

            lock (sync)
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
                    if (midpointPatchActive && aiDistance == expectedAiDistance && aiDelay == expectedAiDelay &&
                        humanDistance == expectedHumanDistance && humanDelay == expectedHumanDelay)
                    {
                        diagnostic = Diagnostic(NativeCapabilityState.Available, "The requested gatehouse values and centered distance origin are already active and were verified.");
                        return true;
                    }
                    WriteTransaction(aiDistance, aiDelay, humanDistance, humanDelay);
                    expectedAiDistance = aiDistance;
                    expectedAiDelay = aiDelay;
                    expectedHumanDistance = humanDistance;
                    expectedHumanDelay = humanDelay;
                    midpointPatchActive = true;
                    VerifyExpected();
                    string values = FormatValues(aiDistance, aiDelay, humanDistance, humanDelay);
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "Gatehouse centered distance origin and values were applied and verified: " + values);
                    NativeApiLog.Info(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, enabled={settings.Enabled}, distanceOrigin=center, status=applied, {values}");
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
            byte[] oldDistanceBytes = midpointPatchActive ? target.CenteredDistanceBytes : target.VanillaDistanceBytes;
            List<PageProtection> protections = AcquireWritablePages();
            Exception primary = null;
            Exception cleanup = null;
            bool writesStarted = false;
            try
            {
                try
                {
                    // Recheck after acquiring write access so a change between the public
                    // preflight and the transaction cannot be silently adopted as rollback state.
                    VerifyExpected();
                    writesStarted = true;
                    WriteBytes(target.DistanceBlock, target.CenteredDistanceBytes);
                    memory.WriteInt32(target.AiDistance, aiDistance);
                    memory.WriteInt32(target.AiDelay, aiDelay);
                    memory.WriteInt32(target.HumanDistance, humanDistance);
                    memory.WriteInt32(target.HumanDelay, humanDelay);
                    VerifyBytes(target.DistanceBlock, target.CenteredDistanceBytes, "centered distance block");
                    Verify(target.AiDistance, aiDistance, "AI distance");
                    Verify(target.AiDelay, aiDelay, "AI delay");
                    Verify(target.HumanDistance, humanDistance, "human distance");
                    Verify(target.HumanDelay, humanDelay, "human delay");
                }
                catch (Exception ex)
                {
                    primary = ex;
                    if (writesStarted)
                    {
                        try
                        {
                            WriteBytes(target.DistanceBlock, oldDistanceBytes);
                            memory.WriteInt32(target.AiDistance, oldAiDistance);
                            memory.WriteInt32(target.AiDelay, oldAiDelay);
                            memory.WriteInt32(target.HumanDistance, oldHumanDistance);
                            memory.WriteInt32(target.HumanDelay, oldHumanDelay);
                            VerifyBytes(target.DistanceBlock, oldDistanceBytes, "rolled-back distance block");
                            Verify(target.AiDistance, oldAiDistance, "rolled-back AI distance");
                            Verify(target.AiDelay, oldAiDelay, "rolled-back AI delay");
                            Verify(target.HumanDistance, oldHumanDistance, "rolled-back human distance");
                            Verify(target.HumanDelay, oldHumanDelay, "rolled-back human delay");
                        }
                        catch (Exception rollback)
                        {
                            primary = new AggregateException("The native write and rollback both failed.", primary, rollback);
                        }
                    }
                }
            }
            finally
            {
                for (int index = protections.Count - 1; index >= 0; index--)
                {
                    PageProtection protection = protections[index];
                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
                foreach (NativeInterval interval in target.Intervals)
                {
                    try { memory.Flush(interval.Start, checked((int)(interval.End - interval.Start))); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
            }

            if (primary != null && cleanup != null)
                throw new AggregateException("The gatehouse transaction and cleanup both failed.", primary, cleanup);
            if (primary != null)
                throw primary;
            if (cleanup != null)
                throw cleanup;
        }

        private List<PageProtection> AcquireWritablePages()
        {
            if (memory.PageSize <= 0)
                throw new InvalidOperationException("The native memory adapter returned an invalid page size.");

            var pages = new SortedSet<long>();
            foreach (NativeInterval interval in target.Intervals)
            {
                long firstPage = PageStart(interval.Start, memory.PageSize);
                long lastPage = PageStart(interval.End - 1, memory.PageSize);
                for (long page = firstPage; page <= lastPage; page = checked(page + memory.PageSize))
                    pages.Add(page);
            }

            var protections = new List<PageProtection>();
            try
            {
                foreach (long page in pages)
                    protections.Add(new PageProtection(page, memory.MakeWritable(page, memory.PageSize)));
                return protections;
            }
            catch (Exception primary)
            {
                Exception cleanup = null;
                for (int index = protections.Count - 1; index >= 0; index--)
                {
                    PageProtection protection = protections[index];
                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
                if (cleanup != null)
                    throw new AggregateException("Acquiring writable native pages and cleanup both failed.", primary, cleanup);
                throw;
            }
        }

        private void VerifyExpected()
        {
            foreach (NativeByteInvariant invariant in target.InstructionInvariants)
            {
                byte actual = memory.ReadByte(invariant.Address);
                if (actual != invariant.Value)
                    throw new InvalidOperationException($"Gatehouse instruction byte changed unexpectedly at target offset: expected=0x{invariant.Value:X2}, actual=0x{actual:X2}.");
            }
            VerifyBytes(target.DistanceBlock,
                midpointPatchActive ? target.CenteredDistanceBytes : target.VanillaDistanceBytes,
                midpointPatchActive ? "centered distance block" : "Vanilla distance block");
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

        private void WriteBytes(long address, byte[] values)
        {
            for (int index = 0; index < values.Length; index++)
                memory.WriteByte(address + index, values[index]);
        }

        private void VerifyBytes(long address, byte[] expected, string name)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                byte actual = memory.ReadByte(address + index);
                if (actual != expected[index])
                    throw new InvalidOperationException($"Gatehouse {name} changed unexpectedly at +0x{index:X}: expected=0x{expected[index]:X2}, actual=0x{actual:X2}.");
            }
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

        internal static int ComputeCenteredDistanceNative(
            int beginX,
            int beginY,
            int endX,
            int endY,
            int unitX,
            int unitY)
        {
            int dx = Math.Abs(checked((beginX + endX) * 4 - unitX));
            int dy = Math.Abs(checked((beginY + endY) * 4 - unitY));
            return Math.Max(dx, dy);
        }

        private static long PageStart(long address, int pageSize) => address - address % pageSize;

        private static Exception Combine(Exception current, Exception next) =>
            current == null ? next : new AggregateException(current, next);

        private static string FormatValues(int aiDistance, int aiDelay, int humanDistance, int humanDelay) =>
            $"humanClose={humanDistance / (double)UnitsPerTile:0.###}tiles/{humanDistance}units, " +
            $"humanReopen={humanDelay / (double)TicksPerSecond:0.###}s/{humanDelay}ticks, " +
            $"aiClose={aiDistance / (double)UnitsPerTile:0.###}tiles/{aiDistance}units, " +
            $"aiReopen={aiDelay / (double)TicksPerSecond:0.###}s/{aiDelay}ticks";

        private readonly struct PageProtection
        {
            public PageProtection(long address, uint protection)
            {
                Address = address;
                Protection = protection;
            }

            public long Address { get; }
            public uint Protection { get; }
        }

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
