using System;
using System.Collections.Generic;
namespace EnemyBridgePathTest
{
    // Full EDF30 and DA020 logic over bounded copied segments only. A building
    // selection, approach enumeration and executed attack are separate evidence.
    internal sealed partial class VirtualCandidateBuilder
    {
        internal int FindAttackObject(int unit,int maximum)
        {
            if(unit<1||unit>9999||maximum<0)throw new ArgumentException("structure-selector-context");
            int record=UnitRecord(unit),x=S(record+0x71c),y=S(record+0x71e);
            Inc(Root+0xa0);I(Root+0x48,0);I(Root+0x4c,0);I(Root+0x44,0);
            if((uint)x>=800||(uint)y>=800||B(0x3a11ea4+y*800+x)==0)return 0;
            Inc(Root+4);if(I(Root+4)>32000){I(Root+4,1);Clear(0x52c2550,N*2);}
            I(Root+0x155f38,1);I(Root+0x155f3c,0);I(Root+0x155f44,1);
            S(Root+0x28f3ec,y);S(Root+0x32be2c,x);int first=I(0x402ff2c+y*12)+x;
            I(Root+0x155f6c,first);S(0x5225b10+first*2,1);S(0x52c2550+first*2,I(Root+4));
            if((Flag(first)&0x30)!=0)return 0;
            int player=S(record+0x6ee);if(player<0||player>8)throw new InvalidOperationException("Unknown:selector-player");
            int budget=N*2;
            while(I(Root+0x155f3c)!=I(Root+0x155f44))
            {
                if(--budget<0)throw new InvalidOperationException("Unknown:selector-budget");
                int head=I(Root+0x155f3c),tile=I(Root+0x155f6c+head*4);
                if((uint)tile>=N)return 0;
                int level=S(0x5225b10+tile*2),cy=S(Root+0x28f3ec+head*2),cx=S(Root+0x32be2c+head*2);I(Root+0x155f38,level);if(level>maximum)return 0;
                for(int d=0;d<8;d+=2)
                {
                    int next=tile+I(0x405edb0+(cy*8+d)*4);if((uint)next>=N)throw new InvalidOperationException("Unknown:selector-neighbor");
                    if(S(0x52c2550+next*2)==I(Root+4))continue;
                    int flags=Flag(next),building=Building(next);
                    if(building==0)
                    {
                        if((flags&0x100)!=0&&(I(0x8574b90)==0||I(0x37ed4c4)==0))
                        {int owner=B(0x4e79d90+next)&7;if(I(0x37edf40+owner*4)!=I(0x37edf3c+player*4)){I(Root+0x4c,next);return next;}}
                    }
                    else
                    {
                        int br=BuildingRecord(building),owner=S(br+0x132);if(owner<0||owner>8)throw new InvalidOperationException("Unknown:selector-building-owner");
                        if(I(0x37edf3c+player*4)!=I(0x37edf3c+owner*4))
                        {
                            int kind=S(br+0x12e);bool excluded=kind==10||kind==40||kind==41||kind==42||kind==51||kind==52||kind==53||kind==55||kind==56||kind==57||kind==58||kind==59||kind==67||kind==71||kind==72||kind==73;
                            if(!excluded&&AttackableBuilding(building)){I(Root+0x4c,next);return next;}
                        }
                    }
                    if((flags&0x30)!=0)continue;
                    S(0x5225b10+next*2,I(Root+0x155f38)+1);S(0x52c2550+next*2,I(Root+4));int tail=I(Root+0x155f44);
                    S(Root+0x32be2c+tail*2,cx+S(0x2d2e50+d*8));S(Root+0x28f3ec+tail*2,cy+S(0x2d2e54+d*8));I(Root+0x155f6c+tail*4,next);I(Root+0x155f44,tail+1==N?0:tail+1);
                }
                I(Root+0x155f3c,head+1==N?0:head+1);
            }
            return 0;
        }
        private bool AttackableBuilding(int building)
        {
            if(building==0)return false;int kind=S(BuildingRecord(building)+0x12e);
            if(kind==45||kind==46||kind>=74&&kind<=78)return true;
            if(kind<0||kind>=336)throw new InvalidOperationException("Unknown:attackable-class");
            return I(0x2e6710+kind*4)!=0;
        }
        private bool AssassinsOnly(int tribe)
        {
            if(tribe<1||tribe>=4500)throw new ArgumentException("tribe-id");
            int at=0x7cc6720+tribe*0x688,count=S(at+0x5c),seen=0;
            for(int n=0;n<count;n++)
            {
                int member=0;seen=0;
                if(S(at+0x40)==2)for(int word=0;word<0x271;word++)
                {ushort bits=(ushort)S(at+0x60+word*2);bool found=false;for(int bit=0;bit<16;bit++)if((bits&(1<<bit))!=0&&seen++==n){member=word*16+bit;found=true;break;}if(found)break;}
                int u=UnitRecord(member);if(S(u+0x6e4)==2&&S(u+0x8f8)==0&&S(u+0x6e6)!=73)return false;
            }
            return true;
        }
        internal int[] RingOffset(int radius,int ordinal)
        {
            int[] starts={0,0x2e3fa0,0x2e3fc0,0x2e4000,0x2e4060,0x2e40e0,0x2e4180,0x2e4240,0x2e4320,0x2e4420,0x2e4540,0x2e4680,0x2e47e0,0x2e4960};
            if(radius<1||radius>13||ordinal<0||ordinal>=I(0x2e8fd8+radius*4))throw new ArgumentException("ring-contract");
            // C0270 uses size=1 and expansion=0 in DA020.
            int x=I(starts[radius]+ordinal*8),y=I(starts[radius]+ordinal*8+4);I(0x67e8380,x);I(0x67e8384,y);return new[]{x,y};
        }
        internal CopiedMeleeApproaches BuildMeleeApproaches(int tribe,int building,int count,int sourceRegion,int control)
        {
            if(building<1||building>4000||control<0||control>8)throw new ArgumentException("approach-context");
            Regions(control);int record=BuildingRecord(building),x=S(record+0x14a),y=S(record+0x14c),size=I(record+0x154),height=S(record+0x148),origin=I(record+0x150);
            if((uint)origin>=N||size<1||size>13)throw new InvalidOperationException("Unknown:approach-origin-or-ring");
            int baseHeight=B(0x4e2b870+origin),maximum=unchecked(count*2);maximum=maximum>=501?500:maximum<50?50:maximum;
            bool assassins=AssassinsOnly(tribe);int primaryHeight=I(0x2e5360+S(record+0x12e)*4)==40?32:16;
            var points=new List<int>();var adjacent=new List<int>();bool advance=true;
            bool Add(int radius,int ordinal,int limit,bool primary)
            {
                advance=true;var offset=RingOffset(radius,ordinal);int xx=unchecked(x+offset[0]),yy=unchecked(y+offset[1]);
                if(!primary&&((uint)xx>=800||(uint)yy>=800||B(0x3a11ea4+yy*800+xx)==0)){advance=false;return false;}
                if((uint)yy>=800)throw new InvalidOperationException("Unknown:primary-ring-row");int tile=unchecked(I(0x402ff2c+yy*12)+xx);if((uint)tile>=N)throw new InvalidOperationException("Unknown:primary-ring-tile");
                if(Math.Abs(baseHeight-B(0x4ddd350+tile))>=32)return false;
                if(Component(tile)!=sourceRegion&&!reachable(sourceRegion,Component(tile)))
                {if(assassins)throw new InvalidOperationException("Unknown:DA020-E2CA0-assassin-surface-fallback");return false;}
                if(Math.Abs(height-B(0x4ddd350+tile))>=limit)return false;
                int contact=0;
                if(primary)for(int d=0;d<4;d++){int dx=I(0x3123b0+d*8),dy=I(0x3123b4+d*8),t=I(0x402ff2c+(yy+dy)*12)+xx+dx;if((Flag(t)&0xf000000)==0&&Building(t)==building){contact=t;break;}}
                int row=Root+0x1b344+points.Count*12;I(row,tile);I(row+4,contact);points.Add(tile);adjacent.Add(contact);return points.Count>=maximum;
            }
            int steps=I(0x2e8fd8+size*4);for(int i=0;i<steps;i++)if(Add(size,i,primaryHeight,true))return new CopiedMeleeApproaches(points.ToArray(),adjacent.ToArray(),false,true);
            int heightLimit=32;for(int radius=size+1;radius<14;radius++,heightLimit+=16){steps=I(0x2e8fd8+radius*4);int ordinal=0;for(int i=0;i<steps;i++){if(Add(radius,ordinal,heightLimit,false))return new CopiedMeleeApproaches(points.ToArray(),adjacent.ToArray(),false,true);if(advance){ordinal++;if(ordinal>=steps)ordinal=0;}}}
            I(Root+0x1b344+points.Count*12,0);I(Root+0x1b348+points.Count*12,0);
            return new CopiedMeleeApproaches(points.ToArray(),adjacent.ToArray(),true,false);
        }
    }
    internal sealed class CopiedMeleeApproaches
    {
        internal readonly int[] Tiles,Contacts;internal readonly bool EnumerationComplete,NativeCapacityReached;
        internal CopiedMeleeApproaches(int[] tiles,int[] contacts,bool complete,bool capped)
        {Tiles=(int[])tiles.Clone();Contacts=(int[])contacts.Clone();EnumerationComplete=complete;NativeCapacityReached=capped;}
        internal VirtualReachability Evaluate(bool coherentTopology,bool completePermissions,Func<int,VirtualReachability> query)
        {if(!coherentTopology||!completePermissions)return VirtualReachability.Unknown;return VirtualMeleeAccess.Evaluate(Tiles,EnumerationComplete,query);}
    }
}
