# UiAcceptance native and managed contracts

All native accesses are hash-bound to CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (checked over the whole
file before first use; an unknown DLL fails the probe step, no fallback). Test utility only.

| Feature | RVA | Use | Evidence |
|---|---|---|---|
| Player move probe | 0x196100 | Called like Vanilla's Chore-17 handler 0x10AE0 (`rcx = 0x67E8400`, lea at 0x10C7C, call at 0x10CAB) under the private `EngineInterface.threadLock`; deliberately goes through BugfixesAndQoL's detour. | `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md` |
| Tribe move mode | 0x7CC6720 + tribe*0x688 + 0x56C | ushort written by 0x196100 (bit 7 of argument 6), read before/after the probe. | same, `OUTPOSTS.md:24` |
| Unit manager | 0x67E8400 | First argument of the probe call (ignored by 0x196100). | DLL bytes at 0x10C7C |
| Path region grid | 0x50EC690 | ushort per tile id; RegionScan candidate selection only. | `BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:110` |
| Move target availability | 0x3A11EA4 | byte at y*800+x; RegionScan only. | same file, line 102 |

Managed members reached by reflection (not public in the real Assembly-CSharp; `verify.ps1` rejects
any compiled nonpublic access): `EngineInterface.threadLock`, `FRONT_Multiplayer.RefFileLists`,
`selectedMPHeader`, `MPMapValid`, `EditorDirector.preDLLCallActions`/`getMouseStateForEngine`
(Harmony postfixes, priority First, active only during a MapClick step), the Noesis component's
`View`, APIShared internals `IngameMenuActions.registry` and `UiWorkRuntime.scheduler`, and
`Spectator.SpectatorPlugin.processRoot`. A missing member fails only the step that needs it.
