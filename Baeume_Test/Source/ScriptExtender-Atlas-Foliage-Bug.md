# Atlas overrides cannot preserve foliage materials

## Affected version

Script Extender 2.3.0

## Problem

Vanilla loads `Sprites/treeSprites` with `foliage: true` and assigns `Unlit/Foliage` plus the shared foliage mask to all non-cactus tree GM groups.

`GameAtlasManagerAPI.BuildMaterials`, however, assigns `Unlit/TeamColour` whenever `atlas_m.png` exists and the plain material when it does not. Auto-discovered atlas mods cannot select or preserve the original material type. Individual sprite overrides have the same issue because `GameSpriteManagerAPI` also creates `Unlit/TeamColour` materials unconditionally.

As a result, structurally valid tree atlas overrides load and replace their frames, but foliage rendering can be visibly incorrect.

## Expected fix

- Preserve the original GM material type by default, replacing only its mask texture, or expose an atlas material mode that includes `Foliage`, `TeamColour`, and `Plain`.
- Keep the existing `gmColors` foliage palettes unless an explicit colour override is supplied.
- For partial atlas overrides, size the replacement arrays to at least the original array length so trailing vanilla frames are not discarded. A concrete reproduction is `tree_birch`: SH1 provides frames 0-72, while Crusader has frames 0-147; the current code creates an array of length 73 and drops vanilla frames 73-147.

