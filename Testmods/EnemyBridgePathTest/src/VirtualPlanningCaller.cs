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
}
