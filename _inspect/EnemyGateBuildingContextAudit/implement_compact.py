from pathlib import Path
root=Path('Testmods/EnemyGatePathfindingTest')
def read(p): return (root/p).read_text(encoding='utf-8-sig')
def write(p,t):
    data=t.replace('\r\n','\n').replace('\r','\n').replace('\n','\r\n').encode('utf-8')
    (root/p).write_bytes(data)
    assert (root/p).read_bytes()==data
def replace(t,a,b):
    assert a in t,a[:120]
    return t.replace(a,b)
p='src/EnemyGatePathfindingTestPlugin.cs';t=read(p)
t=replace(t,'private static ManualLogSource persistentLog;','private static ManualLogSource persistentLog;\n        private static bool detailedDiagnostics;')
t=replace(t,'persistentLog = Logger;','persistentLog = Logger;\n            detailedDiagnostics = Config.Bind("Diagnostics", "DetailedDiagnostics", false,\n                "Enable detailed gate and temporary Raid/Assassin diagnostics. Restart the game after changing this local option.").Value;')
t=replace(t,'$"{PluginName} {PluginVersion} loaded; no settings are used; " +\n                "APIShared provides the editor-capable mission lifecycle."', '$"{PluginName} {PluginVersion}: DetailedDiagnostics={detailedDiagnostics}, " +\n                $"hookOwner={(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BugfixesAndQoL_Serp") ? "BugfixesAndQoL" : "standalone")}; local option requires restart."')
for name in ('targetOrderSubscription','tribeMoveSubscription','unitMoveSubscription'):
    t=replace(t,f'if ({name} == null)',f'if (detailedDiagnostics && {name} == null)')
t=replace(t,'requireCurrentVersion: true);','requireCurrentVersion: true, logSuccess: detailedDiagnostics);')
t=replace(t,'new EnemyGatePathfindingRuntime(persistentLog)','new EnemyGatePathfindingRuntime(persistentLog, detailedDiagnostics)')
t=replace(t,'Shared.DebugLogHelper.LogInfo(\n                    persistentLog,\n                    $"Script Extender identity:', 'if (detailedDiagnostics) Shared.DebugLogHelper.LogInfo(\n                    persistentLog,\n                    $"Script Extender identity:')
write(p,t)
p='src/EnemyGatePathfindingRuntime.cs';t=read(p)
t=replace(t,'private readonly ManualLogSource log;','private readonly ManualLogSource log;\n        private readonly bool detailedDiagnostics;\n        private readonly DeferredGateDiagnosticErrors deferredErrors = new DeferredGateDiagnosticErrors();\n        private string lastIntegrityErrorState;')
t=replace(t,'EnemyGatePathfindingRuntime(ManualLogSource log)','EnemyGatePathfindingRuntime(ManualLogSource log, bool detailedDiagnostics = true)')
t=replace(t,'this.log = log ?? throw new ArgumentNullException(nameof(log));','this.log = log ?? throw new ArgumentNullException(nameof(log));\n            this.detailedDiagnostics = detailedDiagnostics;')
t=replace(t,'new GateTopologySnapshotProvider(log)','new GateTopologySnapshotProvider(log, detailedDiagnostics)')
a='            attackOrderDiagnostics = new AttackOrderCorrelationDiagnostics(log, topologyProvider);'
b='            attackOrderDiagnostics.TemporaryAcceptance = temporaryAcceptance;'
start=t.index(a);end=t.index(b,start)+len(b)
t=t[:start]+'            if (detailedDiagnostics)\n            {\n'+t[start:end]+'\n            }'+t[end:]
t=replace(t,'APIShared.TemporaryGateRouteAcceptanceBridge.Register(temporaryAcceptance);','if (detailedDiagnostics) APIShared.TemporaryGateRouteAcceptanceBridge.Register(temporaryAcceptance);')
t=replace(t,'            Shared.DebugLogHelper.LogInfo(log,\n                "Crash-safe enemy-gate hooks installed:', '            if (detailedDiagnostics) Shared.DebugLogHelper.LogInfo(log,\n                "Crash-safe enemy-gate hooks installed:')
t=replace(t,'if (samePclRouteRuntime?.Installed != true && !friendlyMoatHookOwnerLoaded)','if (detailedDiagnostics && samePclRouteRuntime?.Installed != true && !friendlyMoatHookOwnerLoaded)')
t=replace(t,'                "Enemy-gate map started: Different-PCL filter and native Same-PCL " +','                $"Enemy-gate map started: epoch={topologyProvider?.DiagnosticEpoch}, editor={editor}, " +')
t=replace(t,'                if (Volatile.Read(ref mapActive) != 0 &&\n                    now >=','                FlushDeferredErrors();\n                if (detailedDiagnostics && Volatile.Read(ref mapActive) != 0 &&\n                    now >=')
t=replace(t,'                    Shared.DebugLogHelper.LogInfo(log,\n                        "Script Extender pathfinding globals match','                    if (detailedDiagnostics) Shared.DebugLogHelper.LogInfo(log,\n                        "Script Extender pathfinding globals match')
t=replace(t,'            ref CapturerSample sample = ref samples[(site * DecisionCount) + decisionIndex];','            if (!detailedDiagnostics) return;\n            ref CapturerSample sample = ref samples[(site * DecisionCount) + decisionIndex];')
a='            if (Interlocked.Increment(ref callbackWarnings) <= MaximumCallbackWarningsPerMap)\n                Shared.DebugLogHelper.LogWarning(log,\n                    "Enemy-gate deferred diagnostics failed without changing native behavior: " +\n                    $"{ex.GetType().Name}: {ex.Message}");'
t=replace(t,a,'            Interlocked.Increment(ref callbackWarnings);\n            deferredErrors.Record(ex.GetType().Name, ex.Message);')
t=replace(t,'        private const int MaximumCallbackWarningsPerMap = 8;\n','')
t=replace(t,'            previousStableGateAccess = NativeGateAccessSnapshot.Empty;\n            Array.Clear(siteCalls','            deferredErrors.Reset();\n            lastIntegrityErrorState = null;\n            previousStableGateAccess = NativeGateAccessSnapshot.Empty;\n            Array.Clear(siteCalls')
t=replace(t,'        private void LogDiagnosticCheckpoint(string kind, string reason)\n        {','        private void LogDiagnosticCheckpoint(string kind, string reason)\n        {\n            FlushDeferredErrors();\n            if (!detailedDiagnostics)\n            {\n                LogAcceptanceVerdict(reason);\n                return;\n            }')
needle='            DiagnosticVerdict sameHookVerdict = same.OwnerConflict'
idx=t.index(needle)
t=t[:idx]+'''            if (!detailedDiagnostics)
            {
                Shared.DebugLogHelper.LogInfo(log,
                    $"Enemy-gate map completed: epoch={topologyProvider?.DiagnosticEpoch}, reason={reason}, " +
                    $"queries={same.Queries}, aiQueries={same.AiQueries}, aiNoRoute={same.AiNoRoutes}, " +
                    $"edgeRejected={same.RejectedEdges}, cursorBlocks={same.CursorResultForcedZero}, " +
                    $"capture=[{topologyProvider?.CaptureDiagnostics.Summary ?? "unavailable"}], " +
                    $"errors=[{DescribeIntegrityErrors(same, topology)}], " +
                    $"runtimeIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(hookActivity || same.Queries > 0, runtimeFailed)}, " +
                    "Raid/Assassin routes=NOT_INSPECTED (DetailedDiagnostics=false).");
                return;
            }
'''+t[idx:]
idx=t.index('        private void ResetMapCounters()')
t=t[:idx]+'''        private string DescribeIntegrityErrors(SamePclCoverageSnapshot same, TopologyCoverageSnapshot topology) =>
            $"callback={Volatile.Read(ref callbackWarnings)},snapshot={topology.Errors},exceptions={same.Exceptions}," +
            $"slots={same.SlotConflicts},scope={same.ScopeMismatches},player={same.InvalidPlayers},pool={same.PoolExhaustions}," +
            $"tacticalPlayer={same.AiTacticalInvalidPlayers},tacticalScope={same.AiTacticalScopeConflicts},tacticalExceptions={same.AiTacticalExceptions}," +
            $"identity={DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch)},owner={DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch)}," +
            $"queryPlayer={DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer)},nativeException={DecisionTotal(NativeGateSnapshotDecision.Exception)}," +
            $"unexpectedGate={Read(ref untrackedUnexpectedGate)}";

        private void FlushDeferredErrors()
        {
            foreach (string error in deferredErrors.DrainNewCauses())
                Shared.DebugLogHelper.LogWarning(log, "Enemy-gate deferred failure: " + error);
            if (detailedDiagnostics || Volatile.Read(ref mapActive) == 0) return;
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            TopologyCoverageSnapshot topology = topologyProvider?.GetCoverageSnapshot() ?? default;
            // Only a newly observed category emits a warning; repetitions remain in the final totals.
            string causes = $"{topology.Errors > 0}:{same.Exceptions > 0}:{same.SlotConflicts > 0}:" +
                $"{same.ScopeMismatches > 0}:{same.InvalidPlayers > 0}:{same.PoolExhaustions > 0}:" +
                $"{same.AiTacticalInvalidPlayers > 0}:{same.AiTacticalScopeConflicts > 0}:{same.AiTacticalExceptions > 0}:" +
                $"{DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch) > 0}:{DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch) > 0}:" +
                $"{DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer) > 0}:{DecisionTotal(NativeGateSnapshotDecision.Exception) > 0}:" +
                $"{Read(ref untrackedUnexpectedGate) > 0}";
            bool failed = causes.IndexOf("True", StringComparison.Ordinal) >= 0;
            if (failed && !string.Equals(causes, lastIntegrityErrorState, StringComparison.Ordinal))
                Shared.DebugLogHelper.LogWarning(log, "Enemy-gate integrity errors (complete totals at map end): " + DescribeIntegrityErrors(same, topology));
            lastIntegrityErrorState = causes;
        }

'''+t[idx:]
write(p,t)
p='src/GateTopologySnapshotProvider.cs';t=read(p)
t=replace(t,'internal GateTopologySnapshotProvider(ManualLogSource log)','private readonly bool detailedDiagnostics;\n        internal int DiagnosticEpoch => Volatile.Read(ref epochNumber);\n\n        internal GateTopologySnapshotProvider(ManualLogSource log, bool detailedDiagnostics = true)')
t=replace(t,'this.log = log ?? throw new ArgumentNullException(nameof(log));','this.log = log ?? throw new ArgumentNullException(nameof(log));\n            this.detailedDiagnostics = detailedDiagnostics;')
t=t.replace('Shared.DebugLogHelper.LogInfo(log,','if (detailedDiagnostics) Shared.DebugLogHelper.LogInfo(log,')
write(p,t)
p='src/SamePclGateRouteRuntime.cs';t=read(p)
t=replace(t,'        internal SamePclGateRouteRuntime(ManualLogSource log,','        private readonly IEnemyGatePathPolicy registeredProvider;\n        private bool DetailedDiagnostics => attackOrderDiagnostics != null;\n\n        internal SamePclGateRouteRuntime(ManualLogSource log,')
t=replace(t,'            this.attackOrderDiagnostics = attackOrderDiagnostics;','            this.attackOrderDiagnostics = attackOrderDiagnostics;\n            registeredProvider = DetailedDiagnostics ? (IEnemyGatePathPolicy)this : new FunctionalGatePolicyAdapter(this, this);')
t=replace(t,'EnemyGatePathPolicyBridge.TryRegister(this)','EnemyGatePathPolicyBridge.TryRegister(registeredProvider)')
t=t.replace('Shared.DebugLogHelper.LogInfo(log,','if (DetailedDiagnostics) Shared.DebugLogHelper.LogInfo(log,')
for signature in ('private void CaptureScopeSample(QueryKind kind, int requested, int native, int tribe, int used)',):
    t=replace(t,signature+'\n        {',signature+'\n        {\n            if (!DetailedDiagnostics) return;')
write(p,t)
