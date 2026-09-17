# ExtraFeatures session callback findings

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Script Extender 2.6.0, source commit 2cee24e33b5a5d81d1c275efabc714ac59917b7b.

The detailed [2026-09-17 audit](../../../../HighFrequency-ExtraFeatures-NativeAudit.md) records source provenance, native dataflow, confidence, tests and the final user-selected scope.

Confirmed scope:

- Lord creation event Post at spawn RVA 0x17FEF0 precedes caller 0xC23C0 publishing Player.LordUnitId/global ID and applying AI profile health. Use it as a deferred dirty signal, never as a final HP-write boundary.
- 0xCEF00 -> 0xC23C0 is also reached from simulation scheduling 0xD1AD0, so absence at managed session start does not establish permanent absence.
- Public player IDs and Lord unit IDs are one-based. Resource records use playerId-1; native sentinel-based unit addressing is not evidence for zero-based IDs.
- Current precise buy caller starts are 0x29650 and 0x3EC90. Sell callers include 0x29700 AND allied-transfer accounting 0xD78C0/0xD7AD0. Older curated claim ranges do not exhaust the current callgraph.

HP-basis findings:

A completed event-timing audit alone does not justify HP reconstruction. Unit initialization 0x19A240 uses per-owner modifiers at VA 0x183666058, distinct from global Advanced EnemyHPS at VA 0x1887EE324. New-game normalizer 0x197EF0 and load normalizer 0x19C300 can replace healthy units' current/max from the default table and per-owner modifier without profile scaling. The latter is unconditional in 0x19A8C0 even when format versions match; wounded values remain unchanged. The separate older-format migration 0x19BF70 is not a universal load step.

0xC23C0 profile scaling requires native mode/state 0x188574B90 != 0 (also includes state 99), player slot -1 and nonzero lineup. Zero-state paths skip it. Do not replace this condition with public IsAIPlayer alone or call the native nonzero state exclusively real multiplayer.

Spawn begins in NeedsInit (1), with HP already initialized by 0x19A240. The final spawn callees 0x180230/0x1802A0 and 0x18BE50 do not rewrite HP; caller 0xC23C0 can still scale it after Post. A deferred scheduler must retain NeedsInit as pending rather than treating it as a dead Lord.

The user selected capture/persistence of actual completed Vanilla maxima per player/global identity and explicitly excluded old-save support. ExtraFeatures removes its former reconstruction formula, reads no new private native address, and applies new settings on reload to its validated saved basis. Missing or invalid payloads are diagnosed and do not trigger a guessed reconstruction. Full legacy reconstruction and arbitrary external HP writers remain outside the implementation contract.
