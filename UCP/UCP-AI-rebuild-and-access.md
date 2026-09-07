# UCP `ai_rebuild` und `ai_access`: korrigierter DE-Abgleich

## `ai_access`: bereits abgedeckt

UCP verhindert, dass die KI Gebäude wiederholt abreißt, weil ihre Accessibility-Heatmap sie fälschlich als unerreichbar einstuft.

`BugfixesAndQoL/src/AIEconomyProtectionHook.cs` greift bereits in genau die DE-Abrissentscheidung von `c_game_ai_check_inaccessible_building` ein. Der validierte Vergleich liegt beim aktuellen Hash bei RVA `0x3B2FF`; der zugehörige Hovel-Abriss ist in der Baseline als `c_game_ai_delete_hovel` bei RVA `0x3B1D0` mit bestätigter Semantik und ABI erfasst. Der Mod kann solche Abrisse vollständig unterdrücken oder mit seiner verbesserten Prüfung nur dann zulassen, wenn das Gebäude auch unter Berücksichtigung freundlicher beziehungsweise verbündeter Tore und Zugbrücken wirklich unerreichbar ist.

**Bewertung: funktional abgedeckt.** Der Workspace-Fix erfüllt das UCP-Ziel und bewahrt zugleich eine sinnvolle Reaktion auf echte Blockaden. Ein zusätzlicher UCP-Port würde denselben Kontrollbereich doppelt besitzen und soll nicht gebaut werden.

## `ai_rebuild`: nur teilweise abgedeckt

UCP bündelt mehrere unabhängige Reparaturregeln:

1. zerstörte Türme beziehungsweise Turmruinen wiederherstellen;
2. leicht beschädigte Mauern reparieren;
3. Ingenieursgilde, Tunnelgräbergilde und Pechschmelze mitsamt ihrer Sammelpunkte wieder aufbauen;
4. Steinbruchplattformen korrekt überbauen beziehungsweise erneuern.

### Nachgewiesene Überschneidungen

- `BugfixesAndQoL/src/AITowerRuinRepairFix.cs` bezeichnet und implementiert ausdrücklich den aus UCP2 bekannten Turmruinen-Fall. Er leitet passende, zur selben KI gehörende und erst zur Laufzeit entstandene Ruinen in den Vanilla-Aufräum-/Wiederaufbaupfad. **Teil 1 ist abgedeckt.**
- `BetterAIOverbuildRulesFix` und `BetterAIOverbuildPolicy` schützen unter anderem reservierte Flächen von Kaserne, Ingenieurs- und Tunnelgräbergilde sowie Pechschmelze und lösen wiederholte AIV-Überbaukonflikte. Das verbessert dieselbe Problemfamilie, ist aber **kein Beleg**, dass UCPs konkrete Sammelpunkt-Rebuild-Entscheidung vollständig ersetzt ist.

### Noch offene Teilfälle

- Reparatur nur leicht beschädigter Mauern;
- tatsächlicher Wiederaufbau der drei Gebäude samt verlorenem Sammelpunkt;
- Steinbruchplattform-Sonderfall.

Diese drei Pfade sollten getrennt reproduziert werden. Der Script Extender bietet dafür bereits konfliktfreie Beobachtungspunkte: `OnBuildingSpawn`, `OnBuildingDelete`, `OnBuildingRepair`, `OnBuildingAllowRepairInProximity`, die beiden Repair-Cost-Events, `OnAIBuildWall` sowie Kartenstart/Save-Load. Damit lässt sich pro Building-Game-ID rekonstruieren, ob die KI repariert, löscht, neu baut oder ihren AIV-Slot verliert. Die Events beweisen aber nicht automatisch die interne Rebuild-Entscheidung.

Erst nach diesem Trace den jeweiligen DE-AIV-Rebuild-Zweig identifizieren und selektiv erweitern:

- Mauern über den bestehenden Repair-Query/-Cost-Pfad zulassen, ohne globale Spielermauern kostenlos zu reparieren;
- Gilde/Pechschmelze zusammen mit dem zugehörigen AIV-Sammelpunkt als eine transaktionale Rebuild-Regel behandeln;
- Quarry-Plattform nur beim zur selben KI und zum erwarteten Steinbruch gehörenden Mapper-Slot überbauen.

`eStructs` verwenden, Building-IDs an API-Grenzen 1-basiert halten und wegen Simulationswirkung `NetworkMode=1` setzen. Die vorhandenen `AITowerRuinRepairFix`- und `BetterAIOverbuildRulesFix`-Hooks bleiben alleinige Eigentümer ihrer Bereiche; neue Hooks dürfen sie nicht überdecken. Die Baseline- und Eventmatrix steht in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

