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
        Console.WriteLine("PASS: "+checks+" production panel refresh and input lease checks.");
    }
}
