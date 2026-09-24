# Acht-Match-Serie mit geordneten AIV-Listen

Status 24.09.2026: Testmod gebaut, installiert und offline geprüft. Die installierte Konfiguration hat die Serien-ID `aiv-multi-default-eight-20260924`, acht Läufe und `nextIndex: 0`. Die abgeschlossene vorige Serie und ihr Fortschritt sind unter `../MarkerRuntimeValidation/InstalledSeriesBackup` gesichert.

| Lauf | Karte | Mehrfachauswahl | Completed Castles |
| --- | --- | --- | --- |
| 1 `CL-Early-off` | Crater Lake | Nizar: Default 1, Default 8 | aus |
| 2 `CL-Early-on` | Crater Lake | Nizar: Default 1, Default 8 | an |
| 3 `CL-Middle-off` | Crater Lake | Emir: Default 2, Default 7 | aus |
| 4 `CL-Middle-on` | Crater Lake | Emir: Default 2, Default 7 | an |
| 5 `CC-Early-off` | Craggy Cliffs | Nizar: Default 1, Default 8 | aus |
| 6 `CC-Early-on` | Craggy Cliffs | Nizar: Default 1, Default 8 | an |
| 7 `CC-Middle-off` | Craggy Cliffs | Emir: Default 2, Default 7 | aus |
| 8 `CC-Middle-on` | Craggy Cliffs | Emir: Default 2, Default 7 | an |

Jeder Lauf enthält den Menschen und sieben KIs auf den bereits geprüften Keep-Slots. Beim Öffnen einer neuen lokalen Skirmish-Lobby setzt der Testmod Karte, Spieler, Keeps, AIV-Reihenfolge und Completed-Castles-Option. Die Lobby nicht von Hand ändern. Pro Lauf genügt der sichtbare Beginn der Karte; auf den späteren regulären KI-Bau muss nicht gewartet werden. Dann zurück ins Menü und eine neue lokale Skirmish-Lobby öffnen. Alle acht Starts können in einem Spielprozess erfolgen.

Vor dem Kartenstart prüft der Testmod Map-Hash, Spielerfolge, Keep-Zuordnung, AIV-Datenhashes und Option. Nur ein bestätigter Start erhöht den Fortschritt. Ein abgebrochener oder geänderter Lauf bleibt an derselben Position. Die Diagnose erfasst Spieler und Kandidaten generisch, nicht anhand fest programmierter IDs. Nach der Serie werden Vollständigkeit, Auswahl, Drehung, Native-Fits und Bauzustand gegen die Offline-Prognose verglichen. Die acht Aufnahmen allein beweisen keine allgemeine Schreibgrenze des nativen Konstruktors; unbelegte Fälle bleiben grau.
## Datensicherung

Die drei Trace-Verzeichnisse des installierten `ActiveAIVDetector_Serp` sind Verzeichnisverknüpfungen zu `../LiveCaptureInbox/StartTraces`, `../LiveCaptureInbox/CellTraces` und `../LiveCaptureInbox/PrebuildTraces` im Workspace. Neue Rohtraces landen dadurch sofort im Workspace, auch wenn der Plugin-Ordner später verschoben wird. Vor der Umstellung wurden die 24 Start- und 62 Zelltraces aus der Installation anhand der archivierten SHA-256-Dateien verglichen und ohne Verlust in den Inbox-Ordner übernommen. Die 63 älteren Dateien aus dem Desktop-Ordner sind bereits hashgleich im Archiv `../Pivot13RuntimeRegression/Observed` vorhanden; sie müssen nicht wieder in die Installation kopiert werden. Das globale BepInEx-Append-Log bleibt in der Spielinstallation und wird nach der Testserie zusammen mit der Auswertung archiviert.