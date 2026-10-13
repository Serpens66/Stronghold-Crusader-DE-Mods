# Gatehouse damage and premature drawbridge destruction

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Static audit: 2026-10-13. Semantic function names remain candidate-level; the following byte/data flows were checked directly against this installed binary. This is not an in-game fix confirmation.

## Creation, target eligibility and damage

0x739C0 creates drawbridges through allocator 0xB47E0. Native building Game-IDs are one-based, sentinel-relative slots have stride 0x32C, and the public GameBuilding view starts at slot+0x5C. Allocator writes current/max Int16 health at slot+0x168/+0x16A from the four-byte default table at 0x2E7510. STRUCT_DRAWBRIDGE (49) defaults to zero HP; gate main/inner default to 2000/1000.

Damage eligibility table 0x2E6710 has entry zero for STRUCT_DRAWBRIDGE, one for both stone gatehouses. Both shared direct damage 0x7EB00 and area damage 0x7DF60 reject zero-health/ineligible buildings before subtraction. Target helpers 0xC2190/0xC2280 also reject ordinary drawbridge building targets; managed click handling 0x8C5F0 permits specialized unit attack paths separately. A siege projectile can therefore hit a bridge without damaging/removing the bridge. This describes unchanged Vanilla tables; foreign overrides can differ.

## Premature removal decision

0x7EB00 receives tile manager, tile ID, X, Y, damage, argument 6, source player, argument 8 and unit argument 9. Projectile handler 0x9C730, building fire 0xC60F0 and other non-direct paths pass argument 8=0. Recorded direct unit attack callers pass 1 (including the stack-form call in 0x160420). A source-player negative value can request alliance protection; damage is subtracted as signed-16 native health before the early bridge branch.

At 0x7EE62 the new HP is stored. At 0x7EE6B..0x7EE7E argument 8==1 and type STRUCT_GATE_MAIN/INNER select an early removal branch. This branch does NOT test the remaining HP. It uses adjacency helper 0xB9330 to find up to two live STRUCT_DRAWBRIDGE contacts and calls 0xC4290 on each. Only afterwards does 0x7F018/0x7F024 check positive HP and bypass normal destruction. Consequently a surviving gate can lose bridges on its first effective direct hit.

The later lethal gate branch already repeats coupled-bridge removal before gate removal. Area damage 0x7DF60 removes coupled bridges only in its lethal branch. Manual bulldoze 0x12400 separately removes up to two coupled bridges, validating the gate Global-ID. No extra bridge protection or restoration is required to correct the early branch.

## Coupling and terminal state

0xB9330 iterates footprint-perimeter contacts via 0xC0270, occupancy tile map and type/alive checks; owner parameter is not an ownership filter. It returns a positive one-based Building-ID, excludes the previous result for a second bridge, or returns zero. 0xC4290 sets slot+0x12C to removal state 3, updates removal presentation flags and can propagate shared compound-group removal. This is a state assignment, not immediate slot clearing.

0xC60F0 processes removal state 3 through 0xB8310. Removal releases workers/auxiliary state, calls tile restoration 0x61FC0, clears the record and invalidates pathfinding. Drawbridge-specific 0x622D0 clears bridge tile occupancy and restores moat/support representation; 0x61EF0 clears footprint properties/effects. Regular refresh/terrain semantics remain authoritative.

## Minimal correction boundary

The test correction inserts an additional positive-health guard before early removal, restricted to direct attacks on the two stone gate types. It keeps normal damage, notifications, lethal removal, area damage and demolition unchanged. Hook starts 0x7EE6B and displaces 4+6+5=15 bytes, continues at 0x7EE7A or skips to 0x7EF87. Full-function direct branches and exported incoming xrefs contain no target inside this span. Installed RedBird X64InlineHook uses max(minimum,14) and rounds to whole instructions; production generator is assembled/decoded and tested on private native fixtures.

Last observed save run: test_gates_v2.sav, 2026-10-13 15:27, recorded gate 163/global2560900 losing 6 HP (1000 to 994). Existing log alone does not prove which bridge removal was caused by this hit. At that point dedicated acceptance was pending; see the subsequent acceptance sections and historical documents retained under _inspect/DrawbridgeDestructionIntegration.

## First editor smoke test, 2026-10-13

Process start section begins at LogOutput.log line 6667 (version-independent BepInEx start marker). Installed testmod 0.1.0 reports matching native hash, reference-rva resolution and 15-byte span at 16:30:35.979. Other mission consumers identify a new EditorCreated/MapEditor session at 16:30:49.251..397. At editor unload 16:31:32.352, the testmod reports entered=167, suppressed=166, diagnosticErrors=0. The user reports the fix behaved correctly in the editor. Native counters establish actual guard execution after startup; they count branch decisions, not individual bridges saved. The one unprotected entry is consistent with a lethal hit but cannot be classified from these counters alone.

Diagnostic coverage is incomplete: this editor session has no MAP, HOOK_CONFIRMED, DAMAGE_PRE/POST, NEXT_TICK or REMOVAL_FOLLOWUP marker. The observer's mapActive remains false because its OnStartMap/OnLoadSave registration misses creation of a new editor map. Therefore diagnosticErrors=0 does not validate the inactive capture path, and individual bridge identity preservation/terminal removal are unobserved. The later save-load at 16:31:36.444 activates session 1 and produces a tick marker, but does not retrospectively validate editor captures.

The existing APIShared mission lifecycle covers EditorCreated and EditorLoaded through EditorDirector operation wrappers, native-load evidence and managed completion, with owner-bound observer registration. Prefer that public capability for diagnostic session activation; no new native fix or competing lifecycle hook is needed. This is a source/log diagnosis, not an implemented observer correction. Testmod runtime code remains unchanged after this review.

## Diagnostic revision 2 implemented, 2026-10-13

The independent testmod remains 0.1.0. Its native contract/generator and fix semantics are unchanged. The editor coverage gap is corrected in source by an owner-bound APIShared mission lifecycle observer; registration replay and Current are deduplicated by monotonic SessionId. End publications only retire the matching active session. Native counters are reported as session deltas, with Pre/Post pairing, gate capture counts and pending/confirmation coverage. Runtime lifetime still uses static rooting, the permanent native hook and the durable OnTick publisher.

Diagnostics now use value frames and two fixed bridge snapshots. Footprint geometry is cached by gate/global identity, coordinates and size; membership/occupancy/eligibility remain live on every capture, including NeedsInit-to-IsAlive transitions. Public coupling semantics are retained; APIShared's small result list remains. Once both detailed and lethal budgets are exhausted, building/tile capture stops while lightweight Pre/Post pairing continues. No additional native target or private game member access was introduced.

Automated fixtures compiled from production observer/session sources pass EditorCreated, EditorLoaded, LoadedSave, duplicate/retired replay, duplicate/stale End, quota reset, nested pairing, counter wrap and per-session marker reset. After warmup, 10,000 exhausted-budget damage Pre/Post pairs plus empty ticks allocate 0 bytes on the executing .NET Framework test thread. This does not establish detailed logging/live-query allocation cost or an FPS improvement. The existing 150 native execution cases also pass, including actual RedBird displacement=15. The prescribed elevated build.bat completed without compiler warnings and installed the updated testmod at 16:54:49. In-game validation of diagnostic revision 2 remains pending; earlier editor counters do not retroactively supply missing bridge snapshots.

## Revision 2 editor acceptance and main-mod integration, 2026-10-13

The subsequent EditorCreated session (16:57:44..16:59:25; process start
16:57:23) confirms the revised observer: 668 paired damage calls, 665 protected
native decisions, 0 errors and 0 pending follow-ups. All 64 detailed nonlethal
captures retained the coupled bridge. Lethal sequences 408, 501 and 668 show
both gate and bridge removal state 3, then cleared slots/global IDs in the
follow-up. Both stone gate sizes were observed. User confirms expected game
behavior. This supersedes the pending revision-2 acceptance above; it does not
claim projectile, two-bridge or multiplayer coverage from this editor run.

The same audited native emitter is integrated into BugfixesAndQoL under
src/DrawbridgeDestructionFix with a default-true SyncHostOnly option and
permanent logical activation. APIShared remains unchanged. Native regression
sources now live under BugfixesAndQoL/tests/DrawbridgeDestructionFix.Tests.
Historical testmod acceptance/update documents and logs are retained under
_inspect/DrawbridgeDestructionIntegration after retiring the standalone mod.
Main-mod and multiplayer acceptance remain separate pending gates.
Main-mod integration build completed 2026-10-13 17:29:59 with exit 0: 150 native decision/ABI cases and host/client/preset/Trail checks passed, followed by the existing main regressions. Installed/local main DLL identity and feature/setting were verified; version remains 1.0.187. Standalone test package/source are retired and historical evidence retained under _inspect/DrawbridgeDestructionIntegration. Main-mod in-game and multiplayer acceptance remain pending.

User reports successful thorough main-mod in-game testing and authorizes the final patch bump and README entry (2026-10-13). Main-mod acceptance is confirmed by that report; specific scenario coverage and multiplayer acceptance were not separately specified. Final patch version: 1.0.188. Native contract and runtime behavior remain unchanged.
