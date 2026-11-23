# Matching Unity Editor and asset pipeline [Scope: Mod work, not Vanilla]

## Local installation and reproducible build

On 2026-11-23 the installed SHCDE Windows Player logged Unity **2021.3.45f2**
(revision `88f88f591b2e`) and ran with Direct3D 11. The matching Editor
was installed at `E:\GameDev\Unity\2021.3.45f2\Editor\Unity.exe`, alongside
the existing Unity 6 Editor at `E:\GameDev\Unity\Editor\Unity.exe`.
The matching Editor occupied about 5.94 GiB after installation. Its installer
was removed. Players use the Unity Player shipped with the game and only
need the mod's compiled bundle.

The [BugfixesAndQoL build entry point](../../../../../BugfixesAndQoL/Rebuild-ProjectileTrailShader.ps1)
uses that Editor or the version-checked `SERP_TRAILS_UNITY_EDITOR` override.
It builds a short-lived project for Windows x64 in batch mode, enables the
built-in AssetBundle module, and invokes the checked-in
[bundle builder](../../../../../BugfixesAndQoL/Shader/BuildShaderBundle.cs).
Keep the shader's checked-in `.meta` GUID stable. The working build uses an
uncompressed bundle. Run the builder script with `-Verify` to compare it with the packaged source bundle.
An early LZ4 build without the AssetBundle module was rejected by the loader.
Both settings changed before the successful test, so the initial failure
does not establish which change was necessary.

The bundle shipped with BugfixesAndQoL 1.0.237 reproduced the final
standalone renderer's SHA-256
`2640546595F2C048A2E2FD4B6CC0B10D73EAC2E450615EA3E96792B9EE43B56F`.
The regular mod build copies and verifies that bundle. The Unity Editor
is only needed when rebuilding the shader. The capacity results below
were recorded on an earlier revision of the bundle.

## Confirmed on Direct3D 11

- A second uncached build reproduced the 85,072-byte shader bundle exactly.
  Its SHA-256 was `DA76B283E815C59EB51058EE789528FE30C26450ED18F11EC44D84ACD1148542`.
- A standalone Windows Player built with the same Editor loaded that bundle
  and verified the three opacity stages by pixel readback. It also verified
  that expired and future samples stayed transparent and that all 50,000
  expected positions were drawn. See `.work/a/trail-gpu/001/Player-capacity.log`
  and the adjacent harness sources.
- SHCDE loaded the same bundle by `AssetBundle.LoadFromFile` and rendered
  the shader in its Universal 2D pass. The synthetic in-game fixture submitted
  50,000 visible afterimages in 96 sort groups. The fixed-arrow comparison
  on `test_green` corroborated the Vanilla pose, depth and opacity sequence.
  See [Extreme Troops](EXTREME_TROOPS.md#gpu-renderer-runtime-check-2026-11-23-mod-integration)
  and the [archived game tests](EXTREME_TROOPS.md#archived-projectile-trail-results-mod-integration).

The builder includes Direct3D 12 and Vulkan shader variants. The results
above only cover Direct3D 11. For a different player version or graphics
backend, recheck the engine revision, bundle load and actual draw output.

## Reuse for SHCDE mods

- **Build mod-owned rendering assets.** Package custom shaders, textures,
  materials or meshes as Windows bundles. Confirm each shader pass, sorting
  layer, atlas binding and blend state in SHCDE's
  renderer. [Unity AssetBundle documentation](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundlesIntro.html)
- **Create deterministic visual regressions.** Use a small standalone scene
  and read pixels back for alpha, atlas UVs, draw order, culling and high
  instance counts. Both an isolated scene and an in-game fixture are needed
  when the result depends on SHCDE's sprite layers, camera, tile projection
  or building occlusion.
- **Profile the presentation code independently.** Use a standalone
  development Player to inspect draw calls and buffer updates in isolation.
  The standalone fixture can use the Unity Frame Debugger when it meets
  Unity's connection prerequisites. The shipping game's Profiler or Frame
  Debugger attach path was not established. Reproduce cost in an in-game
  matching workload before drawing conclusions about whole-game performance.
  [Unity Frame Debugger documentation](https://docs.unity3d.com/2021.3/Documentation/Manual/FrameDebugger.html)

### Unit sprites and animation [mod integration]

- **Replace the frames SHCDE already plays.** Start with the SHCDE Sprite
  Previewer to identify frame names and export reference sprites and team-color
  masks. For a few frames, ship the PNGs and matching `_m.png` masks under
  `Override/Sprites/`. For a full set, evaluate the Extender's experimental
  atlas override under `Override/Atlas/<gmFileName>/`. It uses a PNG or DDS
  texture, an optional mask, and `atlas.json` with frame names, rectangles,
  pivots and scale. SHCDE still selects the frame and controls playback timing.
  [Script Extender asset guide](../../../../../shcde-script-extender/docs/guides/asset-api.md#replacing-unit-sprites)
- **Preview a large redraw in Unity when it saves iteration time.** Slice
  sheets, align foot pivots, and preview frame loops and direction changes
  before testing team colors, foot placement and timing in SHCDE. Unity's
  Sprite Atlas format differs from the Extender's override format. Export to
  the Extender's frame metadata and keep the color and mask textures aligned.
  [Unity Sprite Editor](https://docs.unity3d.com/2021.3/Documentation/Manual/SpriteEditor.html)
- **Author independent visual effects.** Own clips, materials, particle effects
  and prefabs can define a shield glint, aura or another visual layer. Package
  them in a Windows bundle and let the mod plugin handle the trigger, unit
  attachment and cleanup. A new clip driven by a unit action needs an explicit
  connection to the game's state. A sprite override only supplies replacement
  frames for the slots the game already selects.
  [Unity Animation Clips](https://docs.unity3d.com/2021.3/Documentation/Manual/AnimationClips.html)

### CastlePlanner [mod integration]

- **Start with a small standalone rendering fixture.** Use fixed inputs,
  camera and zoom to check atlas cropping, opacity and draw order. Verify
  terrain projection and occlusion by buildings in SHCDE, for example with a
  stair on a slope or a marker behind a tower.
- **Measure before introducing a marker shader.** The current
  [BlueprintRenderer](../../../../../CastlePlanner/src/BlueprintRenderer.cs)
  already batches ground markers by map row and handles building icons and
  depth captures separately. For large plans, measure time to first visible
  output, time to full preview and frame time while the preview stays visible.
  Consider a shader only if measurements point to marker generation, upload
  or drawing costs and a like-for-like in-game comparison shows a benefit.

### Other editor uses [mod integration]

- **Package mod-owned audio and reusable objects.** Use bundles for imported
  AudioClips, prefabs and serialized assets that the plugin loads. Custom
  script types live in assemblies and must be available when their serialized
  assets are loaded.
  [Unity AssetBundle documentation](https://docs.unity3d.com/2021.3/Documentation/Manual/AssetBundlesIntro.html)
- **Build visual authoring and inspection tools.** An editor window could
  display an AIV's footprints, rotations and overlaps. A plain .NET test can
  cover format validation, and placement on a real map is verified in SHCDE.
  [Unity Editor windows](https://docs.unity3d.com/2021.3/Documentation/Manual/editor-EditorWindows.html)


The installation gives us a compatible toolchain for mod-owned assets and
test players. SHCDE's native simulation, game asset contracts and Noesis
modsettings still use their existing development and verification paths.
