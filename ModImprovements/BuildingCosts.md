# Building Costs

## 1. Niedrig: ungenutzte Reflection-Methode

### Beleg

`BuildingCosts/src/BuildingCostsRuntime.cs:665-671` definiert `GetMainViewModelString(string propertyName)`. Der Methodenname kommt im gesamten Workspace-Mod nur an dieser Definition vor.

### Fixvorschlag

Die Methode entfernen. `System.Reflection` darf nicht pauschal entfernt werden, weil dieselbe Datei Reflection an anderen Stellen weiterhin benötigt.

### Abnahme

Der Mod baut ohne neue Warnungen; Tooltiptexte und Icons funktionieren unverändert.

## 2. Niedrig: widersprüchlicher Plugin-Lifecycle und faktisch toter Quit-Cleanup

### Beleg

- `BuildingCostsPlugin.cs:34-38` sagt, der Runtime solle bis Prozessende aktiv bleiben, meldet aber in `OnDestroy` den `LibraryLoaded`-Handler ab.
- `OnApplicationQuit`/`DisposeRuntime` an Zeile 40-53 liegen auf derselben Plugin-Komponente, die nachweislich bereits beim Start zerstört wird.
- Das aktuelle Log zeigt, dass `LibraryLoaded` bislang vor `OnDestroy` lief. Es liegt daher aktuell kein beobachteter Initialisierungsfehler vor, das Muster widerspricht aber dem dokumentierten Lifecycle und ist unnötig fragil.

### Fixvorschlag

Den Initialisierungshandler nach erfolgreicher einmaliger Initialisierung selbst abmelden oder durch eine statische Once-Grenze schützen. In `OnDestroy` keine benötigten Script-Extender-Events abmelden. Den nicht erreichbaren Quit-Cleanup entfernen oder an einen nachweislich persistenten Besitzer verlagern; Prozessende-Cleanup ist für diese Hooks normalerweise nicht nötig.

### Abnahme

Ein Initialisierungsmarker erscheint genau einmal, auch wenn die Library vor oder nach der Mod-Subscription verfügbar wird. Nach `OnDestroy` bleiben alle Runtime-Hooks aktiv.
