# Abschlussbericht Wegfindung (Testlinie `dev/pathfinding`, Stand 30.10.2026)

Ziel des Auftrags: die Wegfindung im Spiel allgemein beschleunigen (keine Lags über ~100 ms auf 800×800-Karten mit bis zu 8 KI-Lords und 10.000 Einheiten) und ein Regelkonstrukt in APIShared schaffen, mit dem Mods Regeln wie Grabenquerung, Routenkosten, Mauerklettern und Feuerausweichen ohne Lags umsetzen können. Vanilla-Bewegungen sollen weiter möglich bleiben.

Die Einzelnachweise jedes Laufs stehen in `PathfindingRoadmap.md`. Native Verträge und Prüfpunkte stehen in den `UpdateToNewDLL.md` von MoatMove und BugfixesAndQoL, die Vanilla-Audits in der Native-Baseline (`GROUND_ROUTE_BUILDERS.md`, `MANUAL_GROUP_ADMISSION.md`).

## 1. Testaufbau

- **Save und Last:** Save `test_stresstest4.sav`, 800×800, 8 KI-Lords. Die Automatik (`Run-PathfindingIteration.ps1`) erzeugt per `Spawn` 4.800 Einheiten auf insgesamt bis zu 9.938, zündet nach 15 s Pech und startet nach 20 s zwei Angriffe. Ein Lauf dauert 260 s.
- **Messung:** gezählt werden „Lücken“, also Simulationsticks über 100 ms nach der Ladephase. Endwerte stammen aus Release-Builds; DEVDIAG-Builds dienen nur der Diagnose und sind etwa 20 % langsamer.
- **Gemessene Einstellungen:** MoatMove mit `Mode = anchor`, `AnchorSingleMoves = true` und `AvoidFire = true`. Die Standardwerte sind weiterhin `precise`/`false`/`false`. Die neuen Wege sind als experimentell gekennzeichnet und müssen eingeschaltet werden.

## 2. Ergebnis in Zahlen

| Stand | Lücken über 100 ms im Spiel je Lauf | größte | fps |
|---|---|---|---|
| Vanilla mit Script Extender allein (pf-se-1) | 7 | 213 ms | 253 |
| Mods am Anfang der Testlinie (pf-base-1, ~5.150 Einheiten) | 12 | 647 ms | – |
| Mods vor den Arbeiten dieses Tages (pf-iter-42..48) | 0–2 | 108–152 ms | ~296 |
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
