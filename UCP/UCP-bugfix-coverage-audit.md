# Vollständiger Abdeckungsaudit der UCP-Bugfixes

Stand: 7. September 2026

## Ergebnis

Diese Tabelle vergleicht jeden der 17 expliziten UCP2-`Bugfix`-Einträge, vier weitere bugfixartige Legacy-Optionen und die zwei eigenständigen UCP3-Fixes mit dem real vorhandenen DE-Code. „Abgedeckt“ wird nur verwendet, wenn der Quellcode denselben fachlichen Vertrag erfüllt.

| UCP-Schlüssel | DE-/Modstatus | Quellnachweis oder nächste Aktion |
| --- | --- | --- |
| `o_fix_ladderclimb` | offen, möglicher Altfehler | kein Zielpuffer in vorhandenen Mods; Reproduktion und enger Ladder-Exit-Hook |
| `u_fireballistafix` | offen | keine Autoziel-Erweiterung für Mönche/Tunnelgräber gefunden; Zielklassifikation testen |
| `ai_access` | **abgedeckt: BugfixesAndQoL** | `AIEconomyProtectionHook.cs`, DE-Funktion `c_game_ai_check_inaccessible_building`, optional verbesserte alliierte Tor-/Zugbrückenprüfung |
| `ai_defense` | **abgedeckt: BugfixesAndQoL** | `AiDefensePatrolFix.cs` zählt Rollen 1/4 und füllt die Burgverteidigerquote vor äußeren Patrouillen; Setting standardmäßig aktiv; Integrationstests vorhanden |
| `ai_tethers` | **abgedeckt: shcde-fixes** | `AIMaxOxTethers=10`; `AIStoneToOxenRatio` ergänzt die Regel |
| `ai_buywood` | offen | gleichzeitiges Bauen/Bogenproduktion reproduzieren; Kaufmenge situationsgebunden +2 |
| `ai_towerengines` | nicht als Bugfix empfohlen | optionale Balanceeinstellung; keine identische Mod-Abdeckung |
| `ai_assaultswitch` | offen | Zielwechsel während aktiver Belagerung reproduzieren; mit Improved Attacks koordinieren |
| `ai_rebuild` | **teilweise abgedeckt** | Turmruinen: `AITowerRuinRepairFix`; AIV-Konflikte: `BetterAIOverbuildRulesFix`; Mauern, Sammelpunkte und Steinbruchplattform noch prüfen |
| `ai_fix_laddermen_with_enclosed_keep` | offen | Snake-Testburg und Bool-Entscheidung untersuchen |
| `u_fix_lord_animation_stuck_movement` | offen, niedrige Priorität | Gebäudeangriffe reproduzieren; nur Animations-/Bewegungsabschluss zurücksetzen |
| `u_fix_applefarm_blocking` | offen | Randfelder des Apfelgartens systematisch blockieren und Produktion messen |
| `u_tanner_fix` | offen | konkurrierende Gerber/Kuhreservierung testen; Fehlpfad erneut wählen lassen |
| `o_fix_fletcher_bug` | **in DE integriert** | Advanced Option `ImprovedFletchers`; Script Extender `Is/SetImprovedFletchers` |
| `ai_fix_crusader_archers_pitch` | offen | europäische KI-Bogenschützen und Pechgraben-Zielwahl testen |
| `o_fix_baker_disappear` | offen | Mehl während Anlauf entziehen; nur bestätigten Despawn-Zweig umleiten |
| `o_fix_moat_digging_unit_disappearing` | offen | vorhandene Graben-QoL-Funktionen sind nicht gleichwertig; Zustandsübergang reproduzieren |
| `fix_apple_orchard_build_size` | offen/QoL | tatsächliche DE-Footprint- und Vorschaumaske messen |
| `o_armory_marketplace_weapon_order_fix` | **funktional abgedeckt: BugfixesAndQoL** | `HdMarketViewHook` plus `MarketGoodsOrderDefinition`; frei sortierbar und HD-Reihenfolge wiederherstellbar. Arsenal bleibt separat, daher nur bei sichtbarer Inkonsistenz nacharbeiten |
| `o_fix_rapid_deletion_bug` | offen, wichtiger Exploit-Test | Low-Wall-Refund im Fixes-Mod ist ein anderer Fehler; autoritativen Bulldoze-Pfad testen |
| `o_fix_map_sending` | für DE nicht direkt relevant | alter HD-Puffer-/Namenslängen-Workaround; DE hat neuen Lobby-/Workshop-Pfad |
| `aiv-troops-behaviour` | **Grundfix abgedeckt: BugfixesAndQoL** | `AivDefenderPositionFix.cs` entfernt validiert den Ausschluss der Reihen 9/11/18 und ist standardmäßig aktiv; optionale Hold-/Patrol-/Starttruppenfunktionen bleiben separat offen, siehe [UCP-AI-behavior.md](UCP-AI-behavior.md) |
| `hopfarm-limit-fix` | **abgedeckt: shcde-fixes** | Detour von `c_game_ai_count_active_farms`, standardmäßig aktiv, global/lordabhängig konfigurierbar |

## Konsequenz für die Modplanung

Keine neuen Hooks für `ai_defense`, `ai_access`, `ai_tethers` oder die Hopfenfarm anlegen. Bei `ai_rebuild` nur die drei noch offenen Teilverträge untersuchen. Der Marktreihenfolge-Fix ist als Nutzerfunktion vorhanden; ein Arsenal-Abgleich wäre lediglich eine kleine Ergänzung derselben Funktion, kein eigener nativer UCP-Port.

## Baseline-basierte Integrationsentscheidung für offene Einträge

| UCP-Schlüssel | Erstes DE-Instrument | Korrektur nur bei positivem Test |
| --- | --- | --- |
| `o_fix_ladderclimb` | öffentliche Unit-/Tribe-Order-, State- und Delete-Events | Sidecar-Ziel am Ladder-Exit wiederherstellen; Unit-ID plus Slotgeneration absichern |
| `u_fireballistafix` | Unit-State, Projectile-Spawn und tatsächlicher Damage | engste Auto-Target-Typprüfung um Mönch/Tunnelgräber erweitern |
| `ai_buywood` | Pending-Holzkauf `GamePlayerResources+0x2A74`, KI-Phase und Fletcher-State nur lesen | Vanilla-Kaufrequest situationsgebunden um 2 erhöhen |
| `ai_towerengines` | installierte Turmmaschinen und Rekrutierungsentscheidung zählen | Vergleich mit 3 parametrisieren; kein echtes Unendlich |
| `ai_assaultswitch` | `r_AISiegePlayerIdTarget` `+0x2BD8` und Belagerungszustand | Ziel nur während belegter fortgeschrittener Belagerung binden |
| `ai_rebuild` Rest | Spawn/Delete/Repair/AI-Wall-Events | Mauer, Rally-Gebäude und Quarry-Plattform als getrennte Regeln |
| `ai_fix_laddermen_with_enclosed_keep` | Snake-AIV, `siege_ladder_amount`, eigener Zugang, Planungsresultat | nur falschen eigenen-Keep-Ausschluss ändern |
| `u_fix_lord_animation_stuck_movement` | native State- plus Unity-Visual-Events | visuell lokal oder simulativ synchronisiert – erst nach eindeutiger Trennung |
| `u_fix_applefarm_blocking` | Apple-Pickup/-Dropoff und Unit-State | Koordinatenoffset der Worker-Zielwahl, kein Teleport |
| `u_tanner_fix` | `FUN_18013E0B0` plus State/Kuhreservierung | nur Reservierungs-Fehlpfad erneut wählen/warten lassen |
| `ai_fix_crusader_archers_pitch` | `OnSpawnFire`, Archer-Typ und Eigentümer | nur KI-Pechentzündungsprüfung um europäischen Archer ergänzen |
| `o_fix_baker_disappear` | `FUN_180138850`, Flour-Pickup, State und Unit-Delete | nur belegten Missing-Flour-Despawnzweig umleiten |
| `o_fix_moat_digging_unit_disappearing` | Pitch-Ditch-, State- und Delete-Events | nur belegten Graben-Despawnzustand korrigieren |
| `fix_apple_orchard_build_size` | `OnPlacementValidation` gegen tatsächlichen Bau | autoritative Mappermaske oder – nur bei rein visueller Abweichung – Preview korrigieren |
| `o_fix_rapid_deletion_bug` | Bulldoze/Delete/Refund-Events als Trace | wirksamen Cancel-Vertrag im bestehenden Extender-Detour bevorzugen; Event ignoriert derzeit `SkipOriginalFunction` |

Alle RVAs gelten nur für den in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) dokumentierten Hash. Ein Suchanker ist kein Beleg, dass der HD-Fehler in DE noch auftritt.
