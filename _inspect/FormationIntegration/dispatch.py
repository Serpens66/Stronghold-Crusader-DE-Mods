exec((__import__('pathlib').Path(__file__).parent/'integrate.py').read_text().split("for name in ['FormationModel'")[0])
p=shared/'FormationRuntime.cs';s=read(p)
for prefix in ['        private IDisposable tribeMoveSubscription;\n','        private IDisposable unitMoveSubscription;\n','            IDisposable pendingTribeMove = null;\n','            IDisposable pendingUnitMove = null;\n','                pendingUnitMove?.Dispose();\n','                pendingTribeMove?.Dispose();\n','                tribeMoveSubscription = pendingTribeMove;\n                pendingTribeMove = null;\n','                unitMoveSubscription = pendingUnitMove;\n                pendingUnitMove = null;\n']:
    s=s.replace(prefix,'')
start=s.index('                pendingTribeMove = TribeR3EventHooks.');end=s.index('                pendingKeyDown =',start)
s=s[:start]+s[end:]
write(p,s)
p=shared/'UnitCommandPathAPI.cs';s=read(p).replace('        internal static LargeMoveTargetMarkerRenderer MoveMarkers', '        internal static bool FormationDispatchActive => Runtime?.formationRuntime?.IsDispatching == true;\n        internal static LargeMoveTargetMarkerRenderer MoveMarkers');write(p,s)
p=main/'ExtendedShiftCommandQueueRuntime.cs';s=read(p)
s=s.replace('''            if (args.Phase == EventHookPhase.Pre)
            {
            }

''','')
s=s.replace('                // Formation hooks have completed; never retain an unmatched command snapshot.\n','')
# A previously accepted synchronized formation packet cannot become a Shift waypoint
# because the user's modifier state changed while it was in flight.
start=s.index('        private void OnMoveOrderCore(');end=s.index('        private void SuppressCurrentMoveObservation()',start)
body=s[start:end].replace('if (!IsShiftPressed())','if (!IsShiftPressed() || UnitCommandPathAPI.FormationDispatchActive)').replace('                    SuppressCurrentMoveObservation();\n','').replace('            SuppressCurrentMoveObservation();\n','')
s=s[:start]+body+s[end:]
s=remove_method(s,'private void SuppressCurrentMoveObservation()')
write(p,s)
p=root/'BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualHarness.cs';s=read(p)
s=s.replace('    internal static class MoveFormationCommandContext { internal static Action CaptureForNestedCommand()=>()=>{}; }', '''    internal sealed class FormationRuntime
    {
        internal void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args) { }
    }''')
s=s.replace('        private const int MapWidth=800;','        private FormationRuntime formationRuntime;\n        private const int MapWidth=800;')
write(p,s)
p=root/'BugfixesAndQoL/tests/Formations.Tests/Program.cs';s=read(p)
s=s.replace('''        Check(runtime.Contains("TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable") &&
              runtime.Contains("UnitR3EventHooks.OnUnitMoveHere.Observable"), "existing Extender order and terminal events used");''','''        string dispatch = File.ReadAllText(Path.Combine(shared, "PermanentCommandHooks.cs"));
        string unitDispatch = File.ReadAllText(Path.Combine(shared, "UnitMovementContext.cs"));
        Check(dispatch.Contains("formationRuntime?.OnTribeIssueOrderMoveHere(args)") &&
              unitDispatch.Contains("formationRuntime?.OnUnitMoveHere(args)"), "existing Extender order and terminal dispatch used");
        Check(unitDispatch.IndexOf("formationRuntime?.OnUnitMoveHere(args)", StringComparison.Ordinal) <
              unitDispatch.IndexOf("ObserveUnitMoveOrder(args)", StringComparison.Ordinal), "formation target precedes pathfinding observation");
        string queue = File.ReadAllText(Path.Combine(main, "src", "ExtendedShiftCommandQueueRuntime.cs"));
        Check(queue.Contains("!IsShiftPressed() || UnitCommandPathAPI.FormationDispatchActive"), "accepted packets cannot become Shift waypoints in flight");''')
write(p,s)
# Native/update contract notes, no README or version changes.
note='''
## Integrated formation command contract (2026-10-08)

FormationTest is absorbed into BugfixesAndQoL; checkbox/presets/host synchronization keep
their existing key. FormationRuntime in APIShared uses the existing E1D30/E0970/118E00
dispatchers rather than installing a second native detour. Native 196280 remains owned
by the Script Extender event. Formation targets run before UnitMovementContext Pre;
completion runs after its Post. The accepted protocol-5 command carries kind, density,
direction, width, role placement, unit count and deterministic identity/target hash.
Consumed left/right release state is never restored. Shift queue transport retains only
its own marker; no old density encoding or fallback remains.

FBCB9319 native closure: 879A0/86680, 8B7E0/8C5F0, 195E30, chore 10AE0,
196100/11B520, E1D30/E0970/118E00/119F90/196280, and cursor/overlay
8F3DA..90088, 1222A0/417A0/41D10/41D60/436DE/1A13C0. Preserve authoritative
object, Assassin, terrain, region and owner checks. NativeDetour Indirect contracts are
validated by NativeDetourContracts; marker context displaces 17 bytes to 436EF,
AfterCallback, with All GPRs and replayed TEST/JE. Installed RedBird is exercised by
the copied-buffer regression; no published hook is torn down.

The merged marker renderer is process-owned by UnitCommandPathAPI.MoveMarkers.
Drag preview authorization is memoized per native pass; role dots use the same
authorization. Overflow and queue rendering share the original overlay/draw callbacks.
Installed interop is checked by _inspect/FormationIntegration/Verify-Interop.ps1:
GameUnit 0x490, GameTribe 0x688, cursor view 0x480, including all consumed field
widths/offsets. UnitAccess.IsReallyAlive uses UInt16 r_IsKilledByProjectile at 0x29C.
Game APIs are checked without emission against the true Assembly-CSharp.dll.
Fixes 1.26.0 additions (111C00 and 6A5D0) do not overlap this closure.
Formation config migration reads FormationTest_Serp.cfg without modifying it; existing
main keys win, invalid/missing legacy values use Block/2/Off/true.
Re-audit the complete closure, editor release fields, tile grids and both installed
hook backends after native/Extender updates. Runtime versions remain unchanged during
testing. Multiplayer/gameplay acceptance still requires an actual game session.
'''
for mod in ['APIShared','BugfixesAndQoL']:
    p=root/mod/'UpdateToNewDLL.md';write(p,read(p)+note)
write(root/'_inspect/FormationIntegration/changed-dispatch.txt','\n'.join(changed)+'\n')
