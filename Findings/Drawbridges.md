# Zugbrücken auf erhöhtem Terrain: Vanilla-Audit und ExtraFeatures-Erkenntnisse

Stand: 17.09.2026. Untersucht wurde die Darstellung und Höhenbehandlung von Zugbrücken in Verbindung mit dem ExtraFeatures-Feature für Burggräben auf erhöhtem Terrain.

## Ergebnis in Kürze

Eine Zugbrücke besitzt zwei unterschiedliche Höhenverträge:

- Das Zugbrückentile gehört topologisch zum Graben und erhält bei erhöhtem Terrain tileweise `DefaultHeightGrid - 8`.
- Deck, Ketten, statische Zugbrückenteile und Einheiten bilden dagegen ein starres Gebäudeobjekt. Ihre gemeinsame vertikale Referenz ist die von Vanilla berechnete Gebäudehöhe in `buildingRecord + 0x148`.

Die sichtbar scheinende Doppelzeichnung war keine echte doppelte Erzeugung desselben Sprites. Vanilla zeichnet mehrere Zugbrücken-Unterteile über getrennte Pfade. Unterschiedliche Höhenkorrekturen verschoben diese Teile gegeneinander, wodurch vor allem Ketten wie Duplikate aussahen und das Brett je Terrainhöhe unterschiedlich lag.

Die implementierte Lösung verwendet deshalb für alle starren Zugbrückenteile und Einheiten dieselbe Gebäudehöhe `H`, während Wasser, Grabenboden, Pathfinding und Wiederherstellung weiterhin Vanillas tileweisen HeightGrid-Fluss verwenden. Sie ist gegen die kanonische DLL statisch, mit der installierten RedBird-/Iced-Version und abschließend im laufenden Spiel verifiziert.

## Provenienz

| Quelle | Geprüfter Stand |
|---|---|
| Kanonische installierte `CrusaderDE.dll` | `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll` |
| Native SHA-256 | `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` |
| Referenz-ImageBase | `0x180000000`; im Prozess ASLR berücksichtigen |
| ExtraFeatures-Testversion | `1.0.97` |
| Produktive Generatoren | [ElevatedMoatDrawbridgeHooks.cs](../ExtraFeatures/src/ElevatedMoatDrawbridgeHooks.cs) |
| Native Verträge | [ElevatedMoatNativeContract.cs](../ExtraFeatures/src/ElevatedMoatNativeContract.cs) |
| Hooktransaktion | [ElevatedMoatPatch.cs](../ExtraFeatures/src/ElevatedMoatPatch.cs) |
| Native-/Generatorprüfungen | [ElevatedMoatHeightTests](../_inspect/ElevatedMoatHeightTests) |

Einstieg in die Native-Baseline: [CURRENT.md](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) und [CURRENT.json](../_inspect/CrusaderDE-Native-Baseline/CURRENT.json). Die Hinweise zur ausschließlich lesenden Liveprozessanalyse stehen separat in [LiveProcessReadOnlyInspection.md](LiveProcessReadOnlyInspection.md).

## Begriffe und Höheneinheiten

- `DefaultHeightGrid` enthält die ursprüngliche Geländeoberfläche eines Tiles.
- `HeightGrid` enthält die aktuell wirksame Tilehöhe.
- Eine sichtbare Geländestufe entspricht intern acht HeightGrid-Rohwerten.
- Vanillas normale Zugbrückentopologie verwendet den festen Abstand acht zwischen Gelände-/Deckreferenz und Grabenboden.
- ExtraFeatures verwendet `12` als Grenze zwischen normalem Vanilla-Terrain und dem erweiterten erhöhten Anwendungsbereich.
- `H` bezeichnet in diesem Dokument die Vanilla-Gebäudehöhe aus `buildingRecord + 0x148`.

`DefaultHeightGrid`, aktuelles `HeightGrid` und `H` dürfen nicht miteinander gleichgesetzt werden. Auf einer flachen erhöhten Fläche können sie zwar rechnerisch zusammenpassen; auf einem geneigten Footprint unterscheiden sich die Tilehöhen, während das Gebäude eine einzige gemeinsame Höhe besitzt.

## Vanilla-Datenmodell

### Tile-Grids

| Daten | Image-relative RVA | Managerrelativer Offset |
|---|---:|---:|
| aktuelles `HeightGrid` | `0x4DDD350` | `0xD7E5A0` |
| `DefaultHeightGrid` | `0x4E2B870` | `0xDCCAC0` |
| Tile-Manager | `0x405EDB0` | Basis |

Die Adressdomäne ist wichtig:

- Pfade mit Tile-Managerbasis verwenden die managerrelativen Offsets.
- Pfade mit ImageBase verwenden die image-relativen RVAs.

Ein früherer Unit-Hook kombinierte fälschlich die ImageBase in `RBP` mit `0xD7E5A0`. Das las nicht das echte HeightGrid, sondern zufällige Daten. Deshalb funktionierte eine hohe Testbrücke scheinbar, während eine mittlere Höhe in den Vanilla-Zweig fiel.

### Gebäude-Manager und Gebäudehöhe

| Vertrag | Wert |
|---|---:|
| Gebäude-Manager-RVA | `0x64CCBB0` |
| Record-Stride | `0x32C` |
| Höhenfeld | `+0x148`, `ushort` |
| Gebäudeallocator | RVA `0xB47E0` |
| Höhenwriter | RVA `0xB49DC`, 9 Byte |

Vanilla berechnet die Gebäudehöhe beim Bau aus dem gesamten Footprint:

```text
H = minimale Footprint-Höhe + (maximale Footprint-Höhe - minimale Footprint-Höhe) / 2
```

Die belegte Weitergabekette lautet:

1. RVA `0x6D580` untersucht den Footprint.
2. RVA `0x6D9B6` berechnet den Mittelwert und hält ihn in `ESI`.
3. RVA `0x6DDAF` übergibt `ESI` als achtes Argument an den Zugbrückenerzeuger.
4. RVA `0x739C0` übernimmt das Argument.
5. RVA `0x73A08` reicht es als Höhenargument an den Gebäudeallocator weiter.
6. RVA `0x73A19` ruft `0xB47E0` auf.
7. RVA `0xB49DC` schreibt den Wert nach `buildingRecord + 0x148`.

Damit ist `H` kein hardcodierter Torhauswert und keine aus einem einzelnen Tile geschätzte Höhe. Dieselbe Regel funktioniert für kleine und große Torhäuser sowie für flache und geneigte Footprints.

## Vanilla-Zugbrückenpfade

### Tilehöhe beim Bau und Absenken

Die zwei getrennten Vanilla-Writes sind:

| Pfad | Hook-RVA | Tatsächlicher RedBird-Span | Vanilla-Verhalten |
|---|---:|---:|---|
| erstmalige Fertigstellung | `0x73B35` | 17 Byte | Zustandsaufruf, anschließend HeightGrid-Write `0` |
| erneutes Absenken | `0x64546` | 15 Byte | HeightGrid-Write `0`, anschließend ImageBase-LEA |

Für erhöhtes Terrain setzt ExtraFeatures das Zugbrückentile wie den umgebenden Graben auf:

```text
defaultHeight <= 12: 0
defaultHeight > 12:  defaultHeight - 8
```

Beispiele:

| Default | Graben-/Brückentile |
|---:|---:|
| 0 | 0 |
| 8 | 0 |
| 12 | 4 beim Graben, 0 für Vanillas niedrige Zugbrücke |
| 13 | 5 |
| 56 | 48 |
| 80 | 72 |
| 130 | 122 |
| 255 | 247 |

Die niedrige Zugbrücke bleibt bei Vanillas `0`. Die Klassifikation für die erhöhte Zugbrücke erfolgt anhand der unveränderten Default-Höhe, nicht anhand des bereits abgesenkten aktuellen HeightGrid-Werts.

### Spezialrenderer

Der Spezialrenderer beginnt bei RVA `0x45820`. Der reale RedBird-Span des Prologs beträgt 19 Byte. Er wird im Hauptrenderer ausschließlich an RVA `0x44E3C` und `0x44EC3` aufgerufen.

Für beide Aufrufe gilt:

- Argument 2 in `EDX` ist die Building-ID.
- Argument 5 ist die Tile-ID.
- Argument 4 in `R9D` und Argument 7 auf dem Stack enthalten vertikale Koordinaten.

Die frühere tilebasierte Korrektur zog die aktuelle Tilehöhe ab. Auf geneigten Footprints verschob dies Teile desselben starren Objekts unterschiedlich. Der gemeinsame Vertrag verwendet stattdessen `H - 8`.

### Animierter Rendererpfad

Der Argumentblock bei RVA `0x43BA2` besitzt einen realen RedBird-Span von 17 Byte; der Vanilla-Aufruf folgt bei `0x43BB3` und führt nach `0x488B0`.

Relevante Register:

- `EDX` beziehungsweise zuvor `R12D`: Building-ID.
- `R15D`: Tile-ID als Argument 5.
- Stackargument 6: vertikaler Offset.

Vanilla übergibt auf niedrigem Terrain den bestehenden Wert aus `ESI`. Für eine erhöhte Zugbrücke muss Argument 6 einheitlich `8 - H` sein.

### Statischer Rendererpfad

Der Block bei RVA `0x44EDD` besteht aus drei vollständigen Instruktionen und einem realen RedBird-Span von 16 Byte:

```text
sub R10D,[CurrentRenderedTileHeight]
mov EDX,EDI
mov R9D,[CurrentRenderBase]
```

Vanilla verwendet hier `shadow - currentTileHeight`. Für erhöhte Zugbrücken wird daraus `shadow - H`. `EDI` enthält die Building-ID und wird anschließend wie in Vanilla nach `EDX` kopiert. Der Aufruf bei `0x44F09` und der Renderer bei `0x4C1D0` bleiben unverändert.

Dieser dritte Grafikpfad war der fehlende Teil der früheren Korrekturen. Solange nur Spezial- und Animationspfad angepasst waren, lagen statische und animierte Bestandteile auf unterschiedlichen Höhen.

### Einheitenhöhe

Der gemeinsame Höhenwriter für Einheiten liegt in `FUN_180184FD0`; der relevante Block beginnt bei RVA `0x18511C` und besitzt einen realen RedBird-Span von 19 Byte. Der Hook liegt bereits hinter Vanillas Prüfung auf Gebäudetyp `0x31`, also Zugbrücke.

Vanilla schreibt auf niedrigen Brücken:

```text
unit+0x714 = 8 - unit+0x712
```

Die Type-2-Renderpfade bei `0x43EF3` und `0x44346` konsumieren diesen Wert. Der Type-52-Interpolationsdatensatz übernimmt denselben bereits berechneten Höhenparameter bei `0x1A22C4`. Daher ist kein eigener Bewegungs- oder Interpolationshook erforderlich.

Für erhöhte Zugbrücken lautet der gemeinsame Vertrag:

```text
unit+0x714 = H - unit+0x712
```

Damit ergibt sich unabhängig vom konkreten Brückentile:

```text
unit+0x712 + unit+0x714 = H
```

Die Einheit steht somit auf derselben starren Oberfläche wie Deck und Ketten. Für `H <= 12` oder bei deaktiviertem Feature werden die drei verdrängten Vanilla-Instruktionen exakt ausgeführt.

Die relevante Register- und Feldzuordnung:

- `EDI`: bereits von Vanilla ermittelte Building-ID.
- `RBP`: ImageBase.
- `RBX`: Unit-Slot-Anker aus Managerbasis plus Game-ID × `0x490`, nicht direkt der Anfang des `GameUnit`-Records.
- Native Operanden `RBX+0x712` und `RBX+0x714` entsprechen den Recordfeldern `+0xB6` und `+0xB8`.

## Liveprozess-Befunde

Die Untersuchung erfolgte ausschließlich lesend mit `OpenProcess(PROCESS_VM_READ)` und `ReadProcessMemory`. Es wurden weder Spielspeicher verändert noch Code injiziert. Details und Best Practices stehen in [LiveProcessReadOnlyInspection.md](LiveProcessReadOnlyInspection.md).

Im Testprozess wurden unter anderem Zugbrücken mit folgenden gespeicherten Gebäudehöhen beobachtet:

```text
H = 8, 64, 80, 125, 130
```

Damit waren sowohl niedrige, flache erhöhte als auch geneigte Footprints vertreten. Insbesondere die Werte `64` und `125` belegten, dass ein einzelner Tilewert keine stabile Referenz für alle Teile eines starren Zugbrückenobjekts ist.

Ein diagnostischer Torhaus-HeightGrid-Wert `98` war nur eine Momentaufnahme und wurde nicht hardcodiert. Ebenso wurden Building-IDs und konkrete Test-Tile-IDs ausschließlich zur Korrelation benutzt, nicht als Produktivvertrag.

Ein weiterer Livebefund zum fehlerhaften Unit-Hook:

- Bei einem Tile mit echter Höhe `80` lieferte die falsche Adressdomäne den Wert `0`; die Einheit blieb im Vanilla-Zweig und erschien unter Wasser.
- Bei einem Tile mit echter Höhe `130` lieferte dieselbe falsche Adresse zufällig `173`; der erhöhte Zweig wurde genommen und die Einheit erschien korrekt.

Das scheinbar höhenabhängige Verhalten war damit kein besonderer Rendererfall, sondern ein eindeutiger Adressraumfehler.

## Funktionierende Lösung

Dieser Abschnitt beschreibt die implementierte, statisch vollständig verifizierte und anschließend im Spiel bestätigte Lösung.

### 1. Tiletopologie beibehalten

Die beiden Zugbrücken-HeightGrid-Pfade verwenden weiterhin das zum konkreten Brückentile gehörende `DefaultHeightGrid`:

```text
if feature inactive or defaultHeight <= 12:
    Vanilla-Höhe 0
else:
    HeightGrid = defaultHeight - 8
```

Damit bleiben Grabenboden, Wasser, Pathfinding, Öffnen/Schließen, Laden und Abriss in Vanillas vorhandener Tilelogik.

### 2. Eine gemeinsame starre Gebäudehöhe verwenden

Alle grafischen Zugbrückenteile beziehen sich auf `H = buildingRecord+0x148`:

| Pfad | Erhöhte Korrektur |
|---|---|
| Spezialrenderer `0x45820` | Argument 4 und 7 jeweils um `H - 8` nach unten verschieben |
| animierter Argumentpfad `0x43BA2` | Argument 6 auf `8 - H` setzen |
| statischer Argumentpfad `0x44EDD` | `shadow - H` statt `shadow - currentTileHeight` |
| Unit-Hook `0x18511C` | `unit+0x714 = H - unit+0x712` |

Für deaktiviertes Feature oder `H <= 12` wird in allen vier Pfaden exakt Vanilla ausgeführt.

### 3. Keine eigene Renderpipeline einführen

ExtraFeatures erzeugt keine eigenen Sprites, Queueeinträge, Sortierwerte, Animationen oder Interpolationsdatensätze. Es ersetzt nur die falschen Höhenargumente, bevor die vorhandenen Vanilla-Renderer weiterlaufen.

Unverändert bleiben insbesondere:

- Zugbrückenzustand und Animation;
- Hochziehpfad;
- Grafikrefresh;
- Pathfindingrefresh;
- Wasserlogik;
- Blockierungsflags;
- Laden und Rekonstruktion;
- Abriss und Wiederherstellung der Default-Höhen.

### 4. Atomare, statisch verwurzelte Hooktransaktion

Der neue statische Rendererhook ist Teil derselben atomaren Elevated-Moat-Transaktion. Insgesamt enthält sie 20 Hooks. Eine Signatur-, Vertrags-, Generator- oder Spanabweichung lässt die gesamte Veröffentlichung fail-closed scheitern.

Veröffentlichte Hooks und Flags bleiben statisch beziehungsweise prozessweit verwurzelt. `Dispose()` wird nur verwendet, um einen noch nicht veröffentlichten, fehlgeschlagenen Transaktionskandidaten zurückzurollen.

### 5. Verifikation

Vor dem Build wurden die produktiven Generatoren mit der tatsächlich installierten RedBird-/Iced-Version assembliert und vollständig dekodiert. Bestätigte Spans:

| Hook | Span |
|---|---:|
| Fertigstellung `0x73B35` | 17 Byte |
| Absenken `0x64546` | 15 Byte |
| Spezialrenderer `0x45820` | 19 Byte |
| animierter Pfad `0x43BA2` | 17 Byte |
| statischer Pfad `0x44EDD` | 16 Byte |
| Unit-Höhe `0x18511C` | 19 Byte |

Die Tests prüfen unter anderem:

- kanonischen DLL-Hash;
- exakte Instruktionsgrenzen und Operanden;
- Building-ID-Liveness;
- Gebäude-Stride und Höhenfeld;
- vollständige Footprint-Mittelwert-Weitergabe;
- Fortsetzungsadressen;
- fehlende eingehende Sprünge in Hookspans;
- echte RedBird-`DisplacedByteCount`-Werte;
- Generatorassembly und vollständiges Decoding;
- fehlende, mutierte und doppelte Signaturen;
- fail-closed Verhalten mutierter Native-Fixtures;
- Vanilla-Zweige für niedrige Brücken;
- Formeln für `H = 8, 12, 13, 64, 80, 125, 130, 255`.

Der letzte Vorbuild-Lauf bestand 7.562 Assertions. ExtraFeatures wurde anschließend ohne Warnungen oder Fehler gebaut und installiert; lokale und installierte DLL waren bytegleich. Die Version blieb `1.0.97`.

## Nicht funktionierende oder verworfene Ansätze

### Rohwert `+1` als sichtbare Höhenstufe

Die erste Annahme war, das Brückentile müsse lediglich um einen Rohwert angehoben werden. Das war falsch: Eine sichtbare Geländestufe entspricht acht Rohwerten. `+1` konnte die sichtbare Abweichung daher nicht korrekt beheben.

### Brückenhöhe `defaultHeight - 7`

Aus „Grabenhöhe plus eins“ wurde zeitweise `defaultHeight - 7` abgeleitet. Auch das vermischte sichtbare Stufen mit Rohwerten und führte zu einer falschen Deckhöhe.

### Brückentile auf die ursprüngliche Default-Höhe setzen

`HeightGrid = DefaultHeightGrid` hob Einheiten zwar an, zerstörte aber Vanillas Topologie: Das Zugbrückentile gehört zum abgesenkten Grabenboden, während der sichtbare Deckversatz separat gerendert wird. Wasserflächen, Brett und Ketten passten dadurch nicht mehr zusammen.

### Gebäudehöhe direkt als Tilehöhe oder `buildingHeight + 8`

Die Gebäudehöhe ist die richtige Referenz für das starre Objekt, aber nicht für das HeightGrid des Graben-/Brückentiles. Sie direkt oder mit `+8` in das Tile zu schreiben, koppelte zwei getrennte Vanilla-Verträge falsch zusammen. Das Brett wurde angehoben, blieb aber relativ zu Torhaus und Graben fehlerhaft.

### Nur die beiden HeightGrid-Writes ändern

Die Tilehöhe allein reicht nicht aus. Vanillas Spezial-, Animations- und statische Renderpfade behandeln Höhen unterschiedlich. Ohne konsistente Grafikargumente fehlte die Grafik, lag zu tief oder zeigte gegeneinander versetzte Teile.

### Nur Spezialrenderer und animierten Pfad korrigieren

Dieser Ansatz brachte Deck und Wasser zeitweise fast an die richtige Stelle, ließ aber den statischen Pfad bei `0x44EDD` unverändert. Die Folge waren scheinbar doppelte Ketten und je Terrainhöhe unterschiedlich versetzte Bretter.

### Aktuelles HeightGrid als gemeinsame Renderreferenz

Das aktuelle HeightGrid ist tilebezogen. Auf flachen Flächen kann dies zufällig richtig wirken; bei geneigten Footprints verschiebt es Bestandteile desselben starren Objekts unterschiedlich. Die Livewerte `H=64` und `H=125` machten diese Schwäche sichtbar.

### Nur über `current HeightGrid > 12` klassifizieren

Eine erhöhte Default-Höhe von beispielsweise `13` wird als Grabenboden zu `5`. Eine Klassifikation anhand des aktuellen HeightGrid-Werts würde diese erhöhte Zugbrücke fälschlich als Vanilla behandeln. Klassifikation und Renderwert müssen daher getrennt werden. In der finalen Lösung übernimmt `H > 12` die gemeinsame Objektklassifikation.

### DefaultHeightGrid in Render- und Unit-Generatoren

DefaultHeightGrid ist für die tileweise Topologie richtig, aber nicht die gemeinsame starre Referenz. Die drei Renderer und der Unit-Hook dürfen daher nicht jeweils anhand lokaler Default-Tiles ihre Objektverschiebung berechnen.

### Falscher Registervertrag im Absenkpfad

Im Absenkpfad bei `0x64546` ist `RDI` die echte Tile-ID und `RBX` die Managerbasis. `RSI` als Tile-ID zu behandeln schrieb an die falsche Adresse. Zusätzlich muss die nachfolgende Vanilla-Instruktion `lea RDI,imageBase` erhalten bleiben.

### Unvollständige Emulation kurzer Vanilla-Blöcke

Ein zwischenzeitlicher Callback ersetzte den 15-Byte-Absenkblock vollständig. Das erhöhte Komplexität und Fehlerfläche unnötig. Entscheidend ist, den realen RedBird-Span und alle verdrängten Instruktionen registerkorrekt zu behandeln, nicht nur den scheinbar interessanten Byte-Write.

### Falsche Adressdomäne im Unit-Hook

Der Zugriff `[RBP + tileId + 0xD7E5A0]` war ungültig, weil `RBP` die ImageBase ist, `0xD7E5A0` aber ein managerrelativer Offset. Dieser Fehler erzeugte zufällige, höhenabhängig wirkende Ergebnisse. Der korrigierte Zwischenstand verwendete zwar die image-relative Grid-RVA, wurde später aber durch die gemeinsame Gebäudehöhe als bessere Objektquelle ersetzt.

### Eigene Einheitenrenderung, Sortierung oder Bewegungspatches

Diese Erweiterungen waren nicht nötig. Die tatsächlichen Type-2-Pfade und die Type-52-Weitergabe konsumieren bereits den gemeinsamen Vanilla-Höhenparameter. Der Fehler lag im Writer von `unit+0x714`, nicht in mehreren unabhängigen Renderern.

### Interpretation von RVA `0x4522E` als Einheitensprite

Diese frühere Vertragsannahme war falsch. Dort entsteht ein Type-9-Datensatz, kein Type-2-Einheitensprite. Die tatsächlichen Type-2-Aufrufe liegen bei `0x43EF3` und `0x44346`.

### Fehlerhafte Renderer-Signatur

Eine Laufzeitsignatur enthielt `C0 A7 FB FF` statt Vanillas `C3 A7 FB FF`. Dadurch konnte der Renderer-Hook nicht aufgelöst werden. Weil die Installation atomar ist, wurde anschließend das gesamte Elevated-Moat-Feature korrekt fail-closed deaktiviert. Auflösungsfixture und produktive AOB dürfen deshalb nicht getrennt gepflegt werden.

### Mehrere Sprites mit echter Doppelzeichnung verwechseln

Die sichtbaren doppelten Ketten legten zunächst nahe, dass derselbe Renderer mehrfach aufgerufen werde. Baseline und Livezustand zeigten stattdessen mehrere beabsichtigte Zugbrücken-Unterteile mit inkonsistenten Höhen. Das Unterdrücken vermeintlich doppelter Renderaufrufe wäre falsch gewesen.

### Torhaushöhen hardcoden

Livewerte wie `80`, `98`, `125` oder `130` sind Diagnosewerte, keine stabilen Konstanten. Kleine und große Torhäuser sowie geneigte Footprints können andere Werte besitzen. Vanilla stellt mit `buildingRecord+0x148` bereits die korrekte allgemeine Referenz bereit.

## RedBird-Erkenntnisse

`HookSize` ist bei der installierten RedBird-Version eine Mindestlänge, keine exakte Überschreiblänge. RedBird dekodiert mindestens 14 Byte und rundet auf vollständige Instruktionen auf. Daraus folgten unter anderem:

- Eine angeforderte 9-Byte-Stelle am Fertigstellungswrite würde 23 Byte verdrängen.
- Der sichere Fertigstellungsblock beginnt deshalb bei `0x73B35` und umfasst 17 Byte.
- Der Absenkblock umfasst vollständig 15 Byte.
- Die Rendererprologe besitzen reale Spans von 19, 17 und 16 Byte.

Für jeden Hook müssen daher geprüft werden:

- `DisplacedByteCount`;
- vollständige Instruktionsgrenzen;
- Originalreihenfolge;
- Call-/LEA-Ziele;
- Register- und Flag-Liveness;
- Fortsetzungsadresse;
- eingehende Sprünge in das Innere des Spans;
- tatsächliche Assemblierbarkeit des produktiven Generators.

RedBird erlaubt außerdem höchstens ein gebundenes Label je erzeugter Instruktion. Gemeinsame Fallbackpfade dürfen nicht durch zwei unmittelbar aufeinanderfolgende Labels konstruiert werden.

## Abriss und Wiederherstellung

Die Abrisspfade waren bereits korrekt und wurden nicht umgeschrieben. Sie stellen jedes betroffene Tile anhand seines eigenen `DefaultHeightGrid` wieder her. Das ist besonders bei geneigten Footprints wichtig: Eine gemeinsame Gebäudehöhe darf niemals für die Wiederherstellung der einzelnen Geländetiles verwendet werden.

Der Regressionstest muss beide Zustände umfassen:

- abgesenkte Brücke abreißen;
- hochgezogene Brücke abreißen;
- Objekt und Grafik verschwinden;
- Pathfinding wird aktualisiert;
- jedes Tile erhält seine ursprüngliche Default-Höhe.

## Bestätigte Laufzeitabnahme

Der neueste Lauf nach dem finalen Build bestätigte die Lösung im Spiel:

- Zugbrücken auf den getesteten unterschiedlichen Terrainhöhen wurden korrekt dargestellt.
- Deck und Ketten erschienen ohne die vorherigen gegeneinander versetzten Schein-Duplikate.
- Einheiten standen sichtbar auf der Zugbrückenoberfläche.
- Mehrere Zugbrücken wurden während desselben Laufs erzeugt und wieder abgerissen.
- Die Elevated-Moat-Transaktion installierte alle 20 Hooks: `20/20 installed, 0 failed`.
- Spezialrenderer, animierter Renderer, statischer Renderer und Unit-Höhenhook wurden erfolgreich kompiliert und aktiviert.
- Im frischen BepInEx-Abschnitt traten keine Elevated-Moat-Signatur-, Vertrags-, Generator-, Hook- oder Callbackfehler auf.
- Die spätere Feature-Deaktivierung im Log erfolgte erst mit dem Missionsende und ist der normale fail-closed Zustandswechsel außerhalb einer aktiven Karte.

Für künftige Regressionstests sollten weiterhin eine niedrige, eine flache erhöhte und eine geneigte erhöhte Zugbrücke berücksichtigt werden. Sinnvolle Prüfpunkte bleiben offener, geschlossener und animierter Zustand, stehende und laufende Einheiten, Laden beziehungsweise Rekonstruktion sowie Abriss in beiden Zuständen.

Eine Zugbrücke lässt sich logisch nicht hochziehen, solange Einheiten auf ihr stehen. Ein ausbleibender Zustandswechsel in diesem Fall ist Vanilla-Verhalten und kein Grafikfehler.
