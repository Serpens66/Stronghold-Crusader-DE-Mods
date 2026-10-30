# Statistics capture contracts

Audit: 2026-10-30. Native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
Assembly-CSharp SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.

The mod calls Vanilla's public P/Invoke methods, not computed addresses. It installs
no native hook or competing managed patch. The RVAs below identify audit evidence;
they are not runtime dispatch constants and need no mod-owned pattern fallback.

- `DLL_GetMeritData(int*)`, RVA `0x84140`: caller supplies 45 Int32 slots.
  Player IDs are 1–8; each player occupies five slots: presence/ID, merit/alive,
  gold, army, alliance. Native processing uses helpers `0x187E60` and `0x1E20`
  as documented in the shared `ENGINE_PRESENTATION_BRIDGE.md` audit. An absent
  player only receives the invalid presence marker; other slots may be untouched.
  Clear the working buffer before entering the monitor, as the original wrapper's
  fresh array did. Never interpret other fields unless presence is positive.
- `DLL_GetMPScoreData(byte*)`, RVA `0x83CB0`: writes the caller-owned public
  `EngineInterface.multiplayer_stats_export` layout. The player arrays contain
  nine Int32 entries, the killer/victim matrix 81. Its last array starts at 1392
  and ends at 1428; the managed type includes the required alignment padding.
  Buffer size comes from the real managed type, not a duplicate hardcoded layout.
  Read fields through that type after releasing the lock. Matrix rows are killers,
  columns victims; losses include killer zero (environment), kills exclude victim
  zero, and both exclude the diagonal. Only selected historical values demand
  this export; no full MPScoreData conversion, metadata or avatar resolution runs.

Both wrappers in the audited real assembly use the same private static object
`EngineInterface.threadLock`; `run(bool)` holds it through native simulation and
buffer publication. Resolve it once by validated reflection, use zero-wait
`Monitor.TryEnter`, and release in finally. All raw reads, including player-resource
and identified-local-Lord fields, are protected by this monitor. Native pointers
never leave the copy operation. Formatting, permission projection, providers and
Noesis run outside it. The mod's read path works without BugfixesAndQoL and does
not alter its wrappers, caches, patches or the simulation.

On an update, verify the P/Invoke signatures/visibility, original wrapper locking,
native write bounds, fixed-buffer counts/types and export field meanings against
the matching native/managed pair. Runtime initialization validates the lock and
typed fixed-buffer layout; offline appearance checks validate the real assembly.
Unsupported contracts fail closed for this overview through its existing error
path; there is no blocking or guessed-offset fallback. Recheck standalone and
Fixes/BugfixesAndQoL coexistence, unknown/absent players, rights changes, save/map
replacement and actual allocation/timing measurements before claiming acceptance.
