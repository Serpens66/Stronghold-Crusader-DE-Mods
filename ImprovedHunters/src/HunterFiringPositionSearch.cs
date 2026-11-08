using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ImprovedHunters
{
    /// <summary>
    /// A Hunter whose approach ended without sight of his prey (Vanilla gives up: State 1 to State 6 with the
    /// same live target and nothing carried) gets one other place to shoot from: a walkable tile he can reach as
    /// his player, from which Vanilla's own sight check sees the prey. Vanilla's next State-0 MoveHere for this
    /// Hunter and prey goes there instead of to the prey; Vanilla paths and attacks as usual. Without such a tile,
    /// or when that approach fails as well, the pair waits a cooldown. Everything runs on the simulation tick from
    /// simulation data, so all peers decide alike. The search is rare: once per failed approach, with a fixed budget.
    /// </summary>
    internal sealed unsafe class HunterFiringPositionSearch
    {
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int PreyCorpseFlagOffset = 0x29C;
        private const ushort StateFollowingTarget = 1;
        private const ushort StateGaveUp = 6;
        // Within 20 world units (2.5 tiles) Vanilla's State-1 near refresh (0x130069) sends the Hunter onto the prey's
        // tile, where the direct attack fails; ring 4 (32 units) stays outside it. 12 stays well inside the attack
        // distance of 28 tiles.
        private const int NearestRing = 4;
        private const int FarthestRing = 12;
        private const int MaximumSightChecks = 64;

        private static readonly long StagedLifetime = HunterSimulationTime.MapTicksPerSecond * 30;
        private static readonly long GiveUpCooldown = HunterSimulationTime.MapTicksPerSecond * 120;

        private readonly ManualLogSource log;
        private readonly HunterNativeVisibilityProbe visibility;
        private readonly Action<int, uint, long> setCooldownUntil;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, HunterSample> lastSamples = new Dictionary<int, HunterSample>();
        private readonly Dictionary<int, FiringPosition> staged = new Dictionary<int, FiringPosition>();
        private readonly HashSet<int> seenHunters = new HashSet<int>();
        private readonly List<int> goneHunters = new List<int>();
        private readonly Dictionary<ushort, bool> reachableLand = new Dictionary<ushort, bool>();
        private bool failureLogged;

        public HunterFiringPositionSearch(ManualLogSource log, HunterNativeVisibilityProbe visibility, Action<int, uint, long> setCooldownUntil)
        {
            this.log = log;
            this.visibility = visibility;
            this.setCooldownUntil = setCooldownUntil;
        }

        public void ResetForMap()
        {
            lock (stateLock)
            {
                lastSamples.Clear();
                staged.Clear();
            }
        }

        /// <summary>Called from the native scan with this tick's Hunters; cost grows with Hunters, not with animals.</summary>
        public void Observe(SimpleNativeArray<GameUnit> units, List<IntPtr> hunters, long timestamp)
        {
            try
            {
                lock (stateLock)
                {
                    seenHunters.Clear();
                    foreach (IntPtr address in hunters)
                    {
                        GameUnit* hunter = (GameUnit*)address;
                        int hunterId = checked((int)(hunter - units._array) + 1);
                        byte* bytes = (byte*)hunter;
                        var now = new HunterSample(*(ushort*)(bytes + HunterAiStateOffset), *(uint*)(bytes + HunterTargetGlobalIdOffset));
                        seenHunters.Add(hunterId);
                        if (lastSamples.TryGetValue(hunterId, out HunterSample before) &&
                            before.State == StateFollowingTarget && now.State == StateGaveUp &&
                            now.TargetGlobalId != 0 && now.TargetGlobalId == before.TargetGlobalId &&
                            hunter->r_CarryGoodAmount == 0 &&
                            TryGetLiveTarget(units, bytes, now.TargetGlobalId, out GameUnit* prey))
                        {
                            OnApproachGivenUp(hunterId, hunter, prey, now.TargetGlobalId, timestamp);
                        }

                        lastSamples[hunterId] = now;
                    }

                    // Hunters that left the scan (died, became something else) leave no state behind.
                    goneHunters.Clear();
                    foreach (int hunterId in lastSamples.Keys)
                    {
                        if (!seenHunters.Contains(hunterId))
                            goneHunters.Add(hunterId);
                    }
                    foreach (int hunterId in goneHunters)
                    {
                        lastSamples.Remove(hunterId);
                        staged.Remove(hunterId);
                    }
                }
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;
                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters firing-position search failed: {exception}");
            }
        }

        /// <summary>
        /// Called from the direct-attack result hook when Vanilla's attack failed at tile distance 0 or 1 (the prey
        /// walked onto or beside the Hunter). The failed-attack recovery that follows would send him onto the prey's
        /// tile again; a staged firing position makes that MoveHere go to a tile he can shoot from instead.
        /// </summary>
        public void OnAttackFailedTooClose(int hunterUnitId, uint preyGlobalId)
        {
            try
            {
                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (!APIShared.UnitAccess.TryGetById(hunterUnitId, out GameUnit* hunter, out _) || hunter == null ||
                    !TryGetLiveTarget(units, (byte*)hunter, preyGlobalId, out GameUnit* prey))
                {
                    return;
                }

                lock (stateLock)
                {
                    // An unused firing position stays; a used one may be replaced once, because the prey can walk up
                    // to the Hunter there. After that the give-up path sets the cooldown.
                    int restages = 0;
                    if (staged.TryGetValue(hunterUnitId, out FiringPosition earlier) && earlier.PreyGlobalId == preyGlobalId)
                    {
                        if (!earlier.Handed || earlier.Restages >= 1)
                            return;
                        restages = earlier.Restages + 1;
                    }

                    long timestamp = HunterSimulationTime.Now;
                    if (TrySearch(hunter, prey, out int tileX, out int tileY, out int checks))
                    {
                        staged[hunterUnitId] = new FiringPosition(preyGlobalId, tileX, tileY, timestamp + StagedLifetime) { Restages = restages };
                        LogSample($"Improved Hunters firing position staged after a too-close attack: hunter={hunterUnitId}, prey={preyGlobalId}, " +
                            $"tile={tileX},{tileY}, sightChecks={checks}.");
                    }
                }
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;
                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters firing-position search failed: {exception}");
            }
        }

        /// <summary>
        /// Called from the hook right before Vanilla's State-0 MoveHere. Hands out the staged tile once, and only
        /// while the Hunter still aims at the same prey; otherwise Vanilla's own destination stays.
        /// </summary>
        public bool TryTakeDestination(int hunterUnitId, long timestamp, out int tileX, out int tileY)
        {
            tileX = 0;
            tileY = 0;
            if (!APIShared.UnitAccess.TryGetById(hunterUnitId, out GameUnit* hunter, out _) || hunter == null)
                return false;

            uint targetGlobalId = *(uint*)((byte*)hunter + HunterTargetGlobalIdOffset);
            lock (stateLock)
            {
                if (!staged.TryGetValue(hunterUnitId, out FiringPosition position) ||
                    position.Handed || position.PreyGlobalId != targetGlobalId || timestamp >= position.ExpiresAt)
                {
                    return false;
                }

                position.Handed = true;
                staged[hunterUnitId] = position;
                tileX = position.TileX;
                tileY = position.TileY;
                return true;
            }
        }

        private void OnApproachGivenUp(int hunterId, GameUnit* hunter, GameUnit* prey, uint preyGlobalId, long timestamp)
        {
            // The approach to the firing position failed as well: give up on this prey for a while.
            if (staged.TryGetValue(hunterId, out FiringPosition earlier) && earlier.PreyGlobalId == preyGlobalId && earlier.Handed)
            {
                staged.Remove(hunterId);
                setCooldownUntil(hunterId, preyGlobalId, timestamp + GiveUpCooldown);
                LogSample($"Improved Hunters firing position did not help: hunter={hunterId}, prey={preyGlobalId}, cooldownSeconds=120.");
                return;
            }

            if (TrySearch(hunter, prey, out int tileX, out int tileY, out int checks))
            {
                staged[hunterId] = new FiringPosition(preyGlobalId, tileX, tileY, timestamp + StagedLifetime);
                LogSample($"Improved Hunters firing position staged: hunter={hunterId}, prey={preyGlobalId}, " +
                    $"preyTile={prey->r_CurrentTilePositionX},{prey->r_CurrentTilePositionY}, tile={tileX},{tileY}, sightChecks={checks}.");
                return;
            }

            staged.Remove(hunterId);
            setCooldownUntil(hunterId, preyGlobalId, timestamp + GiveUpCooldown);
            LogSample($"Improved Hunters found no firing position: hunter={hunterId}, prey={preyGlobalId}, sightChecks={checks}, cooldownSeconds=120.");
        }

        // Rings around the prey, nearest first; a tile counts when it is walkable, reachable for the Hunter's
        // player (enemy gates and walls included) and Vanilla's sight check from it sees the prey.
        private bool TrySearch(GameUnit* hunter, GameUnit* prey, out int foundX, out int foundY, out int checks)
        {
            foundX = 0;
            foundY = 0;
            checks = 0;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int hunterTile = tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY);
            if (!tiles.IsValidTileId(hunterTile) || (uint)hunterTile >= (uint)components.Length || components[hunterTile] == 0)
                return false;

            ushort hunterLand = components[hunterTile];
            int player = hunter->r_ControllableForPlayerId;
            int mode = hunter->r_PathConnectionMode;
            int preyX = prey->r_CurrentTilePositionX, preyY = prey->r_CurrentTilePositionY;
            reachableLand.Clear();
            for (int ring = NearestRing; ring <= FarthestRing; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    // Only the ring's border: full rows at the top and bottom, the two ends in between.
                    int step = dx == -ring || dx == ring ? 1 : 2 * ring;
                    for (int dy = -ring; dy <= ring; dy += step)
                    {
                        int x = preyX + dx, y = preyY + dy;
                        if (!tiles.IsTileInsideMapBounds(x, y))
                            continue;

                        int tile = tiles.GetTileId(x, y);
                        if (!tiles.IsValidTileId(tile) || (uint)tile >= (uint)components.Length || !tiles.IsTileWalkableAndUnoccupied(tile))
                            continue;

                        ushort land = components[tile];
                        if (land == 0 || !IsReachable(player, hunterLand, land, mode))
                            continue;

                        if (checks >= MaximumSightChecks)
                            return false;

                        checks++;
                        if (visibility.TryEvaluateFromTile(x, y, tiles.GetTileHeight(tile), prey, out bool visible) && visible)
                        {
                            foundX = x;
                            foundY = y;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsReachable(int player, ushort hunterLand, ushort land, int mode)
        {
            if (land == hunterLand)
                return true;

            if (!reachableLand.TryGetValue(land, out bool reachable))
            {
                reachable = GamePathingManagerAPI.Instance.FindNextComponentTowardDestination(
                    player, hunterLand, land, (PathConnectionQueryMode)mode) != 0;
                reachableLand[land] = reachable;
            }

            return reachable;
        }

        private static bool TryGetLiveTarget(SimpleNativeArray<GameUnit> units, byte* hunterBytes, uint globalId, out GameUnit* prey)
        {
            prey = null;
            int preyId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            if (preyId <= 0 || preyId > units.Length)
                return false;

            prey = units.GetValuePointer(preyId - 1);
            return prey->r_GlobalId == globalId && APIShared.UnitAccess.IsReallyAlive(prey) &&
                prey->r_CurrentHealth > 0 && *(ushort*)((byte*)prey + PreyCorpseFlagOffset) == 0;
        }

        // Development samples only: /devdiag builds compile these calls including their message text.
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogSample(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private readonly struct HunterSample
        {
            public readonly ushort State;
            public readonly uint TargetGlobalId;

            public HunterSample(ushort state, uint targetGlobalId)
            {
                State = state;
                TargetGlobalId = targetGlobalId;
            }
        }

        private struct FiringPosition
        {
            public readonly uint PreyGlobalId;
            public readonly int TileX;
            public readonly int TileY;
            public readonly long ExpiresAt;
            public bool Handed;
            public int Restages;

            public FiringPosition(uint preyGlobalId, int tileX, int tileY, long expiresAt)
            {
                PreyGlobalId = preyGlobalId;
                TileX = tileX;
                TileY = tileY;
                ExpiresAt = expiresAt;
                Handed = false;
                Restages = 0;
            }
        }
    }
}
