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

Die neue Gebäude-Scope-Fassung wurde in drei aktiven test_gates.sav-Epochen
beobachtet: 13:35:51.837–13:36:47.889 (KI-Zähler 11100, Rollenunterschiede 13),
13:36:51.958–13:37:56.844 (14151 / 14), 13:39:09.823–13:41:29.280 (8888 / 30).
Alle beobachteten Builder-Ergebnisse waren positiv, kein NoRoute, keine
Gebäude-Kontext-/Scope-/Snapshotfehler oder Exceptions; runtimeIntegrity=PASS.
Im zweiten Lauf enthielten die Ergebnisaggregate 14157 positive Beobachtungen,
also sechs mehr als der KI-Eintrittszähler. Das ist eine historische Zähldifferenz,
kein nachträglich aufgeklärter Einzelfall. Native Entry/Exit und Ergebnisobserver
verwendeten unterschiedliche KI-Abfragen; künftig bleibt die Entryklassifizierung
im QueryScope eingefroren und gilt für alle drei Stellen. Originalargumente,
Suchergebnisse, Scopemasken und Hookadressen bleiben unverändert.

Gemeinsame Suchaufrufe zählen Attack, BuildingApproach, BuildingConsumer,
CursorCommand und AI Builder jeweils einmal. BuildingConsumer bleibt im
Kantenzähler dieselbe Suchklasse wie bisher, erhält aber seinen eigenen
Aufrufzähler. Standalone CursorCommand erhält ebenfalls einen Aufrufzähler.

Gate-live-Zustände werden ohne Obergrenze nach Spieler, Building-ID, vollständigem
Zustand und vollständigem Detail intern gespeichert. Global-ID, Owner-/Capture-
Relation, Rohwerte, Policy, Ursachen und Brücken stehen in einer Definition
`Enemy-gate state: epoch=N,id=M,...`; Ergebniszeilen referenzieren `gateState=N/M`.
Definitionen erscheinen vor ihrer ersten Verwendung; Definitionen und aktive
Aggregate werden atomar ausgelesen. Identische Zustände behalten ihre Kennung über
Zehn-Sekunden-Fenster, Änderungen erhalten eine andere Kennung. Jede Observation,
erster/letzter Tribe-/Zielwert und bestehende Wechselzähler bleiben erhalten.
Reset erfolgt nur beim bestehenden Kartenstart; Epoche wird erhöht. Kein Unittracking.

Installierter SE 2.12.0, Tag v2.12.0, Commit f8d51730fcb54b25af43d3c9348d57db058e077f,
Tree 657af449e1397c58e6c5ec054977d83198b68e66 und Assemblyhash
DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF sind auditiert.
Die Mindestabhängigkeit bleibt unverändert; der Log-Versionsmatch ist weiterhin
ein Versionsvergleich und beweist keinen Laufzeit-Hashmatch.

Vanilla FBCB9319 liest Connection-Permissions in 0x181E00 mit 90 Int32 pro Zeile
(IMUL 0x5A, Zeilenabstand 0x168), Klassen 1–6 ab 0x32BDB0. Der aktuelle öffentliche
API-Konstantwert ist 89. Der flache Getter liefert 534 statt 540 Einträge;
unser Vergleich interpretiert den vorhandenen Prefix weiterhin korrekt mit
Native-Stride 90. Es fehlen Profileindex 89 und Permissionindizes 534–539
(Klasse 6, Unitindices 84–89). Alle gelesenen Werte der letzten Läufe stimmten.
Die Warnung bezeichnet jetzt unvollständige Abdeckung, keine Tabellenmutation;
Changed-Value-Warnungen bleiben getrennt. Der öffentliche Row-Getter verwendet
jedoch Stride 89 und adressiert Klassen 2–6 falsch; dies betrifft auch seinen
Can/Set-Verbraucher. Unsere Policy verwendet diese Getter/Setter nicht. Fork und
Hauptmods wurden für diesen Schritt nicht geändert. Vollständige Native-Reader-
Belege: _inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt.
Der englische Autorenreport liegt daneben als SCRIPT_EXTENDER_PATHFINDING_REPORT.md.

Logabnahmegrenzen: Alle Pre/Post-Paare und Aggregatsummen stimmten. Der dritte
Lauf beobachtete 12 gefilterte Gebäudekanten. Eroberung, nachgewiesene Cursorsperre
und Raid-Gebäudebefehl 9 wurden in diesen drei Läufen nicht beobachtet. Die
Spielbeobachtungen dieser Epochen sind noch nicht zugeordnet. Ein späterer Lauf
ohne Testmod ist ein eigener Hauptmodvergleich, kein Gatepolicy-Nachweis.
Offen: normaler/Raid-Gebäudeangriff, Eroberung/Rückeroberung, eigener Zugang nach
gesperrtem Cursorziel, mehrere Wellen und beobachteter Hauptmodvergleich. Keine
weitergehende KI-Verhaltensänderung ist aus diesen Logs gerechtfertigt.

Buildabnahme der Diagnosebereinigung: build.bat hat am 02.10.2026 die Fassung
mit 2847 bestandenen Assertions und null Warnungen/Fehlern gebaut und installiert.
Beide installierten RedBird-Capturer-Maschinentests bestanden. Die neuen Tests
prüfen 1500 Beobachtungen über 100 vollständige Torzustände und drei Zeitfenster,
Rekonstruktion, Epochenreset, eingefrorene KI-Zuordnung und unvollständige Tabellen.
Ein erster Testlauf stoppte vor der Installation wegen einer unbeabsichtigten
Änderung am direkten Cursorwrapper; dieser Pfad wurde vollständig wiederhergestellt.
Danach bestanden Tests und sämtliche Vorprüfungen erneut vor dem finalen Treiberlauf.
Lokale/installierte Testmod-DLL SHA-256:
B85CFC510B7912A4D1DF7D6F81ADB7EFB773FBE60B2F41E25853648CF6358C10.
BugfixesAndQoL und APIShared blieben gegenüber dem Stand vor diesem Build identisch
(00A0512B1F94C598B5417B6B5790A396E7B67228B27DEE2AB81E4BA466552CF7 /
02332AF3506C92C47DB066C1C2AC764A312D75E2CAFC144F1FB3B5E27617C6FF).
Version unverändert 0.1.5, README unverändert. Neue Spielabnahme der Ausgabe steht aus.

## 2026-10-02: read-only Assassin gate diagnosis

See the hash-bound baseline `knowledge/ENEMY_GATE_ASSASSINS.md`. Four pre-change runs had 403/402/207/624 NoRoute results; the first three observed open/open, open/closed and closed/closed configurations. Automatic closure in the open runs led to retargeting; no persistent stuck-unit conclusion follows from the counters.

An optional passive APIShared observer measures the existing D9C40 detour, weighted/cache-route edges and native cache contents; it never changes search results. Existing deferred aggregates retain exact counts and numeric sums with no event cap. Gate edge IDs come from mask construction, overlapping identities remain ambiguous. New Unit fields were checked against installed public SE members, and the complete control WORD is reported. Native singleton reads use +0x84/+0x88/+0x90 and ten positive/negative PCL pairs at +0x416D8C/+0x416DDC with stride 8. No new hooks or executable mutations; original route/search/cache algorithms remain unchanged. Native-only flood fields and cache-hit/fallback measurement gaps are explicitly labeled. Versions and README are unchanged.

Abnahme remains open until paired Assassin runs locate the first different reachability result. No additional Assassin movement correction is included in this diagnostic build.
# Weighted Assassin policy integration (2026-10-02)

No new native targets or hook spans. The optional route snapshot provider follows the existing mask publication and QueryScope lifetime; explicit unmasked scopes retain unmasked semantics. Revalidate native/control-word provenance, nested query restoration and snapshot/cache invalidation on updates. All behavior after an uncertain context retains the already computed native result.

## Lateral bridge policy contract (2026-10-02)

Revalidate the complete creation/animation/raising/lowering/footprint-refresh path and the 25-cell native mapper at 2D1A30. Raising 645C0 uses orientation/2, a nonzero mapper cell and a nonzero moat-record index at tile-manager +1EA23F0 before setting 0x40000000. This produces 15 closure cells in the confirmed build. GameBuilding public fields are occupied-array begin +1C8 (UInt32), grid size +F8 (UInt32), sprite variation/orientation +102 (UInt16); GetMoatWorkTaskIndexLayer is a public parameterless Span<UInt16> API in the installed assembly. Do not substitute a bounding rectangle or gate axis. The new policy has no new native hooks; existing span/owner and permanent-publication contracts remain in force. Equal-PCL planning acceptance remains a separate game regression.
