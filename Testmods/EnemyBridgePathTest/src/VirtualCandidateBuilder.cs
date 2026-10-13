using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Pure copied-memory 10DF60/CF020 continuation. No pointer, hook, live
    // lookup or game-state write; absent input always throws Unknown.
    // Present in the runtime assembly but behavior installation remains disabled.
    internal sealed partial class VirtualCandidateBuilder
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
        private void MarkWorkRegion(int x,int y,int maximum=100)
        {
            if((uint)x>=800||(uint)y>=800||B(0x3a11ea4+y*800+x)==0)return;
            Inc(Root+0xa4);var tiles=new int[1000];var rows=new int[1000];var columns=new int[1000];
            tiles[0]=I(0x402ff2c+y*12)+x;rows[0]=y;columns[0]=x;
            int head=0,tail=1,limit=maximum,budget=N;
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
        // Complete EA4E0, with output coordinates supplied by the private caller.
        // Return count identifies written pairs; untouched entries are not evidence.
        internal int FormationTargets(int target,int maximum,int[] coordinates)
        {
            if(coordinates==null||coordinates.Length!=40||target<1||target>8)throw new ArgumentException("formation-output-extent");
            int threshold=Math.Min(maximum,100),count=0,budget=N;I(Root+0x155f3c,0);
            if(I(Root+0x155f44)==0)return 0;
            do
            {
                if(--budget<0)throw new InvalidOperationException("Unknown:formation-queue-bound");
                int head=I(Root+0x155f3c),tile=I(Root+0x155f6c+head*4);if((uint)tile>=N)throw new InvalidOperationException("Unknown:formation-packed-field");
                int strategic=S(0x5759230+target*N*2+tile*2);
                if(strategic!=0)
                {
                    if(threshold<=strategic&&strategic<=unchecked(threshold+10)&&S(0x5225b10+tile*2)!=100)
                    {int y=S(Root+0x28f3ec+head*2),x=tile-I(0x402ff2c+y*12);MarkWorkRegion(x,y,200);coordinates[count*2]=x;coordinates[count*2+1]=y;count++;}
                    if(count>=20)return count;
                }
                I(Root+0x155f3c,head+1==N?0:head+1);
            }while(I(Root+0x155f3c)!=I(Root+0x155f44));
            return count;
        }
        // Full EAB80 copied gatehouse selector, including its building-tile override.
        // This returns a building ID; it does not issue an order or store30E90's target.
        internal int FindEnemyGatehouse(int attacker,int x,int y,int maximum)
        {
            if(attacker<1||attacker>8||x<0||x>=800||y<0||y>=800||maximum<0)throw new ArgumentException("copied-gatehouse-selector-context");
            const int root=0x60ad660,tiles=root+0x155f6c,rows=root+0x28f3ec,dist=0x5225b10,visit=0x52c2550;
            I(root+0xa0,unchecked(I(root+0xa0)+1));I(root+4,unchecked(I(root+4)+1));if(I(root+4)>32000){I(root+4,1);Clear(visit,N*2);}
            I(root+0x155f38,1);I(root+0x155f3c,0);I(root+0x155f44,1);S(rows,y);int start=I(0x402ff2c+y*12)+x;if((uint)start>=N)throw new ArgumentException("copied-gatehouse-selector-origin");I(tiles,start);S(dist+start*2,1);S(visit+start*2,I(root+4));long steps=0;
            while(I(root+0x155f3c)!=I(root+0x155f44))
            {
                if(++steps>2000000)throw new InvalidOperationException("copied-gatehouse-selector-budget");int head=I(root+0x155f3c),tile=I(tiles+head*4);if((uint)tile>=N)return 0;int row=S(rows+head*2),level=S(dist+tile*2);I(root+0x155f38,level);if(maximum<level)return 0;
                for(int direction=0;direction<8;direction++)
                {
                    int next=tile+I(0x405edb0+(row*8+direction)*4);if((uint)next>=N)throw new InvalidOperationException("copied-gatehouse-selector-neighbor");int id=S(0x4b6aa50+next*2);
                    if((id==0&&(B(0x312620+direction)&B(0x51890d0+tile))==0)||S(visit+next*2)==I(root+4))continue;
                    S(dist+next*2,level+1);S(visit+next*2,I(root+4));int tail=I(root+0x155f44);S(rows+tail*2,row+S(0x2d2e54+direction*8));I(tiles+tail*4,next);I(root+0x155f44,tail+1>=N?0:tail+1);
                    if(id==0)continue;int building=BuildingRecord(id),owner=S(building+0x132),kind=S(building+0x12e),alive=S(building+0x12c);
                    if(owner<0||owner>8)throw new ArgumentException("copied-gatehouse-selector-owner");if(I(0x37edf3c+attacker*4)!=I(0x37edf3c+owner*4)&&alive!=0&&alive!=3&&(kind==45||kind==46)&&S(building+0x31a)==0)return id;
                }
                int nextHead=I(root+0x155f3c)+1;I(root+0x155f3c,nextHead>=N?0:nextHead);
            }
            return 0;
        }
        internal int Castle(int attacker,int target)
        {
            if(reachable==null)throw new InvalidOperationException("Unknown:candidate-regions-not-prepared");
            if(I(0x379ae64+attacker*0x583c)==0||I(0x379ae64+target*0x583c)==0)return 1;
            int destination=Component(I(0x379afb0+target*0x583c)),origin=Component(I(0x379afb0+attacker*0x583c));
            if(destination==0||origin==0)return 0;
            return reachable(destination,origin)?1:0;
        }
        // Complete F32B0 over private copied state. The caller supplies a private
        // output bank as E62D0 would; this is not a full 196280 replay.
        internal int CommandLine(int sx,int sy,int tx,int ty,byte[] path,bool relaxedSurface=false)
        {
            if((uint)sx>=800||(uint)sy>=800||(uint)tx>=800||(uint)ty>=800||B(0x3a11ea4+sy*800+sx)==0||B(0x3a11ea4+ty*800+tx)==0)return 0;
            int generation=unchecked(I(Root+4)+1);I(Root+4,generation);
            if(generation>32000){generation=1;I(Root+4,1);Clear(0x52c2550,N*2);}
            int mode=I(0x2f7a690),count=I(Root+0x155f68),tile=I(0x402ff2c+sy*12)+sx,destination=I(0x402ff2c+ty*12)+tx;
            if(count<0||path==null)throw new ArgumentException("line-private-output-bank");
            for(int steps=0;steps<=1600;steps++)
            {
                if((uint)tile>=N)throw new ArgumentException("line-packed-field");S(0x52c2550+tile*2,generation);
                if(tile==destination){I(Root+0x155f68,count);return 1;}
                if(mode!=0&&B(0x51d75f0+tile)!=0){I(Root+0x155f68,count);return 0;}
                int direction=ty<sy?(tx>sx?1:tx<sx?7:0):ty>sy?(tx>sx?3:tx<sx?5:4):tx>sx?2:tx<sx?6:-1;
                if(direction<0||(B(0x51890d0+tile)&(1<<direction))==0){I(Root+0x155f68,count);return 0;}
                tile=checked(tile+I(0x405edb0+(sy*8+direction)*4));sx=unchecked(sx+I(0x2d2e50+direction*8));sy=unchecked(sy+I(0x2d2e54+direction*8));
                if((uint)tile>=N)throw new ArgumentException("line-next-packed-field");
                if(!relaxedSurface&&(Flag(tile)&0x10000100)!=0&&(Flag(tile)&2)==0)
                {int building=Building(tile);if(building==0||I(0x2e68d0+S(BuildingRecord(building)+0x12e)*4)==0)return 0;}
                int at=count/2;if((uint)at>=path.Length)throw new ArgumentException("line-output-capacity");
                if((count&1)==0)path[at]=(byte)direction;else path[at]|=(byte)(direction<<4);count=unchecked(count+1);
            }
            throw new ArgumentException("line-step-bound");
        }
        // Complete111330 selection on copied candidate and own unit inputs;
        // this isolated helper does not imply a replay of the military callers.
        internal int SelectStructureTask(int unit)
        {
            int u=UnitRecord(unit),actor=S(u+0x6ee),shift=actor*0x177bc;if(actor<1||actor>8)throw new InvalidOperationException("Unknown:selector-player");
            int index=-1,tries=0;bool building=false,choiceBuilding=false;
            while(true)
            {
                int best=1000;
                for(int n=0;n<I(0x2ea70e4+shift);n++)
                {
                    if(n>=1000)throw new InvalidOperationException("Unknown:selector-unit-table-capacity");int r=0x2ea70ec+shift+n*16,weight=I(r+8);if(weight>=9999||I(r+12)!=0)continue;
                    int tile=I(r+4),y=S(0x3aae2a4+tile*2),x=tile-I(0x402ff2c+y*12),score=unchecked(Math.Max(Math.Abs(S(u+0x71c)-x),Math.Abs(S(u+0x71e)-y))+weight*8);
                    if(score<best){I(0x2e76f10,x);I(0x2e76f14,y);I(0x2e76f18,I(r));best=score;index=n;}choiceBuilding=building;
                }
                for(int n=0;n<I(0x2eb2cc8+shift);n++)
                {
                    if(n>=1000)throw new InvalidOperationException("Unknown:selector-building-table-capacity");int r=0x2eb2cd0+shift+n*16;if(I(r+8)>=5999||I(r+12)!=0)continue;
                    int tile=I(r+4),y=S(0x3aae2a4+tile*2),x=tile-I(0x402ff2c+y*12),metric=Math.Max(Math.Abs(S(u+0x71c)-x),Math.Abs(S(u+0x71e)-y));
                    if(unchecked(metric+5)<best){I(0x2e76f10,x);I(0x2e76f14,y);I(0x2e76f18,I(r));building=true;best=metric;index=n;}choiceBuilding=building;
                }
                if(index>=0){int r=(choiceBuilding?0x2eb2cd0:0x2ea70ec)+shift+index*16;I(r+12,unit);return I(r+4);}
                for(int n=0;n<I(0x2eb2cc8+shift);n++){if(n>=1000)throw new InvalidOperationException("Unknown:selector-reset-capacity");I(0x2eb2cdc+shift+n*16,0);}
                for(int n=0;n<I(0x2ea70e4+shift);n++){if(n>=1000)throw new InvalidOperationException("Unknown:selector-reset-capacity");I(0x2ea70f8+shift+n*16,0);}
                if(++tries>1)return 0;
            }
        }
        internal void CopyInputsTo(Action<int,byte[]> destination){foreach(var s in segments)destination(s.Start,(byte[])s.Data.Clone());}
        // Complete positive196280/F4930 same-region, mode0 branch. Other branches
        // explicitly require their own continuation instead of guessed results.
        internal int OwnSameRegionCommand(int unit,int tx,int ty,int unknown,byte[] path)
        {
            int u=UnitRecord(unit),tile=I(u+0x72c),row=S(0x3aae2a4+tile*2),x=tile-I(0x402ff2c+row*12);
            if(unknown!=0||x!=S(u+0x71c)||row!=S(u+0x71e))throw new InvalidOperationException("Unknown:command-mode-or-coordinate-repair");
            if((uint)tx>=800||(uint)ty>=800||B(0x3a11ea4+ty*800+tx)==0)throw new InvalidOperationException("Unknown:command-invalid-destination-branch");
            int sx=S(u+(S(u+0x74e)==0&&S(u+0x750)==8?0x71c:0x738)),sy=S(u+(S(u+0x74e)==0&&S(u+0x750)==8?0x71e:0x73a));
            int dest=I(0x402ff2c+ty*12)+tx,type=S(u+0x6e6),profile=I(0x322540+type*4);
            if(Component(I(0x402ff2c+sy*12)+sx)!=Component(dest)||(Flag(dest)&0x30)!=0||profile==0&&(Flag(dest)&0x10000100)!=0||((uint)Flag(tile)>>30&1)!=0||I(Root+0x84)!=0||I(Root+0x88)!=0||S(u+0x8fc)==0||S(u+0x6e8)!=0||sx==tx&&sy==ty)throw new InvalidOperationException("Unknown:command-other-region-profile-or-zero-distance-branch");
            S(u+0x8ec,0);S(u+0x744,tx);S(u+0x746,ty);S(u+0xa4c,0);S(u+0x900,Component(dest));
            Replace(0x67e8400+0x658,Bytes(u+0x658,0x490));Array.Clear(path,0,path.Length);I(Root+8,sx);I(Root+12,sy);I(Root+16,tx);I(Root+20,ty);I(Root+0x7c,1);I(Root+0x155f68,0);
            if(CommandLine(sx,sy,tx,ty,path,profile!=0)!=0)Inc(Root+0xa8);
            else
            {
                Inc(Root+0xac);I(Root+0x7c,0);I(Root+0x155f68,0);int dx=sx<=tx?tx-sx:sx-tx,dy=sy<=ty?ty-sy:sy-ty;
                int radius=unchecked(Math.Max(dx,dy)+4),limit=unchecked(radius*radius*2);
                if(CommandFlood(sx,sy,tx,ty,limit,0,true)==0&&CommandFlood(sx,sy,tx,ty,400000,0,false)==0)throw new InvalidOperationException("Unknown:command-NoRoute-cleanup-required");
                CommandReconstruct(sx,sy,tx,ty,path);if(profile==0&&I(Root+0x94)!=0)throw new InvalidOperationException("Unknown:command-obstacle-DB650-continuation");
            }
            int count=I(Root+0x155f68);I(Root+0x80,0);S(u+0x754,count);S(u+0x74e,2);S(u+0x752,0);S(u+0x720,tx);S(u+0x722,ty);I(u+0x730,dest);S(u+0x726,S(u+0x71e));S(u+0x724,S(u+0x71c));I(u+0x734,I(0x402ff2c+S(u+0x71e)*12)+S(u+0x71c));S(u+0x9c8,I(Root+0x84)!=0?1:0);I(Root+0x84,0);I(Root+0x88,0);I(u+0xad0,0);I(u+0xad4,0);return 1;
        }
        internal int CommandFlood(int sx,int sy,int tx,int ty,int limit,int continuation,bool blocked)
        {
            if((uint)sx>=800||(uint)sy>=800||B(0x3a11ea4+sy*800+sx)==0)return 0;
            if(tx!=-1&&((uint)tx>=800||(uint)ty>=800||B(0x3a11ea4+ty*800+tx)==0)&&limit>500)limit=500;
            int origin=I(0x402ff2c+sy*12)+sx,goal=tx==-1?0:checked(I(0x402ff2c+ty*12)+tx);
            int gen=I(Root+4),head,tail,processed;
            if(continuation==0){gen=unchecked(gen+1);I(Root+4,gen);if(gen>32000){gen=1;I(Root+4,1);Clear(0x52c2550,N*2);}head=0;tail=1;processed=0;}
            else{head=I(Root+0x155f3c);tail=I(Root+0x155f44);processed=I(Root+0x155f58);}
            I(Root+0x155f6c,origin);S(Root+0x28f3ec,sy);S(0x5225b10+origin*2,1);S(0x52c2550+origin*2,gen);
            int[] order={6,2,0,7,1,4,5,3};int result=0;
            while(head!=tail&&processed<limit)
            {
                if((uint)head>=N||(uint)tail>N)throw new InvalidOperationException("Unknown:flood-queue-bound");
                int tile=I(Root+0x155f6c+head*4),row=S(Root+0x28f3ec+head*2);processed=unchecked(processed+1);
                if(tile==goal){result=1;break;}
                if((uint)tile>=N||(uint)row>=800)throw new InvalidOperationException("Unknown:flood-packed-field");
                int distance=S(0x5225b10+tile*2),mask=B(0x51890d0+tile);
                foreach(int dir in order)
                {
                    if((mask&(1<<dir))==0)continue;
                    int next=checked(tile+I(0x405edb0+(row*8+dir)*4));
                    if((uint)next>=N)throw new InvalidOperationException("Unknown:flood-edge-outside-copy");
                    if(S(0x52c2550+next*2)==unchecked((short)gen)||blocked&&B(0x51d75f0+next)!=0)continue;
                    if((uint)tail>=N)throw new InvalidOperationException("Unknown:flood-queue-capacity");
                    S(0x5225b10+next*2,unchecked(distance+((dir&1)==0?1:2)));S(0x52c2550+next*2,gen);
                    I(Root+0x155f6c+tail*4,next);S(Root+0x28f3ec+tail*2,row+VirtualBridgeMap.Dy[dir]);tail++;
                }
                head++;
            }
            I(Root+0x155f3c,head);I(Root+0x155f44,tail);I(Root+0x155f58,processed);return result;
        }
        internal void CommandReconstruct(int sx,int sy,int tx,int ty,byte[] path)
        {
            if((uint)sx>=800||(uint)sy>=800||(uint)tx>=800||(uint)ty>=800||B(0x3a11ea4+sy*800+sx)==0||B(0x3a11ea4+ty*800+tx)==0)return;
            int origin=I(0x402ff2c+sy*12)+sx,tile=I(0x402ff2c+ty*12)+tx,row=ty,count=I(Root+0x155f68),distance=S(0x5225b10+tile*2),stamp=S(0x52c2550+tile*2);
            I(Root+0x94,0);
            while(tile!=origin&&count<2000)
            {
                int chosen=8,next=0,best=distance,mask=B(0x51890d0+tile);
                for(int dir=0;dir<8;dir++)
                {
                    if((B(0x312620+dir)&mask)==0)continue;int adjacent=checked(tile+I(0x405edb0+(row*8+dir)*4));
                    if((uint)adjacent>=N)throw new InvalidOperationException("Unknown:reconstruction-edge-outside-copy");
                    int candidate=S(0x5225b10+adjacent*2);
                    if(S(0x52c2550+adjacent*2)==stamp&&candidate>=best-2&&candidate<best){best=candidate;next=adjacent;chosen=dir;}
                }
                if(chosen==8)throw new InvalidOperationException("Unknown:E1640-native-repair-continuation-required");
                row=unchecked(row+I(0x2d2e54+chosen*8));if((uint)row>=800)throw new InvalidOperationException("Unknown:reconstruction-row");
                if((Flag(next)&0x10000100)!=0&&(Flag(next)&2)==0){int building=Building(next);if(building==0||I(0x2e68d0+S(BuildingRecord(building)+0x12e)*4)==0)Inc(Root+0x94);}
                int direction=(chosen+4)&7,at=count/2;if((uint)at>=path.Length)throw new InvalidOperationException("Unknown:reconstruction-bank-capacity");
                if((count&1)==0)path[at]=(byte)direction;else path[at]=(byte)((path[at]&15)|(direction<<4));count++;I(Root+0x155f68,count);tile=next;distance=best;
            }
            // Complete E4E90: reverse nibbles, preserving unused high nibble.
            for(int left=0,right=count-1;left<right;left++,right--)
            {
                int a=(path[left/2]>>((left&1)*4))&15,b=(path[right/2]>>((right&1)*4))&15;
                path[left/2]=(byte)((path[left/2]&~(15<<((left&1)*4)))|(b<<((left&1)*4)));
                path[right/2]=(byte)((path[right/2]&~(15<<((right&1)*4)))|(a<<((right&1)*4)));
            }
        }
        internal void SelectAndUnitTargets(int attacker,int target,int[] effectiveCounts)
        {
            int tile=I(0x379d968+attacker*0x583c),region=Component(tile);
            if((uint)region>=effectiveCounts.Length)throw new InvalidOperationException("Unknown:selection-component");
            I(0x2e97eac,1);I(0x2e97ea8,Seed(tile));I(0x2e97d10,region);I(0x2e9ca14,effectiveCounts[region]);I(0x2e97d14,Component(I(0x379afb0+target*0x583c)));
            UnitTargetsOnly(attacker);
        }
        internal void UnitTargetsOnly(int attacker)
        {
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
