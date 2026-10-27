# Foreign troop HUD skin selection [Scope: Vanilla + mod integration]

## Provenance (2026-10-23)

- Installed native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Installed managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
- Evidence: current semantic decompiler export, canonical installed PE material table,
  hash-matched managed decompilation, atlas resource dictionaries and ILSpy checks
  against the installed `SHCDESE.dll`. Native function roles below are confirmed
  statically by their data flow; generated raw function labels remain `candidate`.
  Actual in-game visual acceptance is a separate check.

## Closed skin-to-render and portrait paths

`GameUnit.r_GameMaterialIndex` is a public `GM` field at structure offset `0x08`.
`GM` is a UInt16 enum. The unit record starts at manager offset `0x65C`, has stride
`0x490`, and uses a sentinel at ID zero. Thus the material address is manager
`+0x664+unitId*0x490` (image RVA `0x67E8A64+unitId*0x490`); a span index is still
`unitId-1`. Adjacent type, ownership and animation fields agree with this layout.

- Initialization `0x19A240` reads the type-indexed UInt16 material at
  `0x321F80+type*4` and writes the current material and its companion field.
  The canonical PE's monk entry (type 37) contains 109, `GM_BODY_FIGHTING_MONK`.
- The monk update `0x151090` retains this default in the ordinary culture branch,
  or writes 229, `GM_BODY_TEMPLE_GUARD`, in the alternate branch before processing
  movement/combat/other unit states. The choice includes native scenario and
  player-culture conditions; it is not equivalent to the viewer's culture.
- Reset `0x19C130` restores type defaults for live/initializing records except
  the child type. Subsequent monk updates can apply the alternate material again.
  Consumers must read the current field rather than cache a culture choice.
- Visible rendering `0x41D60` reads the same current material as a signed word,
  then passes it unchanged as parameter 6 to `0x1A13C0` for unit packets.
  The alternate render-delay and transparency branches affect emission, placement,
  animation or visibility, not this monk material choice. The special hidden-unit
  branches belong to other unit types.
- `0x1A13C0` emits the material in the unit record consumed by
  `GameMap.processTestMap` (record type 2, word 3, `num47`). Zero means deletion;
  otherwise `addUpdateChimp` passes the file/material to
  `SpriteMapping.SetBodySprite` both for new and existing visuals.
  `spriteLoader` loads `body_fighting_monk` for 109 and `body_temple_guard` for 229.
  Animation and player-color remapping are independent arguments.

All direct current-material global references were reviewed. Other writers belong
to the priest/Imam, Lord variants and another civilian type; they do not add a
monk-specific transformation after the above material is read.

Managed `MainViewModel.UpdateUITroopSprites(int colour, bool arabic)` selects
`UI-Buttons K011`/`K012` for ordinary monks and `K011a`/`K012a` for Temple Guards.
`HUD_Troops.xaml` binds `MonksSelected` to these resources. The ordinary and alternate
K011 resources exist in all eight portrait colors across UI-MasterAtlas,
UI-Colour1Atlas and UI-Colour2Atlas. Other K-series unit portraits in this method
have no culture-specific alternate. Native sound selection at `0xD6030` also has
separate Monk/TempleGuard branches, but is not used as the HUD skin source.

## Consumer contract and validation [mod integration]

Foreign HUD grouping remains owner/type. Each refresh captures the first matching
living marked unit's material for each group; the view uses the Temple Guard key
only for `CHIMP_TYPE_MONK` with `GM_BODY_TEMPLE_GUARD`. Other materials retain the
existing type portrait, and Lords retain the existing dedicated portrait path.
Colored-resource failure falls back to the same skin's base artwork; missing base
artwork retains the existing type-label fallback. No native calls, writes or hooks
are introduced. Current owner-to-portrait color mapping remains independent.

`tests/ForeignTroopHud/Test-Portraits.ps1` compiles the production helper, entry and
complete view with game/Noesis boundary doubles. It checks both skins in all eight
colors, skin-only invalidation, unchanged health/counts, pooled snapshots,
pagination, HUD recreation, camera callbacks and resource fallbacks. The existing
player-color tests remain applicable.
The capture test also compiles the production owner/type aggregation block with
isolated unit/color inputs: first-unit skin choice, mixed-skin aggregation and
pooled-entry reset execute directly. A narrow source guard retains the audited
material offset. These tests do not prove actual Noesis rendering or mission
transitions in-game.
