using System;
using System.Collections.Generic;
using TrappedUnitRescueTest;

namespace TrappedUnitRescueTest.Tests
{
    internal static class RescuePolicyFixture
    {
        private static readonly int[] Dx={0,1,1,1,0,-1,-1,-1}, Dy={-1,-1,0,1,1,1,0,-1};
        private static int checks;
        private static void Require(bool condition,string message)
        { checks++;if(!condition)throw new InvalidOperationException("RescuePolicyFixture: "+message); }
        internal static void Run()
        {
            // The test drives the actual production BFS and selector. Ground/edges are external boundaries.
            Func<int,int,int> tile=(x,y)=>x>=0 && x<25 && y>=0 && y<25?y*25+x:-1;
            Func<int,bool> ground=id=>id%25!=13; // Existing continuous wall cannot be crossed or used as a destination.
            Func<int,int,int,int,bool> edge=(id,x,y,d)=>ground(id) && tile(x+Dx[d],y+Dy[d])>=0 && ground(tile(x+Dx[d],y+Dy[d]));
            var found=RescueRuntime.FindLocalCandidates(12,12,tile,ground,edge);
            Require(found.Count>0,"reachable side remains available");
            var seen=new HashSet<int>();int lastDistance=0;
            foreach(var candidate in found)
            {
                Require(candidate.X<13,"unchanged wall never crossed");
                Require(Math.Max(Math.Abs(candidate.X-12),Math.Abs(candidate.Y-12))<=8,"radius is bounded");
                Require(candidate.Distance>=lastDistance,"shortest old local distance first");lastDistance=candidate.Distance;
                Require(seen.Add(candidate.Tile),"each old reachable tile appears once");
            }
            Require(!seen.Contains(tile(12,12)),"original cell never a relocation candidate");
            Require(!RescueRuntime.ChooseCandidate(found,c=>false).HasValue,"all blocked after construction produces no target");
            Require(RescueRuntime.ChooseCandidate(found,c=>true).Value.Tile==found[0].Tile,"deterministic first shortest target");
            int blocked=found[0].Tile;
            Require(RescueRuntime.ChooseCandidate(found,c=>c.Tile!=blocked).Value.Tile==found[1].Tile,"newly occupied first target is skipped");
            Require(RescueRuntime.FindLocalCandidates(13,12,tile,ground,edge).Count==0,"unpassable source cannot establish old reachability");
            Require(RescueRuntime.FindLocalCandidates(-1,12,tile,ground,edge).Count==0,"invalid source remains empty");
            Require(RescueRuntime.FindLocalCandidates(12,12,tile,ground,(id,x,y,d)=>false).Count==0,"old enclosure creates no rescue candidates");
            // A one-way old passage must follow its supplied edge relation; no reverse edge is invented by BFS.
            var directed=RescueRuntime.FindLocalCandidates(4,4,tile,id=>true,(id,x,y,d)=>d==2);
            Require(directed.Count==8,"one-way corridor limited to eight steps");
            foreach(var candidate in directed)Require(candidate.Y==4 && candidate.X>4,"directed field has no invented exits");
            Require(!RescueRuntime.ChooseCandidate(new List<RescueCandidate>(),c=>true).HasValue,"empty old field cannot authorize movement");
            Console.WriteLine("PASS RescuePolicyFixture "+checks+" checks");
        }
    }
}
