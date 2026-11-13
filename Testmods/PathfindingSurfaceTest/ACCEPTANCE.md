# Pathfinding surface acceptance

Explicit-request-only disposable local Skirmish test. Uses public APIShared Automation and SimulationTickEvents plus the existing Script Extender movement observer. No native hooks, OS mouse/keyboard input or save writes.

Build with `build.bat` (build only). Run `Run.ps1 -SdkRoot <prepared APIShared checkout/package root> -OutputDirectory <outside-game evidence directory> -PathfindingPackage <built Pathfinding_Serp package>` only after coordinating game access. Both APIShared plugin and bootstrap must come from that SDK. The wrapper temporarily tests Script Extender, optional installed Fixes, selected Pathfinding and this fixture; the canonical runner restores the installation/settings and verifies rollback.

The default local map is `test AI.map`; override `[Fixture] Map` in `BepInEx/config/PathfindingSurfaceTest_Serp.cfg` if needed. The candidate terrain must supply a vacant, flat39x33 region within the human keep's legal wall range. An unsuitable map fails setup and cannot produce a gameplay PASS.

Scenarios:

- `surface-matrix`: fast individual searches enabled; four mounted types and a spearman exercise regular replans while walking; a mixed group follows; a closed ring then checks mounted rejection and successful foot travel.
- `surface-groups-no-singles`: same mixed-group and closed-ring checks with individual acceleration disabled before mission start.

The fixture builds a normal high wall with opposing normal stairs, verifies actual tile flags and independently searches the native direction grid. It requires the unrestricted shortest route to use real structures and the mounted ground route to be longer. Closing the other three wall sides must retain a foot route while eliminating the ground route. It then inspects real published unit paths, all simulation ticks, identities and arrival/rejection outcomes. Setup failures, absent fast-route counter changes and missing expected transitions are failures.

The first game attempt failed setup because the complete wall was absent; it did not exercise or accept gameplay. The revised fixture provisions and verifies actual Stone, accepts only a complete wall, and records native placement status immediately after each normal call. A successful compile is not gameplay acceptance. It does not prove real multiplayer or repair pre-existing saved routes.
