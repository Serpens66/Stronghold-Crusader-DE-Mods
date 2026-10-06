using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using APIShared;
using SHCDESE.Interop;

namespace EnemyBridgePathTest
{
    // Offline decoder feeds the production Prepare/Pump adapter, not a second graph implementation.
    internal static class ShadowControlTests
    {
        private static int checks;
        private static void Check(bool value,string text) {checks++;if(!value)throw new Exception("Shadow controls: "+text);}
        private static object Create(string name) => Activator.CreateInstance(typeof(BridgeVirtualShadow).GetNestedType(name,BindingFlags.NonPublic),true);
        private static void Set(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static object Get(object target,string name) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private sealed class Policy : IEnemyGateRoutePolicySnapshot
        {
            internal byte[] Mask;internal bool Current=true;
            public int PlayerId => 8;
            public bool IsCurrent => Current;
            public bool IsDirectionAllowed(int tile,int direction) => (Mask[tile]&(1<<direction))!=0;
        }
        private static object Input(int width,bool alternative)
        {
            int n=width*(alternative?2:1);var pcl=new ushort[n];var edges=new byte[n];var x=new ushort[n];var y=new ushort[n];var rows=new int[alternative?2:1];
            for(int tile=0;tile<n;tile++) {pcl[tile]=1;x[tile]=(ushort)(tile%width);y[tile]=(ushort)(tile/width);if(x[tile]+1<width)edges[tile]|=4;if(x[tile]>0)edges[tile]|=64;if(alternative)edges[tile]|=(byte)(y[tile]==0?16:1);}
            if(alternative)rows[1]=width;
            object input=Create("Captured");Set(input,"Session",1L);Set(input,"Revision",1L);Set(input,"Identity",1L);Set(input,"Clock",123L);
            Set(input,"Pcl",pcl);Set(input,"Edges",edges);Set(input,"Flags",new int[n]);Set(input,"X",x);Set(input,"Y",y);Set(input,"Rows",rows);Set(input,"SpecialIds",new ushort[n]);Set(input,"SpecialKinds",new short[1]);Set(input,"Anchors",new[]{-1,0});
            Set(input,"Owners",new int[1]);Set(input,"Capturers",new int[1]);Set(input,"Globals",new uint[1]);Set(input,"UnitGlobals",new uint[1]);Set(input,"Records",new PathConnectionRecord[1]);Set(input,"ConnectionLimit",1);
            var allies=new bool[9,9];for(int i=1;i<9;i++)allies[i,i]=true;Set(input,"Allies",allies);
            object d=Create("Deck");Set(d,"Id",703);Set(d,"Global",10u);Set(d,"Owner",1);Set(d,"Capturer",0);Set(d,"Tiles",new[]{2});
            Array decks=Array.CreateInstance(d.GetType(),1);decks.SetValue(d,0);Set(input,"Decks",decks);
            Set(input,"Map",new VirtualBridgeMap(1,1,pcl,edges,new int[n],x,y,rows,Array.Empty<VirtualConnection>(),true));return input;
        }
        private static object Request(object input,Policy policy,int native=1)
        {
            object request=Create("Request");Set(request,"Id",1L);Set(request,"Op",40L);Set(request,"Stage","keep-access");Set(request,"Player",8);Set(request,"From",0);Set(request,"To",4);Set(request,"Native",native);Set(request,"Input",input);Set(request,"GatePolicy",policy);Set(request,"PolicyValidAtDecision",policy==null||policy.IsCurrent);return request;
        }
        private static List<string> Calculate(object request,bool invalidate=false)
        {
            var messages=new List<string>();var shadow=new BridgeVirtualShadow((kind,detail)=>messages.Add(kind+":"+detail),_=>throw new Exception("render read"));shadow.Begin(1);
            if(invalidate)shadow.Invalidate();
            Set(shadow,"active",request);for(int i=0;i<10000&&Get(shadow,"active")!=null;i++)shadow.Pump();
            Check(Get(shadow,"active")==null,"bounded completion");return messages;
        }
        private static IEnumerable<byte[]> SmallArtifact()
        {
            yield return BridgeInputArtifact.Header("fixture=True");
            foreach(var block in BridgeInputArtifact.Section("large",new int[200000],4))yield return block;
        }
        internal static Dictionary<string,byte[]> Decode(string path,out Dictionary<string,string> metadata)
        {
            if(Path.GetExtension(path)!=".bin")throw new InvalidDataException("Unpublished partial artifact");
            byte[] bytes=File.ReadAllBytes(path);if(bytes.Length<64||Encoding.ASCII.GetString(bytes,bytes.Length-8,8)!="BRGEND01")throw new InvalidDataException("Missing footer");
            long payload=BitConverter.ToInt64(bytes,bytes.Length-16);if(payload!=bytes.Length-48)throw new InvalidDataException("Length mismatch");
            byte[] hash=SHA256.Create().ComputeHash(bytes,0,(int)payload);for(int i=0;i<32;i++)if(hash[i]!=bytes[bytes.Length-48+i])throw new InvalidDataException("Payload hash mismatch");
            var sections=new Dictionary<string,byte[]>();metadata=new Dictionary<string,string>();
            using(var reader=new BinaryReader(new MemoryStream(bytes,0,(int)payload),Encoding.UTF8))
            {
                if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="BRGINP01"||reader.ReadInt32()!=1)throw new InvalidDataException("Unknown schema");
                int size=reader.ReadInt32();if(size<0||size>65536)throw new InvalidDataException("Metadata extent");
                foreach(string line in Encoding.UTF8.GetString(reader.ReadBytes(size)).Split('\n')) {int eq=line.IndexOf('=');if(eq>0)metadata.Add(line.Substring(0,eq),line.Substring(eq+1));}
                while(reader.BaseStream.Position<payload)
                {
                    int length=reader.ReadInt32();if(length<1||length>128)throw new InvalidDataException("Section name extent");
                    string name=Encoding.UTF8.GetString(reader.ReadBytes(length));int count=reader.ReadInt32(),width=reader.ReadInt32();
                    long extent=(long)count*width;if(count<0||width!=1&&width!=2&&width!=4||extent>16000000||extent>payload-reader.BaseStream.Position)throw new InvalidDataException("Section extent");
                    byte[] data=reader.ReadBytes((int)extent);if(name=="gateMaskChunk") {sections.TryGetValue("gateMask",out byte[] previous);sections["gateMask"]=(previous??Array.Empty<byte>()).Concat(data).ToArray();}else sections.Add(name,data);
                }
            }
            return sections;
        }
        private static T[] ArrayOf<T>(byte[] bytes,int width) where T:struct {var result=new T[bytes.Length/width];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result;}
        internal static int Replay(string path)
        {
            var sections=Decode(path,out var meta);if(meta["nativeHash"]!=BridgeNativeDefinition.NativeHash||meta["backendHash"]!=BridgeNativeDefinition.BackendHash)throw new InvalidDataException("Unsupported Native/backend provenance");object input=Create("Captured");
            foreach(string field in new[]{"components","edges","flags","x","y","rows","specialIds","specialKinds","owners","capturers","globals","unitGlobals"})
            {
                string member=field=="components"?"Pcl":char.ToUpperInvariant(field[0])+field.Substring(1);
                object value=field=="edges"?sections[field]:field=="globals"||field=="unitGlobals"?(object)ArrayOf<uint>(sections[field],4):field=="specialKinds"?ArrayOf<short>(sections[field],2):field=="components"||field=="x"||field=="y"||field=="specialIds"?(object)ArrayOf<ushort>(sections[field],2):ArrayOf<int>(sections[field],4);Set(input,member,value);
            }
            foreach(string key in new[]{"session","revision","identity","captureClock"})Set(input,key=="captureClock"?"Clock":char.ToUpperInvariant(key[0])+key.Substring(1),long.Parse(meta[key]));
            var allies=new bool[9,9];for(int a=0;a<9;a++)for(int b=0;b<9;b++)allies[a,b]=sections["allies"][a*9+b]!=0;Set(input,"Allies",allies);
            int[] raw=ArrayOf<int>(sections["records13"],4);var records=new PathConnectionRecord[raw.Length/13];for(int i=0;i<records.Length;i++)
            {int p=i*13;records[i]=new PathConnectionRecord {r_IsActive=raw[p],r_ConnectionClass=(SHCDESE.Interop.Enums.PathConnectionClass)raw[p+1],r_RecordGlobalId=unchecked((uint)raw[p+2]),r_BuildingId=raw[p+3],r_UnitId=raw[p+4],r_SubjectGlobalId=unchecked((uint)raw[p+5]),r_IsEnabledOrOpen=raw[p+6],r_EntryTileId=raw[p+7],r_ExitTileId=raw[p+8],r_PathComponentA=raw[p+9],r_PathComponentB=raw[p+10],r_OwnerOrAccessPlayerId=raw[p+11],r_PathComponentC=raw[p+12]};}
            Set(input,"Records",records);Set(input,"ConnectionLimit",int.Parse(meta["connectionLimit"]));
            var decks=new List<object>();int[] rd=ArrayOf<int>(sections["decks"],4);for(int p=0;p<rd.Length;)
            {object d=Create("Deck");Set(d,"Id",rd[p++]);Set(d,"Global",unchecked((uint)rd[p++]));Set(d,"Owner",rd[p++]);Set(d,"Capturer",rd[p++]);Set(d,"NativeParent",rd[p++]);Set(d,"Authorized",rd[p++]!=0);Set(d,"BoundaryVerified",rd[p++]!=0);int count=rd[p++];Set(d,"Tiles",rd.Skip(p).Take(count).ToArray());p+=count;decks.Add(d);}
            var deckArray=System.Array.CreateInstance(typeof(BridgeVirtualShadow).GetNestedType("Deck",BindingFlags.NonPublic),decks.Count);for(int i=0;i<decks.Count;i++)deckArray.SetValue(decks[i],i);Set(input,"Decks",deckArray);
            var pcl=(ushort[])Get(input,"Pcl");var anchors=new int[65536];for(int i=0;i<anchors.Length;i++)anchors[i]=-1;for(int i=0;i<pcl.Length;i++)if(pcl[i]!=0&&anchors[pcl[i]]<0)anchors[pcl[i]]=i;Set(input,"Anchors",anchors);
            Set(input,"Map",new VirtualBridgeMap(long.Parse(meta["session"]),long.Parse(meta["revision"]),pcl,(byte[])Get(input,"Edges"),(int[])Get(input,"Flags"),(ushort[])Get(input,"X"),(ushort[])Get(input,"Y"),(int[])Get(input,"Rows"),Array.Empty<VirtualConnection>(),true,(ushort[])Get(input,"SpecialIds"),(short[])Get(input,"SpecialKinds")));
            var policy=meta["gateFilter"]=="absent"?null:new Policy {Mask=sections["gateMask"]};object request=Request(input,policy,int.Parse(meta["nativeBoolean"]));
            foreach(string key in new[]{"definition","op","parent","observationClock"})Set(request,key=="definition"?"Id":key=="observationClock"?"Clock":char.ToUpperInvariant(key[0])+key.Substring(1),long.Parse(meta[key]));
            foreach(string key in new[]{"player","from","to","mode"})Set(request,char.ToUpperInvariant(key[0])+key.Substring(1),int.Parse(meta[key]));Set(request,"Stage",meta["stage"]);Set(request,"PolicyValidAtDecision",bool.Parse(meta["policyValidAtDecision"]));
            var messages=new List<string>();var shadow=new BridgeVirtualShadow((k,d)=>messages.Add(k+":"+d),_=>throw new Exception("offline native read"));shadow.Begin(long.Parse(meta["session"]));Set(shadow,"active",request);
            while(Get(shadow,"active")!=null)shadow.Pump();foreach(string message in messages)Console.WriteLine(message);return 0;
        }
        internal static int Run()
        {
            checks=0;
            Check(VirtualComponentControl.Evaluate(1,1,0,Array.Empty<VirtualComponentLink>())==VirtualReachability.Reachable,"same component shortcut");
            Check(VirtualComponentControl.Evaluate(0,0,0,Array.Empty<VirtualComponentLink>())==VirtualReachability.NoRoute,"zero native result");
            var links=new[]{new VirtualComponentLink(1,2,3,1,true),new VirtualComponentLink(3,4,0,2,true)};
            Check(VirtualComponentControl.Evaluate(1,4,0,links)==VirtualReachability.NoRoute&&VirtualComponentControl.Evaluate(1,4,1,links)==VirtualReachability.Reachable,"both macro passes and modes");
            foreach(bool alternative in new[]{false,true})
            {
                object input=Input(5,alternative);var policy=new Policy {Mask=Enumerable.Repeat((byte)255,alternative?10:5).ToArray()};object request=Request(input,policy);policy.Current=false;
                var messages=Calculate(request,true);Check(messages.Count==3&&messages[0].Contains("noCut=Reachable")&&messages[0].Contains("only703="+(alternative?"Reachable":"NoRoute")),"historical policy/topology retained, alternatives and cut controls");
                Check(messages[0].Contains("macroMatchesNative=True")&&messages[1].Contains("geometryMatchesNative=True")&&messages[2].Contains("policyResult=Unknown"),"native direction baseline gates cut evidence and never enables policy");
                messages=Calculate(Request(input,policy));Check(messages.Count==1&&messages[0].Contains("policyValidAtDecision=False"),"already stale policy at decision cannot become evidence");
                messages=Calculate(Request(input,null,0));Check(messages[0].Contains("cutAssessment=blocked-baseline"),"baseline disagreement blocks interpretation");
            }
            // Cutting only 703 leaves the second deck route; cutting both closes it.
            object multiple=Input(5,true);Array first=(Array)Get(multiple,"Decks");object other=Create("Deck");Set(other,"Id",720);Set(other,"Global",11u);Set(other,"Owner",1);Set(other,"Tiles",new[]{7});
            Array both=System.Array.CreateInstance(other.GetType(),2);both.SetValue(first.GetValue(0),0);both.SetValue(other,1);Set(multiple,"Decks",both);
            var comparison=Calculate(Request(multiple,null));Check(comparison[0].Contains("only703=Reachable,allHostile=NoRoute")&&comparison[0].Contains("cutCells=0/1/2"),"single bridge distinction and alternate hostile bridge");
            foreach(string role in new[]{"owner","ally","capturer"})
            {
                object roles=Input(5,false),d=((Array)Get(roles,"Decks")).GetValue(0);
                if(role=="owner")Set(d,"Owner",8);else if(role=="capturer")Set(d,"Capturer",8);else ((bool[,])Get(roles,"Allies"))[8,1]=true;
                Check(Calculate(Request(roles,null))[0].Contains("only703=Reachable,allHostile=Reachable,cutCells=0/0/0"),"role allows deck: "+role);
            }
            object directed=Input(5,false);Array.Clear((byte[])Get(directed,"Edges"),0,5);for(int i=0;i<4;i++)((byte[])Get(directed,"Edges"))[i]=4;
            Set(directed,"Map",new VirtualBridgeMap(1,1,(ushort[])Get(directed,"Pcl"),(byte[])Get(directed,"Edges"),new int[5],(ushort[])Get(directed,"X"),(ushort[])Get(directed,"Y"),(int[])Get(directed,"Rows"),Array.Empty<VirtualConnection>(),true));
            comparison=Calculate(Request(directed,null));Check(comparison[0].Contains("noCut=Reachable")&&comparison[1].Contains("noCut=NoRoute")&&comparison[0].Contains("geometryMatchesNative=False,cutAssessment=blocked"),"directed mismatch blocks cut interpretation despite component equality");
            object unresolved=Input(5,false);Set(unresolved,"Records",new[]{new PathConnectionRecord(),new PathConnectionRecord {r_IsActive=1,r_IsEnabledOrOpen=1,r_ConnectionClass=(SHCDESE.Interop.Enums.PathConnectionClass)2,r_OwnerOrAccessPlayerId=8,r_EntryTileId=0,r_ExitTileId=4,r_PathComponentA=1,r_PathComponentB=1,r_PathComponentC=1}});Set(unresolved,"ConnectionLimit",2);
            comparison=Calculate(Request(unresolved,null));Check(comparison.Any(v=>v.Contains("only703=Unknown")),"no guessed physical third endpoint proves a negative");
            var ended=new BridgeVirtualShadow((k,d)=>{},_=>throw new Exception("map read"));ended.Begin(1);Set(ended,"active",Request(Input(5,false),null));ended.End();ended.Begin(2);ended.Pump();Check(Get(ended,"active")==null,"map end cancels old work and reused IDs");
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bridge-artifact-tests-"+Guid.NewGuid().ToString("N"));var output=new List<string>();var writer=new BridgeInputArtifact(folder,(s,k,d)=>output.Add(s+":"+k+":"+d));
            Check(writer.Enqueue(1,1,SmallArtifact())&&writer.Enqueue(1,2,SmallArtifact())&&!writer.Enqueue(1,3,SmallArtifact()),"max two artifacts per session");
            Check(writer.Enqueue(2,3,SmallArtifact()),"new session does not lose older writer");int iterations=0;
            while(writer.Pending(1)||writer.Pending(2)) {long before=writer.Bytes;writer.Pump();Check(writer.Bytes-before<=65536,"one 64KiB write per render");Check(++iterations<10000,"artifact progress bounded");}
            Check(output.Count(x=>x.Contains("input-artifact-complete"))==3,"delivery remains valid after session end/new map");
            string path=Path.Combine(folder,"bridge-1-1.bin");var decoded=Decode(path,out var metadata);Check(decoded["large"].Length==800000&&metadata["fixture"]=="True","full binary reconstruction");
            byte[] file=File.ReadAllBytes(path);foreach(int cut in new[]{1,48,65536})
            {string torn=Path.Combine(folder,"torn-"+cut+".bin");File.WriteAllBytes(torn,file.Take(file.Length-cut).ToArray());try {Decode(torn,out metadata);throw new Exception("accepted truncation");}catch(InvalidDataException) {checks++;}}
            file[50]^=1;string corrupt=Path.Combine(folder,"corrupt.bin");File.WriteAllBytes(corrupt,file);try {Decode(corrupt,out metadata);throw new Exception("accepted corruption");}catch(InvalidDataException) {checks++;}
            // Exercise the actual production artifact iterator, then actual production replay.
            object realRequest=Request(Input(5,false),new Policy {Mask=Enumerable.Repeat((byte)255,5).ToArray()});var shadowSource=new BridgeVirtualShadow((k,d)=>{},_=>throw new Exception("artifact native read"));
            var source=(IEnumerable<byte[]>)typeof(BridgeVirtualShadow).GetMethod("Artifact",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(shadowSource,new[]{realRequest});
            Check(writer.Enqueue(3,4,source),"production artifact accepted");while(writer.Pending(3))writer.Pump();Replay(Path.Combine(folder,"bridge-3-4.bin"));
            Console.WriteLine("Artifact regression retained at "+folder);return checks;
        }
    }
}
