# Projectile trail acceptance

The acceptance harness exercises the renderer, settings and Standard Troops
replacement in BugfixesAndQoL 1.0.237 or newer. Build it with
`build.bat /nopause` or set an isolated package output with
`Build.ps1 -NoPause -OutputPath <package directory>`. The default package is
`bin/harness/ProjectileTrailsTest_Serp`. The build also runs the offline policy
tests directly against the main mod's history implementation.

Run the harness with the shared `Invoke-InGameTest.ps1 -HiddenDesktop` runner
and a plugin layout containing BugfixesAndQoL, Fixes and Script Extender. Use
the matching APIShared plugin and patcher. Set `SERP_TRAIL_SHOTS` to write the
optional game screenshots into the run's evidence directory. The harness
configures each variant before loading the map and observes the actual
installed main mod. The runner restores the original plugin and setting files.

| Scenario | Acceptance |
|---|---|
| `trails-green-standard` | Standard on `test_green.map`, host replacement off. Verify Vanilla afterimages and record their visible footprint. |
| `trails-green-standard-replaced` | Standard with host replacement on. Verify that the old copies are suppressed while production replacement copies appear. |
| `trails-green-standard-hidden` | Standard with host replacement on and local length zero. Verify both sets of copies stay hidden. |
| `trails-green-standard-unavailable` | Stage a disposable main mod package without its shader. Request Standard replacement and verify the original afterimages remain visible. |
| `trails-green-standard-restart` | Restart through the in-game menu during a paused shot. Verify a linked new mission clears the old trail but retains the host setting and renders a fresh shot. |
| `trails-green-fire` | Standard with host replacement on. Verify that Vanilla fire-arrow smoke remains visible and the main mod adds no ordinary copies. |
| `trails-green-extreme` | Extreme with host Standard replacement off. Verify the production replacement and pause behavior. |
| `trails-smoke` | Fire real arrow and bolt volleys in all four directions on `test_stresstest5.sav`. Verify pause, then load `test_gates_vanilla.sav` and verify the production renderer has cleared. |
| `trails-mass-default` and `trails-mass-gpu` | Compare 5 copies / 10,000 maximum with 10 / 60,000. Use the same two controlled 5,000-archer volleys, camera and speed. Inspect `MASS_RESULT` for frame times and accepted copies. |

Compare `ECHO` lines from the Standard, Standard-replaced and Extreme runs
for world position, draw order and fade at matched shot phases. Vanilla
alpha comes from the actual SpriteRenderer. Replacement alpha is computed
with the production opacity function for the samples actually submitted to
the renderer. Overlap compositing requires separate visual review.

On 2026-11-23, the Standard Vanilla (`34b74f1b46ba`), Standard replacement
(`de7011b8b47f`) and Extreme replacement (`f959337c6162`) runs on
`test_green.map` each reached ten copies and held ten while paused. Both
replacement runs visited the same 15 positions as Vanilla. At 98 shared
position and launch-relative tick pairs per comparison, all 98 sorting orders
matched. The alpha matched for 91 pairs. The remaining seven faded one level
earlier in the replacement, consistent with the known one-tick difference in
native copy aging. All three variants used the same three opacity levels.
Standard host replacement left zero native arrow copies. A local trail length
of zero left both renderers blank (`407979f6de46`), and an ignited arrow retained
seven active Vanilla smoke particles with zero ordinary copies (`fca362cf6e13`).


In the final build, a disposable package without the shader logged the
missing asset and retained ten native copies with zero production copies
(`999b81abc189`). The same build with its shader reached ten production
copies and zero native copies (`899250006ac8`). The in-game menu restart
opened a new linked mission with empty history, and the next shot again
reached ten production copies and zero native copies (`8876c51266d6`).

The final 1.0.237 build also passed the four-direction rotation, pause and
save-switch check (`e8d91063610b`). On `test_green`, the controlled load test
used 5,000 distinct archers, two volleys of 5,000 admitted shots each,
a fixed camera and zoom 0.256 at 1280×720.

| Copy settings | Peak drawn | Median frame | p95 frame | Average draw calls |
|---|---:|---:|---:|---:|
| 5 / 10,000 | 10,000 | 12.34 ms | 27.05 ms | 853 |
| 10 / 60,000 | 49,997 | 21.81 ms | 37.82 ms | 863 |

Both runs reported zero native afterimage slots. The recorded timings
include the whole frame on this machine. GPU frame timing was unavailable.
The run directories are `1a312efdd3bb` and `cd9ea994d3b0` under
`.work/a/bfq-trails/001`.

## Historical standalone prototype

The instructions and results below refer to retired standalone revisions.

`build.bat /nopause` builds the ordinary test plugin in `BepInEx/plugins/ProjectileTrailsTest_Serp` under this mod directory and runs the offline tests. The local BepInEx setting `[Display] Enabled` defaults to true. Extreme Troops supplies the session gate.

Use `build.bat /nopause /devdiag` for the test runner. The diagnostic package is in `bin/devdiag/ProjectileTrailsTest_Serp`. Run it with the shared `Invoke-InGameTest.ps1 -HiddenDesktop` runner, an allocated test output directory and the matching APIShared plugin/patcher pair. `SERP_TRAIL_SHOTS` can point to the evidence directory for game screenshots.

| Owned scenario | Fixture and acceptance |
|---|---|
| `trails-smoke` | Load `test_stresstest5.sav`, emit real arrow and bolt volleys near a gate, record four camera views and a paused view, then switch to `test_gates_vanilla.sav` in the same process and check that the trails clear. |
| `trails-standard` | Load `test_gates_vanilla.sav` in a fresh process and confirm the Standard Troops gate while firing a real volley. |
| `trails-archers` | Load the less crowded Extreme save `test_dustynoattack2.sav`. Spawn a local group of archers and crossbowmen, order Attack Here and check projectile source IDs. A controlled arrow and bolt volley supplies the paused image pair if the unit shots are insufficient. |
| `trails-stress-off` and `trails-stress-on` | Load `test_stresstest5.sav` with the same zoom, resolution, camera and speed. After a warm-up volley, sample five measured volleys per process. Alternate off/on twice. Read frame p50/p95/p99, accepted shot count, peak rendered segments, Draw Calls and GC bytes from `TRAIL_PERF`. |

Review actual captures for foreground and background shots at a wall, tower and gate. A positive segment count verifies submission. Occlusion and visibility require inspecting the captures. Draw Calls and GC bytes in `TRAIL_PERF` measure the whole Unity frame. The `projectileTrail.meshUploadAllocatedBytes` diagnostic measures the trail renderer separately when the installed Mono allocation counter is available. Newly created or enlarged mesh batches can allocate during warm-up.

## First prototype desktop test on 2026-11-22

Hidden desktop, Script Extender 2.14.4, APIShared 0.63.0, installed Fixes, 1280×720. This run used the original thin-line renderer. The smoke test submitted 41, 96, 192 and 230 local segments in North, East, South and West. Segments persisted during native pause and cleared after switching in the same process to Standard Troops. The second save admitted 48 arrows and 48 bolts and submitted zero custom segments.

The archer case spawned and assigned 24 archers and eight crossbowmen. Vanilla accepted Attack Here. The projectile source check matched 24 of 25 rendered segments to the spawned group before the paired paused screenshots. Views around the terrain and fortifications were inspected. The captures do not isolate overlaps at each wall, tower and gate, so individual occlusion cases remain unverified.

| Stress run | Peak segments | Frame p50 | Frame p95 | Frame p99 | Mean draw calls |
|---|---:|---:|---:|---:|---:|
| Off 1 | 0 | 3.994 ms | 8.471 ms | 10.538 ms | 422.423 |
| On 1 | 1,024 | 4.037 ms | 9.016 ms | 14.151 ms | 477.631 |
| Off 2 | 0 | 3.714 ms | 7.757 ms | 11.811 ms | 421.794 |
| On 2 | 926 | 3.921 ms | 8.857 ms | 12.149 ms | 479.447 |

Each run admitted 800 arrows and 800 bolts at speed 40 and zoom 0.25 with zero measured camera drift. In-game GC byte accounting yielded no frame samples. Mono's per-thread API also failed a positive control with a known 4,096-byte allocation. The offline tests measured zero managed bytes for warmed history and budget operations. Unity mesh publication remains unmeasured for managed allocations in-game.

Local evidence is under `.work/a/projectile-trails/001`. The smoke log is in run `398c30059dda`, the archer test in `f585ad8e16f9` and the stress series in `56f24cd84d83`, `6cd106ec4d1e`, `91b264df4e16` and `cfb6473660e6`. Every completed live runner transaction restored and verified the original plugin and setting files.

## Sprite afterimage revision on 2026-11-22

The installed line renderer logged that it was active in the player's Extreme Troops session, but the player could not distinguish its line from the arrow in a real capture. The replacement renders up to three fading copies using the actual sprite vertices, UVs and prior transform. A paused GPU image pair from run `349a36c56389` contains 97 active copies before the local toggle is switched off. The draw-call and frame-time results below use the same resolution, camera, zoom and 800 arrows plus 800 bolts per run as the first prototype stress test.

| Stress run | Peak sprite copies | Frame p50 | Frame p95 | Frame p99 | Mean draw calls |
|---|---:|---:|---:|---:|---:|
| Off 1 | 0 | 3.700 ms | 7.695 ms | 10.271 ms | 421.934 |
| On 1 | 1,024 | 3.863 ms | 9.187 ms | 11.806 ms | 482.059 |
| Off 2 | 0 | 3.560 ms | 7.519 ms | 11.198 ms | 422.115 |
| On 2 | 1,024 | 3.980 ms | 9.585 ms | 11.961 ms | 483.400 |

The GPU capture pairs and stress logs are in `.work/a/trail-sprites/001`. In the final 0.1.1 close-up (`ea804723b16c`), 196 fading sprite copies were active. The upper-left volley visibly extends behind the original arrows with the mod enabled. Stress runs `fbbfbd321951`, `ab6faac0b19e`, `7f45473e824b` and `d8be63885498` all passed and restored the installed plugin layout between runs. GC bytes were again unavailable in-game.

## Simulation-tick afterimages on test_green

On 2026-11-22 the hidden desktop runner used the local `test_green.map` and pinned
the human player to east slot 2 (radar 158,141; keep 450,393). The automated
Skirmish settings did not supply an archer, so both variants spawned one European
archer at 444,387 and used the same controlled regular shot toward 422,387
after the ordinary Attack Here trial did not fire in its time window. The
recorded speed was 15, zoom 0.5, and rotation North. This checks flight
presentation from an identical launch. It does not count as a test of
selection, archer orders, or the map editor's starting troops.

- Standard vanilla and Extreme with the probe's Unity meshes both reached ten
  simultaneous afterimages and held ten while paused. The comparison matched all
  15 recorded emission positions, subtile offsets, heights, sprite files/frames
  and draw orders, plus 36 opacity samples. All three Vanilla opacity levels
  were observed. The mod uses the shorter confirmed lifetime when native slot
  order would leave a copy active for one additional simulation tick.
- The two GPU screenshots show the paused arrow at the same location and phase.
  A crop of the flight path is visually close. Mesh batching does not guarantee
  the same per-pixel compositing order as independent native SpriteRenderers.
- The follow-up load test admitted both arrows and bolts in all four view
  directions and confirmed local mesh submission, persistence during pause and
  zero mod copies after returning to the menu and loading the Standard save.
  The separate wall, tower and gate occlusion cases remain unisolated.

Evidence: `.work/a/trail-green/003/green-parity.json`, Standard run
`ceab3f3acf46`, Extreme run `5f62244a2175`, rotation/save run `e1eabf0e9988`.
The runner confirmed the test window's hidden desktop and verified plugin and
settings restoration for each of these runs.

The production observer is byte-checked at startup and rechecked at mission
start. It keeps a fixed 16,384-entry process-owned ring (about 512 KiB), plus
reused Unity mesh buffers. It never calls back into managed code on the emission
path. Burst pressure can overwrite older observations in the ring. The
renderer tests viewport visibility before charging the 1,024-copy and 64-group
budgets. Each record is an independent visual sample, so ID reuse cannot
join two shots. Existing copies fade after the source projectile disappears.

### Controlled stress volleys

Four hidden runs alternated Extreme Troops with the local renderer off and on.
Each run launched 800 arrows and 800 bolts across five measured volleys on
`test_stresstest5.sav`. The equal settings were speed 40, zoom 0.25,
1280×720, North, and zero observed camera drift.

| Pair | Off p95 | On p95 | Off draw calls/frame | On draw calls/frame |
|---|---:|---:|---:|---:|
| 1 | 8.180 ms | 8.928 ms | 422.203 | 473.846 |
| 2 | 7.978 ms | 8.975 ms | 422.625 | 476.397 |

Both enabled runs hit the 1,024-copy ceiling. Unity's allocation counter
returned no samples. The Mono per-thread counter failed its positive 4,096-byte
control, so in-game allocation bytes remain unverified. The warmed offline
render-budget test recorded zero allocated bytes. Full report lines and runner
restoration proofs are under `.work/a/trail-stress/001`.

The regular 0.1.2 DLL and matching manifest were installed on this PC and
verified against the build output by SHA-256. The installation record is
`.work/a/trail-install/001/installation.json`.

### Archer mass comparison on test_green

`.work/a/trail-mass/006/mass-comparison.json` records the three completed runs.
Each match started the fixture at exactly 2,999 occupied unit slots, with 2,991
spawned European archers and eight existing units. Every archer was observed
as a source of native projectiles. Two controlled volleys of 2,900 native
arrows were admitted in each variant, with overlapping source lists covering
all 2,991 archers. An ordinary group attack in the earlier control had fired
from only 150 archers. The measured volley uses the native projectile creation
path and does not synchronize the archer attack animations. Any leftover
fixture arrows were retired between volleys outside the measurement interval.
The screenshot was taken during a pause in the first volley and outside the
timed window.

All variants used the same field, 1280×720, North, zoom 0.256 and speed 40.
The Standard map stayed at 2,999 units. Both Extreme variants ended at 3,015
units from normal in-game spawns. Native launches observed from fixture
archers including ordinary shots were 6,263, 6,264 and 6,264, respectively.

| Mode | Peak arrows | Median frame time | p95 frame time | Mean draw calls/frame |
|---|---:|---:|---:|---:|
| Standard, Vanilla trails | 2,960 | 4.574 ms | 11.639 ms | 559.379 |
| Extreme, Vanilla drawing | 2,961 | 4.716 ms | 12.496 ms | 562.416 |
| Extreme, Unity trails | 2,966 | 5.619 ms | 12.820 ms | 667.945 |

In the direct Extreme comparison, that is +0.903 ms at the median,
+0.324 ms at p95, and +105.529 scene draw calls per frame on average. These are
short runs of the full scene. The draw-call difference is not a count of
mesh groups. The native Standard pool peaked at 277 independent AfterImage
records, including records outside the view, while the mod reached its limit
of 1,024 submitted visible copies.

The large test exposed the calendar rollover at the old clock address.
The runtime now uses the monotonic native map clock, with the mass fixture
checking the address through the independent Extender getter before launching
any shots. The change also keeps the timed measurement windows from stopping
early across a month boundary.

With the monotonic clock in the runtime, both single-arrow modes again held
ten copies at the paused capture. The comparison matched 15 emission records
and 36 opacity samples. Updated proof is in
`.work/a/trail-green/004/green-parity.json`. The regular 0.1.3 build and
manifest were installed and verified at `.work/a/trail-install/002/installation.json`.

## GPU renderer

Build with `build.bat /nopause` or `build.bat /nopause /devdiag` using Unity
2021.3.45f2 at `E:\GameDev\Unity\2021.3.45f2\Editor\Unity.exe`. Override the
Editor executable with `SERP_TRAILS_UNITY_EDITOR` when needed. The mod package
contains the DLL, manifest and `projectiletrails-shader`; the installed game
uses its own Unity Player. The Editor build fingerprints the shader, its
fixed Unity asset GUID and the bundle builder. It reuses a verified cached
bundle when those inputs match.

The renderer uploads the original expanded triangle geometry once per
encountered sprite and appends only new geometry on cache growth. It projects
each admitted native observation once. A membership or camera change uploads
a 24-byte record per visible afterimage. Fade-only changes set the map clock
for the shader. Shared static meshes provide vertex IDs for transparent
renderers with the original sort-layer, order and atlas grouping. Inactive
carrier vertices are clipped through degenerate triangles.
The capture ring is sized to the full audited native projectile pool for
ten visible sweeps and one concurrent write sweep. There is no production
visible-copy or depth-group count limit.

The isolated Unity 2021.3.45f2 Windows Player on Direct3D 11 loaded the
same uncompressed bundle, verified the three alpha levels and rejected
expired/future samples by pixel readback. An additional draw verified all
50,000 distinct test positions (1,200,000 bytes of echo input). Evidence
and harness source are in `.work/a/trail-gpu/001`. This test exercises
the compiled projectile shader outside Stronghold and cannot verify
the game's sorting or scene performance.

### Game acceptance and 5,000-archer load on test_green (2026-11-23)

The paired fixed-camera test on `test_green` matched 15 pose, sprite and
depth records and all 36 sampled opacities. Both captures held ten copies
during the pause. The screenshots show the same trail shape with minor pixel
differences. The rotation fixture exercised arrows and bolts in each of the
four views and finished with zero mod copies after loading a Standard save.
It did not isolate occlusion by each of a wall, tower and gate. The sort key
uses the same per-row renderer principle as
[CastlePlanner](../../CastlePlanner/src/BlueprintRenderer.cs), with Vanilla's
projectile offset within the row.

The synthetic in-game fixture drew 50,000 copies in 96 groups and uploaded
exactly 1,200,000 echo bytes with the sprite cache already warm. Separately,
each timed run spawned 5,000 European archers and used their IDs to launch
two controlled 5,000-arrow volleys. This drives the native projectile
creation path without synchronizing the attack animations. Tests used the
same fixture, 1280×720, North, zoom 0.256, speed 40, no camera drift, and
Direct3D 11 on a GeForce RTX 4090. The game peaked at 5,607–5,608 active
arrows and 49,997 submitted afterimages with the renderer enabled.

| Pair | Extreme drawing | Peak mod copies | Median frame | p95 frame | Mean main thread | Mean draw calls |
|---|---|---:|---:|---:|---:|---:|
| 1 | Vanilla, suppressed trails | 0 | 7.038 ms | 17.928 ms | 9.885 ms | 689.907 |
| 1 | GPU trails | 49,997 | 21.713 ms | 51.001 ms | 23.402 ms | 841.928 |
| 2 | Vanilla, suppressed trails | 0 | 6.898 ms | 18.991 ms | 9.748 ms | 688.476 |
| 2 | GPU trails | 49,997 | 20.620 ms | 48.053 ms | 23.933 ms | 850.957 |

The tests measure the entire scene, including the native capture, Unity CPU
work and GPU submission. The game's frame timing API reported no GPU time.
The warmed offline history test reports zero managed allocation bytes.
Mono's allocation counter failed the positive control in game, so the
managed allocation rate for the complete warmed Unity path is unverified.
A 50,000-copy capacity pass does not establish a smooth frame rate at that
load or at 10,000 archers.

The acceptance logs and screenshots are in `.work/a/trail-gpu/001`; the
four mass runs in table order are `bc02f91f92c2`, `7328112fb13d`,
`178804cd848b` and `5a0ad54d3c78`.

### Vanilla comparison and equal-budget renderer check

Four recent runs repeat the 2,999-unit fixture with 2,991 spawned archers,
two 2,900-arrow volleys and identical camera and speed settings. The
1,024-copy / 64-group test ceiling was applied to the GPU build solely
for comparison with 0.1.3. Its sample selection need not match 0.1.3
exactly. Standard enables the original native afterimages and has a
smaller native projectile pool than Extreme Troops.

| Mode | Peak native afterimages | Peak displayed mod copies | Median frame | p95 frame | Mean draw calls |
|---|---:|---:|---:|---:|---:|
| Standard, Vanilla drawing | 270 | 0 | 4.587 ms | 11.728 ms | 560.013 |
| Extreme, Vanilla drawing | 0 | 0 | 4.570 ms | 11.444 ms | 562.728 |
| Extreme, 0.1.3 CPU renderer | 0 | 1,024 | 5.503 ms | 12.837 ms | 669.498 |
| Extreme, GPU renderer with test ceiling | 0 | 1,024 | 5.241 ms | 12.213 ms | 659.774 |

Run directories in table order are `a3a7465e500d`, `d9bc34b9b97e`,
`febdd10dfcb3` and `a229bb4b10f5`. The small improvement at equal
budgets is a single paired observation. The installed 0.2.0 package uses
the full renderer; SHA-256 verification of the DLL, manifest and shader
is recorded at `.work/a/trail-install/003/installation.json`.

## Local display settings (0.3.0)

The Modsettings page stores the activation switch, trail length (0–10),
and total visible-copy budget (0–60,000 in steps of 500) as local preset
values. They are sampled once per mission. The defaults are 10 and 60,000.
A zero value for either setting disables capture and drawing. Once the
budget is exhausted, the visible renderer keeps more recent samples,
preserving the original chronological blend order within each draw group.

A single afterimage uses 68.75% alpha. Lengths 2–9 fall off quadratically
from 100% at the head to 34.375% at the tail. Length 10 retains the original
Vanilla opacity steps. The GPU capacity scenario confirmed that the
Extender loaded the new Noesis page and the renderer reduced 50,000 visible
copies to 7,000, selecting the newest positions, and then to zero. A
second run on test_green retained ten copies for the default. See
`.work/a/trail-settings/001/9ce808ff08c5` and
`.work/a/trail-settings/001/022f3ca32e78`. The same directory contains
the installation manifest with all 25 verified hashes.

A Windows Direct3D 11 Player built with Unity 2021.3.45f2 used the
0.3.0 bundle and passed 121 pixel readbacks across settings 0–10. With
five copies the alpha bytes were 255, 245, 213, 161 and 88. The full
ten-copy setting returned the original three opacity levels and all
expired positions were transparent. See
`.work/a/trail-settings/001/Fade-player.log`.

## Handoff to a main mod

- Port the native capture (`NativeFlightSamples`), expiry history
  (`TrailBuffer`), GPU renderer (`TrailMeshes`), and the initialisation,
  mission observer and before-render adapter from `Plugin.cs`. The native
  observer takes the receiving mod's GUID for exclusive site ownership.
  Use exactly one active owner for RVA `0x9BFC0` and one owner of the
  shader clock. Remove or disable the standalone testmod when integrating.
- Keep runtime local. Map the three display preferences to the receiving
  mod's local preset fields and snapshot them at mission start. Keep
  Standard Troops on its native path. The installation must still ship the
  shader bundle. Pass its installed path explicitly to `TrailMeshes`;
  the receiving mod's assembly name and package layout are independent.
- Preserve APIShared's popularity patch on `GameMap.addUpdateFly`; the
  afterimage adapter is a postfix. Keep the native hash, transaction and
  live-span recheck. In a main mod with other watched sites, use the guard's
  per-feature CaptureIntegrity result to gate this observer. The aggregate
  Recheck count is only a log notification count. Unity work remains on the APIShared render path after
  the Extender's startup cleanup. On mission end hide all render groups and
  stop capture. Keep the native callback and its ring rooted for process life.
- Use the existing bundle build and checked-in shader asset GUID. Existing
  DevDiag fixtures are a standalone acceptance harness. The normal binary
  compiles those fixtures out. Run the fixed-arrow and capacity checks when
  replacing the graphics path. Repeat the matched load tests if changing
  the default budget or the grouping strategy.

### Tower occlusion

On `test_green`, the Direct3D 11 hidden-desktop fixture placed and verified
a native Tower1 building at (388,380), then paused the map facing North.
It repeated the draw with identical arrow samples and identical camera
and shader state while changing only the sort order to a row on either side
of the tower. In the tower-overlap mask, 8,966 of 9,064 pixels visibly changed
by the front overlay stayed at the tower's original value within tolerance
when the same overlay was drawn behind it. 450 changed pixels remained
visible beside the tower. The mask uses the picture of the same ground before
placement. See `.work/a/trail-final/001/tower-depth.png` and
`tower-depth.json` beside it. This isolates renderer layer order from native
collision, projectile trajectories and other camera rotations.

### Renderer finalisation (0.3.1)

Projection reuses the tile from the visibility check for Vanilla's clamped
sort row. Group bounds are accumulated as extrema and assigned only when
changed. Each group retains its last shader-buffer revision and draw
range. A rebinding is required after buffer growth, a different sprite
basis or lifetime, or a changed slice of the echo buffer. Hidden groups
are checked on reactivation. The capacity fixture verifies zero repeated
bindings on an unchanged redraw, rebinding of all 96 groups for a changed
lifetime, and a changed basis taken while the renderer is hidden.

The same 5,000-archer scenario alternated 0.3.0 and 0.3.1 twice. Each
run peaked at 49,997 submitted copies with the camera fixed. Timings
include the complete game frame.

| Pair | Mod | Median frame | p95 frame | Mean main thread |
|---|---|---:|---:|---:|
| 1 | 0.3.0 | 19.943 ms | 47.893 ms | 23.500 ms |
| 1 | 0.3.1 | 17.354 ms | 37.563 ms | 19.028 ms |
| 2 | 0.3.0 | 18.516 ms | 50.849 ms | 23.347 ms |
| 2 | 0.3.1 | 16.907 ms | 36.996 ms | 18.142 ms |

The alternating runs are `886897587634`, `0338129ba0f8`,
`00b5e9335a26` and `da52549e7407` under `.work/a/trail-final/001`.
The remaining heavy-load costs include projecting new observations,
regrouping and uploading changed visible copies and alpha blending.
The global visible-copy setting remains the direct way to bound draw cost.

The paired final visual check matched 15 Vanilla emissions and all 36 checked
opacity samples. See the final run directory's green-parity.json and the
0.3.1 installation manifest with all 25 package hashes.

## Standard mode replacement (0.4.0)

The Host option **Replace Vanilla afterimages in Standard mode** defaults
to enabled. It suppresses type-6 allocations from the arrow, crossbow bolt
and ordinary Attack Here source branches captured by the renderer. The
type-5 smoke producer and other source types retain their native handling.
The entry stub returns zero before the original prologue or allocation,
after restoring the caller stack and all scratch state. See
[Native update notes](../../BugfixesAndQoL/UpdateToNewDLL.md).

The Host preference follows the shared preset, lobby and save settings
paths. The package uses `NetworkMode=1`. In multiplayer the optional
simulation capability requires the enabled setting and intact hook on all
peers. The agreed decision arms the native gate before the map loads and
retirement clears it. Draw activation, duration and visible-copy budget
remain personal. A player can set the number to zero while the selected
native afterimages stay suppressed for everyone. A local draw error does
not change an admitted native multiplayer rule.

The single-player A/B cases use `trails-green-standard`,
`trails-green-standard-replaced` and the Extreme control on `test_green`.
They count occupied native arrow afterimage slots under the engine monitor,
assert that the replacement draws when requested, and check a second real
shot with local rendering off. Test runs temporarily change the registered
settings and let the runner restore the preset store. Two-peer multiplayer
acceptance is separate from the shared host/client preset suite.

All three cases passed using the installed APIShared 0.67.0 with Steam
running on the hidden desktop. With identical ten-copy graphics settings,
the two Standard runs matched 15 sprite emissions and all 36 sampled
opacities. The host setting enabled and disabled the replacement in the
two cases. With replacement enabled, native arrow afterimage occupancy
was zero both with local drawing enabled and after local drawing was
disabled before another shot. Evidence is under
`.work/a/trail-standard/001` (runs `6157b708b713`, `8e1f7c2918aa`, and
Extreme control `8436ca32a3a3`, plus `standard-parity.json`).
