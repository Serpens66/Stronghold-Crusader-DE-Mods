# Gesamtaudit der allgemeinen Einheitenrettung

Stand: 25.11.2026. Dieser Auftrag untersucht die statisch zugänglichen Verträge ausführlich, ohne Installation und ohne Ingame-Tests. Er erweitert weder die Laufzeitfreigabe des Prototyps noch den Produktionsmod. Die erfolgreichen Offline-Tests des vorherigen Auftrags stehen getrennt in [OFFLINE-CHECKS.md](../OFFLINE-CHECKS.md).

## Lesereihenfolge und Nachweise

| Audit | Enthält |
|---|---|
| [Bauquellen](CONSTRUCTION.md) | AIV einschließlich Prebuild und Cleanup, KI-/Menschenmauern, alle 23 Creator-Dispatchfamilien, wirksame Parameter, Spiegelbau, Platzierungsfehler, destruktive Unitbehandlung und Compound-Hüllen |
| [Bewegung und Gruppen](MOVEMENT.md) | Current/Next, tatsächliche und spekulative Schritte, Mittelpunkt/Belegung, physische Ausgänge, native Relocatehelfer, Gruppenführer/-mitglieder, KI-Grenzen und Fehlersignale |
| [Gelände und Arbeiter](TERRAIN-WORKERS.md) | Planung versus terminales Dig/Fill, sämtliche äußeren Editorbrushes, Abriss bis terminaler Entfernung, Wirtschaftsrollen, Geräte-/Ingenieurverbund und Arbeitsfortsetzung |
| [Lifecycle und Kompatibilität](LIFECYCLE-COMPAT.md) | Startup, Mission-/Weltwechsel, Simulationstakt, Save/Resync, aktuelle Extenderstände, Fixes-Unterdrückung, temporäre Scannerkorrektur und echte CastlePlanner-Keeprotation |

Native Identitäten und Bytevergleiche stehen in [MOVEMENT-NATIVE.json](MOVEMENT-NATIVE.json), [TERRAIN-WORKERS-EVIDENCE.json](TERRAIN-WORKERS-EVIDENCE.json) und [construction-native-evidence.txt](construction-native-evidence.txt). Sie werden im Text nicht als ausgeführte Spielszenarien gezählt. Übergreifende Quell-/Referenzidentitäten: `.work/a/rescue-audit/001/identities.json` und `source-inputs.json`.

Die native DLL und die echte Managed-Spielassembly stimmen mit CURRENT/DATABASE_INFO überein. Der native Hash lautet `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Extender-Quellbaum 2.14.6, installierte Referenz 2.14.4 und ältere Dataset-Provenienz werden ausdrücklich getrennt; der vollständige Quelldelta wurde berücksichtigt. Unveränderte Maschinenverträge wurden gegen das installierte PE geprüft. Eine globale Dataset-Aktualisierung wurde nicht vorgetäuscht.

## 1. Was die Untersuchung für das ursprüngliche Problem belegt

1. **Current kann ungültig werden, ohne dass Vanilla jeden Ausgang verbietet.** Gewöhnliche Schritte akzeptieren vorwärts gesetzte Verbindungen oder das passende Rückwärtsbit der Nachbarkachel; danach kommen eigene Oberflächen-, Assassin- und Moatregeln. Ein schlechtes Quellflag oder erfolgloser Zielpfad ist allein kein Einschlussbeweis.
2. **Bereits angenommene Schritte sind ein eigener Fall.** Vor dem Mittelpunkt kann nur Next von der Änderung betroffen sein; Current und die Current-Belegung liegen noch außerhalb. Ein gewöhnlicher Schritt prüft die Oberfläche nicht bei jedem Substep erneut. Außerdem existiert eine spekulative Bewegung auf einer Recordkopie, die nicht als physischer Schritt gezählt werden darf.
3. **Eine einzelne Einheit kann einer KI-Gruppe schaden.** Der echte Leader kann den Gruppenbefehl schon vor Einzelaufträgen abweisen; ein nicht besuchtes Mitglied kann zusätzliche Gruppensuchen auslösen. Nicht jede blockierte Unit stoppt immer jede Gruppe. Das bestehende manuelle Pathfinding-Modsetting schließt KI außerhalb des Editors ausdrücklich aus; Fast-Gruppenrouting ist davon unabhängig.
4. **Bau und Rückbau sind keine einzelnen Tile-Writes.** Compound-Kinder, Farmflächen, variable Anbauten, Cleanup und Mirroraufrufe reichen über ein nominelles Quadrat hinaus. Ein zwischenzeitlicher Pathingrefresh bedeutet nicht, dass der äußere Vorgang abgeschlossen ist. Ein abgelehnter Call kann bereits relevante Vorarbeit verändert haben.
5. **Löschen lässt sich nicht nachträglich als Identitätserhaltung reparieren.** Einige Bau-/Editor-/Gerätepfade setzen terminale Zustände oder löschen Units. Solche Fälle sind von sicherer Rettung überlebender Units und gegebenenfalls früherer Prävention zu trennen.
6. **Arbeitsfortsetzung braucht einen Jobvertrag.** Native Versetzung behält Identität und viele Jobfelder, beendet aber Bewegung. Mehrere Arbeiter lesen das als Ankunft. Erhaltene Gesundheit/Ladung allein beweisen deshalb keine korrekte Fortsetzung. Gleiches gilt für Angriff, Queue und Gruppenabsicht.

Diese Befunde sind aus den untersuchten Kontroll-/Datenflüssen abgeleitet. Sie sind keine Behauptung, jeder konkrete Fehlerfall sei in diesem Auftrag im Spiel reproduziert worden. Aus den bewusst konservativen Ausschlüssen des vorhandenen Prototyps wurde kein neuer bestätigter Runtimefehler konstruiert.

## 2. Erkennung ohne ständigen Vollscan

Für bekannte gewöhnliche Schritte ergibt sich eine konkrete Ereignisstrategie:

    wirksame Bau-/Geländeinvocation mit endgültigen Parametern
        -> konservative vollständige Änderungshülle vor dem ersten Writer
        -> Current-Occupancy der Hülle plus ein Tile Rand
        -> exakte Prüfung: Current oder Next wirklich betroffen?
        -> Identität, Bewegungsphase, Job und alten lokalen Fluchtbereich erfassen
        -> Original unverändert genau einmal ausführen
        -> nach äußerem Abschluss tatsächliche Änderung/Identität neu prüfen
        -> geeignete Kandidaten begrenzt prüfen und gegebenenfalls retten
        -> Nachbeobachtung nur dieser Kandidaten

Der ein Tile breite Rand folgt aus den **acht Nachbardeltas des gewöhnlichen Next-Writers**, nicht aus einer vermuteten Geschwindigkeit. Er deckt keine beliebig entfernten Portalendpunkte, Transport- oder fremden Raw-Writes ab. Diese Sonderzustände bleiben an ihre eigenen Writer und Cleanupverträge gebunden. Compound-Änderungshülle und Bewegungsrand sind zwei verschiedene Größen.

Die 800×800-Koordinatenhülle darf nicht als dichtes natives 640.000-Tile-Array verwendet werden. Die gepackte native ID benutzt eine Zeilen-LUT und hat 320.800 Slots. Ebenso existieren 10.000 Unitslots einschließlich reserviertem Slot0; die regulären IDs sind 1..9999. Bounds/aktive Karte sind vor jedem Zugriff zu prüfen.

Damit entfällt ein permanenter zusätzlicher Scan aller Einheiten. Es entsteht aber keine unbegrenzte Gratisgarantie: Extremes Stapeln, großflächiger Editorumbau oder viele gleichzeitige Bauten verursachen reale Ereignisarbeit. Die heutigen Capturelimits können einen überlasteten Fall sicher ablehnen; das zählt als nicht erfasst, nicht als „keine Unit betroffen“. Für die neue Implementierung sind Deduplizierung, feste Arbeitsbudgets, sichtbarer Überlauf und ein definierter Umgang mit nicht mehr verfügbaren Vorherdaten zwingend. Wandzeitbudgets würden MP-Entscheidungen auseinanderlaufen lassen.

Bewegungsfehler können weitere Kandidaten melden. Sie dürfen die Welt nicht während einer aktiven fremden Suche verändern und ohne Unit-/Auftragszuordnung nicht als Unitfehler interpretiert werden. In einem Altsave fehlen die früheren erreichbaren Ausweichfelder. Keine Ereignisarchitektur kann diese historische Information nachträglich allein aus der neuen Karte rekonstruieren. Eine Altsave-Funktion braucht deshalb einen getrennten zulässigen Zielnachweis und eine ausdrückliche Produktregel.

## 3. Benötigter allgemeiner Beobachtungsvertrag

Dies ist der auditierte Entwurf einer **additiven** APIShared-Grenze, keine bereits existierende API oder Codeänderung. Die fachliche Rettung und ihre Einstellung bleiben beim Testmod beziehungsweise später Pathfinding.

| Vertragsbestandteil | Erforderliche Semantik und Grund |
|---|---|
| Eigentümer | Bestehenden Extender-/APIShared-Originalowner erweitern; kein zweiter konkurrierender Hook. Rettungsinteresse unabhängig vom heutigen Kosteninteresse. |
| Invocation | Stabile Quelle und einmalige Serienkennung, verschachtelte Elternzuordnung, wirksame Parameter und optionale fachliche Besitzer-ID. Keine gemeinsame mutable EventArgs-Kopie als späterer Beweis. |
| Before | Direkt vor der tatsächlich ausgeführten Originalinvocation nach Pre-Änderungen und Vetos. Mirror-Originale separat mit echten Spiegelkoordinaten erfassen. Vorarbeit darf keine nativen Scratch-Enumeratoren oder mutierende „Probe“-Moves aufrufen. |
| Privater Zustand | Jeder Beobachter erhält seinen eigenen Receipt; keine fremden State-Objekte, Pointer oder Frames über eine verzögerte Grenze teilen. |
| Original | Genau einmal; Beobachterfehler verhindern/wiederholen Vanilla nicht. Nicht auf Erfolgsrückgaben vertrauen, die der konkrete native Vertrag nicht liefert. |
| Completion | In `finally` mit getrennten Fakten „aufgerufen“, „normal zurückgekehrt“ und Fehler/Abbruch; kein erfundener Erfolg bei Exception. Der Originalfehler bleibt erhalten. |
| Verschachtelung | Lowlevelupdates zunächst nur sammeln. Ein Scope um den **gesamten Extender-Wrapper** umfasst die vorher ausgeführten Mirror-Siblings; separate native AIV-/Linienrahmen und effektive Original-Childreceipts werden darin zugeordnet. FinalOriginal-Wrapping allein erzeugt keinen solchen Mirror-Batchrahmen. Eine untergeordnete Teilfläche oder das erste Mirror-Original veröffentlicht keine fertige Compound-Welt. |
| Mutation | Tatsächliche alte/neue Geometrie und überlebende Identität bestätigen. Ein Ergebniscode, Spawn-Post oder Cacheinvalidierung allein genügt nicht. |
| Thread/Lebensdauer | Synchron auf dem gültigen nativen Publisherthread, kein UI-/Mainthreadwechsel mitten im Original, prozessweit verwurzelte Registrierung, logische Aktivierung. Beliebige fremde Hintergrundaufrufe werden dadurch nicht legal. |
| Queue | Nach vollständigem Abschluss nur validierte Werte übernehmen. Identität/Welt/Order vor jeder späteren Mutation erneut prüfen; neuere Befehle haben Vorrang. |
| Fehler/Überlast | Begrenzter sichtbarer Nicht-Erfasst-/Nicht-Unterstützt-Ausgang. Auch innere native Such-/Unstackarbeit muss begrenzt beziehungsweise bei unbekanntem Worst Case abgelehnt werden; ein Kandidatenzähler allein begrenzt keinen nativen Vollkartensuchlauf. Keine erfundenen Vorherdaten, kein Originalretry und kein Rückteleport nach bereits erfolgtem Eingriff. |

Die einzelnen Quelladapter unterscheiden sich weiterhin: AIV-Frame, KI-Mauerkachel, menschliche Mauerlinie, finaler Gebäudeoriginal-/Mirrorcall, terminales Dig/Fill, äußerer Editorbrush und terminaler Abriss. Ein neuer universeller „TileChanged“-Callback ersetzt diese Adapter nicht.

Neue native Adapter werden erst für eine konkrete Implementierung ausgewählt. Für Dig/Fill sind die NativeX64-Indirect-Minimalspannen einschließlich Instruktionsgrenzen und eingehender Referenzen offline geprüft. Ein anderes Backend oder eine längere Inline-Spanne erhält daraus keine Freigabe. Die tatsächlich installierten Bytes können naturgemäß erst beim später autorisierten Spielstart geprüft werden.

## 4. Fixes, Wiederholungen und Veröffentlichung

Fixes wird im normalen automatischen Pilotbau nicht pauschal unterdrückt. Seine frühe Mauerrettung ist insbesondere vor destruktiven Baufolgen wirksam. Nur eine tatsächlich vollständige Ersatzprävention darf diesen Schutz übernehmen; Kandidatencapture allein genügt nicht. Fixture-Unterdrückung ist davon getrennte Testisolation.

Unabhängige Fixes-Updates dürfen weiterhin funktionieren: geprüft werden relevante Methoden-/Helper-/Konstantenverträge und die tatsächlich beeinflussten nativen Spannen. Eine unbekannte relevante Änderung muss die Ersatzfunktion deaktivieren, statt blind fremden Code zu überspringen. Die temporäre Reiter-Scannerkorrektur bleibt abtrennbar und verändert den Recovery-Emitter nicht. Der aktuelle Live-CALL von Fixes1.31.0 wurde hier nicht neu getestet.

Save-/Weltwechsel und Multiplayer-Repair invalidieren alte Receipts. Für MP stehen gemeinsame native Zeit und Resyncdienste zur Verfügung; der bestehende Testmod bleibt MP-gesperrt. Weder lokale Sessionnummern noch lokale Runzähler, UI-Auswahl, Diagnosefolge oder Millisekundenbudgets sind gemeinsame Simulationsentscheidungen. CastlePlanner-Rotation wird aus dem tatsächlich erzeugten Keep-/Campground-Compound gelesen.

## 5. Was nach dem Audit folgt

Die geschlossenen statischen Verträge sind Grundlage für die schrittweise Implementierung gemäß [RESCUE-PLAN.md](../RESCUE-PLAN.md). Sie geben keine noch nicht implementierte Quelle oder Einheitenphase automatisch frei. Insbesondere sind ein Native-Bytevergleich, ein gelesener Jobhandler und eine echte physische Spielabnahme verschiedene Nachweisarten.

Die verbleibenden Arbeiten werden getrennt geführt:

- **Implementierung:** funktionaler Broker und Quelladapter, Kandidatenqueue, vollständige Hüllenfamilien, Current/Next-Behandlung, familientreue Fortsetzung und ein aktiver Rettungsbesitzer.
- **Produktentscheidungen:** Alliierte/Neutral/Feind, Altsaves, Queuepersistenz und Umfang der ersten Freigabe. Fehlende Vorherdaten werden nicht mit einem stillen Default ersetzt.
- **Tests des neuen Codes:** reine Gegenproben zu Identität, Reihenfolge, Verschachtelung, Überlast, neueren Befehlen, Writerfehlern und familienbezogenen Wiederaufnahmeverträgen.
- **Laufzeitabnahme:** natürliche begonnene Schritte, reale Orts-/Belegungskonsistenz, vollständige Arbeits-/Gerätezyklen, kombinierte Fremdhooks, Editor/Save, echte Peers und Last auf großer Karte.

Der vollständige aktuelle Status und gegebenenfalls verbleibende statische Detailgrenzen stehen jeweils im Abschlussabschnitt der vier Teilaudits. Ein ausgeschlossener oder noch nicht implementierter Zustand ist kein bestandener Rettungstest. Offene Grabenarbeiter-/Ochsenfälle behalten ihren Status.
