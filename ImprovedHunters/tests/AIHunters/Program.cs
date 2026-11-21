using System;
using System.Diagnostics;
using System.Globalization;
using ImprovedHunters;
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
            var schedule = new HunterCheckSchedule();
            Check(!schedule.TryBeginCheck(80000), "loaded save anchors at its own restored tick");
            Check(!schedule.TryBeginCheck(80000), "paused start cannot consume delay");
            Check(!schedule.TryBeginCheck(80199), "no early automatic attempt");
            Check(schedule.TryBeginCheck(80200), "first attempt after five simulation seconds");
            Check(!schedule.TryBeginCheck(80200), "same tick cannot repeat attempt");
            Check(!schedule.TryBeginCheck(84999), "ordinary retry remains two minutes");
            Check(schedule.TryBeginCheck(85000), "retry two minutes after first attempt");
            schedule = default;
            Check(!schedule.TryBeginCheck(12) && !schedule.TryBeginCheck(211) && schedule.TryBeginCheck(212), "new map resets first delay");
            schedule = default;
            Check(!schedule.TryBeginCheck(int.MaxValue-100) && schedule.TryBeginCheck(unchecked(int.MaxValue-100+200)), "first delay tolerates native tick wrap");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 0, false) == 0x1FE, "neutral prey all players");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 2, false) == 0, "owned prey excluded");
            Check(PreyPresencePolicy.OwnerMask(true, true, false, 2, true) == 4, "own prey correct player");
            Check(PreyPresencePolicy.OwnerMask(true, false, false, 0, false) == 0, "dead exhausted");
            Check(PreyPresencePolicy.OwnerMask(true, false, true, 0, false) == 0x1FE, "collectible corpse");

            var reservation = new AivPlanReservation(100, 100, 32, 3);
            Check(reservation.ReservePlacement(eMappers.MAPPER_HUNTER, 100, 100, AivRotation.South, false), "known AIV footprint");
            Check(reservation.Overlaps(98, 98, 3), "hunter edge overlaps future footprint");
            Check(!reservation.Overlaps(97, 97, 3), "touching corners without overlap allowed");
            Check(!reservation.Overlaps(103, 100, 3), "adjacent nonoverlapping placement allowed");
            Check(reservation.Overlaps(67, 100, 3), "outside captured search bounds fails closed");
            Check(!reservation.ReservePlacement((eMappers)30000, 100, 100, AivRotation.South, false), "unknown mapper refuses capture");
            Check(!reservation.ReservePlacement(eMappers.MAPPER_HUNTER, 100, 100, AivRotation.Default, false), "unknown AIV rotation refuses capture");
            var clipped = new AivPlanReservation(100, 100, 32, 3);
            clipped.ReservePlacement(eMappers.MAPPER_HUNTER, 66, 68, AivRotation.South, false);
            Check(clipped.Overlaps(68, 68, 1), "reservation clipped at search edge still protects boundary");
            foreach (var mapper in new[] { eMappers.MAPPER_WALL, eMappers.MAPPER_CRENAL, eMappers.MAPPER_CRENAL2,
                eMappers.MAPPER_WOODWALL, eMappers.MAPPER_MOAT, eMappers.MAPPER_PITCH_DITCH })
            {
                var grouped = new AivPlanReservation(100, 100, 32, 3);
                grouped.ReservePlacement(mapper, 100, 100, AivRotation.East, true);
                Check(grouped.Overlaps(100,100,1) && !grouped.Overlaps(101,100,1), "grouped AIV tile is exactly one cell: " + mapper);
            }
            // Independent coordinates from the audited native component tables, not the production rotation calculation.
            var rotations = new[] { AivRotation.South, AivRotation.East, AivRotation.North, AivRotation.West };
            int[] guildX = {100,105,100,95}, guildY = {105,100,95,100};
            int[] outsideGuildX = {105,100,95,100}, outsideGuildY = {100,95,100,105};
            int[] oilX = {100,104,100,96}, oilY = {104,100,96,100};
            int[] barracksMinX = {100,100,95,95}, barracksMinY = {100,95,95,100};
            for (int direction = 0; direction < rotations.Length; direction++)
            {
                foreach (var mapper in new[] { eMappers.MAPPER_BEDOUIN_STOCKADE, eMappers.MAPPER_BARRACKS_WOOD, eMappers.MAPPER_BARRACKS_STONE })
                {
                    var compound = new AivPlanReservation(100,100,32,3);
                    compound.ReservePlacement(mapper,100,100,rotations[direction],false);
                    for (int x = barracksMinX[direction]; x < barracksMinX[direction]+10; x++)
                        for (int y = barracksMinY[direction]; y < barracksMinY[direction]+10; y++)
                            Check(compound.Overlaps(x,y,1), "complete barracks/stockade protected");
                    Check(!compound.Overlaps(barracksMinX[direction]-1,barracksMinY[direction],1), "compound does not reserve extra outside column");
                }
                foreach (var mapper in new[] { eMappers.MAPPER_ENGINEERS_GUILD, eMappers.MAPPER_TUNNELERS_GUILD })
                {
                    var guild = new AivPlanReservation(100,100,32,3);
                    guild.ReservePlacement(mapper,100,100,rotations[direction],false);
                    Check(guild.Overlaps(guildX[direction],guildY[direction],1), "rotated guild extra component");
                    Check(!guild.Overlaps(outsideGuildX[direction],outsideGuildY[direction],1), "guild does not reserve full barracks square");
                }
                var oil = new AivPlanReservation(100,100,32,3);
                oil.ReservePlacement(eMappers.MAPPER_OIL_SMELTER,100,100,rotations[direction],false);
                Check(oil.Overlaps(oilX[direction],oilY[direction],1), "rotated oil smelter extra component");
            }

            Check(HunterHutPolicy.AllowsBuild(false, true, 0), "chickens permit first AIV hut");
            Check(!HunterHutPolicy.AllowsBuild(false, true, 1), "chickens do not permit second AIV hut");
            Check(HunterHutPolicy.AllowsBuild(true, true, 4), "wildlife permits more AIV huts");
            Check(!HunterHutPolicy.AllowsBuild(false, false, 0), "no food no first hut");
            Check(HunterHutPolicy.Keep(false, true, 44, 44), "chicken baseline retained");
            Check(!HunterHutPolicy.Keep(false, true, 45, 44), "extra hut retired despite chickens");
            Check(!HunterHutPolicy.Keep(false, false, 44, 44), "baseline retired when all food gone");
            Check(HunterHutPolicy.Keep(true, false, 45, 44), "wildlife retains every hut");
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
            var reused = new AivPlanReservation(100, 100, 32, 3);
            reused.ReservePlacement(eMappers.MAPPER_HUNTER, 100, 100, AivRotation.South, false);
            reused.Reset(200, 200, 32, 3);
            Check(!reused.Overlaps(200, 200, 3), "reused reservation clears previous search contents");
            Check(reused.Overlaps(100, 100, 3), "reused reservation rejects coordinates outside new capture bounds");
            reused.ReservePlacement(eMappers.MAPPER_HUNTER, 200, 200, AivRotation.South, false);
            Check(reused.Overlaps(200, 200, 3), "reused reservation accepts new anchor");
            CheckFullyReservedAivSearch();
            CheckWorldIndex();
            CheckScheduleAllocation();
            BenchmarkHelpers();
            Console.WriteLine("PASS " + checks + " hunter prey/drain checks"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    // This exercises the compiled production value-type scheduler, including periodic due ticks.
    // It is a CLR helper regression, not a measurement of Unity/Mono or of the full game callback.
    private static void CheckScheduleAllocation()
    {
        var schedule = new HunterCheckSchedule();
        for (int tick = 0; tick < 20000; tick++) schedule.TryBeginCheck(tick);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int due = 0;
        for (int tick = 20000; tick < 1020000; tick++) if (schedule.TryBeginCheck(tick)) due++;
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(due > 200 && due < 210, "steady scheduler still admits ordinary checks");
        Check(bytes == 0, "steady production scheduler allocates no memory");
        Console.WriteLine("ALLOCATION production-schedule calls=1000000 due=" + due + " bytes=" + bytes);
    }

    private static long benchmarkSink;
    private static void CheckFullyReservedAivSearch()
    {
        var empty = new AivPlanReservation(100, 100, 32, 3);
        Check(!empty.CoversSearchArea(100, 100, 32, 4, 3), "empty AIV cannot permanently suppress future search");
        empty.ReservePlacement(eMappers.MAPPER_WALL, 68, 68, AivRotation.South, true);
        Check(!empty.CoversSearchArea(100, 100, 32, 4, 3), "one protected candidate does not cover the whole search");

        var full = ReservedAivSearchExcept(-1, -1);
        Check(full.CoversSearchArea(100, 100, 32, 4, 3), "fully AIV-reserved search can be skipped for this mission");
        var sparse = new AivPlanReservation(100, 100, 32, 3);
        for (int y = 68; y <= 132; y += 4)
            for (int x = 68; x <= 132; x += 4)
                if (x != 100 || y != 100)
                    sparse.ReservePlacement(eMappers.MAPPER_WALL, x, y, AivRotation.South, true);
        Check(!sparse.Overlaps(69, 68, 1), "sparse AIV fixture leaves most footprint tiles unreserved");
        Check(sparse.CoversSearchArea(100, 100, 32, 4, 3), "one planned tile overlapping each candidate is sufficient to block all construction");
        Check(!full.CoversSearchArea(101, 100, 32, 4, 3), "changed anchor X must not reuse a differently clipped capture");
        Check(!full.CoversSearchArea(100, 101, 32, 4, 3), "changed anchor Y must not reuse a differently clipped capture");
        Check(!full.CoversSearchArea(100, 100, 28, 4, 3), "changed search radius must not reuse capture bounds");
        Check(!full.CoversSearchArea(100, 100, 32, 4, 2), "changed hunter footprint must not reuse capture bounds");

        // An independent tile mask leaves one complete hunter footprint free. This corner is
        // candidate288 in the builder's ring order, beyond its ordinary256-candidate work cap.
        // Permanent suppression must inspect the entire policy search, including that late site.
        var lateGap = ReservedAivSearchExcept(132, 128);
        Check(!lateGap.Overlaps(132, 128, 3), "late fixture actually leaves all nine footprint tiles free");
        Check(!lateGap.CoversSearchArea(100, 100, 32, 4, 3), "free final candidate prevents permanent suppression despite normal work cap");
        var firstGap = ReservedAivSearchExcept(96, 96);
        Check(!firstGap.CoversSearchArea(100, 100, 32, 4, 3), "free first ring candidate prevents permanent suppression");
        var centerGap = ReservedAivSearchExcept(100, 100);
        Check(centerGap.CoversSearchArea(100, 100, 32, 4, 3), "unsearched center is excluded from full-coverage decision");
        full.Reset(100, 100, 32, 3);
        Check(!full.CoversSearchArea(100, 100, 32, 4, 3), "reused capture clears the prior full-coverage conclusion");
    }

    private static AivPlanReservation ReservedAivSearchExcept(int freeX, int freeY)
    {
        var plan = new AivPlanReservation(100, 100, 32, 3);
        // One-tile ordered AIV wall cells independently describe the complete clipped capture.
        // Only a requested3x3 free patch is omitted; no production candidate enumeration is copied.
        for (int y = 68; y <= 134; y++)
            for (int x = 68; x <= 134; x++)
                if (x < freeX || x >= freeX + 3 || y < freeY || y >= freeY + 3)
                    if (!plan.ReservePlacement(eMappers.MAPPER_WALL, x, y, AivRotation.South, true))
                        throw new Exception("known single-tile AIV fixture rejected");
        return plan;
    }

    private static void CheckWorldIndex()
    {
        var index = new AIHunterWorldIndex();
        index.SetPlayer(2, true, 1);
        var granary = new GameBuilding { r_AliveState = AliveState.IsAlive, r_PlayerIdOwner = 2,
            r_BuildingType = eStructs.STRUCT_GRANARY, r_GlobalId = 1, r_TilePositionXBegin = 20, r_TilePositionYBegin = 20 };
        var distant = new GameBuilding { r_AliveState = AliveState.IsAlive, r_PlayerIdOwner = 2,
            r_BuildingType = eStructs.STRUCT_HUNTERS_HUT, r_GlobalId = 44, r_TilePositionXBegin = 30, r_TilePositionYBegin = 20 };
        var near = distant; near.r_GlobalId = 45; near.r_TilePositionXBegin = 21; near.r_AliveState = AliveState.NeedsInit;
        index.ObserveBuilding(2, in distant); index.ObserveBuilding(3, in near); index.ObserveBuilding(1, in granary);
        index.ObserveBuilding(2, in distant);
        index.SelectBaselines();
        Check(index.Huts.Count == 2, "index deduplicates hut identity and includes pending hut");
        Check(index.Baseline[2] == 45, "index baseline independent of observation order");
        var spawnedNearer = near; spawnedNearer.r_GlobalId = 46; spawnedNearer.r_TilePositionXBegin = 20;
        index.ObserveBuilding(4, in spawnedNearer);
        Check(index.Baseline[2] == 0, "new hut invalidates a previously selected baseline");
        index.SelectBaselines();
        Check(index.Baseline[2] == 46, "new closer hut is retained instead of retired by stale baseline");
        var humanHut = near; humanHut.r_PlayerIdOwner = 1; humanHut.r_GlobalId = 70;
        var deletedHut = near; deletedHut.r_AliveState = AliveState.MarkedForDeletion; deletedHut.r_GlobalId = 71;
        index.ObserveBuilding(7, in humanHut); index.ObserveBuilding(8, in deletedHut);
        Check(index.Huts.Count == 3, "human and already marked huts do not enter AI retirement candidates");
        Check(index.SpawnBuildings.Count == 1 && index.SpawnBuildings[0].GlobalId == 1, "index retains spawn-site identity for live revalidation");
        index.SetPlayer(2, true, 99);
        Check(index.Baseline[2] == 0, "changed first granary immediately invalidates selected baseline");
        index.SelectBaselines();
        Check(index.Baseline[2] == 44, "changed unknown granary cannot reuse the old coordinates");
        var initializingMill = granary; initializingMill.r_GlobalId = 2;
        initializingMill.r_AliveState = AliveState.NeedsInit; initializingMill.r_BuildingType = eStructs.STRUCT_MILL;
        index.ObserveBuilding(5, in initializingMill); index.ObserveBuilding(5, in initializingMill);
        Check(index.SpawnBuildings.Count == 2, "initializing renewal sites are retained once for later revalidation");
        var worker = new GameUnit { r_AliveState = AliveState.IsAlive, r_UnitChimp = eChimps.CHIMP_TYPE_HUNTER,
            r_LinkedProductionBuildingGlobalId = 44, r_AIState = 0 };
        index.ObserveWorker(in worker);
        Check(index.BusyHuts.Count == 0, "index ignores idle empty hunter");
        worker.r_AIState = 4; index.ObserveWorker(in worker); index.ObserveWorker(in worker);
        Check(index.BusyHuts.Count == 1 && index.BusyHuts.Contains(44), "index protects deferred delivery exactly once");
        worker.r_UnitChimp = eChimps.CHIMP_TYPE_DOG; worker.r_LinkedProductionBuildingGlobalId = 45;
        index.ObserveWorker(in worker);
        Check(!index.BusyHuts.Contains(45), "index ignores hound animation state");
        index.Clear();
        Check(index.Huts.Count == 0 && index.SpawnBuildings.Count == 0 && index.BusyHuts.Count == 0 && index.Baseline[2] == 0 && !index.AI[2],
            "index reset drops previous session identities and ownership");
        index.SetPlayer(2, true, 0); index.ObserveBuilding(3, in near); index.ObserveBuilding(2, in distant); index.SelectBaselines();
        Check(index.Baseline[2] == 44, "index missing granary uses stable lowest global identity");
        index.Clear(); index.ObserveBuilding(2, in distant);
        Check(index.Huts.Count == 0, "index does not reuse previous AI roster after clear");
        index.SetPlayer(2, true, 0); index.ObserveBuilding(5, in initializingMill);
        Check(index.SpawnBuildings.Count == 1, "pending-only spawn site is retained for current-state validation");
        initializingMill.r_AliveState = AliveState.IsAlive; index.ObserveBuilding(5, in initializingMill);
        Check(index.SpawnBuildings.Count == 1, "newly initialized site retains exactly one stable identity");
        index.Clear(); index.SetPlayer(2, true, 6);
        var pendingGranary = granary; pendingGranary.r_GlobalId = 6; pendingGranary.r_AliveState = AliveState.NeedsInit;
        index.ObserveBuilding(2, in distant); index.ObserveBuilding(3, in near); index.ObserveBuilding(6, in pendingGranary);
        index.SelectBaselines();
        Check(index.Baseline[2] == 45, "new pending first granary supplies current anchor coordinates");
        index.Clear(); index.SetPlayer(2, true, 1);
        index.ObserveBuilding(2, in distant); index.ObserveBuilding(3, in near); index.SelectBaselines();
        Check(index.Baseline[2] == 44, "unobserved first granary initially uses stable identity fallback");
        index.ObserveBuilding(1, in granary);
        Check(index.Baseline[2] == 0, "first observed granary invalidates fallback baseline");
        index.SelectBaselines();
        Check(index.Baseline[2] == 45, "newly known granary chooses the nearer higher identity");
        index.ObserveBuilding(1, in granary);
        Check(index.Baseline[2] == 45, "identical granary observation keeps a valid selected baseline");
        var movedGranary = granary; movedGranary.r_TilePositionXBegin = 100;
        index.ObserveBuilding(1, in movedGranary);
        Check(index.Baseline[2] == 0, "changed granary coordinates invalidate cached selection");
        index.SelectBaselines();
        Check(index.Baseline[2] == 44, "changed anchor recomputes the nearest retained hut");
    }

    private static void BenchmarkHelpers()
    {
        // Only game-memory input is replaced. ObserveBuilding/ObserveWorker and baseline selection
        // execute from the compiled production mod. This excludes native placement and is not FPS evidence.
        var buildings = new GameBuilding[2000];
        var units = new GameUnit[10000];
        var world = new AIHunterWorldIndex();
        for (int index = 0; index < buildings.Length; index++)
            buildings[index] = new GameBuilding { r_PlayerIdOwner = (ushort)(index % 8 + 1),
                r_AliveState = AliveState.IsAlive,
                r_BuildingType = index % 17 == 0 ? eStructs.STRUCT_HUNTERS_HUT : eStructs.STRUCT_HOVEL,
                r_GlobalId = (uint)(index + 1), r_TilePositionXBegin = (ushort)(index % 700),
                r_TilePositionYBegin = (ushort)(index / 700) };
        for (int index = 0; index < units.Length; index += 100)
            units[index] = new GameUnit { r_AliveState = AliveState.IsAlive, r_UnitChimp = eChimps.CHIMP_TYPE_HUNTER,
                r_AIState = 4, r_LinkedProductionBuildingGlobalId = (uint)(100000 + index) };
        fixed (GameUnit* first = units)
        {
            for (int warm = 0; warm < 32; warm++)
            { Stopwatch.GetTimestamp(); benchmarkSink = IndexPass(world, buildings, first, units.Length); }
            // With no granary all distances tie. Fixture huts occur at indices 17*k and owners cycle1..8,
            // so each owner's first identity is known independently of the production selection algorithm.
            for (int owner = 1; owner <= 8; owner++)
                Check(world.Baseline[owner] == (uint)((owner-1)*17+1), "one-pass index selects first fixture identity");
            var times = new long[128];
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int sample = 0; sample < times.Length; sample++)
            {
                long start = Stopwatch.GetTimestamp();
                benchmarkSink = IndexPass(world, buildings, first, units.Length);
                times[sample] = Stopwatch.GetTimestamp() - start;
            }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(bytes == 0, "warmed production world index reuses collection capacity");
            Check(benchmarkSink > 0, "one-pass workload executed");
            Array.Sort(times);
            double scale = 1000000.0 / Stopwatch.Frequency;
            Console.WriteLine("BENCHMARK CLR production-world-index buildings=2000 units=10000 players=8 samples=128 p50us=" +
                (times[64] * scale).ToString("F3", CultureInfo.InvariantCulture) + " p95us=" +
                (times[121] * scale).ToString("F3", CultureInfo.InvariantCulture) + " allocatedBytes=" + bytes +
                "; helper-only workload, not game or FPS evidence");
        }
    }

    private static long IndexPass(AIHunterWorldIndex world, GameBuilding[] buildings, GameUnit* units, int length)
    {
        world.Clear();
        for (int owner = 1; owner <= 8; owner++) world.SetPlayer(owner, true, 0);
        for (int index = 0; index < buildings.Length; index++) world.ObserveBuilding(index + 1, in buildings[index]);
        world.SelectBaselines();
        for (int index = 0; index < length; index++) world.ObserveWorker(in units[index]);
        long result = world.Huts.Count + world.BusyHuts.Count;
        for (int owner = 1; owner <= 8; owner++) result += world.Baseline[owner];
        return result;
    }
}
