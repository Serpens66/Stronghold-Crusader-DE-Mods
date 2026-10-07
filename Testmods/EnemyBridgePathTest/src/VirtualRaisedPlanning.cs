using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Publication stays private until every copied component is rebuilt. Caller
    // supplies native temporary gate-closure edges and concrete macro endpoints.
    internal sealed class VirtualRaisedPlanning
    {
        private readonly VirtualPlanningInput input;
        private readonly int[] deck;
        private readonly VirtualBridgeMap source;
        internal readonly VirtualTopologyRebuild Rebuild;
        internal VirtualPlanningInput Planning {get;private set;}
        internal VirtualBridgeMap Topology {get;private set;}
        internal VirtualRaisedPlanning(VirtualPlanningInput input,VirtualBridgeMap source,int[] deck,int[] rowEnds)
        {
            this.input=input;this.source=source;this.deck=(int[])deck.Clone();
            var raised=input.Raised(this.deck,input.Components);
            var map=new VirtualBridgeMap(source.Session,source.Revision,source.Components,raised.ClosedGateEdges,raised.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
            Rebuild=new VirtualTopologyRebuild(map,input.Offsets,rowEnds);
        }
        internal void Step(int budget)
        {
            Rebuild.Step(budget);if(Planning!=null||!Rebuild.Complete||!Rebuild.Proven)return;
            Planning=input.Raised(deck,Rebuild.Components);
            // VirtualConnection endpoints are concrete tiles, not old PCL IDs.
            // Their new labels are always read from this publication's grid.
            Topology=new VirtualBridgeMap(source.Session,source.Revision,Rebuild.Components,Planning.ClosedGateEdges,Planning.Flags,source.X,source.Y,source.RowStarts,source.Connections,source.Complete,source.SpecialIds,source.SpecialKinds);
        }
    }
}
