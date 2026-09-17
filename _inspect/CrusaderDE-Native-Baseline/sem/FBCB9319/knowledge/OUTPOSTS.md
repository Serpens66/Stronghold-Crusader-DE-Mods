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

### Macemen-only production replacement audit (2026-09-17)

The narrower test contract is documented in [OutpostTest native contract](../../../../../Testmods/OutpostTest/UpdateToNewDLL.md). Installed Native hash and Extender commit/tree remain unchanged; installed RedBird.X64 1.1.0.0 was inspected. Gate `0xABC78..0xABC88` contains CMP(7), JE(6), TEST(3); incoming jump `0xABC6E` targets its start, no baseline xref or direct function branch targets its interior. `0xACDDB` is the early epilogue before the later RBP/RDI/R14 spills. Skipping this section also skips vicinity initialization and production counters, while keeping the registration/animation prefix.

Maceman type 26 dispatches to `0x146A70`. Its normal state 101 uses the native movement continuation and returns to idle/special states as other native Macemen do. Unit field manager-relative `+0xA82` / GameUnit `+0x426` is exposed by the current Extender as `r_AITribeRole`; the outpost writes value 50. The unit recruitment-rally field `+0x708` / GameUnit `+0xAC` is cleared. The dedicated allocator at `0x119D60` initializes tribe Alive=2, not NeedsInit=1; units created by `0x17FEF0` start NeedsInit=1. The exact completion ABI at `0x2E2B0` is `(AI manager, tribe ID, tribe global ID) -> void`; the third argument is required for queue identity.

Additional production blockers are RVA `0x37EF974` (int) and `0x38722DC` (byte), bypassed for native modes 0/99. The player cap compares sum of `0x379B30C + owner*0x583C` and `0x379E6D4 + owner*0x583C` against `0x37EF950` if player category `0x8574BCC+owner*4` is -1, else `0x37EF954`. Native Create separately enforces its pool limit. These exact predicates are test-contract findings, not guesses about the display names of the modes/counters.

These are specific static relationships, not blanket promotion of the baseline's candidate function signatures. Decompiled signatures may omit live register/stack arguments. Function evidence retains the original confidence and binary hash. Raw exports, database, function-claims and Ghidra projects were not relabeled or regenerated.

Do not infer runtime-safe hooks, all-type AI-role compatibility, complete native save-field persistence or multiplayer archive roundtrip from these findings. Full normal-UI run/stance command tracing, remaining mode/limit semantics, special-type role readers and runtime cases are listed in the detailed audit. Any implementation still requires that narrower contract closure and installed-hook-backend validation.

## Runtime comparison, 2026-09-17 (scoped observation)

The passive OutpostTest comparison under the same verified native hash is documented in [Findings/Outposts.md](../../../../../Findings/Outposts.md), section Vanilla-Vergleichslauf. All three building variants were observed for owners 1 and 2. The previously static incremental-group relationship has a concrete runtime example: tribe 4426/392 grows from 1 to 12 members while its first unit remains at the exit; movement follows. Unlike this incremental path, the earlier custom test completed each five-unit group immediately.

Hold occurs later for eight identity-confirmed guards, but all 39 guards initially have no tribe in the observed samples. The logger measures tribe stance, not an independent unit/UI stance. Therefore neither universal initial Hold nor its writer is established. Six guards also later occur in Aggressive tribes. These observations must not be promoted to a claim that guard membership/stance is immutable. State 114 is absent in this run; its semantics remain unresolved. These are modded-session observations with bounded sampling, not new function-signature or writer claims.

## Maceman state 114 clarification (2026-09-17)

The previously unresolved semantics of 114 are now established for Macemen: death animation, not an unknown live AI command. Under the same reverified native hash, damage handler 0x199110 subtracts target health (+0xA20 manager/slot, GameUnit +0x3C4), returns early for positive health, and writes 0x71 or default 0x72 to state (+0x918 manager/slot, GameUnit +0x2BC) after lethal damage. Additional lethal writers include 0xC1B40 and 0xC70B0. Maceman update 0x146A70 shares animation processing for 0x72/73/74, transitions to 0x6E (110), then marks AliveState=3 after its terminal byte counter exceeds 32. AliveState can remain 2 during the death animation. Generic baseline function confidence remains candidate; these are scoped static field/control-flow findings.

See the State 114 addendum in Findings/Outposts.md for parameter bases and qualifications. The first custom-spawn log shows nine dying Macemen; it does not identify their damage source or the writer of tribe=0. Previous statements that state 114 semantics are unresolved are superseded, without claiming a proven group-detachment path or a spawn defect.

## Macemen timing audit closure (2026-09-17)

ABB90 disassembly confirms group-wait threshold max(2000 - signed acceleration at GameBuilding+316, context minimum): native modes other than 0/99 enforce at least 400; the world predicate int RVA3668E34>3000 && int RVA3669048<11 then enforces 100, otherwise 600. Same world predicate selects group acceleration increment 100 versus 33 at completion. Spawn acceleration +318 increments by 4. Profile 2 uses max(100,250-acceleration) * (100-15*size)/100 with signed integer truncation and resets the shared +308 counter to RNG%40 after each due attempt. Group creation sets that counter to 2000, allowing the first unit in the same update.

Target is (10+RNG%10)*(size+1). Positive +310 delay decrements before production; after the interval test it blocks spawning when members>=target-1. For a delayed empty group, int RVA379D0D0+owner*583C !=0 selects initial target/2 batch; normal Macemen production uses batch 1. Completion requires members>=target and delay<=0. These scoped control-flow claims are extracted from AC4C5..AC52F, AC7D0..AC857, AC871..AC962, ACA8A..ACAD7 and ACD51..ACDC3, under the unchanged verified hash. No new symbolic names for unknown globals are asserted.

The incremental OutpostTest implementation is intentionally Macemen-only and uses independent bounded random draws plus saturation against acceleration short overflow; it does not claim an identical vanilla RNG stream. Save/load and owner/deletion behavior of that new implementation require their own live acceptance tests.

## Rally implementation contracts (2026-09-17)

Reverified canonical hash remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. The installed Extender Create-tribe pattern has a single relative call at 0x11E17C resolving to 0x119C00. Unlike the dedicated outpost allocator's owner-strided scan, it scans 1..4499 and initializes Alive=2; the false third argument leaves selected tribe unchanged. Human per-unit groups can therefore use the public Create(owner,false) wrapper without a new raw allocator adapter.

The full 0x196100 disassembly confirms six Win64 inputs: ignored incoming manager, tribe ID, tile X, tile Y, patrol, flags. It writes manager/slot +0x56C based on low-byte bit7, clears that bit and calls 0x11B520 with newOrder=1. Flags 0x81 propagate moveType=1 and enable the run/formation marker. Calling its entry preserves existing MoatMove detours; installing a competing detour is unnecessary. The function returns void, so observing a forwarded call is not an arrival guarantee. Baseline signatures remain candidate; do not infer the six-argument ABI from the incomplete pseudocode declaration alone.

Implementation and live-test boundaries are recorded in Findings/Outposts.md under human rallypoints. Human Hold is deliberately per newly created singleton tribe, independent of AI production groups; pending destinations are immutable spawn snapshots. This does not retroactively alter the vanilla guard-stance findings above.

Selection follow-up (2026-09-17, same verified binary hash): own outposts reach editor check RVA 0x8A1F9 after the separate owner/restriction switch. Its CMP/JNE/MOV block spans 18 bytes, next instruction 0x8A20B. The earlier foreign-owner editor exception 0x8A051 must not be bypassed. Normal continuation sets panel 45 and pending building ID, 0x8470 commits the selected ID, and 0x19D960 panel-45 export copies GameBuilding+0x300/+0x30E/+0x310 to managed offsets 1264/1266/1268 (marry_m_name1/marry_m_name2/marry_f_name1). The managed GameData mode/submode/building-ID change branch calls InBuildingGameAction -> setUpInbuilding; Show_HUD_Main becomes false, Show_HUD_Building true. Its inSetup guard prevents initialization of the sliders from issuing writes; manual controls are not editor-gated. These are static dataflow findings, not a completed gameplay validation.