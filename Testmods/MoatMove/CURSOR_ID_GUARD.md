# Building cursor ID guard (2026-09-19)

MoatMove 0.1.2 retains the existing original call and scope restoration in `CallBuildingCursorWithRegions`. Nonpositive unit/building IDs now short-circuit before either Extender lookup, matching the correction in BugfixesAndQoL. The wrapper neither creates an additional scope nor changes an enclosing scope for these inputs. The original receives the same arguments exactly once and its result is preserved.

This is a mod-side input-validation correction. Installed SE 2.7.1 rejects nonpositive unit IDs before accessing the array, logs the invalid lookup and returns false. That lookup does not mutate simulation state; it does not substantiate a Script Extender desynchronization report. No new native hook, event, pathfinding algorithm or serializer is introduced.

Compatibility review: installed SE 2.7.1; local tag v2.7.1, commit 68ebf5380d711dfa7b7f84c9d4326ff81e42854c, tree 4dcc39cb0a9950bc5e399bc2623bbe2c38a52d9d; baseline provenance agrees. Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. The local canonical Fixes clone was checked for cursor/pathing/building interception: this managed short-circuit adds no patch range or load-order requirement. This finding covers this correction, not certification of every unrelated combination of features.

Provenance: original extraction hashes remain unchanged. StandaloneContracts permits exactly the additional `unitId <= 0 || buildingId <= 0` condition in CursorConnectivity.cs, reconstructs the previous bytes for the historical hash and source comparison. The full historical gate currently fails earlier on pre-existing FastMovementScheduler.cs changes; historical hashes were not refreshed to conceal this mismatch. All other changes remain subject to the existing provenance gates.

Regression cases: unit/building IDs -1 and 0, no API calls, preserved original arguments/result/call count, existing enclosing scope, valid nested scope and exception restoration. These execute through the actual wrapper in the movement fixtures, including the native-backend fixture run.

Build requested with `/noinstall /nopause`; installed game files and configuration must remain unchanged. No version bump. In-game log confirmation remains pending.

Validation: source compilation against installed SE 2.7.1, movement fixtures with --integration-work (managed and --runtime-native), native hook inventory, and installed RedBird decode-only contracts pass. The existing --integration-work switch omits the stale historical provenance gate; it does not imply that the full default suite passed. The runtime diff against commit ef7d4d1cdd9570413aff24ce6366511cfb450b9c is restricted to the early ID guard. Test compiler references now match the current project (APIShared and Shared/GameBuildingFootprint); --redbird-only exposes the existing backend contract independently.
