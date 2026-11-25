using System;
using TrappedUnitRescueTest;

namespace TrappedUnitRescueTest.Tests
{
    internal static class StartedStepFixture
    {
        internal static void Run()
        {
            int checks=0;
            Action<bool,string> check=(value,message)=>{checks++;if(!value)throw new InvalidOperationException(message);};
            var before=new UnitSnapshot {TileId=100,X=20,Y=30,NextTileId=101,NextX=21,NextY=30,
                OrdinaryStep=true,CurrentWasOrdinary=true,NextWasOrdinary=true,AffectedNext=true};
            Func<int,bool> nextBlocked=tile=>tile!=101;
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
            Console.WriteLine("PASS: accepted Current/Next trap-site checks="+checks+"; production causal selector, no native calls.");
        }
    }
}
