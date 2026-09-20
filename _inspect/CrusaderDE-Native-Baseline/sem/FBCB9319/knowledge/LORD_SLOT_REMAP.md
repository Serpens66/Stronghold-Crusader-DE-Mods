# Remapped-player Lord spawn audit

Audited native build: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

The custom-game initialization path at RVA `0x94350` derives the map-position slot for each occupied lobby player through RVA `0x89870`. It then calls RVA `0xCFE00` with the old map slot and new player slot. This wrapper applies the reassignment to units, player resources, and related managers.

RVA `0x198FB0`, called first by `0xCFE00`, scans live units. Ordinary units owned by either reassigned slot receive the other owner ID. Unit types `0x37`, `0x38`, and `0x39` take a separate branch and are instead assigned unit state `0x6E`; they are not transferred to the new player ID.

RVA `0xC6810` then swaps a fixed list of four-byte fields between the two `GamePlayerResources` records when a live building was reassigned. The list includes the Keep and its associated start-position resources. It does not include `r_LordUnitId` at record offset `0x21F8` or `r_LordUnitGlobalId` at `0x21FC`.

The periodic player initializer at RVA `0xCEF00` invokes RVA `0xC23C0` for player IDs 1 through 8 when its resource gates pass. In `0xC23C0`, the Lord field at `r_LordUnitId` is tested before any identity validation. A nonzero value bypasses the type-`0x37` spawn call at RVA `0x17FEF0`. The stored global ID is checked only on the later existing-Lord path, so a stale nonzero unit ID can suppress creation indefinitely.

When the field is zero, `0xC23C0` calls `0x17FEF0` with one-based owner/player IDs, world coordinates derived from the reassigned Keep position, terrain height, and unit type `0x37`. A successful return is written to `r_LordUnitId`; the unit's global ID is then written to `r_LordUnitGlobalId`, chores are reset at RVA `0x196F80`, and the unit is advanced from `NeedsInit` to the active Lord path. This is the authoritative Vanilla creation sequence.

Runtime evidence from `The Ford Across the River` with synchronized roster players 1 and 8 showed player 8 owning the valid reassigned Keep while already carrying `r_LordUnitId=8` and `r_LordUnitGlobalId=534` on the first simulation tick. That reference failed the installed Script Extender's unit identity validation and remained unchanged through the Vanilla Lord window. Player 1 began with `0/0` and received a valid Lord at the normal counter boundary. Both players had `WinLossState.None`; defeat state was not causal.

The safe managed workaround is therefore limited to a new custom-game session's first three simulation ticks: for a non-kicked synchronized-roster player with `WinLossState.None`, a live owned Keep and a live owned Keep-door/start-marker reference, clear both Lord identity fields through the public `GamePlayerManagerAPI` setters only when the stored unit/global IDs are both nonzero and the referenced unit slot has the exact observed tombstone signature: owner `0`, `CHIMP_TYPE_NULL`, `AliveState.None`, global ID `0`, and current health `0`. A missing or otherwise invalid unit is not sufficient. Vanilla then remains responsible for creation. Saves, trails, editor sessions, all non-tombstone identities, zero stored identities, defeated players, and later runtime Lord deaths remain untouched.

Confidence is structural for `0xC6810` field coverage and `0xC23C0` control flow, and candidate for the broader roles of `0x94350`, `0x89870`, `0xCFE00`, and `0x198FB0`. The second diagnostic build confirmed the complete tombstone signature for both remapped players 7 and 8 before clearing their stale references, then observed Vanilla create correctly owned Lords with matching nonzero global IDs.
