using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed unsafe class WorkshopIdleDelayHook
    {
        private readonly ManualLogSource log;
        private readonly HookTransaction transaction;
        private readonly IntPtr enabledFlag;
        private readonly ulong unitManager, buildingManager;
        private bool afterStartupLogged;

        internal WorkshopIdleDelayHook(ManualLogSource log, ScanRegion region,
            ReadOnlySpan<byte> memory, ulong imageBase, bool nativeHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!nativeHashMatches || imageBase == 0)
                throw new InvalidOperationException("Workshop idle hooks require the audited native DLL hash.");
            unitManager = imageBase + WorkshopIdleDelayNative.UnitManagerRva;
            buildingManager = imageBase + WorkshopIdleDelayNative.BuildingManagerRva;
            ValidateLayouts();
            ValidateSite(memory, imageBase, WorkshopIdleDelayNative.PoleRva,
                WorkshopIdleDelayNative.PoleExitRva, WorkshopIdleDelayNative.PoleBytes);
            ValidateSite(memory, imageBase, WorkshopIdleDelayNative.TannerRva,
                WorkshopIdleDelayNative.TannerExitRva, WorkshopIdleDelayNative.TannerBytes);
            // Decode with the installed backend before any transaction can publish executable bytes.
            Probe(imageBase + WorkshopIdleDelayNative.PoleRva);
            Probe(imageBase + WorkshopIdleDelayNative.TannerRva);
            enabledFlag = Marshal.AllocHGlobal(sizeof(int));
            *(int*)enabledFlag = 0;
            HookTransaction candidate = null;
            try
            {
                ValidateGenerator(imageBase, false);
                ValidateGenerator(imageBase, true);
                candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(region);
                var pole = new HookHandle<X64InlineHook>();
                var tanner = new HookHandle<X64InlineHook>();
                Add(candidate, pole, imageBase, false);
                Add(candidate, tanner, imageBase, true);
                CommitResult result = candidate.Commit();
                if (!result.IsCompleteSuccess || !pole.Success || !tanner.Success ||
                    !pole.IsInstalled || !tanner.IsInstalled ||
                    pole.Hook.DisplacedByteCount != WorkshopIdleDelayNative.DisplacedLength ||
                    tanner.Hook.DisplacedByteCount != WorkshopIdleDelayNative.DisplacedLength ||
                    pole.Hook.TargetAddress != imageBase + WorkshopIdleDelayNative.PoleRva ||
                    tanner.Hook.TargetAddress != imageBase + WorkshopIdleDelayNative.TannerRva)
                    throw new InvalidOperationException("Workshop idle hooks failed their atomic install contract.");
                transaction = candidate;
                candidate = null;
            }
            catch
            {
                // Flag is still zero. Only this unpublished initialization candidate may roll back.
                candidate?.Dispose();
                Marshal.FreeHGlobal(enabledFlag);
                throw;
            }
        }

        private void Add(HookTransaction candidate, HookHandle<X64InlineHook> handle,
            ulong imageBase, bool tanner)
        {
            int rva = tanner ? WorkshopIdleDelayNative.TannerRva : WorkshopIdleDelayNative.PoleRva;
            int exit = tanner ? WorkshopIdleDelayNative.TannerExitRva : WorkshopIdleDelayNative.PoleExitRva;
            candidate.AddInline(handle, HookTarget.FromAddress(imageBase + (uint)rva),
                (assembler, instructions, _) =>
                {
                    WorkshopIdleDelayNative.ValidateInstructions(instructions, imageBase + (uint)exit, tanner);
                    WorkshopIdleDelayNative.Emit(assembler, instructions,
                        unchecked((ulong)enabledFlag.ToInt64()),
                        imageBase + WorkshopIdleDelayNative.UnitManagerRva,
                        imageBase + WorkshopIdleDelayNative.BuildingManagerRva, tanner);
                }, hookSize: 14);
        }

        private void ValidateGenerator(ulong imageBase, bool tanner)
        {
            int rva = tanner ? WorkshopIdleDelayNative.TannerRva : WorkshopIdleDelayNative.PoleRva;
            byte[] bytes = tanner ? WorkshopIdleDelayNative.TannerBytes : WorkshopIdleDelayNative.PoleBytes;
            var assembler = new Assembler(64);
            WorkshopIdleDelayNative.Emit(assembler, WorkshopIdleDelayNative.Decode(bytes, imageBase + (uint)rva),
                unchecked((ulong)enabledFlag.ToInt64()), imageBase + WorkshopIdleDelayNative.UnitManagerRva,
                imageBase + WorkshopIdleDelayNative.BuildingManagerRva, tanner);
            using (var stream = new MemoryStream())
            {
                ulong ip = imageBase + (uint)rva;
                if (!assembler.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _))
                    throw new InvalidOperationException("Workshop idle generator: " + error);
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(stream.ToArray()));
                decoder.IP = ip;
                while (decoder.IP < ip + (ulong)stream.Length)
                    if (decoder.Decode().IsInvalid)
                        throw new InvalidOperationException("Workshop idle generator produced an invalid instruction.");
            }
        }

        internal static void Probe(ulong address)
        {
            // Constructor only decodes a private/live read-only candidate; never Generate or Enable.
            using (var probe = new X64InlineHook(address, 14))
                if (probe.DisplacedByteCount != WorkshopIdleDelayNative.DisplacedLength || probe.IsInstalled)
                    throw new InvalidOperationException("Installed RedBird workshop displacement differs from 17 bytes.");
        }

        private void ValidateSite(ReadOnlySpan<byte> memory, ulong imageBase,
            int rva, int exit, byte[] expected)
        {
            string signature = BitConverter.ToString(expected).Replace('-', ' ');
            var resolved = Shared.NativePatternResolver.ResolveUnique(memory, signature, rva, true,
                "workshop idle " + rva, log);
            // A match elsewhere cannot authorize fixed-layout register/continuation assumptions.
            if (resolved.Rva != rva)
                throw new InvalidOperationException($"Workshop idle pattern moved from audited RVA 0x{rva:X}.");
            int unique = Shared.NativePatternResolver.FindUniquePattern(memory, signature, "workshop idle " + rva);
            if (unique != rva)
                throw new InvalidOperationException("Workshop idle signature moved outside the audited control flow.");
            WorkshopIdleDelayNative.ValidateInstructions(WorkshopIdleDelayNative.Decode(expected, imageBase + (uint)rva),
                imageBase + (uint)exit, rva == WorkshopIdleDelayNative.TannerRva);
            Shared.DebugLogHelper.LogDebug(log,
                $"Workshop idle address resolved: method={resolved.Method}, rva=0x{resolved.Rva:X}; span=17.");
        }

        private void ValidateLayouts()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != WorkshopIdleDelayNative.UnitStride ||
                Marshal.SizeOf(typeof(GameBuilding)) != WorkshopIdleDelayNative.BuildingStride)
                throw new InvalidOperationException("Workshop idle interop strides changed.");
            CheckOffset(typeof(GameUnitManager), nameof(GameUnitManager.GameUnitArray), 0x65C);
            CheckOffset(typeof(GameBuildingManager), nameof(GameBuildingManager.BuildingsArray), 0x5C);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_AliveState), 0x88);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_UnitChimp), 0x8A);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_IsKilledByProjectile), 0x29C);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_WorkModeFlags), 0x2FA);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingId), 0x334);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingGlobalId), 0x364);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_AliveState), 0xD0);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_CowHidesAmount), 0x134);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId), 0x92);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_AIState), 0x2BC);
            CheckOffset(typeof(GameUnit), nameof(GameUnit.r_TicksSinceRestPulse), 0x37C);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType), 0xD2);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId), 0xD8);
            CheckOffset(typeof(GameBuilding), nameof(GameBuilding.r_WoodPlanksAmount), 0x128);
            Shared.DebugLogHelper.LogDebug(log,
                "Workshop idle field map validated: unit manager header=0x65C stride=0x490; " +
                "building header=0x5C stride=0x32C; gameId is 1-based; native hash=" + WorkshopIdleDelayNative.NativeSha256);
        }

        private static void CheckOffset(Type type, string member, int expected)
        {
            if (Marshal.OffsetOf(type, member).ToInt32() != expected)
                throw new InvalidOperationException("Workshop idle field offset changed: " + type.Name + "." + member);
        }

        internal void SetEnabled(bool enabled) => Volatile.Write(ref *(int*)enabledFlag, enabled ? 1 : 0);

        internal void LogAfterStartup(int tick)
        {
            if (afterStartupLogged) return;
            afterStartupLogged = true;
            Shared.DebugLogHelper.LogDebug(log,
                "BUGFIXES_AND_QOL_WORKSHOP_IDLE_READY: permanent poleturner/tanner inline hooks; tick=" + tick +
                "; unit allocation=" + *(int*)unitManager + "; building allocation=" + *(int*)(buildingManager + 0x50));
        }
    }
}
