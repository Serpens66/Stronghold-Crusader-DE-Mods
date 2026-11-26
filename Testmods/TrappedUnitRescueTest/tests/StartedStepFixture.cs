using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using TrappedUnitRescueTest;

namespace TrappedUnitRescueTest.Tests
{
    internal static class StartedStepFixture
    {
        internal static unsafe void Run()
        {
            int checks=0;
            Action<bool,string> check=(value,message)=>{checks++;if(!value)throw new InvalidOperationException(message);};
            var before=new UnitSnapshot {TileId=100,X=20,Y=30,NextTileId=101,NextX=21,NextY=30,
                OrdinaryStep=true,CurrentWasOrdinary=true,NextWasOrdinary=true,AffectedNext=true};
            Func<int,bool> nextBlocked=tile=>tile!=101;
            uint moat=(uint)TilePropertyFlag.IsMoat,wall=(uint)TilePropertyFlag.IsWall;
            check(!RescueRuntime.RequiresCurrentMoatProbe(true,0),"Next-only moat uses reserved same-Current normalization and ordinary reissue before midpoint.");
            check(RescueRuntime.RequiresCurrentMoatProbe(true,moat),"Actual Current on a fresh moat requires functional source-mode proof after midpoint.");
            check(!RescueRuntime.RequiresCurrentMoatProbe(false,moat),"A nonmoat trapped site never implies moat probe permission.");
            check(!RescueRuntime.RequiresCurrentMoatProbe(true,wall),"A wall is not a moat source; independent mixed-surface gates remain authoritative.");
            check(RescueRuntime.TryLocateChangedTrap(before,100,20,30,101,true,nextBlocked,out var site) && site.Tile==101,
                "A newly blocked accepted Next is an eligible trap site while Current remains legal.");
            check(RescueRuntime.TryLocateChangedTrap(before,101,21,30,101,false,nextBlocked,out site) && site.Tile==101,
                "Exactly the captured step may finish before evaluation.");
            check(!RescueRuntime.TryLocateChangedTrap(before,102,22,30,103,true,nextBlocked,out _),"Further movement retires old local proof.");
            check(!RescueRuntime.TryLocateChangedTrap(before,100,20,30,103,true,nextBlocked,out _),"A replaced accepted step cannot inherit the old Next proof.");
            check(!RescueRuntime.TryLocateChangedTrap(before,100,20,30,101,false,nextBlocked,out _),"A cancelled step is not a Next trap.");
            check(!RescueRuntime.TryLocateChangedTrap(before,101,20,30,101,false,nextBlocked,out _),"Packed tile and XY must agree with captured movement.");
            check(!RescueRuntime.TryLocateChangedTrap(before,100,20,30,101,true,tile=>true,out _),"An unchanged legal Next gives no rescue authority.");
            before.NextWasOrdinary=false;
            check(!RescueRuntime.TryLocateChangedTrap(before,101,21,30,101,false,nextBlocked,out _),"A pre-existing invalid Next has no causal mutation proof.");
            before.NextWasOrdinary=true;before.AffectedNext=false;
            check(!RescueRuntime.TryLocateChangedTrap(before,101,21,30,101,false,nextBlocked,out _),"Unrelated mutations cannot nominate Next.");
            before.AffectedCurrent=true;
            check(RescueRuntime.TryLocateChangedTrap(before,100,20,30,101,true,tile=>tile!=100,out site) && site.Tile==100,
                "A newly blocked Current uses the old Current proof.");
            before.CurrentWasOrdinary=false;
            check(!RescueRuntime.TryLocateChangedTrap(before,100,20,30,101,true,tile=>false,out _),"An already invalid Current is not newly trapped.");
            check(!RescueRuntime.TryLocateChangedTrap(null,100,20,30,101,true,nextBlocked,out _),"Missing before state is rejected.");
            before.Health=100;var damaged=before.WithCurrentHealth(60);
            check(before.Health==100 && damaged.Health==60 && damaged.TileId==before.TileId && damaged.NextTileId==before.NextTileId,
                "Delayed legitimate damage rebases the live transaction without overwriting old geometry or restoring health.");
            // Exact field contract of native19BA40 on A->B followed by relocation(A):
            // inactive73C remains B while Current and NextXY are A, marker0, substep8.
            // Test the production typed-record reader, not a duplicate predicate.
            GameUnit normalized=default;
            normalized.r_CurrentTilePositionX=20;normalized.r_CurrentTilePositionY=30;
            normalized.r_NextTilePositionX2=20;normalized.r_NextTilePositionY2=30;
            normalized.r_CurrentPositionTileId=100;normalized.r_MovementSubstep=8;
            foreach(uint inactiveNext in new uint[]{100,101,0,uint.MaxValue})
            {
                normalized.r_NextPositionTileId2=inactiveNext;
                check(NativeRelocation.IsNormalizedPose(&normalized,100,0),
                    "Native normalized pose accepts an inactive packed Next cache; no extra native write is needed.");
            }
            check(!NativeRelocation.IsNormalizedPose(&normalized,-1,0),"Invalid Current tile is rejected.");
            check(!NativeRelocation.IsNormalizedPose(&normalized,101,0),"Current packed tile must still match actual XY.");
            check(!NativeRelocation.IsNormalizedPose(&normalized,100,1),"Pending low midpoint marker is rejected.");
            check(!NativeRelocation.IsNormalizedPose(&normalized,100,0x10000),"Pending high midpoint marker is rejected.");
            normalized.r_MovementSubstep=1;
            check(!NativeRelocation.IsNormalizedPose(&normalized,100,0),"A begun step is not a normalized parked pose.");
            normalized.r_MovementSubstep=8;normalized.r_NextTilePositionX2=21;
            check(!NativeRelocation.IsNormalizedPose(&normalized,100,0),"Different Next X is rejected even with inactive marker.");
            normalized.r_NextTilePositionX2=20;normalized.r_NextTilePositionY2=31;
            check(!NativeRelocation.IsNormalizedPose(&normalized,100,0),"Different Next Y is rejected even with inactive marker.");
            check(!NativeRelocation.IsNormalizedPose(null,100,0),"Missing record is rejected.");
            GameUnit archer=default;archer.r_UnitChimp=eChimps.CHIMP_TYPE_ARCHER;archer.r_CurrentHealth=100;
            archer.N000000DB=1;archer.r_MovementSubstep=8;
            foreach(ushort state in new ushort[]{7,8,11})
            {
                archer.r_AIState=state;archer.r_PathPlanStateBitFlags=0;
                check(RescueRuntime.ClassifyState(&archer)==null,"Audited Archer idle animation reaches stationary intent validation.");
                foreach(ushort flags in new ushort[]{2,4})
                {
                    archer.r_PathPlanStateBitFlags=flags;
                    check(RescueRuntime.ClassifyState(&archer)!=null,"Stationary Archer extension never admits active animation continuation.");
                }
            }
            archer.r_PathPlanStateBitFlags=0;archer.r_AIState=1;
            check(RescueRuntime.ClassifyState(&archer)!=null,"Absent ArcherAI1 remains unsupported.");
            archer.r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_BOW;archer.r_AIState=8;
            check(RescueRuntime.ClassifyState(&archer)!=null,"Other ranged types do not inherit Archer animation numbers.");
            Console.WriteLine("PASS: accepted Current/Next trap-site checks="+checks+"; production causal selector, no native calls.");
        }
    }
}
