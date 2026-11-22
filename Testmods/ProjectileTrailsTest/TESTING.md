# Extreme Troops projectile trails

`build.bat /nopause` builds the ordinary test plugin in `BepInEx/plugins/ProjectileTrailsTest_Serp` under this mod directory and runs the offline tests. The local BepInEx setting `[Display] Enabled` defaults to true. Extreme Troops supplies the session gate.

Use `build.bat /nopause /devdiag` for the test runner. The diagnostic package is in `bin/devdiag/ProjectileTrailsTest_Serp`. Run it with the shared `Invoke-InGameTest.ps1 -HiddenDesktop` runner, an allocated test output directory and the matching APIShared plugin/patcher pair. `SERP_TRAIL_SHOTS` can point to the evidence directory for game screenshots.

| Owned scenario | Fixture and acceptance |
|---|---|
| `trails-smoke` | Load `test_stresstest5.sav`, emit real arrow and bolt volleys near a gate, record four camera views and a paused view, then switch to `test_gates_vanilla.sav` in the same process and check that the trails clear. |
| `trails-standard` | Load `test_gates_vanilla.sav` in a fresh process and confirm the Standard Troops gate while firing a real volley. |
| `trails-archers` | Load the less crowded Extreme save `test_dustynoattack2.sav`. Spawn a local group of archers and crossbowmen, order Attack Here, verify projectile source IDs and capture the same paused scene with the local trails enabled and disabled. |
| `trails-stress-off` and `trails-stress-on` | Load `test_stresstest5.sav` with the same zoom, resolution, camera and speed. After a warm-up volley, sample five measured volleys per process. Alternate off/on twice. Read frame p50/p95/p99, accepted shot count, peak rendered segments, Draw Calls and GC bytes from `TRAIL_PERF`. |

Review actual captures for foreground and background shots at a wall, tower and gate. A positive segment count verifies submission. Occlusion and visibility require inspecting the captures. Draw Calls and GC bytes in `TRAIL_PERF` measure the whole Unity frame. The `projectileTrail.meshUploadAllocatedBytes` diagnostic measures the trail renderer separately when the installed Mono allocation counter is available. Newly created or enlarged mesh batches can allocate during warm-up.

## Desktop test on 2026-11-22

Hidden desktop, Script Extender 2.14.4, APIShared 0.63.0, installed Fixes, 1280×720. The smoke test submitted 41, 96, 192 and 230 local segments in North, East, South and West. Segments persisted during native pause and cleared after switching in the same process to Standard Troops. The second save admitted 48 arrows and 48 bolts and submitted zero custom segments.

The archer case spawned and assigned 24 archers and eight crossbowmen. Vanilla accepted Attack Here. The projectile source check matched 24 of 25 rendered segments to the spawned group before the paired paused screenshots. Views around the terrain and fortifications were inspected. The captures do not isolate overlaps at each wall, tower and gate, so individual occlusion cases remain unverified.

| Stress run | Peak segments | Frame p50 | Frame p95 | Frame p99 | Mean draw calls |
|---|---:|---:|---:|---:|---:|
| Off 1 | 0 | 3.994 ms | 8.471 ms | 10.538 ms | 422.423 |
| On 1 | 1,024 | 4.037 ms | 9.016 ms | 14.151 ms | 477.631 |
| Off 2 | 0 | 3.714 ms | 7.757 ms | 11.811 ms | 421.794 |
| On 2 | 926 | 3.921 ms | 8.857 ms | 12.149 ms | 479.447 |

Each run admitted 800 arrows and 800 bolts at speed 40 and zoom 0.25 with zero measured camera drift. In-game GC byte accounting yielded no frame samples. Mono's per-thread API also failed a positive control with a known 4,096-byte allocation. The offline tests measured zero managed bytes for warmed history and budget operations. Unity mesh publication remains unmeasured for managed allocations in-game.

Local evidence is under `.work/a/projectile-trails/001`. The smoke log is in run `398c30059dda`, the archer test in `f585ad8e16f9` and the stress series in `56f24cd84d83`, `6cd106ec4d1e`, `91b264df4e16` and `cfb6473660e6`. Every completed live runner transaction restored and verified the original plugin and setting files.
