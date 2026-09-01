using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    internal sealed class SerpNativeApiRuntime : ISerpNativeApi
    {
        internal const string SupportedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int GateDecisionRva = 0xB7BBB;
        private const int GateHumanDelayRva = 0xB7C32;
        private const int SelectedCommandRva = 0x199C70;
        private const int SelectedImplementationRva = 0x11E960;
        private const int TribeManagerRva = 0x7CC6720;
        private const string GateDecisionPattern =
            "40 84 F6 75 10 41 81 F8 ?? ?? ?? ?? 7D 10 B8 ?? ?? ?? ?? EB 69 " +
            "41 81 F8 ?? ?? ?? ?? 7C 5B 48 8D 2D ?? ?? ?? ?? 49 FF C6 49 83 C3 02";
        private const string GateHumanDelayPattern =
            "EB 50 B8 ?? ?? ?? ?? 48 8D 2D ?? ?? ?? ?? 66 89 84 2B ?? ?? ?? ?? " +
            "80 BC 2B ?? ?? ?? ?? 00";
        private const string SelectedCommandPattern = "48 8D 0D A9 CA B2 07 E9 E4 4C F8 FF";

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
            ISelectedUnitCommandHookFactory hookFactory,
            ManualLogSource logger)
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
                if (moduleBase == 0 || memory.Length == 0)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The Crusader native module is unavailable.");
                if (string.IsNullOrWhiteSpace(binaryHash))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The installed CrusaderDE.dll hash is unavailable.");
                NativePeImage pe = NativePeImage.Parse(memory);
                if (!string.Equals(binaryHash, SupportedHash, StringComparison.OrdinalIgnoreCase))
                {
                    gatehouseDiagnostic = Unsupported(NativeCapabilityIds.GatehouseTiming);
                    selectedDiagnostic = Unsupported(NativeCapabilityIds.SelectedUnitCommand);
                }
                else
                {
                    var ownership = new NativeOwnershipRegistry();
                    ResolveGatehouse(moduleBase, memory, pe, nativeMemory, ownership);
                    ResolveSelectedCommand(moduleBase, memory, pe, ownership, hookFactory);
                }
            }
            catch (Exception ex)
            {
                terminalState = NativeApiState.Unavailable;
                gatehouseDiagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.GatehouseTiming, NativeCapabilityState.Faulted, binaryHash, ex.Message);
                selectedDiagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.SelectedUnitCommand, NativeCapabilityState.Faulted, binaryHash, ex.Message);
                NativeApiLog.Error(log, $"SerpNativeAPI initialization failed: build={binaryHash}, error={ex}");
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
            NativePeImage pe,
            INativeMemory nativeMemory,
            NativeOwnershipRegistry ownership)
        {
            try
            {
                int decision = NativePattern.ResolveKnownBuild(memory, pe, GateDecisionPattern, GateDecisionRva, "gatehouse decision block", true);
                int humanBlock = NativePattern.ResolveKnownBuild(memory, pe, GateHumanDelayPattern, GateHumanDelayRva, "gatehouse human delay block", true);
                if (humanBlock <= decision || humanBlock - decision > 0x100)
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The gatehouse blocks are not in the same decision region.");
                int aiDistanceRva = checked(decision + 8);
                int aiDelayRva = checked(decision + 15);
                int humanDistanceRva = checked(decision + 24);
                int humanDelayRva = checked(humanBlock + 3);
                NativeSection section = pe.RequireExecutableRange(aiDistanceRva, 4, "gatehouse AI distance");
                if (!section.Contains(aiDelayRva, 4) || !section.Contains(humanDistanceRva, 4) || !section.Contains(humanDelayRva, 4))
                    throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, "The four gatehouse values do not share one executable PE section.");
                RequireInt32(memory, aiDistanceRva, GatehouseTimingTarget.VanillaAiDistance, "gatehouse AI distance");
                RequireInt32(memory, aiDelayRva, GatehouseTimingTarget.VanillaAiDelay, "gatehouse AI delay");
                RequireInt32(memory, humanDistanceRva, GatehouseTimingTarget.VanillaHumanDistance, "gatehouse human distance");
                RequireInt32(memory, humanDelayRva, GatehouseTimingTarget.VanillaHumanDelay, "gatehouse human delay");
                var target = new GatehouseTimingTarget(
                    moduleBase + aiDistanceRva,
                    moduleBase + aiDelayRva,
                    moduleBase + humanDistanceRva,
                    moduleBase + humanDelayRva);
                gatehouse = new GatehouseTimingService(binaryHash, target, nativeMemory, ownership, log);
                gatehouseDiagnostic = Available(NativeCapabilityIds.GatehouseTiming, "The catalogued gatehouse timing target was validated.");
            }
            catch (NativeResolutionException ex)
            {
                gatehouseDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                gatehouseDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, NativeCapabilityState.Faulted, binaryHash, ex.Message);
            }
        }

        private void ResolveSelectedCommand(
            long moduleBase,
            ReadOnlySpan<byte> memory,
            NativePeImage pe,
            NativeOwnershipRegistry ownership,
            ISelectedUnitCommandHookFactory hookFactory)
        {
            try
            {
                int rva = NativePattern.ResolveKnownBuild(memory, pe, SelectedCommandPattern, SelectedCommandRva, "selected-unit command", true);
                int tribeManager = NativePattern.ResolveRelativeTarget(memory, pe, rva + 3, rva + 7, "selected-unit tribe manager");
                int implementation = NativePattern.ResolveRelativeTarget(memory, pe, rva + 8, rva + 12, "selected-unit implementation");
                if (rva != SelectedCommandRva || tribeManager != TribeManagerRva || implementation != SelectedImplementationRva)
                    throw new NativeResolutionException(
                        NativeCapabilityState.ValidationFailed,
                        $"Selected-unit targets differ from the catalog: entry=0x{rva:X}, tribeManager=0x{tribeManager:X}, implementation=0x{implementation:X}.");
                pe.RequireMappedRange(tribeManager, 1, "selected-unit tribe manager");
                pe.RequireExecutableRange(implementation, 1, "selected-unit implementation");
                selectedCommand = new SelectedUnitCommandService(binaryHash, moduleBase + rva, 12, ownership, hookFactory, log);
                selectedDiagnostic = Available(NativeCapabilityIds.SelectedUnitCommand, "The catalogued selected-unit command target was validated.");
            }
            catch (NativeResolutionException ex)
            {
                selectedDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, ex.State, binaryHash, ex.Message);
            }
            catch (Exception ex)
            {
                selectedDiagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.SelectedUnitCommand, NativeCapabilityState.Faulted, binaryHash, ex.Message);
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

        private void RequireInt32(ReadOnlySpan<byte> memory, int rva, int expected, string target)
        {
            int actual = NativePeImage.ReadInt32(memory, rva);
            if (actual != expected)
                throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"{target} changed: expected={expected}, actual={actual}.");
        }

        private NativeCapabilityDiagnostic Unsupported(string capabilityId) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.UnsupportedBuild, binaryHash, "The installed CrusaderDE.dll hash is not present in the compiled target catalog.");
        private NativeCapabilityDiagnostic Available(string capabilityId, string reason) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Available, binaryHash, reason);
        private static NativeCapabilityDiagnostic Pending(string capabilityId) =>
            new NativeCapabilityDiagnostic(capabilityId, NativeCapabilityState.Pending, string.Empty, "SerpNativeAPI has not completed native initialization.");
    }
}
