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

}
