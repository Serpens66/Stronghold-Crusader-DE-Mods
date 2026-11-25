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
        private const int PlacementBlocked = 1;
        // Stores adjacency failure from the audited Stores branch of the placement validator.
        private const int StoresNeedsLiveNeighbor = 3;
        private readonly int playerId;
        private readonly Func<(int State, int Reason)?> readValidation;
        private readonly Dictionary<Tuple<eStructs, int, int>, Identity> observed =
            new Dictionary<Tuple<eStructs, int, int>, Identity>();
        private readonly ThreadLocal<Stack<Capture>> captures =
            new ThreadLocal<Stack<Capture>>(() => new Stack<Capture>());

        internal StoragePlacementBackend(int playerId, Func<(int State, int Reason)?> readValidation = null)
        {
            this.playerId = playerId;
            this.readValidation = readValidation;
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
            if (stack.Count == 0) return;
            Capture capture = stack.Peek();
            // A canceled or redirected spawn is still evidence that construction was reached.
            if (args.Phase == EventHookPhase.Pre || args.Phase == EventHookPhase.Post)
                capture.SpawnObserved = true;
            if (args.Phase != EventHookPhase.Post) return;
            StoragePlacement p = capture.Placement;
            if (args.PlayerId != playerId || args.Building != p.Mapper.ConvertToEStructs() ||
                args.TileX < p.X || args.TileY < p.Y || args.TileX >= p.X + p.Size || args.TileY >= p.Y + p.Size)
                return;
            capture.Events++;
            string invalidReturn = InvalidSpawnReturn(args.ReturnValue);
            if (invalidReturn != null)
            {
                capture.RejectSpawn(invalidReturn);
                return;
            }
            int id = (int)args.ReturnValue;
            if (!global::APIShared.BuildingAccess.TryGetById(id, out GameBuilding* b, out _) || b == null)
            {
                capture.RejectSpawn("lookup-failed");
                return;
            }
            string mismatch = SpawnMismatch(in *b, playerId, args.Building, args.TileX, args.TileY);
            if (mismatch != null)
            {
                capture.RejectSpawn(mismatch);
                return;
            }
            Identity identity = new Identity(id, b->r_GlobalId);
            capture.Identities.Add(identity);
            if (args.TileX == p.X && args.TileY == p.Y) capture.Anchor = identity;
        }

        internal static string InvalidSpawnReturn(long id) => id == 0 ? "return=0" :
            id < 0 || id > int.MaxValue ? "return-out-of-range" : null;

        internal static string SpawnMismatch(in GameBuilding building, int owner, eStructs type, int x, int y)
        {
            if (building.r_PlayerIdOwner != owner) return "owner-mismatch";
            if (building.r_BuildingType != type) return "type-mismatch";
            if (building.r_TilePositionXBegin != x || building.r_TilePositionYBegin != y) return "tile-mismatch";
            return IsPresent(building.r_AliveState) ? null : "alive-state-mismatch";
        }

        internal void ObserveValidation(BuildingPlacementValidationEventArgs args)
        {
            Stack<Capture> stack = captures.Value;
            if (stack.Count != 0) stack.Peek().Validation?.Observe(args);
        }

        public StorageCreateResult TryCreate(StoragePlacement placement, out string reason)
        {
            reason = string.Empty;
            var capture = new Capture(placement, readValidation == null ? null :
                new ValidationObservation(playerId, placement, readValidation));
            Stack<Capture> stack = captures.Value;
            var tiles = GameTileManagerAPI.Instance;
            bool oldEnabled = tiles.TileManager.UsePlacementBlockedOverride;
            bool oldValue = tiles.TileManager.PlacementBlockedOverrideValue;
            long placementReturn;
            stack.Push(capture);
            try
            {
                // Even a surrounding temporary override must not weaken this invocation.
                tiles.TileManager.UsePlacementBlockedOverride = false;
                placementReturn = GameBuildingManagerAPI.Instance.CreatePrefab(playerId, placement.X, placement.Y,
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
            {
                if (capture.Validation?.IsRejectedWithoutSpawn(capture.SpawnObserved) == true)
                {
                    reason = $"placement-validation-rejected, {capture.Describe(expected, placementReturn)}";
                    return StorageCreateResult.RejectedByValidation;
                }
                reason = "unconfirmed-storage-spawn";
                if (capture.Validation != null) reason += ", " + capture.Describe(expected, placementReturn);
                return StorageCreateResult.Unconfirmed;
            }
            eStructs type = placement.Mapper.ConvertToEStructs();
            var ids = new HashSet<int>();
            foreach (Identity identity in capture.Identities)
            {
                if (!ids.Add(identity.Id) || !global::APIShared.BuildingAccess.TryGetById(identity.Id, out GameBuilding* b, out _) ||
                    b == null || b->r_GlobalId != identity.GlobalId || !IsPresent(b->r_AliveState) ||
                    b->r_PlayerIdOwner != playerId || b->r_BuildingType != type ||
                    b->r_TilePositionXBegin < placement.X || b->r_TilePositionXBegin >= placement.X + placement.Size ||
                    b->r_TilePositionYBegin < placement.Y || b->r_TilePositionYBegin >= placement.Y + placement.Size)
                {
                    reason = "changed-storage-spawn-identity";
                    if (capture.Validation != null) reason += ", " + capture.Describe(expected, placementReturn) +
                        $", changedSpawnId={identity.Id}";
                    return StorageCreateResult.Unconfirmed;
                }
                Remember(identity.Id, ref *b);
            }
            Identity last = capture.Identities[capture.Identities.Count - 1];
            OnCreated(placement, new StorageCreatedSection(capture.Anchor.Id, last.Id, last.GlobalId));
            return StorageCreateResult.Created;
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
            internal Capture(StoragePlacement placement, ValidationObservation validation)
            { Placement = placement; Validation = validation; }
            internal readonly StoragePlacement Placement;
            internal readonly ValidationObservation Validation;
            internal readonly List<Identity> Identities = new List<Identity>();
            internal Identity Anchor;
            internal int Events;
            internal bool SpawnObserved;
            internal string FirstRejectedSpawn { get; private set; }

            internal string Describe(int expected, long placementReturn) =>
                $"{Validation.Describe()}, placementReturn={placementReturn}, spawnObserved={SpawnObserved}, " +
                $"events={Events}/{expected}, valid={Identities.Count}/{expected}, " +
                $"anchor={(Anchor.Id > 0 ? "present" : "missing")}, firstRejectedSpawn={FirstRejectedSpawn ?? "none"}";

            internal void RejectSpawn(string reason)
            {
                if (Validation != null && FirstRejectedSpawn == null) FirstRejectedSpawn = reason;
            }
        }

        internal sealed class ValidationObservation
        {
            private readonly int playerId;
            private readonly StoragePlacement placement;
            private readonly Func<(int State, int Reason)?> read;
            private int depth;
            private int calls;
            private bool customValidation;
            private BuildingPlacementValidationEventArgs pre;
            private int? reasonBefore;
            private (int State, int Reason)? result;

            internal ValidationObservation(int playerId, StoragePlacement placement, Func<(int State, int Reason)?> read)
            { this.playerId = playerId; this.placement = placement; this.read = read; }

            internal void Observe(BuildingPlacementValidationEventArgs args)
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    calls++;
                    depth++;
                    if (depth != 1 || !MatchesAttempt(args)) return;
                    pre = args;
                    reasonBefore = Read()?.Reason;
                    return;
                }
                if (args.Phase != EventHookPhase.Post || depth == 0) return;
                if (--depth != 0 || pre == null) return;
                // Post uses the original arguments. The mutable Pre object still holds
                // the arguments that subscribers supplied to the native validator.
                if (MatchesAttempt(args) && MatchesAttempt(pre))
                {
                    result = Read();
                    customValidation = pre.CustomValidationRules;
                }
                pre = null;
            }

            private bool MatchesAttempt(BuildingPlacementValidationEventArgs args) =>
                args.PlayerId == playerId && args.Mappers == placement.Mapper &&
                args.TileX == placement.X && args.TileY == placement.Y &&
                args.Unknown1 == placement.Size && args.Unknown2 == 0;

            private (int State, int Reason)? Read()
            {
                try { return read(); }
                catch (Exception) { return null; } // Support diagnostics must not interrupt placement.
            }

            private bool HasResult => depth == 0 && calls == 1 && result.HasValue;

            internal bool IsRejectedWithoutSpawn(bool spawnObserved) =>
                !spawnObserved && HasResult && !customValidation && result.Value.State == PlacementBlocked;

            internal string Describe()
            {
                if (!HasResult) return "validation=not-observed";
                var (state, reason) = result.Value;
                if (state == 0) return "validation=allowed";
                string before = reasonBefore.HasValue ? $"0x{reasonBefore.Value:X}" : "unknown";
                string suffix = !reasonBefore.HasValue || reason == reasonBefore.Value ? " (possibly stale)" : string.Empty;
                string hint = placement.Mapper == eMappers.MAPPER_STORES && state == PlacementBlocked &&
                    reason == StoresNeedsLiveNeighbor && suffix.Length == 0 && !customValidation
                    ? ", nativeReasonHint=no-shared-edge-with-live-stockpile" : string.Empty;
                return $"validation=blocked, blockState={state}, nativeReasonBefore={before}, " +
                    $"nativeReason=0x{reason:X}{suffix}, customValidation={customValidation}{hint}";
            }
        }
    }
}
