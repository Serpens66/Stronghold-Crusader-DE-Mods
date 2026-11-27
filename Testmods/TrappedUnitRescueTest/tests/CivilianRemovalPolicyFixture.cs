using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest.Tests
{
    internal static unsafe class CivilianRemovalPolicyFixture
    {
        private static int checks;
        private static void Require(bool condition,string message)
        { checks++; if(!condition)throw new InvalidOperationException("CivilianRemovalPolicyFixture: "+message); }
        private static GameUnit Ordinary(eChimps type=eChimps.CHIMP_TYPE_PEASANT) => new GameUnit
        { r_UnitChimp=type,r_AliveState=AliveState.IsAlive,r_CurrentHealth=100,r_MovementSubstep=8 };
        private static void Word(ref GameUnit unit,int nativeOffset,ushort value)
        { fixed(GameUnit* pointer=&unit)*(ushort*)((byte*)pointer+nativeOffset-0x65C)=value; }
        private static string Exclusion(GameUnit unit,bool step=false,bool midpoint=false) =>
            CivilianRemovalPolicy.GetExclusion(&unit,step,midpoint);

        internal static void Run()
        {
            checks=0;
            var expected=new HashSet<eChimps>
            {
                eChimps.CHIMP_TYPE_PEASANT,eChimps.CHIMP_TYPE_WOODCUTTER,eChimps.CHIMP_TYPE_FLETCHER,
                eChimps.CHIMP_TYPE_HUNTER,eChimps.CHIMP_TYPE_QUARRY_MASON,eChimps.CHIMP_TYPE_QUARRY_GRUNT,
                eChimps.CHIMP_TYPE_PITCHMAN,eChimps.CHIMP_TYPE_FARMER_WHEAT,eChimps.CHIMP_TYPE_FARMER_HOPS,
                eChimps.CHIMP_TYPE_FARMER_APPLE,eChimps.CHIMP_TYPE_FARMER_CATTLE,eChimps.CHIMP_TYPE_MILLER,
                eChimps.CHIMP_TYPE_BAKER,eChimps.CHIMP_TYPE_BREWER,eChimps.CHIMP_TYPE_POLETURNER,
                eChimps.CHIMP_TYPE_BLACKSMITH,eChimps.CHIMP_TYPE_ARMOURER,eChimps.CHIMP_TYPE_TANNER,
                eChimps.CHIMP_TYPE_MINER1,eChimps.CHIMP_TYPE_MINER2,eChimps.CHIMP_TYPE_PRIEST,
                eChimps.CHIMP_TYPE_HEALER,eChimps.CHIMP_TYPE_INNKEEPER,eChimps.CHIMP_TYPE_FIREMAN
            };
            Require(expected.Count==24,"explicit human employment scope");
            foreach(eChimps type in Enum.GetValues(typeof(eChimps)))
            {
                bool allowed=expected.Contains(type);
                Require(CivilianRemovalPolicy.IsRemovableType(type)==allowed,"complete SE type domain "+type);
                Require((Exclusion(Ordinary(type))==null)==allowed,"actual pointer type admission "+type);
            }
            Require(!CivilianRemovalPolicy.IsRemovableType((eChimps)ushort.MaxValue),"unknown type fails closed");
            Require(CivilianRemovalPolicy.GetExclusion(null,false)=="missing-unit","null pointer rejected");
            foreach(eChimps type in expected)
            {
                GameUnit unit=Ordinary(type);
                foreach(ushort flags in new ushort[]{0,2,4})
                { unit.r_PathPlanStateBitFlags=flags;Require(Exclusion(unit)==null,"ordinary route state "+type+"/"+flags); }
                foreach(ushort state in new ushort[]{0,1,2,107})
                { unit.r_AIState=state;Require(Exclusion(unit)==null,"bounded AI admission "+type+"/"+state); }
                foreach(ushort state in new ushort[]{108,109,110,111,113,126,127,128,129,ushort.MaxValue})
                { unit.r_AIState=state;Require(Exclusion(unit)=="special-ai-state","terminal/transform/unknown AI "+type+"/"+state); }
            }
            for(int life=0;life<=5;life++)
            { GameUnit unit=Ordinary();unit.r_AliveState=(AliveState)life;Require((Exclusion(unit)==null)==(life==(int)AliveState.IsAlive),"native life admission "+life); }
            GameUnit changed=Ordinary();changed.r_CurrentHealth=0;
            Require(Exclusion(changed)=="dying","zero health excluded");
            foreach(int offset in new[]{0x8F8,0x6EC,0x9C8,0x9B0,0x8EC,0x8F6})
            {
                changed=Ordinary();Word(ref changed,offset,1);
                Require(Exclusion(changed)!=null,"special word excluded "+offset);
                Require(Exclusion(changed,true,true)!=null,"midpoint never bypasses special word "+offset);
            }
            foreach(ushort flags in new ushort[]{1,3,5,6,ushort.MaxValue})
            { changed=Ordinary();changed.r_PathPlanStateBitFlags=flags;Require(Exclusion(changed,true,true)=="special-path-state","special route state "+flags); }
            for(ushort substep=0;substep<8;substep++)
            {
                changed=Ordinary();changed.r_MovementSubstep=substep;changed.r_PathPlanStateBitFlags=2;
                changed.r_CurrentTilePositionX=10;changed.r_CurrentTilePositionY=10;
                changed.r_NextTilePositionX2=11;changed.r_NextTilePositionY2=10;
                Require(Exclusion(changed)=="step-in-progress","step requires capture opt-in "+substep);
                Require(Exclusion(changed,true)==null,"actual HasOrdinaryStep accepted "+substep);
                changed.r_NextTilePositionX2=12;
                Require(Exclusion(changed,true)=="step-in-progress","second/unproved step rejected "+substep);
                changed.r_NextTilePositionX2=10;
                Require(Exclusion(changed,true)=="step-in-progress","same tile not ordinary step "+substep);
                Require(Exclusion(changed,false,true)==null,"caller-proven captured moat midpoint exception "+substep);
                changed.r_AIState=109;
                Require(Exclusion(changed,true,true)=="special-ai-state","midpoint cannot delete recruit "+substep);
            }
            changed=Ordinary();changed.r_MovementSubstep=9;
            Require(Exclusion(changed,true)=="step-in-progress","unknown substep excluded without captured proof");
            // Real employment/recruitment stores +922 before conversion and leaves
            // it after 195D10. Neither a paid recruit nor a stale worker field is
            // classified solely from its current Peasant/worker type.
            foreach(eChimps target in new[]{eChimps.CHIMP_TYPE_KNIGHT,eChimps.CHIMP_TYPE_MONK,
                eChimps.CHIMP_TYPE_ENGINEER,eChimps.CHIMP_TYPE_BEDOUIN_HEALER,eChimps.CHIMP_TYPE_MINER2})
            {
                changed=Ordinary();Word(ref changed,0x922,(ushort)target);changed.r_AIState=109;
                Require(Exclusion(changed)=="special-ai-state","pending target protected "+target);
                changed.r_AliveState=(AliveState)4;changed.r_AIState=0;
                Require(Exclusion(changed)=="not-alive","same-slot transform protected "+target);
                changed=Ordinary(eChimps.CHIMP_TYPE_MILLER);Word(ref changed,0x922,(ushort)target);
                Require(Exclusion(changed)==null,"completed worker stale target not a blanket veto "+target);
            }
            // Classification is read-only, including external-authority words.
            changed=Ordinary(eChimps.CHIMP_TYPE_TANNER);Word(ref changed,0x922,(ushort)eChimps.CHIMP_TYPE_TANNER);
            Word(ref changed,0x9D4,3);changed.N000000DB=0;
            byte* bytes=(byte*)&changed;var before=new byte[sizeof(GameUnit)];
            for(int i=0;i<before.Length;i++)before[i]=bytes[i];
            Require(CivilianRemovalPolicy.GetExclusion(&changed,true)==null,"no job/cargo/group-order gate introduced");
            for(int i=0;i<before.Length;i++)Require(before[i]==bytes[i],"policy leaves native byte unchanged "+i);
            Console.WriteLine("PASS CivilianRemovalPolicyFixture "+checks+" checks");
        }
    }
}
