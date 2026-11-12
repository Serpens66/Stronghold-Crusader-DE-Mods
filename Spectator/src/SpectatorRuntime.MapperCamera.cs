using System;
using APIShared;
using APIShared.Presentation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private sealed class MapperCameraTarget
        {
            internal long Session;
            internal int Owner, Mapper, Id, X, Y;
            internal uint GlobalId;
            internal eStructs Type;
            internal GameMap Map;
            internal CameraControls2D Camera;
            internal PerfectPixelWithZoom Pixels;
        }

        private static void RegisterMapperSelectionCamera()
        {
            ApiShared.ForMod(Guid).WhenReady(client => {
                if (!MapperSelectionCameraEvents.TryRegister(Guid, "place/selection-camera",
                    BeforeMapperSelectionCamera, AfterMapperSelectionCamera, out string reason))
                    LogOnce("SPECTATOR_PLACE_CAMERA_UNAVAILABLE: " + reason);
            });
        }

        private static bool CanCorrectMapperCamera(long session, int owner) =>
            IsObserverBuildUiThread && initialized && session > 0 && IsCurrentSession(session) &&
            IsPlacementSession() && owner > 0 && CurrentAiOwner() == owner &&
            PlayerPerspectiveAPI.GetRawNativeViewPlayerId() != owner;

        private static void BeforeMapperSelectionCamera(MapperSelectionCameraPreEventArgs args)
        {
            if (!IsObserverBuildUiThread) return;
            int owner = CurrentAiOwner();
            if (!CanCorrectMapperCamera(args.SessionId, owner) ||
                !TryGetCameraBuildingType((eMappers)args.Mapper, out eStructs type)) return;

            // A missing/unsafe anchor still suppresses the wrong player's jump. Do not
            // throw from this observer: the broker would rightly discard its decision.
            args.SuppressCamera = true;
            try
            {
                var target = new MapperCameraTarget {
                    Session = args.SessionId, Owner = owner, Mapper = args.Mapper, Type = type,
                    Map = GameMap.instance, Camera = CameraControls2D.instance,
                    Pixels = PerfectPixelWithZoom.instance
                };
                if (target.Map == null || target.Camera == null || target.Pixels == null ||
                    type == eStructs.STRUCT_NULL) return;
                lock (engineLock)
                    if (!TryReadMapperCameraTarget(target, true)) return;
                args.State = target;
            }
            catch (Exception error) { LogOnce("SPECTATOR_PLACE_CAMERA_CAPTURE_ERROR: " + error); }
        }

        private static void AfterMapperSelectionCamera(MapperSelectionCameraPostEventArgs args)
        {
            if (args.Exception != null || args.Result < 0 || !args.SuppressionActive ||
                !(args.State is MapperCameraTarget target)) return;
            try
            {
                if (args.Mapper != target.Mapper || args.SessionId != target.Session ||
                    !IsMapperCameraSceneCurrent(target)) return;
                lock (engineLock)
                    if (!TryReadMapperCameraTarget(target, false)) return;

                // Use the public conversion synchronously. The convenience SE setter
                // queues its work, which could apply it to a later world or selection.
                target.Map.mapGameTileToTilemapCoord(target.X, target.Y, out int x, out int y);
                if (target.Map.getMapTile(x, y) == null) return;
                var position = SHCDESE.CoordinateConverter.ConvertLocalTileToCameraWorld(
                    new System.Numerics.Vector2(target.X, target.Y));
                if (float.IsNaN(position.X) || float.IsInfinity(position.X) ||
                    float.IsNaN(position.Y) || float.IsInfinity(position.Y) ||
                    !IsMapperCameraSceneCurrent(target)) return;
                target.Camera.setCameraPos(position.X, position.Y);
                target.Map.PreCalcScreenCentre();
                target.Map.ignoreNextCachedBounds = false;
            }
            catch (Exception error) { LogOnce("SPECTATOR_PLACE_CAMERA_APPLY_ERROR: " + error); }
        }

        private static bool IsMapperCameraSceneCurrent(MapperCameraTarget target) =>
            CanCorrectMapperCamera(target.Session, target.Owner) &&
            target.Map != null && target.Camera != null && target.Pixels != null &&
            ReferenceEquals(target.Map, GameMap.instance) &&
            ReferenceEquals(target.Camera, CameraControls2D.instance) &&
            ReferenceEquals(target.Pixels, PerfectPixelWithZoom.instance);

        private static unsafe bool TryReadMapperCameraTarget(MapperCameraTarget target, bool capture)
        {
            var players = GamePlayerManagerAPI.Instance;
            var buildings = GameBuildingManagerAPI.Instance;
            if (players == null || buildings == null ||
                !players.TryGetPlayerResourcesById(target.Owner, out GamePlayerResources* resources) ||
                resources == null) return false;
            uint first;
            switch ((eMappers)target.Mapper)
            {
                case eMappers.MAPPER_STORES: first = resources->r_FirstGoodsyardId; break;
                case eMappers.MAPPER_GRANARY: first = resources->r_FirstGranaryId; break;
                case eMappers.MAPPER_ARMOURY: first = resources->r_FirstArmouryId; break;
                case eMappers.MAPPER_TRADEPOST: first = resources->r_TradePostId; break;
                case eMappers.MAPPER_BARRACKS_STONE: first = resources->r_EuroMercPostId; break;
                case eMappers.MAPPER_ENGINEERS_GUILD: first = resources->r_EngineersGuildId; break;
                case eMappers.MAPPER_TUNNELERS_GUILD: first = resources->r_TunnelersGuildId; break;
                default: return false;
            }
            if (first == 0 || first > int.MaxValue ||
                (!capture && target.Id != (int)first) ||
                !global::APIShared.BuildingAccess.TryGetById(buildings, (int)first, out GameBuilding* building, out _) || building == null ||
                building->r_GlobalId == 0 || building->r_AliveState != AliveState.IsAlive ||
                building->r_PlayerIdOwner != target.Owner || building->r_BuildingType != target.Type) return false;
            int x = building->r_TilePositionXBegin + 1, y = building->r_TilePositionYBegin + 1;
            if (GameTileManagerAPI.Instance == null || !GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y)) return false;
            if (!capture) return target.GlobalId == building->r_GlobalId && target.X == x && target.Y == y;
            target.Id = (int)first; target.GlobalId = building->r_GlobalId; target.X = x; target.Y = y;
            return true;
        }

        private static bool TryGetCameraBuildingType(eMappers mapper, out eStructs type)
        {
            switch (mapper)
            {
                case eMappers.MAPPER_STORES: type = eStructs.STRUCT_GOODS_YARD; return true;
                case eMappers.MAPPER_GRANARY: type = eStructs.STRUCT_GRANARY; return true;
                case eMappers.MAPPER_ARMOURY: type = eStructs.STRUCT_ARMOURY; return true;
                case eMappers.MAPPER_TRADEPOST: type = eStructs.STRUCT_TRADEPOST; return true;
                case eMappers.MAPPER_BARRACKS_STONE: type = eStructs.STRUCT_BARRACKS_STONE; return true;
                case eMappers.MAPPER_ENGINEERS_GUILD: type = eStructs.STRUCT_ENGINEERS_GUILD; return true;
                case eMappers.MAPPER_TUNNELERS_GUILD: type = eStructs.STRUCT_TUNNELLERS_GUILD; return true;
                // Vanilla also checks partial construction here. Without a public
                // complete contract, leave the camera stationary for these families.
                case eMappers.MAPPER_BARRACKS_WOOD:
                case eMappers.MAPPER_BEDOUIN_STOCKADE: type = eStructs.STRUCT_NULL; return true;
                default: type = eStructs.STRUCT_NULL; return false;
            }
        }
    }
}
