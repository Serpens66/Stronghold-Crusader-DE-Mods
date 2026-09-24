# GameBuilding reverse-engineering header is out of sync with the compiled interop layout

In Script Extender commit `70a4483fe606733219f0cd9fb1adbc0d08b926ea`,
`src/SHCDESE.BepInEx/Interop/GameBuilding.cs` places
`r_UsedInSiegeAttemptId` at `GameBuilding` offset `0x2A8`. The installed
2.9.0 `SHCDESE.dll` (SHA-256
`710B4DA701D08250C4760181B4B5C0702A333AEAE8EBA33C311B30530FE9A697`)
confirms this through `Marshal.OffsetOf` (680 decimal). `r_GlobalId` is at
`0xD8` (216 decimal); the compiled struct size is 812 bytes (`0x32C`).

The separate `ReverseEngineering/structs/GameBuildingManager.h` still
labels `0x2A8` as `N0000178A` and puts `r_UsedInSiegeAttemptId` at
`0x2AC`. A native caller in the current game DLL (SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`)
uses the field at native building-record offset `+0x304`, equivalent to
`GameBuilding` offset `0x2A8`, when propagating building deletion.

Please update the reverse-engineering header to match the C# and compiled
layout, or mark it as historical. The compiled API appears correct; this
report concerns the auxiliary header only.
