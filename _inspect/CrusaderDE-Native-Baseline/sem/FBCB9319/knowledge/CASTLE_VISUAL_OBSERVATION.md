# CastlePlanner building visual observation (2026-10-24) [Scope: Mod work, not Vanilla]

Installed native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The diagnostic does not install a native hook or change any graphics value.

## Relevant audited boundaries [Vanilla facts]

- The construction dispatch at 0x6D580 reaches the generic construction path
  0x75780, which calls allocator 0xB47E0. Native building records use stride
  0x32C; their managed interop view is `GameBuilding`. The allocator's spawn
  Post event can precede the caller's footprint and graphics registration.
  `NeedsInit` at that boundary is not by itself a failure.
- 0x75780 records occupied tile IDs, current height, building occupancy,
  structure ID/type and changed state. Its 0x6CDD0 helper updates the
  miscellaneous display grid; 0x725E0 updates the affected tile/path region.
  0x69850 supplies the scale/rotation-dependent footprint offsets.
- The confirmed Extender-bound `c_game_update_visual_resourcetile` at
  0x6E620 writes GFX/alpha grid entries. Sprite variation 15 has an explicit
  selection branch; it must not be classified as invalid or normalized to zero.
  Other role names above are descriptive, not newly confirmed symbols.
- The actual managed `GameMap` render-packet consumer maps native coordinates
  with `mapGameTileToTilemapCoord`, reads 21-short entries, resolves graphics
  through `SpriteMapping`, and stores sprite, construction, height and hover
  state in `GameMapTile`. `TilemapManager` subsequently refreshes the tilemaps.
  Native and managed observations can legitimately belong to different phases.

Verified against hash-selected semantic function records and the native field
flow, the local Extender APIs and actual installed Assembly-CSharp metadata.
`getMapTile`, `mapGameTileToTilemapCoord`, `CurrentRotation` and the observed
GameMapTile fields are public in the installed assembly. Unknown grid values
and raw building bytes remain observations, not validity rules.

## Reproduction and interpretation limits

Reported reproduction: The Bulls eye.map, Vanilla wolf6.aivjson, green start
location in the lower right, castle rotation South (0 degrees). The user reports
several affected buildings, hover flicker and persistence across camera rotation.
The two later screenshots show blueprint overlays and the clear ground, not the
glitched placement state. No causal terrain or placement conclusion follows.

CastlePlanner's /devdiag-only observer captures the native construction stages,
then UI/hover/rotation/blueprint states, with immutable bounded file output.
It does not call refresh/repair routines, change sprites or alter gameplay.
The normal DLL is checked for absence of the diagnostic implementation and calls.
Real reproduction and diagnostic output are still needed to locate the fault;
static checks and a successful diagnostic installation are not gameplay evidence.

## Pipeline observation extension (2026-10-24)

The installed hash remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The reproduction produced 41 captures with no dropped captures or observation
exceptions. Granary building 10/global 13170 and the adjacent granary and brewers
had complete, distinct 4x4 occupancy and current/default native height 8 throughout
their footprints. This rules out a height discontinuity in those captured cells,
not a graphics or other terrain-dependent bug. No root cause is established.

Further managed audit against the installed real Assembly-CSharp.dll confirms:

- Director consumes MemoryBuffers in order through GameMap.processTestMap,
  then TilemapManager.endTileRefresh. A buffer can update a cell repeatedly;
  a method-post observation is the final state of that entire buffer, not the
  intermediate result of each record. Records are 21 signed shorts; a negative
  requested count uses a negative record-kind sentinel. Observers additionally
  bound reading to the actual managed array, without changing Vanilla arguments.
- Type-zero records resolve construction, base sprite, heights, light/colour and
  chevron graphics and enqueue a tile refresh. SpriteMapping can legitimately
  remap images and lighting; raw material/image numbers are not Unity sprite IDs.
- endTileRefresh consumes dirty cells only for visible rows and columns; refresh
  and GetTileData completion need not coincide with the incoming native packet.
  gameTile.GetTileData also applies colour and transform to the actual Tilemap,
  updates refresh bits and mirrors the returned sprite. A raw TileData transform
  or colour alone does not describe those separate Tilemap writes.
- setMouseTile performs an explicit mouseOver/refresh operation. There are no
  ordinary managed callers in this assembly; normal building hover is also
  carried by PlayState.in_structure. Do not interpret zero setMouseTile callback
  counts as a failed normal-hover observer.
- Building animation rows are a separate rendering path with composite object
  IDs, pixel offsets, transparency and row/layer sorting. A render object ID is
  not assumed to equal a native building ID. GameMap.buildingAnims is private;
  the exact Dictionary<int,BuildingAnim> reflection contract is validated before
  hooking. BuildingAnim/GameSprite scalar and renderer fields are public.

The /devdiag extension observes these four public managed methods through permanent
MonoMod hooks, preserving the existing APIShared.Audio processTestMap IL hook.
There are no new native hooks or graphics setters. Native snapshots are copied
only in the existing loader/spawn/tick contexts; render observations reference
their copied identities and monotone timestamps, with explicit age/generation.
Loaded-save observation waits for the matching successful central lifecycle start
and does not execute castle creation. The new save is test_castlespawngraphicglitch.sav.

Evidence and pending runtime boundaries are recorded under .work/a/castle-visual/002.
Read-only state comparisons and transition traces are observations; delayed
refresh, transparency changes, missing callbacks and shader property absence are
not automatically classified as errors. Hook callbacks, real save replay and
the first divergent rendering stage still require the next in-game test.

## Loaded-save observer selection correction (2026-10-25)

The next real run loaded test_castlespawngraphicglitch.sav in a new process and
the user reports that the visual defect persisted. Native hash is unchanged.
However, the diagnostic selected zero granary watches: the mode snapshot has
gameMembers=-1, and ActivePlayerHelper intentionally returns an empty list when
the synchronized multiplayer roster is absent. Reusing that helper for local
save observation was a diagnostic bug, not evidence of absent granaries.
There were 1,559 process-map and 5,826 refresh callbacks but no focused building
captures; the single 6,782-byte file contains only presentation-state transitions.
It cannot locate the rendering defect. Zero focused GetTileData callbacks in
this run do not imply that Vanilla did not call GetTileData.

The diagnostic now uses the central MissionContext.LocalPlayerId for local saves;
the roster remains supplemental evidence only in real multiplayer. A corrected
identity or initially missing targets schedules one discovery retry on the existing
native tick. Empty target sets are explicitly unavailable, never logged as ready;
a failed final retry retires observation instead of accumulating unrelated data.
The existing frame/native-thread boundary and all rendering hooks are unchanged.
Runtime proof and regression evidence: .work/a/castle-visual/003. This is a fix
to observation selection; the building graphics cause remains unresolved.

## Confirmed supplemental granary graphics variant mismatch (2026-10-25)

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The corrected observer recorded two actual player-1 granaries in session
20261025-001928-365-p43444-s1. Original AIV granary 10/global 13170 has variant 15;
supplemental granary 245/global 13700 has variant 0, both with size 4 and base
production image 237. The earlier synchronous spawn observation already shows
245 with variant 0 in NeedsInit: this value was not introduced by save loading.

The complete relevant parameter chain is confirmed against the selected native
records and installed Extender enums (MAPPER_GRANARY=80, STRUCT_GRANARY=19):

- CastlePlanner CreateSupplementalPrefab passes constant zero as CreatePrefab a7.
  The Extender forwards it as parameter 7 to 6D580. The dispatcher preserves it
  for mapper 80 (the mapper-54 exception is unrelated), converts mapper to struct
  via C77C0 and uses the generic 75780 path for struct 19. That forwards it as
  allocator B47E0 parameter 9. The allocator stores it at manager/slot+15E, the
  same variation read by the renderer. Granary does not enter the allocator's
  type-specific variation rewriting branches.
- 6E620 (confirmed c_game_update_visual_resourcetile) has distinct variation-15
  automatic and non-15 camera-relative branches. Non-15 adds a direction-dependent
  multiple of footprint size squared to the image; granary size squared is 16.
  The ordinary AIV placement path 51790 starts with variant 15 and granary mapper
  80 does not enter its directional/special-building exceptions. Constructor role
  names remain descriptive; the parameter flow and renderer branch are confirmed.
- Actual UI sprite sets match the unintended offsets exactly: managed camera 7
  gives original 237..252 versus supplemental 269..284 (+32); camera 5 gives
  237..252 versus 301..316 (+64); camera 3 gives 278..293 versus 374..389 (+96).
  The last pair uses the alternate image set in both buildings. These shifts are
  already in type-zero input packets and are preserved by SpriteMapping,
  GetTileData and the actual Tilemap. There is no recorded returned-sprite versus
  Tilemap-sprite discrepancy for this interval.

This establishes a wrong spawn graphics variant for the supplemental granary.
It explains incorrect image fragments and view dependence without a terrain or
missing-refresh hypothesis. Other supplemental buildings share this caller, but
their appropriate variants must be audited per mapper; globally replacing every
zero with 15 is not established by this granary finding.

The packets also alternate rapidly between image sets separated by 41 during
hover/rotation. That fluctuation exists before Unity; whether correcting the
variant removes every flicker symptom requires a targeted gameplay comparison.
The 32 MiB limit was reached after roughly ten seconds, so later activity is not
covered. The captured interval includes the relevant hover and three camera views.

Evidence: .work/a/castle-visual/004/cause.json. No runtime implementation, installed
DLL or game state was changed by this analysis. Existing saves retain variant 0;
a corrected future spawn alone cannot repair those persisted records.

## Supplemental prefab automatic sprites (2026-10-25)

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The complete current supplemental set is audited in
`.work/a/castle-spawn-fix/001/supplemental-audit.json` (21 mapper entries,
checked against installed Extender enums and the catalog-driven spawn routes).
Native AIV builder 51790 initializes argument 7 to 15; none of these mappers
enters its hovel/directional/workshop/drawbridge exceptions. In 6D580, argument
15 and the old 0 both select unrotated placement geometry; the difference is
sprite selection in 6E620, where 15 avoids camera-relative sheet offsets.

Constructors are covered by dispatch, not by assuming granary behavior:
- Granary, armoury and the ordinary fear buildings: 75780 forwards variation to
  B47E0 argument 9. That allocator persists the variation consumed by 6E620.
- Stores (struct 10): 76E80 creates four stockpile records and forwards the
  variation to B47E0 for each record.
- Gardens, statues and shrines: 73EA0 passes the mapper-derived family subtype
  to B47E0 (confirmed from canonical machine code, since decompilation omits
  arguments). The caller's orientation argument is not the family subtype.
- Cess pits and ponds: 76060 similarly retains the mapper-derived family data;
  allocator normalization preserves their specialized sprite behavior.

SupplementalPrefabVisualPolicy accepts exactly the current supported mappers
and supplies the automatic-sprite sentinel 0x0f (no Extender enum defines it).
Unknown supplemental mappers are rejected before creation, pending their audit.
All supplemental calls pass through SupplementalPrefabSpawner and the one
CreatePrefab call in CastlePlannerRuntime; native AIV placement, hovel styles,
keeps, gates and workshops retain their separate conventions. No new native
hook, offset, public member, serializer or lifecycle callback is introduced.

Tests enumerate all Extender mapper values against actual FearFactor and
compound/storage routes, then execute each supported mapper through the real
capture helper and verify a single delegate invocation with variation 15.
Existing identity, nesting, raw-return, terrain and rotation checks remain.
Existing saves keep their persisted wrong variation; this is a future-spawn fix.
Gameplay confirmation, especially the rapid alternate-image hover symptom,
requires a newly spawned castle rather than reloading the old defective save.
