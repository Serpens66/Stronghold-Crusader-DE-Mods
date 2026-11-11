using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AreaDemolitionTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System.Linq;
using System.Diagnostics;

internal static unsafe class Program
{
    private static int checks;
    private static void Check(bool value,string message) { if (!value) throw new Exception(message); checks++; }
    private static int Main()
    {
        try
        {
            ScratchRestoration(); PatternBoundaries(); ClassificationAndRefunds(); RegisteredFootprints(); DecorationContracts(); PreviewIndexAndBudget(); PreviewGeometryBenchmark();
            Console.WriteLine("PASS: native demolition " + checks + " checks (private-memory scratch, native layout, resolver boundaries, registered footprints, decoration ownership/identity/closure, fear families, refund policy).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void ScratchRestoration()
    {
        IntPtr allocation=Marshal.AllocHGlobal(NativeBindings.ActorRva+4096);
        try
        {
            byte* p=(byte*)allocation;
            var saved=new Dictionary<int,byte>();
            void Seed(int offset,int count)
            { for (int i=0;i<count;i++) { byte value=(byte)((offset+i)*37+11); p[offset+i]=value; saved[offset+i]=value; } }
            Seed(NativeBindings.CursorRva,0x80); Seed(NativeBindings.NeighboursRva,32); Seed(NativeBindings.ActorRva,4);
            foreach (int offset in NativeBindings.QueryOffsets) Seed(NativeBindings.TileManagerRva+offset,4);
            foreach (TargetKind kind in new[]{TargetKind.Building,TargetKind.Pitch,TargetKind.Wall,TargetKind.DugMoat})
            {
                var target=new DemolitionTarget { Kind=kind,Slot=17,TileId=991,X=100,Y=77,Owner=4 };
                foreach (int mode in new[]{0,1,2})
                {
                    var binding=new NativeBindings(allocation,new object(),(manager,actor,x,y)=>
                    {
                        Check(manager.ToInt64()==allocation.ToInt64()+NativeBindings.TileManagerRva,"Native manager argument");
                        Check(actor==4 && x==100 && y==77,"Native actor/coordinate arguments");
                        byte* cursor=p+NativeBindings.CursorRva, tiles=(byte*)manager;
                        Check(*(int*)(cursor+0x14)==991 && *(int*)(cursor+0x68)==991,"Packed target setup");
                        Check(*(int*)(p+NativeBindings.ActorRva)==4 && *(int*)(tiles+0x204E694)==0,"Scoped actor and ground targeting");
                        Check(*(int*)(cursor+0x24)==(kind==TargetKind.Building?17:0),"Building selector");
                        Check(*(int*)(cursor+0x4C)==(kind==TargetKind.Pitch?17:0),"Pitch selector");
                        Check(*(int*)(cursor+0x5C)==(kind==TargetKind.Wall?991:0),"Wall selector");
                        Check(*(uint*)(cursor+0x44)==(kind==TargetKind.DugMoat?0x40000000U:0),"Moat selector");
                        for (int i=0;i<0x80;i++) cursor[i]=0xAA;
                        for (int i=0;i<32;i++) p[NativeBindings.NeighboursRva+i]=0xBB;
                        foreach (int offset in NativeBindings.QueryOffsets) *(int*)(tiles+offset)=0x1234;
                        *(int*)(p+NativeBindings.ActorRva)=6;
                        if (mode==2) throw new InvalidOperationException("injected native-query failure");
                        *(int*)(tiles+0x204E6FC)=mode;
                        *(int*)(tiles+0x204E720)=kind==TargetKind.Pitch?-17:17;
                        *(int*)(tiles+0x204E76C)=kind==TargetKind.Wall?1:0;
                        *(int*)(tiles+0x204E770)=kind==TargetKind.DugMoat?1:0;
                    });
                    bool threw=false;
                    try { Check(binding.Allows(target,4)==(mode==0),"Native eligibility result"); }
                    catch (InvalidOperationException) { threw=true; }
                    Check(threw==(mode==2),"Only injected exception propagates");
                    foreach (var pair in saved) Check(p[pair.Key]==pair.Value,"Query leaked scratch at "+pair.Key.ToString("X"));
                }
            }
            Check(Marshal.OffsetOf<SHCDESE.Interop.MoatWorkTask>(nameof(SHCDESE.Interop.MoatWorkTask.r_Progress)).ToInt32()==0xD,"Chore41 zeroes progress, not work type");
            var task=(MoatWorkTask*)(p+NativeBindings.TileManagerRva+0x1F3EE30)+5;
            task->r_Progress=4; task->r_WorkType=SHCDESE.Interop.Enums.MoatWorkType.FillDugMoat;
            ushort* display=(ushort*)(p+NativeBindings.TileManagerRva+0xF05F40);
            display[991]=0xFFFF;
            int calls=0;
            var moatBinding=new NativeBindings(allocation,new object(),null,(manager,index)=>
            {
                calls++; Check(index==5,"Moat helper receives the task index, not packed tile");
                Check(task->r_Progress==0,"A complete moat must be zeroed before the single fill helper call");
                Check(task->r_WorkType==SHCDESE.Interop.Enums.MoatWorkType.FillDugMoat,"Work type is preserved");
                return 1;
            });
            moatBinding.RemoveDugMoat(5,991);
            Check(calls==1,"One native fill call for a completed moat");
            Check(display[991]==0xFBFF,"Only the pending-demolition display bit is cleared");
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }

    private static void PatternBoundaries()
    {
        var image=new byte[0x1000];
        void I32(int offset,int value) => BitConverter.GetBytes(value).CopyTo(image,offset);
        void I16(int offset,ushort value) => BitConverter.GetBytes(value).CopyTo(image,offset);
        I32(0x3C,0x80); I16(0x86,2); I16(0x94,0xF0);
        int section=0x188;
        I32(section+8,0x300); I32(section+12,0x400); I32(section+36,0x20000000);
        I32(section+40+8,0x200); I32(section+40+12,0x800); I32(section+40+36,0x40000000);
        byte[] pattern={0x43,0x87,0x21,0x35};
        pattern.CopyTo(image,0x450); pattern.CopyTo(image,0x810);
        Check(NativeBindings.FindUniqueText(image,pattern)==0x450,"Data-section duplicate must not make a code target ambiguous");
        pattern.CopyTo(image,0x500);
        Check(NativeBindings.FindUniqueText(image,pattern)==-1,"Two executable matches are rejected");
        I32(section+8,int.MaxValue);
        Check(NativeBindings.FindUniqueText(image,pattern)==-1,"Out-of-image section rejected");
    }

    private static void RegisteredFootprints()
    {
        const int capacity=10000;
        var structures=new ushort[capacity];var alpha=new int[capacity];var flags=new uint[capacity];
        int TileAt(int x,int y) => x>=0 && y>=0 && x<100 && y<100 ? y*100+x : -1;
        GameBuilding building=default;building.r_BuildingType=eStructs.STRUCT_QUARRY;building.r_TilePositionXBegin=10;building.r_TilePositionYBegin=10;building.r_OccupyTileGridSize=6;
        void Register(int slot,int x,int y,int size) { for(int dy=0;dy<size;dy++) for(int dx=0;dx<size;dx++) structures[TileAt(x+dx,y+dy)]=(ushort)slot; }
        fixed(ushort* st=structures) fixed(int* al=alpha) fixed(uint* fl=flags)
        {
            Register(1,10,10,6);
            Check(NativeDemolition.CollectRegisteredTiles(1,&building,st,al,fl,capacity,TileAt).Length==36,"Empty optional array must not hide a quarry");
            building.r_OccupyTileGridSize=13;Register(1,10,10,13);
            Check(NativeDemolition.CollectRegisteredTiles(1,&building,st,al,fl,capacity,TileAt).Length==169,"Large13x13 native core must be fully included");
            building.r_BuildingType=eStructs.STRUCT_PARADEGROUND_MISS;building.r_OccupyTileGridSize=5;
            Check(NativeDemolition.CollectRegisteredTiles(1,&building,st,al,fl,capacity,TileAt).Length==25,"Reserved child geometry is independent of its empty optional array");
            building.r_BuildingType=eStructs.STRUCT_WHEATFARM;building.r_OccupyTileGridSize=3;
            uint* extra=&building.r_OccupiedTileIdsArrayBegin;extra[35]=(uint)TileAt(30,30);structures[TileAt(30,30)]=1;
            extra[34]=(uint)TileAt(31,30);structures[TileAt(31,30)]=2;extra[33]=uint.MaxValue;
            var farm=NativeDemolition.CollectRegisteredTiles(1,&building,st,al,fl,capacity,TileAt);
            Check(farm.Length==10 && farm.Contains(TileAt(30,30)),"All36 hints checked, including field cells beyond core size squared");
            Check(!farm.Contains(TileAt(31,30)),"Foreign registration and out-of-range hints are excluded");
            structures[TileAt(11,11)]=0;
            Check(!NativeDemolition.CollectRegisteredTiles(1,&building,st,al,fl,capacity,TileAt).Contains(TileAt(11,11)),"An unregistered hole is not a building tile");
            Array.Clear(structures);Array.Clear(alpha);Array.Clear(flags);building=default;building.r_BuildingType=eStructs.STRUCT_GOODS_YARD;building.r_OccupyTileGridSize=2;
            var all=new HashSet<int>();int id=0;
            foreach(var origin in new[]{(20,20),(23,20),(20,23),(23,23)})
            {
                id++;Register(id,origin.Item1,origin.Item2,2);
            }
            for(int y=20;y<25;y++) for(int x=20;x<25;x++) if(x==22 || y==22)
            { int tile=TileAt(x,y);alpha[tile]=4;flags[tile]=(uint)TilePropertyFlag.GoodsyardConnection; }
            id=0;
            foreach(var origin in new[]{(20,20),(23,20),(20,23),(23,23)})
            {
                id++;building.r_TilePositionXBegin=(ushort)origin.Item1;building.r_TilePositionYBegin=(ushort)origin.Item2;
                all.UnionWith(NativeDemolition.CollectRegisteredTiles(id,&building,st,al,fl,capacity,TileAt));
            }
            Check(all.Count==25 && all.Contains(TileAt(22,22)),"Four stockpile sectors plus nine alpha-linked cross cells form a5x5 footprint");
            flags[TileAt(22,22)]=0;building.r_TilePositionXBegin=23;building.r_TilePositionYBegin=23;
            Check(!NativeDemolition.CollectRegisteredTiles(4,&building,st,al,fl,capacity,TileAt).Contains(TileAt(22,22)),"Alpha renderer indices alone are not stockpile registration");
            building.r_OccupyTileGridSize=14;
            Check(NativeDemolition.CollectRegisteredTiles(4,&building,st,al,fl,capacity,TileAt).Length==0,"Unknown larger native shape is failclosed");
        }
    }

    private static void DecorationContracts()
    {
        foreach(ProjectileType type in Enum.GetValues<ProjectileType>())
            Check(NativeDemolition.IsDecorationType(type)==((int)type>=10 && (int)type<=15),"Only six proven static projectile types: "+type);
        GameProjectile projectile=default;projectile.r_PlayerSourceId=0xABCD0003;
        Check(NativeDemolition.DecorationOwner(&projectile)==3,"Native decoration owner is the low16, not source unit/colour or fullDWORD");
        var flag=new DemolitionTarget { Kind=TargetKind.Decoration,Group=TargetGroup.Decorations,Slot=25,GlobalId=0,Owner=3,Type=(int)ProjectileType.Flag1,TileId=44,X=4,Y=4 };
        var brazier=flag.Copy();brazier.Slot=26;brazier.Type=(int)ProjectileType.Brazier;
        Check(NativeDemolition.OwnDecorationSet(new[]{flag,brazier},3),"Generation zero remains an exact identity for normal static slots");
        brazier.Owner=4;
        Check(!NativeDemolition.OwnDecorationSet(new[]{flag,brazier},3),"One foreign same-tile decoration rejects entire collateral set");
        brazier.Owner=3;brazier.Type=(int)ProjectileType.ArcherArrow;
        Check(!NativeDemolition.OwnDecorationSet(new[]{flag,brazier},3),"Other projectiles cannot enter the static closure");
        var reused=flag.Copy();reused.GlobalId=1;
        Check(!NativeDemolition.SameState(flag,reused),"Same slot/type/location with a new generation is changed, not approved");
        var parent=flag.Copy();parent.Kind=TargetKind.Building;parent.Slot=1;parent.GlobalId=4;
        var frozen=new[]{parent,flag};
        Check(!NativeDemolition.IdentitiesCurrent(frozen,t=>t.Kind==TargetKind.Building?null:t.Copy()),"Missing parent rejects frozen selection before MakeBatches can orphan its surviving decoration");
        Check(!NativeDemolition.IdentitiesCurrent(frozen,t=>{var copy=t.Copy();if(copy.Kind==TargetKind.Building)copy.Owner=4;return copy;}),"Owner change rejects complete frozen identity preflight");
        Check(NativeDemolition.IdentitiesCurrent(frozen,t=>t.Copy()),"Unchanged complete frozen selection remains usable");
        var expected=frozen.ToDictionary(t=>t.Identity,StringComparer.Ordinal);
        var live=frozen.ToDictionary(t=>t.Identity,t=>t.Copy(),StringComparer.Ordinal);
        var newChild=flag.Copy();newChild.Slot=27;
        Check(!NativeDemolition.ClosureMatchesApproval(new[]{parent,flag,newChild},expected,live),"Unknown new same-tile dependent rejects packet before mutation");
        live.Remove(parent.Identity);
        Check(!NativeDemolition.ClosureMatchesApproval(frozen,expected,live),"Missing root cannot pass commit closure preflight while its decoration survives");
        live[parent.Identity]=parent.Copy();
        Check(NativeDemolition.ClosureMatchesApproval(frozen,expected,live),"Complete unchanged closure passes commit preflight");
        var changedCompound=parent.Copy();changedCompound.Flags=7;
        Check(!NativeDemolition.SameState(parent,changedCompound),"Changed compound key invalidates frozen regrouping");
        var changedShape=parent.Copy();changedShape.TaskIndex=13<<16;
        Check(!NativeDemolition.SameState(parent,changedShape),"Changed size/parent stamp invalidates frozen grouping identity");
        var first=parent.Copy();var next=parent.Copy();next.Slot=2;next.GlobalId=5;
        var callbackLive=new Dictionary<string,DemolitionTarget>(StringComparer.Ordinal) { [first.Identity]=first.Copy(),[next.Identity]=next.Copy() };
        var executed=new HashSet<string>(StringComparer.Ordinal);
        DemolitionTarget Live(DemolitionTarget t) => callbackLive.TryGetValue(t.Identity,out var value)?value:null;
        Check(NativeDemolition.PendingIdentitiesCurrent(new[]{first,next},executed,Live),"Initial complete preflight accepts both operations");
        executed.Add(first.Identity);callbackLive.Remove(first.Identity); // First native operation completed.
        callbackLive[next.Identity].Owner=4; // Synchronous SE subscriber changes the next target.
        Check(!NativeDemolition.PendingIdentitiesCurrent(new[]{first,next},executed,Live),"Reentrant owner change after preflight prevents the next mutation");
        callbackLive[next.Identity]=next.Copy();
        Check(NativeDemolition.PendingIdentitiesCurrent(new[]{first,next},executed,Live),"Own completed child need not remain alive for later shared closure");
        var receipt=new[]{new ApplyResult(),new ApplyResult()};
        NativeDemolition.CompleteResults(new[]{first,next},receipt,executed,new HashSet<string>(StringComparer.Ordinal){first.Identity},true);
        Check(receipt[0].Status==ApplyStatus.Applied && receipt[1].Status==ApplyStatus.Forbidden,"Aborting remaining packet preserves the earlier completed result");
        int calls=0;
        var binding=new NativeBindings(new IntPtr(0x10000000),new object(),null,null,(manager,tile)=>
        { calls++;Check(manager.ToInt64()==0x10000000L+NativeBindings.ProjectileManagerRva,"Static helper uses projectile manager");Check(tile==44,"Static helper gets tileID, not projectile slot"); });
        binding.RemoveDecorationTile(44);Check(calls==1,"Exactly one same-tile mutation");
    }

    private static void PreviewGeometryBenchmark()
    {
        foreach(int count in new[]{120,600,1200})
        {
            const int capacity=65536;
            var records=new GameBuilding[count];var structures=new ushort[capacity];var alpha=new int[capacity];var flags=new uint[capacity];
            for(int i=0;i<count;i++)
            {
                records[i].r_BuildingType=eStructs.STRUCT_HOVEL;records[i].r_OccupyTileGridSize=3;
                int x=1+(i%50)*4,y=1+(i/50)*4;records[i].r_TilePositionXBegin=(ushort)x;records[i].r_TilePositionYBegin=(ushort)y;
                for(int dy=0;dy<3;dy++) for(int dx=0;dx<3;dx++) structures[(y+dy)*256+x+dx]=(ushort)(i+1);
            }
            long probes=0;
            int Tile(int x,int y){probes++;return x>=0 && y>=0 && x<256 && y<256?y*256+x:-1;}
            fixed(GameBuilding* buildings=records) fixed(ushort* st=structures) fixed(int* al=alpha) fixed(uint* fl=flags)
            {
                NativeDemolition.CollectRegisteredTiles(1,buildings,st,al,fl,capacity,Tile);
                probes=0;var watch=Stopwatch.StartNew();long beforeSum=0;
                for(int pass=0;pass<2;pass++) for(int i=0;i<count;i++) beforeSum+=NativeDemolition.CollectRegisteredTiles(i+1,buildings+i,st,al,fl,capacity,Tile).Length;
                watch.Stop();long beforeProbes=probes;double beforeMs=watch.Elapsed.TotalMilliseconds;
                probes=0;watch.Restart();var snapshot=new int[count][];
                for(int i=0;i<count;i++) snapshot[i]=NativeDemolition.CollectRegisteredTiles(i+1,buildings+i,st,al,fl,capacity,Tile);
                long afterSum=0;for(int pass=0;pass<2;pass++) foreach(var cells in snapshot) afterSum+=cells.Length;
                watch.Stop();
                Check(beforeSum==afterSum && afterSum==count*18L,"Shared production geometry snapshot preserves all120+ registered results");
                Check(beforeProbes==probes*2,"Projection and quote reuse halves production geometry tile probes");
                Console.WriteLine("BENCH preview-geometry records="+count+" duplicatePassMs="+beforeMs.ToString("F3")+" sharedSnapshotMs="+watch.Elapsed.TotalMilliseconds.ToString("F3")+" duplicateTileProbes="+beforeProbes+" sharedTileProbes="+probes);
            }
        }
    }

    private static void PreviewIndexAndBudget()
    {
        foreach(int count in new[]{120,600,1200})
        {
            var members=new List<NativeDemolition.BuildingMember>();
            for(int slot=1;slot<=count;slot++)
            {
                int group=(slot-1)/6,position=(slot-1)%6;
                eStructs type=position<4?eStructs.STRUCT_GOODS_YARD:position==4?eStructs.STRUCT_QUARRY:eStructs.STRUCT_QUARRYPILE;
                uint key=(uint)(group*2+(position<4?1:2));
                var target=new DemolitionTarget { Kind=TargetKind.Building,Group=NativeDemolition.Classify(type),Slot=slot,Owner=3,Type=(int)type,GlobalId=(uint)(slot+200),Flags=key,TaskIndex=2<<16,X=slot%100,Y=slot/100,TileId=slot };
                members.Add(new NativeDemolition.BuildingMember(slot,3,AliveState.IsAlive,key,target));
            }
            var index=new NativeDemolition.PreviewIndex(members);
            long referenceReads=0;
            DemolitionTarget ReferenceRoot(DemolitionTarget target)
            {
                if ((eStructs)target.Type!=eStructs.STRUCT_GOODS_YARD && (eStructs)target.Type!=eStructs.STRUCT_QUARRYPILE) return target;
                eStructs wanted=(eStructs)target.Type==eStructs.STRUCT_GOODS_YARD?eStructs.STRUCT_GOODS_YARD:eStructs.STRUCT_QUARRY;
                foreach(var item in members)
                { referenceReads++;if(item.Compound==target.Flags && item.Owner==target.Owner && item.State==AliveState.IsAlive && item.Target.Type==(int)wanted)return item.Target; }
                return target;
            }
            foreach(var member in members)
            {
                Check(index.Canonical(member.Target).Identity==ReferenceRoot(member.Target).Identity,"Indexed canonical root equals full-slot scan");
                Check(index.TryCompound(member.Slot,3,out var closure),"Own compound remains accepted");
                var reference=members.Where(m=>m.Compound==member.Compound && m.State!=AliveState.MarkedForDeletion).Select(m=>m.Slot).ToArray();
                Check(closure.SequenceEqual(reference),"Indexed compound closure equals complete old scan");
            }
            var watch=Stopwatch.StartNew();long beforeReads=0;
            for(int pass=0;pass<5;pass++) foreach(var member in members)
            {
                ReferenceRoot(member.Target);
                foreach(var other in members) { beforeReads++;if(other.Compound==member.Compound && other.Owner!=3)throw new Exception("Fixture ownership"); }
            }
            watch.Stop();double before=watch.Elapsed.TotalMilliseconds;
            watch.Restart();long indexedMembers=0;
            for(int pass=0;pass<5;pass++)
            {
                var production=new NativeDemolition.PreviewIndex(members);
                foreach(var member in members) { production.Canonical(member.Target);production.TryCompound(member.Slot,3,out var group);indexedMembers+=group.Length; }
            }
            watch.Stop();
            Check(indexedMembers<beforeReads,"Production index removes repeated full-slot membership walks");
            Console.WriteLine("BENCH preview-index records="+count+" legacyMs="+before.ToString("F3")+" indexedMs="+watch.Elapsed.TotalMilliseconds.ToString("F3")+" legacyMembershipReads="+beforeReads+" indexedMembers="+indexedMembers);
        }
        var parent=new DemolitionTarget { Kind=TargetKind.Building,Type=(int)eStructs.STRUCT_BARRACKS_STONE,Group=TargetGroup.Buildings,Slot=1,Owner=3,GlobalId=1,Flags=99,TaskIndex=5<<16 };
        var child=parent.Copy();child.Slot=2;child.GlobalId=2;child.Type=(int)eStructs.STRUCT_PARADEGROUND_LGT;child.TaskIndex=(5<<16)|1;
        var own=new NativeDemolition.PreviewIndex(new[]{new NativeDemolition.BuildingMember(1,3,AliveState.IsAlive,99,parent),new NativeDemolition.BuildingMember(2,3,AliveState.IsAlive,99,child)});
        Check(own.Canonical(child).Slot==1,"Reserved parent links use stamped parentID");
        var foreign=new NativeDemolition.PreviewIndex(new[]{new NativeDemolition.BuildingMember(1,3,AliveState.IsAlive,99,parent),new NativeDemolition.BuildingMember(2,4,AliveState.MarkedForDeletion,99,null)});
        Check(!foreign.TryCompound(1,3,out _),"Foreign compound members stay blocked even when already marked");
        var initializing=new NativeDemolition.PreviewIndex(new[]{new NativeDemolition.BuildingMember(1,3,AliveState.IsAlive,99,parent),new NativeDemolition.BuildingMember(2,3,AliveState.NeedsInit,99,null)});
        Check(!initializing.TryCompound(1,3,out _),"Unpreviewable initializing child rejects root");
        var roots=Enumerable.Range(1,257).Select(i=>new DemolitionTarget { Kind=TargetKind.Building,Slot=i,GlobalId=(uint)i,Owner=3,Type=(int)eStructs.STRUCT_HOVEL }).ToArray();
        var quote=new NativeDemolition.PreviewQuote { OrderedRoots=roots };
        int evaluated=0;
        IEnumerable<DemolitionTarget> Evaluate(DemolitionTarget target){evaluated++;return new[]{target};}
        Check(!NativeDemolition.StepPreviewRoots(quote,64,100,Evaluate,()=>0) && evaluated==64,"Actual root-step consumer caps native evaluations at64");
        var overBudget=new NativeDemolition.PreviewQuote { OrderedRoots=roots };
        int oversizedCalls=0;
        NativeDemolition.StepPreviewRoots(overBudget,int.MaxValue,100,t=>{oversizedCalls++;return new[]{t};},()=>0);
        Check(oversizedCalls==64,"Native preview boundary caps an oversized caller budget at64");
        Check(!NativeDemolition.StepPreviewRoots(quote,64,100,Evaluate,()=>100) && evaluated==64,"Expired deadline invokes no native evaluation");
        long clock=0;
        Check(!NativeDemolition.StepPreviewRoots(quote,64,3,Evaluate,()=>clock++) && evaluated==67,"Actual clock boundary stops a partially processed root batch");
        while(!NativeDemolition.StepPreviewRoots(quote,64,100,Evaluate,()=>0)) { }
        Check(evaluated==257 && DemolitionPolicy.TargetDigest(quote.Result)==DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(roots)),"Resumed257-root result equals unbudgeted complete result");
    }

    private static void ClassificationAndRefunds()
    {
        foreach (var type in new[]{eStructs.STRUCT_GARDEN,eStructs.STRUCT_GARDEN_SMALL,eStructs.STRUCT_GARDEN_MED,eStructs.STRUCT_GARDEN_LARGE,eStructs.STRUCT_POND,eStructs.STRUCT_POND_SMALL,eStructs.STRUCT_POND_LARGE,eStructs.STRUCT_POND5,eStructs.STRUCT_POND6,eStructs.STRUCT_POND7,eStructs.STRUCT_POND8})
            Check(NativeDemolition.Classify(type)==TargetGroup.PositiveFear,"Positive fear size/variant missing: "+type);
        Check(NativeDemolition.Classify(eStructs.STRUCT_RACK_FLOGGING)==TargetGroup.NegativeFear,"Flogging rack omitted by AIV subset must remain negative fear");
        Check(NativeDemolition.Classify(eStructs.STRUCT_HEAD_ON_SPIKE)==TargetGroup.NegativeFear,"Head on spike remains negative fear");
        Check(NativeDemolition.Classify(eStructs.STRUCT_GOODS_YARD)==TargetGroup.Stockpiles,"Stockpiles have their independent filter group");
        Check(NativeDemolition.Classify(eStructs.STRUCT_DOG_CAGE)==TargetGroup.DogCages,"Dog cage structural trap precedence");
        Check(NativeDemolition.Classify(eStructs.STRUCT_KILLING_PIT)==TargetGroup.KillingPits,"Killing pit has its independent filter");
        Check(NativeDemolition.WallRefundPercent(0,60)==25,"Vanilla exact boundary lower side");
        Check(NativeDemolition.WallRefundPercent(0,61)==50,"Vanilla exact boundary upper side");
        Check(NativeDemolition.WallRefundPercent(1,60)==50,"Fixes bypasses low-wall halving");
        GameBuilding building=default; building.r_AliveState=SHCDESE.Interop.Enums.AliveState.IsAlive;
        building.N00001786=0x10000;
        Check(NativeDemolition.ShouldRefund(&building),"Refund exclusion reads only the low word");
        building.N00001786=1;
        Check(!NativeDemolition.ShouldRefund(&building),"Low-word exclusion suppresses the parent refund");
        building.N00001786=0; building.r_AliveState=SHCDESE.Interop.Enums.AliveState.MarkedForDeletion;
        Check(!NativeDemolition.ShouldRefund(&building),"Already marked objects are not refunded twice");
    }
}
