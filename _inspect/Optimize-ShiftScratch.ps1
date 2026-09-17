$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'BugfixesAndQoL/src/ExtendedShiftCommandQueueRuntime.cs'
$source = [IO.File]::ReadAllText($path)
function Replace-Exact([string] $old, [string] $new) {
    if (-not $script:source.Contains($old)) { throw "Missing replacement: $old" }
    $script:source = $script:source.Replace($old, $new)
}
function Wrap-Method([string] $startMarker, [string] $endMarker) {
    $start = $script:source.IndexOf($startMarker)
    $end = $script:source.IndexOf($endMarker, $start)
    if ($start -lt 0 -or $end -lt 0) { throw "Missing method: $startMarker" }
    $part = $script:source.Substring($start, $end - $start)
    $open = $part.IndexOf("        {`r`n") + 11
    $close = $part.LastIndexOf("        }`r`n")
    $body = $part.Substring($open, $close - $open)
    $indented = (($body -split "`r`n" | ForEach-Object { if ($_) { '    ' + $_ } else { '' } }) -join "`r`n").TrimEnd([char]13,[char]10)
    $part = $part.Substring(0,$open) + "            QueueWorkBuffers buffers = RentWorkBuffers();`r`n            try`r`n            {`r`n" + $indented + "`r`n            }`r`n            finally`r`n            {`r`n                buffers.Clear();`r`n                workBufferPool.Push(buffers);`r`n            }`r`n        }`r`n`r`n"
    $script:source = $script:source.Substring(0,$start) + $part + $script:source.Substring($end)
}
Replace-Exact '        private readonly List<long> cohortIdBuffer = new List<long>();' '        private readonly Stack<QueueWorkBuffers> workBufferPool = new Stack<QueueWorkBuffers>();'
# Each method borrows its own workspace, including nested calls to ReconcileCohorts.
Replace-Exact 'cohortIdBuffer' 'buffers.CohortIds'
Replace-Exact 'List<QueueUnitIdentity> tribeMembers = CaptureTribeMembers(tribeId);' "List<QueueUnitIdentity> tribeMembers = buffers.Members;`r`n            CaptureTribeMembers(tribeId, tribeMembers);"
Replace-Exact 'List<TribeQueueState> affected = new List<TribeQueueState>();' 'List<TribeQueueState> affected = buffers.States;'
Replace-Exact 'List<QueueUnitIdentity> unqueued = new List<QueueUnitIdentity>();' 'List<QueueUnitIdentity> unqueued = buffers.OtherMembers;'
Replace-Exact 'HashSet<long> seen = new HashSet<long>();' 'HashSet<long> seen = buffers.SeenCohorts;'
Replace-Exact 'List<QueueUnitIdentity> affected = CaptureTribeMembers(tribeId);' "List<QueueUnitIdentity> affected = buffers.Members;`r`n            CaptureTribeMembers(tribeId, affected);"
Replace-Exact "                SortedDictionary<int, List<QueueUnitIdentity>> branches =`r`n                    new SortedDictionary<int, List<QueueUnitIdentity>>();`r`n                foreach (QueueUnitIdentity member in state.Members.ToArray())" "                buffers.ClearBranches();`r`n                SortedDictionary<int, List<QueueUnitIdentity>> branches = buffers.Branches;`r`n                buffers.Members.Clear();`r`n                buffers.Members.AddRange(state.Members);`r`n                foreach (QueueUnitIdentity member in buffers.Members)"
Replace-Exact 'branch = new List<QueueUnitIdentity>();' 'branch = buffers.RentBranch();'
Replace-Exact "            List<QueueUnitIdentity> moved = new List<QueueUnitIdentity>();`r`n            foreach (QueueUnitIdentity member in state.Members.OrderBy(value => value.UnitId))" "            List<QueueUnitIdentity> moved = buffers.OtherMembers;`r`n            // Constructor and ReplaceMembers keep this list sorted by UnitId/GlobalId.`r`n            buffers.Members.AddRange(state.Members);`r`n            foreach (QueueUnitIdentity member in buffers.Members)"
Replace-Exact "            Dictionary<int, List<TribeQueueState>> byTribe =`r`n                new Dictionary<int, List<TribeQueueState>>();" '            Dictionary<int, List<TribeQueueState>> byTribe = buffers.ByTribe;'
Replace-Exact 'states = new List<TribeQueueState>();' 'states = buffers.RentStates();'
Replace-Exact 'List<int> tribeIds = byTribe.Keys.ToList();' "List<int> tribeIds = buffers.TribeIds;`r`n            tribeIds.AddRange(byTribe.Keys);"
Replace-Exact "                        List<QueueUnitIdentity> merged = left.Members.Concat(right.Members)`r`n                            .Distinct().OrderBy(member => member.UnitId).ThenBy(member => member.GlobalId).ToList();" "                        List<QueueUnitIdentity> merged = buffers.Members;`r`n                        merged.Clear();`r`n                        buffers.SeenMembers.Clear();`r`n                        foreach (QueueUnitIdentity member in left.Members)`r`n                            if (buffers.SeenMembers.Add(member)) merged.Add(member);`r`n                        foreach (QueueUnitIdentity member in right.Members)`r`n                            if (buffers.SeenMembers.Add(member)) merged.Add(member);`r`n                        merged.Sort(QueueUnitIdentity.Compare);"
Replace-Exact "        private static List<QueueUnitIdentity> CaptureTribeMembers(int tribeId)`r`n        {`r`n            List<QueueUnitIdentity> members = new List<QueueUnitIdentity>();" "        private static void CaptureTribeMembers(int tribeId, List<QueueUnitIdentity> members)`r`n        {"
Replace-Exact "            return members;`r`n        }`r`n`r`n        private bool IsLocalSelectedTribe" "        }`r`n`r`n        private bool IsLocalSelectedTribe"
Wrap-Method '        private bool TryApplyQueuedCommand(' '        private TribeQueueState CreateCohort('
Wrap-Method '        private void CancelQueuesForTribeUnits(' '        private void ReconcileCohorts('
Wrap-Method '        private void ReconcileCohorts(' '        private bool EnsureDedicatedTribe('
Wrap-Method '        private bool EnsureDedicatedTribe(' '        private void LogIsolationFailure('
Wrap-Method '        private void CoalesceEquivalentCohorts(' '        private static bool HaveEquivalentExecutionState('
# Borrow only after the idle guard so idle ticks still allocate nothing.
$start = $source.IndexOf('            PruneExpectedMoveSignals(expectedMoveChores);', $source.IndexOf('        private void OnTickCore('))
$end = $source.IndexOf('        private void ProcessCohort(', $start)
$part = $source.Substring($start,$end-$start)
$body = $part.Substring(0,$part.LastIndexOf("        }`r`n")).TrimEnd([char]13,[char]10)
$indented = ($body -split "`r`n" | ForEach-Object { if ($_) { '    ' + $_ } else { '' } }) -join "`r`n"
$source = $source.Substring(0,$start) + "            QueueWorkBuffers buffers = RentWorkBuffers();`r`n            try`r`n            {`r`n$indented`r`n            }`r`n            finally`r`n            {`r`n                buffers.Clear();`r`n                workBufferPool.Push(buffers);`r`n            }`r`n        }`r`n`r`n" + $source.Substring($end)
$helper = @'
        private QueueWorkBuffers RentWorkBuffers() =>
            workBufferPool.Count == 0 ? new QueueWorkBuffers() : workBufferPool.Pop();

        // Workspaces never escape a call. Nested work borrows another instance; persistent
        // cohort members are copied by TribeQueueState, never backed by these scratch lists.
        private sealed class QueueWorkBuffers
        {
            internal readonly List<long> CohortIds = new List<long>();
            internal readonly List<int> TribeIds = new List<int>();
            internal readonly List<QueueUnitIdentity> Members = new List<QueueUnitIdentity>();
            internal readonly List<QueueUnitIdentity> OtherMembers = new List<QueueUnitIdentity>();
            internal readonly List<TribeQueueState> States = new List<TribeQueueState>();
            internal readonly HashSet<long> SeenCohorts = new HashSet<long>();
            internal readonly HashSet<QueueUnitIdentity> SeenMembers = new HashSet<QueueUnitIdentity>();
            internal readonly SortedDictionary<int, List<QueueUnitIdentity>> Branches =
                new SortedDictionary<int, List<QueueUnitIdentity>>();
            internal readonly Dictionary<int, List<TribeQueueState>> ByTribe =
                new Dictionary<int, List<TribeQueueState>>();
            private readonly Stack<List<QueueUnitIdentity>> branchPool = new Stack<List<QueueUnitIdentity>>();
            private readonly Stack<List<TribeQueueState>> statePool = new Stack<List<TribeQueueState>>();

            internal List<QueueUnitIdentity> RentBranch() =>
                branchPool.Count == 0 ? new List<QueueUnitIdentity>() : branchPool.Pop();
            internal List<TribeQueueState> RentStates() =>
                statePool.Count == 0 ? new List<TribeQueueState>() : statePool.Pop();

            internal void ClearBranches()
            {
                foreach (List<QueueUnitIdentity> branch in Branches.Values)
                {
                    branch.Clear();
                    branchPool.Push(branch);
                }
                Branches.Clear();
            }

            internal void Clear()
            {
                ClearBranches();
                foreach (List<TribeQueueState> states in ByTribe.Values)
                {
                    states.Clear();
                    statePool.Push(states);
                }
                ByTribe.Clear();
                CohortIds.Clear();
                TribeIds.Clear();
                Members.Clear();
                OtherMembers.Clear();
                States.Clear();
                SeenCohorts.Clear();
                SeenMembers.Clear();
            }
        }

'@
$helper = [regex]::Replace($helper,'\r?\n',"`r`n") + "`r`n"
Replace-Exact '        private static bool HaveEquivalentExecutionState(' ($helper + '        private static bool HaveEquivalentExecutionState(')
[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
if ([IO.File]::ReadAllText($path) -cne $source) { throw 'Write verification failed' }
if ([regex]::Matches($source,'(?<!\r)\n').Count -or $source.Contains('\r\n')) { throw 'CRLF failure' }
"Runtime CRLF=$([regex]::Matches($source,"`r`n").Count), bareLF=0"
