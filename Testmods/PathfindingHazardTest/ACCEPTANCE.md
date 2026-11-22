# Feuer-/Pest-Abnahme mit selbst erzeugter Testwelt

Stand 22.11.2026: Der Testmod erzeugt Testwelt, Soldaten, Feuer und Pest selbst und braucht keine
vorbereiteten Spielstände mehr. Er schaltet die produktive Freigabesperre von Pathfinding nicht frei:
Die Gefahrenoptionen gelten nur in einer privaten Kopie der Missionsoptionen (Lease) während eines Falls.

## Einstieg

`build.bat /nopause /noinstall` kompiliert den Testmod und führt die Offline-Vertragsprüfung aus
(Szenarioauswahl und Reflection-Grenze gegen das kompilierte `Pathfinding.dll` im Workspace; zuerst
Pathfinding bauen). Es installiert nichts und startet kein Spiel.

Ein Lauf über `Shared/Tools/Automation/Invoke-InGameTest.ps1` mit `-ModGuid PathfindingHazardTest_Serp`,
dem Testmod-Paket und `-Scenario hazard-matrix` (alle Fälle in einem Spielstart) oder einem einzelnen
Fallnamen. Konfiguration in `BepInEx/config/PathfindingHazardTest_Serp.cfg`, Abschnitt `[Fixture]`:

| Schlüssel | Bedeutung |
| --- | --- |
| `Map` | Gefechtskarte (Standard `test AI.map`); gesucht wird eine flache, freie Fläche nahe der eigenen Burg. |
| `WaitSeconds` | Endliche Wartefrist der begrenzten Fälle und Vergleichsfrist der unbegrenzten (1..15 Spielsekunden). |

## Ablauf

Gefecht mit einer KI (Friedenszeit 60 min), Speed 250, RandomEvents aus, ExtraFeatures-Pestdauer auf Vanilla (Faktor 1;
sonst treiben die Wolken bis zum wartenden Soldaten, dessen Flucht die Wartefälle abschalten). Gesucht wird eine flache Fläche von 31 × 51
Kacheln: begehbar, frei, eine Wegkomponente, Höhenunterschied höchstens 8. Pathfinding-Einstellungen
werden vor dem Start fest gesetzt. Jeder Fall wartet zuerst, bis kein natives Feuer und keine Pestwolke
mehr existiert, spawnt eigene Soldaten in Einmanngruppen und aktiviert die Optionen. Danach beobachtet er
auf dem echten Simulationstick (Ordnung 700 nach Pathfindings Scheduler 600). Am Ende stellt er Optionen
und Bewegungsfälle wieder her und entfernt die Soldaten.

- Feuer: nativer `0x9C120` über den Extender-Wrapper (Zielkachel kann springen; Lücken werden nachgefüllt).
- Pest: `0x9B2B0` über `GameProjectileManagerAPI.CreateProjectile` mit denselben Argumenten wie Vanillas
  natürliche Pest `0xD17D0` (Baseline `FIRE_EFFECTS.md`, Abschnitt „Effect constructor and natural plague creation“).
  Jeder erzeugte Datensatz muss Typ 22 im von Pathfinding gebundenen Effektmanager haben.
- Wegsperren: Ein unabhängiger BFS über das native Richtungsraster belegt, dass kein gefahrfreier Weg
  innerhalb der Umwegregel existiert. Bei Lücken wird nachgefüllt, höchstens viermal.

| Fall | Nachweis |
| --- | --- |
| `fire-escape` | Speerkämpfer im Feuer: Fluchtfall, sichere Ankunft, Rückkehrbefehl, Ankunft höchstens eine Kachel vom Ausgangspunkt, ursprüngliche Gruppe. KI-Speerkämpfer im selben Feuer: nachweislich auf einer Feuerkachel gesehen, nie ein Flucht- oder Wartefall. |
| `plague-escape` | Wie oben mit acht Wolken auf der Kachel des Soldaten. |
| `fire-wait-bounded`, `plague-wait-bounded` | Gefahr entsteht nach dem Befehl quer über den Weg: Wartefall, Abbruch erst nach der eigenen Frist und bei noch bestehender Gefahr, Stillstand, Ziel nicht mehr aktiv, ursprüngliche Gruppe. |
| `fire-wait-unlimited`, `plague-wait-unlimited` | Wartefall überdauert die endliche Frist ohne Abbruch. Nach Ende der Gefahr wird weitergelaufen, Ankunft am Ziel, ursprüngliche Gruppe. |
| `plague-immune-crossing` | Lord (pestimmun, `0x32BC40`) quert die Pestsperre ohne Wartefall und kommt an, nachweislich auf einer Pestkachel gesehen. Ein Kontroll-Speerkämpfer bleibt vor der Sperre stehen (Wartefall). Der eigene Lord läuft vorher zur Startkachel; hat der Spieler keinen (so auf `test AI.map`), wird einer gespawnt und nie getötet, wenn das Spiel ihn als Spieler-Lord führt. `CHIMP_TYPE_HEALER` ist der zivile Apotheker-Heiler und kein Soldat. |

Ein ungeeignetes Gelände, ein Gefahrenaufbau, der nicht schließt, oder ein Zeitlimit führen zu einem
Fehler und nie zu einem PASS. Es wird nichts gespeichert.

## Grenzen und weiterhin offene Abnahmen

- Der Lauf ist eine Einzelspieler-Abnahme auf flachem Boden. Keine Höhen-, Treppen- oder Garnisonsfälle
  (Q2), kein Save/Load während der Fälle (T6), keine Formationen oder Shift-Queues (T5), kein Gebäudebrand.
- Nicht abgedeckt: Sichtprüfung (T10) und echter Zweirechner-Multiplayer (T9).
- Ein PASS der Fälle beweist nicht, dass der native Gruppenweg der Flucht keine Gefahrenkachel betritt
  (Q4). Es zählt der beobachtete Ausgang.
