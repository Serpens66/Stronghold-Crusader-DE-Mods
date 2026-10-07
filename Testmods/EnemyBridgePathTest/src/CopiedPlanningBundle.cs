using System;
namespace EnemyBridgePathTest
{
    // Productive offline adapter for schema2 planning sections. Historical schema1
    // has no such sections and cannot be reconstructed by defaulting arrays.
    internal static class CopiedPlanningBundle
    {
        private static T[] ArrayOf<T>(byte[] data,int width) where T:struct
        {if(data.Length%width!=0)throw new ArgumentException("Planning array width");var values=new T[data.Length/width];Buffer.BlockCopy(data,0,values,0,data.Length);return values;}
        private static void CopyExact(byte[] bytes,Array destination,int width)
        {if(bytes.Length!=destination.Length*width)throw new ArgumentException("Planning array capacity");Buffer.BlockCopy(bytes,0,destination,0,bytes.Length);}
        internal static byte[] Resolve(BridgePlanningCapture.Bundle bundle,string name)
        {
            if(bundle.Sections.TryGetValue(name,out byte[] value))return value;
            if(bundle.References.TryGetValue(name,out string target)&&bundle.Sections.TryGetValue(target,out value))return value;
            throw new ArgumentException("Missing planning definition: "+name);
        }
        internal static bool TryRead(BridgePlanningCapture.Bundle bundle,VirtualBridgeMap geometry,string stage,out VirtualPlanningInput input,out VirtualPlanningState state,out string reason)
        {
            input=null;state=null;reason="missing-historical-planning-values";
            if(bundle==null||!bundle.Complete||geometry==null||bundle.Session!=geometry.Session)return false;
            try
            {
                int[] flags=ArrayOf<int>(Resolve(bundle,"flags"),4),offsets=ArrayOf<int>(Resolve(bundle,"directionOffsets"),4),rowRecords=ArrayOf<int>(Resolve(bundle,"rowRecords"),4);
                ushort[] components=ArrayOf<ushort>(Resolve(bundle,"components"),2),ids=ArrayOf<ushort>(Resolve(bundle,"buildingIds"),2);
                var physical=new VirtualBridgeMap(geometry.Session,geometry.Revision,components,Resolve(bundle,"edges"),flags,geometry.X,geometry.Y,geometry.RowStarts,geometry.Connections,geometry.Complete,geometry.SpecialIds,geometry.SpecialKinds);
                byte[] closed,blocks;ushort[] types;
                if(!VirtualPlanningBuildings.TryPrepare(physical,Resolve(bundle,"buildingRecords"),ids,out closed,out types,out blocks,out reason))return false;
                if(rowRecords.Length!=2400)throw new ArgumentException("Row-record capacity");var rows=new int[800];for(int i=0;i<800;i++)rows[i]=rowRecords[i*3];
                byte[] coarseRecords=Resolve(bundle,"coarseRecords");if(coarseRecords.Length!=25600*48)throw new ArgumentException("Coarse-record capacity");var coarse=new sbyte[25600];for(int i=0;i<coarse.Length;i++)coarse[i]=unchecked((sbyte)coarseRecords[i*48]);
                input=new VirtualPlanningInput(flags,rows,offsets,components,types,ArrayOf<short>(Resolve(bundle,"tileRows"),2),closed,coarse,blocks);
                state=new VirtualPlanningState();
                CopyExact(Resolve(bundle,stage+"/seeds"),state.Seeds,1);
                CopyExact(Resolve(bundle,stage+"/distance"),state.Distance,2);
                CopyExact(Resolve(bundle,stage+"/visits"),state.Visit,2);
                CopyExact(Resolve(bundle,stage+"/queue"),state.Queue,4);
                CopyExact(Resolve(bundle,stage+"/queueRows"),state.QueueY,2);
                int[] controls=ArrayOf<int>(Resolve(bundle,stage+"/controls"),4),q=ArrayOf<int>(Resolve(bundle,stage+"/queueControls"),4);
                if(controls.Length!=0xe0/4||q.Length!=0x34/4)throw new ArgumentException("Control extent");
                state.Generation=controls[1];state.MaxDistance=controls[0x8c/4];state.Level=q[0];state.Head=q[1];state.BatchStart=q[2];state.Tail=q[3];state.BatchEnd=q[4];
                int[] candidates=ArrayOf<int>(Resolve(bundle,stage+"/candidates8"),4);int count=BitConverter.ToInt32(Resolve(bundle,stage+"/candidateCount"),0);
                if(count<0||count>1000||candidates.Length!=count*2)throw new ArgumentException("Candidate extent");
                for(int i=0;i<candidates.Length;i+=2)state.Candidates.Add(candidates[i]);reason="complete-copied-stage";return true;
            }
            catch(Exception error){input=null;state=null;reason="invalid-planning-bundle:"+error.GetType().Name;return false;}
        }
    }
}
