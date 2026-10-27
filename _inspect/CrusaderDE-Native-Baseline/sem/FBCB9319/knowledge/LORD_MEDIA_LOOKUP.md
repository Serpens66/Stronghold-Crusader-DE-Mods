# Lord AIC slots and optional media lookup [Scope: Vanilla + mod integration]

Observed 2026-10-27. Installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed managed SHA-256:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Both were checked against CURRENT.json and the semantic manifest. Native statements
below were compared with the installed DLL's instructions, not only pseudocode.

## Vanilla custom AICs and media

`FRONT_Multiplayer.MPAIVInfo` stores `lordConfig` separately from `lordName`.
Selecting a user AIC sets `builtInLord=false` and chooses `lordConfig`; it does not
require a nonempty custom Lord name. StartSkirmishGame and Platform_Multiplayer's
StartGame import this configuration through EngineInterface.setCustomLordConfig.

DLL_SetExtendedLordConfig (confirmed export, RVA 0x87350) decodes the configuration
into AIC record `playerID + 29` (stride 0x5E4), preserves the original lineup type
in that record, and then replaces the lineup type with `playerID + 29`.
Consequently a generic AIC slot does not establish a custom media identity.

The wrapper at RVA 0x103AA0 (candidate function name FUN_180103aa0; media dataflow
confirmed by instructions) reads the original type from AIC record +4. Original
types 1..29 use Vanilla per-Lord media tables; larger types use generic media with
player-qualified references. Out-of-range message requests and a nonpositive
original type return without enqueueing. Both active branches enqueue a preliminary
announcement with message category 0, followed by the actual speech request.
The per-Lord preliminary announcement has an empty video; for Wazir its sound table
entry is `general_message33.wav`. A preliminary category 0 is not a chat failure.

Enqueue at RVA 0x103290 (candidate name FUN_180103290; instructions confirmed) stores
the supplied media/player values and either presents immediately via 0xFEE50 or
copies them into the queue. A full ten-entry queue returns without adding an entry.
The presentation function emits text, optional audio and optional video using the
supplied paths; its existing detailed presentation/lifecycle audit is in
NOTIFICATION_LAST_FRAME.md. A managed resolver return is not proof of playback.

## Vanilla catalog admission

CustomisationFileManager.BuildCustomLordDirectory admits a Lord to `customLords`
when its parsed AIV and configuration lists are both nonempty. Neither `info.json`
nor `lordmeta.json` is an admission condition. Vanilla's AddCustomLord path uses
this catalog, assigns the custom name and copies its AIC/AIV without an SE-registry
condition. GetCustomLords(bool) is public in the real managed assembly and exposes
a list snapshot; accessing the private dictionary is unnecessary.

## Script Extender and APIShared [mod integration]

Extender source provenance: commit `4014ca67c96974a5e6d697665baf5351cbfebde6`, tree
`372b43a9e9d614075c3b827d3df2a2346ec356fa` from the semantic manifest.

BulkAIDetours treats every generic AIC slot as a candidate for optional custom media.
It obtains a Lord name from skirmish restart data, MP restart data, or frontend lobby
data, then calls GameAIManagerAPI.TryGetVideoAndAudio. An empty name is normal for
a Vanilla Lord with a user AIC. A named Vanilla-style package can also legitimately
have no SE registration. The original resolver logs `lord not found [...]` for a
registry miss, although its false result retains the original native media paths.

SE's CustomisationFileManager hook attempts ProcessCustomLord only when processing
a custom package's `info.json`. No info file therefore means no registration attempt.
ProcessCustomLord can also return or throw before adding LordDataDict when metadata
loading fails; the outer hook catches exceptions and Vanilla can still admit the
AIC/AIV package. Thus a used package with lordmeta.json but no SE registration is a
reachable condition, distinct from the regular empty-name fallback. The existence
of lordmeta.json proves SE-specific metadata was supplied, not that it defines any
particular voice line or that a media file is missing.

APIShared's LordMediaResolverRuntime validates the loaded resolver's output setup,
name normalization, registry lookup and logging-only miss branch. Empty names and
registry misses return false quietly; registered entries retain the original path.
Separate mission diagnostics inspect only named, occupied, unambiguous packages on
the Unity thread after managed initialization. Package/cause warnings identify the
Lord and player slots, retain original media paths and make no playback claim.

Evidence: hash-bound managed decompilation, semantic function queries for 0x87350,
0x103AA0, 0x103290 and 0xFEE50, current native disassembly and installed managed
member/IL inspection. LordMediaResolverTests execute private copies of the installed
Vanilla catalog builder and SE registration method with only filesystem/JSON-parser
boundaries substituted. They demonstrate admission and the registration early return,
not an actual Unity/Noesis session or host/client acceptance. The original 11:25:36
log does not identify the affected player and does not demonstrate this registration
failure; its empty-name warning was a normal fallback misclassified by APIShared.
