# Besetzte Mauern im Spiel testen

Im Workspace-Root bauen und anschließend bei laufendem Steam und geschlossenem Spiel testen.
Das lokale Einzelspieler-Gefecht verwendet die Karte `test AI.map`.

    dotnet msbuild Testmods/OccupiedWallDemolitionTest/OccupiedWallDemolitionTest.csproj /t:Build /p:Configuration=Release
    & 'Testmods/OccupiedWallDemolitionTest/Run-Tests.ps1'

Das Skript testet zuerst Script Extender und APIShared und wiederholt danach
dieselben Schritte mit dem installierten Fixes-Paket. `-Layout Minimal`
und `-Layout Fixes` führen jeweils ein Profil aus. Der Start erfolgt auf einem
separaten, unsichtbaren Win32-Desktop. Der Treiber stellt Plugins, Einstellungen
und Spielstände nach jedem Lauf wieder her bzw. prüft deren Dateiinventare.
Die Testprotokolle liegen in dem beim Start genannten `.work/a/occupied-wall`-Laufordner.

Der Testmod führt je Zielkachel einen normalen einzelnen Abrissbefehl aus.
`WALL_NATIVE_POST` erfasst die Kachel und Einheit unmittelbar nach der nativen
Ausführung. `WALL_OUTCOME` enthält den anschließenden Einheitenzustand.
`WALL_WORKER` enthält Arbeitsziel, Arbeitsplatz und die beobachtete Warenlieferung.
`WALL_COVERAGE` meldet einen Fall, dessen Voraussetzung im Lauf fehlte.

Die Arbeiter beginnen ihre Arbeitswege im Spiel. Für den Abriss wird die
Mauergeometrie unter der belegten Kachel der laufenden Route vorbereitet. Der
Aufbau verändert den bestehenden Auftrag, die Waren und die Belegung der Einheit
nicht. Die Aussage des Falls gilt für diesen Aufbau.

## Ergebnis vom 21. November 2026

Beide Profile bestanden mit dem finalen Build. Der Treiber bestätigte jeweils
das Spielfenster auf dem Testdesktop, einen unveränderten Eingabedesktop und die
Wiederherstellung der ursprünglichen Installation.

| Fall | Ergebnis mit und ohne Fixes 1.31.0 |
| --- | --- |
| Kontrollmauern | Die besetzte Mauer blieb bei ausgeschalteter Freigabe stehen. Der Abriss der leeren Mauer lief über den normalen Pfad mit Rückerstattung. |
| Stehender Speerträger und freier Zivilist | Die Einheiten blieben unverletzt auf derselben Kachel. Ihre Höhe passte sich im nächsten beobachteten Simulationsabschnitt von 98 auf 8 an. |
| Laufender Speerträger | Der bestehende Befehl führte zum Ziel. Die Mauergeometrie war für den Abriss auf der belegten Kachel vorbereitet worden. |
| Huhn | Der Abriss ließ die Einheit zunächst auf derselben Kachel. Auch das Kontrollhuhn auf der intakten Mauer wurde im anschließenden Bestandsfenster entfernt. |
| Apfelbauer auf dem Hinweg | Der Arbeiter erreichte das ursprüngliche Ziel, erntete und lieferte nachweislich vier Äpfel ab. Gesundheit, Arbeitsplatz und Kachelbelegung blieben konsistent. |
| Apfelbauer mit Ladung | Der Arbeiter behielt beim Abriss alle fünf Äpfel und lieferte sie über fünf bestätigte Bestandszugänge ab. |

Protokolle aus dem Workspace unter
[`Minimal`](../../.work/a/occupied-wall/005/Minimal/cb4b8997f27e/LogOutput.log) und
[`Fixes`](../../.work/a/occupied-wall/005/Fixes/98b7a03e3069/LogOutput.log).
