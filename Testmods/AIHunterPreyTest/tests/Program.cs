using System;
using System.Reflection;
using AIHunterPreyTest;
using APIShared.Wildlife;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

internal static unsafe class Program
{
    private static int checks;
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); checks++; }
    private static int Main()
    {
        try
        {
            Check(PreyPresencePolicy.Due(false, 0, 0), "initial scan");
            Check(!PreyPresencePolicy.Due(true, 100, 4899), "not early");
            Check(PreyPresencePolicy.Due(true, 100, 4900), "interval due");
            Check(PreyPresencePolicy.Due(true, int.MaxValue - 100, unchecked(int.MaxValue - 100 + 4800)), "clock wrap");
            Check(!PreyPresencePolicy.Due(true, 100, 100), "paused clock");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 0, false) == 0x1FE, "neutral prey all players");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 2, false) == 0, "owned prey excluded");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 2, true) == 4, "own prey correct player");
            Check(PreyPresencePolicy.OwnerMask(true, false, false, 0, false) == 0, "dead exhausted");
            Check(PreyPresencePolicy.OwnerMask(true, false, true, 0, false) == 0x1FE, "collectible corpse");
            Check(!PreyPresencePolicy.ShouldBlock(true, true, false, false, true, false), "humans unchanged");
            Check(!PreyPresencePolicy.ShouldBlock(true, true, false, true, false, false), "unknown preserves original");
            Check(!PreyPresencePolicy.ShouldBlock(true, false, false, true, true, false), "separate aiv option");
            Check(PreyPresencePolicy.ShouldBlock(true, false, true, true, true, false), "auto call still gated");

            Check(HunterHutPolicy.AllowsBuild(false, true, 0), "chickens permit first AIV hut");
            Check(!HunterHutPolicy.AllowsBuild(false, true, 1), "chickens do not permit second AIV hut");
            Check(HunterHutPolicy.AllowsBuild(true, true, 4), "wildlife permits more AIV huts");
            Check(!HunterHutPolicy.AllowsBuild(false, false, 0), "no food no first hut");
            Check(HunterHutPolicy.Keep(false, true, 44, 44), "chicken baseline retained");
            Check(!HunterHutPolicy.Keep(false, true, 45, 44), "extra hut retired despite chickens");
            Check(!HunterHutPolicy.Keep(false, false, 44, 44), "baseline retired when all food gone");
            Check(HunterHutPolicy.Keep(true, false, 45, 44), "wildlife retains every hut");
            var huts = new GameBuilding[] {
                new GameBuilding { r_PlayerIdOwner=2, r_AliveState=AliveState.IsAlive, r_BuildingType=eStructs.STRUCT_GRANARY, r_TilePositionXBegin=20, r_TilePositionYBegin=20 },
                new GameBuilding { r_PlayerIdOwner=2, r_AliveState=AliveState.IsAlive, r_BuildingType=eStructs.STRUCT_HUNTERS_HUT, r_GlobalId=44, r_TilePositionXBegin=30, r_TilePositionYBegin=20 },
                new GameBuilding { r_PlayerIdOwner=2, r_AliveState=AliveState.NeedsInit, r_BuildingType=eStructs.STRUCT_HUNTERS_HUT, r_GlobalId=45, r_TilePositionXBegin=21, r_TilePositionYBegin=20 }
            };
            Check(HunterHutPolicy.Count(huts, 2) == 2, "AIV and initializing huts count together");
            Check(HunterHutPolicy.Count(huts, 1) == 0, "foreign huts ignored");
            Check(HunterHutPolicy.SelectBaseline(huts, 2, 1) == 45, "nearest granary hut wins");
            Check(HunterHutPolicy.SelectBaseline(huts, 2, 0) == 44, "no granary stable global identity");
            huts[2].r_TilePositionXBegin=10;
            Check(HunterHutPolicy.SelectBaseline(huts, 2, 1) == 44, "equal distance uses lower global identity");
            Check(HunterHutPolicy.HasSpawnSite(huts, 2), "own living granary permits chicken renewal");
            Check(!HunterHutPolicy.HasSpawnSite(huts, 1), "foreign granary not a renewal site");
            huts[0].r_AliveState=AliveState.NeedsInit;
            Check(!HunterHutPolicy.HasSpawnSite(huts, 2), "uninitialized spawn site not ready");
            huts[0].r_AliveState=AliveState.IsAlive; huts[0].r_BuildingType=eStructs.STRUCT_MILL;
            Check(HunterHutPolicy.HasSpawnSite(huts, 2), "mill permits renewal");
            huts[0].r_BuildingType=eStructs.STRUCT_BAKERS_WORKSHOP;
            Check(HunterHutPolicy.HasSpawnSite(huts, 2), "bakery permits renewal");
            huts[0].r_BuildingType=eStructs.STRUCT_GOODS_YARD;
            Check(!HunterHutPolicy.HasSpawnSite(huts, 2), "unrelated building cannot promise renewal");

            var units = new GameUnit[2];
            var hut = new GameBuilding { r_GlobalId = 22 };
            fixed (GameUnit* first = units)
            {
                Check(HunterRetirementNative.IsDrained(in hut, first, units.Length), "empty hut drains");
                hut.r_MeatAmount = 1;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "hut meat retained"); hut.r_MeatAmount = 0;
                hut.r_ProductionCycleMode = 1;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "processing no visible meat"); hut.r_ProductionCycleMode = 0;
                hut.r_ProductionProgress = 1;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "processing progress"); hut.r_ProductionProgress = 0;
                hut.N00001758 = 1;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "native processing stage"); hut.N00001758 = 0;
                units[0] = new GameUnit { r_AliveState = AliveState.IsAlive, r_UnitChimp = eChimps.CHIMP_TYPE_HUNTER,
                    r_GlobalId = 55, r_LinkedProductionBuildingGlobalId = 22, r_AIState = 0 };
                hut.r_WorkerId_1 = 1; hut.N0000176A = 55;
                Check(HunterRetirementNative.IsDrained(in hut, first, units.Length), "idle linked hunter");
                foreach (ushort state in new ushort[] { 1, 2, 3, 4, 5, 6, 9, 10, 11, 109, 110, 999 })
                { units[0].r_AIState = state; Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "unfinished state " + state); }
                foreach (ushort state in new ushort[] { 0, 7, 8 })
                { units[0].r_AIState = state; Check(HunterRetirementNative.IsDrained(in hut, first, units.Length), "idle state " + state); }
                units[0].r_CarryGoodAmount = 1;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "worker meat retained"); units[0].r_CarryGoodAmount = 0;
                units[0].r_GlobalId++;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "reused worker slot"); units[0].r_GlobalId--;
                units[1] = units[0]; units[1].r_AIState = 4;
                Check(!HunterRetirementNative.IsDrained(in hut, first, units.Length), "orphan linked delivery");
                units[1].r_UnitChimp = eChimps.CHIMP_TYPE_DOG; units[1].r_CarryGoodAmount = 99;
                Check(HunterRetirementNative.IsDrained(in hut, first, units.Length), "hound animation counter is not meat");
            }
            var disabledDeer = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", true, deer: false);
            var deerEntry = Entry((int)eChimps.CHIMP_TYPE_DEER, "APIShared_Serp", "vanilla-deer");
            Check(!disabledDeer.AllowsType((int)eChimps.CHIMP_TYPE_DEER, deerEntry), "IH disabled deer overrides builtin catalog");
            var absent = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Absent, "");
            Check(absent.AllowsType((int)eChimps.CHIMP_TYPE_DEER, deerEntry), "standalone vanilla");
            var inactive = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "");
            int rabbit = (int)eChimps.CHIMP_TYPE_RABBIT;
            Check(!inactive.AllowsType(rabbit, Entry(rabbit, "ImprovedHunters_Serp", "CHIMP_TYPE_RABBIT")), "inactive own frozen registration");
            Check(inactive.AllowsType(rabbit, Entry(rabbit, "ThirdParty", "rabbit")), "foreign catalog winner preserved");
            Check(absent.AllowsType(500, Entry(500, "ThirdParty", "new-animal")), "custom raw type outside enum");
            Check(absent.AllowsType(65535, Entry(65535, "ThirdParty", "high-animal")), "full raw ushort domain");
            bool supplyReady = false;
            var renewing = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", enabled: true,
                chickenSupplyConfigured: true, renewalReady: () => supplyReady);
            Check(!renewing.CanRenewChickens, "failed population registration not renewable");
            supplyReady = true;
            Check(renewing.CanRenewChickens, "zero living chickens can retain baseline with active supply");
            supplyReady = false;
            Check(!renewing.CanRenewChickens, "disabled population handle takes effect without recapture");
            Check(!new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", enabled:true,
                chickenSupplyConfigured:false, renewalReady: () => throw new Exception()).CanRenewChickens, "zero population avoids readiness query");
            Check(!new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", enabled:true, chicken:false,
                chickenSupplyConfigured:true, renewalReady: () => true).CanRenewChickens, "disabled chicken hunting no renewal");
            Check(!new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", enabled:false,
                chickenSupplyConfigured:true, renewalReady: () => true).CanRenewChickens, "disabled IH no renewal");
            Check(!new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Absent, "", enabled:true,
                chickenSupplyConfigured:true, renewalReady: () => true).CanRenewChickens, "absent IH no assumed renewal");
            Check(!new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Unavailable, "", enabled:true,
                chickenSupplyConfigured:true, renewalReady: () => true).CanRenewChickens, "unknown IH no assumed renewal");
            int corpseCalls = 0;
            GameUnit corpse = default;
            GameUnit* expectedCorpse = &corpse;
            ImprovedHuntersCorpsePredicate predicate = candidate => { corpseCalls++; return candidate == expectedCorpse; };
            var withoutCorpses = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", corpsePredicate: predicate);
            Check(!withoutCorpses.IsCollectibleCorpse(expectedCorpse) && corpseCalls == 0, "disabled corpse collection does not invoke IH");
            var withCorpses = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", collectsCorpses: true, corpsePredicate: predicate);
            Check(withCorpses.IsCollectibleCorpse(expectedCorpse) && corpseCalls == 1, "exact IH corpse pointer and admission");
            Check(!withCorpses.IsCollectibleCorpse(null) && corpseCalls == 2, "IH corpse rejection preserved");
            var failedCorpse = new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "", collectsCorpses: true,
                corpsePredicate: candidate => throw new InvalidOperationException("unavailable"));
            bool propagated = false;
            try { failedCorpse.IsCollectibleCorpse(expectedCorpse); } catch (InvalidOperationException) { propagated = true; }
            Check(propagated, "failed IH query cannot masquerade as prey absence");
            Console.WriteLine("PASS " + checks + " hunter prey/drain checks"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static HuntablePreyEntry Entry(int type, string owner, string id)
    {
        var constructor = typeof(HuntablePreyEntry).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
        var parameters = constructor.GetParameters(); var args = new object[parameters.Length];
        for (int index = 0; index < args.Length; index++)
        {
            var p = parameters[index];
            args[index] = p.Name == "unitType" ? (object)type : p.Name == "ownerGuid" ? owner : p.Name == "registrationId" ? id :
                p.ParameterType == typeof(string) ? "" : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
        }
        return (HuntablePreyEntry)constructor.Invoke(args);
    }
}
