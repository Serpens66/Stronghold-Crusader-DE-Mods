# Updating Improved Hunters for a new CrusaderDE.dll

## Audited baseline

- Steam build ID: `24651686`
- DLL size: `3450880` bytes
- SHA-256: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`

The mod is strictly hash-gated because it uses raw unit layouts. On the audited
hash, it validates each pattern only at its direct RVA and does not scan the
DLL. Every other DLL leaves the complete runtime inactive.

## Native address map

| Source pattern | Reference RVA | Use / offset |
| --- | ---: | --- |
| `HunterQueryFlagsComparisonPattern` | `0x18AF70` | live-candidate filter anchor; 14-byte inline hooks at `+0x18` / RVA `0x18AF88` and `+0x3D` / RVA `0x18AFAD` |
| `HunterOrderRelationPattern` | `0x18EB72` | owner-relation anchor in `c_game_unit_issue_order`; 16-byte inline hook at `+0x10` / RVA `0x18EB82` |
| `HunterOrderHelperFailurePattern` | `0x18EE14` | behavior-neutral diagnostic after helper RVA `0xA06F0` returned `<= 0`; 16 overwritten bytes |
| `HunterState6TransitionPattern` | `0x130171` | behavior-neutral failure-branch diagnostic; 18 overwritten bytes |
| `CamelDespawnTickTimePattern` | `0x158468` | signed immediate at `+13` |
| `ChickenDespawnTickTimePattern` | `0x163415` | signed immediate at `+13` |

The source constants contain the complete wildcard patterns.

## Required update audit

1. Require one semantic match for all six entries. For the Hunter query,
   verify that the anchor remains RVA `0x18AF70`, the `+0x92` comparison begins
   at pattern offset `0x18`, the reservation comparison begins at pattern
   offset `0x3D`, and both overwritten `cmp` plus `jne` pairs remain exactly
   14 bytes (`8 + 6`). For both despawn entries, verify that operand 1 at
   pattern offset `13` remains the signed 16-bit despawn duration. For the
   order-helper diagnostic, verify instruction lengths `10 + 6`, `R12` as the
   unit-manager base, `RSI`/`RBP` as source/target slot offsets, `R15` as the
   Hunter ID and `EDX` as the signed result returned by helper RVA `0xA06F0`.
   The overwritten compare must still test source type `6`, and its branch
   destination must remain the zero-return block at RVA `0x18F928`. For the
   later state-6 diagnostic, verify instruction lengths `7 + 6 + 5`
   and that `EDX`/`EAX` still carry Hunter ID/order result. For the order-
   relation hook, verify the overwritten `mov` and `cmp` remain `8 + 8`
   bytes, `R13` and `RCX` are the Hunter/target relation indices loaded from
   their 16-bit control words, `R12D` is the Hunter type,
   `R8 + RSI/RBP + 0x6EE` expose the separate Hunter/target owner bytes,
   `R8 + RBP + 0x6E6` is the target type, `R15`/`R14` are Hunter
   and target unit IDs, the technical hook return remains `0x18EB92`, the
   relation-different continuation remains `0x18EB98`, the Vanilla same-
   relation destination remains `0x18EC57`, and the native Hunter target path
   remains `0x18EBD2`. Do not conflate the hook return with the logical
   continuation: the stub handles the original `je` itself.
2. Revalidate the Script Extender unit array and raw fields `+0x88`, `+0x92`,
   `+0x94`, `+0xC0`, `+0xC2`, `+0x29C`, `+0x2BC`, `+0x2C4`, `+0x370`,
   `+0x39A`, `+0x39C` and `+0x448`.
3. Confirm hunter/prey states, corpse flag, death timer, reservations, target
   IDs, coordinates, camel health and visual health refresh behavior.
4. Test hunter retargeting, projectile compensation, corpse cleanup, camel
   health and owner-preserving chicken hunting on fresh and loaded maps.
5. Update all reference RVAs before approving the new shared hash.

## Audit for Steam build 24651686

Both complete patterns match exactly once. Their signed 16-bit immediate still
starts at pattern offset `13`; the surrounding animal state remains `0x6E` and
the death-timer field remains `+0x986` in the native manager-relative form.
The Script Extender initialized the same `0x490`-byte unit records, and targeted
native accesses reconfirmed the raw field map used by the runtime. Fresh/load
map behavior remains a post-build game smoke test.

## Feature 01 Hunter-query audit

The Hunter query function is RVA `0x18AF00`. Its live-candidate loop uses this
sequence on the audited DLL:

| RVA | Meaning | Branch target |
| ---: | --- | ---: |
| `0x18AF70` | `AliveState +0x88 == IsAlive` | reject `0x18B08D` |
| `0x18AF7E` | corpse flag `+0x29C == 0` | reject `0x18B08D` |
| `0x18AF88` | flags/control word `+0x92 == 0` | reject `0x18B08D` |
| `0x18AF96` | Vanilla deer/goat type check; existing Extender callback | reject `0x18B08D` |
| `0x18AFAD` | reservation `+0x448 == 0` | reject `0x18B08D` |
| `0x18AFBB` | shared distance, geometry and path route begins | varies |

The mod hook replaces the 14-byte `cmp` plus `jne` block at `0x18AF88` with a
small native inline filter. It repeats Vanilla's `+0x92 == 0` comparison. A
zero-flags candidate jumps directly to the existing type callback; a nonzero
candidate is rejected exactly as Vanilla unless its type is chicken and a
single unmanaged enable byte says that both `EnableMod` and `HuntChicken` are
active. The final routing policy keeps the proven owner-0 case on its original
Vanilla path: a usual owner-0 controlword of zero already takes the fast path,
while an owner-byte zero candidate is not admitted through Feature 01's
nonzero-controlword exception. Nonzero owners can use that narrow exception.
An admitted chicken then also reaches the existing type callback and all later
Vanilla checks. The enable byte is updated only when
either relevant setting changes; this added flags check introduces no further
managed callback beyond the existing type callback.

An earlier diagnosis attributed the observed move-toward-then-abort failure to
a close-range second query rejecting reservation `2`. The third controlled
test disproved that attribution for the current failure: after the Hunter had
acquired chicken `148/7332016`, no Hunter-query callback and no reserved-
chicken retention marker occurred before the transition from state `1` to
state `6`. The repeated callback block immediately before target acquisition
was the second radius pass of the initial query, not a later re-query.

A second 14-byte inline filter at `0x18AFAD` keeps Vanilla unchanged for
reservation `0`. Reservation `2` is admitted only when all of these conditions
hold: the feature enable byte is set, the candidate is a chicken, `ESI` equals
the current Hunter target ID at `R13 + 0x9F6`, the Hunter is in pursuit state
`1` at `R13 + 0x918`, and candidate global ID `[RBX - 0x208]` equals the
Hunter's stored target global ID at `R13 + 0x9F8`. All other nonzero
reservations still branch to `0x18B08D`.
This preserves exclusion between Hunters and protects against recycled unit
slots while allowing only the current Hunter to revalidate its own chicken.
The filter remains defensive for any real close-range re-query, but it is not
the fix for the currently reproduced abort.

The confirmed current abort path is in the Hunter state-1 routine:

| RVA | Meaning |
| ---: | --- |
| `0x13013D` | call generic unit-order routine at RVA `0x18E950` after setting command `4` |
| `0x130149` | test the routine's return value |
| `0x130154` | if nonzero, additionally require Hunter byte `+0x3FE == 0` |
| `0x13015E` | success writes Hunter state `9` |
| `0x130171` | failure branch begins |
| `0x130191` | failure writes Hunter state `6` |

The same path changed a Hunter targeting a deer from state `1` to state `9` at
distance `5`. The chicken case changed from state `1` to state `6` at distance
`7` within 20 ms, retained a live target with reservation `2`, and spawned no
projectile. Later logging established that RVA `0x18E950` returned zero while
the subsequent `+0x3FE` byte remained zero.

A behavior-neutral diagnostic inline hook is therefore installed at RVA
`0x130171`. Its semantic pattern begins at that RVA and is:

`48 69 CA 90 04 00 00 41 BF 14 00 00 00 B8 06 00 00 00 BE 01 00 00 00 66 46 89 BC 29 20 09 00 00 66 42 89 84 29 18 09 00 00`

The hook observes the exact failure branch, logs the correlated chicken target,
the generic order result, Hunter fields `+0xF2`, `+0xF4`, `+0x398`, and
`+0x3FE` at Info level, then reproduces the overwritten 18 bytes exactly:
`imul rcx, rdx, 0x490`, `mov r15d, 20`, and `mov eax, 6`. It does not change the
branch decision or state. Its `X64FastcallSafeEx` callback receives the Hunter
ID directly from `EDX` and the order result directly from `EAX`; all volatile
registers are preserved and the overwritten `imul` reestablishes subsequent
flags. The shared unmanaged feature byte gates the callback before it enters
managed code, so Mod aus or `HuntChicken` aus has no managed diagnostic call.
Because Vanilla may clear the Hunter target immediately before this branch,
the callback also accepts an identity-checked chicken tuple recorded by the
managed query callback during the preceding ten seconds. The log distinguishes
`native-target` from `recent-query-cache-after-native-clear`; a recycled slot
or mismatching global ID is rejected.

`RBX` is candidate slot `+0x29C`, `ESI` is the 1-based candidate ID, `R14` is
the native Unit Manager, and `R13 = R14 + hunterId * 0x490`. Both replacements
use `RAX` only as scratch. This is safe on all destinations: the type path
overwrites `EAX` at `0x18AF96`, the shared route overwrites it at `0x18AFBB`,
and the reject loop either reaches a later candidate or writes the final result
to `EAX` before return.

The confirmed slot formulas are:

- `GameUnitArray = UnitManager + 0xAEC`
- `GameUnit(id) = GameUnitArray + (id - 1) * 0x490`
- current Hunter slot at the hook = `R13 + 0x65C`
- current candidate slot at the hook = `RBX - 0x29C`

The semantic executable-section pattern starts at `0x18AF70` and matched once:

`66 83 BB EC FD FF FF 02 0F 85 ?? ?? ?? ?? 66 83 3B 00 0F 85 ?? ?? ?? ?? 66 83 BB F6 FD FF FF 00 0F 85 ?? ?? ?? ??`

The preceding corpse comparison is part of the signature because a second,
otherwise nearly identical routine at RVA `0x18B660` uses the opposite corpse
branch and must not match. Runtime installation remains strictly gated to the
audited SHA-256; the pattern is still required as a semantic local-byte check.
It ends exactly at `0x18AF96`, so its validation does not depend on bytes in the
adjacent type block that the official Script Extender hooks independently.

### Fourth runtime test and direct query-zero diagnostic

The fourth controlled log disproved the previous attribution of the reproduced
chicken abort to the later writer at `0x130191`: two Hunters lost live,
identity-matching, reservation-2 chicken targets during state `1 -> 6`, but
the behavior-neutral `0x130171` marker had zero hits. The successful camel
control completed state `1 -> 9`, shot, corpse approach, pickup and drop-off.
The remaining failure is therefore chicken-specific, but it does not use that
later writer.

The state-1 routine contains a separate earlier writer whose branch is tied
directly to the Hunter query return value:

| RVA | Meaning |
| ---: | --- |
| `0x12FF2E` | call Hunter query RVA `0x18AF00` |
| `0x12FF33` | test query result |
| `0x12FF35` | load current Hunter ID into `RAX` without changing flags |
| `0x12FF3C` | zero result branches to `0x12FF53` |
| `0x12FF53` | query-zero failure block begins |
| `0x12FF64` | write Hunter state `6` |

A behavior-neutral inline hook used in versions before 1.1.27 began at
`0x12FF53`. Its exact semantic pattern was
`BE 01 00 00 00 48 69 C8 90 04 00 00 B8 06 00 00 00 66 42 89 84 29 18 09 00 00`.
It logs `chicken query returned zero before state 6` only while either the
Hunter's stored target resolves to a chicken or the preceding ten-second
identity-checked query record still resolves to the same chicken. This is
required because target clearing itself is part of the failure under
investigation. The hook then reproduces the overwritten
`mov esi,1; imul rcx,rax,0x490; mov eax,6` instructions (`5 + 7 + 5` bytes).
The existing unmanaged feature byte prevented a managed callback while the
mod or chicken hunting was disabled. This hook was removed in 1.1.27 because
native side entries made its overwrite window unsafe; see the removal section
below.

The reservation hook additionally logs a bounded `reserved-chicken filter`
marker only for enabled reservation-2 chicken candidates. The callback receives
the native Hunter slot `R13 + 0x65C`, candidate slot `RBX - 0x29C`, and candidate
ID `ESI`; it records Hunter state, target ID/global ID, each equality and the
resulting `willAllow` decision before the native stub repeats the same decision.
Reservation-0 candidates remain on the callback-free Vanilla fast path.

For an update, reconfirm all RVAs, both 14-byte/two-instruction query-hook boundaries,
the 16-byte/two-instruction order-helper boundary at `0x18EE14`,
the 18-byte/three-instruction diagnostic boundary at `0x130171`,
the `+0x92`, type, global-ID and reservation offsets relative to `RBX`, the
Hunter state and target ID/global-ID offsets relative to `R13`, `RAX` scratch
safety, the four remaining register meanings, both slot formulas, and the existing type
callback at `0x18AF96`. Verify in game that the first query still uses
reservation `0`, the close-range query admits only the same Hunter's exact
reserved chicken if such a query occurs, and distance/geometry/path logic
remains native afterward. For the current failure, compare the
`reserved-chicken filter`, `Hunter-order internal helper rejected chicken`,
and `chicken state-6 branch` markers before implementing a behavioral
correction.

The same Hunter routine also writes target ID at RVA `0x13050F` and target
global ID at `0x130529`, but that block stores the target returned by RVA
`0x18B5F0` and then writes Hunter state `11` at `0x130536`. It is therefore an
assignment path rather than the directly observed state-`1` to state-`6`
clear. Keep it in the update audit, but do not treat it as the current abort
writer without new runtime evidence.

### Same-relation chicken order correction

The version-1.1.23 runtime trace identified the later failure branch
unambiguously: all three selected chickens reached Hunter state `1`, then the
call from RVA `0x13013D` to `c_game_unit_issue_order` at RVA `0x18E950`
returned zero. Hunter byte `+0x3FE` remained zero. The caller consequently
entered RVA `0x130171`, wrote state `6`, and cleared the target. Camel control
cases continued through the same caller successfully.

In the command-4 branch, the target identity, alive-state and corpse checks
precede the owner-relation logic. RVA `0x18EB82` loads the Hunter's relation-
table value and RVA `0x18EB8A` compares the target owner's value. Equality
normally jumps to the generic same-relation path at RVA `0x18EC57`. The native
Hunter special case at RVA `0x18EB98` (`R12D == 6`) and its direct target path
at RVA `0x18EBD2` are therefore reached for differently related prey but not
for player-owned prey. This explains why the extended query could select an
owned chicken and approach it, yet order issuance still failed.

`HunterOrderRelationPattern` begins at RVA `0x18EB72` and has one match in the
executable sections of the audited DLL. The inline hook begins at RVA
`0x18EB82` and replaces exactly the two 8-byte relation-table instructions.
It replays both instructions, preserves Vanilla's destinations for every
unrelated case, and redirects only an enabled `Hunter + Chicken` combination
whose relation values compare equal to RVA `0x18EBD2`. This is the same native
Hunter path already used for differently related prey. No owner, color,
relation-table, target, state, path or reservation field is rewritten by the
stub. Owner-0 chickens deliberately retain the proven Vanilla route and do
not use this redirect. Nonzero-owner chickens use the native branch when the
relation differs and the narrow redirect only when relation values compare
equal.

The enabled redirect makes one bounded managed diagnostic call with the
stable unit IDs from `R15` and `R14`; `X64FastcallSafeEx` preserves native
state. The resulting Info marker is
`same-relation chicken order redirected to native Hunter path`. The unmanaged
feature byte keeps Mod-off and `HuntChicken`-off paths callback-free and sends
them to Vanilla's original same-relation destination.

The first implementation incorrectly validated the hook library's technical
return address against RVA `0x18EB98`. Because only the two 8-byte
instructions are overwritten, the actual return address is RVA `0x18EB92`,
the start of the original `je`. The generated stub deliberately does not
execute that `je`: after replaying its comparison, it sends a different
relation to RVA `0x18EB98`, an unchanged equal relation to RVA `0x18EC57`, and
the enabled Hunter/chicken correction to RVA `0x18EBD2`. These are separate
addresses and must remain separate constants and generator arguments.

### Owner-0 Vanilla control introduced in version 1.1.26

Version 1.1.26 excludes owner-0 chickens from all three
Feature-01 behavioral exceptions so a newly spawned neutral chicken can serve
as the old-path control. The live-query and reservation stubs read the owner
byte through `RBX - 0x20A`, which is candidate `GameUnit + 0x92`. The order
stub reads the same field through manager slot offset `0x6EE`, derived from
the already validated slot-to-`GameUnit` base `0x65C` plus owner offset
`0x92`. The neighboring type access remains `0x65C + 0x8A = 0x6E6`.

For the controlled run, owner 0 must therefore produce no reservation-bypass
or same-relation-redirect marker. Its spawn marker records owner, color and
the complete word at `GameUnit + 0x92`; the expected neutral controlword is
zero and therefore follows the original Vanilla fast path. Nonzero owners
continue to exercise the Feature-01 paths.

The successful owner-0 end-to-end test makes this the current final routing
policy, not a limitation of the functional requirement. Owner-independent
means every owner remains huntable; it does not require every owner to use the
same implementation. Owner 0 keeps Vanilla, while nonzero owners receive only
the additional exceptions they need. If an actual owner-0 chicken with a
nonzero owner/control byte is ever observed and Vanilla rejects it, that case
must be analyzed separately rather than routing all neutral chickens through
unneeded new code.

### Removed unsafe query-failure diagnostic hook in version 1.1.27

The behavior-neutral diagnostic hook formerly started at RVA `0x12FF53` and
overwrote 17 bytes. A multi-Hunter test crashed with `STATUS_BREAKPOINT` at RVA
`0x12FF5A`. The minidump showed that the installed 14-byte absolute jump was
intact, but the original function also contains direct branches from RVAs
`0x1304E5` and `0x130585` to RVA `0x12FF58`. Those side entries landed inside
the hook patch's embedded eight-byte destination address; byte `CC` at RVA
`0x12FF5A` was consequently decoded as an instruction.

The complete query-failure diagnostic hook was removed: pattern and reference
RVA, address resolution, inline registration, generated stub, delegate,
callback, success check and initialization marker. It must not be restored at
the same boundary. Any future replacement must start at a basic-block boundary
and account for every incoming branch before choosing its overwrite range. The
separate state-6 diagnostic hook at RVA `0x130171` is unaffected.

### Player-2 comparison and order-helper diagnostic in version 1.1.28

The automatic one-per-map comparison chicken now spawns with both owner and
sprite color set to player 2. This preserves owner 0 as the already proven
Vanilla control and exercises the native differently-related branch for a
nonzero owner. If player 2 is an enemy in the selected test match, no
same-relation redirect should be logged. The existing Feature-01 live-query
and exact reservation-2 exceptions may still apply because its owner is
nonzero.

Focused disassembly of `c_game_unit_issue_order` at RVA `0x18E950` narrowed
the zero result further. The direct Hunter target path calls an internal
routine at RVA `0xA06F0`. At RVA `0x18ED1F` its result is copied from `EAX` to
`EDX`; `test eax,eax` and `jle` then enter RVA `0x18EE14` for results `<= 0`.
At that block source type `6` branches to RVA `0x18F928`, which returns zero.
For a Hunter/chicken pair, reaching the new marker therefore proves the helper
itself rejected the order; it is not merely the caller's later `+0x3FE` check.

`HunterOrderHelperFailurePattern` begins exactly at this basic-block boundary:
`66 42 83 BC 26 E6 06 00 00 06 0F 84 ?? ?? ?? ?? B8 FE FF FF FF`.
The hook overwrites and exactly reproduces the 10-byte source-type compare and
6-byte conditional jump. A full-image native reference scan found no direct
branch to RVA `0x18EE1E`, the only instruction boundary inside the overwrite
window. The bounded Info callback runs only for enabled Hunter/chicken pairs
and records the signed helper result, stable identities, owner/color, state,
health, controlword, reservation, tile positions and all four signed raw unit
bounds at `GameUnit + 0xB2/+0xB4/+0xB6/+0xB8`. The caller feeds these bounds
into RVA `0xA06F0`; that wrapper tries the map/coordinate routine at RVA
`0x9E350` in two orientations. The inspected core path uses coordinate math,
tile arrays and occupancy flags, not an owner/relation table. All other sources
and the disabled path reproduce the original decision without a managed
callback.

### Same-owner projectile damage path in version 1.1.29

The player-2 runtime control proved that foreign-owner chickens already enter
Vanilla ranged damage and complete the Hunter workflow. Same-owner chickens
spawn real arrows but never reach the public projectile-damage pre-event. The
remaining filter is therefore upstream of `c_game_unit_takedamage_projectile`
at RVA `0x192700`.

The projectile update function starts at RVA `0x9C730` and has four direct
calls to that damage routine at RVAs `0x9CA14`, `0x9CAC9`, `0x9CB98`, and
`0x9CC65`. The compare at RVA `0x9C9C6` is not an ownership check: `R14D` was
initialized to constant `1` at RVA `0x9C751`, while `EDX` contains the
projectile type. Do not patch this branch as a same-owner fix. No newly
identified native branch from this investigation is used by the mod, so these
addresses are diagnostic context rather than additional update anchors.

Version 1.1.29 uses the Script Extender's existing public
`GameUnitManagerAPI.DamageUnitRanged(victimUnitId, projectileId, 0)` API. A
projectile-delete Pre subscription invokes it only when the pending shot is an
Archer-arrow Hunter intent, the target is a live chicken, both stable global
IDs still match, and Hunter and chicken have the same nonzero owner. The call
runs while the projectile slot is still valid. A delayed fallback repeats the
same native damage call if the delete callback was not observed. No Script
Extender source or binary is changed.

For DLL updates, revalidate the public ranged-damage API and projectile-delete
Pre-event semantics in the matching Script Extender release. The mod does not
depend on the four projectile-update RVAs for installation. The existing
hash-gated Hunter-query and order anchors remain the only native hooks involved
in this feature.

### Active-flight same-owner damage in version 1.1.30

Runtime evidence from version 1.1.29 showed five same-owner Hunter arrows
reaching projectile deletion without a projectile-damage event. Calling
`GameUnitManagerAPI.DamageUnitRanged` from delete Pre returned `false` five
times and left each chicken at `2500/2500` health. The matching player-2 arrow
entered native damage 459 ms after spawn and dealt `15000` damage. The
difference is therefore upstream of the public damage event and specific to
the same-owner relation.

Version 1.1.30 calls the same public ranged-damage API from the existing
persistent 100-ms scan while the exact Archer-arrow projectile is still
`AliveState.IsAlive` and within 64 native world-coordinate units of the
chicken's current world position. Stable projectile, Hunter and target global
IDs, source/target unit IDs, types and the exact same nonzero owner relation are
validated immediately before each call. At most three calls are attempted per
projectile. A failed call retains the pending intent until normal expiry; it is
not removed by projectile deletion.

The delete Pre callback is diagnostic-only in this version and records the
projectile alive state plus current, aimed and current-unit coordinates. The
same-owner `KillUnit` fallback is disabled because it produced a non-Vanilla
corpse state that Hunters did not collect. Neutral, foreign-owner and
non-chicken fallback behavior is unchanged. No additional native RVA, byte
patch or Script Extender modification is introduced by version 1.1.30.

The installed 1.1.30 DLL is 177,664 bytes with SHA-256
`5FDC7BC26402F3AF01278771EA090D770AF848C7DBA89A6AEC70CCDC80EEF28C`.
The canonical game DLL used for the audit remains 3,450,880 bytes with SHA-256
`33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.

Runtime correction for the next version: despite their names,
`GameProjectile.r_CurrentTileX/Y` are tile-scale coordinates in the observed
projectile lifecycle, while `r_TargetWorldTileX/Y` and
`GameUnit.r_CurrentWorldPositionX/Y` are world-scale coordinates. The observed
conversion factor is 8. For projectile 82, delete Pre recorded current
`438,702`, aimed `3484,5620`, and unit world position `3484,5620`; multiplying
the current pair by 8 yields `3504,5616`. Version 1.1.30 compared these values
without conversion, so its 64-world-unit active-flight threshold never fired.
All four same-owner arrows consequently reached delete with zero active damage
attempts. Delete Pre consistently reported projectile `AliveState=3`, further
confirming that ranged damage must be tested before deletion. Revalidate this
scale relationship if the Script Extender projectile layout changes.
