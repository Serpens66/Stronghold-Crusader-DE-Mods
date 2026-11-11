using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PitchMoatLineTest
{
    internal enum LineReceipt { Pending, Accepted, Rejected }

    // All entry points run under the same engine monitor as Vanilla placement.
    internal sealed unsafe partial class NativePlacement
    {
        private const int BrushWidth = 1;
        private const int MouseDown = 1;
        private const int MoatReserve = 50;
        private const int PitchReserve = 15;
        private const int TileManagerRva = 0x405EDB0, PlayerManagerRva = 0x366C210;
        private const int PathContextRva = 0x60AD660, VegetationManagerRva = 0x32DE440;
        private const int ActiveGridRva = 0x3A11EA4, ActiveMapperRva = 0x60AD45C;
        private const int NativePlayerRva = 0x88E3D70, NativeShiftRva = 0x8EBA9F;
        private const int NativeModeRva = 0x3665F28, NativeMapTypeRva = 0x3665ED8;
        private const int NativeFreeConstructionRva = 0x3665FC8, NativeBusyRva = 0x366A0F8;
        private const int PitchChargePhaseRva = 0x379D04C, PlayerStride = 0x583C;
        private const int PitchSlotLimitOffset = 0x204C6C4;
        private const int FindPitchSlotRva = 0x695C0;
        private const int KeepPathLimitRva = 0x37EF8F8;
        private const int PlacementStateOffset = 0x204E6FC, ValidationScratchEnd = 0x204E78C;
        private const int PlacementReasonRva = 0x60AD4B4, DistanceScratchRva = 0x34A9F5C;
        private const int TileHeightOffset = 0xD7E5A0, TileVegetationOffset = 0xA6F260, TileStructureOffset = 0xB0BCA0;
        private const int ChoreRva = 0x8574320, ChoreSerialOffset = 0xDA0;
        private const int ChoreQueueCursor = 0x370BF4, ChoreQueueStart = 0xB0BF8, ChoreQueueStride = 0x500;
        private const int ChoreQueueCount = 500, ChoreStateOffset = 9, FreeTerminalState = 10;
        private const int StageXRva = 0x86C132C, StageYRva = 0x86C1330;
        private const int StageTypeRva = 0x86C1334, StageDataRva = 0x86C1338, StageVariantRva = 0x86C133C;
        private const int MapPlacementX = 0x3A11DCC, MapPlacementY = 0x3A11DD0;
        private const int MoatPlanVariant = 0, MoatCompletedVariant = 2, MoatStageOperation = 0x40000000;
        private const int EditorPitchOwnerMapType = 12;
        private const byte NativePreviewMode = 1;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void PitchValidator(IntPtr tiles, int player, int x, int y, short mapper, int size, byte preview);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int MoatValidator(IntPtr tiles, int x, int y, int mapper, int width);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int KeepPathValidator(IntPtr pathContext, int x, int y, int radius);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CountSlots(IntPtr tiles);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindPitchSlot(IntPtr tiles, int packedTileId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate byte VegetationValidator(IntPtr vegetation, int tileId);

        private readonly IntPtr module;
        private readonly IntPtr tileManager;
        private readonly IntPtr choreManager;
        private readonly PitchValidator validatePitch;
        private readonly MoatValidator validateMoat;
        private readonly KeepPathValidator blocksKeepPath;
        private readonly CountSlots countMoatSlots, countPitchSlots;
        private readonly FindPitchSlot findPitchSlot;
        private readonly VegetationValidator isRemovableVegetation;
        private static NativePlacement rooted;

        private NativePlacement(CrusaderLibraryLoadContext context)
        {
            module = context.ModuleHandle;
            tileManager = Address(TileManagerRva);
            choreManager = Address(ChoreRva);
            validatePitch = Bind<PitchValidator>(context, 0x77E60, "48895C241055565741544155415641574883EC60");
            validateMoat = Bind<MoatValidator>(context, 0x5CA40, "48895C241848896C242089542410574154");
            blocksKeepPath = Bind<KeepPathValidator>(context, 0xEC130, "48895C240848896C2410488974241857");
            countMoatSlots = Bind<CountSlots>(context, 0x6C990, "33C04C8D815CEEF30141B95553000090");
            countPitchSlots = Bind<CountSlots>(context, 0x6C9D0, "4533C041B9200300004881C1608E0302");
            findPitchSlot = Bind<FindPitchSlot>(context, FindPitchSlotRva, "4C6391C4C6040241B9010000004D3BD1");
            isRemovableVegetation = Bind<VegetationValidator>(context, 0x107160, "4863C2488D15A66E9C04480FBF1442");
        }

        internal static bool TryCreate(CrusaderLibraryLoadContext context, ManualLogSource log, out NativePlacement placement)
        {
            placement = null;
            try
            {
                if (rooted != null) return false;
                VerifyImage(context);
                var candidate = new NativePlacement(context);
                rooted = candidate;
                candidate.InstallMoatWidthHook(context);
                try
                {
                    candidate.InstallPitchMoatHook(context);
                }
                catch (Exception error)
                {
                    Shared.DebugLogHelper.LogWarning(log, "PITCH_MOAT_LINE_OVERLAP_UNAVAILABLE: " + error);
                }
                placement = candidate;
                Shared.DebugLogHelper.LogInfo(log, "PITCH_MOAT_LINE_NATIVE_READY: reference-rva 0x923AB+18; continuation 0x923BD; " +
                    "validators 0x77E60 / 0x5CA40 / 0xEC130; capacity 0x6C990 / 0x6C9D0; pitch lookup 0x695C0; editor vegetation 0x107160.");
                return true;
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, "PITCH_MOAT_LINE_NATIVE_DISABLED: " + error);
                return false;
            }
        }

        private bool IsPitch(int mapper) => mapper == (int)eMappers.MAPPER_PITCH_DITCH;
        private bool IsMoat(int mapper) => mapper == (int)eMappers.MAPPER_MOAT || mapper == (int)eMappers.MAPPER_DUGMOAT;
        private eGameTypeModes Mode => (eGameTypeModes)Read(NativeModeRva);
        private bool CompleteMoat => Mode == eGameTypeModes.GAMETYPE_BUILDER || Mode == eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER;
        internal bool IsNativeShiftHeld => Marshal.ReadByte(Address(NativeShiftRva)) != 0;

        internal bool IsReadyForMapper(int mapper, bool inGame)
        {
            if (!IsPitch(mapper) && !IsMoat(mapper)) return false;
            if (!inGame && Mode != eGameTypeModes.GAMETYPE_BUILDER) return false;
            int expected = IsPitch(mapper) ? mapper : (int)(CompleteMoat ? eMappers.MAPPER_DUGMOAT : eMappers.MAPPER_MOAT);
            return Read(ActiveMapperRva) == expected && Read(NativeBusyRva) == 0;
        }

        private bool ValidContext(int mapper, int player, bool inGame) =>
            IsReadyForMapper(mapper, inGame) && player > 0 && GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) &&
            GameTileManagerAPI.Instance.GetTileManager() == tileManager &&
            GamePlayerManagerAPI.Instance.GetPlayerManager() == Address(PlayerManagerRva) &&
            GameGlobalsManager.Instance.ChoreManagerVA == unchecked((ulong)choreManager.ToInt64()) &&
            player == Read(NativePlayerRva);

        internal bool TryQuote(int mapper, int player, bool inGame, bool allowOverlap,
            IReadOnlyList<LineTile> tiles, LineTileState[] states, out LineQuote quote)
        {
            quote = default;
            if (!ValidContext(mapper, player, inGame) || tiles.Count < 1 || tiles.Count > LinePolicy.MaxTiles)
                return false;

            bool pitch = IsPitch(mapper);
            var budget = pitch ? GetPitchBudget(player) : new PitchBudget(0, 0, 0, PitchPayment.Free);
            if (!budget.Valid)
                return false;
            int freeSlots = pitch ? countPitchSlots(tileManager) : countMoatSlots(tileManager);
            int reserve = pitch ? PitchReserve : MoatReserve;
            int capacity = Math.Max(0, freeSlots - reserve + 1);
            // Native validators use these placement-result fields as scratch. Restore the
            // previous cursor result so an arbitrary line tile does not affect the next tool.
            int* scratch = stackalloc int[(ValidationScratchEnd - PlacementStateOffset) / sizeof(int)];
            int* nativeScratch = (int*)IntPtr.Add(tileManager, PlacementStateOffset);
            for (int i = 0; i < (ValidationScratchEnd - PlacementStateOffset) / sizeof(int); i++)
                scratch[i] = nativeScratch[i];
            int reason = Read(PlacementReasonRva);
            int distance = Read(DistanceScratchRva);
            try
            {
                for (int i = 0; i < tiles.Count; i++)
                    states[i] = ClassifyTile(pitch, player, allowOverlap, tiles[i]);
            }
            finally
            {
                for (int i = 0; i < (ValidationScratchEnd - PlacementStateOffset) / sizeof(int); i++)
                    nativeScratch[i] = scratch[i];
                Marshal.WriteInt32(Address(PlacementReasonRva), reason);
                Marshal.WriteInt32(Address(DistanceScratchRva), distance);
            }
            quote = LinePolicy.ApplyLimits(states, tiles.Count, capacity, budget);
            return true;
        }

        private PitchBudget GetPitchBudget(int player)
        {
            var goods = GamePlayerManagerAPI.Instance;
            var costs = GameBuildingManagerAPI.Instance;
            const eStructs pitch = eStructs.STRUCT_PITCH_DITCH;
            int stock = goods.GetGoodAmount(player, eGoods.STORED_PITCH_RAW);
            bool free = Mode == eGameTypeModes.GAMETYPE_BUILDER || Read(NativeFreeConstructionRva) != 0;
            if (free)
                return new PitchBudget(stock, 0, 0, PitchPayment.Free);
            int phase = Marshal.ReadInt16(Address(PitchChargePhaseRva + player * PlayerStride));
            int rawMinimum = costs.GetRawPitchCost(pitch);
            bool ironAllowed = goods.GetGoodAmount(player, eGoods.STORED_IRON_INGOTS) >= costs.GetIronIngotCost(pitch);
            bool laterResourcesAllowed = goods.GetGoodAmount(player, eGoods.STORED_WOOD_PLANKS) >= costs.GetWoodCost(pitch) &&
                goods.GetGoodAmount(player, eGoods.STORED_STONE_BLOCKS) >= costs.GetStoneCost(pitch) &&
                goods.GetGoodAmount(player, eGoods.STORED_GOLD) >= costs.GetGoldCost(pitch);
            return new PitchBudget(stock, phase, rawMinimum,
                Mode == eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER ? PitchPayment.CheckStockOnly : PitchPayment.Standard,
                ironAllowed, laterResourcesAllowed);
        }

        private LineTileState ClassifyTile(bool pitch, int player, bool allowOverlap, LineTile tile)
        {
            if (!TryTileId(tile, out int tileId)) return LineTileState.Blocked;
            var tiles = GameTileManagerAPI.Instance;
            TilePropertyFlag flags = tiles.GetTilePropertyFlag(tileId);
            if (pitch)
            {
                if ((flags & TilePropertyFlag.PitchTrap) != 0) return LineTileState.Existing;
                if ((flags & TilePropertyFlag.IsMoat) != 0) return LineTileState.Blocked;
                if ((flags & TilePropertyFlag.PlannedMoat) != 0)
                {
                    if (!tiles.TryGetMoatWorkTaskAtTile(tile.X, tile.Y, out var task) ||
                        task.Pointer->r_TileX != tile.X || task.Pointer->r_TileY != tile.Y ||
                        task.Pointer->r_WorkType != MoatWorkType.DigPlannedMoat || task.Pointer->r_Progress >= 4 ||
                        !GamePlayerManagerAPI.Instance.IsPlayerIdValid(task.Pointer->r_OwnerPlayerId))
                        return LineTileState.Blocked;
                    if (!TrackPitchMoat((int)eMappers.MAPPER_PITCH_DITCH, allowOverlap))
                        return LineTileState.OverlapUnavailable;
                }
                validatePitch(tileManager, player, tile.X, tile.Y, (short)eMappers.MAPPER_PITCH_DITCH, BrushWidth, NativePreviewMode);
                return Marshal.ReadInt32(tileManager, PlacementStateOffset) == 0 ? LineTileState.Buildable : LineTileState.Blocked;
            }
            if ((flags & (TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat)) != 0) return LineTileState.Existing;
            if (tiles.GetMoatWorkTaskIndexLayer()[tileId] != 0) return LineTileState.Blocked;
            int keepLimit = Read(KeepPathLimitRva);
            if (keepLimit < 0 || keepLimit > GameTileManagerAPI.MAX_WIDTH ||
                blocksKeepPath(Address(PathContextRva), tile.X, tile.Y, keepLimit + 5) != 0) return LineTileState.Blocked;
            bool valid = CompleteMoat ? IsCompletedMoatTileAllowed(tileId, flags) :
                validateMoat(tileManager, tile.X, tile.Y, (int)eMappers.MAPPER_MOAT, BrushWidth) != 0;
            return valid ? LineTileState.Buildable : LineTileState.Blocked;
        }

        private bool IsCompletedMoatTileAllowed(int tileId, TilePropertyFlag flags)
        {
            // The completed variant bypasses 5CA40 on input and reaches the
            // brush-1 terrain branch at 6FE90. Use the same occupancy mask.
            const TilePropertyFlag blocked = TilePropertyFlag.RealityEdge | TilePropertyFlag.IsElevated |
                TilePropertyFlag.IsBuilding | TilePropertyFlag.IsWall | TilePropertyFlag.Sea |
                TilePropertyFlag.River | TilePropertyFlag.Ford | TilePropertyFlag.ImpassableEdge |
                TilePropertyFlag.HasStone | TilePropertyFlag.HasIron | TilePropertyFlag.IsSwamp |
                TilePropertyFlag.IsFarm | TilePropertyFlag.IsMoat;
            if ((flags & blocked) != 0 || Marshal.ReadInt16(tileManager, TileStructureOffset + tileId * sizeof(short)) != 0)
                return false;
            if (Marshal.ReadInt16(tileManager, TileVegetationOffset + tileId * sizeof(short)) != 0 &&
                isRemovableVegetation(Address(VegetationManagerRva), tileId) == 0)
                return false;
            // The terminal call will still run ExtraFeatures' approved height override, if enabled.
            return Marshal.ReadByte(tileManager, TileHeightOffset + tileId) < 13;
        }

        private bool TrackPitchMoat(int mapper, bool allowOverlap) => IsPitch(mapper) && allowOverlap && PitchMoatReady;

        internal bool TrySubmit(int mapper, int player, bool inGame, bool allowOverlap, LineTile tile)
        {
            if (!ValidContext(mapper, player, inGame) || IsNativeShiftHeld ||
                !TryTileId(tile, out int tileId) || !TryFindFreeChoreSlot(out int freeSlot))
                return false;
            bool pitch = IsPitch(mapper);
            bool trackPitch = TrackPitchMoat(mapper, allowOverlap);
            if (pitch)
            {
                var flags = GameTileManagerAPI.Instance.GetTilePropertyFlag(tileId);
                if ((flags & (TilePropertyFlag.PitchTrap | TilePropertyFlag.IsMoat)) != 0 ||
                    (!trackPitch && (flags & TilePropertyFlag.PlannedMoat) != 0) ||
                    !TryGetPitchOwner(tileId, tile, out int owner) || owner != 0 ||
                    (trackPitch && !TryArmPitchMoat(tile, tileId, player, freeSlot)))
                    return false;
            }
            else if (GameTileManagerAPI.Instance.GetMoatWorkTaskIndexLayer()[tileId] != 0)
                return false;

            int serial = Marshal.ReadInt32(choreManager, ChoreSerialOffset);
            int action = pitch ? mapper : (int)eMappers.MAPPER_MOAT;
            int moatVariant = CompleteMoat ? MoatCompletedVariant : MoatPlanVariant;
            bool enqueued = false;
            try
            {
                if (!pitch)
                {
                    widthScope = this;
                    scopedTile = tile;
                    scopedPlayer = player;
                    widthApplied = false;
                }
                EngineInterface.PlaceMapperItem(action, tile.X, tile.Y, 0, player, inGame, true, MouseDown);
                enqueued = Marshal.ReadInt32(choreManager, ChoreSerialOffset) == unchecked(serial + 1) &&
                    Read(StageXRva) == tile.X && Read(StageYRva) == tile.Y && (pitch
                    ? Read(StageTypeRva) == (int)eMappers.MAPPER_PITCH_DITCH && Read(StageDataRva) == BrushWidth && Read(StageVariantRva) == 0
                    : widthApplied && Read(StageTypeRva) == BrushWidth && Read(StageDataRva) == MoatStageOperation && Read(StageVariantRva) == moatVariant);
            }
            finally
            {
                widthScope = null;
                if (trackPitch)
                    enqueued = ConfirmPitchMoatSubmission(enqueued);
            }
            return enqueued;
        }

        internal LineReceipt PollReceipt(int mapper, int player, bool inGame, bool allowOverlap, LineTile tile)
        {
            if (!ValidContext(mapper, player, inGame) || !TryTileId(tile, out int tileId))
                return LineReceipt.Rejected;
            var tiles = GameTileManagerAPI.Instance;
            TilePropertyFlag flags = tiles.GetTilePropertyFlag(tileId);
            if (IsPitch(mapper))
            {
                if ((flags & TilePropertyFlag.PitchTrap) == 0 ||
                    (TrackPitchMoat(mapper, allowOverlap) && !PitchMoatExecuted)) return LineReceipt.Pending;
                int owner = Read(NativeMapTypeRva) == EditorPitchOwnerMapType ? 1 : player;
                return TryGetPitchOwner(tileId, tile, out int actualOwner) && actualOwner == owner
                    ? LineReceipt.Accepted : LineReceipt.Rejected;
            }
            TilePropertyFlag expected = CompleteMoat ? TilePropertyFlag.IsMoat : TilePropertyFlag.PlannedMoat;
            if ((flags & expected) == 0) return LineReceipt.Pending;
            return tiles.TryGetMoatWorkTaskAtTile(tile.X, tile.Y, out var moat) && moat.Pointer->r_OwnerPlayerId == player
                ? LineReceipt.Accepted : LineReceipt.Rejected;
        }

        private bool TryGetPitchOwner(int packedTileId, LineTile tile, out int owner)
        {
            owner = 0;
            var slots = GameTileManagerAPI.Instance.GetPitchSlots();
            int limit = Marshal.ReadInt32(tileManager, PitchSlotLimitOffset);
            // Vanilla trusts the signed exclusive bound before scanning its records.
            if (slots.IsEmpty || (uint)limit > (uint)slots.Length)
                return false;
            int slot = findPitchSlot(tileManager, packedTileId);
            if (slot == 0)
                return true;
            if ((uint)slot >= (uint)limit)
                return false;
            ref readonly var entry = ref slots[slot];
            if (entry.TileX != tile.X || entry.TileY != tile.Y ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(entry.OwnerId))
                return false;
            owner = entry.OwnerId;
            return true;
        }

        private bool TryTileId(LineTile tile, out int tileId)
        {
            tileId = -1;
            const int gridSide = GameTileManagerAPI.MAX_WIDTH;
            if ((uint)tile.X >= gridSide || (uint)tile.Y >= gridSide ||
                Marshal.ReadByte(Address(ActiveGridRva + tile.Y * gridSide + tile.X)) == 0)
                return false;
            var tiles = GameTileManagerAPI.Instance;
            tileId = tiles.GetTileId(tile.X, tile.Y);
            return tiles.IsValidTileId(tileId);
        }

        private IntPtr Address(int rva) => IntPtr.Add(module, rva);
        private int Read(int rva) => Marshal.ReadInt32(Address(rva));
    }
}
