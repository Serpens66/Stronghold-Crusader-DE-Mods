using System;
using System.Collections.Generic;
using Shared;
using UnitCosts;
using ExtraFeatures;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

internal static class Program
{
    private const eChimps Unit = eChimps.CHIMP_TYPE_KNIGHT;
    private const eStructs Tent = eStructs.STRUCT_SIEGE_TENT_CATAPULT;
    private const eGoods Good = eGoods.STORED_WOOD_PLANKS;
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("neutral settings never write, including host/client startup differences", Neutral);
            Run("explicit unit and tent gold have independent restore values", UnitGold);
            Run("material cells restore independently including the knight horse", Materials);
            Run("overrides rebase on foreign writes and do not claim equal values", Rebase);
            Run("market directions respect foreign absolute prices independently", ForeignMarket);
            Run("repeated price application and factor changes never compound", RepeatedMarket);
            Run("already effective external prices succeed without acquiring ownership", AlreadyEffectiveMarket);
            Run("return to neutral preserves foreign cells", MarketReset);
            Run("combined neutral and rounding retain existing arithmetic", Arithmetic);
            Run("mission end releases ownership for new maps and loaded saves", Sessions);
            Run("setter failures isolate recovery and session boundaries", Failures);
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
        internal PackedGoodPrice Price = new PackedGoodPrice(100, 50);
        internal readonly UnitCostOverrides Units;
        internal readonly MarketPriceOverrides Market;
        internal Fixture()
        {
            Units = new UnitCostOverrides(_ => Gold, (_, v) => { Gold = v; Writes++; },
                _ => TentGold, (_, v) => { TentGold = v; Writes++; },
                _ => Goods, (_, v) => { Goods = v; Writes++; });
            Market = new MarketPriceOverrides(_ => Price, (_, v) => { Price = v; Writes++; },
                _ => new PackedGoodPrice(100, 50));
        }
        internal void End()
        {
            Units.RestoreAll(ex => { throw ex; }, true);
            Market.RestoreAll(ex => { throw ex; }, true);
        }
    }

    private static void Neutral()
    {
        var host = new Fixture(); var client = new Fixture { Gold = 17, TentGold = 44 };
        // Startup snapshots differ; Tweaker then synchronizes both peers before mission Start.
        host.Gold = client.Gold = 77; host.TentGold = client.TentGold = 88;
        foreach (var f in new[] { host, client })
        {
            f.Price = new PackedGoodPrice(211, 119);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                f.Units.ApplyGold(Unit, -1); f.Units.ApplyTentGold(Tent, -1);
                f.Units.ApplyMaterials(Unit, false);
                Equal(true, f.Market.Apply(Good, true, 1)); Equal(true, f.Market.Apply(Good, false, 1));
                f.End(); // Disabled mod, end, or repeated cleanup must also be inert.
            }
            Equal(0, f.Writes); Equal(77, f.Gold); Equal(88, f.TentGold);
            Equal(211, f.Price.BuyPrice); Equal(119, f.Price.SellPrice);
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

    private static void ForeignMarket()
    {
        var f = new Fixture { Price = new PackedGoodPrice(111, 50) };
        Equal(false, f.Market.Apply(Good, true, 2)); Equal(true, f.Market.Apply(Good, false, 2));
        Equal(111, f.Price.BuyPrice); Equal(100, f.Price.SellPrice); Equal(1, f.Writes);
        f.End(); Equal(111, f.Price.BuyPrice); Equal(50, f.Price.SellPrice);
    }

    private static void AlreadyEffectiveMarket()
    {
        var f = new Fixture { Price = new PackedGoodPrice(200, 50) };
        Equal(true, f.Market.Apply(Good, true, 2, out MarketPriceApplication result));
        Equal(100, result.Baseline); Equal(200, result.Current); Equal(200, result.Desired);
        Equal(false, result.Owned); Equal(0, f.Writes);
        f.Market.Apply(Good, true, 1); Equal(200, f.Price.BuyPrice); Equal(0, f.Writes);
        Equal(false, f.Market.Apply(Good, true, 3, out result));
        Equal(200, result.Current); Equal(300, result.Desired);
        f.End(); Equal(200, f.Price.BuyPrice); Equal(0, f.Writes);
        var owned = new Fixture(); owned.Market.Apply(Good, true, 2);
        Equal(true, owned.Market.Apply(Good, true, 2, out result)); Equal(true, result.Owned);
        owned.End(); Equal(100, owned.Price.BuyPrice);
    }

    private static void RepeatedMarket()
    {
        var f = new Fixture();
        for (int i = 0; i < 4; i++) Equal(true, f.Market.Apply(Good, true, 1.5));
        Equal(150, f.Price.BuyPrice); Equal(1, f.Writes);
        Equal(true, f.Market.Apply(Good, true, 2)); Equal(200, f.Price.BuyPrice);
        Equal(true, f.Market.Apply(Good, true, 1)); Equal(100, f.Price.BuyPrice);
    }

    private static void MarketReset()
    {
        var f = new Fixture(); f.Market.Apply(Good, true, 2); f.Market.Apply(Good, false, 2);
        f.Price = new PackedGoodPrice(321, f.Price.SellPrice);
        Equal(false, f.Market.Apply(Good, true, 3));
        f.Market.Apply(Good, true, 1); f.Market.Apply(Good, false, 1);
        Equal(321, f.Price.BuyPrice); Equal(50, f.Price.SellPrice);
    }

    private static void Arithmetic()
    {
        var f = new Fixture();
        f.Market.Apply(Good, true, MarketGoodPriceDefinition.CombineMultipliers(2, 0.5));
        Equal(0, f.Writes);
        f.Market.Apply(Good, false, 1.25); Equal(63, f.Price.SellPrice);
        f.Market.Apply(Good, true, 0); Equal(0, f.Price.BuyPrice);
        f.Market.Apply(Good, true, 2); Equal(200, f.Price.BuyPrice);
    }

    private static void Sessions()
    {
        var f = new Fixture(); f.Units.ApplyGold(Unit, 20); f.Market.Apply(Good, true, 2);
        f.End(); Equal(91, f.Gold); Equal(100, f.Price.BuyPrice);
        f.Gold = 66; f.Price = new PackedGoodPrice(404, 50); // Next map/save's foreign values.
        f.Units.ApplyGold(Unit, -1); Equal(false, f.Market.Apply(Good, true, 2));
        f.End(); Equal(66, f.Gold); Equal(404, f.Price.BuyPrice);
        f.Price = new PackedGoodPrice(100, 50); // A fresh load resets the native table.
        f.Market.Apply(Good, true, 2); f.Market.Apply(Good, true, 2);
        Equal(200, f.Price.BuyPrice); f.End(); Equal(100, f.Price.BuyPrice);
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
}
