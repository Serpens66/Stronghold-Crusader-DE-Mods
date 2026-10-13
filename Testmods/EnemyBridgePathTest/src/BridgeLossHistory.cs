using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
namespace EnemyBridgePathTest
{
    // Historical evidence only. Never supplied to live reachability or authorization.
    internal sealed class BridgeLossHistory
    {
        internal sealed class Identity
        {
            internal int Id,Gate;internal uint Global,GateGlobal;internal int[] Tiles;
            internal int[] LastState,LastCells;internal long Definition,FieldsClock;
        }
        private readonly Func<int,int> read;private readonly Action<string,string> emit;private readonly object gate=new object();
        private readonly List<Identity> identities=new List<Identity>();private long definition,observations,changes,overflow,readTicks,formatTicks;
        internal const int Capacity=128;
        internal BridgeLossHistory(Func<int,int> read,Action<string,string> emit){this.read=read;this.emit=emit;}
        internal void Begin(){lock(gate){identities.Clear();definition=observations=changes=overflow=readTicks=formatTicks=0;}}
        internal string Status{get{lock(gate)return "bridgeLossIdentities="+identities.Count+",bridgeLossObservations="+observations+",bridgeLossChanges="+changes+",bridgeLossOverflow="+overflow+",bridgeLossReadMs="+Ms(readTicks)+",bridgeLossFormatMs="+Ms(formatTicks)+",bridgeLossCoverageComplete="+(overflow==0);}}
        private static string Ms(long ticks)=>(ticks*1000.0/Stopwatch.Frequency).ToString("F3",System.Globalization.CultureInfo.InvariantCulture);
        internal bool KnowsGate(int id,uint global){lock(gate){foreach(var h in identities)if(h.Gate==id&&h.GateGlobal==global)return true;return false;}}
        internal void Retain(BridgeBuildingIndex.Entry[] entries)
        {lock(gate)RetainCore(entries);}
        private void RetainCore(BridgeBuildingIndex.Entry[] entries)
        {
            foreach(var e in entries)
            {
                if(e.Global==0||e.ParentId<1||e.ParentGlobal==0||e.Occupied==null||e.Occupied.Length==0)continue;
                bool found=false;foreach(var h in identities)if(h.Id==e.Id&&h.Global==e.Global&&h.Gate==e.ParentId&&h.GateGlobal==e.ParentGlobal&&Same(h.Tiles,e.Occupied)){found=true;break;}if(found)continue;
                if(identities.Count>=Capacity){if(overflow++==0)emit("bridge-loss-gap","reason=historical-identity-capacity,capacity="+Capacity+",negativeEvidence=unknown");continue;}
                identities.Add(new Identity{Id=e.Id,Global=e.Global,Gate=e.ParentId,GateGlobal=e.ParentGlobal,Tiles=(int[])e.Occupied.Clone()});
            }
        }
        internal void Sample(string stage,int gate=0,uint global=0)
        {lock(this.gate)SampleCore(stage,gate,global);}
        private void SampleCore(string stage,int gate,uint global)
        {
            foreach(var h in identities)
            {
                if(gate!=0&&(h.Gate!=gate||h.GateGlobal!=global))continue;
                observations++;long captureClock=Stopwatch.GetTimestamp();int b=0x64ccbb0+h.Id*0x32c,g=0x64ccbb0+h.Gate*0x32c;
                uint actual=unchecked((uint)read(b+0x134)),actualGate=unchecked((uint)read(g+0x134));
                int[] state={unchecked((int)actual),read(b+0x12c)&65535,read(b+0x12e)&65535,read(b+0x120)&65535,
                    unchecked((int)actualGate),read(g+0x12c)&65535,actualGate==h.GateGlobal?(short)read(g+0x168):int.MinValue,
                    read(0x60ad6cc),read(0x60ad6d4)};
                var cells=new int[h.Tiles.Length*5];for(int i=0;i<h.Tiles.Length;i++)
                {
                    int tile=h.Tiles[i];if((uint)tile>=320800)throw new InvalidOperationException("historical-deck-tile-outside-grid");int at=i*5;
                    cells[at]=tile;cells[at+1]=read(0x50ec690+tile*2)&65535;cells[at+2]=read(0x5f011a0+tile*2)&65535;
                    cells[at+3]=read(0x48f71b0+tile*4);cells[at+4]=read(0x51890d0+tile)&255;
                }
                readTicks+=Stopwatch.GetTimestamp()-captureClock;
                bool sameCells=Same(h.LastCells,cells);if(sameCells&&SameState(h.LastState,state))continue;changes++;
                long formatStart=Stopwatch.GetTimestamp();
                if(!sameCells)
                {
                    h.Definition=++definition;var rows=new StringBuilder();for(int i=0;i<cells.Length;i+=5)rows.Append(cells[i]).Append('/').Append(cells[i+1]).Append('/').Append(cells[i+2]).Append('/').Append(unchecked((uint)cells[i+3]).ToString("X8")).Append('/').Append(cells[i+4]).Append(';');
                    h.FieldsClock=captureClock;emit("bridge-loss-fields","definition="+h.Definition+",building="+h.Id+"/g"+h.Global+",captureClock="+h.FieldsClock+",columns=tile/pcl/moat/flags/direction,cells=["+rows+"],timing=synchronous-historical-footprint,reachabilityProof=not-established");h.LastCells=cells;
                }
                string presence=actual!=h.Global?(actual==0?"cleared-slot":"reused-slot"):state[1]==3?"deletion-marked":state[1]==0?"inactive-slot":"same-identity";
                emit("bridge-loss-state","building="+h.Id+"/g"+h.Global+",gate="+h.Gate+"/g"+h.GateGlobal+",stage="+stage+",fieldsDefinition="+h.Definition+",slotGlobal="+actual+",presence="+presence+",aliveRaw="+state[1]+",typeRaw="+state[2]+",keepMoatRaw="+state[3]+",gateIdentityMatches="+(actualGate==h.GateGlobal)+",gateAliveRaw="+state[5]+",gateHp="+state[6]+",dirty="+state[7]+",nativeGeneration="+state[8]+",accessLoss=not-inferred-from-presence,physicsTiming=current-observation,captureClock="+captureClock+",fieldCaptureClock="+h.FieldsClock+",coherence=scalar-reads-not-atomic,negativePolicyEligible=False");h.LastState=state;
                formatTicks+=Stopwatch.GetTimestamp()-formatStart;
            }
        }
        private static bool Same(int[] a,int[] b){if(a==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private static bool SameState(int[] a,int[] b){if(a==null||a.Length!=b.Length)return false;for(int i=0;i<7;i++)if(a[i]!=b[i])return false;return true;}
    }
}
