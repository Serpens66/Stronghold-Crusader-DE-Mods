$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$old = (& git -C $root show 7e048e6435a72b339dff35f3dcd368210f04ccdb:BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs) -join "`r`n"
$new = [IO.File]::ReadAllText((Join-Path $root 'BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs'))
$model = [IO.File]::ReadAllText((Join-Path $root 'BugfixesAndQoL/src/ExtendedShiftCommandQueueModel.cs'))
function Slice([string] $text, [string] $first, [string] $next) {
    $a = $text.IndexOf($first); $b = $text.IndexOf($next, $a)
    if ($a -lt 0 -or $b -lt 0) { throw "Missing source region $first" }
    return $text.Substring($a, $b-$a)
}
$code = @'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
namespace BugfixesAndQoL {
internal struct GameUnit { public int r_TribeId; }
internal struct GameTribe { public int r_PlayerIdOwner; public uint r_GlobalId; }
internal unsafe class HarnessBase : IDisposable {
 protected readonly Dictionary<long,TribeQueueState> cohorts = new Dictionary<long,TribeQueueState>();
 protected readonly Dictionary<QueueUnitIdentity,long> unitToCohort = new Dictionary<QueueUnitIdentity,long>();
 protected readonly HashSet<long> loggedPredecessorRedispatchFailures = new HashSet<long>(), loggedIsolationFailures = new HashSet<long>();
 protected readonly List<long> cohortIdBuffer = new List<long>();
 protected long nextCohortId = 10;
 protected readonly Dictionary<int,IntPtr> units = new Dictionary<int,IntPtr>(), tribes = new Dictionary<int,IntPtr>();
 public readonly List<string> Trace = new List<string>();
 protected bool TryGetLivingUnit(QueueUnitIdentity member, out GameUnit* unit) { Trace.Add("unit:"+member.UnitId); bool found=units.TryGetValue(member.UnitId,out IntPtr p); unit=(GameUnit*)p; return found; }
 protected bool TryGetAliveTribe(int id, out GameTribe* tribe) { Trace.Add("tribe:"+id); bool found=tribes.TryGetValue(id,out IntPtr p); tribe=(GameTribe*)p; return found; }
 protected void RecordQueueTopology(string op, TribeQueueState state) { Trace.Add(op+":"+state.CohortId); }
 public void Seed(int scenario) {
  for(int i=1;i<=6;i++) {
   if(scenario==1 && i==2) continue;
   IntPtr p=Marshal.AllocHGlobal(sizeof(GameUnit)); units.Add(i,p);
   ((GameUnit*)p)->r_TribeId=scenario==2 ? i%3 : 1;
  }
  for(int i=1;i<=2;i++) {
   IntPtr p=Marshal.AllocHGlobal(sizeof(GameTribe)); tribes.Add(i,p);
   ((GameTribe*)p)->r_PlayerIdOwner=scenario==3 ? 2 : 1; ((GameTribe*)p)->r_GlobalId=(uint)(100+i);
  }
  for(int c=1;c<=2;c++) {
   var members=new List<QueueUnitIdentity>();
   for(int i=c;i<=6;i+=2) members.Add(new QueueUnitIdentity(i,(uint)(200+i)));
   var state=new TribeQueueState(101,128,members,1,c,1);
   state.TryEnqueue(new QueueCommand(QueueCommandKind.Move, scenario==4 ? c : 10, 20, 0));
   cohorts.Add(c,state);
   foreach(var member in members) unitToCohort[member]=c;
  }
 }
 public string Snapshot() => string.Join("|",cohorts.OrderBy(x=>x.Key).Select(x=>x.Key+":"+x.Value.BoundTribeId+":"+x.Value.TribeGlobalId+":"+string.Join(",",x.Value.Members.Select(m=>m.UnitId+"/"+m.GlobalId))+":"+x.Value.PendingCount)) + ";map=" + string.Join(",",unitToCohort.OrderBy(x=>x.Key.UnitId).Select(x=>x.Key.UnitId+":"+x.Value));
 public void Dispose() { foreach(var p in units.Values) Marshal.FreeHGlobal(p); foreach(var p in tribes.Values) Marshal.FreeHGlobal(p); }
}
'@
foreach ($optimized in @($false,$true)) {
    $text = if ($optimized) { $new } else { $old }
    $name = if ($optimized) { 'NewHarness' } else { 'OldHarness' }
    $code += "`ninternal unsafe class $name : HarnessBase {`n"
    if ($optimized) { $code += "private readonly Stack<QueueWorkBuffers> workBufferPool = new Stack<QueueWorkBuffers>();`n" }
    $code += Slice $text '        private void ReconcileCohorts(' '        private bool EnsureDedicatedTribe('
    $code += Slice $text '        private void CoalesceEquivalentCohorts(' '        private void RemoveCohort('
    $code += Slice $text '        private void RemoveCohort(' '        private bool HasQueuedUnitInTribe('
    # Locate the next method after the comparator signature.
    $a = $text.IndexOf('        private static int CompareCohorts(')
    $b = $text.IndexOf('        private static ', $a + 20)
    $code += $text.Substring($a,$b-$a)
    $code += "public void Run() { ReconcileCohorts(); CoalesceEquivalentCohorts(); }`n"
    if ($optimized) {
        $code += @'
public void CheckPool() {
 foreach(var b in workBufferPool) if(b.Members.Count!=0 || b.OtherMembers.Count!=0 || b.States.Count!=0 || b.CohortIds.Count!=0 || b.TribeIds.Count!=0 || b.Branches.Count!=0 || b.ByTribe.Count!=0 || b.SeenMembers.Count!=0 || b.SeenCohorts.Count!=0) throw new Exception("Uncleared workspace");
 var outer=RentWorkBuffers(); outer.Members.Add(new QueueUnitIdentity(99,99));
 var inner=RentWorkBuffers(); if(ReferenceEquals(outer,inner)) throw new Exception("Nested alias");
 inner.Members.Add(new QueueUnitIdentity(88,88)); inner.Clear(); workBufferPool.Push(inner);
 if(outer.Members.Count!=1 || outer.Members[0].UnitId!=99) throw new Exception("Nested corruption");
 outer.Clear(); workBufferPool.Push(outer);
}
'@
    }
    $code += "`n}`n"
}
$code += @'
public static class WorkBufferAudit {
 public static string Run() {
  for(int count=0;count<12;count++) for(int variant=0;variant<3;variant++) {
   var left=new TribeQueueState(1,128); var right=new TribeQueueState(1,128);
   for(int i=0;i<count;i++) { left.TryEnqueue(new QueueCommand(QueueCommandKind.Move,i,20,0)); right.TryEnqueue(new QueueCommand(QueueCommandKind.Move,variant==1 ? i+1 : i,20,0)); }
   if(variant==2) right.TryEnqueue(new QueueCommand(QueueCommandKind.Move,99,20,0));
   var a=left.PendingCommands.ToArray(); var b=right.PendingCommands.ToArray();
   bool expected=a.Length==b.Length && a.Zip(b,(x,y)=>ReferenceEquals(x,y) || (x!=null && x.HasSamePayload(y))).All(x=>x);
   if(left.HasSamePendingCommands(right)!=expected) throw new Exception("Pending equality mismatch");
  }
  for(int scenario=0;scenario<5;scenario++) using(var a=new OldHarness()) using(var b=new NewHarness()) {
   a.Seed(scenario); b.Seed(scenario);
   for(int n=0;n<3;n++) {
    a.Run(); b.Run();
    if(a.Snapshot()!=b.Snapshot() || !a.Trace.SequenceEqual(b.Trace)) throw new Exception("Behavior mismatch scenario "+scenario+" pass "+n);
    b.CheckPool();
   }
  }
  long oldBytes,newBytes;
  using(var a=new OldHarness()) using(var b=new NewHarness()) {
   a.Seed(0); b.Seed(0); a.Run(); b.Run();
   for(int n=0;n<20;n++) { a.Run(); b.Run(); a.Trace.Clear(); b.Trace.Clear(); }
   long start=GC.GetAllocatedBytesForCurrentThread(); for(int n=0;n<10000;n++) { a.Run(); a.Trace.Clear(); } oldBytes=GC.GetAllocatedBytesForCurrentThread()-start;
   start=GC.GetAllocatedBytesForCurrentThread(); for(int n=0;n<10000;n++) { b.Run(); b.Trace.Clear(); } newBytes=GC.GetAllocatedBytesForCurrentThread()-start;
  }
  if(newBytes>=oldBytes) throw new Exception("No allocation reduction");
  return "PASS: actual Reconcile/Coalesce source, 5 scenarios x 3 passes; state and worker-read traces equal; cleared/reentrant buffers. Bytes/10000 active passes: "+oldBytes+" -> "+newBytes;
 }
}
}
'@
# Keep source using directives at compilation-unit level.
$modelBody = $model.Substring($model.IndexOf('namespace BugfixesAndQoL'))
$spacing = [IO.File]::ReadAllText((Join-Path $root 'BugfixesAndQoL/src/MoveFormationSpacingPolicy.cs'))
Add-Type -TypeDefinition ($code + "`n" + $modelBody + "`n" + $spacing) -CompilerOptions '/unsafe'
[BugfixesAndQoL.WorkBufferAudit]::Run()
