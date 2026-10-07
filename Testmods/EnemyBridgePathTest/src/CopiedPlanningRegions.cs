using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // E2610 boolean oracle for D9190 only. Copied native permissions, not effective
    // Gate policy. C4BF0(1) disables the specific connection ID for each live gate.
    internal static class CopiedPlanningRegions
    {
        internal static bool TryCreate(BridgePlanningCapture.Bundle bundle,string stage,bool temporaryClosure,int player,int mode,out Func<int,int,int?> oracle,out string reason)
        {
            oracle=null;reason="missing-region-inputs";
            try
            {
                byte[] records=CopiedPlanningBundle.Resolve(bundle,stage+"/macroRecords"),building=CopiedPlanningBundle.Resolve(bundle,"buildingRecords"),gates=CopiedPlanningBundle.Resolve(bundle,"gateConnectionIds"),alliances=CopiedPlanningBundle.Resolve(bundle,"nativeAlliances9");
                int limit=BitConverter.ToInt32(CopiedPlanningBundle.Resolve(bundle,stage+"/macroLimit"),0);
                if(limit<1||limit>1000||records.Length!=(limit-1)*0x204||building.Length%0x32c!=0||gates.Length!=building.Length/0x32c*2||alliances.Length!=36||player<0||player>8||mode<0||mode>2)throw new ArgumentException("region-copy-extent");
                var closed=new HashSet<int>();
                if(temporaryClosure)for(int at=0;at<building.Length;at+=0x32c)
                {int kind=BitConverter.ToUInt16(building,at+0x12e);if(BitConverter.ToInt16(building,at+0x12c)!=0&&kind>=45&&kind<=47){int id=BitConverter.ToInt16(gates,at/0x32c*2);if(id<0||id>=limit)throw new ArgumentException("gate-connection-id");closed.Add(id);}}
                var links=new List<VirtualComponentLink>();
                for(int id=1;id<limit;id++)
                {
                    int at=(id-1)*0x204;
                    if(BitConverter.ToInt32(records,at)!=1||BitConverter.ToInt32(records,at+24)==0||closed.Contains(id))continue;
                    int owner=BitConverter.ToInt32(records,at+0x1e4),buildingId=BitConverter.ToInt32(records,at+12),kind=BitConverter.ToInt32(records,at+4);
                    if(owner<0||owner>8||buildingId<1||buildingId>building.Length/0x32c)throw new ArgumentException("region-role-identity");
                    bool eligible=player==0||BitConverter.ToInt32(alliances,owner*4)==BitConverter.ToInt32(alliances,player*4)||BitConverter.ToInt16(building,(buildingId-1)*0x32c+0x322)!=0;
                    if(eligible&&(kind!=1||mode!=0)&&(kind==1||mode!=2))links.Add(new VirtualComponentLink(BitConverter.ToInt32(records,at+52),BitConverter.ToInt32(records,at+56),BitConverter.ToInt32(records,at+0x1e8),kind,true));
                }
                // Native local eligible list has 200 entries. No extrapolated negative.
                if(links.Count>200)throw new ArgumentException("native-region-local-capacity");
                var endpoints=new HashSet<int>();foreach(var link in links)foreach(int endpoint in new[]{link.A,link.B,link.C}){if(endpoint<0||endpoint>999)throw new ArgumentException("native-region-endpoint");if(endpoint!=0)endpoints.Add(endpoint);}if(endpoints.Count>400)throw new ArgumentException("native-region-queue-capacity");
                var frozen=links.ToArray();oracle=(from,to)=>{if(from==to)return from;var result=VirtualComponentControl.Evaluate(from,to,mode,frozen);return result==VirtualReachability.Unknown?(int?)null:result==VirtualReachability.Reachable?1:0;};reason="copied-native-region-boolean:"+(temporaryClosure?"temporarily-closed":"observed");return true;
            }
            catch(Exception error){reason="unknown-region-inputs:"+error.GetType().Name+":"+error.Message;return false;}
        }
    }
}
