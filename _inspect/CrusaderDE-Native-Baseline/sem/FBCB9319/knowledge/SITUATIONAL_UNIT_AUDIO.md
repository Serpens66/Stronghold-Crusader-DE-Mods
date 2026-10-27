# Situational unit audio: Arabian idle sword draw [Scope: Vanilla + mod integration]

Audit date: 2026-10-11. Native SHA256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; installed DLL matches CURRENT.json. Managed Assembly-CSharp SHA256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Script Extender local v2.14.1, commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, tree `c238441ea12db35ee36c5c5668d3c8fce0897295`. Installed NativeX64 SHA256 `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

## Complete feature path

Native dispatcher 0x182B00 uses the 1-based current unit Game-ID global at 0x9302C4. Actual type table at 0x321CB0, entry75 at 0x321F08, points to 0x16F590 (Arabian swordsman); European swordsman is type27. The full Arabian handler and its state jump table were inspected. State0 enters rest state1 when unassigned; ordered/other branches enter state3 or 0x69. State1 advances the rest animation, eventually resetting state0 and the action timer; occupancy relocation can lead to state101. Combat/build states106/107 and other sounds are separate paths.

Native unit record = manager(0x67E8400)+0x65C+gameId*0x490. Public GameUnit span begins at slot1, manager+0xAEC. Struct-relative type is +0x8A, AIState +0x2BC, animation timer +0x2AC, animation-advance flag +0x4C, tile sound coordinates +0xC0/+0xC2. Public TryGetUnitById accepts the unchanged Game-ID and indexes gameId-1. These offsets are crosschecked with the installed public struct and native manager/header formula, not inferred from a displacement alone.

State1 targets 0x16FFE9. Animation table depends on gameId&3: 0x330580, 0x3305E0, 0x330640 or 0x3306D0. Sound180/0xB4 (`FX_SHEATH_OUT`) requires animation advancement, gameId&3>2, timer11, and the existing RNG outcome. Actual CALL 0x1700CD -> 0x2960, next0x1700D2. The timer80 branch contains sound179 (`FX_SHEATH_IN`) at 0x17012F; current tables end earlier, so reachability of that later branch is not established. Other handler sounds164/163,65,145 at 0x16FB19,0x16FEA1,0x16FEE0 remain separate. Capturing the entire type75 handler scope plus state1/sound180/flag0 selects the audited draw call without extra RNG calls or simulation writes.

Wrapper0x2960 calls 0x1A41E0 with manager0xA98820 and spatial flag0; wrapper0x2990 passes flag1. Wrapper0x2940 calls explicit emitter0x1A4190 with volume100. Both creators write record kind16, sound[3], volume[11], pan[12], increment count exactly once; spatial emitter also writes x[1],y[2],flag[13],volume=-1,pan=-1. Manager prefix is buffer pointer at0, count int at8. Record size is42 bytes/21 signed shorts. Both writers overwrite volume, so reused records do not retain a prior marker.

DLL_RunTick export0x86680 binds the caller buffer via0x1A45D0, copies pending records through0x1A3EE0 (count*42), executes simulation, returns count and resets output pointer/count through0x19CE70 to its internal buffer at manager+0x10C9E8C. Managed Director.simThread calls the native tick on pinned buffers; six MemoryBuffers.MemBuffer instances hold short[14,700,000]. Director.Update consumes up to three queued buffers via GameMap.processTestMap and returns them to the pool. Simulation thread-local context cannot be consulted later on the Unity playback thread.

GameMap.processTestMap case16 reads volume[11],pan[12],flag[13]. Only BOTH volume>=0 AND pan>=0 select explicit volume/100 and (pan-64)/64. Otherwise Vanilla retains tile lookup, viewport culling, screen-derived pan, and spatial volume attenuation (flag0) before calling SFXManager.playSound. Therefore replacing only spatial volume -1 with -32768 preserves the branch and travels with the record through pending copies and all six buffers.

Actual public SFXManager.playSound signature is `(int soundID, float volumeOfset, float pan, bool unstoppable)`; the last optional argument is still present in IL even though the decompiler omits it at these calls. Preserve it. Both call sites in processTestMap carry the computed volume/pan. Downstream selection/throttling, clip base volume, 20-channel policy, and MyAudioManager.playSFX global SFX/master multiplication are unchanged. Multiplying only the marked per-play volume by0.25 avoids changing global sound-ID properties or clip assets.

## Hook and compatibility contract [mod integration]

Installed NativeX64 Absolute detour needs14 bytes rounded to complete instructions. Handler entry span15 bytes (5+5+5), continuation0x16F59F push rdi; emitter entry span14 (4+3+4+3), continuation0x1A41EE coordinate write. Displaced blocks have no branches or RIP-relative operands. Full-function direct calls/jumps and semantic xrefs contain no incoming interior edges. The actual production backend is tested on complete private function copies before/after publication, including scheme, displacement, target, zero PointerSlot, hook/trampoline addresses, Absolute FF25 pointer patch and NOP padding.

Script Extender audio asset substitution is downstream in MyAudioManager.playSFX and preserves the input volume. No existing contextual unit-sound event was found. APIShared public catalog/integration/architecture contain no owner of these two native entries or the selected managed method. Canonical local Fixes1.25.1 has notification suppression and a different GameMap.getPivotOffset hook, not these targets. Never add a competing MyAudioManager function detour for this feature.

The testmod is statically rooted; native callbacks and the permanent GameMap IL hook survive startup destruction of BaseUnityPlugin. No plugin Update/coroutine or teardown is used. Disabling only changes atomically published logical state. Published hooks are retained until process exit, including failed partial installation. Unknown identities/layout/IL fail closed. Runtime native context and first reduced playback have separate bounded timestamped markers.

## Evidence and limits

Reusable implementation/update contract: Testmods/ArabianIdleSoundTest/UpdateToNewDLL.md. Read-only span/dispatch/call regression: tests/native_audit.py. Isolated executable tests compile the actual runtime sources against installed assemblies and test eligibility, negative spatial marker, unmodified neighboring records, copy/pool transport, reuse, disable/fail-open, real public signatures/offsets, actual IL transformation and NativeX64 private copies.

These static/isolated tests establish the transformation and binary contracts. Audible reduction, actual Unity Mono hook execution after startup cleanup, mission/load transitions and large-army listening remain unconfirmed until an in-game test. No runtime sound-ID match by itself establishes idle context.

## 2026-10-11 startup failure and repair [mod integration]

The user's first listening test left the sound unchanged. The local BepInEx startup at 14:55:06.456 records `ArgumentException: Invalid path` in SoundRuntime.Install line37. Enabled=true was present, but initialization failed before native or managed hook publication. The later "waiting" log was emitted after a synchronously replayed LibraryLoaded callback, so it did not establish an active runtime.

The repair resolves all three binary files from BepInEx.Paths.GameRootPath and retains every audited SHA256 guard. Assembly.Location is not a reliable install path for a byte-loaded Unity assembly. The isolated test now loads the real Assembly-CSharp.dll from bytes, confirms empty Location, validates canonical files independently, and rejects missing files, wrong hashes and a missing game root before activation.

Both immediate unit lookups now use APIShared.UnitAccess.TryGetById with consumed boolean results and the existing type/state/context conditions. This changes no native identity, liveness, RNG or sound semantics. The mod declares APIShared>=0.7.0 and compiles against the verified fresh local package; APIShared sources are unchanged.

The shared build preflight now invokes Test-UnitAccess with the selected mod scope. Scanner fixtures cover guarded calls, the central owner, forbidden SDK calls and ignored results. Exact production gate execution checks scope forwarding and nonzero-exit propagation; 18 existing build drivers no longer repeat the same check. A full workspace scan passed for 1074 runtime sources. The revised isolated audio tests passed 113 checks. Actual post-cleanup hook callbacks and reduced audible playback still require the next Unity game test.

## 2026-10-12 shared audio integration [mod integration]

The local Unity run on 2026-10-11 installed the repaired hooks at16:53:59.180,
confirmed native invocation after startup cleanup at16:56:03.873 and submitted
sound180 at16:56:23.569 with computed gain1.647522 ->0.4118806 (factor0.25).
The user also reported the audible reduction. The log contains no testmod error.
Unrelated-resource-lookup errors were already present in archived runs without
the audio mod; their caller is not identified by those logs.

APIShared/src/Audio now owns the same hash-bound native entries and GameMap IL
sites. The marker transports Arabian context only. Ordered Pre rules run on
Unity and compose gain, effect replacement, panorama or suppression.
Post confirms presentation submission/suppression, not audible output.
Sound IDs refer to the private validated SFXManager.fx_list effect table;
Extender custom clip IDs index play_list and cannot be passed as effect IDs.
Generic managed interception survives native-context unavailability. Unknown
contexts carry no unit or player identity. The migrated integration still
requires a new in-game hearing/activation/map-transition acceptance test.

## Completed integration review (2026-10-12) [mod integration]

The shared integration passed a second focused code review before final
installation. Audio and gatehouse reservations share the process composition
root's NativeOwnershipRegistry; isolated runtime instances retain separate
registries. Logging failures cannot change audio availability or escape native
callbacks. Test-consumer probe changes reset their bounded confirmation marker.
No direct SDK unit lookup, transient Unity callback or published-hook teardown
was introduced. Script Extender and canonical local Fixes targets remain
nonoverlapping with the audited audio entries and managed call sites.

Copied NativeX64 probes use the same UnitTick/Emit delegate types as production
and validate Absolute scheme, displacement, target, entry, patch bytes, verbatim
trampoline prologue and jump continuation before and after publication. Modified
IL branches/switches entering a replaced call are rejected without mutation.
The full APIShared build passed 275 tests (152 Core, 102 installed API, 21
presets), all installed-member and Windows PowerShell 5.1 validation paths,
plus standalone packaging/provenance and workspace permanent-hook checks.
The consumer preflight passed UnitAccess scope/error propagation and its
hook-free public-rule tests. Both elevated build.bat installations completed;
all 14 APIShared payload files and 4 consumer payload files matched local
outputs by SHA-256. The 10 publisher-tool files also matched.

This is build/install evidence, not a new Unity listening test. New shared
hook lifetime, Enabled comparison, optional replacement/suppression/pan probes,
movement/combat and map transitions still require in-game acceptance.

## Latest Unity log evidence (2026-10-12) [mod integration]

The local LogOutput.log run initializes shared managed playback at23:16:14.313,
publishes both native context hooks at23:16:14.656, and registers the consumer
at23:16:15.753 with probe=Volume25 and context=Ready. At23:18:47.807,
AUDIO_PLAYBACK_CONFIRMED proves the permanent shared GameMap dispatch runs
after startup cleanup. The inspected log through23:33:36 contains no audio
initialization/context/callback error, but also no audio unit-callback or
consumer sword-draw playback confirmation. Thus this run proves managed hook
lifetime, not yet the Arabian context capture or25-percent shared rule.
All188 error lines are SHCDE-SE TryGetPlayerResourcesById range failures;
they do not identify the caller and are not audio failure markers.

## Shared Arabian playback confirmed (2026-10-12) [mod integration]

The newer local startup installed shared audio at23:43:49.978/23:43:50.321,
registered Volume25 at23:43:51.487 and reached the actual Arabian callback
at23:44:16.539 (unit9/type75/state1/tile354,477). Shared managed dispatch ran
at23:44:16.762, followed by the consumer's sound180 submission at23:44:18.771:
gain0.6876894 ->0.1719223, suppressed=False. The startup section contains no
Error/Fatal lines. This confirms context transport and quarter-volume dispatch
through APIShared on Unity. Listening comparisons and other probe outcomes are
not observable from these one-time markers alone; the user reports completing
the tests possible within this startup.

The consumer is being equipped with PresetLocal ingame settings to switch its
logical enablement/probe without reinstalling or repatching. Every settings
change resets its next-dispatch marker. Native/IL contracts remain unchanged.

## Live settings replacement evidence (reviewed 2026-10-13) [mod integration]

Latest local startup at2026-10-12 23:57 successfully registers the ingame
settings page and the shared rule. At23:58:25.867 its callback reports
probe=ReplaceVanilla, suppressed=False, final effect179 and unchanged computed
gain1.365933. This demonstrates live mode publication and replacement dispatch,
not an audible-output guarantee. No Error/Fatal lines occur in this section.
The newest local log file write is23:58:39.

The hash-bound managed SFXManager stronghold_main_list has effect179 mapped to
fx/sheathin_01 and fx/sheathin_02 (FX_SHEATH_IN), while180 maps to
fx/sheathout_01 and fx/sheathout_02 (FX_SHEATH_OUT). The replacement is therefore
sword sheathing, not a categorically distinct test cue. The user reports no
noticeable audible difference; distinguish dispatch proof from listening.

## User listening acceptance (2026-10-13) [mod integration]

The user reports testing the available hearing modes. Suppress produced no
idle sword-draw sound, as intended: audible suppression is confirmed. With the
replacement mode, the user heard a sound different from Vanilla but could not
confidently identify it. Combined with the previous final-effect179 dispatch
marker, this supports functioning replacement and an audible change; exact
sheathin variant identification remains unconfirmed. Quarter-volume reduction
was already reported as audible in the earlier test.

The user cannot test stereo panorama. PanLeft remains covered by isolated
contract tests only; presumed operation is not in-game acceptance evidence.
No runtime/code change or version promotion follows from this report alone.

## Additional live-mode log confirmation (2026-10-13) [mod integration]

The same local startup now extends through00:02:35. In addition to replacement179
at23:58:25.867, it reports Suppress at00:01:56.339 with suppressed=True and
PanLeft at00:02:24.741 with suppressed=False. Both retain original effect180.
Suppress retains the computed gain in Post because it omits the playback call,
rather than changing gain to zero. PanLeft logs mode dispatch but this log does
not print final panorama; stereo audibility remains untested. There are zero
Error/Fatal lines in the latest startup section. These entries establish live
settings-mode transitions without a new startup or hook publication.

## Soldier source and custom clip expansion (2026-10-13) [mod integration]

All 91 direct wrapper callsites from 28 soldier/military-support handlers are
inventoried in SOLDIER_AUDIO_SOURCES.json. Full pseudocode and disassembly are
retained under workspace _inspect/AudioExpansion/Unit*-*.txt. The healer handler
has no direct wrapper calls. Workers and animals are excluded. The catalog
records exact return PCs, unit type, effect sets (including 5/172, 65/66 and
163/164), coordinates, pre-emission state-change counterexamples and confidence.
New semantic interpretations remain Inferred, not gameplay confirmations.
The Lord 0x15D2C1 source uses player coordinates and stays Unknown. Arabian
0x1700CD/180 keeps source-specific InGameConfirmed from prior user acceptance;
0x17012F/179 reachability is still unconfirmed.

Complete wrappers 0x2960/0x2990 are 40 bytes; first 15 bytes are 4+3+8,
without RIP operands, branches or external interior entries. Wrapper flag is
0/1. They forward coordinates/effect to 0x1A41E0 and restore 0x38 stack bytes.
The permanent intermediary captures the original native return PC before the
managed transition, as argument five, using aligned Win64 shadow/argument space.
No incoming flags/SIMD are used by these void integer-argument wrappers.
Only the existing emitter writes negative context markers. Marker code is
source ordinal*330+effect above short.MinValue, never -1; decoding checks effect,
flag, pan sentinel and record type. Vanilla pending copy and all six tick buffers
preserve the same 21-short representation; reuse overwrites volume with -1.

Custom IDs are distinct: fx_list effects, play_list clips and API handles.
Private play_list reflection and public sh1_sound.clip are exact-type validated.
Public MyAudioManager.playSFX retains SFX/master gains, channel policy and SE clip
replacement. It intentionally skips Vanilla effect variants/throttles. Extender
owner-private GetModFileBinaryContent and public AudioClipExtensions decode on
its rooted dispatcher. EnqueueStatic always defers; it has no unsafe immediate
fallback. Decoder supports OGG and PCM16 44100 mono/stereo WAV. OnUnloadMap
publishes both Pre and Post; APIShared handles only Pre. Borrowed imports capture
references, never integer IDs, and are conservatively invalidated at map unload.
Owned map objects are detached before deferred destruction; new registrations
cannot be invalidated by that old destruction job. Process mod clips persist.

Installed SE 2.14.1 commit 5908e1f12437deb7ef5f5b1dc2d64e301178fba3 and local
Fixes 1.25.1 have no competing ownership of these wrapper/emitter entries. The
custom playSFX call passes through the SE downstream override; no extra hook is
installed there. New integration audio and provisional actions require game
acceptance; prior hearing reports do not automatically confirm new source entries.

Map clip lifetime also observes load/save-load/start Pre, because the installed
Extender suppresses unload notifications inside map startup. Register map clips
or imports after OnPostLoad Post; registrations during earlier loading phases
may correctly become invalid at a subsequent startup boundary. All publisher
callbacks isolate errors so the Extender cannot repeat Vanilla on an exception.
Map-lifetime failure closes map clips only and is exposed by MapDiagnostic;
process clips and generic events remain independent.

### Abschließende Zweitprüfung der Clip-/Soldaten-Erweiterung (2026-10-13) [mod integration]

- Release: 104 API-Tests bestanden, einschließlich vollständig kopierter Wrapperkörper, echter NativeX64-Intermediärbrücken, IL-Transformation, 91 Herkunftseinträge und Clip-Lebensdauer. Weitere bestehende Core-/Preset-Prüfungen werden vom abschließenden BAT-Build erneut ausgeführt.
- Frischer Windows-PowerShell-Präflight gegen echte installierte Assemblies bestanden; gemeinsamer Präflight einschließlich UnitAccess-Scanner und permanenten Hooks bestanden. Verbrauchertests prüfen zusätzlich den fertigen Custom-Zielvertrag, einen noch nicht verfügbaren Clip und die Live-Situationsauswahl.
- Zusatzlauf Debug: 102/104 bestanden; die beiden bestehenden ImGuiShutdownTests zur nativen Present-/Resize-Argumentweitergabe scheiterten (14 erwartet/5 erhalten bzw. 21/20). Die identischen Tests bestehen im vorgesehenen Release-Build. Dieser separate Debug-Befund ist nicht als Audiofehler oder als behobener Runtimefehler bewertet.
- Map-Lebensdauer gegen die tatsächlichen Extender-Startpfade erneut geprüft: Da Entladebenachrichtigungen innerhalb mancher Startpfade unterdrückt werden, zählen auch Pre-Load, Pre-LoadSave und Pre-Start als konservative Generationengrenzen. Kartenclips nach OnPostLoad Post registrieren.
- Neue Hörwirkung sowie zusätzliche inhaltliche Situationsbestätigungen bleiben bis zu den jeweiligen Spieltests offen.

Abschluss: APIShared und ArabianIdleSoundTest jeweils einmal über ihre erhöhte build.bat gebaut und installiert (Exit 0). Der finale Release-Build besteht 152 Core-, 104 API- und 21 Preset-Tests. 24 API-/Publisher-Dateien und 27 Verbrauchermod-Dateien stimmen per gemeinsamem .NET-SHA-256-Helfer mit den installierten Ausgaben überein; private Test-WAV vorhanden, APIShared nicht im Verbraucher gebündelt. Veraltete Hörtest-Hilfetexte korrigiert und Verbraucherpräflight danach erneut bestanden. Versionen bleiben APIShared 0.7.2 und Testmod 0.1.0. Kein neuer Spieltest aus dieser Sitzung.

### Spieltest und Freigabe 0.8.0 (2026-10-13) [mod integration]

Der neueste lokale Startabschnitt (ab Logzeile 4047; eigene Zeitmarker 01:33:05 bis 01:34:37) bestätigt AUDIO_PLAYBACK_READY, AUDIO_CONTEXT_READY und den nach Startup-Cleanup ausgeführten nativen und verwalteten Callback. AUDIO_TEST_CLIP meldet Ready. CustomClip wurde für Unit75.Source04 (ArabianIdleSwordDraw), Unit70.Source01 und Unit22.Source03 (RangedWeaponFire) an die Clipwiedergabe übergeben. Die beiden Fernkampfaufträge liegen nach dem Save-Load-Abschluss um 01:34:23; der Prozessclip bleibt verfügbar. Keine Audiofehler, AUDIO_SOURCE_REJECTED oder AUDIO_MAP_CLIPS_DISABLED in diesem Startabschnitt.

Der Nutzer bestätigt hörbare Wiedergabe des neuen Testtons. Das belegt den Custom-Clip-Pfad, aber keine bestimmte zusätzlich benannte Einheit-/Situationsdeutung: die beiden Fernkampfquellen bleiben Inferred. Kartenarchivclips, importierte Extender-Clips und alle weiteren Situationen erhalten dadurch keine automatische InGameConfirmed-Einstufung. Der separate Extender-Fehler TryGetPlayerResourcesById auf Thread15 mit Index -594294240/8 ist keinem Audiopfad zugeordnet; daraus wird kein Audiofehler abgeleitet.

APIShared wird mit den dokumentierten Audioverträgen als 0.8.0 geführt. Native Hookstellen, Binärverträge und Runtimeverhalten bleiben gegenüber dem erfolgreich getesteten Stand unverändert.

### Nachfrage-/Dispatch-Optimierung (2026-10-13) [mod integration]

Vor dem Eingriff wurden native, verwaltete und NativeX64-Dateiidentitäten erneut
gegen den vollständigen 91-Quellen-Audit geprüft: alle unverändert. Wrapper-
Intermediärbrücken, verdrängte Bereiche, Marker und Pufferverträge bleiben gleich.
Nur logische Nachfrageprüfungen, Emit-Prüfreihenfolge und verwaltete Dispatch-
Verwaltung ändern sich. Audio-Nachfrage ist ein von den Registrierungs-Handles
vollständig gepflegter Zähler; seltene Änderungen dürfen konservative positive
Nachfrage liefern, nie eine falsche Null für ein aktiviertes Handle. Begin erfasst
weiterhin die tatsächlichen aktiven Registrierungen für Pre/Post.

Die isolierte CLR-Prüfung führt Klone der tatsächlich kompilierten Einstiegsmethoden
mit Recorder statt Unity-Originalwiedergabe aus: 0 zusätzliche Byte je warmem
Rückweg ohne aktive Verbraucher und mit 16 deaktivierten Verbrauchern. IL vor
Originalrückweg enthält keine Heapallokation; native Übergangskosten und Unity-
Audioausgabe sind dadurch nicht vermessen. Vergleichsmessungen stehen in
APIShared/ARCHITECTURE.md. Öffentliche Typen/Member/Defaults/Konstanten wurden
gegen die installierte Vorfassung abgeglichen (2020 Einträge, keine Änderung).
NativeX64-, IL-, Herkunfts-, Clip- und Broker-Verhaltenstests sind weiterhin
maßgeblich; der erneute Ingame-Vergleich dieser Fassung ist noch offen.

Abschluss der Optimierungsprüfung: Release-Build über die erhöhte APIShared build.bat erfolgreich (Exit 0), 153 Core-, 113 API- und 21 Preset-Tests bestanden. Frische Windows-PowerShell-Präflights für Runtime, echte Assemblies, gemeinsame UnitAccess-/Hook-Grenzen und Dokumentation bestanden. 24 installierte API-/Publisher-Dateien stimmen per gemeinsamem .NET-SHA-256-Helfer mit dem Paket überein. Der unveränderte Testmod wurde gegen die neue API geprüft. Versionsstände bleiben APIShared 0.8.0 und Testmod 0.1.0; der Live-Vergleich dieser Optimierungsfassung ist noch offen.
