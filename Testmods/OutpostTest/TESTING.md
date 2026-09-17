## Aktueller Test: normales Outpost-HUD und Rallypoint

- Spiel neu starten. Eigene Outposts aller drei Typen anklicken: Vanilla-HUD mit korrekter Fraktion, Größe, Verzögerung und Profilbuttons muss erscheinen. Diese bisherigen Controls sind vorerst auch im Spiel bedienbar; eigenes Macemen-Profil bleibt fest.
- Mittelklick auf die Weltkarte setzt genau ein Ziel; gedrückt halten darf das Ziel nicht laufend verschieben. HUD, Minimap und Menüs ignorieren. Fahne nur am ausgewählten eigenen Outpost.
- Zwischen zwei Outposts und normalen Gebäuden wechseln; keine alte Auswahl übernehmen. Abwahl, Abriss, Besitzerwechsel und Missionswechsel prüfen. Editor und Multiplayer behalten Vanilla.
- Einheit A erhält Ziel A; nach Versetzen auf B darf erst neu erzeugte Einheit B Ziel B erhalten. Hold, Laufmodus, unerreichbares Ziel, Pause, Rotation, Zoom, Höhen und Save/Load prüfen.
- Im Log einmal `selection gate ready`, später `rally-render confirmed`, bei ersten Klicks `rally-input`, danach `rally-set`/`rally-move` erwarten. Keine Gebäude-ID-0-Fehler.
- Leistung: Im ersten Messfenster nach Missionsstart abwechselnd keine Outpost-Auswahl, Outpost ohne Ziel und Outpost mit Fahne jeweils mehrere Sekunden anzeigen. Nach 1860 aktiven Renderframes erscheint einmal `rally-frame-metrics`; je Kategorie sind maximal 300 Stichproben nach 30 Aufwärmbeobachtungen enthalten. Zum Vergleich mit vielen Outposts Spiel neu starten und denselben Ablauf wiederholen. `samples=0` bedeutet nicht gemessen; `allocation counter=unavailable` verlangt einen externen Unity/Mono-Profiler. Bericht enthält Mittelwert, Maximum und insgesamt allokierte Bytes je Kategorie. Keine dauerhafte Messung.
- Automatische Tests belegen Hook-/Policy-Verträge, keine sichtbare Fahne, Pathfinding-Ankunft oder gemessene Spielperformance. Diese Live-Ergebnisse separat dokumentieren.
# Aktueller Test: Rallypoints für menschliche Outposts

Eigene Outposts auswählen und auf der Spielkarte mit der mittleren Maustaste klicken. Jeder Outpost hat sein eigenes Ziel. Die gelbe Fahne erscheint nur bei ausgewähltem Outpost. HUD-/Menüklicks und fremde Gebäude werden ausgeschlossen. Während Pause kann das Ziel geändert werden; die Simulation bleibt angehalten. Spiel nach Installation neu starten.

Neue menschliche Macemen erhalten jeweils eine eigene Ein-Unit-Tribe mit Hold (Stellung halten). Ohne gesetztes Ziel bleiben sie am Ausgang. Mit Ziel wird eine Kopie beim Spawn gespeichert und nach NeedsInit einmal über Vanillas Laufwrapper befohlen. Eine später versetzte Fahne beeinflusst weder ältere Units noch deren wartende Zielkopien. Spielerbefehle/Umgruppierungen vor Ausführung brechen den wartenden Auftrag ab. Spätere Spielerbefehle werden nicht korrigiert. Kein Angriffsbefehl, keine Teleportation und kein periodischer Rallyzwang.

Die bisherigen Spawnintervalle/Zielgrößen gelten weiter. Menschliche Zyklen zählen erfolgreiche Auslieferungen, keine aktuelle Sammelgruppengröße: Tod oder Umgruppierung einer ausgelieferten Unit produziert keinen Ersatz innerhalb desselben Zyklus. KI benutzt unverändert die bisherige Sammelgruppe und native Übergabe. Alte menschliche Produktionsgruppen werden einmal abgeschlossen, ihre vorhandenen Einheiten nicht nachträglich bewegt oder auf Hold gesetzt.

Versionierte Binärdaten werden mit ModSaveDataAPI unter OutpostTest_Serp gespeichert: unabhängige Ziele einschließlich Gebäude-ID/Global-ID/Besitzer, menschlicher Zyklusfortschritt und ausstehende Zielkopien einschließlich Unit-/Tribe-Global-ID. Native Timer bleiben Gebäudefelder. Auch ein leerer Datensatz wird geschrieben. Alte Saves ohne Zusatzdaten beginnen ohne Ziele; bestehende native Zähler bleiben erhalten. Ungültige Objektidentitäten werden beim Laden verworfen, beschädigtes/unerkanntes Format deaktiviert die Testfunktion mit Fehlermeldung und lässt Vanilla wieder zu.

Logmarker: `rally ready`, `rally-set`, `human-cycle`, `human-spawn ... stance=Hold`, `rally-move`, `human-cycle-finish` und `rally-load`. `nativeResult=1` bedeutet die Rückmeldung des direkten Gruppenbefehls, keine garantierte Ankunft aller Pathingfälle. `deferred-or-unobserved` ist bei vorgeschalteter Verarbeitung etwa durch MoatMove möglich; die native Wrapperfunktion selbst liefert keinen Erfolg zurück. Fehlversuche werden nicht permanent wiederholt. Initialisierungswartezeit ist auf 80 Simulationticks begrenzt; Gesundheit/Identität/Owner/Mitgliederzahl werden vor dem Befehl kontrolliert.

Abnahme im Spiel (für diesen Build noch nicht beobachtet):

- Zwei eigene Outposts mit verschiedenen Zielen; Fahne nur beim jeweils ausgewählten Gebäude. Alle drei Gebäudetypen sowie Auswahl fremder/verbündeter Outposts prüfen.
- Unit A spawnt mit Ziel A; Fahne auf B versetzen; erst danach gespawnte Unit B erhält B. Bereits ohne Ziel erzeugte Units bleiben beim späteren ersten Setzen unberührt.
- Alle neuen menschlichen Units zeigen Hold, laufen zum Ziel und bleiben anschließend steuerbar. KI-Aufbau und Haltungen unverändert; alte menschliche Units unverändert.
- Mittelklick auf HUD, Menüs, Minimap, Kartenrand und außerhalb der Karte; Pause; Zoom/Rotation und Geländehöhen. Fahne verschwindet beim Abwählen, Abriss, Besitzerwechsel und Missionsende.
- Save/Load mit mehreren Zielen, leerem Zustand und einem noch nicht ausgeführten Auftrag; zusätzlich ursprüngliche Zielkopie nach zwischenzeitlichem Versetzen prüfen.
- Bewegung/Angriff/Umgruppierung vor Ausführung eines Auftrags, unerreichbares Ziel, Tribe-/Unit-Limit und MoatMove-Zusammenspiel. Keine eigenen nativen Bewegungsdetours installiert.

Automatisiert werden Zustandsmodell, Zielkopien, Identitätswechsel, Abbruch, Bereitschaft und Binär-Roundtrip samt Fehlerdaten sowie die bestehenden Native-/Hook-Verträge geprüft. Das ersetzt keine Beobachtung der Unity-Darstellung oder des Pathfindings.

## Vorheriger Stand: schrittweiser Spawn ohne Rallypoint

# Aktueller Test: schrittweiser eigener Macemen-Spawn

VanillaObserver ist entfernt. OutpostTest unterdrückt wieder Vanillas Produktion einschließlich Wachenersatz und erzeugt selbst ausschließlich Macemen (Typ 26) für alle drei Varianten. Vorhandene Units/Wachen bleiben bestehen. Einzelspieler-Freigabe, APIShared-Lifecycle und native Hookvalidierung bleiben erhalten; Editor/Multiplayer unverändert. Version weiterhin 0.1.0.

Der bisherige Burst aus fünf sofort übergebenen Units ist ersetzt. Frische Outposts warten zunächst 2000 erlaubte Simulationsticks (50 Spielsekunden). Danach entsteht eine offene Gruppe mit Macemen-Profil 2: Zielgröße (10 + Zufallswert 0–9) × (Editor-Größenstufe + 1). Standardstufe 1 ergibt 20–38 Units. Ein Spawnintervall ergibt sich aus max(100, 250 − Beschleunigung) × (100 − 15 × Größenstufe) / 100, ganzzahlig. Bei Standardstufe zunächst 212 Ticks; nach jedem Versuch startet der Zähler bei einem Zufallswert 0–39, entsprechend zunächst 173–212 Ticks Abstand. Die erste Unit wird im Gruppenerzeugungsdurchlauf erzeugt.

Bis zum Abschluss bleibt dieselbe native Tribe verknüpft. Jede erfolgreiche Unit erhält die bisher geprüfte Vanilla-Initialisierung/Zuordnung; nach einem Produktionsschritt erfolgt Vanillas Ausgangsbewegung. Die Gruppe startet Aggressive, ihre Haltung wird danach nicht fortlaufend überschrieben. Nur bei erreichter aktueller Mitgliederzahl und abgelaufener Editor-Verzögerung erfolgt der native Abschluss. Vanilla übernimmt dabei die menschliche/KI-Unterscheidung und spätere Zielwahl. Stirbt eine Unit vorher oder verlässt die Gruppe, zählt sie nicht mehr zur aktuellen Ziel-Mitgliederzahl.

Positive Verzögerung wird tickweise reduziert und hält den letzten Spawn zurück. Für eine leere KI-Gruppe mit positiver Verzögerung entsteht wie im geprüften Vanilla-Zweig anfangs eine halbe Zielgruppe; sonst eine Unit je Produktionsschritt. Nach Abschluss steigt die Beschleunigung zwischen Gruppen um 33 beziehungsweise 100 und innerhalb der Gruppe um 4. Die auditierten Modus-/Weltzustands-Minima gelten. Beschleunigungen werden an der wirksamen Sättigungsgrenze begrenzt, damit lange Läufe keinen short-Überlauf erzeugen.

Zufallsentscheidungen verwenden eine eigene deterministische Mischung aus Gebäude-Global-ID, Simulationstick und Entscheidungskennung. Wertebereiche und Zeit-/Größenformeln entsprechen dem Macemen-Profil, die konkrete Zufallsfolge und Zahl verbrauchter Vanilla-RNG-Werte werden nicht nachgebildet. Profilmasken werden für diesen Macemen-Test ignoriert; Größenstufe und Verzögerung gelten für alle drei Varianten. Keine Wachenproduktion, keine Prozentkonfiguration oder neue UI.

Native Gebäudefelder speichern aktive Tribe-ID/Global-ID, Profil 2, Zielgröße, Zähler, Verzögerung und Beschleunigungen. Nach Save-Laden wird eine gültige Macemen-Profilgruppe mit passendem Besitzer und Rolle 184 fortgesetzt. Fremde angefangene Profile werden beim Übernehmen einmal abgeschlossen. Keine eigene Save-Erweiterung. Die tatsächliche Save/Load-Fortsetzung dieses Builds bleibt live zu testen; frühere Reset-Timer-Angaben beziehen sich nur auf den alten Burst-Test.

Bei Besitzerwechsel wird die noch identitätsgeprüft verknüpfte Gruppe einmal übergeben und abgelöst. Für Abriss berücksichtigt die Mod, dass native Bereinigung dies bei Typ 106/107 bereits erledigt, bei Typ 2 aber nicht; noch bestehende passende Links werden vor späterer nativer Bereinigung gelöscht. Alte Global-IDs werden nie auf neue Slots angewendet. Missionsende führt keine nativen Abschlussaufrufe aus. Bei Vertragsfehlern wird das Gate deaktiviert; die nativen Gruppenfelder bleiben für Vanilla erhalten.

Limits führen zu Warten, nicht zu vorzeitiger Übergabe oder Nachholwellen. Erfolgreiche Teilspawns bleiben in ihrer offenen Gruppe; ein komplett fehlgeschlagener erster Spawn lässt die leere Tribe regulär bereinigen. Das übersprungene Vanilla-Präfix innerhalb des Produktionsblocks (einschließlich Umgebungsscan) bleibt wie beim bisherigen Test unterdrückt.

Logmarker: `mode=incremental-macemen`, `hook confirmed`, `group-start` mit Zielgröße, `spawn` mit Mitgliederstand, `production` und `group-finish ... reason=target-reached`. Keine umfangreichen Vanilla-Zustandslogs mehr. `handoff=called` beweist den Abschlussaufruf, nicht ein bestimmtes KI-Angriffsziel. Vereinzelte Fehlversuche stehen in `production`; länger anhaltende Limits werden gedrosselt protokolliert.

Abnahme: frische Outposts aller Varianten, beide Besitzertypen; nach etwa 50 Spielsekunden erste Unit, dann dieselbe anwachsende Tribe und erst am Ziel Abschluss. Positive Verzögerung mit menschlichen/KI-Besitzern, höhere Größenstufen, Pause/Geschwindigkeit, Verluste während Sammlung, Einheitenlimit, Besitzerwechsel, Abriss und Save/Load prüfen. Keine frühen Fünferangriffe und keine neuen Wachen. Statische/Synthesetests und Build ersetzen diese Spieltests nicht.

## Historisch: erster Burst-Test

# OutpostTest 0.1.0: erster Spieltest

Der installierte Testmod wirkt automatisch in Einzelspielerpartien mit normalen Besitzern 1–8. APIShared liefert Missionsstart/-ende und die zentrale Moduserkennung. Editor, Multiplayer, Tutorial und unbekannte/widersprüchliche Modi bleiben unverändert. Kein neues HUD und keine Einstellungen.

Für jede der drei Outpost-Varianten einen menschlichen und einen KI-Besitzer prüfen. Nach fünf Spielsekunden sollen je fünf Macemen erscheinen; anschließend alle fünf Spielsekunden eine neue Fünfergruppe. Reguläre Vanilla-Truppen und neue Wachen bleiben aus. Bereits vorhandene Truppen bleiben erhalten. Pause stoppt den Takt; höhere Geschwindigkeit beschleunigt ihn. Karteneditor-Maske, Größenstufe und Verzögerung bestimmen diesen Testspawn nicht.

Menschliche Macemen auswählen und bewegen/angreifen lassen. KI-Macemen über mehrere Wellen beobachten: Die Mod ruft Vanillas Gruppenabschluss auf, erfindet aber keinen eigenen Angriffsbefehl. Ein `handoff=called` im Log ist kein Beweis, dass die KI bereits angegriffen hat. Fehlende Gegner oder Vanillas volle Übergabeliste können den weiteren Verlauf beeinflussen.

Danach Abriss, Neubau/Slot-Wiederverwendung, mehrere Outposts, Besitzerwechsel und Einheitenlimit prüfen. Bei einem Limit sind kleinere Wellen oder kein Spawn zulässig; es gibt keine späteren Nachholwellen. Save laden startet einen neuen Fünfsekunden-Countdown. Native Units und Gruppen werden regulär im Save gespeichert; der Mod speichert keinen eigenen Timer.

## Diagnose

`BepInEx/LogOutput.log` enthält Zeitstempel mit Millisekunden und diese Marker:

- `gate installed inactive`: Installation/Layouts validiert, noch keine Missionsfreigabe.
- `session ... active=True`: passende Einzelspielermission; Modusdetails stehen daneben.
- `tracking`: Gebäude-ID/Global-ID, Typ, Besitzer, Ausgang und erster Spawn-Tick.
- `hook confirmed`: Das native Gate wurde in dieser Mission tatsächlich durchlaufen.
- `wave`: fünf angefordert, tatsächliche Anzahl, Tribe-Identität, Mitgliederzahl, Haltung, Rolle und Move-Ergebnis.
- `followup`: Unittyp, AliveState, Tribe und AI-Zustand nach mindestens 1, 40 und 200 Ticks.
- `failed closed`: Vertragsfehler; weitere eigene Spawns aus, Vanilla wieder aktiv.

`moveResult=False` bei frisch erzeugten NeedsInit-Units wird nicht durch erzwungenen Alive-/Bewegungszustand kaschiert. Der Aufruf erfolgt an derselben Stelle der Spawnfolge wie bei Vanilla; der weitere Zustand muss im Spiel geprüft werden.

## Bisherige Validierung

Automatisierte Prüfungen umfassen Timer/Identitäten/Limits, installierte Strukturgrößen und Feldoffsets, Native-Hash/Bytes, Ablehnung von Sprüngen ins Gate-Innere, reale RedBird-Längendekodierung und Ausführung des tatsächlich generierten Gates in synthetischem Speicher. Das prüft auch Originalzweige, Register-/Flags-/Stackerhaltung. Es ist kein Spieltest.

## Logprüfung des ersten Spieltests, 17.09.2026

Quelle: installierte BepInEx/LogOutput.log, letzter Spielstart, Zeilen 2151–5569 bei Auswertung. OutpostTest-Zeitfenster 00:46:41–00:48:33. Native-Hash im Initialisierungslog: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Version 0.1.0; Einzelspieler-CustomGame, save=False. Der Benutzer berichtet, dass es ingame funktional aussah.

- Native Gate mit 16 verdrängten Bytes installiert, tatsächlicher Durchlauf in dieser Mission bestätigt; Missionsende deaktiviert den Test regulär. Keine OutpostTest-Fehler oder Fail-closed-Meldungen.
- Sechs Outposts: 1/6925 (Typ 106, Besitzer 6), 2/6926 (106, 1), 57/6928 (106, 3), 58/6930 (106, 5), 59/6933 (107, 2), 60/6936 (107, 4).
- Je 18 Wellen, zusammen 108 Wellen und 540 Macemen. Erfassung bei Tick 2, erster Spawn bei 202, letzter bei 3602. Sämtliche Intervalle exakt 200 Simulationsticks.
- Jede Welle: requested=5, created=5, members=5, stance=Aggressive, role=184, moveResult=True, handoff=called, reason=complete. Kein Teilspawn oder Slotfehler in diesem Lauf.
- 318 Nachbeobachtungen: 108 bei Alter 1, 108 bei Alter 40 und 102 bei Alter 200. Die letzte Welle erreicht vor Missionsende die dritte Beobachtung nicht. Alle 1590 Unit-Beobachtungen zeigen Typ 26 und AliveState 2, keine retired-Identitäten. AliveState 2 allein beweist keinen bestimmten Kampf- oder Todeszustand.
- Bei Alter 1 und 40 stimmen alle Unit-Gruppen mit ihrer Spawn-Gruppe überein. Bei 530 unterschiedlichen Einheiten verändert sich die protokollierte Position gegenüber Alter 1. Bewegung ist für alle sechs Outposts belegt; Angriffserfolg und menschliche Steuerbarkeit sind durch diese Marker allein nicht bewiesen.
- Offener Folgezustand: Neun unterschiedliche Einheiten zeigen bei Alter 200 tribe=0 und state=114 (Gebäude 59: fünf; 60: zwei; 2: zwei). Die anfängliche Zuweisung war korrekt. Ursache und Bedeutung dieses späteren Übergangs sind aus dem Log nicht nachgewiesen; weder als Spawnfehler noch als harmloser Normalfall abschließend bewertet.
- Separater Startfehler um 00:46:40.397: Bugfixes and QoL, Feature 'ally goods amount modifiers', NullReferenceException in CrusaderDE.MainViewModel..ctor, aufgerufen über AllyGoodsAmountModifierHook.RefreshSetting (Zeile 110). Kein OutpostTest-Stack. Andere Features laufen laut Fehlerbehandlung weiter. Die fehlende OutpostTest-Sprite-Logo-Datei wird vom Extender ausdrücklich als ignorierbare Warnung gemeldet.

Noch nicht belegt: Typ 2, vollständige Zuordnung menschlicher/KI-Besitzer, unabhängige Zählung unterdrückter Vanilla-Spawns/Wachen, Save/Load, Pause, Abriss, Besitzerwechsel, Limits, Editor und Multiplayer. Es wurden für diese Logprüfung weder Runtimecode noch Version verändert.
