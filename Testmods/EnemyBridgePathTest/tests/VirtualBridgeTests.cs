using System;
using System.Collections.Generic;
using System.Diagnostics;
namespace EnemyBridgePathTest
{
    internal static class VirtualBridgeTests
    {
        private static int checks;
        private static void Check(bool value,string message) {checks++;if(!value)throw new Exception("Virtual topology: "+message);}
        private static VirtualBridgeMap Map(int width,int height,bool alternate,VirtualConnection[] connections=null)
        {
            int count=width*height;var pcl=new ushort[count];var edges=new byte[count];var flags=new int[count];var x=new ushort[count];var y=new ushort[count];var rows=new int[height];
            for(int row=0;row<height;row++)
            {
                rows[row]=row*width;
                for(int column=0;column<width;column++)
                {
                    int tile=rows[row]+column;pcl[tile]=1;x[tile]=(ushort)column;y[tile]=(ushort)row;
                    if(column+1<width)edges[tile]|=4;if(column>0)edges[tile]|=64;
                    if(alternate) {if(row+1<height)edges[tile]|=16;if(row>0)edges[tile]|=1;}
                }
            }
            return new VirtualBridgeMap(1,1,pcl,edges,flags,x,y,rows,connections??Array.Empty<VirtualConnection>(),true);
        }
        private static VirtualBridgeQuery Run(VirtualBridgeMap map,int source,int target,int mode,int[] deck,bool authorized=true,Func<int,int,bool> allowed=null)
        {
            var query=new VirtualBridgeQuery(map,source,target,mode,deck,authorized,allowed);int steps=0;
            while(!query.Complete) {query.Step(2);Check(++steps<map.Components.Length*4+20,"bounded traversal");}
            return query;
        }
        private static void TestGateEndpoints()
        {
            foreach(int kind in new[]{3,4})foreach(int orientation in new[]{0,2})
            {
                var map=Map(16,16,true);int size=kind==3?7:5,ox=3,oy=4,mid=size/2;
                int entry=orientation==0?map.Tile(ox+mid,oy+size):map.Tile(ox+size,oy+mid);
                int exit=orientation==0?map.Tile(ox+mid,oy-1):map.Tile(ox-1,oy+mid);
                int expected=map.Tile(ox+1,oy+1);map.Components[expected]=3;
                Check(VirtualGateEndpoint.TryResolve(map,kind,entry,exit,3,out int tile)&&tile==expected,"native C from class/oriented endpoints");
                Check(!VirtualGateEndpoint.TryResolve(map,kind,exit,entry,3,out _),"swapped endpoints cannot invent orientation");
                Check(!VirtualGateEndpoint.TryResolve(map,kind,entry,exit,2,out _),"stale C component fails closed");
                Check(!VirtualGateEndpoint.TryResolve(map,5,entry,exit,3,out _),"unconfirmed class5 is unresolved");
                Check(!VirtualGateEndpoint.TryResolve(map,kind,-1,exit,3,out _),"invalid endpoint fails closed");
                Check(!VirtualGateEndpoint.TryResolve(map,kind,entry,exit,0,out _),"absent C is not a physical anchor");
                map.X[expected]++;Check(!VirtualGateEndpoint.TryResolve(map,kind,entry,exit,3,out _),"packed coordinate mismatch fails closed");
            }
        }
        private static void TestNativeCoupling()
        {
            var candidates=APIShared.GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(10,20,5);
            Check(candidates.Count==20&&candidates[0].X==12&&candidates[0].Y==19,"native perimeter begins at north midpoint");
            int reads=0;var ids=APIShared.GatehouseDrawbridgeCoupling.CollectFirstDistinctBuildingIds(candidates,
                (x,y)=>{reads++;return reads==1?0:reads<4?703:reads==4?720:828;},id=>id==703||id==720||id==828);
            Check(ids.Count==2&&ids[0]==703&&ids[1]==720&&reads==4,"first two distinct live bridges; no owner filter or ID0 lookup");
            ids=APIShared.GatehouseDrawbridgeCoupling.CollectFirstDistinctBuildingIds(candidates,(x,y)=>703,id=>false);
            Check(ids.Count==0,"inactive identities cannot couple");
        }
        internal static int Run()
        {
            checks=0;TestGateEndpoints();TestNativeCoupling();var line=Map(5,1,false);
            Check(Run(line,0,4,0,new[]{2}).Result==VirtualReachability.NoRoute,"equal native PCL splits at enemy deck");
            Check(Run(line,0,4,1,Array.Empty<int>()).Result==VirtualReachability.Reachable,"owner/allied/capturer no cut");
            Check(Run(Map(5,2,true),0,4,0,new[]{2}).Result==VirtualReachability.Reachable,"alternate terrain route survives");
            Check(Run(line,0,4,0,new[]{2},false).Result==VirtualReachability.Unknown,"geometric parent cannot authorize negative");
            Check(Run(line,0,4,0,Array.Empty<int>(),false).Result==VirtualReachability.Unknown,"unknown roles remain unknown with witness");
            Check(Run(line,0,4,0,new[]{1,3}).Result==VirtualReachability.NoRoute,"multiple bridges");
            Check(Run(line,0,4,0,new[]{2},true,(tile,direction)=>false).Result==VirtualReachability.NoRoute,"immutable gate edge mask composed");
            var diagonal=new VirtualBridgeMap(2,3,new ushort[]{1,1},new byte[]{8,128},new int[2],new ushort[]{0,1},new ushort[]{0,1},new int[]{0,0},Array.Empty<VirtualConnection>(),true);
            Check(Run(diagonal,0,1,0,Array.Empty<int>()).Result==VirtualReachability.Reachable,"packed diagonal follows coordinate view");
            Check(Run(diagonal,0,1,0,new[]{1}).Result==VirtualReachability.Unknown,"blocked target is not a seed");
            var oneWay=new VirtualBridgeMap(1,1,new ushort[]{1,1},new byte[]{4,0},new int[2],new ushort[]{0,1},new ushort[]{0,0},new int[]{0},Array.Empty<VirtualConnection>(),true);
            Check(Run(oneWay,1,0,0,Array.Empty<int>()).Result==VirtualReachability.NoRoute,"directed edge never converted to undirected component");
            Check(Run(oneWay,0,1,0,Array.Empty<int>()).Result==VirtualReachability.Reachable,"directed witness");
            foreach(int kind in new[]{1,2})foreach(int mode in new[]{0,1})foreach(bool enabled in new[]{false,true})foreach(bool permitted in new[]{false,true})
            {
                var record=new VirtualConnection(0,4,-1,kind,true,enabled,permitted,true);
                var result=Run(line.WithConnections(new[]{record}),0,4,mode,new[]{2});
                bool reachable=enabled&&permitted&&(kind!=1||mode==1);
                Check(result.Result==(reachable?VirtualReachability.Reachable:VirtualReachability.NoRoute),"native macro eligibility modes/open/access");
                Check(!reachable||result.StructureRequired==(kind==1),"class1 is second-pass structure-required");
            }
            Check(Run(line.WithConnections(new[]{new VirtualConnection(0,1,4,2,true,true,true,true)}),0,4,0,new[]{2}).Result==VirtualReachability.Reachable,"third endpoint remapped to actual tile");
            Check(Run(line.WithConnections(new[]{new VirtualConnection(0,1,-1,2,true,true,true,false)}),0,4,0,new[]{2}).Result==VirtualReachability.Unknown,"unresolved third endpoint cannot prove no route");
            foreach(int mode in new[]{0,1})foreach(bool reversed in new[]{false,true})
            {
                var partial=line.WithConnections(new[]{new VirtualConnection(0,4,-1,2,true,true,true,true,true)});
                Check(Run(partial,reversed?4:0,reversed?0:4,mode,new[]{2}).Result==VirtualReachability.Reachable,"known A/B direct transition independent of C and direction");
            }
            var work=new VirtualBridgeWorkspace(line.Components.Length);
            foreach(var cut in new[]{new[]{2},Array.Empty<int>(),new[]{2}})
            {
                var reused=new VirtualBridgeQuery(line,0,4,0,cut,true,workspace:work);while(!reused.Complete)reused.Step(64);
                Check(reused.Result==(cut.Length==0?VirtualReachability.Reachable:VirtualReachability.NoRoute),"workspace clears cut and visited state between variants");
                Check(cut.Length==0||reused.Expanded==2,"equivalent second macro pass is skipped when no known class1 edges exist");
            }
            var components=new ushort[]{1,1};var edgeCopy=new byte[]{4,64};var map=new VirtualBridgeMap(1,8,components,edgeCopy,new int[2],new ushort[]{0,1},new ushort[2],new int[1],Array.Empty<VirtualConnection>(),true);
            components[0]=0;edgeCopy[0]=0;
            Check(Run(map,0,1,0,Array.Empty<int>()).Result==VirtualReachability.Reachable,"copied inputs immune to ID/map replacement writes");
            Check(new VirtualBridgeQuery(map,0,1,8,Array.Empty<int>(),true).Result==VirtualReachability.Unknown,"unknown route mode");
            var incomplete=new VirtualBridgeMap(2,9,new ushort[]{1},new byte[1],new int[1],new ushort[1],new ushort[1],new int[1],Array.Empty<VirtualConnection>(),false);
            Check(new VirtualBridgeQuery(incomplete,0,0,0,Array.Empty<int>(),true).Result==VirtualReachability.Unknown,"incomplete map never negative");
            var negativeUnverified=new VirtualBridgeQuery(line,0,4,0,new[]{2},true,negativeProofComplete:false);while(!negativeUnverified.Complete)negativeUnverified.Step(8);
            Check(negativeUnverified.Result==VirtualReachability.Unknown,"unverified closed-boundary contract");
            var seeds=new VirtualBridgeMap(1,1,new ushort[]{1,1,1,1},new byte[4],new[]{0,0x1000,0x1000,0x1000},new ushort[]{0,1,2,3},new ushort[4],new int[1],Array.Empty<VirtualConnection>(),true,new ushort[]{0,1,2,65535},new short[]{0,5,15});
            Check(seeds.SeedEligibility(0)==1&&seeds.SeedEligibility(1)==1&&seeds.SeedEligibility(2)==0&&seeds.SeedEligibility(3)==-1,"exact surface/107160 exception and signed invalid record");
            var messages=new List<string>();int reads=0;
            var shadow=new BridgeVirtualShadow((kind,detail)=>messages.Add(kind+":"+detail),_=>{reads++;throw new Exception("unexpected native read");});
            shadow.Begin(10);shadow.Compare("keep-access",1,0,8,0,4,0,0);shadow.Pump();
            Check(messages.Count==2&&messages[1].Contains("missing-decision-input-or-policy")&&reads==0,"missing capture is explicit Unknown with no render read");
            shadow.Compare("keep-access",2,0,8,0,4,0,0);shadow.Compare("keep-access",3,0,8,0,4,0,0);shadow.Pump();
            Check(messages.Count==2,"unchanged missing-input observations coalesced before queue");
            shadow.Invalidate();shadow.Compare("keep-access",4,0,8,0,4,0,0);shadow.End();shadow.Begin(11);shadow.Pump();
            Check(reads==0&&messages.Count==5&&messages[3].Contains("repeatCount")&&messages[4].Contains("pending=1"),"map end counts cancellation, reused IDs cannot carry request to new map");
            Check(BridgeDecisionTrace.PackZeros("1/0/0/0/2")=="1/z3/2","numeric zero-run transport");
            // Production-size copied terrain, bounded step work and no native calls.
            var large=Map(800,401,true);var job=new VirtualBridgeQuery(large,0,320799,0,Array.Empty<int>(),true);
            long before=job.Expanded;job.Step(64);Check(job.Expanded-before<=64,"render node budget");
            var watch=Stopwatch.StartNew();while(!job.Complete)job.Step(256);watch.Stop();
            Check(job.Result==VirtualReachability.Reachable&&job.Expanded<=320800,"full packed-capacity traversal");
            Console.WriteLine("Virtual full-grid managed query: "+watch.Elapsed.TotalMilliseconds.ToString("F3")+" ms, "+job.Expanded+" nodes (not game-frame timing)");
            return checks;
        }
    }
}
