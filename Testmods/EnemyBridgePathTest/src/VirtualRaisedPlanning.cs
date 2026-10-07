using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Publication stays private until every copied component is rebuilt. Caller
    // supplies copied physical edges and concrete macro endpoints.
    internal sealed class VirtualRaisedPlanning
    {
        private readonly VirtualPlanningInput input;
        private readonly int[] deck;
        private readonly VirtualBridgeMap source;
        private readonly byte[] buildings;
        private readonly ushort[] buildingIds;
        private readonly VirtualPlanningInput physical;
        internal VirtualBridgeMap Physical {get;private set;}
        internal readonly VirtualTopologyRebuild Rebuild;
        internal VirtualPlanningInput Planning {get;private set;}
        internal VirtualBridgeMap Topology {get;private set;}
        internal VirtualRaisedPlanning(VirtualPlanningInput input,VirtualBridgeMap source,int[] deck,int[] rowEnds,byte[] buildings=null,ushort[] buildingIds=null)
        {
            this.input=input;this.source=source;this.deck=(int[])deck.Clone();
            if((buildings==null)!=(buildingIds==null))throw new ArgumentException("building-copy-pair");
            this.buildings=buildings==null?null:(byte[])buildings.Clone();this.buildingIds=buildingIds==null?null:(ushort[])buildingIds.Clone();
            // The legacy pure raise fixture supplies already-closed global edges.
            // Real replay starts from physical edges and reapplies C4BF0(1) on its own copy.
            var physicalInput=new VirtualPlanningInput(input.Flags,input.Rows,input.Offsets,input.Components,input.BuildingTypes,input.Y,source.Edges,input.Coarse,input.BuildingBlocks);
            physical=physicalInput.Raised(this.deck,input.Components);
            byte[] globalEdges=physical.ClosedGateEdges;
            if(buildings!=null)
            {
                var raisedMap=new VirtualBridgeMap(source.Session,source.Revision,source.Components,globalEdges,physical.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
                ushort[] types;byte[] blocks;string reason;
                if(!VirtualPlanningBuildings.TryPrepare(raisedMap,this.buildings,this.buildingIds,out globalEdges,out types,out blocks,out reason))throw new ArgumentException(reason);
            }
            var map=new VirtualBridgeMap(source.Session,source.Revision,source.Components,globalEdges,physical.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
            Rebuild=new VirtualTopologyRebuild(map,input.Offsets,rowEnds);
        }
        internal void Step(int budget)
        {
            Rebuild.Step(budget);if(Planning!=null||!Rebuild.Complete||!Rebuild.Proven)return;
            Physical=new VirtualBridgeMap(source.Session,source.Revision,Rebuild.Components,physical.ClosedGateEdges,physical.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
            byte[] closed=physical.ClosedGateEdges,blocks=input.BuildingBlocks;ushort[] types=input.BuildingTypes;string reason;
            if(buildings!=null&&!VirtualPlanningBuildings.TryPrepare(Physical,buildings,buildingIds,out closed,out types,out blocks,out reason))throw new ArgumentException(reason);
            Planning=new VirtualPlanningInput(physical.Flags,input.Rows,input.Offsets,Rebuild.Components,types,input.Y,closed,input.Coarse,blocks);
            // VirtualConnection endpoints are concrete tiles, not old PCL IDs.
            // Their new labels are always read from this publication's grid.
            Topology=new VirtualBridgeMap(source.Session,source.Revision,Rebuild.Components,Planning.ClosedGateEdges,Planning.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
        }
    }
}
