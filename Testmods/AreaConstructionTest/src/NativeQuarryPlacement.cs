using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Buildings;
using APIShared.NativeSites;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.DebugMenu;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.GameGlobals;
using SHCDESE.ImMenu.MapEditor;
using SHCDESE.Interop;

namespace AreaConstructionTest
{
    internal sealed unsafe class NativeQuarryPlacement
    {
        internal const int QuarrySize = 6;
        internal const int PileSize = 2;

        private const int FinderRva = 0xBD7A0;
        private const int ComponentRebuildRva = 0xE49D0;
        private const int PathContextRva = 0x60AD660;
        private const int BuildingManagerRva = 0x64CCBB0;
        private const int TileManagerRva = 0x405EDB0;
        private const int SearchOffsetsRva = BuildingManagerRva + 0x31B7D0;
        private const int PlacementScratchRva = 0x60AD458;
        private const int SkipValidationRva = 0x60AD4A8;
        private const int PlacementStateRva = 0x60AD4AC;
        private const int WorldKindRva = 0x3665F28;
        private const int FreeBuildRva = 0x3665FC8;
        private const int PlacementDisabledRva = 0x366A0F8;
        private const int AvailableBuildingsRva = 0x64CCC04;
        private const int PlayerBuildingCountsRva = 0x64CCBB4;
        private const int NetworkModeRva = 0x8574B90;
        private const int ParticipantSlotsRva = 0x8574BCC;
        private const int ParticipantFlagsRva = 0x8574C44;
        private const int PlacementExtraRva = 0x67E83D0;

        private static readonly (int Rva, byte[] Bytes)[] NativeSpans =
        {
            (FinderRva, new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x18, 0x48, 0x89, 0x6C, 0x24, 0x20, 0x89, 0x54, 0x24, 0x10, 0x56 }),
            (0x76968, new byte[] { 0xE8, 0x33, 0x6E, 0x04, 0x00, 0x83, 0xBB, 0xFC, 0xE6, 0x04, 0x02, 0x00, 0x74, 0x1C }),
            (0x767C1, new byte[] { 0xE8, 0x0A, 0xE2, 0x06, 0x00 }),
            (0xBD817, new byte[] { 0xE8, 0x54, 0x2A, 0x00, 0x00 }),
            (0xBD851, new byte[] { 0xE8, 0x0A, 0xA6, 0xFB, 0xFF, 0x83, 0x3D, 0x4F, 0xFC, 0xFE, 0x05, 0x00, 0x75, 0x4A })
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FindPile(IntPtr manager, int actor, int x, int y, int size, int pileSize,
            int outwardOffset, short mapper, int pathComponent);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RebuildComponents(IntPtr context, int force);

        private sealed class PileScope
        {
            internal PileScope(int actor, Predicate<Tile> allows)
            {
                Actor = actor;
                Allows = allows;
            }

            internal readonly int Actor;
            internal readonly Predicate<Tile> Allows;
            internal bool Observed;
        }

        [ThreadStatic] private static PileScope active;

        private readonly IntPtr image, tileManager, buildingManager;
        private readonly ManualLogSource log;
        private readonly FindPile findPile;
        private readonly RebuildComponents rebuildComponents;
        private readonly FieldInfo mirrorField;
        private readonly byte[] savedPlacement = new byte[0x150];
        private bool intact;

        private NativeQuarryPlacement(IntPtr image, ManualLogSource log)
        {
            this.image = image;
            this.log = log;
            tileManager = IntPtr.Add(image, TileManagerRva);
            buildingManager = IntPtr.Add(image, BuildingManagerRva);
            findPile = Marshal.GetDelegateForFunctionPointer<FindPile>(IntPtr.Add(image, FinderRva));
            rebuildComponents = Marshal.GetDelegateForFunctionPointer<RebuildComponents>(IntPtr.Add(image, ComponentRebuildRva));
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
                foreach (var span in NativeSpans)
                {
                    if (span.Rva > context.Memory.Length - span.Bytes.Length ||
                        !context.Memory.Slice(span.Rva, span.Bytes.Length).SequenceEqual(span.Bytes))
                        throw new InvalidOperationException("Load-image bytes differ at RVA 0x" + span.Rva.ToString("X") + ".");
                }
                adapter.RequireLiveSpans();
                const string operation = "AreaConstructionTest quarry component refresh";
                var claim = NativeSiteRegistry.Register(Plugin.Guid, operation, ComponentRebuildRva, 1, NativeSiteMode.CallOnly);
                if (!claim.Succeeded)
                    throw new InvalidOperationException(claim.Reason);
                // The Extender owns the validator detour and roots the subscriber for the process.
                BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(adapter.ObservePlacement);
                Volatile.Write(ref adapter.intact, true);
                log.LogInfo(Stamp("Quarry pile finder bound at reference RVA 0xBD7A0."));
                return adapter;
            }
            catch (Exception exception)
            {
                log.LogError(Stamp("Quarry drag disabled. " + exception.Message));
                return null;
            }
        }

        internal bool IsIntact => Volatile.Read(ref intact);

        // Invoke only under the engine monitor while executing a released block.
        // Pathfinding's registered entry detour forwards normal simulation calls.
        internal bool RefreshBeforeBuild()
        {
            if (!IsIntact || active != null || !ManagersMatch())
                return false;
            var context = GamePathingManagerAPI.Instance.GetPathfindingContextView();
            if (context.Address != IntPtr.Add(image, PathContextRva))
                return false;
            if (context.ComponentGridDirty == 0)
                return true;
            uint previous = context.ComponentGeneration;
            return rebuildComponents(context.Address, 1) == 1 && context.ComponentGeneration != previous;
        }

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
            if (!IsIntact || active != null || (uint)(actor - 1) >= 8 || !ManagersMatch())
                return false;

            var quarry = Corner(cursor);
            var tiles = GameTileManagerAPI.Instance;
            if (!ValidSquare(tiles, quarry, QuarrySize))
                return false;
            int quarryTileId = tiles.GetTileId(quarry.X, quarry.Y);
            Span<ushort> components = tiles.GetPathConnectionLayer();
            if ((uint)quarryTileId >= (uint)components.Length)
                return false;

            var scope = new PileScope(actor, p => ValidSquare(tiles, p, PileSize) && acceptsPileCorner(p));
            int component = components[quarryTileId];
            long oldOffsets = Marshal.ReadInt64(image, SearchOffsetsRva);
            var placement = IntPtr.Add(image, PlacementScratchRva);
            Marshal.Copy(placement, savedPlacement, 0, savedPlacement.Length);
            active = scope;
            try
            {
                // Vanilla grows the search square three times, then extends the same perimeter.
                for (int round = 1; round <= 11; round++)
                {
                    int radius = Math.Min(round, 3);
                    Write(PlacementStateRva, 1);
                    findPile(buildingManager, actor, quarry.X - radius, quarry.Y - radius,
                        QuarrySize + radius * 2, PileSize, Math.Max(0, round - 3), (short)eMappers.MAPPER_QUARRYPILE, component);
                    if (!scope.Observed || Read(PlacementStateRva) != 0)
                        continue;
                    var found = new Tile(quarry.X - radius + Read(SearchOffsetsRva),
                        quarry.Y - radius + Read(SearchOffsetsRva + sizeof(int)));
                    if (!scope.Allows(found))
                        return false;
                    pileCorner = found;
                    return true;
                }
                return false;
            }
            finally
            {
                active = null;
                Marshal.Copy(savedPlacement, 0, placement, savedPlacement.Length);
                Marshal.WriteInt64(image, SearchOffsetsRva, oldOffsets);
            }
        }

        internal bool TryBuild(int actor, Tile cursor, Tile pileCorner, bool editor)
        {
            int world = Read(WorldKindRva);
            if (!IsIntact || active != null || Read(PlacementDisabledRva) != 0 ||
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
            var scope = new PileScope(actor, candidate => candidate.Equals(pileCorner));
            int previousExtra = Read(PlacementExtraRva);
            int previousSkipValidation = Read(SkipValidationRva);
            bool previousMirror = mirror.BuildingMirrorEnabled;
            active = scope;
            try
            {
                // Use the Extender wrapper so other mods see the normal build and spawn events.
                // A drag already defines its exact targets, independently of the local SE mirror tool.
                mirror.BuildingMirrorEnabled = false;
                Write(PlacementExtraRva, 0);
                Write(SkipValidationRva, 0);
                GameBuildingManagerAPI.Instance.CreatePrefab(actor, quarry.X, quarry.Y, mapper, QuarrySize, editor ? 15 : 0, false);
                return scope.Observed && HasLinkedPile(actor, quarry, pileCorner);
            }
            finally
            {
                active = null;
                mirror.BuildingMirrorEnabled = previousMirror;
                Write(PlacementExtraRva, previousExtra);
                Write(SkipValidationRva, previousSkipValidation);
            }
        }

        private void ObservePlacement(BuildingPlacementValidationEventArgs args)
        {
            var scope = active;
            if (scope == null || args.Phase != EventHookPhase.Post || args.PlayerId != scope.Actor ||
                args.Mappers != eMappers.MAPPER_QUARRYPILE || args.Unknown1 != PileSize || args.Unknown2 != 0)
                return;

            scope.Observed = true;
            if (Read(PlacementStateRva) == 0 && !scope.Allows(new Tile(args.TileX, args.TileY)))
                Write(PlacementStateRva, 1);
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
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(actor, out GamePlayerResources* player) ||
                !ConstructionCosts.TryGetCurrent(ConstructionCostTarget.FromMapper(eMappers.MAPPER_QUARRY), out var cost, out _))
                return false;
            return player->r_TotalGoodsWoodPlanks >= cost.Wood &&
                player->r_TotalGoodsStoneBlocks >= cost.Stone &&
                player->r_TotalGoodsIronIngots >= cost.Iron &&
                player->r_TotalGoodsPitchRaw >= cost.Pitch &&
                player->r_TotalGoodsGold >= cost.Gold;
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

        private static bool ValidSquare(GameTileManagerAPI tiles, Tile corner, int size)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (!tiles.IsTileInsideMapBounds(corner.X + x, corner.Y + y))
                        return false;
            return true;
        }

        private void RequireLiveSpans()
        {
            foreach (var span in NativeSpans)
            {
                var live = new ReadOnlySpan<byte>((void*)IntPtr.Add(image, span.Rva), span.Bytes.Length);
                if (!live.SequenceEqual(span.Bytes))
                    throw new InvalidOperationException("Live foreign patch at RVA 0x" + span.Rva.ToString("X") + ".");
            }
        }

        private static Tile Corner(Tile cursor) => new Tile(cursor.X - QuarrySize / 2, cursor.Y - QuarrySize / 2);
        private int Read(int rva) => Marshal.ReadInt32(image, rva);
        private void Write(int rva, int value) => Marshal.WriteInt32(image, rva, value);
        private static string Stamp(string text) => $"[{DateTime.Now:HH:mm:ss.fff}] {text}";
    }
}
