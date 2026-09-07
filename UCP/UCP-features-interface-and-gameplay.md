# UCP-Features: Oberfläche, Steuerung und allgemeines Gameplay


## Sinnvolle, noch offene Komfortfunktionen

| Feature | DE-Relevanz und mögliche Umsetzung |
| --- | --- |
| `o_firecooldown` | UCP ersetzt einen `ushort`-Default 2000. Das ist nicht nachweislich `r_OnFireTicks`; `Get/SetOnFireTicks` daher nur zur Diagnose verwenden. Durch Löschen und erneutes Entzünden den zweiten Immunitäts-/Cooldowntimer identifizieren und erst diesen parametrisieren; synchronisiert, Save/Load-Test |
| `o_increase_path_update_tick_rate` | UCP 200→50 Ticks; DE-Schedulerintervall zuerst messen. Den bestätigten globalen Resetwert parametrisieren und CPU/Pathfinding profilieren. Kein eigenes Vollkarten-Polling über `OnTick` |
| `o_change_siege_engine_spawn_position_catapult` | UCP verschiebt bei Katapult, Tribok, Belagerungsturm, Rammbock, Schild und Feuerballista X/Y jeweils um +1. In DE Spawnargumente am gemeinsamen Engine-Spawnpfad ändern, aber Kartenrand, Belegung und AIV testen. `OnAISelectSiegeRallypoint` ist nicht derselbe Vertrag |
| `o_stop_player_keep_rotation` | trotz Namens dreht UCP den menschlichen Bergfried zur Kartenmitte. Orientierung in der verwalteten Platzierung bestimmen und dem einmaligen Vanilla-Baubefehl übergeben; nicht nach dem Spawn drehen |

## Lokale UI-/Eingabefunktionen

| Feature | Bewertung |
| --- | --- |
| `o_keys` | WASD sowie Ctrl+S/Ctrl+L gegen DE-Standardbelegung prüfen. Workspace deckt nur einzelne Markt-/Geschwindigkeitskeys ab. Falls fehlend, über Unity/WPF-Eingabe mit Fokusprüfung implementieren, damit Textfelder nicht reagieren |
| `o_disable_border_scrolling` | sinnvoll im Fenstermodus; Kamera-Edge-Scroll vor der Bewegungsanwendung lokal unterdrücken (`NetworkMode=0`) |
| `o_remove_right_click_context_menu` | lokale UI-Option; nur Kontextmenü unterdrücken, Rechtsklick-Befehle nicht blockieren |


## Integrationsentscheidung

Die offenen Komfortfunktionen sollten nicht in einen gemeinsamen Native-Patchblock gepackt werden. `o_responsivegates` kann auf einem öffentlichen Simulationsevent aufbauen und gehört in ein `NetworkMode=1`-Gameplaymodul. Fast Placement, Engineer Tent, Moat Visibility, Tasten, Randscrollen und Kontextmenü sind verwaltete lokale Funktionen mit `NetworkMode=0`. Feuer, Pfadintervall und Engine-Spawn bleiben erst nach Laufzeitbeleg native, gameplayrelevante Kandidaten. Details und Sicherheitsbedingungen stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).
