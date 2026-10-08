# Plague popularity: scheduling and identity contract

Audited installed Native SHA256:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audit 2026-10-08, semantic database selected by CURRENT.json; local/installed
Script Extender 2.14.0, source commit 5049b5d051d0face848cc2eb098db1fedf5451a7.
Role names below are descriptive; generic database symbols remain candidates.

## Relevant control/data flow

- Disease scheduling via 0xD1600 (callers include random-event 0xF82B0 and Chore
  dispatcher 0x1045F0) selects an eligible player's building. 0xD17D0 receives its
  building game ID and creates a herd near the building. Native placement attempts,
  bounds, map eligibility, RNG, cloud count and sounds remain Vanilla-owned.
- Projectile creation 0x9B2B0 allocates/reuses a native E8-byte slot, writes Global-ID
  at +44, state at +3C and type at +3E; type helper 0xA1DB0, movement initialization
  0xA13E0 and map registration 0x9C730 precede return. Failure may return zero.
  Projectile API IDs are native slots including a sentinel at zero; they do not use
  the unit-array index conversion. Herd-created Disease type 22 can have source
  player zero, so the source building supplies the herd owner. Catapult-generated
  Disease follows another route; this optimization does not broaden the fix.
- 0x9F960 advances projectiles: NeedsInit=1 becomes IsAlive=2; state=3 deletes through
  0x9F2D0. Movement/map checks in 0x9C730 and helper 0xA0540 can write state=3 directly.
  Type 22 dispatch at native pointer table 0x2DA9C0 + 22*8 resolves to 0x9A080; its
  expiration/fade path also writes state=3 without calling Delete in that operation.
  Therefore a Delete-only cache can still count an expired cloud before the next
  deletion. 0x9F2D0 unlinks through 0x9F490 and clears its native record through
  0x7490/memory fill. Slot reuse must additionally match Global-ID and Disease type.
- Resource scheduler 0xD1AD0 calls popularity updater 0xCB090. Its normal per-player
  path computes Vanilla's timed plague modifier and writes the accumulator before
  the common report write at 0xCB57C. Some special player/mode branches bypass this
  block. At the existing correction boundary, R14 identifies the player, signed AX
  is the Vanilla plague contribution, EDX the accumulated popularity, and
  R12+RBP+12EC20 the authoritative accumulator. Both EDX and that memory value must
  replace the old plague contribution; AX supplies the corresponding report.
  Subsequent components and Vanilla's final 0..10000 clamp remain authoritative.
- A previously managed player must remain eligible for zero-herd correction after
  the last cloud expires; otherwise Vanilla's remaining timed penalty can reappear.
  The save payload preserves ManagedPlayerIds independently from surviving herds.

## Managed publisher boundary

Installed Extender source BulkProjectileDetours confirms Spawn/Delete Pre/Post are
synchronous around the original operation. Delete Pre may change ProjectileId or
skip Original; skip produces no Post. Original receives the changed ID, whereas
Post is built with the original argument. Consequently Post's reported slot may
still contain its old living identity. Revalidate that identity before deleting
the index entry, and reconcile the actual player's living clouds at popularity.

The existing popularity hook is the sufficient authoritative reconciliation point;
an additional global OnTick scan is unnecessary. Full reconciliation is still
required before serialization. Original creation remains exactly once, with nested
capture restored in finally; native creation and random state are untouched.

Confidence: selected branch/offset/dispatch/identity and accumulator flows are
confirmed by semantic decompilation, installed-image disassembly/table inspection
and current managed APIs. Generic native role names are not confirmed symbols.
No gameplay timing, FPS gain or multiplayer acceptance is inferred from this audit.
The optimization leaves hook addresses, displacement and backend unchanged.
