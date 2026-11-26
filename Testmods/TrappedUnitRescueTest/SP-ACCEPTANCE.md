# Singleplayer-Abnahme und Abschluss der Einheitenrettung

Auftrag vom 26.11.2026. Aktuelle Zielintegration: **BugfixesAndQoL**. Die frühere Planung einer Übernahme nach Pathfinding ist für die Zielintegration überholt; Pathfinding bleibt ein ausdrücklich zu prüfender Kompatibilitätspartner. Historische Belege und native Audits bleiben gültig innerhalb ihres jeweils dokumentierten Umfangs.

## Startfreigabe und Schutz der laufenden Arbeit

Vor Installation oder Spielstart muss der Chat `01a214b3-87f7-7f13-8b01-973c9fe82412` (Mehrfachplatzierung erweitern) seine Aufgabe tatsächlich abgeschlossen haben. Ein Kommentar über einen bestandenen Build genügt dafür nicht. Den aktuellen Chatstatus vor dem ersten Tausch prüfen und den Beobachtungsnachweis in `.work/a/rescue-sp/001` speichern. Bis dahin ausschließlich Quellvorbereitung und Offlineprüfungen; fremde Änderungen erhalten.

Nur lokaler Singleplayer. Kein Clientzugriff und keine Multiplayerfreigabe. Keine globalen Maus-/Tastatureingaben; Start auf dem geprüften separaten Win32-Desktop ohne Desktopwechsel. Neue Testobjekte in eigens gestarteten Partien erzeugen; Nutzerspielstände nur als geschützte Kopie verwenden, falls ein konkret benannter Fall sie benötigt.

## Festgelegte Testwelt

- Basiskarte mit nachgewiesener großer freier Fläche; zunächst `4spielerkarte.map` als Kandidat. Eignung anhand echter Kartengröße, spielbarer Grenzen, Belegung, Höhen und Erreichbarkeit im Spiel prüfen. Keine Eignung allein aus Dateiname oder Dateigröße ableiten.
- Ein menschlicher Besitzer und ein festgelegter KI-Lord; Startplätze explizit festlegen. Keine vorgebaute KI-Burg. Tatsächliche Burgpositionen, AIV/AIC-Auswahl und Lebensidentitäten protokollieren.
- Friedenszeit60 Minuten bereits beim Start setzen und den nativen aktiven Zustand prüfen. Der versteckte Gefechtsregler darf die Friedenszeit nicht wieder auf0 setzen.
- RandomEvents aus dem Testlayout entfernen; falls für ein späteres Kompatibilitätsprofil geladen, explizit ausschalten und zurücklesen. Moddefaults und alle beeinflussten Einstellungen vor jeder Partie setzen. CastlePlanner-Burgspawn ausschalten, seine sonstige Integration gezielt prüfen.
- Fälle selbst erzeugen: echte Einheiten, Gebäude, Aufträge und Bau-/Grabenereignisse an geprüften Positionen. Erforderliche Reisephasen anhand des realen Zustands abwarten, statt AI-/Cargo-Werte als Arbeitsbeleg zu erfinden.
- Simulationszustände und native Ticks als Wartebedingungen; Echtzeit nur als obere Deadline. Hohe Geschwindigkeit250 für passende Fälle, Geschwindigkeit variieren, wenn das Bewegungsfenster oder eine Leistungsfrage es erfordert.

## Abnahmeblöcke und erforderliche Belege

Alle Zeilen starten mit **vorbereitet / noch nicht ausgeführt**. Ein historischer direkter Pilotlauf ist kein PASS des neuen automatischen Coordinators.

| Block | Positive Fälle und Gegenproben | Erforderliches Ergebnis |
|---|---|---|
| A — Vorrichtung und Minimalprofil | Richtige SDK-Plugin/Patcher-Paarung, echte Session und fortlaufende Ticks, feste Positionen, Friedenszeit, RandomEvents aus, keine fremden Hooks an derselben Stelle | Start- und Vorbedingungen tatsächlich bestätigt; Einstellungen/Plugins/Saves nach der Serie bytegleich wiederhergestellt |
| B — Automatische Bauquellen | AIV-Brücke aus/an, weitere native Gebäude-/Mauerpfade, Cleanup/Compound/Farm, mehrere Betroffene; No-op und abgelehnter Bau | Originalbau läuft, Erfassung vor Original, keine Rettung im Childcallback, spätere echte Tickrettung ohne manuelles BeginMutation |
| C — Menschen, KI und Gruppen | Anführer und Mitglied, gültiger bestehender Auftrag, neuerer Auftrag, gemischt betroffene Gruppe | Originale Lebensidentität/Gesundheit/Verband, freie Gruppenmitglieder bewegen sich weiter, keine Wiederherstellung alter Befehle |
| D — Current und Next | Stehend, echter begonnener Schritt, nur Next betroffen, Current betroffen, Mittelpunkt-/Schrittabschluss, unbelegter zweiter Schritt | Genau einmalige natürliche Belegung, zulässiger sicherer Zielort, keine zweite Versetzung oder Wiederaufnahme ungültiger Schrittteile |
| E — Graben und untätige Flucht | Echter terminaler Dig/Fill, nichtterminaler Fortschritt als Gegenprobe; Native1-Flucht und Native0-Ersatz, kein Zielort | Kein unnötiger Teleport bei gültiger Route, tatsächliche Ankunft und keine falsche Arbeit/Ankunft durch Wegende |
| F — Arbeiter | Alle freigegebenen Rollen/Reisephasen anhand natürlicher Arbeitsbeziehungen; Apple mit/ohne Ladung, natürlicher beladener Ochse, Bindungszerstörung/-wiederverwendung | Ware, Aufgabe, Arbeitsplatz/Lager/Baum und vollständiger nachfolgender Arbeits-/Lieferzyklus erhalten; ausgeschlossene Kontaktphasen bleiben unverändert |
| G — Geschütze und Reiter | Gerät mit Originalcrew, Absitzen und weitere Bewegung; vier Reitertypen × drei Bergfriede × vier reale Rotationen, stehende/begonnene Schritte, blockierter Ausgang | Gerät und Ingenieure unverändert erhalten, korrekte Belegung, Fixes-Rettung und CastlePlanner-Rotation kompatibel, keine zweite Rettung |
| H — Frischer Auftrag/P7 | Beobachtete Blockade, neuer tatsächlicher Native0-Aufruf mit frischer Absicht; verschachteltes äußeres Native1; gleiche Ziele mit neuer Aufgabe; keine Vorherdaten als Gegenprobe | Nur aktuelle Absicht ausführen; keine Rettung allein aus Native0; unbelegter Schritt, neue Bindung oder erfolgreicher neuer Weg entwerten den Fall |
| I — Alte Ausweichbelege | Spätere Änderung im alten Ausweichgebiet ohne Actor auf der Hülle; räumlich getrennte Änderung; früher/später Captureausfall im verschachtelten Batch | Betroffene alte Nachweise verworfen, unabhängige erhalten, niemals partielle Compoundbelege |
| J — Save und Lifecycle | Pending vor erstem Tick, reine Geometrie, verbundener P7-Auftrag, eigene Flags5-Quittung; wiederholtes Laden, Ende/Neustart, aus/an nur außerhalb der Partie | Atomare Speicherung, Alter/Versuche bleiben erhalten, keine doppelte Aktion oder fremde Welt; tatsächliche Arbeit/Bewegung nach Laden |
| K — Lokaler Editor | Neu/Laden, reale Brush-/Bauereignisse, erlaubte gewöhnliche Zustände und negative Sonderbewegung | Eigener dokumentierter Editorpfad; keine ungeprüfte Verwendung der auf Spiele beschränkten InSimulation-/Pause-API |
| L — Grenzen und Fehler | Volle Kapazität, beschädigte/fehlende Daten, kein Ziel, unbekannter Zustand, Legal-Exit, unerreichbares Ziel auf legalem Boden | Begrenzte sichtbare Ablehnung, keine Waren/AI-Heilung, kein Freiteleport über echte Hindernisse |
| M — Leistung | Leerlauf, wenige lokale Änderungen, viele gleichzeitige Fälle, hoher Einheitenbestand und große Karte; A/B-Fenster mit gleicher Welt | Zusätzliche Takt-/Allokations-/Aufrufkosten messen; keine dauerhafte Vollprüfung aller Einheiten oder Kacheln |
| N — Modkombinationen | Pflichtabhängigkeiten, Fixes, Pathfinding/BugfixesAndQoL, CastlePlanner und gegebenenfalls vollständiges Profil; temporärer Fixes-Workaround getrennt | Gleiche eindeutige Hookbesitzer, keine neuen Error/Fatal, funktionale Ergebnisse erhalten |
| O — Übernahme und Entfernung | Erfolgreichen Kern nach BugfixesAndQoL übernehmen, eigene Modsetting/Saveverträge passend anbinden, denselben SP-Kern gegen Hauptmod nachprüfen | Genau ein aktiver Rettungsbesitzer; ursprünglichen Rettungs-Testmod nach bestandener Übernahme entfernen, relevante Tests/Audits/Nachweise erhalten |

Für jede positive Aktion: Original-ID/Global-ID, Typ/Besitzer, Gesundheit, echter Verband, Current/Next/Kachelketten, ursprünglicher und aktueller Auftrag sowie tatsächlich erreichte Bewegung beziehungsweise reale Warenabgabe erfassen. Ein success-Flag oder verschobenes Sprite genügt nicht. Negative Fälle müssen ihren Auslöser und alle Vorbedingungen ebenso belegen.

## Ausführung und Nachverfolgung

Zuerst den echten AIV-Brücken-Coordinator als Smoke aus/an ausführen. Erst danach zusätzliche automatische Quellen und Familien erweitern. Mehrere klar getrennte Partien möglichst in einem Spielprozess; eindeutige Schritt-IDs je Fall. Pluginlayout und SDK-Paar nur einmal pro zusammengehöriger Serie sichern/tauschen und am Ende vollständig wiederherstellen. Bei Fehlern Vorrichtung und Modfehler unterscheiden, konkrete Ursache belegen, Fix samt Gegenprobe prüfen und betroffene Fälle erneut ausführen.

Aktive Quellen: `Testmods/TrappedUnitRescueTest`. Getrenntes Entwicklungs-SDK: `.work/a/rescue-api/001`, gesicherter Stand3eee484; vor jeder Installation aktuellen Buildproof und vollständige Paarung prüfen. Der kanonische APIShared-Checkout bleibt bis zur gesondert geprüften Integration unberührt. Neue allgemeine Hooks nur bei belegter Lücke; vorhandene WorldMutation-/UnitMove-Broker und Simulationstakt nutzen.

Aktuelle Vorbereitung und spätere Laufnachweise: `.work/a/rescue-sp/001`. Der Bericht INGAME-PENDING.md bleibt die detaillierte Fallliste, bis tatsächliche Ergebnisse seine offenen Punkte ersetzen. Multiplayerpunkte werden als außerhalb dieses Auftrags ausgewiesen, nicht als bestanden.
