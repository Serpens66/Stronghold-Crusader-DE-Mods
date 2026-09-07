# UCP-Features: Oberfläche, Steuerung und allgemeines Gameplay

## Bereits vorhanden oder mit Workspace-Mods erreichbar

| UCP-Feature | DE-/Workspace-Abgleich | Ergebnis |
| --- | --- | --- |
| `o_gamespeed` | `BugfixesAndQoL.MultiplayerGameSpeedRuntime` bietet synchronisierte MP-Geschwindigkeit, Pause, Shift-Schritte und konfigurierbares Maximum; auch SP-Schritte werden erweitert | funktional abgedeckt, Wertebereich über vorhandenes MaxGameSpeed konfigurieren |
| `o_default_multiplayer_speed` | der Workspace-Mod erlaubt Änderungen im Match, besitzt aber keine eigene persistente UCP-gleiche Startgeschwindigkeit | teilweise abgedeckt; falls benötigt, Hostwert beim bestätigten Kartenstart einmal über denselben Runtime-/Chore-Pfad anwenden |
| `o_onlyai` | DE besitzt einen echten Zuschauerzustand (`spectatorMode`) und einen Vanilla-Spectator-GameAction-Pfad; BugfixesAndQoL nutzt ihn auch nach Eliminierung | Kernfunktion in DE vorhanden; für KI-only beim Lobby-Start die vorhandene Zuschaueroption verwenden, keinen UCP-Slot-Hack portieren |
| `o_override_identity_menu` | UCP ergänzt nur Start-Fern- und Nahkämpfer; `StartConditions` kann Startressourcen und viele einzelne Truppentypen festlegen | durch allgemeineren Mod abgedeckt |
| `o_freetrader` | `BuildingCosts` kann `MAPPER_TRADEPOST` auf null setzen | abgedeckt über Preset |

## Sinnvolle, noch offene Komfortfunktionen

| Feature | DE-Relevanz und mögliche Umsetzung |
| --- | --- |
| `o_firecooldown` | UCP ersetzt einen `ushort`-Default 2000. Das ist nicht nachweislich `r_OnFireTicks`; `Get/SetOnFireTicks` daher nur zur Diagnose verwenden. Durch Löschen und erneutes Entzünden den zweiten Immunitäts-/Cooldowntimer identifizieren und erst diesen parametrisieren; synchronisiert, Save/Load-Test |
| `o_fast_placing` | nach bestätigter Platzierung ausschließlich Palette und aktives Bauwerkzeug im verwalteten HUD wieder auswählen. `OnBuildStructure` Post enthält derzeit kein Ergebnis und feuert deshalb allein nicht als Erfolgsbeleg; verwalteten Command-Abschluss oder `OnBuildingSpawn` heranziehen. Den Build-Aufruf nicht wiederholen |
| `o_engineertent` | gleiches UI-Muster, aber nur für Belagerungsgeräte; Spawn-/Crewlogik bleibt vollständig Vanilla |
| `o_moatvisibility` | geplante eigene Graben-Tiles in der verwalteten Vorschau-/Overlay-Schicht dauerhaft anzeigen. Keine Tileflags ändern; dann bleibt die Funktion lokal und simulationsneutral |
| `o_responsivegates` | Script Extender 2.3.0 besitzt mit `BuildingR3EventHooks.OnGatehouseQuery` einen konfliktfreien per-Unit-Override über `ShouldClose`; dadurch lässt sich die Schließdistanz auf 140 begrenzen. UCPs Wiederöffnung 1200→100 braucht separat den noch unbenannten Timer in `GameGatehouseEntry`. Diesen erst per Zustandsdiff kartieren; keinen zweiten Inlinehook installieren |
| `o_increase_path_update_tick_rate` | UCP 200→50 Ticks; DE-Schedulerintervall zuerst messen. Den bestätigten globalen Resetwert parametrisieren und CPU/Pathfinding profilieren. Kein eigenes Vollkarten-Polling über `OnTick` |
| `o_change_siege_engine_spawn_position_catapult` | UCP verschiebt bei Katapult, Tribok, Belagerungsturm, Rammbock, Schild und Feuerballista X/Y jeweils um +1. In DE Spawnargumente am gemeinsamen Engine-Spawnpfad ändern, aber Kartenrand, Belegung und AIV testen. `OnAISelectSiegeRallypoint` ist nicht derselbe Vertrag |
| `o_stop_player_keep_rotation` | trotz Namens dreht UCP den menschlichen Bergfried zur Kartenmitte. Orientierung in der verwalteten Platzierung bestimmen und dem einmaligen Vanilla-Baubefehl übergeben; nicht nach dem Spawn drehen |

## Lokale UI-/Eingabefunktionen

| Feature | Bewertung |
| --- | --- |
| `o_keys` | WASD sowie Ctrl+S/Ctrl+L gegen DE-Standardbelegung prüfen. Workspace deckt nur einzelne Markt-/Geschwindigkeitskeys ab. Falls fehlend, über Unity/WPF-Eingabe mit Fokusprüfung implementieren, damit Textfelder nicht reagieren |
| `o_disable_border_scrolling` | sinnvoll im Fenstermodus; Kamera-Edge-Scroll vor der Bewegungsanwendung lokal unterdrücken (`NetworkMode=0`) |
| `o_remove_right_click_context_menu` | lokale UI-Option; nur Kontextmenü unterdrücken, Rechtsklick-Befehle nicht blockieren |
| `o_playercolor` | DE-Lobby und Rendering verwenden neue Farbdaten. Eine echte Umsetzung muss Lobbyfarbe, Spritefarbe, Minimap, Embleme und Save/Load konsistent halten; hoher Umfang, rein kosmetisch nur bei nachgewiesener Simulationsneutralität |

## Nicht aktive Experimente

`o_seed_modification_possibility_title` ist in der UCP2-Locale und im Quellcode auskommentiert. Es gehört nicht zum aktiven UCP-Featureumfang. Falls reproduzierbare KI-Turniere gewünscht sind, wäre ein DE-eigenes Seed-Feature sinnvoll, aber erst nach Analyse aller Zufallsquellen und Netzwerkverträge.

## Integrationsentscheidung

Die offenen Komfortfunktionen sollten nicht in einen gemeinsamen Native-Patchblock gepackt werden. `o_responsivegates` kann auf einem öffentlichen Simulationsevent aufbauen und gehört in ein `NetworkMode=1`-Gameplaymodul. Fast Placement, Engineer Tent, Moat Visibility, Tasten, Randscrollen und Kontextmenü sind verwaltete lokale Funktionen mit `NetworkMode=0`. Feuer, Pfadintervall und Engine-Spawn bleiben erst nach Laufzeitbeleg native, gameplayrelevante Kandidaten. Details und Sicherheitsbedingungen stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).
