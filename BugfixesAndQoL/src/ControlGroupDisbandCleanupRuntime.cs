// Remove a unit from local control groups immediately after Vanilla processes its disband.
using BepInEx.Logging;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class ControlGroupDisbandCleanupRuntime : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate byte DisbandUnitDelegate(IntPtr unitManager, int unitId, byte playSound);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly IUnitHudPresentationCapability unitHud;
        private HookTransaction transaction;
        private readonly DetourHandle<DisbandUnitDelegate> detour =
            new DetourHandle<DisbandUnitDelegate>();
        private bool callbackErrorLogged;

        internal ControlGroupDisbandCleanupRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (region == null)
                throw new ArgumentNullException(nameof(region));
            if (memory.IsEmpty)
                throw new ArgumentException("The loaded CrusaderDE image is empty.", nameof(memory));
            if (libraryBase == 0)
                throw new ArgumentOutOfRangeException(nameof(libraryBase));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The loaded CrusaderDE.dll does not match the audited native baseline.");

            int disbandFunctionRva = ValidateDisbandTarget(memory);
            if (!ApiShared.Current.TryGetUnitHudPresentation(
                    BugfixesAndQoLPlugin.PluginGuid,
                    out unitHud,
                    out NativeCapabilityDiagnostic diagnostic))
            {
                throw new InvalidOperationException(
                    "APIShared Unit HUD control-group access is unavailable: " + diagnostic?.Reason);
            }

            try
            {
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                transaction.AddDetour(
                    detour,
                    HookTarget.FromAddress(libraryBase + unchecked((ulong)disbandFunctionRva)),
                    DisbandUnit);
                CommitResult commitResult = transaction.Commit();
                if (!commitResult.IsCompleteSuccess || !detour.Success)
                    throw new InvalidOperationException("The disband cleanup detour was not installed.");
            }
            catch
            {
                transaction?.Dispose();
                transaction = null;
                throw;
            }
        }

        public void Dispose()
        {
            transaction?.Dispose();
            transaction = null;
        }

        private byte DisbandUnit(IntPtr unitManager, int unitId, byte playSound)
        {
            byte result = detour.Original(unitManager, unitId, playSound);
            if (!ControlGroupDisbandCleanupPolicy.ShouldClean(
                    settings.EnableClientFeatures,
                    settings.EnableDisbandedUnitControlGroupCleanup))
            {
                return result;
            }

            try
            {
                if (!unitHud.TryRemoveUnitFromControlGroups(
                        unitId,
                        out _,
                        out NativeCapabilityDiagnostic diagnostic))
                {
                    throw new InvalidOperationException(diagnostic?.Reason ??
                        "APIShared rejected the control-group cleanup.");
                }
            }
            catch (Exception ex)
            {
                if (!callbackErrorLogged)
                {
                    callbackErrorLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL disbanded-unit control-group cleanup failed; Vanilla behavior remains active: {ex}");
                }
            }
            return result;
        }

        private static int ValidateDisbandTarget(ReadOnlySpan<byte> memory)
        {
            ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.DisbandDispatcherInstructions,
                ControlGroupNativeDefinition.DisbandDispatcherRva,
                "UIT_DISBAND unit-type dispatcher");
            ResolveUniquePattern(
                memory,
                ControlGroupNativeDefinition.DisbandBranchInstructions,
                ControlGroupNativeDefinition.DisbandBranchRva,
                "UIT_DISBAND normal-unit block");

            int callRva = ControlGroupNativeDefinition.DisbandCallRva;
            if (memory[callRva] != 0xE8)
                throw new InvalidOperationException("The audited UIT_DISBAND helper call opcode is missing.");
            int targetRva = checked(
                callRva + 5 + Shared.NativePatternResolver.ReadInt32(memory, callRva + 1));
            if (targetRva != ControlGroupNativeDefinition.DisbandFunctionRva)
            {
                throw new InvalidOperationException(
                    $"The UIT_DISBAND call targets RVA 0x{targetRva:X}, expected RVA 0x{ControlGroupNativeDefinition.DisbandFunctionRva:X}.");
            }
            return targetRva;
        }

        private static int ResolveUniquePattern(
            ReadOnlySpan<byte> memory,
            string pattern,
            int expectedRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(memory, pattern, label);
            if (resolvedRva != expectedRva)
                throw new InvalidOperationException($"The {label} resolved to RVA 0x{resolvedRva:X}, not audited RVA 0x{expectedRva:X}.");
            return resolvedRva;
        }
    }
}
