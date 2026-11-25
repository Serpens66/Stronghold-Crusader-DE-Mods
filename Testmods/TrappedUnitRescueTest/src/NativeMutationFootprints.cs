using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared.WorldMutations;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal readonly struct RescueFootprintMetrics
    {
        internal readonly int NativeReads, BuildingSlots, BrushSearches, BrushCacheHits, BrushNodes;
        internal RescueFootprintMetrics(int reads, int buildings, int searches, int hits, int nodes)
        { NativeReads = reads; BuildingSlots = buildings; BrushSearches = searches; BrushCacheHits = hits; BrushNodes = nodes; }
    }
    internal interface IRescueFootprintMemory
    {
        int Int32(int rva);
        short Int16(int rva);
        byte Byte(int rva);
    }

    // Read-only, invocation-local adapter. The NativeRelocation factory owns canonical image admission.
    // No native enumerator, editor function or building-removal function is invoked here.
    internal sealed unsafe class NativeMutationFootprints
    {
        internal const int TileManagerRva = 0x405EDB0, BuildingManagerRva = 0x64CCBB0;
        private readonly IRescueFootprintMemory memory;
        private readonly RescueFootprintBudget budget;
        private readonly long imageBase;
        private readonly int maximumBuildingReads;
        internal NativeMutationFootprints(NativeRelocation native, RescueFootprintBudget budget, int maximumBuildingReads = 4096)
            : this(new LiveMemory(native == null ? IntPtr.Zero : native.ImageBase), budget,
                  native == null ? 0 : native.ImageBase.ToInt64(), maximumBuildingReads)
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionXBegin)).ToInt32() != 0xEE ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OccupyTileGridSize)).ToInt32() != 0xF8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OccupiedTileIdsArrayBegin)).ToInt32() != 0x1C8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000178A)).ToInt32() != 0x2A8)
                throw new InvalidOperationException("Footprint building layout differs.");
        }
        // Reader seam for offline behavioral fixtures; never installs or invokes native code.
        internal NativeMutationFootprints(IRescueFootprintMemory memory, RescueFootprintBudget budget, long imageBase = 0, int maximumBuildingReads = 4096)
        {
            if (memory == null || budget.MaximumTiles < 1 || budget.MaximumWork < 1 || maximumBuildingReads < 1)
                throw new ArgumentException("Invalid native footprint context.");
            this.memory = memory; this.budget = budget; this.imageBase = imageBase; this.maximumBuildingReads = maximumBuildingReads;
        }

        internal bool TryPlan(WorldMutationContext source, out List<RescueTile> tiles, out int owner, out string reason)
        {
            tiles = new List<RescueTile>(); owner = 0; reason = null;
            if (source == null) { reason = "missing-mutation-context"; return false; }
            try
            {
                if (source.Source == WorldMutationSource.MoatDig || source.Source == WorldMutationSource.MoatFill)
                {
                    if (source.NativeContextAddress != unchecked((ulong)(imageBase + TileManagerRva))) throw new InvalidOperationException("mutation-manager-address-mismatch");
                    int slot = source.TaskSlot, limit = memory.Int32(TileManagerRva + 0x2038E30);
                    if (slot <= 0 || slot >= 64000 || limit < 1 || limit > 64000 || slot >= limit) throw new InvalidOperationException("moat-task-slot-contract");
                    int task = TileManagerRva + 0x1F3EE30 + slot * 16;
                    owner = memory.Byte(task + 12);
                    if (owner < 1 || owner > 8) throw new InvalidOperationException("moat-task-owner-contract");
                    // Constant-cost progress filter before allocating a footprint session or reading terrain.
                    if (!RescueCleanupProjection.MoatCanChangeGeometry(source.Source == WorldMutationSource.MoatFill, memory.Byte(task + 13))) return true;
                }
                var session = new Session(memory, budget, maximumBuildingReads);
                var requests = new List<RescueMutationFootprintRequest>();
                if (source.Source == WorldMutationSource.AivBuildStep)
                {
                    if (!Aiv(source, session, requests, out owner, out reason)) return false;
                }
                else
                {
                    long expected = imageBase + (source.Source == WorldMutationSource.BuildingRemoval ? BuildingManagerRva : TileManagerRva);
                    if (source.NativeContextAddress != unchecked((ulong)expected)) { reason = "mutation-manager-address-mismatch"; return false; }
                    owner = source.PlayerId;
                    switch (source.Source)
                    {
                        case WorldMutationSource.BuildStructure:
                            AddBuilding(requests, session, source.X, source.Y, (eMappers)source.Mapper, source.Size, source.Direction, false); break;
                        case WorldMutationSource.HumanWall:
                            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.HumanWallLine, X = source.X, Y = source.Y, EndX = source.EndX, EndY = source.EndY }); break;
                        case WorldMutationSource.AiWall:
                            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.AiWallTile, X = source.X, Y = source.Y }); break;
                        case WorldMutationSource.WallRemoval:
                            var wall = session.Decode(source.TileId);
                            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.AiWallTile, X = wall.X, Y = wall.Y }); break;
                        case WorldMutationSource.MoatDig:
                        case WorldMutationSource.MoatFill:
                            int slot = source.TaskSlot;
                            int limit = session.Int32(TileManagerRva + 0x2038E30);
                            if (slot <= 0 || slot >= 64000 || limit < 1 || limit > 64000 || slot >= limit) throw new InvalidOperationException("moat-task-slot-contract");
                            int task = TileManagerRva + 0x1F3EE30 + slot * 16;
                            owner = session.Byte(task + 12);
                            if (owner < 1 || owner > 8) throw new InvalidOperationException("moat-task-owner-contract");
                            if (!RescueCleanupProjection.MoatCanChangeGeometry(source.Source == WorldMutationSource.MoatFill, session.Byte(task + 13))) return true;
                            int x = session.Int16(task + 4), y = session.Int16(task + 6), tile = session.Int32(task);
                            if (owner < 1 || owner > 8 || session.ActiveTile(x, y) != tile) throw new InvalidOperationException("moat-task-identity-contract");
                            requests.Add(new RescueMutationFootprintRequest { Source = source.Source == WorldMutationSource.MoatDig ? RescueMutationSource.MoatDig : RescueMutationSource.MoatFill, X = x, Y = y }); break;
                        case WorldMutationSource.BuildingRemoval:
                            var removed = session.Building(source.ObjectId);
                            owner = removed.Owner;
                            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.Demolition, X = removed.X, Y = removed.Y, ReplacedBuildingId = removed.Id }); break;
                        case WorldMutationSource.EditorDelete:
                            reason = "intentional-unit-deletion"; return false;
                        case WorldMutationSource.EditorTileType:
                        case WorldMutationSource.EditorTileTypeClear:
                        case WorldMutationSource.EditorHeightPreset:
                        case WorldMutationSource.EditorHeightRaise:
                        case WorldMutationSource.EditorHeightNumber:
                        case WorldMutationSource.EditorHeightEven:
                            var center = session.Decode(source.TileId);
                            int expectedY = checked((int)source.GetArgument(source.Source == WorldMutationSource.EditorTileType ? 3 : 2));
                            if (center.Y != expectedY) throw new InvalidOperationException("editor-center-y-contract");
                            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.EditorBrush, X = center.X, Y = center.Y,
                                BrushWidth = checked((int)source.GetArgument(source.Source == WorldMutationSource.EditorTileType ? 4 : 3)),
                                CenteredSquareBrush = source.Source == WorldMutationSource.EditorTileType && unchecked((uint)source.GetArgument(5)) == 0x40000000U }); break;
                        default: reason = "unsupported-mutation-source"; return false;
                    }
                }
                if (owner < 0 || owner > 8) { reason = "mutation-owner-contract"; return false; }
                return PlanRequests(session, requests, out tiles, out reason);
            }
            catch (Exception ex) { reason = ex is InvalidOperationException ? ex.Message : "native-footprint-reader-failed"; return false; }
        }
        private bool PlanRequests(Session session, List<RescueMutationFootprintRequest> requests, out List<RescueTile> tiles, out string reason)
        {
                tiles = new List<RescueTile>(); reason = null;
                var union = new Dictionary<int, RescueTile>();
                foreach (var request in requests)
                {
                    if (!RescueMutationFootprints.TryPlan(request, session.Context, budget, out var part, out reason)) return false;
                    foreach (var tile in part)
                    {
                        if (union.TryGetValue(tile.TileId, out var previous) && (previous.X != tile.X || previous.Y != tile.Y))
                            throw new InvalidOperationException("footprint-coordinate-alias");
                        union[tile.TileId] = tile;
                        if (union.Count > budget.MaximumTiles) throw new InvalidOperationException("footprint-tile-budget");
                    }
                }
                tiles.AddRange(union.Values); tiles.Sort((a, b) => a.TileId.CompareTo(b.TileId)); return true;
        }

        private static bool IsKeep(eStructs type) => type >= eStructs.STRUCT_KEEP_ONE && type <= eStructs.STRUCT_KEEP_FIVE;
        private static void AddBuilding(List<RescueMutationFootprintRequest> requests, Session session, int x, int y, eMappers mapper, int size, int direction, bool cleanup)
        {
            if (!Enum.IsDefined(typeof(eMappers), mapper)) throw new InvalidOperationException("unknown-effective-mapper");
            // Installed SDK maps legacy KEEP4/5 despite C77C0's audited default-zero entries.
            // Their native scale is-1; never manufacture a supported keep from these aliases.
            eStructs type = mapper == eMappers.MAPPER_KEEP4 || mapper == eMappers.MAPPER_KEEP5 ? eStructs.STRUCT_NULL : mapper.ConvertToEStructs();
            if (type < eStructs.STRUCT_HOVEL || type > eStructs.STRUCT_DOCK) throw new InvalidOperationException("unsupported-effective-native-mapper");
            // 77E60 runs after the common boundary and can determine the later replacement path.
            // Capture its possible5D3A0 effects even when the current scratch flag is still zero.
            if (IsKeep(type))
                session.KeepReplacement(requests, x, y, type, size);
            // Lower Fixes/Planner wrappers may replace a keep direction after the common broker.
            int orientations = IsKeep(type) ? 4 : 1;
            for (int orientation = 0; orientation < orientations; orientation++)
                requests.Add(new RescueMutationFootprintRequest { Source = cleanup ? RescueMutationSource.AivBuilding : RescueMutationSource.Building,
                    Structure = type, Mapper = mapper, X = x, Y = y, Size = size, Direction = IsKeep(type) ? orientation * 2 : direction,
                    CleanupPreservesBrushFlags = cleanup });
        }

        private bool Aiv(WorldMutationContext source, Session session, List<RescueMutationFootprintRequest> requests, out int owner, out string reason)
        {
            owner = source.PlayerId; reason = null;
            var api = GameAIVManagerAPI.Instance;
            if (owner < 1 || owner > 8 || source.NativeContextAddress != (ulong)api.GetAIVSystemPointer() ||
                !api.TryGetVillageSlotByPlayerId(owner, out int villageSlot) || !api.TryGetVillageBySlot(villageSlot, out var village) ||
                village->OwnerPlayerId != owner || !api.TryGetBuildStep(villageSlot, source.FrameIndex, out var step))
            { reason = "aiv-frame-identity-contract"; return false; }
            eMappers mapper = step->BuildingType;
            var fields = api.GetBuildStepMapTiles(villageSlot, source.FrameIndex);
            if (fields.Length > budget.MaximumTiles) { reason = "aiv-field-budget"; return false; }
            if (fields.Length == 0)
            {
                if (GameAIVManagerAPI.UsesOrderedMapTileBuffer(mapper) && step->TileCount == 0) return true;
                reason = "aiv-field-reference-contract"; return false;
            }
            int direction = (int)village->Rotation;
            var copiedFields = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++) copiedFields[i] = fields[i];
            BuildAivRequests(session, requests, mapper, direction, copiedFields);
            return true;
        }
        private void BuildAivRequests(Session session, List<RescueMutationFootprintRequest> requests, eMappers mapper, int direction, int[] fields)
        {
            if (fields == null) throw new InvalidOperationException("aiv-field-reference-contract");
            if (fields.Length > budget.MaximumTiles) throw new InvalidOperationException("aiv-field-budget");
            // Native non-square gate selectors and drawbridge validation are shape-specific.
            // The shape planner does not use those selectors; cardinal compound anchors do.
            bool ordered = GameAIVManagerAPI.UsesOrderedMapTileBuffer(mapper);
            bool singleWall = mapper >= eMappers.MAPPER_STAIR1 && mapper <= eMappers.MAPPER_STAIR6;
            for (int i = 0; i < fields.Length; i++)
            {
                var tile = session.Decode(fields[i]);
                if (ordered || singleWall)
                {
                    if (mapper == eMappers.MAPPER_MOAT)
                    {
                        // 59730's low-ground moat and high-ground scale1 KillingPit alternative
                        // both mutate this tile; include ordinary neighbour refreshes as well.
                        requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.MoatDig, X = tile.X, Y = tile.Y });
                        continue;
                    }
                    // Wall cleanup precedes each ordinary tile writer. Pitch uses a one-tile creator.
                    requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.AivBuilding,
                        Structure = mapper == eMappers.MAPPER_PITCH_DITCH ? eStructs.STRUCT_PITCH_DITCH : eStructs.STRUCT_HOVEL,
                        Mapper = mapper, X = tile.X, Y = tile.Y, Size = 1, Direction = 0, CleanupPreservesBrushFlags = true });
                }
                else
                {
                    int size = BuildingScales.GetScale(mapper);
                    AddBuilding(requests, session, tile.X, tile.Y, mapper, size, direction, true);
                    // 51790 separately cleans the three barracks children, guild child or oil child.
                    int orientation = direction == 15 ? 0 : direction / 2;
                    if (orientation < 0 || orientation > 3) throw new InvalidOperationException("aiv-direction-contract");
                    if (mapper == eMappers.MAPPER_BARRACKS_WOOD || mapper == eMappers.MAPPER_BARRACKS_STONE || mapper == eMappers.MAPPER_BEDOUIN_STOCKADE)
                        for (int child = 0; child < 3; child++) AddCleanupOnly(requests, session, tile, size, 0x2D2E90 + orientation * 24 + child * 8);
                    else if (mapper == eMappers.MAPPER_ENGINEERS_GUILD || mapper == eMappers.MAPPER_TUNNELERS_GUILD)
                        AddCleanupOnly(requests, session, tile, size, 0x2D2E98 + orientation * 24);
                    else if (mapper == eMappers.MAPPER_OIL_SMELTER)
                        AddCleanupOnly(requests, session, tile, size, 0x2D2EF0 + orientation * 8);
                }
            }
        }
        private static void AddCleanupOnly(List<RescueMutationFootprintRequest> requests, Session session, RescueTile anchor, int size, int rva)
        {
            requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.AivBuilding, Structure = eStructs.STRUCT_HOVEL,
                X = checked(anchor.X + session.Int32(rva)), Y = checked(anchor.Y + session.Int32(rva + 4)), Size = size, Direction = 0, CleanupPreservesBrushFlags = true });
        }

        internal RescueFootprintContext ContextForOfflineFixture() => new Session(memory, budget, maximumBuildingReads).Context;
        internal bool TryPlanAivFieldsForOfflineFixture(eMappers mapper, int direction, int[] fields,
            out List<RescueTile> tiles, out RescueFootprintMetrics metrics, out string reason)
        {
            tiles = new List<RescueTile>(); metrics = default; reason = null;
            var session = new Session(memory, budget, maximumBuildingReads);
            try
            {
                var requests = new List<RescueMutationFootprintRequest>();
                BuildAivRequests(session, requests, mapper, direction, fields);
                return PlanRequests(session, requests, out tiles, out reason);
            }
            catch (Exception ex) { reason = ex is InvalidOperationException ? ex.Message : "native-footprint-reader-failed"; return false; }
            finally { metrics = new RescueFootprintMetrics(session.Reads, session.BuildingReads, session.Context.BrushSearches, session.Context.BrushCacheHits, session.Context.BrushNodes); }
        }
        private sealed class LiveMemory : IRescueFootprintMemory
        {
            private readonly byte* image;
            internal LiveMemory(IntPtr address) { if (address == IntPtr.Zero) throw new ArgumentNullException(nameof(address)); image = (byte*)address; }
            public int Int32(int rva) => *(int*)(image + rva);
            public short Int16(int rva) => *(short*)(image + rva);
            public byte Byte(int rva) => image[rva];
        }
        private sealed class BuildingRecord
        {
            internal int Id, Owner, X, Y, Size, Alive;
            internal uint Global, Compound;
            internal eStructs Type;
            internal int[] Plots;
            internal int[] FarmLookupPlots;
        }
        private sealed class Session
        {
            private readonly IRescueFootprintMemory memory;
            private readonly RescueFootprintBudget budget;
            private readonly int maximumBuildings;
            private int reads, buildingsRead;
            private Dictionary<int, BuildingRecord> index;
            private readonly Dictionary<int, int> dwords = new Dictionary<int, int>();
            private readonly Dictionary<int, short> words = new Dictionary<int, short>();
            private readonly Dictionary<int, byte> bytes = new Dictionary<int, byte>();
            private int indexedLimit;
            private RescueFootprintOffset[] cleanupSpill;
            internal readonly RescueFootprintContext Context;
            internal int Reads => reads;
            internal int BuildingReads => buildingsRead;
            internal Session(IRescueFootprintMemory memory, RescueFootprintBudget budget, int maximumBuildings)
            {
                this.memory = memory; this.budget = budget; this.maximumBuildings = maximumBuildings;
                Context = new RescueFootprintContext { TileAt = ActiveTile, RawTileAt = RawTile, Flags = tile => RescueCleanupProjection.ExpansionBits(Flags(tile)),
                    Neighbor = Neighbor, TableInt = Table, SquareOffset = SquareOffset, CompleteRemovalFootprint = Removal, CompleteCleanupFootprint = Cleanup,
                    BrushCache = new Dictionary<RescueBrushOrigin, List<RescueTile>>() };
            }
            private void Spend() { if (++reads > budget.MaximumWork) throw new InvalidOperationException("native-footprint-read-budget"); }
            internal int Int32(int rva) { if (dwords.TryGetValue(rva, out int value)) return value; Spend(); value = memory.Int32(rva); dwords.Add(rva, value); return value; }
            internal short Int16(int rva) { if (words.TryGetValue(rva, out short value)) return value; Spend(); value = memory.Int16(rva); words.Add(rva, value); return value; }
            internal byte Byte(int rva) { if (bytes.TryGetValue(rva, out byte value)) return value; Spend(); value = memory.Byte(rva); bytes.Add(rva, value); return value; }
            internal int RawTile(int x, int y)
            {
                if ((uint)x >= 800 || (uint)y >= 800) return -1;
                long id = (long)Int32(0x402FF2C + y * 12) + x;
                return id >= 0 && id < budget.TileCapacity ? (int)id : -1;
            }
            internal int ActiveTile(int x, int y) => (uint)x < 800 && (uint)y < 800 && Byte(0x3A11EA4 + y * 800 + x) != 0 ? RawTile(x, y) : -1;
            internal RescueTile Decode(int tile)
            {
                if ((uint)tile >= budget.TileCapacity) throw new InvalidOperationException("footprint-packed-tile-contract");
                int y = Int16(0x3AAE2A4 + tile * 2);
                if ((uint)y >= 800) throw new InvalidOperationException("footprint-packed-y-contract");
                int x = checked(tile - Int32(0x402FF2C + y * 12));
                if (RawTile(x, y) != tile) throw new InvalidOperationException("footprint-packed-xy-contract");
                return new RescueTile(x, y, tile);
            }
            private uint Flags(int tile)
            { if ((uint)tile >= budget.TileCapacity) throw new InvalidOperationException("footprint-flags-tile-contract"); return unchecked((uint)Int32(0x48F71B0 + tile * 4)); }
            private bool Neighbor(RescueTile current, int direction, out RescueTile next)
            {
                next = default;
                if ((uint)current.Y >= 800 || direction < 0 || direction > 6 || (direction & 1) != 0 || RawTile(current.X, current.Y) != current.TileId) return false;
                int tile = checked(current.TileId + Int32(TileManagerRva + current.Y * 32 + direction * 4));
                int y = checked(current.Y + Int32(0x2D2E54 + direction * 8));
                if ((uint)tile >= budget.TileCapacity || (uint)y >= 800) return false;
                int x = checked(tile - Int32(0x402FF2C + y * 12));
                if (RawTile(x, y) != tile) return false;
                next = new RescueTile(x, y, tile); return true;
            }
            private int Table(int rva)
            {
                if ((rva & 3) != 0 || !(rva >= 0x2D1C38 && rva < 0x2D1C60 || rva >= 0x2D2E90 && rva < 0x2D4310 || rva >= 0x2E3FA0 && rva < 0x2E4B00))
                    throw new InvalidOperationException("footprint-table-rva-contract");
                return Int32(rva);
            }
            private RescueFootprintOffset SquareOffset(int size, int ordinal)
            {
                if (size < 1 || size > 14 || ordinal < 0 || ordinal >= 545 || size == 14 && ordinal >= 196) throw new InvalidOperationException("footprint-offset-table-contract");
                if (size == 14)
                {
                    // 61DB0 initializes only0..13. Size14 aliases mutable adjacent globals;
                    // capture its actual196 native records once in this qualified invocation.
                    if (cleanupSpill == null)
                    {
                        var captured = new RescueFootprintOffset[196];
                        for (int i = 0; i < captured.Length; i++)
                        { int at = 0x8DDCC0 + (14 * 169 + i) * 24; captured[i] = new RescueFootprintOffset(Int32(at), Int32(at + 4)); }
                        cleanupSpill = captured;
                    }
                    return cleanupSpill[ordinal];
                }
                int rva = checked(0x8DDCC0 + (size * 169 + ordinal) * 24);
                return new RescueFootprintOffset(Int32(rva), Int32(rva + 4));
            }
            internal BuildingRecord Building(int id)
            {
                if (id < 1 || id >= 4000) throw new InvalidOperationException("footprint-building-id-contract");
                if (index != null && index.TryGetValue(id, out var cached)) return cached;
                if (++buildingsRead > maximumBuildings) throw new InvalidOperationException("footprint-building-read-budget");
                int record = 0x64CCC0C + id * 0x32C;
                int alive = Int16(record + 0xD0);
                if (alive == 0) throw new InvalidOperationException("footprint-building-not-present");
                var b = new BuildingRecord { Id = id, Alive = alive, Type = (eStructs)Int16(record + 0xD2), Owner = Int16(record + 0xD6),
                    Global = unchecked((uint)Int32(record + 0xD8)), X = Int16(record + 0xEE), Y = Int16(record + 0xF0), Size = Int32(record + 0xF8), Compound = unchecked((uint)Int32(record + 0x2A8)) };
                if (b.Global == 0 || b.Size < 1 || b.Size > 13 || b.Owner < 0 || b.Owner > 8 || ActiveTile(b.X, b.Y) < 0)
                    throw new InvalidOperationException("footprint-building-record-contract");
                int count = PlotCount(b.Type);
                if (count != 0)
                {
                    b.Plots = new int[count]; b.FarmLookupPlots = new int[36];
                    // B8ED0 compares all36 entries for every farm type, including unused tail entries.
                    for (int i = 0; i < 36; i++) { int plot = Int32(record + 0x1C8 + i * 4); b.FarmLookupPlots[i] = plot; if (i < count) b.Plots[i] = plot; }
                }
                return b;
            }
            private void EnsureIndex(bool fullCapacity = false)
            {
                if (index != null && (!fullCapacity || indexedLimit == 4000)) return;
                var records = index ?? new Dictionary<int, BuildingRecord>();
                int limit = Int32(BuildingManagerRva + 0x50);
                if (limit < 1 || limit > 4000) throw new InvalidOperationException("footprint-building-limit-contract");
                if (fullCapacity) limit = 4000;
                for (int id = Math.Max(1, indexedLimit); id < limit; id++)
                {
                    if (++buildingsRead > maximumBuildings) throw new InvalidOperationException("footprint-building-read-budget");
                    if (Int16(0x64CCC0C + id * 0x32C + 0xD0) == 0) continue;
                    buildingsRead--; records.Add(id, Building(id));
                }
                index = records; indexedLimit = limit;
            }
            internal void KeepReplacement(List<RescueMutationFootprintRequest> requests, int x, int y, eStructs type, int size)
            {
                // 5D3A0 uses its own default-direction table entries, not the new keep's direction.
                var probes = new Dictionary<int, RescueTile>(); Square(probes, x, y, size);
                int typeIndex = (int)type - (int)eStructs.STRUCT_KEEP_ONE;
                for (int i = 0; i < 3; i++) Add(probes, checked(x + Table(0x2D30F0 + typeIndex * 96 + i * 8)), checked(y + Table(0x2D30F4 + typeIndex * 96 + i * 8)));
                Square(probes, checked(x + Table(0x2D3210 + typeIndex * 32)), checked(y + Table(0x2D3214 + typeIndex * 32)), 7);
                Square(probes, checked(x + Table(0x2D3270 + typeIndex * 32)), checked(y + Table(0x2D3274 + typeIndex * 32)), 5);
                var selected = new HashSet<int>();
                foreach (var tile in probes.Values)
                { int id = Int16(0x4B6AA50 + tile.TileId * 2); if (id != 0) selected.Add(id); }
                // Unlike C43A0's high-water loop, the final5D3A0 pass checks all3999 slots.
                EnsureIndex(true);
                foreach (var b in index.Values) if (b.Alive == 3) selected.Add(b.Id);
                foreach (int id in selected)
                {
                    var b = Building(id);
                    requests.Add(new RescueMutationFootprintRequest { Source = RescueMutationSource.Demolition, X = b.X, Y = b.Y, ReplacedBuildingId = id });
                    if (requests.Count > budget.MaximumTiles) throw new InvalidOperationException("footprint-replacement-operation-budget");
                }
            }
            private static int PlotCount(eStructs type) => type == eStructs.STRUCT_WHEATFARM ? 36 : type == eStructs.STRUCT_HOPSFARM ? 24 : type == eStructs.STRUCT_APPLEFARM ? 8 : type == eStructs.STRUCT_CATTLEFARM ? 27 : 0;
            private void Add(Dictionary<int, RescueTile> tiles, int x, int y)
            {
                int tile = ActiveTile(x, y); if (tile < 0) return;
                tiles[tile] = new RescueTile(x, y, tile);
                if (tiles.Count > budget.MaximumTiles) throw new InvalidOperationException("footprint-removal-tile-budget");
            }
            private void Square(Dictionary<int, RescueTile> tiles, int x, int y, int size, bool nativeCleanupSpill = false)
            {
                if (size < 1 || size > (nativeCleanupSpill ? 14 : 13) || nativeCleanupSpill && size != 14) throw new InvalidOperationException("footprint-removal-size-contract");
                var seen = new HashSet<long>();
                for (int i = 0; i < size * size; i++)
                {
                    var p = SquareOffset(size, i);
                    if (!nativeCleanupSpill && ((uint)p.X >= size || (uint)p.Y >= size || !seen.Add(((long)p.X << 32) | (uint)p.Y))) throw new InvalidOperationException("footprint-square-contract");
                    if (nativeCleanupSpill && ((long)x + p.X < 0 || (long)x + p.X >= 800 || (long)y + p.Y < 0 || (long)y + p.Y >= 800)) throw new InvalidOperationException("footprint-native-spill-coordinate-contract");
                    Add(tiles, checked(x + p.X), checked(y + p.Y));
                }
            }
            private void Geometry(Dictionary<int, RescueTile> tiles, BuildingRecord b)
            {
                Square(tiles, b.X, b.Y, b.Size);
                if (b.Plots != null)
                {
                    foreach (int tile in b.Plots) { var p = Decode(tile); Add(tiles, p.X, p.Y); }
                    Square(tiles, b.X, b.Y, b.Type == eStructs.STRUCT_WHEATFARM || b.Type == eStructs.STRUCT_HOPSFARM ? 9 : b.Type == eStructs.STRUCT_APPLEFARM ? 11 : 10);
                }
                if (b.Type == eStructs.STRUCT_GOODS_YARD)
                    for (int i = 0; i < 9; i++) Add(tiles, checked(b.X + Table(0x2D3060 + i * 8)), checked(b.Y + Table(0x2D3064 + i * 8)));
            }
            private void AddClosure(Dictionary<int, RescueTile> tiles, BuildingRecord b)
            {
                Geometry(tiles, b);
                if (b.Compound == 0) return;
                EnsureIndex();
                foreach (var linked in index.Values) if (linked.Compound == b.Compound && linked.Id != b.Id) Geometry(tiles, linked);
            }
            private IList<RescueTile> Removal(int id)
            { var tiles = new Dictionary<int, RescueTile>(); AddClosure(tiles, Building(id)); return new List<RescueTile>(tiles.Values); }
            private IList<RescueTile> Cleanup(int x, int y, int size)
            {
                var footprint = new Dictionary<int, RescueTile>(); Square(footprint, x, y, size);
                if (size <= 11) { int halo = size == 11 ? 1 : 2; int side = size == 11 ? 13 : size + 4; Square(footprint, checked(x - halo), checked(y - halo), side, side == 14); }
                var affected = new HashSet<int>();
                foreach (var tile in footprint.Values)
                {
                    int building = Int16(0x4B6AA50 + tile.TileId * 2);
                    if (building != 0) { affected.Add(building); continue; }
                    uint flags = Flags(tile.TileId);
                    if ((flags & 0x04000004U) == 0) continue;
                    EnsureIndex();
                    foreach (var candidate in index.Values)
                    {
                        // B8ED0/B8E00 consider Alive2 farms only; include every matching one.
                        if (candidate.Alive != 2 || candidate.Plots == null) continue;
                        if ((flags & 0x04000000U) != 0 && Array.IndexOf(candidate.FarmLookupPlots, tile.TileId) >= 0) affected.Add(candidate.Id);
                        int prep = candidate.Type == eStructs.STRUCT_WHEATFARM || candidate.Type == eStructs.STRUCT_HOPSFARM ? 9 : 10;
                        if ((flags & 4) != 0 && tile.X >= candidate.X && tile.X < candidate.X + prep && tile.Y >= candidate.Y && tile.Y < candidate.Y + prep) affected.Add(candidate.Id);
                        Spend();
                    }
                }
                foreach (int id in affected) AddClosure(footprint, Building(id));
                return new List<RescueTile>(footprint.Values);
            }
        }
    }
}
