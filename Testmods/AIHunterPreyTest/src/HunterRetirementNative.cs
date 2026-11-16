using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using APIShared.NativeSites;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIHunterPreyTest
{
    // No patch: reproduce the three calls made by Vanilla's AI retirement branch at 0x3B305.
    // The caller owns the simulation boundary and must independently establish prey absence.
    internal sealed unsafe class HunterRetirementNative
    {
        private const string AuditedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int RetirementBranch = 0x3B270;
        private readonly ManualLogSource log;
        private readonly IntPtr buildingManager, aivState;
        private readonly AvoidCell avoidCell;
        private readonly Refund refund;
        private readonly MarkForRemoval markForRemoval;
        private readonly bool supported;
        private bool faulted, invoking;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AvoidCell(IntPtr state, int x, int y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Refund(IntPtr manager, int buildingId, int playerId, int percent);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MarkForRemoval(IntPtr manager, int buildingId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int capacity);

        internal HunterRetirementNative(ManualLogSource logger, CrusaderLibraryLoadContext context)
        {
            log = logger;
            try
            {
                if (IntPtr.Size != 8 || context == null || context.ModuleHandle == IntPtr.Zero)
                    throw new InvalidOperationException("No x64 native image.");
                ValidateLayout();
                var path = new StringBuilder(32768);
                uint count = GetModuleFileName(context.ModuleHandle, path, path.Capacity);
                if (count == 0 || count >= path.Capacity) throw new InvalidOperationException("Cannot identify loaded native file.");
                using (var stream = File.OpenRead(path.ToString()))
                using (var sha = SHA256.Create())
                    if (!string.Equals(Hex(sha.ComputeHash(stream)), AuditedHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Native image is outside the audited retirement contract.");

                // Validate the original load snapshot, then call live entries so SE's refund hooks run.
                var memory = context.Memory;
                CheckFunction(memory, RetirementBranch, 229, "EFF1F3C1FB0BB922746F17F266D62EAA252E6FF7C64610EF50856EA2833AAD9D");
                int avoid = Relative(memory, 0x3B31E, 1, 5, 0xE8);
                int repay = Relative(memory, 0x3B331, 1, 5, 0xE8);
                int remove = Relative(memory, 0x3B33B, 1, 5, 0xE8);
                CheckFunction(memory, avoid, 109, "84F5ED0A91B52B6FD62CB4B56F35A5EB320BAB9B4A1F18E9603B89C2208DB828");
                CheckFunction(memory, repay, 1320, "C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7");
                CheckFunction(memory, remove, 257, "713BF8FC70A1320F9EA7BB8B1CEBC82F0E52211C78A41D1DA3286EA5E24B0E56");
                if (memory[0x3B280] != 0x8D || memory[0x3B281] != 0x35 ||
                    memory[0x3B30F] != 0x8D || memory[0x3B310] != 0x0D)
                    throw new InvalidOperationException("Retirement manager anchors differ.");
                int managerRva = Relative(memory, 0x3B27F, 3, 7, 0x48);
                int aivRva = Relative(memory, 0x3B30E, 3, 7, 0x48);
                if (!InSection(memory, managerRva, 1, unchecked((int)0xC0000040)) ||
                    !InSection(memory, aivRva, 1, unchecked((int)0xC0000040)))
                    throw new InvalidOperationException("Retirement roots are outside readable/writable data sections.");
                buildingManager = IntPtr.Add(context.ModuleHandle, managerRva);
                aivState = IntPtr.Add(context.ModuleHandle, aivRva);
                avoidCell = Marshal.GetDelegateForFunctionPointer<AvoidCell>(IntPtr.Add(context.ModuleHandle, avoid));
                refund = Marshal.GetDelegateForFunctionPointer<Refund>(IntPtr.Add(context.ModuleHandle, repay));
                markForRemoval = Marshal.GetDelegateForFunctionPointer<MarkForRemoval>(IntPtr.Add(context.ModuleHandle, remove));
                var claim = NativeSiteRegistry.Register(AIHunterPreyPlugin.PluginGuid, "HunterRetirementCalls", new[] {
                    new NativeSiteSpan(avoid, 1, NativeSiteMode.CallOnly),
                    new NativeSiteSpan(repay, 1, NativeSiteMode.CallOnly),
                    new NativeSiteSpan(remove, 1, NativeSiteMode.CallOnly) });
                if (!claim.Succeeded) throw new InvalidOperationException(claim.ToString());
                supported = true;
                log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] RETIREMENT_READY method=reference-rva; calls=0x{avoid:X}/0x{repay:X}/0x{remove:X}; managers=0x{managerRva:X}/0x{aivRva:X}; layouts=Building32C/Unit490; vanilla AI refund; no hook");
            }
            catch (Exception error)
            {
                log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] RETIREMENT_UNAVAILABLE {error.Message}");
            }
        }

        internal bool Supported => supported && !faulted;

        internal bool TryRetire(int buildingId, uint globalId, int owner, out string reason)
        {
            reason = "";
            if (!Supported || invoking) { reason = "Retirement unavailable or reentrant."; return false; }
            var buildings = GameBuildingManagerAPI.Instance;
            if ((IntPtr)buildings.GetBuildingManager().Pointer != buildingManager)
            { reason = "Native building manager identity differs."; return false; }
            if (!GamePlayerManagerAPI.Instance.IsAIPlayer(owner) ||
                !APIShared.BuildingAccess.TryGetById(buildings, buildingId, out GameBuilding* building, out _) ||
                !IsCandidate(building, globalId, owner))
            { reason = "Hunter identity, owner, life state or demolition group changed."; return false; }
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            fixed (GameUnit* first = units)
                if (!IsDrained(in *building, first, units.Length))
                { reason = "Hunter still has meat or unfinished work."; return false; }

            invoking = true;
            try
            {
                avoidCell(aivState, building->r_TilePositionXBegin, building->r_TilePositionYBegin);
                refund(buildingManager, buildingId, owner, 50);
                // Refund subscribers are arbitrary consumers; never delete a reused or changed slot.
                if (!IsCandidate(building, globalId, owner))
                    throw new InvalidOperationException("Hunter changed during refund; retirement disabled to prevent repeat refunds.");
                fixed (GameUnit* first = units)
                    if (!IsDrained(in *building, first, units.Length))
                        throw new InvalidOperationException("Hunter work changed during refund; retirement disabled to prevent repeat refunds.");
                markForRemoval(buildingManager, buildingId);
                if (building->r_GlobalId != globalId || building->r_AliveState != AliveState.MarkedForDeletion)
                    throw new InvalidOperationException("Native retirement did not mark the expected hunter for deletion.");
                return true;
            }
            catch (Exception error)
            {
                faulted = true;
                reason = error.Message;
                return false;
            }
            finally { invoking = false; }
        }

        private static bool IsCandidate(GameBuilding* building, uint global, int owner) => building != null && global != 0 &&
            building->r_GlobalId == global && building->r_PlayerIdOwner == owner &&
            building->r_AliveState == AliveState.IsAlive && building->r_BuildingType == eStructs.STRUCT_HUNTERS_HUT &&
            building->N0000178A == 0; // Native removal also marks every building in a nonzero demolition group.

        // State 4 already owns a delivery even while both visible meat amounts are zero. Unknown,
        // hunting, corpse-return, processing, delivery and return states therefore all wait.
        internal static bool IsDrained(in GameBuilding building, GameUnit* units, int length)
        {
            if (building.r_GlobalId == 0 || building.r_MeatAmount != 0 || building.r_ProductionCycleMode != 0 ||
                building.r_ProductionProgress != 0 || building.N00001758 != 0 || length < 0 || (length != 0 && units == null))
                return false;
            if (building.r_WorkerId_1 != 0)
            {
                int index = building.r_WorkerId_1 - 1;
                if (index >= length) return false;
                ref GameUnit worker = ref units[index];
                if (building.N0000176A == 0 || worker.r_GlobalId != building.N0000176A || worker.r_LinkedProductionBuildingGlobalId != building.r_GlobalId ||
                    worker.r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || !IdleAndEmpty(in worker)) return false;
            }
            // Scan all links as well: an unregistered/second worker must not hide unfinished meat.
            // The hound's carry field is an animation counter, so only Hunters are inspected here.
            for (int i = 0; i < length; i++)
            {
                ref GameUnit worker = ref units[i];
                if (worker.r_AliveState != AliveState.None && worker.r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER &&
                    worker.r_LinkedProductionBuildingGlobalId == building.r_GlobalId && !IdleAndEmpty(in worker)) return false;
            }
            return true;
        }

        private static bool IdleAndEmpty(in GameUnit worker) => worker.r_AliveState == AliveState.IsAlive &&
            worker.r_CarryGoodAmount == 0 && (worker.r_AIState == 0 || worker.r_AIState == 7 || worker.r_AIState == 8);

        private static void CheckFunction(ReadOnlySpan<byte> image, int rva, int length, string expected)
        {
            if (rva < 0 || length > image.Length - rva || !InSection(image, rva, length, 0x20000000))
                throw new InvalidOperationException("Retirement function is outside executable image memory.");
            using (var sha = SHA256.Create())
                if (!string.Equals(Hex(sha.ComputeHash(image.Slice(rva, length).ToArray())), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Retirement function snapshot differs at 0x" + rva.ToString("X") + ".");
        }

        private static int Relative(ReadOnlySpan<byte> image, int instruction, int operand, int size, byte opcode)
        {
            if (instruction < 0 || instruction > image.Length - size || image[instruction] != opcode)
                throw new InvalidOperationException("Retirement reference instruction differs.");
            int target = checked(instruction + size + Read32(image, instruction + operand));
            if (target < 0 || target >= image.Length) throw new InvalidOperationException("Retirement reference is outside the image.");
            return target;
        }

        private static bool InSection(ReadOnlySpan<byte> image, int rva, int length, int requiredFlags)
        {
            int pe = Read32(image, 0x3C);
            if (pe < 0 || pe > image.Length - 24 || Read32(image, pe) != 0x4550) return false;
            int count = image[pe + 6] | image[pe + 7] << 8;
            int table = checked(pe + 24 + (image[pe + 20] | image[pe + 21] << 8));
            for (int i = 0; i < count; i++)
            {
                int section = checked(table + i * 40);
                if (section < 0 || section > image.Length - 40) return false;
                int start = Read32(image, section + 12), size = Read32(image, section + 8);
                if ((Read32(image, section + 36) & requiredFlags) == requiredFlags && rva >= start &&
                    (long)rva + length <= (long)start + size) return true;
            }
            return false;
        }

        private static int Read32(ReadOnlySpan<byte> bytes, int at) => bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24;
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

        private static void ValidateLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C || Marshal.SizeOf(typeof(GameUnit)) != 0x490)
                throw new InvalidOperationException("Hunter retirement structure sizes differ.");
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_AliveState), 0xD0);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_BuildingType), 0xD2);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_GlobalId), 0xD8);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_TilePositionXBegin), 0xEE);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_TilePositionYBegin), 0xF0);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_ProductionCycleMode), 0x26);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_ProductionProgress), 0x2C);
            CheckOffset<GameBuilding>(nameof(GameBuilding.N00001758), 0x118);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_MeatAmount), 0x150);
            CheckOffset<GameBuilding>(nameof(GameBuilding.r_WorkerId_1), 0x19E);
            CheckOffset<GameBuilding>(nameof(GameBuilding.N0000176A), 0x1A8);
            CheckOffset<GameBuilding>(nameof(GameBuilding.N0000178A), 0x2A8);
            CheckOffset<GameUnit>(nameof(GameUnit.r_AliveState), 0x88);
            CheckOffset<GameUnit>(nameof(GameUnit.r_UnitChimp), 0x8A);
            CheckOffset<GameUnit>(nameof(GameUnit.r_GlobalId), 0x94);
            CheckOffset<GameUnit>(nameof(GameUnit.r_AIState), 0x2BC);
            CheckOffset<GameUnit>(nameof(GameUnit.r_LinkedProductionBuildingGlobalId), 0x364);
            CheckOffset<GameUnit>(nameof(GameUnit.r_CarryGoodAmount), 0x384);
        }

        private static void CheckOffset<T>(string member, int expected)
        {
            if (Marshal.OffsetOf(typeof(T), member).ToInt32() != expected)
                throw new InvalidOperationException("Hunter retirement field layout differs: " + typeof(T).Name + "." + member);
        }
    }
}
