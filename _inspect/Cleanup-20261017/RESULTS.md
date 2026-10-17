# Bereinigung vom 17.10.2026

Aktueller Stand: Auf Nutzerwunsch wurden die historische Analyse-ZIP, die zugehörigen einmaligen Archiv-/Rückholskripte und die alte große SQLite-Sicherung im Extender-Audit entfernt. Frühere Zahlen und Prüfschritte unten dokumentieren den jeweiligen damaligen Stand.

## Ergebnis

- Ausgangsbestand ohne Verfolgen von Links: 89.16 GiB; 202,604 Inventareinträge einschließlich 43 Links.
- Entfernt: 123,911 reguläre Dateien mit 75.18 GiB Nutzdaten.
- In Runde 1 angelegtes historisches Analysearchiv: 305.30 MiB; inzwischen auf Nutzerwunsch gelöscht.
- 270 historische Exporte wurden als byteidentische Kopien aktueller Dateien erkannt. Das zunächst für 193 weitere Exporte angelegte ZIP wurde anschließend auf Nutzerwunsch gelöscht.
- Verbleibender Bestand einschließlich Bereinigungsnachweisen: ungefähr 14.29 GiB.
- Netto eingespart: ungefähr **74.87 GiB**. Das sind Dateilängen, keine Messung physischer NTFS-Belegung; Hardlinks/Komprimierung können davon abweichen.
- Während der Einzelprüfung übersprungen: 0 Dateien. Die genaue Liste steht in execution-summary.json.

## Bewertung und Aufbewahrung

Entfernt wurden wiederholte synthetische Bridge-Fixtures, reproduzierbare Ghidra-Arbeitsverzeichnisse, nicht ausgewählte SQLite-Indizes, Test-Buildausgaben und nachgewiesen erzeugte Workshop-Fixtures. Historische Analyseexporte wurden ausschließlich nach SHA-256 dedupliziert oder verlustfrei komprimiert; unterschiedliche Exportinhalte waren zunächst archiviert; dieses Archiv wurde anschließend auf Nutzerwunsch gelöscht.

Aktuelle Baseline-Eingaben, produktive Datenbank, Knowledge-Dateien, historische Vergleichsbasis, Referenz-Ghidra-/Rizinprojekte, Regressionstestquellen, historische Spielaufzeichnungen, Rückwegsicherungen und offene Laufzeitnachweise bleiben erhalten. Keine README.md wurde bearbeitet oder gelöscht. Es erfolgten keine Runtime-Builds, Versionsänderungen, Commits oder Veröffentlichungen.

Die Abschlussprüfung identifizierte 16 winzige synthetische C#-Dateien, die im aktuellen Quell-Snapshot erfasst sind. Diese wurden nach zunächst erfolgter Entfernung aus den Erzeugerskripten bytegenau wiederhergestellt und gegen die gespeicherten SHA-256 geprüft; sie bleiben an ihren ursprünglichen Pfaden erhalten. Siehe restored-referenced-fixtures.json. Weitere synthetische GUID-Bestände wurden anhand der Erzeuger MainSource/PublishedSdk/PublishedModpack/ModpackBranches separat geprüft: extra-actions.json und extra-creators.json.

Die APIShared-Release-/Handoff-/Merge-Ordner enthalten weiterhin registrierte Git-Worktrees: siehe [apishared-worktrees.txt](apishared-worktrees.txt). Diese Checkouts und ihre Verknüpfungen bleiben vollständig erhalten. Weitere Quell-/Git-Sicherungen wie APISharedOwnership bleiben erhalten, weil vollständige Wiederherstellbarkeit und Entbehrlichkeit ihrer eigenständigen Stände nicht nachgewiesen ist. Bild-/Atlasdaten und alte Spielversion-Vergleiche bleiben ebenfalls erhalten. Alle 324 Bestandsgruppen sind mit Vorher-/Nachhergrößen und Entscheidung in [folder-assessment.csv](folder-assessment.csv) erfasst.

## Prüfung

- 971 geschützte Baseline-Dateien vor und nach dem Lauf vollständig per SHA-256 geprüft: unverändert. Sämtliche aktuellen Manifest-Eingaben stimmten schon vor der Bereinigung mit ihren Soll-Hashes überein.
- Aktuelle Datenbank unverändert; frische Windows-PowerShell-Abfrage `stats` liefert identische Ergebnisse.
- `coverage` meldete bereits vorher **stale**, unter anderem wegen geänderter SHCDESE.dll und neuer/geänderter Modquellen. Dieser bestehende Zustand wurde nicht behoben oder als neue Beschädigung behandelt. Nachher verschwanden ausschließlich 17 Warnungen zu entfernten synthetischen Quelldateien; neue Warnungen: 0.
- Alle ZIP-Objekte vor dem Löschen vollständig dekomprimiert und gegen SHA-256/Dateilänge geprüft. Rückholung sowohl aus ZIP als auch aus einer byteidentischen aktuellen Datei getestet; Smoke-Testdateien anschließend entfernt.
- Keine Links/Junctions verfolgt. Löschung ausschließlich anhand der vorher gesicherten Dateiliste, mit erneuter Identitäts-/Zeit-/Größenprüfung, innerhalb der beiden erlaubten Ordner. Keine rekursive Fremdverzeichnis-Löschung.

## Nachweise der ersten Bereinigungsrunde

- [Vorprüfung](preflight.json), [Prüfergebnis](verification.json), [Ausführungsergebnis](execution-summary.json)
- [Historische Dateizuordnung](compaction-map.json): reines Protokoll der früheren Archivierung; das Archiv und das Rückholwerkzeug sind gelöscht.
- [Zusammenfassung der historischen Analysegenerationen](generation-summary.json): ursprüngliche Abdeckung, Funktions-/Instruktionszahlen und Zuordnung identischer beziehungsweise archivierter Exporte. Vertrauensstufen und Abnahmestatus werden durch die Bereinigung nicht aufgewertet.
- `inventory-before.csv.gz` / `inventory-after.csv.gz`: vollständige Bestandslisten; `actions.json.gz` / `actions-verified.json.gz`: vorab dokumentierte Maßnahmen und Ersatznachweise; `execution.jsonl.gz`: tatsächlich entfernte Dateien mit Zeitstempel.
- `external-references.txt` und `literal-references.json`: Referenzsuche. Die Literalübersicht enthielt auch die eigenen Vorprüfprotokolle; diese sind keine aktiven Verbraucher. Die ausschließlich auf das inzwischen gelöschte Archiv verweisenden COMPACTED.md-Notizen wurden entfernt. Aktive externe Verbraucher verweisen auf erhaltene Test-/Nachweisbestände; ausgeschlossene Generationspfade in .gitignore bleiben bewusste Ignore-Regeln.

Die historischen Exporte stehen nicht mehr in einem zusätzlichen Archiv bereit. Die früheren Dateizuordnungen und Prüfprotokolle sind lediglich Nachweise der durchgeführten Bereinigung.

## Größte erhaltene Bestände

| Bestand | GiB | Dateien |
|---|---:|---:|
| _inspect/CrusaderDE-Native-Baseline | 5.77 | 1,097 |
| _inspect/ScriptExtender2142Audit | 2.01 | 445 |
| _inspect/BridgePlanningTests | 0.98 | 297 |
| _inspect/APISharedOwnership | 0.70 | 12,236 |
| .inspect/ScriptExtenderUpdates | 0.44 | 1,975 |
| .inspect/ScriptExtenderBranchA_20260831_export | 0.31 | 1,373 |
| _inspect/SHCDESE-2.0.2-release | 0.26 | 464 |
| .inspect/ScriptExtenderV142_Compatibility_20260831 | 0.23 | 1,125 |
| .inspect/[root files] | 0.23 | 106 |
| _inspect/EnemyGateBuildingContextAudit | 0.22 | 236 |
| _inspect/SpectatorHudVanilla-20261016 | 0.16 | 1,157 |
| .inspect/APISharedWorkshopHotfix-20261015 | 0.13 | 9,180 |


## Zweite Bereinigung: direkte Loeschungen am 17.10.2026

- Weitere **3,669 Dateien** mit **682.90 MiB** Nutzdaten direkt entfernt: **1,683 getrackte**, **1,339 Git-sichtbare ungetrackte** und **647 ignorierte Dateien.
- Die neun freigegebenen Altbestaende (compile-check, compile-check-trail-preset, compile-check-trail-state, verify-build, host-client-builds, BuildDrivers20261009, SHCDESE-2.0.2-release, AssemblyCSharp und CrusaderDE) sind entbehrliche Build-/Paket-/Protokoll-/Dekompilierkopien. Aktive Verbraucher und Bezuege im veroeffentlichten Quell-Snapshot wurden vor der Loeschung erneut ausgeschlossen.
- Erzeugte GUID-Ausgaben der Spectator-HUD-/Powers-/Selection-/Gates-/Focus-Tests entfernt; Erzeugerskripte, Testquellen und Diagnoseberichte erhalten.
- 409 Audit-Sicherungskopien erst nach exaktem Vergleich mit erhaltenen aktuellen Dateien beziehungsweise Git-HEAD-Inhalten entfernt. 15 unterschiedliche Sicherungsdateien bleiben erhalten. Das alte backup-files.json ist ein historisches Vorher-Inventar; es bezeichnet keine weiterhin vollstaendige Rueckwegsicherung.
- 967 aktuelle Baseline-/Referenzdateien per SHA-256 unveraendert. Frische Windows-PowerShell-Abfragen stats und coverage liefern exakt dieselben Ergebnisse wie unmittelbar vor dieser Runde; keine neuen Aktualitaetswarnungen.
- 0 Dateien wegen zwischenzeitlicher Aenderungen ausgelassen. README-Dateien, registrierte Git-Worktrees und bestehende Nutzerarbeit wurden erhalten.
- Git-sichtbare ungetrackte Dateien im gesamten Workspace: 1693 vorher, 354 nachher. Bereits geloeschte Dateien aus Runde 1 kommen im Git-Diff zusaetzlich hinzu.
- Keine neuen Archive, Sicherungs-/Rueckholwerkzeuge oder Protokolldateien angelegt; nur diesen Bericht ergaenzt. Das zunächst erhaltene Archiv aus Runde 1 wurde auf anschließenden Nutzerwunsch gelöscht. Keine Runtime-Builds, Versionsaenderungen, Commits oder Veroeffentlichungen.

## Entfernung des historischen Archivs und der alten Audit-Datenbank

- `historical-analysis-objects.zip` (305,30 MiB) und fünf inzwischen überflüssige einmalige Bereinigungsskripte direkt gelöscht.
- Die alte Audit-Sicherung `_inspect/ScriptExtender2142Audit/backup/sem/FBCB9319/analysis/g1E2FBFF53E33/index-50932c3d7459.sqlite` (1,943,207,936 Bytes, rund 1,81 GiB) entfernt. Sie gehörte zum Stand vor dem Update auf Extender 2.14.2; die aktuelle Datenbank liegt separat in der Native-Baseline.
- 9 veraltete Archiv-Verweisnotizen entfernt. Insgesamt weitere 16 Dateien und 2.11 GiB Nutzdaten entfernt.
- Aktuelle Datenbank und ihr Manifest per SHA-256 unverändert; frische Windows-PowerShell-Abfrage `stats` erfolgreich. Keine neuen Archive oder Rückholwerkzeuge angelegt.
