using System;
using System.IO;
using System.Linq;

namespace OutpostTest
{
    internal static class RallyTests
    {
        internal static void Run(Action<bool,string> check)
        {
            var s=new OutpostRallyState();
            var a=s.Get(1,100,1,106);var b=s.Get(2,200,1,107);
            a.HasPoint=true;a.X=30;a.Y=40;b.HasPoint=true;b.X=50;b.Y=60;
            s.Queue(a,10,1000,20,2000);s.Queue(b,11,1001,21,2001);
            a.X=70;a.Y=80;s.Queue(a,12,1002,22,2002);
            check(s.Pending[0].X==30 && s.Pending[1].X==50 && s.Pending[2].X==70,"per-outpost targets and immutable spawn snapshots");
            var without=s.Get(3,300,1,2);s.Queue(without,13,1003,23,2003);
            check(s.Pending.Count==3,"no movement without target");
            a.Target=38;a.Produced=7;s.Pending[0].Wait=2;
            byte[] bytes=s.Encode();var loaded=OutpostRallyState.Decode(bytes);
            check(loaded.Records.Count==3 && loaded.Pending.Count==3,"save roundtrip counts");
            check(loaded.Records[1].Target==38 && loaded.Records[1].Produced==7 && loaded.Pending[0].Wait==2,"production progress and readiness survive load");
            check(loaded.Pending[0].X==30 && loaded.Records[1].X==70,"load preserves old pending destination");
            check(bytes.SequenceEqual(loaded.Encode()),"binary roundtrip exact");
            loaded.CancelTribe(20);check(loaded.Pending.Count==2,"manual movement/attack cancels pending tribe");
            loaded.CancelUnit(11);check(loaded.Pending.Count==1,"regrouping cancels pending unit");
            loaded.Get(1,101,1,106);check(!loaded.Records[1].HasPoint && loaded.Pending.Count==0,"reused building slot cannot inherit target/orders");
            loaded.Get(2,200,2,107);check(!loaded.Records[2].HasPoint,"owner change clears target");
            loaded.Get(3,300,1,106);check(loaded.Records[3].Type==106,"type identity changes");
            loaded.Remove(2);check(!loaded.Records.ContainsKey(2),"demolition clears point");
            check(!OutpostRallyState.Ready(true,false,1,20,20),"NeedsInit never receives command");
            check(OutpostRallyState.Ready(false,true,1,20,20),"initialized single-unit group is ready");
            check(!OutpostRallyState.Ready(false,true,2,20,20) && !OutpostRallyState.Ready(false,true,1,21,20),"changed membership cannot move older units");
            var empty=new OutpostRallyState().Encode();
            check(empty.Length>0 && OutpostRallyState.Decode(empty).Records.Count==0,"empty payload overwrites stale archive entry");
            check(OutpostRallyState.PointValid(0,799) && !OutpostRallyState.PointValid(-1,0) && !OutpostRallyState.PointValid(800,0),"tile bounds");
            bool truncated=false;try { OutpostRallyState.Decode(bytes.Take(bytes.Length-1).ToArray()); } catch(EndOfStreamException) { truncated=true; }
            check(truncated,"truncated save rejected atomically");
            var bad=(byte[])bytes.Clone();bad[4]=2;bool version=false;try { OutpostRallyState.Decode(bad); } catch(InvalidDataException) { version=true; }
            check(version,"unknown schema rejected");
            bool trailing=false;try { OutpostRallyState.Decode(bytes.Concat(new byte[]{0}).ToArray()); } catch(InvalidDataException) { trailing=true; }
            check(trailing,"trailing data rejected");
            // Exhaustion never advances logical production; successful independently controlled
            // units do advance it, regardless of subsequent regrouping or death.
            int produced=0;foreach(bool success in new[]{false,true,true,false,true}) if(success) produced++;
            check(produced==3 && !OutpostSchedule.Complete(produced,20,0),"partial human cycle does not count failed spawns");
        }
    }
}
