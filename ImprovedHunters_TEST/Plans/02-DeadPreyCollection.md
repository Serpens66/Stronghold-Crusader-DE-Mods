# Feature 02 – Tote, nicht reservierte Beute einsammeln

## Arbeitsauftrag

Jäger sollen bereits tote, nicht reservierte und tatsächlich abholbare Beutetiere zuerst einsammeln, insbesondere Tiere, die vom Militär getötet wurden. Tote Beute bildet eine absolute Prioritätsstufe vor lebender Beute. Innerhalb jeder Stufe bleibt die vorhandene Fleisch-pro-Zeit-Bewertung erhalten.

Dieses Feature baut auf Feature 01 auf. Vor Beginn ../PLAN.md und 01-HunterQuery-OwnedAnimals.md lesen und den dort dokumentierten tatsächlichen Hookstand prüfen. Ist Feature 01 noch nicht implementiert oder getestet, darf dieses Feature nicht durch einen zweiten konkurrierenden Hook begonnen werden.

## Gewünschtes Verhalten

- Ein toter, nicht reservierter, unterstützter Kadaver wird vor jedem lebenden Jagdziel gewählt.
- Mehrere Kadaver werden untereinander nach erwarteter Fleischmenge pro Abholzeit bewertet.
- Gibt es keinen geeigneten Kadaver, wird lebende Beute wie bisher nach Fleisch pro Jagdzeit gewählt.
- Ein reservierter Kadaver wird nicht von einem zweiten Jäger gewählt.
- Nicht abholbare, despawnende oder semantisch unbekannte tote Units werden ignoriert.
- Ein ausgewählter Kadaver wird ohne Abschussphase direkt abgeholt.
- Nach Pickup verschwindet die sichtbare Leiche ohne doppelte Verarbeitung; Fleisch wird nicht doppelt geliefert.
- Die Mod verändert nicht global die Zustände aller Kadaver.

## Aktueller Zustand und Ursache

Der vorhandene OnUnitHunterQueryTarget-Callback erhält tote Tiere nicht. Die native Hunter Query lehnt einen Kandidaten vor dem bestehenden Extender-Hook ab, wenn:

- AliveState nicht IsAlive ist,
- Corpse-Flag +0x29C nicht 0 ist,
- oder andere frühe Filter nicht passen.

Der Mod kennt bereits:

- CorpsePickupState 0x6E
- CorpseFreshState 0x6F
- Reservation +0x448
- AI-State +0x2BC
- Corpse-Flag +0x29C

TryGetPreyEligibility erlaubt derzeit Corpse-Flag 0 oder AI-State 0x6E, verlangt aber zugleich IsAlive. Diese Logik muss anhand echter militärisch getöteter Tiere neu validiert werden; aus den Konstanten allein folgt nicht, dass jeder Zustand sicher abholbar ist.

## Abhängigkeit von Feature 01

Feature 01 soll einen frühen HunterTargetEligibilityHook vor den nativen Corpse-, Flag- und Typfiltern schaffen. Feature 02 erweitert genau dessen zentrale Entscheidung. Vor Änderungen aus dem Ergebnisse-Abschnitt von Feature 01 übernehmen:

- endgültiges Hook-RVA und Referenzbytes
- Registerbelegung
- Kandidaten-ID und Slotformel
- Ablehnungs- und gemeinsames Fortsetzungsziel
- zentrale Eligibility-Methode
- bestätigte Zustandswerte aus Laufzeittests

Bei Abweichungen hat die real implementierte und getestete Dokumentation Vorrang vor den ursprünglichen Annahmen dieses Dossiers.

## Implementierungsphasen

### 1. Reine Zustandsdiagnose

Auf einer Testkarte mehrere unterstützte Tierarten auf verschiedene Weise töten:

- durch Jäger
- durch manuell befohlene Fernkämpfer
- durch automatisch angreifende Einheit, solange dies vor Feature 04 noch vorkommt
- wenn möglich durch Nahkämpfer

Für jede stabile Unit-Identität über die Zustandsübergänge begrenzt loggen:

- Slot und globale ID
- Typ und Besitzer/Flags
- AliveState
- Corpse-Flag
- AI-State
- Reservation
- Timer-/Transform-Feld
- Position
- Zeitpunkt und Despawn

Nicht aus einer einzelnen Momentaufnahme schließen. Der Pfad muss bis zum terminalen Zustand verfolgt werden. Einmalige Invarianten verwenden, zum Beispiel:

- eligible = changed + remaining
- pickup attempts = successes + rejected
- pro globaler ID höchstens eine erfolgreiche Fleischgutschrift

### 2. Abholbarkeitsprädikat

Eine klar benannte Methode IsPickupableCorpse erstellen. Sie akzeptiert nur eine durch Laufzeitdaten bestätigte Kombination. Mindestbedingungen:

- unterstützter und in Modsettings aktivierter Beutetyp
- Reservation 0
- plausibler Unit-Slot und stabile globale ID
- bestätigter toter beziehungsweise Kadaverzustand
- noch vorhandene Visual-/Unit-Repräsentation
- kein bereits abgeholter oder terminal gelöschter Zustand

Die Methode darf einen Zustand nicht allein deshalb als abholbar behandeln, weil AliveState ungleich IsAlive ist.

Falls die Vanilla-Abholbewegung zwingend AI-State 0x6E erwartet, darf nur der ausgewählte, validierte Kadaver in genau diesen Zustand überführt werden. Vorher muss belegt sein, dass 0x6E Vanillas regulärem Folgezustand entspricht und nicht im nächsten Update normalisiert wird. Keine globale Normalisierung aller Kadaver.

### 3. Frühe Query-Erweiterung

Den bestehenden Hook aus Feature 01 ergänzen:

- lebende Kandidaten folgen der dortigen Logik
- bestätigte Kadaver dürfen die frühen Alive-/Corpse-Ablehnungen selektiv passieren
- Reservation 0 bleibt zwingend
- unbekannte tote Zustände fallen fail closed auf Vanilla-Ablehnung
- native Distanz-, Geometrie- und Pfadprüfung wird soweit semantisch passend weiterverwendet

Falls Vanillas späterer Pfad zwingend IsAlive voraussetzt, ist ein eigener klar begrenzter Kadaverpfad notwendig. Nicht blind in einen Pfad springen, der später andere Live-Invarianten voraussetzt.

### 4. Zweistufige Zielbewertung

Die bestehende Bewertung erhält zuerst einen diskreten Rang:

1. pickupable corpse
2. live prey

Erst danach wird innerhalb des Rangs der Score verglichen.

Für Kadaver enthält der Zeitaufwand:

- Weg des Jägers zum Kadaver
- Pickup
- Rückweg zur Hütte beziehungsweise vorhandener Abgabepfad

Nicht enthalten sind:

- Schussvorbereitung
- Projektilflug
- Kill-Kompensationswartezeit

Für lebende Beute bleibt die bestehende Fleisch-pro-Zeit-Rechnung maßgeblich. Ein lebendes Tier mit sehr hohem Fleischwert darf keinen gültigen Kadaver überstimmen.

### 5. Pickup- und Despawn-Pfad

- Der Jäger darf bei einem Kadaver nicht schießen.
- Reservierung muss vor dem Weg konsistent gesetzt beziehungsweise von Vanilla gesetzt werden.
- Bei abgebrochenem Weg muss die Reservierung wieder freigegeben werden.
- Das bestehende visuelle Entfernen beim Pickup darf nicht doppelt greifen; erneute Versuche nach Fehlern oder bei Tests neuen Codes bleiben zulässig.
- Der verlängerte Despawn muss genügend Zeit für Auswahl und Weg bieten.
- Entfernte oder inzwischen reservierte Kadaver müssen ohne festhängenden Jäger verworfen werden.

## Laufzeittests

| Fall | Erwartung |
|---|---|
| ein Kadaver und ein lebendes Tier | Kadaver wird zuerst gewählt |
| zwei Kadaver unterschiedlicher Entfernung/Fleischmenge | besserer Fleisch-pro-Abholzeit-Score gewinnt |
| reservierter Kadaver und lebendes Tier | reservierter Kadaver wird ignoriert |
| zwei Jäger, ein Kadaver | genau ein Jäger reserviert und liefert |
| Kadaver despawnt auf dem Weg | Jäger löst Ziel und arbeitet weiter |
| militärisch getötetes Kaninchen | wird bei bestätigtem Zustand abgeholt |
| militärisch getötetes Kamel/Huhn | analog, sofern Typ aktiviert |
| unbekannter toter Zustand | fail closed, keine Zustandsmutation |
| Mod deaktiviert | Vanilla-Verhalten |

Zusätzlich jede Fleischgutschrift mit stabiler Tieridentität korrelieren. Es darf weder Doppelgutschrift noch sichtbare stehengebliebene Leiche nach erfolgreichem Pickup geben.

## Abnahmekriterien

- Tote, unreservierte und validierte Beute hat absolute Priorität.
- Die Abholbarkeitsentscheidung ist durch protokollierte Zustandsfolgen belegt.
- Keine globale Kadaverzustandskorrektur.
- Reservierung, Abbruch und Despawn führen nicht zu festhängenden Jägern.
- Pro Kadaver genau ein Pickup und eine Fleischgutschrift.
- Hookdiagnose, Invarianten und UpdateToNewDLL.md sind aktualisiert.
- Mod aus verhält sich wie Vanilla.

## Ergebnisse und offene Punkte

Noch nicht bearbeitet. Hier am Ende insbesondere die bestätigten Zustandskombinationen je Todesart, eventuelle notwendige 0x6E-Transitionen und die Testinvarianten festhalten.

## Startprompt für einen neuen Chat

Arbeite Feature 02 aus ImprovedHunters/Plans/02-DeadPreyCollection.md vollständig ab. Lies zuerst ImprovedHunters/PLAN.md sowie das Dossier und den Ergebnisabschnitt von Feature 01. Prüfe, ob dessen früher Hunter-Query-Hook wirklich implementiert und getestet ist. Führe vor jeder Verhaltensänderung eine begrenzte Zustandsdiagnose echter Kadaver durch, implementiere danach die absolute Kadaverpriorität und aktualisiere alle Dokumentations- und Ergebnisabschnitte. Kein zweiter konkurrierender Hunter-Query-Hook und keine Script-Extender-Änderung in diesem Chat.
