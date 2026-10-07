using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static class Program
    {
        private const string Native="E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll";
        internal const int Root=0x60AD660,TileRoot=0x405EDB0,Flags=0x48F71B0,Pcl=0x50EC690,Edges=0x51890D0,Seeds=0x535EF90,Visits=0x52C2550,Distances=0x5759230,N=320800,W=401;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V2(IntPtr self,int p);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V3(IntPtr self,int a,int b);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V4(IntPtr self,int a,int b,int c);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void V5(IntPtr self,int a,int b,int c,int d);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate long R2(IntPtr self,int p);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int R5(IntPtr self,int player,int start,int target,int mode);
        private static int checks,cases;
        private static void Check(bool v,string text){checks++;if(!v)throw new Exception(text);}
        private static void Equal<T>(T[] a,T[] b,string field)
        {Check(a.Length==b.Length,field+" capacity");for(int i=0;i<a.Length;i++)if(!EqualityComparer<T>.Default.Equals(a[i],b[i]))throw new Exception(field+" mismatch at "+i+": native="+a[i]+",managed="+b[i]);checks++;}
        private sealed class Fixture
        {
            internal int[] Flag=new int[N],Rows=new int[800],Offsets=new int[6400];internal short[] Y=new short[N];
            internal ushort[] Component=new ushort[N],Type=new ushort[N];internal byte[] Edge=new byte[N],Block=new byte[N];internal sbyte[] Coarse=new sbyte[25600];
            internal int SeedTile=100*W+100;internal VirtualPlanningState State=new VirtualPlanningState();
            internal VirtualPlanningInput Input()=>new VirtualPlanningInput(Flag,Rows,Offsets,Component,Type,Y,Edge,Coarse,Block);
            internal Fixture(int width,int height)
            {
                for(int y=0;y<800;y++){Rows[y]=y*W;for(int x=0;x<W;x++){int t=y*W+x;Y[t]=(short)y;Flag[t]=0x31;}for(int d=0;d<8;d++)Offsets[y*8+d]=VirtualBridgeMap.Dy[d]*W+VirtualBridgeMap.Dx[d];}
                for(int y=100;y<100+height;y++)for(int x=100;x<100+width;x++)
                {int t=y*W+x;Flag[t]=0;Component[t]=1;for(int d=0;d<8;d++){int xx=x+VirtualBridgeMap.Dx[d],yy=y+VirtualBridgeMap.Dy[d];if(xx>=100&&xx<100+width&&yy>=100&&yy<100+height)Edge[t]|=(byte)(1<<d);}}
            }
            internal void Install(NativeImage image,bool active=true)
            {
                image.Put(Flags,Flag);image.Put(Edges,Edge);image.Put(Pcl,Component.Select(v=>unchecked((short)v)).ToArray());image.Put(0x3AAE2A4,Y);
                image.Put(Seeds,State.Seeds.Select(v=>unchecked((byte)v)).ToArray());image.Put(Visits,State.Visit);image.Put(Distances+2*N*2,State.Distance);
                image.Put(Root+0x155f6c,State.Queue);image.Put(Root+0x28f3ec,State.QueueY);image.Int(Root+4,State.Generation);
                foreach(int at in new[]{0x155f38,0x155f3c,0x155f40,0x155f44,0x155f48,0x8c,0xc4})image.Int(Root+at,0);
                image.Int(0x64CCBB0+0x50,1);image.Int(0x64CCBB0+0x319874,0);image.Int(Root,1);image.Int(0x405B4F0,0);
                image.Int(0x379AE64+0x583c,active?1:0);image.Int(0x379AFA8+0x583c,SeedTile%W);image.Int(0x379AFAC+0x583c,SeedTile/W);image.Int(0x2EA70DC+8*0x177bc,2);
                for(int y=-1;y<=800;y++){image.Int(0x402FF2C+y*12,y*W);image.Int(0x402FF34+y*12,(y+1)*W);}
                image.Put(0x405EDB0,Offsets);for(int i=0;i<Coarse.Length;i++)image.Byte(0x35057AF+i*48,Coarse[i]);
                // Building identities for synthetic gate-overridden tile admission.
                var ids=new short[N];for(int i=0;i<Type.Length;i++)if(Type[i]!=0){ids[i]=3;image.Short(0x64CCCDE+3*0x32c,Type[i]);image.Byte(0x64CCEB0+3*0x32c,Block[i]);}image.Put(0x4B6AA50,ids);
            }
        }
        private static void Compare(NativeImage m,VirtualPlanningState s,bool seed,int bank=2)
        {
            Equal(m.Bytes(Seeds,N),s.Seeds.Select(v=>unchecked((byte)v)).ToArray(),"seed/weight field");
            Equal(m.Ints(Root+0x155f6c,N),s.Queue,"entire tile queue");Equal(m.Shorts(Root+0x28f3ec,N),s.QueueY,"entire row queue");
            Check(m.Int(Root+0x155f38)==s.Level,"level");Check(m.Int(Root+0x155f3c)==s.Head,"head");Check(m.Int(Root+0x155f44)==s.Tail,"tail");
            if(seed){Check(m.Int(Root+0x155f40)==s.BatchStart,"batch start");Check(m.Int(Root+0x155f48)==s.BatchEnd,"batch end");}
            else {Equal(m.Shorts(Distances+bank*N*2,N),s.Distance,"distance bank2");Equal(m.Shorts(Visits,N),s.Visit,"visit stamps");Check(m.Int(Root+4)==s.Generation,"visit generation");Check(m.Int(Root+0x8c)==s.MaxDistance,"maximum distance");Check(m.Int(0x405B4F0)==s.Candidates.Count,"candidate count");for(int i=0;i<s.Candidates.Count;i++)Check(m.Int(0x405B4F4+i*8)==s.Candidates[i],"candidate order");}
        }
        private static void Callers(NativeImage m)
        {
            var build=m.Function<V3>(0x2D250);var select=m.Function<V3>(0x2C5A0);
            foreach(int target in new[]{1,2,7,8})foreach(int strength in new[]{90,91,120,121})foreach(int mode in new[]{0,1})
            {
                var f=new Fixture(12,12);f.Install(m);int attacker=target==8?1:8,castle=f.SeedTile+W+1;
                m.Int(0x379D9A8+attacker*0x583c,target);m.Int(0x379E768+target*0x583c,strength);
                m.Int(0x379AE64+attacker*0x583c,1);m.Int(0x379AE64+target*0x583c,1);
                m.Int(0x379AFB0+attacker*0x583c,f.SeedTile);m.Int(0x379AFB0+target*0x583c,castle);
                m.Int(0x379AFA8+target*0x583c,castle%W);m.Int(0x379AFAC+target*0x583c,castle/W);
                m.Put(Distances+target*N*2,new short[N]);
                build(m.Ptr(0x366C210),attacker,mode);
                var call=new VirtualPlanningCall(attacker,target,mode,strength,1,true);
                Check(m.Int(0x2EA70DC+attacker*0x177bc)==call.Bank,"2D250 actual target distance bank");
                Check(VirtualPlanningCaller.Run(f.Input(),f.State,call,castle,true,(c,d)=>c==d?d:0),"productive full planning caller");Compare(m,f.State,false,target);
                int chosen=f.SeedTile+2;m.Int(0x379D968+attacker*0x583c,chosen);m.Int(Root+0xe0+4,144);
                select(m.Ptr(0x366C210),attacker,target);
                var counts=new int[1000];counts[1]=144;var selection=new VirtualTargetSelection(f.Input(),f.State,chosen,castle,counts);
                Check(m.Int(0x2E97EAC)==selection.SeedMode&&m.Int(0x2E97EA8)==selection.Seed&&m.Int(0x2E97D10)==selection.Region&&m.Int(0x2E9CA14)==selection.Count&&m.Int(0x2E97D14)==selection.TargetCastleRegion,"2C5A0 all scalar outputs");cases++;
            }
            Console.WriteLine("PASS native complete 2D250/2C5A0: 32 target/bank/mode/radius-boundary cases");
            foreach(int dirty in new[]{0,1})
            {
                var f=new Fixture(12,12);f.Install(m);int castle=f.SeedTile+W+1;
                m.Int(0x379D9A8+8*0x583c,1);m.Int(0x379E768+0x583c,90);m.Int(0x379AE64+8*0x583c,1);m.Int(0x379AE64+0x583c,1);m.Int(0x379AFB0+8*0x583c,f.SeedTile);m.Int(0x379AFB0+0x583c,castle);m.Int(0x379AFA8+0x583c,castle%W);m.Int(0x379AFAC+0x583c,castle/W);m.Put(Distances+N*2,new short[N]);m.Int(Root+0x6c,dirty);m.Int(Root+0x74,4);
                build(m.Ptr(0x366C210),8,1);Check(m.Int(Root+0x6c)==dirty&&m.Int(Root+0x74)==4,"full native2D250 plans with unchanged Dirty/revision and no rebuild");
                Check(VirtualPlanningCaller.Run(f.Input(),f.State,new VirtualPlanningCall(8,1,1,90,1,true),castle,true,(c,d)=>c==d?d:0),"managed caller models native dirty0/dirty1 cached inputs");Compare(m,f.State,false,1);cases++;
            }
            Console.WriteLine("PASS full native2D250 Dirty0/Dirty1 without forced topology rebuild");
            // Fixes 1.25's exact semantic inline replacement at 2C5E1: mov rax,
            // count-address; mov eax,[rax+rcx*4]; displaced RIP store; continuation.
            // This is an offline patched-body reference, not a hook backend test.
            byte[] original=m.Bytes(0x2C5E1,14);const int stub=0x8800000,table=0x8801000;
            var code=new List<byte>();code.AddRange(new byte[]{0x48,0xb8});code.AddRange(BitConverter.GetBytes(m.Ptr(table).ToInt64()));code.AddRange(new byte[]{0x8b,0x04,0x88,0x89,0x05});code.AddRange(BitConverter.GetBytes(0x2E9CA14-(stub+19)));code.AddRange(new byte[]{0xff,0x25,0,0,0,0});code.AddRange(BitConverter.GetBytes(m.Ptr(0x2C5EF).ToInt64()));
            m.Put(stub,code.ToArray());m.PrivateExecutable(stub,code.Count);
            var patch=new List<byte>(new byte[]{0xff,0x25,0,0,0,0});patch.AddRange(BitConverter.GetBytes(m.Ptr(stub).ToInt64()));m.Put(0x2C5E1,patch.ToArray());
            try
            {
                m.Int(table+4,912);select(m.Ptr(0x366C210),1,8);Check(m.Int(0x2E9CA14)==912,"effective Fixes count differs from Vanilla count144");
                Check(m.Int(0x2E97EAC)==1&&m.Int(0x2E97D10)==1&&m.Int(0x2E97D14)==1,"patched continuation preserves other target outputs");
            }
            finally{m.Put(0x2C5E1,original);}
            Console.WriteLine("PASS private Fixes-equivalent 2C5E1 patched body; installed owner preserved; not backend validation");
        }
        private static void Planning(NativeImage m)
        {
            var seed=m.Function<V4>(0xD95E0);var distance=m.Function<V5>(0xD9190);
            for(int scenario=0;scenario<16;scenario++)
            {
                var f=new Fixture(scenario==4||scenario==5||scenario==14?80:10,scenario==4||scenario==5||scenario==14?80:10);
                bool active=scenario!=9,shortLimit=scenario!=5;
                if(scenario==1){f.Edge[f.SeedTile]=0;f.Type[f.SeedTile+1]=45;}
                if(scenario==2){f.Component[f.SeedTile+1]=0;f.Flag[f.SeedTile+W+1]=0x100;}
                if(scenario==3){f.Edge[f.SeedTile]=0;f.State.Seeds[f.SeedTile+1]=-128;f.Flag[f.SeedTile+1]=0x40000000;}
                if(scenario==12){f.Type[f.SeedTile+W]=45;f.Block[f.SeedTile+W]=1;}
                if(scenario==13){f.Flag[f.SeedTile+W+1]=0x100031;f.State.Seeds[f.SeedTile+1]=127;}
                if(scenario==6){f.State.Generation=32000;for(int i=0;i<N;i++)f.State.Visit[i]=234;}
                if(scenario==7||scenario==8){for(int i=0;i<f.Coarse.Length;i++)f.Coarse[i]=5;}
                f.Install(m,active);Console.WriteLine("native case "+scenario+" seed");seed(m.Ptr(Root),1,shortLimit?1:0,1);
                Check(VirtualBridgePlanning.Seed(f.Input(),f.State,f.SeedTile,active,shortLimit),"managed seed completed");Compare(m,f.State,true);
                if(scenario==9)continue;
                if(scenario>=7){Array.Clear(f.State.Seeds,0,N);f.State.Seeds[f.SeedTile]=1;m.Put(Seeds,f.State.Seeds.Select(v=>(byte)v).ToArray());}
                if(scenario==14||scenario==15){for(int tile=0;tile<N;tile++)f.State.Seeds[tile]=(sbyte)(scenario==15||f.Component[tile]!=0?1:0);m.Put(Seeds,f.State.Seeds.Select(v=>(byte)v).ToArray());}
                int origin=scenario==8?2:1,mark=scenario==14?1:scenario==15?0:scenario>=7?2:0;
                Console.WriteLine("native case "+scenario+" distance");distance(m.Ptr(Root),scenario==10?1:110,mark,origin,8);
                Check(VirtualBridgePlanning.Distance(f.Input(),f.State,scenario==10?1:110,mark,origin,(c,dest)=>c==dest?dest:0),"managed distance completed");Compare(m,f.State,false);if(scenario==14)Check(f.State.Candidates.Count==1000,"native candidate cap1000");if(scenario==15)Check(f.State.Tail==0&&f.State.Head==0,"full initial queue ring wrap");cases++;
            }
            var bounded=new Fixture(10,10);Check(!VirtualBridgePlanning.Seed(bounded.Input(),bounded.State,bounded.SeedTile,true,true,1)&&bounded.State.Reason=="planning-budget-exhausted","budget fails without complete answer");
            var missing=new Fixture(10,10);missing.State.Seeds[missing.SeedTile]=1;Check(!VirtualBridgePlanning.Distance(missing.Input(),missing.State,10,1,1,null),"missing region input remains incomplete");
        }


        private static void GatePlanning(NativeImage m)
        {
            var gateUpdate=m.Function<V2>(0xD8CE0);var seed=m.Function<V4>(0xD95E0);var distance=m.Function<V5>(0xD9190);
            for(int orientation=0;orientation<2;orientation++)for(int status=0;status<3;status++)
            {
                var f=new Fixture(12,12);int ox=102,oy=102,size=5,id=1,b=0x64CCBB0+id*0x32c;
                for(int y=oy;y<oy+size;y++)for(int x=ox;x<ox+size;x++)f.Type[y*W+x]=45;
                f.Install(m);m.Int(0x64CCBB0+0x50,2);m.Short(b+0x12c,2);m.Short(b+0x12e,45);m.Short(b+0x14a,ox);m.Short(b+0x14c,oy);m.Int(b+0x154,size);m.Short(b+0x15e,orientation==0?0:80);m.Byte(b+0x2fe,status);m.Short(b+0x32e,1);
                var ids=new short[N];for(int y=oy;y<oy+size;y++)for(int x=ox;x<ox+size;x++)ids[y*W+x]=1;m.Put(0x4B6AA50,ids);
                gateUpdate(m.Ptr(Root),id);byte[] restored=m.Bytes(Edges,N);f.Edge=(byte[])restored.Clone();
                int a,b2,c,d;if(orientation==0){a=(oy-1)*W+ox+size/2;b2=oy*W+ox+size/2;c=(oy+size)*W+ox+size/2;d=c-W;f.Edge[a]&=0xef;f.Edge[b2]&=0xfe;f.Edge[c]&=0xfe;f.Edge[d]&=0xef;}
                else {a=(oy+size/2)*W+ox-1;b2=a+1;c=(oy+size/2)*W+ox+size;d=c-1;f.Edge[a]&=0xfb;f.Edge[b2]&=0xbf;f.Edge[c]&=0xbf;f.Edge[d]&=0xfb;}
                var gx=new ushort[N];var gy=new ushort[N];for(int t=0;t<N;t++){gx[t]=(ushort)(t%W);gy[t]=(ushort)(t/W);}
                var gateMap=new VirtualBridgeMap(1,1,f.Component,restored,f.Flag,gx,gy,f.Rows,Array.Empty<VirtualConnection>(),true);
                byte[] closed,block;ushort[] types;string why;
                Check(VirtualPlanningBuildings.TryPrepare(gateMap,m.Bytes(b,0x32c),ids.Select(v=>(ushort)v).ToArray(),out closed,out types,out block,out why),"productive temporary gate reconstruction");
                Equal(closed,f.Edge,"native-validated temporary closed aperture directions");Equal(types,f.Type,"copied building classes");Equal(block,f.Block,"independent building blocking bytes");
                int connection=Root+0x2024+0x204;m.Int(Root,2);m.Put(connection,new byte[0x204]);m.Int(connection,1);m.Int(connection+4,3);m.Int(connection+12,1);m.Int(connection+24,status==2?0:1);m.Int(connection+52,101);m.Int(connection+56,1);m.Int(connection+0x1e4,8);m.Int(connection+0x1e8,17);for(int p=0;p<=8;p++)m.Int(0x37EDF3C+p*4,p);
                var regionBundle=new BridgePlanningCapture.Bundle();regionBundle.Sections.Add("buildingRecords",m.Bytes(b,0x32c));regionBundle.Sections.Add("gateConnectionIds",m.Bytes(b+0x32e,2));regionBundle.Sections.Add("nativeAlliances9",m.Bytes(0x37EDF3C,36));regionBundle.Sections.Add("distance-pre/macroLimit",BitConverter.GetBytes(2));regionBundle.Sections.Add("distance-pre/macroRecords",m.Bytes(connection,0x204));
                Func<int,int,int?> closedOracle,restoredOracle;string regionReason;
                Check(CopiedPlanningRegions.TryCreate(regionBundle,"distance-pre",true,8,0,out closedOracle,out regionReason),"copied temporary native macro closure");Check(CopiedPlanningRegions.TryCreate(regionBundle,"distance-pre",false,8,0,out restoredOracle,out regionReason),"copied observed macro state");
                m.Function<V2>(0xC4BF0)(m.Ptr(0x64CCBB0),1);Check(closedOracle(101,1)==m.Function<R5>(0xE2610)(m.Ptr(Root),8,101,1,0),"closed macro oracle versus actual native closure");m.Function<V2>(0xC4BF0)(m.Ptr(0x64CCBB0),0);Check((restoredOracle(101,1)!=0)==(m.Function<R5>(0xE2610)(m.Ptr(Root),8,101,1,0)!=0),"restored oracle does not inherit temporary closure");
                Console.WriteLine("native temporary gate orientation="+orientation+" state="+status);seed(m.Ptr(Root),1,1,1);
                Check(VirtualBridgePlanning.Seed(f.Input(),f.State,f.SeedTile,true,true),"gate seed completes");Compare(m,f.State,true);
                Check(m.Byte(b+0x2fe)==status&&m.Short(b+0x128)==status,"native gate state and backup round trip");Equal(m.Bytes(Edges,N),restored,"native gate directions restored");
                distance(m.Ptr(Root),110,0,1,8);Check(VirtualBridgePlanning.Distance(f.Input(),f.State,110,0,1,(cx,dest)=>cx==dest?dest:0),"gate distance completes");Compare(m,f.State,false);cases++;
            }
        }

        private static void Raised(NativeImage m)
        {
            var update=m.Function<V3>(0xD86F0);var raise=m.Function<V2>(0x645C0);var rebuild=m.Function<R2>(0xE49D0);
            int raisedCases=0;
            for(int orientation=0;orientation<8;orientation++)for(int moatMode=0;moatMode<3;moatMode++)for(int heightMode=0;heightMode<3;heightMode++)for(int scene=0;scene<2;scene++)
            {
                var f=new Fixture(20,20);int ox=107,oy=107,id=1,b=0x64CCBB0+id*0x32c;
                if(scene==1)
                {
                    for(int t=0;t<N;t++){f.Flag[t]=0x31;f.Component[t]=0;f.Edge[t]=0;}
                    bool rows=((orientation/2)&1)==0;
                    for(int cell=0;cell<25;cell++)if(rows?cell/5>=1&&cell/5<=3:cell%5>=1&&cell%5<=3){int t=(oy+cell/5)*W+ox+cell%5;f.Flag[t]=0;f.Component[t]=1;}
                    foreach(int t in rows?new[]{(oy+2)*W+ox-2,(oy+2)*W+ox-1,(oy+2)*W+ox+5,(oy+2)*W+ox+6}:new[]{(oy-2)*W+ox+2,(oy-1)*W+ox+2,(oy+5)*W+ox+2,(oy+6)*W+ox+2}){f.Flag[t]=0;f.Component[t]=1;}
                }
                f.Install(m);var heights=new byte[N];for(int t=0;t<N;t++)if(f.Flag[t]==0)heights[t]=(byte)(((t%W+t/W)&1)*(heightMode==1?16:heightMode==2?17:0));m.Put(0x4DDD350,heights);m.Put(0x4E2B870,heights);
                m.Short(b+0x12c,2);m.Short(b+0x12e,49);m.Short(b+0x14a,ox);m.Short(b+0x14c,oy);m.Int(b+0x154,5);m.Short(b+0x15e,orientation);m.Int(0x64CCBB0+0x50,2);
                m.Int(TileRoot+0x204e65c,0);m.Int(TileRoot+0x204e690,0);m.Int(0x6203594,0);
                m.Put(TileRoot+0x1ea23f0,new short[N]);
                for(int cell=0;cell<25;cell++)
                {
                    int mapper=0x8DDCC0+(5*169+cell)*24;m.Int(mapper,cell%5);m.Int(mapper+4,cell/5);
                    int tile=(oy+cell/5)*W+ox+cell%5;
                    if(moatMode==0||moatMode==1&&cell%2==0)m.Short(TileRoot+0x1ea23f0+tile*2,cell+1);
                }
                for(int y=99;y<121;y++)for(int x=99;x<121;x++)m.Byte(0x3A11EA4+y*800+x,1);
                for(int y=100;y<120;y++)for(int x=100;x<120;x++)update(m.Ptr(Root),y,y*W+x);
                Check(rebuild(m.Ptr(Root),1)==1,"native initial completed rebuild");
                byte[] originalEdges=m.Bytes(Edges,N);int[] originalFlags=m.Ints(Flags,N);short[] before=m.Shorts(Pcl,N);
                Console.WriteLine("native raise orientation="+orientation+" moat="+moatMode+" height="+heightMode+" scene="+scene);
                raise(m.Ptr(TileRoot),id);
                int[] afterFlags=m.Ints(Flags,N);byte[] afterEdges=m.Bytes(Edges,N);var cuts=new List<int>();
                for(int tile=0;tile<N;tile++)if(afterFlags[tile]!=originalFlags[tile])
                {Check((afterFlags[tile]^originalFlags[tile])==0x40000000,"raise only adds audited deck flag");cuts.Add(tile);}
                int expected=0;for(int cell=0;cell<25;cell++)
                {
                    bool mask=m.Int(0x2D1A30+(orientation/2)*100+cell*4)!=0;
                    bool moat=moatMode==0||moatMode==1&&cell%2==0;
                    if(mask&&moat){expected++;Check(cuts.Contains((oy+cell/5)*W+ox+cell%5),"exact native closure deck cell");}
                }
                Check(cuts.Count==expected,"orientation and moat-record closure count");
                var predictedEdges=(byte[])originalEdges.Clone();foreach(int tile in cuts)for(int direction=0;direction<8;direction++){int neighbor=tile+f.Offsets[tile/W*8+direction];predictedEdges[tile]&=unchecked((byte)~(1<<direction));predictedEdges[neighbor]&=unchecked((byte)~(1<<((direction+4)&7)));}
                Equal(afterEdges,predictedEdges,"entire native raised edge grid versus virtual deck isolation");Check(m.Int(Root+0x6c)==1,"raise marks topology dirty");
                int attempts=0; while(rebuild(m.Ptr(Root),0)==0 && ++attempts<200) {} Check(attempts==199 && m.Int(Root+0x6c)==0,"native countdown then completed raised rebuild");
                short[] after=m.Shorts(Pcl,N);var xx=new ushort[N];var yy=new ushort[N];for(int tile=0;tile<N;tile++){xx[tile]=(ushort)(tile%W);yy[tile]=(ushort)(tile/W);}
                var map=new VirtualBridgeMap(1,1,before.Select(v=>(ushort)v).ToArray(),originalEdges,originalFlags,xx,yy,f.Rows,Array.Empty<VirtualConnection>(),true);
                // Compare actual directed crossings, not merely post-hoc PCL-number equality.
                foreach(var pair in new[]{new[]{(oy+2)*W+ox-2,(oy+2)*W+ox+6},new[]{(oy-2)*W+ox+2,(oy+6)*W+ox+2}})
                {
                    foreach(bool reverse in new[]{false,true})
                    {
                        int start=pair[reverse?1:0],end=pair[reverse?0:1];if(before[start]==0||before[end]==0)continue;var query=new VirtualBridgeQuery(map,start,end,0,cuts,true);
                        while(!query.Complete)query.Step(4096);
                        bool actual=DirectedWitness(afterEdges,after,start,end,f.Offsets);
                        Check((query.Result==VirtualReachability.Reachable)==actual,"native raised directed route matches copied cut: virtual="+query.Result+"/"+query.Reason+",actual="+actual+",labels="+before[start]+"/"+before[end]+" -> "+after[start]+"/"+after[end]+",start="+start+",end="+end);
                    }
                }
                // Every surviving/non-surviving tile label is compared with a managed native-order flood.
                ushort[] copied=Relabel(afterFlags,afterEdges,f.Rows,f.Offsets,m);
                Equal(after.Select(v=>(ushort)v).ToArray(),copied,"native raised full component grid");raisedCases++;
                if(heightMode==0)
                {
                    f.Flag=originalFlags;f.Edge=originalEdges;f.Component=before.Select(v=>(ushort)v).ToArray();
                    var publication=new VirtualRaisedPlanning(f.Input(),map,cuts.ToArray(),f.Rows.Select(v=>v+W).ToArray());while(!publication.Rebuild.Complete)publication.Step(97);var overlay=publication.Planning;Equal(overlay.Components,copied,"joint raised planning topology publication");Equal(overlay.Flags,afterFlags,"managed raised planning flags");Equal(overlay.ClosedGateEdges,afterEdges,"managed raised planning directions");
                    f.State=new VirtualPlanningState();m.Put(Seeds,new byte[N]);m.Put(Visits,new short[N]);m.Put(Distances+2*N*2,new short[N]);m.Put(Root+0x155f6c,new int[N]);m.Put(Root+0x28f3ec,new short[N]);m.Int(Root+4,0);m.Int(0x405B4F0,0);
                    foreach(int at in new[]{0x155f38,0x155f3c,0x155f40,0x155f44,0x155f48,0x8c})m.Int(Root+at,0);
                    int target=Array.FindIndex(copied,v=>v!=0);m.Int(0x379AFA8+0x583c,target%W);m.Int(0x379AFAC+0x583c,target/W);
                    m.Function<V4>(0xD95E0)(m.Ptr(Root),1,0,1);
                    Check(VirtualBridgePlanning.Seed(overlay,f.State,target,true,false),"raised seed completes");Compare(m,f.State,true);
                    m.Function<V5>(0xD9190)(m.Ptr(Root),30,0,1,8);
                    Check(VirtualBridgePlanning.Distance(overlay,f.State,30,0,1,(cx,dest)=>cx==dest?dest:0),"raised distance completes");Compare(m,f.State,false);cases++;
                }
            }
            Console.WriteLine("PASS native raise/update/rebuild: "+raisedCases+" synthetic orientation/moat cases");
        }
        private static bool DirectedWitness(byte[] edges,short[] pcl,int start,int target,int[] offsets)
        {
            var seen=new bool[N];var queue=new Queue<int>();if(pcl[start]==0||pcl[target]==0)return false;queue.Enqueue(start);seen[start]=true;
            while(queue.Count>0){int at=queue.Dequeue();if(at==target)return true;for(int d=0;d<8;d++)if((edges[at]&(1<<d))!=0){int next=at+offsets[at/W*8+d];if((uint)next<N&&!seen[next]){seen[next]=true;queue.Enqueue(next);}}}return false;
        }
        private static ushort[] Relabel(int[] flags,byte[] edges,int[] rows,int[] offsets,NativeImage native=null)
        {
            var xx=new ushort[N];var yy=new ushort[N];for(int t=0;t<N;t++){xx[t]=(ushort)(t%W);yy[t]=(ushort)(t/W);}
            var map=new VirtualBridgeMap(1,1,new ushort[N],edges,flags,xx,yy,rows,Array.Empty<VirtualConnection>(),true);
            var rebuild=new VirtualTopologyRebuild(map,offsets,rows.Select(v=>v+W).ToArray());
            while(!rebuild.Complete)rebuild.Step(137);
            Check(rebuild.Proven,"productive resumable native-order rebuild");
            if(native!=null){Equal(native.Ints(Root+0xe0,1000),rebuild.Counts,"entire component tile-count table");Check(native.Int(Root+0xcc)==rebuild.NextComponent&&native.Int(Root+0x1080)==rebuild.Total,"native component limit and total count");}
            return rebuild.Components;
        }

        private static void MacroRoles(NativeImage m)
        {
            var query=m.Function<R5>(0xE2610);
            for(int kind=1;kind<=3;kind+=2)for(int mode=0;mode<=1;mode++)for(int role=0;role<5;role++)foreach(bool reverse in new[]{false,true})
            {
                var f=new Fixture(1,1);f.Install(m);int r=Root+0x2024+0x204;int owner=role==0?8:role==1?2:1,captured=role==3?8:role==4?3:0;
                for(int p=0;p<=8;p++)m.Int(0x37EDF3C+p*4,p);m.Int(0x37EDF3C+2*4,8);
                m.Put(r,new byte[0x204]);m.Int(Root,2);m.Int(r,1);m.Int(r+4,kind);m.Int(r+12,1);m.Int(r+24,1);m.Int(r+52,101);m.Int(r+56,1);m.Int(r+0x1e8,17);m.Int(r+0x1e4,owner);m.Short(0x64CCBB0+0x32c+0x322,captured);
                bool allowed=(kind!=1||mode!=0)&&(role<2||captured!=0);
                Check((query(m.Ptr(Root),8,reverse?1:101,reverse?101:1,mode)!=0)==allowed,"native role/mode/direction admission");
                Check(query(m.Ptr(Root),8,17,101,mode)!=0==allowed,"native third endpoint role/mode");
                Check(query(m.Ptr(Root),8,101,101,mode)==101,"native same-component shortcut preserves original");
            }
            Console.WriteLine("PASS native macro roles/modes/directions/C; any nonzero native capturer is NOT sufficient for Gate policy");
        }
        private static void SpecialSeeds(NativeImage m)
        {
            var special=m.Function<R2>(0x107160);var rebuild=m.Function<R2>(0xE49D0);
            foreach(int kind in new[]{0,4,5,14,15,16})
            {
                var f=new Fixture(1,1);int tile=f.SeedTile;f.Flag[tile]=0x40001000;f.Edge[tile]=0;f.Install(m);
                m.Put(0x4ACE010,new short[N]);m.Short(0x4ACE010+tile*2,1);m.Short(0x32DE440+0x9c+0x6a,kind);
                bool allow=kind>=5&&kind!=15;Check((special(m.Ptr(0x32DE440),tile)&255)==(allow?1:0),"107160 native special-kind predicate");
                Check(rebuild(m.Ptr(Root),1)==1,"native special rebuild");Check((m.Short(Pcl+tile*2)!=0)==allow,"E49D0 special seed overrides raised flag only for allowed kind");
                var x=new ushort[N];var y=new ushort[N];for(int t=0;t<N;t++){x[t]=(ushort)(t%W);y[t]=(ushort)(t/W);}
                var specialIds=new ushort[N];specialIds[tile]=1;
                var map=new VirtualBridgeMap(1,1,f.Component,f.Edge,f.Flag,x,y,f.Rows,Array.Empty<VirtualConnection>(),true,specialIds,new short[]{0,(short)kind});
                var copy=new VirtualTopologyRebuild(map,f.Offsets,f.Rows.Select(v=>v+W).ToArray());while(!copy.Complete)copy.Step(53);
                Check(copy.Proven,"complete special seed input");Equal(copy.Components,m.Shorts(Pcl,N).Select(v=>(ushort)v).ToArray(),"entire special component grid");Equal(copy.Counts,m.Ints(Root+0xe0,1000),"entire special count grid");
                var unknownMap=new VirtualBridgeMap(1,1,f.Component,f.Edge,f.Flag,x,y,f.Rows,Array.Empty<VirtualConnection>(),true);
                var unknown=new VirtualTopologyRebuild(unknownMap,f.Offsets,f.Rows.Select(v=>v+W).ToArray());while(!unknown.Complete)unknown.Step(1000);Check(!unknown.Proven&&unknown.Reason=="missing-special-seed-input","missing 107160 input cannot prove NoRoute");
            }
            Console.WriteLine("PASS special seed kinds: isolated raised flag1000, including excluded kind15");
        }
        private static void Tables(NativeImage m)
        {
            NativePathfindingTableCopy a,b;string reason;Check(NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,m.Size,m.Int,out a,out reason),"full private native tables");
            Check(a.Profile(89).HasValue&&a.Permission(6,89).HasValue,"last native slots present");
            for(int kind=1;kind<=6;kind++)for(int type=0;type<90;type++)Check(a.Permission(kind,type)==(m.Int(0x32BDB0+(kind-1)*0x168+type*4)!=0),"native class row stride");
            Check(!NativePathfindingTableCopy.TryCapture("bad",m.Size,m.Int,out b,out reason),"wrong hash denied");
            Check(!NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,10,m.Int,out b,out reason),"short image denied");
            m.Int(0x32BDB0+5*0x168+89*4,2);Check(NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,m.Size,m.Int,out b,out reason)&&!b.Permission(6,89).HasValue&&!a.SameContent(b),"nonboolean current permission unknown, exact cache invalidation");
            int reads=0;Check(!NativePathfindingTableCopy.TryCapture(NativePathfindingTableCopy.Hash,m.Size,r=>{reads++;return reads>630?m.Int(r)^1:m.Int(r);},out b,out reason),"concurrent table change rejected");
        }
        private static void Capture(NativeImage m,int dirty=0)
        {
            var f=new Fixture(12,12);f.Install(m);m.Int(0x405B4F8,12345);m.Int(0x379D9A8+8*0x583c,1);m.Int(Root+0x6c,dirty);m.Int(Root+0x74,4);
            object token=new object();object discardedShadow=new object();BridgePlanningCapture.Bundle bundle=null;var reasons=new List<string>();int originalCalls=0;
            var capture=new BridgePlanningCapture(m.Size,m.Bytes,()=>token,v=>bundle=v,(k,d)=>reasons.Add(k+":"+d));
            capture.Begin(1);capture.Observe(0x2D250,false,1,100,90,8,8,1,0,0,true);
            discardedShadow=null;Check(discardedShadow==null&&capture.ActiveFamily==100,"discarded shadow publication does not discard own map-bound planning family");
            m.Int(0x2EA70DC+8*0x177bc,1); // Audited 2D250 bank write precedes both child calls.
            capture.Observe(0xD95E0,false,1,101,100,8,1,1,1,0,true);originalCalls++;m.Function<V4>(0xD95E0)(m.Ptr(Root),1,1,1);capture.Observe(0xD95E0,true,1,101,100,8,1,1,1,0,true);
            m.Int(0x2EA70DC+8*0x177bc,1);
            capture.Observe(0xD9190,false,1,102,100,8,110,50,1,8,true);originalCalls++;m.Function<V5>(0xD9190)(m.Ptr(Root),110,50,1,8);capture.Observe(0xD9190,true,1,102,100,8,110,50,1,8,true);
            capture.Observe(0x2D250,true,1,100,90,8,8,1,0,0,true);
            Check(originalCalls==2&&bundle!=null&&bundle.Complete&&bundle.Stages.Count==4,"production capture wraps originals exactly once and completes four stages: "+string.Join(";",reasons));
            Check(bundle.Dirty==dirty&&bundle.Revision==4&&!bundle.NegativeEligible,"dirty decision capture separate from negative proof eligibility");
            Check(bundle.Sections.ContainsKey("permissions540")&&bundle.Sections["permissions540"].Length==2160,"complete current native permission rows");
            Check(bundle.References.Count>0&&bundle.References.All(v=>bundle.Sections.ContainsKey(v.Value)),"exact deduplicated stage definitions");
            var gx=new ushort[N];var gy=new ushort[N];for(int t=0;t<N;t++){gx[t]=(ushort)(t%W);gy[t]=(ushort)(t/W);}
            var geometry=new VirtualBridgeMap(1,4,f.Component,f.Edge,f.Flag,gx,gy,f.Rows,Array.Empty<VirtualConnection>(),true);
            VirtualPlanningInput copiedInput;VirtualPlanningState copiedState;string copiedReason;
            Check(CopiedPlanningBundle.TryRead(bundle,geometry,"seed-pre",out copiedInput,out copiedState,out copiedReason),"productive complete copied planning input");
            Check(VirtualBridgePlanning.Seed(copiedInput,copiedState,f.SeedTile,true,true),"copied seed replay completes");
            Equal(copiedState.Seeds.Select(v=>(byte)v).ToArray(),CopiedPlanningBundle.Resolve(bundle,"seed-post/seeds"),"captured native seed field versus productive copied replay");
            Check(CopiedPlanningBundle.TryRead(bundle,geometry,"distance-pre",out copiedInput,out copiedState,out copiedReason),"copied distance bank initial state");
            Check(VirtualBridgePlanning.Distance(copiedInput,copiedState,110,50,1,(c,d)=>c==d?d:0),"captured distance replay completes");Compare(m,copiedState,false,1);
            Check(!CopiedPlanningBundle.TryRead(null,geometry,"distance-pre",out copiedInput,out copiedState,out copiedReason)&&copiedReason=="missing-historical-planning-values","schema1 has no invented planning inputs");
            long measuredCapture=bundle.Ticks;
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"planning-capture-"+Guid.NewGuid().ToString("N"));
            var writer=new BridgeInputArtifact(folder,(s,k,d)=>{});
            var blocks=new List<byte[]> {BridgeInputArtifact.Header("fixture=True\n"+bundle.Metadata,2)};blocks.AddRange(bundle.Serialize());
            foreach(var pair in new[]{new KeyValuePair<string,Array>("components",f.Component),new KeyValuePair<string,Array>("edges",f.Edge),new KeyValuePair<string,Array>("flags",f.Flag),new KeyValuePair<string,Array>("x",gx),new KeyValuePair<string,Array>("y",gy),new KeyValuePair<string,Array>("rows",f.Rows)})blocks.AddRange(BridgeInputArtifact.Section(pair.Key,pair.Value,pair.Value is byte[]?1:pair.Value is ushort[]?2:4));
            Check(writer.Enqueue(1,100,blocks,2),"planning schema2 artifact accepted");int pumps=0;while(writer.Pending(1)){long before=writer.Bytes;writer.Pump();Check(writer.Bytes-before<=65536&&++pumps<10000,"bounded planning artifact writes");}
            BridgePlanningImporter.Artifact imported;string importReason;
            string artifactPath=Path.Combine(folder,"bridge-1-100.bin");
            Check(BridgePlanningImporter.TryRead(artifactPath,out imported,out importReason)&&imported.Planning!=null,"productive schema2 disk importer: "+importReason);
            Check(imported.Planning.Dirty==dirty&&imported.Planning.Revision==4,"dirty and native revision survive disk import");
            foreach(var pair in bundle.Sections)Equal(pair.Value,CopiedPlanningBundle.Resolve(imported.Planning,pair.Key),"disk section "+pair.Key);
            foreach(var pair in bundle.References)Equal(CopiedPlanningBundle.Resolve(bundle,pair.Key),CopiedPlanningBundle.Resolve(imported.Planning,pair.Key),"disk definition reference "+pair.Key);
            string replayReason;Check(CopiedPlanningReplay.TryBaseline(imported.Planning,geometry,out replayReason),"productive whole disk baseline: "+replayReason);
            Check(CopiedPlanningReplay.TryBaseline(imported,out replayReason),"complete file to geometry to productive planning baseline: "+replayReason);
            byte[] wrongCandidate=(byte[])CopiedPlanningBundle.Resolve(imported.Planning,"distance-post/candidateCapacity8").Clone();wrongCandidate[4]^=1;imported.Planning.References.Remove("distance-post/candidateCapacity8");imported.Planning.Sections["distance-post/candidateCapacity8"]=wrongCandidate;Check(!CopiedPlanningReplay.TryBaseline(imported,out replayReason)&&replayReason.Contains("whole-candidate-table"),"untouched second candidate column mismatch blocks baseline");Check(BridgePlanningImporter.TryRead(artifactPath,out imported,out importReason),"restore unchanged disk fixture after rejection test");
            Check(CopiedPlanningBundle.TryRead(imported.Planning,geometry,"seed-pre",out copiedInput,out copiedState,out copiedReason)&&VirtualBridgePlanning.Seed(copiedInput,copiedState,f.SeedTile,true,true),"productive seed replay from disk");
            Equal(copiedState.Seeds.Select(v=>(byte)v).ToArray(),CopiedPlanningBundle.Resolve(imported.Planning,"seed-post/seeds"),"disk seed complete output");
            Check(CopiedPlanningBundle.TryRead(imported.Planning,geometry,"distance-pre",out copiedInput,out copiedState,out copiedReason)&&VirtualBridgePlanning.Distance(copiedInput,copiedState,110,50,1,(c,d)=>c==d?d:0),"productive distance replay from disk");Compare(m,copiedState,false,1);
            byte[] corrupt=File.ReadAllBytes(artifactPath);corrupt[20]^=1;string broken=Path.Combine(folder,"broken.bin");File.WriteAllBytes(broken,corrupt);Check(!BridgePlanningImporter.TryRead(broken,out imported,out importReason),"modified payload rejected");
            File.WriteAllBytes(broken,corrupt.Take(corrupt.Length-1).ToArray());Check(!BridgePlanningImporter.TryRead(broken,out imported,out importReason),"truncated delivery rejected");
            Check(!BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-1-100.partial"),out imported,out importReason),"unfinished publisher file rejected");
            capture.Begin(2);bundle=null;capture.Observe(0x2D250,false,2,200,190,8,8,1,0,0,true);token=new object();capture.Observe(0xD95E0,false,2,201,200,8,1,1,1,0,true);
            Check(bundle==null&&reasons.Any(v=>v.Contains("identity-or-topology-changed")),"identity replacement invalidates pending family");
            capture.Observe(0x2D250,false,2,210,190,8,8,1,0,0,true);Check(capture.ActiveFamily==210,"failed first capture permits second suitable family");capture.Observe(0xD95E0,true,2,211,210,8,1,1,1,0,false);capture.Observe(0x2D250,false,2,220,190,8,8,1,0,0,true);Check(capture.ActiveFamily==0,"two failed attempts cannot allocate a third bundle");
            capture.Begin(3);capture.Observe(0x2D250,false,3,300,290,8,8,1,0,0,true);capture.Observe(0x2D250,false,3,301,300,8,8,1,0,0,true);Check(reasons.Any(v=>v.Contains("nested-planning-family")),"nested family isolated");
            capture.Begin(4);capture.Observe(0x2D250,false,4,400,390,8,8,1,0,0,true);capture.End();Check(reasons.Any(v=>v.Contains("map-ended-during-planning")),"map end explicitly terminates incomplete capture");
            var failed=new BridgePlanningCapture(m.Size,(r,n)=>{throw new InvalidOperationException("fixture-read-failure");},()=>token,v=>{throw new Exception("unexpected delivery");},(k,d)=>reasons.Add(d));failed.Begin(5);failed.Observe(0x2D250,false,5,500,490,8,8,1,0,0,true);Check(reasons.Any(v=>v.Contains("capture-error")),"copy exception isolated before original");
            var unbound=new BridgePlanningCapture(m.Size,(r,n)=>{throw new Exception("unbound family must not read memory");},()=>token,v=>{throw new Exception("unbound delivery");},(k,d)=>reasons.Add(d),requireMilitaryRoot:true);unbound.Begin(6);unbound.Observe(0x2D250,false,6,600,590,8,8,1,0,0,true);Check(reasons.Any(v=>v.Contains("unresolved-military-parent"))&&unbound.ActiveFamily==0,"unresolved parent is distinct and makes no complete copy");
            f.Install(m);m.Int(0x379D9A8+8*0x583c,1);m.Int(Root+0x6c,dirty);m.Int(Root+0x74,4);m.Int(Root+0xe0+4,144);capture.Begin(7);bundle=null;
            capture.Observe(0x2D250,false,7,700,699,8,8,1,0,0,true,699);
            m.Int(0x2EA70DC+8*0x177bc,1); // Same audited caller ordering for selected-artifact fixture.
            capture.Observe(0xD95E0,false,7,701,700,8,1,1,1,0,true,699);m.Function<V4>(0xD95E0)(m.Ptr(Root),1,1,1);capture.Observe(0xD95E0,true,7,701,700,8,1,1,1,0,true,699);
            m.Int(0x2EA70DC+8*0x177bc,1);capture.Observe(0xD9190,false,7,702,700,8,110,50,1,8,true,699);m.Function<V5>(0xD9190)(m.Ptr(Root),110,50,1,8);capture.Observe(0xD9190,true,7,702,700,8,110,50,1,8,true,699);capture.Observe(0x2D250,true,7,700,699,8,8,1,0,0,true,699);Check(bundle==null,"immutable publication waits for completed military root and selection");
            m.Int(0x379D968+8*0x583c,f.SeedTile);m.Int(0x379AFB0+0x583c,f.SeedTile);m.Int(Root+0xe0+4,144);
            capture.Observe(0x2C5A0,false,7,703,699,8,8,1,0,0,true,699);m.Function<V3>(0x2C5A0)(m.Ptr(Root),8,1);m.Int(0x2E9CA14,912);capture.Observe(0x2C5A0,true,7,703,699,8,8,1,0,0,true,699);capture.Observe(0x3C2E0,true,7,699,690,8,8,0,0,0,true,0);
            Check(bundle!=null&&bundle.Sections.ContainsKey("target/703/values8"),"target selection associated by actual frame and completed parent");byte[] selectionBytes=bundle.Sections["target/703/values8"];Check(BitConverter.ToInt32(selectionBytes,20)==912&&BitConverter.ToInt32(selectionBytes,28)==144,"synthetic effective and native counts kept separate");
            Check(CopiedPlanningReplay.TryBaseline(bundle,new VirtualBridgeMap(7,4,f.Component,f.Edge,f.Flag,gx,gy,f.Rows,Array.Empty<VirtualConnection>(),true),out replayReason),"productive complete baseline with captured selection: "+replayReason);
            var selectedBlocks=new List<byte[]> {BridgeInputArtifact.Header("fixture=True\n"+bundle.Metadata,2)};selectedBlocks.AddRange(bundle.Serialize());Check(writer.Enqueue(7,700,selectedBlocks,2),"selected planning bundle enqueue");while(writer.Pending(7))writer.Pump();Check(BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-7-700.bin"),out imported,out importReason)&&imported.Planning!=null,"selection frame disk identity validation: "+importReason);
            imported.Planning.Sections["target/703/identity5"][0]^=1;var wrongBlocks=new List<byte[]> {BridgeInputArtifact.Header("fixture=True\n"+imported.Planning.Metadata,2)};wrongBlocks.AddRange(imported.Planning.Serialize());Check(writer.Enqueue(7,701,wrongBlocks,2),"wrong session fixture accepted by writer only");while(writer.Pending(7))writer.Pump();Check(!BridgePlanningImporter.TryRead(Path.Combine(folder,"bridge-7-701.bin"),out imported,out importReason),"valid hash with wrong selection session rejected");
            capture.Begin(8);capture.Observe(0x2D250,false,8,799,790,8,8,1,0,0,true,0,false);Check(capture.ActiveFamily==0&&capture.Status.Contains("planningCaptureAttempts=0"),"preparatory phases skipped without consuming capture attempt");
            f.Install(m);m.Int(0x379D9A8+8*0x583c,1);m.Int(Root+0x6c,dirty);m.Int(Root+0x74,4);capture.Begin(8);capture.Observe(0x2D250,false,8,800,790,8,8,1,0,0,true);m.Int(Root+0x74,5);capture.Observe(0xD95E0,false,8,801,800,8,1,1,1,0,true);Check(capture.ActiveFamily==0,"rebuild during capture rejects family even with same map identity");
            f.Install(m);m.Int(0x379D9A8+8*0x583c,1);m.Int(Root+0x6c,dirty);m.Int(Root+0x74,4);capture.Begin(9);capture.Observe(0x2D250,false,9,900,890,8,8,1,0,0,true);m.Int(Root+0x6c,dirty==0?1:0);capture.Observe(0xD95E0,false,9,901,900,8,1,1,1,0,true);Check(capture.ActiveFamily==0,"dirty state transition during pending capture is not silently accepted");
            string originals=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","..","EnemyGateBuildingContextAudit","bridge-inputs-20261006-235642"));
            foreach(var original in new[]{new[]{"bridge-1-174.bin","C1765553075387D23183D792726DAC98BF1DEA8E6B444C908EE6C8325C0283FC"},new[]{"bridge-1-204.bin","3E5D974B7684C5F00490E2DE1D0BCBC388BB50A37EFEA7EACD798BF1A83C6121"}})Check(BridgePlanningImporter.TryRead(Path.Combine(originals,original[0]),out imported,out importReason)&&imported.Schema==1&&imported.Planning==null&&imported.Hash==original[1],"original schema1 unchanged and no assumed planning input: "+importReason);
            Console.WriteLine("PASS passive production planning capture and schema2 delivery; artifact="+Path.Combine(folder,"bridge-1-100.bin")+"; captureMs="+(measuredCapture*1000.0/System.Diagnostics.Stopwatch.Frequency).ToString("F3"));
        }
        private static void PackedAndLimit(NativeImage m)
        {
            foreach(bool limit in new[]{false,true})
            {
                var f=new Fixture(1,1);var x=new ushort[N];var y=new ushort[N];var ends=new int[800];int packed=0;
                Array.Clear(f.Edge,0,N);for(int t=0;t<N;t++)f.Flag[t]=0x31;
                for(int row=0;row<800;row++)
                {
                    int width=(row&1)==0?400:402;f.Rows[row]=packed;
                    for(int col=0;col<width;col++){x[packed+col]=(ushort)col;y[packed+col]=(ushort)row;f.Y[packed+col]=(short)row;}packed+=width;ends[row]=packed;
                }
                for(int row=1;row<799;row++)for(int d=0;d<8;d++)f.Offsets[row*8+d]=d==0||d==7||d==1?f.Rows[row-1]-f.Rows[row]+VirtualBridgeMap.Dx[d]:d==4||d==5||d==3?f.Rows[row+1]-f.Rows[row]+VirtualBridgeMap.Dx[d]:VirtualBridgeMap.Dx[d];
                if(limit){for(int t=f.Rows[100];t<f.Rows[100]+1000;t++)f.Flag[t]=0;}
                else for(int row=100;row<110;row++)for(int col=100;col<110;col++)
                {int t=f.Rows[row]+col;f.Flag[t]=0;for(int d=0;d<8;d++)if(col+VirtualBridgeMap.Dx[d]>=100&&col+VirtualBridgeMap.Dx[d]<110&&row+VirtualBridgeMap.Dy[d]>=100&&row+VirtualBridgeMap.Dy[d]<110)f.Edge[t]|=(byte)(1<<d);}
                f.Install(m);for(int row=0;row<800;row++){m.Int(0x402FF2C+row*12,f.Rows[row]);m.Int(0x402FF34+row*12,ends[row]);}
                Check(m.Function<R2>(0xE49D0)(m.Ptr(Root),1)==1,"private native packed-row rebuild");
                var map=new VirtualBridgeMap(1,1,f.Component,f.Edge,f.Flag,x,y,f.Rows,Array.Empty<VirtualConnection>(),true);
                var copy=new VirtualTopologyRebuild(map,f.Offsets,ends);while(!copy.Complete)copy.Step(7);
                Equal(copy.Components,m.Shorts(Pcl,N).Select(v=>(ushort)v).ToArray(),"native packed component grid");Equal(copy.Counts,m.Ints(Root+0xe0,1000),"packed count grid");
                Check(copy.Proven==!limit&&copy.NextComponent==m.Int(Root+0xcc)&&copy.Total==m.Int(Root+0x1080),"999 native component cap preserves partial grid but forbids negative proof");
            }
            Console.WriteLine("PASS alternating packed row widths and native 999-component boundary");
        }
        private static int Main()
        {
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            try{Check(IntPtr.Size==8,"x64 process");using(var m=new NativeImage(Native,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv"))){Tables(m);Callers(m);Planning(m);GatePlanning(m);Raised(m);SpecialSeeds(m);MacroRoles(m);PackedAndLimit(m);Capture(m,0);Capture(m,1);}Console.WriteLine("PASS private native planning: "+cases+" synthetic differential cases, "+checks+" checks; no game/library initialization");Console.WriteLine("offline-cost: totalMs="+elapsed.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+",scope=all-fixtures-native-and-managed-including-copies-and-full-grid-checks,gameCost=not-measured,behaviorFix=disabled");return 0;}
            catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
    }
}
