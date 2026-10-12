# Arabian idle sword-draw audio contract

Reference native SHA256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Installed NativeX64 SHA256: `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

| Feature target | Reference RVA / derivation | Search and fallback |
| --- | --- | --- |
| Arabian unit handler | `0x16F590`, dispatch table `0x321CB0 + 75*8` -> handler | Unique executable-section prologue pattern in NativeContract; validate RVA first, search after mismatch; reject moved semantic boundary. |
| Spatial sound emitter | `0x1A41E0`, wrapper `0x2960`; idle call `0x1700CD` -> wrapper | Unique executable-section record-writing pattern in NativeContract; same fallback and boundary validation. |
| Current unit Game-ID | `0x9302C4`, RIP operand at handler+`0x1F`, next instruction handler+`0x23` | Derived only after complete prologue pattern validation; expected target and module bounds checked. |
| Native unit manager | `0x67E8400`, LEA operand at handler+`0x26`, next handler+`0x2A` | Derivation checked; runtime obtains units through public TryGetUnitById, not raw manager access. |

The pattern fallback resolves code entries only. Unit layout (stride 0x490), state semantics, sound record shape and marker availability have no general cross-version fallback; this single feature remains hash-bound. A changed hash or contract logs a timestamped Error and leaves Vanilla active. An already-published partial chain stays rooted and logically inactive; only unpublished candidates can be released.

NativeX64 Absolute only: handler span `[0x16F590,0x16F59F)` = 5+5+5, continuation push rdi; emitter span `[0x1A41E0,0x1A41EE)` = 4+3+4+3, continuation writes x. No branches or RIP operands in either displaced block; full-function direct edges and semantic xrefs contain no interior entry. Retest the actual backend, selected scheme, displacement, target, FF25 patch with hook pointer, zero PointerSlot and NOP padding after updates.

GameUnit slot1 = manager+0xAEC; raw native lookup = manager+0x65C+gameId*0x490. Public array lookup uses gameId-1. Type offset 0x8A, AIState 0x2BC, tile X/Y 0xC0/0xC2 are checked against the installed public struct. Type75, state1, sound180 and spatial flag0 select the single audited idle sword-draw emission; native RNG is untouched.

Emitter records contain 21 signed shorts (42 bytes): kind[0]=16, sound[3], volume[11], pan[12], spatial flag[13]. Normal spatial emission overwrites volume and pan with -1. The mod changes only volume[11] to -32768 on eligible newly appended records. Both negative values preserve the same Vanilla spatial branch; memcpy/pool transport preserves the marker. Managed processTestMap computes Vanilla volume/pan before the wrapper multiplies volume by 0.25 and calls public SFXManager.playSound once. No global sound settings, clip volumes, simulation state or additional random calls change.

Before accepting an update, re-audit the whole handler/dispatcher and other sound producers, both record writers, pending/tick transport, managed spatial/explicit paths and downstream channel selection. Inspect other mods for owners of both native entries and processTestMap. Current Script Extender 2.14.1 and local Fixes 1.25.1 do not hook these entries; their audio substitution/notification controls retain their normal path. Runtime logging and the in-game listening comparison are still required to confirm actual playback on Unity Mono.

## Startup and shared access regression

The first in-game test (2026-10-11 14:55:06.456) failed before hook publication: `ArgumentException: Invalid path`, caused by deriving installed file paths from GameMap.Assembly.Location. All binary paths now derive exclusively from BepInEx.Paths.GameRootPath. Test byte-loaded assemblies with empty Location and missing/wrong-hash files after changes to startup or hashing. Log the waiting state before subscribing because LibraryLoaded may replay synchronously.

Runtime unit lookup uses APIShared.UnitAccess.TryGetById (dependency>=0.7.0) and preserves the exact audited type/state/context checks. No extra liveness filter is applied. Compile against the verified local APIShared package; never bundle it. The shared preflight owns the UnitAccess scan and must stop the build for direct SDK lookups or ignored checked results. Sound factor and testmod version remain 0.25 and0.1.0 while the listening test is pending.

## APIShared ownership (2026-10-12)

The test plugin is now a consumer of APIShared.Audio.SoundEvents, dependency
0.7.2 during testing. Its own native/IL hook implementations have been replaced,
not retained as a fallback. All binary, copied-backend, installed-member and IL
tests now live in APIShared/tests/APISharedTests. The original audit above is
historical evidence; APIShared/src/Audio owns the production contracts.

The consumer only registers ArabianIdleSwordDraw -> MultiplyVolume(0.25f),
retains Enabled, and reports first successful SFXManager submission. That marker
is not a guarantee of audible playback. Additional custom clips, repeat limits
and unit situations must be developed and tested here before becoming public
APIShared contracts. Version remains0.1.0.

`Tests.PlaybackProbe` defaults to Volume25. Optional ReplaceVanilla selects
effect179, Suppress omits the managed call, and PanLeft sets panorama-1.
All probes require the same audited ArabianIdleSwordDraw context and Enabled.
These are hearing probes in the test consumer, never APIShared defaults.

## Live ingame hearing settings (2026-10-12)

The latest Unity run confirmed the migrated shared native callback at23:44:16.539
and Volume25 dispatch at23:44:18.771: sound180, gain0.6876894 ->0.1719223.
There were no Error/Fatal lines in that startup section. Other probes and map
transitions are not inferable from the first-dispatch marker alone.

The testmod now registers its own Script Extender modsettings page through the
APIShared preset registration path. EnableMod and ProbeIndex are PresetLocal;
setters immediately update the permanent sound registration's logical state.
The old BepInEx cfg is left on disk but is no longer read. Preset persistence
owns the replacement settings; defaults remain enabled and Volume25. Changes
reset the bounded confirmation so each hearing probe can be checked in one run.
No hooks, native context contracts or public APIShared behavior changed.

## Custom clips and soldier source expansion (2026-10-13)

The production hook owner remains APIShared. The old handler span above is
historical and is no longer installed; current wrappers are 0x2960/0x2990 (15
bytes each) plus the unchanged emitter. See APIShared/UpdateToNewDLL.md and its
public audio catalog. The consumer adds CustomClip (private Sounds/APISharedTriad.wav:
PCM16/44100 mono, three rising 440/660/880 Hz tones) and a local situation selector.
Default is still the confirmed Arabian sword-draw quarter-volume rule. New
source meanings are Inferred unless accepted for that exact source/type. Logs
include source, situation, evidence, unit type, target, gain and final panorama.
Read AUDIO_TEST_CLIP and AUDIO_TEST_DISPATCH together; a target Effect means the
custom clip was not ready or playback fell back. Hearing and new source acceptance
remain in-game tests. No clips or integer namespaces are bundled as global effects.

## Consumer unit filter and repeat-window checks

The unit selector contains all attributed catalog types, plus an all-types entry.
It compares nullable Context.UnitType and does not perform a live native lookup.
The 0–60000 ms setting is a local validated integer persisted through presets;
its text property is only a UI projection, applied on focus loss. Defaults stay
all types / 0 ms. A single monotonic Stopwatch window covers all matching
requests, not individual units. Blocked requests do not extend it. Settings and
APIShared mission start/end/initialization callbacks reset the window. The
process-rooted mission publisher and sound registry survive startup cleanup.
No new game member, native hook, file loading or Unity lifecycle callback is used.

Verify interval boundaries with injected timestamps, exclusion without consuming
the window, sharing across different sources/types, a pre-existing veto, reset
and zero interval before changing DLL references. Validate real Unity rendering
of the two new controls and the AUDIO_TEST_COOLDOWN marker in the next game test.
