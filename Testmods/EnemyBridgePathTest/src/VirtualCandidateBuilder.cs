using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Pure copied-memory 10DF60/CF020 continuation. No pointer, hook, live
    // lookup or game-state write; absent input always throws Unknown.
    // Present in the runtime assembly but behavior installation remains disabled.
    internal sealed class VirtualCandidateBuilder
    {
        private sealed class Segment { internal int Start; internal byte[] Data; }
        private readonly List<Segment> segments=new List<Segment>();
        private Segment last;
        internal void Put(int start,byte[] bytes)
        {
            if(bytes==null||start<0||(long)start+bytes.Length>0x8903000)throw new ArgumentException("candidate-copy-range");
            // Historical table copies have overlapping extents. Shared input
            // bytes must agree; inconsistent snapshots cannot be combined.
            foreach(var s in segments)
            {
                int from=Math.Max(start,s.Start),to=Math.Min(start+bytes.Length,s.Start+s.Data.Length);
                for(int p=from;p<to;p++)if(bytes[p-start]!=s.Data[p-s.Start])throw new ArgumentException("candidate-inconsistent-overlapping-copy");
            }
            segments.Add(new Segment{Start=start,Data=(byte[])bytes.Clone()});
        }
        private Segment At(int address,int length)
        {
            if(last!=null&&address>=last.Start&&(long)address+length<=last.Start+last.Data.Length)return last;
            foreach(var s in segments)if(address>=s.Start&&(long)address+length<=s.Start+s.Data.Length)return last=s;
            throw new InvalidOperationException("Unknown:uncopied-candidate-memory:"+address.ToString("X")+":"+length);
        }
        internal byte[] Bytes(int address,int length){var s=At(address,length);var result=new byte[length];Buffer.BlockCopy(s.Data,address-s.Start,result,0,length);return result;}
        internal void Replace(int address,byte[] bytes){Write(address,bytes);}
        private int B(int a){var s=At(a,1);return s.Data[a-s.Start];}
        private int S(int a){var s=At(a,2);return BitConverter.ToInt16(s.Data,a-s.Start);}
        private int I(int a){var s=At(a,4);return BitConverter.ToInt32(s.Data,a-s.Start);}
        private void B(int a,int v){var s=At(a,1);s.Data[a-s.Start]=unchecked((byte)v);}
        private void S(int a,int v){var s=At(a,2);int p=a-s.Start;s.Data[p]=unchecked((byte)v);s.Data[p+1]=unchecked((byte)(v>>8));}
        private void I(int a,int v){var s=At(a,4);int p=a-s.Start;s.Data[p]=unchecked((byte)v);s.Data[p+1]=unchecked((byte)(v>>8));s.Data[p+2]=unchecked((byte)(v>>16));s.Data[p+3]=unchecked((byte)(v>>24));}
        private void Write(int a,byte[] b){var s=At(a,b.Length);Buffer.BlockCopy(b,0,s.Data,a-s.Start,b.Length);}
        private void Clear(int a,int n){var s=At(a,n);Array.Clear(s.Data,a-s.Start,n);}
        private void Inc(int a){I(a,unchecked(I(a)+1));}
        private const int N=320800, Root=0x60ad660;
        private int Offset(int tile,int dir)=>I(0x405edb0+(S(0x3aae2a4+tile*2)*8+dir)*4);
        private int Component(int tile)=>S(0x50ec690+tile*2);
        private int Flag(int tile)=>I(0x48f71b0+tile*4);
        private int Seed(int tile)=>unchecked((sbyte)B(0x535ef90+tile));
        private int Building(int tile)=>S(0x4b6aa50+tile*2);
        private int BuildingRecord(int id)=>checked(0x64ccbb0+id*0x32c);
        private int Unit(int tile)=>S(0x4c559b0+tile*2);
        private int UnitRecord(int id)=>checked(0x67e8400+id*0x490);
        private int Height(int tile)
        {
            int id=Building(tile);if(id==0)return B(0x4ddd350+tile);
            int kind=S(BuildingRecord(id)+0x12e),extra;
            switch(kind){case 40:extra=64;break;case 41:extra=92;break;case 42:extra=190;break;case 45:case 46:extra=128;break;case 69:extra=118;break;case 74:extra=296;break;case 75:extra=148;break;case 76:extra=180;break;case 77:case 78:extra=192;break;default:extra=0;break;}
            return B((kind==45||kind==46?0x4e2b870:0x4ddd350)+tile)+extra;
        }
        private bool Used(int tile)
        {int n=I(0x2e97d0c);if(n<0||n>4000)throw new InvalidOperationException("Unknown:unit-task-count");for(int i=0;i<n;i++)if(I(0x2e93e8c+i*4)==tile)return true;return false;}
        private bool BuildingUsed(int id)
        {for(int i=0;i<1000;i++)if(I(0x2e8a840+i*16)==id||I(0x2e8e6dc+i*16)==id)return true;return false;}
        private bool OccupiedBuilding(int id)
        {
            int r=BuildingRecord(id),size=I(r+0x154),x=S(r+0x14a),y=S(r+0x14c);
            if(size>800)throw new InvalidOperationException("Unknown:building-size");
            for(int dy=0;dy<size;dy++)for(int dx=0;dx<size;dx++)
            {int u=Unit(I(0x402ff2c+(y+dy)*12)+x+dx);if(u!=0&&I(0x8574bcc+S(UnitRecord(u)+0x6ee)*4)!=-1)return true;}
            return false;
        }
        private Func<int,int,bool> reachable;
        private readonly Dictionary<long,bool> regionResults=new Dictionary<long,bool>();
        private void Regions(int attacker)
        {
            var links=new List<VirtualComponentLink>();int limit=I(Root);
            if(limit<1||limit>1000)throw new InvalidOperationException("Unknown:macro-limit");
            for(int id=1;id<limit;id++)
            {
                int r=Root+0x2228+(id-1)*0x204;
                if(I(r)!=1||I(r+24)==0)continue;
                int owner=I(r+0x1e4),building=I(r+12),kind=I(r+4);
                if(owner<0||owner>8||building<1)throw new InvalidOperationException("Unknown:macro-identity");
                bool eligible=attacker==0||I(0x37edf3c+owner*4)==I(0x37edf3c+attacker*4)||S(BuildingRecord(building)+0x322)!=0;
                if(eligible&&kind!=1)links.Add(new VirtualComponentLink(I(r+52),I(r+56),I(r+0x1e8),kind,true));
            }
            if(links.Count>200)throw new InvalidOperationException("Unknown:region-list-capacity");
            var frozen=links.ToArray();regionResults.Clear();reachable=(a,b)=>{
                if(a==b)return a!=0;if(a==0||b==0)return false;
                Inc(Root+0xc4);I(Root+0xc0,0);
                long key=((long)(uint)a<<32)|(uint)b;
                if(regionResults.TryGetValue(key,out bool cached))
                {if(!cached){I(Root+0xc0,1);I(Root+0x98,0);}return cached;}
                if(regionResults.Count>=4096)regionResults.Clear();
                // Mode 0 excludes class 1 before both passes. Retain Vanilla's
                // consumed-record queue (including its possibly zero A/B entry).
                var used=new bool[frozen.Length];var queue=new int[400];int head=0,tail=0;
                bool Expand(int component)
                {
                    for(int i=0;i<frozen.Length;i++)
                    {
                        if(used[i])continue;var link=frozen[i];int first,second;
                        if(link.A==component){first=link.B;second=link.C;}
                        else if(link.B==component){first=link.A;second=link.C;}
                        else if(link.C==component){first=link.A;second=link.B;}
                        else continue;
                        if(first==a||second==a)return true;
                        used[i]=true;if(tail>=queue.Length)throw new InvalidOperationException("Unknown:region-queue-capacity");queue[tail++]=first;
                        if(second>0){if(tail>=queue.Length)throw new InvalidOperationException("Unknown:region-queue-capacity");queue[tail++]=second;}
                    }
                    return false;
                }
                if(Expand(b)){regionResults[key]=true;return true;}while(head<tail)if(Expand(queue[head++])){regionResults[key]=true;return true;}
                regionResults[key]=false;I(Root+0xc0,1);I(Root+0x98,0);return false;
            };
        }
        private void Append(int count,int table,int tile,int next)
        {int n=I(count);if(n<0||n>=1000)throw new InvalidOperationException("Unknown:candidate-capacity");I(table+n*16,tile);I(table+n*16+4,next);I(count,n+1);}
        private bool Near(int tile,int height)=>Math.Abs(Height(tile)-height)<=16;
        private void Flood(int tile,int max,bool ladder)
        {
            int y=S(0x3aae2a4+tile*2),x=tile-I(0x402ff2c+y*12);
            if((uint)x>=800||(uint)y>=800||B(0x3a11ea4+y*800+x)==0)return;
            Inc(Root+4);if(I(Root+4)>32000){I(Root+4,1);Clear(0x52c2550,N*2);}
            I(Root+0x155f38,1);I(Root+0x155f3c,0);I(Root+0x155f44,1);
            S(Root+0x28f3ec,y);S(Root+0x32be2c,x);I(Root+0x155f6c,tile);
            if(!ladder&&(B(0x53ad4b0+tile)&15)!=0)return;
            S(0x5225b10+tile*2,1);S(0x52c2550+tile*2,I(Root+4));
            int budget=N*9;
            while(I(Root+0x155f3c)!=I(Root+0x155f44))
            {
                if(--budget<0)throw new InvalidOperationException("Unknown:flood-work-limit");
                int head=I(Root+0x155f3c),current=I(Root+0x155f6c+head*4);
                if((uint)current>=N){if(!ladder)return;throw new InvalidOperationException("Unknown:ladder-tile");}
                B(0x53ad4b0+current,B(0x53ad4b0+current)|(ladder?32:1));
                int level=S(0x5225b10+current*2),cx=S(Root+0x32be2c+head*2),cy=S(Root+0x28f3ec+head*2);
                I(Root+0x155f38,level);if(max<level)return;
                // E7530 tests the low byte of flags[current]. The decompiler
                // omits the *4; the audited instruction at E7673 retains it.
                if((Flag(current)&0x30)==0)
                for(int d=0;d<8;d++)
                {
                    int next=current+I(0x405edb0+(cy*8+d)*4);
                    if(S(0x52c2550+next*2)==I(Root+4)||!ladder&&B(0x53ad4b0+next)!=0)continue;
                    S(0x5225b10+next*2,level+1);S(0x52c2550+next*2,I(Root+4));
                    int tail=I(Root+0x155f44);
                    S(Root+0x32be2c+tail*2,cx+S(0x2d2e50+d*8));S(Root+0x28f3ec+tail*2,cy+S(0x2d2e54+d*8));
                    I(Root+0x155f6c+tail*4,next);I(Root+0x155f44,tail+1==N?0:tail+1);
                }
                I(Root+0x155f3c,head+1==N?0:head+1);
            }
        }
        internal void WorkFlood(int tile,int maximum,bool ladder){Flood(tile,maximum,ladder);}
        // CF020 -> DB650 -> E9610 -> E3590. These work buffers are distinct
        // from D9190's strategic distance bank and candidate selection mask.
        internal void PostConsumer(int target)
        {
            int x=I(0x379afa8+target*0x583c),y=I(0x379afac+target*0x583c);
            if((uint)x<800&&(uint)y<800&&B(0x3a11ea4+y*800+x)!=0)
            {
                Inc(Root+4);if(I(Root+4)>32000){I(Root+4,1);Clear(0x52c2550,N*2);}
                I(Root+0x155f38,1);I(Root+0x155f3c,0);I(Root+0x155f44,1);
                S(Root+0x28f3ec,y);S(Root+0x32be2c,x);
                int tile=I(0x402ff2c+y*12)+x;I(Root+0x155f6c,tile);S(0x5225b10+tile*2,1);S(0x52c2550+tile*2,I(Root+4));
                while(I(Root+0x155f3c)!=I(Root+0x155f44))
                {
                    int head=I(Root+0x155f3c),current=I(Root+0x155f6c+head*4);
                    if(current==0||head>=8000||(uint)current>=N)break;
                    int cy=S(Root+0x28f3ec+head*2),cx=S(Root+0x32be2c+head*2),level=S(0x5225b10+current*2);
                    I(Root+0x155f38,level);
                    for(int d=0;d<8;d++)
                    {
                        int next=current+I(0x405edb0+(cy*8+d)*4);
                        if(S(0x52c2550+next*2)==I(Root+4)||(B(0x51890d0+current)&B(0x312620+d))==0)continue;
                        int flags=Flag(next);
                        if((flags&2)==0&&(flags&0x10000100)!=0)
                        {int id=Building(next);if(id==0||I(0x2e68d0+S(BuildingRecord(id)+0x12e)*4)==0)continue;}
                        S(0x5225b10+next*2,level+1);S(0x52c2550+next*2,I(Root+4));
                        int tail=I(Root+0x155f44);S(Root+0x32be2c+tail*2,cx+S(0x2d2e50+d*8));S(Root+0x28f3ec+tail*2,cy+S(0x2d2e54+d*8));
                        I(Root+0x155f6c+tail*4,next);I(Root+0x155f44,tail+1==N?0:tail+1);
                    }
                    I(Root+0x155f3c,head+1==N?0:head+1);
                }
            }
            Inc(Root+0xa0);I(Root+0x155f3c,0);int count=0,budget=N;
            if(I(Root+0x155f44)==0)return;
            do
            {
                if(--budget<0)throw new InvalidOperationException("Unknown:work-target-limit");
                int head=I(Root+0x155f3c),tile=I(Root+0x155f6c+head*4),ty=S(Root+0x28f3ec+head*2),tx=tile-I(0x402ff2c+ty*12);
                if(S(0x5225b10+tile*2)!=100)
                {
                    MarkWorkRegion(tx,ty);int output=0x37f1edc+(target*50+count)*16;
                    I(output,tx);I(output+4,ty);I(output+8,0);I(output+12,0);if(++count>=50)return;
                }
                I(Root+0x155f3c,head+1==N?0:head+1);
            }while(I(Root+0x155f3c)!=I(Root+0x155f44));
        }
        private void MarkWorkRegion(int x,int y)
        {
            if((uint)x>=800||(uint)y>=800||B(0x3a11ea4+y*800+x)==0)return;
            Inc(Root+0xa4);var tiles=new int[1000];var rows=new int[1000];var columns=new int[1000];
            tiles[0]=I(0x402ff2c+y*12)+x;rows[0]=y;columns[0]=x;
            int head=0,tail=1,limit=100,budget=N;
            do
            {
                if(--budget<0)throw new InvalidOperationException("Unknown:work-region-limit");
                int tile=tiles[head],cy=rows[head],cx=columns[head];if((uint)tile>=N||limit<=tail||tail>998)return;
                S(0x5225b10+tile*2,100);
                for(int d=0;d<8;d++)
                {
                    int next=tile+I(0x405edb0+(cy*8+d)*4);
                    if(S(0x52c2550+next*2)!=I(Root+4)){limit--;continue;}
                    int distance=S(0x5225b10+next*2);
                    if(distance==100){limit--;continue;}
                    if(distance<=0)continue;
                    S(0x5225b10+next*2,100);tiles[tail]=next;
                    // E3590 uses signed int DX/DY entries, not the queue shorts.
                    columns[tail]=unchecked(cx+I(0x2d2e50+d*8));rows[tail]=unchecked(cy+I(0x2d2e54+d*8));
                    tail++;if(d!=0&&tail>999)tail=0;
                }
                head++;if(head>=1000)head=0;
            }while(head!=tail);
        }
        internal int Castle(int attacker,int target)
        {
            if(reachable==null)throw new InvalidOperationException("Unknown:candidate-regions-not-prepared");
            if(I(0x379ae64+attacker*0x583c)==0||I(0x379ae64+target*0x583c)==0)return 1;
            int destination=Component(I(0x379afb0+target*0x583c)),origin=Component(I(0x379afb0+attacker*0x583c));
            if(destination==0||origin==0)return 0;
            return reachable(destination,origin)?1:0;
        }
        internal void SelectAndUnitTargets(int attacker,int target,int[] effectiveCounts)
        {
            int tile=I(0x379d968+attacker*0x583c),region=Component(tile);
            if((uint)region>=effectiveCounts.Length)throw new InvalidOperationException("Unknown:selection-component");
            I(0x2e97eac,1);I(0x2e97ea8,Seed(tile));I(0x2e97d10,region);I(0x2e9ca14,effectiveCounts[region]);I(0x2e97d14,Component(I(0x379afb0+target*0x583c)));
            int limit=I(0x67e8400);if(limit<1||limit>10000)throw new InvalidOperationException("Unknown:unit-manager-count");
            int count;var tasks=VirtualCandidateConsumers.UnitTargets(Bytes(0x67e8400,0x65c+limit*0x490),attacker,out count);
            var copied=new byte[tasks.Length*4];Buffer.BlockCopy(tasks,0,copied,0,copied.Length);Replace(0x2e93e8c,copied);I(0x2e97d0c,count);
        }
        internal void Run(int attacker,int target)
        {
            if(attacker<1||attacker>8||target<1||target>8)throw new ArgumentException("players");
            int prior=(attacker-1)*0x177bc,shift=attacker*0x177bc;
            foreach(int a in new[]{0x2ebe89c,0x2ec2748,0x2ece31c}){I(a+prior,0);I(a+prior+4,0);}
            foreach(int a in new[]{0x2ec2744,0x2eca480,0x2ed21b8,0x2ec65e4,0x2eca484,0x2ed21bc,0x2ec65e8})I(a+prior,0);
            foreach(int a in new[]{0x2e8a82c,0x2e8e6c8,0x2eb9664,0x2ea377c,0x2eb9668,0x2ea3780,0x2e8a830,0x2e8e6cc,0x2ea3774})I(a,0);
            I(0x2e97eb0,1000);I(0x2e9ca18,B(0x8574b90)!=0||I(0x2e9ca14)>2999?4:10);
            foreach(int a in new[]{0x2ebe8a8,0x2ec2754,0x2ec65f0,0x2eca48c,0x2ed21c4,0x2ece328})Clear(a+prior,16000);
            foreach(int a in new[]{0x2eb9670,0x2ea3788,0x2e8a838,0x2e8e6d4})Clear(a,16000);
            Regions(attacker);int region=I(0x2e97d10),cost=I(0x2e9ca18);
            for(int tile=0;tile<N;tile++)
            {
                int u=Unit(tile),flags=Flag(tile);if(u==0&&(flags&0x50000700)==0)continue;
                int id=Building(tile),height=Height(tile),distance=S(0x5759230+target*N*2+tile*2);
                if(u!=0)
                {
                    int record=UnitRecord(u),owner=S(record+0x6ee);
                    if(owner!=0&&I(0x37edf3c+owner*4)!=I(0x37edf3c+attacker*4))
                    {
                        if(distance<=0)continue;
                        if(Component(tile)==region&&B(record+0x984)==0&&S(record+0x8fc)!=0)
                        {
                            Inc(0x2eb9664);for(int d=0;d<8;d+=2)
                            {int n=tile+Offset(tile,d);if(Component(n)!=region)continue;if(I(0x2eb9668)>=1000||Used(tile))break;if(Near(n,height))Append(0x2eb9668,0x2eb9670,tile,n);}
                        }
                        if(S(record+0x6e6)==55)
                        {Inc(0x2ea377c);I(0x2ea3774,Component(tile));if(Component(tile)==region)for(int d=0;d<8;d++){int n=tile+Offset(tile,d);if(Component(n)!=0){if(I(0x2ea3780)>=1000)break;Append(0x2ea3780,0x2ea3788,tile,n);}}}
                    }
                }
                int baseHeight=B(0x4e2b870+tile);
                if(id!=0&&S(BuildingRecord(id)+0x132)==target)
                {
                    int kind=S(BuildingRecord(id)+0x12e);
                    if(I(0x2e6c50+kind*4)!=0)
                    {
                        if(distance<=0)continue;Inc(0x2eb2cc4+shift);
                        for(int d=0;d<8;d+=2){int n=tile+Offset(tile,d);if(Component(n)!=region)continue;if(I(0x2eb2cc8+shift)>=1000||Used(tile))break;if((Flag(n)&0x10000100)==0&&Near(n,baseHeight)){Append(0x2eb2cc8+shift,0x2eb2cd0+shift,tile,n);if(Seed(tile)!=0)Flood(tile,cost,false);break;}}
                    }
                    else if(I(0x2e6710+kind*4)!=0)
                    {if((flags&0xf000000)!=0)continue;Inc(0x2eaee28+shift);for(int d=0;d<8;d+=2){if(I(0x2eaee2c+shift)>=1000)break;int n=tile+Offset(tile,d);if(Component(n)==region){if(Used(tile))break;if(Near(n,baseHeight)){Append(0x2eaee2c+shift,0x2eaee34+shift,tile,n);break;}}}}
                    else if((flags&0x10000000)!=0&&distance>0&&!BuildingUsed(id))
                    {
                        int seed=0;for(int d=0;d<8;d+=2){int value=Seed(tile+Offset(tile,d));if(value!=0)seed=value;}
                        if(seed>0){S(BuildingRecord(id)+0x310,seed);bool occupied=OccupiedBuilding(id);int seen=occupied?0x2e8a82c:0x2e8e6c8,count=occupied?0x2e8a830:0x2e8e6cc,table=occupied?0x2e8a838:0x2e8e6d4;Inc(seen);if(I(count)<1000&&reachable(region,Component(tile))){int at=I(count);Append(count,table,tile,tile);I(table+at*16+8,id);}}
                    }
                    continue;
                }
                if((flags&0x300)==0||(flags&2)!=0)
                {
                    if((flags&0x40000000)==0||distance<=0)continue;Inc(0x2eb6b60+shift);
                    for(int d=0;d<8;d+=2){if(I(0x2eb6b64+shift)>=1000)break;int n=tile+Offset(tile,d);if(!reachable(region,Component(n)))continue;if(Used(tile))break;if((Flag(n)&0x10000100)==0&&Near(n,baseHeight)){Append(0x2eb6b64+shift,0x2eb6b6c+shift,tile,n);if(Seed(tile)!=0)Flood(tile,1,false);break;}}
                    continue;
                }
                if(distance<=0||(B(0x4e79d90+tile)&7)+1!=target||B(0x4ddd350+tile)-baseHeight<=20||id!=0)continue;
                bool ladder=false;
                for(int d=0;d<8;d+=2)
                {
                    if(I(0x2ebaa00+shift)>=1000)break;int n=tile+Offset(tile,d);
                    if(!reachable(region,Component(n))||!reachable(region,Component(tile+Offset(tile,d+1)))||!reachable(region,Component(tile+Offset(tile,(d+7)%8)))||(B(0x53ad4b0+tile)&32)!=0)continue;
                    if(Used(tile))break;
                    if((Flag(n)&0x10000100)==0&&Near(n,baseHeight)){Append(0x2ebaa00+shift,0x2ebaa08+shift,tile,n);Inc(0x2eba9fc+shift);Flood(tile,4,true);ladder=true;break;}
                }
                if(ladder)continue;
                Inc(0x2eaaf88+shift);
                for(int d=0;d<8;d+=2){if(I(0x2ea70e4+shift)>=1000)break;int n=tile+Offset(tile,d);if(!reachable(region,Component(n)))continue;if(Used(tile))break;if((Flag(n)&0x10000100)==0&&Near(n,baseHeight)){Append(0x2ea70e4+shift,0x2ea70ec+shift,tile,n);B(0x535ef90+n,10);if(Seed(tile)!=0)Flood(tile,cost,false);break;}}
                Inc(0x2ea70e0+shift);
                if((flags&0x400000)!=0)continue;
                Inc(0x2eaaf8c+shift);int currentSeed=Seed(tile);if(currentSeed!=0&&currentSeed<I(0x2e97eb0))I(0x2e97eb0,currentSeed);
                for(int d=0;d<8;d+=2){if(I(0x2eaaf90+shift)>=1000)break;int n=tile+Offset(tile,d);if(!reachable(region,Component(n)))continue;if(Used(tile))break;if((Flag(n)&0x10000100)==0&&Near(n,baseHeight)){Append(0x2eaaf90+shift,0x2eaaf98+shift,tile,n);if(Seed(tile)!=0)Flood(tile,cost,false);break;}}
            }
        }
    }
}
