using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIWoodSafetyTest.Tests
{
    [TestClass]
    public sealed unsafe class PolicyTests
    {
        private static Fortification Fort(int slot = 1, int owner = 2, uint global = 10, eStructs type = eStructs.STRUCT_TOWER1, int side = 20) =>
            new Fortification(slot, global, owner, type, new SafetyRect(100, 100, side), true);
        [TestMethod]
        public void InclusiveRangeUsesWholeFootprintsAndEuclideanDistance()
        {
            var masks = new ThreatMasks(); masks.SetRange(54); masks.Update(1, Fort());
            Assert.IsTrue(masks.Blocks(1 << 2, 173, 110)); // 173 - last tower tile 119 = 54
            Assert.IsFalse(masks.Blocks(1 << 2, 174, 110));
            Assert.IsTrue(masks.Blocks(1 << 2, 44, 110)); // hut ends at 46; 100 - 46 = 54
            Assert.IsFalse(masks.Blocks(1 << 2, 43, 110));
            Assert.IsTrue(masks.Blocks(1 << 2, 151, 151)); // sqrt(32²+32²)
            Assert.IsFalse(masks.Blocks(1 << 2, 158, 158)); // sqrt(39²+39²)
            Assert.IsFalse(masks.Blocks(1 << 2, -1, 110)); Assert.IsFalse(masks.Blocks(1 << 2, 798, 110));
        }
        [TestMethod]
        public void AllNativeTowerAndGateVariantsButNoRuinsOrEconomyBuildings()
        {
            int intact = 0, ruins = 0;
            foreach (eStructs type in Enum.GetValues(typeof(eStructs)))
            {
                string name = type.ToString();
                if (!name.StartsWith("STRUCT_TOWER") && !name.StartsWith("STRUCT_GATE")) continue;
                if (name.EndsWith("DESTROYED")) { Assert.IsFalse(Fortification.IsIntactType(type)); ruins++; }
                else { Assert.IsTrue(Fortification.IsIntactType(type), name); intact++; }
            }
            Assert.IsTrue(intact >= 18); Assert.AreEqual(5, ruins);
            Assert.IsFalse(Fortification.IsIntactType(eStructs.STRUCT_WOODCUTTERS_HUT));
        }
        [TestMethod]
        public void OverlapCaptureDeletionRebuildAndRuinsUpdateReferenceCounts()
        {
            var masks = new ThreatMasks(); masks.SetRange(54);
            masks.Update(1, Fort()); masks.Update(2, Fort(2));
            int cell = 2 * ThreatMasks.OwnerStride + 150 * 800 + 150;
            Assert.AreEqual((ushort)2, masks.Counts[cell]);
            masks.Update(1, Fort(owner: 3, global: 11));
            Assert.AreEqual((ushort)1, masks.Counts[cell]); Assert.IsTrue(masks.Blocks(1 << 3, 150, 150));
            masks.Update(2, Fort(2, type: eStructs.STRUCT_TOWER1_DESTROYED));
            Assert.AreEqual((ushort)0, masks.Counts[cell]); Assert.IsFalse(masks.Blocks(1 << 2, 150, 150));
            masks.Update(1, null); Assert.IsFalse(masks.Blocks(1 << 3, 150, 150));
            masks.Update(1, Fort(global: 12)); Assert.IsTrue(masks.Blocks(1 << 2, 150, 150));
            masks.Update(1, Fort(global: 13, type: eStructs.STRUCT_WOODCUTTERS_HUT)); Assert.AreEqual(1, masks.RecordCount); // retained ruin only
            Assert.IsFalse(masks.Blocks(1 << 2, 150, 150));
            masks.Clear(); Assert.AreEqual(0, masks.RecordCount);
        }
        [TestMethod]
        public void AllianceAndEffectiveRangeChangesDoNotNeedBuildingChanges()
        {
            var masks = new ThreatMasks(); masks.SetRange(54); masks.Update(1, Fort()); masks.Update(2, Fort(2, 3));
            int[] teams = { 0, 1, 2, 1, 4, 5, 6, 7, 8 };
            Assert.AreEqual(1 << 2, masks.EnemyOwnerBits(1, teams));
            teams[2] = 1; Assert.AreEqual(0, masks.EnemyOwnerBits(1, teams));
            teams[3] = 3; Assert.AreEqual(1 << 3, masks.EnemyOwnerBits(1, teams));
            Assert.AreEqual(1 << 3, masks.EnemyOwnerBits(2, teams)); // own never hostile
            masks.SetRange(10); Assert.IsFalse(masks.Blocks(1 << 3, 150, 150));
            masks.SetRange(54); Assert.IsTrue(masks.Blocks(1 << 3, 150, 150));
        }
        [TestMethod]
        public void FineMaskMatchesDirectGeometryAtArbitraryAivCoordinates()
        {
            var masks = new ThreatMasks(); masks.SetRange(54);
            var forts = new[] { Fort(side: 4), new Fortification(2, 22, 8, eStructs.STRUCT_GATE_STONE2B, new SafetyRect(760, 750, 20), true) };
            foreach (var f in forts) masks.Update(f.Slot, f);
            var rng = new Random(812);
            for (int i = 0; i < 10000; i++)
            {
                int x = rng.Next(798), y = rng.Next(798);
                foreach (var f in forts)
                    Assert.AreEqual(SafetyRect.DistanceSquared(new SafetyRect(x, y, 3), f.Footprint) <= 54 * 54,
                        masks.Blocks(1 << f.Owner, x, y), x + "," + y);
            }
        }
        private static GameUnit Worker() => new GameUnit { r_GlobalId = 70, r_UnitChimp = eChimps.CHIMP_TYPE_WOODCUTTER,
            r_AIState = 110, r_AliveState = AliveState.MarkedForDeletion, r_CurrentHealth = 0, r_IsKilledByProjectile = 1,
            r_LinkedProductionBuildingId = 4, r_LinkedProductionBuildingGlobalId = 80, r_ControllableForPlayerId = 3 };
        private static GameBuilding Hut() => new GameBuilding { r_GlobalId = 80, r_PlayerIdOwner = 3,
            r_BuildingType = eStructs.STRUCT_WOODCUTTERS_HUT, r_AliveState = AliveState.IsAlive };
        [TestMethod]
        public void ActualWriterRemovesOnlyCurrentDeathTenIncludingUnknownLoadedHistory()
        {
            WoodSafetyRuntime.ValidateLayout();
            foreach (short before in new short[] { 0, 7, 20, 100, 32760 }) foreach (ushort marker in new ushort[] { 1, 2 })
            {
                var unit = Worker(); var hut = Hut(); unit.r_IsKilledByProjectile = marker;
                short after = unchecked((short)(before + 10)); *((short*)((byte*)&hut + 0x2DC)) = after;
                Assert.IsTrue(DeathCounterPolicy.TryUndo(&unit, 6, 70, &hut, 4, 80, after, true));
                Assert.AreEqual(before, *((short*)((byte*)&hut + 0x2DC)));
                Assert.IsFalse(DeathCounterPolicy.TryUndo(&unit, 6, 70, &hut, 4, 80, after, true)); // duplicate callback
            }
        }
        [TestMethod]
        public void LiveReturnFailureStaleIdentityOrContradictionNeverWritesCounter()
        {
            for (int scenario = 0; scenario < 13; scenario++)
            {
                var unit = Worker(); var hut = Hut(); *((short*)((byte*)&hut + 0x2DC)) = 27;
                uint ug = 70, bg = 80; bool ai = true; int uid = 6, bid = 4; short expected = 27;
                switch (scenario)
                {
                    case 0: unit.r_CurrentHealth = 10; unit.r_IsKilledByProjectile = 0; break;
                    case 1: unit.r_IsKilledByProjectile = 0; break;
                    case 2: ug++; break; case 3: bg++; break;
                    case 4: unit.r_LinkedProductionBuildingGlobalId++; break;
                    case 5: unit.r_LinkedProductionBuildingId++; break;
                    case 6: unit.r_ControllableForPlayerId = 2; break;
                    case 7: ai = false; break; case 8: uid = 0; break; case 9: bid = 5; break;
                    case 10: expected++; break; case 11: hut.r_AliveState = AliveState.MarkedForDeletion; break;
                    case 12: unit.r_AIState = 111; break;
                }
                Assert.IsFalse(DeathCounterPolicy.TryUndo(&unit, uid, ug, &hut, bid, bg, expected, ai), "scenario=" + scenario);
                Assert.AreEqual((short)27, *((short*)((byte*)&hut + 0x2DC)));
            }
        }
    }
}
