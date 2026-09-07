# Offene Restfälle aus UCP `ai_rebuild`

UCP bündelt mehrere unabhängige Reparaturregeln:

1. leicht beschädigte Mauern reparieren;
2. Ingenieursgilde, Tunnelgräbergilde und Pechschmelze mitsamt ihrer Sammelpunkte wieder aufbauen;
3. Steinbruchplattformen korrekt überbauen beziehungsweise erneuern.

### Nachgewiesene Überschneidungen

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

