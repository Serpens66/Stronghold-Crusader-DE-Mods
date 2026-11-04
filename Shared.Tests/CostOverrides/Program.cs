using System;
using System.Collections.Generic;
using Shared;
using APIShared.Economy;
using APIShared.Modifiers;
using UnitCosts;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

internal static class Program
{
    private const eChimps Unit = eChimps.CHIMP_TYPE_KNIGHT;
    private const eStructs Tent = eStructs.STRUCT_SIEGE_TENT_CATAPULT;
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("neutral settings never write, including host/client startup differences", Neutral);
            Run("explicit unit and tent gold have independent restore values", UnitGold);
            Run("material cells restore independently including the knight horse", Materials);
            Run("overrides rebase on foreign writes and do not claim equal values", Rebase);
            Run("mission end releases ownership for new maps and loaded saves", Sessions);
            Run("setter failures isolate recovery and session boundaries", Failures);
            Run("shared market bases do not compound and preserve foreign writes", SharedMarket);
            Run("shared market saves retain original bases and isolate partial setters", SharedMarketSave);
            Console.WriteLine("PASS: " + passed + " cost ownership behavior scenarios (real SE value types; no game process).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS: " + name); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception("Expected " + expected + ", actual " + actual);
    }

    private sealed class Fixture
    {
        internal int Gold = 91, TentGold = 123, Writes;
        internal UnitGoodCosts Goods = new UnitGoodCosts(eGoods32.STORED_SWORDS,
            eGoods32.STORED_METAL_ARMOUR, eGoods32.STORED_NULL, eGoods32._SE_REQUIRE_HORSE);
        internal readonly UnitCostOverrides Units;
        internal Fixture()
        {
            Units = new UnitCostOverrides(_ => Gold, (_, v) => { Gold = v; Writes++; },
                _ => TentGold, (_, v) => { TentGold = v; Writes++; },
                _ => Goods, (_, v) => { Goods = v; Writes++; });
        }
        internal void End()
        {
            Units.RestoreAll(ex => { throw ex; }, true);
        }
    }

    private static void Neutral()
    {
        var host = new Fixture(); var client = new Fixture { Gold = 17, TentGold = 44 };
        // Startup snapshots differ; Tweaker then synchronizes both peers before mission Start.
        host.Gold = client.Gold = 77; host.TentGold = client.TentGold = 88;
        foreach (var f in new[] { host, client })
        {
            for (int repeat = 0; repeat < 3; repeat++)
            {
                f.Units.ApplyGold(Unit, -1); f.Units.ApplyTentGold(Tent, -1);
                f.Units.ApplyMaterials(Unit, false);
                f.End(); // Disabled mod, end, or repeated cleanup must also be inert.
            }
            Equal(0, f.Writes); Equal(77, f.Gold); Equal(88, f.TentGold);
        }
    }

    private static void UnitGold()
    {
        var f = new Fixture();
        f.Units.ApplyGold(Unit, 20); f.Units.ApplyTentGold(Tent, 20);
        f.Units.ApplyGold(Unit, 20); f.Units.ApplyTentGold(Tent, 20);
        Equal(2, f.Writes);
        f.TentGold = 777; // A later BuildingCosts/Tweaker write is not ours.
        f.Units.ApplyGold(Unit, -1); f.Units.ApplyTentGold(Tent, -1);
        Equal(91, f.Gold); Equal(777, f.TentGold); Equal(3, f.Writes);
        f.Units.ApplyGold(Unit, 0); f.End(); Equal(91, f.Gold);
    }

    private static void Materials()
    {
        var f = new Fixture(); var original = f.Goods;
        f.Units.ApplyMaterials(Unit, true); Equal(3, f.Writes);
        Equal(eGoods32.STORED_NULL, f.Goods.cost4);
        f.Goods.cost2 = eGoods32.STORED_BOWS;
        f.Units.ApplyMaterials(Unit, false);
        Equal(original.cost1, f.Goods.cost1); Equal(eGoods32.STORED_BOWS, f.Goods.cost2);
        Equal(original.cost3, f.Goods.cost3); Equal(original.cost4, f.Goods.cost4);
        int writes = f.Writes; f.End(); Equal(writes, f.Writes);
    }

    private static void Rebase()
    {
        var f = new Fixture(); f.Units.ApplyGold(Unit, 20); f.Gold = 45;
        f.Units.ApplyGold(Unit, 30); f.Units.ApplyGold(Unit, 40); f.Units.ApplyGold(Unit, -1);
        Equal(45, f.Gold);
        f.Units.ApplyGold(Unit, 45); f.Gold = 60; f.End(); Equal(60, f.Gold);
    }

    private static void Sessions()
    {
        var f = new Fixture(); f.Units.ApplyGold(Unit, 20);
        f.End(); Equal(91, f.Gold);
        f.Gold = 66; // Next map/save's foreign value.
        f.Units.ApplyGold(Unit, -1); f.End(); Equal(66, f.Gold);
    }

    private static void Failures()
    {
        var values = new Dictionary<int, int> { [0] = 5, [1] = 6 };
        bool fail = false; int failures = 0;
        var owned = new OwnedValueOverrides<int, int>(key => values[key], (key, v) =>
        { if (fail && key == 0) throw new Exception("setter rejected"); values[key] = v; });
        owned.Apply(0, 10); owned.Apply(1, 11); fail = true;
        owned.RestoreAll(_ => failures++, true);
        Equal(1, failures); Equal(6, values[1]);
        fail = false; values[0] = 10; owned.Restore(0); Equal(10, values[0]);
        var partial = new OwnedValueOverrides<int, int>(key => values[key], (key, v) =>
        { values[key] = v; if (v == 22) throw new Exception("setter failed after mutation"); });
        try { partial.Apply(0, 22); } catch (Exception) { }
        partial.Restore(0); Equal(10, values[0]);
    }

    private static void SharedMarket()
    {
        int[] prices = { 100, 50 }; int writes = 0;
        var table = new MarketPriceTableCoordinator(1, (g, b) => prices[b ? 0 : 1],
            (g, b, v) => { prices[b ? 0 : 1] = v; writes++; }, (g, b) => b ? 100 : 50);
        table.Begin(null); decimal factor = 1;
        Func<int, bool, bool, int, int?, MarketPriceResolution> resolve = (g, b, ai, basis, foreign) =>
            foreign.HasValue ? new MarketPriceResolution(foreign.Value, false) : new MarketPriceResolution(
                (int)ValueModifierCalculator.Calculate(basis, new[] { ValueModifierContribution.Factor(ai ? 1 : factor) }, 0, 32767), factor != 1);
        Action<string> error = message => { throw new Exception(message); };
        table.Refresh(resolve, error); Equal(0, writes);
        factor = 1.5m; table.Refresh(resolve, error); table.Refresh(resolve, error);
        Equal(150, prices[0]); Equal(75, prices[1]); Equal(2, writes); Equal(100, table.AIPrice(0, true));
        prices[0] = 333; factor = 3; table.Refresh(resolve, error); Equal(333, prices[0]); Equal(150, prices[1]);
        prices[0] = 100; table.Refresh(resolve, error); Equal(100, prices[0]);
        factor = 1; table.Refresh(resolve, error); table.End(error); Equal(100, prices[0]); Equal(50, prices[1]);
    }

    private static void SharedMarketSave()
    {
        int[] prices = { 100, 50 }; bool fail = true; int errors = 0;
        var table = new MarketPriceTableCoordinator(1, (g, b) => prices[b ? 0 : 1],
            (g, b, v) => { prices[b ? 0 : 1] = v; if (b && fail) throw new Exception("partial setter"); }, (g, b) => b ? 100 : 50);
        table.Begin(null); decimal factor = 2;
        Func<int, bool, bool, int, int?, MarketPriceResolution> resolve = (g, b, ai, basis, foreign) =>
            new MarketPriceResolution(foreign ?? (int)(basis * factor), !foreign.HasValue && factor != 1);
        table.Refresh(resolve, _ => errors++); Equal(1, errors); Equal(200, prices[0]); Equal(100, prices[1]);
        fail = false; byte[] saved = table.Save();
        table.Begin(MarketPriceTableCoordinator.Decode(saved, 1)); factor = 3; table.Refresh(resolve, _ => errors++);
        Equal(300, prices[0]); Equal(150, prices[1]);
        saved = table.Save(); table.Begin(MarketPriceTableCoordinator.Decode(saved, 1)); table.Refresh(resolve, _ => errors++);
        Equal(300, prices[0]);
        table.End(_ => errors++); Equal(100, prices[0]); Equal(50, prices[1]);
        bool rejected = false; saved[20] = 2;
        try { MarketPriceTableCoordinator.Decode(saved, 1); } catch (InvalidOperationException) { rejected = true; }
        Equal(true, rejected);
    }
}
