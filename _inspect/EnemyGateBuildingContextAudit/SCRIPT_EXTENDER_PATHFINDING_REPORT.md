# Pathfinding connection-class API uses the wrong native row stride

Revalidated on 2026-10-07 against installed SHCDESE 2.13.0.0,
SHA-256 7F7750481B392007CCCAD6D609554FBA138D8ED2781AAC54CCE0B235FC5F39F4,
and local v2.13.0 commit 85ab962b342c18f663da830570884a25b85116d0.
Reflection reports constant89; the installed row-getter IL contains multiplication
by89 (`1F-59-5A`) and span length89. Canonical source matches this implementation.
CrusaderDE.dll SHA-256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Full native reader bodies 181E00 and117C70 are retained in
`../BridgePlanningTests/native-evidence.txt`; storage is90 profiles and540 permissions.

`GamePathingManagerAPI.PATHFINDING_UNIT_TYPE_COUNT` is 89, derived from
`eChimps.CHIMP_NUM_TYPES`. Native `0x181E00` instead indexes permission rows with
`class * 0x5A + unitType` from class-zero table RVA 0x32BC48. Public class one
starts at 0x32BDB0; the native row stride is 90 Int32 / 0x168 bytes.

`GetUnitTypePathfindingConnectionClassPermissions` computes row offsets using
89. Classes 2â€“6 therefore start 1â€“5 Int32 values before their native row.
`CanUnitTypeUsePathConnectionClass` and `SetUnitTypeCanUsePathConnectionClass`
inherit this offset error; writes can affect a different native class/type.

Please separate the valid enum count from native table capacity/row stride and
use the confirmed native stride for class addressing. The flat permissions span
currently exposes only 534 of 540 storage entries; profile storage contains 90
entries while the profile view exposes 89. These shortened views alone do not
prove an enum validation bug, but they cannot claim full native table coverage.

The live testmod still reads the flat prefix with native stride90 and explicitly
logs missing coverage. A dormant internal adapter is now prepared and tested for
all90/540 current values, full-hash/bounds validation and two-pass stability. It does not call the affected per-class getter/setter.
No Script Extender source changes were made. Evidence includes installed machine
code, all direct native profile-table readers and canonical table contents.
