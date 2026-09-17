$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$baseline = '7e048e6435a72b339dff35f3dcd368210f04ccdb'
$before = (& git -C $root show ($baseline + ':BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs')) -join "`r`n"
$after = [IO.File]::ReadAllText((Join-Path $root 'BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs'))
function Block([string]$source, [string]$marker) {
    $start=$source.IndexOf($marker); if($start -lt 0){throw "Missing $marker"}
    $open=$source.IndexOf('{',$start); $depth=1; $end=$open+1
    while($depth){if($source[$end] -eq '{'){$depth++}; if($source[$end] -eq '}'){$depth--}; $end++}
    return $source.Substring($start,$end-$start)
}
$marker='private BuildingConsumerFallbackResult TryApplyBuildingConsumerFallback('
$old=Block $before $marker
$new=Block $after $marker
$bindings=[ordered]@{
    'List<int> diggerUnitIds = new List<int>();'='List<int> diggerUnitIds = work.DiggerUnitIds;'
    'var retained = new Dictionary<int, BuildingApproachCandidate>();'='var retained = work.Retained;'
    'var candidates = new List<BuildingApproachCandidate>();'='var candidates = work.Candidates;'
    'var targets = new List<int>();'='var targets = work.Targets;'
    'var pending = new List<int>();'='var pending = work.Pending;'
    'var reserved = new HashSet<int>();'='var reserved = work.Reserved;'
    'var leaderStarts = new List<int>();'='var leaderStarts = work.LeaderStarts;'
    'var otherStarts = new List<int>();'='var otherStarts = work.OtherStarts;'
    'var remaining = new List<int>(); var remainingIndices = new List<int>();'='var remaining = work.Remaining; var remainingIndices = work.RemainingIndices;'
}
foreach($entry in $bindings.GetEnumerator()) {
    if(-not $new.Contains($entry.Value)){throw "Missing binding: $($entry.Value)"}
    $new=$new.Replace($entry.Value,$entry.Key)
}
$new=[regex]::Replace($new,'BuildingFallbackWorkBuffers work = RentBuildingFallbackWorkBuffers\(\);\s*try\s*\{','')
$new=[regex]::Replace($new,'\}\s*finally\s*\{\s*ReturnBuildingFallbackWorkBuffers\(work\);\s*\}','')
if([regex]::Replace($new,'\s','') -cne [regex]::Replace($old,'\s','')) {
    throw 'Worker tokens changed beyond container binding and try/finally wrapper.'
}
# Compile actual storage implementation; no game DLL or native execution.
$poolStart=$after.IndexOf('private readonly Stack<BuildingFallbackWorkBuffers>')
$poolEnd=$after.IndexOf('private readonly Stack<MoatCandidateField>', $poolStart)
$pool=$after.Substring($poolStart,$poolEnd-$poolStart)
$candidate=Block $after 'private struct BuildingApproachCandidate'
$code="using System; using System.Collections; using System.Collections.Generic; using System.Diagnostics; public class FriendlyBufferTest {`n"+$pool+$candidate
$fill=@'
            for (int i = 0; i < 16; i++) {
                diggerUnitIds.Add(i); retained[i] = new BuildingApproachCandidate(i, i + 1, i + 2);
                candidates.Add(retained[i]); targets.Add(i); pending.Add(i); reserved.Add(i);
                leaderStarts.Add(i); otherStarts.Add(i); remaining.Add(i); remainingIndices.Add(i);
            }
            return diggerUnitIds.Count + retained.Count + candidates.Count + targets.Count + pending.Count + reserved.Count + leaderStarts.Count + otherStarts.Count + remaining.Count + remainingIndices.Count;
'@
$code+="`nprivate int Old() {`n"+($bindings.Keys -join "`n")+"`n"+$fill+"`n}`n"
$code+="`nprivate int New() { var work = RentBuildingFallbackWorkBuffers(); try {`n"+($bindings.Values -join "`n")+"`n"+$fill+"`n} finally { ReturnBuildingFallbackWorkBuffers(work); } }`n"
$code+=@'
private static void Assert(bool ok, string message) { if(!ok) throw new Exception(message); }
private static void AssertClear(BuildingFallbackWorkBuffers b) {
    foreach(var field in typeof(BuildingFallbackWorkBuffers).GetFields()) {
        object value=field.GetValue(b);
        int count=(int)value.GetType().GetProperty("Count").GetValue(value);
        Assert(count==0, "Not cleared: "+field.Name);
    }
}
public static string Run() {
    var test=new FriendlyBufferTest();
    for(int i=0;i<100;i++) Assert(test.Old()==test.New(), "Container workload differs");
    Assert(test.buildingFallbackWorkBuffers.Count==1, "Lease not returned");
    var first=test.RentBuildingFallbackWorkBuffers(); first.DiggerUnitIds.Add(99);
    var second=test.RentBuildingFallbackWorkBuffers(); Assert(!ReferenceEquals(first,second), "Nested leases alias");
    second.DiggerUnitIds.Add(1); Assert(first.DiggerUnitIds[0]==99, "Nested overwrite");
    test.ReturnBuildingFallbackWorkBuffers(second); AssertClear(second);
    Assert(first.DiggerUnitIds[0]==99, "Returning nested lease clears outer");
    test.ReturnBuildingFallbackWorkBuffers(first); AssertClear(first);
    try { var lease=test.RentBuildingFallbackWorkBuffers(); try { lease.Reserved.Add(3); throw new InvalidOperationException(); } finally { test.ReturnBuildingFallbackWorkBuffers(lease); } } catch(InvalidOperationException) {}
    Assert(test.buildingFallbackWorkBuffers.Count==2, "Failure loses lease");
    foreach(var b in test.buildingFallbackWorkBuffers) AssertClear(b);
    var watch=new Stopwatch(); long oldStart=GC.GetAllocatedBytesForCurrentThread(); watch.Start();
    for(int i=0;i<10000;i++) test.Old();
    watch.Stop(); long oldBytes=GC.GetAllocatedBytesForCurrentThread()-oldStart; long oldTicks=watch.ElapsedTicks;
    long newStart=GC.GetAllocatedBytesForCurrentThread(); watch.Restart();
    for(int i=0;i<10000;i++) test.New();
    watch.Stop(); long newBytes=GC.GetAllocatedBytesForCurrentThread()-newStart;
    Assert(newBytes < oldBytes, "No allocation reduction");
    return $"Worker tokens identical; nested/exception cleanup passed. Scratch workload 10000 x 16: {oldBytes} -> {newBytes} bytes; elapsed ticks {oldTicks} -> {watch.ElapsedTicks}. Not a game benchmark.";
}
}
'@
Add-Type -TypeDefinition $code -Language CSharp
[FriendlyBufferTest]::Run()
