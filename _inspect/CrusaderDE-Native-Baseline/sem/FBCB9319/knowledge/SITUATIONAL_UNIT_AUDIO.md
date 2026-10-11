# Situational unit audio: Arabian idle sword draw

Audit date: 2026-10-11. Native SHA256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; installed DLL matches CURRENT.json. Managed Assembly-CSharp SHA256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Script Extender local v2.14.1, commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, tree `c238441ea12db35ee36c5c5668d3c8fce0897295`. Installed NativeX64 SHA256 `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

## Complete feature path

Native dispatcher 0x182B00 uses the 1-based current unit Game-ID global at 0x9302C4. Actual type table at 0x321CB0, entry75 at 0x321F08, points to 0x16F590 (Arabian swordsman); European swordsman is type27. The full Arabian handler and its state jump table were inspected. State0 enters rest state1 when unassigned; ordered/other branches enter state3 or 0x69. State1 advances the rest animation, eventually resetting state0 and the action timer; occupancy relocation can lead to state101. Combat/build states106/107 and other sounds are separate paths.

Native unit record = manager(0x67E8400)+0x65C+gameId*0x490. Public GameUnit span begins at slot1, manager+0xAEC. Struct-relative type is +0x8A, AIState +0x2BC, animation timer +0x2AC, animation-advance flag +0x4C, tile sound coordinates +0xC0/+0xC2. Public TryGetUnitById accepts the unchanged Game-ID and indexes gameId-1. These offsets are crosschecked with the installed public struct and native manager/header formula, not inferred from a displacement alone.

State1 targets 0x16FFE9. Animation table depends on gameId&3: 0x330580, 0x3305E0, 0x330640 or 0x3306D0. Sound180/0xB4 (`FX_SHEATH_OUT`) requires animation advancement, gameId&3>2, timer11, and the existing RNG outcome. Actual CALL 0x1700CD -> 0x2960, next0x1700D2. The timer80 branch contains sound179 (`FX_SHEATH_IN`) at 0x17012F; current tables end earlier, so reachability of that later branch is not established. Other handler sounds164/163,65,145 at 0x16FB19,0x16FEA1,0x16FEE0 remain separate. Capturing the entire type75 handler scope plus state1/sound180/flag0 selects the audited draw call without extra RNG calls or simulation writes.

Wrapper0x2960 calls 0x1A41E0 with manager0xA98820 and spatial flag0; wrapper0x2990 passes flag1. Wrapper0x2940 calls explicit emitter0x1A4190 with volume100. Both creators write record kind16, sound[3], volume[11], pan[12], increment count exactly once; spatial emitter also writes x[1],y[2],flag[13],volume=-1,pan=-1. Manager prefix is buffer pointer at0, count int at8. Record size is42 bytes/21 signed shorts. Both writers overwrite volume, so reused records do not retain a prior marker.

DLL_RunTick export0x86680 binds the caller buffer via0x1A45D0, copies pending records through0x1A3EE0 (count*42), executes simulation, returns count and resets output pointer/count through0x19CE70 to its internal buffer at manager+0x10C9E8C. Managed Director.simThread calls the native tick on pinned buffers; six MemoryBuffers.MemBuffer instances hold short[14,700,000]. Director.Update consumes up to three queued buffers via GameMap.processTestMap and returns them to the pool. Simulation thread-local context cannot be consulted later on the Unity playback thread.

GameMap.processTestMap case16 reads volume[11],pan[12],flag[13]. Only BOTH volume>=0 AND pan>=0 select explicit volume/100 and (pan-64)/64. Otherwise Vanilla retains tile lookup, viewport culling, screen-derived pan, and spatial volume attenuation (flag0) before calling SFXManager.playSound. Therefore replacing only spatial volume -1 with -32768 preserves the branch and travels with the record through pending copies and all six buffers.

Actual public SFXManager.playSound signature is `(int soundID, float volumeOfset, float pan, bool unstoppable)`; the last optional argument is still present in IL even though the decompiler omits it at these calls. Preserve it. Both call sites in processTestMap carry the computed volume/pan. Downstream selection/throttling, clip base volume, 20-channel policy, and MyAudioManager.playSFX global SFX/master multiplication are unchanged. Multiplying only the marked per-play volume by0.25 avoids changing global sound-ID properties or clip assets.

## Hook and compatibility contract

Installed NativeX64 Absolute detour needs14 bytes rounded to complete instructions. Handler entry span15 bytes (5+5+5), continuation0x16F59F push rdi; emitter entry span14 (4+3+4+3), continuation0x1A41EE coordinate write. Displaced blocks have no branches or RIP-relative operands. Full-function direct calls/jumps and semantic xrefs contain no incoming interior edges. The actual production backend is tested on complete private function copies before/after publication, including scheme, displacement, target, zero PointerSlot, hook/trampoline addresses, Absolute FF25 pointer patch and NOP padding.

Script Extender audio asset substitution is downstream in MyAudioManager.playSFX and preserves the input volume. No existing contextual unit-sound event was found. APIShared public catalog/integration/architecture contain no owner of these two native entries or the selected managed method. Canonical local Fixes1.25.1 has notification suppression and a different GameMap.getPivotOffset hook, not these targets. Never add a competing MyAudioManager function detour for this feature.

The testmod is statically rooted; native callbacks and the permanent GameMap IL hook survive startup destruction of BaseUnityPlugin. No plugin Update/coroutine or teardown is used. Disabling only changes atomically published logical state. Published hooks are retained until process exit, including failed partial installation. Unknown identities/layout/IL fail closed. Runtime native context and first reduced playback have separate bounded timestamped markers.

## Evidence and limits

Reusable implementation/update contract: Testmods/ArabianIdleSoundTest/UpdateToNewDLL.md. Read-only span/dispatch/call regression: tests/native_audit.py. Isolated executable tests compile the actual runtime sources against installed assemblies and test eligibility, negative spatial marker, unmodified neighboring records, copy/pool transport, reuse, disable/fail-open, real public signatures/offsets, actual IL transformation and NativeX64 private copies.

These static/isolated tests establish the transformation and binary contracts. Audible reduction, actual Unity Mono hook execution after startup cleanup, mission/load transitions and large-army listening remain unconfirmed until an in-game test. No runtime sound-ID match by itself establishes idle context.

## 2026-10-11 startup failure and repair

The user's first listening test left the sound unchanged. The local BepInEx startup at 14:55:06.456 records `ArgumentException: Invalid path` in SoundRuntime.Install line37. Enabled=true was present, but initialization failed before native or managed hook publication. The later "waiting" log was emitted after a synchronously replayed LibraryLoaded callback, so it did not establish an active runtime.

The repair resolves all three binary files from BepInEx.Paths.GameRootPath and retains every audited SHA256 guard. Assembly.Location is not a reliable install path for a byte-loaded Unity assembly. The isolated test now loads the real Assembly-CSharp.dll from bytes, confirms empty Location, validates canonical files independently, and rejects missing files, wrong hashes and a missing game root before activation.

Both immediate unit lookups now use APIShared.UnitAccess.TryGetById with consumed boolean results and the existing type/state/context conditions. This changes no native identity, liveness, RNG or sound semantics. The mod declares APIShared>=0.7.0 and compiles against the verified fresh local package; APIShared sources are unchanged.

The shared build preflight now invokes Test-UnitAccess with the selected mod scope. Scanner fixtures cover guarded calls, the central owner, forbidden SDK calls and ignored results. Exact production gate execution checks scope forwarding and nonzero-exit propagation; 18 existing build drivers no longer repeat the same check. A full workspace scan passed for 1074 runtime sources. The revised isolated audio tests passed 113 checks. Actual post-cleanup hook callbacks and reduced audible playback still require the next Unity game test.
