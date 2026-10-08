from pathlib import Path
import shutil
root=Path.cwd(); out=root/'_inspect/FormationUnitHover'
paths=['APIShared/src/UnitCommands/FormationRuntime.cs','APIShared/src/UnitCommands/GroundMovePreviewAuthorization.cs','BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/Program.cs','BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationStartupTests.cs','_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_COMMAND_RELEASE.md']
for rel in paths:
    dst=out/'before'/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/rel,dst)
def save(rel,s):
    data=s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8');(root/rel).write_bytes(data);assert (root/rel).read_bytes()==data
p=paths[1];s=(root/p).read_text(encoding='utf-8-sig')
pos=s.index('        internal bool GroundAllowed')
s=s[:pos]+'''        // Terminal ordinary-Move proof remains authoritative over unit hover.
        internal bool FormationMoveAllowed => Mode == 1 && Kind == 3 && File == 0x6B &&
            ((Image == 0 && Command == 1) || (Image == 0x20 && Command == 9)) &&
            CommandDetail == 0;
'''+s[pos:]
pos=s.index('    // Read only.')
s=s[:pos]+'''    // Formation-only proof; the strict queue ground policy remains unchanged.
    internal sealed class FormationMoveAuthorization
    {
        private readonly int player, tribe, count, x, y;
        private readonly long startedAfterGeneration;
        private long observedGeneration;
        internal bool IsConfirmed { get; private set; }

        internal FormationMoveAuthorization(int player, int tribe, int count,
            int x, int y, long startedAfterGeneration)
        {
            this.player = player; this.tribe = tribe; this.count = count;
            this.x = x; this.y = y;
            this.startedAfterGeneration = startedAfterGeneration;
            observedGeneration = startedAfterGeneration;
        }

        internal bool Observe(GroundMoveFeedback feedback, long generation, bool coherentCursor)
        {
            if (feedback.Player != player || feedback.Tribe != tribe ||
                feedback.Count != count || feedback.Mode != 1)
                return IsConfirmed = false;
            if (generation <= startedAfterGeneration || generation <= observedGeneration)
                return IsConfirmed;
            observedGeneration = generation;
            // A later hover supplies facing, never a replacement command anchor.
            // Incoherent snapshots cannot establish or replace command proof.
            if (coherentCursor && feedback.X == x && feedback.Y == y)
                IsConfirmed = feedback.FormationMoveAllowed;
            return IsConfirmed;
        }
    }

'''+s[pos:];save(p,s)
p=paths[0];s=(root/p).read_text(encoding='utf-8-sig')
s=s.replace('private NativeGroundMoveFeedbackReader groundFeedbackReader;','private NativeGroundMoveFeedbackReader groundFeedbackReader;\n        private long nativeFeedbackGeneration;')
s=s.replace('EvaluateFixedGroundTarget(', 'EvaluateFormationTargetBounds(')
s=s.replace('if (Enabled && state != null)\n                self.AllowZoom = false;', 'if (Enabled && state != null && state.Authorization.IsConfirmed)\n                self.AllowZoom = false;')
s=s.replace('float wheel = Input.mouseScrollDelta.y;', 'float wheel = state.Authorization.IsConfirmed ? Input.mouseScrollDelta.y : 0f;')
s=s.replace('state.Authorization = new GroundMovePreviewAuthorization(', 'state.MapEpoch = commandRuntime.mapEpoch;\n            state.Authorization = new FormationMoveAuthorization(')
s=s.replace('                target.NativeX, target.NativeY);\n            state.PreviewAuthorization', '                target.NativeX, target.NativeY, nativeFeedbackGeneration);\n            menuViewModel.SetPreviewAuthorization(false);\n            state.PreviewAuthorization')
s=s.replace('internal GroundMovePreviewAuthorization Authorization;', 'internal FormationMoveAuthorization Authorization;\n            internal int MapEpoch;')
start=s.index('            int[] underCursor = null;',s.index('private bool TryCaptureCommandTarget('));end=s.index('        private Shared.GroundMovePreviewRejection EvaluateCommandMode()',start)
s=s[:start]+'''            // A candidate is not an object-free-ground verdict. Coherent final
            // Vanilla feedback, observed after native dispatch, authorizes Move.
            rejection = EvaluateFormationTargetBounds(target);
            return rejection == Shared.GroundMovePreviewRejection.None;
        }

'''+s[end:]
start=s.index('            return Shared.GroundMovePreviewEligibility.EvaluateFixedTarget(',s.index('private Shared.GroundMovePreviewRejection EvaluateFormationTargetBounds'));end=s.index('\n        private static bool IsTargetInsideNativeMap',start)
s=s[:start]+'''            return insideMap ? Shared.GroundMovePreviewRejection.None :
                Shared.GroundMovePreviewRejection.OutsideMap;
        }
'''+s[end:]
old='''            originalEntered = true;
            return engineRunOriginal(mpFrameSkip);'''
assert old in s
s=s.replace(old,'''            originalEntered = true;
            int result = engineRunOriginal(mpFrameSkip);
            // The real run returns zero without a free buffer. Positive output
            // follows DLL_RunTick; the marker pass observes final cursor feedback
            // before Vanilla clears its cursor kind at the render tail.
            if (result > 0)
            {
                lock (stateSync) nativeFeedbackGeneration++;
            }
            return result;''')
needle='''                bool releaseClaimed;
                lock (stateSync)'''
assert needle in s
s=s.replace(needle,'''                if (!ValidateActiveDrag(state))
                {
                    AbortDrag("release-state-changed");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }
                if (!state.Authorization.IsConfirmed)
                {
                    if (FormationReleaseStateModel.HasCommandRelease(inputState, state.CommandButton))
                        AbortDrag("release-unconfirmed-move");
                    return RunOriginalOnce(mpFrameSkip, ref originalEntered);
                }

                bool releaseClaimed;
                lock (stateSync)''',1)
old='''            lock (stateSync)
                allowed = Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state) &&
                    state.Authorization.Observe(groundFeedbackReader.Read());'''
assert old in s
s=s.replace(old,'''            lock (stateSync)
            {
                allowed = false;
                if (Enabled && ReferenceEquals(drag, state) && ValidateActiveDrag(state))
                {
                    GroundMoveFeedback feedback = groundFeedbackReader.Read();
                    GameCursorManager* cursor = GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                    bool coherent = cursor != null && cursor->r_IsCursorInGame == 1 &&
                        cursor->r_MouseTileX == (uint)feedback.X &&
                        cursor->r_MouseTileY == (uint)feedback.Y;
                    bool wasConfirmed = state.Authorization.IsConfirmed;
                    allowed = state.Authorization.Observe(feedback, nativeFeedbackGeneration, coherent);
                    if (allowed && !wasConfirmed)
                        LogDebugNoThrow($"FORMATION_MOVE_CONFIRMED: tribe={state.TribeId}, " +
                            $"target={state.Target.NativeX},{state.Target.NativeY}, " +
                            $"generation={nativeFeedbackGeneration}, hoveredUnit={feedback.HoveredUnit}.");
                }
            }''')
s=s.replace('markerRenderer.ReplacementAvailable && HasValidMap() && !IsShiftHeld() &&', 'markerRenderer.ReplacementAvailable && HasValidMap() && !IsShiftHeld() &&\n            state.MapEpoch == commandRuntime.mapEpoch &&')
save(p,s)
p=paths[2];s=(root/p).read_text();s=s.replace('FormationStartupTests.Validate(root);','FormationStartupTests.Validate(root);\nFormationMoveRuntimeTests.Validate(root);');save(p,s)
p=paths[3];s=(root/p).read_text();s=s.replace('internal GroundTarget Target;\n        }','internal GroundTarget Target;\n            internal AuthorizationFixture Authorization=new AuthorizationFixture();\n        }\n        private sealed class AuthorizationFixture { internal bool IsConfirmed=true; }');save(p,s)
p=paths[4];s=(root/p).read_text();s+='''

## 2026-10-09: formation Move classification over units

Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Rechecked the complete command-decision export at 8C5F0, target resolver 79B90 and Move staging 195E30, with the input and terminal flow above. The earlier requirement for empty unit hover/tile occupancy describes our conservative ground-only preview policy, not a general Vanilla prohibition on Move over units.

Ordinary mode 1 initializes file 6B/image 0/command 1/detail 0. Object interactions and attacks set distinct detail values (1..6, 10..12, etc.); rejected routes use detail -10, file AC/image 41. Final kind 3 plus the accepted file/image/command pair and detail 0 classifies ordinary Move; hovered-unit identity alone does not invalidate it. Attack Here remains a separate mode even when staging a preliminary Move. Confidence: confirmed-static; visible game and multiplayer acceptance remain separate.

Formation uses separate authorization from the unchanged strict queue ground policy. A candidate waits for newer positive EngineInterface.run output and coherent terminal-render cursor feedback at its captured anchor. The real managed run calls preDLLCallActions then DLL_RunTick when a free buffer exists; its no-buffer path returns zero. Proof is read in the existing first tile-render callback before the render tail clears cursor kind. Later hover affects facing only. Selection/player/group/mode and map identity changes invalidate proof; unconfirmed releases remain untouched for Vanilla. Existing accepted-release consumption and per-slot placement checks remain authoritative.
''';save(p,s)
