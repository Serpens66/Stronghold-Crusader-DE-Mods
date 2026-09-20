// Feature: Extend every audited Vanilla peace-time gameplay gate to non-MP modes.
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class VanillaPeaceTimeGameplayPatch
    {
        private readonly HookHandle<X64InlineHook> startingTroopsHook =
            new HookHandle<X64InlineHook>();
        private readonly List<HookHandle<X64AssemblyPatch>> gameplayPatches =
            new List<HookHandle<X64AssemblyPatch>>();
        private readonly HookTransaction transaction;

        internal VanillaPeaceTimeGameplayPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || region == null || memory.Length == 0)
                throw new ArgumentException("The Crusader native library is unavailable.");

            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
            VanillaPeaceTimeNativeContract.Validate(memory, imageBase, referenceHashMatches);

            HookTransaction pending = null;
            try
            {
                pending = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                foreach (VanillaPeaceTimePatchSite site in VanillaPeaceTimeNativeContract.PatchSites)
                {
                    HookHandle<X64AssemblyPatch> handle = new HookHandle<X64AssemblyPatch>();
                    VanillaPeaceTimePatchSite capturedSite = site;
                    pending.AddAssemblyPatch(
                        handle,
                        HookTarget.FromAddress(imageBase + unchecked((ulong)site.Rva)),
                        (assembler, _) => VanillaPeaceTimeNativeContract.EmitPatch(
                            assembler,
                            capturedSite,
                            imageBase),
                        maxByteCount: site.ExpectedBytes.Length,
                        name: "VanillaPeaceTime_" + site.Name);
                    gameplayPatches.Add(handle);
                }

                ulong peaceFlagAddress = imageBase +
                    unchecked((ulong)VanillaPeaceTimeNativeContract.PeaceTimeActiveFlagRva);
                pending.AddInline(
                    startingTroopsHook,
                    HookTarget.FromAddress(imageBase + unchecked((ulong)
                        VanillaPeaceTimeNativeContract.StartingTroopsDispatcherRva)),
                    (assembler, instructions, _) =>
                    {
                        VanillaPeaceTimeNativeContract.EmitStartingTroopsGuardPrefix(
                            assembler,
                            peaceFlagAddress);
                        assembler.AddInstructions(instructions);
                    },
                    hookSize: VanillaPeaceTimeNativeContract.StartingTroopsHookLength,
                    name: "VanillaPeaceTime_StartingTroops");

                CommitResult commitResult = pending.Commit();
                if (!commitResult.IsCompleteSuccess || !startingTroopsHook.Success ||
                    gameplayPatches.Exists(handle => !handle.Success))
                {
                    throw new InvalidOperationException(
                        "The complete Vanilla peace-time gameplay patch transaction did not commit.");
                }
                if (startingTroopsHook.Hook.DisplacedByteCount !=
                    VanillaPeaceTimeNativeContract.StartingTroopsHookLength)
                {
                    throw new InvalidOperationException(
                        $"The starting-troop hook displaced {startingTroopsHook.Hook.DisplacedByteCount} bytes; " +
                        $"expected {VanillaPeaceTimeNativeContract.StartingTroopsHookLength}.");
                }

                transaction = pending;
                pending = null;
            }
            catch
            {
                if (pending != null)
                {
                    try { pending.Dispose(); } catch { }
                }
                throw;
            }

            Shared.CrashBreadcrumbDiagnostics.Record(
                "VanillaPeaceTimeGameplayPatchInstalled",
                VanillaPeaceTimeNativeContract.PatchSites.Length,
                VanillaPeaceTimeNativeContract.StartingTroopsHookLength);
            Shared.DebugLogHelper.LogDebug(
                log,
                $"BUGFIXES_AND_QOL_VANILLA_PEACE_TIME_PATCH_INSTALLED: " +
                $"sha256={VanillaPeaceTimeNativeContract.ReferenceSha256}, " +
                $"branchPatches={VanillaPeaceTimeNativeContract.PatchSites.Length}, " +
                $"startingTroopsRva=0x{VanillaPeaceTimeNativeContract.StartingTroopsDispatcherRva:X}, " +
                $"displacedBytes={startingTroopsHook.Hook.DisplacedByteCount}.");
        }
    }
}
