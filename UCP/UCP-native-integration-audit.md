# Native Integrationsprüfung der offenen UCP-Patches

Stand: 7. September 2026

## Verbindliche Analysebasis

Alle nachstehenden Native-Aussagen beziehen sich auf die installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Der Hash stimmt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` überein. Verwendet wurde die semantische Baseline `sem/FBCB9319`, gebunden an Script Extender 2.2.0, Commit `10d28f717d38166e5875c666f20fc5653ae44b0c`.

Eine Feldbezeichnung oder ein ähnlicher Kontrollfluss ist noch kein Laufzeitbeweis. Deshalb bedeuten die Einstufungen:

- **öffentlich:** Script Extender 2.2.0 bietet einen passenden, bereits typisierten API-/Eventvertrag;
- **baseline-bestätigt:** Funktion oder Datenfluss ist in der aktuellen Baseline semantisch belegt;
- **starker Kandidat:** Struktur, Defaults und Serialisierung passen, die konkrete Spielformel braucht aber noch einen kontrollierten Laufzeittest;
- **nur Suchanker:** Funktion existiert, doch der UCP-Fehler selbst ist in DE noch nicht reproduziert.

Alte UCP-AOBs, Adressen und x86-Bytes sind ausschließlich Verhaltensreferenzen. Sie dürfen niemals in DE angewandt werden.

## Wichtigste Korrektur: DE hat zusätzliche Angriffs-AIC-Felder

DEs `InternalAIC` enthält gegenüber HD drei unmittelbar einschlägige Felder:

| Feld | Offset | DE-Default | UCP-Bezug |
| --- | ---: | ---: | --- |
| `siege_max_troops` | `0x454` | 200 | `ai_attacklimit` |
| `siege_normal_wave_multiplier` | `0x458` | 5 | normaler absoluter Anteil von `ai_addattack` |
| `siege_high_gold_wave_multiplier` | `0x45C` | 7 | UCPs stärkerer Zuwachs bei mindestens 10.000 Gold |

Die Zuordnung ist ein **starker Kandidat**, weil vier unabhängige Befunde zusammenpassen: Feldnamen, Offsets im Extender, Defaults 200/5/7 in der nativen AIC-Initialisierung und die Übertragung derselben Felder durch DEs verwalteten `CustomisationFileManager`/`EngineInterface`. `FUN_18000B520` kopiert zudem den vollständigen AIC-Datensatz einschließlich `0x454` bis `0x45C` in die aktive Struktur. Noch offen ist nur ein Laufzeittest, der die exakte Formel und den Goldschwellwert beobachtet.

Die Werte sind außerdem nicht nur strukturell vorhanden: exportierte DE-Lorddaten enthalten lordabhängige Kombinationen, beispielsweise 120/3/7 beim Surgeon, 300/4/12 bei Bullseye und 500/5/20 bei Baibars (`siege_max_troops`/normaler/hoher Wellenzuwachs). Das belegt, dass DE diese Felder als veränderliche AIC-Konfiguration führt. Ob jede Kombination im aktuellen nativen Angriffsplan exakt wie benannt verrechnet wird, bleibt Gegenstand des kontrollierten Laufzeittests.

**Sinnvollste Integration:** Zuerst keine Berechnungsroutine hooken. Beim bestätigten Kartenstart die den aktiven KI-Spielern tatsächlich zugeordneten, bereits vollständig geladenen AICs über `GameAIManagerAPI.GetAICArray()` lesen, nur die gewählten Werte ändern und das unveränderte restliche `InternalAIC` zurückschreiben. So wird bei Custom Lords von deren geladenem `lordjson`-Wert ausgegangen. Für einen vollständigen Datensatz kann der öffentliche `SetAICFromBytes`-Pfad verwendet werden, jedoch nur mit exakt validiertem AIC-Index und einem Byte-Span in exakt `sizeof(InternalAIC)`; Teilpuffer oder direkt aus Einstellungen gebildete Rohbytes sind abzulehnen. `AIR3EventHooks.OnAIProcessCustomLord` ist hierfür kein belegter Schreibpunkt: Der Extender löst es erst nach dem verwalteten `ProcessExtendedLordFile` aus; es kann höchstens als Discovery-Signal dienen. Änderungen müssen nach dem Laden der Custom-Lord-Daten, aber vor der ersten KI-Angriffsplanung gelten. Offset und `sizeof(InternalAIC)` sind statisch mit `Marshal.OffsetOf`/`sizeof` abzusichern.

Für UCPs **prozentuale** Variante kann bereits ohne Formel-Hook pro Lord aus `siege_trigger_level` die absolute Schrittweite berechnet werden, etwa `round(siege_trigger_level * Prozent / 100)`, und in `siege_normal_wave_multiplier` geschrieben werden. Der hohe-Gold-Wert muss ausdrücklich definiert werden, beispielsweise derselbe relative Schritt oder die Vanilla-Relation 7:5. Das bildet prozentuale lordabhängige Skalierung mit ganzzahliger Schrittweite ab. Nur wenn der Laufzeittest zeigt, dass DE diese Felder anders verwendet oder eine andere Rundung pro Welle verlangt, ist ein enger Hook am Schreibpunkt von `r_AITargetAttackForceSize` (`GamePlayerResources+0x38A0`) gerechtfertigt. Periodisches Überschreiben des Zustandsfeldes ist nicht geeignet.

## Integrationsmatrix: KI-Features

| UCP-Patch | Bester DE-Eingriffspunkt | Evidenz | Ergebnis |
| --- | --- | --- | --- |
| `ai_addattack` absolut | AIC `siege_normal_wave_multiplier`/`siege_high_gold_wave_multiplier` | starker Kandidat; Defaults und Serialisierung bestätigt | AIC-Override vor Native-Hook testen |
| `ai_addattack_alt` relativ | pro Lord Schrittweite aus `siege_trigger_level` berechnen und obige AIC-Felder setzen | starker Kandidat | wahrscheinlich vollständig ohne Code-Hook möglich; Rundung/Goldmodus testen |
| `ai_attacklimit` | AIC `siege_max_troops` | starker Kandidat, Default exakt 200 | AIC-Override ist klar bevorzugt |
| `ai_attacktarget` | AIC `who_to_pick_on` (`0x2F0`) | öffentlich serialisiert; Werte 0/1/2/4 kommen in DE-Lords vor, Bedeutungen noch nicht vollständig belegt | Werte per Vier-Spieler-Test kartieren; erst danach global/pro Lord setzen |
| `ai_recruitinterval` | AIC `troop_production_rate1..3` (`0x168..0x170`) | öffentlich serialisiert, entspricht UCPs gelesenen drei Lordwerten | auf 1/1/1 setzen statt Countdown-Hook |
| `ai_recruitstate_initialtimer` | noch kein typisiertes AIC-Feld | UCP ersetzt `6 * 800` Ticks; DE-Vertrag offen | Startzustand instrumentieren; enger nativer Timer-Initialisierungshook nur nach Reproduktion |
| `ai_resources_rebuy` | KI-Marktroutine; `GamePlayerResources` nur zur Diagnose | Kaufphase und Pending-/Suppress-Felder sind typisiert, ihre Namen allein beweisen den Schreibvertrag nicht | zunächst Werte beobachten; anschließend bevorzugt Request-/Marktroutine hooken, nicht blind Felder setzen |
| `ai_housing` | `AIR3EventHooks.OnAIQueryBuildHovelEventArgs` | öffentlich und bereits von `shcde-fixes` genutzt | abgedeckt; keine zweite Policy |
| `ai_nosleep` | bestehender `AIEconomyProtectionHook` | Baseline-Claim `c_game_building_sync_sleep_state`, RVA `0xC7D50`, semantisch `probable` | abgedeckt |
| `ai_demolish` Wirtschaft/Häuser | bestehender Economy-Hook und Hovel-Detour | `c_game_ai_delete_hovel`, RVA `0x3B1D0`, Semantik/ABI bestätigt | abgedeckt; Fortifikationen separat prüfen |

`PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder` ist kein Ersatz für `ai_attacktarget`: Das Event bewertet Hilfs-Angriffsbefehle zwischen Spielern und ist nicht als endgültige Hauptbelagerungs-Zielwahl belegt. Es eignet sich zur Instrumentierung dieses Teilpfads, aber nicht zum ungeprüften Umschreiben der Hauptziel-Policy.

## Integrationsmatrix: Gameplay und Oberfläche

| UCP-Patch | Bester DE-Eingriffspunkt | Konsequenz |
| --- | --- | --- |
| `o_responsivegates` | `BuildingR3EventHooks.OnGatehouseQuery` plus `GameBuildingManagerAPI.GetGatehouseArray()` | Der per Unit ermittelte Schließentscheid kann öffentlich über `ShouldClose` überschrieben und so auf 140 begrenzt werden. Der Wiederöffnungstimer ist im Gatehouse-Struct noch unbenannt; ihn zuerst kartieren und dann separat parametrisieren. Kein zweiter Hook über den Extender-Inlinehook. |
| `o_firecooldown` | nativer Building-Entzündungs-/Löschtimer; `Get/SetOnFireTicks` nur Diagnose | UCPs 2000 ist ein Immunitäts-/Cooldownwert und nicht automatisch `r_OnFireTicks`. Den betreffenden zweiten Timer per Feuer löschen/neu entzünden identifizieren; danach eng parametrisieren. |
| `o_increase_path_update_tick_rate` | globaler nativer Pfad-Neuberechnungs-Scheduler | `GameTimeManagerAPI.OnTick` darf ihn nicht durch eigenes Vollkarten-Polling nachbauen. Erst Intervall und CPU-Kosten messen, dann den Scheduler-Resetwert ändern. |
| `o_fast_placing`/`o_engineertent` | verwalteter HUD-Bauauswahlzustand nach bestätigter Platzierung | Bauauftrag bleibt Vanilla. `OnBuildStructure` Post feuert auch ohne übergebenes Ergebnis und ist allein kein Erfolgsbeleg; deshalb verwalteten Command-Abschluss oder passenden `OnBuildingSpawn`-Nachweis verwenden. Nur Palette/aktives Werkzeug erneut auswählen. |
| `o_moatvisibility` | verwaltete Vorschau-/Overlaydarstellung | reine Anzeige lokal möglich; keine Tile- oder Simulationsflags dauerhaft verändern. |
| `o_change_siege_engine_spawn_position_catapult` | Spawnargumente der sechs Engine-Typen oder ein künftiges Spawn-Event | UCP verschiebt X und Y jeweils um +1; DE braucht Kollisions-/Kartenrandprüfung. Das vorhandene `AISelectSiegeRallypoint` betrifft KI-Rallypunkte und ist nicht gleichwertig. |
| `o_stop_player_keep_rotation` | verwaltete Keep-Platzierung vor dem autoritativen Build-Befehl | bevorzugte Orientierung zur Kartenmitte berechnen und als vorhandenes Rotationsargument übergeben; nicht nachträglich das Gebäude drehen. |
| `o_keys`, Randscrollen, Rechtsklickmenü | `InputR3EventHooks` beziehungsweise verwaltete Kamera-/ContextMenu-Schicht | lokal, Fokus- und Textfeldeingabe respektieren, `NetworkMode=0`. |
| `o_playercolor` | Lobbydaten plus alle visuellen Konsumenten | kein kleiner Patch: Lobby, Chimp-/Gebäudesprites, Minimap, Embleme, Save/Load und Custom-Lord-Farbe gemeinsam behandeln. Erst nach Nachweis reiner Kosmetik `NetworkMode=0`. |
| `o_xtreme` | XAML-Sichtbarkeit und Command-Ausführung | Leiste, Klicks und Netzwerkaktion getrennt deaktivieren; Konfliktbesitz mit `ExtremePowers` festlegen. |

## Integrationsmatrix: offene Bugfixes

| Fix | Baseline-/API-Anker | Sinnvoller Integrationsweg |
| --- | --- | --- |
| Fletcher | `FUN_18012D230`, RVA `0x12D230`; `ImprovedFletchers` wird im Pfad ausgewertet | offizielle Option über `SetImprovedFletchers`; kein Doppelpatch |
| Gerber | `FUN_18013E0B0`, RVA `0x13E0B0`, nur Kandidatenname | `OnUnitAIStateChange` zur Reproduktion; enger Hook ausschließlich am belegten Reservierungs-Fehlpfad |
| Bäcker | `FUN_180138850`, RVA `0x138850`, Versionsmatch bestätigt | Worker-Events und State-Event zur Diagnose; nur den belegten Missing-Flour-Despawnzweig umleiten |
| Leiterziel | öffentliche Unit-/Tribe-Order-Events plus Unit-Delete/State-Events | Sidecar nach 1-basierter Unit-ID und Generation; Wiederanwendung nur am bestätigten Ladder-Exit. Kein Cache allein nach Slotindex. |
| Feuerballista/Pitch-Bogenschütze | Projectile-/Unit-Events bieten Beobachtung, aber keinen vollständigen Auto-Target-Filter | erst Auto- gegen manuellen Angriff differenzieren; dann Typprüfung im nativen Filter eng erweitern |
| Apfelfarm/Orchard-Footprint | Build-Validation-/BuildStructure-Events und Worker-State-Events | Vorschau und autoritative Mappermaske getrennt messen; bei Simulationsabweichung Mapperdaten, nicht nur UI ändern |
| Moat-Despawn | Pitch-Ditch-Build/Remove und Unit-State/Delete-Events | verschwundene Unit-ID samt letztem Zustand belegen; nur diesen Übergang korrigieren |
| Rapid Deletion | `OnBuildingBulldoze`/`OnBuildingDelete` sind Diagnosepunkte | Der Extender-Detour ruft beim Bulldoze das Original derzeit ungeachtet `SkipOriginalFunction` auf. Daher nicht über das Event „blockieren“. Bevorzugt Extender-Erweiterung um einen wirksamen Cancel-Vertrag; ersatzweise konfliktgeprüfter Hook vor Refund/Delete. |
| `ai_rebuild` Restfälle | vorhandene Building-Repair-, AI-Wall- und Spawn/Delete-Events | Ereignisse zur Rekonstruktion nutzen; Mauern, Sammelpunkte und Quarry-Plattform als drei getrennte Verträge implementieren |

## Modarchitektur und Tests

Gameplayrelevante Optionen gehören bevorzugt in ein gemeinsames KI-/Gameplay-Modul mit `NetworkMode=1`, `[SyncHostOnly]` und dem Workspace-Presetsystem. Einstellungen werden nur beim bestätigten Kartenstart auf die bereits geladenen AIC-Slots der aktiven KI-Spieler angewandt. Mehrere Spieler, die denselben AIC-Slot teilen, müssen denselben Override erhalten; widersprüchliche per-Spieler-Werte sind mit dieser Tabellenarchitektur nicht darstellbar. UI- und Eingabefunktionen ohne Simulationseinfluss können getrennt als `NetworkMode=0` umgesetzt werden.

Vor jeder nativen Implementierung sind erforderlich:

1. reproduzierbarer Vanilla-DE-Test und ein negativer Kontrollfall;
2. aktueller DLL-Hashabgleich mit `CURRENT.json`;
3. vollständiger Funktions- und Datenfluss über `query.ps1 function`, Caller und Callees;
4. Konfliktprüfung gegen Script-Extender-Detours, `BugfixesAndQoL` und `shcde-fixes`;
5. Save/Load, Pause, hohe Geschwindigkeit und Multiplayer-Determinismus;
6. fail-closed Verhalten, falls Pattern, Strukturgröße oder Offset nicht exakt passt.

Die Reihenfolge für die Umsetzung sollte deshalb sein: AIC-basierte Angriffsoptionen, danach kontrollierter Nachkauf-Test, anschließend Event-basierte Tore; erst danach echte Native-Hooks für bestätigte Restfehler.
