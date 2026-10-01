from pathlib import Path
def write(p,s): Path(p).write_bytes(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))
p='Testmods/EnemyGatePathfindingTest/tests/Program.cs'
s=Path(p).read_text(encoding='utf-8-sig').replace('CursorPreviewDecisionIsCausalAndStable();','CursorPreviewDecisionIsCausalAndStable();\n                CursorCacheIsExactAndBounded();')
s=s.replace('ExtractMethodBody(runtime, "ProcessCursorPreview")','ExtractMethodBody(runtime, "TryValidateCursorPreview")')
s=s.replace('"positive Same- and Different-PCL cursor decisions share the throttled tile validation"','"positive cursor decisions share immediate tile validation"')
s=s.replace('"deferred preview validates one unit with reference-first and filtered-last DB650 searches"','"immediate preview validates one unit with reference-first and filtered-last DB650 searches"')
s=s.replace('runtime.IndexOf("cursorRefreshMs=200", StringComparison.Ordinal) >= 0,\n                "cursor preview is globally throttled to five native validations per second"', '''runtime.IndexOf("cursorCacheTtlMs=200", StringComparison.Ordinal) >= 0 &&
                    callback.Contains("TryValidateCursorPreview") &&
                    !runtime.Contains("ProcessCursorPreview"),
                "cursor cache has a 200ms TTL without deferred/global target throttling"''')
a=s.index('        private static void CursorPreviewDecisionIsCausalAndStable()')
s=s[:a]+'''        private static void CursorCacheIsExactAndBounded()
        {
            var cache = new CursorPreviewCache(200);
            var key = new CursorPreviewCache.Key(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
            cache.Put(key, 1000, false);
            bool allowed;
            Assert(cache.TryGet(key, 1200, out allowed) && !allowed, "exact blocked target is reused through TTL");
            Assert(!cache.TryGet(key, 1201, out allowed) && allowed, "cache reads never extend TTL");
            cache.Put(key, 1000, false);
            Assert(!cache.TryGet(key, 999, out allowed), "backwards clock invalidates cache");
            cache.Put(key, 1000, false);
            for (int field = 0; field < 12; field++)
            {
                int[] values = { 1,2,3,4,5,6,7,8,9,10,11,12 };
                values[field]++;
                var changed = new CursorPreviewCache.Key(values[0],values[1],values[2],values[3],
                    values[4],values[5],values[6],values[7],values[8],values[9],values[10],(ulong)values[11]);
                Assert(!cache.TryGet(changed, 1000, out allowed), "cache field " + field + " is exact");
            }
            for (int target = 0; target < 100; target++)
            {
                var current = new CursorPreviewCache.Key(1,2,3,4,5,target,7,8,9,10,11,12);
                if (!cache.TryGet(current, 1000, out allowed)) cache.Put(current, 1000, target % 2 == 0);
                Assert(cache.TryGet(current, 1000, out allowed) && allowed == (target % 2 == 0),
                    "eviction does not omit target " + target);
            }
            cache.Clear();
            Assert(!cache.TryGet(key, 1000, out allowed), "map reset clears approvals and denials");
        }

'''+s[a:]
write(p,s)
p='BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests/Program.cs'
s=Path(p).read_text(encoding='utf-8-sig').replace('        CheckGroundMovePreviewEligibility();','        CheckGroundMovePreviewEligibility();\n        CheckGroundMoveAuthorization();')
a=s.index('    private static void CheckGroundMovePreviewEligibility()')
s=s[:a]+'''    private static void CheckGroundMoveAuthorization()
    {
        GroundMoveFeedback Feedback(int x = 10, int y = 20, int player = 1,
            int tribe = 2, int count = 3, int mode = 1, int kind = 3, int file = 0x6B,
            int image = 0, int command = 1, int detail = 0, int unit = 0, int building = 0,
            int wall = 0) => new GroundMoveFeedback(player, tribe, count, mode, x, y,
                kind, file, image, command, detail, unit, building, wall);
        var gate = new GroundMovePreviewAuthorization(1,2,3,10,20);
        Check(!gate.Observe(Feedback(x: 11)), "new gesture waits for matching anchor output");
        Check(gate.Observe(Feedback()), "ordinary native ground approval enables preview");
        Check(gate.Observe(Feedback(x: 11, kind: 5, file: 0xAC, image: 0x41, detail: -10)),
            "drag hover rejection does not replace fixed command point");
        Check(!gate.Observe(Feedback(kind: 5, file: 0xAC, image: 0x41, detail: -10)),
            "native rejection at anchor hides markers without testmod dependency");
        Check(!gate.Observe(Feedback(x: 11)), "moving away cannot authorize rejected anchor");
        Check(gate.Observe(Feedback(image: 0x20, command: 9)), "native alternate ground approval retained");
        Check(!gate.Observe(Feedback(player: 2)), "player switch clears proof");
        Check(!gate.Observe(Feedback(x: 11)), "old proof does not return after player switch");
        foreach (var invalid in new[] { Feedback(tribe: 3), Feedback(count: 4), Feedback(mode: 5),
            Feedback(kind: -1), Feedback(file: 0), Feedback(image: 0x41), Feedback(command: 17),
            Feedback(detail: -1), Feedback(unit: 1), Feedback(building: 1), Feedback(wall: 1) })
        {
            gate.Observe(Feedback());
            Check(!gate.Observe(invalid), "unconfirmed/other-command native output hides markers");
        }
        gate = new GroundMovePreviewAuthorization(1,2,3,10,20);
        Check(!gate.Observe(Feedback(x: 11)), "replacement gesture starts unconfirmed");
    }

'''+s[a:]
write(p,s)
# Normalize touched files and analysis scripts; verify every write ordinally.
paths=['Testmods/EnemyGatePathfindingTest/src/CursorPreviewCache.cs',
       'BugfixesAndQoL/src/GroundMovePreviewAuthorization.cs',
       '_inspect/implement-cursor-preview.py','_inspect/implement-marker-authorization.py',
       '_inspect/test-cursor-marker-changes.py']
for p in paths:
    s=Path(p).read_text(encoding='utf-8-sig'); write(p,s)
    assert Path(p).read_bytes() == s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
# Correct generated indentation only, no change to semantics.
p='Testmods/EnemyGatePathfindingTest/src/SamePclGateRouteRuntime.cs'
s=Path(p).read_text(); a=s.index('            NativeMaskSnapshot snapshot = currentMasks;',s.index('private bool TryValidateCursorPreview'))
b=s.index('            return CursorSnapshotMatches(snapshot, epoch, generation);',a)+len('            return CursorSnapshotMatches(snapshot, epoch, generation);')
s=s[:a]+ '\n'.join('    '+l if l else l for l in s[a:b].split('\n')) +s[b:]; write(p,s)
p='BugfixesAndQoL/src/LargeMoveTargetMarkerRenderer.cs'
s=Path(p).read_text().replace('                    publishedPreview = EmptyPreview;\n                previewAuthorization = null;\n                previewAllowedThisPass = false;', '                    publishedPreview = EmptyPreview;\n                    previewAuthorization = null;\n                    previewAllowedThisPass = false;')
write(p,s)
