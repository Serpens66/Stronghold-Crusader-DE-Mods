using System;
namespace EnemyBridgePathTest
{
    internal readonly struct VirtualPlanningCall
    {
        internal readonly int Attacker,Target,Bank,Radius,CandidateDistance,OriginComponent;
        internal readonly bool SeedShortLimit;
        internal VirtualPlanningCall(int attacker,int target,int mode,int targetStrength,int origin,bool keepReachable)
        {
            if(attacker<1||attacker>8||target<1||target>8||origin<0||origin>999)throw new ArgumentException("Unresolved planning players/component");
            Attacker=attacker;Target=Bank=target;Radius=targetStrength>120?98:targetStrength>90?70:50;
            CandidateDistance=mode!=0?Radius:0;OriginComponent=origin;SeedShortLimit=keepReachable;
        }
    }
    internal readonly struct VirtualTargetSelection
    {
        internal readonly int SeedMode,Seed,Region,Count,TargetCastleRegion;
        internal VirtualTargetSelection(VirtualPlanningInput input,VirtualPlanningState state,int targetTile,int targetCastleTile,int[] counts)
        {
            if((uint)targetTile>=(uint)input.Components.Length||(uint)targetCastleTile>=(uint)input.Components.Length)throw new ArgumentException("Unknown selection tile");
            SeedMode=1;Seed=state.Seeds[targetTile];Region=(short)input.Components[targetTile];TargetCastleRegion=(short)input.Components[targetCastleTile];
            if((uint)Region>=(uint)counts.Length)throw new ArgumentException("Unknown component count");Count=counts[Region];
        }
    }
    internal static class VirtualPlanningCaller
    {
        // 2D250: target's bank, cleared seeds, CF360 result as R8, then D9190.
        internal static bool Run(VirtualPlanningInput input,VirtualPlanningState state,VirtualPlanningCall call,int targetCastleTile,bool targetActive,Func<int,int,int?> regions)
        {
            Array.Clear(state.Seeds,0,state.Seeds.Length);
            return VirtualBridgePlanning.Seed(input,state,targetCastleTile,targetActive,call.SeedShortLimit)&&
                VirtualBridgePlanning.Distance(input,state,110,call.CandidateDistance,call.OriginComponent,regions);
        }
    }
    // Native 1126B0 and the five 2C480 availability consumers. Byte copies only;
    // never constructs a large interop value or accesses current game state.
    internal static class VirtualCandidateConsumers
    {
        internal const int Root=0x2E76F10,Bytes=0x11AF14;
        internal static int[] UnitTargets(byte[] manager,int player,out int count)
        {
            if(player<1||player>8||manager.Length<0x65c)throw new ArgumentException("unit-consumer-input");
            int limit=BitConverter.ToInt32(manager,0);if(limit<1||limit>10000||manager.Length!=0x65c+limit*0x490)throw new ArgumentException("unit-consumer-extent");
            var result=new int[4000];count=0;
            for(int id=1;id<limit;id++)
            {
                int a=0x65c+id*0x490;
                if(BitConverter.ToInt16(manager,a+0x88)==2&&BitConverter.ToInt16(manager,a+0x2a0)!=0&&BitConverter.ToInt16(manager,a+0x92)==player&&BitConverter.ToInt32(manager,a+0x3a4)!=0&&BitConverter.ToInt16(manager,a+0x8a)!=71)
                {result[count++]=BitConverter.ToInt32(manager,a+0x3a4);if(count==4000)break;}
            }
            return result;
        }
        internal static byte[] Availability(byte[] before,int player,int ignoreReservations)
        {
            if(before.Length!=Bytes||player<1||player>8)throw new ArgumentException("candidate-consumer-input");
            byte[] after=(byte[])before.Clone();
            int[] starts={0x2EAAF90,0x2EAEE2C,0x2EB2CC8,0x2EBAA00,0x2EB6B64};
            for(int kind=0;kind<starts.Length;kind++)
            {
                int a=starts[kind]+player*0x177bc-Root,n=BitConverter.ToInt32(before,a),available=0;
                if(n>1000)throw new ArgumentException("candidate-consumer-capacity");
                for(int q=0;q<n;q++){int row=a+8+q*16;int score=BitConverter.ToInt32(before,row+8),reserved=BitConverter.ToInt32(before,row+12);if((kind==1||score<5999)&&(reserved==0||ignoreReservations!=0))available++;}
                Buffer.BlockCopy(BitConverter.GetBytes(available),0,after,a+4,4);
            }
            return after;
        }
    }

    // Complete 115B10 and 193C80/193D90 over immutable byte copies. The caller
    // supplies the actual game-dependent 70/150 limit or compares both outcomes.
    internal static class VirtualCandidateWeights
    {
        internal static byte[] Run(byte[] before,int attacker,int target,byte[] players,byte[] distances,int bank,byte[] flags,byte[] occupancy,byte[] units,byte[] tileRows,byte[] directionOffsets,int maximum)
        {
            if(before.Length!=VirtualCandidateConsumers.Bytes||players.Length!=9*0x583c||distances.Length!=320800*2||flags.Length!=320800*4||occupancy.Length!=320800*2||tileRows.Length!=320800*2||directionOffsets.Length!=6400*4||attacker<1||attacker>8||target<1||target>8||(maximum!=70&&maximum!=150))throw new ArgumentException("weight-input-extent-or-identity");
            if(units.Length<0x65c)throw new ArgumentException("weight-unit-extent");int limit=BitConverter.ToInt32(units,0);if(limit<1||limit>10000||units.Length!=0x65c+limit*0x490)throw new ArgumentException("weight-unit-extent");
            byte[] result=(byte[])before.Clone();if(BitConverter.ToInt32(players,target*0x583c+0x1a4)==0)return result;
            int shift=attacker*0x177bc-VirtualCandidateConsumers.Root;
            int Get(int absolute)=>BitConverter.ToInt32(result,checked(absolute+shift));
            void Set(int absolute,int value)=>Buffer.BlockCopy(BitConverter.GetBytes(value),0,result,checked(absolute+shift),4);
            int Count(int absolute){int n=Get(absolute);if(n>1000)throw new ArgumentException("weight-candidate-capacity");return n;}
            int Distance(int tile,int requestedBank){if(requestedBank!=bank||(uint)tile>=320800)throw new ArgumentException("unknown-weight-bank-or-tile");return BitConverter.ToInt16(distances,tile*2);}
            bool Blocked(int tile)
            {
                if((uint)tile>=320800)throw new ArgumentException("weight-neighbor-origin");int row=BitConverter.ToInt16(tileRows,tile*2);if((uint)row>=800)throw new ArgumentException("weight-neighbor-row");
                for(int d=0;d<8;d+=2)
                {
                    int neighbor=checked(tile+BitConverter.ToInt32(directionOffsets,(row*8+d)*4));if((uint)neighbor>=320800)throw new ArgumentException("weight-neighbor-tile");
                    if((BitConverter.ToInt32(flags,neighbor*4)&0x4a5015b1)!=0)continue;
                    int id=BitConverter.ToInt16(occupancy,neighbor*2),steps=0;
                    while(id!=0)
                    {
                        if(id<1||id>=limit||++steps>=limit)throw new ArgumentException("weight-unit-list-identity-or-cycle");int a=checked(id*0x490);
                        if(BitConverter.ToInt16(units,a+0x6e6)==29&&BitConverter.ToInt16(units,a+0x918)==3)return true;
                        id=BitConverter.ToInt16(units,a+0x8fa);
                    }
                }
                return false;
            }
            int n=Count(0x2ea70e4),minimum=10000;Set(0x2eaaf7c,minimum);
            for(int i=0;i<n;i++){int distance=Distance(Get(0x2ea70ec+i*16),target);if(distance<minimum){minimum=distance;Set(0x2eaaf7c,minimum);}}
            Set(0x2ea70e8,0);
            for(int i=0;i<n;i++)
            {
                int tile=Get(0x2ea70ec+i*16),distance=Distance(tile,target),score=9999;
                if(!Blocked(tile)&&distance>=1&&minimum<50&&distance<minimum+15){score=distance;Set(0x2ea70e8,Get(0x2ea70e8)+1);}Set(0x2ea70f4+i*16,score);
            }
            int selectedBank=Get(0x2ea70dc);
            foreach(int table in new[]{0x2eaaf90,0x2ebaa00})for(int i=0;i<Count(table);i++)Set(table+16+i*16,Distance(Get(table+8+i*16),selectedBank)>5?9999:0);
            for(int i=0;i<Count(0x2eaee2c);i++){int distance=Distance(Get(0x2eaee34+i*16),selectedBank);Set(0x2eaee3c+i*16,distance<1?100:distance);}
            n=Count(0x2eb2cc8);minimum=10000;Set(0x2eaaf80,minimum);
            for(int i=0;i<n;i++){int distance=Distance(Get(0x2eb2cd0+i*16),selectedBank);if(distance<minimum){minimum=distance;Set(0x2eaaf80,minimum);}}
            for(int i=0;i<n;i++){int distance=Distance(Get(0x2eb2cd0+i*16),selectedBank);Set(0x2eb2cd8+i*16,minimum<11?(distance<11?0:9999):(distance>20?9999:0));}
            for(int i=0;i<Count(0x2eb6b64);i++)
            {
                int distance=Distance(Get(0x2eb6b6c+i*16),selectedBank),score=distance;
                if(distance<1||distance>maximum)score=9999;
                else if(distance>10)score=distance*(distance<16?2:distance<21?3:distance<26?4:distance<51?5:6);
                Set(0x2eb6b74+i*16,score);
            }
            return result;
        }
    }

}
