using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Buildings;
using APIShared.Pathfinding;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.DebugMenu;
using SHCDESE.GameGlobals;
using SHCDESE.ImMenu.MapEditor;
using SHCDESE.Interop;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed unsafe class NativeQuarryPlacement
    {
        internal const int QuarrySize = 6;
        private const int BuildingManagerRva = 0x64CCBB0;
        private const int TileManagerRva = 0x405EDB0;
        private const int SkipValidationRva = 0x60AD4A8;
        private const int WorldKindRva = 0x3665F28;
        private const int FreeBuildRva = 0x3665FC8;
        private const int PlacementDisabledRva = 0x366A0F8;
        private const int AvailableBuildingsRva = 0x64CCC04;
        private const int PlayerBuildingCountsRva = 0x64CCBB4;
        private const int NetworkModeRva = 0x8574B90;
        private const int ParticipantSlotsRva = 0x8574BCC;
        private const int ParticipantFlagsRva = 0x8574C44;
        private const int PlacementExtraRva = 0x67E83D0;

        private const int BuildFinderCallRva = 0x76968;
        private static readonly byte[] BuildFinderCall =
            { 0xE8, 0x33, 0x6E, 0x04, 0x00, 0x83, 0xBB, 0xFC, 0xE6, 0x04, 0x02, 0x00, 0x74, 0x1C };

        private readonly IntPtr image, tileManager, buildingManager;
        private readonly ManualLogSource log;
        private readonly FieldInfo mirrorField;
        private bool intact;

        private NativeQuarryPlacement(IntPtr image, ManualLogSource log)
        {
            this.image = image;
            this.log = log;
            tileManager = IntPtr.Add(image, TileManagerRva);
            buildingManager = IntPtr.Add(image, BuildingManagerRva);
            mirrorField = typeof(DebugMenuManager).GetField("_mirrorTool", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mirrorField?.FieldType != typeof(MirrorTool))
                throw new InvalidOperationException("Script Extender building-mirror contract differs.");
        }

        // NativePlacement has already verified the hash on this LibraryLoaded notification.
        internal static NativeQuarryPlacement TryInstall(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            try
            {
                var adapter = new NativeQuarryPlacement(context.ModuleHandle, log);
                if (BuildFinderCallRva > context.Memory.Length - BuildFinderCall.Length ||
                    !context.Memory.Slice(BuildFinderCallRva, BuildFinderCall.Length).SequenceEqual(BuildFinderCall))
                    throw new InvalidOperationException("Load-image bytes differ at quarry construction's finder call.");
                adapter.RequireLiveSpans();
                Volatile.Write(ref adapter.intact, true);
                return adapter;
            }
            catch (Exception exception)
            {
                log.LogError(Stamp("Quarry drag disabled. " + exception.Message));
                return null;
            }
        }

        internal bool IsIntact => Volatile.Read(ref intact) && QuarryPilePlacement.InitialSearchAvailable;

        // Invoke only under the engine monitor while executing a released block.
        // Pathfinding's registered entry detour forwards normal simulation calls.
        internal bool RefreshBeforeBuild() => IsIntact && ManagersMatch() &&
            PathComponentRefresh.TryRefresh(force: false, out _, out _);

        internal void Recheck()
        {
            if (!IsIntact)
                return;
            try { RequireLiveSpans(); }
            catch (Exception exception)
            {
                Volatile.Write(ref intact, false);
                log.LogError(Stamp("Quarry drag disabled. " + exception.Message));
            }
        }

        internal bool TryChoose(int actor, Tile cursor, Predicate<Tile> acceptsPileCorner, out Tile pileCorner)
        {
            pileCorner = default;
            if (!IsIntact)
                return false;

            var quarry = Corner(cursor);
            if (!QuarryPilePlacement.TryFindInitialCandidate(actor, quarry.X, quarry.Y,
                (x, y) => acceptsPileCorner(new Tile(x, y)), out int pileX, out int pileY))
                return false;
            pileCorner = new Tile(pileX, pileY);
            return true;
        }

        internal bool TryBuild(int actor, Tile cursor, Tile pileCorner, bool editor)
        {
            int world = Read(WorldKindRva);
            if (!IsIntact || Read(PlacementDisabledRva) != 0 ||
                (editor ? world != 1 : world == 1 || world == 4 || world == 6) ||
                (uint)(actor - 1) >= 8 || !ManagersMatch() || !HasTwoBuildingSlots(actor))
                return false;

            var mapper = eMappers.MAPPER_QUARRY;
            if (!editor && (!GamePlayerManagerAPI.Instance.GetBuildingAvailability(mapper) || !CanAffordQuarry(actor)))
                return false;

            // Repeat the native candidate search at execution, before any debit.
            if (!TryChoose(actor, cursor, candidate => candidate.Equals(pileCorner), out _) ||
                !(mirrorField.GetValue(DebugMenuManager.Instance) is MirrorTool mirror) ||
                GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride)
                return false;

            var quarry = Corner(cursor);
            int previousExtra = Read(PlacementExtraRva);
            int previousSkipValidation = Read(SkipValidationRva);
            bool previousMirror = mirror.BuildingMirrorEnabled;
            try
            {
                // Use the Extender wrapper so other mods see the normal build and spawn events.
                // A drag already defines its exact targets, independently of the local SE mirror tool.
                mirror.BuildingMirrorEnabled = false;
                Write(PlacementExtraRva, 0);
                Write(SkipValidationRva, 0);
                return QuarryPilePlacement.TryWithInitialCandidateFilter(actor, pileCorner.X, pileCorner.Y, () =>
                {
                    GameBuildingManagerAPI.Instance.CreatePrefab(actor, quarry.X, quarry.Y, mapper, QuarrySize, editor ? 15 : 0, false);
                    return HasLinkedPile(actor, quarry, pileCorner);
                });
            }
            finally
            {
                mirror.BuildingMirrorEnabled = previousMirror;
                Write(PlacementExtraRva, previousExtra);
                Write(SkipValidationRva, previousSkipValidation);
            }
        }

        private bool HasTwoBuildingSlots(int actor)
        {
            if (Read(AvailableBuildingsRva) < 21)
                return false;
            if (Read(NetworkModeRva) == 0)
                return true;
            int participants = 0;
            for (int i = 1; i <= 8; i++)
                if (Read(ParticipantSlotsRva + i * sizeof(int)) != -1 ||
                    Marshal.ReadByte(image, ParticipantFlagsRva + i * sizeof(int)) != 0)
                    participants++;
            return Read(PlayerBuildingCountsRva + actor * sizeof(int)) + 2 < 4000 / Math.Max(2, participants);
        }

        // Vanilla has no first-free quarry exception. A live resource check stays
        // independent of Spectator's local cursor and trade-view overrides at 0xCC420.
        private bool CanAffordQuarry(int actor)
        {
            if (Read(FreeBuildRva) != 0)
                return true;
            if (!ConstructionCosts.TryGetPlayerMaterialStock(actor, out var stock, out _) ||
                !ConstructionCosts.TryGetCurrent(ConstructionCostTarget.FromMapper(eMappers.MAPPER_QUARRY), out var cost, out _))
                return false;
            return stock.Wood >= cost.Wood && stock.Stone >= cost.Stone && stock.Iron >= cost.Iron &&
                stock.Pitch >= cost.Pitch && stock.Gold >= cost.Gold;
        }

        private static bool HasLinkedPile(int actor, Tile quarry, Tile pile)
        {
            var tiles = GameTileManagerAPI.Instance;
            int quarryId = tiles.GetTileBuildingId(tiles.GetTileId(quarry.X, quarry.Y));
            int pileId = tiles.GetTileBuildingId(tiles.GetTileId(pile.X, pile.Y));
            return quarryId != 0 && pileId != 0 &&
                BuildingAccess.TryGetById(quarryId, out var main, out _) &&
                BuildingAccess.TryGetById(pileId, out var storage, out _) &&
                main->r_PlayerIdOwner == actor && storage->r_PlayerIdOwner == actor &&
                main->r_BuildingType == eStructs.STRUCT_QUARRY && storage->r_BuildingType == eStructs.STRUCT_QUARRYPILE &&
                main->r_StoneQuarry_StockPileBuildingId == pileId &&
                main->r_TilePositionXBegin == quarry.X && main->r_TilePositionYBegin == quarry.Y &&
                storage->r_TilePositionXBegin == pile.X && storage->r_TilePositionYBegin == pile.Y;
        }

        private bool ManagersMatch() =>
            GameGlobalsManager.Instance.GameTileManagerVA == unchecked((ulong)tileManager.ToInt64()) &&
            GameGlobalsManager.Instance.GameBuildingManagerVA == unchecked((ulong)buildingManager.ToInt64());

        private void RequireLiveSpans()
        {
            var live = new ReadOnlySpan<byte>((void*)IntPtr.Add(image, BuildFinderCallRva), BuildFinderCall.Length);
            if (!live.SequenceEqual(BuildFinderCall))
                throw new InvalidOperationException("Live foreign patch at quarry construction's finder call.");
        }

        private static Tile Corner(Tile cursor) => new Tile(cursor.X - QuarrySize / 2, cursor.Y - QuarrySize / 2);
        private int Read(int rva) => Marshal.ReadInt32(image, rva);
        private void Write(int rva, int value) => Marshal.WriteInt32(image, rva, value);
        private static string Stamp(string text) => $"[{DateTime.Now:HH:mm:ss.fff}] {text}";
    }
}
