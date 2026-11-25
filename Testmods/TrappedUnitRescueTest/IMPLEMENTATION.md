# Rettungssystem: aktueller Implementierungsstand

Stand 25.11.2026. Fortlaufender Entwicklerbericht, keine Ingame-Abnahme. Auftrag: Testmod und getrennte APIShared-Arbeitskopie, ausschließlich offline; keine Installation, Spielstarts, Produktionsmodänderungen oder Commits.

## Quellzuständigkeit und Entwicklungs-SDK

- Testmod: `Testmods/TrappedUnitRescueTest`, Version 0.1.0; eigene Einstellung weiterhin standardmäßig aus.
- API-Kopie: `.work/a/rescue-api/001`, Branch`codex/rescue-observation`, Basis`681f57f36cdd349bc540a52651d81b3effad04a6`. Uncommittierte Entwicklung für spätere gesonderte Integration; nicht löschen.
- Callbackkorrekturen aus`855516b19236e9c0cd809e182b17b74aa69d0123` vollständig übernommen. Kanonischer APIShared-Checkout bleibt durch diesen Auftrag unverändert.
- Neue Dienste brauchen die eigene Entwicklungsversion APIShared 0.74.0. Manifest und BepInDependency des Testmods stimmen überein. Öffentliche 0.73.4 enthält diese neuen Dienste nicht.
- Tatsächlich ausgewählte installierte Extenderreferenz:2.14.6. Frühere 2.14.4-Berichte bleiben historische Momentaufnahmen. Native-Hash weiterhin`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Der API-Brokerbuild mit 15 Quellen besteht vollständig; dokumentierter Zwischenstand 1776 Tests bestanden,1 optionaler Consumer-Rescan übersprungen, zusätzliche native Fixtures bestanden. Ein weiterer finaler UnitMove-Dienst wird wegen der unten beschriebenen Ergebnis-/Argumentgrenze ergänzt; der bisherige Build belegt diesen neuen Dienst ausdrücklich noch nicht. Aktuelle Buildidentitäten nach weiteren Änderungen immer aus`.local/build-proof.json` der Arbeitskopie nehmen, nicht aus dieser Momentaufnahme.

## Bereits verbundene Funktion

`WorldMutationEvents` beobachtet effektive Originalaufrufe, private Receipts und ganze äußere Batches einschließlich Mirrors. Der Testmod ermittelt die tatsächliche lokale Änderungshülle, validiert Current-Belegung samt Rand für gewöhnliche Next-Schritte und erfasst nur lokale betroffene Einheiten. Keine dauerhafte Beobachtung aller Einheiten oder aller Kartenzellen.

AIV-Cleanup, Compoundteile, Farmplots, native BFS-Brushes, echte Keep-Offsettabellen und mögliche CastlePlanner-/Fixes-Rotationen gehören zur Hülle. NativeDomänen, Tabellen und Adressen sind an den auditierten Hash gebunden. Nichtterminale Grabenarbeit wird vor teurer Erfassung anhand des tatsächlichen signedByte-Fortschritts ausgeschlossen. Reale Keepfälle werden innerhalb eines seltenen Bauereignisses mit festem Budget verarbeitet; das ist kein proTick-Gebäudescan.

Der Coordinator sammelt bis zum äußeren Batchende, führt dort keine Rettung aus und prüft erst am zugelassenen Simulationstick. Keine alte Einheit oder alte Befehlsabsicht wird allein wegen ähnlicher Koordinaten wiederverwendet. Einheit/Global-ID, Sitzung/Welt, Ziel, AI, Arbeitsdaten und Gruppenidentität werden erneut geprüft. Für laufende Bewegung ist höchstens der vorher erfasste einzelne Schritt zulässig. Ein unpassierbares Next kann durch native Normalisierung am weiterhin gültigen Current abgefangen werden.

Für die 20 auditierten Wirtschaftsrollen sind ausschließlich die belegten gewöhnlichen Reiseabschnitte freigegeben. Kontaktarbeit, Transport-/Portal-/Tunnelzustände und unbewiesene Sonderflächen werden dadurch nicht freigegeben. Vor eigener Arbeiterversetzung existiert bereits eine reservierte Fortsetzungsquittung. Eine erfolglose Wegsuche darf keine falsche Ankunft oder Warenproduktion erlauben: eigene wartende Fortsetzung verwendet ausschließlich die auditierte Flags 5-Ankunftssperre und erhält Identität, Jobdaten und Cargo. Retry ist begrenzt; danach wartet die Quittung auf eine neue Änderung. Neue native Befehle ersetzen alte Absichten.

Bei neuem fertigem Graben wird für qualifizierte Arbeiter zunächst der echte ursprüngliche Auftrag funktional geprüft. Eine erfolgreiche neue Moat-Route bedeutet keinen Teleport. Bei abgelehntem Probe bleiben lokale Versetzung und erneute Veröffentlichung durch die bereits reservierte Quittung abgesichert. Gewöhnliche Soldaten auf neuem fertigem Moat brauchen noch die entsprechende abgesicherte allgemeine Befehlsfortsetzung; der vorhandene Modusguard ist keine Abnahme dieser Fälle.

Eigene wartende Fortsetzungen und noch nicht abgearbeitete kausale Kandidaten werden als begrenzte binäre Einträge über APIShared`VersionedSaveData` gespeichert. Capture erfolgt unter dem geschützten atomaren Savevertrag und verändert keine Simulation. Unvollständige Recovery-/Reservierungsphasen verhindern den Save vor dem Schreiben. Alte processlokale Welt-/Befehls-/Batchnummern werden beim Laden nicht als aktuelle Identität übernommen; Alter, Versuchsverbrauch und alte lokale Erreichbarkeit bleiben dagegen erhalten. Das repariert keine älteren Saves ohne eigene Vorherdaten.

Eine ausgeschaltete Einstellung startet keine Rettung. Eigene gespeicherte Worker-Quittungen bleiben als Eigentumsnachweis erhalten, bis die Funktion wieder aktiv ist beziehungsweise ein tatsächlicher nativer Folgeauftrag sie ablöst. Noch ungebundene kausale Kandidaten können bei ausgeschalteter, pausiert geladener Karte unverändert weitergespeichert werden; der erste tatsächlich fortschreitende inaktive Simulationstick verwirft diese historische Beweisgrundlage ohne native Änderung. So erwacht nach inaktivem Spielen keine veraltete Ursache.

## Budgets und Grenzen

- Höchstens 512 ausstehende kausale Kandidaten; Lebenszeit 80 nativeTicks, höchstens 4 Evaluierungen pro Kandidat.
- ProTick maximal 32 Queuebesuche und 8 Kandidatenversuche, anschließend maximal 4 Worker-Retry-Versuche.
- Das ist kein Versprechen von 8 nativen Aufrufen: ein Worker-Moatfall kann ProbeMove, Relocate und ResumeMove benötigen. Gegenwärtiger Worstcase höchstens 28 direkte native Einstiegaufrufe proTick, davon höchstens 20 Move-Suchen. Vanilla-Suchlaufzeit wird dadurch nicht begrenzt; echte Laufzeitmessung steht aus.
- Hülle maximal 2048 aktiveTiles und 131072 Arbeitsschritte; Discovery maximal 4096 Randzellen,16384 Belegungslinks und 512 Einheiten. Überlast wird sichtbar abgelehnt und niemals als gerettet gezählt.
- Größter aktuell vermessener Keepfall: benötigte Mindestgrenze 67932 Work,1219 Tiles,7584 eindeutige nativeReads und 3999 einmalige Slotprüfungen für die mögliche Replacement-Closure. Diese Zähler sind keine FPS-Messung.
- Reale Multiplayer-/Repairfreigabe bleibt geschlossen. Gespeicherte Gameplaydaten sind simulation-relevant deklariert; gleiche Dateien allein beweisen noch keine Peer-Abnahme.

## Aktuelle Offline-Nachweise

- `.work/a/rescue-impl/001/stage-persistence-build.log`: tatsächlicher Testmod-Build und Tests erfolgreich. Unter anderem Coordinator 169, Queue 10032, PendingCodec 196, Discovery 24, Current/Next-Selektor 12, WorkerPolicy 3167, Ledger 1844, Supervisor 33, Service 29, Hüllenkern 83, NativeAdapter 566 und WorkerGuard 263 Einzelprüfungen. Spätere Coordinator-Ergänzungen 174 bestehen separat in`general-integrated-tests.log`.
- `.work/a/rescue-impl/001/stage-integrated-boundaries.log`: modbezogene Ownership-, JSON-, Lifecycle-, Abhängigkeits-, XAML- und permanenteHook-Quellgrenzen bestanden.
- `.work/a/rescue-impl/001/api-full-build.log` und API-Kopie`.local/build-proof.json`: vollständiger Entwicklungs-SDK-Build ohne Installation; aktuelle Inputs erneut prüfen.
- `Audits/` sowie kompakte Worker-/Mapper-/Keep-Nachweise in`.work/a/rescue-impl/001`: Native- und SDK-Verträge, ausdrücklich getrennt von tatsächlichem Spielverhalten.

## Noch laufende Offline-Arbeit

1. Final-UnitMove-Beobachtung/guardedReceipt am bestehenden gemeinsamen Besitzer; effektive Argumente und echtes Originalresultat dürfen nicht aus veränderbaren R 3-Postdaten geraten werden.
2. NativeReissue und externe Orderbeobachtung darauf umstellen, dann gewöhnliche Soldaten-Moatfortsetzung mit entsprechender eigener Fortsetzungsabsicherung schließen.
3. Gemeinsame Regressionen für Save vor erstem Recoverytick, Codec-/Welt-/Ordergrenzen, zurückgestellte Resumequittungen, tatsächliche Aufrufbudgets und Szenarioanpassungen abschließen.
4. Aktuelle Quellen abschließend auf Fremdpatch-/Lifecycle-/Standaloneverträge prüfen, Normalbuild und reproduzierbare Übergabe der API-Kopie erstellen.

Spielprüfungen werden ausschließlich in[INGAME-PENDING.md](INGAME-PENDING.md) als offen geführt. Historische positive Spielversuche des früheren Prototyps gelten nicht automatisch für diese neuen Quellen.
