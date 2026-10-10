using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EnemyBridgePathTest;
namespace BridgePlanningTests
{
    internal static partial class Program
    {
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
        private delegate int R3(IntPtr self,int a,int b);
        // Explicit original files, not an arbitrary-file bypass. Importer also verifies envelope.
        private static bool ApprovedPlanning(string hash) =>
            hash=="3C5290A9081930B4E0B3D55A537217DA2CDD04A497034403585D29ED059DB7AB" ||
            hash=="15091D5F73D6CCE6BC3487563BB46CFDFB5329977E0AA62B2EFB2253B68D8872";

        private static byte[] Bytes(Array data){var bytes=new byte[Buffer.ByteLength(data)];Buffer.BlockCopy(data,0,bytes,0,bytes.Length);return bytes;}
        private static T[] ArrayValues<T>(byte[] bytes) where T:struct
        {var result=new T[bytes.Length/System.Runtime.InteropServices.Marshal.SizeOf(typeof(T))];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result;}
        private static int Changed(byte[] actual,byte[] expected){Check(actual.Length==expected.Length,"changed-buffer extent");int count=0;for(int i=0;i<actual.Length;i++)if(actual[i]!=expected[i])count++;return count;}
        private static VirtualPlanningState ComputedVariant(BridgePlanningImporter.Artifact a,int variant,out VirtualBridgeMap physical,out int[] counts)
        {
            if(variant<0||variant>2)throw new ArgumentException("variant");
            var b=a.Planning;byte[] Get(string name)=>CopiedPlanningBundle.Resolve(b,name);
            var map=new VirtualBridgeMap(b.Session,b.Revision,ArrayValues<ushort>(a.Sections["components"]),a.Sections["edges"],ArrayValues<int>(a.Sections["flags"]),ArrayValues<ushort>(a.Sections["x"]),ArrayValues<ushort>(a.Sections["y"]),ArrayValues<int>(a.Sections["rows"]),Array.Empty<VirtualConnection>(),true,ArrayValues<ushort>(a.Sections["specialIds"]),ArrayValues<short>(a.Sections["specialKinds"]));
            Equal(Bytes(map.Flags),Get("consumer/pre/flags"),"same physical flags at caller and consumer");Equal(Bytes(map.Components),Get("consumer/pre/components"),"same published components at caller and consumer");Equal(map.Edges,Get("consumer/pre/edges"),"same physical directions at caller and consumer");
            VirtualPlanningInput input;VirtualPlanningState state;string reason;
            if(!CopiedPlanningBundle.TryRead(b,map,"caller-pre",out input,out state,out reason))throw new Exception(reason);
            physical=map;counts=new int[1000];foreach(int c in map.Components){if(c>=1000)throw new Exception("component bound");if(c!=0)counts[c]++;}
            if(variant!=0)
            {
                // Reuse the production validator for coupling identities and all role checks.
                if(!CopiedPlanningReplay.TryVariants(a,out reason))throw new Exception(reason);
                var decks=ArrayValues<int>(a.Sections["decks"]);var parents=ArrayValues<int>(a.Sections["coupledParents5"]);var cut=new List<int>();
                bool Related(int p)=>p!=0&&(p==b.Attacker||a.Sections["allies"][b.Attacker*9+p]!=0);
                for(int p=0;p<decks.Length;)
                {
                    int id=decks[p],count=decks[p+7],parent=-1;for(int q=0;q<parents.Length;q+=5)if(parents[q]==id)parent=q;
                    if(parent<0)throw new Exception("missing coupling");
                    bool allowed=Related(decks[p+2])||Related(decks[p+3])||Related(parents[parent+3])||Related(parents[parent+4]);
                    if(!allowed&&(variant==2||id==703))cut.AddRange(decks.Skip(p+8).Take(count));p+=8+count;
                }
                int[] rr=ArrayValues<int>(Get("rowRecords")),ends=new int[800];for(int y=0;y<800;y++)ends[y]=rr[y*3+2];
                var publication=new VirtualRaisedPlanning(input,map,cut.ToArray(),ends,Get("buildingRecords"),ArrayValues<ushort>(Get("buildingIds")));
                while(!publication.Rebuild.Complete)publication.Step(4096);if(!publication.Rebuild.Proven)throw new Exception(publication.Rebuild.Reason);
                input=publication.Planning;physical=publication.Physical;counts=publication.Rebuild.Counts;
            }
            Func<int,int,int?> keep,regions;
            if(!CopiedPlanningRegions.TryCreate(b,"caller-pre",false,b.Attacker,0,variant==0?null:map,variant==0?null:physical.Components,out keep,out reason)||!CopiedPlanningRegions.TryCreate(b,"distance-pre",true,b.Attacker,0,variant==0?null:map,variant==0?null:physical.Components,out regions,out reason))throw new Exception(reason);
            var players=ArrayValues<int>(Get("playerScalars7"));int target=map.Tile(players[b.Target*7+1],players[b.Target*7+2]),origin=physical.Components[players[b.Attacker*7+3]];int? reachable=keep(physical.Components[target],origin);if(!reachable.HasValue)throw new Exception("unknown copied castle query");
            var call=new VirtualPlanningCall(b.Attacker,b.Target,b.Mode,players[b.Target*7+5],origin,reachable.Value!=0);
            if(!VirtualPlanningCaller.Run(input,state,call,target,players[b.Target*7]!=0,regions))throw new Exception(state.Reason);
            if(variant==0){Equal(state.Seeds.Select(v=>unchecked((byte)v)).ToArray(),Get("consumer/pre/seeds"),"computed seeds at consumer entry");Equal(Bytes(state.Distance),Get("distance-post/distance"),"computed distance at consumer entry");}
            return state;
        }
        private static byte[] RemapConnections(byte[] original,BridgePlanningImporter.Artifact a,ushort[] rebuilt)
        {
            var bytes=(byte[])original.Clone();var b=a.Planning;
            var map=new VirtualBridgeMap(b.Session,b.Revision,ArrayValues<ushort>(a.Sections["components"]),a.Sections["edges"],ArrayValues<int>(a.Sections["flags"]),ArrayValues<ushort>(a.Sections["x"]),ArrayValues<ushort>(a.Sections["y"]),ArrayValues<int>(a.Sections["rows"]),Array.Empty<VirtualConnection>(),true);
            for(int at=0;at<bytes.Length;at+=0x204)
            {
                if(BitConverter.ToInt32(bytes,at)!=1||BitConverter.ToInt32(bytes,at+24)==0)continue;
                int entry=BitConverter.ToInt32(bytes,at+36),exit=BitConverter.ToInt32(bytes,at+48),c=BitConverter.ToInt32(bytes,at+0x1e8),third=0;
                if((uint)entry>=N||(uint)exit>=N||map.Components[entry]!=BitConverter.ToInt32(bytes,at+52)||map.Components[exit]!=BitConverter.ToInt32(bytes,at+56))throw new Exception("unproved macro endpoints");
                if(c!=0&&!VirtualGateEndpoint.TryResolve(map,BitConverter.ToInt32(bytes,at+4),entry,exit,c,out third))throw new Exception("unproved third endpoint");
                Buffer.BlockCopy(BitConverter.GetBytes((int)rebuilt[entry]),0,bytes,at+52,4);Buffer.BlockCopy(BitConverter.GetBytes((int)rebuilt[exit]),0,bytes,at+56,4);
                if(c!=0)Buffer.BlockCopy(BitConverter.GetBytes((int)rebuilt[third]),0,bytes,at+0x1e8,4);
            }
            return bytes;
        }

        private static int HistoricalCandidates(string path,bool continuous=false,int variant=0,bool referenceTables=false)
        {
            BridgePlanningImporter.Artifact a;string reason;
            if(!BridgePlanningImporter.TryRead(path,out a,out reason))throw new Exception(reason);
            if(!ApprovedPlanning(a.Hash)||a.Planning==null)throw new Exception("unapproved historical candidate input");
            var b=a.Planning;
            if(!CopiedPlanningReplay.TryBaseline(a,out reason))throw new Exception(reason);
            if(!CopiedPlanningReplay.TryConsumerInputClosure(a,out reason))throw new Exception(reason);
            byte[] Get(string name)=>CopiedPlanningBundle.Resolve(b,name);
            var tableInputs=new Dictionary<int,byte[]>();
            foreach(var table in new[]{Tuple.Create("nativeWorkDirections64",0x2d2e50,64),Tuple.Create("nativeDirectionMasks8",0x312620,8),Tuple.Create("nativeWorkBuildingClasses336",0x2e68d0,336*4)})
            {
                string key="consumer/pre/"+table.Item1;
                if(b.Sections.ContainsKey(key)||b.References.ContainsKey(key))
                {var value=Get(key);if(value.Length!=table.Item3)throw new Exception("Unknown:invalid-consumer-table-extent:"+key);tableInputs.Add(table.Item2,value);}
                else if(!referenceTables)throw new Exception("Unknown:missing-own-consumer-native-table:"+key+",rva="+table.Item2.ToString("X")+",bytes="+table.Item3+";PE-initial-values-are-not-historical-inputs");
            }
            Console.WriteLine("historical-table-coverage="+tableInputs.Count+"/3,reference-table-hypothesis="+referenceTables+",negativePolicyEligible=False");
            var variantState=ComputedVariant(a,variant,out var physical,out var counts);
            using(var m=new NativeImage(Native,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv")))
            {
                foreach(var table in tableInputs)m.Put(table.Key,table.Value);
                void Put(string name,int address)=>m.Put(address,Get(name));
                Put(continuous?"consumer/pre/candidates":"consumer/build-pre/candidates",VirtualCandidateConsumers.Root);
                Put("consumer/pre/players",0x379AE00);Put("consumer/pre/unitManager",0x67E8400);
                Put("consumer/pre/unitTiles",0x4C559B0);Put("consumer/pre/flags",Flags);
                Put("consumer/pre/components",Pcl);Put("consumer/pre/edges",Edges);
                Put("consumer/pre/height",0x4DDD350);Put("consumer/pre/baseHeight",0x4E2B870);
                Put("consumer/pre/terrainOwner",0x4E79D90);Put("consumer/pre/buildingIds",0x4B6AA50);
                Put("consumer/pre/buildingCount",0x64CCBB0+0x50);
                Put("consumer/pre/buildingRecords",0x64CCBB0+0x32c);
                Put("consumer/pre/nativeAlliances9",0x37EDF3C);
                Put("consumer/pre/macroLimit",Root);Put("consumer/pre/macroRecords",Root+0x2228);
                Put("consumer/pre/packedValidity",0x3A11EA4);Put("consumer/pre/combatClassMask",0x8574BCC);
                Put("consumer/pre/buildingTransitionClasses",0x2E6710);Put("consumer/pre/buildingSeedClasses",0x2E6C50);
                Put("consumer/pre/costBranch",0x8574B90);
                Put("directionOffsets",TileRoot);Put("rowRecords",0x402FF2C);Put("tileRows",0x3AAE2A4);
                Put("coarseRecords",0x35057AF);Put("caller-post/candidateCount",0x405B4F0);Put("caller-post/candidateCapacity8",0x405B4F4);
                Put(continuous?"consumer/pre/seeds":"consumer/build-pre/seeds",Seeds);Put(continuous?"consumer/pre/selectionMask":"consumer/build-pre/selectionMask",0x53AD4B0);
                // Continue from our calculated caller output, not a recorded Post substitute.
                m.Put(Distances+b.Bank*N*2,variantState.Distance);
                if(continuous)m.Put(Seeds,variantState.Seeds.Select(v=>unchecked((byte)v)).ToArray());
                if(variant!=0)
                {
                    m.Put(Flags,physical.Flags);m.Put(Pcl,Bytes(physical.Components));m.Put(Edges,physical.Edges);
                    m.Put(Root+0x2228,RemapConnections(Get("consumer/pre/macroRecords"),a,physical.Components));
                }
                Put("consumer/pre/workControls",Root);Put("consumer/pre/workQueueControls",Root+0x155f38);
                Put("consumer/pre/workQueue",Root+0x155f6c);Put("consumer/pre/workQueueRows",Root+0x28f3ec);
                Put("consumer/pre/workQueueX",Root+0x32be2c);Put("consumer/pre/workDistances",0x5225B10);
                Put("consumer/pre/workVisits",Visits);Put("consumer/pre/workTargets",0x37F1EDC);
                // The captured frame establishes exact target/attacker ordering.
                var identity=Get("consumer/build-pre/identity");
                if(identity.Length!=80||BitConverter.ToInt64(identity,32)!=b.Attacker||BitConverter.ToInt64(identity,40)!=b.Target)
                    throw new Exception("historical candidate argument identity");
                if(continuous)
                {
                    m.Put(0x53AD4B0,new byte[N]);
                    m.Put(Root+0xe0,variant==0?Get("componentCounts1000"):Bytes(counts));
                    m.Function<V3>(0x2C5A0)(m.Ptr(0x366C210),b.Attacker,b.Target);
                    // Existing Fixes owner replaces this one uint field with a full
                    // histogram. Recompute it; never use a captured effective Post value.
                    int region=m.Int(0x2E97D10),effective=0;
                    foreach(short component in m.Shorts(Pcl,N))if((ushort)component==region)effective++;
                    m.Int(0x2E9CA14,effective);
                    m.Function<V2>(0x1126B0)(m.Ptr(VirtualCandidateConsumers.Root),b.Attacker);
                    if(variant==0)Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-pre/candidates"),"continuous selection/unit-target handoff");
                }
                var managedCandidates=new VirtualCandidateBuilder();
                foreach(var range in new[]{
                    Tuple.Create(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),
                    Tuple.Create(0x67E8400,Get("consumer/pre/unitManager").Length),
                    Tuple.Create(0x4C559B0,N*2),Tuple.Create(Flags,N*4),Tuple.Create(Pcl,N*2),
                    Tuple.Create(0x4DDD350,N),Tuple.Create(0x4E2B870,N),Tuple.Create(0x4E79D90,N),Tuple.Create(0x4B6AA50,N*2),
                    Tuple.Create(0x64CCBB0+0x32c,Get("consumer/pre/buildingRecords").Length),
                    Tuple.Create(0x37EDF3C,36),Tuple.Create(Root,0xe0),Tuple.Create(Root+0x2228,Get("consumer/pre/macroRecords").Length),
                    Tuple.Create(0x3A11EA4,640000),Tuple.Create(0x8574BCC,Get("consumer/pre/combatClassMask").Length),
                    Tuple.Create(0x2E6710,Get("consumer/pre/buildingTransitionClasses").Length),Tuple.Create(0x2E6C50,Get("consumer/pre/buildingSeedClasses").Length),Tuple.Create(0x8574B90,1),
                    Tuple.Create(TileRoot,800*8*4),Tuple.Create(0x402FF2C,800*12),Tuple.Create(0x3AAE2A4,N*2),
                    Tuple.Create(Seeds,N),Tuple.Create(0x53AD4B0,N),Tuple.Create(Distances+b.Bank*N*2,N*2),
                    Tuple.Create(Edges,N),Tuple.Create(0x379AE00,9*0x583c),Tuple.Create(0x37F1EDC,9*50*16),
                    Tuple.Create(Root+0x155f38,0x34),Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2),Tuple.Create(Root+0x32be2c,N*2),
                    Tuple.Create(0x5225B10,N*2),Tuple.Create(Visits,N*2)})managedCandidates.Put(range.Item1,m.Bytes(range.Item1,range.Item2));
                foreach(var range in new[]{Tuple.Create(0x2D2E50,64),Tuple.Create(0x312620,8),Tuple.Create(0x2E68D0,336*4)})managedCandidates.Put(range.Item1,m.Bytes(range.Item1,range.Item2));
                if(continuous)
                {
                    managedCandidates.Replace(VirtualCandidateConsumers.Root,Get("consumer/pre/candidates"));
                    managedCandidates.SelectAndUnitTargets(b.Attacker,b.Target,counts);
                    Equal(managedCandidates.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),"complete calculated managed projection/Fixes histogram/unit handoff");
                }
                managedCandidates.Run(b.Attacker,b.Target);
                m.Function<V3>(0x10DF60)(m.Ptr(VirtualCandidateConsumers.Root),b.Attacker,b.Target);
                var managedManager=managedCandidates.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes);
                var nativeManager=m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes);
                int diagnostic=0;for(int di=0;di<managedManager.Length;di++)if(managedManager[di]!=nativeManager[di]&&diagnostic++<24)
                    Console.WriteLine("candidate-difference:"+(VirtualCandidateConsumers.Root+di).ToString("X")+",managed="+managedManager[di]+",native="+nativeManager[di]);
                foreach(var range in new[]{Tuple.Create(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Tuple.Create(Seeds,N),Tuple.Create(0x53AD4B0,N),
                    Tuple.Create(0x64CCBB0+0x32c,Get("consumer/pre/buildingRecords").Length),Tuple.Create(Root,0xe0),Tuple.Create(Root+0x155f38,0x34),
                    Tuple.Create(Root+0x155f6c,N*4),Tuple.Create(Root+0x28f3ec,N*2),Tuple.Create(Root+0x32be2c,N*2),Tuple.Create(0x5225B10,N*2),Tuple.Create(Visits,N*2)})
                    Equal(managedCandidates.Bytes(range.Item1,range.Item2),m.Bytes(range.Item1,range.Item2),"entire managed candidate/flood output:"+range.Item1.ToString("X"));
                if(variant==0)
                {
                    Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-post/candidates"),"entire historical native candidate manager");
                    Equal(m.Bytes(Seeds,N),Get("consumer/build-post/seeds"),"native candidate seeds");
                    Equal(m.Bytes(0x53AD4B0,N),Get("consumer/build-post/selectionMask"),"native candidate selection mask");
                }
                else Console.WriteLine("variant="+variant+",nativeCandidateChangedBytes="+Changed(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/build-post/candidates")));
                if(continuous)
                {
                    var modes=Get("consumer/weight-pre/gameModeValues");m.Int(0x3665F10,BitConverter.ToInt32(modes,0));m.Int(0x3665F28,BitConverter.ToInt32(modes,4));
                    int maximum=BitConverter.ToInt32(modes,4)==0&&BitConverter.ToInt32(modes,0)==28?150:70;
                    var predictedWeights=VirtualCandidateWeights.Run(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),b.Attacker,b.Target,m.Bytes(0x379AE00,9*0x583c),m.Bytes(Distances+b.Bank*N*2,N*2),b.Bank,m.Bytes(Flags,N*4),Get("consumer/pre/unitTiles"),Get("consumer/pre/unitManager"),Get("tileRows"),Get("directionOffsets"),maximum);
                    m.Function<V3>(0x115B10)(m.Ptr(VirtualCandidateConsumers.Root),b.Target,b.Attacker);
                    Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),predictedWeights,"entire productive weight buffer on computed variant");
                    if(variant==0)Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/weight-post/candidates"),"continuous native weights");
                    else Console.WriteLine("variant="+variant+",nativeWeightChangedBytes="+Changed(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/weight-post/candidates")));
                    var predictedAvailability=VirtualCandidateConsumers.Availability(predictedWeights,b.Attacker,1);
                    foreach(int function in new[]{0x112370,0x1123E0,0x112200,0x112450,0x112190})m.Function<V3>(function)(m.Ptr(VirtualCandidateConsumers.Root),1,b.Attacker);
                    Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),predictedAvailability,"entire productive availability buffer on computed variant");
                    if(variant==0)Equal(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/post/candidates"),"continuous availability consumers");
                    else Console.WriteLine("variant="+variant+",nativeConsumerChangedBytes="+Changed(m.Bytes(VirtualCandidateConsumers.Root,VirtualCandidateConsumers.Bytes),Get("consumer/post/candidates")));
                    // 61E70's mutable work-marker arrays were not captured. Protect
                    // them so any actually required read aborts this private process.
                    m.DenyMissingData(TileRoot+0x1f3ee30,64000*0x10);
                    m.DenyMissingData(TileRoot+0x1ea23f0,64000*2);
                    m.DenyMissingData(TileRoot+0x2038e34,4);
                    managedCandidates.PostConsumer(b.Target);
                    m.Function<V2>(0xCF020)(m.Ptr(0x366C210),b.Target);
                    foreach(var pair in new[]{Tuple.Create("workControls",Root,0xe0),Tuple.Create("workQueueControls",Root+0x155f38,0x34),Tuple.Create("workQueue",Root+0x155f6c,N*4),Tuple.Create("workQueueRows",Root+0x28f3ec,N*2),Tuple.Create("workQueueX",Root+0x32be2c,N*2),Tuple.Create("workDistances",0x5225B10,N*2),Tuple.Create("workVisits",Visits,N*2),Tuple.Create("workTargets",0x37F1EDC,9*50*16)})
                    {
                        Equal(managedCandidates.Bytes(pair.Item2,pair.Item3),m.Bytes(pair.Item2,pair.Item3),"complete managed post-consumer "+pair.Item1);
                        if(variant==0)Equal(m.Bytes(pair.Item2,pair.Item3),Get("consumer/post/"+pair.Item1),"continuous final "+pair.Item1);
                        else Console.WriteLine("variant="+variant+",field="+pair.Item1+",changedBytes="+Changed(m.Bytes(pair.Item2,pair.Item3),Get("consumer/post/"+pair.Item1)));
                    }
                    int predictedCastle=managedCandidates.Castle(b.Attacker,b.Target);
                    int castleResult=m.Function<R3>(0xCF360)(m.Ptr(0x366C210),b.Attacker,b.Target);
                    Check(predictedCastle==castleResult,"managed post-consumer castle decision");
                    Equal(managedCandidates.Bytes(Root,0xe0),m.Bytes(Root,0xe0),"complete managed castle-query controls");
                    if(variant==0)Check(castleResult!=0,"recorded positive post-consumer castle decision");
                    Console.WriteLine("variant="+variant+",nativeCastleResult="+castleResult+",projectedTarget="+m.Int(0x379D968+b.Attacker*0x583c)+",militaryBranch="+(castleResult!=0?"4-to-6-return":"4-to-5-follow-up-required")+",fullMilitaryExecuted=False,policy=Unknown");
                    if(variant==0)Equal(m.Bytes(0x379AE00,9*0x583c),Get("consumer/post/players"),"native consumer entire player output");
                    int playerOffset=b.Attacker*0x583c;
                    if(castleResult!=0)
                    {
                        m.Int(0x379D974+playerOffset,6);
                        if(variant==0)Equal(m.Bytes(0x379AE00+playerOffset,0x583c),Get("military/exitPlayer"),"whole recorded post-consumer military player");
                    }
                    else
                    {
                        m.Int(0x379D974+playerOffset,5);
                        // All three recorded tribe slots are zero. Do not silently
                        // fall back to uncaptured search/selector state for other cases.
                        foreach(int slot in new[]{0x379F688,0x379F686,0x379F68A})if(m.Short(slot+playerOffset)!=0)throw new Exception("Unknown:military-follow-up-needs-active-tribe-input-closure");
                        foreach(int wrapper in new[]{0x3C150,0x3B8D0,0x2B2C0})m.Function<V2>(wrapper)(m.Ptr(0x366C210),b.Attacker);
                        // With a fresh rebuilt virtual grid the native uint counters
                        // equal the full Fixes histogram. Original stale counts do not.
                        m.Put(Root+0xe0,counts);
                        m.Function<V3>(0x2D250)(m.Ptr(0x366C210),b.Attacker,0);
                        m.Function<V2>(0x2C480)(m.Ptr(0x366C210),b.Attacker);
                        m.Short(0x379E78C+playerOffset,0);
                    }
                    Console.WriteLine("variant="+variant+",postConsumerMilitaryPhase="+m.Int(0x379D974+playerOffset)+",savedTarget="+m.Int(0x379D968+playerOffset)+",playerChangedBytes="+Changed(m.Bytes(0x379AE00+playerOffset,0x583c),Get("military/exitPlayer"))+",scope=post-consumer-suffix-excluding-map-observer2990-and-entry-preamble,policy=Unknown");
                    Console.WriteLine(variant==0?"PASS continuous actual2C480 calls, complete candidate/weight/availability/work outputs; Fixes count computed independently; full military entry remains separate":"PASS native counterfactual consumer execution; changed outputs measured, not matched to recorded counterfactuals; full military entry remains separate");
                }
            }
            Console.WriteLine("PASS historical native candidate reference; variant="+variant+",recordedOutputComparison="+(variant==0)+",hash="+a.Hash+",managedCandidateBuilder=Matched,managedWorkAndCastle=Matched,militaryConsumer=post-consumer-native-suffix-only,behaviorFix=disabled");return 0;
        }
        private static int HistoricalPhysical(string path)
        {
            BridgePlanningImporter.Artifact a;string reason;
            if(!BridgePlanningImporter.TryRead(path,out a,out reason))throw new Exception(reason);
            if(!ApprovedPlanning(a.Hash)||a.Planning==null)throw new Exception("unapproved historical physical input");
            var b=a.Planning;byte[] Get(string name)=>CopiedPlanningBundle.Resolve(b,name);
            // Heights were recorded at consumer entry, not at the earlier caller entry.
            // Keep the original timing explicit; do not borrow later fields for that caller.
            using(var m=new NativeImage(Native,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","native-contracts.tsv")))
            {
                m.Put(Flags,Get("consumer/pre/flags"));m.Put(Pcl,Get("consumer/pre/components"));m.Put(Edges,Get("consumer/pre/edges"));
                m.Put(0x4DDD350,Get("consumer/pre/height"));m.Put(0x4E2B870,Get("consumer/pre/baseHeight"));
                m.Put(0x4B6AA50,Get("consumer/pre/buildingIds"));m.Put(0x402FF2C,Get("rowRecords"));m.Put(TileRoot,Get("directionOffsets"));
                m.Put(0x64CCBB0+0x32c,Get("buildingUpdaterSlots"));m.Put(0x64CCBB0+0x319874,Get("buildingUpdaterControls"));
                m.Put(0x64CCBB0+0x50,Get("consumer/pre/buildingCount"));m.Put(0x64CCBB0+0x32c,Get("consumer/pre/buildingRecords"));
                m.Put(Root,Get("consumer/pre/workControls"));m.Put(Root+0x2228,Get("consumer/pre/macroRecords"));
                m.Put(0x4ACE010,a.Sections["specialIds"]);
                var kinds=a.Sections["specialKinds"];for(int i=0;i<kinds.Length/2;i++)m.Short(0x32DE440+i*0x9c+0x6a,BitConverter.ToInt16(kinds,i*2));
                // Slot/update controls are captured at caller entry. Require exact relevant
                // building consistency before using that complete retained 3999-slot view.
                var early=Get("buildingRecords");var current=Get("consumer/pre/buildingRecords");
                Equal(early,current,"same building inputs across caller and consumer");
                if(BitConverter.ToInt32(Get("buildingUpdaterControls"),0)!=0)
                    throw new Exception("Unknown:updater-controls-not-recorded-at-consumer-entry");
                Check(m.Function<R2>(0xE49D0)(m.Ptr(Root),1)==1,"full historical native physical rebuild");
                // Gate closure/restoration and all updater dispatch remain real native calls.
                var recorded=new ushort[N];Buffer.BlockCopy(Get("consumer/pre/components"),0,recorded,0,N*2);
                var actual=m.Shorts(Pcl,N);var forward=new System.Collections.Generic.Dictionary<int,int>();var reverse=new System.Collections.Generic.Dictionary<int,int>();
                for(int i=0;i<N;i++)
                {
                    int before=recorded[i],after=(ushort)actual[i];if((before==0)!=(after==0))throw new Exception("physical occupancy differs at "+i);
                    if(before==0)continue;
                    if(forward.TryGetValue(before,out int f)&&f!=after||reverse.TryGetValue(after,out int r)&&r!=before)throw new Exception("physical partition differs at "+i);
                    forward[before]=after;reverse[after]=before;
                }
                var histogram=new int[1000];foreach(short value in actual){int region=(ushort)value;if(region>=histogram.Length)throw new Exception("native component bound");if(region!=0)histogram[region]++;}
                Equal(m.Ints(Root+0xe0,1000),histogram,"native field counts and rebuilt grid histogram");
                Equal(m.Bytes(Edges,N),Get("consumer/pre/edges"),"native physical restored directions");
                Console.WriteLine("PASS full native physical rebuild including C5040/C4BF0/updaters; inputTiming=consumer-entry;partitionEquivalent=True;updaterDirtyAtCaller=0;nativeGeneration="+m.Int(Root+0x74)+",earlierCallerHeightProof=Unknown;negativeEligible=False");
            }
            return 0;
        }
    }
}
