# Native projectile afterimage observer

Reference `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

- `0x9BFC0` is the type-6 afterimage allocator's entry. The installed
  RedBird hook displaces the complete 5/5/1/4-byte prologue. It validates
  the unchanged entry bytes, following bound-load instruction at
  `0x9BFCF`, and sole call at `0x9FED0` before publication. The watched
  live span is checked again before admission and at mission start.
- `0x9FED0` in `0x9F960` calls the allocator before the same projectile's
  movement at `0x9EF20`. The Win64 argument and register contract supplies
  source position, height, sprite file and frame. The caller disregards
  the return value. On Extreme's bound failure, the original allocator
  returns zero at `0x9C0E6` without allocating a slot.
- The same entry hook now optionally returns zero for the audited arrow,
  bolt and Attack Here source types in Standard games. It restores the
  scratch registers, flags and entry stack before returning. All other
  calls replay the displaced instructions and resume at `0x9BFCF`.
  The opted-in native rule uses a separate gate from the local capture
  generation. No second entry patch is installed.
- Current projectile slot is a signed dword at `0x8F8C30`. The projectile
  manager is passed in RCX. Slot stride is `0xE8` and the type word is at
  record offset `0x3E`. The read-only map tick is at `0x3665F58`,
  incremented at `0xCE03B` before the projectile sweep call at `0xCE245`.
  The subtile offsets are signed words at offsets 0 and 4 of each 8-byte
  entry in the table at `0x2DB520`.

An unrecognized hash keeps the feature dormant. A pattern for the entry
alone cannot validate the sweep timing, record layout, caller continuation
or early return. Re-audit those contracts, the live span and the type-1
smoke producer before admitting another DLL. See
[Extreme Troops baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/EXTREME_TROOPS.md).
