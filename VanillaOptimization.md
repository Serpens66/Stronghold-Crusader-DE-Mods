# Vanilla-Optimierung SHCDE

Sammelliste der Vanilla-Performanceprobleme, die per Mod verbessert werden sollen. Zuerst werden alle Probleme erfasst und gemessen, erst danach wird optimiert.

## Arbeitsregeln

- Jeder Eintrag nennt Beleg, Kosten pro Frame oder Tick, betroffene Vanilla-Methoden, Risiko, MP-Relevanz und Status.
- Status: `vermutet` (nur statische Analyse), `gemessen` (PerfProbe-Beleg), `geplant`, `umgesetzt`, `verworfen`.
- **Keine akzeptierten Lösungen** (Nutzervorgabe): Zoomstufen oder Zoomgrenzen ändern bzw. sperren; FPS-Limit, VSync oder `targetFrameRate` ändern.
- Optimierungen müssen das Bild unverändert lassen. Ausnahmen werden einzeln mit dem Nutzer abgestimmt.
- Kompatibilität mit Script Extender und Fixes-Mod ist Pflicht; keine überlappenden Hooks.
- Messszenario (Nutzervorgabe): die Testspielstände `test_dusty*` bei 1920×1080, Zoom 1 → 0.5 → 0.25 je etwa 15 s. Der Zoom-Einbruch ist dort deutlich; größere Karten oder andere Situationen sind nicht nötig.
- Dieses Dokument enthält nur den aktuellen Stand. Messwerte sind Momentaufnahmen und gelten für die unten genannten Versionen; bei neuen Spiel- oder Extender-Versionen neu messen.

## Grundlage

- Spiel: Native `CrusaderDE.dll` `FBCB9319…31E2`, Managed `Assembly-CSharp.dll` `BC8B6A39…D789`, Unity 2021.3.45f2, URP 2D (`URP-2D`, SRP Batcher an, Dynamic Batching an, 1 Light2D), Mono-Backend, D3D11 mit eigenem Render-Thread, inkrementeller GC aktiv (`gc-max-time-slice=3` in `boot.config`).
- Messrechner: RTX 4090, Ryzen 9 7950X3D, 1920×1080 Fullscreen-Window.
- Messung: Lauf vom 2026-10-19 23:58 bis 2026-10-20 00:00 mit SHCDE-SE 2.14.3 und `Testmods/PerfProbe` (DEVDIAG-Build, nie weitergeben), Spielstand `test_dustynoattack.sav`, `tilemapSize=500`, Sim mit 40 Ticks/s. Sheet-Belegung und Statik-Anteil stammen aus dem Lauf vom 2026-10-20 00:23–00:25, Seitensimulation, Allokationszuordnung und Pausenvergleich aus dem Lauf vom 2026-10-20 00:42–00:44 (beide `test_dustyFPS.sav`, sonst gleich). PerfProbe misst inklusive seines eigenen Overheads.

## Architektur

- **Sim-Thread (nativ):** `Director.runSimTickTest` → `EngineInterface.run` → `DLL_RunTick` (RVA `0x86680`) → Kartenrenderer `0x41D60`. Dieser läuft bei jedem Tick über alle Kacheln im Sichtbereich und liefert einen Delta-Stream aus 21-short-Datensätzen. `EngineInterface.run` hält dabei `EngineInterface.threadLock` (`private static`), den 74 `lock`-Stellen in `EngineInterface` ebenfalls nutzen.
- **Main-Thread (verwaltet):**
  - `Director.Update` verarbeitet bis zu 3 Puffer pro Frame mit `GameMap.processTestMap`. Danach folgen `ApplyRadarMap` (Minimap), `experimentalRowManager` und `TilemapManager.endTileRefresh`.
  - `FatControler.NoesisGUIUpdateChecksInGame` läuft nach jedem Puffer, spätestens alle 100 ms.
  - `GameMap.Update` → `lerpChimps` interpoliert bei jedem Frame alle Einheiten.
- **Darstellung:**
  - Eine Unity-`Tilemap` pro Bildschirmzeile.
  - Objekte sind einzelne GameObjects mit `SpriteRenderer` (Pool 30 000 + 4 000); eine Einheit belegt bis zu 7 GameObjects.
  - Sortierformel `-20000 + Zeile*49 + lokaler Offset`; Einheitenteile liegen bei `nextBaseSortLayer + 8…14` (+34 bei `layerDelay`), Lebensbalken optional bei 32767.
- **Texturen und Materialien:**
  - Die Sprites liegen in großen Resources-Sheets (`body*Sprites`, `anims1Sprites`, `treeSprites`, `AllTiles/AllTileSprites`). Jedes Sheet hat eine Maske `<Sheet>_m` mit identischem Layout, die als `_TeamMask` **im Material** steckt.
  - `Unlit/TeamColour`: 7 Materialien pro Sheet (`_SpriteCutoff`-Staffel für abgeschnittene Füße).
  - `Unlit/Foliage`: 1 Material pro Sheet (GM 29–31, 70–72, 97).
  - Plain: ein gemeinsames `Sprite-Unlit-Default` für 69 der 195 GM-Gruppen, darunter GM 200.
  - Die Zuordnung aller 195 Gruppen steht maschinenlesbar in `Helpers/AtlasBuilder/atlas-contract SHOM/atlas_contract/loader_groups.json`.
- **Gemessene Sheets:**
  - TeamColour: `body1/2/5/8/12/13/14/15/16Sprites` je 8192×8192 BC7 (Bilinear), `anims1Sprites` 8192×4096 BC7. Masken gleich groß in DXT1 (Point).
  - Foliage: `treeSprites` 4096×8192 BC7, Maske 1024×2048 DXT1.
  - Plain: `body4Sprites` und `body7Sprites` 8192×8192 BC7.
  - Alle ohne Mipmaps und nicht lesbar.

## Messstand

| Messgröße | Zoom ~1 | Zoom 0.25 |
|---|---|---|
| FPS / Frame | 293 / 3.4 ms (FPS-Limit 300) | **122 / 8.2 ms** |
| `PlayerLoop` (Unity-Main-Thread) | 3.4 ms | 8.5 ms |
| Spielskripte gesamt (alle Unity-Callbacks) | 0.69 ms | **2.6 ms** |
| davon `Director.Update` | 0.30 ms | 1.46 ms |
| … `processTestMap` | 0.03 ms | 0.61 ms |
| … `ApplyRadarMap` (davon `TestRadarMap`) | 0.13 (0.12) ms | 0.46 (**0.44**) ms |
| davon `GameMap.Update` (`lerpChimps`) | 0.09 ms | **0.61 ms** |
| davon `NoesisView.LateUpdate` / `MainControls.Update` / `EditorDirector.Update` | 0.08 / 0.14 / 0.06 ms | 0.24 / 0.16 / 0.12 ms |
| Main wartet auf Render-Thread (`Gfx.WaitForPresentOnGfxThread`) | 0.06 ms | 0.96 ms |
| GC (`GC.Collect`, inkrementell) | – | 0.37 ms |
| Rest: Unity-Engine auf dem Main-Thread (Culling, Sortierung, Batching, Submit) | ~2.6 ms | **~4.5 ms** |
| Draw Calls / SetPass | 346 / 560 | **1 752 / 2 342** |
| Vertex-Buffer-Uploads pro Frame | 345 (0.75 MB) | **1 769 (4.3 MB)** |
| SpriteRenderer sichtbar / Zeilen-Tilemaps sichtbar | ~1 000 / 107 | 3 495 / 295 |
| Einheiten in `lerpChimps` / davon interpolierend | 671 / 118 | 2 872 / 847 |
| Sim `EngineInterface.run` pro Tick | 3.7 ms | 6.6 ms |

**Befund bei Zoom 0.25:**
- Der Engpass ist der Unity-Main-Thread. Die GPU ist nicht der Engpass.
- Etwa 55 % des Frames sind Unity-Engine-Arbeit für ~3 500 SpriteRenderer und ~300 Tilemaps, 32 % sind Spielskripte, 12 % Warten auf den Render-Thread. Der Sim-Thread begrenzt die FPS nicht.
- Pausiert (keine Interpolation, kaum Datensätze) liegt die FPS bei Zoom 0.25 mit ~140 auf demselben Niveau wie im laufenden Spiel. Die Kosten des Herauszoomens entstehen also durch die Darstellung der vielen sichtbaren Objekte und durch die Kosten pro Frame bzw. pro Einheit, nicht durch die Simulation oder die Verarbeitung der Datensätze.

**Ursachen der Draw Calls (Census bei Zoom 0.25, Schätzung 1 792 ≈ gemessen 1 752):**

| Bruchursache zwischen sortierten Nachbarn | Anzahl | Anteil |
|---|---|---|
| anderes Material derselben Klasse (TeamColour aus einem anderen Sheet mit eigener Maske) | **1 113** | 62 % |
| Tilemap-Zeile (eigener Renderer, ~2 Brüche pro sichtbarer Zeile) | 538 | 30 % |
| andere Materialklasse (Plain ↔ TeamColour ↔ Foliage, Cutoff-Stufe) | 132 | 7 % |
| andere Textur bei gleichem Material (Plain-Sheets) | 8 | <1 % |

- Wenn alle Sheets und Masken einer Materialklasse ein gemeinsames Material hätten, blieben ~670 Batches (−62 %).
- Ein Atlas nur der Plain-Texturen bringt nichts.

## Kandidaten und Vorschläge

Alle Vorschläge sind verwaltete Harmony-Eingriffe, nur clientseitig (`NetworkMode=0`) und ohne native Hooks. Reihenfolge nach Aufwand und Risiko. Jede Umsetzung entsteht zuerst als Testmod. Erst nach bestätigter Wirkung kommt eine Übernahme in einen Hauptmod bzw. APIShared in Frage (Nutzervorgabe).

**Testmod `Testmods/PerfFixTest`** (normaler Build, kein DEVDIAG) enthält V1, V5 und V9. Jedes Feature ist in der BepInEx-Config unter `[Features]` einzeln schaltbar (`RadarRebindSkip`, `MeritDataLockCache`, `InterpolateEventReuse`; Neustart nötig).
- Gemessen wird zusammen mit PerfProbe im üblichen Szenario.
- Für jeden Vorschlag gibt es einen Bestätigungsmarker im Log: `V1 confirmed`, `V5 served cached merit data`, `V9 confirmed`. Fehlschläge erscheinen als `… disabled: …`.
- Für A/B-Vergleiche ein Feature abschalten und denselben Lauf wiederholen.

**Testergebnis `PerfFixTest` (Lauf 2026-10-20 01:04–01:06, `test_dustyFPS`, mit PerfProbe):**

| Vorschlag | Marker | Wirkung (Zoom 0.25, gegenüber den Läufen ohne Testmod) |
|---|---|---|
| V1 | `V1 confirmed` nach dem ersten Binden; danach keine weiteren Neubindungen | `ApplyRadarMap` 4–5 ms/s statt 56–150 ms/s (≈0.03 statt 0.4–1.1 ms pro Frame); Allokation 0.1–0.16 statt 1.4–5 MB/s |
| V5 | `V5 served cached merit data` einmal während eines 483-ms-Sim-Ticks (01:05:14) | **Kein Main-Thread-Spike, kein Frame-Spike**; das Bild lief weiter (97 Frames während des Ticks) |
| V9 | `V9 confirmed`, ~400 000 wiederverwendete Events pro Sekunde, 0 verschachtelte Allokationen | Allokation in `lerpChimps` 0.9–2.7 statt 10–14 MB/s; Heap-Wachstum gesamt 5–15 statt 30–45 MB/s; der `GC.Collect`-Marker liegt unter der Messschwelle (vorher 0.37–0.53 ms pro Frame); `lerpChimps` nur leicht schneller (pausiert 0.24 statt 0.27 ms pro Frame) |

- Fehlermeldungen gab es keine.
- **A/B-Vergleich (automatisiert, 2026-10-20 01:41/01:47):** Die Automatisierung in `PerfFixTest/src/Automation` lädt `test_dustyFPS`, zoomt nach 80 Sim-Ticks in 6 Schritten auf 0.25, pausiert 25 s (identische Szene: 505 Draw Calls, 1 131 sichtbare Sprites in beiden Läufen) und lässt danach 25 s laufen. Beide Läufe endeten mit PASS. Werte als Mittel der 5-s-Fenster, Zoom 0.25, bei diesem Kameraausschnitt mit weniger Objekten als in den manuellen Läufen:

| Messgröße | A (V1+V5+V9 an), pausiert / laufend | B (alle aus), pausiert / laufend | Differenz |
|---|---|---|---|
| FPS | **202.5 / 209.5** | 197.3 / 193.8 | **+2.7 % / +8.1 %** (−0.13 / −0.39 ms pro Frame) |
| `Director.Update` | 32 / 65 ms/s | 153 / 162 ms/s | −121 / −97 ms/s |
| davon `ApplyRadarMap` (V1) | 5 / 5 ms/s | 127 / 97 ms/s | −0.6 / −0.45 ms pro Frame |
| `lerpChimps` (V9) | 15 / 36 ms/s | 21 / 39 ms/s | −6 / −3 ms/s |
| Heap-Wachstum | 5.6 / 9.9 MB/s | 22.5 / 23.4 MB/s | **−75 % / −58 %** |
| GC-Sammlungen je 5 s | 4.0 / 4.75 | 4.75 / 5.0 | leicht weniger |
| Sim-Spike ~4 s nach dem Entpausieren | 486 ms; `V5 served cached merit data`, kein Main-Thread-Spike | 441 ms; zufällig kein `GetMeritData`-Aufruf währenddessen | V5-Wirkung wie in den manuellen Läufen |

- Die gesparte Skriptzeit (~0.6 ms pro Frame pausiert) wirkt sich nur teilweise auf die Frame-Zeit aus (−0.13 ms). Bei ~200 FPS wartet der Main-Thread einen Teil der Zeit auf den Render-Thread (`Gfx.WaitForPresentOnGfxThread`). Im laufenden Spiel ist der Gewinn größer (+8 %).
- Der ~450-ms-Sim-Tick trat in beiden Läufen ~4 s nach dem Entpausieren bzw. ~30 s nach dem Laden auf, unabhängig vom Zoomzeitpunkt. Er hängt also vermutlich nicht am Zoom; die Ursache bleibt nativ offen.
- Sichtprüfung durch den Nutzer bestanden (2026-10-20): Minimap läuft weiter, Bündnisanzeige normal, Einheiten normal dargestellt.

### V1 – Minimap-Bild nicht bei jedem Tick neu an Noesis binden — im Testmod bestätigt

- **Beleg:**
  - `MainViewModel.TestRadarMap` kostet 0.87 ms (Zoom 1) bis 1.9 ms (Zoom 0.25) pro Aufruf, im pausierten Spiel bis 3.8 ms. Es wird 40-mal pro Sekunde aufgerufen, auch pausiert.
  - Das sind 0.44–1.1 ms pro Frame bei Zoom 0.25 und praktisch die gesamten Kosten von `ApplyRadarMap`. Der eigentliche Textur-Upload (`SetPixelData` + `Apply`) ist dagegen billig.
  - `TestRadarMap` allokiert dabei 1.4–5 MB/s, also bis ~110 KB pro Aufruf.
- **Ursache:** `TestRadarMap` erzeugt jedes Mal ein neues `Noesis.TextureSource` für dieselbe `radarTexture` und setzt `RadarMapImage` neu. Noesis bindet das Bild dadurch neu und es wird Speicher allokiert.
- **Vorschlag:** Prefix auf `TestRadarMap`, der den Aufruf überspringt, solange `GameMap.getRadarTexture()` dieselbe Instanz wie beim letzten Binden ist und `RadarMapImage` gesetzt ist. Bei einer neuen Texturinstanz (Kartenwechsel, Rotation, Auflösungswechsel; `GameMap` Z. 496/579) wird normal gebunden.
- **Erwartung:** −0.4 bis −1.1 ms pro Frame bei Zoom 0.25 und bis zu 5 MB/s weniger Allokation.
- **Zu prüfen im Spiel:** Die Minimap muss sich weiter aktualisieren. `Apply()` schreibt in dieselbe GPU-Textur, die das bestehende `TextureSource` umhüllt.
- **Risiko:** gering.

### V2 – `lerpChimps` entschlacken — gemessen

- **Beleg:**
  - `GameMap.Update` kostet 0.61 ms pro Frame bei Zoom 0.25. Die Schleife besucht ~3 000 Einheiten, nur ~900 davon interpolieren.
  - Im pausierten Spiel interpoliert keine Einheit, und trotzdem bleiben 0.27 ms pro Frame. Dieser Sockel stammt größtenteils vom Script-Extender-Detour pro Einheit (siehe V9), nicht von Vanilla.
- **Vorschlag:** inhaltsgleicher Ersatz der Schleife.
  - `DateTime.UtcNow` einmal pro Frame statt pro Einheit.
  - Nicht interpolierende Einheiten ohne weitere Arbeit überspringen.
  - Transform-Positionen und `sortingOrder` nur schreiben, wenn sich der Wert ändert.
- **Erwartung:** ein Teil der ~0.34 ms, die über den Sockel hinaus auf die interpolierenden Einheiten entfallen; dazu kommt der Anteil der Unity-Engine durch weniger geänderte Transforms. Der Sockel selbst wird über V9 behoben.
- **Kompatibilität:** Der Script Extender detourt `GameMap.interpChimp` (RedBird `ManagedDetour`). Ein Ersatz der Schleife muss weiterhin `interpChimp` je Einheit aufrufen, damit dieser Detour und das Extender-Event erhalten bleiben. `interpChimp` selbst darf nicht zusätzlich gehookt werden.
- **Risiko:** gering bis mittel; Positionen und Sortierung müssen bitgleich bleiben.

### V3 – `processTestMap`: wiederholte `GetComponent`-Aufrufe vermeiden — gemessen

- **Beleg:**
  - `processTestMap` kostet 0.61 ms pro Frame bei Zoom 0.25.
  - `addUpdateChimp` läuft 23 500-mal pro Sekunde zu je ~2 µs (49 ms/s).
  - Datensätze kommen mit ~40 000/s (Einheiten Typ 2 und Typ 52 dominieren).
- **Vorschlag:** `SpriteRenderer` pro Pool-Objekt einmalig cachen und die Vanilla-Aufrufe (`GetComponent<SpriteRenderer>()` an 23 Stellen und in `ObjectPool.GetObjectForType`) per Transpiler auf den Cache umleiten.
- **Erwartung:** 0.05–0.15 ms pro Frame bei Zoom 0.25.
- **Risiko:** gering.

### V4 – Feste Kosten pro Frame — gemessen

- **Beleg:** `NoesisView.LateUpdate` 0.24 ms, `MainControls.Update` 0.16 ms, `EditorDirector.Update` 0.12 ms pro Frame. `EditorDirector.Update` läuft auch im normalen Spiel (`updateFPS`, Maus- und Tile-Abfragen über `MainControls`).
- **Vorschlag:** doppelte Maus- und Tile-Berechnungen einmal pro Frame cachen. Vorher einen Audit, wer die Ergebnisse liest.
- **Erwartung:** 0.1–0.2 ms pro Frame.
- **Risiko:** gering bis mittel.

### V5 – Freeze durch Warten auf `EngineInterface.threadLock` — im Testmod bestätigt

- **Beleg:** In drei Messläufen dauerte beim ersten Herauszoomen ein Sim-Tick 452–463 ms. Währenddessen wartete der Main-Thread 260–381 ms in `EngineInterface.GetMeritData`, aufgerufen aus `FatControler.NoesisGUIUpdateChecksInGame`. Die Frames dauerten entsprechend 263–385 ms. Ein solcher Sim-Spike tritt beim ersten Herauszoomen bzw. kurz nach dem Laden auf.
- **Audit `GetMeritData`** (Managed `BC8B6A39`):
  - `public static int[,] GetMeritData()` legt bei jedem Aufruf ein neues `int[45]` an. Es ruft unter `lock (threadLock)` `DLL_GetMeritData(int*)` auf und kopiert die Spieler 1–8 mit je 5 Werten in ein **neues** `int[9,5]`. Es gibt keinen geteilten Zustand; jeder Aufrufer erhält ein eigenes Array.
  - Aufrufer, alle nur lesend:
    - `HUD_AlliesPanel.ShowAlliesPip` (aus dem GUI-Check, höchstens 1-mal pro Sekunde, nur im Skirmish bei geschlossenem Bündnisfenster) — das ist der beobachtete Freeze-Pfad;
    - `HUD_AlliesPanel.Open` und `UpdateAllies` (alle 2 s bei offenem Fenster);
    - `HUD_MeritPanel` (Rangliste, höchstens 1-mal pro Sekunde).
  - Die Werte (Rang bzw. Bündnisstatus pro Spieler) ändern sich langsam. Ein um einen Tick älterer Stand ist für alle Aufrufer fachlich gleichwertig.
- **Vorschlag:** Prefix auf `EngineInterface.GetMeritData`.
  - Ist `threadLock` frei, läuft das Original unverändert, und ein Postfix speichert eine Kopie des Ergebnisses.
  - Ist `threadLock` belegt (`Monitor.TryEnter` ohne Wartezeit) und eine Kopie vorhanden, wird ein frischer Klon der letzten Kopie zurückgegeben, statt zu blockieren. Ohne Kopie (erster Aufruf) blockiert das Original wie bisher.
  - Die Kopie wird bei Kartenwechsel bzw. Missionsende verworfen.
  - Zugriff auf `threadLock` einmalig per Reflection mit Typ- und Namensprüfung; fehlt das Feld, bleibt der Vorschlag inaktiv.
  - Restfenster: Wird der Lock zwischen Prüfung und Original belegt, wartet der Aufruf wie in Vanilla. Das ist selten und nie schlechter als heute.
- **Erwartung:** kein sichtbarer Freeze bei Sim-Spikes über diesen Pfad.
- **Risiko:** gering. Andere `EngineInterface`-Aufrufe aus dem GUI-Check (z. B. `GameAction`, gemessen 4 ms während eines Ticks) können denselben Effekt haben. Für reine Lesefunktionen gilt dasselbe Muster; Aktionen wie `GameAction` dürfen nicht zwischengespeichert werden.
- **Offen:** Die Ursache des ~450-ms-Sim-Ticks selbst liegt im nativen Code.

### V6 – Speicherallokation und GC — gemessen, Quellen bestätigt

- **Beleg:**
  - Bei Zoom 0.25 laufen etwa 1.2 inkrementelle Vollsammlungen pro Sekunde (Boehm, gen0 = gen1 = gen2), im Mittel 0.37–0.53 ms pro Frame.
  - Der Mono-Heap umfasst ~570–650 MB; davon sind ~200 MB die 6 Renderpuffer von `MemoryBuffers` (je 14.7 Mio. `short`).
  - Das Heap-Wachstum liegt prozessweit bei 20–45 MB/s.
- **Messung:**
  - Ein threadgenauer Zähler steht nicht zur Verfügung. `GC.GetAllocatedBytesForCurrentThread` existiert in Unitys mscorlib, liefert unter dem Boehm-GC aber 0.
  - Gemessen wird das Heap-Wachstum während jedes Callbacks. Proben mit GC oder mit überlappendem Sim-Tick werden verworfen; die übrigen 67–100 % der Laufzeit werden hochgerechnet.
- **Quellen:**
  1. **Script-Extender-Detour auf `interpChimp`: 10–14 MB/s** (siehe V9). Die Allokation fällt in `GameMap.lerpChimps`, auch im pausierten Spiel ohne Interpolation und ohne Datensätze, und auch bei Zoom 1. Sie skaliert mit der Zahl der Einheiten im `chimps`-Dictionary, nicht mit dem Zoom.
  2. **`MainViewModel.TestRadarMap`: 1.4–5 MB/s** (neues `TextureSource` pro Tick, siehe V1).
  3. **`processTestMap`:** 0.6–2.8 MB/s, abhängig von der Zahl der Datensätze.
  4. **Kleinere Posten:** `MainControls.Update`, `NoesisView.LateUpdate` und `EditorDirector.Update` mit je 0.3–1.4 MB/s.
- **Vorschlag:** V9 und V1 beseitigen die beiden größten Quellen (zusammen ~70 % des gemessenen Wachstums). `processTestMap` lässt sich zusammen mit V3 näher eingrenzen.

### V7 – Ein Material pro Materialklasse statt pro Sheet — gemessen, Gewinn simuliert

- **Beleg:** Wechsel zwischen TeamColour-Materialien verschiedener Sheets verursachen 62 % der Draw Calls bei Zoom 0.25. Mit einem gemeinsamen Material pro Klasse blieben ~670 statt ~1 790 Batches.
- **Vorschlag:**
  - Farb- und Masken-Sheets einer Materialklasse auf gemeinsame Seiten kopieren, GPU-seitig per `Graphics.CopyTexture` mit gleichem Format (BC7/DXT1) und ganzen Rechtecken.
  - Pro Seite und Cutoff-Stufe ein Material, für Foliage und Plain je eins. `gmMaterials` und die Sprite-Referenzen (`allSprites`, `gmSprites`, `gmAltSprites`, Tile-Sprites) werden umgestellt.
  - Neue Sprites per `Sprite.Create` mit gleichem Rect, Pivot und PPU; danach `Sprite.OverrideGeometry` mit den Original-`vertices`/`triangles`, weil Vanilla-Sprites teils Tight-Mesh sind.
  - Eingriff nach `SHCDESE.API.GameSpriteManagerAPI.ApplyRuntimeOverrides`, damit Sprites aus Asset-Mods berücksichtigt werden. Diese Stelle nutzt bereits das AtlasBuilder-Begleitplugin.
- **Belegung der Sheets** (aus `spriteLoader`, einmal pro Prozess):
  - Alle Sheets sind voll. Die summierte Rect-Fläche liegt bei 94–130 % der Sheetfläche. Werte über 100 % entstehen, weil sich die Rects der Tight-Mesh-Sprites überlappen; ihre sichtbaren Dreiecke sind ineinander verschachtelt.
  - Tight-Mesh-Anteil: z. B. body1 14 221 von 17 060 Sprites, body12 4 928 von 4 928. Nur AllTileSprites und treeSprites sind rechteckig.
  - Ein dichteres Umpacken einzelner Sprites ist daher nicht möglich. Nur ganze Sheets lassen sich zusammenlegen.
- **Harte Grenze:**
  - Die TeamColour-Klasse umfasst laut `gmMaterials` 13 Sheets (`body1/2/4/5/6/7/8/12/13/14/15/16Sprites` je 8192², `anims1Sprites` 8192×4096), zusammen 3.13 Seiten à 16384² (D3D11-Maximum).
  - Mit 4 ganzen Sheets pro Seite sind **mindestens 4 Seiten** nötig. Aus 13 Materialien pro Cutoff-Stufe würden also 4.
  - Weil die Sheets als Ganzes kopiert werden, bleibt jede Sprite-Geometrie erhalten (gleiche Dreiecke, Rect nur verschoben).
- **Simulierter Gewinn (Census mit Seitenzuordnung, Zoom 0.25):**
  - Die Sheets der Maskenklassen werden als Ganzes auf 16384²-Seiten verteilt. Sheets, die in Sortierreihenfolge oft aufeinander folgen, kommen zusammen.
  - Ergebnis: **~810–880 statt 1 370–1 500 Batches (−41 %)**. Mit mehr sichtbaren Einheiten sind es ~820–845 statt ~1 950 (−58 %).
  - Es bleiben ~100 sheetbedingte Brüche zwischen Seiten. Der Rest sind Tilemap-Grenzen (~540) und Wechsel der Materialklasse (~170–215).
  - Gefundene Zuordnung: `body12+body14+body16+body13` (4) ; `anims1+body2+body1+body8` (3.5) ; `body7+body5+body4` (3) ; `body15` (1) ; `treeSprites` getrennt (Foliage). `body15` passt noch auf die dritte Seite, also genügen 4 TeamColour-Seiten.
  - Die Zuordnung entsteht aus denselben Daten, das Ergebnis ist daher eher optimistisch.
  - Erwartung für die Engine-Zeit: Draw Calls und Vertex-Uploads sinken in ähnlicher Größe; das betrifft einen großen Teil der ~4.5 ms Engine-Arbeit bei Zoom 0.25.
- **Speicher:**
  - Die TeamColour-Sheets belegen schon heute ~1.25 GB VRAM (13 × BC7 8192² à 64 MB plus 13 Masken DXT1 à 32 MB).
  - 4 neue Seiten kosten ~1.5 GB (je 256 MB BC7 + 128 MB DXT1).
  - Ohne Freigabe der Originale (`Resources.UnloadAsset`, erst wenn keine Referenz mehr besteht) verdoppelt sich der Bedarf. Für Grafikkarten mit wenig VRAM ist das kritisch.
- **Kompatibilität:**
  - Asset-Mods registrieren pro GM-Gruppe eigene Texturen und Materialien; ein globaler Atlas wird vom Extender nicht unterstützt.
  - Indexverträge bleiben unangetastet: geteiltes Array `tile_sea_new_01`/`tile_sea_shore` über `ID_Offset`/`additionalStorage`, Teilatlanten. Es werden nur Referenzen ersetzt, nie Arraylängen oder Indizes.
  - Kompatibilität mit dem Fixes-Mod und dem Foliage-Begleitplugin, das ebenfalls `gmMaterials` schreibt.
- **Risiko:** hoch.

### V9 – Script-Extender-Detour auf `interpChimp` ohne Allokation pro Einheit — im Testmod bestätigt

- **Befund** (Script Extender v2.14.3, `src/SHCDESE.BepInEx/ManagedHooks/GameMap_Hooks.cs`):
  - `ManagedHookManager.GameMap_InterpChimpHook` ist als RedBird-`ManagedDetour` vor `GameMap.interpChimp` geschaltet. Bei **jedem** Aufruf, also für jede Einheit im `chimps`-Dictionary und jeden Frame, erzeugt er ein neues `UnitUnityVisualInterpolateEventArgs` (Klasse) und ruft `UnitR3EventHooks.OnUnitUnityVisualInterpolate.Raise` auf, ein R3-`Subject.OnNext`. Erst danach folgt das Vanilla-`interpChimp`.
  - Das passiert auch für Einheiten, die nicht interpolieren; Vanilla tut für sie nichts.
  - Einziger Abonnent im Extender ist `GameAfterImageManager.UpdateEffectForChimp` (Nachbild-Effekte); er macht pro Einheit einen Dictionary-Lookup und kehrt ohne aktiven Effekt sofort zurück.
- **Beleg:**
  - Im pausierten Spiel (0 interpolierende Einheiten, ~3 100 Einheiten im Dictionary, ~145 FPS, also ~450 000 Aufrufe/s) kostet `lerpChimps` weiterhin 0.27 ms pro Frame und allokiert 10–14 MB/s. Das sind ~25–30 Byte pro Aufruf, passend zu einem kleinen Event-Objekt.
  - Mit Interpolation bei Zoom 0.25 sind es 0.61 ms pro Frame.
- **Vorschlag** (Korrektur des Extenders gehört laut Workspace-Regeln nach APIShared):
  - Den Extender-Hook so ersetzen, dass pro Aufruf **kein** neues Objekt entsteht.
  - Option A: ein wiederverwendetes Event-Objekt pro Thread.
  - Option B: das Event nur auslösen, wenn tatsächlich ein Abonnent etwas damit tun kann. Das ist der Fall, wenn ein Nachbild-Effekt für diese Einheit aktiv ist oder es Abonnenten außerhalb des Extenders gibt.
  - Wie bisher das Vanilla-`interpChimp` über die Trampoline aufrufen. Kein zusätzlicher Hook auf `interpChimp` selbst (überlappende Detours vermeiden); der Eingriff erfolgt an der Extender-Hookmethode.
  - Vorher Audit:
    - welche Mods `OnUnitUnityVisualInterpolate` abonnieren (Workspace, Fixes-Mod, bekannte Extender-Nutzer);
    - ob ein Abonnent das Event-Objekt über den Aufruf hinaus speichert (dann scheidet Option A aus);
    - Thread-Herkunft (Main-Thread).
- **Erwartung:** bis zu −10…14 MB/s Allokation (~1/3 des Heap-Wachstums) mit entsprechend selteneren GC-Läufen. Dazu ein Teil der 0.27 ms Sockelkosten pro Frame bei ~3 000 Einheiten.
- **Risiko:** gering bis mittel, abhängig vom Audit der Abonnenten. Rein clientseitige Darstellung, keine Netzwerkrelevanz.

### Weitere gemessene Punkte mit geringem Nutzen

- **Nicht sichtbare aktive Renderer:** Bei Zoom 0.25 sind 3 937 SpriteRenderer aktiv, 3 495 sichtbar (~11 % unsichtbar aktiv). `ObjectPool.filterRows` blendet nur Zeilengruppen aus, keine Spalten. Eine spaltenweise Deaktivierung lohnt sich nur, wenn eine Messung den Culling-Anteil bestätigt.
- **Spikes beim Zoomwechsel in `TilemapManager.endTileRefresh`** (bis ~50 ms beim Nachladen neu sichtbarer Zeilen): Die Arbeit ließe sich auf mehrere Frames verteilen.
- **Native Sichtbereichsschleife:** 3.7 → 6.6 ms pro Tick beim Herauszoomen, der Sim-Thread ist zu ~26 % ausgelastet. Das begrenzt die FPS nicht und bleibt unangetastet.

## Ausgeschlossen bzw. als keine gute Lösung bestätigt

- **Zoom, FPS-Limit, VSync ändern:** Nutzervorgabe.
- **Unveränderte Minimap-Uploads überspringen:** Kein Upload war identisch (0 von ~200 je 5 s). Der Upload selbst ist ohnehin billig; die Kosten liegen in `TestRadarMap` (V1).
- **Atlas nur der Plain-Texturen:** nur 8 von ~1 790 Brüchen entfallen.
- **Zeilen-Tilemaps zusammenlegen:** Die Tiefensortierung zwischen Kacheln und Einheiten hängt an der Zeilen-Tilemap; ein Zusammenlegen würde das Bild ändern. Diese ~30 % der Brüche sind die Untergrenze.
- **Inkrementellen GC aktivieren:** bereits aktiv.
- **Statische SpriteRenderer als Mesh pro Zeile zusammenfassen:** Von den sichtbaren SpriteRenderern sind nach 5 s nur 0–8 % unverändert. Bei TeamColour sind es höchstens 2 %, bei Plain allerdings ~85 %, das sind aber nur ~130 Renderer. Statische Inhalte wie Gebäude und Boden liegen bereits in den Zeilen-Tilemaps; die SpriteRenderer sind fast nur bewegte bzw. animierte Objekte. Ein Zeilen-Mesh für statische Sprites lohnt sich daher nicht.
- **Sprites innerhalb der Sheets dichter packen:** Die Sheets sind voll und die Tight-Mesh-Rects überlappen sich (siehe V7).
- **Native Sim-/Renderschleife umschreiben:** zu riskant, begrenzt die FPS nicht.

## Außerhalb des Zoomproblems

Ladephase: `EngineInterface.LoadSaveFile` 260–310 ms, `NoesisView.OnGUI` 730–800 ms nach dem Laden eines Spielstands, `FatControler.Update` ~2.3 s beim Wechsel vom Hauptmenü auf die Karte.

## Offene Punkte vor der Umsetzung

Die Messphase ist abgeschlossen. Vor der Umsetzung stehen noch diese statischen Audits bzw. Prüfungen aus:

| Punkt | Offene Frage | Art |
|---|---|---|
| **V1** | Zeigt Noesis die per `Apply()` aktualisierte Radartextur ohne neues `TextureSource` an? | Laufzeittest `PerfFixTest` (Minimap muss sich weiter bewegen) |
| **V2** | Liefert der Schleifenersatz bitgleiche Positionen und Sortierungen, bei weiterem Aufruf des Extender-Detours? | Audit von `interpChimp` vollständig, dann Laufzeittest |
| **V4** | Wer liest die in `EditorDirector.Update`/`MainControls.Update` doppelt berechneten Maus- und Tile-Werte? | statischer Audit |
| **V5** | Wirkung des Lock-Caches (Spike-Zeilen `main:EngineInterface.GetMeritData` dürfen nicht mehr erscheinen); Ursache des ~450-ms-Sim-Ticks beim ersten Herauszoomen bzw. nach dem Laden | native Diagnose (nur für die Ursache; V5 selbst braucht sie nicht) |
| **V7** | Freigabe der Original-Sheets (VRAM), Verhalten mit Asset-Mods und dem Foliage-Begleitplugin, Kopierbarkeit per `Graphics.CopyTexture` bei BC7/DXT1 | Prototyp im DEVDIAG-Testmod |
| **V9** | Abonnenten-Audit abgeschlossen: Extender (Nachbilder), ExtraFeatures (KnightDismount), ImprovedHunters, SkinTest, VirtualUnitsPrototype; Fixes-Mod keiner. Alle lesen `args.Chimp` nur synchron; keiner speichert das Objekt. Offen ist nur der Laufzeittest im Testmod. | Laufzeittest `PerfFixTest` |

## Workspace-Wissen zu Texturen

Quellen: `Guides/Replace Assets with Script Extender.md`, `Helpers/AtlasBuilder/USAGE.md`, `Helpers/AtlasBuilder/atlas-contract SHOM/*`, CastlePlanner.

- Farb- und Maskentextur müssen pixelgenau dasselbe Layout haben. Ein gemeinsamer Atlas ist nur zusammen mit einem gemeinsamen Maskenatlas batchbar.
- Script-Extender-Atlanten: Point-Filter, Clamp, keine Mips, `SpriteMeshType.FullRect`, pro GM-Gruppe eine eigene Textur und ein eigenes Material. Jeder Asset-Mod erhöht damit die Zahl der Materialien und so die Batch-Brüche.
- Indexfallen: `tile_sea_new_01`/`tile_sea_shore` teilen ein Array (`ID_Offset`/`additionalStorage`); Teilatlanten erhalten nicht ersetzte Vanilla-Frames.
- Pivots sind normalisiert, 1.0 ist eine Kantenkonvention, Werte außerhalb von 0..1 kommen vor. Bei 1:1 übernommenem Rect, Pivot und Geometrie unkritisch.
- Native-Baseline: Die nativen Datensätze liefern nur GM-Datei- und Bildindex. Alle Textur- und Materialentscheidungen fallen verwaltet in `SpriteMapping`/`spriteLoader`.

## Messwerkzeug PerfProbe

`Testmods/PerfProbe`, nur `build.bat /devdiag`. Alle Messbereiche sind in der BepInEx-Config unter `[Extras]` bzw. `[Spikes]` schaltbar. Ausgabe alle 5 s im BepInEx-Log:

| Logzeile | Inhalt |
|---|---|
| `perf:` | Zeiten der Vanilla-Kernmethoden, Sim-Tick, Datensätze, `addUpdate*`, `TestRadarMap` |
| `snapshot` | Zoom, Tilemaps, Pool-Sprites, Draw Calls, SetPass, Vertices, Main-Thread-Zeit |
| `engine` | `PlayerLoop`, `Gfx.WaitForPresentOnGfxThread`, `GC.Collect`, Render-Counter |
| `census` | Renderer, Materialien, Materialklassen, Sheets, Bruchursachen, Atlas-Szenarien |
| `pageSim` | V7-Szenario: verbleibende Batches bei Zusammenlegung ganzer Sheets auf 16384²-Seiten, mit Seitenzuordnung |
| `textures new`, `materialClasses` | einmaliges Inventar der Sheets und Masken |
| `sweep`, `sweepAlloc` | alle Unity-Callbacks der Spielskripte und die verschachtelten Kernmethoden (`lerpChimps`, `processTestMap`, `ApplyRadarMap`, `TestRadarMap`, GUI-Check, `endTileRefresh`, `experimentalRowManager`) mit Zeit und Heap-Wachstum aus sauberen Proben |
| `threadAlloc selfTest` | prüft den threadgenauen Zähler (unter Boehm ungültig) |
| `sheets:`, `sheetClasses` | Belegung der Sprite-Sheets und Seitenbedarf je Materialklasse (einmal pro Prozess) |
| `static` | Anteil sichtbarer Sprites, die nach 5 s unverändert sind |
| `engineCalls(main)` | `EngineInterface`-Aufrufe auf dem Main-Thread, Anteil während eines Sim-Ticks |
| `chimps`, `records byType`, `gc` | Einheiten, Datensatzarten, GC und Heap |
| `spike:` | einzelne Ruckler über 200 ms mit Kontext (blockierender Aufruf, Top-Callbacks, GC) |
