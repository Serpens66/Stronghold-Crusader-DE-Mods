using System;
using System.Collections.Generic;

namespace EnemyBridgePathTest
{
    // E2610 boolean reachability, not its next-component return or native side effects.
    // Starts at destination, first excludes class 1, then retries with class 1.
    internal readonly struct VirtualComponentLink
    {
        internal readonly int A,B,C,Class;
        internal readonly bool Eligible;
        internal VirtualComponentLink(int a,int b,int c,int kind,bool eligible)
        {A=a;B=b;C=c;Class=kind;Eligible=eligible;}
    }
    internal static class VirtualComponentControl
    {
        internal static VirtualReachability Evaluate(int current,int destination,int mode,VirtualComponentLink[] links)
        {
            if(mode<0||mode>2||current<0||destination<0)return VirtualReachability.Unknown;
            if(current==destination)return current==0?VirtualReachability.NoRoute:VirtualReachability.Reachable;
            if(current==0||destination==0)return VirtualReachability.NoRoute;
            for(int pass=0;pass<2;pass++)
            {
                var queue=new Queue<int>();var seen=new HashSet<int>();queue.Enqueue(destination);seen.Add(destination);
                while(queue.Count!=0)
                {
                    int component=queue.Dequeue();
                    foreach(var link in links)
                    {
                        if(!link.Eligible||link.Class==1&&(mode==0||pass==0)||link.Class!=1&&mode==2)continue;
                        if(link.A!=component&&link.B!=component&&link.C!=component)continue;
                        if(link.A==current||link.B==current||link.C==current)return VirtualReachability.Reachable;
                        foreach(int next in new[]{link.A,link.B,link.C})if(next>0&&seen.Add(next))queue.Enqueue(next);
                    }
                }
            }
            return VirtualReachability.NoRoute;
        }
    }
}
