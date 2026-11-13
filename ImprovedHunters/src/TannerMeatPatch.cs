using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ImprovedHunters
{
    /// <summary>
    /// Lets a Tannery send its Tanner to a granary with meat for the share of hides its slider
    /// chooses. The Tanner keeps Vanilla's states 7 and 8; only the good, its storage and its
    /// delivery follow the Hunter's meat path. All other runs stay exactly as they are, so the
    /// workshop's produced-good fields stay free for other mods. AI Tanneries have no table entry.
    /// Bound to the audited DLL (see UpdateToNewDLL.md).
    /// </summary>
    internal sealed class TannerMeatPatch : IDisposable
    {
        private const int YieldBlockRva = 0x13EBD8;
        private const string YieldBlockPattern =
            "48 63 0D ?? ?? ?? ?? 33 FF 48 69 D9 90 04 00 00 45 8B C5 41 8B D5 " +
            "66 42 89 BC 23 20 09 00 00 E8 ?? ?? ?? ?? 66 42 89 84 23 E0 09 00 00";
        private const int YieldSiteOffset = 0x1F;
        private const int YieldDisplaced = 14;
        private const int YieldCallOperand = 0x20;
        private const int YieldCurrentUnitOperand = 3;

        private const int FindBlockRva = 0x13ED74;
        private const string FindBlockPattern =
            "33 D2 42 0F BF 84 23 E0 09 00 00 48 8B CE 46 0F BF 8C 23 EE 06 00 00 " +
            "44 8D 47 17 89 44 24 20 E8 ?? ?? ?? ?? 48 63 D8 85 C0 75 24 48 63 0D ?? ?? ?? ?? " +
            "48 69 D1 90 04 00 00 42 0F BF 8C 22 EE 06 00 00 3B 0D ?? ?? ?? ?? 75 05 " +
            "E8 ?? ?? ?? ?? 45 8B C5 8B D3";
        private const int FindSiteOffset = 0x17;
        private const int FindDisplaced = 16;
        private const int FindArmouryCallOperand = 0x20;
        private const int FindLocalPlayerOperand = 0x44;
        private const int FindAfterWarning = 0x4F;

        private const int AcceptBlockRva = 0x13EED9;
        private const string AcceptBlockPattern =
            "44 8D 47 17 48 69 C8 90 04 00 00 44 89 6C 24 20 46 0F BF 8C 21 EE 06 00 00 " +
            "42 0F BF 94 21 8E 09 00 00 48 8B CE E8 ?? ?? ?? ?? 85 C0 0F 84 ?? ?? ?? ??";
        private const int AcceptSiteOffset = 0x22;
        private const int AcceptDisplaced = 16;
        private const int AcceptFailOperand = 0x2E;

        private const int DepositBlockRva = 0x13EF38;
        private const string DepositBlockPattern =
            "46 8B 84 21 F8 06 00 00 48 8B CE E8 ?? ?? ?? ?? 48 63 05 ?? ?? ?? ?? 45 8B C5 " +
            "48 69 C8 90 04 00 00 4A 0F BF 94 21 EE 06 00 00 48 8D 0D ?? ?? ?? ?? 44 8B CA " +
            "FF 84 91 ?? ?? ?? ?? 8B D5 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? " +
            "48 63 1D ?? ?? ?? ?? 48 69 CB 90 04 00 00 66 42 FF 8C 21 E0 09 00 00 " +
            "42 38 BC 21 A6 0A 00 00 0F 85 ?? ?? ?? ?? 46 0F BF 84 21 1E 07 00 00 44 8D 4F 27 " +
            "42 0F BF 94 21 1C 07 00 00 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ??";
        private const int DepositDisplaced = 16;
        private const int DepositCallOperand = 0xC;
        private const int DepositStatsManagerOperand = 0x40;
        private const int DepositStatsCallOperand = 0x45;
        private const int DepositDoneOperand = 0x6A;
        private const int DepositSoundManagerOperand = 0x87;
        private const int DepositSoundCallOperand = 0x8C;

        private const int RefindBlockRva = 0x13EFE3;
        private const string RefindBlockPattern =
            "48 63 05 ?? ?? ?? ?? 33 D2 48 69 C8 90 04 00 00 44 8D 42 17 42 0F BF 84 21 E0 09 00 00 " +
            "46 0F BF 8C 21 EE 06 00 00 48 8B CE 89 44 24 20 E8 ?? ?? ?? ?? 48 63 D8";
        private const int RefindSiteOffset = 0x26;
        private const int RefindDisplaced = 15;

        private const int HunterFindBlockRva = 0x130929;
        private const string HunterFindBlockPattern =
            "8D 56 0B 46 0F BF 8C 2F EE 06 00 00 44 8B C6 48 8D 3D ?? ?? ?? ?? 48 8B CF E8 ?? ?? ?? ?? " +
            "4C 63 F0 85 C0 75 24 48 63 0D ?? ?? ?? ?? 48 69 D1 90 04 00 00 " +
            "42 0F BF 8C 2A EE 06 00 00 3B 0D ?? ?? ?? ?? 75 05 E8 ?? ?? ?? ??";
        private const int HunterFindCallOperand = 0x1A;
        private const int HunterWarningOperand = 0x45;

        private const int HunterAcceptBlockRva = 0x130A98;
        private const string HunterAcceptBlockPattern =
            "41 B9 FA 00 00 00 48 69 C8 90 04 00 00 41 B8 0C 00 00 00 " +
            "42 0F BF 94 29 8E 09 00 00 48 8B CF E8 ?? ?? ?? ?? 85 C0";
        private const int HunterAcceptCallOperand = 0x20;

        private const int HunterMeatStatRva = 0x130B3E;
        private const string HunterMeatStatPattern = "FF 84 82 ?? ?? ?? ?? 8D 56 01 E8 ?? ?? ?? ??";
        private const int HunterMeatStatDisplacement = 3;

        private readonly PermanentNativeGate gate;
        private readonly IntPtr meatAmount;

        public TannerMeatPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches,
            TannerRunTable table)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");
            if (!referenceHashMatches)
                throw new InvalidOperationException("The Tanner meat path is bound to the audited native hash.");

            int yield = Shared.NativePatternResolver.ResolveUnique(
                memory, YieldBlockPattern, YieldBlockRva, true, "Tanner run yield", log).Rva;
            int find = Shared.NativePatternResolver.ResolveUnique(
                memory, FindBlockPattern, FindBlockRva, true, "Tanner storage search", log).Rva;
            int accept = Shared.NativePatternResolver.ResolveUnique(
                memory, AcceptBlockPattern, AcceptBlockRva, true, "Tanner storage acceptance", log).Rva;
            int deposit = Shared.NativePatternResolver.ResolveUnique(
                memory, DepositBlockPattern, DepositBlockRva, true, "Tanner deposit", log).Rva;
            int refind = Shared.NativePatternResolver.ResolveUnique(
                memory, RefindBlockPattern, RefindBlockRva, true, "Tanner storage re-search", log).Rva;
            int hunterFind = Shared.NativePatternResolver.ResolveUnique(
                memory, HunterFindBlockPattern, HunterFindBlockRva, true, "Hunter granary search", log).Rva;
            int hunterAccept = Shared.NativePatternResolver.ResolveUnique(
                memory, HunterAcceptBlockPattern, HunterAcceptBlockRva, true, "Hunter granary acceptance", log).Rva;
            int hunterStat = Shared.NativePatternResolver.ResolveUnique(
                memory, HunterMeatStatPattern, HunterMeatStatRva, true, "Hunter meat statistic", log).Rva;

            var addresses = new HunterNativeStubs.TannerMeatAddresses
            {
                CurrentUnit = Relative(memory, imageBase, yield + YieldCurrentUnitOperand),
                LocalPlayer = Relative(memory, imageBase, find + FindLocalPlayerOperand),
                ArmouryFind = Relative(memory, imageBase, find + FindArmouryCallOperand),
                GranaryFind = Relative(memory, imageBase, hunterFind + HunterFindCallOperand),
                GranaryMissingWarning = Relative(memory, imageBase, hunterFind + HunterWarningOperand),
                GranaryAccept = Relative(memory, imageBase, hunterAccept + HunterAcceptCallOperand),
                Deposit = Relative(memory, imageBase, deposit + DepositCallOperand),
                StatsManager = Relative(memory, imageBase, deposit + DepositStatsManagerOperand),
                StatsAdd = Relative(memory, imageBase, deposit + DepositStatsCallOperand),
                SoundManager = Relative(memory, imageBase, deposit + DepositSoundManagerOperand),
                SoundPlay = Relative(memory, imageBase, deposit + DepositSoundCallOperand),
                // This displacement is relative to the image base, not to the instruction.
                FoodDeliveredStat = imageBase + (ulong)Shared.NativePatternResolver.ReadInt32(memory, hunterStat + HunterMeatStatDisplacement),
                AfterFindWarning = imageBase + (ulong)(find + FindAfterWarning),
                AcceptFail = Relative(memory, imageBase, accept + AcceptFailOperand),
                DepositDone = Relative(memory, imageBase, deposit + DepositDoneOperand),
                RunTable = table.Address
            };

            // Both the meat and logical-disabled Vanilla branch enter the shared
            // receipt relay; it stays entirely native when no observer is active.
            bool hasReceiptAdapter = APIShared.Economy.ProductionGoodsReceived.TryGetNativeDeliveryAdapter(out ulong receiptAdapter, out string receiptDiagnostic);
            if (hasReceiptAdapter)
                addresses.Deposit = receiptAdapter;
            else
                Shared.DebugLogHelper.LogError(log, "Tanner production receipts unavailable; the independent Tanner meat path retains its normal deposit: " + receiptDiagnostic);

            ulong yieldHelper = Relative(memory, imageBase, yield + YieldCallOperand);
            int meat = (int)eGoods.STORED_FOOD_MEAT;
            IntPtr candidateAmount = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(candidateAmount, TannerMeatPolicy.DefaultMeatPerTrip);
            ulong amountAddress = unchecked((ulong)candidateAmount.ToInt64());

            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                candidate.AddSite(memory, yield + YieldSiteOffset, YieldDisplaced,
                    (a, i) => HunterNativeStubs.EmitTannerYield(a, i, flag, table.Address, amountAddress, yieldHelper));
                candidate.AddSite(memory, find + FindSiteOffset, FindDisplaced,
                    (a, i) => HunterNativeStubs.EmitTannerFind(a, i, flag, addresses, meat));
                candidate.AddSite(memory, accept + AcceptSiteOffset, AcceptDisplaced,
                    (a, i) => HunterNativeStubs.EmitTannerAccept(a, i, flag, addresses, meat));
                candidate.AddSite(memory, deposit, DepositDisplaced,
                    (a, i) => HunterNativeStubs.EmitTannerDeposit(a, i, flag, addresses, meat));
                candidate.AddSite(memory, refind + RefindSiteOffset, RefindDisplaced,
                    (a, i) => HunterNativeStubs.EmitTannerRefind(a, i, flag, addresses, meat));
                candidate.Commit();
                gate = candidate;
                meatAmount = candidateAmount;
                if (hasReceiptAdapter) APIShared.Economy.ProductionGoodsReceived.ConfirmExternalTannerAdapter();
            }
            catch
            {
                candidate.Dispose();
                Marshal.FreeHGlobal(candidateAmount);
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters Tanner meat path installed: yield=0x{yield + YieldSiteOffset:X}, " +
                $"find=0x{find + FindSiteOffset:X}, accept=0x{accept + AcceptSiteOffset:X}, " +
                $"deposit=0x{deposit:X}, refind=0x{refind + RefindSiteOffset:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void Configure(bool enabled, int meatPerTrip)
        {
            if (!IsAvailable)
                return;

            // Written before the flag; settings only change between matches.
            Marshal.WriteInt32(meatAmount, TannerMeatPolicy.ClampMeatPerTrip(meatPerTrip));
            Thread.MemoryBarrier();
            gate.SetEnabled(enabled);
        }

        public void Dispose() => gate?.Dispose();

        private static ulong Relative(ReadOnlySpan<byte> memory, ulong imageBase, int operand) =>
            imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(memory, operand, operand + 4);
    }
}
