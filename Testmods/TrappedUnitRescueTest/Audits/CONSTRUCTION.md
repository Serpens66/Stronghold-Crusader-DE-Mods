# Bauquellen und endgültiger Mutationsabschluss

Offline-Audit, 25.11.2026. Keine Installation, kein Spielstart, keine Änderungen an Runtimecode oder fremden Quellen. Dieser Bericht trennt belegte Beobachtungsgrenzen von einer noch zu implementierenden allgemeinen Rettung. Die Feststellungen sind keine neue Ingame-Abnahme.

## 1. Identität, Verfahren und Beleggrenzen

Die kanonische installierte `CrusaderDE.dll` wurde mit `Get-ApiRepositoryHash` erneut gelesen. SHA-256 und beide Identitäten aus `CURRENT.json` / `DATABASE_INFO.json` stimmen überein:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Alle nativen Adressen unten sind RVAs in genau diesem Bild; VA = `0x180000000 + RVA`. Native Funktionsnamen des Decompilers bleiben Metadaten, deren Namen allein keine Semantik beweisen. Die hier beschriebenen Seitenwirkungen stammen aus vollständigen Funktionskörpern, aufgerufenen Schreibern und unabhängigen Lesern sowie den vorhandenen hashgebundenen Vanilla-Audits. Kritische Unit-Relocation-/Delete-Aufrufe und der Konstruktorabschluss wurden zusätzlich direkt aus der aktuellen PE disassembliert; kompakte Instruktionsanker: [construction-native-evidence.txt](construction-native-evidence.txt).

Die normalen Abfragen erfolgten über den von `CURRENT.json` ausgewählten Nur-Lese-Einstieg `query.ps1 function <RVA>` und die ausgegebenen Caller/Callees. Exakte aktuelle Datensätze wurden nach `binary_hash` und `rva` ausgewählt; historische gleichnamige Funktionen zählen nicht als aktuelle Belege. Der aktuelle Decompiler liefert für `0x77E60` keinen Körper. Die erweiterte Flowabfrage bestätigt `Flow exceeded maximum allowable instructions` mit einem ungültigen Decompiler-Leseziel. Deshalb wurde die Validierungsfunktion direkt bis zu ihrem tatsächlichen Rücksprung `0x798F2` gelesen. Der große Source-Freshness-Bericht dieses Werkzeugs ist kein zusätzlicher Vanilla-Befund; er erklärt den langsamen Abfragestart. Keine falsche Vollständigkeit aus einem leeren Pseudocode ableiten.

Zum Lesen beobachteter Extender-Quell-HEAD: `81a7a945c47c2c5dcc347cd1ad3661a54be02429`. Die beiden relevanten Dateien `BulkAIDetours.cs` und `BulkBuildingDetours.cs` haben gegenüber der im Dataset genannten Extender-Identität `4014ca67c96974a5e6d697665baf5351cbfebde6` keinen Diff. Das ist ein gezielter Beleg ihrer aktuellen Quellverträge, keine Erneuerung sämtlicher Dataset-Provenienz. Installierte Assembly-/Backendverträge und der gemeinsame Provenienzabschluss stehen im übergeordneten Audit.

## 2. Ergebnis der Eintritts- und Abschlussprüfung

| Quelle | Tatsächlich wirksamer Eintritt | Passender Abschluss | Urteil für automatische Rettung |
|---|---|---|---|
| Vorbereiteter AIV-Frame, regulär und Prebuild | `0x539B0` beziehungsweise `0x55F50` → `0x51790` | Rückkehr der gesamten einzelnen `0x51790`-Invocation, sofern Original normal zurückkehrte | Vorhandener APIShared-Broker ist ein gültiger synchroner äußerer Rahmen. Eine erfolgreiche Rückkehr ist allein kein Bau-/Geländeänderungsbeweis. |
| AIV-Mauer-/Treppenframe | `0x51790` → gegebenenfalls Cleanup `0x5CD90` → pro Kachel `0x6CB20` | Rückkehr der jeweiligen effektiven `0x6CB20`-Originalinvocation; zusätzlicher äußerer Frame verhindert verfrühte Mehrfachbearbeitung | Pre/Post allein reicht nicht als neuer generischer Transaktionsvertrag. Fixes-Pre und dessen Post-Höhenkorrektur berücksichtigen. |
| AIV- und prozeduraler KI-Gebäudebau | Verschiedene AI-Caller → gemeinsamer Konstruktor `0x6D580` | Rückkehr seines finalen nativen Originals; äußerer AIV-Rahmen kann weitere Arbeit enthalten | Bereits vorhandenen Extender-Eigentümer nutzen; ein zweiter nativer Entry-Detour ist unzulässig. |
| Menschengebäude über Chore | `0x12090` → `0x6D580` | Jede tatsächlich ausgeführte finale Originalinvocation mit ihren effektiven Argumenten | Beobachtung am Extender-Pre erfasst nicht alle Mirror-Originale und nicht notwendigerweise die endgültigen Argumente. |
| Menschenmauer über Chore | `0x11B50` → `0x6F230` | Rückkehr der gesamten Linieninvocation einschließlich aller begrenzten Teilbauten | Kein einzelner Tile-Refresh ist Linienabschluss; keine vollständige Linie aus bloßem Post annehmen. |
| Extender `CreatePrefab`, einschließlich CastlePlanner | Direkter Aufruf von `c_game_player_build_structure_hook_impl` → finales Original `0x6D580` | Derselbe einzelne Originalabschluss wie oben | Auch kostenloser Bau und Placement-Override müssen in einem allgemeinen Broker unabhängig vom Kosteninteresse sichtbar sein. |
| Spiegelbau | Eigene Compiler-Closure ruft für jede Spiegelung das Original auf, **vor** dem normalen Pre | Pro effektiver Originalinvocation; zusammenfassen erst beim äußersten Rahmen | Normales Pre/Post-Paar ist kein vollständiger Mirror-Broker. |
| Rohe Gebäudeallokation `Create` / `0xB47E0` | Rohes Kerngebäude, häufig noch `NeedsInit` | Kein allgemeiner kompletter Bauabschluss | Als Ersatzquelle abgelehnt: Allokation beweist keine fertigen Kacheln, Untergebäude, Topologie oder erfolgreichen gesamten Bau. |

Der Konstruktor `0x6D580` hat im aktuellen nativen Callgraph 22 direkte Caller: `0x12090`, `0x29B40`, `0x2ECD0`, `0x50E00`, `0x50F90`, `0x51190`, `0x51270`, `0x51540`, `0x51790`, `0x52270`, `0x575B0`, `0x59730`, `0x7DF60`, `0x7EB00`, `0x94350`, `0xC9EF0`, `0xE8340`, `0x1134D0`, `0x1137C0`, `0x113930`, `0x12E400`, `0x15ECE0`. Insbesondere ist er kein ausschließlich menschlicher Auftrag. Die Beobachtungsgrenze muss vom tatsächlichen Konstruktoraufruf ausgehen; die Bezeichnung des Extender-Adapters als `player_build_structure` ist keine Besitzer-/Threadfreigabe. Ebenso ist ein AIV-only-Broker kein Vollständigkeitsbeweis für alle 22 Quellen.

## 3. AIV: Parameter, Alternativen und echte Änderungen

`0x51790` erhält nativen AIV-Systempointer, Spieler-ID, Frameindex, Restricted-Mode und ein Byte für freie/erzwungene Ausführung. Native Frames enthalten Typ, Status/Delay, Anzahl und Index beziehungsweise Anchor in den vorbereiteten Kacheldaten. Ressourcen-/Status-/Delay-/Kapazitäts-/AI-Verfügbarkeitsprüfungen können vor einer Platzierung zurückkehren. `restrictedMode` begrenzt die zugelassenen Typen; `freeOrForced` umgeht nicht automatisch die gesamte Platzierungsvalidierung.

Die Funktionszweige sind unterschieden:

- Gewöhnliche Einzelgebäude: Mapper-/Strukturauflösung, Cleanup, Anschlussprüfung `0xC3BF0`, `0x6D580`, gegebenenfalls nachgelagerter AI-Refresh `0x31000`; anschließender Status und Retry-Delay. Ablehnung kann nach einer früheren Cleanup-Seitenwirkung auftreten.
- Gewöhnliche Mauergruppen und spezielle Höhenstücke: Schleife über vorbereitete Kacheln, Cleanup und `0x6CB20`. Der Frame kann mehrere Einzelkacheln ändern; eine frühe Unterinvocation ist kein Abschluss des gesamten Frames.
- Mehrfachbau-Mapper: Wiederholte `0x6D580`-Aufrufe, ohne dass der abschließende Rückgabewert ein zuverlässiger Zähler erfolgreicher Unterbauten wäre.
- Moat-Frame: pro vorbereiteter Kachel `0x59730` und späterer Arbeiterabschluss. Das Zeichnen des Auftrags ist kein fertig ausgehobener Graben; hierfür gilt der eigene Gelände-/Arbeiteraudit.
- Verarbeitete/gesperrte/abgewiesene Frames: kein Bau oder nur Delay-/Retryänderung. Keine Rettung aufgrund der Rückgabe `0` oder `1` allein.

`0x5CD90` ist kein reiner Test. Nach einem ersten Platzierungsscan kann er Gebäude entfernen, Slots/Goods-/Eigentümerverweise bereinigen und über `0x61FC0` / `0xC43A0` weitere Kacheln normalisieren. Ein zweiter Durchlauf kann bei geeigneten Fällen die Hülle um zwei Kacheln erweitern (`size+4`, Anchor jeweils `-2`; bei Größe 11 entsprechend `+2/-1`). Verknüpfte Gebäudeteile können ebenfalls betroffen sein. Deshalb muss ein allgemeiner äußerer AIV-Beobachter die reale Mutationsmenge beziehungsweise deren vollständige konservative Hülle berücksichtigen.

**Abgrenzung zum vorhandenen Piloten:** Daraus ist kein konkreter Fehler des auf den tatsächlichen neuen 5×5-Brückenstandort beschränkten Piloten nachgewiesen. Insbesondere wurde kein überlebender, durch die äußere Cleanup-Zone neu eingeschlossener Actor nachgewiesen. Die Hülle ist eine Allgemeinheits-/Kausalitätsgrenze für eine spätere Erweiterung; sie wird nicht als reproduzierter Coveragebug ausgegeben.

APIShared `Diagnostics/AivBuildStepCapability.cs:216–288` liefert einen passenden äußeren synchronen Receipt-Vertrag: TryBegin vor Original; Original genau einmal; Completion in `finally`; Flag `VanillaCompleted` nur bei normaler Rückkehr; Gewöhnliche Beobachterfehler werden isoliert; Completion in umgekehrter Reihenfolge. Die gezielt reproduzierte Ausnahme bei einem werfenden Exception.ToString und deren Auswirkungen auf Original/weitere Receipts steht in [AUDIT-FINDINGS.md, F1](AUDIT-FINDINGS.md#f1-fehler-beim-formatieren-eines-beobachterfehlers-durchbricht-die-aiv-isolation); dieser Fehlerpfad erhält hier keine uneingeschränkte Freigabe. Das heutige `GeneralRescueObserver.cs:30–59,86–101` beschränkt sich zusätzlich auf bekannte Session, Spieler/Village, Frame, genau eine Drawbridge-Anchor-Kachel und 5×5-Größe. Der Receipt ist einmalig und identitätsgebunden. Unbekannte Frames werden nicht versehentlich als unterstützt behandelt.

## 4. Gebäudekonstruktor und alle Creatorfamilien

### Gemeinsamer Pfad `0x6D580`

Der Konstruktor prüft gewöhnlich über `0x77E60`, außer ein vorgelagerter validierter Override ist aktiv. Eine Ablehnung beendet die Invocation vor dem eigentlichen Creator. Danach bereinigt er jede Haupt-Footprintkachel, verarbeitet Vegetation und vorhandene Einheiten, stellt temporäre Höhen/Flags wieder her, berechnet die gemeinsame Bauhöhe und dispatcht nach Strukturtyp. Nach dem Creator folgen je nach Flag Zahlungen/Buchhaltung, Bereichsinvalidierung `0xE3B90`, Dirtyflags und `0xD95A0`. Erst danach kehrt er an `0x6DFE7` zurück.

**Die ursprüngliche Einheit kann schon während dieses Pfads verschwinden.** Im Unit-Loop kann `0x6D85E` zu `0x19BA40` versetzen, `0x6D87A` zu `0x195D10` den Folgezustand herstellen und `0x6D8BA` zu `0x1869C0` löschen. Das hängt unter anderem vom Unittyp, dem eigentlichen Strukturtyp und dem Spieler-/Moduszustand ab. Strukturen wie Türme, Torhäuser und Brücken besitzen besondere Ausschlusszweige. Der konkrete maschinelle Delete-Aufruf ist bestätigt; eine Nachher-Rettung kann eine gelöschte Identität nicht bewahren. Sie muss den Receipt verwerfen, statt einen anderen Slot oder eine neu erzeugte Einheit als Rettung auszugeben. Das ist eine Vanilla-Präventions-/Entfernungsfrage und kein Beweis eines Fehlers unseres Rettungspiloten.

Die folgende vollständige Creator-Dispatchmatrix beschreibt die für Kandidatenentdeckung relevanten Schreibmengen. `scale` bezeichnet das effektive native Argument, nicht einen ungeprüften UI-Katalogwert. Strukturwerte sind ausschließlich Maschinen-/Dispatchwerte dieser DLL; zukünftiger Code nutzt die entsprechenden Extender-Enums.

| Creator-RVA | Dispatchfamilie in `0x6D580` | Geometrie / relevante Verzweigung |
|---|---|---|
| `0x75780` / `0x74C30` | Default; besondere Hovelvariante | Hauptquadrat; Hovel-/Typ-/Spielmodusvariation ändert Darstellung/Records, nicht den Grundsatz der Quadratregistrierung. |
| `0x72E10` | Strukturen 8/9/108 | Hauptquadrat **plus drei 5×5-Unterflächen** aus richtungsabhängigen Offsets `0x2D2E90..0x2D2EA4`; Kind-IDs, Kachelbereinigung und je eigener Refresh. |
| `0x76E80` | Struktur 10 und indirekt Keep-Teile | Vier 2×2-Komponenten aus `0x2D2FB8` plus neun zusätzliche Tabellenkacheln aus `0x2D3060`; nicht bloß ein Creator-Hauptrecord. |
| `0x76780` | Struktur 20 | Hauptquadrat; sucht einen zusätzlichen 2×2-Ort über `0xBD7A0`, höchstens elf Anläufe mit wachsender Suchhülle bis zur nativen Größenbegrenzung; kann **nach Hauptbau** ohne Anbau zurückkehren. |
| `0x73BB0` / `0x77400` | Strukturen 24/25 | Hauptquadrat plus ein 5×5-Kind aus richtungsabhängiger Tabelle `0x2D2E98/9C`; eigene Bereinigung/Registrierung. |
| `0x75B00` | Struktur 28 | Hauptquadrat plus 4×4-Kind aus `0x2D2EF0/F4`; beide Kind-/Elternverbindungen und Refreshes. |
| `0x776F0` | Struktur 30 | 3×3-Haus, vorbereitende 9×9-Flagfläche `0x72490`, 36 Kacheln aus einer von zwei Tabellenvarianten bei `0x2D37C0`. |
| `0x74410` | Struktur 31 | 3×3-Haus, 9×9-Vorbereitung, 24 Kacheln aus einer von zwei Tabellenvarianten bei `0x2D3C40`; Variantenzähler wird erst am Ende weitergeschaltet. |
| `0x72A50` | Struktur 32 | 3×3-Haus, 10×10-Vorbereitung, acht Tabellenorte bei `0x2D3DC0` einschließlich Vegetationserzeugung. |
| `0x73450` | Struktur 33 | 3×3-Haus, 10×10-Vorbereitung, 27 Tabellenorte aus vier Varianten `0x2D3E00` mit 12-Byte-Stride; 23 davon erhalten zusätzliche Farmflags/Buildingbindung. |
| `0x76C00` | Struktur 39 | Hauptquadrat, struktur-/mapperabhängige Variation als zusätzliches Argument. |
| `0x74DA0` | Root-Keeps 40–44 | Hauptquadrat, drei Richtung-/Typmarker, 7×7-Campground und 5×5-Unterteil; optional zuvor `0x5D3A0` zum Entfernen vorhandener Teile. Tatsächlicher siebter Creatorparameter bestimmt Richtung. |
| `0x74240` / `0x74080` | Strukturen 45/46 | Hauptquadrat mit hoher Wall-/Owner-/Belegungsregistrierung und Portal-Registrierung `0xD8510`; Richtung/Art beeinflussen Portaldaten. |
| kein Creator | Struktur 47 | Der gemeinsame Vorpfad kann dennoch bereits verändert haben; ein fehlender Creator ist kein transaktionaler No-op-Beweis. |
| `0x739C0` | Drawbridge-Struktur 49 | Geordnetes 5×5-Quadrat; Mapper `0x2D1A30` entscheidet Deck/Abutments; pro Deckkachel gegebenenfalls Moat-Task, Clear/Heightänderung, dann gesamte Grafik-/Topologieveröffentlichung. |
| `0x770F0` | Strukturen 54/80–84 | Hauptquadrat mit Sperr-/Belegungsregistrierung und abschließendem Refresh. |
| `0x73EA0` | Struktur 66 sowie 100/101 | Hauptquadrat; Mapper-/Untertypumrechnung im zentralen Dispatcher erhalten. |
| `0x75DF0` | Struktur 67 | Hauptquadrat; Typ entscheidet Höhenabsenkung und Terrainänderung über `0x6FE90`. |
| `0x59380` | Struktur 68 | Eigenständiger kleiner Tile-/Taskrecord; nur bei gültigem Ort/freiem Slot beziehungsweise bestehendem Record; setzt Höhe, Flags und Updatezustand, kein gewöhnlicher Root-Building-Erfolg. |
| `0x76D40` | Struktur 69 | Hauptquadrat mit speziellem Strukturflag, danach vollständiger Refresh. |
| `0x77230` | Strukturen 74–78 | Hauptquadrat; Tabellen wählen Eingangskacheln gegenüber Torhausmaske. |
| `0x76060` | Strukturen 91/104 | Hauptquadrat; Terrainzweig nach Untertyp; bestimmte Untertypen erzeugen zwei weitere Komponenten aus `0x2D2FE0/2D3020`. |
| `0x737E0` | Struktur 109 | Hauptquadrat, Mehrheitsprüfung der ursprünglichen Wasserflags, Höhe null und neue Terrain-/Gebäudezuordnung. |

Die Matrix schließt den früher offenen Punkt „ist BuildingScale stets die vollständige Hülle?“ mit **Nein**. Das ist statisch nachgewiesen und benötigt keinen Spieltest. Für einen freigegebenen gewöhnlichen Quadratcreator kann der künftige Broker die reine Quadratmenge verwenden; bei den übrigen Familien muss er vor der ersten Änderung die oben genannten Tabellen-/Unterflächen beziehungsweise die vollständige Suchhülle berücksichtigen. Eine Implementierung darf eine nicht implementierte Hüllenfamilie ausdrücklich ablehnen. Das ist ein geschlossenes Nichtfreigabeurteil, kein Erlaubnisersatz durch großzügiges Raten.

`0x69850` ist **keine nebenwirkungsfreie Enumeratorhilfe**: Sie schreibt Anzahl und aktuelle X/Y-/Richtungs-Offets in gemeinsames TileManager-Scratch (`+0x204E758..768`). Ein Callback, der sie zur Hüllenberechnung aufruft, kann Vanillas laufende Schleife beschädigen. Hüllen deshalb aus geprüften Tabellen rein lesend ableiten oder die Werte außerhalb eines aktiven Scratchnutzers erfassen; niemals als unauffällige Suchhilfe innerhalb eines fremden Originalrahmens verwenden.

## 5. Mauerbau: KI und Menschen sind unterschiedliche Verträge

### KI `0x6CB20` und Extender/Fixes

`0x77CF0` setzt zuerst die Platzierungsablehnung. Nur nach allen Gelände-/Gebäude-/Nähetests wird sie zurückgesetzt. Die Funktion prüft eine aktuelle Unit-Kette und enthält typabhängige Sonderbehandlung; das ist kein allgemeines Verbot des KI-Bauens auf eigenen Einheiten. Ein abgelehnter Call verändert keine neue Mauer. Im Erfolgszweig schreibt `0x6CB20` Höhe, Wall-/Typflags, Kodierung des Besitzers und Redrawdaten; danach `0xE0850` → `0xD90D0`, `0x5B020`, `0xE3B90` und Kosten-/Dirtyzustand. Es gibt hier keinen mit `0x6D580` identischen allgemeinen Unit-Erhaltungspfad.

`BulkAIDetours.cs:155–185` erzeugt Pre in `try`, fängt Pre-Fehler und ruft Original im `finally`. Damit läuft Original auch nach Pre-Ausnahme mit dem bis dahin vorhandenen Eventobjekt beziehungsweise den ursprünglichen Werten. Post folgt im selben `finally` nach Original; bei einem Originalfehler wird Post nicht erreicht. Post konstruiert **ursprüngliche** Argumente neu, obwohl Pre Spieler/Koordinaten/Mapper ändern kann. Ein künftiger Broker benötigt die effektiven Werte am Originalcall, nicht die alleinige Post-Kopie. Es gibt keine unabhängige threadübergreifende Warteschlange zwischen Pre und Original.

Fixes `Events/FixesAIEvents.cs:109–229`, bei aktivem `EnableUnstuckUnitsOnAIBuiltWall` registriert in `Bootstrap/Plugin.cs:107–110`, führt in **Pre** die bisherige Einheitenrettung aus; Post kann danach benutzerdefinierte Mauerhöhen schreiben. Die Pre-Rettung berücksichtigt nur tatsächliche Current-Koordinaten und auf Wunsch Belagerungsgeräte, mit eigenständiger Behandlung von Gerät/Ingenieuren. Seine Aktivierung wird hier nicht verändert. Ein AIV-Capture allein berechtigt nicht zur Unterdrückung dieser Prävention: Wenn unsere Nachher-Rettung noch nicht erfolgreich war, würde eine pauschale Unterdrückung einen vorhandenen Schutz entfernen. Der automatische GeneralRescueObserver unterdrückt deshalb nur im eng begrenzten eigenen synchronen Rettungsaufruf; seine normalen AIV-/Vanilla-Originale bleiben ununterdrückt. Die kontrollierte Fixture hat einen anderen Vertrag: Fixture.cs:249–261 hält ihre Sperre bei aktivem Ersatz um die gesamte Testaction einschließlich Bau und löst sie in finally wieder. Solche kontrollierten Ersatztests beweisen keine ununterdrückte natürliche Fixes-Reihenfolge. Eine spätere wirkliche Ersatzfunktion benötigt einen passenden früheren Eingriff und eigene abgeschlossene Garantie für diesen Fall.

### Menschen `0x6F230`

`0x7A3B0` prüft die Linien-/Maueranforderung. Im zugelassenen Call endet die Linie je nach Typ/Maximalanzahl/Kosten/Höhenbudget beziehungsweise ersten verbauten Zellen teilweise früher. Normale Mauern/Treppen wählen unterschiedliche Vorschubregeln; schon vorhandene Teile können nur umgewandelt statt neu angelegt werden. Pro tatsächlich geänderter Kachel werden Höhe, Wall-/Ownerflags, Vegetation und Units behandelt. Einige Unittypen werden gelöscht oder zum Keep versetzt, andere erhalten den nativen Endzustand 110. Danach erfolgen Kachel-/Nachbaraktualisierung; erst nach der gesamten Schleife Kosten, Maske-/Richtungsrefresh und Dirtyflags.

Die Quellenfolge einer Linie ist daher eine **geordnete Folge tatsächlich wirksamer Teiländerungen**, kein garantiert vollständig verändertes Rechteck zwischen Endpunkten. Für Capture vor Original ist eine konservative Linienhülle zulässig; für spätere Rettungszulassung ist die tatsächliche physische Änderung und derselbe überlebende Actor erforderlich. Die Linie kann legitim ohne jede Änderung zurückkehren.

## 6. Menschenbau, Spiegelung und vorhandener APIShared-Owner

`BulkBuildingDetours.cs:876–914` und `:984–1022` führen Mirror-Originale vor dem normalen Pre aus. Die Mirror-Closures besitzen jeweils einen eigenen Originalgetter und verwenden die berechneten Spiegelkoordinaten. Nach normalem Pre gehen die editierten Eventfelder ins normale Original. Post wird aus den ursprünglichen Methodenargumenten erzeugt, nicht aus den finalen Eventfeldern. Beide EventArgs-Typen erben zwar `SkipOriginalFunction`, ihre gezeigten Wrapper werten es aber nicht aus; ihre `ReturnValue`-Properties sind unveränderlich mit 0 initialisiert und werden im Post nicht mit dem Originalresultat befüllt. Keine dieser beiden Methoden hat einen Abschluss-`finally`, der unabhängig vom Originalergebnis zuverlässig ein Post garantiert. Ein `SkipOriginalFunction`-Versprechen darf für diese Implementierungen nicht erfunden werden; der gezeigte BuildStructure-Pfad prüft diesen Schalter nicht.

APIShared `Buildings/ConstructionCosts/HumanConstructionRuntime.cs:82–148` hat die entscheidende Stelle bereits sauber getrennt: Es transformiert den einzelnen finalen Originalgetter und bei Build zusätzlich die Compiler-Closures des MirrorTool. Der typisierte Ersatz erhält die **effektiven tatsächlichen Argumente**. Das ist ein geeigneter vorhandener Besitzpunkt für einen separaten allgemeinen Bau-Beobachtungsvertrag. Ein konkurrierender nativer Hook auf `0x6D580` würde Eigentümerschaft und Mirrors nicht verbessern.

Der heutige Runtime-Service ist aber noch kein solcher Broker: `Interested` (`:150–159`) und `Build` (`:295–312`) hängen an `ready`, Kosteninteresse und der menschlichen bezahlten Session. KI, Editor, kostenloser Bau und viele No-demand-Pfade können direkt ins Original gehen. Seine öffentliche Kosten-/Receipt-Schnittstelle darf nicht funktional als Rettungsereignis missbraucht werden. Eine Erweiterung müsste Rettungsinteresse unabhängig von Kostennachfrage behandeln, Original weiterhin genau einmal ausführen und Beobachterfehler isolieren. `[ThreadStatic]`-Frames/Scope-Guards (`HumanConstructionRuntime.Scopes.cs:13–98`) belegen synchrone verschachtelte Zuordnung, keine allgemeine Thread-Autorisierung.

Die native Platzierung arbeitet mit gemeinsamen Scratchfeldern. Pre-/Post-Publisher, Compiler-Closures und zusätzliche Mod-Aufrufe laufen synchron auf dem jeweiligen aufrufenden Thread. Daraus folgt **nicht**, dass ein beliebiger fremder `CreatePrefab`-Aufruf auf einem Workerthread zulässig wäre. Ein allgemeiner Broker darf dort keine zweite Weltmutation starten. Seine Kandidaten werden im selben gültigen Simulationskontext erfasst und erst an einem belegten äußeren Abschluss beziehungsweise der passenden Simulationstaktung abgearbeitet; UI-/Hintergrundaufrufe benötigen ihren eigenen autorisierten Kontext. Kein Rückruf darf Noesis verändern oder einen nativen Callback blockierend zum Mainthread schicken.

## 7. Negative Fälle und Widerlegungsprüfung

| Vermutung | Geprüfter Gegenpfad | Ergebnis |
|---|---|---|
| AIV-Rückgabe 0 heißt unveränderte Welt | Cleanup/mehrere Unterbauten/Delaypfade | Verworfen: Rückgabe ist kein Rollbackbeweis. |
| `CreatePrefab != 0` oder BuildStructure-Post beweist Erfolg | Native `0x6D580` und `0x6F230` haben keinen stabilen booleschen Erfolgsreturn; Caller verwenden Scratch-/Identitydaten; Post ohne Ergebnisse | Verworfen: Geometrie-/Identitätsbelege erforderlich. Extender-Kommentar „typically indicates success“ ersetzt diesen Vertrag nicht. |
| BuildingSpawn(Post) ist finaler Bauabschluss | Allokation `0xB47E0` vor Kachelregistrierung/Kindern/Topologie | Verworfen: nur Identitäts-/Allokationsquelle. |
| Pfadrefresh heißt fertige Welt | `0x725E0` wird pro Kind aufgerufen; `0xE0850` und `0xD90D0` laufen in laufenden Schleifen | Verworfen: refresh ist Dirty-/Revisionssignal, kein Commit. |
| Nominales Quadrat deckt jeden Bau | Alle Creatorzweige oben, Cleanup und Keep-Kinder | Verworfen; nichtrechteckige Hüllen sind nachgewiesen. Kein daraus abgeleiteter neuer Pilotbug. |
| Ein falscher Current-Standort ist immer noch rettbar | Native Delete-/Relocate-Schritte vor Creator | Verworfen: Current+GlobalId+Life neu prüfen; gelöschte/neu verwendete Slots verwerfen. |
| Menschen können auf keiner Einheit bauen | `0x7B060` / `0x5BF80`, Editor-/AI-/Sondertyp-/Overridezweige; CreatePrefab bypass | Verworfen als globale Garantie. Reguläre menschliche Platzierungsablehnung bleibt autoritativ. |
| Platzierungsprüfungen sind stets rein lesend | `0x7B060` enthält typabhängige Unit-Timer/-Zustandsänderungen | Verworfen; keine wiederholte native Validierung als unsichtbarer dry-run. |
| Ein späterer größerer PCL beweist alten Fluchtbereich | laufende Dirty-/Refreshphasen, gemeinsame Scratchdaten und unterschiedliche Profile | Verworfen; alte physische lokale Kanten müssen vor der Mutation feststehen. |
| Fixes-Mauerhilfe ist nachher beliebig austauschbar | Fixes läuft vor der ersten Maueränderung; Vanilla kann Einheiten entfernen | Verworfen; Ersatzprävention und nachträgliche Rettung getrennt freigeben. |

Die bestehenden Rettungssnapshots lesen aktuelle Unit-Identität, Gesundheit, Gruppe und Current-Belegung. `RescueRuntime.cs:53–117` validiert alle Kachelketten vor dem teureren Reachability-Capture und begrenzt Besuche/Actors. `:198–218` speichert einen rein lesend ermittelten früheren lokalen Fluchtbereich. Das ist eine valide begrenzte Pilotstrategie; es deckt bewusst keine bloß im Next-/Auxiliary-Feld gelisteten Units und keine unbekannten nichtgewöhnlichen Flächen ab. Diese Fälle gehören zum eigenen Bewegungs-/Occupancy-Audit, nicht zu einer stillen Erweiterung des Bau-Brokers.

## 8. CastlePlanner und tatsächliche Keep-Rotation

CastlePlanner verändert Startdaten vor der Keep-Erzeugung und stimmt bei aktiver Fixes-Keeprotation die temporären `PlayerKeepRotationData`-Werte ab (`FixesKeepRotationCompatibility.cs:43–81`, Rücknahme `:98–119`). `CastlePlannerRuntime.cs:539–576` prüft nach Keepbau die **gespeicherte tatsächliche Richtung**, bevor der vorbereitete AIV ausgeführt wird. Dieser Post-Subscriber kann selbst neue native Bauaufrufe verschachteln; ein allgemeiner Broker darf nicht am vermeintlichen äußeren Keep-Post jeden Prozess-Frame abschließen oder die folgende Castle-Erzeugung übersehen.

Die Autorität ist das erstellte Root-Keep-Record: `GameBuilding+0x92`, nicht Spritevariation `+0x102`, nicht Kamerarotation und nicht die inzwischen zurückgesetzte Fixes-Konfiguration. Native Writer `0x74ED4` und unabhängiger Leser `0xD069D` stimmen überein. Richtung `0/2/4/6`, Defaultwert `15` nimmt die South-Geometrie. Die vollständigen eigenen Hüllen umfassen die vom effektiven siebten Keep-Creatorargument bestimmten Marker-, Campground- und Unterflächen; eine Restore-Konfiguration darf nicht später rückwirkend einen anderen Fluchtort berechnen.

CastlePlanners zusätzliche Prefabs laufen über `CreateSupplementalPrefab` (`CastlePlannerRuntime.cs:1201–1221`) und den normalen `CreatePrefab`-Adapter; sein `finally` stellt die vorherigen Placement-Overridewerte wieder her. Diese Aufrufe sind kostenlos und mit Placement-Bypass, also genau ein Gegenbeispiel zum Kosteninteresse als Beobachtungsfilter. Nur aus diesen klaren Verträgen folgt eine zukünftige Beobachtbarkeit; eine erneute Ingame-Keep-/Reiterabnahme bei allen tatsächlichen Rotationen wird dadurch nicht ersetzt.

## 9. Geschlossene Freigabe-/Nichtfreigabeentscheidungen

1. **AIV-Brückenpilot:** vorhandener äußerer `IAivBuildStepObserver`, bekannte Frame-/Identitygates und nachgewiesene Brücken-Schreibmenge bleiben statisch geeignet. Kein neuer confirmed Runtimefehler dieses Piloten aus dem Bauaudit.
2. **Allgemeiner Gebäudebroker:** fachlicher Ort ist die bereits besessene finale Extender-Originalinvocation samt Mirror-Closures. Gegenwärtige HumanConstruction-Kostenfilter sind kein allgemeiner Broker. Ohne eigenständigen funktionalen Vertrag ist die allgemeine Freigabe abgelehnt; ein neuer konkurrierender Entry-Hook ist ebenfalls abgelehnt. Das Urteil ist aus dem vollständigen Eintritt-/Abschlussvertrag bestimmt, nicht aus fehlendem Ingamezugang.
3. **Mauerbroker:** pro effektivem KI-Tile-/Human-Line-Original einen eigenen Rahmen; reale Parameter erfassen; Fixes-Pre und Post-Höhen berücksichtigen. Originalrückkehr beziehungsweise Tile-Refresh allein erlaubt keine Versetzung. Ein Ersatz bestehender Fixes-Prävention ist erst zulässig, wenn dieser Ersatz den ursprünglichen Fall vor destruktiven Schritten selbst sicher behandelt.
4. **Nichtrechteckige/zusammengesetzte Bauten:** kein Quadratfallback. Die Matrix liefert die zu implementierenden Hüllenfamilien; unbekannte oder noch nicht vollständig implementierte Familie bleibt ohne Rettungszulassung. Das betrifft auch die dynamische Sekundärplatzierung und verknüpfte Abrissflächen.
5. **Endgültige Abarbeitung:** Kandidaten dürfen während verschachtelter Kachelupdates nur gesammelt werden. Ein Receipt enthält Session, Seriennummer, Quelle, effektive Parameter, Unit-ID+GlobalId, ursprünglichen gültigen Standort/Fluchtbereich und den konkreten Abschlusszustand. Nur abgeschlossener äußerer Kontext plus aktuell bestätigter neuer physischer Einschluss erlaubt die spätere lokale Rettung. Bei Ausnahme/Teilbau keine Annahme „erfolgreich“ und kein Originalretry. Eine geänderte Teilwelt bleibt Vanilla; die nächste eigenständige gültige Quelle kann erneut beobachten.
6. **Kandidatenkosten:** Arbeit aus den betroffenen endlichen Flächen und ihren Occupancyketten ableiten. Kein globaler Scan aller 10.000 Units pro Tick. Überlauf, kaputte Ketten oder fehlende Hüllen verwerfen/deferieren sichtbar und ohne Vanilla zu blockieren. Vorherdaten nicht mit einem späteren Pfadquery rekonstruieren.
7. **Nativer Update-/Hookbesitz:** Extender besitzt `0x6D580`, `0x6F230`, `0x6CB20`; APIShared besitzt den dauerhaften `0x51790`-Broker; Pathfinding besitzt unter anderem `0xD90D0`, `0x59210`, `0x61E70`, `0xDAA50`. Ihre bereits vorhandenen funktionalen Grenzen erweitern; keine überlappenden eigenen Hooks veröffentlichen. Weitere Live-Spannenverifikation gehört zu Implementierung/Start, nicht zu einer hier behaupteten Offline-Livebeobachtung.

Damit sind für diese Bauquellen der frühere Eingangs-/Spiegel-/Abschluss-/Hüllenpunkt und die Grenze nachträglicher Rettbarkeit statisch entschieden. Offen bleiben die Implementierung der ausgewählten Verträge, ihre neuen isolierten Verhaltenstests und reale Spielabnahmen. Nicht behauptet werden ein universeller Erfolg nach beliebigen fremden Raw-Pointer-Schreibzugriffen, eine vollständige rückwirkende Reparatur alter Saves oder eine automatische Freigabe jeder Einheitenfamilie.

## Quellen zum erneuten Prüfen

- `APIShared/src/Diagnostics/AivBuildStepCapability.cs:18–39,82–175,216–288`; zugehörige unveränderliche öffentliche Context-/Completion-Verträge.
- `APIShared/src/Buildings/ConstructionCosts/HumanConstructionRuntime.cs:23–39,82–165,295–335` und `HumanConstructionRuntime.Scopes.cs:13–98`.
- `shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkAIDetours.cs:24–44,155–185`; `BulkBuildingDetours.cs:876–914,984–1022`; `API/GameBuildingManagerAPI.cs:422–466`.
- `Pathfinding/src/UnitCommands/Movement/NativeMovementRecovery.cs:46–85,174–222`; `Testmods/TrappedUnitRescueTest/src/GeneralRescueObserver.cs:30–101`; `RescueRuntime.cs:53–117,190–218`.
- Kanonischer Fixes-Klon: `Events/FixesAIEvents.cs:109–300`, `Bootstrap/Plugin.cs:109`; aktuelle Quellverträge getrennt von historischer Ingameabnahme.
- Baseline-Vanillaverträge: `AIV_BUILD_RANGE.md`, `DRAWBRIDGES.md`, `KEEP_DIRECTION.md`, `UNIT_LOAD_PLACEMENT_CONTRACTS.md`, `AREA_DEMOLITION.md`; ihre jeweiligen Vanilla-/Integrationsabschnitte auseinanderhalten.

Die RVAs in den Tabellen lassen sich über `query.ps1 function <RVA>` erneut verfolgen. Fehlt ein Decompilerkörper oder widerspricht er den Maschinenbytes, bleibt der direkte kanonische PE-Daten-/Kontrollfluss maßgeblich; ein leeres Resultat ist niemals ein Beweis fehlender Seitenwirkung.
