$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$before = (& git -C $root show 7e048e6435a72b339dff35f3dcd368210f04ccdb:CastlePlanner/src/CastlePlannerRuntime.cs) -join "`r`n"
$after = [IO.File]::ReadAllText((Join-Path $root 'CastlePlanner/src/CastlePlannerRuntime.cs'))
function Get-TickMethod([string] $source) {
    $start = $source.IndexOf('        private void OnGameTick(int tick)')
    $end = $source.IndexOf('        private bool ProcessDeferredCompoundBuilding(', $start)
    if ($start -lt 0 -or $end -lt 0) { throw 'Missing tick method' }
    return $source.Substring($start, $end - $start).Replace('private void OnGameTick', 'public void OnGameTick')
}
$prefix = @'
using System;
using System.Collections.Generic;
using System.Linq;
namespace Shared { public static class DebugLogHelper { public static void LogError(object log, string text) {} } }
public class QueueStub {
    public sealed class DeferredCompoundBuildingQueue { public int Id; }
    public readonly SortedDictionary<int, DeferredCompoundBuildingQueue> deferredCompoundBuildings = new SortedDictionary<int, DeferredCompoundBuildingQueue>();
    protected readonly Stack<int[]> deferredCompoundKeyBuffers = new Stack<int[]>();
    protected object log;
    public bool Allowed = true;
    public Func<int, int, bool> Work;
    protected bool IsCastleSpawningModeAllowed() { return Allowed; }
    protected bool ProcessDeferredCompoundBuilding(DeferredCompoundBuildingQueue q, int tick) { return Work(q.Id, tick); }
    public void Put(int id) { deferredCompoundBuildings[id] = new DeferredCompoundBuildingQueue { Id = id }; }
}
'@
$code = $prefix + "`npublic class BeforeQueue : QueueStub {`n" + (Get-TickMethod $before) + "`n}`npublic class AfterQueue : QueueStub {`n" + (Get-TickMethod $after) + "`n}`n" + @'
public static class QueueAudit {
    static string Run(bool optimized, int scenario) {
        QueueStub q = optimized ? (QueueStub)new AfterQueue() : new BeforeQueue();
        Action<int> tick = optimized ? new Action<int>(((AfterQueue)q).OnGameTick) : ((BeforeQueue)q).OnGameTick;
        var calls = new List<string>();
        q.Put(8); q.Put(1); q.Put(4);
        bool nested = false;
        q.Work = (id, t) => {
            calls.Add(id + ":" + t);
            if (scenario == 1 && id == 1) { q.deferredCompoundBuildings.Remove(4); q.Put(2); }
            if (scenario == 2 && id == 4) throw new InvalidOperationException("expected");
            if (scenario == 3 && !nested) { nested = true; q.Put(3); tick(t + 1); }
            if (scenario == 4 && id == 1) q.deferredCompoundBuildings.Clear();
            return scenario == 5;
        };
        if (scenario == 6) q.Allowed = false;
        tick(10); tick(11);
        return string.Join(",", calls) + ";remaining=" + string.Join(",", q.deferredCompoundBuildings.Keys);
    }
    static long Measure(bool optimized, bool active) {
        QueueStub q = optimized ? (QueueStub)new AfterQueue() : new BeforeQueue();
        Action<int> tick = optimized ? new Action<int>(((AfterQueue)q).OnGameTick) : ((BeforeQueue)q).OnGameTick;
        q.Work = (id, t) => false;
        if (active) for (int i = 1; i <= 8; i++) q.Put(i);
        for (int i = 0; i < 100; i++) tick(i);
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) tick(i);
        return GC.GetAllocatedBytesForCurrentThread() - start;
    }
    public static string Execute() {
        for (int s = 0; s < 7; s++) {
            string before = Run(false, s), after = Run(true, s);
            if (before != after) throw new Exception("Mismatch scenario " + s + ": " + before + " / " + after);
        }
        long oldActive = Measure(false, true), newActive = Measure(true, true);
        long oldIdle = Measure(false, false), newIdle = Measure(true, false);
        if (newActive >= oldActive || newIdle != oldIdle) throw new Exception("Allocation regression");
        return "PASS: 7 equivalence scenarios (order/tick/removal/insertion/error/nesting/clear/completion/disabled); 10000 ticks: active bytes " + oldActive + " -> " + newActive + "; idle bytes " + oldIdle + " -> " + newIdle;
    }
}
'@
Add-Type -TypeDefinition $code
[QueueAudit]::Execute()
