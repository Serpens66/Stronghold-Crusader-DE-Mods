# EnemyBridgePathTest: native addresses

Since 2026-11-17 the drawbridge closure belongs to Pathfinding. All hooked entries, called
functions, data addresses, layouts and the update procedure are documented in
`Pathfinding/UpdateToNewDLL.md` (section "Enemy drawbridge closure"); Vanilla facts in
`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/DRAWBRIDGE_REGION_CLOSURE.md`.

This test mod installs no hooks. Its read-only `src/DeckInspector.cs` reads the same
FBCB9319 tables (building manager `0x64CCBB0`, path manager/portals `0x60AD660`, PCL
`0x50EC690`, direction grid `0x51890D0`, tile flags `0x48F71B0`, building id grid `0x4B6AA50`,
moat task grid `0x5F011A0`, drawbridge mapper `0x2D1A30`) only after comparing the installed
`CrusaderDE.dll` hash with `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
on any other build the acceptance stops with an error. After a game update adapt it together
with Pathfinding. Earlier contracts of this test mod: `history/UpdateToNewDLL.md`.
