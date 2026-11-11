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
        g.Begin(1,2,10,10,plain);g.Move(1,2,13,10,1);Check(g.Release(false)==GestureRelease.Single,"small movement remains single");
        g.Begin(1,2,10,10,plain);g.Move(5,4,14,10,1);Check(g.Stage==GestureStage.Dragging,"four pixel drag");
        g.Move(1,2,10,10,1);Check(g.Stage==GestureStage.Dragging,"return does not become a single deletion");
        Check(g.Release(false)==GestureRelease.Area,"release area");
        g.Begin(0,0,0,0,plain);g.Cancel();Check(g.Owned,"cancel owns remaining input");Check(g.Release(false)==GestureRelease.Cancelled,"cancel has no deletion");
        g.Begin(0,0,0,0,plain);Check(g.Release(true)==GestureRelease.Cancelled,"release over UI cancels");
        var same=new SelectionSpec(0,true,Target(TargetGroup.PositiveFear));
        Check(same.Matches(Target(TargetGroup.PositiveFear,99)),"positive fear crosses variants");
        Check(!same.Matches(Target(TargetGroup.NegativeFear)),"fear signs stay separate");
        Check(!new SelectionSpec(255,true,null).Matches(Target(TargetGroup.Buildings)),"shift on empty never broadens");
        Check(new SelectionSpec(0,true,Target(TargetGroup.Buildings,2)).Matches(Target(TargetGroup.Buildings,2)),"same type ignores disabled category");
        Check(!new SelectionSpec(0,true,Target(TargetGroup.Buildings,2)).Matches(Target(TargetGroup.Buildings,3)),"normal types stay distinct");
        Check(plain.Matches(Target(TargetGroup.Buildings))&&!plain.Matches(Target(TargetGroup.Fortifications)),"local category filter");
        Check(new AreaRect(3,3,5,5).Intersects(new AreaRect(1,1,3,3)),"boundary tile overlap counts");
        Check(!new AreaRect(4,4,5,5).Intersects(new AreaRect(1,1,3,3)),"disjoint footprints excluded");
        var a=Target(TargetGroup.Buildings,1,2);var b=Target(TargetGroup.Buildings,1,1);
        Check(DemolitionPolicy.Canonicalize(new[]{a,b,a}).Length==2,"identities deduplicated");
        Check(DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{a,b}))==DemolitionPolicy.TargetDigest(DemolitionPolicy.Canonicalize(new[]{b,a})),"canonical packet order");
        Console.WriteLine("PASS "+checks+" policy behavior checks");
    }
}
