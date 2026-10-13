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
        private static int HistoricalOwnCommand(string file)
        {
            if(!BridgePlanningImporter.TryRead(file,out var artifact,out string reason))throw new Exception(reason);
            if(artifact.Hash!="3E471B66DEB3CC4237450EAF1406EC7E951E2E3A45AA15EB9F85D52FC030DC61"&&artifact.Hash!="5FE8D6432D3D41B72BC0FA2EEA74580FE0F60EADEF2B31FC79595816DB712F9B")throw new Exception("unapproved historical own command");
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
                if(!CopiedStructureInput.TryCommandClosure(artifact,out reason))
                {
                    Console.WriteLine("nativeCommandExecution=refused;productiveCommandExecution=Unknown;"+reason+";fullNativeHashValidated=True;behaviorFix=disabled");return 2;
                }
                Console.WriteLine("nativeCommandExecution=Unknown:full-continuation-not-yet-validated;"+reason);return 2;
            }
        }
    }
}
