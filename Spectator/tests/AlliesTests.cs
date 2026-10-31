using System;
using System.Collections.Generic;
using Spectator;
internal static class AlliesTests
{
    static int checks;
    static void Check(bool condition, string text) { checks++; if(!condition) throw new Exception(text); }
    static void Main(string[] args)
    {
        PanelInputLeaseTests.Run(Check);
        var pending = new PanelRefreshState(); var panel = new object(); var snapshot = new object();
        pending.Begin(1,panel,snapshot); long generation=pending.Generation;
        Check(pending.IsCurrent(1,panel,true) && !pending.HasFreshState(snapshot),"wait for fresh snapshot");
        Check(!pending.IsCurrent(1,panel,false),"closing wins before state refresh");
        Check(!pending.IsCurrent(2,panel,true),"session replacement invalidates refresh");
        Check(!pending.IsCurrent(1,new object(),true),"HUD replacement invalidates refresh");
        pending.Cancel(); Check(!pending.Pending && !pending.HasFreshState(new object()),"delayed completion after close is inert");
        pending.Begin(1,panel,new object()); Check(pending.Generation>generation,"reopen gets new generation");
        pending.Begin(1,panel,snapshot); Check(!pending.HasFreshState(snapshot) && pending.HasFreshState(new object()),"last rapid switch wins");
        var report=new ReportRefreshState(); var oldState=new object(); var fresh=new object();
        report.Begin(1,panel,3,71,100); generation=report.Generation;
        Check(report.IsCurrent(1,panel,3,16,71) && !report.Observe(oldState,99),"a newly delivered pre-switch buffer is still stale");
        Check(report.Observe(fresh,100) && report.HasFreshState(fresh),"the first buffer allocated after switching can complete the report");
        report.Begin(1,panel,7,71,104);
        Check(report.Generation>generation && !report.HasFreshState(fresh) && !report.Observe(new object(),103),"rapid player switches retire earlier accepted states and queued buffers");
        Check(!report.IsCurrent(1,panel,3,16,71) && !report.IsCurrent(1,new object(),7,16,71) &&
            !report.IsCurrent(2,panel,7,16,71) && !report.IsCurrent(1,panel,7,14,71) && !report.IsCurrent(1,panel,7,16,75),
            "actor, HUD, mission, close and report-subpage changes retire a refresh");
        report.Cancel(); Check(!report.Observe(new object(),106),"late delivery after close cannot complete a retired refresh");
        report.Begin(2,panel,2,76,int.MaxValue);
        Check(!report.Observe(oldState,int.MaxValue-1) && report.Observe(fresh,int.MinValue),"Int32 buffer clock rollover preserves the bounded queue order");
        var gameState=new EngineInterface.PlayState();
        MemoryBuffers.instance=new MemoryBuffers(111,new MemoryBuffers.MemBuffer {gameState=gameState,frameID=106});
        var clock=new ReportFrameClock();
        Check(clock.Capture(new object())==111 && clock.TryRead(gameState,out int frame) && frame==106,
            "report clock reads the next allocation boundary and the exact delivered snapshot's frame");
        Check(!clock.TryRead(new EngineInterface.PlayState(),out frame),"an unrelated state cannot borrow a buffer's frame ID");
        MemoryBuffers.instance=new MemoryBuffers(120);
        Check(!clock.TryRead(gameState,out frame),"buffer replacement invalidates old snapshot lookup");
        Check(clock.Capture(new object())==120,"a later mission binds its new buffer pool");
        clock.Clear(); Check(!clock.TryRead(gameState,out frame),"mission cleanup drops all retained buffer references");
        Console.WriteLine("PASS: "+checks+" production panel refresh and input lease checks.");
    }
}
