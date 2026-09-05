# `GetStablesUnitIdLink` reads `ushort` slots through an `int*`

Affected version: Script Extender 2.0.2 (`v2.0.2` / `6dc82d1d`).

`GameBuildingManagerAPI.GetStablesUnitIdLink` casts `&building->r_UsedHorse1UnitId` to `int*`, although the four `r_UsedHorse*UnitId` fields and the corresponding setter use `ushort`. Indexing that pointer therefore combines adjacent slots and can read beyond the four Unit-ID fields for higher slot indices.

Please use a `ushort*` for the Unit-ID link array and reject slots outside `0..3` in the get, set, and unlink methods before dereferencing. `GetStablesUnitGlobalIdLink` can retain its 32-bit element type but should receive the same bounds validation.
