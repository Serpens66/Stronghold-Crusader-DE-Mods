using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using APIShared.Presentation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

// These doubles replace only external storage, camera conversion and broker boundaries.
// The compiled consumer is the actual runtime source. This is not a native ABI,
// projection-math, Unity destroyed-object or in-game camera rendering test.
namespace SHCDESE.Interop.Enums { internal enum AliveState { None, NeedsInit, IsAlive, Deleted } }
namespace SHCDESE.Interop
{
    internal enum eMappers { MAPPER_NULL, MAPPER_STORES, MAPPER_GRANARY, MAPPER_ARMOURY,
        MAPPER_TRADEPOST, MAPPER_BARRACKS_STONE, MAPPER_ENGINEERS_GUILD,
        MAPPER_TUNNELERS_GUILD, MAPPER_BARRACKS_WOOD, MAPPER_BEDOUIN_STOCKADE, Unsupported }
    internal enum eStructs { STRUCT_NULL, STRUCT_GOODS_YARD, STRUCT_GRANARY, STRUCT_ARMOURY,
        STRUCT_TRADEPOST, STRUCT_BARRACKS_STONE, STRUCT_ENGINEERS_GUILD, STRUCT_TUNNELLERS_GUILD }
    internal struct GamePlayerResources { internal uint r_FirstGoodsyardId, r_FirstGranaryId,
        r_FirstArmouryId, r_TradePostId, r_EuroMercPostId, r_EngineersGuildId, r_TunnelersGuildId; }
    internal struct GameBuilding { internal uint r_GlobalId; internal AliveState r_AliveState;
        internal int r_PlayerIdOwner, r_TilePositionXBegin, r_TilePositionYBegin; internal eStructs r_BuildingType; }
}
namespace SHCDESE.API
{
    internal sealed class GameTileManagerAPI
    {
        internal static readonly GameTileManagerAPI Instance = new GameTileManagerAPI();
        internal bool Inside = true;
        internal bool IsTileInsideMapBounds(int x, int y) { return Inside; }
    }
    internal unsafe sealed class GamePlayerManagerAPI
    {
        internal static readonly GamePlayerManagerAPI Instance = new GamePlayerManagerAPI();
        internal readonly GamePlayerResources* Resources = (GamePlayerResources*)Marshal.AllocHGlobal(sizeof(GamePlayerResources));
        internal bool Readable = true;
        internal bool TryGetPlayerResourcesById(int owner, out GamePlayerResources* resources)
        { resources = Readable && owner == 3 ? Resources : null; return resources != null; }
    }
    internal unsafe sealed class GameBuildingManagerAPI
    {
        internal static readonly GameBuildingManagerAPI Instance = new GameBuildingManagerAPI();
        internal readonly GameBuilding* Building = (GameBuilding*)Marshal.AllocHGlobal(sizeof(GameBuilding));
        internal bool Throw;
        internal bool TryGetBuildingById(int id, out GameBuilding* building)
        { if (Throw) throw new InvalidOperationException("storage unavailable"); building = id == 5 ? Building : null; return building != null; }
    }
}
namespace SHCDESE
{
    internal static class CoordinateConverter
    {
        internal static Vector2 Result; internal static Vector2 Requested; internal static Action During;
        internal static Vector2 ConvertLocalTileToCameraWorld(Vector2 position)
        { Requested = position; During?.Invoke(); return Result; }
    }
}
internal sealed class GameMap
{
    internal static GameMap instance; internal bool TileAvailable = true, ignoreNextCachedBounds = true;
    internal int Recalculated;
    internal void mapGameTileToTilemapCoord(int x, int y, out int tx, out int ty) { tx = x; ty = y; }
    internal object getMapTile(int x, int y) { return TileAvailable ? this : null; }
    internal void PreCalcScreenCentre() { Recalculated++; }
}
internal sealed class CameraControls2D
{
    internal static CameraControls2D instance; internal int Moves; internal Vector2 Position;
    internal void setCameraPos(float x, float y) { Moves++; Position = new Vector2(x, y); }
}
internal sealed class PerfectPixelWithZoom { internal static PerfectPixelWithZoom instance; }
namespace APIShared
{
    internal static class PlayerPerspectiveAPI { internal static int Raw; internal static int GetRawNativeViewPlayerId() { return Raw; } }
    internal sealed class FixtureClient { internal void WhenReady(Action<object> action) { action(this); } }
    internal static class ApiShared { internal static FixtureClient ForMod(string owner) { return new FixtureClient(); } }
}
namespace APIShared.Presentation
{
    internal sealed class MapperSelectionCameraPreEventArgs
    { internal int Mapper; internal long SessionId; internal bool SuppressCamera; internal object State; }
    internal sealed class MapperSelectionCameraPostEventArgs
    { internal int Mapper, Result; internal long SessionId; internal bool SuppressionActive; internal object State; internal Exception Exception; }
    internal static class MapperSelectionCameraEvents
    {
        internal static Action<MapperSelectionCameraPreEventArgs> Pre;
        internal static Action<MapperSelectionCameraPostEventArgs> Post;
        internal static bool TryRegister(string owner, string id, Action<MapperSelectionCameraPreEventArgs> pre,
            Action<MapperSelectionCameraPostEventArgs> post, out string reason)
        { Pre = pre; Post = post; reason = null; return true; }
    }
}
namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private const string Guid = "camera-fixture";
        private static readonly object engineLock = new object();
        private static bool initialized, IsObserverBuildUiThread, placing;
        private static int selectedOwner, ownerReads;
        private static long session;
        private static readonly List<string> errors = new List<string>();
        private static bool IsCurrentSession(long value) { return value > 0 && value == session; }
        private static bool IsPlacementSession() { return placing; }
        private static int CurrentAiOwner()
        { if (!IsObserverBuildUiThread) throw new InvalidOperationException("off-thread owner read"); ownerReads++; return placing ? selectedOwner : -1; }
        private static void LogOnce(string value) { errors.Add(value); }
        private static int checks;
        private static void Check(bool value, string reason)
        { checks++; if (!value) throw new Exception(reason); }

        private static unsafe void Reset(eMappers mapper = eMappers.MAPPER_GRANARY)
        {
            initialized = IsObserverBuildUiThread = placing = true; session = 10; selectedOwner = 3; ownerReads = 0;
            APIShared.PlayerPerspectiveAPI.Raw = 1; errors.Clear();
            GameMap.instance = new GameMap(); CameraControls2D.instance = new CameraControls2D();
            PerfectPixelWithZoom.instance = new PerfectPixelWithZoom();
            SHCDESE.CoordinateConverter.Result = new Vector2(123.5f, 456.5f); SHCDESE.CoordinateConverter.During = null;
            GamePlayerManagerAPI.Instance.Readable = true;
            GameTileManagerAPI.Instance.Inside = true;
            *GamePlayerManagerAPI.Instance.Resources = default;
            var resources = GamePlayerManagerAPI.Instance.Resources;
            switch (mapper)
            {
                case eMappers.MAPPER_STORES: resources->r_FirstGoodsyardId = 5; break;
                case eMappers.MAPPER_GRANARY: resources->r_FirstGranaryId = 5; break;
                case eMappers.MAPPER_ARMOURY: resources->r_FirstArmouryId = 5; break;
                case eMappers.MAPPER_TRADEPOST: resources->r_TradePostId = 5; break;
                case eMappers.MAPPER_BARRACKS_STONE: resources->r_EuroMercPostId = 5; break;
                case eMappers.MAPPER_ENGINEERS_GUILD: resources->r_EngineersGuildId = 5; break;
                case eMappers.MAPPER_TUNNELERS_GUILD: resources->r_TunnelersGuildId = 5; break;
            }
            // This expected family table is independent of the production mapping.
            eStructs type = mapper == eMappers.MAPPER_STORES ? eStructs.STRUCT_GOODS_YARD :
                mapper == eMappers.MAPPER_ARMOURY ? eStructs.STRUCT_ARMOURY :
                mapper == eMappers.MAPPER_TRADEPOST ? eStructs.STRUCT_TRADEPOST :
                mapper == eMappers.MAPPER_BARRACKS_STONE ? eStructs.STRUCT_BARRACKS_STONE :
                mapper == eMappers.MAPPER_ENGINEERS_GUILD ? eStructs.STRUCT_ENGINEERS_GUILD :
                mapper == eMappers.MAPPER_TUNNELERS_GUILD ? eStructs.STRUCT_TUNNELLERS_GUILD : eStructs.STRUCT_GRANARY;
            GameBuildingManagerAPI.Instance.Throw = false;
            *GameBuildingManagerAPI.Instance.Building = new GameBuilding { r_GlobalId = 77,
                r_AliveState = AliveState.IsAlive, r_PlayerIdOwner = 3, r_BuildingType = type,
                r_TilePositionXBegin = 20, r_TilePositionYBegin = 30 };
        }

        private static MapperSelectionCameraPreEventArgs Capture(eMappers mapper = eMappers.MAPPER_GRANARY)
        {
            var pre = new MapperSelectionCameraPreEventArgs { Mapper = (int)mapper, SessionId = session };
            MapperSelectionCameraEvents.Pre(pre); return pre;
        }
        private static void Complete(MapperSelectionCameraPreEventArgs pre, int result = 80,
            Exception error = null, bool suppressed = true, int? mapper = null, long? world = null)
        {
            MapperSelectionCameraEvents.Post(new MapperSelectionCameraPostEventArgs {
                Mapper = mapper ?? pre.Mapper, SessionId = world ?? pre.SessionId,
                State = pre.State, Result = result, Exception = error, SuppressionActive = suppressed });
        }
        private static void RejectPost(Action mutation, string name)
        {
            Reset(); var pre = Capture(); var originalCamera = CameraControls2D.instance;
            mutation(); Complete(pre); Check(originalCamera.Moves == 0, name);
        }
        private static void RejectCapture(Action mutation, string name)
        {
            Reset(); mutation(); var pre = Capture();
            Check(pre.SuppressCamera && pre.State == null, name); Complete(pre);
            Check(CameraControls2D.instance.Moves == 0, name + " keeps camera stationary");
        }

        public static unsafe void Main()
        {
            RegisterMapperSelectionCamera();
            Check(MapperSelectionCameraEvents.Pre != null && MapperSelectionCameraEvents.Post != null, "production registration wires both callbacks");
            foreach (eMappers mapper in new[] { eMappers.MAPPER_STORES, eMappers.MAPPER_GRANARY, eMappers.MAPPER_ARMOURY,
                eMappers.MAPPER_TRADEPOST, eMappers.MAPPER_BARRACKS_STONE, eMappers.MAPPER_ENGINEERS_GUILD, eMappers.MAPPER_TUNNELERS_GUILD })
            {
                Reset(mapper); var pre = Capture(mapper);
                Check(pre.SuppressCamera && pre.State != null, "capture correct first-family ID: " + mapper);
                Complete(pre); Check(CameraControls2D.instance.Moves == 1, "successful selection centers target: " + mapper);
                Check(SHCDESE.CoordinateConverter.Requested == new Vector2(21, 31), "Vanilla building origin+1 reaches converter");
                Check(CameraControls2D.instance.Position == SHCDESE.CoordinateConverter.Result &&
                    GameMap.instance.Recalculated == 1 && !GameMap.instance.ignoreNextCachedBounds, "synchronous camera and cache update");
                Check(APIShared.PlayerPerspectiveAPI.Raw == 1, "consumer never borrows the native viewpoint");
            }
            Reset(); IsObserverBuildUiThread = false; var background = Capture();
            Check(!background.SuppressCamera && ownerReads == 0, "background call never reads UI owner state");
            Reset(); placing = false; Check(!Capture().SuppressCamera, "blocked/read-only mapper start requests no camera scope");
            Reset(); initialized = false; Check(!Capture().SuppressCamera, "partial runtime initialization remains dormant");
            Reset(); APIShared.PlayerPerspectiveAPI.Raw = 3; Check(!Capture().SuppressCamera, "already-correct native owner keeps Vanilla camera");
            Reset(); Check(!Capture(eMappers.MAPPER_NULL).SuppressCamera && !Capture(eMappers.Unsupported).SuppressCamera, "cancellation and other mapper families remain dormant");
            foreach (eMappers mapper in new[] { eMappers.MAPPER_BARRACKS_WOOD, eMappers.MAPPER_BEDOUIN_STOCKADE }) {
                Reset(mapper); var pre = Capture(mapper); Check(pre.SuppressCamera && pre.State == null, "partial-family contract suppresses wrong jump without guessing anchor");
            }
            RejectCapture(() => GamePlayerManagerAPI.Instance.Resources->r_FirstGranaryId = 0, "missing first ID");
            RejectCapture(() => GamePlayerManagerAPI.Instance.Resources->r_FirstGranaryId = uint.MaxValue, "invalid first ID");
            RejectCapture(() => GamePlayerManagerAPI.Instance.Readable = false, "unreadable resources");
            RejectCapture(() => GameTileManagerAPI.Instance.Inside = false, "out-of-map anchor");
            RejectCapture(() => GameBuildingManagerAPI.Instance.Building->r_GlobalId = 0, "invalid global ID");
            RejectCapture(() => GameBuildingManagerAPI.Instance.Building->r_AliveState = AliveState.NeedsInit, "unfinished anchor");
            RejectCapture(() => GameBuildingManagerAPI.Instance.Building->r_PlayerIdOwner = 2, "foreign anchor");
            RejectCapture(() => GameBuildingManagerAPI.Instance.Building->r_BuildingType = eStructs.STRUCT_ARMOURY, "wrong anchor type");
            RejectPost(() => session++, "replacement world");
            RejectPost(() => selectedOwner = 2, "changed selected owner");
            RejectPost(() => placing = false, "Place ended");
            RejectPost(() => GameMap.instance = new GameMap(), "replacement map");
            RejectPost(() => CameraControls2D.instance = new CameraControls2D(), "replacement camera");
            RejectPost(() => PerfectPixelWithZoom.instance = new PerfectPixelWithZoom(), "replacement zoom service");
            RejectPost(() => GamePlayerManagerAPI.Instance.Resources->r_FirstGranaryId = 6, "first ID changed");
            RejectPost(() => GameBuildingManagerAPI.Instance.Building->r_GlobalId++, "reused building slot");
            RejectPost(() => GameBuildingManagerAPI.Instance.Building->r_TilePositionXBegin++, "anchor moved");
            RejectPost(() => GameBuildingManagerAPI.Instance.Building->r_AliveState = AliveState.Deleted, "anchor deleted");
            RejectPost(() => SHCDESE.CoordinateConverter.Result = new Vector2(float.NaN, 0), "invalid conversion");
            RejectPost(() => SHCDESE.CoordinateConverter.During = () => session++, "world replaced during conversion");
            foreach (int invalid in new[] { 0, 1, 2, 3 }) {
                Reset(); var pre = Capture();
                if (invalid == 0) Complete(pre, -1);
                if (invalid == 1) Complete(pre, error: new Exception("native rejected"));
                if (invalid == 2) Complete(pre, suppressed: false);
                if (invalid == 3) Complete(pre, mapper: (int)eMappers.MAPPER_ARMOURY);
                Check(CameraControls2D.instance.Moves == 0, "blocked/error/unscoped/mismatched Post is inert");
            }
            Reset(); GameBuildingManagerAPI.Instance.Throw = true; var failed = Capture();
            Check(failed.SuppressCamera && failed.State == null && errors.Count == 1, "capture exception preserves requested suppression and reports error");
            Console.WriteLine("PASS: " + checks + " source-linked mapper camera consumer checks; no APIShared build or game installation.");
            Marshal.FreeHGlobal(new IntPtr(GameBuildingManagerAPI.Instance.Building));
            Marshal.FreeHGlobal(new IntPtr(GamePlayerManagerAPI.Instance.Resources));
        }
    }
}
