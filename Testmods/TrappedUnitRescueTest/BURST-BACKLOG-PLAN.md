# Ereignisspitzen: vollständige Vorhernachweise mit festen Tickbudgets

Stand27.11.2026. **Implementiert im Testmod; erste vollständige Offline-Suite bestanden, neue Spielabnahme noch offen.** Ergänzt N6 des aktiven RESCUE-PLAN; Produktion bleibt BugfixesAndQoL, zunächst ausschließlich Testmod. Kein neuer permanent beobachteter Einheitenbestand und keine menschliche Jobfortsetzung.

## Bestätigter Ausgangsstand

- Discovery lehnt ab513 ausgewählten Current-/Next-Belegungen insgesamt ab, noch vor der Typfilterung.
- Ein zusammengesetzter Batch wird ab513 Snapshots insgesamt ungültig. Gemeinsame Queue/Geometry/Staging-Grenze512 kann weitere Nachweise verwerfen. Es gibt keine Overflowablage; P7 benötigt bereits vorhandene Geometrie und ersetzt verlorene Erfassung nicht.
- Vorhandene GeneralObserverFixture.BudgetsAndCapacity-Fälle spezifizieren dieses Fail-closed-Verhalten:513→0 und aggregiert300+300→512. Das sind reine Codebelege, keine bereits durchgeführte native Dichteprobe.
- Drain ist auf32Besuche/8Evaluierungen je zugelassenemTick begrenzt. Bei leerer512erQueue gibt es unter durchgängig zugelassenenTicks spätestens nach64Ticks einen Erstkontakt. Daher keinen unbelegten TTL-vor-Erstkontakt-Fehler behaupten.
- Absolute Lebensfrist80Ticks ab Quelle und höchstens4Versuche begrenzen Wiederholungen. Vier Versuche jeFall sind nicht garantiert:159Retryfälle benötigen636Evaluierungen, vorT+80 gibt es bei erstemDrainT+1 nur632.
- Die aktuelle4000er-ABAB-Messung179 qualifiziert Normalbetrieb innerhalb ihres künstlichen Profils. Sie misst keine großflächige Rettung und schließt diese Verluste nicht.

## Empfohlene Richtung

Vollständige ereignisbezogene Aufnahme bis zur nachgewiesenen nativen ID-Grenze; danach kompakter wartender Nachweisbestand und budgetierte Bearbeitung. Vor Original weiterhin alle semantischen Daten erfassen, deren ursprünglicher Wert nachher nicht mehr belegt werden kann. Die teure lokale BFS und die Materialisierung ihrer Ergebnisliste aus unveränderlich gespeicherter alter Geometrie verschieben. Aktuelle freie Rettungskachel und Eingriffsbedingungen erst vor tatsächlicher Maßnahme prüfen.

Kein blindes Erhöhen von512oder80: Erfassung, Batchaggregation, Queue, Quittungen, Idleplätze, Fristmodell und Saves müssen zusammen passen.

## Nachweisdaten und Grenzen

1. Vor Original: Session/World, ID/Global/Typ/Owner, HP/Tribe, tatsächlichesCurrent und genau belegtesNext samt Schritt-/Belegungsgrenzen, alter Ausgangsbeleg. Für Soldaten/Ox zusätzlich ursprüngliche Absicht, Ziel, Command-/Job-/Entitybindungen und Crew-ID/Globals. Diese Werte niemals nach Original als ursprüngliche Absicht neu aufnehmen.
2. Zivile Entfernung behält den vorhandenen minimalen Vorhernachweis ohne Job/Cargo/BFS. Für Command/Ox alte lokale17×17-Geometrie: gewöhnlicher Boden und acht Verbindungsbits. Dicht gepackt ungefähr326Bytes jeUrsprung; genaue Kodierung und alle übrigen Felder noch nachzuweisen.
3. BFS muss aus diesem alten Feld dieselbe Reihenfolge, Reichweite und Entfernungen wie der heutige Vorherpfad liefern. Kein heutiges Gelände als Ersatz, keine nativen Aufrufe an kopierten Spielobjekten.
4. Belegungsketten ausschließlich im tatsächlichen Ereignis lesen. Ereignislokale Datensatzcaches dürfen doppelte Reads vermeiden; Zyklen, widersprüchliche Links und Identitätswechsel bleiben Ablehnungsgründe. Auch die16384-Linkgrenze vollständig prüfen;9999lebendeIDs bedeuten nicht automatisch höchstens9999relevante Current-/Next-Links.
5. Feste aktive32/8-Budgets beibehalten. Fehlender Quittungsplatz (derzeit512) oder Idleplatz (64) wird vor jeder nativen Änderung ein eigener Wartezustand. Keine Normalisierung/Versetzung ohne vorher verfügbare Reservierung.

## Fristmodell: vor Umsetzung zwingend absichern

Quellalter unverändert erhalten und diagnostizieren, auch beim Umlagern und Laden. Davon getrenntes Bearbeitungsfenster höchstens4Versuche/80aktiveTicks ab erster zugelassener Bearbeitung. Reines Ressourcenwarten verbraucht keine Versuche. Bei9999Fällen dauert allein der erste Durchlauf mit8Evaluierungen mindestens1250Ticks.

**Geänderte Garantie:** Nachweise würden länger als80nativeTicks warten dürfen. Das ist erst zulässig, nachdem alle späteren relevanten Geometrieänderungen über wartende/aktive/staged/Batches/Saves vollständig invalidieren. Der gesonderte Review und die kleineVorbedingung sind inzwischen geschlossen: separate spätereWritezellen im ursprünglichenRadius8 invalidieren bereits alle historischenSpeicherformen; neu korrigiert gilt das auch für fremdeBauereignisse währendRecovery. Reentryplant nur dieWriteEnvelope, ohneSnapshot/BFS. General247/Replan1055 und Spiele186/187 bestanden. Innerhalb desselben nochlaufendenOuterbatch bleibt bewusst der frühesteVorherzustand maßgeblich; wartendeArbeit erst nach dessen erfolgreichemAbschluss freigeben. Für neueBacklogs muss dieselbeInvalidierung erst noch auf die neueSpeicherform ausgedehnt und getestet werden.

Alle heutigen Eingriffsgates bleiben: gleicheWelt/Life, zulässigeraktuellerTyp/Phase, Rekrutierungsschutz, ursprünglichesCurrent oder ein belegter alterNext, korrekteBelegung, echteaktuelleGefangenschaft, sichereaktuelleBank, unveränderte relevanteAbsicht/Bindungen/Crew. NeuereAufträge gewinnen; menschlicheEntfernung erhält keine alteAuftragsautorität.

## Speicherung und Implementierungspunkte

SDK0.74-Momentaufnahme des ausgewähltenQuellstands:16384Eigenschaften jeMod,1MiB jeEigenschaft,8MiB gesamterModdatensatz.9999Einträge scheitern nicht automatisch amPropertylimit. Vollständige materialisierte289×16Byte-Banklisten ergeben dagegen schon ungefähr46MB reine Kandidatendaten und sind kein tragfähiger Saveentwurf.

Neues kompaktes Pendingformat für aktive und wartende Fälle; keine zusätzliche persistente materialisierte BFS-Liste. Bisherige Pending-v5/Legacy-v4-, Geometry-v1- und Quittungsdecoder streng erhalten. Maximale echte Drahtgröße mit sämtlichen zulässigen Command-/Ox-/Crew-/Bindungsschemata berechnen und mit dem tatsächlichen8MiB-Vertrag nachweisen; ungefähr3,3MB reine lokaleGeometrie sind noch kein vollständiger Größenbeleg. Blockweise Speicherung ist eine Option, kein Zwang aus demPropertycount.

Betroffene Quellen: RescueDiscovery, RescueRuntime, GeneralRescueObserver/Replans, RescueCandidateQueue, Pending-/Geometrycodecs und die gemeinsamen Archivgrenzen im WorkerRecoveryService. Native Rettungsroutinen und Hookbesitzer bleiben unverändert.

## Nachweisfolge

- Vollständigen Invalidierungs-/Intent-/Savevertrag schließen; Entwurf danach konkretisieren.
- Gleichheit alter/neuer BFS an echten bisherigen Vorherfeldern sowie Grenz-/Zyklus-/Bankfällen offline prüfen.
-512/513/600undmaximaleIDs vollständig, faireersteBearbeitung, begrenzte32/8-Drainarbeit, volleQuittungs-/Idleplätze, spätereGeometrie, neueAufträge, Tod/Reuse/Weltende prüfen. Neue Tests nur für diese konkreten bisher nicht abgesicherten Garantien.
- Vorher-Capturezeit/NativeReads/Allokation getrennt für zivileRemoval und Command/Ox mit unterschiedlichenUrsprüngen messen; maximalenBestand und tatsächlicheSavegröße belegen.
- Deterministische nativeBurstprobe mit echten erzeugtenEinheiten, gültigerQuelle und vollständigerOriginalidentitätsliste; keine zufälligeBelagerung und kein synthetischerTicketbestand als Ingameersatz.
- TatsächlichesSave/Load unterBacklog, Quellen-/Gruppen-/Crewregression und normalesPaket ohneDEVDIAG; erst dann N6 und Integration abschließen.

## Implementierungscheckpoint nach aktueller AGENTS-/Baseline-Aktualisierung

Der Rückstau fasst jetzt bis zu9999 kausale Originalidentitäten. Ereignislokale Discovery liest jede eindeutige Belegung zweimal (Aufnahme und Identitätsrevalidierung), statt gemeinsame Kettensuffixe mehrfach abzulaufen. Unveränderte lokale17×17-Geometrie wird kompakt vor dem Original gespeichert; ihre BFS wird erst bei zugelassener Rettung materialisiert. Die General-Queue bleibt auf32Besuche/8Aktionsversuche begrenzt; der Replan-Scheduler besitzt daneben ein eigenes32er-Besuchsbudget. Passive Idleaufnahmen teilen ihr32er-Budget mit der Queue. Das ist keine globale32-IDs- oder8-Nativecalls-Garantie. Reines Quittungs-/Idle-Ressourcenwarten verbraucht weder Versuche noch aktive Frist. Vorherdaten, Quellalter, neue Aufträge, spätere Geometrieänderungen und Weltgrenzen bleiben maßgeblich.

Erster integrierter Offline-Lauf bestanden: vollständige600/9999-General-Fälle, Fairness und Fristen, Ressourcenwartezeiten, lokale BFS-Gleichheit, alte v4/v5-/Geometry-v1-Daten und kompakte Archivgegenproben. Geprüfter Maximaldatensatz mit9999Fällen,512historischen Bankpaaren und512echten Quittungen:7981610Bytes einschließlich APSV-Hülle; unter8388608Bytes. Dies ist eine konkrete Größenprobe samt gültigen Maschinenfeldbreiten, kein Spielnachweis.

Der ausgewählte isolierte SDK0.74.0-Stand fb53ce17 bleibt unverändert. Sein vorhandener vollständiger Buildnachweis wurde mit expliziter SDK-Auswahl erfolgreich geprüft; kein Neubuild oder Proofumschreiben nötig. Nachweis: `.work/a/rescue-sp/001/sdk-refresh-result.json`. Erster integrierter Testlauf: `backlog-compile.log` und `backlog-tests.log` im selben Evidenzordner. Spätere Korrekturen erfordern erneute relevante Prüfungen.

Aktueller Stand27.11.2026: Ungültige geladene Kausalarchive werden über den tatsächlichen Callback vollständig abgelehnt; die ursprünglichen Archivbytes bleiben über KeepLoaded erhalten.195belegt513/600einzelne und600über drei getrennte echte Brücken verteilte Originalsoldaten mit vollständiger Ankunft an ihren ursprünglichen gültigen Gruppenzielen.196belegt das echte Speichern/Laden aller600wartenden Originalbeweise vor dem ersten Ladetick und anschließende vollständige Rettung/Ankunft.197befreite ebenfalls alle600, erzeugte jedoch keinen natürlichen passiven Übergang; dieser Sonderzweig ist weiter offen. Details und Grenzen stehen in SP-ACCEPTANCE.md.

Noch offen: gezielte passive Rettung ohne alte Auftragswiederholung; positive Gesundheitsänderungen und harmlose Idlephasenwechsel streng revalidieren; aktuelle Quellen-/Gruppen-/Crewregression; Messung von Aufnahme und Drain; aktueller Normalbetrieb und Paket ohne DEVDIAG. Erst danach N6 abschließen und nach BugfixesAndQoL migrieren.187ist historischer Vorherstand.

## Gemessene Großfallkosten, 27.11.2026

Die Diagnoseauswertung195/196 liegt in `.work/a/rescue-sp/001/backlog-costs.json`. Im ersten513er-Capture wurden13,5ms einschließlich Planung/Discovery/Snapshots gemessen;1026Kettenlesungen,513Snapshots und keine Vorher-BFS. Der warme600er-Capture lag bei2,6ms; der erste600er-Capture im separaten Saveprozess bei15,0ms, jeweils1200Kettenlesungen und600Snapshots. Das sind unterschiedliche Aufwärmzustände und Einzelereignisse, keine feste Zeitgarantie. Die drei200er-Captures lagen unter der2ms-Spikeschwelle; aus fehlenden Spikezeilen folgt keine exakte Laufzeit.

Die höchsten in den Diagnosefenstern geloggten kompletten eigenen Rettungsticks betrugen12,668ms beziehungsweise16,738ms. Darin stecken native Rettungs-/Bewegungsaufrufe und Diagnosearbeit; das ist kein isolierter verwalteter Verwaltungsaufwand. Höchstens8Maßnahmen jeTick wurden an den realen Originalidentitäten geprüft. Allokationsmessung meldete in diesen Capturezeilen nicht verfügbar und wird nicht als0interpretiert. Ein aktueller kontrollierter Normalbetrieb-Vergleich und die Regressionen bleiben gesondert offen.

## Präzise Arbeitsbudgets und abschließender Effizienzreview

27.11.2026, aktueller Quellstand: Im Leerlauf bleiben Count-/Demandprüfungen konstant; keine dauernde Einheiten- oder Karteniteration. Native Movebeobachtung prüft bekannte IDs per Array/Dictionary vor einem Snapshot. Reine Geometrie ohne frische Absicht oder aktive Frist bleibt dormant. Ereignislokale Discovery liest jeden eindeutigen Datensatz einmal plus Identitätsrevalidierung; lokale326Byte-Felder werden nur innerhalb desselben unveränderten nativen Originalaufrufs pro Ursprung geteilt. Die spätere lokale Suche umfasst höchstens289 Zellen/acht Nachbarn.

Budgets sind ausdrücklich **stufenbezogen**:

- Replan-Scheduler: höchstens32 Besuche. Aktive FreshIntent-Validierung hat ihr eigenes Budget; passive Idleprüfung teilt das32er-ID-Budget mit nachfolgender Queuearbeit.
- General-Queue: höchstens32 Besuche und8 tatsächliche Aktionsentscheidungen. Eine Rettung kann mehrere native Operationen benötigen; acht ist keine globale Nativecallgrenze. Observe/Await sind keine eigenen nativen Rettungsversuche.
- IdleEscape-Folgeprüfung: Kapazität64, höchstens8 Prüfungen proTick, Frist600; Advance sendet keine neuen Bewegungsbefehle.
- Resume-Supervisor: Kapazität512, höchstens32 Slotbesuche und4 fällige Fortsetzungsversuche; pro Continue höchstens ein Reissue. Eigene ereignisgesammelte DeferredOrders sind separat auf diese512 Receipts begrenzt.
- Historische Parkbereinigung: gesondert höchstens32 Slotbesuche/4 Cleanupentscheidungen; keine neue menschliche Jobfortsetzung.

Save/Load/Reset und spätere Geometrieinvalidierung dürfen vorhandene begrenzte Records ereignisbezogen durchlaufen. StageRestore sortiert einmalig O(NlogN). Daraus folgt keine pauschale Zeitgarantie für maximal große Bau-/Saveereignisse. Der unabhängige Review fand im geprüften Delta keine neue unbeschränkte Tickarbeit oder quadratische Queue-/Discoverysuche. Die reale9871er-Messung prüft Grundlast und1000 laufende Einheiten; maximale Capture-/Save-Burst-CPU und Allokationen werden damit nicht vorgetäuscht.

## Abschluss dieses N6-Arbeitspakets

202 normale achtteilige funktionale Serie einschließlich600 echter aktiver/passiver Originale und strikter600Ziele,203 aktuelle echte600er-Persistenz sowie204 zwölf Quellen-/Editorfälle sind bestanden.205 qualifiziert die tatsächliche9871er-Testwelt mit1000Walking und achtVergleichsfenstern. Die Stagebudgets und Profilgrenzen oben bleiben maßgeblich. Abschließender normaler Build, vollständige Offline-Suite,154 Performance-Validatorprüfungen und NoDevGate sind bestanden; Produktionsmods und ausgewähltes isoliertesSDK wurden nicht verändert.

Die in früheren Zwischenständen als offen aufgeführten nativen Großfälle, aktuelle Persistenz, positive Editorabdeckung und relevante funktionale Regressionen sind damit geschlossen. Die bewusst dokumentierten Grenzen sind keine nachträglich als bestanden markierten Tests. Nächster Schritt ist N7/N8 nach RESCUE-PLAN.md; davor stehen zwei bereits gestellte Produktpräferenzen (Default und temporäreFixes-Korrektur bei BQoL-MasterOFF) aus.
