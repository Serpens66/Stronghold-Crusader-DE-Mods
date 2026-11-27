# Noch ausstehende Spielabnahme des allgemeinen Rettungssystems

**Vorrang ab 27.11.2026:** Ziel und Spieltests sind pausiert. Für die Wiederaufnahme gilt ausschließlich der aktive Teil von [RESCUE-PLAN.md](RESCUE-PLAN.md): feststeckende Bauern/Arbeiter entfernen, Soldaten und Spezialisten erhalten. Die nachfolgenden alten Fortsetzungs-, Zweichargen- und Identitätserhaltungsprüfungen für Arbeiter sind historische Restlisten und keine weiter abzuarbeitenden Freigabehürden. Neue Workerprüfungen weisen Entfernung, Belegungs-/Slotbereinigung und bedingte natürliche Neubesetzung nach; Warenverlust und verzögerter Ersatz sind akzeptiert. Soldaten-, Quellen-, Negativ-, Lifecycle- und relevante Lastprüfungen bleiben nach tatsächlicher Betroffenheit erforderlich. Lauf164 nicht unverändert fortsetzen.

Aktualisierung26.11.2026: Der Singleplayer-Auftrag ist inzwischen autorisiert und läuft. Nachweise, tatsächliche Teilabnahmen und neu erkannte Lücken stehen in [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md) und `.work/a/rescue-sp/001/results.json`. Die folgenden Listen sind der vollständige ursprüngliche Abnahmeumfang; eine Zeile gilt nur in dem dort ausdrücklich belegten Umfang als erledigt. Zielintegration jetzt BugfixesAndQoL. Noch keine vollständige Abschlussabnahme oder Produktionsübernahme; Multiplayer bleibt außerhalb dieses Auftrags.

Historischer Offline-Ausgangsstand:

Stand: 25.11.2026. **Keine dieser neuen Abnahmen wurde in diesem Offlineauftrag ausgeführt.** Ein erfolgreicher Managed-/Maschinencode-Fixturelauf ersetzt weder tatsächliche Belegung noch Warenlieferung im Spiel. Historische Versuche des früheren Prototyps bleiben in TESTING.md dokumentiert.

Vor jedem später autorisierten Lauf müssen Quell-/SDK-/Paketidentität, passende APIShared-Plugin-/Patcher-Paarung, Spiel-/Extender-/Fixes-Stand und tatsächlich wirksame Einstellungen protokolliert werden. Die neue Entwicklung braucht das zugehörige APIShared-SDK ab 0.74.0; ein ungeprüftes installiertes gleich benanntes Paket genügt nicht. Dieser Bericht ist keine Installations- oder Teststartfreigabe.

## Gemeinsame Vorbedingungen und Belege

Jeder positive Rettungsfall braucht dieselben Nachweise: echte native Änderung, alte legale Oberfläche/Erreichbarkeit, danach tatsächlicher vollständiger Verlust eines zulässigen Ausgangs, unveränderte Lebensidentität/Gesundheit/Verband, kohärente Current-/Next- und Kachelbelegung sowie ein bestätigter sicherer Zielort. Falls die Einheit einen Auftrag hatte, muss ihre tatsächliche weitere Bewegung beziehungsweise Arbeit geprüft werden. Ein zurückgelieferter Erfolgscode oder ein verschobenes Sprite genügt nicht.

Zu jeder Familie gehören Gegenproben: Funktion aus, No-op/abgelehnter Writer, vorhandener legaler Ausgang, legitimer Sondermodus, fehlender freier Zielort, zwischenzeitlich neuer Befehl und zerstörte beziehungsweise wiederverwendete Objekt-ID. Voraussetzungen selbst herstellen und prüfen; Zufallslage ist kein gültiger Testgrund.

## Bau- und Geländequellen

| Quelle | Offene Spielprüfung |
|---|---|
| Echter KI-AIV-Zugbrückenbau | Aktualisierte Deferred-Tick-Vorrichtung aus/an, No-op, ursprünglicher Bau unsuppressed, Rettung erst nach vollständigem Batch |
| Menschlicher gemeinsamer Bau | Gültige Platzierungen sowie natürliche Ablehnung auf Einheiten; keine künstliche Umgehung als Beleg für die normale Menschen-UI |
| Alle 23 Creator-Dispatchfamilien | Reale betroffene Randfelder, Compound-/Subsidiärteile, Farms und Quarry-Erweiterungen; leere/unbetroffene Hüllen müssen ohne Aktion bleiben |
| AIV-Cleanup und Replacements | Entfernte Compoundteile, Farmfelder ohne BuildingLayer, vollständige Vorherdaten auch vor Cleanup; anschließend echte neue Konstruktion |
| Verschachtelter Erfassungsausfall | Frühe oder späte Teiloperation kann nicht vollständig erfasst werden: gesamter betroffener Batch darf keinen partiellen Rettungsbeleg veröffentlichen; ursprünglicher Bau läuft weiter |
| Menschen-/KI-Mauern | Einzeltiles und Linien, Mirrors, mehrere Einheiten und Gruppen, frühere Fixes-Aktionen erhalten; kein Doppelretten |
| Sieben Editoroperationen | Reale Editor-Neuanlage und Laden, Typ-/Height-/Clear-Brushes, Kartenränder; absichtliches Löschen von Einheiten darf nicht rückgängig gemacht werden |
| Graben Dig/Fill | Tatsächlich terminaler Fortschritt, mehrere Arbeiter und Reservierungen; nichtterminale Fortschritte lösen keine Erfassung/Rettung aus; Caller-AI erst vollständig abgewickelt |
| Terminaler Gebäude-/Mauerabriss | Entfernte Verbundteile, verbliebene legale Dach-/Bodenflächen und erst später eintretende strukturelle Änderungen |
| AIV-Grabenalternative | Niedrige Moatkachel und bauliche 1×1-Alternative auf geeigneter Höhe getrennt; kein aus einem Ergebniscode erfundener Erfolg |

## Bewegung, Verbände und Sonderflächen

- Stehende und bereits begonnene gewöhnliche Schritte getrennt: nur Current betroffen, nur Next betroffen, beide betroffen und genau ein vor der Auswertung abgeschlossener erfasster Schritt. Ein späterer anderer Schritt muss alte Daten entwerten.
- Neu ungültiges Next bei weiterhin legalem Current: Current kann Ziel der nativen Normalisierung sein. Belegung bleibt genau einmal korrekt, keine künstliche freie Nachbarkachel erforderlich.
- Anführer und Nicht-Anführer in echten menschlichen und KI-Verbänden. Freie Mitglieder müssen weiterlaufen; der gerettete Actor behält Verband und aktuellen gültigen Auftrag.
- Assassinen an echten Mauern, Torhäusern und Zugbrücken: legale Kletter-/Abstiegskanten dürfen keine Rettung auslösen. Tunnel, Portal, Transport und Ladder-/Sonderzustände bleiben gesonderte negative Freigabeprüfungen.
- Kein Zielort: Einheit nicht auf belegtes, ungültiges oder zuvor nicht erreichbares Feld setzen. Später wieder freier Zielort und Retry-/Altersgrenzen getrennt prüfen.
- Vor dem ersten Recoverytick erteilter neuer Auftrag, einschließlich gleicher Zielkoordinate, veränderter Pre-Argumente, Veto, Post-Manipulation und fremder geschachtelter Aufträge. Keine alte Absicht wiederherstellen.

## P7: neuer fehlgeschlagener Auftrag bei bereits belegter Blockade

Diese Fälle benötigen echten Native0/1-Ausgang und den vollständigen nativen Aufrufernachlauf; die Offline-Completions sind dafür kein Spielbeleg.

- Echte Bauänderung erfasst einen vorher gewöhnlichen Standort; noch vor dem Recoverytick tritt ein neuer qualifizierter aktiver Auftrag zum aktuellen Requested-Ziel auf und scheitert nativ. Rettung führt ausschließlich den neuen Auftrag fort. Gleiches Ziel mit neuer Aufgabe gesondert prüfen.
- Worker-Reise mit natürlicher Arbeitsplatz-/Lager-/Baumidentität sowie militärischer Auftrag mit echtem Verband; Zerstörung oder Wiederverwendung eines gebundenen Objekts nach Begin muss ablehnen. Schäden bleiben erhalten.
- Standort bleibt original Current oder beendet genau das zuvor angenommene Next. Zweiter Schritt, anderer Nachfolger, neuer AI-/Kontaktzustand, geändertes Ziel oder Verbandswechsel müssen den Nachweis verwerfen.
- Älterer äußerer Aufruf veröffentlicht nach verschachteltem Native0 eine tatsächliche positive Route: keine zusätzliche Rettung. Veto, Redirect, Ausnahme und manipuliertes Legacy-Post-Ergebnis jeweils getrennt testen.
- Spätere Bauänderung verändert die alte Ausweichregion, ohne die Actor-Kachel zu berühren; alte räumliche Belege dürfen nicht weiterwirken. Eine räumlich getrennte Änderung darf einen unabhängigen Fall nicht unnötig verlieren lassen.
- Save mit ausschließlich erhaltener Geometrie: nach Laden keine spontane Aktion. Erst ein neuer tatsächlich qualifizierter Native0-Aufruf darf einen frischen Trigger liefern. Save mit bereits verbundenem frischem Auftrag dagegen genau einmal abarbeiten; Originalalter, Retryverzögerung und verbrauchte Versuche erhalten.
- Save innerhalb eines fremden Begin/Original/Completion und vor erster Taktgrenze atomar ablehnen. Nach erster Taktgrenze darf eine verspätete Null-Completion keinen neuen Trigger erzeugen.
- Viele gleichzeitige Fehlschläge: gemeinsame Kapazität512,32 frische Validierungen sowie gemeinsame8 Recoveryversuche pro Tick nachweisen; wiederholter Callback desselben Ticks darf nichts verdoppeln. Alter80 und Versuchslimit4 durch neue Aufträge nicht zurücksetzen.
- Negative Kontrolle ohne Vorherdaten: gesunde Einheit mit unerreichbarem Ziel, eingeschlossener legaler Hof und defekter Altsave ohne eigene Daten erzeugen durch Native0 keine Teleportberechtigung.

## Untätige Einheiten auf neuem fertigem Graben

Der neue Fluchtauftrag benötigt eigene Spielbelege. Für jede zugelassene Familie den echten AI0-Ausgang und Spearman2 getrennt herstellen; alle fünf mobilen Geräte nur mit vollständiger ursprünglicher Crew. Engineer0, Trebuchet, Dig/Fill, Rekrutierung, besondere Bewegung und fremde laufende Aufgaben sind negative Aufnahmefälle. Ein beliebiges Flags0 oder Current=Requested genügt nicht als Vorbedingung.

- Native1 muss eine wirkliche positive Route veröffentlichen und anschließend physisch am Ziel ankommen. `EscapeInProgress` ist kein abgeschlossener Rettungsbeleg. Pause, Feind-/Patientenkontakt und neue AI-/Gruppenabsicht dürfen nicht durch erneutes Erteilen des Fluchtauftrags übersteuert werden.
- Ausschließlich normales tatsächliches Native0 darf nach erneuter Prüfung zur lokalen Versetzung führen. Veto, Umleitung, Originalausnahme und Postausnahme nach Native0 dürfen keinen Teleport auslösen. Native1 mit Postausnahme darf nur bei weiterhin kohärenter Route beobachtet werden.
- Ein anderer Begin-Observer verändert vor dem Original Pose, Aufgabe, Belegung oder Welt: der ursprüngliche Fluchtaufruf muss abgelehnt werden. Ein fremder verschachtelter Auftrag bleibt wirksam, auch bei identischem Ziel.
- Save nach Veröffentlichung und erneutes Laden: der normale gespeicherte Auftrag läuft ohne Receipt-Replay weiter. Save während des eigenen synchronen Aufrufs wird vor Dateiersetzung gesperrt. Ausschalten, Missionstausch und abgelaufene Beobachtung erzeugen keine Nachholaktion.
- Ein weiteres Bauereignis betrifft dieselbe noch fliehende Einheit: die alte reine Beobachtung darf keine zweite Rettung zählen oder alte Befehle ausführen. Ein nun belegtes Ziel oder ein Richtungsbyte ausschließlich in blockiertes Gelände ist kein sicherer Versetzungsort.

## Wirtschaft und eigene wartende Fortsetzung

Für **alle 20 Rollen** ausschließlich die in WorkerTravelPolicy freigegebenen Reiseabschnitte als positive Fälle testen: Woodcutter, Fletcher, Hunter, Mason, Quarry-Grunt, Ox, Pitch-Digger, Wheat/Hops/Apple/Cattle, Miller, Baker, Brewer, Poleturner, Smith, Armourer, Tanner und beide Miner. Jede Rolle braucht natürliche reale Arbeitsplatz-/Lager-/Vegetationsbeziehungen; manuelles Setzen von AI/Cargo gilt nicht als Beleg natürlicher Arbeitsfortsetzung.

Je freigegebenem Abschnitt Warenaufnahme, Rückweg und Warenabgabe entsprechend der Auditmatrix unterscheiden. Cargo, Rendite, Rest-/Retryzustand, Mitarbeiter-/Gebäude-/Baumidentität sowie tatsächliche spätere Arbeitsleistung prüfen. K-/S-/Kontaktabschnitte und verlinkte Beute-/Kuh-/Transportzustände sind negative Fälle, keine stillschweigend freigegebenen Varianten.

Besonders wichtig:

1. Native funktionale Neuplanung vom neuen Moat wird akzeptiert: keine Versetzung, ursprüngliches Segment bleibt korrekt und wird tatsächlich beendet.
2. Probe abgelehnt, lokale Rettung erfolgreich, Reissue erfolgreich: dieselbe Einheit liefert ursprüngliche Waren korrekt ab.
3. Reissue scheitert nach Relocation: eigene Flags 5-Sperre verhindert falsche Ankunft und Warenproduktion. Späterer zulässiger Retry führt zur tatsächlichen Fortsetzung.
4. Arbeitsplatz/Lager/Baum wird während des Wartens zerstört oder sein Slot wiederverwendet: kein falscher Zielbezug und keine erfundenen Waren. Besonders Apple: denselben nativen Baumindex mit neuer Global-ID bereits zwischen Before-Capture und Recoverytick erzeugen; der alte Vorherbeleg muss die Rettung ablehnen. Nach Save/Laden muss die identische Prüfung greifen.
5. Externer neuer Auftrag im Wartezustand: erfolgreiche neue Route löst die alte Quittung ab; fehlgeschlagener neuer Auftrag darf niemals das alte Ziel wiederherstellen.
6. Fehlender Zielort, volle Quittungskapazität und bewusst verletzte Parknachbedingung: keine ungesicherte native Mutation; unsichere Reservierung darf nicht als erfolgreiche Rettung verschwinden.
7. Bereits vorliegende offene Apfel-Cargo-/Grabenarbeiter-/Ochsen-Grenzen bleiben einzeln offen, bis ein tatsächlicher Arbeits-/Lieferbeleg vorliegt.

## Geschütze, Ingenieure und Fixes-Reiterrettung

Alle bisherigen Geschütz-/Crewfälle mit aktuellem Code wiederholen: ursprüngliches Gerät, ursprüngliche Ingenieure, Anzahl/Global-IDs/Gesundheit, Absitzen und spätere Kachelbelegung. Beschädigte Mehrfachzuordnung ist ein Ablehnungsfall.

Fixes-Reiterrettung aus/an zur eigenen Rettung: vier Reitertypen, drei tatsächliche Bergfriede, vier Campground-Ausrichtungen, stehende Einheiten und begonnene Schritte sowie blockierter Ausgang. CastlePlanner-Rotation muss aus realen Gebäudefeldern überprüft werden, nicht aus Kamera oder ursprünglichem AIV-Argument. Nach eigener erfolgreicher lokaler Rettung darf der nächste Fixes-Tick nicht nochmals versetzen.

Der temporäre separate Fixes-Scannerpatch wird weiterhin mit und ohne Patch gegen die tatsächlich installierte Fixes-Version geprüft. Sobald der Originalautor korrigiert hat, Vertrag erneut kontrollieren und den separaten Workaround entfernen; keine automatische Mutation fremder Einstellungen.

## Save, Editor, Lifecycle und Modus

- Save unmittelbar nach einer echten Änderung vor dem ersten Recoverytick: eigene gespeicherte Vorherdaten restaurieren, frische Welt-/Sitzungsidentität binden und denselben gültigen Fall einmal abarbeiten.
- Save mit Flags 5-Quittungen, mehrfaches Laden und tatsächliche spätere Arbeitsfortsetzung. Kein Retryverbrauch zurücksetzen, keine Arbeitswaren verdoppeln.
- Saveversuch während eigener Reservierung/Mutation: atomarer Save wird vor Dateiersetzung abgelehnt; bestehender Nutzersave bleibt bytegleich erhalten.
- Pausiert geladene Karte bei ausgeschalteter Funktion: pending Daten können unverändert weitergespeichert werden. Erster inaktiver Simulationstick verwirft ungebundene kausale Kandidaten; keine nachträglich wiedererweckte alte Mutation.
- Eigene Worker-Eigentumsquittungen bleiben bei ausgeschalteter Funktion gespeichert; keine automatische Rettung oder Änderung fremder Einstellungen. Erneutes aktives Laden und bereits erteilter nativer Folgeauftrag jeweils prüfen.
- Neue Partie, Save/Restart, EditorCreated/EditorLoaded, Missionsende, native Weltgeneration und fehlgeschlagener Start: keine alte Queue oder Quittung darf eine andere Welt verändern.
- Unbekannte/zu neue/beschädigte Metadaten: keine geratenen Vorherdaten, keine unbemerkte alte Auftragswiederholung.
- Echte Host-/Client-/Save-/Repairabnahme ist weiterhin offen; bis dahin keine automatische Multiplayerfreigabe. Jeder Peer muss deterministisch dieselben zulässigen Entscheidungen und gespeicherten Gameplaydaten haben.

## Last und Kompatibilität

Leerer Normalbetrieb, viele laufende Einheiten ohne Änderungen, dichte Baustelle, viele tatsächliche Blockaden und voller Retrybestand getrennt messen. Bis zu 10000 Einheiten und große 800×800-Karten sind ein Lastszenario, kein Grund zum dauerhaften Vollscan. Zähler für Erfassungsarbeit, Queuebesuche, direkte Nativecalls, Speicher und tatsächliche Simulations-/Renderzeiten getrennt aufnehmen.

Prüfen: normales Minimalprofil, Fixes, Pathfinding/BugfixesAndQoL, CastlePlanner und vollständiges freigegebenes Modprofil. Fremdpatch an eigener Spanne vor Veröffentlichung und bei Missions-/Editorstart muss mit Error zur logischen Ablehnung führen, ohne Um-/Zurückpatchen. Dormante Quellen müssen Originalverhalten erhalten.

Jedes Ergebnis benötigt konkrete Build-/Runidentität und vollständige Vorbedingungen. `not-executed`, `unsupported` und `overload` bleiben solche Ergebnisse und werden nicht als PASS zusammengezählt.
