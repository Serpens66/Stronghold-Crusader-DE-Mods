using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace SerpNativeAPI
{
    internal sealed class GatehouseBuildTarget
    {
        public static GatehouseBuildTarget Supported { get; } = new GatehouseBuildTarget(
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2",
            0xB73D0,
            2325,
            "F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8",
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

    internal sealed class SerpNativeApiRuntime : ISerpNativeApi
    {
        internal const string SupportedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        private readonly object sync = new object();
        private readonly List<Action<ISerpNativeApi>> readyCallbacks = new List<Action<ISerpNativeApi>>();
        private NativeApiState state;
        private string binaryHash = string.Empty;
        private GatehouseTimingService gatehouse;
        private SelectedUnitCommandService selectedCommand;
        private NativeCapabilityDiagnostic gatehouseDiagnostic = Pending(NativeCapabilityIds.GatehouseTiming);
        private NativeCapabilityDiagnostic selectedDiagnostic = Pending(NativeCapabilityIds.SelectedUnitCommand);
        private ManualLogSource log;

        internal static SerpNativeApiRuntime ProcessInstance { get; } = new SerpNativeApiRuntime();

        public NativeApiState State { get { lock (sync) return state; } }

        public void WhenReady(Action<ISerpNativeApi> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            lock (sync)
            {
                if (state == NativeApiState.Pending)
                {
                    readyCallbacks.Add(callback);
                    return;
                }
            }
            callback(this);
        }

        internal void Initialize(
            long moduleBase,
            ReadOnlySpan<byte> memory,
            string hash,
            INativeMemory nativeMemory,
            ISelectedUnitCommandEventSource eventSource,
            ManualLogSource logger,
            GatehouseBuildTarget gateTarget = null)
        {
            lock (sync)
            {
                if (state != NativeApiState.Pending)
                    return;
                binaryHash = hash ?? string.Empty;
                log = logger;
            }

            NativeApiState terminalState = NativeApiState.Ready;
            try
            {
                ResolveSelectedCommand(eventSource);
                ResolveGatehouse(moduleBase, memory, nativeMemory, new NativeOwnershipRegistry(), gateTarget ?? GatehouseBuildTarget.Supported);
            }
            catch (Exception ex)
            {
                // Capability resolvers contain their own error boundaries. Reaching this catch
                // means publication itself failed and the API cannot be trusted globally.
                terminalState = NativeApiState.Unavailable;
                gatehouse = null;
                selectedCommand = null;
                gatehouseDiagnostic = Faulted(NativeCapabilityIds.GatehouseTiming, ex.Message);
                selectedDiagnostic = Faulted(NativeCapabilityIds.SelectedUnitCommand, ex.Message);
                NativeApiLog.Error(log, $"SerpNativeAPI initialization failed globally: build={binaryHash}, error={ex}");
            }

            Action<ISerpNativeApi>[] callbacks;
            lock (sync)
            {
                state = terminalState;
                callbacks = readyCallbacks.ToArray();
                readyCallbacks.Clear();
            }
            NativeApiLog.Info(log, $"SerpNativeAPI initialized: state={terminalState}, build={binaryHash}, gatehouse={gatehouseDiagnostic.State}, selectedUnitCommand={selectedDiagnostic.State}.");
            foreach (Action<ISerpNativeApi> callback in callbacks)
            {
                try { callback(this); }
                catch (Exception ex) { NativeApiLog.Error(log, $"SerpNativeAPI readiness callback failed: build={binaryHash}, error={ex}"); }
            }
        }

        public bool TryGetGatehouseTiming(string ownerGuid, out IGatehouseTimingCapability capability, out NativeCapabilityDiagnostic diagnostic)
        {
            capability = null;
            if (!ValidateOwner(ownerGuid, NativeCapabilityIds.GatehouseTiming, out diagnostic))
                return false;
            lock (sync)
            {
                if (gatehouse == null)
                {
                    diagnostic = gatehouseDiagnostic;
                    return false;
                }
                capability = gatehouse.Bind(ownerGuid);
                diagnostic = gatehouseDiagnostic;
                return true;
            }
        }

        public bool TryGetSelectedUnitCommand(string ownerGuid, out ISelectedUnitCommandCapability capability, out NativeCapabilityDiagnostic diagnostic)
        {
            capability = null;
            if (!ValidateOwner(ownerGuid, NativeCapabilityIds.SelectedUnitCommand, out diagnostic))
                return false;
            lock (sync)
            {
                if (selectedCommand == null)
                {
                    diagnostic = selectedDiagnostic;
                    return false;
                }
                capability = selectedCommand.Bind(ownerGuid);
                diagnostic = selectedDiagnostic;
                return true;
            }
        }

        private void ResolveGatehouse(
            long moduleBase,
            ReadOnlySpan<byte> memory,
            INativeMemory nativeMemory,
            NativeOwnershipRegistry ownership,
            GatehouseBuildTarget target)
        {
            try
            {
                if (!string.Equals(binaryHash, target.BuildHash, StringComparison.OrdinalIgnoreCase))
                {
                    gatehouseDiagnostic = Unsupported(NativeCapabilityIds.GatehouseTiming);
                    return;
                }
                if (moduleBase == 0 || memory.Length == 0)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Crusader native module is unavailable.");
                if (nativeMemory == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The native memory adapter is unavailable.");

                NativePeImage pe = NativePeImage.Parse(memory);
                NativeSection functionSection = pe.RequireExecutableRange(target.FunctionRva, target.FunctionSize, "gatehouse handler function");
                RequireInsideFunction(target, target.DecisionBlockRva, target.DecisionBlockBytes.Length, "gatehouse decision block");
                RequireInsideFunction(target, target.HumanDelayBlockRva, target.HumanDelayBlockBytes.Length, "gatehouse human delay block");
                RequireInsideFunction(target, target.AiCloseDistanceRva, 4, "gatehouse AI close distance");
                RequireInsideFunction(target, target.AiReopenDelayRva, 4, "gatehouse AI reopen delay");
                RequireInsideFunction(target, target.HumanCloseDistanceRva, 4, "gatehouse human close distance");
                RequireInsideFunction(target, target.HumanReopenDelayRva, 4, "gatehouse human reopen delay");
                RequireSameSection(functionSection, target, "gatehouse targets");

                string actualFunctionHash = ComputeSha256(memory.Slice(target.FunctionRva, target.FunctionSize));
                if (!string.Equals(actualFunctionHash, target.FunctionHash, StringComparison.OrdinalIgnoreCase))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"The gatehouse handler function hash changed: expected={target.FunctionHash}, actual={actualFunctionHash}.");
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
                gatehouse = new GatehouseTimingService(binaryHash, memoryTarget, nativeMemory, ownership, log);
                gatehouseDiagnostic = Available(NativeCapabilityIds.GatehouseTiming,
                    $"Validated gatehouse handler RVA 0x{target.FunctionRva:X}-0x{target.FunctionEndRva:X} with function SHA-256 {target.FunctionHash}.");
            }
            catch (NativeResolutionException ex)
            {
                gatehouseDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                gatehouseDiagnostic = Faulted(NativeCapabilityIds.GatehouseTiming, ex.Message);
            }
        }

        private void ResolveSelectedCommand(ISelectedUnitCommandEventSource eventSource)
        {
            try
            {
                if (eventSource == null)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Script Extender selected-unit event source is unavailable.");
                selectedCommand = new SelectedUnitCommandService(binaryHash, eventSource, log);
                selectedDiagnostic = Available(NativeCapabilityIds.SelectedUnitCommand,
                    "Provided through the Script Extender OnTribeIssueOrderWithTarget Pre event; SerpNativeAPI installs no native detour.");
            }
            catch (NativeResolutionException ex)
            {
                selectedDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                selectedDiagnostic = Faulted(NativeCapabilityIds.SelectedUnitCommand, ex.Message);
            }
        }

        private bool ValidateOwner(string ownerGuid, string capabilityId, out NativeCapabilityDiagnostic diagnostic)
        {
            lock (sync)
            {
                if (state == NativeApiState.Pending)
                {
                    diagnostic = Pending(capabilityId);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(ownerGuid))
                {
                    diagnostic = new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.ValidationFailed, binaryHash, "A non-empty BepInEx owner GUID is required.");
                    return false;
                }
            }
            diagnostic = null;
            return true;
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
            int[] starts = { target.DecisionBlockRva, target.HumanDelayBlockRva, target.AiCloseDistanceRva,
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

        internal static string ComputeSha256(ReadOnlySpan<byte> bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", string.Empty);
        }

        private NativeCapabilityDiagnostic Unsupported(string capabilityId) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.UnsupportedBuild, binaryHash, "The installed CrusaderDE.dll hash is not present in the compiled target catalog.");
        private NativeCapabilityDiagnostic Available(string capabilityId, string reason) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Available, binaryHash, reason);
        private NativeCapabilityDiagnostic Faulted(string capabilityId, string reason) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Faulted, binaryHash, reason);
        private static NativeCapabilityDiagnostic Pending(string capabilityId) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Pending, string.Empty, "SerpNativeAPI has not completed native initialization.");
    }
}
