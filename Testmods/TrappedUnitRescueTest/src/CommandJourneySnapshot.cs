using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // Separate command intent: this never invents a worker job or goods payload.
    // Machine fields are tied to NativeRelocation.NativeHash. Current AI101 bodies,
    // 118660/11D370 (group identity),193DE0 and1946A0 (contact owner) define this set.
    internal sealed class CommandJourneySnapshot
    {
        private static readonly WorkerJourneyField[] Common={
            W(0x6E4,"alive"),W(0x6E6,"type"),W(0x6EE,"owner"),D(0x6F0,"unit-global"),
            W(0x918,"ai"),W(0x91A,"resume-ai"),W(0x930,"tribe-id"),D(0x93C,"unit-tribe-global"),
            W(0x934,"attack-move-x"),W(0x936,"attack-move-y"),W(0x956,"work-mode"),
            W(0x958,"objective-role"),W(0x95A,"objective-role2"),W(0x9F4,"last-command"),
            W(0x996,"melee-adversary"),D(0x998,"melee-adversary-global"),
            W(0x99C,"ranged-adversary"),D(0x6F8,"ranged-adversary-global"),
            W(0x9F6,"context-unit"),D(0x9F8,"context-unit-global"),D(0xA00,"context-building-tile"),
            W(0xA40,"context-x"),W(0xA42,"context-y"),W(0xA82,"tribe-role"),W(0xA86,"native-role-context"),
            W(0xA9E,"group-order-capability")
        };
        private static readonly WorkerJourneyField[] Crew={
            W(0x6E8,"native-control-input"),W(0x98E,"building-target-id"),
            W(0x96C,"crew1"),W(0x96E,"crew2"),W(0x970,"crew3"),W(0x972,"crew4"),
            D(0x974,"crew1-global"),D(0x978,"crew2-global"),D(0x97C,"crew3-global"),D(0x980,"crew4-global"),
            W(0xA0C,"crew-count")
        };
        private static readonly WorkerJourneyField[] Plain=Common;
        private static readonly WorkerJourneyField[] Siege=Join(Common,Crew);
        private static readonly WorkerJourneyField[] Tower=Join(Siege,new[]{D(0xA10,"wall-approach-tile")});
        private static readonly WorkerJourneyField[] Engineer=Join(Common,new[]{W(0x9E0,"engineer-payload"),W(0xAA4,"engineer-payload-state")});
        private static readonly WorkerJourneyField[] Demolisher=Join(Common,new[]{W(0x748,"deferred-goal-x"),W(0x74A,"deferred-goal-y")});
        private readonly uint[] values;
        internal readonly eChimps UnitType;
        internal readonly int AIState;
        private CommandJourneySnapshot(eChimps type,int state,uint[] values){UnitType=type;AIState=state;this.values=values;}
        private static WorkerJourneyField W(int at,string name)=>new WorkerJourneyField(at,2,name);
        private static WorkerJourneyField D(int at,string name)=>new WorkerJourneyField(at,4,name);
        private static WorkerJourneyField[] Join(WorkerJourneyField[] first,WorkerJourneyField[] second)
        {var result=new WorkerJourneyField[first.Length+second.Length];Array.Copy(first,result,first.Length);Array.Copy(second,0,result,first.Length,second.Length);return result;}
        private static WorkerJourneyField[] Fields(eChimps type)=>type==eChimps.CHIMP_TYPE_SIEGE_TOWER?Tower:
            CommandResumePolicy.IsSiege(type)?Siege:type==eChimps.CHIMP_TYPE_ENGINEER?Engineer:
            type==eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER?Demolisher:Plain;
        // Type admission and functional phase admission are intentionally distinct.
        // AI0/1 autonomous selection is still audited separately before broad admission.
        internal static bool IsSupportedPhase(eChimps type,int state)=>CommandResumePolicy.IsKnownType(type) &&
            type!=eChimps.CHIMP_TYPE_TREBUCHET && (state==101 || RequiredCrewCount(type)>0 && (state==0 || state==1));
        internal static int RequiredCrewCount(eChimps type)
        {
            switch(type)
            {
                case eChimps.CHIMP_TYPE_CATAPULT:case eChimps.CHIMP_TYPE_ARAB_BALLISTA:return 2;
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:case eChimps.CHIMP_TYPE_BATTERING_RAM:return 4;
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:return 1;
                default:return 0;
            }
        }
        internal int FieldCount=>Fields(UnitType).Length;
        internal WorkerJourneyField Field(int index)=>Fields(UnitType)[index];
        internal uint[] CopyValues()=>(uint[])values.Clone();
        internal static bool TryCapture(eChimps type,int state,Func<int,ushort> word,Func<int,uint> dword,out CommandJourneySnapshot snapshot)
        {
            snapshot=null;if(!IsSupportedPhase(type,state) || word==null || dword==null)return false;
            try
            {
                var schema=Fields(type);var data=new uint[schema.Length];
                for(int i=0;i<data.Length;i++)data[i]=schema[i].Read(word,dword);
                var candidate=new CommandJourneySnapshot(type,state,data);
                if(!candidate.Valid())return false;snapshot=candidate;return true;
            }
            catch{return false;}
        }
        private bool Valid()
        {
            if(!IsSupportedPhase(UnitType,AIState) || !TryGetValue(0x6E4,out var alive) || alive!=(uint)AliveState.IsAlive ||
                !TryGetValue(0x6E6,out var type) || type!=(uint)UnitType || !TryGetValue(0x918,out var state) || state!=(uint)AIState ||
                !TryGetValue(0xA9E,out var capability) || capability!=1 || !TryGetValue(0x956,out var mode) || mode==6 || mode==7)return false;
            if(UnitType==eChimps.CHIMP_TYPE_ENGINEER &&
                (!TryGetValue(0x9E0,out var payload) || payload!=0 || !TryGetValue(0xAA4,out var payloadState) || payloadState!=0))return false;
            if(CommandResumePolicy.IsSiege(UnitType))
            {
                TryGetValue(0xA0C,out var count);if(count!=(uint)RequiredCrewCount(UnitType))return false;
                for(int i=0;i<4;i++)
                {
                    TryGetValue(0x96C+i*2,out var id);if((id!=0)!=(i<count) || id>NativeRelocation.MaximumUnitId)return false;
                    for(int j=0;j<i;j++){TryGetValue(0x96C+j*2,out var other);if(id!=0 && id==other)return false;}
                }
            }
            return true;
        }
        internal bool TryGetValue(int offset,out uint value)
        {for(int i=0;i<values.Length;i++)if(Field(i).NativeOffset==offset){value=values[i];return true;}value=0;return false;}
        internal bool Matches(eChimps type,int state,Func<int,ushort> word,Func<int,uint> dword)
        {
            if(type!=UnitType || state!=AIState || word==null || dword==null)return false;
            try{for(int i=0;i<values.Length;i++)if(Field(i).Read(word,dword)!=values[i])return false;return true;}catch{return false;}
        }
        internal static bool TryRestoreKnownSnapshot(eChimps type,int state,int[] offsets,int[] widths,uint[] source,out CommandJourneySnapshot snapshot)
        {
            snapshot=null;if(!IsSupportedPhase(type,state) || offsets==null || widths==null || source==null)return false;
            var schema=Fields(type);if(offsets.Length!=schema.Length || widths.Length!=schema.Length || source.Length!=schema.Length)return false;
            for(int i=0;i<schema.Length;i++)if(offsets[i]!=schema[i].NativeOffset || widths[i]!=schema[i].Width ||
                (widths[i]==2 && source[i]>ushort.MaxValue))return false;
            var candidate=new CommandJourneySnapshot(type,state,(uint[])source.Clone());if(!candidate.Valid())return false;
            snapshot=candidate;return true;
        }
    }
}
