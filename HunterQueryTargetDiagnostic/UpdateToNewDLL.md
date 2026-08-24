# Hunter Query Target Diagnostic native update notes

Reference DLL:

- Steam build ID: `24816905`
- DLL size: `3451392` bytes
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Hunter update handler: RVA `0x12FC70`
- Hunter target query: RVA `0x18AF50`
- Known Script Extender problem caller: RVA `0x1941B4`

State-7 diagnostic sites (historical RVAs below refer to the preceding audited
build unless explicitly marked current):

Version 1.4.2 keeps only the independently proven-stable no-target writer, now
at RVA `0x12FEC1`. Event-side State-7 snapshots reproduce the native
linked-building identity check without hooking the crash region: the linked
building's raw worker ID at `GameBuilding + 0xA0` selects a unit, whose global
ID is compared with the expected value at `GameBuilding + 0xAC` when the linked
building slot has `AliveState.None`.

- RVA `0x12FC80`: Hunter-update AI-state load. Disabled in v1.3.3 as a controlled isolation test after the old save still crashed with both later query-path hooks removed.
- RVA `0x12FD96`: marker reached when the Hunter's linked production-building ID/global-ID identity is invalid.
- RVA `0x12FDA4`: shared State-7 assignment reached either from the invalid-building marker or after movement initialization at RVA `0x196230` returned zero.
- RVA `0x12FDB2`: first instruction after the shared State-7 store. Disabled in v1.3.4 together with RVA `0x12FE7F` as the second controlled isolation stage.
- RVA `0x12FE71`: State-7 assignment after the target query returned zero and the signed 16-bit field at `GameUnit + 0x2A2` compared greater than 400.
- RVA `0x12FE7F`: first instruction after the threshold State-7 store. Disabled in v1.3.4 together with RVA `0x12FDB2`; only the previously stable v1.2 three-hook set remains active.
- RVA `0x18CE22`: common Hunter-query dispatcher before `RBX` is overwritten with the UnitManager. This site is deliberately not hooked: in both v1.3.0 and v1.3.1 the old save produced its candidate-query events and then crashed while returning through the displaced call/conditional-branch sequence. The state-load observer captures the real Hunter identity earlier instead.
- RVA `0x194164`: State-7-only call of the Hunter target query. This site is deliberately not hooked: the v1.3.0 direct call-site observer caused a hard native crash when the old diagnostic save reached its query sequence. The safe diagnosis correlates the earlier dispatcher capture with the subsequent Script Extender event instead.

Resolution policy:

- When the installed DLL hash matches, use the audited RVAs and validate their semantic byte patterns locally.
- When the hash differs, search executable PE sections for a unique semantic pattern.
- If any pattern is missing or ambiguous, all State-7 cause hooks remain inactive and the initialization error is logged.

Update audit:

1. Revalidate the Hunter handler and all incoming branches to both State-7 assignments.
2. Confirm unit slot size `0x490`, unit-array manager offset `0x65C`, AI-state offset `0x2BC`, linked-building offset `0x334`, and unknown threshold-field offset `0x2A2`.
3. Confirm that the shared writer still has exactly the two classified predecessors.
4. Confirm that unit type 6 with State 7 still selects RVA `0x194164`, with Hunter ID in `EDX` and UnitManager in `RBX`.
5. Confirm that the Script Extender hook still snapshots `RBX` as its Hunter-ID event argument before interpreting a reproduced mismatch.
6. Update the reference hash, RVAs, patterns, and this document together.

## Audit for Steam build 24816905

The active no-target State-7 writer moved by `0x50` to `0x12FEC1`; its complete
semantic pattern remains unique. The Hunter handler and query function likewise
moved to `0x12FC70` and `0x18AF50`. Unit stride `0x490`, manager offset `0x65C`
and all raw fields used by the event-side snapshots are unchanged. Historical
disabled diagnostic sites above remain documentation only and were not restored.
