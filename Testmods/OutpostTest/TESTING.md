# Aktueller Test: Vanilla-Beobachtung

Ab diesem Build ist ausschließlich VanillaObserver aktiv: keine eigene Produktion, kein Produktions-Gate, keine Adopt-/Gruppenabschluss-Aufrufe. Der bisherige Fünfergruppen-Test ist auf Wunsch deaktiviert; sein Code bleibt für den anschließenden Vergleich erhalten und wird nicht instanziiert. Version bleibt während des Tests 0.1.0.

Spiel neu starten und die Outposts länger produzieren lassen: Vanillas eigene Timer/Editorprofile gelten wieder, nicht der Fünfsekundentakt. Für Macemen-Vergleiche im Editor das entsprechende europäische Truppenprofil wählen. Zusätzlich arabische und beduinische Outposts mit ihren normalen Truppen/Wachen beobachten. Möglichst denselben Spielstand beziehungsweise dieselbe Karte und Geschwindigkeit verwenden; bereits vorhandene Einheiten werden nicht verändert und nicht rückwirkend als neue Spawns erfasst.

Logmarker: `vanilla-session ... customSpawn=False suppression=False`, `vanilla-create`, `vanilla-state`, `vanilla-end`, `vanilla-stop`. Ein Create-Post-Snapshot liegt vor Outposts Nachinitialisierung. Anschließend werden Zustands-, Alive-, Gesundheits-, Rollen- und Gruppenwechsel tickweise erfasst; Position und Gruppengröße zusätzlich spätestens alle 40 Ticks. Pro Kandidat maximal 2400 Ticks oder 400 Zustandszeilen, gleichzeitig maximal 2048 Kandidaten. Limits und Slot-/Global-ID-Wechsel werden ausdrücklich protokolliert. Missionsende enthält die Zählinvariante captured=ended+remaining.

Grenzen: Das vorhandene Create-Event liefert keine Aufrufer-/Outpost-ID. Besitzer und exakte Ausgangs-Weltkoordinaten (Endtile ×8) ergeben zunächst `exit-candidate`; Rekrutierung am gleichen Ort oder überlappende Outposts können daher Kandidaten erzeugen. `guard-identity` bestätigt die gespeicherte Wachenidentität; `production-tribe-identity` bestätigt Unit-Mitgliedschaft in der identitätsgeprüften Produktionsgruppe (auch gegen den beim Create gemerkten Gruppenstand). Bestätigung bezeichnet die Herkunft, nicht unveränderte spätere Mitgliedschaft. Besatzungen und sofort abgeschlossene Gruppen können unbestätigte Kandidaten bleiben. Keine heuristische Zuordnung als gesicherte Herkunft ausgeben.

Tickzeit stammt aus demselben FrameProvider wie OnTick. Mehrere native Übergänge innerhalb eines Ticks werden außerhalb des Create-Post-Zeitpunkts nicht einzeln beobachtet. Zustandszahlen bleiben roh; insbesondere state=114 ist damit noch nicht semantisch erklärt. Gesundheit, Slot-Lebensdauer und vorheriger Zustand sollen genau diese Frage klären. Pause erzeugt keine Tick-Folgelogs. Editor und Multiplayer werden nicht beobachtet; Vanilla bleibt auch bei Diagnosefehlern unverändert.

Build-/Quellprüfung ist kein Live-Nachweis dieser neuen Diagnose. Die folgenden Ergebnisse betreffen den vorherigen eigenen Spawnlauf.

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
