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

Under sustained simulation load, zero-wait main-thread attempts can starve. The
mod therefore queues a prepared raw read through APIShared EngineReadbacks after
a failed immediate attempt. APIShared uses its existing run/returnBuffer hooks,
with a thread-local run scope and actual Monitor.IsEntered check before executing
callbacks. No extra hook is installed by this mod. A single atomic job owns the
back buffer; pause fallback and simulation producer cannot copy concurrently.
Completion is consumed by the existing GUI publisher without waiting for the next
cadence interval. Cancellation never clears a running producer's storage. Session,
rights, HUD generation and controlled-player identity are checked before delivery;
obsolete completed data is discarded and cleared. All managed/UI identity reads
are prepared or evaluated on the main thread, not inside the simulation callback.

On an update, verify the P/Invoke signatures/visibility, original wrapper locking,
native write bounds, fixed-buffer counts/types and export field meanings against
the matching native/managed pair. Runtime initialization validates the lock and
typed fixed-buffer layout; offline appearance checks validate the real assembly.
Unsupported contracts fail closed for this overview through its existing error
path; there is no blocking or guessed-offset fallback. Recheck standalone and
Fixes/BugfixesAndQoL coexistence, unknown/absent players, rights changes, save/map
replacement and actual allocation/timing measurements before claiming acceptance.

## Vanilla presentation and world input (2026-10-31)

For the same managed/native pair, `FatControler.overNoesisGUI()` calls the raw
VisualTreeHelper.HitTest and walks logical Parent, with visual-parent fallback.
Tag `Ignore` returns false for a complete subtree; `IgnoreSelf` returns false only
when that tagged node is the original hit. IsHitTestVisible=false alone is not the
game's world-input exemption. The statistic root uses Ignore outside editing and
IgnoreSelf during header editing. Passive leaves/backgrounds and the view-owned
tooltip use Ignore; the settings window is outside that statistic subtree.

`FatControler.NoesisGUIUpdateChecksInGame()` makes BookPopularityColour green at
popularity >= 50, otherwise red. WGT_Popularity.SetPopHead maps positive/neutral/
negative to MainViewModel.GameSprites[0/1/2]. Cards use only the permitted projected
value; list/menu headers stay green and unknown card values use the neutral mask.

HUD_MissionOver.xaml supplies the atlas keys UI-Buttons S048 (gold), S051 (army),
S055/S058 (kills/losses), S061/S064/S067/S070/S073/S076 (production), S079 (population),
S082 (fear), S085/S088 (buildings), S091 (troops made), S094/S097 (trade).
MP_Star represents lords_killed in the result rows. Sources are cached per HUD;
extension metadata sources additionally invalidate on catalog changes. No native
RVA or gameplay hook is introduced by these presentation changes.

The existing prepared resource copy now also retains each player's typed official
WinLossState.Loss flag. Combined with negative merit this marks eliminated rows,
without extra exports, retained pointers or managed allocation under threadLock.
