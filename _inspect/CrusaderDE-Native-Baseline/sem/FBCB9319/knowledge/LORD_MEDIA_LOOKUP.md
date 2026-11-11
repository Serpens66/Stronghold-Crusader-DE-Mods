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

## Packaged names and Coop restart metadata (2026-10-31)

Rechecked against the same native/managed hashes above and installed Extender 2.14.3.
`Platform_Multiplayer.StartGame` creates one `RestartMPInfo`. Its original ordinary-MP
branch (`coopTrailID == 0`) copies lobby AIV names/images; the Coop branch skips that
copy. Both branches encode this object before native pre-init and later assign the
same object to the HUD. Restoring names only at the HUD assignment is too late for
the encoded restart/save state. The type array is left untouched by the original
name-copy path and is not an authenticated custom-AIC identity (see SAVE_WRITER_ROUTES).
`RegisterSkirmishUser` (export RVA 0x86550) stores the selected nickname subtype;
the same subtype can legitimately occur in several different player slots.

SE resolves generic-slot identities through restart data and resolves name, title,
details, portrait and speech independently from its process catalog. An embedded
AIC/AIV alone cannot reconstruct absent `lordmeta.json`. Full packaged metadata
therefore needs a separate, session-scoped presentation source; it must not create
a false script/catalog registration. Complete JSON preservation is independent of
whether the referenced media files are included.

APIShared's packaged presentation adapter supplies this source and fills claimed
Coop AI names/images immediately before the original encode call, retaining all
unclaimed selections and gameplay types. Codec tests exercise the real restart
encode/decode; an IL fixture transforms a copy of the installed StartGame method.
These checks do not establish Unity rendering, playback or host/client acceptance.

## Saved nickname subtypes and title fallback (2026-11-06) [Vanilla facts]

The installed native and managed files were rehashed and still match the full
identities above. The native save stores nickname subtypes, not localized title
strings. Confirmed export `DLL_RegisterSkirmishUser` (RVA `0x86550`) writes the
supplied `int32` subtype to `module + 0x8574C68 + playerID * 4`. The installed PE's
save-descriptor table at RVA `0x336B20`, zero-based row 124, contains pointer
`0x188574C68`, skip flag 0, length `0x24`, compression flag 1 and section ID 1101.
That section contains nine `int32` values, including slot zero. The common writer
at RVA `0x52B0` serializes the active descriptor through its regular codec; the
reader at RVA `0x87D0` matches the section ID against the same table and restores
it through the regular decoder. These internal function names remain candidates;
the field/table/section dataflow is backed by native machine evidence. The ordinary
save/load exports and synchronized writer routes are described in SAVE_WRITER_ROUTES.
The PlayState producer at RVA `0x19D960` transfers player slots 1 through 8 as
`int16` values; managed conversion copies these to `computer_names[1..8]`.

`OnScreenText.getComputerName` receives the one-based Lord type and the zero-based
nickname subtype. Its generic custom-Lord path uses the saved restart identity and
resolves a suffix from `GetCustomLordText(name.ToLower(), 34 + subtype)` when custom
media exists; a null/empty result retains
`Translate.lookUpText(TEXT_CUSTOMISATION, 23 + subtype)`. The latter is the localized
"Number one" and following suffixes. `GetCustomLordText` removes the Workshop/Trail
prefix through `SplitCustomTrailName` before reading the `text.txt` tag table.

The public static `Platform_Multiplayer.MPLobbyMember.GetName` instead receives a
zero-based Lord type. Its generic cases 29 through 37 resolve only the suffix by
the same custom-text/translation fallback. Passing `(int)Enums.AILords.SK_X1 - 1`
therefore obtains the custom-Lord suffix without reconstructing or consulting a
different player's name. This path does not call `OnScreenText.getComputerName`
and cannot recurse through its name hook. Valid saved nickname subtypes are 0..7;
the Vanilla custom-text accessor itself does not validate an arbitrary tag index.

## Missing metadata after save loading (2026-11-06) [mod integration]

ExtendedData's normal single-player start publishes selected Lord paths without
creating a packaged presentation. On save restoration, `RestoreLegacySavedPresentation`
can instead call `PrepareLegacyEntries` for a named custom Lord absent from the SE
registry. It publishes an entry with null metadata. Snapshot/trail capture likewise
preserves absent metadata as null through its own save and network serialization.
The former APIShared entry constructor parsed null as `{}`; the full-name and title
hooks then treated the entry as an authored title override and returned the bare
fallback name or a successful empty title. This bypassed Vanilla's still-valid
saved subtype and suffix lookup. It was an output-resolution defect, not lost native
save data.

The corrective integration distinguishes original null metadata internally while
retaining the public constructor, `MetadataJson` representation and pure metadata
resolvers. Full-name lookups combine the authenticated authored name with Vanilla's
suffix resolver when metadata is absent, preserving author names across peer-local
media aliases. The custom-text hook delegates absent-metadata titles to its original;
the SE title hook returns false rather than claiming an empty authored suffix or
reading a different local SE metadata document. The nested SE custom-text hook then
retains its original `text.txt` result. Explicit `{}` remains an authored document
with its existing empty-title semantics. Name/title entry equivalence includes
metadata presence; non-title queries retain their previous equivalence. Otherwise
identical null/`{}` entries must still share their empty media claim in either
order, across player slots or owners. Treating the presence distinction as a
global conflict would surrender that claim to peer-local SE clip selection and
could introduce an extra random draw. Identity claims remain available for Coop
restart encoding.

For an otherwise equivalent null/`{}` conflict the SE title adapter reports no
unambiguous title instead of consulting the local SE registry. Non-title support
remains claimed, so forwarding that title query to SE would otherwise log a
missing registration or substitute a peer-local title. The original custom-text
path can still retain its `text.txt` result and Vanilla suffix fallback.

This is deliberately a name/title correction. Media, audio, portraits, details,
subtitle caching and Lua resolution keep their existing ownership rules. A blanket
fallback to a peer's installed SE media would bypass the exact metadata/provider
check: `lordmeta.json` and media are excluded from gameplay fingerprints, while SE
clip selection consumes the shared `DeterministicRandom` also used by gameplay.
No local media availability may introduce an extra random draw on one peer.

Evidence and limits: native hashes and the PE descriptor were checked directly;
managed Vanilla, SE and mod call chains were inspected, including the two different
Lord-index bases and custom-text fallback. Runtime regression fixtures can verify
hook results, save codecs and unchanged media/RNG behavior; such fixtures do not
establish Unity rendering, an actual repeated save/load roundtrip or matching
host/client acceptance. Those in-game checks remain separate from this static audit.

## Allied messenger placeholders and presentation groups (2026-11-11) [Vanilla facts]

The installed native SHA-256 was rechecked against CURRENT.json and the semantic
manifest: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The matching managed baseline remains
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
The function labels below remain candidate names; the stated table addressing,
record fields and managed call flow are confirmed-static from instructions,
canonical PE table reads and the hash-bound managed decompilation.

### Original per-Lord references

The two addressing forms in wrapper RVA `0x103AA0` corroborate the speech table
at RVA `0x316450` and video table at RVA `0x318340`: each category has 30 pointer
slots, indexed by `category * 30 + originalSpeechLord`. Slots 1 through 29 are
built-in Lords; slot zero is the generic/custom source. The original speech Lord
comes from the AIC record's field at offset +4, so a player using a user AIC does
not by itself lose the original Vanilla speech identity. The built-in text
request uses `TEXT_SKIRMISH_SPEECH` and
`(originalSpeechLord - 1) * 34 + category`; actual message categories are 1..33.
The separate preliminary announcement has category zero and is not the message
being spoken in this table lookup.

For a goods request, Rat, Snake, Pig and Wolf all select `all_req_01.wav` and
`bad_soldier_taunt.webm`. Their generic allied categories otherwise use the
`all_*` voice family with `bad_soldier_taunt.webm` or the game's spelling
`bad_soldier_nevous.webm`. This is a shared messenger presentation, not a distinct
voice resource for each of those four Lords. Rat and Pig have their own
`DieAlly` media and therefore must not be treated as generic for that category.
Wolf also has generic `KillNpc` media; that category is outside the allied
request/response group and must not be inferred to be an allied request merely
because its media uses the same messenger family.

Existing `Taunt1` references are:

| Lord | Speech filename | Video filename |
| --- | --- | --- |
| Rat | `rt_taunt_01.wav` | `rt_taunt2.webm` |
| Snake | `sn_taunt_01.wav` | `sn_taunt1.webm` |
| Pig | `pg_taunt_03.wav` | `pg_taunt1.webm` |
| Wolf | `wf_taunt_01.wav` | `wf_taunt1.webm` |

These are original table references, not copied assets or a claim about the
precise wording in any installed speech language. All 29 built-in Lords' 33
message slots have nonempty speech and video reference strings in this image;
that does not prove that every unused category has meaningful or playable media.

### Output group and text lifetime

Presentation RVA `0xFEE50` passes a text header to emitter `0x1A2850` before
speech and video. A complete ordinary Lord group has record kinds 20 (text),
18 (speech channel 1, gain 100), 22 (controls), then 21 (video). The optional
frontend branch between controls and video can emit music/ambient changes or
stop a previous video. The two byte booleans passed to `0x1A2850` become short
fields `[13]` = audio present and `[14]` = video present in its 21-short record.
Neither field is a speaker/player identity. The header's text section and index
are stored in fields `[11]` and `[12]`.

`GameMap.processTestMap` case 20 currently reads the text section/index and
discards those two presence flags. Case 18 reads the speech filename from the
supplied PlayState and submits it to `SFXManager.playSpeech`. Case 21 reads the
video filename from that same PlayState. Its field `[3] < 0` means stop;
`[3] == 0` is a valid ordinary nonlooping video start; `[3] > 0` requests looping.
Field `[11] > 0` requests waiting for speech. `speechFileName` and `binkFileName`
are one value each per PlayState, not independently carried by every record;
they do not establish separate speaker identity for multiple headers.

`OnScreenText` reuses one OST object per slot. Each MESSAGE_BAR add writes the
current text data and deadline into that existing object; object identity alone
therefore cannot distinguish two queued messages, even if their text indices
are identical. Its regular `updateOST` resolves and displays the active message.
The public `getOST(..., handleStatChange: false)` observes the active object
without consuming its changed/off flags. `initOST` replaces the slot objects.

## Lord message presentation extension (2026-11-11) [mod integration]

APIShared's new Lord-message service attaches to the managed presentation path,
after native queueing and the Extender's media choice. A local render-invocation
and header-index pair identifies each validated complete group. Selection changes
only the managed media call arguments and a generation-bound text presentation;
no native queue field, text index or record buffer is rewritten. This avoids
passing private provider-qualified filenames through the native queue's fixed
100-byte path slots. The existing per-Lord media resolver and packaged custom-Lord
claims retain their ownership and random-selection behaviour.

The test consumer filters Rat, Snake, Pig and Wolf, the allied categories
`RequestGoods` through `MerryChristmas` and `About2Siege` through `WillAttackEnemy`,
and the effective original messenger audio/video. It then references `Taunt1`
for all three components of that same Lord. A custom AIC retaining a Vanilla
speech identity can match; authored Extender media with different effective paths
must not match merely because the text index names one of these Lords.

These are source/static integration contracts. Original table reads and isolated
managed fixtures do not prove audible speech, video decoding, subtitle appearance,
queue transitions or actual host/client acceptance in a running game.
