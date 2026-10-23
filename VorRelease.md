# Vor-Release: offene Punkte develop → main

Stand 2026-10-24, 01:15. Hier steht nur, was vor bzw. beim Release noch offen ist. Erledigtes steht in den Changelogs (`info.json`) und in der Git-Historie.

## 1. Versionen (geprüft 24.10.2026 gegen `SerpsMods/v1.0.28`, Commit `e4306050d`)

Jeder Mod hat seit dem letzten Modpack-Release eine höhere, noch nicht veröffentlichte Version; Plugin-Konstante und `info.json` stimmen überein.

| Mod | Release 1.0.28 | jetzt |
|---|---|---|
| APIShared | 0.9.2 | 0.14.0 |
| BugfixesAndQoL | 1.0.192 | 1.0.199 |
| BuildingCosts | 1.0.114 | 1.0.116 |
| BuildingLimit | 1.0.32 | 1.0.33 |
| ExtraFeatures | 1.0.114 | 1.0.117 (verlangt APIShared 0.14.0) |
| RandomEvents | 1.0.52 | 1.0.54 |
| CastlePlanner | 0.8.41 | 0.8.45 |
| StartConditions | 1.0.32 | 1.0.33 |
| UnitCosts | 1.0.36 | 1.0.38 |
| UnitLimit | 1.0.106 | 1.0.108 |
| ExtendedData | 1.0.12 | 1.0.18 |
| SerpsModsHost (Modpack) | 1.0.28 | 1.0.30 |

Die eingecheckten Paketordner `*/BepInEx/plugins/*` erzeugt der Release-Build neu; nicht von Hand angleichen.

## 2. Prüfungen (24.10.2026, Quellstand develop nach „1018“ plus Release-Vorbereitung)

- APIShared `build.bat /nopause /noinstall` (develop-Stand plus Testanpassung) mit allen Regressionstests bestanden (01:01).
- Steam-/Release-Werkzeuge: `Test-ModpackBranches` (108), `Test-WorkshopOutputRetention` (632), `Test-WorkshopOutputGit`, `Test-PublishedSdkPolicy` (64), `Test-SteamPackPolicy`, `Test-PublishedModpack` (22), `Test-MainSource` (42), `Test-ReleaseSetup`, `Test-BootstrapEnvironment` bestanden. APIShared `Test-GitHubRelease` bestanden.
- `Test-WorkshopCacheAndChangelog` (Teil von `Test-WorkshopWorkflow`) prüft die vorhandene lokale Produktionsvorbereitung; die stammt noch von SerpsMods 1.0.28 (15.10.2026) ohne SDK-Identität und scheitert deshalb. Der nächste `CreateSteamModPack.bat`-Lauf ersetzt sie; danach erneut prüfen.
- Konsumenten: Alle 10 Modpack-Mods (installierte Builds) binden laut Metadatenscan (Mono.Cecil) ausschließlich öffentliche APIShared-0.14.0-Member. APIShared hat keine Friend-Assemblies mehr außer `APISharedTests`, `APISHARED_CONSUMER_SCAN` ist daher gegenstandslos.
- `Test-DevDiagnostics` für alle 10 Modpack-Mods: nur zulässige `#if SERP_DEVDIAG`-Quellen; die Binärsperre greift im Packager.
- Im Plugins-Ordner liegt zusätzlich der Testmod PerfProbe (DEVDIAG); er steht nicht in der Modpack-Liste.

## 3. Release (Nutzer)

1. Die PRs mergen, jeweils mit „Create a merge commit“ (beide langlebigen Branches bleiben):
   - APIShared `dev/Serpens66` → `develop` (Testanpassung für die Release-Erstellung im Modpack-Workflow), danach APIShared `develop` → `main` („Release APIShared 0.14.0“).
   - Mod-Repo `develop` → `main` („Release Serps Mods 1.0.30“).
2. Spiel schließen, `CreateSteamModPack.bat` starten, **3 (Beide)** und **1 (main)** wählen. Der Workflow
   - spult das saubere lokale `main` (Worktree `.release-output/Modpack/main/source`) per Fast-Forward auf `origin/main` nach,
   - baut APIShared aus dem gepinnten öffentlichen `main`, fragt `VEROEFFENTLICHEN` ab und legt das GitHub-Release `APIShared/v0.14.0` an,
   - baut danach das Modpack gegen dieses Release und fragt für `SerpsMods/v1.0.30` erneut `VEROEFFENTLICHEN` ab.
3. `UploadWorkshop.bat /both` (APIShared und Modpack).
4. Auf den Workshop-Seiten von APIShared und Modpack ergänzen: Alle Spieler einer MP-Partie brauchen dieselben Versionen von APIShared und Modpack (Texte stehen nicht im Repo).

Alle Mods erscheinen zeitgleich; ältere BFQoL-Versionen sind mit dem neuen APIShared nicht kompatibel, weil der Friend-Zugriff entfernt ist.

## 4. Spieltests (Nutzer)

Bisher ist keiner als durchgeführt vermerkt. Vorrangig 1–4, 13, 15 und 16.

1. **Bewegung durch eigene Gräben (B1):** ohne MoatMove keine Bewegung durch eigene Gräben, kein Lag mehr. Dazu Angriffe von Einheiten mit Grab-Fähigkeit nahe eigener Gräber, Gebäudeangriffe, Feindgraben füllen und Bewegung aus dem eigenen Graben, jeweils mit und ohne MoatMove. Lag-Vergleich gegenüber dem bisherigen Stand.
2. **MP-Lobby und Presets (Host und Client):** Beitritt, Slotwechsel, Hoständerungen vor und nach dem Beitritt, Preset laden, Kartenstart. Ein Setting, das einen Wert ablehnt, darf das Preset nicht blockieren.
3. **Coop-Paketmission im MP mit mehreren Burgen pro KI-Slot:** Host unverändert; Host ändert nach der ersten Burg (es soll die synchronisierte Hostliste genommen werden); BFQoL-Synchronisierung aus (Meldung am Startknopf erwartet).
4. **Sprite-Merge:** Modus Automatisch auf einer Karte mit weniger als 8 GB und einer mit mindestens 8 GB VRAM (Logzeile `SPRITE_SHEET_MERGE_*`), Wechsel An/Aus mit Neustart, ComboBox in den Modoptionen.
5. **Upgrade:** von einer 0.9.2-/Modpack-main-Installation mit Presets, AI-Auswahl-Store und Saves. Presets sind danach in `Documents/SHCDE/APIShared`, die Migration läuft nur einmal, alte Saves lassen sich laden.
6. **MP-Save/Load:** beide Peers auf gleicher Version; ein Peer mit alter APIShared-Version (erwartet: sofortige Blockade mit Meldung beider Protokollversionen); Solo-Host lädt einen MP-Save; Save mit pausiertem Einzelgebäude und laufendem synchronisiertem Truppenmarsch.
7. **Neustart:** Skirmish-Neustart SP und MP; Neustart aus dem Editor; „Ja“ im Neustart-Dialog. CastlePlanner Preview → Commit → Restart im MP (Host und Client); prüfen, ob der 30-s-Timeout von `FreeCastleIdentityReadiness` bei langsamen Peers reicht.
8. **Determinismus MP:** Assassinen über Tore, Mauern und Zugbrücken (auch mit Fixes); Tor erobern und sofort durchlaufen; mehrere Minuten ohne Desync. MP-Rekrutierung mit UnitCosts und UnitLimit. Shift-Patrol und Formationen im MP (Chore-Synchronisierung).
9. **Meldungen und Sprache:** Minimap-Video mit Sprache, Briefing schließen und wieder öffnen, schnelle Meldungsfolgen, Linksklick zum Ausblenden, Kartenwechsel. Formationsbefehle mit Sprachausgabe.
10. **Laden mit ExtendedData und APIShared:** Beide Lade-Dialog-Hosts setzen `Margin 45,360`; prüfen, dass sie nie gleichzeitig sichtbar sind. Save mit fehlender Karte laden (SavegameMissingMap ohne Datei-Hash).
11. **Kompatibilität:** alles einmal zusammen mit dem Fixes-Mod und dem aktuellen Script Extender; Startzeit messen (`startup menuReadySinceProcessStartS`) und mit dem vorherigen Stand vergleichen. Friedenszeit an- und abschalten. Mit Fixes erscheint `Fixes wheat CORRECTED` bzw. `ALREADY-CORRECT`.
12. **Karteneditor:** Start, Gate-Routing, Speichern im Editor; Trail Maker mit einer Trail ohne installierte Karte (Kartencache).
13. **ExtendedData-Teilschalter:**
    - Jeweils einen Hostschalter aus: Mission-Settings, Coop-Pakete oder Lord-Daten. Eine Coop-Paketmission mit Modsettings bzw. Lord-Anforderungen muss dann mit der passenden Meldung blockieren.
    - Editor-Dateien und Workshop-Upload als Clientschalter aus.
    - Jeweils Host und Client.
14. **KI-Verkauf (BFQoL „AI sell threshold safety“):** KI verkauft nie unter null bzw. mehr als den Bestand. Mit ExtraFeatures-KI-Marktpreisen zusammen. Schalter aus heißt Vanilla.
15. **Workshop:** ModSettings-Suche (auch mit türkischer Systemsprache), Upload eines eigenen Items, Startbehandlung bei laufenden Downloads.
16. **Tooltips mit APIShared 0.14.0:** Gebäude-Hover und -Platzierung (kompakt und ausführlich), Bauvorschau-Bodenwerte (BFQoL), Kosten und Limits von BuildingCosts, BuildingLimit, UnitCosts und UnitLimit bei Rekrutierung, Belagerungsbau und Reparatur; Darstellung und Text ohne doppelte oder fehlende Zeilen.
17. **Assassinen-Wegsuche:** Getrennte Hostschalter „Mensch“ und „KI-Lords“ in allen vier Kombinationen; Kletter-Button nur mit „Mensch“. Im MP ohne Desync. Auf großen Karten mit vielen KI-Assassinen kein Lag nach dem Laden (`test_stresstest4.sav` automatisiert bereits bestätigt).
18. **Formationsvorschau:** Große Auswahl (mehrere Hundert Einheiten) mit Linie und Kreis ziehen und drehen; Zielpunkte wie bisher, flüssigere Vorschau.
19. **Pest auf Mauern (ExtraFeatures 1.0.117):** Pestwolken schaden Einheiten auf Mauern, Toren, Türmen und (wenn aktiviert) Bergfrieden gemäß den vier Hostschaltern; Vanilla-Verhalten am Boden unverändert.

## 5. Später (nicht releaseblockierend)

- Hashgebundene Stellen auf Pattern-/IL-Verträge umstellen: Liste, Reihenfolge und auditierte Anker in `HashAdressTODO.md` und `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md`.
