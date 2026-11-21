using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using APIShared.Buildings;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed unsafe partial class NativePlacement
    {
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int Mapper = 0x60AD45C, CursorState = 0x60AD4B0, CursorScale = 0x60AD460;
        private const int PlacementDisabled = 0x366A0F8, PlacementState = 0x60AD4AC;
        private const int ValidMapMask = 0x3A11EA4, Axis = 800;
        private const int PreviewOverride = 0x60AD59C;
        private const int PreviewTokens = 0x4411FB0, PreviewFlags = 0x4F64CF0;
        private const int Queue = 0x8574320, QueueEntries = 0xB0BF8, QueueStride = 0x500, QueueCapacity = 500;
        private const int WorldKind = 0x3665F28, FreeBuild = 0x3665FC8;
        private const int FreeBuildingSlots = 0x64CCC04, PlayerBuildings = 0x64CCBB4;
        private const int NetworkMode = 0x8574B90, ParticipantSlots = 0x8574BCC, ParticipantFlags = 0x8574C44;
        private const int OutboundCursor = 0xA98690, OutboundCapacity = 1_000_000, BuildRecordBytes = 23;
        private const int FirstHovelFrame = 0x2E90A8;
        private const byte BuildStructure = 28;
        private const int GoodsCount = 5;
        internal const int MaximumFootprint = 13;
        private static readonly eGoods[] Goods =
        {
            eGoods.STORED_WOOD_PLANKS, eGoods.STORED_STONE_BLOCKS, eGoods.STORED_IRON_INGOTS,
            eGoods.STORED_PITCH_RAW, eGoods.STORED_GOLD
        };

        private readonly IntPtr image;
        private readonly StorageNeighborProbe storageNeighbor;
        private readonly StorageEmptyLimitPatch storageLimitPatch;
        private readonly List<Footprint> pending = new List<Footprint>();
        private readonly HashSet<Tile> pendingCurrent = new HashSet<Tile>();
        private readonly long[] resources = new long[GoodsCount];
        private readonly int[] cost = new int[GoodsCount];
        private readonly int[] tileIds = new int[MaximumFootprint * MaximumFootprint];
        private readonly int[] previewTokens = new int[MaximumFootprint * MaximumFootprint];
        private readonly short[] previewFlags = new short[MaximumFootprint * MaximumFootprint];
        private readonly int[] otherCost = new int[GoodsCount];
        // Scope the synthetic hover's placement and map-action presentation
        // fields. Path search scratch uses its own generations and stays live.
        private readonly byte[] placementScratch = new byte[0x150];
        private readonly byte[] cursorScratch = new byte[16];
        private readonly byte[] mouseScratch = new byte[9];
        private int allowance, storageAllowance, freeSlots, outboundSlots, freeWoodcutters, freeGranaries;

        internal static void RequireSupportedImage(CrusaderLibraryLoadContext context)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero || context.Memory.Length <= Queue + 0x370C00)
                throw new InvalidOperationException("Invalid native image range.");
            string dll = FindDll(context.ModuleHandle);
            using (var hash = SHA256.Create())
            using (var stream = File.OpenRead(dll))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                if (!StringComparer.OrdinalIgnoreCase.Equals(actual, NativeHash))
                    throw new InvalidOperationException("Unsupported CrusaderDE.dll SHA-256 " + actual);
            }
        }

        internal NativePlacement(CrusaderLibraryLoadContext context, ManualLogSource log, StorageEmptyLimitPatch storageLimit)
        {
            image = context.ModuleHandle;
            storageNeighbor = StorageNeighborProbe.TryInstall(context, log);
            storageLimitPatch = storageLimit;
        }

        internal void ValidateStorage() => storageNeighbor?.Recheck();

        internal static bool Supports(eMappers tool) =>
            IsStorage(tool) || tool == eMappers.MAPPER_WOODSMAN || tool == eMappers.MAPPER_HOVEL ||
            tool == eMappers.MAPPER_MILL || tool == eMappers.MAPPER_KILLING_PIT ||
            WorkshopFrame(tool) > 0 || RegularBuildingFrame(tool) > 0;

        internal static bool IsStorage(eMappers tool) =>
            tool == eMappers.MAPPER_STORES || tool == eMappers.MAPPER_GRANARY || tool == eMappers.MAPPER_ARMOURY;

        private static int RecordCount(eMappers tool)
        {
            if (tool == eMappers.MAPPER_STORES)
                return 4;
            // The oil smelter has two parts. A quarry may attach a separate pile.
            return tool == eMappers.MAPPER_QUARRY || tool == eMappers.MAPPER_OIL_SMELTER ? 2 : 1;
        }

        internal static Enums.GM SpriteFile(eMappers tool)
        {
            if (tool == eMappers.MAPPER_KILLING_PIT)
                return Enums.GM.GM_KILLING_PITS;
            if (IsChurch(tool))
                return Enums.GM.GM_CHURCHS;
            if (LandscapeFrame(tool) > 0)
                return Enums.GM.GM_BUILDINGS1;
            return WorkshopFrame(tool) > 0 ? Enums.GM.GM_WORKSHOPS : Enums.GM.GM_BUILDINGS2;
        }

        private static int WorkshopFrame(eMappers tool)
        {
            // Native sprite table first images. The ordinary open-ground preview
            // uses road variant 8, sixteen frames per 4x4 workshop variant.
            const int OpenGround = 8 * 16;
            switch (tool)
            {
                case eMappers.MAPPER_FLETCHER: return 1 + OpenGround;
                case eMappers.MAPPER_BAKER: return 289 + OpenGround;
                case eMappers.MAPPER_POLETURNER: return 577 + OpenGround;
                case eMappers.MAPPER_BREWER: return 865 + OpenGround;
                case eMappers.MAPPER_BLACKSMITH: return 1153 + OpenGround;
                case eMappers.MAPPER_ARMOURER: return 1441 + OpenGround;
                case eMappers.MAPPER_TANNER: return 1729 + OpenGround;
                default: return 0;
            }
        }

        internal bool TryResolveMapper(eMappers menu, out eMappers active)
        {
            active = (eMappers)Read(Mapper);
            return Supports(active) && PlacementVariants.SameSelection(menu, active);
        }
        internal bool SupportsMode(bool editor)
        {
            int kind = Read(WorldKind);
            return editor ? kind == 1 : kind != 1 && kind != 4 && kind != 6;
        }

        internal int Frame(eMappers tool, int actor)
        {
            if (tool == eMappers.MAPPER_WOODSMAN || tool == eMappers.MAPPER_KILLING_PIT)
                return 1;
            if (tool == eMappers.MAPPER_STORES)
                return 26;
            if (tool == eMappers.MAPPER_GRANARY)
                return 278;
            if (tool == eMappers.MAPPER_ARMOURY)
                return 294;
            if (tool == eMappers.MAPPER_MILL)
                return 219;
            if (tool != eMappers.MAPPER_HOVEL)
                return Math.Max(WorkshopFrame(tool), RegularBuildingFrame(tool));
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(actor, out GamePlayerResources* player))
                return 0;
            uint appearance = player->N00003F41;
            return appearance < 7 ? Read(FirstHovelFrame + (int)appearance * sizeof(int)) : 0;
        }

        internal bool Begin(eMappers tool, int actor, bool editor)
        {
            pending.Clear();
            pendingCurrent.Clear();
            allowance = 0;
            freeSlots = outboundSlots = 0;
            if (!SupportsMode(editor) || !TryResolveMapper(tool, out _) || Read(PlacementDisabled) != 0 ||
                (IsStorage(tool) && storageNeighbor?.IsIntact != true) ||
                Read(PreviewOverride) != 0 ||
                unchecked((long)GameGlobalsManager.Instance.ChoreManagerVA) != image.ToInt64() + Queue ||
                !ConstructionCosts.TryGetPlayerMaterialStock(actor, out var stock, out _))
                return false;
            if (tool == eMappers.MAPPER_OIL_SMELTER)
            {
                // The PC hover validates the axis-aligned apron at (0,+4).
                // A queued orientation with another apron would need its own lattice.
                int variation = Read(0x60AD4BC);
                if (variation != 0 && variation != 1 && variation != 15)
                    return false;
            }

            for (int i = 0; i < GoodsCount; i++)
                resources[i] = stock.GetAmount(Goods[i]);
            bool free = editor || Read(FreeBuild) != 0;
            freeWoodcutters = free ? 0 : FreeWoodcutterQuota(actor);
            freeGranaries = free ? 0 : FreeGranaryQuota(actor);
            if (!Quote(tool, actor, free, cost))
                return false;
            storageAllowance = IsStorage(tool) && storageLimitPatch?.IsActive != true
                ? StorageAllowance(tool, actor) : int.MaxValue;

            int ownPending = 0;
            int occupiedSlots = 0;
            for (int slot = 0; slot < QueueCapacity; slot++)
            {
                int entry = Queue + QueueEntries + slot * QueueStride;
                sbyte state = unchecked((sbyte)Marshal.ReadByte(image, entry + 9));
                if (state == 0 || state >= 10)
                {
                    freeSlots++;
                    continue;
                }
                if (Marshal.ReadByte(image, entry + 8) != BuildStructure)
                    continue;
                var pendingTool = (eMappers)Marshal.ReadInt16(image, entry + 21);
                occupiedSlots += RecordCount(pendingTool);
                int scale = Marshal.ReadByte(image, entry + 23);
                if (scale > 0)
                {
                    var corner = new Tile(Marshal.ReadInt16(image, entry + 17), Marshal.ReadInt16(image, entry + 19));
                    pending.Add(new Footprint(corner, scale));
                    if (pendingTool == eMappers.MAPPER_OIL_SMELTER)
                        AddPendingOilPart(corner, scale, Marshal.ReadByte(image, entry + 24));
                }
                if (Read(entry + 4) != actor)
                    continue;
                ownPending += RecordCount(pendingTool);
                if (pendingTool == tool && scale > 0)
                {
                    pendingCurrent.Add(new Tile(Marshal.ReadInt16(image, entry + 17) + scale / 2,
                        Marshal.ReadInt16(image, entry + 19) + scale / 2));
                    if (IsStorage(tool))
                        storageAllowance -= RecordCount(tool);
                }
                if (free)
                    continue;

                if (pendingTool == eMappers.MAPPER_WOODSMAN && freeWoodcutters > 0)
                    freeWoodcutters--;
                else if (pendingTool == eMappers.MAPPER_GRANARY && freeGranaries > 0)
                    freeGranaries--;
                else if (pendingTool == tool)
                    Spend(cost);
                else if (Supports(pendingTool) && Quote(pendingTool, actor, false, otherCost))
                    Spend(otherCost);
                else
                {
                    // Specialized construction can charge or occupy a different
                    // footprint. Allow a fresh quote once it has been executed.
                    return false;
                }
            }

            allowance = Math.Max(0, Read(FreeBuildingSlots) - 19 - occupiedSlots);
            int network = Read(NetworkMode);
            outboundSlots = int.MaxValue;
            if (network != 0)
            {
                if (network != 99)
                {
                    int cursor = Read(OutboundCursor);
                    outboundSlots = cursor >= 4 && cursor <= OutboundCapacity
                        ? (OutboundCapacity - cursor) / BuildRecordBytes : 0;
                }
                int participants = 0;
                for (int id = 1; id <= 8; id++)
                {
                    if (Read(ParticipantSlots + id * sizeof(int)) != -1 ||
                        Marshal.ReadByte(image, ParticipantFlags + id * sizeof(int)) != 0)
                        participants++;
                }
                int quota = 4000 / Math.Max(2, participants);
                allowance = Math.Min(allowance, Math.Max(0, quota - Read(PlayerBuildings + actor * sizeof(int)) - 1 - ownPending));
            }
            return true;
        }

        private static bool Quote(eMappers tool, int actor, bool free, int[] result)
        {
            if (free)
            {
                Array.Clear(result, 0, result.Length);
                return true;
            }

            var target = ConstructionCostTarget.FromMapper(tool);
            if (!ConstructionCosts.TryGetCurrent(target, out var baseCosts, out _))
                return false;
            for (int i = 0; i < GoodsCount; i++)
                result[i] = baseCosts.GetAmount(Goods[i]);
            if (HumanConstructionCosts.TryGetEffectiveCosts(target, actor, out var additional))
                for (int i = 0; i < GoodsCount; i++)
                    if (additional.TryGetValue(Goods[i], out int amount) && amount > 0)
                        result[i] = checked(result[i] + amount);
            return true;
        }

        internal bool HasQueueSpace => freeSlots > 0 && outboundSlots > 0;
        internal bool IsPending(Tile cursor) => pendingCurrent.Contains(cursor);

        internal void Place(eMappers mapper, Tile cursor, int actor, bool editor)
        {
            int previous = Read(Mapper);
            bool replace = previous != (int)mapper;
            if (replace)
                Marshal.WriteInt32(image, Mapper, (int)mapper);
            try
            {
                EngineInterface.PlaceMapperItem((int)mapper, cursor.X, cursor.Y, 0, actor, !editor, false, 1);
            }
            finally
            {
                if (replace)
                    Marshal.WriteInt32(image, Mapper, previous);
            }
        }

        internal void Submitted()
        {
            // Immediate execution may actually free the slot again. Counting it
            // as occupied here is conservative until the next live scan.
            freeSlots--;
            outboundSlots--;
        }

        internal int[] LastPreviewTiles { get; private set; }

        internal bool Reserve(eMappers tool, int actor, bool editor, Tile cursor, int scale, ref int frame)
            => ReserveCore(tool, actor, editor, cursor, scale, ref frame, false, false);

        internal bool ReserveVisual(eMappers tool, int actor, bool editor, Tile cursor, int scale, ref int frame)
        {
            LastPreviewTiles = null;
            return ReserveCore(tool, actor, editor, cursor, scale, ref frame, false,
                IsFarm(tool) || tool == eMappers.MAPPER_OIL_SMELTER);
        }

        internal bool ReserveStoragePreview(eMappers tool, int actor, bool editor, Tile cursor, int scale, ref int frame, bool draftNeighbor)
            => ReserveCore(tool, actor, editor, cursor, scale, ref frame, draftNeighbor, false);

        private bool ReserveCore(eMappers tool, int actor, bool editor, Tile cursor, int scale, ref int frame, bool draftNeighbor, bool captureTiles)
        {
            bool storage = IsStorage(tool);
            if (allowance <= 0 || storageAllowance <= 0 ||
                (uint)(scale - 1) >= MaximumFootprint || (draftNeighbor && (!storage || storageNeighbor?.IsIntact != true)))
                return false;
            bool freeWoodcutter = tool == eMappers.MAPPER_WOODSMAN && freeWoodcutters > 0;
            bool freeGranary = tool == eMappers.MAPPER_GRANARY && freeGranaries > 0;
            if (!freeWoodcutter && !freeGranary)
                for (int i = 0; i < GoodsCount; i++)
                    if (cost[i] > 0 && resources[i] < cost[i])
                        return false;

            var corner = new Tile(cursor.X - scale / 2, cursor.Y - scale / 2);
            int height = FootprintHeight(tool, scale);
            foreach (Footprint item in pending)
                if (item.Intersects(corner, scale, height))
                    return false;

            var tiles = GameTileManagerAPI.Instance;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < scale; x++)
                {
                    int mapX = corner.X + x, mapY = corner.Y + y;
                    if ((uint)mapX >= Axis || (uint)mapY >= Axis ||
                        Marshal.ReadByte(image, ValidMapMask + mapY * Axis + mapX) == 0)
                        return false;
                    int tile = tiles.GetTileId(mapX, mapY);
                    if (!tiles.IsValidTileId(tile))
                        return false;
                    int index = y * scale + x;
                    tileIds[index] = tile;
                    previewTokens[index] = Read(PreviewTokens + tile * sizeof(int));
                    previewFlags[index] = Marshal.ReadInt16(image, PreviewFlags + tile * sizeof(short));
                }
            }

            // The game monitor serializes both the native probe and its temporary preview-grid writes.
            // Do not undo Vanilla's path-search scratch counters and visitation marks.
            const int PlacementScratch = 0x60AD458, CursorScratch = 0x3A11DCC, MouseScratch = 0x8EBA93;
            const int CursorDepth = 0x3A11E28, EditorOwner = 0x88E3D78, MapperResult = 0x2D4D8C;
            const int NeighborScratch = 0x67E8380;
            Marshal.Copy(IntPtr.Add(image, PlacementScratch), placementScratch, 0, placementScratch.Length);
            Marshal.Copy(IntPtr.Add(image, CursorScratch), cursorScratch, 0, cursorScratch.Length);
            Marshal.Copy(IntPtr.Add(image, MouseScratch), mouseScratch, 0, mouseScratch.Length);
            int depth = Read(CursorDepth), editorOwner = Read(EditorOwner), result = Read(MapperResult);
            long neighborScratch = storage ? Marshal.ReadInt64(image, NeighborScratch) : 0;

            bool valid;
            try
            {
                if (Read(Mapper) != (int)tool)
                    Marshal.WriteInt32(image, Mapper, (int)tool);
                if (draftNeighbor)
                {
                    using (storageNeighbor.Scope(actor, corner.X, corner.Y, scale, tool.ConvertToEStructs()))
                        Hover();
                }
                else Hover();
                valid = Read(CursorScale) == scale && Read(CursorState) == 0 && Read(PlacementState) == 0;
                if (valid)
                {
                    int first = int.MaxValue;
                    int file = (int)SpriteFile(tool);
                    int graphicScale = IsFarm(tool) ? 3 : scale;
                    for (int y = 0; y < graphicScale && valid; y++)
                    {
                        for (int x = 0; x < graphicScale; x++)
                        {
                            int token = Read(PreviewTokens + tileIds[y * scale + x] * sizeof(int));
                            if (token >> 16 != file)
                            {
                                valid = false;
                                break;
                            }
                            first = Math.Min(first, (token & 0xFFFF) + 1);
                        }
                    }
                    if (valid)
                    {
                        frame = first;
                        if (captureTiles)
                            LastPreviewTiles = ReadPreviewTiles(tool, scale, height, file);
                    }
                }
            }
            finally
            {
                for (int i = 0; i < scale * height; i++)
                {
                    Marshal.WriteInt32(image, PreviewTokens + tileIds[i] * sizeof(int), previewTokens[i]);
                    Marshal.WriteInt16(image, PreviewFlags + tileIds[i] * sizeof(short), previewFlags[i]);
                }
                Marshal.Copy(placementScratch, 0, IntPtr.Add(image, PlacementScratch), placementScratch.Length);
                Marshal.Copy(cursorScratch, 0, IntPtr.Add(image, CursorScratch), cursorScratch.Length);
                Marshal.Copy(mouseScratch, 0, IntPtr.Add(image, MouseScratch), mouseScratch.Length);
                Marshal.WriteInt32(image, CursorDepth, depth);
                Marshal.WriteInt32(image, EditorOwner, editorOwner);
                Marshal.WriteInt32(image, MapperResult, result);
                if (storage)
                    Marshal.WriteInt64(image, NeighborScratch, neighborScratch);
            }

            if (!valid) return false;

            allowance -= RecordCount(tool);
            if (storage)
                storageAllowance -= RecordCount(tool);
            if (freeWoodcutter)
                freeWoodcutters--;
            else if (freeGranary)
                freeGranaries--;
            else
                Spend(cost);
            return true;

            void Hover() => EngineInterface.DLL_MapAction((int)tool, cursor.X, cursor.Y, 0, actor, !editor, true, 0);
        }

        private int[] ReadPreviewTiles(eMappers tool, int scale, int height, int primaryFile)
        {
            bool farm = IsFarm(tool);
            var graphic = new int[scale * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < scale; x++)
                {
                    int index = y * scale + x;
                    int token = Read(PreviewTokens + tileIds[index] * sizeof(int));
                    int expected = farm
                        ? (x < 3 && y < 3 ? primaryFile : (int)Enums.GM.GM_FARMLAND)
                        : (y < scale ? primaryFile : (int)Enums.GM.GM_BUILDINGS1);
                    if (token >> 16 == expected)
                        graphic[index] = token;
                }
            }
            return graphic;
        }

        private void AddPendingOilPart(Tile corner, int scale, int rotation)
        {
            int x = 0, y = 0;
            switch (rotation == 15 ? 0 : rotation / 2)
            {
                case 0: y = scale; break;
                case 1: x = scale; break;
                case 2: y = -scale; break;
                case 3: x = -scale; break;
                default:
                    // An unknown queued orientation may still allocate around the main building.
                    pending.Add(new Footprint(new Tile(corner.X - scale, corner.Y - scale), 3 * scale));
                    return;
            }
            pending.Add(new Footprint(new Tile(corner.X + x, corner.Y + y), scale));
        }

        private void Spend(int[] amounts)
        {
            for (int i = 0; i < GoodsCount; i++)
                resources[i] -= Math.Max(0, amounts[i]);
        }

        private int Read(int offset) => Marshal.ReadInt32(image, offset);

        private static string FindDll(IntPtr image)
        {
            using (Process process = Process.GetCurrentProcess())
            {
                foreach (ProcessModule module in process.Modules)
                    if (module.BaseAddress == image && StringComparer.OrdinalIgnoreCase.Equals(module.ModuleName, "CrusaderDE.dll"))
                        return module.FileName;
            }
            throw new InvalidOperationException("Unable to resolve the loaded CrusaderDE.dll path.");
        }

        private readonly struct Footprint
        {
            private readonly Tile corner;
            private readonly int size;
            internal Footprint(Tile corner, int size) { this.corner = corner; this.size = size; }
            internal bool Intersects(Tile other, int width, int height) =>
                other.X < corner.X + size && corner.X < other.X + width &&
                other.Y < corner.Y + size && corner.Y < other.Y + height;
        }
    }
}
