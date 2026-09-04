using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using StockpileAccessFixTest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

internal static class Program
{
    private const string DllPath =
        @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";

    private static int assertions;

    private static int Main()
    {
        try
        {
            CheckNativeContracts();
            CheckManagedLayouts();
            CheckWorkerContracts();
            CheckPolicyForAllWorkers();
            CheckCandidateResetsAndExclusions();
            CheckRepairVerificationAndCooldown();
            CheckSourceContracts();
            Console.WriteLine($"PASS: StockpileAccessFixTest tests ({assertions} assertions).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void CheckNativeContracts()
    {
        byte[] image = File.ReadAllBytes(DllPath);
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(sha.ComputeHash(image)).Replace("-", string.Empty);
            Check(actual == StockpileAccessFixNativeDefinition.ReferenceSha256, "canonical native SHA-256");
            Check(actual == Shared.DebugLogHelper.CurrentNativeSha256, "shared and mod native hashes agree");
        }

        byte[] expectedProlog = ParseBytes(StockpileAccessFixNativeDefinition.RevalidateBuildingAccessPattern);
        int prologOffset = RvaToFileOffset(image, StockpileAccessFixNativeDefinition.RevalidateBuildingAccessRva);
        for (int index = 0; index < expectedProlog.Length; index++)
            Check(image[prologOffset + index] == expectedProlog[index], "access helper prolog byte " + index);

        foreach (StockpileWorkerContract contract in StockpileWorkerContracts.All)
        {
            int tableRva = checked(
                StockpileAccessFixNativeDefinition.UnitHandlerTableRva + ((int)contract.UnitType * sizeof(long)));
            ulong actualVa = ReadUInt64(image, RvaToFileOffset(image, tableRva));
            ulong expectedVa = StockpileAccessFixNativeDefinition.PreferredImageBase + unchecked((uint)contract.HandlerRva);
            Check(actualVa == expectedVa, contract.UnitType + " handler table entry");
        }

        Check(StockpileAccessFixNativeDefinition.MoveHereRva == 0x196280, "audited MoveHere RVA");
        Check(StockpileAccessFixNativeDefinition.RevalidateBuildingAccessRva == 0xC90E0, "audited access helper RVA");
        Check(
            StockpileAccessFixNativeDefinition.NativeAccessXWorkingOffset -
            StockpileAccessFixNativeDefinition.BuildingManagerArrayBias ==
            StockpileAccessFixNativeDefinition.BuildingEntryXOffset,
            "native working X offset maps to publicized building entry X");
        Check(
            StockpileAccessFixNativeDefinition.NativeAccessYWorkingOffset -
            StockpileAccessFixNativeDefinition.BuildingManagerArrayBias ==
            StockpileAccessFixNativeDefinition.BuildingEntryYOffset,
            "native working Y offset maps to publicized building entry Y");
        Check(
            (StockpileAccessFixNativeDefinition.AccessCandidateRejectedTileMask &
                unchecked((uint)TilePropertyFlag.IsBuilding)) != 0,
            "native access selection rejects building-occupied tiles");
        Check(
            (unchecked((uint)TilePropertyFlag.Goodsyard) &
                StockpileAccessFixNativeDefinition.AccessCandidateRejectedTileMask) != 0,
            "occupied stockpile tile cannot be a normal access candidate");
        Check(
            (unchecked((uint)TilePropertyFlag.GoodsyardConnection) &
                StockpileAccessFixNativeDefinition.AccessCandidateRejectedTileMask) == 0,
            "passable stockpile connection can remain a native access candidate");
    }

    private static void CheckManagedLayouts()
    {
        Check(Marshal.SizeOf(typeof(GameUnit)) == StockpileAccessFixNativeDefinition.GameUnitSize, "GameUnit size");
        Check(Marshal.SizeOf(typeof(GameBuilding)) == StockpileAccessFixNativeDefinition.GameBuildingSize, "GameBuilding size");
        CheckOffset<GameUnit>(nameof(GameUnit.r_AliveState), 0x88);
        CheckOffset<GameUnit>(nameof(GameUnit.r_UnitChimp), 0x8A);
        CheckOffset<GameUnit>(nameof(GameUnit.r_GlobalId), 0x94);
        CheckOffset<GameUnit>(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
        CheckOffset<GameUnit>(nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
        CheckOffset<GameUnit>(nameof(GameUnit.r_TargetTilePositionX2), 0xE8);
        CheckOffset<GameUnit>(nameof(GameUnit.r_TargetTilePositionY2), 0xEA);
        CheckOffset<GameUnit>(nameof(GameUnit.r_PathPlanStateBitFlags), 0xF2);
        CheckOffset<GameUnit>(nameof(GameUnit.r_PathPlanRelated3), 0x290);
        CheckOffset<GameUnit>(nameof(GameUnit.r_AIState), 0x2BC);
        CheckOffset<GameUnit>(nameof(GameUnit.r_LinkedProductionBuildingId), 0x334);
        CheckOffset<GameBuilding>(nameof(GameBuilding.r_AliveState), 0xD0);
        CheckOffset<GameBuilding>(nameof(GameBuilding.r_BuildingType), 0xD2);
        CheckOffset<GameBuilding>(nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
        CheckOffset<GameBuilding>(nameof(GameBuilding.r_GlobalId), 0xD8);
        Check(StockpileAccessFixNativeDefinition.UnitStoredBuildingGlobalIdOffset == 0x9C, "stored building global-id raw offset");
        Check(StockpileAccessFixNativeDefinition.UnitStorageBuildingIdOffset == 0x332, "stockpile building-id raw offset");
        Check(StockpileAccessFixNativeDefinition.BuildingEntryXOffset == 0xFE, "stockpile entry X raw offset");
        Check(StockpileAccessFixNativeDefinition.BuildingEntryYOffset == 0x100, "stockpile entry Y raw offset");
    }

    private static void CheckWorkerContracts()
    {
        Dictionary<eChimps, ushort> expected = new Dictionary<eChimps, ushort>
        {
            [eChimps.CHIMP_TYPE_FLETCHER] = 1,
            [eChimps.CHIMP_TYPE_MILLER] = 3,
            [eChimps.CHIMP_TYPE_BAKER] = 7,
            [eChimps.CHIMP_TYPE_BREWER] = 2,
            [eChimps.CHIMP_TYPE_POLETURNER] = 2,
            [eChimps.CHIMP_TYPE_BLACKSMITH] = 2,
            [eChimps.CHIMP_TYPE_ARMOURER] = 2,
            [eChimps.CHIMP_TYPE_INNKEEPER] = 2
        };

        Check(StockpileWorkerContracts.All.Length == expected.Count, "exactly eight worker contracts");
        foreach (KeyValuePair<eChimps, ushort> pair in expected)
        {
            Check(StockpileWorkerContracts.TryGet(pair.Key, out StockpileWorkerContract contract), pair.Key + " is supported");
            Check(contract.FetchState == pair.Value, pair.Key + " fetch state");
            Check(contract.HandlerRva != 0, pair.Key + " handler RVA");
        }
    }

    private static void CheckPolicyForAllWorkers()
    {
        int unitId = 10;
        foreach (StockpileWorkerContract contract in StockpileWorkerContracts.All)
        {
            StockpileAccessEpisodePolicy policy = new StockpileAccessEpisodePolicy();
            StockpileObservation stuck = Observation(contract, unitId++, 1000u + unchecked((uint)unitId));
            Check(policy.Observe(stuck, 1) == StockpileEpisodeAction.CandidateStarted, contract.UnitType + " candidate starts");
            for (int tick = 2; tick < StockpileAccessEpisodePolicy.RequiredConsecutiveTicks; tick++)
                Check(policy.Observe(stuck, tick) == StockpileEpisodeAction.None, contract.UnitType + " remains pending " + tick);
            Check(policy.Observe(stuck, StockpileAccessEpisodePolicy.RequiredConsecutiveTicks) == StockpileEpisodeAction.ConfirmAndRepair,
                contract.UnitType + " confirms on tick 50");
        }
    }

    private static void CheckCandidateResetsAndExclusions()
    {
        StockpileWorkerContract contract = StockpileWorkerContracts.All[0];
        StockpileObservation stuck = Observation(contract);
        AssertFreshFiftyAfterInterruption(stuck, Change(stuck, currentX: 11), "movement");
        AssertFreshFiftyAfterInterruption(stuck, Change(stuck, targetX: 21, entryX: 21), "target change");
        AssertFreshFiftyAfterInterruption(stuck, Change(stuck, supported: false, state: 99), "state change");
        AssertFreshFiftyAfterInterruption(stuck, Change(stuck, globalId: 999), "unit slot reuse");
        AssertFreshFiftyAfterInterruption(
            stuck,
            Change(stuck, alternatePathConnectionId: 8),
            "alternate path-connection change");

        StockpileAccessEpisodePolicy gapPolicy = new StockpileAccessEpisodePolicy();
        for (int tick = 1; tick <= 20; tick++)
            gapPolicy.Observe(stuck, tick);
        Check(gapPolicy.Observe(stuck, 22) == StockpileEpisodeAction.CandidateStarted, "tick gap restarts candidate");
        for (int tick = 23; tick < 71; tick++)
            Check(gapPolicy.Observe(stuck, tick) == StockpileEpisodeAction.None, "gap restart pending " + tick);
        Check(gapPolicy.Observe(stuck, 71) == StockpileEpisodeAction.ConfirmAndRepair, "gap requires fresh fifty ticks");

        Check(!Change(stuck, alive: false).HasIdleBugSignature, "dead worker excluded");
        Check(!Change(stuck, supported: false).HasIdleBugSignature, "wrong fetch state excluded");
        Check(!Change(stuck, ownedStockpile: false).HasIdleBugSignature, "wrong or foreign building excluded");
        Check(!Change(stuck, storageGenerationMatches: false).HasIdleBugSignature, "stale building global-id excluded");
        Check(!Change(stuck, pathFlags: 2).HasIdleBugSignature, "active path excluded");
        Check(Change(stuck, alternatePathConnectionId: 0).HasIdleBugSignature,
            "zero alternate path-connection ID remains a valid idle signature");
        Check(!StockpileAccessEpisodePolicy.CanStartCandidate(stuck, hasMatchingRecentlyActiveRoute: false),
            "idle signature without an immediately preceding active fetch route is excluded");
        Check(StockpileAccessEpisodePolicy.CanStartCandidate(stuck, hasMatchingRecentlyActiveRoute: true),
            "idle signature immediately after a matching active fetch route is eligible");
        Check(!Change(stuck, currentX: stuck.TargetX, currentY: stuck.TargetY).HasIdleBugSignature, "already reached target excluded");
        Check(!Change(stuck, targetX: 44).HasIdleBugSignature, "secondary target not equal to stockpile access excluded");
    }

    private static void CheckRepairVerificationAndCooldown()
    {
        StockpileWorkerContract contract = StockpileWorkerContracts.All[5];
        StockpileObservation stuck = Observation(contract);
        StockpileAccessEpisodePolicy policy = Confirm(stuck);
        StockpileObservation accepted = Change(stuck, targetX: 30, targetY: 31, entryX: 30, entryY: 31, pathFlags: 2);
        policy.RecordRepairOutcome(accepted, 50, routeAccepted: true);
        Check(policy.Observe(accepted, 51) == StockpileEpisodeAction.None, "accepted route alone is not mistaken for movement");
        Check(policy.Observe(Change(accepted, currentX: 12), 52) == StockpileEpisodeAction.Progress, "movement reports repair progress");
        Check(policy.Observe(Change(accepted, currentX: 13), 53) == StockpileEpisodeAction.None, "movement does not duplicate progress marker");
        StockpileObservation leftFetch = Change(accepted, supported: false, state: 3, storageBuildingId: 0, ownedStockpile: false, storageGenerationMatches: false);
        Check(policy.Observe(leftFetch, 54) == StockpileEpisodeAction.Verified, "fetch-state exit verifies despite cleared stockpile link");
        Check(policy.CanDiscard, "verified episode can be discarded");
        Check(policy.Observe(stuck, 55) == StockpileEpisodeAction.CandidateStarted, "a later incident starts but is not repaired immediately");

        policy = Confirm(stuck);
        policy.RecordRepairOutcome(stuck, 50, routeAccepted: false);
        for (int tick = 51; tick < 250; tick++)
            Check(policy.Observe(stuck, tick) == StockpileEpisodeAction.None, "failed retry cooldown " + tick);
        Check(policy.Observe(stuck, 250) == StockpileEpisodeAction.CandidateStarted, "retry starts after 200 ticks");
        for (int tick = 251; tick < 299; tick++)
            Check(policy.Observe(stuck, tick) == StockpileEpisodeAction.None, "retry confirmation pending " + tick);
        Check(policy.Observe(stuck, 299) == StockpileEpisodeAction.ConfirmAndRepair, "retry confirms after a fresh fifty ticks");

        policy = Confirm(stuck);
        policy.RecordRepairOutcome(Change(stuck, pathFlags: 2), 50, routeAccepted: true);
        Check(policy.Observe(Change(stuck, pathFlags: 2, globalId: 404), 51) == StockpileEpisodeAction.Unverified,
            "slot reuse during verification fails closed");
    }

    private static void CheckSourceContracts()
    {
        string workspace = FindWorkspace();
        string mod = Path.Combine(workspace, "StockpileAccessFixTest");
        string plugin = File.ReadAllText(Path.Combine(mod, "src", "StockpileAccessFixTestPlugin.cs"));
        string runtime = File.ReadAllText(Path.Combine(mod, "src", "StockpileAccessFixTestRuntime.cs"));
        string project = File.ReadAllText(Path.Combine(mod, "StockpileAccessFixTest.csproj"));
        string info = File.ReadAllText(Path.Combine(mod, "info.json"));

        Check(plugin.Contains("requireCurrentVersion: true"), "native hash mismatch fails closed");
        Check(runtime.Contains("latestSimulationTick = tick;") &&
            runtime.IndexOf("ProcessAutomaticTestTrigger();", StringComparison.Ordinal) <
            runtime.IndexOf("ScanWorkers(tick);", StringComparison.Ordinal),
            "test trigger runs automatically before the next simulation scan");
        Check(!plugin.Contains("private void Update()"),
            "automatic trigger does not depend on an unobserved Unity Update callback");
        Check(!plugin.Contains("Input.GetKey") && !plugin.Contains("KeyCode."), "test trigger requires no hotkey");
        Check(runtime.Contains("RevalidateBuildingAccessDelegate"), "vanilla access helper is used");
        Check(runtime.Contains("GameUnitManagerAPI.Instance.MoveToTile"), "movement uses Script Extender API");
        Check(runtime.Contains("GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition") &&
            runtime.Contains("TryFindFetchingFletcherBlocker"),
            "test trigger uses a second fetching Fletcher as a dynamic blocker");
        Check(runtime.Contains("SetCurrentLocalTilePositionAdjacent+VanillaMoveToTile") &&
            runtime.Contains("TryFindFreeBlockerApproach"),
            "Fletcher is teleported only to a free neighbor and walks natively onto the access");
        Check(!runtime.Contains("GamePlayerManagerAPI.Instance.GetLocalPlayerId"),
            "automatic trigger can use naturally suitable non-local workers");
        Check(!runtime.Contains("r_NextTilePositionX2") && !runtime.Contains("r_NextTilePositionY2"),
            "automatic trigger never substitutes a route tile for the cached access");
        Check(runtime.Contains("cached access is not an internal GoodsyardConnection tile"),
            "automatic trigger fails closed unless the cached access is an internal stockpile connection");
        Check(runtime.Contains("trackedRoutes.TryGetValue(unitId, out TrackedRoute activeRoute)") &&
            runtime.Contains("tick - activeRoute.LastActiveTick <= 2") &&
            runtime.Contains("trackedRoutes[observation.UnitId] = new TrackedRoute(route, tick)"),
            "idle diagnosis requires an immediately preceding matching active fetch route");
        Check(!runtime.Contains("observation.PathMarker") &&
            !runtime.Contains("candidate.PathMarker"),
            "automatic trigger does not require a nonzero alternate path-connection ID");
        Check(runtime.Contains("TileUnitIdGrid never reported") &&
            runtime.Contains("STOCKPILE_TEST_OCCUPANCY_CONFIRMED"),
            "forced retry waits for a real dynamic tile occupancy observation");
        Check(runtime.Contains("STOCKPILE_TEST_NATURAL_OCCUPANCY_USED") &&
            runtime.Contains("existingVanillaOccupancy"),
            "an already registered Vanilla occupant is preferred without moving that occupant");
        Check(runtime.Contains("VanillaMoveToOriginalTargetAccepted") &&
            runtime.Contains("ResumeRegisteredBlockerToOriginalTarget"),
            "a synthetic registered blocker is returned through Vanilla movement");
        Check(runtime.Contains("tileApi.GetTileUnitId(originTileId) != 0"),
            "only a moving Fletcher absent from the native origin occupancy grid may be teleported");
        Check(runtime.Contains("nextAutomaticTriggerTick = checked") &&
            runtime.Contains("tick >= nextAutomaticTriggerTick") &&
            runtime.Contains("AutomaticTriggerRetryTicks"),
            "one global trigger cooldown prevents alternating routes from bypassing throttling");
        Check(runtime.Contains("TryRestoreTestBlocker(\"diagnostic disabled after runtime failure\")") &&
            !runtime.Contains("RequestTestBlockerCleanup"),
            "runtime failure performs immediate best-effort blocker recovery before disabling ticks");
        Check(!runtime.Contains("SetTilePropertyFlag") && !runtime.Contains("SetTileBuildingId"),
            "test trigger does not mutate tile grids directly");
        Check(!runtime.Contains("HookTransaction") && !runtime.Contains("AddDetour"), "runtime installs no native inline hook");
        Check(!ContainsDirectAssignment(runtime, "r_AIState"), "runtime does not mutate AI state");
        Check(!ContainsDirectAssignment(runtime, "r_PathPlanRelated3"),
            "runtime does not mutate the alternate path-connection ID");
        Check(!ContainsDirectAssignment(runtime, "r_CurrentTilePositionX") &&
            !ContainsDirectAssignment(runtime, "r_CurrentTilePositionY"),
            "runtime does not write unit position fields directly");
        Check(!runtime.Contains("CreatePrefab") && !runtime.Contains("MAPPER_WOODWALL") &&
            !runtime.Contains("DeleteBuildingSafe"),
            "automatic test no longer creates or deletes a wall/building");
        Check(project.Contains(@"Shared\DebugLogHelper.cs") && project.Contains(@"Shared\NativePatternResolver.cs"), "required shared helpers are linked");
        Check(!project.Contains("UnityEngine.InputLegacyModule"), "automatic test has no input-module dependency");
        Check(info.Contains("\"Version\": \"0.1.0\"") && info.Contains("\"NetworkMode\": 1"), "test version and network mode");

        string[] markers =
        {
            "STOCKPILE_FETCH_ROUTE_TRACKED",
            "STOCKPILE_ACCESS_BUG_CANDIDATE",
            "STOCKPILE_ACCESS_BUG_CONFIRMED",
            "STOCKPILE_ACCESS_RESELECTED",
            "STOCKPILE_ACCESS_FIX_APPLIED",
            "STOCKPILE_ACCESS_FIX_PROGRESS",
            "STOCKPILE_ACCESS_FIX_VERIFIED",
            "STOCKPILE_ACCESS_FIX_FAILED",
            "STOCKPILE_ACCESS_MAP_SUMMARY"
        };
        foreach (string marker in markers)
            Check(runtime.Contains(marker), marker + " logging contract");
        foreach (string marker in new[]
        {
            "STOCKPILE_TEST_BLOCKER_READY",
            "STOCKPILE_TEST_BLOCKER_SPAWNED",
            "STOCKPILE_TEST_NATURAL_OCCUPANCY_USED",
            "STOCKPILE_TEST_BLOCKER_FAILED",
            "STOCKPILE_TEST_BLOCKER_REMOVED",
            "STOCKPILE_TEST_OCCUPANCY_CONFIRMED",
            "STOCKPILE_TEST_FAULT_INJECTED",
            "STOCKPILE_TEST_AUTOMATION_RESULT"
        })
        {
            Check(runtime.Contains(marker), marker + " test-trigger logging contract");
        }

        Check(StockpileAccessFixTestRuntime.IsFreeStockpileConnectionTile(
                TilePropertyFlag.GoodsyardConnection, 0, 0),
            "free internal stockpile connection accepts a civilian test blocker");
        Check(!StockpileAccessFixTestRuntime.IsFreeStockpileConnectionTile(TilePropertyFlag.Free, 0, 0),
            "ordinary external land is rejected");
        Check(!StockpileAccessFixTestRuntime.IsFreeStockpileConnectionTile(
                TilePropertyFlag.GoodsyardConnection, 7, 0),
            "building-occupied stockpile connection is rejected");
        Check(!StockpileAccessFixTestRuntime.IsFreeStockpileConnectionTile(
                TilePropertyFlag.GoodsyardConnection, 0, 7),
            "unit-occupied stockpile connection is rejected before injection");
        Check(StockpileAccessFixTestRuntime.IsStockpileConnectionTile(
                TilePropertyFlag.GoodsyardConnection, 0),
            "occupied and free native stockpile connections share the same property flag");
        Check(!StockpileAccessFixTestRuntime.IsStockpileConnectionTile(
                TilePropertyFlag.Goodsyard, 0),
            "ordinary stockpile storage tile is not treated as its connection");
        Check(StockpileAccessFixTestRuntime.BlockerApproachSearchRadius == 8,
            "synthetic blocker approach uses the audited Ox-style radius-eight search");
        Check(StockpileAccessFixTestRuntime.AutomaticTriggerRetryTicks == 50,
            "automatic trigger failures use a global fifty-tick retry delay");
        Check(StockpileAccessFixTestRuntime.IsSafeBlockerApproachTile(
                TilePropertyFlag.GoodsyardConnection, 0, 0, vanillaWalkable: false),
            "internal stockpile connection is accepted as an adjacent approach despite the wall bit");
        Check(StockpileAccessFixTestRuntime.IsSafeBlockerApproachTile(
                TilePropertyFlag.Free, 0, 0, vanillaWalkable: true),
            "Vanilla-walkable ordinary neighbor is accepted as an approach");
        Check(!StockpileAccessFixTestRuntime.IsSafeBlockerApproachTile(
                TilePropertyFlag.Free, 4, 0, vanillaWalkable: true),
            "building-occupied approach is rejected");
        Check(!StockpileAccessFixTestRuntime.IsSafeBlockerApproachTile(
                TilePropertyFlag.Free, 0, 4, vanillaWalkable: true),
            "unit-occupied approach is rejected");

        StockpileWorkerContract fletcher = Array.Find(
            StockpileWorkerContracts.All,
            contract => contract.UnitType == eChimps.CHIMP_TYPE_FLETCHER);
        StockpileObservation victim = Change(Observation(fletcher, unitId: 20, globalId: 200), pathFlags: 2);
        StockpileObservation blocker = Change(
            Observation(fletcher, unitId: 21, globalId: 201),
            pathFlags: 2,
            targetX: 30,
            targetY: 31,
            entryX: 30,
            entryY: 31,
            storageBuildingId: 6);
        Check(StockpileAccessFixTestRuntime.IsEligibleFletcherBlocker(victim, blocker),
            "same-owner selection may use a moving Fletcher with an independent stockpile target");
        Check(StockpileAccessFixTestRuntime.IsEligibleFletcherBlocker(
                victim, Change(blocker, alternatePathConnectionId: 0)),
            "blocker eligibility does not depend on a nonzero alternate path-connection ID");
        Check(!StockpileAccessFixTestRuntime.IsEligibleFletcherBlocker(victim, Change(blocker, pathFlags: 0)),
            "idle Fletcher without an active route is rejected as blocker");
        Check(!StockpileAccessFixTestRuntime.IsEligibleFletcherBlocker(
                victim,
                Change(blocker, targetX: victim.TargetX, targetY: victim.TargetY,
                    entryX: victim.EntryX, entryY: victim.EntryY)),
            "Fletcher sharing the victim target is rejected because it cannot natively move away afterward");
        StockpileWorkerContract poleturner = Array.Find(
            StockpileWorkerContracts.All,
            contract => contract.UnitType == eChimps.CHIMP_TYPE_POLETURNER);
        StockpileObservation wrongType = Change(Observation(poleturner, unitId: 21, globalId: 201), pathFlags: 2);
        Check(!StockpileAccessFixTestRuntime.IsEligibleFletcherBlocker(victim, wrongType),
            "non-Fletcher worker is rejected as blocker");
    }

    private static void AssertFreshFiftyAfterInterruption(
        in StockpileObservation stuck,
        in StockpileObservation interrupted,
        string name)
    {
        StockpileAccessEpisodePolicy policy = new StockpileAccessEpisodePolicy();
        for (int tick = 1; tick <= 20; tick++)
            policy.Observe(stuck, tick);
        policy.Observe(interrupted, 21);
        Check(policy.Observe(stuck, 22) == StockpileEpisodeAction.CandidateStarted, name + " starts a fresh candidate");
        for (int tick = 23; tick < 71; tick++)
            Check(policy.Observe(stuck, tick) == StockpileEpisodeAction.None, name + " reset pending " + tick);
        Check(policy.Observe(stuck, 71) == StockpileEpisodeAction.ConfirmAndRepair, name + " requires fresh fifty ticks");
    }

    private static StockpileAccessEpisodePolicy Confirm(in StockpileObservation stuck)
    {
        StockpileAccessEpisodePolicy policy = new StockpileAccessEpisodePolicy();
        for (int tick = 1; tick <= StockpileAccessEpisodePolicy.RequiredConsecutiveTicks; tick++)
            policy.Observe(stuck, tick);
        return policy;
    }

    private static StockpileObservation Observation(StockpileWorkerContract contract, int unitId = 10, uint globalId = 100) =>
        new StockpileObservation(
            unitId, globalId, contract.UnitType, contract.FetchState,
            alive: true, supportedFetchState: true, ownedStockpile: true, storageGenerationMatches: true,
            pathFlags: 0, alternatePathConnectionId: 7,
            currentX: 10, currentY: 10, targetX: 20, targetY: 20, entryX: 20, entryY: 20,
            storageBuildingId: 5, productionBuildingId: 8);

    private static StockpileObservation Change(
        in StockpileObservation source,
        int? unitId = null,
        uint? globalId = null,
        ushort? state = null,
        bool? alive = null,
        bool? supported = null,
        bool? ownedStockpile = null,
        bool? storageGenerationMatches = null,
        ushort? pathFlags = null,
        ushort? alternatePathConnectionId = null,
        ushort? currentX = null,
        ushort? currentY = null,
        ushort? targetX = null,
        ushort? targetY = null,
        ushort? entryX = null,
        ushort? entryY = null,
        ushort? storageBuildingId = null) =>
        new StockpileObservation(
            unitId ?? source.UnitId,
            globalId ?? source.UnitGlobalId,
            source.UnitType,
            state ?? source.State,
            alive ?? source.Alive,
            supported ?? source.SupportedFetchState,
            ownedStockpile ?? source.OwnedStockpile,
            storageGenerationMatches ?? source.StorageGenerationMatches,
            pathFlags ?? source.PathFlags,
            alternatePathConnectionId ?? source.AlternatePathConnectionId,
            currentX ?? source.CurrentX,
            currentY ?? source.CurrentY,
            targetX ?? source.TargetX,
            targetY ?? source.TargetY,
            entryX ?? source.EntryX,
            entryY ?? source.EntryY,
            storageBuildingId ?? source.StorageBuildingId,
            source.ProductionBuildingId);

    private static void CheckOffset<T>(string field, int expected) =>
        Check(Marshal.OffsetOf(typeof(T), field).ToInt32() == expected, typeof(T).Name + "." + field + " offset");

    private static bool ContainsDirectAssignment(string source, string field) =>
        Regex.IsMatch(source, @"\b" + Regex.Escape(field) + @"\s*=(?!=)");

    private static byte[] ParseBytes(string pattern)
    {
        string[] parts = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        byte[] result = new byte[parts.Length];
        for (int index = 0; index < parts.Length; index++)
            result[index] = Convert.ToByte(parts[index], 16);
        return result;
    }

    private static int RvaToFileOffset(byte[] image, int rva)
    {
        int pe = ReadInt32(image, 0x3C);
        int sectionCount = ReadUInt16(image, pe + 6);
        int optionalHeaderSize = ReadUInt16(image, pe + 20);
        int sectionTable = pe + 24 + optionalHeaderSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int header = sectionTable + index * 40;
            int virtualSize = ReadInt32(image, header + 8);
            int virtualAddress = ReadInt32(image, header + 12);
            int rawSize = ReadInt32(image, header + 16);
            int rawAddress = ReadInt32(image, header + 20);
            int extent = Math.Max(virtualSize, rawSize);
            if (rva >= virtualAddress && rva < virtualAddress + extent)
                return checked(rawAddress + rva - virtualAddress);
        }
        throw new InvalidOperationException($"RVA 0x{rva:X} is outside the PE sections.");
    }

    private static int ReadUInt16(byte[] image, int offset) => image[offset] | image[offset + 1] << 8;

    private static int ReadInt32(byte[] image, int offset) =>
        image[offset] | image[offset + 1] << 8 | image[offset + 2] << 16 | image[offset + 3] << 24;

    private static ulong ReadUInt64(byte[] image, int offset) =>
        BitConverter.ToUInt64(image, offset);

    private static string FindWorkspace()
    {
        DirectoryInfo current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "StockpileAccessFixTest")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Workspace root not found.");
    }

    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Assertion failed: " + name);
    }
}
