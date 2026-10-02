# Pathfinding connection-class API uses the wrong native row stride

Verified with installed SHCDESE 2.12.0 (v2.12.0, commit
f8d51730fcb54b25af43d3c9348d57db058e077f) and CrusaderDE.dll SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

`GamePathingManagerAPI.PATHFINDING_UNIT_TYPE_COUNT` is 89, derived from
`eChimps.CHIMP_NUM_TYPES`. Native `0x181E00` instead indexes permission rows with
`class * 0x5A + unitType` from class-zero table RVA 0x32BC48. Public class one
starts at 0x32BDB0; the native row stride is 90 Int32 / 0x168 bytes.

`GetUnitTypePathfindingConnectionClassPermissions` computes row offsets using
89. Classes 2–6 therefore start 1–5 Int32 values before their native row.
`CanUnitTypeUsePathConnectionClass` and `SetUnitTypeCanUsePathConnectionClass`
inherit this offset error; writes can affect a different native class/type.

Please separate the valid enum count from native table capacity/row stride and
use the confirmed native stride for class addressing. The flat permissions span
currently exposes only 534 of 540 storage entries; profile storage contains 90
entries while the profile view exposes 89. These shortened views alone do not
prove an enum validation bug, but they cannot claim full native table coverage.

Our testmod only reads the flat prefix with native stride 90 and explicitly logs
the missing coverage. It does not call the affected per-class getter/setter.
No Script Extender source changes were made. Evidence includes installed machine
code, all direct native profile-table readers and canonical table contents.
