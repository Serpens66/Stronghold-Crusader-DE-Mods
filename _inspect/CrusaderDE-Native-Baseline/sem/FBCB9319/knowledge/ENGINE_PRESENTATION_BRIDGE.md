# Managed presentation access under the engine lock [Scope: Vanilla + mod integration]

Audit observation: 2026-10-23. Native canonical installed DLL SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
installed Assembly-CSharp.dll SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
RVAs below are evidence for that image, not new consumer hook contracts.

## Merit

The public static managed `GetMeritData()` returns a rectangular Int32 array.
It enters the private static `EngineInterface.threadLock` at IL 0013,
calls DLL_GetMeritData at 0037 and exits at 0045 in its original finally.
The native export at RVA 84140 reads eight player records, using helpers
187E60 and 1E20 (including the read-only CEB30 helper), into the nine-row,
five-column result. Audited callers include ally presentation and Merit UI.
HUD_MeritPanel can mutate the returned array: both publication and delivery
therefore require independent copies. Returning a delivered clone must not
renew the snapshot's original monotonic timestamp.

No native writer is introduced. Refresh uses the regular managed method
from the existing simulation critical section, preserving foreign Harmony
patches. A missing snapshot still uses the synchronous original. Older
same-session data may be displayed while a refresh is requested; no snapshot
crosses a load attempt or central mission/session boundary.

## Achievement tuple and save ordering

Managed `SetAchData(Int32 food, Int32 wood, Int32 weapons)` enters the same
lock at IL 000B, calls the public DLL_SetAchValues at 0013 and exits at 001E.
The native export at RVA 87200 converts the original integers to signed
16-bit values and stores food at 3666050, weapons at 3666052, wood at 3666054.
There are no gameplay operations inside this setter. The direct readers
are save/header handling (52B0), with corresponding load handling (87D0)
and map-load/reset writers. Managed callers are AchievementsCommon's
StoreValuesInEngine and UpdateAfterLoadingASave. Achievement/platform
notification work remains in those callers, outside the deferred native store.

`SaveSaveGame(String,Int32,Int32,Int32,Int32,Boolean,Boolean,Boolean)` first
checks SafeToSave. Its original lock enters at 0024, calls DLL_SaveSaveGame
at 0062 and exits at 0071 in a finally. Native save export: 86F80.
Flushing immediately before that native call inside this existing lock
ensures a save never bypasses a pending tuple. Unsafe-to-save early returns
must retain the tuple. Failed transfer prevents native saving and retains
the latest complete tuple for retry. Main-thread tuples can coalesce;
foreign synchronous setters drain an older pending tuple before their own
write so it cannot overwrite them later. No new native conversion is used.

## Run bridge

`run(Boolean)` obtains a free buffer at IL 0006 and returns zero before
locking if none is available. Original Monitor.Enter is at 001D,
DLL_RunTick at 0222, returnBuffer at 0282 and Monitor.Exit at 0291.
The single original finally owns release. Native tick return and the skip
argument remain untouched; a call is not evidence of advanced game time.
Pending achievement data is applied after entering the lock and before
DLL_RunTick. Merit refresh follows successful native processing and managed
copy/deserialization, before buffer publication. Native time includes
Extender/mod callbacks. No event observer is assumed to survive as a Unity
component: static Harmony/lifecycle roots retain the bridge after startup cleanup.

## Existing direction-filter adapter liveness [mod integration]

The eleven original byte spans remain in EnemyGatePathfindingNativeDefinition.
At F33F5, RCX remains the module base until its original MOVZX reads
`[R8+RCX+51890D0]`; it cannot hold scratch thread identity beforehand.
At DC536, MOVZX ECX reads `[R12+R9+51890D0]` and the displaced LEA changes
ESI. Those instructions do not define flags. The full DC3C0 continuation
overwrites incoming flags with ADD at DC56B before any reader, so flags are
dead at the span exit. All other compared spans define observable flags.
Execution fixtures compare all live registers, flags, stack and edge counters
against the installed original producers, including inactive/colliding slots,
signed source bytes and invalid indices. Private fixture memory is never
published as a game hook. APIShared slot layout and context ownership are unchanged.

## Evidence and limits

Production source-linked EnginePresentation.Tests installs actual Harmony
prefixes/finalizers and run/save transpilers, including PerfProbe composition.
It covers cloned data, >3-second cache age, request cadence, exceptions,
reentrancy, foreign patches, 10,000 concurrent complete tuple publications,
failed transfer/save, unavailable buffers, skip and retired sessions.
GateRouting.Tests executes all eleven native adapters against original bytes.
Timer/helper and parser fixtures cover sleeping cadence, missed intervals,
bounded transfer, raw histories and ambiguous unknown IP attribution.
Task evidence is retained under `.work/t/perf-opt/001` in this workspace.
No mod build, installation or new in-game acceptance was performed in this task.
The next game comparison must establish the effect on frame lags; static and
fixture results alone do not prove their elimination or native tick speedups.

## Raw history export and completed-buffer readback (2026-10-31)

For the same native/managed hashes above, public `DLL_GetMPScoreData(byte*)`
dispatches to RVA 83CB0. Its managed explicit `multiplayer_stats_export` contains
nine-entry Int32 player arrays and the 81-entry killer-row/victim-column matrix.
The last `teams` array begins at byte 1392 and ends at 1428; use the managed
type's aligned size, not just the final array end. The public convenience wrapper
allocates raw storage, takes threadLock for the export, then builds the full
MPScoreData including arrays, identities and colour data outside that lock.
Merit raw storage is 45 Int32 slots; absent player records only receive the
presence marker, so reused buffers must be reset before each export.

[mod integration] APIShared's existing run/returnBuffer interception can execute
requested prepared reads before publishing a beingFilled buffer. The additional
readback gate checks a thread-local run scope and Monitor.IsEntered on the real
engine lock. Main-thread TryEnter alone has no fairness guarantee: in an eight-Lord
stress pilot all active-phase overview attempts were skipped, while paused raw
copies succeeded in roughly 5 microseconds after warm-up. This is evidence about
that pilot, not a guarantee for every machine or save. The Spectator refresh button
uses GetMPScoreData synchronously; the BFQoL merit optimization instead refreshes
requested merit at its already-locked pre-publication run bridge.
