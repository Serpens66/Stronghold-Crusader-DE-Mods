from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
def save(path,text):
    expected=text.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8')
    path.write_bytes(expected); assert path.read_bytes()==expected
    assert b'\n' not in expected.replace(b'\r\n',b'')
mod=ROOT/'Testmods/EnemyGatePathfindingTest'
path=mod/'src/PathfindingGlobalsBaseline.cs'
text=path.read_text()
text=text.replace('internal string MissingCoverage => "profiles=" + ComparedProfiles + ".." +\n            (PathfindingGlobalsBaseline.UnitTypeCount - 1) + ";permissions=" +\n            ComparedPermissions + ".." + (PathfindingGlobalsBaseline.PermissionCount - 1);', '''internal string MissingCoverage => "profiles=" + Missing(ComparedProfiles, PathfindingGlobalsBaseline.UnitTypeCount) +
            ";permissions=" + Missing(ComparedPermissions, PathfindingGlobalsBaseline.PermissionCount);
        private static string Missing(int compared, int capacity) => compared >= capacity
            ? "none" : compared + ".." + (capacity - 1);''')
save(path,text)
path=mod/'src/EnemyGatePathfindingNativeDefinition.cs'; text=path.read_text()
text=text.replace('        public const string AuditedRedBirdVersion', '''        public const string AuditedScriptExtenderTree = "657af449e1397c58e6c5ec054977d83198b68e66";
        public const string AuditedScriptExtenderSha256 =
            "DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF";
        public const string AuditedRedBirdVersion''')
save(path,text)
path=mod/'src/EnemyGatePathfindingTestPlugin.cs';text=path.read_text()
text=text.replace('$"auditedCommit={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit}, " +', '''$"auditedCommit={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"auditedTree={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderTree}, " +
                    $"auditedAssemblySha256={EnemyGatePathfindingNativeDefinition.AuditedScriptExtenderSha256}, " +''')
save(path,text)
notes='''

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
'''
for path in [mod/'PROJECT_FINDINGS.md',mod/'UpdateToNewDLL.md',ROOT/'_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md']:
    save(path,path.read_text(encoding='utf-8-sig')+notes)
report='''# Pathfinding connection-class API uses the wrong native row stride

Verified with installed SHCDESE 2.12.0 (v2.12.0, commit
f8d51730fcb54b25af43d3c9348d57db058e077f) and CrusaderDE.dll SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

`GamePathingManagerAPI.PATHFINDING_UNIT_TYPE_COUNT` is 89, derived from
`eChimps.CHIMP_NUM_TYPES`. Native `0x181E00` instead indexes permission rows with
`class * 0x5A + unitType` from class-zero table RVA 0x32BC48. Public class one
starts at 0x32BDB0; the native row stride is 90 Int32 / 0x168 bytes.

`GetUnitTypePathfindingConnectionClassPermissions` computes row offsets using
89. Classes 2–6 therefore start 1–5 Int32 values before their native row.
`CanUnitTypeUsePathConnectionClass` and `SetUnitTypeCanUsePathConnectionClass`
inherit this offset error; writes can affect a different native class/type.

Please separate the valid enum count from native table capacity/row stride and
use the confirmed native stride for class addressing. The flat permissions span
currently exposes only 534 of 540 storage entries; profile storage contains 90
entries while the profile view exposes 89. These shortened views alone do not
prove an enum validation bug, but they cannot claim full native table coverage.

Our testmod only reads the flat prefix with native stride 90 and explicitly logs
the missing coverage. It does not call the affected per-class getter/setter.
No Script Extender source changes were made. Evidence includes installed machine
code, all direct native profile-table readers and canonical table contents.
'''
save(ROOT/'_inspect/EnemyGateBuildingContextAudit/SCRIPT_EXTENDER_PATHFINDING_REPORT.md',report)
print('PASS: targeted CRLF source corrections, baseline/testmod notes and author report saved.')
