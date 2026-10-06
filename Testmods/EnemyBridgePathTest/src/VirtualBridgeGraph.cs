using System;
using System.Collections.Generic;

namespace EnemyBridgePathTest
{
    internal enum VirtualReachability { Unknown, Reachable, NoRoute }
    // Owns copied input only. There is no native delegate, write or Vanilla search.
    internal sealed class VirtualBridgeMap
    {
        internal readonly ushort[] Components, X, Y;
        internal readonly byte[] Edges;
        internal readonly int[] Flags, RowStarts;
        internal readonly VirtualConnection[] Connections;
        internal readonly long Session, Revision;
        internal readonly bool Complete;
        internal readonly ushort[] SpecialIds;
        internal readonly short[] SpecialKinds;
        internal VirtualBridgeMap(long session,long revision,ushort[] components,byte[] edges,int[] flags,
            ushort[] x,ushort[] y,int[] rows,VirtualConnection[] connections,bool complete,ushort[] specialIds=null,short[] specialKinds=null,bool takeOwnership=false)
        {
            Session=session;Revision=revision;Components=takeOwnership?components:(ushort[])components.Clone();Edges=takeOwnership?edges:(byte[])edges.Clone();
            Flags=takeOwnership?flags:(int[])flags.Clone();X=takeOwnership?x:(ushort[])x.Clone();Y=takeOwnership?y:(ushort[])y.Clone();RowStarts=takeOwnership?rows:(int[])rows.Clone();
            Connections=(VirtualConnection[])connections.Clone();Complete=complete;
            SpecialIds=specialIds==null?Array.Empty<ushort>():takeOwnership?specialIds:(ushort[])specialIds.Clone();SpecialKinds=specialKinds==null?Array.Empty<short>():takeOwnership?specialKinds:(short[])specialKinds.Clone();
            if(Components.Length!=Edges.Length||Components.Length!=Flags.Length||Components.Length!=X.Length||X.Length!=Y.Length)
                throw new ArgumentException("Different packed grid capacities");
        }
        private VirtualBridgeMap(VirtualBridgeMap source,VirtualConnection[] connections)
        {
            Session=source.Session;Revision=source.Revision;Components=source.Components;Edges=source.Edges;
            Flags=source.Flags;X=source.X;Y=source.Y;RowStarts=source.RowStarts;
            Connections=(VirtualConnection[])connections.Clone();Complete=source.Complete;SpecialIds=source.SpecialIds;SpecialKinds=source.SpecialKinds;
        }
        internal VirtualBridgeMap WithConnections(VirtualConnection[] connections) => new VirtualBridgeMap(this,connections);
        // Exact E49D0 seed predicate; -1 denotes unavailable 107160 record input.
        // Transition eligibility is deliberately separate from seed eligibility.
        internal int SeedEligibility(int tile)
        {
            uint flags=unchecked((uint)Flags[tile]);if((flags&0x4A5014B1u)==0)return 1;
            if((flags&0x1000)==0)return 0;
            if(SpecialIds.Length!=Components.Length)return -1;
            int record=(short)SpecialIds[tile];if(record==0)return 0;
            if(record<0||record>=SpecialKinds.Length)return -1;
            short kind=SpecialKinds[record];return kind>4&&kind!=15?1:0;
        }
        internal int Tile(int x,int y)
        {
            if(x<0||y<0||y>=RowStarts.Length)return -1;
            long tile=(long)RowStarts[y]+x;
            return tile>=0&&tile<X.Length&&X[tile]==x&&Y[tile]==y?(int)tile:-1;
        }
        internal int Neighbor(int tile,int direction) => Tile(X[tile]+Dx[direction],Y[tile]+Dy[direction]);
        internal static readonly int[] Dx={0,1,1,1,0,-1,-1,-1},Dy={-1,-1,0,1,1,1,0,-1};
    }
    // Full native class updaters D7E90 (class3) and D8040 (class4).
    // Their C lookup is PCL(originX+1,originY+1), not an arbitrary PCL anchor.
    internal static class VirtualGateEndpoint
    {
        internal static bool TryResolve(VirtualBridgeMap map,int kind,int entry,int exit,int component,out int tile)
        {
            tile=-1;int size=kind==3?7:kind==4?5:0;
            if(size==0||component<=0||(uint)entry>=(uint)map.X.Length||(uint)exit>=(uint)map.X.Length)return false;
            int ox,oy,mid=size/2;
            // Audited orientations: 0 has the entry south; 2 has the entry east.
            if(map.X[entry]==map.X[exit]&&map.Y[entry]-map.Y[exit]==size+1)
            {ox=map.X[entry]-mid;oy=map.Y[exit]+1;}
            else if(map.Y[entry]==map.Y[exit]&&map.X[entry]-map.X[exit]==size+1)
            {ox=map.X[exit]+1;oy=map.Y[entry]-mid;}
            else return false;
            if(ox<0||oy<0||ox+size>800||oy+size>800)return false;
            int candidate=map.Tile(ox+1,oy+1);
            if(candidate<0||map.Components[candidate]!=component)return false;
            tile=candidate;return true;
        }
    }
    internal readonly struct VirtualConnection
    {
        internal readonly int A,B,C,Class;
        internal readonly bool Active,Enabled,AccessAllowed,EndpointsKnown,ThirdEndpointUnknown;
        internal VirtualConnection(int a,int b,int c,int kind,bool active,bool enabled,bool allowed,bool known,bool thirdUnknown=false)
        {A=a;B=b;C=c;Class=kind;Active=active;Enabled=enabled;AccessAllowed=allowed;EndpointsKnown=known;ThirdEndpointUnknown=thirdUnknown;}
    }
    internal sealed class VirtualBridgeWorkspace
    {
        internal readonly bool[] Cut,Seen;
        internal readonly int[] Queue;
        internal VirtualBridgeWorkspace(int count) {Cut=new bool[count];Seen=new bool[count];Queue=new int[count];}
    }
    // Incremental directed tile traversal. Actual endpoint tiles express a split even
    // when Vanilla gives source and target the same PCL. Never merge directed edges.
    internal sealed class VirtualBridgeQuery
    {
        private readonly VirtualBridgeMap map;
        private readonly bool[] cut,seen;
        private readonly int[] queue;
        private readonly int target,mode;
        private readonly Dictionary<int,List<VirtualConnection>> macro=new Dictionary<int,List<VirtualConnection>>();
        private readonly bool authoritative;
        private readonly Func<int,int,bool> edgeAllowed;
        private int head,tail,pass;
        private readonly bool negativeProofComplete;
        private bool uncertain;
        private bool secondPassChangesKnownGraph;
        internal VirtualReachability Result {get;private set;}=VirtualReachability.Unknown;
        internal bool Complete {get;private set;}
        internal bool StructureRequired {get;private set;}
        internal long Expanded {get;private set;}
        internal string Reason {get;private set;}="pending";
        internal VirtualBridgeQuery(VirtualBridgeMap map,int source,int target,int mode,IEnumerable<int> deck,
            bool authoritative,Func<int,int,bool> edgeAllowed=null,bool negativeProofComplete=true,VirtualBridgeWorkspace workspace=null)
        {
            this.map=map;this.target=target;this.mode=mode;this.authoritative=authoritative;this.edgeAllowed=edgeAllowed;this.negativeProofComplete=negativeProofComplete;
            if(workspace!=null&&workspace.Cut.Length!=map.Components.Length)throw new ArgumentException("Workspace capacity mismatch");
            cut=workspace?.Cut??new bool[map.Components.Length];seen=workspace?.Seen??new bool[cut.Length];queue=workspace?.Queue??new int[cut.Length];
            if(workspace!=null) {Array.Clear(cut,0,cut.Length);Array.Clear(seen,0,seen.Length);}
            foreach(int tile in deck) {if((uint)tile<(uint)cut.Length)cut[tile]=true;else uncertain=true;}
            if(uncertain||!map.Complete||mode<0||mode>1||(uint)source>=(uint)cut.Length||(uint)target>=(uint)cut.Length)
                {Finish(VirtualReachability.Unknown,"incomplete-input-or-mode");return;}
            if(map.Components[source]==0||map.Components[target]==0||cut[source]||cut[target])
                {Finish(VirtualReachability.Unknown,"endpoint-not-in-copied-traversable-topology");return;}
            foreach(var connection in map.Connections)
            {
                if(!connection.Active||!connection.Enabled||!connection.AccessAllowed||connection.Class==1&&mode==0)continue;
                if(connection.ThirdEndpointUnknown)uncertain=true; // Unknown C blocks negative proof, never a known A/B witness.
                if(!connection.EndpointsKnown) {uncertain=true;continue;}
                foreach(int endpoint in new[]{connection.A,connection.B,connection.C})
                {
                    if(endpoint<0)continue;
                    if((uint)endpoint>=(uint)cut.Length||map.Components[endpoint]==0) {uncertain=true;continue;}
                    if(!macro.TryGetValue(endpoint,out List<VirtualConnection> records))macro[endpoint]=records=new List<VirtualConnection>();
                    records.Add(connection);
                    if(connection.Class==1)secondPassChangesKnownGraph=true;
                }
            }
            Push(source);Source=source;
        }
        private int Source;
        private void Push(int tile) {if((uint)tile<(uint)cut.Length&&!cut[tile]&&!seen[tile]&&map.Components[tile]!=0) {seen[tile]=true;queue[tail++]=tile;}}
        private void Finish(VirtualReachability value,string reason) {Result=value;Reason=reason;Complete=true;}
        internal void Step(int nodeBudget)
        {
            if(nodeBudget<1)throw new ArgumentOutOfRangeException(nameof(nodeBudget));
            while(!Complete&&nodeBudget-->0)
            {
                if(head==tail)
                {
                    if(pass==0&&secondPassChangesKnownGraph) {pass=1;Array.Clear(seen,0,seen.Length);head=tail=0;Push(Source);continue;}
                    Finish(uncertain||!authoritative||!negativeProofComplete?VirtualReachability.Unknown:VirtualReachability.NoRoute,
                        uncertain?"unresolved-transition":!negativeProofComplete?"closed-boundary-or-special-contract-unverified":!authoritative?"authorization-unresolved":secondPassChangesKnownGraph?"exhausted-both-native-macro-passes":"exhausted-known-graph-equivalent-second-pass");break;
                }
                int tile=queue[head++];Expanded++;
                if(tile==target) {StructureRequired=pass==1;Finish(authoritative?VirtualReachability.Reachable:VirtualReachability.Unknown,
                    authoritative?"copied-directed-witness":"authorization-unresolved-with-geometric-witness");break;}
                byte edges=map.Edges[tile];
                for(int direction=0;direction<8;direction++)
                {
                    if((edges&(1<<direction))==0)continue;
                    int next=map.Neighbor(tile,direction);
                    if(next<0) {uncertain=true;continue;}
                    // Native gates were temporarily closed during PCL construction.
                    // Restored cross-PCL edge bits are macro transitions, not terrain.
                    if(map.Components[next]!=map.Components[tile])continue;
                    if(edgeAllowed==null||edgeAllowed(tile,direction))Push(next);
                }
                if(macro.TryGetValue(tile,out List<VirtualConnection> outgoing))foreach(var connection in outgoing)
                {
                    if(connection.Class==1&&pass==0)continue;
                    Push(connection.A);Push(connection.B);if(connection.C>=0)Push(connection.C);
                }
            }
        }
    }
}
