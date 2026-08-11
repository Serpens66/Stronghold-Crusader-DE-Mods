# Feature 06 – Allgemeine Hooks in den Script Extender überführen

## Arbeitsauftrag

Nach erfolgreichen Laufzeittests der jeweiligen selbständigen ImprovedHunters-Hooks sollen daraus saubere, allgemeine und policy-neutrale Script-Extender-APIs entstehen. Die Arbeit erfolgt im kanonischen lokalen Fork auf einem eigenen Branch und wird erst nach professioneller Prüfung als Merge Request vorbereitet.

Dieses Dossier ist kein Ersatz für die vorherigen Feature-Tests. Ein Modhook darf erst portiert werden, wenn sein Hookpunkt, seine Semantik, sein Fehlerverhalten und seine Laufzeitdaten im zugehörigen Dossier abschließend dokumentiert sind.

## Repository- und Branchregeln

- kanonischer Fork: Workspace-Projekt shcde-script-extender
- origin: eigener GitLab-Fork
- upstream: Rawras Originalprojekt
- zuletzt geprüfter Ausgangscommit: 368124119be230306f3f2593efa2a270b0e3dfb1
- vorgesehener Branch: feature/hunter-target-and-mapper-query-hooks
- lokale build.bat und update.bat sind über .git/info/exclude ausgeschlossen und dürfen nicht committed werden
- Aktualisierung des Forks ausschließlich über seine update.bat, nicht per ZIP oder Robocopy
- Push und Merge Request erst nach finaler Benutzerfreigabe und sauberer Diff-/Testprüfung

Vor Branchbeginn den aktuellen Upstreamstand und eventuell inzwischen vorhandene ähnliche APIs prüfen. Keine Doppelimplementierung auf Basis dieses historischen Plans.

## Trennung zwischen Mod und Extender-Test

ImprovedHunters bleibt zunächst mit dem offiziellen Extender kompatibel und enthält seine validierten eigenen Hooks. Beim Test eines Extender-Branches darf niemals zusätzlich der identische interne Modhook aktiv sein.

Zulässige Strategien:

- temporärer, klar markierter Testbuild des Mods mit deaktiviertem internen Hook
- bedingter Adapter, der nachweislich entweder Extender-API oder internen Hook aktiviert
- eigenes minimales Extender-Testplugin

Unzulässig:

- beide Hooks gleichzeitig auf dieselben Instruktionen
- dauerhafter doppelter Fallback nach erfolgreicher Upstream-Übernahme

Nach Merge und einer offiziellen Extender-Version wird der interne Modhook entfernt. Es bleibt keine veraltete Parallelversion, sofern der Benutzer nicht ausdrücklich einen Fallback verlangt.

## Vorgeschlagene APIs

Die endgültigen Namen müssen zu den Konventionen des aktuellen Extenders passen. Semantische Vorschläge:

### OnUnitHunterEvaluateTarget

Früher Callback vor den nativen Alive-/Corpse-/Flags-/Typ-Ablehnungen.

Argumente mindestens:

- HunterUnitId
- CandidateUnitId
- VanillaEligibility oder aussagekräftige Filterreason
- mutable Eligibility/Result
- Eventphase entsprechend vorhandener R3-Eventarchitektur

Anforderungen:

- ausreichender Kontext für lebende Tiere und Kadaver
- keine ImprovedHunters-Typ- oder Besitzerpolicy im Extender
- native Distanz-/Pfadlogik bleibt standardmäßig unangetastet
- Lua-Exposition prüfen, sofern vergleichbare Events sie besitzen

### OnMapperAvailabilityQuery

Callback um DLL_IsMapperAvailable:

- Mapper-ID
- Vanilla-Ergebnis
- mutables Ergebnis
- klare Dokumentation, ob Kartenregeln bereits im Vanilla-Ergebnis enthalten sind

Vanilla wird exakt einmal ausgeführt. Der Extender darf nicht selbst Jägerhütten- oder Ökologiepolicy enthalten.

### OnUnitAutomaticAttackTargetEvaluate

Nur nach abgeschlossenem Feature 04:

- AttackerUnitId
- CandidateTargetUnitId
- Vanilla-Ergebnis
- mutables Ergebnis
- soweit sicher ermittelbar ein Marker für explizite AttackUnit-Lineage beziehungsweise Akquisitionsart

Wenn die Unterscheidung nicht zuverlässig ist, keine irreführende API veröffentlichen. Zunächst kann ein engerer, korrekt benannter Event besser sein.

### Tanner-Ausgabeevent

Nur nach stabiler Semantik des Gerberei-Features und nur wenn wirklich allgemein:

- Unit-/Building-ID
- Output-Good
- Menge
- Zielgebäude
- mutable, klar zeitlich definierte Werte

Kein Rüstung-zu-Fleisch-Verhalten und keine ImprovedHunters-Settings im Extender. Wenn sichere Mutation, Serialisierung oder Zustandsübergang nicht allgemein garantiert werden kann, nur eine ehrliche Notification anbieten oder auf die API verzichten.

## Implementierungsstandard

Jeder neue Hook benötigt:

- Referenzhash, RVA und semantische Signatur
- direkter RVA-Pfad bei bekanntem Hash mit lokaler Bytevalidierung
- eindeutiger, section-bounded Pattern-Fallback nur bei abweichendem Hash
- atomare Installation und Rollback bei Initialisierungsfehler
- langlebige Referenz entsprechend Extender-Lifecycle
- Callbackfehler dürfen Vanilla nicht verhindern
- Vanilla exakt einmal, sofern es ein Detour ist
- aussagekräftige Initialisierungs- und erste Callbackdiagnose
- gedrosselte Fehlerlogs
- dokumentierte Thread-/Timing-/Mutationssemantik
- Test bei keinem Subscriber
- Test bei Subscriber ohne Änderung
- Test bei mutiertem Ergebnis
- Test bei Callbackexception
- Test bei nicht passender DLL/Signatur

Die bestehende Event- und Lua-Struktur des Extenders ist maßgeblich. Keine nur für den Mod passende Sonderarchitektur einführen.

## API-Kompatibilität

- bestehende Events behalten ihre Semantik
- neue Events additiv
- stabile Argumenttypen und Benennung
- keine öffentlichen Rohpointer, wenn Unit-/Building-IDs genügen
- keine Layoutdetails als vermeintlich stabile API exponieren
- Pre-/Post-Phase nur anbieten, wenn beide Phasen semantisch sinnvoll sind
- mutable Werte müssen eindeutig angeben, wann und wie sie in native Register/Rückgabewerte zurückgeschrieben werden

Dokumentation und Beispielcode gehören zum selben Commit oder zu sauber getrennten, jeweils baubaren Commits.

## Commitplan

Bevorzugt kleine professionelle Commits:

1. Hunter Target Evaluation Hook plus Tests/Dokumentation
2. Mapper Availability Query plus Tests/Dokumentation
3. Automatic Attack Target Hook plus Tests/Dokumentation, erst wenn bewiesen
4. optional Tanner API separat, niemals ungeprüft mit den ersten Hooks vermischen

Jeder Commit muss eigenständig kompilieren. Keine ImprovedHunters-Settings, XAML, Fleischwerte oder Besitzerpolicy im Extender-Diff.

## Testplan

- Extender bauen und mit minimalem Subscriber testen
- offiziellen ImprovedHunters-Hook für denselben Pfad dabei deaktivieren
- Vanilla ohne Subscriber vergleichen
- BepInEx-Log maschinell auf Hookmarker und Callbackfehler prüfen
- alle im ursprünglichen Feature-Dossier definierten relevanten Fälle über die neue API wiederholen
- Lua-Test ergänzen, wenn exponiert
- Update-/Pattern-Fallback soweit mit einer kontrollierten Vergleichs-DLL möglich testen
- öffentliche Dokumentation gegen tatsächliche Reihenfolge und Mutabilität prüfen

## Merge-Request-Checkliste

- Branch basiert auf aktuellem upstream
- Arbeitsbaum enthält keine lokalen Build-/Update-Dateien oder Artefakte
- Diff ist policy-neutral
- Commitnachrichten erklären Zweck und native Semantik
- keine spekulativen Feldoffsets
- Build und Tests erfolgreich
- genaue Spiel-DLL-Version dokumentiert
- API-Dokumentation und gegebenenfalls Lua-Dokumentation vorhanden
- kein doppelter Hook im Test
- MR-Beschreibung enthält Motivation, Hookpunkt, Sicherheitsverhalten, Tests und Updatehinweise
- Benutzer hat Push und MR ausdrücklich freigegeben

## Rückmigration des Mods

Erst nach Upstream-Merge und verfügbarer Extender-Version:

1. Mindestversion des Extenders bewusst anheben.
2. ImprovedHunters auf die offizielle API umstellen.
3. internen identischen Hook und Resolvercode entfernen.
4. UpdateToNewDLL.md auf die neue Zuständigkeit aktualisieren.
5. vollständige Mod-Testmatrix wiederholen.
6. keinen ungefragten Legacy-Fallback parallel behalten.

## Ergebnisse und offene Punkte

Noch nicht begonnen. Vor Start hier die abgeschlossenen Feature-Dossiers und deren geprüfte Hook-RVAs auflisten.

## Startprompt für einen neuen Chat

Bearbeite Feature 06 aus ImprovedHunters/Plans/06-ScriptExtender-Upstream.md. Lies ImprovedHunters/PLAN.md sowie die abgeschlossenen Ergebnisabschnitte aller Hooks, die portiert werden sollen. Prüfe zuerst den aktuellen Upstreamstand und bestehende APIs. Erstelle beziehungsweise verwende den Branch feature/hunter-target-and-mapper-query-hooks und überführe ausschließlich bereits laufzeitvalidierte Mechanismen in allgemeine policy-neutrale Extender-Events. Sorge dafür, dass beim Test kein identischer ImprovedHunters-Hook parallel aktiv ist. Noch nicht pushen und keinen Merge Request öffnen, bis der Benutzer den finalen Diff freigegeben hat.
