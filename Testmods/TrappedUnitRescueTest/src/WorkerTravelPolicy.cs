using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest
{
    internal enum WorkerJourneyKind { Unknown, OrdinaryTravel, EntityOrLinkedTravel, ContactOrOther }
    [Flags]
    internal enum WorkerExternalBindings { None=0, Workplace=1, Store=2, Vegetation=4, LinkedEntity=8 }

    internal readonly struct WorkerJourneyRule
    {
        private readonly WorkerRoleContract contract;
        internal readonly int AIState;
        internal readonly WorkerJourneyKind Kind;
        internal WorkerJourneyRule(WorkerRoleContract contract, int state, WorkerJourneyKind kind)
        { this.contract = contract; AIState = state; Kind = kind; }
        internal bool IsKnown => contract != null;
        internal bool CanResumeOrdinary => contract != null && Kind == WorkerJourneyKind.OrdinaryTravel;
        internal eChimps UnitType => contract == null ? eChimps.CHIMP_TYPE_NULL : contract.Type;
        internal int HandlerRva => contract == null ? 0 : contract.HandlerRva;
        internal WorkerExternalBindings RequiredExternalBindings => contract == null ? WorkerExternalBindings.None :
            WorkerTravelPolicy.RequiredBindings(UnitType,AIState,Kind);
        // Woodcutter's unit record stamps its selected tree. Apple records only a tree slot;
        // its owner must capture the vegetation record's identity before mutation separately.
        internal int VegetationSerialNativeOffset => UnitType==eChimps.CHIMP_TYPE_WOODCUTTER ? 0x700 : 0;
        internal int FieldCount => contract == null ? 0 : contract.Fields.Length;
        internal WorkerJourneyField Field(int index) => contract.Fields[index];
        internal bool MayAdvanceWhileWaiting(int index)
        {
            int offset=Field(index).NativeOffset;
            return UnitType==eChimps.CHIMP_TYPE_QUARRY_OX && offset==0x8E6 ||
                (UnitType==eChimps.CHIMP_TYPE_FARMER_WHEAT || UnitType==eChimps.CHIMP_TYPE_FARMER_HOPS) && offset==0x9D8;
        }
    }

    // Native offsets are manager-record-relative, not offsets from the GameUnit* view.
    // NativeRelocation's pinned image/layout gate must precede these injected reads.
    internal readonly struct WorkerJourneyField
    {
        internal readonly int NativeOffset;
        internal readonly int Width;
        internal readonly string Name;
        internal WorkerJourneyField(int offset, int width, string name)
        { NativeOffset = offset; Width = width; Name = name; }
        internal uint Read(Func<int, ushort> word, Func<int, uint> dword)
        {
            if (Width == 4) return dword(NativeOffset);
            if (Width == 2) return word(NativeOffset);
            // Native A47/AA6 are bytes; use an aligned word without adopting the other byte.
            int aligned = NativeOffset & ~1;
            return (uint)(word(aligned) >> ((NativeOffset & 1) * 8)) & 0xff;
        }
    }

    internal sealed class WorkerRoleContract
    {
        internal readonly eChimps Type;
        internal readonly int HandlerRva;
        internal readonly int[] OrdinaryStates, EntityStates;
        internal readonly WorkerJourneyField[] Fields;
        internal WorkerRoleContract(eChimps type, int handler, int[] states, int[] entity, WorkerJourneyField[] fields)
        { Type = type; HandlerRva = handler; OrdinaryStates = states; EntityStates = entity; Fields = fields; }
    }

    internal static class WorkerTravelPolicy
    {
        // Hash-bound AI-state numbers below are the audited machine state contract, not unit IDs.
        // Complete role/state/arrival/cargo/terminal evidence: Audits/TERRAIN-WORKERS.md §6.
        // 188340 only treats flags zero as arrival. Entity/linked and working states are
        // deliberately described but never admitted by the ordinary single-unit policy.
        private static readonly WorkerJourneyField[] Common = {
            W(0x6E4,"alive"), W(0x6E6,"unit-type"), W(0x6EE,"owner"), D(0x6F0,"unit-global"),
            W(0x918,"ai-state"), W(0x91A,"saved-ai"), W(0x956,"work-mode"),
            W(0x95C,"yield-fraction"), W(0x95E,"bonus-yield"),
            W(0x990,"workplace-id"), D(0x9C0,"workplace-global"),
            W(0x992,"workplace-access-x"), W(0x994,"workplace-access-y"),
            W(0x98E,"store-id"), D(0x6F8,"store-global"),
            W(0x9CA,"work-rest-request"), W(0x9CC,"job-retry-mode"), W(0x9E0,"carry-or-role-payload")
        };
        private static readonly int[] None = new int[0];
        private static readonly WorkerRoleContract[] Roles = {
            Role(eChimps.CHIMP_TYPE_WOODCUTTER,0x12BC00,new[]{1,4,8,9},None,
                D(0x700,"vegetation-serial"),W(0x996,"job-reference"),W(0x9A0,"vegetation-id"),W(0x9D8,"work-selector"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_FLETCHER,0x12D230,new[]{1,2,3,7},None,
                W(0x9D8,"work-selector"),B(0xA47,"captured-weapon-kind"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_HUNTER,0x12FC70,new[]{2,5,6},new[]{1,11},
                D(0x700,"target-context-global"),W(0x99C,"worker-or-prey-slot"),W(0x9D8,"work-selector"),W(0x9EE,"job-counter"),
                W(0x9F4,"saved-command"),W(0x9F6,"prey-id"),D(0x9F8,"prey-global"),W(0xA0C,"prey-work-state"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_QUARRY_MASON,0x131430,new[]{2},None,W(0x9EE,"worker-slot")),
            Role(eChimps.CHIMP_TYPE_QUARRY_GRUNT,0x1319A0,new[]{3,4,101,102},None,W(0x9D8,"work-selector"),W(0x9EE,"worker-slot")),
            Role(eChimps.CHIMP_TYPE_QUARRY_OX,0x132910,new[]{1,3},None,W(0x8E6,"departure-delay"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_PITCHMAN,0x1332B0,new[]{3,4},None,B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_FARMER_WHEAT,0x133DD0,new[]{2,3,5,6,8,10},None,
                D(0x730,"selected-field-endpoint"),W(0x9D6,"crop-selector"),W(0x9D8,"work-selector"),W(0x9EC,"crop-counter"),W(0x9EE,"crop-phase"),D(0xA00,"crop-context-tile"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_FARMER_HOPS,0x134F00,new[]{2,3,5,8},None,
                D(0x730,"selected-field-endpoint"),W(0x9D6,"crop-selector"),W(0x9D8,"work-selector"),W(0x9EE,"crop-phase"),D(0xA00,"crop-context-tile"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_FARMER_APPLE,0x135E30,new[]{2,5,7,8},None,
                W(0x9A0,"selected-tree"),W(0x9D0,"job-selector"),W(0x9EC,"picking-cycle"),W(0x9EE,"job-counter"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_FARMER_CATTLE,0x136D40,new[]{2,5},None,W(0x9EE,"job-counter"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_MILLER,0x1377C0,new[]{2,3,4,8},None,
                W(0x9D8,"work-selector"),W(0x9EA,"mill-slot-state"),W(0x9EE,"mill-worker-slot"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_BAKER,0x138850,new[]{2,6,7,8},None,W(0x9D8,"work-selector"),W(0x9EA,"input-reserve"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_BREWER,0x139950,new[]{2,3,4,8},None,W(0x9D8,"work-selector"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_POLETURNER,0x13AAD0,new[]{2,3,4,8},None,
                W(0x9D8,"work-selector"),B(0xA47,"captured-weapon-kind"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_BLACKSMITH,0x13BDD0,new[]{2,3,4,8},None,W(0x9D8,"work-selector"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_ARMOURER,0x13CF30,new[]{2,3,4,8},None,W(0x9D8,"work-selector"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_TANNER,0x13E0B0,new[]{4,8},new[]{2,3},
                W(0x9D8,"work-selector"),W(0x9F6,"linked-cow-id"),D(0x9F8,"linked-cow-global"),B(0xAA6,"delivery-stat-once")),
            Role(eChimps.CHIMP_TYPE_MINER1,0x14D6F0,new[]{2},None,W(0x9EE,"worker-slot")),
            Role(eChimps.CHIMP_TYPE_MINER2,0x14DAC0,new[]{2,4},None,W(0x9EE,"worker-slot"),B(0xAA6,"delivery-stat-once"))
        };

        private static WorkerJourneyField W(int offset,string name) => new WorkerJourneyField(offset,2,name);
        private static WorkerJourneyField D(int offset,string name) => new WorkerJourneyField(offset,4,name);
        private static WorkerJourneyField B(int offset,string name) => new WorkerJourneyField(offset,1,name);
        private static WorkerRoleContract Role(eChimps type,int handler,int[] ordinary,int[] entity,params WorkerJourneyField[] specific)
        {
            var fields=new WorkerJourneyField[Common.Length+specific.Length];
            Array.Copy(Common,fields,Common.Length);Array.Copy(specific,0,fields,Common.Length,specific.Length);
            return new WorkerRoleContract(type,handler,ordinary,entity,fields);
        }
        internal static bool TryDescribe(eChimps type,int aiState,out WorkerJourneyRule rule)
        {
            foreach(var role in Roles)
                if(role.Type==type)
                {
                    WorkerJourneyKind kind=Contains(role.OrdinaryStates,aiState)?WorkerJourneyKind.OrdinaryTravel:
                        Contains(role.EntityStates,aiState)?WorkerJourneyKind.EntityOrLinkedTravel:WorkerJourneyKind.ContactOrOther;
                    rule=new WorkerJourneyRule(role,aiState,kind);return true;
                }
            rule=default;return false;
        }
        internal static bool IsActiveTravel(eChimps type,int aiState,int pathFlags) =>
            (pathFlags==2 || pathFlags==4) && TryDescribe(type,aiState,out var rule) && rule.CanResumeOrdinary;
        internal static WorkerExternalBindings RequiredBindings(eChimps type,int state,WorkerJourneyKind kind)
        {
            if(kind!=WorkerJourneyKind.OrdinaryTravel && kind!=WorkerJourneyKind.EntityOrLinkedTravel)return WorkerExternalBindings.None;
            var value=WorkerExternalBindings.Workplace;
            if(kind==WorkerJourneyKind.EntityOrLinkedTravel)return value|WorkerExternalBindings.LinkedEntity;
            bool store;
            switch(type)
            {
                case eChimps.CHIMP_TYPE_WOODCUTTER: store=state==8; if(state==1)value|=WorkerExternalBindings.Vegetation; break;
                case eChimps.CHIMP_TYPE_FLETCHER: store=state==1 || state==7; break;
                case eChimps.CHIMP_TYPE_HUNTER: store=state==5; break;
                case eChimps.CHIMP_TYPE_QUARRY_GRUNT: store=state==3 || state==101 || state==102; break;
                case eChimps.CHIMP_TYPE_QUARRY_OX: store=state==1; break;
                case eChimps.CHIMP_TYPE_PITCHMAN: store=state==3; break;
                case eChimps.CHIMP_TYPE_FARMER_WHEAT: store=state==10; break;
                case eChimps.CHIMP_TYPE_FARMER_HOPS: store=state==8; break;
                case eChimps.CHIMP_TYPE_FARMER_APPLE: store=state==7; if(state==2 || state==8)value|=WorkerExternalBindings.Vegetation; break;
                case eChimps.CHIMP_TYPE_FARMER_CATTLE: store=state==5; break;
                case eChimps.CHIMP_TYPE_MILLER: store=state==3 || state==8; break;
                case eChimps.CHIMP_TYPE_BAKER: store=state==6 || state==7; break;
                case eChimps.CHIMP_TYPE_BREWER:
                case eChimps.CHIMP_TYPE_POLETURNER:
                case eChimps.CHIMP_TYPE_BLACKSMITH:
                case eChimps.CHIMP_TYPE_ARMOURER: store=state==2 || state==8; break;
                case eChimps.CHIMP_TYPE_TANNER: store=state==8; break;
                case eChimps.CHIMP_TYPE_MINER2: store=state==4; break;
                default: store=false; break;
            }
            return store ? value|WorkerExternalBindings.Store : value;
        }
        private static bool Contains(int[] values,int value)
        { foreach(int item in values)if(item==value)return true;return false; }
    }

    // Exact values, never a hash: no collisions and no retained Unit/Game pointers.
    // This protects the worker record, not the lifetime of a referenced building/tree.
    // The owner must additionally validate those external identities in the same world.
    internal sealed class WorkerJourneySnapshot
    {
        private readonly WorkerJourneyRule rule;
        private readonly uint[] values;
        private WorkerJourneySnapshot(WorkerJourneyRule rule,uint[] values) { this.rule=rule;this.values=values; }
        internal WorkerJourneyRule Rule => rule;
        internal uint[] CopyValues() => (uint[])values.Clone();
        internal static bool TryRestoreKnownSnapshot(eChimps type,int state,int[] offsets,int[] widths,uint[] source,out WorkerJourneySnapshot snapshot)
        {
            snapshot=null;
            if(!WorkerTravelPolicy.TryDescribe(type,state,out var rule) || !rule.CanResumeOrdinary ||
                offsets==null || widths==null || source==null || offsets.Length!=rule.FieldCount ||
                widths.Length!=rule.FieldCount || source.Length!=rule.FieldCount)return false;
            for(int i=0;i<source.Length;i++)
            {
                var field=rule.Field(i);
                if(offsets[i]!=field.NativeOffset || widths[i]!=field.Width ||
                    (field.Width==1 && source[i]>byte.MaxValue) || (field.Width==2 && source[i]>ushort.MaxValue))return false;
            }
            // The captured identity must agree with its declared contract.
            for(int i=0;i<source.Length;i++)
                if((offsets[i]==0x6E6 && source[i]!=(uint)type) || (offsets[i]==0x918 && source[i]!=(uint)state))return false;
            snapshot=new WorkerJourneySnapshot(rule,(uint[])source.Clone());return true;
        }
        internal static bool TryCapture(WorkerJourneyRule rule,Func<int,ushort> word,Func<int,uint> dword,out WorkerJourneySnapshot snapshot)
        {
            snapshot=null;
            if(!rule.CanResumeOrdinary || word==null || dword==null)return false;
            try
            {
                if(word(0x6E6)!=(ushort)rule.UnitType || word(0x918)!=rule.AIState)return false;
                var values=new uint[rule.FieldCount];
                for(int i=0;i<values.Length;i++)values[i]=rule.Field(i).Read(word,dword);
                snapshot=new WorkerJourneySnapshot(rule,values);return true;
            }
            catch { return false; } // Invalid injected boundary never admits a partial job snapshot.
        }
        internal bool Matches(eChimps type,int aiState,Func<int,ushort> word,Func<int,uint> dword)
            => MatchesCore(type,aiState,word,dword,false);
        // Only for an owner-tagged, delayed continuation. Its caller must still validate
        // the native order/world identity and current parked state; this method authorizes no write.
        internal bool MatchesContinuation(eChimps type,int aiState,Func<int,ushort> word,Func<int,uint> dword)
            => MatchesCore(type,aiState,word,dword,true);
        private bool MatchesCore(eChimps type,int aiState,Func<int,ushort> word,Func<int,uint> dword,bool allowWaitingTimers)
        {
            if(type!=rule.UnitType || aiState!=rule.AIState || word==null || dword==null)return false;
            try
            {
                for(int i=0;i<values.Length;i++)
                {
                    var field=rule.Field(i);
                    // Only these audited roles' timers may advance before arrival.
                    // The same offset in another role remains an exact job selector.
                    if(allowWaitingTimers && rule.MayAdvanceWhileWaiting(i))continue;
                    if(field.Read(word,dword)!=values[i])return false;
                }
                return true;
            }
            catch { return false; }
        }
        internal bool TryGetValue(int nativeOffset,out uint value)
        {
            for(int i=0;i<values.Length;i++)if(rule.Field(i).NativeOffset==nativeOffset){value=values[i];return true;}
            value=0;return false;
        }
    }
}
