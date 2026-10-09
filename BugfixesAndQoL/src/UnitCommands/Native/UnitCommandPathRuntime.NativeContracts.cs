using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal APIShared.Internal.NativeResolution Resolve(
            ReadOnlySpan<byte> memory, string pattern, int expectedRva, string label)
        {
            APIShared.Internal.NativeResolution resolution = APIShared.Internal.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                expectedRva,
                referenceHashMatches: true,
                name: label,
                log: null);
            if (resolution.Rva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} resolved to 0x{resolution.Rva:X} instead of 0x{expectedRva:X}.");
            }
            return resolution;
        }

        internal static void ValidateExactBytes(
            ReadOnlySpan<byte> memory, int rva, byte[] expected, string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The validated instruction bytes for {label} did not match CrusaderDE.dll.");
            }
        }

        internal static void ValidateGameUnitFieldOffset(string fieldName, int expectedOffset)
        {
            ValidateStructFieldOffset(typeof(GameUnit), fieldName, expectedOffset);
        }

        internal static void ValidateStructFieldOffset(Type structType, string fieldName, int expectedOffset)
        {
            int actualOffset = Marshal.OffsetOf(structType, fieldName).ToInt32();
            if (actualOffset != expectedOffset)
            {
                throw new InvalidOperationException(
                    $"Unexpected {structType.Name}.{fieldName} offset 0x{actualOffset:X}; " +
                    $"expected 0x{expectedOffset:X}.");
            }
        }

        internal static void ValidateCallTarget(
            ReadOnlySpan<byte> memory,
            int callRva,
            int expectedTargetRva,
            byte[] expectedBytes,
            string label)
        {
            ValidateExactBytes(memory, callRva, expectedBytes, label);
            if (expectedBytes.Length != 5 || expectedBytes[0] != 0xE8)
                throw new InvalidOperationException($"The validated {label} is not a near CALL.");

            int targetRva = APIShared.Internal.NativePatternResolver.ResolveRelativeTarget(
                memory, callRva + 1, callRva + 5);
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"The validated {label} targets 0x{targetRva:X} instead of 0x{expectedTargetRva:X}.");
            }
        }

        internal static bool IsValidTileId(int tileId) =>
            tileId >= 0 && tileId < NativeTileCount;

        internal bool ResolveNativeSpecialStructure(int tileId) =>
            IsValidTileId(tileId) && nativeSpecialStructurePredicate != null &&
            nativeSpecialStructurePredicate(nativeSpecialStructureContext, tileId);

        internal HookTransaction CreateOwnedHookTransaction()
        {
            return new HookTransaction(
                nativeRegion,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    // Published hooks remain process-owned permanently.
                    OwnsHooks = true,
                    Backend = APIShared.AssassinPathAPI.Backend
                });
        }

        internal RedBirdDetour<TDelegate> AddDetour<TDelegate>(
            HookTransaction transaction,
            ulong targetAddress,
            TDelegate callback,
            IDetourIntermediaryFactory intermediaryFactory = null)
            where TDelegate : Delegate
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));
            var detour = new RedBirdDetour<TDelegate>(targetAddress);
            PrepareNativeDetour(transaction, detour, targetAddress, callback, intermediaryFactory);
            if (intermediaryFactory == null)
                transaction.AddDetour(
                    detour.Handle,
                    HookTarget.FromAddress(targetAddress),
                    callback);
            else
                transaction.AddDetour(
                    detour.Handle,
                    HookTarget.FromAddress(targetAddress),
                    callback,
                    intermediaryFactory);
            return detour;
        }

        internal void ValidateMoatModeDetourCandidate(ulong targetAddress)
        {
            byte[] expectedPrefix =
            {
                0x48, 0x63, 0xC2, 0x48, 0x69, 0xD0, 0x90, 0x04, 0x00, 0x00
            };
            for (int index = 0; index < expectedPrefix.Length; index++)
            {
                if (Marshal.ReadByte(new IntPtr(unchecked((long)targetAddress)), index) !=
                    expectedPrefix[index])
                    throw new InvalidOperationException(
                        "The installed moat-mode prologue differs from the audited 10 bytes.");
            }

            var request = new DetourRequest<UnitStandingOnCompletedMoatDelegate>
            {
                Name = "BugfixesAndQoL moat-mode contract preflight",
                TargetAddress = targetAddress,
                Callback = rootedUnitStandingOnCompletedMoat,
                IntermediaryFactory = MoatModeFlagIntermediaryFactory.Instance
            };
            var candidate = APIShared.AssassinPathAPI.Backend.CreateDetour(in request)
                as NativeDetour<UnitStandingOnCompletedMoatDelegate>;
            if (candidate == null)
                throw new InvalidOperationException("The installed moat-mode backend is not NativeX64.");
            using (candidate)
                MoatModeFlagIntermediaryFactory.ValidateNativeHook(
                    candidate, targetAddress,
                    Marshal.GetFunctionPointerForDelegate(rootedUnitStandingOnCompletedMoat),
                    installed: false);
        }

    }
}
