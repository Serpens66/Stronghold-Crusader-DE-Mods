using System;
using AreaDemolitionTest;

internal static class Program
{
    private static int checks;
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static DemolitionTarget Target(TargetGroup group,int type=1,uint id=1)=>new DemolitionTarget {Kind=TargetKind.Building,Group=group,Type=type,Slot=(int)id,GlobalId=id,Bounds=new AreaRect(1,1,3,3)};
    private static void Main()
    {
        var g=new SelectionGesture();var plain=new SelectionSpec(1,false,null);
        g.Begin(10,10,plain);g.Move(13,10,true,1);Check(g.Release(false)==GestureRelease.Single,"small movement remains single");
        g.Begin(10,10,plain);g.Move(14,10,true,1);Check(g.Stage==GestureStage.Dragging,"four pixel drag");
        g.Move(10,10,true,1);Check(g.Stage==GestureStage.Dragging,"return does not become a single deletion");
        Check(g.Release(false)==GestureRelease.Area,"release area");
        g.Begin(0,0,plain);g.Cancel();Check(g.Owned,"cancel owns remaining input");Check(g.Release(false)==GestureRelease.Cancelled,"cancel has no deletion");
        g.Begin(0,0,plain);Check(g.Release(true)==GestureRelease.Cancelled,"single release over UI cancels");
        g.Begin(10,10,plain);g.Move(25,40,true,1);var lastValid=g.Area;g.Move(400,400,false,1);
        Check(g.Area==lastValid && g.Stage==GestureStage.Dragging,"HUD crossing retains last valid endpoint and gesture");
        Check(g.Release(true)==GestureRelease.Area,"owned area release over HUD commits");
        Check(g.Release(true)==GestureRelease.None,"the same release cannot commit twice");
        g.Begin(10,10,plain);g.Move(100,100,false,1);
        Check(g.Stage==GestureStage.Dragging && g.Area==new ScreenRect(10,10,10,10),"fast HUD crossing still meets screen movement threshold");
        g.Move(50,60,true,1);Check(g.Area==new ScreenRect(10,10,50,60),"return to map resumes endpoint tracking");g.Reset();
        var same=new SelectionSpec(0,true,Target(TargetGroup.PositiveFear));
        Check(same.Matches(Target(TargetGroup.PositiveFear,99)),"positive fear crosses variants");
        Check(!same.Matches(Target(TargetGroup.NegativeFear)),"fear signs stay separate");
        Check(!new SelectionSpec(255,true,null).Matches(Target(TargetGroup.Buildings)),"shift on empty never broadens");
        Check(new SelectionSpec(0,true,Target(TargetGroup.Buildings,2)).Matches(Target(TargetGroup.Buildings,2)),"same type ignores disabled category");
        Check(!new SelectionSpec(0,true,Target(TargetGroup.Buildings,2)).Matches(Target(TargetGroup.Buildings,3)),"normal types stay distinct");
        Check(plain.Matches(Target(TargetGroup.Buildings))&&!plain.Matches(Target(TargetGroup.Fortifications)),"local category filter");
        var start=Target(TargetGroup.Buildings,2,12);
        g.Begin(1,2,new SelectionSpec(1,false,start));g.Move(20,30,true,1);
        Check(g.SetSameType(true) && g.Selection.SameType && g.Selection.Start.GlobalId==12,"late Shift uses original down object");
        Check(g.Selection.Matches(Target(TargetGroup.Buildings,2))&&!g.Selection.Matches(Target(TargetGroup.Buildings,3)),"late Shift narrows to start type");
        Check(!g.SetSameType(true),"unchanged modifier does not invalidate the quote");
        Check(g.SetSameType(false) && !g.Selection.SameType && g.Selection.Filters==1,"releasing Shift restores original menu mask");
        g.Cancel();Check(!g.SetSameType(true) && g.Stage==GestureStage.Cancelled,"Shift cannot revive cancelled gesture");g.Release(false);
        Check(!g.SetSameType(true),"Shift cannot alter a completed gesture");
        g.Begin(0,0,new SelectionSpec(ushort.MaxValue,false,null));g.SetSameType(true);
        Check(!g.Selection.Matches(Target(TargetGroup.Buildings)),"late Shift on empty original start never picks a later object");g.Reset();
        var decoration=new DemolitionTarget {Kind=TargetKind.Decoration,Group=TargetGroup.Decorations,Type=9};
        Check(new SelectionSpec((ushort)(1<<(int)TargetGroup.Decorations),false,null).Matches(decoration),"ninth category bit is selectable");
        Check(!new SelectionSpec(255,false,null).Matches(decoration),"old eight category bits do not accidentally enable decorations");
        var stockpile=Target(TargetGroup.Stockpiles,10,30);
        Check((int)TargetGroup.Decorations==8 && (int)TargetGroup.Stockpiles==9,"new group appends without changing existing category identities");
        Check(!plain.Matches(stockpile),"building-only filter excludes stockpiles");
        Check(new SelectionSpec((ushort)(1<<(int)TargetGroup.Stockpiles),false,null).Matches(stockpile),"independent tenth category bit selects stockpiles");
        Check(!new SelectionSpec((ushort)(1<<(int)TargetGroup.Stockpiles),false,null).Matches(Target(TargetGroup.Buildings)),"stockpile filter does not select ordinary buildings");
        Check(new SelectionSpec(0,true,stockpile).Matches(Target(TargetGroup.Stockpiles,10,31)),"Shift selects matching stockpile type despite disabled category");
        Check(!new SelectionSpec(0,true,stockpile).Matches(Target(TargetGroup.Buildings,11)),"Shift stockpile type remains distinct from other buildings");
        var wall=new DemolitionTarget {Kind=TargetKind.Wall,Group=TargetGroup.Walls,Type=1};
        var stairs=new DemolitionTarget {Kind=TargetKind.Wall,Group=TargetGroup.Fortifications,Type=2};
        var fortifications=new SelectionSpec((ushort)(1<<(int)TargetGroup.Fortifications),false,null);
        var walls=new SelectionSpec((ushort)(1<<(int)TargetGroup.Walls),false,null);
        Check(fortifications.Matches(stairs) && !fortifications.Matches(wall),"fortifications retain stairs and exclude ordinary walls");
        Check(walls.Matches(wall) && !walls.Matches(stairs),"walls have an independent eleventh category bit");
        Check(new SelectionSpec(0,true,wall).Matches(new DemolitionTarget {Kind=TargetKind.Wall,Group=TargetGroup.Walls,Type=1}),"Shift selects wall type despite disabled category");
        Check(new AreaRect(3,3,5,5).Intersects(new AreaRect(1,1,3,3)),"boundary tile overlap counts");
        Check(!new AreaRect(4,4,5,5).Intersects(new AreaRect(1,1,3,3)),"disjoint footprints excluded");
        var a=Target(TargetGroup.Buildings,1,2);var b=Target(TargetGroup.Buildings,1,1);
        Check(DemolitionPolicy.Canonicalize(new[]{a,b,a}).Length==2,"identities deduplicated");
        Check(DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{a,b}))==DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{b,a})),"canonical packet order");
        ScreenGeometry();ColumnRanges();Settings();PreviewScheduling();RenderGeometry();PointerUi();ShrinkingGestures();ScreenGeometryReference.Verify(Check);BodySelectionChecks.Verify(Check);
        Console.WriteLine("PASS "+checks+" policy behavior checks");
    }
    private static void ScreenGeometry()
    {
        var a=new ScreenPoint(0,1);var b=new ScreenPoint(1,0);var c=new ScreenPoint(0,-1);var d=new ScreenPoint(-1,0);
        Check(!new ScreenRect(.8f,.8f,1,1).IntersectsQuad(a,b,c,d),"diamond AABB empty corner is not an overlap");
        Check(new ScreenRect(1,0,2,1).IntersectsQuad(a,b,c,d),"edge tangent contact counts");
        Check(new ScreenRect(-2,-.1f,2,.1f).IntersectsQuad(a,b,c,d),"crossing strip without enclosed vertices counts");
        Check(new ScreenRect(-.1f,-.1f,.1f,.1f).IntersectsQuad(a,b,c,d),"rectangle wholly inside footprint counts");
        Check(new ScreenRect(0,-2,0,2).IntersectsQuad(a,b,c,d),"zero-width inclusive contact counts");
        var rect=new ScreenRect(2,3,8,9);
        Check(rect==new ScreenRect(8,3,2,9) && rect==new ScreenRect(2,9,8,3) && rect==new ScreenRect(8,9,2,3),"all screen drag directions normalize equally");
        Check(!new ScreenRect(float.NaN,0,1,1).IsFinite && !new ScreenRect(float.NaN,0,1,1).IntersectsQuad(a,b,c,d),"NaN selection is never a target");
        Check(!new ScreenRect(0,0,float.PositiveInfinity,1).IsFinite && !new ScreenRect(0,0,float.PositiveInfinity,1).IntersectsQuad(a,b,c,d),"infinite selection is never a target");
    }
    private static void Settings()
    {
        var file=new BepInEx.Configuration.ConfigFile();
        file.Values["Area filters/Buildings"]=false;file.Values["Area filters/Fortifications"]=true;file.Values["Area filters/Pitch"]=true;
        var settings=new DemolitionSettings(file);
        Check(!settings.Get(TargetGroup.Buildings) && settings.Get(TargetGroup.Fortifications) && settings.Get(TargetGroup.Pitch),"old preference keys retain their values");
        Check(!settings.Get(TargetGroup.Decorations),"new decoration preference defaults off");
        Check(!settings.Get(TargetGroup.Stockpiles),"new stockpile preference defaults off");
        Check(!settings.Get(TargetGroup.Walls),"independent wall preference defaults off even when fortifications were enabled");
        ushort before=settings.Mask;int changes=0;settings.Changed+=()=>changes++;
        settings.Set(TargetGroup.Decorations,true);
        Check(settings.Mask==(ushort)(before|(1<<(int)TargetGroup.Decorations)) && settings.Get(TargetGroup.Pitch),"ninth bit does not truncate or alter old preferences");
        settings.Set(TargetGroup.Decorations,true);Check(changes==1,"unchanged preference does not republish");
        ushort nine=settings.Mask;
        settings.Set(TargetGroup.Stockpiles,true);
        Check(settings.Mask==(ushort)(nine|(1<<(int)TargetGroup.Stockpiles)),"tenth bit preserves all nine original preferences");
        var reopened=new DemolitionSettings(file);
        Check(reopened.Get(TargetGroup.Stockpiles) && reopened.Mask==settings.Mask,"stockpile key persists independently of old keys");
        reopened.Set(TargetGroup.Stockpiles,false);
        Check(reopened.Mask==nine && !reopened.Get(TargetGroup.Buildings) && reopened.Get(TargetGroup.Decorations),"disabling stockpile leaves old settings untouched");
        ushort oldFilters=reopened.Mask;
        reopened.Set(TargetGroup.Walls,true);
        var withWalls=new DemolitionSettings(file);
        Check(withWalls.Get(TargetGroup.Walls) && withWalls.Mask==(ushort)(oldFilters|(1<<(int)TargetGroup.Walls)),"wall preference persists without changing ten original keys");
        withWalls.Set(TargetGroup.Fortifications,false);
        Check(withWalls.Get(TargetGroup.Walls),"disabling fortifications leaves the independent wall preference enabled");
    }
    private static void ColumnRanges()
    {
        // The oracle uses the exact production SAT, independently of the slab's X-only cull.
        // Include both screen-height signs and all four projected native grid rotations.
        var axes=new[]{(32f,16f,-32f,16f),(-32f,16f,-32f,-16f),(-32f,-16f,32f,-16f),(32f,-16f,32f,16f)};
        var heights=new[]{short.MinValue/32f,-32f,-1f,0f,1f,32f,short.MaxValue/32f};
        var rectangles=new[]{new ScreenRect(-100,-100,160,140),new ScreenRect(100,-100000,150,100000),new ScreenRect(-800,-100000,-720,100000)};
        foreach(var axis in axes) foreach(float elevationX in new[]{0f,.25f,-.25f}) foreach(var rectangle in rectangles)
        {
            float ux=axis.Item1,uy=axis.Item2,vx=axis.Item3,vy=axis.Item4;
            double halfWidth=(Math.Abs(ux)+Math.Abs(vx))*.5;
            bool covered=true;int collisions=0;
            for(int y=0;y<24;y++)
            {
                bool range=rectangle.TryGetColumnRange(y*vx,ux,elevationX,short.MinValue/32.0,short.MaxValue/32.0,halfWidth,0,31,out int first,out int last);
                if(range) Check(first>=0 && last<=31 && first<=last,"broadphase stays within native column bounds");
                for(int x=0;x<32;x++) foreach(float height in heights)
                {
                    float cx=x*ux+y*vx+height*elevationX,cy=x*uy+y*vy+height*32;
                    var a=new ScreenPoint(cx-(ux+vx)*.5f,cy-(uy+vy)*.5f);
                    var b=new ScreenPoint(cx+(ux-vx)*.5f,cy+(uy-vy)*.5f);
                    var c=new ScreenPoint(cx+(ux+vx)*.5f,cy+(uy+vy)*.5f);
                    var d=new ScreenPoint(cx+(-ux+vx)*.5f,cy+(-uy+vy)*.5f);
                    if(!rectangle.IntersectsQuad(a,b,c,d))continue;
                    collisions++;if(!range || x<first || x>last)covered=false;
                }
            }
            Check(covered,"slab never drops a brute-force SAT hit at a signed height or rotated projection");
            // Not every off-screen rectangle must hit every orientation; some are intentionally disjoint.
            if(elevationX==0 && rectangle==rectangles[0])Check(collisions>0,"rotation oracle exercises actual overlaps");
        }
        var small=new ScreenRect(0,0,10,10);
        Check(small.TryGetColumnRange(0,0,0,-1024,1024,1,0,31,out int lo,out int hi) && lo==0 && hi==31,"vertical column projection conservatively includes every column");
        Check(!small.TryGetColumnRange(100,0,0,-1024,1024,1,0,31,out _,out _),"disjoint vertical column projection is excluded");
        var nearlyVertical=new ScreenRect(1e-7f,0,1e-7f,1);
        Check(nearlyVertical.TryGetColumnRange(0,1e-10,0,0,0,0,0,2000,out lo,out hi) && lo<=1000 && hi>=1000,"small nonzero projected column step is not treated as zero");
    }
    private static void PreviewScheduling()
    {
        const long frequency=1000000;
        var policy=new PreviewWorkPolicy(frequency);
        int starts=0;
        // Every 100FPS input frame requests a different endpoint: dirtiness cannot bypass pacing.
        for(int frame=0;frame<100;frame++)
        {
            long now=frame*frequency/100;
            if(!policy.CanBegin(now,false))continue;
            starts++;policy.Began(now);
        }
        Check(starts==10,"continuous 100FPS requests start at most ten ordinary quotes per second");
        Check(!policy.CanBegin(frequency-1,false) && policy.CanBegin(frequency,false),"next ordinary quote waits until its deadline boundary");
        policy.Began(frequency);
        long releaseTime=frequency+frequency/1000;
        Check(!policy.CanBegin(releaseTime,false) && policy.CanBegin(releaseTime,true),"final release can start immediately while an ordinary quote is delayed");
        long ordinaryBudget=policy.Deadline(releaseTime)-releaseTime;
        policy.Began(releaseTime);
        Check(policy.Deadline(releaseTime)-releaseTime==ordinaryBudget,"final quote start does not expand the incremental step budget");
        // Two milliseconds is the UI work contract, independent of the clock's tick units.
        Check(ordinaryBudget*1000==frequency*2,"step deadline grants two milliseconds rather than an unbounded final capture");
        Check(!policy.CanBegin(releaseTime,false),"completed final start also paces the next ordinary quote");
        policy.Reset();Check(policy.CanBegin(0,false),"mission reset removes previous quote timing state");
        var coarse=new PreviewWorkPolicy(1);coarse.Began(0);
        Check(!coarse.CanBegin(0,false) && coarse.CanBegin(1,false) && coarse.Deadline(0)>0,"coarse clocks still require progress between steps");
    }
    private static void RenderGeometry()
    {
        var building=Target(TargetGroup.Buildings,4,7);building.X=10;building.Y=20;building.OccupiedTiles=new[]{100,101,102};
        var other=Target(TargetGroup.Stockpiles,10,8);other.X=20;other.Y=30;other.OccupiedTiles=new[]{200,201};
        var original=new[]{building,other};var clone=new[]{building.Copy(),other.Copy()};
        clone[0].GlobalId++;clone[0].Owner=8;clone[0].Flags=456;clone[0].TaskIndex=789;
        Check(RenderSelectionGeometry.Equals(original,clone),"fresh immutable receipt metadata does not invalidate identical render membership");
        foreach(Action<DemolitionTarget> change in new Action<DemolitionTarget>[] {
            t=>t.Kind=TargetKind.Pitch,t=>t.Slot++,t=>t.Type++,t=>t.X++,t=>t.Y++,
            t=>t.OccupiedTiles=new[]{100,101,999},t=>t.OccupiedTiles=new[]{100,101},t=>t.OccupiedTiles=new[]{102,101,100}})
        {
            var changed=new[]{building.Copy(),other.Copy()};change(changed[0]);
            Check(!RenderSelectionGeometry.Equals(original,changed),"render dependency mutation invalidates cached geometry");
        }
        Check(!RenderSelectionGeometry.Equals(original,new[]{building.Copy()}),"target count change invalidates cached geometry");
        Check(!RenderSelectionGeometry.Equals(original,new[]{other.Copy(),building.Copy()}),"target order change cannot reuse an aligned comparison");
        var decoration=new DemolitionTarget {Kind=TargetKind.Decoration,Group=TargetGroup.Decorations,Slot=25,Type=10,X=4,Y=5,OccupiedTiles=new[]{44}};
        var decorationClone=decoration.Copy();decorationClone.OccupiedTiles=new[]{99,100};decorationClone.GlobalId=99;
        Check(RenderSelectionGeometry.Equals(new[]{decoration},new[]{decorationClone}),"decoration render membership uses slot/type/XY rather than unrelated footprint metadata");
        decorationClone.Type++;
        Check(!RenderSelectionGeometry.Equals(new[]{decoration},new[]{decorationClone}),"decoration type change invalidates exact Fly membership");
    }
    private static void PointerUi()
    {
        Check(PointerUiPolicy.Filter(PointerVisualRole.Decoration)==PointerHitDisposition.SkipSubtree,"selection frame and status text subtree do not hit their own drag");
        Check(PointerUiPolicy.Filter(PointerVisualRole.Overlay)==PointerHitDisposition.SkipSelf,"overlay self is ignored while its menu/backdrop children remain searchable");
        Check(PointerUiPolicy.Filter(PointerVisualRole.Ordinary)==PointerHitDisposition.Continue,"real HUD/menu/backdrop are retained by filtered hit traversal");
        Check(PointerUiPolicy.IsBlocking(new string[]{null,null},false),"underlying ordinary HUD remains blocking after decoration is skipped");
        Check(!PointerUiPolicy.IsBlocking(new[]{"IgnoreSelf",(string)null},false),"Vanilla exact-hit IgnoreSelf remains pass-through");
        Check(PointerUiPolicy.IsBlocking(new[]{(string)null,"IgnoreSelf"},false),"ancestor IgnoreSelf does not make an ordinary text/button hit pass-through");
        Check(!PointerUiPolicy.IsBlocking(new[]{(string)null,"Ignore"},false),"Vanilla Ignore applies across a child-to-parent walk");
        Check(!PointerUiPolicy.IsBlocking(new[]{"IgnoreNotME"},false) && PointerUiPolicy.IsBlocking(new[]{"IgnoreNotME"},true),"IgnoreNotME honors Vanilla editor-menu mode");
        Check(PointerUiPolicy.IsBlocking(new[]{"IgnoreME"},false) && !PointerUiPolicy.IsBlocking(new[]{"IgnoreME"},true),"IgnoreME honors Vanilla editor-menu mode");
        Check(PointerUiPolicy.IsBlocking(new[]{"ignore"},false),"unknown or differently cased tags never weaken the HUD shield");
        Check(PointerUiPolicy.IsBlocking(null,false) && PointerUiPolicy.IsBlocking(Array.Empty<string>(),false),"an unclassified remaining visual fails closed");
    }
    private static void ShrinkingGestures()
    {
        var g=new SelectionGesture();var selection=new SelectionSpec(1,false,null);
        foreach(int dx in new[]{-1,1})foreach(int dy in new[]{-1,1})
        {
            g.Begin(100,100,selection);g.Move(100+80*dx,100+60*dy,true,1);
            var large=g.Area;
            g.Move(100+20*dx,100+15*dy,true,1);
            Check(g.Area.Width==20 && g.Area.Height==15 && g.Area.Width<large.Width && g.Area.Height<large.Height,"all four drag directions shrink while held toward their start");
            g.Move(100-30*dx,100-25*dy,true,1);
            Check(g.Area==new ScreenRect(100,100,100-30*dx,100-25*dy),"drag can cross its original start without retaining the historical extent");
            var beforeHud=g.Area;g.Move(100,100,false,1);
            Check(g.Area==beforeHud,"real HUD still freezes the last valid endpoint during reverse motion");
            g.Move(100,100,true,1);
            Check(g.Area.Width==0 && g.Area.Height==0 && g.Stage==GestureStage.Dragging,"returning to the origin shrinks to zero without becoming a single demolition");
            Check(g.Release(false)==GestureRelease.Area,"reverse-drag completion preserves area semantics");
        }
    }
}
