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
        Check(new AreaRect(3,3,5,5).Intersects(new AreaRect(1,1,3,3)),"boundary tile overlap counts");
        Check(!new AreaRect(4,4,5,5).Intersects(new AreaRect(1,1,3,3)),"disjoint footprints excluded");
        var a=Target(TargetGroup.Buildings,1,2);var b=Target(TargetGroup.Buildings,1,1);
        Check(DemolitionPolicy.Canonicalize(new[]{a,b,a}).Length==2,"identities deduplicated");
        Check(DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{a,b}))==DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{b,a})),"canonical packet order");
        ScreenGeometry();ColumnRanges();Settings();
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
        ushort before=settings.Mask;int changes=0;settings.Changed+=()=>changes++;
        settings.Set(TargetGroup.Decorations,true);
        Check(settings.Mask==(ushort)(before|(1<<(int)TargetGroup.Decorations)) && settings.Get(TargetGroup.Pitch),"ninth bit does not truncate or alter old preferences");
        settings.Set(TargetGroup.Decorations,true);Check(changes==1,"unchanged preference does not republish");
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
    }
}
