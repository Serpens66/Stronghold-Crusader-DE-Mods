# EnemyBridgePathTest native update contract

Version 0.1.0 is diagnosis-only. It installs no native hooks, detours, executable mutations or policy. Hard dependencies are Script Extender, APIShared and BugfixesAndQoL. It does not depend on EnemyGatePathfindingTest.

Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Reference SE SHA-256: `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`, commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree `657af449e1397c58e6c5ec054977d83198b68e66`.

## Owning features and derivation

| Feature | Reference native chain | Derivation / update action |
|---|---|---|
| Hypothetical closure cells | 0x739C0, 0x69850, 0xA5E20, 0x645C0, 0x64460, mapper 0x2D1A30 | Confirm ordered 5×5 occupancy, orientation/2 stride 100, 15 nonzero cells in four actual fixtures |
| Record prerequisite / physical flag | 0x69560, moat index layer +0x1EA23F0, 0x725A0 | Public GetMoatWorkTaskIndexLayer, nonzero index prerequisite; no private direct pointer to the layer |
| Native surface/region consequence | 0x725E0, 0xD90D0, 0xD86F0, 0xE3B90 | Confirm surface mask 0x4A5014B1 and physical raised flag 0x40000000; no modification |
| Same PCL / ordinary group | 0xE2610, 0x11B520, 0x117820, 0x117C70 | Recheck equal-PCL early return / skipped call, Assassin bypass and group mode; label absence separately |
| Work assignment | 0x11E960, 0xE7F60, 0x11B520 | Recheck command-7 selector success/early return/zero-PCL bypass, command-6 fallthrough and emitted command/context fields |
| Existing searches | mainmod-owned F4930, DBC60, DA020, 123090, 195E30, E2610 and D9C40 | Passive observer forwards existing results; exact native call count/result unchanged; hook absence is missing coverage |

No RVA, mapper address or instruction pattern in this table is a new executable runtime target. The pure closure predicate is hash-bound; there is no semantic pattern fallback for ordered occupancy and the mapper. On native mismatch the diagnostic runtime stays inactive and logs an error. Unknown shapes, IDs, associations and reread mismatches remain unknown; they never create an active policy.

## Installed field / ID validation

`_inspect/EnemyGateBuildingContextAudit/verify-bridge-split.ps1` checks the complete public member list and signatures against installed SHCDESE.dll. Unit issued command/context X/Y/task offsets are 0x398/0x3E4/0x3E6/0x3B4. Bridge ordered occupancy begins at GameBuilding +0x1C8; grid +0xF8, orientation +0x102. Game IDs are 1-based; spans are 0-based. GetTileId has no coordinate guard, so coordinates are checked before row lookup. PCL accesses are bounded by the installed Span length.

Unity calls are public Application.onBeforeRender and Time.frameCount from installed Unity assemblies. No private or publicized Assembly-CSharp access is introduced. State/output objects and event registrations are statically rooted. Central APIShared session lifecycle resets logical state; no normal teardown unregisters hooks or observers. A post-startup render marker confirms the runtime survives component cleanup.

For a new Extender/native version, first update the full feature audit, installed enum/member/layout checks and compatibility with local Fixes/RedBird. Do not infer live validity from a successful build. README and historical evidence remain unchanged.
