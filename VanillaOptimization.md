# Vanilla-Optimierung SHCDE

Ergebnis der Untersuchung, warum SHCDE beim Herauszoomen stark an FPS verliert, und welche Optimierungen per Mod sinnvoll sind. Alle Optimierungen wurden zuerst im Testmod `PerfFixTest` umgesetzt (stillgelegt, siehe Anhang „Stillgelegter Testmod PerfFixTest“). Seit 21.10.2026 sind V1, V5 und V7 als lokale Client-Optionen in BugfixesAndQoL übernommen (`src/VanillaPerformance`, Standard an) und V9 als Script-Extender-Korrektur in APIShared (`src/ScriptExtenderFixes/InterpolateEvent`).

## Kurzfazit

- **Ursache:** Der Engpass ist der Unity-Main-Thread, nicht die GPU und nicht die Simulation. DE stellt jede Einheit als eigene Unity-Objekte dar (bis zu 7 pro Einheit). Unity muss sie jeden Frame prüfen, sortieren und bündeln. Beim Herauszoomen wächst die Zahl sichtbarer Objekte stark, und Sprites aus verschiedenen Sheets lassen sich nicht in einem Draw Call bündeln.
- **Umgesetzt (Testmod, standardmäßig aktiv):** V1, V5, V9 und V7. Zusammen grob **+35 % FPS** in der schweren Szene, deutlich gleichmäßigere Frames, keine Freezes mehr; das Bild bleibt unverändert.
- **Geprüft und verworfen:** LOD, einfachere Sprite-Meshes, Graphics Jobs, D3D12/Vulkan, Freigabe der Original-Sheets, Eingriffe in KI-Wegsuche oder Simulation und weitere Kleinigkeiten. Jeder Ansatz ist gemessen oder begründet.
- **Einzige verbleibende Option mit deutlichem Potenzial:** ein eigener Einheiten-Renderer (V12, geschätzt +20–30 %). Das wäre ein eigenes großes Projekt, kein Testmod-Feature.
- **Offen:** die Sichtprüfung von V7 durch den Nutzer in verschiedenen Spielständen, Tests mit Atlas-Mods und auf Grafikkarten mit wenig VRAM.

## Ergebnis auf einen Blick

Schwere Szene `test_dustyFPS2.sav`, maximal herausgezoomt (Zoom 0.25), 1920×1080:

| Stand | FPS pausiert | FPS laufend | Draw Calls | größter Ruckler (laufend) |
|---|---|---|---|---|
| Vanilla + Script Extender | 132 | 124.5 | ~1 805 | 85–173 ms |
| + V1, V5, V9 | 143 (+8 %) | 139 (+12 %) | ~1 805 | 18–32 ms |
| + V7 | **~179 (+35 %)** | **~172 (+38 %)** | **~840 (−54 %)** | < 25 ms |

Die Werte sind Mittel bzw. typische Werte aus mehreren automatisierten Läufen. Zwischen getrennten Läufen streuen die FPS durch Fremdlast auf dem PC deutlich (identische Läufe: 133–173 FPS). Belastbar sind deshalb nur Vergleiche im selben Lauf oder abwechselnde Wiederholungen.

| Nr. | Maßnahme | Wirkung | Bild | Status |
|---|---|---|---|---|
| V1 | Minimap nicht bei jedem Tick neu an Noesis binden | −0.5…1.4 ms pro Frame, p95-Frame-Zeit −30 % (zusammen mit V5/V9) | unverändert | BugfixesAndQoL |
| V5 | Merit-Daten nicht blockierend lesen | kein Bild-Freeze bei langen Sim-Ticks | unverändert | BugfixesAndQoL |
| V9 | Script-Extender-Event ohne Allokation pro Einheit | −10…14 MB/s Allokation, weniger GC | unverändert | APIShared (nur bei nachweislich synchronen Abonnenten) |
| V7 | TeamColour-Sheets auf gemeinsame Texturseiten | Draw Calls −54 %, +23–25 % FPS | pixelgleich bis auf Rundung | BugfixesAndQoL (eigener Schalter), Sichtprüfung offen |
| V10 | LOD (Details bei weitem Zoom ausblenden) | ≤ 3 %, im Messrauschen | würde sich ändern | verworfen |
| V11 | Sprite-Meshes als Rechteck statt Polygon | Obergrenze +2–5 % | würde sich ändern | verworfen |
| – | Graphics Jobs (D3D11) | kein Gewinn, laufend eher langsamer | unverändert | verworfen |
| – | D3D12 + native Graphics Jobs | pausiert +30–60 %, laufend instabil | **HUD fehlt** | verworfen (nicht reparierbar) |
| – | Vulkan | startet nicht | – | nicht verfügbar |
| – | Nativer Sim-Spike (KI-Wegsuche) | – | – | Ursache bestimmt, Optimierung verworfen |
| V12 | Eigener Einheiten-Renderer | geschätzt +20–30 % | gleich (Ziel) | nicht umgesetzt, eigenes Projekt |

## Warum DE beim Herauszoomen langsam ist

- **Aufbau:** Der alte HD-Code läuft nativ als Simulation weiter. Unity ist nur Darstellungsschicht. Pro Sim-Tick liefert die native DLL einen Änderungsstrom (Kacheln, Gebäude, Einheiten). Der Main-Thread bildet ihn auf Unity-Objekte ab:
  - eine Tilemap pro Bildschirmzeile (Boden und Gebäude);
  - ein `SpriteRenderer`-GameObject je Einheitenteil, bis zu 7 pro Einheit.
- **Engpass:** Bei ~1 800 Draw Calls liegen ~8–9 ms pro Frame im Main-Thread, überwiegend in Unitys eigener Arbeit: Sichtbarkeit, Sortierung, Bündelung, Zeichenbefehle. Die verwaltete Spiellogik kostet nur ~1 ms. Die GPU hat Reserven, die Simulation begrenzt die FPS nicht.
- **Draw-Call-Ursachen** (Zoom 0.25, ohne V7):

| Bruch zwischen sortierten Nachbarn | Anteil | Behebbar? |
|---|---|---|
| Einheit aus einem anderen TeamColour-Sheet (eigenes Material je Sheet) | 60 % | ja, durch V7 |
| Zeilen-Tilemap (eigener Renderer pro Zeile) | 30 % | nein, die Tiefensortierung hängt daran |
| andere Materialart (normal ↔ TeamColour ↔ Foliage, Cutoff-Stufe) | 9 % | nein, fachlich nötig |
| andere Textur bei gleichem Material | < 1 % | nicht lohnend |

- **Nach V7** kosten die Einheiten pro Objekt, nicht pro Eckpunkt oder Bilddetail (V10/V11). Mehr FPS gäbe es nur mit weniger Unity-Objekten, siehe V12.

## Umgesetzte Optimierungen (zuerst im Testmod PerfFixTest)

Alle Eingriffe sind verwaltete Harmony-Eingriffe, nur clientseitig (`NetworkMode=0`), ohne native Hooks und verträglich mit Script Extender und Fixes-Mod. Im Testmod standen die Schalter unter `[Features]` (Namen in Klammern). Heute gelten die Fassungen in BugfixesAndQoL bzw. APIShared.

### V1 – Minimap nicht bei jedem Tick neu binden (`RadarRebindSkip`)

- **Problem:** `MainViewModel.TestRadarMap` erzeugt bei jedem Sim-Tick (~40/s, auch pausiert) ein neues `Noesis.TextureSource` für dieselbe Minimap-Textur und bindet es neu. Das kostet 0.9–3.8 ms pro Aufruf und bis ~110 KB Allokation.
- **Lösung:** Der Aufruf wird übersprungen, solange dieselbe Texturinstanz gebunden ist. Die Minimap aktualisiert sich weiter, weil der Upload in dieselbe GPU-Textur schreibt. Eine neue Textur (Kartenwechsel, Rotation) wird normal gebunden.
- **Wirkung:** `ApplyRadarMap` sinkt von 100–180 auf ~5 ms pro Sekunde, die regelmäßigen 1–4-ms-Spitzen verschwinden.

### V5 – Merit-Daten nicht blockierend lesen (`MeritDataLockCache`)

- **Problem:** Hält ein langer Sim-Tick (siehe Sim-Spike) `EngineInterface.threadLock`, wartet der GUI-Check in `EngineInterface.GetMeritData` bis zum Ende des Ticks. Das Bild friert dabei 260–380 ms ein.
- **Lösung:** Ist der Lock belegt, bekommt der Aufrufer eine Kopie des höchstens 3 s alten letzten Ergebnisses; sonst läuft Vanilla. Die Werte (Rang und Bündnisstatus je Spieler) werden nur gelesen und ändern sich langsam.
- **Wirkung:** keine Main-Thread-Freezes mehr während langer Sim-Ticks.

**BFQoL-Überarbeitung, Quellstand 23.10.2026 (noch kein Mod-Build/Spieltest):**
Ein erfolgreicher `TryEnter` wird bis zum Finalizer gehalten. Bei belegter
Sperre wird eine unabhängige Kopie des letzten Ergebnisses derselben Sitzung
geliefert, auch jenseits von drei Sekunden. Drei Sekunden sind eine
diagnostizierte Altersgrenze; nur der Erstbezug ohne Snapshot bleibt synchron.
Bedarf wird im bestehenden `run`-Sperrbereich nach erfolgreicher nativer
Verarbeitung vor der Pufferveröffentlichung bedient; weitere benötigte
Aktualisierungen sind auf einen Versuch je Sekunde begrenzt. Laden,
Missionsende und Deaktivierung invalidieren die Sitzungszuordnung.

Der ebenfalls blockierende Main-Thread-Schreiber `SetAchData` verwendet bei
belegter Sperre ein vollständiges, sitzungsgebundenes Food/Wood/Weapons-Tupel.
Neuere Tupel ersetzen ältere. Übertragung erfolgt bei freier Sperre oder im
nächsten tatsächlich betretenen `run` vor `DLL_RunTick`; vor nativem Speichern
wird zwingend übertragen. Fremde Threads und Aufrufe außerhalb einer Sitzung
bleiben synchron. Integerargumente und native Konvertierung bleiben erhalten.
Beide Änderungen folgen BFQoLs bestehender Client-Option für Vanilla-
Performanceoptimierungen; keine neue öffentliche API oder native Hookstelle.
Die Managed-IL-Anker werden vor Aktivierung geprüft.

### V9 – Script-Extender-Event ohne Allokation (`InterpolateEventReuse`)

- **Problem:** Der Extender (2.14.3) erzeugt vor `GameMap.interpChimp` für jede Einheit und jeden Frame ein neues `UnitUnityVisualInterpolateEventArgs`. Das sind ~450 000 Objekte bzw. 10–14 MB pro Sekunde, die größte Allokationsquelle im Spiel.
- **Lösung:** APIShared verwendet für nachweislich rein lesende Empfänger ein internes Objekt wieder. Unmittelbar vor dem ersten unbekannten oder speichernden Empfänger wird für diese Auslieferung ein frisches Objekt erzeugt; alle folgenden Empfänger erhalten dasselbe frische Objekt. Verschachtelte Hook-Aufrufe und andere Threads allokieren wie bisher. Unbekannte Codeverträge verhindern die Aktivierung.
- **Bisher gemessene Wirkung:** Allokation in `lerpChimps` sank mit der früheren Implementierung auf ~1–3 MB/s, mit weniger GC-Spitzen. Die Zahlen sind historische Messungen, keine Messung der überarbeiteten Absicherung.
- **Absicherung, Quellstand 21.10.2026:** Die frühere Prüfung über `TransientObserverGate` konnte kurzzeitig hinzugekommene und wieder entfernte Abonnenten übersehen. Sie wird durch eine Prüfung direkt am tatsächlich aufgerufenen Empfänger ersetzt. R3-Auslieferung läuft als private Kopie des geprüften Originals; `Subject<T>.OnNext` und andere R3-Events werden nicht gepatcht. Die Freigabe erfolgt mit `finally`, ohne zusätzlichen Render-Callback. Build, Regressionstest-Ausführung und erneute Ingame-/Performancemessung dieser Fassung stehen noch aus.

### V7 – TeamColour-Sheets auf gemeinsame Seiten (`SheetMerge`)

- **Problem:** 60 % der Draw Calls entstehen, weil benachbarte Einheiten aus verschiedenen der 13 TeamColour-Sheets stammen. Jedes Sheet hat seine Teamfarben-Maske im Material, also ein eigenes Material.
- **Lösung:**
  - Einmal direkt nach dem Laden der Sprites (Hauptmenü, ~1.5–1.7 s) werden die 13 Sheets und ihre Masken per GPU-Kopie auf 4 Texturseiten à 16384² gelegt.
  - Je Seite gibt es 7 Materialien (Cutoff-Stufen).
  - Alle Sprites werden mit gleichem Rechteck, Pivot und **Original-Polygon** neu erzeugt (`SpriteDataAccessExtensions`; `Sprite.OverrideGeometry` wird zur Laufzeit abgelehnt).
  - Sprites und Materialien werden in `spriteLoader` und an vorhandenen Renderern immer gemeinsam ersetzt.
  - GM-Dateien, die ein Asset-Mod verändert hat, bleiben unverändert.
- **Bild:**
  - Off-screen-Vergleich von 5 460 Sprite-/Cutoff-Kombinationen: 99,3 % der Pixel identisch, der Rest weicht um 1–2 Farbstufen ab.
  - Im Spielbild gibt es vorher/nachher im selben Lauf keine sichtbaren Abweichungen.
  - Die Zeichenreihenfolge ist durch die `sortingOrder`-Blöcke je Kartenzeile festgelegt (siehe Anhang); Gebäude und Einheiten können nicht gleichrangig sein, V7 ändert ihre Reihenfolge nicht.
- **Wirkung:** Draw Calls ~1 805 → ~840, FPS pausiert 145 → 178–180, laufend ~139 → ~172 (+23–25 %).
- **Kosten:** ~1,66 GB zusätzlicher Texturspeicher (2 447 → 4 111 MB). Die Original-Sheets müssen geladen bleiben (siehe Verworfen).
- **Asset-Mods:** getestet mit einem Sprite-Override (Extender-Beispielmod): Die betroffene GM-Datei wird übersprungen, das Bild bleibt korrekt, +21 % FPS. Atlas-Overrides laufen über dieselbe Überspring-Logik, sind aber nicht im Spiel getestet.
- **Robustheit:** Fehlt die Unterstützung (z. B. maximale Texturgröße < 16384), bricht V7 mit `v7 FAIL` im Log ab und Vanilla bleibt unverändert.

## Geprüft und verworfen

| Ansatz | Ergebnis | Grund |
|---|---|---|
| Zoom, FPS-Limit oder VSync ändern | – | Nutzervorgabe: keine akzeptierte Lösung |
| **V10 LOD** bei weitem Zoom | Detailarten einzeln ausgeblendet: Reiter/Zweitteil 180, Gebäudeanimationen 180, Mauer-Füllstücke 184, Fliegendes/Orgs 174–178 FPS; Kontrolle 169–179 | kein Gewinn über das Rauschen. Nur das Ausblenden aller Einheitenkörper bringt viel (+31 %), das wäre aber kein LOD mehr. Schatten sind Teil der Einheiten-Sprites. |
| **V11** Sprite-Polygone durch Rechtecke ersetzen | Eckpunkte 182 900 → 167 700, FPS +2–5 % als Obergrenze (mit Bildfehlern) | Die meisten Eckpunkte stammen von den Tilemaps; Einheiten kosten pro Objekt, nicht pro Eckpunkt |
| Graphics Jobs unter D3D11 (`gfx-enable-gfx-jobs=1`) | pausiert 137–153 statt 133–173, laufend 126–136 statt 136–162 | kein Gewinn, laufend eher langsamer |
| **D3D12** (`-force-d3d12`) + native Graphics Jobs | pausiert 208–276 FPS (+30–60 %), laufend 113–208 (stark schwankend) | Die Noesis-Oberfläche (Leiste, Schreiber, Rahmen) wird nicht gezeichnet. Das HUD kostet unter D3D11 nichts messbar, der Gewinn ist also echt. Die Ursache liegt im geschlossenen nativen D3D12-Pfad von `Noesis.dll`: Umstellen der Bildquellen auf den funktionierenden `TextureSource`-Weg hilft nicht; Format und Mip-Anzahl sind nicht die Ursache. Per Mod nicht vertretbar reparierbar. |
| D3D12 ohne Graphics Jobs | pausiert 116, laufend 88 | langsamer, HUD fehlt ebenfalls |
| Vulkan (`-force-vulkan`) | startet nicht | nicht im Spiel-Build enthalten |
| V7: Original-Sheets nach dem Zusammenlegen freigeben | spart ~1,1 GB, zerstört aber Mauern und Türme | Zeilen-Tilemaps halten Original-Sprites dieser Sheets; Unity lädt freigegebene Texturen dafür nicht nach |
| Nativen **Sim-Spike** beheben | einzelne Ticks ~450–490 ms | Ursache: Ein KI-Gruppenbefehl berechnet im selben Tick für jede Einheit eine Route (A*-Suche mit Budget bis 400 000). Verteilen oder Kürzen ändert die Simulation (MP-Synchronität, KI-Verhalten); ein schnellerer Pfadfinder bräuchte einen nativen Hook, den APIShared bereits belegt. Das Bild läuft dabei weiter; den Freeze behebt V5. |
| V2 `lerpChimps` entschlacken | ~0.2 ms Restsockel nach V9 | Vanilla-Anteil steckt in `interpChimp`, das der Extender detourt (überlappender Hook) |
| V3 `GetComponent` cachen | ~0.04 ms pro Frame | Transpiler über 23 Stellen lohnt nicht |
| V4 Maus-/Tile-Berechnung cachen | ≤ 0.15 ms pro Frame | Risiko veralteter Hover-Anzeigen |
| V8 statische Sprites zu Zeilen-Meshes | nur 0–8 % der Sprites sind über 5 s unverändert | Statisches liegt schon in den Tilemaps |
| Zeilen-Tilemaps zusammenlegen | – | Tiefensortierung zwischen Kacheln und Einheiten hängt daran |
| Minimap-Upload überspringen, Plain-Atlas, dichteres Packen, Renderer spaltenweise abschalten | jeweils < 1 % bzw. nicht möglich | kein Nutzen bzw. Sheets voll |
| Inkrementellen GC aktivieren | – | bereits aktiv |
| Native Sim-/Renderschleife umschreiben | – | zu riskant, begrenzt die FPS nicht |

## Verbleibende Option mit Potenzial: V12 eigener Einheiten-Renderer

- **Idee:** Statt ~3 000 einzelner `SpriteRenderer` für Einheitenteile wird pro sichtbarer Kartenzeile ein selbst gebautes Mesh gezeichnet. Es wird per Job-System auf mehreren Kernen gebaut und mit dem V7-Seitenmaterial gezeichnet.
- **Begründung:** Nach V7 kosten Einheiten pro Unity-Objekt. Alle Einheitenkörper auszublenden brachte +31 %; einen Großteil davon könnte ein eigener Renderer gewinnen. Geschätzt +20–30 %.
- **Schwierigkeiten:**
  - Die exakte Sortierung (`sortingOrder`-Blöcke je Zeile, `layerDelay`, Lebensbalken) muss zwischen den Zeilen-Tilemaps nachgebildet werden.
  - Positionen und Animationen ändern sich jeden Frame.
  - Script Extender und Mods hängen an den Einheiten-Renderern (Interpolationsevent, `SetBodySprite`-Detour, Skins, Nachbilder).
  - Auswahl und Mausabfrage müssen weiter funktionieren.
- **Empfehlung:** Nur als eigenes Projekt, zuerst mit einem Prototyp für eine Einheitenart, um den tatsächlichen Gewinn zu messen.

## Simulationsdurchsatz auf großen Karten (23.10.2026)

Messung `test_stresstest4.sav` (8 KI, größte Karte, Speed 300, Zoom 0.5, nur SE + PerfProbe): **88–97 Ticks/s**, Sim-Thread ~10,4–11,2 ms pro Tick. Die Simulation erreicht damit auf dieser Karte schon in Vanilla nur ~90–95 Ticks/s; Spielgeschwindigkeiten über ~90 bringen dort auch ohne Mods kaum etwas.

| angeforderte Speed | nur SE | SE + APIShared + BFQoL |
|---|---|---|
| 90 | 89,1 | 87,6 |
| 150 | 93,1 | 86,0 |
| 300 | 92,4–97,2 | 84,6–90,7 |

Die kurzen FPS-Einbrüche (nur SE: ~80–99 Frames > 50 ms pro Minute) entstehen, wenn lange Sim-Ticks (KI-Gruppenwegsuche, 150–200 ms) den Main-Thread über `EngineInterface.threadLock` blockieren; mit BFQoLs Vanilla-Performance-Optionen (V1/V5) sind es 0, ohne sie 73.

| Anteil Sim-Thread | Funktion | Befund |
|---|---|---|
| ~41 % | `0xF5A40` | Blattfunktion ohne Aufrufe, einmal pro `DLL_RunTick` (`0x86B9B`); hängt von Kartenbreite (0x320) und Kamerarotation ab: nativer Durchlauf über den sichtbaren Ausschnitt für die Darstellung. Kosten skalieren mit Sichtfläche/Zoom. |
| ~17 % | `0x41D60` | Sichtkachel-Renderer (siehe BFQoL A2). |
| ~8 % | `0x18C160`, `0x19CBE0`, `0x182B00` | Einheitenlogik, nicht weiter untersucht. |

Machbarkeitsprüfung `0xF5A40` (23.10.2026, statisch): Die Schleifen lesen pro sichtbarer Kachel Einheitenrecords (`+0x67E8AE6`, `+0x67E8D18/E90/E9C`), mehrere kachelgroße Kartenebenen (`0x485A770` … `0x4F64CF0`) und die Diplomatie-Tabelle (`+0x37EDF3C`) und schreiben indexbasierte Int-Tabellen (u. a. `-1`-Füllungen). Die Ausgabe hängt damit vom Zustand jedes Ticks ab; eine Wiederverwendung bei unveränderter Kamera wäre nicht exakt. Übrig bliebe eine byte-genaue Neuimplementierung (vier Rotationsvarianten, speicherbegrenzt, Gewinn unsicher). Eine Optimierung von `0xF5A40` wäre ein Umbau der nativen Render-Exportschleife (siehe „Native Sim-/Renderschleife umschreiben“ unter Verworfen) und wurde nicht begonnen. Fremder Code, der zwischen den nativen Phasen viel Speicher berührt, verlangsamt `0xF5A40` messbar über Cache-Verdrängung (BFQoL-Torhausabfrage vorher: +0,3–0,5 ms pro Tick).

## Offene Punkte

- **V7:** Sichtprüfung durch den Nutzer in verschiedenen Spielständen. Tests mit Atlas-Overrides, dem Foliage-Begleitplugin des AtlasBuilders und auf Grafikkarten mit wenig VRAM bzw. kleinerer maximaler Texturgröße.
- **Übernahme** (21.10.2026): V1/V5/V7 in BugfixesAndQoL, V9 in APIShared. Noch offen: Spieltest der übernommenen Fassungen. Die Messautomatisierung liegt seit 23.10.2026 in PerfProbe; PerfFixTest ist stillgelegt.
- **Ladephase** (unabhängig vom Zoom): `EngineInterface.LoadSaveFile` ~300 ms, `NoesisView.OnGUI` ~800 ms nach dem Laden, `FatControler.Update` ~2.4 s beim Wechsel vom Hauptmenü auf die Karte.

## Anhang

### Messgrundlage

- Spiel: Native `CrusaderDE.dll` `FBCB9319…31E2`, Managed `Assembly-CSharp.dll` `BC8B6A39…D789`, Unity 2021.3.45f2, URP 2D (SRP Batcher und Dynamic Batching an), Mono, D3D11 mit Render-Thread, Graphics Jobs aus, inkrementeller GC aktiv.
- Messrechner: RTX 4090, Ryzen 9 7950X3D, 1920×1080 Fullscreen-Window. Ausnahme: die HUD-/D3D12-Vergleichsläufe, beide Varianten bei 3840×2160.
- Installation: Script Extender 2.14.3, `PerfProbe` (DEVDIAG) und `PerfFixTest`; bewusst ohne APIShared und weitere Mods.
- Messzeitraum: 2026-10-20. Die Werte sind Momentaufnahmen für diese Versionen; bei neuen Spiel- oder Extender-Versionen neu messen.

### Messverfahren

- **Automatisierung** (seit 23.10.2026 in PerfProbe, [PerfProbeAutomation.DevDiag.cs](Testmods/PerfProbe/src/PerfProbeAutomation.DevDiag.cs), `[Automation]` in `PerfProbe_Serp.cfg`, standardmäßig aus, nur im DEVDIAG-Build): Hauptmenü → Spielstand laden → 80 Sim-Ticks → Zoom bis `TargetZoom` bzw. Grenze → native Pause halten → laufen lassen → Spiel beenden. Die Frame-Statistik je Phase misst die Automatisierung selbst. Die Bereitschaft wird ohne APIShared erkannt, damit nichts das Ergebnis verfälscht. `AutomationOnly = true` installiert keine anderen PerfProbe-Hooks, Sampler oder Extras (Basislauf „nur SE“; entspricht dem früheren PerfFixTest mit allen Fixes aus, Logmarker `PERFPROBE_AUTOMATION_ONLY`).
- **Lauf-Skript** [Testmods/PerfProbe/tools/Run-Automation.ps1](Testmods/PerfProbe/tools/Run-Automation.ps1):
  - `-Label`, `-OutputDirectory`, `-Config "Abschnitt.Schlüssel=Wert;…"` (abschnittsgenau, z. B. `Automation.RunSeconds=60;Sampler.Enabled=true`), `-AutomationOnly`;
  - `-AffinityMask` (Prozess auf feste Kerne, z. B. `0xFFFF`);
  - `-BootConfig "key=value;…"` (`boot.config` vorübergehend, byte-genau wiederhergestellt);
  - `-LaunchArgs` (Direktstart der exe, braucht vorübergehend `steam_appid.txt`).
  - Es sichert `LogOutput.log`, `Player.log` und den Pause-Screenshot und setzt `[Automation]` danach zurück.
  - Vor jedem Start wartet es, bis der vorige Spielprozess samt Crash-Handler beendet ist.
- **Vergleiche im selben Lauf (historisch, PerfFixTest):** Proben unter `[Probes]` schalteten in der Pause um und maßen Fenster für Fenster, mit Kontrollfenstern und Screenshots vorher/nachher. Beschreibung im Anhang „Stillgelegter Testmod PerfFixTest“.

### Sortierung je Kartenzeile (49er-Block)

| Offset | Inhalt |
|---|---|
| +0 | Zeilen-Tilemap (Boden, Gebäude) |
| +1, +16…+40 | Gebäudeanimationen |
| +2 | Mauer-Füllstücke |
| +3, +41 | Pixies |
| +4…+7 | Orgs |
| +8…+14 bzw. +42…+48 | Einheitenteile (mit `layerDelay` +34) |
| +15…+17 | Fliegendes |

Formel: `-20000 + Zeile*49 + Offset`. Lebensbalken können auf 32767 liegen. Gleichstände gibt es nur zwischen gleichartigen Teilen derselben Zeile; Unity ordnet sie nach Material.

### Texturen und Materialien

- `Unlit/TeamColour`: 13 Sheets (`body1/2/4/5/6/7/8/12/13/14/15/16Sprites` je 8192² BC7, `anims1Sprites` 8192×4096) mit gleich großen DXT1-Masken (`_TeamMask` im Material), 7 Materialien je Sheet (`_SpriteCutoff`), zusammen 111 492 Sprites, überwiegend Tight-Mesh.
- `Unlit/Foliage`: `treeSprites`. Normal (`Sprite-Unlit-Default`): u. a. `AllTileSprites` (Kacheln, Gebäude).
- Script-Extender-Atlanten haben pro GM-Gruppe eine eigene Textur und ein eigenes Material; jeder Asset-Mod erhöht also die Zahl der Materialien. Farb- und Maskentextur müssen dasselbe Layout haben.
- Die GM-Gruppenzuordnung steht maschinenlesbar in `Helpers/AtlasBuilder/atlas-contract SHOM/atlas_contract/loader_groups.json`.

### Nativer Sim-Spike

Aufrufkette laut Stack-Sampler (PerfProbe `simSample`):

`DLL_RunTick` (`0x86680`) → KI-Spieler-Scheduler (`0x2AE40`) → KI-Militärroutine → Gruppenbewegungsbefehl (`0x11B520`) → Einzel-Wegsuche (`0x196280`) → Pfadfinder (`0xF4930`).

Details stehen in der Native-Baseline unter `knowledge/SIM_TICK_SPIKE_PATHING.md`.

### Messwerkzeuge

- **PerfProbe** (`Testmods/PerfProbe`, nur `build.bat /devdiag`): Zeitmessung der Kernmethoden und des Sim-Ticks, Render-Snapshots (Draw Calls, SetPass), Engine-Recorder, Draw-Call-Census, Sweep über Unity-Callbacks, Allokationen, Ruckler-Zeilen mit Kontext, Stack-Sampler für lange Sim-Ticks (`simSample`, RVAs mit `query.ps1 function <RVA>` auflösen).
- **Automatisierung in PerfProbe:** Log-Marker `AUTOMATION … phase=`, `framestats`, `tickstats`, `nativecounters`, `gcstats`, `result PASS|FAIL`.

### Stillgelegter Testmod PerfFixTest

Am 23.10.2026 entfernt. Letzter vollständiger Quellstand: Commit `a7978899f`, Ordner `Testmods/PerfFixTest`. Wiederherstellen bei Bedarf mit `git checkout a7978899f -- Testmods/PerfFixTest`. Die Proben setzten das damalige Assembly-CSharp `BC8B6A39…` voraus.

- **Fixes** (`[Features]`): `RadarRebindSkip` (V1), `MeritDataLockCache` (V5), `InterpolateEventReuse` (V9) und `SheetMerge` (V7). V1, V5 und V7 leben überarbeitet in BugfixesAndQoL (`src/VanillaPerformance`), V9 in APIShared (`src/ScriptExtenderFixes/InterpolateEvent`). Die Testfassung von V9 war ein Transpiler auf `ManagedHookManager.GameMap_InterpChimpHook`. Er ersetzte die eine Allokation durch eine wiederverwendete Instanz, nur beim äußersten Aufruf auf dem Main-Thread. Bewusst gab es kein Präfix und keinen Finalizer, weil deren try/finally-Hülle bei ~3000 Aufrufen pro Frame messbar bremste.
- **Automatisierung** (`[Automation]`): nach PerfProbe übernommen. Neu ist `AutomationOnly`; die probebezogenen Teile entfielen (Mid-Pause-Umschaltung, zwei Screenshots, `features(…)` in den Logzeilen).
- **Proben** (`[Probes]`, alle nur messend, nur in der gehaltenen Pause):
  - `SheetMergeProbe` (Hauptmenü): GPU-Grenzen (max. Texturgröße, CopyTexture, BC7/DXT1, VRAM), Tight-Mesh-Anteil, Prüfung „alle Mesh-Ecken im Sprite-Rect“ (Voraussetzung, weil `Sprite.OverrideGeometry` nur Ecken im Rect annimmt) und eine zeitgemessene GPU-Kopie eines BC7-Sheets auf eine 16384²-Seite.
  - `SheetMergeMidPause`/`SheetMergeControl`: V7 mitten in der Pause anwenden bzw. nur so tun. Statistik und Screenshot jeweils vorher/nachher.
  - `SheetMergeRenderCheck`: Ausgewählte Sprites werden off-screen pixelgenau gerendert, je Cutoff-Stufe mit Original- bzw. Seiten-Sprite und Material und mit nicht-weißer Tönung (Teamfarben-Maske), und die Pixel verglichen. Damit trennt die Probe echte Renderfehler von reinen Reihenfolgeänderungen. Ergebnis siehe V7.
  - `LodProbe`: Bestand je Detailkategorie (aus Vanillas `GameMap`-Objekt-Dictionaries, nicht aus Sortieroffsets), dann je Kategorie ein Fenster mit `renderer.enabled = false` und exakter Wiederherstellung. Kontrollfenster am Anfang und Ende. Liefert die Obergrenze eines LOD-Gewinns.
  - `QuadProbe` (braucht V7): Tight-Meshes werden durch das 4-Ecken-Begrenzungsrechteck ersetzt. Je Sprite wird off-screen geprüft, ob das verlustfrei ist. Gemessen werden die Fenster Kontrolle, alle Quads, nur verlustfreie Quads und Kontrolle.
  - `HudProbe`: alle `NoesisView` für ein Fenster aus (Kosten des HUD, getrennt vom D3D12-Effekt).
  - `NoesisProbe`/`NoesisSingleMip`: Liste der Texturen, die `NoesisTextureProvider.UpdateTextures` an Noesis übergibt (Format, Mips, nativer Zeiger). Ein Transpiler testete Mip-Anzahl 1 (D3D12-HUD-Hypothese). Der Extender hookt `GetTextureInfo`, nicht `UpdateTextures`, daher gab es keine Überlappung.
  - `NoesisRebindProbe`: `BitmapImage`-Quellen von `Image`/`ImageBrush` werden durch `TextureSource` um dieselbe Unity-Textur ersetzt (der Weg, der unter D3D12 rendert, wie die Minimap). Screenshots vorher/nachher; die Szene wird nicht zurückgesetzt.
- **Nativer Zählerzugriff** (übernommen): Pfadmanager `base+0x60AD660`, `+0x78` fehlgeschlagene Routenrekonstruktionen, `+0x74` PCL-Neuaufbauten. Nur lesend und mit Prüfung der Imagegröße.
