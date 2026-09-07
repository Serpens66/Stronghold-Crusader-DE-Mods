# UCP `o_fix_ladderclimb`: Befehlsziel nach dem Leitersteigen behalten

## HD-Fehler und UCP-Lösung

Einheiten können nach dem Erklimmen einer Leiter ihr ursprüngliches Ziel verlieren und auf der Mauer stehen bleiben. UCP2 reserviert Zusatzspeicher für Zielinformationen von bis zu 10.000 Einheiten und greift in mehrere Pfade ein: Zielvergabe, normale Bewegung, Verlassen der Leiter und Kletterzustand. Das zeigt, dass ein einfacher Zustandsbyte-Patch nicht ausreicht.

## DE-Relevanz

**Bewertung: möglicher übernommener Altfehler; noch nicht bestätigt.** DE besitzt weiterhin native Ladderman- und Bewegungszustandsmaschinen. Die DE-Option `ImprovedLaddermen` ist nicht derselbe Fix: Ihre nachgewiesenen Verwendungen verändern unter anderem Kosten und Kampfwerte. Daraus darf nicht geschlossen werden, dass das verlorene Marschziel korrigiert ist.

Weder `BugfixesAndQoL` noch `shcde-fixes-main` enthalten derzeit einen gleichwertigen Zielpuffer. Die Ladderman-Erwähnungen in vorhandenen Bewegungsmods betreffen andere Mechaniken.

## Mögliche DE-Implementierung

Ein Port sollte pro Unit-Game-ID ein gespeichertes Ziel samt Gültigkeitsgeneration führen. Script Extender 2.2.0 liefert mit `UnitR3EventHooks.OnUnitMoveHere`, `TribeR3EventHooks.OnTribeIssueOrderMoveHere`, `OnTribeIssueOrderWithTarget`, `OnUnitAIStateChange` und `OnUnitDelete` bereits passende öffentliche Beobachtungs- und Lifecyclepunkte. Sie erlauben, den ursprünglichen Befehl zu erfassen und den Sidecar beim Löschen sicher zu verwerfen; sie beweisen noch nicht, an welchem Ladder-Exit die Wiederanwendung nötig ist.

Wegen der 1-/0-basierten Verträge ist die Grenze strikt: `unitId` bleibt 1-basiert, ein direkter Span-Zugriff verwendet einmalig `unitId - 1`. Neben Unit-ID und Ziel müssen Unittyp, Eigentümer und möglichst eine Spawn-/Global-ID-Generation gespeichert werden, weil native Slots wiederverwendet werden. Der Cache wird bei Kartenstart, Save-Laden und Unit-Delete geleert.

Ein zusätzlicher Hook wäre nur am bestätigten Übergang vom Kletter- zurück in den Bewegungszustand nötig. Dort das gespeicherte Ziel ausschließlich wieder anwenden, wenn derselbe Befehl noch aktuell, das Ziel gültig und kein neuer Spieler-/Tribe-Befehl eingegangen ist. Die allgemeine Zielvergabe muss dank der öffentlichen Order-Events nicht selbst gehookt werden. Vorher muss ein Test bestätigen, ob DE das Ziel tatsächlich verliert oder inzwischen intern bewahrt. Gameplayänderung: `NetworkMode=1`.

## Testfall

Eine gemischte Gruppe über Leitern auf eine Mauer und anschließend zu einem weiter entfernten Punkt befehlen. Einheit für Einheit Zielkoordinaten, Zustand und Ladder-Exit protokollieren. Fälle mit zerstörter Leiter, belegtem Austrittsfeld, Gruppenbefehl und Save/Load getrennt testen.

Siehe auch die übergreifende [Native-Integrationsprüfung](UCP-native-integration-audit.md).

