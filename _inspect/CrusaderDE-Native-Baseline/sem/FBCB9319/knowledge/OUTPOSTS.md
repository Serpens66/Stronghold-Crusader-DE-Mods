# Outposts: curated static observations

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Installed Script Extender: 2.6.0. Local source commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`, tree `9cfb59b7b531553b23709b90b3cc3f10b0615cc1`.
Date: 2026-09-16. Static analysis only; no runtime verification or approved hook spans.

Detailed audit, all profiles, field map, limitations, workspace references and diagnostic cases: [Findings/Outposts.md](../../../../../Findings/Outposts.md).
Reproducible hash-checked evidence extraction: [audit.py](../../../../OutpostAudit/audit.py).

## Confirmed static relationships

- Building dispatch table RVA `0x2DEAE0` entries 2, 106 and 107 all point to RVA `0xABB90`. Caller is the indirect dispatch at `0xC64AD`, within `0xC60F0`. An empty direct-call graph for ABB90 is not evidence of no callers.
- ABB90 lacks usable baseline pseudocode. Its actual contiguous code ends with RET at `0xACDE7`; jump tables begin at `0xACDE8` and `0xACDFC`. Baseline function size 4501 is summed body size, not the contiguous extent. See [full disassembly evidence](../../../../OutpostAudit/FBCB9319/ABB90.json).
- Profile table RVA `0x2DD880` has 24 records of 13 signed int32 values (52 bytes): interval, minimum, base group size, random span, eight type slots, role. Records 0–7 European, 8–15 Arabian, 16–23 Bedouin. See [extracted data](../../../../OutpostAudit/FBCB9319/profiles.json). Mask chooses enabled profiles, not percentages of units.
- Building slot formula is manager RVA `0x64CCBB0` + `0x5C` + one-based buildingId * `0x32C`; managed array starts at manager + `0x388`. GameBuilding offsets `0x300/302/304/308/30A/30C/30E/310/312/314/316/318` are respectively profile mask, active tribe ID/global ID, counter, target size, profile, size setting, delay, catapult flag, vicinity-init flag, two acceleration counters. Offsets are structure-relative, not manager-relative.
- Initializer `0xB47E0` sets mask 255, size 1, delay 0, guard target 6 or 7, guard counter 1200. Dispatch `0xC60F0` first promotes NeedsInit, later executes ordinary updates; deletion state enters `0xB8310`.
- ABB90 has a separate guard path, not restricted to AI: types 22/70/82 depending on variant. Guards and regular units clear native unit manager/slot field `+0x708`, suppressing the ordinary recruitment-rally path.
- Primary spawn calls `0x17FEF0`, then `0x11D370(manager, unitId, tribeId)` and optional crew creation; a later `0x11B520` command directs the current tribe to the building exit. UnitCreate post occurs too early to override this complete sequence reliably. Original local type still selects the crew branch after a type-changing creation event.
- Crew mapping from the two ABB90 jump tables: 39→2, 40→3, 58→4, 59→4, 60→1, 77→2 additional units of type 30. Crew spawn failures do not roll back the machine. Crew receives job value 16 and equipment unit ID; this path does not add crew through the primary tribe assignment.
- Group completion `0x2E2B0` explicitly excludes human-owner branches from the AI queue. Empty groups are marked for deletion. `0x2A720` performs AI enemy-lord choice. New outpost groups start with stance 2.
- Selection `0x89F40` opens panel 45 for types 2/106/107 only in editor mode 1. The gameplay branch records an identity tuple without opening that building panel.
- Rally placement `0x90CD0` emits Chore 102; handler `0x19AF0` serializes five bytes (signed-byte category, short tile X, short tile Y). Coordinates are player/category fields, not per-building fields. Chore table RVA `0x2C7A30`, entry 102 confirms handler target.
- `0x196100` processes low-byte bit 7 of its sixth argument, sets tribe manager/slot field `+0x56C`, clears that bit before `0x11B520`. `0x19B260` reads the field when applying formation speed limits; `0x11DD10` can clear it later. The direct public IssueMoveHereCommand target lacks this wrapper step. Enum Fast=-255 alone is not a proven reproduction of the normal player fast-move command.
- Delete `0xC4290` marks state 3; terminal cleanup `0xB8310` clears the record. The latter explicitly hands off a matching unfinished tribe for types 106/107, but not type 2. This difference must not be generalized into a claim that all Bedouin troops are deleted or orphaned without observing subsequent updates.

## Confidence and remaining work

These are specific static relationships, not blanket promotion of the baseline's candidate function signatures. Decompiled signatures may omit live register/stack arguments. Function evidence retains the original confidence and binary hash. Raw exports, database, function-claims and Ghidra projects were not relabeled or regenerated.

Do not infer runtime-safe hooks, all-type AI-role compatibility, complete native save-field persistence or multiplayer archive roundtrip from these findings. Full normal-UI run/stance command tracing, remaining mode/limit semantics, special-type role readers and runtime cases are listed in the detailed audit. Any implementation still requires that narrower contract closure and installed-hook-backend validation.
