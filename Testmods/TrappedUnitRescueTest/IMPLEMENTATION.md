# Rettungssystem: aktueller Implementierungsstand

Stand 25.11.2026. Entwicklerbericht und Offline-Nachweis, keine neue Spielabnahme. Der Auftrag bleibt auf Testmod und getrennte APIShared-Arbeitskopie beschränkt. Keine Installation, Spielstarts, Produktionsmodänderungen oder Commits durch diesen Auftrag.

## Quellzuständigkeit und Entwicklungs-SDK

- Testmod: `Testmods/TrappedUnitRescueTest`, Version 0.1.0; eigene experimentelle Einstellung weiterhin standardmäßig aus.
- API-Kopie: `.work/a/rescue-api/001`, Branch `codex/rescue-observation`, Basis `681f57f36cdd349bc540a52651d81b3effad04a6`. Eigenständige, uncommittierte Entwicklung für spätere gesonderte Integration; nicht löschen.
- Callbackkorrekturen aus `855516b19236e9c0cd809e182b17b74aa69d0123` vollständig übernommen und mit Gegenbeispielen geprüft. Der kanonische APIShared-Checkout bleibt durch diesen Auftrag unverändert.
- Die neuen Dienste brauchen APIShared 0.74.0 aus dieser Entwicklung. Manifest und BepInDependency stimmen überein; die öffentliche Version 0.73.4 enthält diese Dienste noch nicht.
- Tatsächlich ausgewählte installierte Extenderreferenz: 2.14.6. Native-Hash: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Frühere Berichte bleiben datierte Momentaufnahmen.

Der abschließende vollständige Entwicklungs-SDK-Build besteht: 1787 Tests bestanden, ein optionaler Consumer-Rescan übersprungen, zusätzliche native Fixtures bestanden; öffentliche API gegenüber der Entwicklungsbasis +112/-0. Nachweis: `.work/a/rescue-impl/001/api-full-build.log` und `.work/a/rescue-api/001/.local/build-proof.json`. Aktuelle Eingaben und Ausgaben immer gegen diesen Nachweis prüfen; eine Versionsnummer allein beweist keine identischen Dateien.

## Automatische Erkennung und kausaler Nachweis

`WorldMutationEvents` beobachtet 15 effektive Originalquellen, private Receipts und vollständige äußere Batches einschließlich Mirrors. Der Testmod ermittelt die tatsächliche lokale Änderungshülle und erfasst Current-Belegung samt einem Feld Rand für gewöhnliche Next-Schritte. Es gibt keine permanente Beobachtung aller Einheiten oder Kartenzellen.

AIV-Cleanup, entfernte Compoundteile, Farmplots, native BFS-Brushes, tatsächliche Keep-Offsettabellen und mögliche CastlePlanner-/Fixes-Rotationen gehören zur Hülle. Nichtterminale Grabenarbeit wird vor teurer Erfassung anhand des tatsächlichen signed-Byte-Fortschritts ausgeschlossen. Der seltene Keep-Replacementpfad benötigt einen begrenzten Gebäudeslotindex innerhalb des Bauereignisses; dieser bleibt nicht über Mutationen hinweg erhalten.

Der Coordinator sammelt bis zum äußeren Batchende und rettet erst am zugelassenen Simulationstick. Er behält die früheste belegte lokale Erreichbarkeit. Eine veränderte Einheit wird einzeln verworfen; ihre unveränderten Nachbarn verlieren dadurch nicht ihre unabhängigen Nachweise. Identität, Sitzung/Welt, Ziel, AI, Arbeitsdaten und Verbandsstempel werden erneut geprüft. Legitime Schäden zwischen Erfassung und Rettung werden übernommen, niemals geheilt. Von einer begonnenen Bewegung ist höchstens der zuvor erfasste einzelne Schritt zulässig.

Sichere Ziele müssen zuvor lokal erreichbar und jetzt gewöhnlich sowie frei sein. Ein tatsächlicher gewöhnlicher Nachbar mit passender Vorwärts- oder Rückwärtskante muss bestehen; ein beliebiges nichtnull Richtungsbyte genügt nicht. Ein weiterhin legales Current kann bei ausschließlich ungültigem Next selbst Normalisierungsziel sein.

## Befehlsabsicht und qualifizierte Fortsetzung

`UnitMoveEvents` nutzt den bestehenden gemeinsamen Originalbesitzer. Effektive Einheit/Ziel/Modus, tatsächliches Native0/1, Veto, Umleitung, Ausnahme und fremde verschachtelte Aufträge bleiben getrennt. Die veränderbare Legacy-Post-Rückgabe ist keine Erfolgsquelle. Entry-Bytes und indirekter Pointer-Slot des tatsächlichen Extender-Hooks werden geprüft; fremde Überschreibungen sperren den Dienst vor einem eigenen Aufruf.

Für die 20 auditierten Wirtschaftsrollen sind ausschließlich belegte gewöhnliche Reiseabschnitte freigegeben. Kontaktarbeit, Transport, Tunnel und unbewiesene Sonderflächen sind davon nicht umfasst. Für militärische Befehle gilt die genaue Typ-/Phasenmatrix in [Audits/COMMAND-RESUME.md](Audits/COMMAND-RESUME.md). Insbesondere rechtfertigt A9E allein keine Freigabe: auch Tiere tragen den Wert. Trebuchet, Engineer0/1 und sechs nicht vorhandene Fernkampf-AI1-Zweige werden nicht als generische Reise behandelt.

Auch stationäre Soldaten erhalten einen unveränderlichen Semantiknachweis. Nur die belegten AI0/1-Abschnitte und Spearman2 sind zulässig; stationäres AI101 bleibt ein eigener Kontaktfall. Ein stationärer Nachweis kann weder durch Save-Laden noch durch den Runtimepfad zu einem aktiven Resume-Receipt werden. Dig/Fill wird über das tatsächliche Befehlsfeld9F4 erkannt; WorkMode956 mit derselben Zahl ist kein Digauftrag.

Vor einer eigenen Versetzung oder funktionalen Neuplanung existiert bereits eine reservierte Fortsetzungsquittung. Eine erfolglose Suche darf keine falsche Ankunft oder Warenproduktion erlauben: die eigene Wartephase verwendet die auditierte Flags5-Ankunftssperre nur in kohärenter Pose mit Substep8, Current=Next und leerem740. Retry ist begrenzt; danach wartet die Quittung auf eine neue Änderung. Neue native AI-/Kontakt-/Befehlsabsichten gewinnen, statt alte Zustände zurückzuschreiben.

Auf neuem fertigem Graben wird für qualifizierte aktive Reisen zunächst der echte ursprüngliche Auftrag funktional geprüft. Eine passende neue Moat-Veröffentlichung bedeutet keine Versetzung. Bei bestätigter Ablehnung bleiben lokale Rettung und erneute Veröffentlichung durch den reservierten Besitzer abgesichert. Nicht fortsetzbare aktive Routen besitzen keinen alten ungesicherten Direktpfad mehr.

## Neuer Fluchtauftrag für untätige Einheiten auf Moat

Für einen streng qualifizierten untätigen Ausgangszustand gibt es einen getrennten Besitzer: `IdleEscapeOwner`. Er übernimmt keinen alten Arbeitsauftrag. Er erteilt einmal einen echten neuen Auftrag zu einem belegten alten Ausweichfeld und beobachtet eine veröffentlichte Route als `EscapeInProgress`. Erst physische Ankunft erhöht seinen Abschlusszähler. Es gibt keine Planner-Wiederholung, AI-Rücksetzung oder eigene Arrival-Barriere.

Vor dem tatsächlichen Original prüft eine eigene private Completion-Phase die ursprüngliche Pose und Semantik erneut, auch nach fremden Begin-Observern. Nur ein bestätigtes Native0 mit normal abgeschlossenem Wrapper erlaubt nach erneuter Prüfung die lokale Ersatzversetzung. Veto, Umleitung, Originalausnahme und Postausnahme nach Native0 erlauben sie nicht. Native1 darf nach einer Legacy-Postausnahme weiterhin ausschließlich beobachtet werden, sofern aktuelle Identität und veröffentlichte Route tatsächlich stimmen.

Neue Aufträge und legitime Zustandsänderungen beenden die Beobachtung. Der bereits veröffentlichte normale native Auftrag benötigt keine zusätzliche Receipt-Persistenz: beim Laden wird er nicht nochmals erteilt. Details und native Widerlegungen: [Audits/IDLE-MOAT-ESCAPE.md](Audits/IDLE-MOAT-ESCAPE.md).

## Speicherung und Lifecycle

Eigene wartende Fortsetzungen und noch nicht abgearbeitete kausale Kandidaten werden als begrenzte binäre Einträge über APIShared `VersionedSaveData` gespeichert. Capture verändert keine Simulation. Alle Einträge werden vor dem ersten Write validiert; unvollständige Recovery-/Reservierungsphasen oder halb beobachtete externe Orders sperren den atomaren Save vor Dateiersetzung.

Prozesslokale Welt-/Befehls-/Batchnummern werden beim Laden nicht wiederverwendet. Alter, Versuchsverbrauch, unveränderliche Semantik und alte lokale Erreichbarkeit bleiben erhalten. Das repariert keine alten Saves ohne eigene Vorherdaten.

Ausgeschaltete Rettung startet keine Eingriffe. Gespeicherte Worker-Eigentumsquittungen bleiben als Nachweis erhalten; es erfolgt keine automatische Zustandsrücksetzung. Noch ungebundene kausale Kandidaten können bei pausiert geladenem, ausgeschaltetem Feature unverändert weitergespeichert werden. Der erste fortschreitende inaktive Tick verwirft diese historischen Kandidaten ohne native Änderung. Idle-Fluchtbeobachtung ist reine Metadatenarbeit und wird bei Ende/Load/Ausschalten verworfen.

Langlebige statische Mission-, Bau-, Move- und Simulationstakt-Publisher tragen die Runtime nach dem SHCDE-Startup-Cleanup. Es gibt keine Update-/Coroutine-Hostkomponente oder Hookentfernung bei Plugin-OnDestroy. Reale Multiplayerfreigabe bleibt geschlossen.

## Budgets und messbare Grenzen

- Maximal 512 kausale Kandidaten; Lebenszeit80 native Ticks und höchstens4 Evaluierungen je Kandidat. Ein Tick besucht maximal32 Queueeinträge und versucht8 Kandidaten.
- Maximal512 eigene Worker-/Command-Quittungen; der Retrytakt besucht32 und versucht4. Externe Handoff-Reconciliation ist gesondert durch die512 Quittungen begrenzt und ruft keinen Planner auf.
- Maximal64 Idle-Fluchtbeobachtungen; maximal8 Beobachtungen je Tick, Deadline600 Ticks. Mehrfache Callbacks desselben Ticks verbrauchen das Budget nicht nochmals. Beobachtung ruft weder Planner noch Relocation auf.
- Ein Kandidatenversuch ist nicht ein Nativecall: Probe, Relocation und Resume können drei Einstiege benötigen. Gegenwärtige obere Grenze im Recoverytick:28 direkte native Einstiege, davon maximal20 Move-Suchen. Vanilla-Suchlaufzeit wird dadurch nicht begrenzt.
- Hülle maximal2048 aktive Felder/131072 Work; Discovery maximal4096 Randzellen/16384 Belegungslinks/512 Einheiten. Überlast wird benannt abgelehnt; keine Teilhülle gilt als vollständig.
- Größter vermessener Keepfall: Mindestgrenze67932 Work,1219 Tiles,7584 eindeutige native Reads,3999 einmalige Gebäudeslotprüfungen. Das sind deterministische Operationszähler, keine FPS-Messung.
- Beim erstmaligen Geräte-Rettungsversuch ist wegen fehlender nativer Rückreferenz eine begrenzte Prüfung möglicher Crew-Aliase nötig: einmal bis9999 Unitslots pro Gerät, gemeinsam für alle Ingenieure. Kein solcher Vollvergleich bei normalen Soldaten, leerem Betrieb oder Worker-Retry. Reale Kosten und dichte Gerätegruppen müssen im Spiel gemessen werden.

## Aktuelle Offline-Nachweise und verbleibende Arbeit

- `.work/a/rescue-impl/001/stage-idle-build.log`: integrierter Build, tatsächliche gemeinsame Policy-/Coordinator-/Persistenz-/Native-Fixtures. Spätere endgültige Identität wird im Übergabenachweis festgehalten.
- `.work/a/rescue-impl/001/idle-boundaries.log`: modbezogene Ownership-, JSON-, Lifecycle-, Abhängigkeits-, XAML- und permanente Hookgrenzen.
- `.work/a/rescue-impl/001/api-full-build.log` und API-Kopie `.local/build-proof.json`: vollständiger Entwicklungs-SDK-Build ohne Installation.
- Unabhängige Prüfung der Idle-Originalphase, Native0/1-/Post-Ausnahmegrenzen, Supersession, Save und Lifecycle abgeschlossen; kein weiterer bestätigter Befund.
- Kanonische Native-Baseline `TRAPPED_UNIT_RESCUE_AUDIT.md` um die belegten Bau-, Bewegungs-, Identitäts- und Phasenverträge ergänzt; Rohprojekte und historische Nachweise unverändert.

Noch abzuschließen sind der gesonderte Bewegungsfehler-Eingang P7, endgültiger Gesamtbuild/Identitätsnachweis und die nachvollziehbare Übergabe der API-Arbeitskopie. Fehlgeschlagene Wege ohne Vorherdaten erfinden keine sichere alte Ausweichseite. Die tatsächlichen Spiel-, Gruppen-, Wirtschafts-, Save-/Editor-, Kompatibilitäts-, Last- und MP-Abnahmen stehen vollständig offen in [INGAME-PENDING.md](INGAME-PENDING.md). Die spätere Migration nach Pathfinding erfolgt erst auf gesonderten Auftrag nach Abnahme.
