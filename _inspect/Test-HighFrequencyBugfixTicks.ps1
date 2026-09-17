$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
function Read-Method([string] $path, [string] $startMarker, [string] $endMarker, [bool] $original) {
    $text = if ($original) { (& git -C $root show ('7e048e6435a72b339dff35f3dcd368210f04ccdb:' + $path)) -join "`r`n" } else { [IO.File]::ReadAllText((Join-Path $root $path)) }
    $start = $text.IndexOf($startMarker)
    $end = $text.IndexOf($endMarker, $start)
    if ($start -lt 0 -or $end -lt 0) { throw "Missing method in $path" }
    return $text.Substring($start, $end - $start).Replace('private void ', 'public void ')
}
$code = @'
using System;
using System.Collections.Generic;
namespace Shared { public static class DebugLogHelper {
 public static void LogError(object log, string text) { ((List<string>)log).Add("error"); }
 public static void LogInfo(object log, string text) { ((List<string>)log).Add("first-tick"); }
} }
public class QuarryBase {
 public bool mapActive = true, aiSpawnObservationArmed, Active = true;
 public readonly Dictionary<int,int> pendingAIQuarriesByGlobalId = new Dictionary<int,int>();
 protected readonly Stack<int[]> pendingAIQuarryKeyBuffers = new Stack<int[]>();
 public readonly List<string> Trace = new List<string>();
 protected object log;
 public Action<int,int> Work;
 public QuarryBase() { log = Trace; }
 protected bool IsAIAutomationActive() { return Active; }
 protected void LogInfo(string text) { Trace.Add("armed"); }
 protected void TryProcessPendingAIQuarry(int id, int tick) { Work(id,tick); }
}
public class PlagueBase {
 public bool mapActive = true, correctionAvailable = true;
 public List<int> herds = new List<int>();
 public HashSet<int> managedPlayerIds = new HashSet<int>();
 public int Prunes, Reports, Errors;
 protected void PruneInvalidProjectiles() { Prunes++; }
 protected void ReportMissingPopularityCallbacks() { Reports++; }
 protected void DisableCorrectionToVanilla(string text, Exception ex) { Errors++; }
}
public class ShiftBase {
 public bool installed = true, FeatureEnabled = true, runtimeTickLogged;
 public int currentTick, Prunes, Reconciles, Coalesces, Processed;
 public readonly List<int> expectedMoveChores = new List<int>(), expectedMoveEvents = new List<int>();
 public readonly Dictionary<long,int> cohorts = new Dictionary<long,int>();
 protected readonly List<long> cohortIdBuffer = new List<long>();
 public readonly List<string> Trace = new List<string>();
 protected object log;
 protected sealed class QueueWorkBuffers { public readonly List<long> CohortIds = new List<long>(); public void Clear() { CohortIds.Clear(); } }
 protected readonly Stack<QueueWorkBuffers> workBufferPool = new Stack<QueueWorkBuffers>();
 protected QueueWorkBuffers RentWorkBuffers() { return workBufferPool.Count == 0 ? new QueueWorkBuffers() : workBufferPool.Pop(); }
 public ShiftBase() { log = Trace; }
 protected void PruneExpectedMoveSignals(List<int> list) { Prunes++; list.RemoveAll(x => x < currentTick); }
 protected void ReconcileCohorts() { Reconciles++; }
 protected void CoalesceEquivalentCohorts() { Coalesces++; }
 protected int CompareCohorts(int a,int b) { return a.CompareTo(b); }
 protected void ProcessCohort(long id) { Processed++; }
}
'@
foreach ($original in @($true, $false)) {
    $prefix = if ($original) { 'Before' } else { 'After' }
    $method = Read-Method 'BugfixesAndQoL/src/QuarryPileRelocationRuntime.cs' '        private void OnGameTick(int tick)' '        private void TryProcessPendingAIQuarry(' $original
    $code += "`npublic class ${prefix}Quarry : QuarryBase {`n$method`n}`n"
    $method = Read-Method 'BugfixesAndQoL/src/PlaguePopularityFix.cs' '        private void OnGameTick(int _)' '        private void CorrectPlaguePopularity(' $original
    $code += "`npublic class ${prefix}Plague : PlagueBase {`n$method`n}`n"
    $method = Read-Method 'BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs' '        private void OnTickCore(int tick)' '        private void ProcessCohort(' $original
    $code += "`npublic class ${prefix}Shift : ShiftBase {`n$method`n}`n"
}
$code += @'
public static class BugfixTickAudit {
 static string Quarry(bool optimized, int scenario) {
  QuarryBase q = optimized ? (QuarryBase)new AfterQuarry() : new BeforeQuarry();
  Action<int> tick = optimized ? new Action<int>(((AfterQuarry)q).OnGameTick) : ((BeforeQuarry)q).OnGameTick;
  if (scenario != 5) { q.pendingAIQuarriesByGlobalId.Add(8,8); q.pendingAIQuarriesByGlobalId.Add(1,1); q.pendingAIQuarriesByGlobalId.Add(4,4); }
  bool nested = false;
  q.Work = (id,t) => {
   q.Trace.Add(id + ":" + t);
   if (scenario == 1 && id == 8) { q.pendingAIQuarriesByGlobalId.Remove(1); q.pendingAIQuarriesByGlobalId[3] = 3; }
   if (scenario == 2 && id == 1) throw new Exception();
   if (scenario == 3 && !nested) { nested = true; q.pendingAIQuarriesByGlobalId[2] = 2; tick(t+1); }
   if (scenario == 4) q.pendingAIQuarriesByGlobalId.Clear();
  };
  if (scenario == 6) q.Active = false;
  tick(10); tick(11);
  return string.Join(",",q.Trace) + ";" + string.Join(",",q.pendingAIQuarriesByGlobalId.Keys) + ";" + q.aiSpawnObservationArmed;
 }
 static long Allocate(bool optimized, bool active) {
  QuarryBase q = optimized ? (QuarryBase)new AfterQuarry() : new BeforeQuarry();
  Action<int> tick = optimized ? new Action<int>(((AfterQuarry)q).OnGameTick) : ((BeforeQuarry)q).OnGameTick;
  q.Work = (id,t) => {};
  if (active) for (int i=1;i<=8;i++) q.pendingAIQuarriesByGlobalId.Add(i,i);
  for(int i=0;i<100;i++) tick(i);
  long start = GC.GetAllocatedBytesForCurrentThread();
  for(int i=0;i<10000;i++) tick(i);
  return GC.GetAllocatedBytesForCurrentThread()-start;
 }
 public static string Run() {
  for(int s=0;s<7;s++) if(Quarry(false,s)!=Quarry(true,s)) throw new Exception("Quarry scenario " + s);
  for(int mask=0;mask<16;mask++) {
   var a=new BeforePlague(); var b=new AfterPlague();
   a.mapActive=b.mapActive=(mask&1)!=0; a.correctionAvailable=b.correctionAvailable=(mask&2)!=0;
   if((mask&4)!=0) { a.herds.Add(1); b.herds.Add(1); }
   if((mask&8)!=0) { a.managedPlayerIds.Add(1); b.managedPlayerIds.Add(1); }
   a.OnGameTick(1); b.OnGameTick(1);
   if(a.Prunes!=b.Prunes || a.Errors!=b.Errors || ((mask&12)!=0 && a.Reports!=b.Reports)) throw new Exception("Plague scenario " + mask);
  }
  for(int mask=0;mask<32;mask++) {
   var a=new BeforeShift(); var b=new AfterShift();
   a.installed=b.installed=(mask&1)!=0; a.FeatureEnabled=b.FeatureEnabled=(mask&2)!=0;
   if((mask&4)!=0) { a.cohorts.Add(1,1); b.cohorts.Add(1,1); }
   if((mask&8)!=0) { a.expectedMoveChores.Add(1); b.expectedMoveChores.Add(1); }
   if((mask&16)!=0) { a.expectedMoveEvents.Add(1); b.expectedMoveEvents.Add(1); }
   a.OnTickCore(3); b.OnTickCore(3);
   if(a.currentTick!=b.currentTick || a.runtimeTickLogged!=b.runtimeTickLogged || a.Processed!=b.Processed || a.expectedMoveChores.Count!=b.expectedMoveChores.Count || a.expectedMoveEvents.Count!=b.expectedMoveEvents.Count) throw new Exception("Shift scenario " + mask);
   if((mask&28)!=0 && (a.Prunes!=b.Prunes || a.Reconciles!=b.Reconciles || a.Coalesces!=b.Coalesces)) throw new Exception("Shift work skipped " + mask);
  }
  long before=Allocate(false,true), after=Allocate(true,true), idleBefore=Allocate(false,false), idleAfter=Allocate(true,false);
  if(after>=before || idleBefore!=idleAfter) throw new Exception("Quarry allocation regression");
  var oldIdle=new BeforeShift(); var newIdle=new AfterShift();
  for(int i=0;i<10000;i++) { oldIdle.OnTickCore(i); newIdle.OnTickCore(i); }
  return "PASS: Quarry 7 equivalence cases; Plague 16 guard combinations; Shift 32 state combinations. Quarry bytes/10000 active ticks " + before + " -> " + after + ", idle " + idleBefore + " -> " + idleAfter + ". Shift idle Reconcile/Coalesce calls " + oldIdle.Reconciles + "/" + oldIdle.Coalesces + " -> " + newIdle.Reconciles + "/" + newIdle.Coalesces;
 }
}
'@
Add-Type -TypeDefinition $code
[BugfixTickAudit]::Run()
