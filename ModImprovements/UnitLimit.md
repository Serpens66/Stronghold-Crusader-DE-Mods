# Unit Limit

## 1. Niedrig: veralteter auskommentierter Vollscan

### Beleg

- `UnitLimit/src/UnitLimitRuntime.cs:25` und `:211` enthalten Reste von `matchingUnitIds`.
- `UnitLimit/src/UnitLimitRuntime.UnitLimits.cs:168-178` enthält die alte `CountAliveUnits`-Implementierung vollständig als Kommentar.
- Die aktive Methode ab Zeile 180 verwendet `ActiveUnitCache` und `ActiveSiegeTentCache`.

### Fixvorschlag

Auskommentiertes Feld, Clear-Aufruf und alte Methode entfernen. Wenn der alte Scan als Referenzvergleich nützlich ist, ihn in einen gezielten Cache-Test verschieben, nicht im Produktivcode behalten.

### Abnahme

Unit-, Siege- und Pending-Recruitment-Zählungen bleiben durch Tests abgedeckt; im Runtime-Code existiert nur der aktive Cachepfad.

## 2. Niedrig: widersprüchlicher Plugin-Lifecycle

`UnitLimitPlugin.cs:32-51` meldet im frühen `OnDestroy` den Library-Handler ab und enthält einen später nicht verlässlich erreichbaren `OnApplicationQuit`-Cleanup. Das aktuelle Log zeigt erfolgreiche Initialisierung vor `OnDestroy`, dennoch sollte der Mod wie die anderen dauerhaften Mods einen expliziten einmaligen Prozesslebenszyklus verwenden.

Abnahme: Initialisierung genau einmal, Runtime-Hooks bleiben nach Zerstörung der BepInEx-Komponente aktiv, kein Cleanup-Pfad suggeriert eine nicht stattfindende Quit-Ausführung.
