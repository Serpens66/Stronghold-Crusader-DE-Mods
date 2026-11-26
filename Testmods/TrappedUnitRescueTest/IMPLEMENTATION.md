# Rettungssystem: aktueller Implementierungsstand

Fortsetzung 26.11.2026: Die autorisierte Singleplayer-Abnahme läuft nach bestätigtem Abschluss des Mehrfachplatzierungs-Chats. Der aktuelle Testmodbuild besteht mit64637 Offlineprüfungen, das isolierte SDK auf Sicherungscommit25a9b35 mit1794 Tests und einem bestehenden optionalen Skip. Tatsächliche AIV-/CreatePrefab-/Next-/Gruppen-/Leerlauf- und Fixes-Reiterbelege liegen vor; die dadurch aufgedeckte Lücke bei begonnenen Schritten ist korrigiert und für Apfelarbeiter, Ochsen, Müller sowie gewöhnliche Befehle einschließlich Next-only-Moat im Spiel geprüft. Auch Pending- und Flags5-Save/Load-Fälle sind einzeln belegt. Weitere Rollen und Sonderfälle bleiben offen. Aktuelle Durchführung und genaue Grenzen: [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md); übrige Arbeiterfamilien: [WORKER-GAME-MATRIX.md](WORKER-GAME-MATRIX.md). Zielintegration ist BugfixesAndQoL, erst nach vollständiger Abnahme. Die folgenden datierten Abschnitte halten den vorangegangenen Offlineabschluss fest.

Stand 25.11.2026. Entwicklerbericht und Offline-Nachweis, keine neue Spielabnahme. Der Auftrag bleibt auf Testmod und getrennte APIShared-Arbeitskopie beschränkt. Keine Installation, Spielstarts oder Produktionsmodmigration. Die ausdrücklich angeforderten lokalen Sicherungscommits sind erfolgt; nichts wurde gepusht.

## Quellzuständigkeit und Entwicklungs-SDK

- Testmod: `Testmods/TrappedUnitRescueTest`, Version 0.1.0; eigene experimentelle Einstellung weiterhin standardmäßig aus.
- API-Kopie: `.work/a/rescue-api/001`, Branch `codex/rescue-observation`, Basis `681f57f36cdd349bc540a52651d81b3effad04a6`. Sicherungscommit `3eee4849a3bb81f7098415a670f457aa8dfa689d`. Eigenständige Entwicklung für spätere gesonderte Integration; nicht löschen.
- Callbackkorrekturen aus `855516b19236e9c0cd809e182b17b74aa69d0123` vollständig übernommen und mit Gegenbeispielen geprüft. Der kanonische APIShared-Checkout bleibt durch diesen Auftrag unverändert.
- Die neuen Dienste brauchen APIShared 0.74.0 aus dieser Entwicklung. Manifest und BepInDependency stimmen überein; die öffentliche Version 0.73.4 enthält diese Dienste noch nicht.
- Tatsächlich ausgewählte installierte Extenderreferenz: 2.14.6. Native-Hash: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Frühere Berichte bleiben datierte Momentaufnahmen.

Der abschließende vollständige Entwicklungs-SDK-Build besteht: 1787 Tests bestanden, ein optionaler Consumer-Rescan übersprungen, zusätzliche native Fixtures bestanden; öffentliche API gegenüber der Entwicklungsbasis +112/-0. Nachweis: `.work/a/rescue-impl/001/api-full-build.log` und `.work/a/rescue-api/001/.local/build-proof.json`. Aktuelle Eingaben und Ausgaben immer gegen diesen Nachweis prüfen; eine Versionsnummer allein beweist keine identischen Dateien.

## Automatische Erkennung und kausaler Nachweis

`WorldMutationEvents` beobachtet 15 effektive Originalquellen, private Receipts und vollständige äußere Batches einschließlich Mirrors. Der Testmod ermittelt die tatsächliche lokale Änderungshülle und erfasst Current-Belegung samt einem Feld Rand für gewöhnliche Next-Schritte. Es gibt keine permanente Beobachtung aller Einheiten oder Kartenzellen.

AIV-Cleanup, entfernte Compoundteile, Farmplots, native BFS-Brushes, tatsächliche Keep-Offsettabellen und mögliche CastlePlanner-/Fixes-Rotationen gehören zur Hülle. Nichtterminale Grabenarbeit wird vor teurer Erfassung anhand des tatsächlichen signed-Byte-Fortschritts ausgeschlossen. Der seltene Keep-Replacementpfad benötigt einen begrenzten Gebäudeslotindex innerhalb des Bauereignisses; dieser bleibt nicht über Mutationen hinweg erhalten.

Eine spätere Änderung entwertet historische Kandidaten und Geometrien bereits vor ihrem Original, wenn ihre vollständige Hülle die ursprüngliche Radius8-Suchregion überschneidet. Dafür genügt die geplante Hülle auch bei null gefundenen Einheiten oder anschließend gescheiterter Belegungserfassung. Räumlich getrennte Fälle bleiben erhalten. Unbekannte Hüllen liefern keine Teilfreigabe. Ein fehlgeschlagener früher oder später Teilcapture markiert den ganzen Batch ungültig; ein bereits verworfener Actor wird im selben Batch nicht durch ein späteres Kind erneut aufgenommen.

Der Coordinator sammelt bis zum äußeren Batchende und rettet erst am zugelassenen Simulationstick. Er behält die früheste belegte lokale Erreichbarkeit. Eine veränderte Einheit wird einzeln verworfen; ihre unveränderten Nachbarn verlieren dadurch nicht ihre unabhängigen Nachweise. Identität, Sitzung/Welt, Ziel, AI, Arbeitsdaten und Verbandsstempel werden erneut geprüft. Legitime Schäden zwischen Erfassung und Rettung werden übernommen, niemals geheilt. Von einer begonnenen Bewegung ist höchstens der zuvor erfasste einzelne Schritt zulässig.

Sichere Ziele müssen zuvor lokal erreichbar und jetzt gewöhnlich sowie frei sein. Ein tatsächlicher gewöhnlicher Nachbar mit passender Vorwärts- oder Rückwärtskante muss bestehen; ein beliebiges nichtnull Richtungsbyte genügt nicht. Ein weiterhin legales Current kann bei ausschließlich ungültigem Next selbst Normalisierungsziel sein.

## Befehlsabsicht und qualifizierte Fortsetzung

`UnitMoveEvents` nutzt den bestehenden gemeinsamen Originalbesitzer. Effektive Einheit/Ziel/Modus, tatsächliches Native0/1, Veto, Umleitung, Ausnahme und fremde verschachtelte Aufträge bleiben getrennt. Die veränderbare Legacy-Post-Rückgabe ist keine Erfolgsquelle. Entry-Bytes und indirekter Pointer-Slot des tatsächlichen Extender-Hooks werden geprüft; fremde Überschreibungen sperren den Dienst vor einem eigenen Aufruf.

Für die 20 auditierten Wirtschaftsrollen sind ausschließlich belegte gewöhnliche Reiseabschnitte freigegeben. Kontaktarbeit, Transport, Tunnel und unbewiesene Sonderflächen sind davon nicht umfasst. Für militärische Befehle gilt die genaue Typ-/Phasenmatrix in [Audits/COMMAND-RESUME.md](Audits/COMMAND-RESUME.md). Insbesondere rechtfertigt A9E allein keine Freigabe: auch Tiere tragen den Wert. Trebuchet, Engineer0/1 und sechs nicht vorhandene Fernkampf-AI1-Zweige werden nicht als generische Reise behandelt.

Auch stationäre Soldaten erhalten einen unveränderlichen Semantiknachweis. Nur die belegten AI0/1-Abschnitte und Spearman2 sind zulässig; stationäres AI101 bleibt ein eigener Kontaktfall. Ein stationärer Nachweis kann weder durch Save-Laden noch durch den Runtimepfad zu einem aktiven Resume-Receipt werden. Dig/Fill wird über das tatsächliche Befehlsfeld9F4 erkannt; WorkMode956 mit derselben Zahl ist kein Digauftrag.

Vor einer eigenen Versetzung oder funktionalen Neuplanung existiert bereits eine reservierte Fortsetzungsquittung. Eine erfolglose Suche darf keine falsche Ankunft oder Warenproduktion erlauben: die eigene Wartephase verwendet die auditierte Flags5-Ankunftssperre nur in kohärenter Pose mit Substep8, Current=Next und leerem740. Retry ist begrenzt; danach wartet die Quittung auf eine neue Änderung. Neue native AI-/Kontakt-/Befehlsabsichten gewinnen, statt alte Zustände zurückzuschreiben.

Auf neuem fertigem Graben wird für qualifizierte aktive Reisen zunächst der echte ursprüngliche Auftrag funktional geprüft. Eine passende neue Moat-Veröffentlichung bedeutet keine Versetzung. Bei bestätigter Ablehnung bleiben lokale Rettung und erneute Veröffentlichung durch den reservierten Besitzer abgesichert. Nicht fortsetzbare aktive Routen besitzen keinen alten ungesicherten Direktpfad mehr.

## Fehlgeschlagener neuer Auftrag mit erhaltenem Geometrienachweis

P7 trennt alte räumliche Belege strikt von der aktuellen Absicht. `RescueGeometryWitness` enthält nur Lebensidentität, Typ/Besitzer, ursprüngliches Current und gegebenenfalls genau ein angenommenes Next sowie kopierte alte Ausweichfelder. Er enthält keine ausführbare alte AI, Aufgabe, Warenbindung oder Zielposition. Originalalter und Versuchsverbrauch werden bei neuen Aufträgen nicht zurückgesetzt.

Der finale Move-Begin läuft nach der bestehenden Auftragsinvalidierung. Nur ein frisch erfasster qualifizierter aktiver Auftrag mit effektivem Ziel gleich dem aktuellen Requested-Ziel und Extra0 kann den Nachweis ergänzen. Die aktive Arbeitsplatz-/Lager-/Vegetations-/Crewidentität wird dabei bereits vor dem Original festgehalten. Ausschließlich ein tatsächliches normales Native0 kann die Prüfung am nächsten Simulationstakt anstoßen. Native1 verwirft den räumlichen Nachweis auch dann, wenn ein älterer äußerer Aufruf nach einem neueren verschachtelten Native0 erfolgreich wird.

Am Takt werden frische Absicht, Bindungen, Verband und Pose erneut geprüft. Zulässig sind nur die ursprünglichen räumlichen Anker und höchstens der ursprünglich angenommene Schritt. Ein weiterer oder unbelegter Schritt verwirft den Nachweis. Der Join übernimmt die frische Befehlsabsicht, aber die unveränderte alte Ausweichmenge; beim Wechsel auf originales Next darf deshalb ein ursprüngliches Feld neun Koordinaten entfernt liegen. Diese zweite Koordinatenbasis wird nicht fälschlich auf Radius8 gekürzt.

Geometrie allein löst keine Aktion aus. Ein beliebiger Wegefehler ohne beobachtete Vorhergeometrie, ein unerreichbares Ziel und ein Altsave ohne eigenen Beleg bleiben ohne Teleport. Während Begin/Completion und bis zur ersten zugelassenen Taktgrenze sperrt eine offene Replan-Phase die atomare Speicherung. Späte Null-Completions hinter dieser Grenze können keinen späteren Auftrag aktivieren.

## Neuer Fluchtauftrag für untätige Einheiten auf Moat

Für einen streng qualifizierten untätigen Ausgangszustand gibt es einen getrennten Besitzer: `IdleEscapeOwner`. Er übernimmt keinen alten Arbeitsauftrag. Er erteilt einmal einen echten neuen Auftrag zu einem belegten alten Ausweichfeld und beobachtet eine veröffentlichte Route als `EscapeInProgress`. Erst physische Ankunft erhöht seinen Abschlusszähler. Es gibt keine Planner-Wiederholung, AI-Rücksetzung oder eigene Arrival-Barriere.

Vor dem tatsächlichen Original prüft eine eigene private Completion-Phase die ursprüngliche Pose und Semantik erneut, auch nach fremden Begin-Observern. Nur ein bestätigtes Native0 mit normal abgeschlossenem Wrapper erlaubt nach erneuter Prüfung die lokale Ersatzversetzung. Veto, Umleitung, Originalausnahme und Postausnahme nach Native0 erlauben sie nicht. Native1 darf nach einer Legacy-Postausnahme weiterhin ausschließlich beobachtet werden, sofern aktuelle Identität und veröffentlichte Route tatsächlich stimmen.

Neue Aufträge und legitime Zustandsänderungen beenden die Beobachtung. Der bereits veröffentlichte normale native Auftrag benötigt keine zusätzliche Receipt-Persistenz: beim Laden wird er nicht nochmals erteilt. Details und native Widerlegungen: [Audits/IDLE-MOAT-ESCAPE.md](Audits/IDLE-MOAT-ESCAPE.md).

## Speicherung und Lifecycle

Die ausgewählte Vegetationsidentität wird bereits vor der Mutation erfasst. Native Arrayindizes werden an der SDK-Grenze übersetzt und gegen die wirkliche Datensatzadresse geprüft. Eine gelöschte oder unter derselben Nummer neu belegte Baumidentität wird vor jeder Rettungsaktion abgelehnt; sie darf nicht erst am Recoverytick neu übernommen werden. Dasselbe gilt nach dem Laden ausstehender Kandidaten mit Codec4.

Eigene wartende Fortsetzungen und noch nicht abgearbeitete kausale Kandidaten werden als begrenzte binäre Einträge über APIShared `VersionedSaveData` gespeichert. Capture verändert keine Simulation. Alle Einträge werden vor dem ersten Write validiert; unvollständige Recovery-/Reservierungsphasen oder halb beobachtete externe Orders sperren den atomaren Save vor Dateiersetzung.

Prozesslokale Welt-/Befehls-/Batchnummern werden beim Laden nicht wiederverwendet. Alter, Versuchsverbrauch, unveränderliche Semantik und alte lokale Erreichbarkeit bleiben erhalten. Das repariert keine alten Saves ohne eigene Vorherdaten.

Ausgeschaltete Rettung startet keine Eingriffe. Gespeicherte Worker-Eigentumsquittungen bleiben als Nachweis erhalten; es erfolgt keine automatische Zustandsrücksetzung. Noch ungebundene kausale Kandidaten können bei pausiert geladenem, ausgeschaltetem Feature unverändert weitergespeichert werden. Der erste fortschreitende inaktive Tick verwirft diese historischen Kandidaten ohne native Änderung. Idle-Fluchtbeobachtung ist reine Metadatenarbeit und wird bei Ende/Load/Ausschalten verworfen.

Langlebige statische Mission-, Bau-, Move- und Simulationstakt-Publisher tragen die Runtime nach dem SHCDE-Startup-Cleanup. Es gibt keine Update-/Coroutine-Hostkomponente oder Hookentfernung bei Plugin-OnDestroy. Reale Multiplayerfreigabe bleibt geschlossen.

## Budgets und messbare Grenzen

- Gemeinsam maximal512 kausale Kandidaten und erhaltene Geometrien; Lebenszeit80 native Ticks und höchstens4 Evaluierungen je Fall. Ein Tick validiert maximal32 frische Replan-Nachweise, besucht maximal32 Queueeinträge und versucht insgesamt8 Kandidaten. Unbesuchte frische Trigger werden verworfen; ein neuer tatsächlicher Fehler ist nötig. Metadatenbereinigung ist auf512 Einträge begrenzt und liest keine Einheiten.
- Maximal512 eigene Worker-/Command-Quittungen; der Retrytakt besucht32 und versucht4. Externe Handoff-Reconciliation ist gesondert durch die512 Quittungen begrenzt und ruft keinen Planner auf.
- Maximal64 Idle-Fluchtbeobachtungen; maximal8 Beobachtungen je Tick, Deadline600 Ticks. Mehrfache Callbacks desselben Ticks verbrauchen das Budget nicht nochmals. Beobachtung ruft weder Planner noch Relocation auf.
- Ein Kandidatenversuch ist nicht ein Nativecall: Probe, Relocation und Resume können drei Einstiege benötigen. Gegenwärtige obere Grenze im Recoverytick:28 direkte native Einstiege, davon maximal20 Move-Suchen. Vanilla-Suchlaufzeit wird dadurch nicht begrenzt.
- Historische Überschneidungsprüfung nur bei Bauereignissen und offenen Fällen: maximal2048 Hülleneinträge indizieren und pro höchstens512 Altfall maximal289 Koordinatenschlüssel prüfen; keine nativen Einheitenleser. Maximal64 aktive Batchnachweise. Kann ein fehlgeschlagener Batch wegen dieser Grenze nicht mehr vermerkt werden, werden offene Nachweise ungültig und die Erfassung bleibt bis zum Weltwechsel gesperrt.
- Hülle maximal2048 aktive Felder/131072 Work; Discovery maximal4096 Randzellen/16384 Belegungslinks/512 Einheiten. Überlast wird benannt abgelehnt; keine Teilhülle gilt als vollständig.
- Größter vermessener Keepfall: Mindestgrenze67932 Work,1219 Tiles,7584 eindeutige native Reads,3999 einmalige Gebäudeslotprüfungen. Das sind deterministische Operationszähler, keine FPS-Messung.
- Beim erstmaligen Geräte-Rettungsversuch ist wegen fehlender nativer Rückreferenz eine begrenzte Prüfung möglicher Crew-Aliase nötig: einmal bis9999 Unitslots pro Gerät, gemeinsam für alle Ingenieure. Kein solcher Vollvergleich bei normalen Soldaten, leerem Betrieb oder Worker-Retry. Reale Kosten und dichte Gerätegruppen müssen im Spiel gemessen werden.

## Abgleich mit dem Offlineauftrag

| Angeforderter Baustein | Tatsächliche Umsetzung und Offlinebeleg | Grenze |
|---|---|---|
| Kandidatenkern mit Identität, Ursache, Dedupe, Ablauf und Budget | Produktionsqueue, Coordinator, Geometrie-/Intenttrennung und Codecs werden direkt in den ausführbaren Fixtures verwendet | Keine Behauptung einer gemessenen Ticklaufzeit |
| Lokale Current-/Next-Erkennung einschließlich Grenzen und beschädigter Belegung | Produktions-Discovery und vollständiger nativer Footprintadapter mit injizierten Lesern; Rand-, Kapazitäts-, Listen- und verschachtelte Fälle | Reale native Listenübergänge im Spiel noch offen |
| Brückenpilot auf gemeinsamen Mechanismus umstellen | Gemeinsamer WorldMutation-Batch, lokale Erfassung, deferred Recoverytick und bestehender nativer Rettungs-/Fortsetzungsbesitzer | Historische Pilot-Spieltests ersetzen keine Abnahme des neuen Coordinators |
| Weitere Bauquellen, gewöhnliche Soldaten, Grabenabschluss und Arbeiterreisen |15 effektive Quellen,23 Creatorfamilien, terminale Dig/Fill-Gates, ausdrückliche Command-/20-Rollen-Matrix und Crewschutz | Nur auditierte Phasen freigegeben; echte Arbeits-/Gruppenergebnisse offen |
| APIShared sinnvoll gemeinsam nutzen | Isolierte SDK-Kopie mit WorldMutationEvents, finalem UnitMoveEvents-Broker, SimulationTickEvents, Lifecycle und VersionedSaveData; vorhandene native Originalbesitzer | Späterer Merge und Installation separat |
| Spätere Spielprüfungen aufschreiben | INGAME-PENDING.md mit positiven/negativen Voraussetzungen, P7, Lebensidentität, Belegung, Lieferzyklus, Save/Editor/MP und Last | Kein darin genannter neuer Spieltest wurde in diesem Auftrag ausgeführt |

## Aktuelle Offline-Nachweise und verbleibende Arbeit

- `.work/a/rescue-impl/001/p7-final-build.log`: abschließender integrierter Normalbuild mit64101 Einzelprüfungen in21 Suiten plus Fixes-IL-/Cecil-Verträgen bestanden. Aktuelle Paket- und Quellidentitäten stehen in `.work/a/rescue-impl/001/offline-completion.json`. Der frühere61407-Stand in integrated-stage.json bleibt historischer Nachweis.
- `.work/a/rescue-impl/001/p7-final-boundaries.log`: modbezogene Ownership-, JSON-, Lifecycle-, Abhängigkeits-, XAML- und permanente Hookgrenzen. 21 Locales mit vier konsistenten Schlüsseln geprüft; kompiliertes Paket ohne DEVDIAG-Marker/-Typ/-Aufruf.
- `.work/a/rescue-impl/001/api-full-build.log` und API-Kopie `.local/build-proof.json`: vollständiger Entwicklungs-SDK-Build ohne Installation.
- Unabhängige Prüfung der Idle-Originalphase, Native0/1-/Post-Ausnahmegrenzen, Supersession, Save und Lifecycle sowie des korrigierten Fremdauftrag-Handoffs und der vollständigen zeitlichen Vegetationsbindung abgeschlossen; kein weiterer bestätigter Befund.
- Kanonische Native-Baseline `TRAPPED_UNIT_RESCUE_AUDIT.md` um die belegten Bau-, Bewegungs-, Identitäts- und Phasenverträge ergänzt; Rohprojekte und historische Nachweise unverändert.

Der gesonderte P7-Vertrag für erhaltene Geometrie und frische Befehlsabsicht ist im Coordinator und Speicherformat implementiert. Untersuchung und Grenzen stehen in `Audits/MOVE-FAILURE-SOURCE.md`. Die integrierte Offline-Abnahme einschließlich späterer überschneidender Geometrieänderungen ist bestanden. Geometrie209, frische Absicht928, Persistenz637 und Coordinator919 Prüfungen sind Teil des abschließenden Gesamtbuilds. Der bisherige Gesamtbuild und die API-Übergabe sind nachgewiesen; siehe auch [API-HANDOFF.md](API-HANDOFF.md). Fehlgeschlagene Wege ohne Vorherdaten erfinden keine sichere alte Ausweichseite. Die tatsächlichen Spiel-, Gruppen-, Wirtschafts-, Save-/Editor-, Kompatibilitäts-, Last- und MP-Abnahmen stehen vollständig offen in [INGAME-PENDING.md](INGAME-PENDING.md). Die spätere Migration nach Pathfinding erfolgt erst auf gesonderten Auftrag nach Abnahme.


Der Offlineauftrag ist für den dokumentierten Umfang abgeschlossen. Das Paket ist ein experimenteller Teststand, keine allgemeine Spielfreigabe. Insbesondere bleiben Jobs außerhalb der auditierten Reisephasen, Sonderbewegungen und historische Defekte ohne Vorherdaten ausgeschlossen. Eine neue Besitzerpolitik, echte Multiplayerfreigabe und Migration nach Pathfinding sind gesonderte Entscheidungen beziehungsweise spätere Aufträge.
