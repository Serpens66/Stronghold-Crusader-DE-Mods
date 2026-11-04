# Abschlussbericht Wegfindung (Testlinie `dev/pathfinding`, Stand 30.10.2026)

Nachtrag vom 02.11.2026: [Abschnitt 8](#8-feuerbewegung-und-rückkehr--nachtrag-vom-02112026) dokumentiert den aktuellen kombinierten Testlauf mit PitchStairsTest, die Feuerbewegung und Empfehlungen für getrennte Wartefristen. Die Abschnitte 1–7 bleiben historische Aussagen zum Stand 30.10.2026.

Ziel des Auftrags: die Wegfindung im Spiel allgemein beschleunigen (keine Lags über ~100 ms auf 800×800-Karten mit bis zu 8 KI-Lords und 10.000 Einheiten) und ein Regelkonstrukt in APIShared schaffen, mit dem Mods Regeln wie Grabenquerung, Routenkosten, Mauerklettern und Feuerausweichen ohne Lags umsetzen können. Vanilla-Bewegungen sollen weiter möglich bleiben.

Die Einzelnachweise jedes Laufs stehen in `PathfindingRoadmap.md`. Native Verträge und Prüfpunkte stehen in den `UpdateToNewDLL.md` von MoatMove und BugfixesAndQoL, die Vanilla-Audits in der Native-Baseline (`GROUND_ROUTE_BUILDERS.md`, `MANUAL_GROUP_ADMISSION.md`).

**Abnahme (30.10.2026):** Der Nutzer hat diesen Stand akzeptiert. Mehrspieler- und Augentest sowie die Entscheidung über die Standardwerte folgen später; der extreme Regeltest bleibt als künftige Arbeit dokumentiert (Abschnitt 5).

## 1. Testaufbau

- **Save und Last:** Save `test_stresstest4.sav`, 800×800, 8 KI-Lords. Die Automatik (`Run-PathfindingIteration.ps1`) erzeugt per `Spawn` 4.800 Einheiten auf insgesamt bis zu 9.938, zündet nach 15 s Pech und startet nach 20 s zwei Angriffe. Ein Lauf dauert 260 s.
- **Messung:** gezählt werden „Lücken“, also Simulationsticks über 100 ms nach der Ladephase. Endwerte stammen aus Release-Builds; DEVDIAG-Builds dienen nur der Diagnose und sind etwa 20 % langsamer.
- **Gemessene Einstellungen:** MoatMove mit `Mode = anchor`, `AnchorSingleMoves = true` und `AvoidFire = true`. Die Standardwerte sind weiterhin `precise`/`false`/`false`. Die neuen Wege sind als experimentell gekennzeichnet und müssen eingeschaltet werden.

## 2. Ergebnis in Zahlen

| Stand | Lücken über 100 ms im Spiel je Lauf | größte | fps |
|---|---|---|---|
| Vanilla mit Script Extender allein (pf-se-1) | 7 | 213 ms | 253 |
| Mods am Anfang der Testlinie (pf-base-1, ~5.150 Einheiten) | 12 | 647 ms | – |
| Mods vor den Arbeiten dieses Tages (pf-iter-42..53) | 0–2 | 107–152 ms | ~296 (pf-iter-49..53) |
| Mods mit allen Änderungen, normales Spiel (pf-iter-73, 74, 75) | **0, 0, 0** | – | 284–292 |
| Wie oben, mit extremem Regeltest (pf-cons-3) | 2 | 129 ms | 286 |

- **pf-iter-73..75:** Auf die Simulationszeit (die Ruckler, die man im Spiel merkt) kommen in drei Läufen nacheinander keine Lags über 100 ms. Das 99-%-Perzentil der Bildzeit liegt bei 6–7 ms.
- **Einzelbild je Lauf:** In jedem Lauf gibt es genau ein Bild über 100 ms. Das ist der künstliche Massen-`Spawn` von 4.800 Einheiten auf einmal, also ein Testartefakt und kein Spielgeschehen.
- **pf-cons-3:** ein absichtlich extremer Test mit einer kartenweiten Sperre mit nur einer Lücke, 20 wandernden Gefahrenzonen und Grabenkosten ×50. Die zwei Lücken bestehen aus Vanillas eigenen Suchen für die Befehle, die über dem Budget an Vanilla gehen, plus dem Kern. Ohne die Begrenzungen dieses Tages waren es 534 ms.

## 3. Was umgesetzt wurde (Testlinie, nicht gepusht)

**Kern und Einzelwege (MoatMove, nativer Anker-Kern)**
- Gruppenbefehle der KI und des Spielers bekommen exakte gewichtete Routen aus einem gemeinsamen Feld statt einer Vanilla-Suche je Einheit. Davon profitieren auch große Gruppen.
- Einzelbefehle außerhalb von Gruppen beantwortet der Kern mit einem exakten A* statt Vanillas Suche, sofern `AnchorSingleMoves` eingeschaltet ist.
- Ein Knotenbudget je Tick begrenzt den schlechtesten Fall (pf-rule-3: 534 ms).
  - Über dem Budget behält ein Bodenbefehl Vanillas Weg.
  - Assassinen werden angerechnet, aber nie an Vanilla übergeben, weil deren Vanilla-Kette teurer ist.
- Fehlgeschlagene Bodensuchen innerhalb eines Gruppenbefehls werden exakt wiederverwendet (`GroundFailureProofs`).

**Grabenmodus (Häufungen nach großen Truppenschüben)**
- Fehlgeschlagene Grabenmodus-Suchen werden exakt wiederverwendet (`MoatFailureProofs`).
- Grundlage ist der Vanilla-Audit: Vanillas `DAFD0` schöpft bei einem Fehlschlag immer die ganze erreichbare Fläche aus.
- Ein Fingerabdruck der Randkacheln wird bei jeder Nutzung geprüft.
- Nachweise:
  - Im Spiel waren 18 von 18 Selbstprüfungen richtig.
  - Bei einer echten Häufung (pf-iter-71) beantworteten die Beweise 29 Fehlschläge in 12,9 statt ~130 ms.

**Gruppenaufnahme (BugfixesAndQoL)**
- Vanillas Mitgliedsprüfung bei Gruppenbefehlen (`0x124740`) war quadratisch in der Gruppengröße und lief bis zu 220-mal je Sekunde.
- Sie läuft jetzt in einem Durchgang mit genau demselben Ergebnis.
- Im Spiel wurde jeder Aufruf gegen Vanillas Original geprüft, ohne eine einzige Abweichung.
- Diese Änderung brachte die letzten Lücken von 104–112 ms unter 100 ms.

**Assassinen und Mauerklettern (BugfixesAndQoL)**
- Die gewichtete Klettersuche läuft in einem nativen Kern mit identischen Ergebnissen.
- Pro Suche dauert sie höchstens ~11 ms statt 24–26 ms.

**Cursor-Topologie für Grabenplatzierung (MoatMove)**
- Der volle Aufbau dauert etwa halb so lange und wird beim Kartenstart vorgebaut (deine Entscheidung).
- Bei einem Neuaufbau nach einem Regionswechsel werden die Speicherstrukturen wiederverwendet.

**Feuer und Umplanen**
- Brennendes Pech und brennende Gebäude sind Gefahrenkacheln (`AvoidFire`).
- Laufende Einheiten planen 3 Schritte vorher um, erlaubt sind 50 % Umweg. Ist kein Umweg möglich, warten sie bis zu etwa 10 s.
- Die Prüfungen der wartenden Einheiten haben ein Budget je Tick, damit viele gleichzeitig wartende Einheiten keinen Lag erzeugen (vorher 169 ms in einem Tick).

**Regelkonstrukt in APIShared (öffentliche API für alle Mods)**
- `RouteTileRules` / `RouteTileRuleSet`: Kacheln sperren (`Forbidden`) oder verteuern (`Penalty` 1–255), je Spieler wählbar, mit Änderungsjournal.
- `RouteReplanPolicy`: wann laufende Einheiten bei neuen Regelkacheln umplanen oder warten.
- `RouteSearchEvents`: Routenkosten einer zusätzlichen gewichteten Suche ändern, zum Beispiel Grabenkanten teurer machen, oder die Suche überspringen.
- Dazu kommen der vorhandene Torzugang (`GateAccessRules`) und das Assassinen-/Mauerklettern über BFQoL.
- Leitfaden `docs/PATHFINDING_RULES.md`, Katalogeinträge, Beispiele `RouteTileRulesExample.cs` und `RouteSearchExample.cs`.
- Belegt mit dem Testmod `Testmods/RuleConsumerProbe`, der nur die öffentliche APIShared-Assembly nutzt. Seine eigenen Aufrufe kosten unter Volllast höchstens 2,6 ms je 10 s.

**Vanilla bleibt möglich**
- Die Regeln wirken nur in Engines, die sie unterstützen (heute MoatMoves Anker-Modus). Vanillas eigene Suchen und Befehle bleiben unverändert.
- Ohne MoatMove oder mit `AnchorSingleMoves = false` läuft wieder Vanillas Einzelsuche.
- Jede neue native Verknüpfung prüft beim Start die exakten Bytes. Bei jeder Abweichung, etwa nach einem Spielupdate, schaltet sich nur das betroffene Teilfeature ab und Vanilla übernimmt.

## 4. Absicherung

- **Neue Tests nur mit konkretem Nutzen:**
  - Gleichwertigkeitstests für den Topologie-Aufbau, die Grabenmodus-Beweise und die Mitgliedsprüfung, jeweils gegen eine wörtliche Nachbildung oder den alten Weg.
  - Budget- und Vertragstests im Kern sowie ein Integrationstest für das Umplanbudget.
- **Mutantenproben:** Jeder neue Vergleich wurde mit einem absichtlich eingebauten Fehler geprüft und schlug an.
- **DEVDIAG-Prüfungen im Spiel:** bei Grabenmodus-Beweisen, Mitgliedsprüfung, Topologie, gewichteter Assassinen-Suche und Bodenbeweisen jeweils 0 Abweichungen von Vanilla.
- **Mehrspieler:** Alle Budgets zählen Knoten statt Zeit. Alle Mitspieler mit gleichen Mods treffen deshalb dieselben Entscheidungen (deterministisch). Ein echter Mehrspielertest steht noch aus, siehe Abschnitt 5.

## 5. Einfach erklärt: was abgedeckt ist und was später kommen könnte

**Abgedeckt**
- **Viele Einheiten ohne Ruckler:** Auf einer großen Karte mit 8 KI-Lords und knapp 10.000 Einheiten ruckelt das Spiel im normalen Ablauf nicht mehr über 100 ms. Vanilla allein ruckelt in derselben Lage siebenmal pro Lauf, bis 213 ms.
- **Wege für Gruppen:** Große Gruppen bekommen ihre Wege gemeinsam berechnet statt Einheit für Einheit. Das ist schneller und liefert die günstigsten Wege, auch über eigene Gräben.
- **Assassinen:** Sie finden Kletterwege über Mauern deutlich schneller.
- **Feuer:** Einheiten weichen brennenden Stellen aus. Laufen sie schon, planen sie kurz davor um oder warten.
- **Regeln für andere Mods:** Andere Mods können eigene Regeln festlegen, etwa „hier nicht durchlaufen“, „dieser Bereich ist teuer“ oder „Gräben meiden“. Sie brauchen dafür keine eigene Wegfindung, und das ruckelt auch unter Last nicht.
- **Vanilla und Absicherung:** Vanilla funktioniert weiter. Erkennt der Mod nach einem Spielupdate seine Prüfpunkte nicht mehr, schaltet er nur das betroffene Teil ab und überlässt es Vanilla.

**Noch offen oder sinnvoll für später**
1. **Spieltest mit Augen:** Halten und Weiterlaufen vor Feuer, die Wege großer Gruppen und das Klettern der Assassinen sollten einmal von Hand angeschaut werden. Die Automatik misst Zeiten, aber nicht, ob es natürlich aussieht.
2. **Mehrspieler testen:** Alles ist deterministisch gebaut, aber noch nicht in einer echten Mehrspielerpartie mit zwei Rechnern geprüft.
3. **Standardwerte entscheiden:** Die neuen Wege sind noch Opt-in (`anchor`, `AnchorSingleMoves`, `AvoidFire`). Nach den Tests 1 und 2 könnte man sie zum Standard machen.
4. **Extreme Regeln:** Eine kartenweite Sperre mit nur einer Lücke erzeugt noch einzelne Lags von 120–130 ms. Der Großteil davon ist Vanillas eigene Suche für Befehle, die über dem Budget an Vanilla gehen.
   - Abhilfe wäre ein weiterer, genauerer Schätzer für Umwege im Kern oder ein Vorrang für billige Suchen.
   - Für normale Regeln wie Feuer oder kleine Sperrzonen ist das nicht nötig.
5. **Weitere Testsituationen:** Gemessen wurde mit einem Save. Sinnvoll wären ein zweiter Save mit großen Burgen und Mauerringen, ein menschlicher Spieler mit Massenbefehlen und der Karteneditor.
6. **Wartende Einheiten im Spielstand:** Einheiten, die vor Feuer warten, werden noch nicht mitgespeichert. Nach dem Laden bleiben sie stehen, bis ein neuer Befehl kommt; KI-Einheiten planen selbst neu.
7. **Ziel auf gesperrter Kachel:** Liegt das Ziel selbst auf einer gesperrten Kachel, gilt es heute als unerreichbar, und dann übernimmt Vanilla. Hier ist eine klarere Regel denkbar.
8. **Übernahme in die Hauptlinie:** Die Änderungen liegen auf `dev/pathfinding` (BugfixesAndQoL, MoatMove, APIShared-Pin). Vor einer Veröffentlichung braucht es Code-Review, Versionsanhebungen und die Übernahme der APIShared-Erweiterungen nach dem dortigen Verfahren.

## 6. Grenzen, ehrlich benannt

- Alle Messungen stammen von einem Rechner und einem Save. Lags hängen von CPU und Karte ab.
- Die nativen Verträge sind an die Spielversion `FBCB9319` gebunden. Nach einem Spielupdate sind die Audits zu wiederholen; die Prüflisten stehen in den `UpdateToNewDLL.md`.
- Über dem Knotenbudget bekommen Bodenbefehle Vanillas Weg, der die Regeln nicht kennt. In normalem Spiel passiert das etwa einmal pro Lauf für 3–6 Befehle. Feuer erfasst trotzdem der Umplanwächter.
- Der Testmod `RuleConsumerProbe` ist installiert, aber ausgeschaltet (`StartTicks = 0`). Er kann bei Bedarf entfernt werden.

## 7. Wo was liegt

- **Branch:** `dev/pathfinding` im Worktree `.work/a/pathfind/001`, nicht gepusht. Die Commits dieses Tages beginnen bei `7b256daa7` (Knotenbudget) und reichen bis `27841dca6` (Mitgliedsprüfung), dazu dieser Bericht.
- **Laufprotokolle:** `Testmods/MoatMove/.local/runs/` (pf-iter-…, pf-rule-…, pf-cons-…, pf-mp-…, pf-flood-…, pf-topo-…).
- **Fahrplan mit allen Läufen:** `PathfindingRoadmap.md`.

## 8. Feuerbewegung und Rückkehr – Nachtrag vom 02.11.2026

### 8.1 Testkontext und aktuelle Logbefunde

Dieser Nachtrag ergänzt den historischen Bericht vom 30.10.2026. Er dokumentiert neue Nutzerbeobachtungen, Logbefunde, native Untersuchung und Empfehlungen; die unten beschriebenen Bewegungskorrekturen und getrennten Fristen sind noch nicht implementiert.

Quelle ist der Host-Prozesslauf vom 02.11.2026, etwa 10:57:20 bis 11:03:54 (Europe/Berlin):

    E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log

Log-SHA-256:

    725265D719174DA5709C1645D6A40AB56A9041903C2AD039F7A779861F845D97

Gleichzeitig geladen waren **MoatMove 0.1.6** und **PitchStairsTest 0.1.0**, außerdem BugfixesAndQoL 1.0.208, APIShared 0.23.0 und Script Extender 2.14.3.0. Die Pechhöhen-/Ladenormalisierung ist separat in [PitchStairsTest, Nachweise vom 02.11.2026](BugfixesAndQoL/docs/PITCHSTAIRS.md#dated-evidence-02112026-eleven-site-builds) ausgewertet; Vanilla-Befunde in [PITCH_STAIRS_HEIGHT.md](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PITCH_STAIRS_HEIGHT.md). Wartezeit, Umplanung, Feuerflucht und Rückkehr liegen im MoatMove-Code.

MoatMove meldete `replanning=True`, `escape=True`, `waitSeconds=10` und die Umwegregel 50 % / mindestens 10 Zusatzfelder. Das sind die Werte des untersuchten Builds; die Empfehlungen in 8.3 sind davon zu unterscheiden.

| Logzeit | Replan-Ereignisse im protokollierten Fenster | Feuerflucht-Ereignisse im Fenster |
|---|---|---|
| 10:59:22.168 | 2 Wartevorgänge, 0 Wiederaufnahmen, 2 sonstige Beendigungen | 24 Starts, 0 Rückkehrmeldungen, 2 sonstige Beendigungen |
| 10:59:28.598 | 1 Wartevorgang, 1 Wiederaufnahme | Keine neuen Fluchtstarts oder Rückkehrmeldungen |
| 11:01:43.400 | Keine weiteren für die Summe relevanten Wartevorgänge | 18 Starts, 0 Rückkehrmeldungen, 1 Fristablauf, 5 sonstige Beendigungen |

Summierte protokollierte Ereignisse: **3 Wartevorgänge, 1 Wiederaufnahme; 42 Fluchtstarts, 0 Rückkehrmeldungen, 1 Fristablauf, 7 sonstige Beendigungen und 0 Fluchtfehler**. Die 42 Starts sind keine 42 eindeutigen Einheiten: erneutes Ausweichen eines schon aktiven Falls wird ebenfalls gezählt. Ereigniszähler werden nach dem Leistungsbericht zurückgesetzt; `active` und `waiting` sind dagegen Momentaufnahmen. Der Lauf hatte keine Error-/Fatal-Zeilen.

Die Meldungen enthalten keine Fall-IDs mit Einzelverlauf, Deadline, AI-/Animationszustand oder eindeutigem Abbruchgrund. Darum lässt sich nicht jede verlorene Rückkehr Fristablauf, neuem Befehl oder einer anderen Zustandsänderung zuordnen. Die fehlende Animation ist eine Nutzerbeobachtung; die Leistungszähler allein beweisen oder widerlegen sie nicht. Der anschließende Save-Start um 11:03:20 enthielt keine weiteren protokollierten Feuerereignisse und ist kein Nachweis gespeicherter Warte-/Rückkehrfälle.

### 8.2 Warum ein veröffentlichter Weg noch keine normale Laufbewegung ist

**Statischer nativer Befund:** Der untersuchte Vanilla-Hash ist FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 (kanonische DLL, mit CURRENT.json abgeglichen). Der öffentliche Extender-Aufruf `GameUnitManagerAPI.MoveToTile` führt nach 0x196280. Diese Funktion setzt Ziel- und Pfadfelder und publiziert den Weg; sie setzt weder den militärischen `r_AIState` noch den Animationstimer.

Der normale Bewegungsbefehl geht über **0x196100 -> 0x11B520 -> 0x196280**. Die Gruppenroutine übernimmt pro zugelassener Einheit zusätzlich die Kommandovorbereitung: individuelles Bewegungsziel, militärischer Bewegungszustand 101, Kontextbereinigung und passender letzter Befehl. Besondere Vorzustände besitzen weitere Behandlung. Anschliessend wählen die jeweiligen Einheitentyp-Handler die Laufanimation und führen den normalen Bewegungsfortschritt über 0x188340 aus. Der alternative Gruppenpfad 0x118E00 bestätigt dieselbe grundsätzliche Trennung.

`RouteReplanWatch.ReplanMove` nutzt derzeit nur MoveToTile. Bei einer inzwischen stehenden oder anders beschäftigten Einheit reicht ein neuer Weg deshalb nicht, um wieder den vollständigen militärischen Bewegungszustand herzustellen. Das erklärt die beobachtete gleitende Bewegung beim Wiederanlaufen und Flüchten aus dem Datenfluss; eine korrigierte Implementierung ist noch nicht durch einen ausgeführten nativen Vergleichstest oder Sichttest bestätigt.

**Empfohlene Korrektur:** Eine zentrale, gegen Vanillas Pro-Einheit-Semantik geprüfte Vorbereitung für normale individuelle militärische Bewegung vorsehen und für Flucht, Wiederaufnahme und Rückkehr verwenden. Dabei native Zulassung, Leben/Global-ID, gesperrte und besondere Zustände sowie Typausnahmen erhalten; aktives Assassinenklettern beispielsweise darf nicht durch ein pauschales State-Setzen abgebrochen werden. Reine Umplanung einer bereits laufenden Einheit gesondert behandeln.

Danach weiterhin die öffentliche MoveToTile-API samt Ergebnisprüfung und den normalen nativen Bewegungsablauf verwenden. Eine fehlgeschlagene Wegbildung muss sauber gestoppt werden; 0x196280 kann bei Fehlschlag den Wegpuffer nullen, ohne die alten Pfadsteuerfelder zu löschen. Höhen, Übergänge, Kollisionsverhalten, Geschwindigkeit und Animation bleiben Aufgaben des nativen Bewegungsablaufs.

Keine Positionsverschiebung, keine künstlich hochgezählten Animationsframes und kein Befehl an die gesamte Tribe als Einzelbewegungsersatz. Der öffentliche Tribe-Befehl ist vollständig, würde aber weitere Mitglieder erfassen; der Extender bietet hier keinen ebenso vollständigen individuellen Befehlswrapper.

### 8.3 Zwei Rückkehrarten und zwei tickbasierte Fristen

**Aktueller Code:** `FireEscapeEntry.Until` wird bereits beim Fluchtstart aus derselben `MaximumWaitTicks`-Policy gesetzt, die auch das Warten vor Feuer begrenzt. Nach deren Ablauf entfernt `ProcessFireEscapes` den Fall dauerhaft und erhöht `escapeStayed`. Ein später erloschenes Feuer löst dann keine Rückkehr mehr aus. Zieländerungen können den Fall ebenfalls beenden. Die bisherige Frist betrug 400 Simulationsticks für beide Fälle.

**Nutzerwahl und Empfehlung für die weitere Umsetzung:**

| Zweck | Maximale Wartefrist in Simulationsticks | Umrechnung bei 40 Ticks je Spielsekunde |
|---|---:|---:|
| Bisheriges Bewegungsziel fortsetzen / einen sicheren Umweg finden | **400** | 10 Spielsekunden |
| Nach einer Flucht zur vorherigen Standposition zurückkehren | **4.800** | 120 Spielsekunden |

Alle Fristen und Vergleiche in Simulationsticks führen. Die Sekundenwerte dienen der Erklärung; weder Echtzeit noch Frames dürfen den Ablauf bestimmen. Die Werte sind Höchstfristen für weiterhin blockierte Fälle, keine vorgeschriebene Verzögerung: Sobald Ziel und Weg sicher sowie gemäß Umwegregel erreichbar sind, kann die normale Bewegung früher beginnen.

Die Rückkehrart muss beim Anlegen des Falls ausdrücklich festgehalten werden: vorherige Standposition oder bisheriges Bewegungsziel. Ein Soldat mit einem laufenden beziehungsweise vor Feuer wartenden Bewegungsauftrag behält dessen Ziel; eine zuvor stehende Einheit behält ihre Standposition. Ein einziges Zielkoordinatenpaar ohne Herkunft reicht für die Wahl der Frist nicht.

Empfohlen wird, die Wartefrist nach einer Flucht an das erste sichere Ankommen zu koppeln, statt schon während des Herauslaufens Wartezeit zu verbrauchen. Wiederholtes Ausweichen bewahrt den ursprünglichen Rückkehrzweck und darf die Frist nicht unbegrenzt neu starten. Neue Befehle, Tod oder eine geänderte Global-ID beenden den alten Fall; automatische Rückkehr darf einen neueren Auftrag nicht überschreiben.

Ausgegebenes Rückkehrkommando und tatsächliches Ankommen unterscheiden. Der aktuelle Zähler `escapeReturned` wird bereits bei erfolgreicher Befehlsausgabe erhöht; er ist kein sicherer Ankunftsnachweis. Für eine Abnahme beide Ereignisse getrennt erfassen. Ziel- und Wegfreigabe weiter aus dem aktuellen Feuerzustand und den normalen Bewegungsregeln bestimmen, nicht allein aus dem Ablauf einer geschätzten Brenndauer.

### 8.4 Wie lange ein Pechfeuer brennt

**Isoliertes natives Experiment, kein vollständiger Kartenlauf:** Ein einzelnes Feuer wurde mit dem originalen Erzeuger 0x9C450 als Typ 9, Stärke 3 angelegt. Ausgeführt wurden die Original-Updates 0x9F960 -> 0x99420 sowie die echte Entfernung über 0x9F2D0 im privaten Abbild der oben identifizierten DLL. Kein Gebäude, keine Einheiten, keine erneute Entzündung. Zur Isolation des Einzelfelds wurde der Ausbreitungsfolgeaufruf 0x9C120 im privaten Abbild durch eine sofortige Rückkehr ersetzt und der separate Begleitrauch entfernt. Die installierte DLL und Spielstände blieben unverändert.

| Zufallsvariante des Feuereffekts, Bit 7 im Feld +0xC0 | Zur Löschung markiert nach Updates | Tatsächlich entfernt nach Updates | Umrechnung bei 40 Updates pro Spielsekunde |
|---|---:|---:|---:|
| gesetzt | 877 | **878** | **21,95 Spielsekunden** |
| nicht gesetzt | 1087 | **1.088** | **27,2 Spielsekunden** |

Diese Zahlen gelten für den genannten isolierten Ausgangszustand einschließlich des Löschupdates. Sie sind keine gemessene Stoppuhrdauer des aktuellen Nutzerlaufs. Die tatsächliche Echtzeit hängt von Spielgeschwindigkeit und Pausen ab.

Die Pechbeschreibung selbst durchläuft in 0x7D280 20 Updates bis zur Folgephase und weitere 200 bis zum verbrauchten Zustand. Diese **220 Updates sind nicht das Ende des Feuereffekts**. Pech- und Effektaktualisierung werden von 0xCDE60 jeweils einmal im selben regulären Simulationsblock aufgerufen.

Wiederentzündung über 0x9C450 oder 0x9C120 setzt Phase und Frame eines vorhandenen Feuers zurück. Brennt ein zugeordnetes Gebäude weiter, kann 0x99420 die Hauptphase halten und ihren Zykluszähler zurücksetzen. Für eine zusammenhängende Pechfläche, Nachfeuern oder Gebäudebrände ergibt sich deshalb **kein allgemeines Maximum von 1.088 Ticks**. Auch die frühere etwa 13-sekündige Flächenbeobachtung aus den Automatikläufen ist kein allgemeiner Höchstwert.

Die bisherige Rückkehrfrist von 400 Ticks ist deutlich kürzer als beide isolierten Feuereffektdauern. Ein Rückkehrfall kann somit bereits verworfen werden, während das ursprüngliche Feld noch brennt. Die längere Standpositionsfrist adressiert dieses Problem, ersetzt aber nicht die laufende Sicherheitsprüfung.

### 8.5 Erforderliche Abnahme der Empfehlungen

Die folgenden Punkte sind noch umzusetzen beziehungsweise zu prüfen; dieser Nachtrag nimmt keine Runtime-Änderung vor:

1. Native Vergleichstests für die individuelle Bewegungsvorbereitung gegen den normalen Vanilla-Pro-Einheit-Ablauf, einschließlich Zulassung, Sonderzustand und fehlgeschlagener Wegbildung. Vorhandene MoatMove-Integrationsfälle anpassen; ein MoveToTile-Testdouble allein prüft die Animation nicht.
2. Sichttests für Laufanimation und Bewegungsregeln beim Wiederanlaufen, Verlassen des Feuers und Zurückkehren. Verschiedene Soldatentypen, Treppen, Höhenwechsel, Hindernisse und Kollisionen einbeziehen; andere Gruppenmitglieder bleiben von einem individuellen Fluchtauftrag unberührt.
3. Getrennte 400-/4.800-Tick-Fristen, frühe sichere Bewegung, Pause, unterschiedliche Spielgeschwindigkeiten, erneutes Ausweichen und Ablauf ohne erneute Gefahrbewegung testen.
4. Ursprüngliches Bewegungsziel und Standposition auseinanderhalten. Rückkehrkommando und tatsächliche Ankunft separat nachweisen; neue Befehle, Tod und Slot-Wiederverwendung dürfen keinen alten Auftrag fortsetzen.
5. Begrenzte Einzelfalldiagnose nur im DEVDIAG-Build: Global-ID, Fallart, Start-/Ankunfts-/Ablauftick, Ziel, Befehls- und Ankunftsergebnis sowie Beendigungsgrund. Bestehende Suchbudgets erhalten; keine unbegrenzten neuen Suchen pro Tick.
6. Kombinierter Lauf mit PitchStairsTest, Karteneditor und echter Host-/Client-Abgleich mit identischen Einstellungen. Der ausgewertete Singleplayer-Lauf belegt keinen deterministischen Mehrspielerbetrieb.

Die historischen Aussagen dieses Berichts bleiben auf ihren damaligen Stand bezogen. Insbesondere sind weitere Spieltests und gegebenenfalls die Speicherung wartender Rückkehrfälle getrennte Aufgaben; die Empfehlungen hier beweisen keine bereits vorhandene Save-Wiederherstellung dieser Fälle.
