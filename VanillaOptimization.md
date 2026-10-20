# Vanilla-Optimierung SHCDE

Ergebnis der Untersuchung, warum SHCDE beim Herauszoomen stark an FPS verliert, und welche Optimierungen per Mod sinnvoll sind. Alle Optimierungen sind im Testmod `Testmods/PerfFixTest` umgesetzt. Eine Übernahme in Hauptmods oder APIShared ist derzeit nicht vorgesehen (Nutzervorgabe).

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
| V1 | Minimap nicht bei jedem Tick neu an Noesis binden | −0.5…1.4 ms pro Frame, p95-Frame-Zeit −30 % (zusammen mit V5/V9) | unverändert | umgesetzt (Testmod) |
| V5 | Merit-Daten nicht blockierend lesen | kein Bild-Freeze bei langen Sim-Ticks | unverändert | umgesetzt (Testmod) |
| V9 | Script-Extender-Event ohne Allokation pro Einheit | −10…14 MB/s Allokation, weniger GC | unverändert | umgesetzt (Testmod) |
| V7 | TeamColour-Sheets auf gemeinsame Texturseiten | Draw Calls −54 %, +23–25 % FPS | pixelgleich bis auf Rundung | umgesetzt (Testmod), Sichtprüfung offen |
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

## Umgesetzte Optimierungen (Testmod PerfFixTest)

Alle Eingriffe sind verwaltete Harmony-Eingriffe, nur clientseitig (`NetworkMode=0`), ohne native Hooks und verträglich mit Script Extender und Fixes-Mod. Die Schalter stehen in `BepInEx/config/PerfFixTest_Serp.cfg` unter `[Features]`, alle standardmäßig an; nach einer Änderung ist ein Neustart nötig.

### V1 – Minimap nicht bei jedem Tick neu binden (`RadarRebindSkip`)

- **Problem:** `MainViewModel.TestRadarMap` erzeugt bei jedem Sim-Tick (~40/s, auch pausiert) ein neues `Noesis.TextureSource` für dieselbe Minimap-Textur und bindet es neu. Das kostet 0.9–3.8 ms pro Aufruf und bis ~110 KB Allokation.
- **Lösung:** Der Aufruf wird übersprungen, solange dieselbe Texturinstanz gebunden ist. Die Minimap aktualisiert sich weiter, weil der Upload in dieselbe GPU-Textur schreibt. Eine neue Textur (Kartenwechsel, Rotation) wird normal gebunden.
- **Wirkung:** `ApplyRadarMap` sinkt von 100–180 auf ~5 ms pro Sekunde, die regelmäßigen 1–4-ms-Spitzen verschwinden.

### V5 – Merit-Daten nicht blockierend lesen (`MeritDataLockCache`)

- **Problem:** Hält ein langer Sim-Tick (siehe Sim-Spike) `EngineInterface.threadLock`, wartet der GUI-Check in `EngineInterface.GetMeritData` bis zum Ende des Ticks. Das Bild friert dabei 260–380 ms ein.
- **Lösung:** Ist der Lock belegt, bekommt der Aufrufer eine Kopie des höchstens 3 s alten letzten Ergebnisses; sonst läuft Vanilla. Die Werte (Rang und Bündnisstatus je Spieler) werden nur gelesen und ändern sich langsam.
- **Wirkung:** keine Main-Thread-Freezes mehr während langer Sim-Ticks.

### V9 – Script-Extender-Event ohne Allokation (`InterpolateEventReuse`)

- **Problem:** Der Extender (2.14.3) erzeugt vor `GameMap.interpChimp` für jede Einheit und jeden Frame ein neues `UnitUnityVisualInterpolateEventArgs`. Das sind ~450 000 Objekte bzw. 10–14 MB pro Sekunde, die größte Allokationsquelle im Spiel.
- **Lösung:** Ein Transpiler auf die Extender-Methode verwendet ein Objekt wieder. Verschachtelte Aufrufe und andere Threads allokieren wie bisher. Bei unerwartetem Code wird das Feature nicht aktiviert (fail-closed). Alle bekannten Abonnenten lesen das Objekt nur synchron.
- **Wirkung:** Allokation in `lerpChimps` sinkt auf ~1–3 MB/s, weniger GC-Spitzen.
- **Hinweis:** Das korrigiert den Extender; bei einer Übernahme gehört es nach APIShared.

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

## Offene Punkte

- **V7:** Sichtprüfung durch den Nutzer in verschiedenen Spielständen. Tests mit Atlas-Overrides, dem Foliage-Begleitplugin des AtlasBuilders und auf Grafikkarten mit wenig VRAM bzw. kleinerer maximaler Texturgröße.
- **Übernahme** von V1/V5/V9/V7 in Hauptmods: derzeit nicht vorgesehen. Bei einer Übernahme gehört V9 nach APIShared.
- **Ladephase** (unabhängig vom Zoom): `EngineInterface.LoadSaveFile` ~300 ms, `NoesisView.OnGUI` ~800 ms nach dem Laden, `FatControler.Update` ~2.4 s beim Wechsel vom Hauptmenü auf die Karte.

## Anhang

### Messgrundlage

- Spiel: Native `CrusaderDE.dll` `FBCB9319…31E2`, Managed `Assembly-CSharp.dll` `BC8B6A39…D789`, Unity 2021.3.45f2, URP 2D (SRP Batcher und Dynamic Batching an), Mono, D3D11 mit Render-Thread, Graphics Jobs aus, inkrementeller GC aktiv.
- Messrechner: RTX 4090, Ryzen 9 7950X3D, 1920×1080 Fullscreen-Window. Ausnahme: die HUD-/D3D12-Vergleichsläufe, beide Varianten bei 3840×2160.
- Installation: Script Extender 2.14.3, `PerfProbe` (DEVDIAG) und `PerfFixTest`; bewusst ohne APIShared und weitere Mods.
- Messzeitraum: 2026-10-20. Die Werte sind Momentaufnahmen für diese Versionen; bei neuen Spiel- oder Extender-Versionen neu messen.

### Messverfahren

- **Automatisierung** (`PerfFixTest`, `[Automation]`, standardmäßig aus): Hauptmenü → Spielstand laden → 80 Sim-Ticks → auf Zoom 0.25 → native Pause halten → laufen lassen → Spiel beenden. Die Frame-Statistik je Phase misst die Automatisierung selbst. Die Bereitschaft wird ohne APIShared erkannt, damit nichts das Ergebnis verfälscht.
- **Lauf-Skript** `Testmods/PerfFixTest/tools/Run-Automation.ps1`:
  - `-Label`, `-OutputDirectory`, `-Features "Schalter=Wert;…"`;
  - `-BootConfig "key=value;…"` (`boot.config` vorübergehend, byte-genau wiederhergestellt);
  - `-LaunchArgs` (Direktstart der exe, braucht vorübergehend `steam_appid.txt`).
  - Es sichert `LogOutput.log`, `Player.log` und Screenshots und setzt die Config danach zurück.
  - Vor jedem Start wartet es, bis der vorige Spielprozess samt Crash-Handler beendet ist.
- **Vergleiche im selben Lauf:** Proben unter `[Probes]` schalten in der Pause um und messen Fenster für Fenster, mit Kontrollfenstern und Screenshots vorher/nachher.
  - `SheetMergeMidPause`, `SheetMergeControl`, `SheetMergeRenderCheck`, `SheetMergeProbe`;
  - `LodProbe`, `QuadProbe`, `HudProbe`;
  - `NoesisProbe`, `NoesisSingleMip`, `NoesisRebindProbe`.

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
- **PerfFixTest** (`Testmods/PerfFixTest`):
  - Fixes unter `[Features]`: `RadarRebindSkip`, `MeritDataLockCache`, `InterpolateEventReuse`, `SheetMerge`.
  - Automatisierung unter `[Automation]`, Proben unter `[Probes]`.
  - Log-Marker: `AUTOMATION … phase=`, `framestats`, `probe=`, `v7`, `result PASS|FAIL`.
