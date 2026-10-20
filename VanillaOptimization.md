# Vanilla-Optimierung SHCDE

Sammelliste der Vanilla-Performanceprobleme beim Herauszoomen und der Optimierungen, die per Mod umsetzbar sind. Jede Optimierung entsteht zuerst im Testmod `Testmods/PerfFixTest`. Eine Übernahme in einen Hauptmod bzw. APIShared kommt erst nach bestätigter Wirkung in Frage (Nutzervorgabe).

## Ergebnis auf einen Blick

| Optimierung | Wirkung (schwere Szene, Zoom 0.25) | Bild | Status |
|---|---|---|---|
| V1 Minimap nicht neu an Noesis binden | −0.5 bis −1.4 ms Skriptzeit pro Frame, deutlich gleichmäßigere Frames | unverändert (Sichtprüfung) | im Testmod bestätigt |
| V5 Lock-Cache für `GetMeritData` | kein Freeze mehr bei langen Sim-Ticks (vorher 85–380 ms) | unverändert | im Testmod bestätigt |
| V9 Extender-Event ohne Allokation pro Einheit | −10…14 MB/s Allokation, weniger GC | unverändert | im Testmod bestätigt |
| **V1+V5+V9 zusammen** | **+8.4 % FPS pausiert, +11.9 % laufend, p95-Frame-Zeit −30 %** | unverändert | im Testmod bestätigt |
| **V7 Sheets pro Materialklasse zusammenlegen** | **+23–25 % FPS** zusätzlich zu V1+V5+V9 | Pixelvergleich ohne sichtbare Abweichung | im Testmod wirksam, **Sichtprüfung durch den Nutzer offen** |
| Nativer Sim-Spike (KI-Gruppenbefehl mit Wegsuche) | – | – | Ursache bestimmt, Optimierung verworfen |
| V10 LOD (Details bei weitem Zoom ausblenden) | höchstens im Messrauschen (≤ 3 %) | würde das Bild ändern | gemessen, verworfen |
| V11 Einheiten-Sprites als 4-Eck-Rechteck statt Polygon | Obergrenze +2–5 % | Obergrenze mit Fragmenten | gemessen, verworfen |
| V2, V3, V4, V8 und weitere | gering bzw. nicht sauber umsetzbar | – | verworfen (siehe unten) |

Alle vier Optimierungen V1, V5, V9 und V7 sind im Testmod standardmäßig aktiv. Eine Übernahme in Hauptmods oder APIShared ist derzeit nicht vorgesehen (Nutzervorgabe).

## Arbeitsregeln

- Jeder Eintrag nennt Beleg, Kosten pro Frame oder Tick, betroffene Vanilla-Methoden, Risiko, MP-Relevanz und Status.
- Status: `vermutet` (nur statische Analyse), `gemessen` (PerfProbe-Beleg), `im Testmod bestätigt`, `umgesetzt`, `verworfen`.
- **Keine akzeptierten Lösungen** (Nutzervorgabe): Zoomstufen oder Zoomgrenzen ändern bzw. sperren; FPS-Limit, VSync oder `targetFrameRate` ändern.
- Optimierungen müssen das Bild unverändert lassen. Ausnahmen werden einzeln mit dem Nutzer abgestimmt.
- Kompatibilität mit Script Extender und Fixes-Mod ist Pflicht; keine überlappenden Hooks.
- Messszenario: automatisierter Lauf mit `test_dustyFPS2.sav` (Kamera an einer Stelle mit vielen Einheiten) bei 1920×1080, siehe Messverfahren.
- Dieses Dokument enthält nur den aktuellen Stand. Messwerte sind Momentaufnahmen und gelten für die unten genannten Versionen; bei neuen Spiel- oder Extender-Versionen neu messen.

## Grundlage

- Spiel: Native `CrusaderDE.dll` `FBCB9319…31E2`, Managed `Assembly-CSharp.dll` `BC8B6A39…D789`, Unity 2021.3.45f2, URP 2D (`URP-2D`, SRP Batcher an, Dynamic Batching an, 1 Light2D), Mono-Backend, D3D11 mit eigenem Render-Thread, inkrementeller GC aktiv (`gc-max-time-slice=3` in `boot.config`).
- Messrechner: RTX 4090 (24 GB), Ryzen 9 7950X3D, 1920×1080 Fullscreen-Window.
- Installation bei den Messungen: SHCDE-SE 2.14.3, `PerfProbe` (DEVDIAG-Build, nie weitergeben) und `PerfFixTest`. Keine weiteren Mods, damit nichts das Ergebnis verfälscht. APIShared ist für diese Tests bewusst nicht installiert (Nutzerentscheidung).
- Messungen vom 2026-10-20: 02:18–02:37 (Läufe A3, B3, P1, M1, C1, B4; V1/V5/V9) und 06:20–07:04 (Läufe R1–R4, F1, U1, M2; V7 und Sim-Spike) und 11:56–12:10 (Läufe L1, Q1; LOD und Sprite-Meshes).

## Architektur

- **Sim-Thread (nativ):** `Director.runSimTickTest` → `EngineInterface.run` → `DLL_RunTick` (RVA `0x86680`) → Kartenrenderer `0x41D60`. Dieser läuft bei jedem Tick über alle Kacheln im Sichtbereich und liefert einen Delta-Stream aus 21-short-Datensätzen. `EngineInterface.run` hält dabei `EngineInterface.threadLock` (`private static`), den 74 `lock`-Stellen in `EngineInterface` ebenfalls nutzen.
- **Main-Thread (verwaltet):**
  - `Director.Update` verarbeitet bis zu 3 Puffer pro Frame mit `GameMap.processTestMap`. Danach folgen `ApplyRadarMap` (Minimap), `experimentalRowManager` und `TilemapManager.endTileRefresh`.
  - `FatControler.NoesisGUIUpdateChecksInGame` läuft nach jedem Puffer, spätestens alle 100 ms.
  - `GameMap.Update` → `lerpChimps` ruft für jede Einheit `interpChimp` auf. Der Script Extender detourt `interpChimp` (siehe V9).
- **Darstellung:**
  - Eine Unity-`Tilemap` pro Bildschirmzeile.
  - Objekte sind einzelne GameObjects mit `SpriteRenderer` (Pool 30 000 + 4 000); eine Einheit belegt bis zu 7 GameObjects.
  - Sortierformel `-20000 + Zeile*49 + lokaler Offset`; Einheitenteile liegen bei `nextBaseSortLayer + 8…14` (+34 bei `layerDelay`), Lebensbalken optional bei 32767. Sprites mit gleichem Sortierschlüssel ordnet Unity nach Material (Zustandsoptimierung beim Sortieren).
- **Texturen und Materialien:**
  - Die Sprites liegen in großen Resources-Sheets. Jedes Sheet hat eine Maske `<Sheet>_m` mit identischem Layout, die als `_TeamMask` **im Material** steckt.
  - `Unlit/TeamColour`: 7 Materialien pro Sheet (`_SpriteCutoff`-Staffel für abgeschnittene Füße). 13 Sheets: `body1/2/4/5/6/7/8/12/13/14/15/16Sprites` je 8192² BC7 (Bilinear), `anims1Sprites` 8192×4096. Masken gleich groß, DXT1 (Point). Zusammen 111 492 Sprites, davon 80 848 Tight-Mesh.
  - `Unlit/Foliage`: `treeSprites` 4096×8192 BC7, Maske 1024×2048 DXT1 (GM 29–31, 70–72, 97).
  - Plain: ein gemeinsames `Sprite-Unlit-Default`, u. a. für `AllTileSprites` (8192² BC7, Kacheln und Gebäude).
  - Alle Sheets ohne Mipmaps und nicht lesbar. Die Zuordnung aller 195 GM-Gruppen steht maschinenlesbar in `Helpers/AtlasBuilder/atlas-contract SHOM/atlas_contract/loader_groups.json`.

## Messstand ohne Optimierungen (Vanilla + Extender)

Schwere Szene `test_dustyFPS2.sav`, Zoom 0.25 (1 805 Draw Calls, ~3 700 sichtbare Sprites, ~3 080 Einheiten):

| Messgröße | pausiert | laufend |
|---|---|---|
| FPS (Läufe B3, B4) | 132 (130–134) | 124.5 (124–125) |
| p95-Frame-Zeit | 11.0–11.7 ms | 11.6–11.7 ms |
| größter Ruckler | 22–27 ms | 85–173 ms |
| Draw Calls / SetPass | ~1 805 / ~2 380 | ~1 820 / ~2 400 |
| `ApplyRadarMap` (fast nur `TestRadarMap`) | ~1.5 ms pro Frame | ~0.9 ms pro Frame |
| Heap-Wachstum | ~40 MB/s | ~38 MB/s |

- **Der Engpass ist der Unity-Main-Thread.** Bei ~1 800 Draw Calls liegen ~8–9 ms im `PlayerLoop`, davon der größte Teil in Unitys eigener Arbeit für Culling, Sortierung, Batching und Submit. Die GPU ist nicht der Engpass, und auch der Sim-Thread begrenzt die FPS nicht.
- Pausiert und laufend liegen die FPS nah beieinander. Die Kosten des Herauszoomens entstehen durch die Darstellung der vielen sichtbaren Objekte und die Kosten pro Frame bzw. pro Einheit, nicht durch die Simulation.

**Ursachen der Draw Calls** (Census, Zoom 0.25, Schätzung 1 839 ≈ gemessen 1 805):

| Bruch zwischen sortierten Nachbarn | Anzahl | Anteil |
|---|---|---|
| anderes Material derselben Klasse (TeamColour aus einem anderen Sheet) | **1 107** | 60 % |
| Tilemap-Zeile (eigener Renderer, ~2 pro sichtbarer Zeile) | 559 | 30 % |
| andere Materialklasse (Plain ↔ TeamColour ↔ Foliage, Cutoff-Stufe) | 164 | 9 % |
| andere Textur bei gleichem Material | 7 | <1 % |

## Messverfahren

- **Automatisierung** (`Testmods/PerfFixTest/src/Automation`, Config `[Automation]`, standardmäßig aus): Hauptmenü → Spielstand laden → 80 Sim-Ticks → in 6 Schritten auf Zoom 0.25 → 3 s warten → native Pause halten → entpausieren und laufen lassen → PASS/FAIL → Spiel beenden.
  - Bereitschaft wird ohne APIShared aus Spielzuständen und beobachteten Sim-Ticks erkannt. Das ist eine bewusste Ausnahme von der Lifecycle-Regel, damit die Messung unverfälscht bleibt (Nutzerentscheidung).
  - Die Frame-Statistik je Phase (FPS, Mittel, p50/p95/p99, Maximum) misst die Automatisierung selbst; sie stimmt mit PerfProbe überein (139.1 gegen 140.8 FPS).
  - In der Pause wird ein Screenshot aufgenommen (`BepInEx/PerfFixTest_shots`).
- **Lauf-Skript** `Testmods/PerfFixTest/tools/Run-Automation.ps1 -Label <X> -OutputDirectory <Ordner> -Features "Schalter=Wert;…"`: setzt die Config, startet über Steam, wartet auf das Ende und sichert `LogOutput.log`, `Player.log` und die Screenshots. Danach setzt es die Installation zurück (Automatisierung aus, Fixes an). Das ist nötig, weil BepInEx `AppendLog` deaktiviert ist und jeder Start das Log überschreibt.
- **Streuung:** Zwischen getrennten Läufen streuen die FPS um mehrere Prozent. A/B-Aussagen beruhen deshalb auf mehreren Läufen je Variante. Ein Pixelvergleich zwischen getrennten Läufen taugt nicht, weil die Pause je Lauf bei einem leicht anderen Sim-Tick greift.
- **Vergleich im selben Lauf** (V7): Pausiert werden Frame-Statistik und Screenshot vor und nach der Umschaltung aufgenommen. Ein Kontrolllauf ohne Umschaltung (`SheetMergeControl`) zeigt, dass sich ohne Eingriff nur die Uhr oben rechts ändert (582 Pixel) und die FPS konstant bleiben (146.0 → 145.9).

## Optimierungen im Testmod

Alle Eingriffe sind verwaltete Harmony-Eingriffe, nur clientseitig (`NetworkMode=0`) und ohne native Hooks. Schalter in `BepInEx/config/PerfFixTest_Serp.cfg` unter `[Features]` (Neustart nötig).

### Gesamtwirkung V1 + V5 + V9

| Messgröße (Zoom 0.25) | mit V1+V5+V9 (A3, P1, C1, M1 vorher) | ohne (B3, B4) | Wirkung |
|---|---|---|---|
| FPS pausiert | Ø 143.2 (139–146) | Ø 132.1 (130–134) | **+8.4 %** |
| FPS laufend | Ø 139.3 (137–141) | Ø 124.5 (124–125) | **+11.9 %** |
| p95-Frame-Zeit pausiert | 7.3–8.6 ms | 11.0–11.7 ms | **−30 %** |
| größter Ruckler laufend | 18–32 ms | 85–173 ms | Freezes beseitigt |
| Heap-Wachstum | 5–17 MB/s | ~38–40 MB/s | −55 bis −80 % |

Die Sichtprüfung durch den Nutzer bestätigt: Minimap, Bündnisanzeige und Einheiten sehen normal aus.

### V1 – Minimap nicht bei jedem Tick neu an Noesis binden — im Testmod bestätigt

- **Ursache:** `GameMap.ApplyRadarMap` lädt die Radartextur bei jedem Sim-Tick per `SetPixelData`/`Apply` hoch; das ist billig. Danach erzeugt `MainViewModel.TestRadarMap` jedes Mal ein **neues** `Noesis.TextureSource` für dieselbe Textur und bindet `RadarMapImage` neu. Das kostet 0.9–3.8 ms pro Aufruf bei 40 Aufrufen pro Sekunde, auch pausiert, und allokiert bis ~110 KB pro Aufruf.
- **Umsetzung** (`RadarRebindFix.cs`): Prefix auf `TestRadarMap`. Er überspringt den Aufruf, solange dieselbe Texturinstanz und dasselbe gebundene Bild bestehen. Eine neue Texturinstanz (Kartenwechsel, Rotation, Auflösung) wird normal gebunden. Die Minimap aktualisiert sich weiter, weil `Apply()` in dieselbe GPU-Textur schreibt.
- **Wirkung:** `ApplyRadarMap` sinkt von 100–180 auf ~5 ms/s. Die regelmäßigen 1–4-ms-Spitzen verschwinden; das ist der Hauptgrund für die bessere p95-Frame-Zeit.
- **Risiko:** gering.

### V5 – Kein Freeze durch Warten auf `EngineInterface.threadLock` — im Testmod bestätigt

- **Ursache:** Einzelne native Sim-Ticks dauern ~450–490 ms (KI-Gruppenbefehl mit Wegsuche, siehe Abschnitt Sim-Spike). `EngineInterface.run` hält dabei `threadLock`. Ruft der GUI-Check in dieser Zeit `EngineInterface.GetMeritData` auf (`HUD_AlliesPanel.ShowAlliesPip`, höchstens 1-mal pro Sekunde), steht der Main-Thread bis zum Ende des Ticks. Beobachtet wurden Freezes von 260–380 ms.
- **Audit:** `GetMeritData` liefert bei jedem Aufruf ein neues `int[9,5]` (Rang bzw. Bündnisstatus je Spieler). Alle 4 Aufrufer lesen nur, die Werte ändern sich langsam.
- **Umsetzung** (`MeritDataLockFix.cs`): Ist der Lock frei (`Monitor.TryEnter`), läuft Vanilla und das Ergebnis wird zwischengespeichert. Ist er belegt, bekommt der Aufrufer einen Klon des höchstens 3 s alten letzten Ergebnisses. Ohne Kopie läuft Vanilla wie bisher.
- **Wirkung:** In den Läufen mit V5 wurde während des langen Ticks jeweils aus dem Cache bedient, und es gab keinen Main-Thread-Spike. Ohne V5 traten Ruckler bis 173 ms auf.
- **Risiko:** gering. Aktionen wie `GameAction` dürfen nicht so behandelt werden; weitere reine Lesefunktionen aus dem GUI-Check wurden bei langen Ticks bisher nicht beobachtet.

### V9 – Script-Extender-Event ohne Allokation pro Einheit — im Testmod bestätigt

- **Ursache** (SHCDE-SE 2.14.3, `ManagedHookManager.GameMap_InterpChimpHook`): Der Extender-Detour vor `GameMap.interpChimp` erzeugt für jede Einheit und jeden Frame ein neues `UnitUnityVisualInterpolateEventArgs` und löst `OnUnitUnityVisualInterpolate` aus, auch für Einheiten, die nicht interpolieren. Das sind ~450 000 Objekte pro Sekunde, also 10–14 MB/s, und die größte Allokationsquelle im Spiel. Nachgewiesen im pausierten Spiel ohne Interpolation und ohne Datensätze.
- **Audit der Abonnenten:** Extender (Nachbilder), ExtraFeatures (KnightDismount), ImprovedHunters, SkinTest, VirtualUnitsPrototype; der Fixes-Mod keiner. Alle lesen `args.Chimp` nur synchron, keiner speichert das Objekt.
- **Umsetzung** (`InterpolateEventFix.cs`): Ein Transpiler auf die Extender-Methode ersetzt die eine Allokation durch ein wiederverwendetes Objekt und gibt es direkt nach `Raise` frei.
  - Verschachtelte Auslösungen (ein Abonnent ruft erneut `interpChimp`) und andere Threads allokieren wie bisher.
  - Wirft ein Abonnent, setzt `onBeforeRender` den Zustand im nächsten Frame zurück.
  - Ohne Harmony-Prefix/Finalizer, weil deren try/finally-Hülle bei ~3 000 Aufrufen pro Frame messbar Zeit kostet.
  - `interpChimp` selbst wird nicht gehookt.
  - Fail-closed: genau eine Allokation und ein `Raise` in der Extender-Methode erwartet.
- **Wirkung:** Allokation in `lerpChimps` sinkt von 10–14 auf ~1–3 MB/s; es gab 0 verschachtelte Allokationen.
- **Hinweis:** Das ist eine Korrektur am Script Extender. Bei einer Übernahme gehört sie laut Workspace-Regeln nach APIShared.

### V7 – Sheets einer Materialklasse auf gemeinsame Seiten — im Testmod wirksam, Sichtprüfung offen

- **Ursache:** 60 % der Draw Calls entstehen, weil benachbarte TeamColour-Sprites aus verschiedenen Sheets stammen. Jedes Sheet hat eine eigene Maske im Material, also auch ein eigenes Material.
- **Machbarkeit** (Probe `SheetMergeProbe`, im Spiel):
  - max. Texturgröße 16384;
  - `Graphics.CopyTexture` für BC7 und DXT1 vorhanden;
  - alle Masken so groß wie ihr Sheet;
  - 0 von 111 492 Sprites mit Mesh-Eckpunkten außerhalb ihres Rects;
  - die Sheets sind voll (überlappende Rects), deshalb werden nur ganze Sheets verschoben.
- **Umsetzung** (`SheetMerge.cs`, Schalter `[Features] SheetMerge`, Standard an):
  - Einmalig direkt nach dem Laden der Vanilla-Sprites. Die Overrides des Script Extenders laufen im selben Lade-Schritt und sind dann bereits angewendet.
  - Die 13 TeamColour-Sheets und ihre Masken werden per GPU-Kopie als Ganzes auf 4 Seiten à 16384² gelegt: `body12+14+16+13`, `body1+2+8+anims1`, `body7+5+4+15`, `body6`. Gruppiert wird nach der Census-Simulation.
  - Je Seite entstehen 7 Materialien (Cutoff-Stufen), geklont vom Original; nur `_TeamMask` zeigt auf die Maskenseite.
  - Alle Sprites werden mit gleichem Rect, Pivot und PPU neu erzeugt und erhalten ihr **Original-Tight-Mesh**: Positionen unverändert, UVs in den Seitenplatz verschoben, gesetzt über `SpriteDataAccessExtensions`.
  - Umgestellt werden `gmSprites`, `gmAltSprites`, `allSprites`, `gmMaterials` und vorhandene Renderer; Material und Sprite wechseln immer gemeinsam.
  - GM-Dateien, deren Sprites ein Asset-Mod ganz oder teilweise ersetzt hat, bleiben unverändert (Sprites und Material).
- **Bestätigter Irrweg:** `Sprite.OverrideGeometry` wird zur Laufzeit für alle 111 492 Sprites abgelehnt („Not allowed to override geometry“), auch bei lesbarer Seite. Ohne Tight-Mesh zeichnet jedes Sprite sein volles Rechteck und damit Teile der überlappenden Nachbarsprites. Diese Fragmente sahen wie geänderte Überlappungsreihenfolgen aus.
- **Bildprüfung:**
  - **Render-Check** (`SheetMergeRenderCheck`): 60 Sprites je Sheet × 7 Cutoff-Stufen, off-screen pixelgenau mit Original- und Seitenmaterial gezeichnet. 5 243 von 5 460 Tests sind identisch; der Rest weicht nur an einzelnen Randpixeln ab (höchstens 10 von 61 279 Pixeln eines Sprites). Gesamt: 99,3 % der Pixel identisch, 0,7 % mit Delta 1–2, 464 von 65,5 Mio. Pixeln mit Delta über 2.
  - **Spielbild** (Lauf R4, pausiert, vor/nach der Umschaltung im selben Lauf): Abweichungen über Delta 16 gibt es nur an der Uhr oben rechts (wie im Kontrolllauf), dazu 906 Pixel mit Delta 1–16.
- **Zeichenreihenfolge:** Die Sortierung ist durch `sortingOrder` je Kartenzeile in 49er-Blöcken eindeutig festgelegt:

| Offset im Zeilenblock | Inhalt |
|---|---|
| +0 | Zeilen-Tilemap (Boden, Gebäude) |
| +1, +16…+40 | Gebäudeanimationen |
| +2 | Mauer-Füllstücke |
| +3, +41 | Pixies |
| +4…+7 | Orgs |
| +8…+14 bzw. +42…+48 | Einheiten |
| +15…+17 | Fliegen |

  Ein Gleichstand zwischen Gebäude- und Einheitenteilen ist damit ausgeschlossen. V7 kann deren Reihenfolge nicht ändern, und gemessen ändert sie sich auch nicht.
- **Wirkung** (zusätzlich zu V1+V5+V9):

| Messgröße | ohne V7 | mit V7 |
|---|---|---|
| Draw Calls | ~1 805 | **~800–860 (−54 %)** |
| FPS pausiert (R4 im selben Lauf; F1) | 145.1 | **178.3; 179.5 (+23–24 %)** |
| p95-Frame-Zeit pausiert | 7.3 ms | 5.9–6.1 ms |
| FPS laufend (Ø ohne V7; R4, F1) | Ø 139.3 | **172.3; 171.9 (+23 %)** |

  Der fehlerhafte Rechteck-Prototyp kam in einem anderen Lauf auf +36 %. Laut V11 liegt der Unterschied nicht an den Eckpunkten; er ist vor allem Lauf-zu-Lauf-Streuung.
- **Kosten bzw. offen:**
  - Aufbau ~1.7 s einmalig im Hauptmenü.
  - Texturspeicher (`Texture.currentTextureMemory`, Lauf U1) steigt um ~1,66 GB: 2 447 → 4 111 MB. Die Original-Sheets müssen geladen bleiben (siehe Verworfen).
  - Asset-Mods (Lauf M2): getestet mit dem Sprite-Override des Extender-Beispielmods (`body_archer-169` samt Maske).
    - Die betroffene GM-Datei bleibt unverändert (`v7 skipped 1 GM file`); 109 532 statt 111 492 Sprites werden umgestellt.
    - Das Bild weicht nur an der Uhr ab.
    - FPS pausiert 143 → 174 (+21 %).
  - Ungetestet:
    - Atlas-Overrides (ganze GM-Gruppen) und das Foliage-Begleitplugin des AtlasBuilders;
    - Grafikkarten mit weniger VRAM bzw. kleinerer maximaler Texturgröße. Bei fehlender Unterstützung schlägt V7 fail-closed fehl: Log `v7 FAIL`, Vanilla bleibt unverändert.
  - Sichtprüfung durch den Nutzer in verschiedenen Spielständen.

## V10 LOD bei weitem Zoom — gemessen, verworfen

- **Frage:** Bringt es etwas, bei weitem Zoom Details auszublenden (geringere Detailstufe)?
- **Messung** (Probe `LodProbe`, Lauf L1, pausiert, Zoom 0.25, V1+V5+V9+V7 aktiv):
  - Die Kategorien kommen aus Vanillas `GameMap`-Objektlisten.
  - Jede Kategorie wird 5 s lang ausgeblendet; danach werden genau diese Renderer wiederhergestellt.
  - Kontrollfenster liegen am Anfang und am Ende. Das Bild vor und nach dem Experiment ist bis auf die Uhr identisch.

| Kategorie | sichtbare Renderer | ausgeblendet | Draw Calls | FPS |
|---|---|---|---|---|
| Kontrolle Anfang / Ende | – | – | 838 | 168.8 / 179.4 |
| Einheitenkörper (nur Referenz) | 2 700 | 3 080 | 453 | **235.6** |
| Einheitenteil 2 (`file2`, z. B. Reiter) | 500 | 630 | 831 | 180.1 |
| Einheitenteil 3 | 0 | 163 | 837 | 178.3 |
| Belagerungsflagge, Lebensbalken, Zielmarker, Pixies | 0 | 0 | 837–838 | 171.6–177.7 |
| Fliegendes (Geschosse, Ziffern) | 56 | 82 | 802 | 173.7 |
| Orgs | 76 | 86 | 789 | 177.9 |
| Gebäudeanimationen | 148 | 416 | 744 | 179.8 |
| Mauer-Füllstücke | 120 | 204 | 785 | 184.4 |

- **Bewertung:**
  - Keine Detailkategorie bringt mehr als das Messrauschen (Kontrollfenster 169–179 FPS); höchstens die Mauer-Füllstücke liegen mit +3 % an der Grenze.
  - Kategorien, die gar nicht im Bild sind, liegen im selben Band.
  - Die Kosten hängen nach V7 an der **Anzahl der Einheiten**: Nur das Ausblenden aller Einheitenkörper bringt deutlich mehr (+31 %). Das wäre aber kein LOD mehr, sondern ein anderes Bild.
  - Schatten sind Teil der Einheiten-Sprites und haben keine eigenen Renderer.
  - Die zweite Kachelebene der Tilemaps wurde nicht gemessen. Sie liegt im selben Renderer wie die erste Ebene, ihr Ausblenden spart also keine Draw Calls.
  - LOD ändert das Bild und bringt keinen messbaren Nutzen, daher kein gutes Aufwand-Nutzen-Verhältnis.

## V11 Sprite-Meshes vereinfachen — gemessen, verworfen

- **Idee:** Die Sprites der TeamColour-Sheets sind Tight-Meshes mit durchschnittlich ~8 Eckpunkten. Unity bündelt Sprites, indem es jeden Eckpunkt in jedem Frame auf der CPU kopiert und transformiert. Das umschließende Rechteck (4 Eckpunkte) wäre dort bildgleich, wo im Rechteck außerhalb des Polygons nur transparente Pixel liegen.
- **Messung** (Probe `QuadProbe`, Lauf Q1, pausiert, Zoom 0.25, V7 aktiv):
  - 1 165 verschiedene Sprites auf 4 544 Renderern; 297 davon haben schon höchstens 4 Eckpunkte.
  - Alle übrigen Renderer (3 812) werden für ein Fenster auf ihre Rechteck-Sprites umgestellt.
- **Ergebnis:**
  - Die Eckpunkte im Bild sinken nur von 182 900 auf 167 700, denn der Großteil stammt von den Tilemaps.
  - Die FPS steigen von 141–145 (Kontrollfenster) auf 148, also +2–5 %. Das ist die **Obergrenze**, einschließlich der Fehler durch Nachbarfragmente.
  - Bildgleich wäre die Umstellung laut Render-Check bei 0 von 868 Sprites; dieser Wert wurde nicht weiter untersucht, weil schon die Obergrenze im Rauschen liegt.
  - Der ganze Lauf war langsamer als üblich (Fremdlast), der Vergleich innerhalb des Laufs gilt trotzdem.
- **Bewertung:** Der Gewinn liegt im Messrauschen, kein gutes Aufwand-Nutzen-Verhältnis. Ergänzend zu V10 bestätigt das: Nach V7 kosten die Einheiten pro Renderer (Culling, Sortierung, Bündelung), nicht pro Eckpunkt oder Bilddetail.

## Nativer Sim-Spike — Ursache bestimmt, Optimierung verworfen

- **Erscheinung:** Einzelne Sim-Ticks dauern ~450–490 ms (sonst ~5 ms), unabhängig vom Zoom. In `test_dustyFPS2.sav` tritt der Spike ~4 s nach dem Entpausieren auf; in einem anderen Spielstand wiederholte er sich etwa alle 25 s.
- **Ursache** (Stack-Sampler, 388–390 Proben pro Spike-Tick, 2 Läufe identisch, Details in der Baseline `knowledge/SIM_TICK_SPIKE_PATHING.md`):
  - 96 % der Zeit liegen in der Kette `DLL_RunTick` → KI-Spieler-Scheduler `0x2AE40` → KI-Militärroutine → Gruppenbewegungsbefehl `0x11B520` → Einzel-Wegsuche `0x196280` → Pfadfinder `0xF4930`. Der Pfadfinder arbeitet mit Suchbudgets bis 400 000.
  - Ein KI-Gruppenbefehl berechnet also für jede Einheit der Gruppe eine Route im selben Tick.
  - Die Kartenrenderschleife ist nicht beteiligt (3 von 390 Proben).
- **Folgen:** Das Bild läuft weiter (86–91 Frames während des Ticks); nur die Simulation steht ~0,5 s. Ohne V5 fror der Main-Thread mit ein, sobald ein GUI-Check auf `threadLock` wartete.
- **Bewertung – keine gute Lösung per Mod:**
  - Wegsuchen zu verteilen oder zu verkürzen ändert die Simulation. Das gefährdet Determinismus bzw. MP-Synchronität und das KI-Verhalten.
  - Ein schnellerer Pfadfinder wäre ein nativer Hook auf `0xF4930`, den APIShared bereits besitzt (überlappender Hook), mit hohem Risiko.
  - Der sichtbare Teil, das Einfrieren des Bildes, ist durch V5 behoben.

## Verworfen bzw. als keine gute Lösung bestätigt

| Vorschlag | Grund |
|---|---|
| Zoom, FPS-Limit, VSync ändern | Nutzervorgabe |
| V2 `lerpChimps` entschlacken | Der Vanilla-Anteil steckt in `interpChimp`, das der Extender detourt; ein weiterer Hook dort wäre ein überlappender Detour. Nach V9 bleibt pausiert nur ~0.2 ms Sockel. |
| V3 `GetComponent` in `processTestMap` cachen | geschätzt ~0.04 ms pro Frame gegen einen Transpiler über 23 Stellen |
| V4 Maus-/Tile-Berechnung cachen | `EditorDirector` liest nur aus; die eine Tile-Pick-Berechnung pro Frame (~0.15 ms) zu cachen riskiert veraltete Hover-Anzeigen |
| V8 statische Sprites als Zeilen-Mesh | nur 0–8 % der sichtbaren SpriteRenderer sind über 5 s unverändert; Statisches liegt bereits in den Tilemaps |
| Unveränderte Minimap-Uploads überspringen | kein Upload war identisch; der Upload ist ohnehin billig |
| Atlas nur der Plain-Texturen | nur ~7 von ~1 800 Brüchen |
| Sprites innerhalb der Sheets dichter packen | Sheets voll, Tight-Mesh-Rects überlappen sich |
| Zeilen-Tilemaps zusammenlegen | die Tiefensortierung zwischen Kacheln und Einheiten hängt an der Zeilen-Tilemap; ~30 % der Brüche sind die Untergrenze |
| Nicht sichtbare aktive Renderer spaltenweise deaktivieren | ~11 % der aktiven Renderer, Culling ist billig |
| Inkrementellen GC aktivieren | bereits aktiv |
| Native Sim-/Renderschleife umschreiben | zu riskant, begrenzt die FPS nicht |
| V7: Original-Sheets nach dem Zusammenlegen freigeben (`Resources.UnloadAsset`) | Spart ~1,1 GB Texturspeicher, zerstört aber Mauern und Türme. Die Zeilen-Tilemaps halten in ihren Kachel-Daten Original-Sprites dieser Sheets, und Unity lädt die freigegebenen Texturen dafür nicht nach. Alle Tilemaps und Caches umzustellen wäre riskant; das lohnt sich nur bei knappem VRAM. |
| KI-Wegsuche beschleunigen oder verteilen (Sim-Spike) | ändert Simulation bzw. Determinismus; nativer Hook auf `0xF4930` überlappt APIShared |

## Engine-Einstellungen: Graphics Jobs und Grafik-API — gemessen, verworfen

Ausgangslage: D3D11 mit eigenem Render-Thread, Graphics Jobs aus (`GfxDevice: … threaded=1; jobified=0`), `boot.config` ohne Job-Einträge.

Gemessen mit allen Optimierungen aktiv (V1/V5/V9/V7), abwechselnde Läufe, 2026-10-20 12:37–13:05:

| Variante | Läufe | FPS pausiert | FPS laufend | Bild |
|---|---|---|---|---|
| D3D11 wie ausgeliefert | G0a–d | 133 / 173 / 161 / 163 | 136 / 160 / 149 / 162 | ok |
| D3D11 + `gfx-enable-gfx-jobs=1` (`jobified=1`) | G1a–c | 138 / 137 / 153 | 126 / 127 / 136 | ok |
| D3D12 (`-force-d3d12`) + native Graphics Jobs | D12a | 276 | 208 | **HUD fehlt** |
| D3D12 ohne Graphics Jobs | D12n | 116 | 88 | **HUD fehlt** |
| Vulkan (`-force-vulkan`) | VKa | – | – | startet nicht (nicht im Build enthalten) |

- **D3D11-Graphics-Jobs:** kein Gewinn, laufend eher langsamer.
- **D3D12:** Die Noesis-Oberfläche (Leiste, Schreiber, Rahmen) wird nicht gezeichnet, nur Minikarte, Datum und Uhr. Ohne Graphics Jobs ist D3D12 deutlich langsamer.
  - **Der Gewinn ist echt, nicht HUD-bedingt** (Probe `HudProbe`, Läufe H11a/b und H12a/b, beide Varianten bei 3840×2160):
    - Unter D3D11 kostet das HUD nichts messbar: HUD aus 160 / 160 FPS, HUD an 158–166.
    - D3D12 pausiert: 208–263 FPS mit HUD-Bäumen (die nur unsichtbar sind), 233–281 ohne.
    - Laufend schwankt D3D12 stark: 113–208 FPS über alle Läufe, D3D11 136–162.
  - **Ursachensuche HUD:**
    - Unter D3D12 gibt es keine Fehlermeldungen. Noesis 3.2.3 rendert Text und die Minikarte, die über `TextureSource` → `Noesis_WrapTexture` kommt.
    - XAML-Bilder kommen über `NoesisTextureProvider.UpdateTextures` → `Noesis_TextureProviderStoreTextureInfo` (geschlossener nativer Code). Format und Mip-Anzahl sind nicht die Ursache: 129× RGBA8 mit 1 Mip, gültige native Zeiger.
    - Versuch (`NoesisRebindProbe`, Lauf RB1): 17 `Image`-Quellen und 1 `ImageBrush` im Elementbaum auf `TextureSource` umgestellt. Das HUD bleibt trotzdem unsichtbar.
    - Die Ursache liegt also tiefer im nativen D3D12-Renderpfad von Noesis (z. B. Offscreen-/Effektpfade), nicht nur im Texturweg.
  - **Bewertung:** Mit vertretbarem Aufwand per Mod nicht reparierbar (geschlossene native Noesis-DLL); dazu kommt die instabile Laufleistung. Verworfen, solange Noesis/Spiel D3D12 nicht selbst unterstützen.
- **Achtung Auflösung:** Ab Lauf N1 lief das Spiel mit 3840×2160 statt 1920×1080 (vermutlich durch den ersten D3D12-Direktstart gesetzt). Läufe sind nur innerhalb derselben Auflösung vergleichbar; die H-Läufe (D3D11 und D3D12) liefen beide bei 4K.
- **Vulkan:** Unity meldet „Forced GfxDevice 'Vulkan' was not built…“ und `InitializeEngineGraphics failed`.
- **Testweg:**
  - `Run-Automation.ps1 -BootConfig "key=value;…"` ändert `boot.config` vorübergehend und stellt sie byte-genau wieder her (Hash `A2EEC40C…` geprüft).
  - `-LaunchArgs` startet die exe direkt. Ohne `steam_appid.txt` startet das Spiel über Steam neu und verliert die Argumente; für die Tests lag die Datei deshalb kurzzeitig im Spielordner und wurde danach entfernt.
- **Streuung zwischen Läufen:** Identische D3D11-Läufe lagen zwischen 133 und 173 FPS. Fremdlast (Browser, weitere Programme) wirkt sich deutlich aus; Vergleiche brauchen abwechselnde, wiederholte Läufe.

## Kandidaten mit echtem Potenzial (nicht umgesetzt)

### V12 Eigener Einheiten-Renderer — Potenzial groß, Aufwand und Risiko hoch

- **Ansatz:** Statt ~3 000 einzelnen `SpriteRenderer`-Objekten für Einheitenteile würde pro sichtbarer Kartenzeile ein selbst gebautes Mesh gezeichnet. Es enthält alle Einheitenteile der Zeile in Vanillas Reihenfolge, wird per Job-System auf mehreren Kernen gebaut und mit dem V7-Seitenmaterial gezeichnet.
- **Begründung aus den Messungen:**
  - Nach V7 kosten Einheiten pro Objekt (Culling, Sortierung, Bündelung im Main-Thread), nicht pro Eckpunkt oder Detail; siehe V10/V11.
  - Alle Einheitenkörper auszublenden brachte +31 %. Diese Kosten ließen sich mit eigenen Meshes größtenteils vermeiden.
  - Geschätztes Potenzial: +20–30 %.
- **Schwierigkeiten:**
  - Die exakte Sortierung (`sortingOrder` je 49er-Zeilenblock, `layerDelay`-Versatz, Lebensbalken auf 32767) muss zwischen den Zeilen-Tilemaps nachgebildet werden, ein Zeilen-Mesh pro Sortierstufe.
  - Interpolation (`lerpChimps`) und Animationen ändern Positionen und Sprites in jedem Frame, die Meshes müssten also jeden Frame neu gebaut werden.
  - Script Extender und Mods hängen an den `SpriteRenderer`-Objekten der Einheiten: `OnUnitUnityVisualInterpolate`, `SetBodySprite`-Detour, Skins, Nachbilder.
  - Auswahl und Mausabfrage müssten weiter funktionieren.
- **Bewertung:** Die einzige verbleibende Optimierung mit deutlichem Potenzial, aber ein eigenes Projekt (Darstellungsschicht neu schreiben), kein Testmod-Feature. Vor einem Start wäre ein Prototyp für nur eine Einheitenart sinnvoll, um den Gewinn zu messen.

## Offene Punkte

- **V7:** Sichtprüfung durch den Nutzer in verschiedenen Spielständen; Test mit Atlas-Overrides und auf Hardware mit weniger VRAM (auf dem Messrechner nicht prüfbar).
- **Übernahme V1/V5/V9/V7:** derzeit nicht vorgesehen (Nutzervorgabe); bei Übernahme gehört V9 laut Regeln nach APIShared, weil es den Extender korrigiert.
- **Ladephase** (außerhalb des Zoomproblems): `EngineInterface.LoadSaveFile` ~300 ms, `NoesisView.OnGUI` ~800 ms nach dem Laden, `FatControler.Update` ~2.4 s beim Wechsel vom Hauptmenü auf die Karte.

## Workspace-Wissen zu Texturen

Quellen: `Guides/Replace Assets with Script Extender.md`, `Helpers/AtlasBuilder/USAGE.md`, `Helpers/AtlasBuilder/atlas-contract SHOM/*`, CastlePlanner.

- Farb- und Maskentextur müssen pixelgenau dasselbe Layout haben; ein gemeinsamer Atlas ist nur zusammen mit einem gemeinsamen Maskenatlas batchbar.
- Script-Extender-Atlanten: Point-Filter, Clamp, keine Mips, `SpriteMeshType.FullRect`, pro GM-Gruppe eine eigene Textur und ein eigenes Material. Jeder Asset-Mod erhöht damit die Zahl der Materialien.
- Indexfallen: `tile_sea_new_01`/`tile_sea_shore` teilen ein Array (`ID_Offset`/`additionalStorage`); Teilatlanten erhalten nicht ersetzte Vanilla-Frames. V7 ersetzt nur Referenzen, nie Arraylängen oder Indizes.
- Native-Baseline: Die nativen Datensätze liefern nur GM-Datei- und Bildindex; alle Textur- und Materialentscheidungen fallen verwaltet in `SpriteMapping`/`spriteLoader`.

## Messwerkzeuge

- **PerfProbe** (`Testmods/PerfProbe`, nur `build.bat /devdiag`, Config `[Extras]`/`[Spikes]`): `perf:` (Kernmethoden, Sim-Tick), `snapshot` (Zoom, Draw Calls, SetPass), `engine` (`PlayerLoop`, Render-Thread-Wartezeit, GC), `census` (Bruchursachen, Atlas-Szenarien), `pageSim` (V7-Seitensimulation), `sheets:`/`sheetClasses`, `static`, `sweep`/`sweepAlloc` (Unity-Callbacks mit Zeit und Heap-Wachstum aus sauberen Proben), `engineCalls(main)`, `chimps`, `records byType`, `gc`, `spike:` (Ruckler über 200 ms mit Kontext), `simSample` (Stack-Sampler für Sim-Ticks ab 80 ms: Funktions-RVAs in `CrusaderDE.dll`, aufzulösen mit `query.ps1 function <RVA>`).
- **PerfFixTest** (`Testmods/PerfFixTest`):
  - Fixes unter `[Features]`: `RadarRebindSkip`, `MeritDataLockCache`, `InterpolateEventReuse`, `SheetMerge`.
  - Automatisierung unter `[Automation]`: `Enabled`, `SavePath`, `RunLabel`, Zeiten, `ScreenshotDir`.
  - Proben unter `[Probes]`: `SheetMergeProbe`, `SheetMergeMidPause` (V7 wird dann statt im Menü in der Mitte der Pause angewendet), `SheetMergeControl`, `SheetMergeRenderCheck`, `LodProbe`, `QuadProbe`, `HudProbe`, `NoesisProbe`, `NoesisSingleMip`, `NoesisRebindProbe`.
  - Log-Marker: `AUTOMATION … phase=`, `framestats`, `screenshot`, `probe=`, `v7`, `result PASS|FAIL`.
