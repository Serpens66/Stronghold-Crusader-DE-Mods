using System;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private unsafe Actor FindMoatWorker(int farm,int requiredState=0)
        {
            ValidateAppleWorld();
            var native=runtime.Native;
            var tiles=GameTileManagerAPI.Instance;
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_ControllableForPlayerId!=owner ||
                    unit->r_LinkedProductionBuildingId!=farm || unit->r_LinkedProductionBuildingGlobalId!=appleWorld.FarmGlobal ||
                    // Hash-bound135E30: 5 returns to the workplace through196280; 7 delivers to a granary through196280.
                    (unit->r_AIState!=5 && unit->r_AIState!=7) || requiredState!=0 && unit->r_AIState!=requiredState || unit->r_MovementSubstep!=8 ||
                    (unit->r_PathPlanStateBitFlags!=2 && unit->r_PathPlanStateBitFlags!=4))continue;
                UnitSnapshot state=runtime.SnapshotUnit(id);
                if(state==null || state.Unsupported!=null || state.Candidates.Count==0 ||
                    !MoatWorkerSourceEligible(state.TileId) ||
                    Math.Max(Math.Abs(state.RequestedX-state.X),Math.Abs(state.RequestedY-state.Y))<2)continue;
                int destination=native.Tile(state.RequestedX,state.RequestedY);
                if(destination<0 || (tiles.GetTilePropertyFlag(destination)&(TilePropertyFlag.IsMoat|TilePropertyFlag.Sea|TilePropertyFlag.River|TilePropertyFlag.Ford))!=0)continue;
                log("moat-worker-selected unit="+id+"/"+unit->r_GlobalId+" ai="+unit->r_AIState+" cargo="+unit->r_CarryGoodAmount+
                    " source="+state.X+","+state.Y+" tile="+state.TileId+" flags="+((uint)tiles.GetTilePropertyFlag(state.TileId)).ToString("X8")+
                    " building="+tiles.GetTileBuildingId(state.TileId)+" vegetation="+tiles.GetTileVegetationId(state.TileId)+" height="+tiles.GetTileHeight(state.TileId)+
                    " target="+state.RequestedX+","+state.RequestedY+" targetFlags="+((uint)tiles.GetTilePropertyFlag(destination)).ToString("X8"));
                return new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                    Tribe=unit->r_TribeId,X=state.X,Y=state.Y};
            }
            return null;
        }

        private bool MoatWorkerSourceEligible(int tile)
        {
            var tiles=GameTileManagerAPI.Instance;
            if(tile<0 || !runtime.Native.OrdinaryGround(tile))return false;
            // Hash-bound6FE90(param7=2): use the strict vegetation==0 branch and all tile rejection bits.
            // Farm/tree variants are excluded additionally to keep this seam outside the natural orchard.
            TilePropertyFlag excluded=TilePropertyFlag.RealityEdge|TilePropertyFlag.IsBuilding|TilePropertyFlag.IsElevated|
                TilePropertyFlag.IsWall|TilePropertyFlag.Sea|TilePropertyFlag.River|TilePropertyFlag.Ford|TilePropertyFlag.ImpassableEdge|
                TilePropertyFlag.HasStone|TilePropertyFlag.HasIron|TilePropertyFlag.IsSwamp|TilePropertyFlag.IsFarm|TilePropertyFlag.IsMoat|
                TilePropertyFlag.IsTree|TilePropertyFlag.IsAppleFarm|TilePropertyFlag.IsFarmFence|TilePropertyFlag.IsWheat|TilePropertyFlag.IsHops;
            return (tiles.GetTilePropertyFlag(tile)&excluded)==0 && tiles.GetTileBuildingId(tile)==0 &&
                tiles.GetTileVegetationId(tile)==0 && tiles.GetTileHeight(tile)<13 && MoatWorkerWithinOwnKeepRange(tile);
            // Native59210 remains authoritative; the fixture still requires the real writer to accept the tile.
        }

        private bool MoatWorkerWithinOwnKeepRange(int tile)
        {
            if(APIShared.GameModes.GameModeHelper.IsMapEditor())return true;
            var tiles=GameTileManagerAPI.Instance;
            if(!tiles.IsValidTileId(tile))return false;
            var buildings=GameBuildingManagerAPI.Instance;
            int range=buildings.GetKeepProximityRange(tiles.GetCurrentMapSize());
            int overrideRange=buildings.KeepProximityOverride.GetValue();
            if(overrideRange>0)range=overrideRange;
            if(range<0 || range>int.MaxValue-5)return false;
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            var point=tiles.GetTileVectorFromId(tile);
            //6FE90->EE320->EEF90 allows range+5+width/2 from the own Keep.
            // Keep four tiles of margin for brush extent/anchor conventions; no
            // allied half-range exception is needed for this sufficient filter.
            int safeRange=range+1;
            return Math.Max(Math.Abs((long)point.X-keep.X),Math.Abs((long)point.Y-keep.Y))<=safeRange;
        }

    }
}
