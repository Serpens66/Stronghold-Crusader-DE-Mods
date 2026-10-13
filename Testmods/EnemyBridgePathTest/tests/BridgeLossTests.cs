using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest.Tests
{
    internal static class BridgeLossTests
    {
        internal static int Run()
        {
            int checks=0;void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
            Check(BridgePlanningCapture.IsDecisionCapturePhase(4)&&BridgePlanningCapture.IsDecisionCapturePhase(5)&&!BridgePlanningCapture.IsDecisionCapturePhase(6)&&!BridgePlanningCapture.IsDecisionCapturePhase(0),"relevant phase5 capture, never silently reclassify later phases");
            foreach(int shift in new[]{0,73})
            {
                int id=7+shift,gate=8+shift,tile=15+shift,alive=2,hp=100,flags=0,pcl=1,moat=41;uint global=(uint)(70+shift);var records=new List<string>();
                int Reader(int address)
                {if(address==0x64ccbb0+id*0x32c+0x134)return (int)global;if(address==0x64ccbb0+gate*0x32c+0x134)return 80+shift;if(address==0x64ccbb0+id*0x32c+0x12c)return alive;if(address==0x64ccbb0+id*0x32c+0x12e)return 49;if(address==0x64ccbb0+gate*0x32c+0x168)return hp;if(address==0x5f011a0+tile*2)return moat;if(address==0x48f71b0+tile*4)return flags;if(address==0x50ec690+tile*2)return pcl;return 0;}
                var h=new BridgeLossHistory(Reader,(kind,detail)=>records.Add(kind+","+detail));h.Begin();var entry=new BridgeBuildingIndex.Entry{Id=id,Global=global,ParentId=gate,ParentGlobal=(uint)(80+shift),Occupied=new[]{tile}};
                h.Retain(new[]{entry});h.Sample("before");int size=records.Count;for(int i=0;i<75117;i++)h.Retain(new[]{entry});h.Sample("unchanged");Check(records.Count==size,"unchanged observations produce no duplicate definitions");
                alive=3;hp=94;h.Sample("damage-post",gate,(uint)(80+shift));Check(records.Exists(r=>r.Contains("presence=deletion-marked")),"deletion mark distinguished from cleared slot");
                h.Retain(Array.Empty<BridgeBuildingIndex.Entry>());global=0;flags=0x40000000;pcl=0;h.Sample("completed-topology-rebuild");Check(h.KnowsGate(gate,(uint)(80+shift))&&records.Exists(r=>r.Contains("presence=cleared-slot"))&&records.Exists(r=>r.Contains("/41/40000000/")),"bridge disappearance preserves gate and moat evidence after rebuild");
                hp=88;h.Sample("damage-post",gate,(uint)(80+shift));Check(records[records.Count-1].Contains("gateHp=88"),"further gate hits after last bridge loss remain observable");
                global=(uint)(90+shift);h.Sample("slot-reuse");Check(records[records.Count-1].Contains("presence=reused-slot"),"reused bridge slot not attributed to old identity");
                h.Retain(new[]{new BridgeBuildingIndex.Entry{Id=id,Global=global,ParentId=gate,ParentGlobal=(uint)(81+shift),Occupied=new[]{tile}}});Check(h.KnowsGate(gate,(uint)(81+shift))&&h.KnowsGate(gate,(uint)(80+shift)),"new coupling identity retained separately from historical gate");
                h.Begin();Check(!h.KnowsGate(gate,(uint)(80+shift)),"map change clears historical evidence");
            }
            var capped=new BridgeLossHistory(_=>0,(_,__)=>{});capped.Begin();for(int id=1;id<=BridgeLossHistory.Capacity+1;id++)capped.Retain(new[]{new BridgeBuildingIndex.Entry{Id=id,Global=(uint)id,ParentId=3999,ParentGlobal=1,Occupied=new[]{id}}});Check(capped.Status.Contains("bridgeLossCoverageComplete=False"),"historical budget overflow explicitly incomplete");
            Console.WriteLine("PASS bridge loss: mark, removal, retained moat, subsequent gate damage, unchanged repetition, renumbering and session boundary");return checks;
        }
    }
}
