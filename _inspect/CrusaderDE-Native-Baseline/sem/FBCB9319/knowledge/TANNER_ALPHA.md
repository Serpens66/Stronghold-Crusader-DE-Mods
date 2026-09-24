# Tannery building animation transparency

This audit applies to installed `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- The building simulation loop at RVA `0xC60F0` uses 1-based building game IDs,
  manager RVA `0x64CCBB0` (VA `0x1864CCBB0`), record stride `0x32C`, and
  the building type at record offset `0x12E`. Native fields are addressed as
  `manager + buildingId * 0x32C + offset`, with no additional `0x5C` in this
  manager-relative formula. The alive field is at `+0x12C`.
  Dispatch table VA `0x1802DEAE0`, type 16, points to the tannery updater
  at RVA `0xB10D0`. This updater has no explicit arguments and reads the
  current building ID from RVA `0x8F9BE4`.
- The updater writes the animation phase at offset `0x174`, frame at `0x7C`,
  image at `0x78`, and transparency at `0x11A`. Phase 0, frame 0 sets
  transparency to 31. Later phase-0 frames decrement a positive value;
  phases 1 through 5 can leave the value at 30. Phase 6 writes the frame
  index as transparency, beginning Vanillas subsequent fade. The updater
  clamps native transparency to the range 0..31 before return.
- The simulation call at RVA `0xCDE60` invokes `0xC60F0` behind its tick and
  pause checks (`0x7E50` and `0x7EA0`). Counting tannery updater calls therefore
  follows unpaused simulation progress, including changes in game speed.
- The native building render path at RVA `0x488B0` reads offset `0x11A` and
  passes it to the animation packet writer at RVA `0x19D110`. For building
  packets (`kind=4`), that writer composes the object ID as
  `animationLayer * 4000 + buildingId` and writes its twelfth argument as
  packet transparency; the managed `GameMap.addUpdateBuildingAnim` path uses
  that value for the sprite. The observed test log showed raw 31 (3.125%)
  for about 803 ms, raw 30 (6.25%) for about 9147 ms, and raw 0 at the next
  native section.
- The first testmod hooked the updater at RVA `0xB10D0`. Its detour installed
  successfully, but the live test produced no fade-start marker. The exact
  failing subcondition of its phase/frame/image/raw start filter was not
  observed, so the filter is not a validated runtime contract.
- The packet writer prologue begins
  `4C 8B DC 53 55 57 41 56 41 57 48 83 EC 60`.
  Its first six bytes are complete instructions. The Ghidra reference export
  has 131 incoming calls to the function entry and no incoming edge to an
  interior byte of that six-byte span. The revised testmod accepts only
  RedBird NativeX64's six-byte `Indirect` detour and checks the live pointer
  slot and hook entry; other schemes fail closed.

The first working packet-writer revision changed only the transparency argument passed to
the native packet writer during the initial tannery animation. It retains the
original native building state and all other packet arguments. Fade progress
comes from the Script Extender's `OnTick` event, which the installed publisher
emits only while unpaused; callback count, rather than the event's game-tick
value, avoids an apparent jump when a paused clock advances internally.

For the next testmod revision, the installed DLL hash was rechecked. The
type-16 entry at VA `0x1802DEB60` in the building dispatcher table points to
`0x1800B10D0`; the `0xC60F0` loop calls it once for each active tannery on
each building simulation pass after `0xCDE60`'s pause/tick gates. The updater
entry bytes are `40 53 55 41 54 41 57`, three complete pushes covering
seven bytes. The Ghidra reference export has table references to its entry
and no incoming edge to an interior byte of this span. The installed NativeX64
backend must produce an `Indirect` detour with seven displaced bytes.

The direct-field revision restores the preceding Vanilla transparency before
each updater call, invokes the full updater once, then writes the visible
transparency to record offset `0x11A` only while phase `0..5` and initial
images are active. The updater's phase-0 branch reads/decrements this same
field, so the restoration preserves that input; phase 6 overwrites it with
Vanilla's exit fade value. Renderers `0x45820`, `0x488B0`, and `0x4C1D0` use
the field as packet transparency. Creation/removal paths also initialize the
field, but the type-16 updater is its active simulation writer. The live
packet-writer test reached 100% alpha after 30 `OnTick` callbacks in about
303 ms, so 64 actual updater calls are the new approximate 600-750 ms target.
The first native updater callback and phase/image/raw values still require a
new live test because the earlier updater detour had no unconditional marker.

The 2026-09-24 test with the direct-field revision installed the seven-byte
updater detour and logged its first callback for building ID 181, but reported
`eligible=False` and no fade-start marker. The managed renderer still showed
raw 31 and 30. Source review found a transcription error: the testmod used
manager RVA `0x64CBB0`, whereas the installed native DLL, this baseline's
building loop, and other building audits use `0x64CCBB0`. The wrong pointer
made the type/alive gate reject the real tannery before fade logic ran. The
corrected address was verified in the following live test.

The subsequent 2026-09-24 game run logged `eligible=True`, building type 16,
alive state 2, phase 0, image 12, and Vanilla raw transparency 31 on the first
updater callback for building 181. It reached effective raw 0 after 64 updater
calls; another tannery, building 364, completed the same fade in 710 ms. The
first fade took 1084 ms wall time and included a startup delay, so wall time
is not an invariant of the callback-count contract. No tannery callback error
was logged. The user confirmed that the animation is visible in-game.

The same run exposed a one-frame transition artifact in building 181: the
rendered alpha went from 1.0 (raw 0) to 0.0625 (raw 30) for approximately
10 ms, then back to 1.0 (raw 0) before Vanilla's descending fade. In native
updater `0xB10D0`, the phase-5 completion branch changes the phase to 6 but
does not overwrite transparency. The next invocation's phase-6 branch writes
its frame index to transparency. Restoring Vanilla raw 30 before the final
phase-5 invocation therefore exposes that old value for one render frame.
The targeted bridge retains raw 0 only on the verified phase-5-to-6 update
after a completed 64-update fade; the following phase-6 call resumes Vanilla.
The user confirmed the bridge works in-game after the next build on 2026-09-24.
The final testmod kept the native updater detour, 64-update fade and one-frame
bridge, with only initialization, first native callback and errors logged.
