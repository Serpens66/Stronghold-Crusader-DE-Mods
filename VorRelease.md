# Vor-Release: offene Punkte develop → main

Stand 2026-10-23, 18:40. Hier steht nur, was vor bzw. beim Release noch offen ist. Erledigtes steht in den Changelogs (`info.json`) und in der Git-Historie (Mod-Repo bis `603b87b8c` „1013“, APIShared bis `4a49893` „46 shared tooltips“).

## 1. Commit (Nutzer)

Noch nicht committet (parallele Arbeit, Stand 18:40; vor dem Commit `git status` prüfen):

- Tooltip-Umstellung auf APIShared 0.13.0 in BuildingCosts, UnitCosts, UnitLimit und BuildingLimit (Quellen, XAML-Patches, Plugin-Ausgaben).
- ExtendedData: `LordDataSnapshot.cs`, `TrailLordRequirements.cs`, neu `FixesPreferenceSources.cs`.
- RandomEvents: Plugin-Ausgaben (`info.json`, DLL, PDB).
- Gelöschte Planungsdateien: `ExtendedData-APIShared-Umzugsplan.md`, `ModsettingsFix.md`, `PerformanceFix.md`.

## 2. Bauen und prüfen (Spiel vorher schließen)

1. Nach allen Codeänderungen (auch aus anderen Chats) neu bauen. Für den Release-Build gegen das veröffentlichte APIShared-SDK bauen (siehe 3.), nicht gegen `/workspace`.
2. Nach dem Start im Log prüfen, was am 23.10.2026 noch nicht bestätigt ist:
   - Mit installiertem Fixes-Mod erscheint `Fixes wheat CORRECTED` bzw. `ALREADY-CORRECT` (ohne Fixes kam korrekt `NOT-NEEDED`).
   - Info-Zeilen erscheinen nur einmalig pro Start oder bei Nutzeraktionen.
   - Bereits bestätigt (Start `S3FIX` 18:02 mit APIShared 0.13.0 und BFQoL 1.0.197): NotificationQueue mit `method=reference-rva`, kein `signature-fallback`, kein Friedenszeit-Fehler, `AI sell hook resolved` zweimal, keine Fehlerzeilen; SavegameMissingMap `READY` (nur mit `[Diagnostics] Enabled`, Lauf `L3BFQDIAG`). Nach weiteren Änderungen erneut kurz kontrollieren.
3. Den BFQoL-Release-Build vor der Veröffentlichung mit `APISHARED_CONSUMER_SCAN` gegen APIShared scannen. Es dürfen keine Interna gebunden sein.
4. BugfixesAndQoL ist derzeit normal installiert (kein DEVDIAG). Im Plugins-Ordner liegt zusätzlich der Testmod PerfProbe (DEVDIAG); er gehört nicht ins Modpack.

## 3. Release-Reihenfolge (Nutzer)

1. APIShared `dev/Serpens66` → develop → main mergen, Tag für die veröffentlichte Version (derzeit **0.13.0**), GitHub-Release und Workshop-Update. Erst danach das Modpack gegen das veröffentlichte SDK bauen. BuildingCosts, UnitCosts und UnitLimit verlangen APIShared 0.13.0, BFQoL 0.12.6.
2. Mod-Repo develop → main, Modpack-Release. Alle Mods erscheinen zeitgleich; ältere BFQoL-Versionen sind mit dem neuen APIShared nicht kompatibel, weil der Friend-Zugriff entfernt ist.
3. Auf den Steam-Workshop-Seiten von APIShared und Modpack ergänzen: Alle Spieler einer MP-Partie brauchen dieselben Versionen von APIShared und Modpack. Diese Texte stehen nicht im Repo.
4. Nach dem Release-Build die committeten `*/BepInEx/plugins/*/info.json` mit den Quell-`info.json` abgleichen. Die Quellversionen sind einheitlich (Stand 23.10.2026 abends: Plugin-Konstante, Quell-`info.json` und csproj stimmen in allen Release-Mods überein). Veraltet sind nur eingecheckte Paketausgaben vom 18.10.2026, und zwar `info.json` **und** DLL zusammen: ExtraFeatures (Paket 1.0.114, Quelle 1.0.116), SerpsModsHost (Paket 1.0.28 einschließlich `serps-modpack.json`, Quelle 1.0.30) und die APIShared-Mindestversion im ExtendedData-Paket (0.12.3, Quelle 0.12.5). Branch-Builds (`/source:working`) aktualisieren diese Paketordner nicht. Nicht von Hand angleichen, sonst entsteht ein gemischtes Paket mit alter DLL; der Release-Build erzeugt sie neu.

## 4. Spieltests (Nutzer)

Bisher ist keiner durchgeführt. Vorrangig 1–4, 13, 15 und 16.

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
11. **Kompatibilität:** alles einmal zusammen mit dem Fixes-Mod und dem aktuellen Script Extender; Startzeit messen (`startup menuReadySinceProcessStartS`) und mit dem vorherigen Stand vergleichen. Friedenszeit an- und abschalten.
12. **Karteneditor:** Start, Gate-Routing, Speichern im Editor; Trail Maker mit einer Trail ohne installierte Karte (Kartencache).
13. **ExtendedData-Teilschalter:**
    - Jeweils einen Hostschalter aus: Mission-Settings, Coop-Pakete oder Lord-Daten. Eine Coop-Paketmission mit Modsettings bzw. Lord-Anforderungen muss dann mit der passenden Meldung blockieren.
    - Editor-Dateien und Workshop-Upload als Clientschalter aus.
    - Jeweils Host und Client.
14. **KI-Verkauf (BFQoL „AI sell threshold safety“):** KI verkauft nie unter null bzw. mehr als den Bestand. Mit ExtraFeatures-KI-Marktpreisen zusammen. Schalter aus heißt Vanilla.
15. **Workshop:** ModSettings-Suche (auch mit türkischer Systemsprache), Upload eines eigenen Items, Startbehandlung bei laufenden Downloads.
16. **Tooltips mit APIShared 0.13.0:** Gebäude-Hover und -Platzierung (kompakt und ausführlich), Bauvorschau-Bodenwerte (BFQoL), Kosten und Limits von BuildingCosts, BuildingLimit, UnitCosts und UnitLimit bei Rekrutierung, Belagerungsbau und Reparatur; Darstellung und Text ohne doppelte oder fehlende Zeilen.
17. **Assassinen-Wegsuche (neu 23.10.2026):** Getrennte Hostschalter „Mensch“ und „KI-Lords“ in allen vier Kombinationen; Kletter-Button nur mit „Mensch“. Im MP ohne Desync. Auf großen Karten mit vielen KI-Assassinen kein Lag nach dem Laden (`test_stresstest4.sav` automatisiert bereits bestätigt).
18. **Formationsvorschau (neu 23.10.2026):** Große Auswahl (mehrere Hundert Einheiten) mit Linie und Kreis ziehen und drehen; Zielpunkte wie bisher, flüssigere Vorschau.

## 5. Später (nicht releaseblockierend)

- Hashgebundene Stellen auf Pattern-/IL-Verträge umstellen: Liste, Reihenfolge und auditierte Anker in `HashAdressTODO.md` und `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md`.
