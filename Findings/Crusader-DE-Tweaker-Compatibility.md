# Crusader DE Tweaker: bestätigte Kompatibilitätsprobleme

Geprüft: Crusader DE Tweaker 2.6.5, Commit `bf54c77768cc0b7c5c8fa7c07de70ec84e514619`, mit dem aktuellen Serps-Modpack und Script Extender 2.8.0.

## 1. Gemeinsame Kostenwerte haben keinen gemeinsamen Besitzer

Der Tweaker und `UnitCosts` schreiben beide die globalen Goldkosten über `SetUnitGoldCost`; der Tweaker und `BuildingCosts` schreiben dieselben globalen Gebäude-Kostenfelder. Bei gleichzeitig aktivierten Overrides bestimmt der jeweils letzte Schreibvorgang den tatsächlichen Preis.

**Folge:** Serps-Lobbysettings und Tooltips können einen anderen Preis anzeigen als der zur Laufzeit wirksame Wert; erneutes Anwenden einer Einstellung kann das Ergebnis während derselben Sitzung wieder umschalten.

**Belegt durch:** Tweaker `Config/Toml/Units/Properties/GoldCostProperty.cs` und `Config/Toml/Structures/Properties/StructureCostProperties.cs`; SerpsMods `UnitCosts/src/UnitCostsRuntime.cs` und `BuildingCosts/src/BuildingCostsRuntime.cs`.

## 2. Rekrutierungsgrenzen teilen den Detour-Vertrag nicht

Tweaker, `UnitLimit` und `UnitCosts` detouren dieselbe Methode `EngineInterface.GameAction(MakeTroop, ...)`. Die Serps-Hooks reichen Kürzungen über `Shared.RecruitmentHookContext` durch die Hook-Kette weiter; der Tweaker meldet eine von ihm gekürzte Menge dort nicht. Seine eigene Unit-Grenze oder Pferdeprüfung kann deshalb nach der Serps-Entscheidung nochmals reduzieren.

**Folge:** `UnitLimit` kann mehr ausstehende Rekruten reservieren als tatsächlich an Vanilla weitergereicht wurden und bis zum Ablauf seiner Pending-Reservierung weitere Rekrutierung fälschlich blockieren. Zusätzlich gilt stets die strengere der unabhängig konfigurierten Grenzen, während jede Mod nur ihren eigenen Grenzwert anzeigt.

**Belegt durch:** Tweaker `Config/BepInEx/Systems/Handlers/MakeTroopRecruitHook.cs`; SerpsMods `UnitLimit/src/MakeTroopGameActionHook.cs`, `UnitCosts/src/MakeTroopGameActionHook.cs` und `Shared/RecruitmentHookContext.cs`; nativer Rekrutierungspfad über `MakeTroop` gegen Baseline-Hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## 3. Gebäudegrenzen werden unabhängig doppelt erzwungen

Tweaker und `BuildingLimit` setzen unabhängig voneinander im selben `OnPlacementValidation(Pre)`-Pfad den erzwungenen Blockierungszustand.

**Folge:** Es gilt stets die strengere Grenze. Die Serps-Anzeige kann noch freie Kapazität melden, obwohl der Tweaker die Platzierung bereits blockiert; umgekehrt erklärt der Tweaker keine Blockierung durch die zusammengefassten Serps-Gebäudekategorien.

**Belegt durch:** Tweaker `Config/BepInEx/Systems/Handlers/BuildingCapHandler.cs`; SerpsMods `BuildingLimit/src/BuildingLimitRuntime.cs` und `BuildingLimit/src/BuildingLimitRuntime.BuildingLimits.cs`.

## 4. Tweaker-Spielwerte sind im Multiplayer nicht synchronisiert

Die Tweaker-Werte stammen ausschließlich aus lokalen TOML-, CSV- und BepInEx-Konfigurationsdateien. Der Mod besitzt keinen Lobbysettings- oder Netzwerk-Synchronisationspfad. Die Serps-Modinventur vergleicht für Fremdplugins GUID und Version, aber nicht deren Dateien unter `BepInEx/config`.

**Folge:** Teilnehmer mit derselben Tweaker-Version, aber unterschiedlichen Konfigurationsdateien passieren die Versionsprüfung und können dennoch verschiedene Kosten, Limits, globale Werte oder Schadensdaten verwenden. Dadurch sind abweichende Befehlsannahme und Simulationszustände möglich.

**Belegt durch:** Tweaker `Config/Toml/ConfigLoader.cs` und `Config/BepInEx/BepInExConfigManager.cs`; SerpsMods `SerpsModsHost/src/LobbyModHashWarning.cs` und `SerpsModsHost/src/ModInventoryCompatibility.cs` (Inventarfelder ausschließlich GUID, Name, Version und Clientside-Flag).
