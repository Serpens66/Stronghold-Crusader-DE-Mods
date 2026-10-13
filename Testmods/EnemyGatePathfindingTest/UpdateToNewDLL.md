# Native update contract: EnemyGatePathfindingTest

## Capturer comparison adapters

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

The PCL graph adapter owns RVA `0xE2705..0xE2718` inside `0xE2610`. The builder-precheck adapter owns RVA `0xE3024..0xE3037` inside `0xE2F60`. Both spans are exactly 20 bytes: `MOVSXD RCX,[R9-0xC]`, `IMUL RDX,RCX,0x32C`, and the capture-word `CMP`. The first comparison uses the module base in RAX and compares with zero; the second uses R13 and compares with AX. Query player is R14D / EBP respectively. No incoming branch enters either span's interior. Original continuations are `JE 0xE272C` at `0xE2719` and `JE 0xE3047` at `0xE3038`; owner/alliance acceptance bypasses the hooks through `0xE271B` / `0xE303A`.

Resolution validates the reference bytes and then the module-bounded unique patterns declared in `EnemyGatePathfindingNativeDefinition`. Both resolved sites must still match their audited RVAs because the layout is hash-bound. Failed resolution, displacement or assembly validation aborts the unpublished hook transaction. Published adapters remain installed until process exit; map changes publish only empty/active policy snapshots. BugfixesAndQoL owns the `0xE2610` function detour; these interior sites do not overlap its prolog. Installed Script Extender and local Fixes must be checked for collisions on every update.

The capture table is module RVA `0x64CCED2`, indexed by one-based building ID times `0x32C`, and read as `ushort`. R9 addresses the current connection record's enabled field at structure offset `0x18`. Record stride is `0x204`; building ID / subject global ID / owner are R9-relative `-0xC` / `-0x4` / `+0x1CC`. Component A/B/C are `+0x1C` / `+0x20` / `+0x1D0` (structure offsets `0x34` / `0x38` / `0x1E8`). The prior negative PCL offsets read the predecessor and must not be reused. Field offsets have no cross-build semantic fallback; unknown hashes disable this native feature.

RedBird.X64 `1.5.0.0` and installed Script Extender `2.12.0.0` were checked on 2026-10-01. RedBird's stock ContextAssemblyGenerator ends with `ADD RSP,144` after POPFQ; writing Rflags therefore cannot drive a subsequent JE. Our inline emitter performs the original comparison once, saves real R11, seeds R11b with SETNE, preserves all GPRs and volatile XMM0..5 across the IntPtr callback, then performs TEST R11b after callback cleanup followed by flag-neutral POP R11. Only ZF is live at the native successor; subsequent INC/ADD replace other arithmetic flags. No original SIMD value is used across these native blocks; SIMD preservation additionally protects the managed callback boundary.

The custom context uses X64SmartCPUContext field offsets `0..128` in eight-byte increments and a 144-byte context area. Machine tests verify installed metadata, Win64 shadow space/alignment, actual RedBird displacement, both real branch outcomes, GPR/XMM preservation and stack balance. Re-run these tests on every RedBird update. Runtime callbacks and their function pointers are rooted by the static plugin runtime; no new Assembly-CSharp member access or APIShared interface is introduced.

Other native targets and existing policies remain declared in `EnemyGatePathfindingNativeDefinition` and documented in `PROJECT_FINDINGS.md`. This change introduces no additional native target.


## Building query-player contract audit (2026-10-02)

No new runtime address or hook is introduced. The complete static contract is
recorded in the semantic baseline knowledge/ENEMY_GATE_BUILDING_CONTEXT.md.
For the reference hash above, DA020's sixth argument is the selected opponent
in 30620 (call 30A33), but the signed leader control WORD in 11E960 commands
9/38 (call 11FF9A), or zero for command 36. The planner field is tribe-relative
0x620, not the named AttackTargetOwnerPlayerId field at 0x61C. The native
control read at unit-relative 0x92 spans two installed Interop BYTE fields.
A future implementation must preserve this width and distinguish Vanilla's
query player from the moving actor. Same-PCL and E2CA0 alternative/cache paths
must be audited too. Reference-specific machine checks and installed-layout
checks are retained in _inspect/EnemyGateBuildingContextAudit. Unknown builds
must not reuse these field/call-site claims as runtime adapters.


## Building movement scope implementation (2026-10-02)

No additional native hook or public interface. The common resolver captures
GameTribe.r_GlobalId/r_PlayerIdOwner/r_LeaderUnitId/N00000580 and
GameUnit.r_GlobalId/r_ControllableForPlayerId/N00000569 through the installed
public Interop and public TryGetTribeById/TryGetUnitById wrappers. The complete
installed member list is machine checked by installed-layout.ps1. N00000580
uses its low signed WORD at +0x620; the control WORD combines bytes +0x92/+0x93.
Mission lifecycle supplies editor status for the planner's normal-game fallback.
The snapshot validates tribe global identity and owner; inconsistent/unknown
contexts stay open. Original DA020 arguments are unchanged. Re-audit names,
widths, layout, native writers and caller roles after Extender/native updates.


## Diagnosebereinigung und Spielbefunde, 02.10.2026

Die neue GebÃƒÂ¤ude-Scope-Fassung wurde in drei aktiven test_gates.sav-Epochen
beobachtet: 13:35:51.837Ã¢â‚¬â€œ13:36:47.889 (KI-ZÃƒÂ¤hler 11100, Rollenunterschiede 13),
13:36:51.958Ã¢â‚¬â€œ13:37:56.844 (14151 / 14), 13:39:09.823Ã¢â‚¬â€œ13:41:29.280 (8888 / 30).
Alle beobachteten Builder-Ergebnisse waren positiv, kein NoRoute, keine
GebÃƒÂ¤ude-Kontext-/Scope-/Snapshotfehler oder Exceptions; runtimeIntegrity=PASS.
Im zweiten Lauf enthielten die Ergebnisaggregate 14157 positive Beobachtungen,
also sechs mehr als der KI-EintrittszÃƒÂ¤hler. Das ist eine historische ZÃƒÂ¤hldifferenz,
kein nachtrÃƒÂ¤glich aufgeklÃƒÂ¤rter Einzelfall. Native Entry/Exit und Ergebnisobserver
verwendeten unterschiedliche KI-Abfragen; kÃƒÂ¼nftig bleibt die Entryklassifizierung
im QueryScope eingefroren und gilt fÃƒÂ¼r alle drei Stellen. Originalargumente,
Suchergebnisse, Scopemasken und Hookadressen bleiben unverÃƒÂ¤ndert.

Gemeinsame Suchaufrufe zÃƒÂ¤hlen Attack, BuildingApproach, BuildingConsumer,
CursorCommand und AI Builder jeweils einmal. BuildingConsumer bleibt im
KantenzÃƒÂ¤hler dieselbe Suchklasse wie bisher, erhÃƒÂ¤lt aber seinen eigenen
AufrufzÃƒÂ¤hler. Standalone CursorCommand erhÃƒÂ¤lt ebenfalls einen AufrufzÃƒÂ¤hler.

Gate-live-ZustÃƒÂ¤nde werden ohne Obergrenze nach Spieler, Building-ID, vollstÃƒÂ¤ndigem
Zustand und vollstÃƒÂ¤ndigem Detail intern gespeichert. Global-ID, Owner-/Capture-
Relation, Rohwerte, Policy, Ursachen und BrÃƒÂ¼cken stehen in einer Definition
`Enemy-gate state: epoch=N,id=M,...`; Ergebniszeilen referenzieren `gateState=N/M`.
Definitionen erscheinen vor ihrer ersten Verwendung; Definitionen und aktive
Aggregate werden atomar ausgelesen. Identische ZustÃƒÂ¤nde behalten ihre Kennung ÃƒÂ¼ber
Zehn-Sekunden-Fenster, Ãƒâ€žnderungen erhalten eine andere Kennung. Jede Observation,
erster/letzter Tribe-/Zielwert und bestehende WechselzÃƒÂ¤hler bleiben erhalten.
Reset erfolgt nur beim bestehenden Kartenstart; Epoche wird erhÃƒÂ¶ht. Kein Unittracking.

Installierter SE 2.12.0, Tag v2.12.0, Commit f8d51730fcb54b25af43d3c9348d57db058e077f,
Tree 657af449e1397c58e6c5ec054977d83198b68e66 und Assemblyhash
DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF sind auditiert.
Die MindestabhÃƒÂ¤ngigkeit bleibt unverÃƒÂ¤ndert; der Log-Versionsmatch ist weiterhin
ein Versionsvergleich und beweist keinen Laufzeit-Hashmatch.

Vanilla FBCB9319 liest Connection-Permissions in 0x181E00 mit 90 Int32 pro Zeile
(IMUL 0x5A, Zeilenabstand 0x168), Klassen 1Ã¢â‚¬â€œ6 ab 0x32BDB0. Der aktuelle ÃƒÂ¶ffentliche
API-Konstantwert ist 89. Der flache Getter liefert 534 statt 540 EintrÃƒÂ¤ge;
unser Vergleich interpretiert den vorhandenen Prefix weiterhin korrekt mit
Native-Stride 90. Es fehlen Profileindex 89 und Permissionindizes 534Ã¢â‚¬â€œ539
(Klasse 6, Unitindices 84Ã¢â‚¬â€œ89). Alle gelesenen Werte der letzten LÃƒÂ¤ufe stimmten.
Die Warnung bezeichnet jetzt unvollstÃƒÂ¤ndige Abdeckung, keine Tabellenmutation;
Changed-Value-Warnungen bleiben getrennt. Der ÃƒÂ¶ffentliche Row-Getter verwendet
jedoch Stride 89 und adressiert Klassen 2Ã¢â‚¬â€œ6 falsch; dies betrifft auch seinen
Can/Set-Verbraucher. Unsere Policy verwendet diese Getter/Setter nicht. Fork und
Hauptmods wurden fÃƒÂ¼r diesen Schritt nicht geÃƒÂ¤ndert. VollstÃƒÂ¤ndige Native-Reader-
Belege: _inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt.
Der englische Autorenreport liegt daneben als SCRIPT_EXTENDER_PATHFINDING_REPORT.md.

Logabnahmegrenzen: Alle Pre/Post-Paare und Aggregatsummen stimmten. Der dritte
Lauf beobachtete 12 gefilterte GebÃƒÂ¤udekanten. Eroberung, nachgewiesene Cursorsperre
und Raid-GebÃƒÂ¤udebefehl 9 wurden in diesen drei LÃƒÂ¤ufen nicht beobachtet. Die
Spielbeobachtungen dieser Epochen sind noch nicht zugeordnet. Ein spÃƒÂ¤terer Lauf
ohne Testmod ist ein eigener Hauptmodvergleich, kein Gatepolicy-Nachweis.
Offen: normaler/Raid-GebÃƒÂ¤udeangriff, Eroberung/RÃƒÂ¼ckeroberung, eigener Zugang nach
gesperrtem Cursorziel, mehrere Wellen und beobachteter Hauptmodvergleich. Keine
weitergehende KI-VerhaltensÃƒÂ¤nderung ist aus diesen Logs gerechtfertigt.

Buildabnahme der Diagnosebereinigung: build.bat hat am 02.10.2026 die Fassung
mit 2847 bestandenen Assertions und null Warnungen/Fehlern gebaut und installiert.
Beide installierten RedBird-Capturer-Maschinentests bestanden. Die neuen Tests
prÃƒÂ¼fen 1500 Beobachtungen ÃƒÂ¼ber 100 vollstÃƒÂ¤ndige TorzustÃƒÂ¤nde und drei Zeitfenster,
Rekonstruktion, Epochenreset, eingefrorene KI-Zuordnung und unvollstÃƒÂ¤ndige Tabellen.
Ein erster Testlauf stoppte vor der Installation wegen einer unbeabsichtigten
Ãƒâ€žnderung am direkten Cursorwrapper; dieser Pfad wurde vollstÃƒÂ¤ndig wiederhergestellt.
Danach bestanden Tests und sÃƒÂ¤mtliche VorprÃƒÂ¼fungen erneut vor dem finalen Treiberlauf.
Lokale/installierte Testmod-DLL SHA-256:
B85CFC510B7912A4D1DF7D6F81ADB7EFB773FBE60B2F41E25853648CF6358C10.
BugfixesAndQoL und APIShared blieben gegenÃƒÂ¼ber dem Stand vor diesem Build identisch
(00A0512B1F94C598B5417B6B5790A396E7B67228B27DEE2AB81E4BA466552CF7 /
02332AF3506C92C47DB066C1C2AC764A312D75E2CAFC144F1FB3B5E27617C6FF).
Version unverÃƒÂ¤ndert 0.1.5, README unverÃƒÂ¤ndert. Neue Spielabnahme der Ausgabe steht aus.

## 2026-10-02: read-only Assassin gate diagnosis

See the hash-bound baseline `knowledge/ENEMY_GATE_ASSASSINS.md`. Four pre-change runs had 403/402/207/624 NoRoute results; the first three observed open/open, open/closed and closed/closed configurations. Automatic closure in the open runs led to retargeting; no persistent stuck-unit conclusion follows from the counters.

An optional passive APIShared observer measures the existing D9C40 detour, weighted/cache-route edges and native cache contents; it never changes search results. Existing deferred aggregates retain exact counts and numeric sums with no event cap. Gate edge IDs come from mask construction, overlapping identities remain ambiguous. New Unit fields were checked against installed public SE members, and the complete control WORD is reported. Native singleton reads use +0x84/+0x88/+0x90 and ten positive/negative PCL pairs at +0x416D8C/+0x416DDC with stride 8. No new hooks or executable mutations; original route/search/cache algorithms remain unchanged. Native-only flood fields and cache-hit/fallback measurement gaps are explicitly labeled. Versions and README are unchanged.

Abnahme remains open until paired Assassin runs locate the first different reachability result. No additional Assassin movement correction is included in this diagnostic build.
# Weighted Assassin policy integration (2026-10-02)

No new native targets or hook spans. The optional route snapshot provider follows the existing mask publication and QueryScope lifetime; explicit unmasked scopes retain unmasked semantics. Revalidate native/control-word provenance, nested query restoration and snapshot/cache invalidation on updates. All behavior after an uncertain context retains the already computed native result.

## Lateral bridge policy contract (2026-10-02)

Revalidate the complete creation/animation/raising/lowering/footprint-refresh path and the 25-cell native mapper at 2D1A30. Raising 645C0 uses orientation/2, a nonzero mapper cell and a nonzero moat-record index at tile-manager +1EA23F0 before setting 0x40000000. This produces 15 closure cells in the confirmed build. GameBuilding public fields are occupied-array begin +1C8 (UInt32), grid size +F8 (UInt32), sprite variation/orientation +102 (UInt16); GetMoatWorkTaskIndexLayer is a public parameterless Span<UInt16> API in the installed assembly. Do not substitute a bounding rectangle or gate axis. The new policy has no new native hooks; existing span/owner and permanent-publication contracts remain in force. Equal-PCL planning acceptance remains a separate game regression.

## 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.

## 2026-10-13: large gatehouse footprint correction (game acceptance pending)

The 17:08:53-17:12:00 editor evidence diagnosed small gate1 (25 cells,60 restrictions) versus large gate2/global3 (door-only fallback,zero restrictions). Technical integrity did not establish mask coverage. This is a confirmed missing ground mask, not an Assassin-specific verdict. Weighted search was active; this run does not complete the earlier native-only Assassin check.

Native basis remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Full60 search/climb bodies,16,960 instruction ranges and21 planning/output bodies revalidated. Large-gate construction0x74240 writes Building-ID raster for the full footprint but writes inline cells only for index<36 (CMP at0x7431C).0x69850 uses mutable construction scratch and is not invoked by this reader. The existing inline capacity remains36; grid7 is accepted only for STRUCT_GATE_MAIN by a separate raster reconstruction.

ConnectedGateFootprint reconstructs exactly49 connected cells in a7x7 bounding square using at most36 inline seeds and four-neighbor Building-ID lookups. Foreign neighbors are excluded; larger, incomplete or inconsistent components fail open. Cell identity is re-read and Building/Global-ID, type,alive,owner,capturer,grid and origin are checked before use. Signature and snapshot share this reader. Coordinates use the installed public IsTileInsideMapBounds/GetTileId/GetTileVectorFromId/GetTileBuildingId contracts; the in-memory compiler used installed assemblies, including actual Assembly-CSharp. No construction scratch, map tile, gate state or route query is changed.

Door fallback remains diagnostic and is no longer marked Footprint=true. Production GatePassageMask is now linked into tests; existing axial/diagonal restriction semantics are retained. Both complete boundaries must validate before any byte changes. The failing logged coordinates (411/355 and411/347, footprint408..414/348..354) now produce84 directed restrictions in the fixture. Small5x5 gates retain60. Mask rules, permissions and the existing Assassin transition validator are unchanged.

Coverage is separate from runtime integrity: requiredEdgePolicy expresses the permission rule; maskCoverage states present/missing/identity-unknown. Every epoch ends with current missing-required gate IDs and peak missing count. Failed footprint-only gate records remain visible. A zero missing count only describes tracked gate geometry, not a full behavioral acceptance.

Regression suite:67,263 assertions including production mask generation, all four entry/exit orientations, diagonals, foreign neighbors, incomplete/oversized geometry, stable ordering, grid changes during reread, slot reuse, capture/demolition/type changes and all-or-nothing map-boundary validation. Existing own/allied/captured, cursor, Assassin, native adapter and lifecycle tests remain passing. Synthetic native data is not a new game result. Compilation, shared boundaries, installed layouts/hooks, JSON/lifecycle/XAML/CRLF and permanent hook checks pass. No APIShared/Bugfixes source changes, new hooks or API contracts; versions,minimum dependencies,README and saves unchanged.

Next: TARGETED_TESTS.md large-gate regression. Stored weighted Assassin search=true,direct gatehouse climb=true; DetailedDiagnostics=true,MacemenDiagnostics=false. Check effective options after loading presets. Mainmod integration remains withheld until the large-gate in-game result is confirmed; historical unknown native Assassin routes remain unknown.

## 2026-10-13: both gate sizes accepted in editor; native-only Assassin comparison next

Evidence: LogOutput.log SHA256 edbcd431de8816489afb0ec86e691a352472db40f4c11a1afd723150a32390ed, editor epoch1 17:41:43.984--17:43:30.939. Gate1/global1 has60 directed restrictions; gate2/global3 has84. Current and peak missingRequiredMasks=0; ambiguousPassages=0. User explicitly confirms both gates behaved as desired. The large-gate correction is therefore an observed passed game regression; no repeat of that weighted-mode test is requested.

16 route observations comprise6 weighted calculations and10 published outputs, not16 independent orders. No confirmed violations or unclear inspected routes;8 permitted-climb observations also overlap stages. Four capture transitions and two recapture transitions recorded; permitted comparisons preserved426 times. Paired events: tribe movement12/12, unit movement16/16. No Error/Fatal lines, scope, exception, snapshot or adapter errors. Zero AI queries: this is editor/human coverage, not a new AI match.719 unit-buffer-mismatch publications and314 unattributed searches remain observation gaps, not failed routes.

Loaded APIShared0.8.0, BugfixesAndQoL1.0.187, Fixes1.26.4 and Gate0.1.6. Weighted Assassin search and direct climb were both enabled; EnemyBridgePathTest was not loaded. This run does not close native-only Assassin or historical secondary-ground +0x94 output questions. Full60 search/climb bodies,16,960 instruction ranges and21 planning/output bodies revalidated against installed native hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

With game stopped, prepared next test: EnableImprovedAssassinPathfinding=false, EnableAssassinGatehouseClimbFix=true, DetailedDiagnostics=true, MacemenDiagnostics=false. Parsed102 local MessagePack fields and changed exactly one boolean byte; all other bytes preserved. Settings SHA256 after=979421d0f3e6679d59d3e4ff17c10a85be0d8a7d1f48b98870ff12861e70c75f. Bridge remains off as in accepted run; presets/lobby overrides must be checked after loading. No runtime code, binaries, public API, version, README or save changed; no build required.

Only remaining requested game comparison: one hostile Assassin, fresh ground order behind each open uncaptured gate, then reload initial state and repeat with gates closed. Keep direct climbing enabled. Observe first access before capture; subsequent permitted ground passage is not a violation. See TARGETED_TESTS.md. Successful Raid/quarry, real detour, recapture and both gate-size regressions remain accepted and are not requested again. Historical native outputs are not retroactively cleared. Integration remains withheld until native-output evidence is evaluated; +0x94 is only covered when specifically established.
## 2026-10-13: native test preparation corrected for authoritative schema3 working state

Editor17:58:05--17:59:10 again behaved correctly according to the user. Full LogOutput SHA256 at evaluation=54a6c702ae437b368f476256b42783dd988bd5f2a03c6ebfc1ae969ae39384cd (includes previous starts; latest process selected by BepInEx start marker). Both masks60/84 and current/peak missing masks0. No Error/Fatal, scope, exception or snapshot failures; paired group/unit events6/6 each. Six checked route observations, no confirmed violations; two additional native mask overlaps remain movement-unproven. Those two outputs have secondaryGroundFlag94=0/0, no observed secondary-ground pass. User observation supports correct behavior; it does not prove the branch of each historical route.

The intended native-only setting was NOT effective: weightedConfigured=True and two exact weighted publications. Mainmod was1.0.188, APIShared0.8.0, Fixes1.26.4, Gate0.1.6. The previous preparation helper only changed the outer compatibility field. APIShared's schema3 loader restores __SerpCurrentSettings, where weighted search remained true. The earlier claim that native-only preparation was complete is withdrawn. This is a preparation-tool defect, not a gatepolicy or APIShared loader defect.

The existing helper now validates schema3 and both nested/outer maps, changes only the two intended weighted boolean positions as needed, requires climbing=true at both levels, and rejects unsupported schemas, malformed/duplicate fields and non-boolean settings. The effective working state and all unrelated bytes are verified after preparation. Exclusive Windows file access (share mode0) holds the check/write/readback; a changed file aborts before writing. Tests cover differing outer/inner states, idempotence, unrelated opaque values, unsupported/truncated format handling and stale input; actual exclusive write/readback and stale-file rejection passed on a workspace fixture. A first fixture attempt under sandbox temporary storage failed permissions before writing; rerun in the workspace succeeded.

Applied with game stopped: outerWeighted=false, __SerpCurrentSettings.EnableImprovedAssassinPathfinding=false, direct climb=true at both levels. Exactly one byte changed in the real file, all others preserved. Result SHA256=8ae241e9535d4fabd895e72d0b790672d249ea5a715aace804d7cc2acb356b04. DetailedDiagnostics=true, MacemenDiagnostics=false retained. No presets, saves, runtime sources, public API, version or README changed; no mod build required. Loaded presets/mission values can still replace the working state, so next log must confirm effectiveWeighted/weightedConfigured=false and zero weighted publications.

Next user action: TARGETED_TESTS.md, only single Assassin ground commands behind both gate sizes with side walls and no bridges, open then reloaded/closed. Existing successful gate sizes, Raid/quarry, true detour and recapture remain accepted and are not repeated as separate tests. Integration remains withheld pending the native comparison and explicit assessment of any still-unobserved +0x94 branch.
# 2026-10-13 optional native reconstruction diagnostics

Native hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; full60 search/climb and21 planning/output audit repeated. Private testmod observers do not change gameplay. Exact audited spans (start,length): F4944,20; F4C30,15; F4BCB,16; F4BF0,16; E1640,16; E1A1F,14; E1A5A,16; E1ABE,18; E1BB4,15; E1BE3,14. They do not overlap APIShared endpoint adapters E19D8/E19F9, its D9C40 builder, the Gate direction filters or installed SE spans. Local Fixes source has no competing patch in these spans. Every actual live span is byte-checked and backend-decoded before commit; mismatch disables only optional trace coverage. No relocated fallback is safe for the fixed manager/stack/grid layouts.

F4944/F4C30 delimit a builder after its prologue/before its epilogue with the same RSP and RBX manager at exit. E1640 argument6 is entryRSP+30; interiorRSP=entryRSP-88. Mode1 requires a direction connection; mode3 accepts connected ground or the separately tested wall branch. E1A5A is reached only after all transition and decreasing-distance checks; RDX is predecessor, R8 current tile, R12 reverse direction. Saved connection byte is interiorRSP+98. E1ABE selects R10/R13; selected edges are reversed and directions rotated by4 before final comparison. Source/target wall flags are read at that actual acceptance, not later. E1BB4 is shared by success and failure; E1BE3 marks failure first. E1BAC is NOT a valid18-byte hook: external E1C2D jumps into its interior at E1BB4. These incoming edges are tested over the complete bodies through E1C2F/F4C45 and baseline xrefs.

F4BCB observes the actual secondary DB650 call; F4BF0 observes its failed-result fallback to DA590. Mode1 E1640 may replace prior mode3 output. A changed +94 field alone remains insufficient. Invalid identity, unmatched output, nested overwrite, incomplete reconstruction or changed policy never produces a confirmed native transition. Read-only emitter preserves RFLAGS/all GPR/volatile XMM0..5 and Win64 stack alignment; no Rflags decision transport. Tests assemble and decode all ten generated adapters with installed RedBird and execute the transparent callback/displaced-call harness.


## 2026-10-13: verified closed-gate descent exception (pending game acceptance)

The editor epoch 20:00:26--20:01:48 established real route clearing at 20:01:31.700 and 20:01:45.066 on the closed, uncaptured large gate: index/length 16/19 and 11/13 became 0/0. The fresh ground order from the roof completed descent at 20:01:35.544. Five fully matched native outputs contained 67 ground and seven climb edges, no confirmed forbidden ground crossing. These observations supersede the hypothesis that no descent was planned.

Functional owner AssassinGateDescentRuntime replaces the old diagnostic hook at 185D55..185D69 (20 bytes). On a validated consumed cardinal roof-to-ground step only, it continues at 185934 with every incoming register/flag restored. The currently consumed R14 direction must match GameUnit+2B0; the incremented path index is bounded but is not used to select the next nibble. Unit identity/control, current snapshot gate global/owner/capturer, direct gate-climb capability, path mode, ordinary connection absence and safe ground surface are required. Both forward and reverse ordinary connections disqualify the exception. All later native step and obstacle checks remain authoritative. Unknown contexts retain original 198ED0/197950 cleanup. No new path query or replacement order.

The guard works without DetailedDiagnostics; the optional old hook installer skips manifest index 10. Hooks and delegates remain process-rooted; mission lifecycle changes only activation data. Mainmods, APIShared contracts, bridge policy, versions, README and saves are unchanged.

Diagnostic correction: 1855A0 BPL distinguishes the regular call from the speculative call whose GameUnit is subsequently restored by 182B00. Logs now say ascent/descent-state-selected with movementCall=regular or speculative-restored-by-caller. Dispatcher completion remains separate. Historical descent-started alone does not prove committed physical execution; the actual route clear and later descent completion remain valid.

Technical evidence: complete 60 search/climb bodies plus 21 planning/output bodies rechecked against installed FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Native predicate, original 197950 clear, real DCE60 and functional adapter run in isolated executable memory. Both gate type predicates, both observed index/length pairs, rejected/allowed/exception cases and independent functional/diagnostic switches checked. Caller save/restore is simulated; 198ED0 unrelated cleanup is stubbed. Full native frame shadow space is supplied in the fixture. SDK lookup/configuration boundaries are explicitly simulated in the focused production-reader fixture, which checks the current method text against source. Existing weighted/native route suites remain regressions. This does not replace the remaining occupied-gate game test.

Installed interop exposes +90 as ushort N00000573 but +36C/+36D as two bytes N000001CE/UnknownRelevant1. Native 18593E reads a signed WORD at +36C: the guard checks BOTH bytes, as the native instruction does. No publicized Unity access was added.
