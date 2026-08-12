# Building Limit

## 1. Niedrig: veralteter Scanpfad bleibt als auskommentierter Code im Produktivcode

### Beleg

- `BuildingLimit/src/BuildingLimitRuntime.cs:23` und `:167` enthalten auskommentierte Reste von `matchingBuildingIds`.
- `BuildingLimit/src/BuildingLimitRuntime.BuildingLimits.cs:99-116` enthält die vollständige alte, lineare `CountAliveBuildings`-Implementierung als Kommentar.
- Die aktive Implementierung ab Zeile 118 verwendet den `ActiveBuildingCache`.

### Fixvorschlag

Die auskommentierten Felder, Clear-Aufrufe und die alte Methode entfernen. Historische Vergleichslogik gehört bei Bedarf in Versionsverwaltung oder einen gezielten Cache-Regressionstest, nicht parallel in die Runtime-Datei.

### Abnahme

Cache-Tests und BuildingLimit-Funktion bleiben unverändert; im Produktivcode existiert nur noch der aktive Zählpfad.

## 2. Niedrig: widersprüchlicher Plugin-Lifecycle

`BuildingLimitPlugin.cs:32-51` besitzt dasselbe Muster wie BuildingCosts: Abmeldung von `LibraryLoaded` in dem während des Starts laufenden `OnDestroy`, gefolgt von einem später nicht verlässlich erreichbaren `OnApplicationQuit`-Cleanup. Das aktuelle Log zeigt zwar Library-Initialisierung vor `OnDestroy`, die Implementierung soll dennoch auf einen einmaligen, prozessweiten Initialisierungspfad umgestellt werden.

Abnahme: Initialisierung genau einmal, keine Abmeldung benötigter Events in `OnDestroy`, aktive Cache-/UI-Hooks nach Zerstörung der Plugin-Komponente.
