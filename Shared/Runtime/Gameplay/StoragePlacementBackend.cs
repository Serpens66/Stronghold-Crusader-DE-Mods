using System;
using System.Collections.Generic;
using System.Threading;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Shared
{
    internal readonly struct StorageCreatedSection
    {
        internal StorageCreatedSection(int anchorId, int lastId, uint lastGlobalId)
        { AnchorBuildingId = anchorId; LastBuildingId = lastId; LastGlobalId = lastGlobalId; }
        internal int AnchorBuildingId { get; }
        internal int LastBuildingId { get; }
        internal uint LastGlobalId { get; }
    }

    internal unsafe class StoragePlacementBackend : IDeferredStorageBackend
    {
        // 51790's automatic sprite sentinel, not a direction or building variant.
        private const int AutomaticSpriteVariation = 0x0f;
        private const int NativeMaximumHeight = 200;
        private readonly int playerId;
        private readonly Dictionary<Tuple<eStructs, int, int>, Identity> observed =
            new Dictionary<Tuple<eStructs, int, int>, Identity>();
        private readonly ThreadLocal<Stack<Capture>> captures =
            new ThreadLocal<Stack<Capture>>(() => new Stack<Capture>());

        internal StoragePlacementBackend(int playerId)
        {
            this.playerId = playerId;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding b = ref buildings[spanIndex];
                if (b.r_PlayerIdOwner == playerId && IsStorageType(b.r_BuildingType) && IsPresent(b.r_AliveState))
                    Remember(spanIndex + 1, ref b);
            }
        }

        public virtual bool IsCurrent(StoragePlacement placement, out string reason)
        { reason = string.Empty; return true; }
        public virtual void OnReady(StoragePlacement placement, bool created) { }
        protected virtual void OnCreated(StoragePlacement placement, StorageCreatedSection section) { }

        public StorageSectionState Inspect(StoragePlacement placement)
        {
            StorageSectionState anchor = InspectAt(placement.Mapper.ConvertToEStructs(), placement.X, placement.Y);
            if (anchor == StorageSectionState.Missing || anchor == StorageSectionState.Lost ||
                placement.Mapper != eMappers.MAPPER_STORES) return anchor;
            // 76E80's four 2x2 subrecord origins, table RVA 2D2FB8 on the audited
            // image: (0,0), (3,0), (0,3), (3,3). Its remaining nine tiles are links.
            // Never call an entire section ready while another subrecord needs init.
            return CombineYardStates(anchor,
                InspectAt(eStructs.STRUCT_GOODS_YARD, placement.X + 3, placement.Y),
                InspectAt(eStructs.STRUCT_GOODS_YARD, placement.X, placement.Y + 3),
                InspectAt(eStructs.STRUCT_GOODS_YARD, placement.X + 3, placement.Y + 3));
        }

        internal static StorageSectionState CombineYardStates(StorageSectionState first,
            StorageSectionState second, StorageSectionState third, StorageSectionState fourth)
        {
            if (first == StorageSectionState.Lost || second == StorageSectionState.Lost ||
                third == StorageSectionState.Lost || fourth == StorageSectionState.Lost ||
                first == StorageSectionState.Missing || second == StorageSectionState.Missing ||
                third == StorageSectionState.Missing || fourth == StorageSectionState.Missing)
                return StorageSectionState.Lost;
            return first == StorageSectionState.IsAlive && second == StorageSectionState.IsAlive &&
                third == StorageSectionState.IsAlive && fourth == StorageSectionState.IsAlive
                ? StorageSectionState.IsAlive : StorageSectionState.NeedsInit;
        }

        private StorageSectionState InspectAt(eStructs type, int x, int y)
        {
            var key = Tuple.Create(type, x, y);
            if (observed.TryGetValue(key, out Identity previous))
                return InspectIdentity(previous, type, x, y);
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding b = ref buildings[spanIndex];
                if (Matches(ref b, type, x, y) && IsPresent(b.r_AliveState))
                {
                    Identity identity = Remember(spanIndex + 1, ref b);
                    return InspectIdentity(identity, type, x, y);
                }
            }
            return StorageSectionState.Missing;
        }

        public bool HasInitializedSeed(eMappers mapper)
        {
            eStructs type = mapper.ConvertToEStructs();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding b = ref buildings[spanIndex];
                if (b.r_PlayerIdOwner == playerId && b.r_BuildingType == type && b.r_AliveState == AliveState.IsAlive)
                    return true;
            }
            return false;
        }

        public bool CanPlace(StoragePlacement placement, out string reason)
        {
            reason = string.Empty;
            if (!StoragePlacement.IsStorage(placement.Mapper) ||
                placement.Size != BuildingScales.GetScale(placement.Mapper))
            { reason = "unexpected-storage-footprint"; return false; }
            var tiles = GameTileManagerAPI.Instance;
            for (int y = 0; y < placement.Size; y++)
                for (int x = 0; x < placement.Size; x++)
                {
                    int tileX = placement.X + x, tileY = placement.Y + y;
                    if (!tiles.IsTileInsideMapBounds(tileX, tileY))
                    { reason = "footprint-out-of-bounds"; return false; }
                    int tileId = tiles.GetTileId(tileX, tileY);
                    if (!tiles.IsValidTileId(tileId))
                    { reason = "invalid-footprint-tile"; return false; }
                    if (tiles.GetTileBuildingId(tileId) != 0 || tiles.GetTileHeight(tileId) > NativeMaximumHeight ||
                        IsBlockedTerrain(tiles.GetTilePropertyFlag(tileId)))
                    { reason = "occupied-or-blocked-footprint"; return false; }
                }
            return true;
        }

        internal static bool IsBlockedTerrain(TilePropertyFlag flags)
        {
            // The audited storage subset of the AIV physical terrain contract.
            const TilePropertyFlag blocked = TilePropertyFlag.Sea | TilePropertyFlag.IsFarm |
                TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder | TilePropertyFlag.IsWall |
                TilePropertyFlag.IsBuilding | TilePropertyFlag.IsElevated | TilePropertyFlag.River |
                TilePropertyFlag.Ford | TilePropertyFlag.IsWheat | TilePropertyFlag.IsHops |
                TilePropertyFlag.IsAppleFarm | TilePropertyFlag.IsFarmFence | TilePropertyFlag.IsSwamp | TilePropertyFlag.IsMoat;
            return (flags & (blocked | TilePropertyFlag.ImpassableEdge)) != 0;
        }

        public void ObserveSpawn(BuildingSpawnEventArgs args)
        {
            Stack<Capture> stack = captures.Value;
            if (stack.Count == 0 || args.Phase != EventHookPhase.Post) return;
            Capture capture = stack.Peek();
            StoragePlacement p = capture.Placement;
            if (args.PlayerId != playerId || args.Building != p.Mapper.ConvertToEStructs() ||
                args.TileX < p.X || args.TileY < p.Y || args.TileX >= p.X + p.Size || args.TileY >= p.Y + p.Size)
                return;
            capture.Events++;
            if (args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
            int id = (int)args.ReturnValue;
            if (!global::APIShared.BuildingAccess.TryGetById(id, out GameBuilding* b, out _) || b == null ||
                !Matches(ref *b, args.Building, args.TileX, args.TileY) || !IsPresent(b->r_AliveState)) return;
            Identity identity = new Identity(id, b->r_GlobalId);
            capture.Identities.Add(identity);
            if (args.TileX == p.X && args.TileY == p.Y) capture.Anchor = identity;
        }

        public bool TryCreate(StoragePlacement placement, out string reason)
        {
            reason = string.Empty;
            var capture = new Capture(placement);
            Stack<Capture> stack = captures.Value;
            var tiles = GameTileManagerAPI.Instance;
            bool oldEnabled = tiles.TileManager.UsePlacementBlockedOverride;
            bool oldValue = tiles.TileManager.PlacementBlockedOverrideValue;
            stack.Push(capture);
            try
            {
                // Even a surrounding temporary override must not weaken this invocation.
                tiles.TileManager.UsePlacementBlockedOverride = false;
                GameBuildingManagerAPI.Instance.CreatePrefab(playerId, placement.X, placement.Y,
                    placement.Mapper, placement.Size, AutomaticSpriteVariation, true, false);
            }
            finally
            {
                tiles.TileManager.PlacementBlockedOverrideValue = oldValue;
                tiles.TileManager.UsePlacementBlockedOverride = oldEnabled;
                stack.Pop();
            }
            int expected = placement.Mapper == eMappers.MAPPER_STORES ? 4 : 1;
            if (capture.Events != expected || capture.Identities.Count != expected || capture.Anchor.Id <= 0)
            { reason = "unconfirmed-storage-spawn"; return false; }
            eStructs type = placement.Mapper.ConvertToEStructs();
            var ids = new HashSet<int>();
            foreach (Identity identity in capture.Identities)
            {
                if (!ids.Add(identity.Id) || !global::APIShared.BuildingAccess.TryGetById(identity.Id, out GameBuilding* b, out _) ||
                    b == null || b->r_GlobalId != identity.GlobalId || !IsPresent(b->r_AliveState) ||
                    b->r_PlayerIdOwner != playerId || b->r_BuildingType != type ||
                    b->r_TilePositionXBegin < placement.X || b->r_TilePositionXBegin >= placement.X + placement.Size ||
                    b->r_TilePositionYBegin < placement.Y || b->r_TilePositionYBegin >= placement.Y + placement.Size)
                { reason = "changed-storage-spawn-identity"; return false; }
                Remember(identity.Id, ref *b);
            }
            Identity last = capture.Identities[capture.Identities.Count - 1];
            OnCreated(placement, new StorageCreatedSection(capture.Anchor.Id, last.Id, last.GlobalId));
            return true;
        }

        private StorageSectionState InspectIdentity(Identity identity, eStructs type, int x, int y)
        {
            if (!global::APIShared.BuildingAccess.TryGetById(identity.Id, out GameBuilding* b, out _) || b == null ||
                b->r_GlobalId != identity.GlobalId || !Matches(ref *b, type, x, y) || !IsPresent(b->r_AliveState))
                return StorageSectionState.Lost;
            return b->r_AliveState == AliveState.IsAlive ? StorageSectionState.IsAlive : StorageSectionState.NeedsInit;
        }
        private Identity Remember(int id, ref GameBuilding building)
        {
            var identity = new Identity(id, building.r_GlobalId);
            observed[Tuple.Create(building.r_BuildingType, (int)building.r_TilePositionXBegin,
                (int)building.r_TilePositionYBegin)] = identity;
            return identity;
        }
        private bool Matches(ref GameBuilding b, eStructs type, int x, int y) => b.r_PlayerIdOwner == playerId &&
            b.r_BuildingType == type && b.r_TilePositionXBegin == x && b.r_TilePositionYBegin == y;
        private static bool IsPresent(AliveState state) => state == AliveState.NeedsInit || state == AliveState.IsAlive;
        private static bool IsStorageType(eStructs type) => type == eStructs.STRUCT_GOODS_YARD ||
            type == eStructs.STRUCT_GRANARY || type == eStructs.STRUCT_ARMOURY;
        private readonly struct Identity
        {
            internal Identity(int id, uint globalId) { Id = id; GlobalId = globalId; }
            internal int Id { get; }
            internal uint GlobalId { get; }
        }
        private sealed class Capture
        {
            internal Capture(StoragePlacement placement) { Placement = placement; }
            internal readonly StoragePlacement Placement;
            internal readonly List<Identity> Identities = new List<Identity>();
            internal Identity Anchor;
            internal int Events;
        }
    }
}
