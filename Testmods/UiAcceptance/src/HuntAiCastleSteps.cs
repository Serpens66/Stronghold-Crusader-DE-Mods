using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.Buildings;
using APIShared.Sight;
using APIShared.Simulation;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static unsafe class HuntAiCastleSteps
    {
        private const int TargetIdOffset = 0x39A, TargetGlobalIdOffset = 0x39C;
        private static readonly Dictionary<uint, (int State, int Target, uint TargetGlobalId, int Carry)> Previous =
            new Dictionary<uint, (int, int, uint, int)>();
        private static readonly HashSet<int> ObservedOwners = new HashSet<int>();
        private static IDisposable projectileSubscription;
        private static IDisposable placementSubscription;
        private static bool registered, active, armed;
        private static int currentTick, shots, placementAttempts, placementBlocked;

        internal static void Register()
        {
            Runner.Register("HuntAiCastles.Arm", s => GameSteps.Do(Arm));
            Runner.Register("HuntAiCastles.Begin", s => GameSteps.Do(Begin));
            Runner.Register("HuntAiCastles.Snapshot", s => GameSteps.Do(() => Snapshot(s.Arg(0), s.Int(1, 0) != 0, s.Int(2, 0) != 0)));
            Runner.Register("HuntAiCastles.SupplyHutCosts", s => GameSteps.Do(SupplyHutCosts));
            Runner.Register("HuntAiCastles.End", s => GameSteps.Do(End));
        }

        private static void Arm()
        {
            placementAttempts = placementBlocked = 0;
            if (placementSubscription == null)
                placementSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(ObservePlacement);
            armed = true;
        }

        private static void ObservePlacement(BuildingPlacementValidationEventArgs args)
        {
            if (!armed || args.Phase != EventHookPhase.Post || args.Mappers != eMappers.MAPPER_HUNTER ||
                args.PlayerId < 1 || args.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId)) return;
            placementAttempts++;
            if (GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked) placementBlocked++;
        }

        private static T EconomyField<T>(object economy, string name) =>
            (T)GameSteps.Read(economy.GetType(), economy, name, BindingFlags.Instance);

        // A native world snapshot is the assertion; private state is read only to diagnose
        // whether this run reached the intended production feature and prey configuration.
        private static void Snapshot(string label, bool afterCheck, bool requireHut)
        {
            lock (HuntSteps.Engine())
            {
                object economy = GameSteps.ResolveStatic("ImprovedHunters.ImprovedHuntersPlugin::persistentRuntime.aiHunterEconomy");
                Runner.Require(EconomyField<bool>(economy, "initialized") && EconomyField<bool>(economy, "active") &&
                    EconomyField<bool>(economy, "started") && !EconomyField<bool>(economy, "faulted") &&
                    !EconomyField<bool>(economy, "initializationFailed"), "ImprovedHunters AI economy is not healthy and active");
                Runner.Require(EconomyField<bool>(economy, "controlAiv") && EconomyField<bool>(economy, "automatic") &&
                    EconomyField<bool>(economy, "retire"), "expected all three AI options enabled");
                int prey = EconomyField<int>(economy, "preyMask"), other = EconomyField<int>(economy, "otherPreyMask");
                int food = EconomyField<int>(economy, "baselineFoodMask");
                if (afterCheck)
                    Runner.Require(EconomyField<bool>(economy, "preyKnown") && other == 0,
                        "expected a completed chicken-only prey check");
                Runner.Require(ConstructionCosts.TryGetCurrent(ConstructionCostTarget.FromStructure(eStructs.STRUCT_HUNTERS_HUT),
                    out var cost, out _), "native hunter hut costs unavailable");
                var counts = new int[GamePlayerManagerAPI.MAX_PLAYERS + 1];
                var identities = new List<string>();
                var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref readonly GameBuilding building = ref buildings[spanIndex];
                    int player = building.r_PlayerIdOwner;
                    if (building.r_BuildingType != eStructs.STRUCT_HUNTERS_HUT ||
                        (building.r_AliveState != AliveState.IsAlive && building.r_AliveState != AliveState.NeedsInit) ||
                        player < 1 || player >= counts.Length || !GamePlayerManagerAPI.Instance.IsAIPlayer(player)) continue;
                    counts[player]++;
                    identities.Add(player + ":" + (spanIndex + 1) + "/" + building.r_GlobalId);
                }
                int total = 0;
                for (int player = 1; player < counts.Length; player++)
                {
                    if (!GamePlayerManagerAPI.Instance.IsAIPlayer(player)) continue;
                    Runner.Require(counts[player] <= 1, "chicken-only AI player " + player + " has " + counts[player] + " Hunter's huts");
                    total += counts[player];
                    AcceptLog.Info("HUNT_AI_CASTLES_SNAPSHOT label=" + label + " player=" + player + " huts=" + counts[player] +
                        " wood=" + GamePlayerManagerAPI.Instance.GetGoodAmount(player, eGoods.STORED_WOOD_PLANKS) +
                        " gold=" + GamePlayerManagerAPI.Instance.GetGoodAmount(player, eGoods.STORED_GOLD));
                }
                AcceptLog.Info("HUNT_AI_CASTLES_ECONOMY label=" + label + " identities=" + string.Join(",", identities) +
                    " preyMask=" + prey + " otherPreyMask=" + other + " baselineFoodMask=" + food +
                    " cycleActive=" + EconomyField<bool>(economy, "cycleActive") + " placementAttempts=" + placementAttempts +
                    " blocked=" + placementBlocked + " hutCost=" + cost.Wood + "/" + cost.Stone + "/" + cost.Iron + "/" + cost.Pitch + "/" + cost.Gold);
                if (requireHut) Runner.Require(total > 0, "ImprovedHunters did not provide a real AI Hunter's hut");
            }
        }

        private static void Begin()
        {
            lock (HuntSteps.Engine())
            {
                ProbeSteps.RequireNativeReferenceHash();
                var players = GamePlayerManagerAPI.Instance;
                Runner.Require(players._choreManagerOptionsInternal.AdvOpt_PreBuild == 1,
                    "native prebuilt castles are disabled");
                int aiCount = 0, pigs = 0;
                for (int player = 1; player <= 8; player++)
                {
                    Enums.AILords lord = players.GetAILord(player);
                    AcceptLog.Info("HUNT_AI_CASTLES_LORD player=" + player + " lord=" + lord);
                    if (lord == Enums.AILords.SK_NULL)
                        continue;
                    aiCount++;
                    if (lord == Enums.AILords.SK_PIG)
                        pigs++;
                }
                Runner.Require(aiCount == 7 && pigs > 0, "expected seven AI lords including The Pig");
                AcceptLog.Info("HUNT_AI_CASTLES_BEGIN ai=" + aiCount + " pigs=" + pigs + " prebuilt=True");
                Previous.Clear();
                ObservedOwners.Clear();
                shots = 0;
                if (!registered)
                {
                    Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "hunt-ai-castles", Observe,
                        out string reason, 900), reason);
                    registered = true;
                }
                projectileSubscription?.Dispose();
                projectileSubscription = ProjectileR3EventHooks.OnProjectileSpawn.Observable.Subscribe(ObserveProjectile);
                active = true;
            }
        }

        private static void SupplyHutCosts()
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(ConstructionCosts.TryGetCurrent(
                    ConstructionCostTarget.FromStructure(eStructs.STRUCT_HUNTERS_HUT), out var cost, out _),
                    "native hunter hut costs unavailable");
                var players = GamePlayerManagerAPI.Instance;
                var goods = new[] {
                    (eGoods.STORED_WOOD_PLANKS, cost.Wood), (eGoods.STORED_STONE_BLOCKS, cost.Stone),
                    (eGoods.STORED_IRON_INGOTS, cost.Iron), (eGoods.STORED_PITCH_RAW, cost.Pitch),
                    (eGoods.STORED_GOLD, cost.Gold)
                };
                for (int player = 1; player <= 8; player++)
                {
                    if (!players.IsAIPlayer(player))
                        continue;
                    foreach ((eGoods good, int amount) in goods)
                    {
                        int missing = amount - players.GetGoodAmount(player, good);
                        if (missing <= 0)
                            continue;
                        bool added = players.TryAddGood(player, good, missing);
                        AcceptLog.Info("HUNT_AI_CASTLES_SUPPLY player=" + player + " good=" + good +
                            " amount=" + missing + " added=" + added);
                    }
                }
            }
        }

        private static void Observe(int tick)
        {
            if (!active)
                return;
            currentTick = tick;
            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* hunter = units.GetValuePointer(index);
                if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || !UnitAccess.IsReallyAlive(hunter) ||
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(hunter->r_ControllableForPlayerId))
                    continue;

                int owner = hunter->r_ControllableForPlayerId;
                ObservedOwners.Add(owner);
                int state = hunter->r_AIState;
                int target = *(ushort*)((byte*)hunter + TargetIdOffset);
                uint targetGlobalId = *(uint*)((byte*)hunter + TargetGlobalIdOffset);
                var sample = (state, target, targetGlobalId, (int)hunter->r_CarryGoodAmount);
                if (Previous.TryGetValue(hunter->r_GlobalId, out var old) && old.Equals(sample))
                    continue;
                Previous[hunter->r_GlobalId] = sample;

                string prey = "-";
                if (target > 0 && UnitAccess.TryGetById(target, out GameUnit* animal, out _) &&
                    animal != null && animal->r_GlobalId == targetGlobalId)
                {
                    LineOfSight.TryEvaluateUnits(in *hunter, in *animal, LineOfSightRoutine.Directional,
                        out int forward, out _);
                    LineOfSight.TryEvaluateUnits(in *animal, LineOfSight.TargetHeight, in *hunter, LineOfSight.ViewerEyeHeight,
                        LineOfSightRoutine.Directional, out int reverse, out _);
                    prey = animal->r_UnitChimp + "/" + animal->r_GlobalId + " at=" +
                        animal->r_CurrentTilePositionX + "," + animal->r_CurrentTilePositionY +
                        " hp=" + animal->r_CurrentHealth + " sight=" + forward + "/" + reverse;
                }
                AcceptLog.Info("HUNT_AI_CASTLES_TICK tick=" + tick + " player=" + owner + " hunter=" +
                    (index + 1) + "/" + hunter->r_GlobalId + " state=" + state + " tile=" +
                    hunter->r_CurrentTilePositionX + "," + hunter->r_CurrentTilePositionY + " path=" +
                    hunter->r_PathPlanStateBitFlags + "/" + hunter->r_CurrentPathPlanIndex + "/" +
                    hunter->r_PathPlanLength + " target=" + target + "/" + prey + " carry=" + hunter->r_CarryGoodAmount);
            }
        }

        private static void ObserveProjectile(ProjectileSpawnEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 ||
                args.ProjectileType != ProjectileType.ArcherArrow ||
                !UnitAccess.TryGetById(args.SourceUnitId, out GameUnit* shooter, out _) || shooter == null ||
                shooter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(shooter->r_ControllableForPlayerId))
                return;
            shots++;
            AcceptLog.Info("HUNT_AI_CASTLES_ARROW tick=" + currentTick + " player=" + shooter->r_ControllableForPlayerId +
                " hunter=" + args.SourceUnitId + " prey=" + args.AttackedUnitId + " projectile=" + args.ReturnValue);
        }

        private static void End()
        {
            lock (HuntSteps.Engine())
            {
                active = false;
                armed = false;
                projectileSubscription?.Dispose();
                projectileSubscription = null;
                AcceptLog.Info("HUNT_AI_CASTLES_END ownersWithHunter=" + ObservedOwners.Count + " hunters=" +
                    Previous.Count + " shots=" + shots + " placementAttempts=" + placementAttempts + " blocked=" + placementBlocked);
                Runner.Require(ObservedOwners.Count > 0, "ImprovedHunters did not provide a Hunter in this run");
            }
        }
    }
}
