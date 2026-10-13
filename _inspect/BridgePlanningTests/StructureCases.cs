using System;
using System.Linq;
using System.Runtime.InteropServices;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static partial class Program
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void StructureV6(IntPtr self,int tribe,int building,int count,int region,int control);
        private static VirtualCandidateBuilder StructureCopy(NativeImage m)
        {
            var b=new VirtualCandidateBuilder();
            foreach(var pair in new[]{Tuple.Create(Root,0x3d0000),Tuple.Create(0x48f71b0,N*4),Tuple.Create(0x51890d0,N),Tuple.Create(0x50ec690,N*2),Tuple.Create(0x5225b10,N*2),Tuple.Create(0x52c2550,N*2),Tuple.Create(0x4b6aa50,N*2),Tuple.Create(0x4ddd350,N),Tuple.Create(0x4e2b870,N),Tuple.Create(0x4e79d90,N),Tuple.Create(0x3a11ea4,640000),Tuple.Create(0x405edb0,800*32),Tuple.Create(0x402ff2c,800*12),Tuple.Create(0x2d2e50,64),Tuple.Create(0x2e6710,336*4),Tuple.Create(0x2e5360,336*4),Tuple.Create(0x2e8fd8,56),Tuple.Create(0x2e3fa0,0xb60),Tuple.Create(0x3123b0,32),Tuple.Create(0x37edf3c,36),Tuple.Create(0x8574b90,4),Tuple.Create(0x37ed4c4,4),Tuple.Create(0x67e8400,0x65c+3*0x490),Tuple.Create(0x7cc6720,0x2a+2*0x688),Tuple.Create(0x64ccbb0+0x32c,3*0x32c),Tuple.Create(0x67e8380,8)})b.Put(pair.Item1,m.Bytes(pair.Item1,pair.Item2));
            return b;
        }
        private static void StructureCases(NativeImage m)
        {
            for(int trial=0;trial<16;trial++)
            {
                var f=new Fixture(12,12);f.Install(m);m.Put(0x4b6aa50,new short[N]);m.Put(0x5225b10,new short[N]);m.Put(0x52c2550,new short[N]);m.Put(0x3a11ea4,new byte[640000]);
                for(int y=100;y<112;y++)for(int x=100;x<112;x++)m.Byte(0x3a11ea4+y*800+x,1);
                m.Put(0x67e8400,new byte[0x65c+3*0x490]);m.Put(0x7cc6720,new byte[0x2a+2*0x688]);m.Int(Root,1);m.Int(0x8574b90,trial%2);m.Int(0x37ed4c4,1);for(int p=0;p<9;p++)m.Int(0x37edf3c+p*4,p);
                int u=0x67e8400+0x490,br=0x64ccbb0+0x32c,t=103+103*W;
                m.Short(u+0x71c,100);m.Short(u+0x71e,100);m.Short(u+0x6ee,trial%8+1);m.Short(u+0x6e4,2);m.Short(u+0x6e6,5);
                m.Short(br+0x12e,trial%4==0?40:45);m.Short(br+0x132,trial%4==1?trial%8+1:0);m.Short(0x4b6aa50+t*2,1);
                m.Int(Root+4,trial%3==0?32000:1);m.Int(Root+0x44,99);m.Int(Root+0x48,88);m.Int(Root+0x4c,77);
                var b=StructureCopy(m);int found=b.FindAttackObject(1,trial%3==0?1:15);m.Function<V3>(0xEDF30)(m.Ptr(Root),1,trial%3==0?1:15);
                Check(found==m.Int(Root+0x4c),"EDF30 full selector tile");
                foreach(var pair in new[]{Tuple.Create(Root,0xe0),Tuple.Create(Root+0x155f38,0x34),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2),Tuple.Create(Root+0x32be2c,N*2),Tuple.Create(0x5225b10,N*2),Tuple.Create(0x52c2550,N*2)})Equal(b.Bytes(pair.Item1,pair.Item2),m.Bytes(pair.Item1,pair.Item2),"EDF30 complete outputs trial="+trial+" address="+pair.Item1.ToString("X"));
                int tribe=0x7cc6720+0x688;m.Short(tribe+0x40,2);m.Short(tribe+0x5a,1);m.Short(tribe+0x5c,1);m.Short(tribe+0x60,2);
                m.Short(br+0x14a,102);m.Short(br+0x14c,102);m.Int(br+0x150,102+102*W);m.Int(br+0x154,trial%3+1);m.Short(br+0x148,trial%2==0?0:16);m.Short(br+0x12e,45);m.Int(0x2e5360+45*4,trial%2==0?40:0);
                m.Put(0x4ddd350,new byte[N]);m.Put(0x4e2b870,new byte[N]);m.Put(Root+0x1b344,new byte[501*12]);
                b=StructureCopy(m);var approaches=b.BuildMeleeApproaches(1,1,trial%2==0?1:100,trial%3==0?2:1,trial%8+1);
                m.Function<StructureV6>(0xDA020)(m.Ptr(Root),1,1,trial%2==0?1:100,trial%3==0?2:1,trial%8+1);
                Equal(b.Bytes(Root+0x1b344,501*12),m.Bytes(Root+0x1b344,501*12),"DA020 complete approach/contact/sentinel output");Equal(b.Bytes(0x67e8380,8),m.Bytes(0x67e8380,8),"C0270 final offset outputs trial="+trial+" points="+approaches.Tiles.Length);
                Check(approaches.Evaluate(false,true,_=>VirtualReachability.NoRoute)==VirtualReachability.Unknown,"incoherent topology cannot reject melee");
                Check(approaches.Evaluate(true,true,_=>VirtualReachability.Reachable)==(approaches.Tiles.Length>0?VirtualReachability.Reachable:VirtualReachability.NoRoute),"valid alternative attack field remains allowed");
                cases++;
            }
            Console.WriteLine("PASS16 native/managed EDF30 and DA020 full selector/approach differential cases; no order issued; assassin surface fallback remains Unknown");
        }
    }
}
