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
            ushort[] x,ushort[] y,int[] rows,VirtualConnection[] connections,bool complete,ushort[] specialIds=null,short[] specialKinds=null)
        {
            Session=session;Revision=revision;Components=(ushort[])components.Clone();Edges=(byte[])edges.Clone();
            Flags=(int[])flags.Clone();X=(ushort[])x.Clone();Y=(ushort[])y.Clone();RowStarts=(int[])rows.Clone();
            Connections=(VirtualConnection[])connections.Clone();Complete=complete;
            SpecialIds=specialIds==null?Array.Empty<ushort>():(ushort[])specialIds.Clone();SpecialKinds=specialKinds==null?Array.Empty<short>():(short[])specialKinds.Clone();
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
    internal readonly struct VirtualConnection
    {
        internal readonly int A,B,C,Class;
        internal readonly bool Active,Enabled,AccessAllowed,EndpointsKnown;
        internal VirtualConnection(int a,int b,int c,int kind,bool active,bool enabled,bool allowed,bool known)
        {A=a;B=b;C=c;Class=kind;Active=active;Enabled=enabled;AccessAllowed=allowed;EndpointsKnown=known;}
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
        internal VirtualReachability Result {get;private set;}=VirtualReachability.Unknown;
        internal bool Complete {get;private set;}
        internal bool StructureRequired {get;private set;}
        internal long Expanded {get;private set;}
        internal string Reason {get;private set;}="pending";
        internal VirtualBridgeQuery(VirtualBridgeMap map,int source,int target,int mode,IEnumerable<int> deck,
            bool authoritative,Func<int,int,bool> edgeAllowed=null,bool negativeProofComplete=true)
        {
            this.map=map;this.target=target;this.mode=mode;this.authoritative=authoritative;this.edgeAllowed=edgeAllowed;this.negativeProofComplete=negativeProofComplete;
            cut=new bool[map.Components.Length];seen=new bool[cut.Length];queue=new int[cut.Length];
            foreach(int tile in deck) {if((uint)tile<(uint)cut.Length)cut[tile]=true;else uncertain=true;}
            if(uncertain||!map.Complete||mode<0||mode>1||(uint)source>=(uint)cut.Length||(uint)target>=(uint)cut.Length)
                {Finish(VirtualReachability.Unknown,"incomplete-input-or-mode");return;}
            if(map.Components[source]==0||map.Components[target]==0||cut[source]||cut[target])
                {Finish(VirtualReachability.Unknown,"endpoint-not-in-copied-traversable-topology");return;}
            foreach(var connection in map.Connections)
            {
                if(!connection.Active||!connection.Enabled||!connection.AccessAllowed||connection.Class==1&&mode==0)continue;
                if(!connection.EndpointsKnown) {uncertain=true;continue;}
                foreach(int endpoint in new[]{connection.A,connection.B,connection.C})
                {
                    if(endpoint<0)continue;
                    if((uint)endpoint>=(uint)cut.Length||map.Components[endpoint]==0) {uncertain=true;continue;}
                    if(!macro.TryGetValue(endpoint,out List<VirtualConnection> records))macro[endpoint]=records=new List<VirtualConnection>();
                    records.Add(connection);
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
                    if(pass==0) {pass=1;Array.Clear(seen,0,seen.Length);head=tail=0;Push(Source);continue;}
                    Finish(uncertain||!authoritative||!negativeProofComplete?VirtualReachability.Unknown:VirtualReachability.NoRoute,
                        uncertain?"unresolved-transition":!negativeProofComplete?"closed-boundary-or-special-contract-unverified":!authoritative?"authorization-unresolved":"exhausted-both-native-macro-passes");break;
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
