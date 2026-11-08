using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using RedBird.Core.Memory;
using RedBird.X64.Assembly.Stateful;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // Candidate discovery, cost ranking, initial reachability and State-0 selection handoff.
        // APIShared target policy: runs inside Vanilla's query with the correct Hunter id (the Script Extender's
        // own event reports a wrong one). Only simulation queries may change this mod's state; the local
        // speech query of the selected Hunter gets the type, owner and range rules only.
        private unsafe APIShared.Wildlife.HunterTargetDecision OnHunterTargetCandidate(in APIShared.Wildlife.HunterTargetCandidate candidate)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            var decision = DecideHunterTarget(candidate.CandidateUnitId, candidate.HunterUnitId, candidate.IsSimulationQuery);
            Shared.DevDiag.Stop(ref measured, "hunters.query");
            return decision;
        }

        private unsafe APIShared.Wildlife.HunterTargetDecision DecideHunterTarget(int queryUnitId, int hunterUnitId, bool simulation)
        {
            const APIShared.Wildlife.HunterTargetDecision Default = APIShared.Wildlife.HunterTargetDecision.Default;
            if (!IsValidUnitId(queryUnitId))
                return Default;
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                // This mod's registrations follow the settings, not the mode; a mode it may not change keeps
                // its extra animals unhunted, as without the mod.
                return APIShared.UnitAccess.TryGetById(queryUnitId, out GameUnit* candidate, out _) && IsRegisteredPrey(candidate->r_UnitChimp)
                    ? APIShared.Wildlife.HunterTargetDecision.Refuse
                    : Default;
            }

            long timestamp = HunterSimulationTime.Now;

            // Rankings run only where the mode policy allows them.
            // Elsewhere every enabled candidate of the query is accepted.
            bool targetSelectionActive = CanRunHunterTargetSelection();

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!IsLiveHunter(unitApi, hunterUnitId))
                return Default;

            eChimps queryType = (APIShared.UnitAccess.TryGetById(unitApi, queryUnitId, out _, out _) ? unitApi.GetType(queryUnitId) : eChimps.CHIMP_TYPE_NULL);
            int queryGlobalId = (APIShared.UnitAccess.TryGetById(unitApi, queryUnitId, out _, out _) ? unitApi.GetGlobalId(queryUnitId) : -1);
            if (simulation)
                TrackHunterSearchQuery(hunterUnitId, timestamp);

            if (!settings.IsKnownAnimal(queryType))
                return Default;

            // Deer and goats are huntable by default, so a switched-off type must be refused explicitly.
            if (!IsRuntimeHuntingEnabled(queryType))
                return APIShared.Wildlife.HunterTargetDecision.Refuse;

            if (!IsOwnerAllowed((APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out _, out _) ? unitApi.GetOwner(hunterUnitId) : -1), queryUnitId, queryType))
                return Default;

            if (settings.HunterSearchRange != HunterSearchRangePolicy.Unlimited &&
                !IsWithinHunterSearchRange(unitApi, hunterUnitId, queryUnitId))
            {
                return APIShared.Wildlife.HunterTargetDecision.Refuse;
            }

            // The rankings below read and fill per-Hunter caches and cooldowns.
            if (!simulation)
                return Default;

            bool targetPclUnreachable = false;
            if (targetSelectionActive &&
                queryGlobalId > 0 &&
                hunterPclReachability != null &&
                hunterPclReachability.TryIsReachable(
                    hunterUnitId,
                    queryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType,
                    timestamp,
                    out bool targetPclReachable))
            {
                targetPclUnreachable = !targetPclReachable;
            }
            bool targetOnCooldown =
                targetSelectionActive &&
                queryGlobalId > 0 &&
                IsTargetOnCooldown(hunterUnitId, unchecked((uint)queryGlobalId), timestamp);
            bool isValidTarget = !targetPclUnreachable && !targetOnCooldown;
            bool usedFallback = false;
            TargetSelection targetSelection = default;
            BestTarget bestTarget = default;
            if (!targetSelectionActive)
            {
                isValidTarget = true;
            }
            else if (targetPclUnreachable)
            {
                // A zero from the same player-aware PCL query used by MoveHere
                // is sufficient to reject this candidate before any order.
            }
            else if (targetOnCooldown)
            {
                // A failed native MoveHere is authoritative for this Hunter/prey
                // pair until its bounded retry window expires.
            }
            else if (TryGetTargetSelectionForHunter(hunterUnitId, timestamp, out targetSelection))
            {
                bestTarget = targetSelection.BestTarget;
                isValidTarget = targetSelection.IsAllowed(queryUnitId);
                usedFallback = false;
            }
            else
            {
                hunterTargetNoBestEvents++;
                isValidTarget = true;
                usedFallback = true;
            }

            // isValidTarget is the decision for this candidate.
            if (isValidTarget &&
                targetSelectionActive &&
                targetSelection.HasTarget &&
                queryGlobalId > 0)
            {
                hunterTargetSearchFallback?.RecordCandidate(
                    hunterUnitId,
                    queryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType,
                    preferred: queryUnitId == bestTarget.UnitId,
                    timestamp: timestamp);
            }
            hunterTargetQueryEvents++;
            if (isValidTarget)
            {
                hunterTargetAcceptedEvents++;
            }
            else
            {
                hunterTargetRejectedEvents++;
            }

            if (usedFallback)
                hunterTargetFallbackEvents++;

            LogHunterTargetQueryDiagnostic(
                hunterUnitId,
                queryUnitId,
                queryType,
                isValidTarget,
                usedFallback,
                targetPclUnreachable,
                targetOnCooldown,
                targetSelection);
            return isValidTarget ? APIShared.Wildlife.HunterTargetDecision.Allow : APIShared.Wildlife.HunterTargetDecision.Refuse;
        }

        private static unsafe bool IsLiveHunter(GameUnitManagerAPI unitApi, int unitId)
        {
            return IsValidUnitId(unitId) &&
                APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _) &&
                unit != null &&
                APIShared.UnitAccess.IsReallyAlive(unit) &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;
        }

        private unsafe bool TryGetTargetSelectionForHunter(int hunterUnitId, long timestamp, out TargetSelection targetSelection)
        {
            targetSelection = default;

            if (bestTargetCache.TryGetValue(hunterUnitId, out CachedBestTarget cachedBestTarget) &&
                timestamp < cachedBestTarget.ExpiresAt)
            {
                targetSelection = cachedBestTarget.Selection;
                return targetSelection.HasTarget;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) ||
                hunter == null ||
                !APIShared.UnitAccess.IsReallyAlive(hunter) ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            int hunterOwner = (APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out _, out _) ? unitApi.GetOwner(hunterUnitId) : -1);
            if (!TryGetHunterOrigin(hunter, hunterOwner, out int originTileX, out int originTileY, out int granaryRoundTripHeuristicCost))
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            RefreshPreyCacheIfNeeded(force: false, timestamp);
            if (preyCache.Count == 0)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            int candidateRadius = settings.HunterSearchRange == HunterSearchRangePolicy.Unlimited
                ? HunterTargetCandidateRadius
                : Math.Min(settings.HunterSearchRange, HunterTargetCandidateRadius);
            List<PreyCandidate> candidates = new List<PreyCandidate>();
            for (int i = 0; i < preyCache.Count; i++)
            {
                PreySnapshot prey = preyCache[i];
                if (!TryGetLiveAvailablePreySnapshot(prey, out prey))
                    continue;

                if (!IsOwnerAllowed(hunterOwner, prey.UnitId, prey.Type))
                    continue;

                int heuristicDistance = GetChebyshevDistance(originTileX, originTileY, prey.TileX, prey.TileY);
                if (heuristicDistance > candidateRadius)
                    continue;

                if (hunterPclReachability != null &&
                    hunterPclReachability.TryIsReachable(
                        hunterUnitId,
                        prey.UnitId,
                        prey.GlobalId,
                        prey.Type,
                        timestamp,
                        out bool preyReachable) &&
                    !preyReachable)
                {
                    continue;
                }

                if (IsTargetOnCooldown(hunterUnitId, prey.GlobalId, timestamp))
                    continue;

                int heuristicCycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripHeuristicCost + (heuristicDistance * 10 * 2);
                candidates.Add(new PreyCandidate(prey, heuristicCycleCost));
            }

            if (candidates.Count == 0)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            candidates.Sort(ComparePreyCandidatesByHeuristic);

            bool hasBest = false;
            BestTarget currentBest = default;
            List<BestTarget> evaluatedTargets = new List<BestTarget>();
            int limit = Math.Min(candidates.Count, MaxHeuristicCandidatesPerHunter);
            for (int i = 0; i < limit; i++)
            {
                PreySnapshot prey = candidates[i].Prey;
                if (!TryGetLiveAvailablePreySnapshot(prey, out prey))
                    continue;

                // The Script Extender's managed A* has no expansion budget and
                // can monopolize the game thread for unreachable destinations.
                // PCL connectivity has already rejected disconnected regions.
                // This estimate ranks the remaining candidates while Vanilla's
                // detailed MoveHere path creation stays authoritative.
                int approachHeuristicCost = GetChebyshevDistance(
                    originTileX,
                    originTileY,
                    prey.TileX,
                    prey.TileY) * 10;
                int cycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripHeuristicCost + (approachHeuristicCost * 2);
                if (cycleCost <= 0)
                    cycleCost = 1;

                BestTarget candidate = new BestTarget(
                    prey.UnitId,
                    prey.GlobalId,
                    prey.Type,
                    prey.MeatAmount,
                    approachHeuristicCost,
                    granaryRoundTripHeuristicCost,
                    cycleCost);
                evaluatedTargets.Add(candidate);
                if (!hasBest || IsBetterTarget(candidate, currentBest))
                {
                    currentBest = candidate;
                    hasBest = true;
                }
            }

            if (!hasBest)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            HashSet<int> allowedUnitIds = new HashSet<int>();
            for (int i = 0; i < evaluatedTargets.Count; i++)
            {
                BestTarget candidate = evaluatedTargets[i];
                if (IsWithinTargetTolerance(candidate, currentBest))
                    allowedUnitIds.Add(candidate.UnitId);
            }

            if (allowedUnitIds.Count == 0)
                allowedUnitIds.Add(currentBest.UnitId);

            targetSelection = new TargetSelection(currentBest, allowedUnitIds);
            CacheTargetSelection(hunterUnitId, targetSelection, timestamp);
            return true;
        }

        private unsafe bool TryGetLiveAvailablePreySnapshot(PreySnapshot cachedPrey, out PreySnapshot livePrey)
        {
            livePrey = default;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!APIShared.UnitAccess.TryGetById(unitApi, cachedPrey.UnitId, out GameUnit* unit, out _) ||
                unit == null)
            {
                return false;
            }

            if (!TryGetPreyEligibility(cachedPrey.UnitId, unit, out PreyEligibility eligibility) ||
                eligibility.GlobalId != cachedPrey.GlobalId ||
                eligibility.Type != cachedPrey.Type ||
                !eligibility.Eligible)
            {
                return false;
            }

            livePrey = new PreySnapshot(
                cachedPrey.UnitId,
                cachedPrey.GlobalId,
                cachedPrey.Type,
                eligibility.TileX,
                eligibility.TileY,
                cachedPrey.MeatAmount);
            return true;
        }

        // The range counts from the Hunter's hut, like the ranking: he may stand at the granary when he searches.
        private unsafe bool IsWithinHunterSearchRange(GameUnitManagerAPI unitApi, int hunterUnitId, int preyUnitId)
        {
            if (!APIShared.UnitAccess.TryGetById(unitApi, hunterUnitId, out GameUnit* hunter, out _) || hunter == null ||
                !APIShared.UnitAccess.TryGetById(unitApi, preyUnitId, out GameUnit* prey, out _) || prey == null)
            {
                return true;
            }

            GetHunterHome(hunter, out int homeTileX, out int homeTileY);
            int distance = GetChebyshevDistance(homeTileX, homeTileY, prey->r_CurrentTilePositionX, prey->r_CurrentTilePositionY);
            return HunterSearchRangePolicy.IsWithin(distance, settings.HunterSearchRange);
        }

        private unsafe void GetHunterHome(GameUnit* hunter, out int tileX, out int tileY)
        {
            tileX = hunter->r_CurrentTilePositionX;
            tileY = hunter->r_CurrentTilePositionY;

            ushort linkedBuildingId = hunter->r_LinkedProductionBuildingId;
            if (linkedBuildingId != 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(linkedBuildingId, out GameBuilding* building) &&
                building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_BuildingType == eStructs.STRUCT_HUNTERS_HUT)
            {
                tileX = building->r_TilePositionXBegin;
                tileY = building->r_TilePositionYBegin;
            }
        }

        private unsafe bool TryGetHunterOrigin(
            GameUnit* hunter,
            int hunterOwner,
            out int originTileX,
            out int originTileY,
            out int granaryRoundTripHeuristicCost)
        {
            GetHunterHome(hunter, out originTileX, out originTileY);
            granaryRoundTripHeuristicCost = 0;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(originTileX, originTileY))
                return false;

            int originTileId = tileApi.GetTileId(originTileX, originTileY);
            if (!tileApi.IsValidTileId(originTileId))
                return false;

            if (!tileApi.IsTileWalkableAndUnoccupied(originTileId))
            {
                UnmanagedVector2<ushort> nearestWalkable = tileApi.GetNearestUnoccupiedTile(originTileX, originTileY, maxRange: 8);
                originTileX = nearestWalkable.X;
                originTileY = nearestWalkable.Y;
                if (!tileApi.IsTileInsideMapBounds(originTileX, originTileY))
                    return false;

                originTileId = tileApi.GetTileId(originTileX, originTileY);
            }

            if (!tileApi.IsValidTileId(originTileId))
                return false;

            if (TryGetNearestGranaryHeuristicCost(hunterOwner, originTileX, originTileY, out int granaryHeuristicCost))
                granaryRoundTripHeuristicCost = granaryHeuristicCost * 2;

            return true;
        }

        private unsafe bool TryGetNearestGranaryHeuristicCost(
            int hunterOwner,
            int originTileX,
            int originTileY,
            out int bestHeuristicCost)
        {
            bestHeuristicCost = 0;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            SimpleNativeArray<GameBuilding> buildings = buildingApi.GetBuildingsArray();
            if (buildings._array == null || buildings.Length == 0)
                return false;

            bool found = false;
            int bestBuildingId = int.MaxValue;
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_GRANARY ||
                    building->r_PlayerIdOwner != hunterOwner)
                {
                    continue;
                }

                if (!TryGetWalkableTileNear(
                        building->r_TilePositionXBegin,
                        building->r_TilePositionYBegin,
                        10,
                        out int targetTileX,
                        out int targetTileY,
                        out _))
                {
                    continue;
                }

                int heuristicCost = GetChebyshevDistance(
                    originTileX,
                    originTileY,
                    targetTileX,
                    targetTileY) * 10;
                int buildingId = index + 1;
                if (!found ||
                    heuristicCost < bestHeuristicCost ||
                    (heuristicCost == bestHeuristicCost && buildingId < bestBuildingId))
                {
                    bestHeuristicCost = heuristicCost;
                    bestBuildingId = buildingId;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetWalkableTileNear(int tileX, int tileY, int maxRange, out int walkableTileX, out int walkableTileY, out int walkableTileId)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            UnmanagedVector2<ushort> nearestWalkable = tileApi.GetNearestUnoccupiedTile(tileX, tileY, maxRange);
            walkableTileX = nearestWalkable.X;
            walkableTileY = nearestWalkable.Y;
            walkableTileId = 0;

            if (!tileApi.IsTileInsideMapBounds(walkableTileX, walkableTileY))
                return false;

            walkableTileId = tileApi.GetTileId(walkableTileX, walkableTileY);
            return tileApi.IsValidTileId(walkableTileId) &&
                tileApi.IsTileWalkableAndUnoccupied(walkableTileId);
        }

        private unsafe void RefreshPreyCacheIfNeeded(bool force, long timestamp)
        {
            if (!force && timestamp < nextPreyCacheRefreshTimestamp)
                return;

            nextPreyCacheRefreshTimestamp = timestamp + PreyCacheRefreshInterval;
            preyCache.Clear();
            bestTargetCache.Clear();

            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return;

            int knownCount = 0;
            int skippedKnownCount = 0;
            int eligibleDeer = 0;
            int eligibleGoat = 0;
            int eligibleRabbit = 0;
            int eligibleCamel = 0;
            int eligibleChicken = 0;
            int skippedCamels = 0;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                int unitId = index + 1;
                TryGetPreyEligibility(unitId, unit, out PreyEligibility eligibility);
                if (!eligibility.KnownAnimal)
                    continue;

                knownCount++;
                if (!eligibility.Eligible)
                {
                    skippedKnownCount++;
                    if (eligibility.Type == eChimps.CHIMP_TYPE_CAMEL)
                    {
                        skippedCamels++;
                        LogPreyCacheDiagnostic(unitId, eligibility, GetPreyIneligibilityReason(eligibility));
                    }

                    continue;
                }

                preyCache.Add(new PreySnapshot(
                    unitId,
                    eligibility.GlobalId,
                    eligibility.Type,
                    eligibility.TileX,
                    eligibility.TileY,
                    settings.GetMeatAmount(eligibility.Type)));

                IncrementAnimalCount(
                    eligibility.Type,
                    ref eligibleDeer,
                    ref eligibleGoat,
                    ref eligibleRabbit,
                    ref eligibleCamel,
                    ref eligibleChicken);

                if (eligibility.Type == eChimps.CHIMP_TYPE_CAMEL)
                    LogPreyCacheDiagnostic(unitId, eligibility, "eligible");
            }

            LogPreyCacheSummary(
                knownCount,
                skippedKnownCount,
                skippedCamels,
                eligibleDeer,
                eligibleGoat,
                eligibleRabbit,
                eligibleCamel,
                eligibleChicken);
        }

        // Development samples only: /devdiag builds compile these calls including their message text.
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogPreyCacheSummary(
            int knownCount,
            int skippedKnownCount,
            int skippedCamels,
            int eligibleDeer,
            int eligibleGoat,
            int eligibleRabbit,
            int eligibleCamel,
            int eligibleChicken)
        {
            if (preyCacheDiagnosticLogs >= MaxPreyCacheDiagnosticLogs)
                return;

            preyCacheDiagnosticLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters prey cache refreshed: eligible={preyCache.Count}, known={knownCount}, skippedKnown={skippedKnownCount}, " +
                $"eligibleByType=deer:{eligibleDeer}/goat:{eligibleGoat}/rabbit:{eligibleRabbit}/camel:{eligibleCamel}/chicken:{eligibleChicken}, " +
                $"skippedCamels={skippedCamels} ({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");

            if (preyCacheDiagnosticLogs == MaxPreyCacheDiagnosticLogs)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters prey cache diagnostic limit reached.");
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogPreyCacheDiagnostic(int unitId, PreyEligibility eligibility, string status)
        {
            if (preyCacheDiagnosticLogs >= MaxPreyCacheDiagnosticLogs)
                return;

            preyCacheDiagnosticLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters prey cache animal: unit={unitId}/{eligibility.Type}, globalId={eligibility.GlobalId}, " +
                $"tile={eligibility.TileX},{eligibility.TileY}, status={status}, aliveState={eligibility.AliveState}, " +
                $"flags92={eligibility.FlagsAt92}, aiState=0x{eligibility.AiState:X}, corpseFlag={eligibility.CorpseFlag}, " +
                $"reservation={eligibility.Reservation}, runtimeEnabled={eligibility.RuntimeHuntingEnabled}, ownerAllowed={eligibility.OwnerAllowed} " +
                $"({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");

            if (preyCacheDiagnosticLogs == MaxPreyCacheDiagnosticLogs)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters prey cache diagnostic limit reached.");
        }

        private static string GetPreyIneligibilityReason(PreyEligibility eligibility)
        {
            if (!eligibility.RuntimeHuntingEnabled)
                return "disabled";

            if (!eligibility.OwnerAllowed)
                return "owner-not-allowed";

            if (eligibility.AliveState != (short)AliveState.IsAlive)
                return $"aliveState={eligibility.AliveState}";

            if (!eligibility.FlagsAllowed)
                return $"flags92={eligibility.FlagsAt92}";

            if (eligibility.Reservation != 0)
                return $"reservation={eligibility.Reservation}";

            if (eligibility.CorpseFlag != 0 && eligibility.AiState != HunterCorpsePickupAiState)
                return $"corpseFlag={eligibility.CorpseFlag}/aiState=0x{eligibility.AiState:X}";

            return "unknown";
        }

        private static void IncrementAnimalCount(
            eChimps type,
            ref int deer,
            ref int goat,
            ref int rabbit,
            ref int camel,
            ref int chicken)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_DEER:
                    deer++;
                    break;
                case eChimps.CHIMP_TYPE_GOAT:
                    goat++;
                    break;
                case eChimps.CHIMP_TYPE_RABBIT:
                    rabbit++;
                    break;
                case eChimps.CHIMP_TYPE_CAMEL:
                    camel++;
                    break;
                case eChimps.CHIMP_TYPE_CHICKEN:
                    chicken++;
                    break;
            }
        }

        private static int ComparePreyCandidatesByHeuristic(PreyCandidate left, PreyCandidate right)
        {
            long leftScore = (long)left.Prey.MeatAmount * right.HeuristicCycleCost;
            long rightScore = (long)right.Prey.MeatAmount * left.HeuristicCycleCost;
            int scoreCompare = rightScore.CompareTo(leftScore);
            if (scoreCompare != 0)
                return scoreCompare;

            return left.HeuristicCycleCost.CompareTo(right.HeuristicCycleCost);
        }

        private static bool IsBetterTarget(BestTarget candidate, BestTarget currentBest)
        {
            long candidateScore = (long)candidate.MeatAmount * currentBest.CycleCost;
            long currentScore = (long)currentBest.MeatAmount * candidate.CycleCost;
            if (candidateScore != currentScore)
                return candidateScore > currentScore;

            if (candidate.Type == currentBest.Type &&
                candidate.ApproachHeuristicCost != currentBest.ApproachHeuristicCost)
            {
                return candidate.ApproachHeuristicCost < currentBest.ApproachHeuristicCost;
            }

            if (candidate.MeatAmount != currentBest.MeatAmount)
                return candidate.MeatAmount > currentBest.MeatAmount;

            return candidate.UnitId < currentBest.UnitId;
        }

        private static bool IsWithinTargetTolerance(BestTarget candidate, BestTarget currentBest)
        {
            if (candidate.UnitId == currentBest.UnitId)
                return true;

            int candidateMeat = Math.Max(1, candidate.MeatAmount);
            int bestMeat = Math.Max(1, currentBest.MeatAmount);
            long candidateNormalizedCost = (long)candidate.CycleCost * bestMeat;
            long toleratedBestNormalizedCost = (long)(currentBest.CycleCost + BestTargetToleranceCost) * candidateMeat;
            return candidateNormalizedCost <= toleratedBestNormalizedCost;
        }

        private static int GetChebyshevDistance(int ax, int ay, int bx, int by)
        {
            return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        }

        private static int GetPreyHandlingCost(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_RABBIT:
                case eChimps.CHIMP_TYPE_CHICKEN:
                    return 80;
                case eChimps.CHIMP_TYPE_CAMEL:
                    return 120;
                default:
                    return DefaultPreyHandlingCost;
            }
        }

        private void CacheTargetSelection(int hunterUnitId, TargetSelection selection, long timestamp)
        {
            bestTargetCache[hunterUnitId] = new CachedBestTarget(selection, timestamp + BestTargetCacheInterval);
        }

        private void TrackHunterSearchQuery(int hunterUnitId, long timestamp)
        {
            if (!IsValidUnitId(hunterUnitId))
                return;

            bool isNewSearch =
                !lastHunterQueryTimestamps.TryGetValue(hunterUnitId, out long lastTimestamp) ||
                timestamp - lastTimestamp > HunterSearchDetectionGap;

            lastHunterQueryTimestamps[hunterUnitId] = timestamp;
            if (!isNewSearch)
                return;

            hunterTargetSearchStarts++;
            LogTargetSearchStartSample(hunterUnitId);
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogTargetSearchStartSample(int hunterUnitId)
        {
            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters target search start: hunter={hunterUnitId}, " +
                    $"searchCount={hunterTargetSearchStarts}.");
            }
        }

    }
}
