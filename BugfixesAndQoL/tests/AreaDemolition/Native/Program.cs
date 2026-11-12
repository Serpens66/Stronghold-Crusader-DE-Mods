using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BugfixesAndQoL.AreaDemolition;
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
            ScratchRestoration(); PatternBoundaries(); ClassificationAndRefunds(); WallIdentityNormalization(); RegisteredFootprints(); DecorationContracts(); ComponentCommitContracts(); RequestHitContracts(); PreviewIndexAndBudget(); PreparationMonitorContracts(); PreviewGeometryBenchmark();
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
            var request=new NativeBindings.RequestState(100,77,991,17,992,993,101,78,(uint)TilePropertyFlag.IsMoat,19,true);
            var requestBinding=new NativeBindings(allocation,new object(),(manager,actor,x,y)=>
            {
                byte* cursor=p+NativeBindings.CursorRva,tiles=(byte*)manager;
                Check(*(int*)(cursor+0x14)==991 && *(int*)(cursor+0x68)==993,"Request query preserves distinct requested/effective tiles");
                Check(*(int*)(cursor+0x6C)==101 && *(int*)(cursor+0x70)==78,"Request query receives effective coordinates");
                Check(*(int*)(cursor+0x24)==17 && *(int*)(cursor+0x5C)==992 && *(int*)(cursor+0x4C)==19,"Request query retains each independent selector");
                Check(*(uint*)(cursor+0x44)==(uint)TilePropertyFlag.IsMoat && *(int*)(tiles+0x204E694)==1,"Request query retains native flattened/moat context");
                *(int*)(cursor+0x5C)=994;*(int*)(tiles+0x204E774)=995;
                *(int*)(tiles+0x204E720)=-19;*(int*)(tiles+0x204E76C)=1;*(int*)(tiles+0x204E770)=0;*(int*)(tiles+0x204E6FC)=0;
            });
            Check(requestBinding.QueryRequested(request,4,out int selected,out bool wall,out bool moat,out int wallTile,out int moatTile) && selected==-19 && wall && !moat && wallTile==994 && moatTile==995,"Request query copies native outputs before restoring scratch");
            foreach (var pair in saved) Check(p[pair.Key]==pair.Value,"Requested-point query leaked scratch at "+pair.Key.ToString("X"));
            foreach (bool injectFailure in new[]{false,true})
            {
                var rejected=new NativeBindings(allocation,new object(),(manager,actor,x,y)=>
                {
                    for(int i=0;i<0x80;i++) p[NativeBindings.CursorRva+i]=0xAA;
                    for(int i=0;i<32;i++) p[NativeBindings.NeighboursRva+i]=0xBB;
                    foreach(int offset in NativeBindings.QueryOffsets) *(int*)((byte*)manager+offset)=1;
                    *(int*)(p+NativeBindings.ActorRva)=7;
                    if(injectFailure) throw new InvalidOperationException("injected request-query failure");
                });
                bool threw=false;
                try { Check(!rejected.QueryRequested(request,4,out _,out _,out _,out _,out _),"Native requested-point rejection remains a rejection"); }
                catch(InvalidOperationException) { threw=true; }
                Check(threw==injectFailure,"Only injected requested-point exception propagates");
                foreach(var pair in saved) Check(p[pair.Key]==pair.Value,"Rejected/throwing requested-point query leaked scratch at "+pair.Key.ToString("X"));
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
            foreach(bool injectFailure in new[]{false,true})
            {
                int deletes=0;
                var single=new NativeBindings(allocation,new object(),null);
                bool threw=false;
                try
                {
                    single.ScopeVanillaSingle(request,123,()=>
                    {
                        deletes++;byte* cursor=p+NativeBindings.CursorRva;
                        Check(*(int*)(cursor+8)==1 && *(int*)(cursor+0x30)==123,"Single stages fresh active point and unit hover");
                        Check(*(int*)(cursor+0x14)==991 && *(int*)(cursor+0x24)==17 && *(int*)(cursor+0x4C)==19 && *(int*)(cursor+0x5C)==992,"Single stages independent building/pitch/wall selectors");
                        for(int i=0;i<0x80;i++) cursor[i]=0xCC;
                        *(int*)(p+NativeBindings.TileManagerRva+0x204E720)=4321; // Stand-in for legitimate native output, not input scratch.
                        if(injectFailure) throw new InvalidOperationException("injected single wrapper failure");
                    });
                }
                catch(InvalidOperationException) { threw=true; }
                Check(threw==injectFailure && deletes==1,"Single invokes its normal Vanilla wrapper exactly once, including failure");
                for(int i=0;i<0x80;i++) Check(p[NativeBindings.CursorRva+i]==saved[NativeBindings.CursorRva+i],"Single restores complete cursor after callback success/failure");
                Check(*(int*)(p+NativeBindings.TileManagerRva+0x204E720)==4321,"Single preserves real native output instead of rolling back gameplay/queue effects");
            }
        }
        finally { Marshal.FreeHGlobal(allocation); }
    }

    private static void RequestHitContracts()
    {
        Check(NativeDemolition.SelectUnitHover(9,-32767,-40000,false)==9,"No-building sentinel permits foreground unit");
        Check(NativeDemolition.SelectUnitHover(9,-32766,-32767,false)==0,"Exact native invalid-depth threshold is strict");
        Check(NativeDemolition.SelectUnitHover(9,-32766,-32766,false)==9,"Equal building/unit depth includes unit");
        Check(NativeDemolition.SelectUnitHover(9,100,99,false)==0 && NativeDemolition.SelectUnitHover(9,100,99,true)==9,"Selection-on preserves native unit-depth bypass");
        Check(NativeDemolition.SelectUnitHover(0,0,0,true)==0 && NativeDemolition.SelectUnitHover(-1,0,0,true)==0,"Absent/invalid first unit never stages a stale hover");
        Check(NativeDemolition.SelectUnitHover(9,100-49,75,false)==9 && NativeDemolition.SelectUnitHover(9,100,75,false)==0,"Managed building click depth requires the audited RunTick minus49 convention");
        const int width=17;
        int[] Cardinal(int tile)
        {
            int x=tile%width,y=tile/width;var result=new List<int>();
            if(y>0) result.Add(tile-width);if(x<width-1) result.Add(tile+1);
            if(y<width-1) result.Add(tile+width);if(x>0) result.Add(tile-1);
            return result.ToArray();
        }
        int centre=8*width+8;
        Check(NativeDemolition.DecorationSearchTiles(centre,Cardinal,_=>false).Length==25,"Depth1..4 visits25 cardinal cells, not a radius4/square halo");
        Check(!NativeDemolition.DecorationSearchTiles(centre,Cardinal,_=>false).Contains(centre+4),"Four cardinal steps are outside native decoration recognition");
        foreach (var blocked in new[]{new HashSet<int>(),new HashSet<int>{centre},new HashSet<int>{centre-1,centre+1,centre-width},new HashSet<int>{0,1,width,centre+2}})
        {
            bool Border(int tile)=>blocked.Contains(tile);
            var inverted=NativeDemolition.DecorationRequestTiles(centre,Cardinal,Border).ToHashSet();
            for(int source=0;source<width*width;source++)
                Check(inverted.Contains(source)==NativeDemolition.DecorationSearchTiles(source,Cardinal,Border).Contains(centre),"Reverse decoration search equals forward recognition, including destination-border asymmetry");
        }
        var records=new GameProjectile[40];
        for(int slot=25;slot<36;slot++) { records[slot].N000053FA_2=(ushort)(slot+1);records[slot].r_ProjectileType=ProjectileType.ArcherArrow;records[slot].r_PlayerSourceId=4; }
        records[33].r_ProjectileType=ProjectileType.Flag1;records[33].r_PlayerSourceId=0x12340003;
        fixed(GameProjectile* first=records)
        {
            Check(NativeDemolition.FirstOwnDecorationSlot(25,first,records.Length,3)==33,"Native ninth link is visible and ownership uses low16 bits");
            records[33].r_PlayerSourceId=4;records[34].r_ProjectileType=ProjectileType.Flag1;records[34].r_PlayerSourceId=3;
            Check(NativeDemolition.FirstOwnDecorationSlot(25,first,records.Length,3)==0,"Native tenth link must not extend the candidate search");
            records[25].N000053FA_2=25;
            Check(NativeDemolition.FirstOwnDecorationSlot(25,first,records.Length,3)==0,"Self-linked unrelated projectile terminates safely");
            Check(NativeDemolition.FirstOwnDecorationSlot(-1,first,records.Length,3)==0 && NativeDemolition.FirstOwnDecorationSlot(40,first,records.Length,3)==0,"Invalid signed FlyGrid IDs fail closed");
        }
        var towerCodes=new HashSet<int>{0x28,0x29,0x2A,0x2B,0x2C,0x3D,0x4A,0x4B,0x4C,0x4D,0x4E}; // Exact audited459D0 switch, independent fixture input.
        foreach(eStructs type in Enum.GetValues(typeof(eStructs)))
            Check(NativeDemolition.EffectiveRequestTile(100,TilePropertyFlag.IsBuilding,0,101,TilePropertyFlag.IsElevated,type)==(towerCodes.Contains((int)type)?101:0),"459D0 keep/tower-only elevated-neighbour branch: "+type);
        Check(NativeDemolition.EffectiveRequestTile(100,0,2,101,0,default)==100,"Connected ordinary tile keeps requested cell");
        Check(NativeDemolition.EffectiveRequestTile(100,TilePropertyFlag.IsMoat,0,101,0,default)==100,"Dug moat keeps requested tile even without path connection");
        Check(NativeDemolition.EffectiveRequestTile(100,TilePropertyFlag.CrenelatedLow,2,101,TilePropertyFlag.IsWall,default)==101,"Crenellation selects neighbouring wall");
        Check(NativeDemolition.EffectiveRequestTile(100,TilePropertyFlag.CrenelatedLow,0,101,0,default)==100,"Crenellation without neighbouring wall falls back");
        int[] Eight(int tile)=>new[]{tile-18,tile-17,tile-16,tile-1,tile+1,tile+16,tile+17,tile+18};
        var halo=NativeDemolition.ExpandNeighbourRequests(new[]{centre,centre+1},Eight);
        Check(halo.Length==12 && halo.Contains(centre-18) && halo.Contains(centre+19),"Registered-cell inverse includes all eight view directions and deduplicates overlaps");
        var house=new DemolitionTarget { Kind=TargetKind.Building,Type=(int)eStructs.STRUCT_HOVEL,Slot=1,GlobalId=1,Owner=3,Flags=77,TaskIndex=3<<16 };
        var other=house.Copy();other.Slot=2;other.GlobalId=2;other.Flags=88;
        var stock=house.Copy();stock.Type=(int)eStructs.STRUCT_GOODS_YARD;stock.Slot=3;stock.GlobalId=3;stock.Flags=99;
        var sector=stock.Copy();sector.Slot=4;sector.GlobalId=4;
        var reserved=house.Copy();reserved.Slot=5;reserved.GlobalId=5;reserved.Type=(int)eStructs.STRUCT_PARADEGROUND_LGT;reserved.TaskIndex=(5<<16)|1;
        var requestIndex=new NativeDemolition.PreviewIndex(new[]{house,other,stock,sector,reserved}.Select(t=>new NativeDemolition.BuildingMember(t.Slot,t.Owner,AliveState.IsAlive,t.Flags,t)));
        Check(!NativeDemolition.BuildingRequestMatches(requestIndex,house,0,false,new[]{1}),"Plain grass beside a house is not a building request cell");
        Check(NativeDemolition.BuildingRequestMatches(requestIndex,house,1,false,Array.Empty<int>()),"Requested registered structure reaches its actual house");
        Check(!NativeDemolition.BuildingRequestMatches(requestIndex,house,2,false,new[]{1}),"Another requested building never redirects to an adjacent ordinary house");
        Check(NativeDemolition.BuildingRequestMatches(requestIndex,house,5,false,Array.Empty<int>()),"Reserved-child request preserves canonical barracks/root linkage");
        Check(NativeDemolition.BuildingRequestMatches(requestIndex,stock,0,true,new[]{4}),"Stockpile connector can reach another sector of the same canonical stockpile");
        Check(!NativeDemolition.BuildingRequestMatches(requestIndex,stock,0,false,new[]{4}),"Plain grass next to stockpile does not enter the native connector branch");
        Check(!NativeDemolition.BuildingRequestMatches(requestIndex,stock,2,true,new[]{4}),"Existing requested structure takes precedence over connector neighbour fallback");
        foreach(int count in new[]{120,600,1200})
        {
            var watch=Stopwatch.StartNew();long cells=0;
            for(int root=0;root<count;root++)
            {
                int start=1000+root*100;
                var occupied=Enumerable.Range(0,3).SelectMany(y=>Enumerable.Range(0,3).Select(x=>start+y*17+x)).ToArray();
                var requests=NativeDemolition.ExpandNeighbourRequests(occupied,Eight);cells+=requests.Length;
                var reference=Enumerable.Range(-1,5).SelectMany(y=>Enumerable.Range(-1,5).Select(x=>start+y*17+x)).ToHashSet();
                Check(reference.SetEquals(requests),"Production120+ root inverse equals independent5x5 geometric fixture");
            }
            watch.Stop();
            Check(cells==25L*count,"Request expansion stays linear in selected registered cells");
            Console.WriteLine("BENCH preview-request-roots="+count+" expandedCells="+cells+" productionAndReferenceMs="+watch.Elapsed.TotalMilliseconds.ToString("F3"));
        }
        var roots=Enumerable.Range(1,120).Select(i=>new DemolitionTarget { Kind=TargetKind.Building,Owner=3,Slot=i,GlobalId=(uint)i,TileId=i }).ToArray();
        var quote=new NativeDemolition.PreviewQuote { OrderedRoots=roots };
        IEnumerable<DemolitionTarget> Closure(DemolitionTarget root)=>root.Slot%2==0?new[]{root,new DemolitionTarget { Kind=TargetKind.Decoration,Owner=3,Slot=1000+root.Slot,TileId=root.TileId }}:null;
        while(!NativeDemolition.StepPreviewRoots(quote,64,1,Closure,()=>0)) { }
        Check(quote.CompletedRoots.Count==60 && quote.CompletedRoots.All(r=>r.Root.Slot%2==0 && r.Closure.Length==2),"Approved120-root quote retains per-root closures; failed roots never become clipping entries");
        Check(quote.CompletedRoots.SelectMany(r=>r.Closure).Select(t=>t.Identity).ToHashSet().SetEquals(quote.Result.Select(t=>t.Identity)),"Root-associated quote result equals the frozen flat closure union");
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
        Check(!NativeDemolition.IdentitiesCurrent(frozen,t=>t.Kind==TargetKind.Building?null:t.Copy()),"Missing parent fails its frozen dependent component identity check");
        Check(!NativeDemolition.IdentitiesCurrent(frozen,t=>{var copy=t.Copy();if(copy.Kind==TargetKind.Building)copy.Owner=4;return copy;}),"Owner change fails the frozen dependent component identity check");
        Check(NativeDemolition.IdentitiesCurrent(frozen,t=>t.Copy()),"Unchanged complete frozen selection remains usable");
        var expected=frozen.ToDictionary(t=>t.Identity,StringComparer.Ordinal);
        var live=frozen.ToDictionary(t=>t.Identity,t=>t.Copy(),StringComparer.Ordinal);
        var newChild=flag.Copy();newChild.Slot=27;
        Check(!NativeDemolition.ClosureMatchesApproval(new[]{parent,flag,newChild},expected,live),"Unknown new same-tile dependent rejects component before mutation");
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
        Check(receipt[0].Status==ApplyStatus.Applied && receipt[1].Status==ApplyStatus.Forbidden,"Aborting remaining dependent component preserves the earlier completed result");
        int calls=0;
        var binding=new NativeBindings(new IntPtr(0x10000000),new object(),null,null,(manager,tile)=>
        { calls++;Check(manager.ToInt64()==0x10000000L+NativeBindings.ProjectileManagerRva,"Static helper uses projectile manager");Check(tile==44,"Static helper gets tileID, not projectile slot"); });
        binding.RemoveDecorationTile(44);Check(calls==1,"Exactly one same-tile mutation");
    }

    private static void ComponentCommitContracts()
    {
        DemolitionTarget Wall(int slot,int dependency=0) => new DemolitionTarget {Kind=TargetKind.Wall,Group=TargetGroup.Walls,
            Slot=slot,TileId=1000+slot,Type=1,Owner=3,X=slot,Y=4,Dependency=dependency};
        bool Plan(DemolitionTarget[] targets,Func<DemolitionTarget,DemolitionTarget> resolve,
            Func<DemolitionTarget,DemolitionTarget> canonical,Func<DemolitionTarget,List<DemolitionTarget>> closure,
            Func<DemolitionTarget,bool> permitted,out List<NativeDemolition.Operation> operations,out ApplyResult[] results) =>
            NativeDemolition.PlanComponent(3,targets,resolve,canonical,closure,permitted,out _,out operations,out results);
        var walls=Enumerable.Range(1,32).Select(id=>Wall(id,id-1)).ToArray();
        int permissionCalls=0,plannedOperations=0;
        var receipts=NativeDemolition.ApplyComponents(walls,component=>
        {
            bool allowed=Plan(component,t=>t.Copy(),t=>t,t=>new List<DemolitionTarget>{t},t=>{permissionCalls++;return t.Slot!=13;},out var plans,out var results);
            if(allowed)
            {
                plannedOperations+=plans.Count;
                foreach(var result in results)result.Status=ApplyStatus.Applied;
            }
            return results;
        });
        Check(permissionCalls==32 && plannedOperations==31,"Production component preflight checks each independent wall and keeps31 eligible plans");
        Check(receipts.Count(r=>r.Status==ApplyStatus.Applied)==31 && receipts[12].Status==ApplyStatus.Forbidden,"One occupied wall does not discard31 independent wall receipts");
        var parent=Wall(40);parent.Kind=TargetKind.Building;parent.Group=TargetGroup.Buildings;parent.GlobalId=40;
        var decoration=Wall(41);decoration.Kind=TargetKind.Decoration;decoration.Group=TargetGroup.Decorations;decoration.GlobalId=41;
        var independent=Wall(42,1);
        var frozen=new[]{parent,decoration,independent};int visited=0;
        receipts=NativeDemolition.ApplyComponents(frozen,component=>
        {
            visited++;
            bool allowed=Plan(component,t=>t.Identity==parent.Identity?null:t.Copy(),t=>t,
                t=>new List<DemolitionTarget>{t},t=>true,out _,out var results);
            if(allowed)foreach(var result in results)result.Status=ApplyStatus.Applied;
            return results;
        });
        Check(visited==2 && receipts[0].Status==ApplyStatus.Missing && receipts[1].Status==ApplyStatus.Forbidden && receipts[2].Status==ApplyStatus.Applied,
            "Missing parent protects its live decoration while independent wall proceeds through actual PlanComponent");
        foreach(int change in new[]{0,1,2})
        {
            bool allowed=Plan(new[]{parent,decoration},t=>
            {
                var live=t.Copy();
                if(t.Identity==parent.Identity)
                {
                    if(change==0)live.GlobalId++;
                    if(change==1)live.Owner=4;
                    if(change==2)live.Flags++;
                }
                return live;
            },t=>t,t=>new List<DemolitionTarget>{t},t=>true,out _,out var results);
            Check(!allowed && results[0].Status==ApplyStatus.Changed && results[1].Status==ApplyStatus.Forbidden,
                "Replacement, owner transfer and compound changes block the frozen family, variant"+change);
        }
        var child=parent.Copy();child.Slot=43;child.GlobalId=43;
        Check(Plan(new[]{parent,child},t=>t.Copy(),t=>t.Identity==child.Identity?parent:t,
            t=>new List<DemolitionTarget>{parent,child},t=>true,out var linkedPlans,out _)
            && linkedPlans.Count==1 && linkedPlans[0].Root.Identity==parent.Identity,"Canonical linked child contributes closure but never an independent refund plan");
        Check(!Plan(new[]{child},t=>t.Copy(),t=>parent,t=>new List<DemolitionTarget>{child},t=>true,out _,out _),
            "Canonical root absent from frozen component cannot orphan or separately refund its child");
        Check(!Plan(new[]{parent,child},t=>t.Copy(),t=>t.Identity==child.Identity?parent:t,
            t=>new List<DemolitionTarget>{parent},t=>true,out _,out _),"Every frozen linked child must remain covered by an approved root closure");
        Check(!Plan(new[]{parent},t=>t.Copy(),t=>t,t=>new List<DemolitionTarget>{parent,independent},t=>true,out _,out _),
            "New live child from another packet component cannot borrow that component's approval");
        Check(!Plan(new[]{parent},t=>t.Copy(),t=>t,t=>null,t=>true,out _,out _),"Unowned or unsupported live closure rejects before any plan executes");
        bool fatal=false;
        try {Plan(new[]{parent},t=>throw new InvalidOperationException("native preflight failure"),t=>t,t=>new List<DemolitionTarget>{t},t=>true,out _,out _);}
        catch(InvalidOperationException){fatal=true;}
        Check(fatal,"Actual PlanComponent propagates genuine native preflight exceptions to the fatal adapter boundary");
        bool accepted=NativeDemolition.TryMutationAttempt(_=>throw new NativeDemolition.ExpectedRejectionException(ApplyStatus.Changed,"stale before mutation"),out var rejected);
        Check(!accepted && rejected.Status==ApplyStatus.Changed,"Typed expected rejection before the first helper is locally recoverable");
        foreach(bool nativeError in new[]{false,true})
        {
            fatal=false;int refunds=0,deletes=0;
            try
            {
                NativeDemolition.TryMutationAttempt(attempt=>
                {
                    attempt.BeforeMutation();refunds++;
                    if(nativeError)throw new InvalidOperationException("refund Post failed");
                    throw new NativeDemolition.ExpectedRejectionException(ApplyStatus.Changed,"refund Post replaced target");
                },out _);
                deletes++;
            }
            catch(Exception){fatal=true;}
            Check(fatal && refunds==1 && deletes==0,"After refund begins both stale rejection and genuine exception are fatal, without delete/retry");
        }
        fatal=false;
        try {NativeDemolition.TryMutationAttempt(_=>throw new InvalidOperationException("genuine helper failure before tracked mutation"),out _);}
        catch(InvalidOperationException){fatal=true;}
        Check(fatal,"An arbitrary exception is never reclassified as a benign stale target");
        int appliedComponents=0;
        receipts=NativeDemolition.ApplyComponents(walls,component=>
        {
            appliedComponents++;
            return component.Select(t=>new ApplyResult {Identity=t.Identity,Status=ApplyStatus.Failed}).ToArray();
        });
        Check(appliedComponents==1 && receipts[0].Status==ApplyStatus.Failed && receipts.Skip(1).All(r=>r.Status==ApplyStatus.Forbidden),
            "A genuine native failure stops later components inside the current packet too");
        var completed=new HashSet<string>(StringComparer.Ordinal){parent.Identity};
        var partial=new[]{new ApplyResult(),new ApplyResult {Status=ApplyStatus.Changed,Detail="replacement"}};
        NativeDemolition.CompleteResults(new[]{parent,child},partial,completed,new HashSet<string>(),true);
        Check(partial[0].Status==ApplyStatus.Applied && partial[1].Status==ApplyStatus.Changed && partial[1].Detail=="replacement",
            "Component completion preserves already Applied identities and precise pre-mutation rejection status");
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
        Check(oversizedCalls==roots.Length,"Clock-budgeted preview does not impose an arbitrary64-root frame delay");
        Check(!NativeDemolition.StepPreviewRoots(quote,64,100,Evaluate,()=>100) && evaluated==64,"Expired deadline invokes no native evaluation");
        long clock=0;
        Check(!NativeDemolition.StepPreviewRoots(quote,64,3,Evaluate,()=>clock++) && evaluated==67,"Actual clock boundary stops a partially processed root batch");
        while(!NativeDemolition.StepPreviewRoots(quote,64,100,Evaluate,()=>0)) { }
        Check(evaluated==257 && DemolitionPolicy.TargetDigest(quote.Result)==DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(roots)),"Resumed257-root result equals unbudgeted complete result");
    }

    private static void PreparationMonitorContracts()
    {
        // Only managed preflight/expired/error branches are exercised here. Zero base pointers
        // deliberately cannot service a game API read, so no game or Unity process is needed.
        var gate=new object();
        var native=(NativeDemolition)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(NativeDemolition));
        typeof(NativeDemolition).GetField("native",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)
            .SetValue(native,new NativeBindings(IntPtr.Zero,gate,null));
        typeof(NativeDemolition).GetField("available",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(native,true);
        var selection=new SelectionSpec((ushort)(1<<(int)TargetGroup.Walls),false,null);
        var job=native.BeginPreviewPreparation(3,selection);
        using var entered=new System.Threading.ManualResetEventSlim();
        using var release=new System.Threading.ManualResetEventSlim();
        var worker=System.Threading.Tasks.Task.Run(()=>{lock(gate){entered.Set();release.Wait();}});
        Check(entered.Wait(2000),"Fixture owns native monitor before testing busy path");
        try
        {
            Check(!native.TryStepPreviewPreparation(job,long.MaxValue,out bool complete,out _) && !complete,"Busy preparation monitor defers without advancing native work");
            var ground=native.BeginGroundPreparation(job.Snapshot,Array.Empty<int>(),3,selection);
            Check(!native.TryStepGroundPreparation(ground,long.MaxValue,out complete) && !complete,"Busy ground preparation monitor defers without waiting");
        }
        finally {release.Set();worker.GetAwaiter().GetResult();}
        Check(native.TryStepPreviewPreparation(job,0,out bool done,out _) && !done && job.Phase==0,"Expired preparation deadline reads no native slot and retains resumable phase");
        Check(!System.Threading.Monitor.IsEntered(gate),"Preparation returns with native monitor released");
        var expiredGround=native.BeginGroundPreparation(job.Snapshot,Array.Empty<int>(),3,selection);
        Check(native.TryStepGroundPreparation(expiredGround,0,out done) && !done && expiredGround.Phase==0,"Expired ground deadline retains resumable phase");
        Check(!System.Threading.Monitor.IsEntered(gate),"Ground preparation releases monitor on a budget yield");
        job.Phase=6;job.Ordered.Add(null);job.Result=new PreviewHitCandidate[1];
        bool threw=false;
        try {native.TryStepPreviewPreparation(job,long.MaxValue,out _,out _);} catch(NullReferenceException){threw=true;}
        Check(threw && !System.Threading.Monitor.IsEntered(gate),"Preparation exception never retains monitor across frames");
        var invalid=native.BeginGroundPreparation(new NativeDemolition.PreviewSnapshot {Actor=3,HitCandidates=new PreviewHitCandidate[]{null}},Array.Empty<int>(),3,selection);
        threw=false;try {native.TryStepGroundPreparation(invalid,long.MaxValue,out _);} catch(NullReferenceException){threw=true;}
        Check(threw && !System.Threading.Monitor.IsEntered(gate),"Ground preparation exception releases monitor");
        var closureMethod=typeof(NativeDemolition).GetMethod("ClosureOwn",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        foreach(var kind in new[]{TargetKind.Pitch,TargetKind.DugMoat})
        {
            var root=new DemolitionTarget {Kind=kind,Owner=3};object[] args={root,3,null,null};
            Check((bool)closureMethod.Invoke(native,args) && ((List<DemolitionTarget>)args[2]).Single()==root,"Non-decoration closure avoids global projectile reads: "+kind);
        }
    }
    private static void WallIdentityNormalization()
    {
        const int tile=233160,x=376,y=504;
        var flags=TilePropertyFlag.IsWall|TilePropertyFlag.CrenelatedLow;
        DemolitionTarget Wall(TilePropertyFlag value,int height=98,int owner=1) =>
            NativeDemolition.CreateWallIdentity(tile,x,y,value,owner,height);
        var frozen=Wall(flags);
        var normalized=Wall(flags|TilePropertyFlag.CrenelatedHigh);
        Check(NativeDemolition.SameState(frozen,normalized),"Observed neighbor normalization preserves the same wall: flags0x300 to0x400300, height106/default8");
        Check(NativeDemolition.SameState(normalized,frozen),"Frozen identity is independent of the derived High appearance bit");
        Check(frozen.Type==normalized.Type && frozen.Flags==normalized.Flags,"Both wire identity fields omit only derived High");
        Check(!NativeDemolition.SameState(frozen,Wall(flags&~TilePropertyFlag.CrenelatedLow)),"Crenellated material change remains rejected");
        Check(!NativeDemolition.SameState(frozen,Wall(flags|TilePropertyFlag.IsLowWall)),"Low-wall material change remains rejected");
        Check(!NativeDemolition.SameState(frozen,Wall(flags|TilePropertyFlag.IsStairs)),"Stair conversion remains rejected");
        Check(!NativeDemolition.SameState(frozen,Wall(flags&~TilePropertyFlag.IsWall)),"Removed wall remains rejected");
        Check(!NativeDemolition.SameState(frozen,Wall(flags,97)),"Changed height remains rejected");
        Check(!NativeDemolition.SameState(frozen,Wall(flags,98,2)),"Changed owner remains rejected");
        var low=Wall(TilePropertyFlag.IsWall|TilePropertyFlag.IsLowWall,60);
        Check(!NativeDemolition.SameState(low,Wall(flags|TilePropertyFlag.CrenelatedHigh,68)),"Observed low-wall and crenellated-wall variants remain distinct");
        var other=NativeDemolition.CreateWallIdentity(tile+1,x+1,y,flags,1,98);
        Check(!NativeDemolition.SameState(frozen,other),"Adjacent replacement tile remains rejected");
    }

    private static void ClassificationAndRefunds()
    {
        foreach (var type in new[]{eStructs.STRUCT_GARDEN,eStructs.STRUCT_GARDEN_SMALL,eStructs.STRUCT_GARDEN_MED,eStructs.STRUCT_GARDEN_LARGE,eStructs.STRUCT_POND,eStructs.STRUCT_POND_SMALL,eStructs.STRUCT_POND_LARGE,eStructs.STRUCT_POND5,eStructs.STRUCT_POND6,eStructs.STRUCT_POND7,eStructs.STRUCT_POND8})
            Check(NativeDemolition.Classify(type)==TargetGroup.PositiveFear,"Positive fear size/variant missing: "+type);
        Check(NativeDemolition.Classify(eStructs.STRUCT_RACK_FLOGGING)==TargetGroup.NegativeFear,"Flogging rack omitted by AIV subset must remain negative fear");
        Check(NativeDemolition.Classify(eStructs.STRUCT_HEAD_ON_SPIKE)==TargetGroup.NegativeFear,"Head on spike remains negative fear");
        var storageTypes=new[]{eStructs.STRUCT_GOODS_YARD,eStructs.STRUCT_GRANARY,eStructs.STRUCT_ARMOURY};
        var buildingsFilter=new SelectionSpec((ushort)(1<<(int)TargetGroup.Buildings),false,null);
        var storageFilter=new SelectionSpec((ushort)(1<<(int)TargetGroup.Stockpiles),false,null);
        foreach(var type in storageTypes)
        {
            var target=new DemolitionTarget {Kind=TargetKind.Building,Type=(int)type,Group=NativeDemolition.Classify(type)};
            Check(target.Group==TargetGroup.Stockpiles,"Storage building has its existing independent filter group: "+type);
            Check(storageFilter.Matches(target) && !buildingsFilter.Matches(target),"Storage is selected only by its own enabled group: "+type);
            var shift=new SelectionSpec(0,true,target);
            foreach(var otherType in storageTypes)
            {
                var other=new DemolitionTarget {Kind=TargetKind.Building,Type=(int)otherType,Group=NativeDemolition.Classify(otherType)};
                Check(shift.Matches(other)==(type==otherType),"Shift keeps each storage building's exact type despite disabled filters: "+type+"/"+otherType);
            }
        }
        var house=new DemolitionTarget {Kind=TargetKind.Building,Type=(int)eStructs.STRUCT_HOVEL,Group=NativeDemolition.Classify(eStructs.STRUCT_HOVEL)};
        Check(buildingsFilter.Matches(house) && !storageFilter.Matches(house),"Ordinary buildings remain outside the storage filter");
        foreach(var type in new[]{ProjectileType.Flag1,ProjectileType.Flag2,ProjectileType.Flag3,ProjectileType.CrusaderFlag,ProjectileType.Brazier,ProjectileType.Heads})
            Check(NativeDemolition.IsDecorationType(type),"Renaming the decoration filter preserves its native static type: "+type);
        Check(NativeDemolition.Classify(eStructs.STRUCT_STAIRS)==TargetGroup.Fortifications,"Stair structures stay fortifications");
        foreach(var wall in new[]{eStructs.STRUCT_WOOD_WALL,eStructs.STRUCT_STONE_WALL,eStructs.STRUCT_CRENAL_WALL})
            Check(NativeDemolition.Classify(wall)==TargetGroup.Walls,"Wall structures have the independent wall filter");
        Check(NativeDemolition.WallGroup(TilePropertyFlag.IsWall)==TargetGroup.Walls,"Ordinary wall tiles use Walls");
        Check(NativeDemolition.WallGroup(TilePropertyFlag.IsWall|TilePropertyFlag.IsStairs)==TargetGroup.Fortifications,"Stair wall tiles stay Fortifications");
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
