using System;
using SHCDESE.Interop;

namespace TrappedUnitRescueTest.Tests
{
    internal static class WorkerTravelFixture
    {
        private static int checks;
        private sealed class Memory
        {
            // A manager-relative record view, deliberately not a live GameUnit pointer.
            internal readonly byte[] Bytes=new byte[0xAEC];
            internal ushort Word(int at)=>(ushort)(Bytes[at]|Bytes[at+1]<<8);
            internal uint Dword(int at)=>(uint)(Word(at)|(uint)Word(at+2)<<16);
            internal void Word(int at,int value){Bytes[at]=(byte)value;Bytes[at+1]=(byte)(value>>8);}
            internal void Dword(int at,uint value){Word(at,(ushort)value);Word(at+2,(ushort)(value>>16));}
            internal Memory(eChimps type,int state)
            {
                for(int i=0;i<Bytes.Length;i++)Bytes[i]=(byte)(i*13+7);
                Word(0x6E6,(ushort)type);Word(0x918,state);
            }
        }
        private readonly struct Case
        {
            internal readonly eChimps Type;
            internal readonly int[] Travel, Linked;
            internal readonly int Work;
            internal Case(eChimps type,int[] travel,int work,params int[] linked)
            {Type=type;Travel=travel;Work=work;Linked=linked;}
        }
        // These are external native state contracts, not an enumeration of implementation members.
        // In particular QuarryGrunt101/102 are travel, Tanner2/3 and Hunter1/11 are linked.
        private static readonly Case[] Cases={
            new Case(eChimps.CHIMP_TYPE_WOODCUTTER,new[]{1,4,8,9},2),
            new Case(eChimps.CHIMP_TYPE_FLETCHER,new[]{1,2,3,7},4),
            new Case(eChimps.CHIMP_TYPE_HUNTER,new[]{2,5,6},10,1,11),
            new Case(eChimps.CHIMP_TYPE_QUARRY_MASON,new[]{2},1),
            new Case(eChimps.CHIMP_TYPE_QUARRY_GRUNT,new[]{3,4,101,102},2),
            new Case(eChimps.CHIMP_TYPE_QUARRY_OX,new[]{1,3},2),
            new Case(eChimps.CHIMP_TYPE_PITCHMAN,new[]{3,4},1),
            new Case(eChimps.CHIMP_TYPE_FARMER_WHEAT,new[]{2,3,5,6,8,10},7),
            new Case(eChimps.CHIMP_TYPE_FARMER_HOPS,new[]{2,3,5,8},6),
            new Case(eChimps.CHIMP_TYPE_FARMER_APPLE,new[]{2,5,7,8},3),
            new Case(eChimps.CHIMP_TYPE_FARMER_CATTLE,new[]{2,5},3),
            new Case(eChimps.CHIMP_TYPE_MILLER,new[]{2,3,4,8},5),
            new Case(eChimps.CHIMP_TYPE_BAKER,new[]{2,6,7,8},4),
            new Case(eChimps.CHIMP_TYPE_BREWER,new[]{2,3,4,8},6),
            new Case(eChimps.CHIMP_TYPE_POLETURNER,new[]{2,3,4,8},6),
            new Case(eChimps.CHIMP_TYPE_BLACKSMITH,new[]{2,3,4,8},6),
            new Case(eChimps.CHIMP_TYPE_ARMOURER,new[]{2,3,4,8},6),
            new Case(eChimps.CHIMP_TYPE_TANNER,new[]{4,8},9,2,3),
            new Case(eChimps.CHIMP_TYPE_MINER1,new[]{2},1),
            new Case(eChimps.CHIMP_TYPE_MINER2,new[]{2,4},3)
        };
        internal static void Run()
        {
            checks=0;
            foreach(var item in Cases)
            {
                foreach(int state in item.Travel)
                {
                    Require(WorkerTravelPolicy.TryDescribe(item.Type,state,out var rule) && rule.CanResumeOrdinary,
                        "audited active journey admitted: "+item.Type+"/"+state);
                    Require(WorkerTravelPolicy.IsActiveTravel(item.Type,state,2) && WorkerTravelPolicy.IsActiveTravel(item.Type,state,4),
                        "both published travel flags accepted");
                    foreach(int flags in new[]{0,1,3,5,6,255})
                        Require(!WorkerTravelPolicy.IsActiveTravel(item.Type,state,flags),"unpublished or parked route is not a new journey");
                    VerifySnapshot(rule);
                }
                Require(WorkerTravelPolicy.TryDescribe(item.Type,item.Work,out var contact) &&
                    contact.Kind==WorkerJourneyKind.ContactOrOther && !WorkerTravelPolicy.IsActiveTravel(item.Type,item.Work,2),
                    "contact is not converted into travel: "+item.Type);
                foreach(int state in item.Linked)
                {
                    Require(WorkerTravelPolicy.TryDescribe(item.Type,state,out var linked) && linked.Kind==WorkerJourneyKind.EntityOrLinkedTravel,
                        "entity/linked state remains identifiable");
                    Require(!WorkerTravelPolicy.IsActiveTravel(item.Type,state,2),"linked trip is not rescued as a single unit");
                    Require(!WorkerJourneySnapshot.TryCapture(linked,_=>0,_=>0,out _),"no ordinary snapshot for linked trip");
                }
                foreach(int state in new[]{108,109,110,111,121,126,127,128,129})
                    Require(!WorkerTravelPolicy.IsActiveTravel(item.Type,state,2),"fade/death/leisure/climb is not an ordinary worker journey");
            }
            Require(!WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_KNIGHT,1,out _),"military movement is owned by its own policy");
            Require(!WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_PEASANT,1,out _),"unknown economic job is not guessed");
            Require(!default(WorkerJourneyRule).CanResumeOrdinary,"default rule never admits work");
            VerifyConcreteCargoAndSelection();
            VerifyExternalBindings();
            VerifyDelayedContinuation();
            VerifyReadFailure();
            Console.WriteLine("PASS: worker travel policy "+checks+" checks; all20 role contracts, linked/contact rejection, exact job snapshots; no game execution.");
        }
        private static void VerifyDelayedContinuation()
        {
            foreach(var item in Cases)
            {
                int state=item.Travel[0];
                WorkerTravelPolicy.TryDescribe(item.Type,state,out var rule);
                var memory=new Memory(item.Type,state);
                WorkerJourneySnapshot.TryCapture(rule,memory.Word,memory.Dword,out var snapshot);
                for(int index=0;index<rule.FieldCount;index++)
                {
                    var field=rule.Field(index);byte old=memory.Bytes[field.NativeOffset];
                    memory.Bytes[field.NativeOffset]^=1;
                    bool isWaitingTimer=item.Type==eChimps.CHIMP_TYPE_QUARRY_OX && field.NativeOffset==0x8E6 ||
                        (item.Type==eChimps.CHIMP_TYPE_FARMER_WHEAT || item.Type==eChimps.CHIMP_TYPE_FARMER_HOPS) && field.NativeOffset==0x9D8;
                    Require(snapshot.MatchesContinuation(item.Type,state,memory.Word,memory.Dword)==isWaitingTimer,
                        "only pre-arrival timer progress may survive delayed comparison: "+field.Name);
                    memory.Bytes[field.NativeOffset]=old;
                }
                Require(!snapshot.MatchesContinuation(item.Type,state+1,memory.Word,memory.Dword),"delayed continuation respects new AI state");
            }
        }
        private static void VerifyExternalBindings()
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_WOODCUTTER,1,out var treeTrip);
            Require(treeTrip.RequiredExternalBindings==(WorkerExternalBindings.Workplace|WorkerExternalBindings.Vegetation),
                "tree approach does not demand a stale old store");
            Require(treeTrip.VegetationSerialNativeOffset==0x700,"wood tree serial is unit-stamped");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_WOODCUTTER,8,out var delivery);
            Require(delivery.RequiredExternalBindings==(WorkerExternalBindings.Workplace|WorkerExternalBindings.Store),
                "delivery needs its store rather than an already cut tree");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_FARMER_APPLE,2,out var apple);
            Require(apple.RequiredExternalBindings==(WorkerExternalBindings.Workplace|WorkerExternalBindings.Vegetation) &&
                apple.VegetationSerialNativeOffset==0,"Apple requires an independently captured tree-record identity");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_QUARRY_GRUNT,101,out var input);
            Require((input.RequiredExternalBindings&WorkerExternalBindings.Store)!=0,"Grunt input journey binds the selected source");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_TANNER,3,out var pair);
            Require((pair.RequiredExternalBindings&WorkerExternalBindings.LinkedEntity)!=0 && !pair.CanResumeOrdinary,
                "Tanner with cow cannot pass single-unit admission");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_HUNTER,2,out var meat);
            Require(meat.RequiredExternalBindings==WorkerExternalBindings.Workplace,"homeward meat trip does not demand living prey");
        }
        private static void VerifySnapshot(WorkerJourneyRule rule)
        {
            var memory=new Memory(rule.UnitType,rule.AIState);
            Require(WorkerJourneySnapshot.TryCapture(rule,memory.Word,memory.Dword,out var snapshot),"capture complete job state");
            Require(snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"unchanged job matches");
            Require(!snapshot.Matches(rule.UnitType,rule.AIState+1,memory.Word,memory.Dword),"new AI job cancels old receipt");
            for(int index=0;index<rule.FieldCount;index++)
            {
                var field=rule.Field(index);
                byte old=memory.Bytes[field.NativeOffset];memory.Bytes[field.NativeOffset]^=0x20;
                Require(!snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"changed job field rejected: "+field.Name);
                memory.Bytes[field.NativeOffset]=old;
                if(field.Width==4)
                {
                    memory.Bytes[field.NativeOffset+3]^=0x80;
                    Require(!snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"full global/tile identity uses all32bits");
                    memory.Bytes[field.NativeOffset+3]^=0x80;
                }
            }
            // Native relocation and route publication can change these while preserving the job.
            foreach(int at in new[]{0x70E,0x710,0x712,0x71C,0x71E,0x72C,0x738,0x73A,0x73C,0x740,0x74E,0x750,0x752,0x754,0x908,0x9A2,0x9A4})
                memory.Bytes[at]^=0x40;
            Require(snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"ordinary kinematic fields are not job identity");
            if(rule.UnitType==eChimps.CHIMP_TYPE_FLETCHER || rule.UnitType==eChimps.CHIMP_TYPE_POLETURNER)
            {
                memory.Bytes[0xA46]^=0x40;
                Require(snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"captured goods byte excludes adjacent native byte");
            }
            memory.Bytes[0xAA7]^=0x40;
            Require(snapshot.Matches(rule.UnitType,rule.AIState,memory.Word,memory.Dword),"delivery once-byte excludes its neighbour");
        }
        private static void VerifyConcreteCargoAndSelection()
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_FARMER_APPLE,7,out var apple);
            var memory=new Memory(apple.UnitType,apple.AIState);
            memory.Word(0x9E0,12);
            Require(WorkerJourneySnapshot.TryCapture(apple,memory.Word,memory.Dword,out var snapshot),"loaded Apple journey");
            memory.Word(0x9E0,11);
            Require(!snapshot.Matches(apple.UnitType,apple.AIState,memory.Word,memory.Dword),"one ware delivered invalidates the previous job snapshot");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_WOODCUTTER,1,out var wood);
            memory=new Memory(wood.UnitType,wood.AIState);memory.Dword(0x700,0x11223344);
            WorkerJourneySnapshot.TryCapture(wood,memory.Word,memory.Dword,out snapshot);
            memory.Dword(0x700,0x99223344);
            Require(!snapshot.Matches(wood.UnitType,wood.AIState,memory.Word,memory.Dword),"reused vegetation slot has different32bitserial");
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_FARMER_WHEAT,6,out var wheat);
            memory=new Memory(wheat.UnitType,wheat.AIState);memory.Dword(0x730,12345);
            WorkerJourneySnapshot.TryCapture(wheat,memory.Word,memory.Dword,out snapshot);
            memory.Dword(0x730,12346);
            Require(!snapshot.Matches(wheat.UnitType,wheat.AIState,memory.Word,memory.Dword),"new crop endpoint cannot reuse old field receipt");
        }
        private static void VerifyReadFailure()
        {
            WorkerTravelPolicy.TryDescribe(eChimps.CHIMP_TYPE_QUARRY_OX,1,out var ox);
            var memory=new Memory(ox.UnitType,ox.AIState);
            Require(!WorkerJourneySnapshot.TryCapture(ox,_=>throw new InvalidOperationException("reader failed"),memory.Dword,out var empty) && empty==null,
                "failed boundary does not leak partial snapshot");
            WorkerJourneySnapshot.TryCapture(ox,memory.Word,memory.Dword,out var snapshot);
            Require(!snapshot.Matches(ox.UnitType,ox.AIState,memory.Word,_=>throw new InvalidOperationException("reader failed")),
                "failed live reference read refuses comparison");
            Require(!WorkerJourneySnapshot.TryCapture(ox,null,memory.Dword,out _),"missing reader rejects capture");
            memory.Word(0x6E6,(ushort)eChimps.CHIMP_TYPE_FARMER_APPLE);
            Require(!WorkerJourneySnapshot.TryCapture(ox,memory.Word,memory.Dword,out _),"memory type disagrees with descriptor");
        }
        private static void Require(bool condition,string message)
        {checks++;if(!condition)throw new InvalidOperationException("WorkerTravelFixture: "+message);}
    }
}
