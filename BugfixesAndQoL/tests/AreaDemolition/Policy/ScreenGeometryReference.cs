using System;
using System.Diagnostics;
using BugfixesAndQoL.AreaDemolition;

// Frozen pre-optimization implementation, captured before the contained-quad fast path.
// The reference intentionally retains the old full SAT to detect selection broadening.
internal static class ScreenGeometryReference
{
    internal static bool IntersectsQuadReference(ScreenRect rectangle,ScreenPoint a,ScreenPoint b,ScreenPoint c,ScreenPoint d)=>FullSat(rectangle,new Quad(a,b,c,d));
    internal static void Verify(Action<bool,string> check)
    {
        var rectangles=new[]{new ScreenRect(-100,-100,160,140),new ScreenRect(.8f,.8f,1,1),new ScreenRect(-2,-.1f,2,.1f),new ScreenRect(0,-1000,0,1000),new ScreenRect(-100000,-100000,100000,100000)};
        var axes=new[]{(32f,16f,-32f,16f),(-32f,16f,-32f,-16f),(-32f,-16f,32f,-16f),(32f,-16f,32f,16f)};
        foreach(var rect in rectangles) foreach(var axis in axes)
            for(int sample=0;sample<257;sample++)
            {
                float cx=(sample*73%401)-200,cy=(sample*97%401)-200;
                float height=(sample%7-3)*32f;
                var quad=Make(cx,cy+height,axis.Item1,axis.Item2,axis.Item3,axis.Item4);
                check(rect.IntersectsQuad(quad.A,quad.B,quad.C,quad.D)==FullSat(rect,quad),"contained-quad optimization preserves full SAT for rotated/boundary/height geometry");
            }
        var diamond=new Quad(new ScreenPoint(0,1),new ScreenPoint(1,0),new ScreenPoint(0,-1),new ScreenPoint(-1,0));
        foreach(var rect in rectangles)
            check(rect.IntersectsQuad(diamond.A,diamond.B,diamond.C,diamond.D)==FullSat(rect,diamond),"reference agrees at corner/crossing/zero-width boundaries");
        foreach(var quad in new[]{Make(0,0,0,0,0,0),Make(0,0,0,1,0,1),Make(0,0,float.NaN,1,1,0),Make(float.PositiveInfinity,0,1,0,0,1)})
            check(rectangles[0].IntersectsQuad(quad.A,quad.B,quad.C,quad.D)==FullSat(rectangles[0],quad),"fast path preserves degenerate and nonfinite behavior");
        Benchmark(100);Benchmark(500);Benchmark(1000);BenchmarkRenderReuse(100);BenchmarkRenderReuse(500);BenchmarkRenderReuse(1000);
    }

    private static void Benchmark(int buildings)
    {
        var quads=new Quad[buildings*25];
        for(int building=0;building<buildings;building++) for(int tile=0;tile<25;tile++)
        {
            float x=building%32*6+tile%5,y=building/32*6+tile/5;
            quads[building*25+tile]=Make((x-y)*32,(x+y)*16,32,16,-32,16);
        }
        var rect=new ScreenRect(-100000,-100000,100000,100000);
        Run(rect,quads,8,false);Run(rect,quads,8,true);
        int passes=Math.Max(20,1000000/quads.Length);
        long prior=long.MaxValue,current=long.MaxValue;
        int priorHits=0,currentHits=0;
        for(int round=0;round<3;round++)
        {
            var watch=Stopwatch.StartNew();priorHits=Run(rect,quads,passes,false);watch.Stop();prior=Math.Min(prior,watch.ElapsedTicks);
            watch.Restart();currentHits=Run(rect,quads,passes,true);watch.Stop();current=Math.Min(current,watch.ElapsedTicks);
        }
        if(priorHits!=currentHits)throw new Exception("Benchmark changed selection results.");
        double calls=(double)quads.Length*passes;
        Console.WriteLine("BENCH managed SAT only: "+buildings+" buildings x25 tiles, "+calls.ToString("0")+" calls; prior="+(prior*1e9/Stopwatch.Frequency/calls).ToString("F1")+"ns/call, current="+(current*1e9/Stopwatch.Frequency/calls).ToString("F1")+"ns/call. No in-game/FPS claim.");
    }

    private static int Run(ScreenRect rect,Quad[] quads,int passes,bool optimized)
    {
        int hits=0;
        if(optimized)
        {
            for(int p=0;p<passes;p++) for(int i=0;i<quads.Length;i++)
            {var q=quads[i];if(rect.IntersectsQuad(q.A,q.B,q.C,q.D))hits++;}
        }
        else
        {
            for(int p=0;p<passes;p++) for(int i=0;i<quads.Length;i++)if(FullSat(rect,quads[i]))hits++;
        }
        return hits;
    }

    private static void BenchmarkRenderReuse(int buildings)
    {
        var original=new DemolitionTarget[buildings];var quoted=new DemolitionTarget[buildings];
        for(int i=0;i<buildings;i++)
        {
            var target=new DemolitionTarget {Kind=TargetKind.Building,Group=TargetGroup.Buildings,Slot=i+1,GlobalId=(uint)i+1,Owner=1,Type=4,X=i%32,Y=i/32,OccupiedTiles=new int[25]};
            for(int cell=0;cell<25;cell++)target.OccupiedTiles[cell]=i*25+cell;
            original[i]=target;quoted[i]=target.Copy();quoted[i].GlobalId++;
        }
        int iterations=Math.Max(100,10000000/(buildings*25));
        for(int i=0;i<32;i++)if(!RenderSelectionGeometry.Equals(original,quoted))throw new Exception("Render reuse warmup changed geometry.");
        long best=long.MaxValue;
        for(int round=0;round<3;round++)
        {
            int matches=0;var watch=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)if(RenderSelectionGeometry.Equals(original,quoted))matches++;
            watch.Stop();if(matches!=iterations)throw new Exception("Unchanged receipt did not reuse render geometry.");
            best=Math.Min(best,watch.ElapsedTicks);
        }
        Console.WriteLine("BENCH managed render-geometry reuse only: "+buildings+" buildings x25 tiles, "+iterations+" quotes; "+(best*1e6/Stopwatch.Frequency/iterations).ToString("F2")+"us/quote. Excludes native selection and Unity rendering.");
    }

    private static Quad Make(float cx,float cy,float ux,float uy,float vx,float vy)=>new Quad(
        new ScreenPoint(cx-(ux+vx)*.5f,cy-(uy+vy)*.5f),new ScreenPoint(cx+(ux-vx)*.5f,cy+(uy-vy)*.5f),
        new ScreenPoint(cx+(ux+vx)*.5f,cy+(uy+vy)*.5f),new ScreenPoint(cx+(-ux+vx)*.5f,cy+(-uy+vy)*.5f));

    private readonly struct Quad
    {
        internal readonly ScreenPoint A,B,C,D;
        internal Quad(ScreenPoint a,ScreenPoint b,ScreenPoint c,ScreenPoint d){A=a;B=b;C=c;D=d;}
    }

    private static bool FullSat(ScreenRect rect,Quad q)
    {
        var a=q.A;var b=q.B;var c=q.C;var d=q.D;
        if(!rect.IsFinite || !a.IsFinite || !b.IsFinite || !c.IsFinite || !d.IsFinite)return false;
        if(Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X))<rect.MinX ||
            Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X))>rect.MaxX ||
            Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y))<rect.MinY ||
            Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y))>rect.MaxY)return false;
        return !Separates(rect,a,b,q) && !Separates(rect,b,c,q) && !Separates(rect,c,d,q) && !Separates(rect,d,a,q);
    }

    private static bool Separates(ScreenRect rect,ScreenPoint first,ScreenPoint second,Quad q)
    {
        double nx=-(double)second.Y+first.Y,ny=(double)second.X-first.X;
        if(nx==0 && ny==0)return false;
        double pa=nx*q.A.X+ny*q.A.Y,pb=nx*q.B.X+ny*q.B.Y,pc=nx*q.C.X+ny*q.C.Y,pd=nx*q.D.X+ny*q.D.Y;
        double qMin=Math.Min(Math.Min(pa,pb),Math.Min(pc,pd)),qMax=Math.Max(Math.Max(pa,pb),Math.Max(pc,pd));
        double rMin=nx*(nx<0?rect.MaxX:rect.MinX)+ny*(ny<0?rect.MaxY:rect.MinY);
        double rMax=nx*(nx<0?rect.MinX:rect.MaxX)+ny*(ny<0?rect.MinY:rect.MaxY);
        return qMax<rMin || qMin>rMax;
    }
}
