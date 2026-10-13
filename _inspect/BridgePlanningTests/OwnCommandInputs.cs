using System;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static partial class Program
    {
        private static void CommandLineCases(NativeImage native)
        {
            for(int direction=0;direction<8;direction++)for(int scenario=0;scenario<6;scenario++)
            {
                var f=new Fixture(7,7);int sx=103,sy=103,tx=sx+VirtualBridgeMap.Dx[direction]*2,ty=sy+VirtualBridgeMap.Dy[direction]*2;
                int origin=sy*W+sx,next=origin+f.Offsets[sy*8+direction];
                var visits=new short[N];var block=new byte[N];int mode=0,generation=scenario==5?32000:9;
                if(scenario==1)f.Edge[origin]=0;if(scenario==2)f.Flag[next]=0x100;
                if(scenario==3)f.Flag[next]=0x102;if(scenario==4){mode=1;block[origin]=1;}
                var valid=new byte[640000];for(int i=0;i<valid.Length;i++)valid[i]=1;
                var rows=new int[2400];for(int y=0;y<800;y++)rows[y*3]=f.Rows[y];var directions=new int[16];for(int d=0;d<8;d++){directions[d*2]=VirtualBridgeMap.Dx[d];directions[d*2+1]=VirtualBridgeMap.Dy[d];}
                var managed=new VirtualCandidateBuilder();
                void Put(int address,Array values){byte[] data=Bytes(values);native.Put(address,data);managed.Put(address,data);}
                Put(0x3a11ea4,valid);Put(0x402ff2c,rows);Put(0x405edb0,f.Offsets);Put(0x2d2e50,directions);Put(0x51890d0,f.Edge);Put(0x48f71b0,f.Flag);Put(0x4b6aa50,new short[N]);Put(0x2e68d0,new int[336]);Put(0x51d75f0,block);Put(Visits,visits);
                var control=new byte[224];Buffer.BlockCopy(BitConverter.GetBytes(generation),0,control,4,4);Put(Root,control);Put(Root+0x155f68,new int[1]);Put(0x2f7a690,new[]{mode});
                byte[] path=new byte[1000];native.Put(0x8790000,path);native.Long(Root+0x155f60,native.Ptr(0x8790000).ToInt64());
                int n=native.Function<R5>(0xF32B0)(native.Ptr(Root),sx,sy,tx,ty),m=managed.CommandLine(sx,sy,tx,ty,path);
                Check(n==m,"all line directions/edge/solid/passable/block/reset result");Equal(native.Bytes(0x8790000,1000),path,"line packed bank");Equal(native.Bytes(Visits,N*2),managed.Bytes(Visits,N*2),"line visits");Check(native.Int(Root+4)==BitConverter.ToInt32(managed.Bytes(Root+4,4),0),"line generation");Check(native.Int(Root+0x155f68)==BitConverter.ToInt32(managed.Bytes(Root+0x155f68,4),0),"line count");cases++;
            }
            Console.WriteLine("PASS F32B0 native/managed:48 cases,8 orientations,solid/passable/block/edge/reset and packed output/visits");
        }
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
        private delegate int Flood7(IntPtr self,int sx,int sy,int tx,int ty,int limit,int continuation);
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
        private delegate void Reconstruct6(IntPtr self,int sx,int sy,int tx,int ty,int mode);
        private static void CommandFloodCases(NativeImage native)
        {
            foreach(bool blocked in new[]{false,true})for(int direction=0;direction<8;direction++)for(int scenario=0;scenario<4;scenario++)
            {
                var f=new Fixture(7,7);int sx=103,sy=103,tx=sx+VirtualBridgeMap.Dx[direction]*2,ty=sy+VirtualBridgeMap.Dy[direction]*2;
                int origin=sy*W+sx;if(scenario==1)f.Edge[origin]=0;
                var managed=new VirtualCandidateBuilder();void Put(int address,Array a){byte[] bytes=Bytes(a);native.Put(address,bytes);managed.Put(address,bytes);}
                var valid=new byte[640000];for(int i=0;i<valid.Length;i++)valid[i]=1;var rows=new int[2400];for(int y=0;y<800;y++)rows[y*3]=f.Rows[y];var blocks=new byte[N];if(scenario==2)blocks[origin+f.Offsets[sy*8+direction]]=1;
                Put(0x3a11ea4,valid);Put(0x402ff2c,rows);Put(0x405edb0,f.Offsets);Put(0x51890d0,f.Edge);Put(0x51d75f0,blocks);Put(0x5225b10,new short[N]);Put(Visits,new short[N]);
                Put(Root,new byte[224]);Put(Root+0x155f3c,new byte[32]);Put(Root+0x155f68,new byte[4]);Put(Root+0x155f6c,new int[N]);Put(Root+0x28f3ec,new short[N]);native.Int(Root+4,scenario==3?32000:9);managed.Replace(Root+4,BitConverter.GetBytes(scenario==3?32000:9));
                Put(0x48f71b0,f.Flag);Put(0x4b6aa50,new short[N]);Put(0x2e68d0,new int[336]);var masks=new byte[8];var directions=new int[16];for(int d=0;d<8;d++){masks[d]=(byte)(1<<d);directions[d*2]=VirtualBridgeMap.Dx[d];directions[d*2+1]=VirtualBridgeMap.Dy[d];}Put(0x312620,masks);Put(0x2d2e50,directions);
                int limit=scenario==3?1:400000;var run=native.Function<Flood7>(blocked?0xDAAC0:0xDA590);
                for(int continuation=0;continuation<=(scenario==3?1:0);continuation++)
                {
                    int n=run(native.Ptr(Root),sx,sy,tx,ty,continuation==1?400000:limit,continuation),m=managed.CommandFlood(sx,sy,tx,ty,continuation==1?400000:limit,continuation,blocked);Check(n==m,"normal flood result");
                    foreach(var range in new[]{Tuple.Create(Root,224),Tuple.Create(Root+0x155f3c,32),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2),Tuple.Create(Visits,N*2),Tuple.Create(0x5225b10,N*2)})Equal(native.Bytes(range.Item1,range.Item2),managed.Bytes(range.Item1,range.Item2),"complete flood queue/controls/visited/distances");
                }
                if(native.Int(Root+0x155f3c)!=native.Int(Root+0x155f44)&&native.Int(Root+0x155f58)<400000&&scenario!=1)
                {
                    var path=new byte[1000];native.Put(0x8790000,path);native.Long(Root+0x155f60,native.Ptr(0x8790000).ToInt64());native.Int(Root+0x155f68,0);managed.Replace(Root+0x155f68,new byte[4]);
                    native.Function<Reconstruct6>(0xE1640)(native.Ptr(Root),sx,sy,tx,ty,1);managed.CommandReconstruct(sx,sy,tx,ty,path);
                    Equal(native.Bytes(0x8790000,1000),path,"full mode1 reconstructed/reversed path bytes");Check(native.Int(Root+0x155f68)==BitConverter.ToInt32(managed.Bytes(Root+0x155f68,4),0),"reconstructed count");Check(native.Int(Root+0x94)==BitConverter.ToInt32(managed.Bytes(Root+0x94,4),0),"obstacle count");
                }
                cases++;
            }
            Console.WriteLine("PASS DAAC0/DA590 native/managed:64 cases,8 directions,blocked grid,dead ends,generation reset and continuation;full path reconstruction remains separate");
        }
        private static int HistoricalOwnCommand(string file)
        {
            if(!BridgePlanningImporter.TryRead(file,out var artifact,out string reason))throw new Exception(reason);
            if(artifact.Hash!="3E471B66DEB3CC4237450EAF1406EC7E951E2E3A45AA15EB9F85D52FC030DC61"&&artifact.Hash!="5FE8D6432D3D41B72BC0FA2EEA74580FE0F60EADEF2B31FC79595816DB712F9B"&&artifact.Hash!="1CAE92A57B9A08C23EE74E0817F733243D1A6B73C8D9175C22C26031DA9F709E")throw new Exception("unapproved historical own command");
            if(!CopiedStructureInput.TryCreate(artifact,out var memory,out reason))throw new Exception(reason);
            using(var native=new NativeImage(Native,System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv")))
            {
                // Refuse executing a native body on PE-initialized mutable data.
                // This is input closure, not a claimed command/path replay.
                var arguments=artifact.Sections["structure/unitCommandArguments7"];int id=checked((int)BitConverter.ToInt64(arguments,16));
                int record=checked(0x67e8400+id*0x490),packed=BitConverter.ToInt32(memory.Bytes(record+0x72c,4),0),sy=BitConverter.ToInt16(memory.Bytes(0x3aae2a4+packed*2,2),0),sx=packed-BitConverter.ToInt32(memory.Bytes(0x402ff2c+sy*12,4),0);
                int tx=checked((int)BitConverter.ToInt64(arguments,32)),ty=checked((int)BitConverter.ToInt64(arguments,40));
                foreach(var range in new[]{Tuple.Create(0x3a11ea4,640000),Tuple.Create(0x402ff2c,9600),Tuple.Create(0x2f7a690,4),Tuple.Create(0x405edb0,25600),Tuple.Create(0x2d2e50,64),Tuple.Create(0x51890d0,320800),Tuple.Create(0x48f71b0,1283200),Tuple.Create(0x4b6aa50,641600),Tuple.Create(0x2e68d0,1344),Tuple.Create(0x52c2550,641600),Tuple.Create(0x60ad660,224)})native.Put(range.Item1,memory.Bytes(range.Item1,range.Item2));
                native.Put(0x64ccbb0+0x32c,memory.Bytes(0x64ccbb0+0x32c,artifact.Sections["structure/pre/buildingRecords"].Length));
                // Private output memory is initialized by the caller, never an old recorded path.
                const int bank=0x8790000;var privatePath=new byte[1000];native.Put(bank,privatePath);native.Long(0x60ad660+0x155f60,native.Ptr(bank).ToInt64());native.Int(0x60ad660+0x155f68,0);memory.Replace(0x60ad660+0x155f68,new byte[4]);
                int nativeLine=native.Function<R5>(0xF32B0)(native.Ptr(0x60ad660),sx,sy,tx,ty),managedLine=memory.CommandLine(sx,sy,tx,ty,privatePath);
                Console.WriteLine("line-probe native="+nativeLine+",managed="+managedLine+",native-count="+native.Int(0x60ad660+0x155f68)+",managed-count="+BitConverter.ToInt32(memory.Bytes(0x60ad660+0x155f68,4),0)+",nativeBytes="+BitConverter.ToString(native.Bytes(bank,8))+",managedBytes="+BitConverter.ToString(privatePath,0,8));
                Check(nativeLine==managedLine,"own line native return");Equal(native.Bytes(bank,1000),privatePath,"own line packed bytes");Equal(native.Bytes(0x52c2550,641600),memory.Bytes(0x52c2550,641600),"own line visit stamps");Check(native.Int(0x60ad660+0x155f68)==BitConverter.ToInt32(memory.Bytes(0x60ad660+0x155f68,4),0),"own line cursor count");
                Console.WriteLine("isolatedF32B0NativeManaged=Matched;lineResult="+nativeLine+";origin="+sx+"/"+sy+";target="+tx+"/"+ty+";fullCommandReplay=Unknown;no-recorded-post-used");
                if(nativeLine==0)
                {
                    foreach(var range in new[]{Tuple.Create(0x51d75f0,N),Tuple.Create(0x5225b10,N*2),Tuple.Create(Root+0x155f38,0x34),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2)})native.Put(range.Item1,memory.Bytes(range.Item1,range.Item2));
                    int radius=checked(Math.Max(Math.Abs(sx-tx),Math.Abs(sy-ty))+4),limit=checked(radius*radius*2);
                    int n=native.Function<Flood7>(0xDAAC0)(native.Ptr(Root),sx,sy,tx,ty,limit,0),m=memory.CommandFlood(sx,sy,tx,ty,limit,0,true);
                    Check(n==m,"own blocked flood native result");
                    foreach(var range in new[]{Tuple.Create(Visits,N*2),Tuple.Create(0x5225b10,N*2),Tuple.Create(Root+0x155f3c,32),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2)})Equal(native.Bytes(range.Item1,range.Item2),memory.Bytes(range.Item1,range.Item2),"own normal flood output");
                    Console.WriteLine("isolatedDAAC0NativeManaged=Matched;result="+n+";limit="+limit+";radiusSource=audited-79C0-max-axis-plus4;fullCommandReplay=Unknown;native-entry-not-executed");
                    if(n==0){n=native.Function<Flood7>(0xDA590)(native.Ptr(Root),sx,sy,tx,ty,400000,0);m=memory.CommandFlood(sx,sy,tx,ty,400000,0,false);Check(n==m,"own unrestricted flood native result");foreach(var range in new[]{Tuple.Create(Visits,N*2),Tuple.Create(0x5225b10,N*2),Tuple.Create(Root+0x155f3c,32),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2)})Equal(native.Bytes(range.Item1,range.Item2),memory.Bytes(range.Item1,range.Item2),"own unrestricted flood output");Console.WriteLine("isolatedDA590NativeManaged=Matched;result="+n+";fullCommandReplay=Unknown");}
                }
                if(!CopiedStructureInput.TryCommandClosure(artifact,out reason))
                {
                    Console.WriteLine("nativeCommandExecution=refused;productiveCommandExecution=Unknown;"+reason+";fullNativeHashValidated=True;behaviorFix=disabled");return 2;
                }
                Console.WriteLine("nativeCommandExecution=Unknown:full-continuation-not-yet-validated;"+reason);return 2;
            }
        }
    }
}
