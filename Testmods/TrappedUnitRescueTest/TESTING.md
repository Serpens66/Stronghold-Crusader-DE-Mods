# Einheitenrettung testen

Dieser Testmod ist ein isolierter Versuchsstand. Normale Spielstände werden nicht automatisch repariert. Die experimentelle Einstellung ist standardmäßig ausgeschaltet; automatische Rettung und Fixes-Unterdrückung werden nur in einer ausdrücklich angeforderten Testmission freigeschaltet. Nicht als fertigen Spielfix veröffentlichen.

Die Fixes-Unterdrückung ist zusätzlich auf die eigenen synchronen Testaktionen begrenzt. Sie wird erst innerhalb des bestehenden Simulationsmonitors eingeschaltet und vor dessen Freigabe in `finally` wieder ausgeschaltet, auch bei fehlgeschlagenen Aktionen. Natürliche KI-Updates zwischen diesen Aktionen behalten die vorhandenen Fixes-Lösungen. Das ist eine Testabgrenzung und noch kein automatischer Beobachter für sämtliche Bau- oder Grabenereignisse.

## Bauen und starten

`build.bat /nopause` baut das Paket und führt die spielunabhängigen nativen und Regeltests aus. Es installiert nichts dauerhaft.

In PowerShell im Testmod-Verzeichnis:

    .\Run-Tests.ps1 -Layout Fixes -Scenarios rescue-off,rescue-on

`Minimal` verwendet Script Extender + APIShared, `Fixes` zusätzlich Fixes, `Integrated` zusätzlich BugfixesAndQoL und Pathfinding. Verwendet werden Kopien des aktuell installierten Abhängigkeitsstands. Die Läufe finden nacheinander statt; ein bereits laufendes Spiel wird nicht übernommen.

Für längere Wirtschaftsläufe kann `-Layout Integrated -PeaceTimeMinutes 60` die reguläre Lobby-Friedenszeit anfordern. Diese Prüfung benötigt eine Lobby, die Friedenszeit anbietet; eine fehlende Option wird als Fehler gemeldet. Farm und Kornspeicher müssen während der gesamten Beobachtung dieselbe globale Identität behalten.

Weitere Szenarien: `rescue-group-off/on`, `rescue-aiwall-off/on`, `rescue-moat-off/on`, `rescue-moatworker-off/on`, `rescue-worker-off/on`, `rescue-ox-off/on`, `rescue-siege-off/on` (jeweils die vollständigen Namen wie `rescue-siege-on` übergeben). `-Map` wählt eine vorhandene lokale Gefechtskarte; Standard ist `test AI.map`. Die Vorrichtungen prüfen freie, flache Flächen tatsächlich. Fehlende Voraussetzungen sind Fehler, keine bestandenen Tests.

Der Runner erzeugt verwerfbare Gefechte, sichert Logs und stellt Plugins, Einstellungen und eigene Config wieder her. Er steuert keine globale Maus/Tastatur. Produktive Saves werden nicht geschrieben. Der Testmod bleibt nach dem Lauf nicht installiert; das Paket unter `BepInEx/plugins/TrappedUnitRescueTest_Serp` bleibt zum erneuten Testen erhalten.

## Aussage der Tests

- Zugbrücke: tatsächliche native Brückenplatzierung über kontrolliert platzierten Einheiten; Erstzustand, echte Ausgangssperre und Bewegung nach Rettung werden unabhängig geprüft.
- Gruppen: drei echte Soldaten im selben Verband, getrennte Fälle für blockierten tatsächlichen Anführer und Nicht-Anführer; native Einzelziele und wirkliche Ankunft werden geprüft. Die Off-Gegenprobe misst die freien Mitglieder, ohne eine universelle Gruppenblockade vorauszusetzen.
- KI-Mauerbau: tatsächlicher nativer KI-Baupfad; Off dokumentiert vorhandenes Fixes-Verhalten, On prüft lokale Rettung statt Bergfriedteleport.
- Graben: echter nativer Grabenpfad als Gegenprobe. Ein fertiger Graben darf nicht pauschal als unzulässiger Standort gelten; ein neuer Bewegungsbefehl kann Vanillas Grabenmodus aktivieren.
- Grabenarbeiter: natürlich beschäftigter Apfelbauer mit echtem Graben am Standort. Vergleicht Fixes' tatsächlichen Löschmarker mit dem aktivierten Schutz und anschließendem regulärem Verlassen des Grabens zum bisherigen Arbeitsziel.
- Weitere Szenarien prüfen Arbeiteraufträge/Waren und Gerät/Besatzung. Konkrete `source=`- und Abdeckungsmarker im jeweiligen Log unterscheiden natürliche Abläufe, kontrollierte native Aufrufe und künstliche Zustandsvorrichtungen.
- No-op und Einschluss auf unverändert gültigem Boden dürfen keine Rettung auslösen. Ein erfolgreicher Teleport allein belegt weder Auftragsfortsetzung noch korrekte Arbeit.

`PASS` gilt ausschließlich für die aufgeführten Szenarioassertionen. Abdeckungsmarker weisen ausgelassene Fälle aus; insbesondere sind aktive Sonderzustände, alte Saves und echter Host/Client-Multiplayer nicht durch einen erfolgreichen lokalen Lauf abgenommen. Unbekannte Native-/Fixes-Verträge sperren die Ersatzfunktion, statt fremde Hooks blind zu beeinflussen.

## Prüfstand vom 15.11.2026

Die Hauptfälle für Brücke, KI-Mauerbau, gemeinsamen Verband, Gerät/Besatzung und den vollständigen Apfelzyklus wurden positiv geprüft. Der Verbandstest unterscheidet einen blockierten tatsächlichen Anführer von einem blockierten anderen Mitglied. Das beweist keine universelle Blockade durch jedes einzelne Mitglied.

Grabenarbeiter und Ochsen bleiben zusätzliche, noch nicht abgenommene Szenarien. Sie dürfen fehlschlagen und liefern dazu echte Zustandsnachweise. Insbesondere bedeutet der Schutz vor Fixes' Löschmarker noch keine erfolgreiche Wegplanung oder Warenlieferung. Diese offenen Fälle sind kein freigegebener Gameplay-Fix und kein Grund, normale Spielstände automatisch zu bearbeiten.
