# Tannery building animation transparency

This audit applies to installed `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- The building simulation loop at RVA `0xC60F0` uses 1-based building game IDs,
  record stride `0x32C`, and the building type at record offset `0x12E`.
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

The revised diagnostic fix changes only the transparency argument passed to
the native packet writer during the initial tannery animation. It retains the
original native building state and all other packet arguments. Fade progress
comes from the Script Extender's `OnTick` event, which the installed publisher
emits only while unpaused; callback count, rather than the event's game-tick
value, avoids an apparent jump when a paused clock advances internally.
