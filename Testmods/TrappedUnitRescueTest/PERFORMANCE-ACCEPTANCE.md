# Rescue performance acceptance plan

Status: source preparation, 26 November 2026. No measurements, FPS result or performance acceptance is claimed here. This plan supplements the singleplayer functional cases in INGAME-PENDING.md. The current target for later integration is BugfixesAndQoL; this document does not authorize installation or integration by itself.

## Purpose and architecture under test

Verify that a nearly full physical unit pool on the largest supported map does not cause continuous global rescue discovery. Automatic discovery must remain tied to the shared API world-mutation and final-UnitMove owners; rescue processing must retain its actual admission, queue and native-operation budgets. Measure ordinary idle/movement overhead separately from the deliberately changed behavior of successful rescue.

Current source evidence:

- `GeneralRescueObserver.Tick` returns when inactive or PendingCount is zero; it does not enumerate every unit or tile.
- `RescueRuntime.TryCaptureMutation` uses the actual footprint and occupancy chains through `RescueDiscovery`, including the one-cell ordinary-Next halo. The current invocation limits are 4096 visited cells, 16384 links and 512 units; footprint extraction has its independent 2048-cell/131072-read limits.
- The shared pending/geometry bank is bounded at 512. The recovery queue drains at most 32 visits and 8 attempts per simulation tick, and fresh replan joining has its separate 32-visit boundary. Eight attempts are NOT eight native calls: an owned functional recovery may use up to four native operations.
- Siege crew exclusivity can inspect the unit pool at admission. It is not a universal per-tick scan; include a separate siege stress sample if it appears as a measured hotspot.
- The legacy explicit `MutationScope` scans unit IDs for manual fixtures. Do not use it as the automatic performance source or claim its census measures automatic discovery.

Reconfirm these contracts from the source used for the run. A future limit change requires an updated expectation, not silently changed assertions.

## Deterministic world

Use the installed built-in file `Stronghold Crusader Definitive Edition_Data/StreamingAssets/Maps/Back to Back.map`. It exists in the local installation and is the map selected by `UiAcceptance/scenarios/SP-hunters-perf-800-500.json`; that existing stress fixture requires the loaded map size to equal 800. Record the actual selected full path and SHA-256. Do not treat the map filename or this historical scenario as proof of its current loaded dimension.

Require `GameTileManagerAPI.GetCurrentMapSize() == 800` after load. This API reads `GameGlobalsManager.CurrentMapSizeVA` and explicitly documents 400 for a 400-size map. The maximum 800-coordinate raster is an isometric diamond with a packed native capacity of 320800; it is NOT 640000 physical native tiles. Coordinates greater than 400 alone prove nothing about map size. Select sites using `IsTileInsideMapBounds`, `GetTileId`, actual ordinary-ground/height/component checks, and read back XY/packed identity. Never use `y*800+x` to index the packed unit/tile layers.

Use the existing deterministic StartSkirmish beforeLaunch callback and fixture preparation: fixed local/AI slots and start places, 60 minutes peace, known pinned Rat AIC/AIV and hashes, no prebuilt AI castle, random events disabled, and recorded settings for every loaded gameplay mod. Keep CastlePlanner enabled with SpawnCastle false to preserve its rotation compatibility setting. Verify actual world/lord/map/peace/settings before measuring.

Prepare one paused engine save for each population tier using normal engine saving. Reload that SAME save for every off/on member of its ABAB series. Record the save hash, world identity, camera/zoom, exact entity count and fixture identity checksum. Loading the same save preserves the initial simulation/RNG state; do not invent or write a random seed field. OFF/ON is set before loading, never changed within a measured mission. Use an empty causal archive in this common baseline and verify restored pending count is zero.

The same `.sav` also contains saved modsettings, including `EnableExperimentalRescue`. Merely setting OFF/ON in the frontend is insufficient if the following load restores those saved values. For EVERY ABAB load, first apply and read back all current frontend settings, then call the public `APIShared.SavegameModSettings.PrepareLoad(fullSavePath, true)` immediately before invoking/enumerating the ordinary save-load operation from an unbound frontend step. This call captures the current compatible host settings for exactly that selected save; make it after the intended values have settled and before `EditorDirector.loadSaveGame` begins, not after OnStart or on the first running tick. Do not rewrite the save or mutate settings during the mission. Verify the selected-source diagnostic reports requestedCurrent=true and effective current-host-settings, then read back the actual rescue setting/gate during OnStart before any measured simulation work. Creator/Trail rules remain authoritative; a different effective value is a failed precondition, not an A/B sample.

Source check on 26 November 2026: PerfProbe's standalone loader comments that dialog-less loading keeps current settings, but it does NOT itself explicitly call PrepareLoad. APIShared's default preparation can choose current settings when no matching preparation exists; a retained matching choice must not substitute for the explicit per-load selection above. The performance fixture owns this preparation while PerfProbe Automation.Enabled is false. No new persistent configuration or SDK change is required.

## Population and placement

Tiers: approximately 1000, 5000, and the highest reproducibly supported count near the physical pool limit. The public extender reserves 10000 physical slots; the rescue contract accepts IDs 1 through 9999. Existing lords, civilians, wildlife, NeedsInit and dead-but-not-released records consume capacity too. Count occupied physical records ONCE while paused before setup, reserve real actors plus a recorded headroom margin, and log both the requested and actual count. Never label a 9900-unit result as 10000 tested units.

Spawn deterministic row-major, distinct ordinary sites through existing `CreateUnitLocal` using the already audited ordinary Spearman family. Read back each actual ID/global/type/owner/position/health and initialization state. Use the fixture's shared tribe Create/Assign pattern with bounded group sizes and verify membership; do not call `SpawnSingle` thousands of times because it creates one tribe per actor. Respect actual type/population/tribe limits. A refused or projected spawn is a fixture precondition failure unless a smaller achieved tier was explicitly selected and recorded; do not patch population counters or silently count requests as units.

Setup/site enumeration is outside measured windows. During windows avoid full unit checksums/censuses, formatting per-unit diagnostics, or spawning more population. Boundary-only lifetime/occupancy checks and fixed actor samples suffice. New native field or spawn-policy assumptions require their normal feature audit before implementation.

## Workloads

For each tier and identical save, execute OFF, ON, OFF, ON with the same binary profile and settings except rescue activation. Use a fixed simulation-tick schedule, fixed zoom/camera, identical affinity if used, normal speed and a separately recorded high-speed 250 series. Exclude setup/loading/JIT/warmup. Target at least 3600 actual advancing simulation ticks per steady window; wall-clock waits are timeouts, not the denominator.

1. Idle, ordinary and unmutated: no source writes, no new orders. Expected capture/rescue/native-recovery counts are zero.
2. Ordinary walking: identical accepted short group routes, no terrain mutation. Verify native accepted orders and selected sample endpoints. Existing final-UnitMove observation is exercised without constructing false failures.
3. Supported empty geometry writes away from actors: repeated actual supported bridge or Moat source operations at predetermined ticks, with valid old/new geometry preconditions. This isolates source/batch overhead without recovery and must show zero affected actors.
4. Dense actual affected geometry: separate bounded groups of 32, 128 and up to 512 actors over recorded valid footprints, using the existing unsuppressed automatic Moat/bridge writers. Verify genuine changed trap witnesses and queue outcomes. Include an explicitly over-budget source whose expected outcome is fail-closed without an incomplete rescue proof. Do not fabricate candidate queues or replace native recovery with a fake benchmark.

OFF and ON recovery windows intentionally diverge after rescue. Their total tick difference is an end-to-end outcome, not proof of pure overhead. Attribute cost through direct measurement spans or one matched sampler pair if needed. Include query/budget exhaustion and graceful refusal in results, rather than accepting speed obtained by skipping functional work.

## Existing PerfProbe integration and new duration seam

Use the coordinator's transactional hidden desktop runner and receipt-based restore. Include the same `/devdiag` PerfProbe build in both variants. Do not run PerfProbe's independent launcher/quit controller: set Automation.Enabled false. AutomationOnly must be false, because true omits the timing hooks. Disable thread sampling for the primary low-overhead series. Enable Spikes.RunPhaseTiming and verify the published managed run IL contract is instrumented. No additional native detour is needed.

The existing `PerfProbeRunTiming.BeforeNative/AfterNative` seam measures the actual `DLL_RunTick` invocation, including extender/mod callbacks, with monotonic wall timestamps and simulation-thread CPU time. No-buffer `EngineInterface.run` calls and interrupted native calls are not completed samples. `run(true)` samples are retained but counted separately. A run invocation is NOT necessarily an advancing simulation tick; record actual tick progression, skipped calls and no-buffer counts separately. Existing `tickstats` is cadence/throughput, not native duration.

The new diagnostic-only type is `PerfProbe.PerfProbeNativeDurations` in PerfProbe.dll:

- `public static bool Begin(string label)` starts one explicit named window only if existing run-phase instrumentation is available. Labels contain 1..64 letters/digits/hyphen/underscore. Nested begin requests fail.
- `public static string End(string label)` closes only that exact window, returns and logs its summary through Shared.DevDiag; mismatch/inactive returns null. A foreign label cannot close someone else's measurement.
- Both methods exist only in `/devdiag` builds. The rescue fixture can reflect and validate their exact signatures once during preparation; no hard production dependency is introduced.
- Call Begin/End while paused under the normal engine lock BETWEEN native invocations. A call straddling a window boundary is intentionally excluded; tick-event begin/end inside an already running native call is not an exact whole-tick window. No buffer is allocated per sample. A closed captured window cannot publish into a later window.
- The summary contains completed count, skip count, wall sum/mean/max, CPU available count/sum/mean, strict durations greater than 50/100ms, and p50/p95/p99 histogram upper bounds. Fixed 0.25ms buckets cover durations below 1024ms, with explicit >=1024ms overflow; an overflow quantile is infinity. These are approximate upper bounds, not exact percentiles. No advancing-tick count is inferred from sample count.

Root must still wire the reflection/window calls and actual tick-count boundary checks into the rescue performance fixture. The helper tests compile the production histogram and managed timing seam through existing `PerfProbe/tests/Test-DiagnosticHelpers.ps1`; they do not execute the native game or establish performance.

## Minimal additional development measurements

If direct rescue attribution is required, use Shared.DevDiag and `/devdiag` only around existing source capture, outer batch, replan and recovery boundaries. Aggregate source count, visited cells/links, snapshots, admissions, queue peak, visits, attempts and actual native-operation count. Emit summaries after the window. Do not add callbacks on every unit/tile or permanent runtime profiling. Window/reset/export hooks must remain test-owned or diagnostic-only and preserve original result/exception handling.

Use PerfProbe GC/frame/cadence/native-counter output as supporting evidence. Actual native-inclusive duration and thread CPU are primary for simulation work. Frame rate alone can be rendering-bound; Unity WaitForTicks wall time includes scheduling and throttling. Sampler stacks and equal instrumentation are necessary before attributing a spike to a mod. PerfProbe's existing aggregate whole-run time includes buffer/lock/copy/publication work; keep it separate from native-inclusive duration.

## Acceptance record

For each run preserve compact metadata, full startup/run identity, map/save/DLL hashes, settings, population and capacity census, fixed window/tick boundaries, summaries and functional outcome. Report paired medians/ranges and tail bounds, not only the best run. Reject disturbed or uninstrumented runs with their reason. Keep existing health/identity/order/crew/lifecycle tests separate; performance is not permission to weaken them.

Acceptance requires no continuous all-unit/all-map scan in the measured rescue path, the verified work limits being respected, no incomplete proof or ownership loss at saturation, no rescue-generated unexplained long-tail spikes, and a quantified idle/walking overhead across paired runs. A numerical overhead threshold must be recorded before final acceptance based on the measured baseline and user priorities; this plan does not invent a passing FPS number. Existing source-only tests establish bounded algorithm contracts, not real-time throughput on an 800 map.

Helper regression evidence: Root executed the existing complete in-memory helper harness successfully, including NativeDurationTests (25 checks). This is helper correctness evidence, not game performance.

Remaining work: build matching diagnostic artifacts; add the bounded test fixture and transactional profile metadata; execute the singleplayer series; inspect outliers; record measured acceptance or specific remaining failures. No installation/game run was performed while preparing this document or duration source.
