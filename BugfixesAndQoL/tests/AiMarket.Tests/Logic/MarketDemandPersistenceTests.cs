using System;
using System.IO;
using BugfixesAndQoL.AI.Market;

internal static class MarketDemandPersistenceTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Market persistence contract failed."); }
    private static void Rejected(Action action)
    { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid ledger accepted."); }
    internal static void Run()
    {
        MarketDemandTarget.TryStartPackage(0, 5, 1, 5, out var batch);
        Check(batch.TryReconcile(5, 1, 1, 5, out int remaining) && remaining == 4);
        Check(batch.ObserveStock(4, 0, 1)); // Consumption must not undo a receipt, including across saves.
        var row = new MarketDemandSaveRow { Player = 1, Good = 17, SourceRva = 0x40D62, PolicySourceRva = 0x40D62, GoalAmount = 5, Target = batch };
        byte[] encoded = MarketDemandSaveCodec.Encode(new[] { row });
        var loaded = MarketDemandSaveCodec.Decode(encoded);
        Check(loaded.Length == 1 && loaded[0].Target.Credited == 1 && loaded[0].Target.ExpectedPending == 4);
        var restored = loaded[0].Target;
        Check(restored.ObserveFixedStore(0, 4, 5, 1, 5, MarketDemandSourceFamily.Recruit, out bool refreshed) && refreshed);
        Check(restored.TryReconcile(5, 0, 1, 5, out remaining) && remaining == 4);
        Check(MarketDemandSaveCodec.Encode(Array.Empty<MarketDemandSaveRow>()).Length == 8);
        var material = default(MarketDemandTarget);
        Check(material.ObserveMaterialStore(12, 0, 10, 2, 1, 0, out _));
        var materialRow = new MarketDemandSaveRow { Player = 2, Good = 2, SourceRva = 0xCC555, PolicySourceRva = 0xCC555, SourceStock = 12, GoalAmount = 10, Target = material };
        var ammo = default(MarketDemandTarget);
        Check(ammo.ObserveAutoAmmoStore(9, 0, 10, 10, 1, 1, false, out _));
        var ammoRow = new MarketDemandSaveRow { Player = 3, Good = 4, SourceRva = 0xD5791, PolicySourceRva = 0xD5791,
            Target = ammo, GoalAmount = 10, SourceStock = 9, UnitId = 33, UnitOwner = 3, UnitGlobalId = uint.MaxValue, UnitCreationTick = uint.MaxValue };
        loaded = MarketDemandSaveCodec.Decode(MarketDemandSaveCodec.Encode(new[] { row, materialRow, ammoRow }));
        Check(loaded[1].Target.MaterialCost == 20 && loaded[1].Target.MaterialPadding == 2);
        Check(loaded[2].Target.Policy == MarketDemandPolicy.AutoAmmo && loaded[2].UnitGlobalId == uint.MaxValue);
        var loadedMaterial = loaded[1].Target;
        Check(loadedMaterial.TryReconcile(10, 18, 1, 0, out int materialNeed) && materialNeed == 4);
        Check(loadedMaterial.TryReconcile(4, 20, 1, 0, out materialNeed) && materialNeed == 0 && !loadedMaterial.Active);
        var loadedAmmo = loaded[2].Target;
        Check(loadedAmmo.TryReconcileAutoAmmo(10, 9, 1, 1, 1, out int ammoNeed) && ammoNeed == 1);
        Check(loadedAmmo.TryReconcileAutoAmmo(1, 9, 1, 1, 0, out ammoNeed) && ammoNeed == 0 && !loadedAmmo.Active);
        Rejected(() => MarketDemandSaveCodec.Encode(new[] { row, row }));
        Rejected(() => MarketDemandSaveCodec.Encode(new[] { materialRow, row }));
        Rejected(() => MarketDemandSaveCodec.Decode(new byte[32769]));
        Rejected(() => MarketDemandSaveCodec.Decode(new byte[8]));
        byte[] truncated = new byte[encoded.Length - 1]; Array.Copy(encoded, truncated, truncated.Length);
        Rejected(() => MarketDemandSaveCodec.Decode(truncated));
        var all = new MarketDemandSaveRow[184]; int index = 0;
        for (int player = 1; player <= 8; player++) for (int good = 1; good < 25; good++) if (good != 15)
        { var r = row; r.Player = player; r.Good = good; all[index++] = r; }
        Check(index == 184);
        encoded = MarketDemandSaveCodec.Encode(all);
        Check(encoded.Length == 16936 && encoded.Length <= MarketDemandSaveCodec.MaximumBytes);
        Check(MarketDemandSaveCodec.Decode(encoded).Length == 184);

        // Actual production admission state: archive preparation/restore and OnStart share one attempt.
        var host = new MarketDemandMissionState(); var client = new MarketDemandMissionState();
        Check(host.Begin(10, false, true) && client.Begin(10, false, true));
        Check(!host.CanObserve && !client.CanObserve);
        host.Agreed = client.Agreed = true;
        Check(!host.CanObserve && !client.CanObserve); // BeforeLoad still belongs to the old native world.
        host.NativeArmed = client.NativeArmed = true;
        Check(host.CanObserve && client.CanObserve);
        // Gameplay admission and native quantities must still agree on both peers.
        Check(!host.Begin(10, false, true) && !client.Begin(10, false, true));
        var h = loaded[0].Target; var c = loaded[0].Target;
        h.ObserveStock(4, 1, 1); c.ObserveStock(4, 1, 1);
        Check(h.TryReconcile(4, 1, 1, 5, out int hp) == c.TryReconcile(4, 1, 1, 5, out int cp) && hp == cp && hp == 3);
        Check(host.CanObserve && client.CanObserve);
        host.InRepair = client.InRepair = true;
        Check(!host.CanObserve && !client.CanObserve);
        host.InRepair = client.InRepair = false;
        Check(host.CanObserve && client.CanObserve);
        host.End(); Check(!host.CanObserve);
        Check(host.Begin(20, true, false) && !host.CanObserve); // Editor explicitly excluded.
        Check(host.Begin(21, false, false) && !host.CanObserve);
        host.NativeArmed = true; Check(host.CanObserve); // SP does not depend on peer agreement.
        host.Agreed = false; Check(host.CanObserve);
        Console.WriteLine("PASS: deterministic bounded B/M/A save codec; permanent credit, admission, replay-idempotence, editor exclusion, SP/MP and repair fence.");
    }
}
