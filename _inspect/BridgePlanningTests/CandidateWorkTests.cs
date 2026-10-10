using System;
using System.IO;
using System.Linq;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static partial class Program
    {
        private static void CandidateWorkCases()
        {
            using(var m=new NativeImage(Native,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv")))
            foreach(int generation in new[]{0,32000})foreach(int maximum in new[]{0,1,4,10})foreach(bool ladder in new[]{false,true})foreach(int initialMask in new[]{0,1})
            {
                var fixture=new Fixture(9,7);fixture.Install(m);int seed=fixture.SeedTile;
                m.Int(Root+4,generation);m.Put(0x53ad4b0,new byte[N]);m.Byte(0x53ad4b0+seed,initialMask);
                m.Put(0x5225b10,new short[N]);m.Put(Visits,Enumerable.Repeat((short)29999,N).ToArray());
                m.Put(Root+0x32be2c,new short[N]);m.Put(Root+0x155f6c,new int[N]);m.Put(Root+0x28f3ec,new short[N]);
                var managed=new VirtualCandidateBuilder();
                var ranges=new[]{Tuple.Create(Root,0xe0),Tuple.Create(Root+0x155f38,0x34),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2),Tuple.Create(Root+0x32be2c,N*2),Tuple.Create(0x5225b10,N*2),Tuple.Create(Visits,N*2),Tuple.Create(0x53ad4b0,N)};
                foreach(var r in ranges)managed.Put(r.Item1,m.Bytes(r.Item1,r.Item2));
                foreach(var r in new[]{Tuple.Create(Flags,N*4),Tuple.Create(0x3aae2a4,N*2),Tuple.Create(0x402ff2c,800*12),Tuple.Create(TileRoot,800*32),Tuple.Create(0x3a11ea4,800*800),Tuple.Create(0x2d2e50,64)})managed.Put(r.Item1,m.Bytes(r.Item1,r.Item2));
                managed.WorkFlood(seed,maximum,ladder);
                if(ladder)m.Function<V4>(0xe7530)(m.Ptr(Root),maximum,seed%W,seed/W);
                else m.Function<V5>(0xe6ae0)(m.Ptr(Root),maximum,seed%W,seed/W,1);
                foreach(var r in ranges)Equal(managed.Bytes(r.Item1,r.Item2),m.Bytes(r.Item1,r.Item2),"candidate-work-"+r.Item1.ToString("X")+"-"+generation+"-"+maximum+"-"+ladder+"-"+initialMask);
                cases++;
            }
            Console.WriteLine("PASS 32 managed/native candidate floods: queue state, early exit, selection masks, visit wrap and directions");
        }
    }
}
