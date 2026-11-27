using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // Admission only: marking/removal belongs to the qualified recovery tick. No
    // job, cargo, employee or recruitment state is reconstructed by this policy.
    internal static unsafe class CivilianRemovalPolicy
    {
        internal static bool IsRemovableType(eChimps type)
        {
            switch(type)
            {
                case eChimps.CHIMP_TYPE_PEASANT:
                case eChimps.CHIMP_TYPE_WOODCUTTER: case eChimps.CHIMP_TYPE_FLETCHER:
                case eChimps.CHIMP_TYPE_HUNTER: case eChimps.CHIMP_TYPE_QUARRY_MASON:
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT: case eChimps.CHIMP_TYPE_PITCHMAN:
                case eChimps.CHIMP_TYPE_FARMER_WHEAT: case eChimps.CHIMP_TYPE_FARMER_HOPS:
                case eChimps.CHIMP_TYPE_FARMER_APPLE: case eChimps.CHIMP_TYPE_FARMER_CATTLE:
                case eChimps.CHIMP_TYPE_MILLER: case eChimps.CHIMP_TYPE_BAKER:
                case eChimps.CHIMP_TYPE_BREWER: case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_BLACKSMITH: case eChimps.CHIMP_TYPE_ARMOURER:
                case eChimps.CHIMP_TYPE_TANNER: case eChimps.CHIMP_TYPE_MINER1:
                case eChimps.CHIMP_TYPE_MINER2: case eChimps.CHIMP_TYPE_PRIEST:
                case eChimps.CHIMP_TYPE_HEALER: case eChimps.CHIMP_TYPE_INNKEEPER:
                case eChimps.CHIMP_TYPE_FIREMAN:return true;
                // Oxen are tether-allocated animals, not employed Peasants.
                default:return false;
            }
        }

        internal static string GetExclusion(GameUnit* unit,bool includeOrdinaryStep,bool allowCapturedMoatMidpoint=false)
        {
            if(unit==null)return "missing-unit";
            if(!IsRemovableType(unit->r_UnitChimp))return "not-removable-civilian";
            if(unit->r_AliveState!=AliveState.IsAlive)return "not-alive";
            if(unit->r_CurrentHealth<=0 || Word(unit,0x8F8)!=0)return "dying";
            // Conservative first scope, not a claim about every native AI state.
            // This includes 109 recruitment/fade, 110 retirement and death states.
            if(unit->r_AIState>=108)return "special-ai-state";
            if(Word(unit,0x6EC)!=0)return "nonordinary-movement-class";
            if(Word(unit,0x9C8)!=0)return "stored-moat-route-needs-mode-aware-exit-proof";
            if(Word(unit,0x9B0)!=0)return "mounted-or-hidden";
            if(Word(unit,0x8EC)!=0 || Word(unit,0x8F6)!=0)return "portal-or-transport";
            ushort flags=unit->r_PathPlanStateBitFlags;
            if(flags!=0 && flags!=2 && flags!=4)return "special-path-state";
            // The caller may provide this exception only after proving the exact
            // captured original step crossed its newly changed moat midpoint.
            if(unit->r_MovementSubstep!=8 && !allowCapturedMoatMidpoint &&
                (!includeOrdinaryStep || !RescueRuntime.HasOrdinaryStep(unit)))return "step-in-progress";
            // +922 persists after completed employment: requiring zero would
            // reject genuine workers. Pending recruitment is excluded above.
            return null;
        }

        private static ushort Word(GameUnit* unit,int nativeOffset) =>
            *(ushort*)((byte*)unit+nativeOffset-0x65C);
    }
}
