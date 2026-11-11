using System;
using System.Diagnostics;
using AreaDemolitionTest;

internal static class BodySelectionChecks
{
    internal static void Verify(Action<bool,string> check)
    {
        check(VanillaHitGeometry.MaximumRise(2,1,200,1)==192,"building height takes precedence over sprite height in Vanilla body rise");
        check(VanillaHitGeometry.MaximumRise(0,0,64,2)==64,"sprite overhang uses Vanilla32pixel subtraction and zoom");
        check(VanillaHitGeometry.MaximumRise(0,0,16,1)==0 && VanillaHitGeometry.MaximumRise(-1,0,16,1)==0,"short sprites and negative total heights do not extrude downward");
        check(VanillaHitGeometry.MaximumRise(100,100,100,3)==800,"body rise respects Vanilla800pixel probe limit");
        check(VanillaHitGeometry.MaximumRise(.11334002017974854f,2.230409860610962f,0,.800000011920929f)>=120,"float Vanilla height accepts probe120 even when ideal double multiplication falls below it");
        bool invalid=false;try{VanillaHitGeometry.MaximumRise(float.NaN,0,0,1);}catch(ArgumentException){invalid=true;}
        check(invalid,"invalid body height fails closed");
        ProbeOracle(check);FractionalProbeCoverage(check);ProbeThresholds(check);RootMembership(check);
        Benchmark(100);Benchmark(500);Benchmark(1000);
    }

    private static ScreenHitHull Hull(float x,float y,float rise=0,float width=1,float height=1)=>new ScreenHitHull(
        new ScreenPoint(x,y+height),new ScreenPoint(x+width,y),new ScreenPoint(x,y-height),new ScreenPoint(x-width,y),rise);

    private static void ProbeOracle(Action<bool,string> check)
    {
        // Reference the original full SAT for each of Vanilla's401 two-pixel depth probes.
        // These32pixel-deep quads overlap adjacent probes, so their union equals the hull.
        var axes=new[]{(32f,16f,-32f,16f),(-32f,16f,-32f,-16f),(-32f,-16f,32f,-16f),(32f,-16f,32f,16f)};
        foreach(var axis in axes)
        {
            float ux=axis.Item1,uy=axis.Item2,vx=axis.Item3,vy=axis.Item4;
            var a=new ScreenPoint(-(ux+vx)*.5f,-(uy+vy)*.5f);var b=new ScreenPoint((ux-vx)*.5f,(uy-vy)*.5f);
            var c=new ScreenPoint((ux+vx)*.5f,(uy+vy)*.5f);var d=new ScreenPoint((-ux+vx)*.5f,(-uy+vy)*.5f);
            var hull=new ScreenHitHull(a,b,c,d,800);
            for(int x=-36;x<=36;x+=12)for(int y=-18;y<=822;y+=12)
            {
                var point=new ScreenRect(x,y,x,y);bool sampled=false;
                for(int rise=0;rise<=800 && !sampled;rise+=2)
                    sampled=ScreenGeometryReference.IntersectsQuadReference(point,new ScreenPoint(a.X,a.Y+rise),new ScreenPoint(b.X,b.Y+rise),new ScreenPoint(c.X,c.Y+rise),new ScreenPoint(d.X,d.Y+rise));
                check(hull.Intersects(point)==sampled,"body hull agrees with401depth probes for point coverage across four rotations");
            }
        }
        var body=Hull(0,0,10);
        check(!body.Intersects(new ScreenRect(.8f,10.8f,1,11)),"body hull excludes empty AABB corner above a sloped edge");
        check(body.Intersects(new ScreenRect(-2,4,2,4.1f)),"body hull catches crossing strips without contained vertices");
        check(body.Intersects(new ScreenRect(1,5,2,6)),"body hull includes tangent side contact");
        check(body.Intersects(new ScreenRect(-.1f,5,.1f,6)),"rectangle entirely inside extruded body is selected");
        check(!Hull(0,0).Intersects(new ScreenRect(-.1f,5,.1f,6)),"body-only hit is distinct from a ground-footprint hit");
    }

    private static DemolitionTarget Root(int slot)=>new DemolitionTarget {Kind=TargetKind.Building,Group=TargetGroup.Buildings,Slot=slot,GlobalId=(uint)slot,Owner=1,Type=4,TileId=slot};
    private static void FractionalProbeCoverage(Action<bool,string> check)
    {
        foreach(float zoom in new[]{.125f,.33333334f,.75f,1.125f,3.7f})
        foreach(float testHeight in new[]{0f,.03125f,.125f,1.5f})
        foreach(float buildingHeight in new[]{0f,.03125f,.125f})
        foreach(float spriteHeight in new[]{16f,63f,128f})
        {
            float rise=VanillaHitGeometry.MaximumRise(testHeight,buildingHeight,spriteHeight,zoom);
            var a=new ScreenPoint(.12345f,-.45678f+16*zoom);var b=new ScreenPoint(.12345f+32*zoom,-.45678f);
            var c=new ScreenPoint(.12345f,-.45678f-16*zoom);var d=new ScreenPoint(.12345f-32*zoom,-.45678f);
            var hull=new ScreenHitHull(a,b,c,d,rise);
            float extra=buildingHeight!=0?buildingHeight*64f:Math.Max(0,spriteHeight-32f);
            float combined=testHeight*64f+extra;
            bool covered=true;string failure="";
            for(int probe=0;probe<=800;probe+=2)
            {
                if(-probe/zoom+combined<0)continue;
                foreach(var corner in new[]{a,b,c,d})
                {
                    var point=new ScreenRect(corner.X,corner.Y+probe,corner.X,corner.Y+probe);
                    bool sampled=ScreenGeometryReference.IntersectsQuadReference(point,new ScreenPoint(a.X,a.Y+probe),new ScreenPoint(b.X,b.Y+probe),new ScreenPoint(c.X,c.Y+probe),new ScreenPoint(d.X,d.Y+probe));
                    if(sampled && !hull.Intersects(point))
                    {covered=false;failure=" zoom="+zoom+" test="+testHeight+" building="+buildingHeight+" sprite="+spriteHeight+" probe="+probe+" rise="+rise+" x="+corner.X+" y="+(corner.Y+probe);break;}
                }
                if(!covered)break;
            }
            check(covered,"fractional height/zoom hull includes every accepted401probe corner"+failure);
        }
    }
    private static void ProbeThresholds(Action<bool,string> check)
    {
        foreach(float zoom in new[]{.125f,.8f,1.3333334f,3.7f})foreach(int threshold in new[]{2,4,10,32,120,400,800})
        {
            float height=threshold/zoom;
            foreach(float sample in new[]{MathF.BitDecrement(height),height,MathF.BitIncrement(height)})
            {
                float rise=VanillaHitGeometry.MaximumRise(0,sample/64f,0,zoom);int lastAccepted=0;
                for(int probe=0;probe<=800;probe+=2)if(-probe/zoom+sample>=0)lastAccepted=probe;
                check(rise>=lastAccepted && rise<=800,"generated adjacent-float two-pixel thresholds retain every accepted Vanilla probe");
            }
        }
    }
    private static void RootMembership(Action<bool,string> check)
    {
        var a=Root(1);var b=Root(2);var shared=new DemolitionTarget {Kind=TargetKind.Decoration,Group=TargetGroup.Decorations,Slot=25,GlobalId=99,Owner=1,Type=10,TileId=50};
        var geometry=new ScreenHitGeometry(new[]{a,b},new[]{new[]{Hull(0,0)},new[]{Hull(100,0)}});
        var results=new[]{new PreviewRootResult {Root=a,Closure=new[]{a,shared}},new PreviewRootResult {Root=b,Closure=new[]{b,shared}}};
        var large=new ScreenRect(-2,-2,102,2);var small=new ScreenRect(99,-2,102,2);var gap=new ScreenRect(49,-2,51,2);
        var selection=new PreviewRootSelection();selection.Replace(geometry,results);
        check(selection.Refresh(large) && selection.Targets.Length==3,"large selection unions overlapping approved root closures once");
        check(selection.Refresh(small) && selection.Targets.Length==2 && ReferenceEquals(selection.Targets[0],b),"shrinking drops the outside root while retaining its shared dependent for the remaining root");
        var retained=selection.Targets;
        check(!selection.Refresh(small) && ReferenceEquals(retained,selection.Targets),"unchanged root membership retains the exact immutable target array");
        for(int i=0;i<10;i++)selection.Refresh(small);
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++)selection.Refresh(small);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        check(allocated==0,"unchanged frame refresh performs no target cloning or managed allocation");
        check(selection.Refresh(gap) && selection.Targets.Length==0,"shrinking to a gap removes complete closures rather than leaving orphan decoration");
        selection.Replace(geometry,results);selection.Refresh(gap);
        check(selection.Targets.Length==0,"late completed large quote is clipped against current small area before restoring preview membership");
        selection.Clear();check(!selection.Refresh(large) && selection.Targets.Length==0,"cancel clears geometry and cached closures");
        var progress=new System.Collections.Generic.List<PreviewRootResult> {results[0]};
        selection.UpdateProgress(geometry,new[]{a,b},progress.Count,progress);selection.Refresh(large);
        check(selection.Targets.Length==2 && ReferenceEquals(selection.Targets[0],a),"first approved root is visible before the rest of the quote completes");
        progress.Add(results[1]);selection.UpdateProgress(geometry,new[]{a,b},progress.Count,progress);selection.Refresh(small);
        check(selection.Targets.Length==2 && ReferenceEquals(selection.Targets[0],b),"new root approval is clipped to the latest smaller rectangle including shared dependents");
        selection.UpdateProgress(geometry,new[]{a,b},progress.Count,progress);var unchanged=selection.Targets;
        check(!selection.Refresh(small) && ReferenceEquals(unchanged,selection.Targets),"republishing the same completed prefix does not rebuild target membership");
        selection.Refresh(gap);selection.UpdateProgress(geometry,new[]{a,b},progress.Count,progress);selection.Refresh(gap);
        check(selection.Targets.Length==0,"late progress cannot restore outside roots after shrinking to a gap");
        var newGeometry=new ScreenHitGeometry(new[]{b},new[]{new[]{Hull(100,0)}});
        selection.UpdateProgress(newGeometry,new[]{b},1,new[]{results[1]});selection.Refresh(large);
        check(selection.Targets.Length==2 && ReferenceEquals(selection.Targets[0],b),"new quote geometry replaces earlier progress rather than carrying old roots forward");
        selection.Replace(geometry,results);selection.Refresh(large);
        var nextGeometry=new ScreenHitGeometry(new[]{a,b},new[]{new[]{Hull(0,0)},new[]{Hull(100,0)}});
        selection.UpdateProgress(nextGeometry,new[]{a,b},1,new[]{results[0]});selection.Refresh(large);
        check(selection.Targets.Length==3,"a fresh quote prefix retains previously approved unchecked roots without whole-preview flicker");
        selection.UpdateProgress(nextGeometry,new[]{a,b},2,new[]{results[0]});selection.Refresh(large);
        check(selection.Targets.Length==2 && ReferenceEquals(selection.Targets[0],a),"a newly rejected root immediately revokes its preceding quote's dependent closure");
        var rejectedGeometry=new ScreenHitGeometry(new[]{a},new[]{new[]{Hull(0,0)}});
        selection.UpdateProgress(rejectedGeometry,new[]{a},1,Array.Empty<PreviewRootResult>());selection.Refresh(large);
        check(selection.Targets.Length==0,"a quote rejecting every root does not retain old red targets");
        var merged=new ScreenHitGeometry(new[]{a,a},new[]{new[]{Hull(0,0)},new[]{Hull(100,0)}});
        check(merged.SelectRoots(large).Length==1 && merged.Intersects(a.Copy(),small),"duplicate request parts union under one stable root identity");
        check(!merged.Intersects(a,gap),"root union does not fill the gap between separate parts");
        var changed=a.Copy();changed.GlobalId++;
        check(!merged.Intersects(changed,large),"a reused slot with a different generation cannot borrow old hit geometry");
        check(new ScreenHitGeometry(new[]{a},new[]{new[]{Hull(0,0,20)}}).Intersects(a,new ScreenRect(-.1f,15,.1f,16)),"any intersecting body part makes its root selectable");
    }

    private static void Benchmark(int count)
    {
        var roots=new DemolitionTarget[count];var hulls=new ScreenHitHull[count][];var approved=new PreviewRootResult[count];
        for(int i=0;i<count;i++)
        {
            roots[i]=Root(i+1);hulls[i]=new ScreenHitHull[25];
            for(int tile=0;tile<25;tile++)hulls[i][tile]=Hull(i%32*80+(tile%5-tile/5)*8,i/32*80+(tile%5+tile/5)*4,64,8,4);
            approved[i]=new PreviewRootResult {Root=roots[i],Closure=new[]{roots[i]}};
        }
        var geometry=new ScreenHitGeometry(roots,hulls);var area=new ScreenRect(0,0,512,512);
        var selection=new PreviewRootSelection();selection.Replace(geometry,approved);selection.Refresh(area);
        int iterations=Math.Max(200,1000000/count),hits=0;long queryBest=long.MaxValue,refreshBest=long.MaxValue;
        for(int i=0;i<16;i++){geometry.SelectRoots(area);selection.Refresh(area);}
        for(int round=0;round<3;round++)
        {
            var watch=Stopwatch.StartNew();
            for(int i=0;i<iterations;i++)hits+=geometry.SelectRoots(area).Length;
            watch.Stop();queryBest=Math.Min(queryBest,watch.ElapsedTicks);
            watch.Restart();for(int i=0;i<iterations;i++)if(selection.Refresh(area))throw new Exception("Unchanged benchmark root set changed.");
            watch.Stop();refreshBest=Math.Min(refreshBest,watch.ElapsedTicks);
        }
        if(hits==0)throw new Exception("Body benchmark did not select any root.");
        Console.WriteLine("BENCH managed body geometry: "+count+" roots x25 hulls; select="+(queryBest*1e6/Stopwatch.Frequency/iterations).ToString("F2")+"us/query, unchanged preview="+(refreshBest*1e6/Stopwatch.Frequency/iterations).ToString("F2")+"us/frame. Excludes native and Unity costs.");
        var sources=new SourceCell[count][];
        for(int i=0;i<count;i++)
        {
            sources[i]=new SourceCell[25];
            for(int tile=0;tile<25;tile++)
            {
                float x=i%32*80+(tile%5-tile/5)*8,y=i/32*80+(tile%5+tile/5)*4;
                sources[i][tile]=new SourceCell {A=new ScreenPoint(x,y+4),B=new ScreenPoint(x+8,y),C=new ScreenPoint(x,y-4),D=new ScreenPoint(x-8,y),
                    TestHeight=tile%4/32f,BuildingHeight=tile%3/8f,SpriteHeight=64+tile,Zoom=1.125f};
            }
        }
        int builds=Math.Max(20,10000000/(count*25));
        MeasureConstruction(count,"index only (prebuilt hull arrays)",builds,()=>new ScreenHitGeometry(roots,hulls));
        MeasureConstruction(count,"hulls+index (prebuilt cell sources)",builds,()=>BuildFromSources(roots,sources));
    }

    private struct SourceCell
    {
        internal ScreenPoint A,B,C,D;
        internal float TestHeight,BuildingHeight,SpriteHeight,Zoom;
    }
    private static ScreenHitGeometry BuildFromSources(DemolitionTarget[] roots,SourceCell[][] sources)
    {
        var hulls=new ScreenHitHull[roots.Length][];
        for(int i=0;i<roots.Length;i++)
        {
            hulls[i]=new ScreenHitHull[sources[i].Length];
            for(int cell=0;cell<sources[i].Length;cell++)
            {
                var s=sources[i][cell];float rise=VanillaHitGeometry.MaximumRise(s.TestHeight,s.BuildingHeight,s.SpriteHeight,s.Zoom);
                hulls[i][cell]=new ScreenHitHull(s.A,s.B,s.C,s.D,rise);
            }
        }
        return new ScreenHitGeometry(roots,hulls);
    }
    private static void MeasureConstruction(int count,string name,int iterations,Func<ScreenHitGeometry> build)
    {
        for(int i=0;i<8;i++)GC.KeepAlive(build());
        long best=long.MaxValue,bestBytes=0;double bestCpuMs=0;
        using(var process=Process.GetCurrentProcess())
        {
            for(int round=0;round<3;round++)
            {
                TimeSpan cpu=process.TotalProcessorTime;var watch=new Stopwatch();
                long allocated=GC.GetAllocatedBytesForCurrentThread();watch.Start();
                for(int i=0;i<iterations;i++)GC.KeepAlive(build());
                watch.Stop();long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
                double cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;
                if(watch.ElapsedTicks<best){best=watch.ElapsedTicks;bestBytes=bytes;bestCpuMs=cpuMs;}
            }
        }
        Console.WriteLine("BENCH managed geometry construction: "+count+" roots x25 cells; "+name+"; wall="+(best*1e6/Stopwatch.Frequency/iterations).ToString("F2")+
            "us/build, processCPU="+(bestCpuMs*1000/iterations).ToString("F2")+"us/build, allocated="+((double)bestBytes/iterations).ToString("F0")+
            "B/build over "+iterations+" builds. Excludes Unity projection and native costs; processCPU includes runtime/GC threads.");
    }
}
