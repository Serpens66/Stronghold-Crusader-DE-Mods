using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ImprovedHunters
{
    // Shared by the scan and the existing State-0 MoveHere callback. The scan leaves
    // a small reserve for hunters that need a position before the next scan.
    internal sealed class HunterFiringPositionBudget
    {
        public const int WindowTicks = HunterSimulationTime.MapTicksPerSecond / 10;
        private long window = -1;
        private int tiles, sight, connections, inlineTiles, inlineSight, urgentSight;

        public void Reset() => window = -1;

        private void Begin(long tick)
        {
            long current = tick / WindowTicks;
            if (window == current)
                return;

            window = current;
            tiles = sight = connections = inlineTiles = inlineSight = urgentSight = 0;
        }

        public bool TryTakeTile(long tick, bool inline)
        {
            Begin(tick);
            if (tiles >= 96 || (inline ? inlineTiles >= 24 : tiles - inlineTiles >= 72))
                return false;

            tiles++;
            if (inline)
                inlineTiles++;
            Shared.DevDiag.Count("hunters.firing.tiles");
            return true;
        }

        public bool CanTakeSight(long tick, bool inline)
        {
            Begin(tick);
            return sight < 32 && (inline ? inlineSight < 12 : sight - inlineSight < 20);
        }

        public bool TryTakeSight(long tick, bool inline, bool urgent = false)
        {
            const int Rays = 2;
            if (!CanTakeSight(tick, inline) || (urgent && urgentSight >= 8))
            {
                return false;
            }

            sight += Rays;
            if (inline)
                inlineSight += Rays;
            if (urgent)
                urgentSight += Rays;
            Shared.DevDiag.Count("hunters.firing.raysReserved", Rays);
            return true;
        }

        public bool TryTakeConnection(long tick)
        {
            Begin(tick);
            if (connections >= 4)
                return false;

            connections++;
            Shared.DevDiag.Count("hunters.firing.connections");
            return true;
        }
    }

    /// <summary>Finds an alternate State-0 destination without ordering movement or changing prey.</summary>
    internal sealed unsafe class HunterFiringPositionSearch
    {
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int PreyCorpseFlagOffset = 0x29C;
        private const int PreyReservationOffset = 0x448;
        private const ushort StateFollowingTarget = 1;
        private const ushort StateGaveUp = 6;
        private const ushort StateAiming = 9;
        private const ushort StateShooting = 10;
        private const int MaximumAttackDistance = 28;
        private const int MaximumSightCandidates = 64;
        private const int MaximumTilesPerSlice = 12;
        private const int SearchAnchorDisplacement = 3;
        private const int MaximumAcceptedPositions = 2;
        private const int WorldUnitsPerTile = 8;
        private const int NativeNearRefreshDistance = 20;

        private const long WorkLifetime = HunterSimulationTime.MapTicksPerSecond * 30;
        private const long GiveUpCooldown = HunterSimulationTime.MapTicksPerSecond * 120;
        private const long RejectedPositionPause = HunterSimulationTime.MapTicksPerSecond * 2;

        private static readonly (int X, int Y)[][] OffsetsByBearing;

        static HunterFiringPositionSearch()
        {
            OffsetsByBearing = BuildOffsets();
        }

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterNativeVisibilityProbe visibility;
        private readonly HunterFiringPositionBudget budget;
        private readonly Action<int, uint, long> setCooldownUntil;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, HunterSample> lastSamples = new Dictionary<int, HunterSample>();
        private readonly Dictionary<int, Search> searches = new Dictionary<int, Search>();
        private readonly List<int> expired = new List<int>();
        private int lastServedHunterId;
        private bool failureLogged;

        public HunterFiringPositionSearch(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            HunterNativeVisibilityProbe visibility,
            HunterFiringPositionBudget budget,
            Action<int, uint, long> setCooldownUntil)
        {
            this.log = log;
            this.settings = settings;
            this.visibility = visibility;
            this.budget = budget;
            this.setCooldownUntil = setCooldownUntil;
        }

        public void ResetForMap()
        {
            lock (stateLock)
            {
                lastSamples.Clear();
                searches.Clear();
                lastServedHunterId = 0;
            }
        }

        public void Observe(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            HunterActiveTargetVisibilitySnapshot activeVisibility,
            long tick)
        {
            try
            {
                lock (stateLock)
                {
                    int first = 0;
                    for (int index = 0; index < hunters.Count; index++)
                    {
                        GameUnit* hunter = (GameUnit*)hunters[index];
                        int id = (int)(hunter - units._array) + 1;
                        if (id <= lastServedHunterId)
                            first = index + 1;

                        byte* bytes = (byte*)hunter;
                        ushort state = *(ushort*)(bytes + HunterAiStateOffset);
                        uint target = *(uint*)(bytes + HunterTargetGlobalIdOffset);
                        bool gaveUp = lastSamples.TryGetValue(id, out HunterSample before) &&
                            before.GlobalId == hunter->r_GlobalId && before.Target == target &&
                            before.State == StateFollowingTarget && state == StateGaveUp;
                        lastSamples[id] = new HunterSample(hunter->r_GlobalId, target, state, tick);

                        if ((!gaveUp && state != StateFollowingTarget && state != StateAiming && state != StateShooting) ||
                            hunter->r_CarryGoodAmount != 0 ||
                            !TryGetLiveTarget(units, hunter, target, out GameUnit* prey))
                        {
                            continue;
                        }

                        if (gaveUp)
                        {
                            RequestRecovery(id, hunter, prey, tick, gaveUp: true);
                        }
                        else
                        {
                            int distance = TileDistance(hunter, prey);
                            if (distance <= 1)
                            {
                                RequestRecovery(id, hunter, prey, tick, gaveUp: false);
                            }
                            else if (state == StateFollowingTarget && distance <= MaximumAttackDistance &&
                                prey->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN &&
                                FindSearch(id, hunter, prey, tick) == null &&
                                activeVisibility != null &&
                                activeVisibility.TryGetObservation(id, *(ushort*)(bytes + HunterTargetUnitIdOffset),
                                    target, prey->r_UnitChimp, tick, out HunterActiveVisibilityObservation observation) &&
                                observation.PositionsMatch && observation.State == HunterActiveVisibilityState.Blocked)
                            {
                                StartSearch(id, hunter, prey, tick, 0);
                            }
                        }
                    }

                    // Across scans, start after the last hunter that spent budget. A single blocked pair
                    // cannot hold the queue while hundreds of other hunters need a first slice.
                    for (int count = 0; count < hunters.Count && budget.CanTakeSight(tick, inline: false); count++)
                    {
                        GameUnit* hunter = (GameUnit*)hunters[(first + count) % hunters.Count];
                        int id = (int)(hunter - units._array) + 1;
                        if (!searches.TryGetValue(id, out Search search) || search.ExpiresAt <= tick ||
                            search.Issued || search.Accepted || search.Exhausted ||
                            search.HunterGlobalId != hunter->r_GlobalId ||
                            !TryGetLiveTarget(units, hunter, search.PreyGlobalId, out GameUnit* prey))
                        {
                            continue;
                        }

                        if (search.Ready)
                        {
                            ushort state = *(ushort*)((byte*)hunter + HunterAiStateOffset);
                            if ((state != StateFollowingTarget || TileDistance(hunter, prey) <= 1) &&
                                RefreshReadyConnection(search, hunter, prey, tick))
                            {
                                lastServedHunterId = id;
                            }
                            continue;
                        }

                        if (Advance(search, hunter, prey, tick, inline: false))
                            lastServedHunterId = id;
                        if (search.Exhausted && search.AfterGiveUp)
                            CoolDown(id, search, tick);
                    }

                    expired.Clear();
                    foreach (var entry in searches)
                    {
                        if (entry.Value.ExpiresAt <= tick ||
                            !lastSamples.TryGetValue(entry.Key, out HunterSample sample) || sample.Tick != tick ||
                            sample.GlobalId != entry.Value.HunterGlobalId ||
                            (sample.Target != entry.Value.PreyGlobalId && entry.Value.RejectedUntil <= tick))
                        {
                            expired.Add(entry.Key);
                        }
                    }
                    foreach (int id in expired)
                        searches.Remove(id);

                    expired.Clear();
                    foreach (var entry in lastSamples)
                    {
                        if (entry.Value.Tick != tick)
                            expired.Add(entry.Key);
                    }
                    foreach (int id in expired)
                        lastSamples.Remove(id);
                }
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
            }
        }

        public void StageTooCloseRetry(int hunterUnitId, uint preyGlobalId)
        {
            try
            {
                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (!APIShared.UnitAccess.TryGetById(hunterUnitId, out GameUnit* hunter, out _) ||
                    !TryGetLiveTarget(units, hunter, preyGlobalId, out GameUnit* prey) ||
                    TileDistance(hunter, prey) > 1)
                {
                    return;
                }

                lock (stateLock)
                    RequestRecovery(hunterUnitId, hunter, prey, HunterSimulationTime.Now, gaveUp: false);
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
            }
        }

        // Called after Vanilla has latched the selected target and its lease, immediately before MoveHere.
        public bool TryTakeDestination(
            int hunterUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long tick,
            out int tileX,
            out int tileY)
        {
            tileX = tileY = 0;
            try
            {
                lock (stateLock)
                {
                    if (preyType != eChimps.CHIMP_TYPE_CHICKEN && !searches.ContainsKey(hunterUnitId))
                        return false;

                    SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                    if (!APIShared.UnitAccess.TryGetById(hunterUnitId, out GameUnit* hunter, out _) ||
                        !TryGetLiveTarget(units, hunter, preyGlobalId, out GameUnit* prey) ||
                        prey->r_UnitChimp != preyType ||
                        *(ushort*)((byte*)prey + PreyReservationOffset) != 2)
                    {
                        return false;
                    }

                    Search search = FindSearch(hunterUnitId, hunter, prey, tick);
                    if (search == null)
                    {
                        int distance = TileDistance(hunter, prey);
                        if (prey->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN || distance > MaximumAttackDistance)
                            return false;

                        if (distance > 1)
                        {
                            if (!budget.TryTakeSight(tick, inline: true) ||
                                !visibility.TryEvaluateNearVisibility(hunterUnitId, hunter->r_GlobalId,
                                    *(ushort*)((byte*)hunter + HunterTargetUnitIdOffset), preyGlobalId, preyType,
                                    out _, out int forward, out int backward, out _) ||
                                (forward > 0 && backward > 0))
                            {
                                return false;
                            }
                        }

                        search = StartSearch(hunterUnitId, hunter, prey, tick, 0);
                    }

                    if (search.Issued || search.Accepted || search.Exhausted || tick < search.RejectedUntil)
                        return false;

                    if (search.Ready)
                    {
                        if (TryValidatePosition(search, hunter, prey, tick))
                        {
                            return Issue(search, out tileX, out tileY);
                        }

                        // The same site may become useful again after an animal moves.
                        // Leave the pending choice for the scan if this callback ran out of budget.
                        if (search.Ready)
                            return false;
                    }

                    // A position just evaluated here needs no second ray pair in the same callback.
                    Advance(search, hunter, prey, tick, inline: true);
                    return search.Ready && Issue(search, out tileX, out tileY);
                }
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
                return false;
            }
        }

        public bool RecordMoveResult(int hunterUnitId, uint preyGlobalId, int moveResult, long tick)
        {
            lock (stateLock)
            {
                if (!searches.TryGetValue(hunterUnitId, out Search search) ||
                    !search.Issued || search.PreyGlobalId != preyGlobalId)
                {
                    return false;
                }

                search.Issued = false;
                search.Ready = false;
                search.Accepted = moveResult != 0;
                if (search.Accepted)
                {
                    search.AcceptedPositions++;
                    search.AcceptedAt = tick;
                    search.ExpiresAt = tick + WorkLifetime;
                }
                else
                {
                    search.RejectedUntil = tick + RejectedPositionPause;
                }

                LogSample($"Improved Hunters firing position MoveHere: hunter={hunterUnitId}, prey={preyGlobalId}, " +
                    $"tile={search.TileX},{search.TileY}, accepted={search.Accepted}, attempts={search.AcceptedPositions}.");
                return true;
            }
        }

        private static bool Issue(Search search, out int tileX, out int tileY)
        {
            search.Issued = true;
            tileX = search.TileX;
            tileY = search.TileY;
            return true;
        }

        private void RequestRecovery(int id, GameUnit* hunter, GameUnit* prey, long tick, bool gaveUp)
        {
            Search search = FindSearch(id, hunter, prey, tick);
            if (search == null)
                search = StartSearch(id, hunter, prey, tick, 0);
            else if (search.Accepted && (gaveUp || !search.RetriedAtCloseRange))
            {
                if (search.AcceptedPositions >= MaximumAcceptedPositions)
                {
                    if (gaveUp)
                        CoolDown(id, search, tick);
                    return;
                }

                // While walking away from an overlapping animal, do not replace
                // the newly accepted path before it has had time to move.
                if (!gaveUp && tick < search.AcceptedAt + RejectedPositionPause)
                    return;

                search = StartSearch(id, hunter, prey, tick, search.AcceptedPositions);
                search.RetriedAtCloseRange = !gaveUp;
            }

            search.AfterGiveUp |= gaveUp;
            if (gaveUp && search.Exhausted)
                CoolDown(id, search, tick);
        }

        private void CoolDown(int id, Search search, long tick)
        {
            searches.Remove(id);
            setCooldownUntil(id, search.PreyGlobalId, tick + GiveUpCooldown);
        }

        private Search FindSearch(int id, GameUnit* hunter, GameUnit* prey, long tick)
        {
            if (searches.TryGetValue(id, out Search search) &&
                search.HunterGlobalId == hunter->r_GlobalId &&
                search.PreyGlobalId == prey->r_GlobalId &&
                search.ExpiresAt > tick)
            {
                return search;
            }

            return null;
        }

        private Search StartSearch(int id, GameUnit* hunter, GameUnit* prey, long tick, int previousAttempts)
        {
            var search = new Search(hunter->r_GlobalId, prey->r_GlobalId, tick + WorkLifetime)
            {
                AcceptedPositions = previousAttempts
            };
            searches[id] = search;
            return search;
        }

        private bool TryValidatePosition(Search search, GameUnit* hunter, GameUnit* prey, long tick)
        {
            if (!budget.TryTakeTile(tick, inline: true))
                return false;

            if (!TryGetComponent(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY, out int source) ||
                !TryGetPositionComponent(hunter, prey, search.TileX, search.TileY, out int destination))
            {
                search.Ready = false;
                return false;
            }

            if ((source != destination && !HasFreshConnection(search, hunter, source, destination, tick)) ||
                !budget.TryTakeSight(tick, inline: true))
            {
                return false;
            }

            search.Ready = visibility.TryEvaluateFromTile(search.TileX, search.TileY, prey, out bool visible) && visible;
            return search.Ready;
        }

        private bool RefreshReadyConnection(Search search, GameUnit* hunter, GameUnit* prey, long tick)
        {
            if (!TryGetComponent(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY, out int source) ||
                source == search.Destination ||
                !budget.TryTakeTile(tick, inline: false))
            {
                return false;
            }

            if (!TryGetPositionComponent(hunter, prey, search.TileX, search.TileY, out int destination))
            {
                search.Ready = false;
                return true;
            }

            if (source == destination || HasFreshConnection(search, hunter, source, destination, tick) ||
                !budget.TryTakeConnection(tick))
            {
                return true;
            }

            search.Ready = HasConnection(hunter, source, destination);
            if (search.Ready)
                RecordConnection(search, hunter, source, destination, tick);
            return true;
        }

        private static bool HasFreshConnection(Search search, GameUnit* hunter, int source, int destination, long tick) =>
            tick - search.ValidatedAt < HunterFiringPositionBudget.WindowTicks &&
            source == search.ValidatedSource && destination == search.Destination &&
            search.Player == hunter->r_ControllableForPlayerId && search.Mode == hunter->r_PathConnectionMode;

        private static bool HasConnection(GameUnit* hunter, int source, int destination) =>
            APIShared.Pathfinding.PathReachability.BetweenComponents(
                hunter->r_ControllableForPlayerId, (PathConnectionQueryMode)hunter->r_PathConnectionMode,
                source, destination) == APIShared.Pathfinding.PathReachabilityResult.Connected;

        private static void RecordConnection(Search search, GameUnit* hunter, int source, int destination, long tick)
        {
            search.ValidatedSource = source;
            search.Destination = destination;
            search.Player = hunter->r_ControllableForPlayerId;
            search.Mode = hunter->r_PathConnectionMode;
            search.ValidatedAt = tick;
        }

        private bool Advance(Search search, GameUnit* hunter, GameUnit* prey, long tick, bool inline)
        {
            if (!TryGetComponent(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY, out int source))
                return false;

            int preyX = prey->r_CurrentTilePositionX;
            int preyY = prey->r_CurrentTilePositionY;
            // Offsets are always evaluated around the current prey. Small moves should not
            // make a walking animal restart at the first direction every scan.
            if (search.Source != source ||
                Math.Max(Math.Abs(search.OriginX - preyX), Math.Abs(search.OriginY - preyY)) >= SearchAnchorDisplacement)
            {
                search.OriginX = preyX;
                search.OriginY = preyY;
                search.Source = source;
                search.Bearing = GetBearing(hunter->r_CurrentTilePositionX - preyX, hunter->r_CurrentTilePositionY - preyY);
                search.Cursor = 0;
            }

            var offsets = OffsetsByBearing[search.Bearing];
            bool spent = false;
            int deferredCursor = -1;
            int sightAtStart = search.CheckedSight;
            if (!inline && tick < search.RejectedUntil)
                return false;

            for (int visited = 0; visited < MaximumTilesPerSlice && search.Cursor < offsets.Length &&
                (inline || search.CheckedSight - sightAtStart < 2) && budget.CanTakeSight(tick, inline); visited++)
            {
                if (!budget.TryTakeTile(tick, inline))
                    break;

                spent = true;
                var offset = offsets[search.Cursor];
                int x = preyX + offset.X;
                int y = preyY + offset.Y;
                if (!TryGetPositionComponent(hunter, prey, x, y, out int destination))
                {
                    search.Cursor++;
                    continue;
                }

                if (source != destination)
                {
                    if (inline)
                    {
                        // Keep the first skipped offset for the player-aware scan.
                        if (deferredCursor < 0)
                            deferredCursor = search.Cursor;
                        search.Cursor++;
                        continue;
                    }
                    if (!budget.TryTakeConnection(tick))
                        break;
                    if (!HasConnection(hunter, source, destination))
                    {
                        search.Cursor++;
                        continue;
                    }
                }

                if (!budget.TryTakeSight(tick, inline))
                    break;

                search.Cursor++;
                search.CheckedSight++;
                if (visibility.TryEvaluateFromTile(x, y, prey, out bool visible) && visible)
                {
                    search.TileX = x;
                    search.TileY = y;
                    RecordConnection(search, hunter, source, destination, tick);
                    search.Ready = true;
                    LogSample($"Improved Hunters firing position staged: hunter={search.HunterGlobalId}, " +
                        $"prey={search.PreyGlobalId}, tile={x},{y}, inline={inline}, sightChecks={search.CheckedSight}.");
                    return true;
                }

                if (search.CheckedSight >= MaximumSightCandidates)
                    break;
            }

            if (deferredCursor >= 0)
                search.Cursor = deferredCursor;
            search.Exhausted = search.Cursor >= offsets.Length || search.CheckedSight >= MaximumSightCandidates;
            return spent;
        }

        private static bool TryGetComponent(int x, int y, out int component) =>
            APIShared.Pathfinding.PathReachability.TryGetComponent(x, y, out component) && component != 0;

        private static bool TryGetPositionComponent(GameUnit* hunter, GameUnit* prey, int x, int y, out int component)
        {
            component = 0;
            int dx = Math.Abs(x - prey->r_CurrentTilePositionX);
            int dy = Math.Abs(y - prey->r_CurrentTilePositionY);
            if (dx + dy > MaximumAttackDistance || Math.Max(
                    Math.Abs(x * WorldUnitsPerTile + WorldUnitsPerTile / 2 - prey->r_CurrentWorldPositionX),
                    Math.Abs(y * WorldUnitsPerTile + WorldUnitsPerTile / 2 - prey->r_CurrentWorldPositionY)) <= NativeNearRefreshDistance ||
                (x == hunter->r_CurrentTilePositionX && y == hunter->r_CurrentTilePositionY) ||
                !TryGetComponent(x, y, out component))
            {
                return false;
            }

            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            return tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(x, y));
        }

        private bool TryGetLiveTarget(SimpleNativeArray<GameUnit> units, GameUnit* hunter, uint globalId, out GameUnit* prey)
        {
            prey = null;
            if (hunter == null || hunter->r_GlobalId == 0 || hunter->r_CurrentHealth == 0 ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || !APIShared.UnitAccess.IsReallyAlive(hunter) ||
                units._array == null || globalId == 0)
            {
                return false;
            }

            byte* bytes = (byte*)hunter;
            int id = *(ushort*)(bytes + HunterTargetUnitIdOffset);
            if (*(uint*)(bytes + HunterTargetGlobalIdOffset) != globalId || id <= 0 || id > units.Length)
                return false;

            prey = units.GetValuePointer(id - 1);
            return prey->r_GlobalId == globalId && APIShared.UnitAccess.IsReallyAlive(prey) &&
                prey->r_CurrentHealth > 0 && *(ushort*)((byte*)prey + PreyCorpseFlagOffset) == 0 &&
                settings.IsKnownAnimal(prey->r_UnitChimp) && settings.IsHuntingEnabled(prey->r_UnitChimp) &&
                (prey->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    prey->r_ControllableForPlayerId == hunter->r_ControllableForPlayerId);
        }

        private static int TileDistance(GameUnit* hunter, GameUnit* prey) =>
            Math.Abs(hunter->r_CurrentTilePositionX - prey->r_CurrentTilePositionX) +
            Math.Abs(hunter->r_CurrentTilePositionY - prey->r_CurrentTilePositionY);

        private static int GetBearing(int dx, int dy)
        {
            if (Math.Abs(dx) > 2 * Math.Abs(dy))
                return dx < 0 ? 4 : 0;
            if (Math.Abs(dy) > 2 * Math.Abs(dx))
                return dy < 0 ? 6 : 2;
            return dy < 0 ? (dx < 0 ? 5 : 7) : (dx < 0 ? 3 : 1);
        }

        private static (int X, int Y)[][] BuildOffsets()
        {
            var directions = new (int X, int Y)[]
            {
                (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)
            };
            int[] rings = { 4, 7, 10, 14 };
            // Probe the approach side and then the opposite side before filling the intervening sectors.
            int[] order = { 0, 4, 1, 7, 2, 6, 3, 5 };
            var result = new (int X, int Y)[directions.Length][];
            for (int bearing = 0; bearing < directions.Length; bearing++)
            {
                var offsets = new List<(int, int)>();
                var seen = new HashSet<(int, int)>();
                foreach (int ring in rings)
                {
                    foreach (int direction in order)
                    {
                        var offset = directions[(bearing + direction) % directions.Length];
                        var point = (offset.X * ring, offset.Y * ring);
                        offsets.Add(point);
                        seen.Add(point);
                    }
                }

                for (int ring = 4; ring <= 14; ring++)
                {
                    var refinement = new List<(int X, int Y)>();
                    for (int x = -ring; x <= ring; x++)
                    {
                        int step = Math.Abs(x) == ring ? 1 : 2 * ring;
                        for (int y = -ring; y <= ring; y += step)
                        {
                            if (seen.Add((x, y)))
                                refinement.Add((x, y));
                        }
                    }
                    var heading = directions[bearing];
                    refinement.Sort((left, right) =>
                    {
                        int leftGain = left.X * heading.X + left.Y * heading.Y;
                        int rightGain = right.X * heading.X + right.Y * heading.Y;
                        int resultOrder = rightGain.CompareTo(leftGain);
                        if (resultOrder == 0)
                            resultOrder = left.X.CompareTo(right.X);
                        return resultOrder != 0 ? resultOrder : left.Y.CompareTo(right.Y);
                    });
                    offsets.AddRange(refinement);
                }
                result[bearing] = offsets.ToArray();
            }

            return result;
        }

        private void ReportFailure(Exception exception)
        {
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(log, $"Improved Hunters firing-position search failed: {exception}");
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogSample(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private readonly struct HunterSample
        {
            public readonly uint GlobalId;
            public readonly uint Target;
            public readonly ushort State;
            public readonly long Tick;

            public HunterSample(uint globalId, uint target, ushort state, long tick)
            {
                GlobalId = globalId;
                Target = target;
                State = state;
                Tick = tick;
            }
        }

        private sealed class Search
        {
            public readonly uint HunterGlobalId;
            public readonly uint PreyGlobalId;
            public long ExpiresAt;
            public int OriginX, OriginY, Source, ValidatedSource, Destination, Bearing, Cursor, CheckedSight;
            public int TileX, TileY, AcceptedPositions, Player, Mode;
            public long ValidatedAt, RejectedUntil, AcceptedAt;
            public bool Ready, Issued, Accepted, Exhausted, AfterGiveUp, RetriedAtCloseRange;

            public Search(uint hunterGlobalId, uint preyGlobalId, long expiresAt)
            {
                HunterGlobalId = hunterGlobalId;
                PreyGlobalId = preyGlobalId;
                ExpiresAt = expiresAt;
            }
        }
    }
}
