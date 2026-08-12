# Start Conditions

## 1. Niedrig: ungenutzter Parser

`StartConditions/src/StartConditionsRuntime.Helpers.cs:35-40` definiert `TryParseNullableInt`, ohne dass es eine Aufrufstelle gibt. Entfernen und danach prüfen, ob dadurch ein `using` überflüssig wird.

## 2. Niedrig: ungenutztes Feld in `PendingStartTroopPlayer`

### Beleg

`StartConditions/src/StartConditionsRuntime.StartTroops.cs:283-294` speichert `IsAI`. Der Konstruktor bekommt den Wert, aber nach dem Erstellen wird nur `PlayerId` und `Multiplier` gelesen. Die AI-/Human-Auswahl ist bereits vor dem Erzeugen in den Multiplikator eingeflossen.

### Fixvorschlag

Feld und Konstruktorparameter entfernen und die Erzeugung auf `new PendingStartTroopPlayer(playerId, multiplier)` umstellen.

### Abnahme

Starttruppen für AI und Menschen behalten ihre unterschiedlichen Multiplikatoren; die vorhandenen verzögerten Starttruppen-Tests bleiben grün.

## 3. Niedrig: widersprüchlicher Plugin-Lifecycle

`StartConditionsPlugin.cs:32-51` meldet `LibraryLoaded` in dem beim Start ausgeführten `OnDestroy` ab und erwartet später `OnApplicationQuit` für Cleanup. Das aktuelle Log zeigt zwar die erfolgreiche Library-Initialisierung vor `OnDestroy`, das Muster ist aber nicht robust und der Quit-Pfad liegt auf einer bereits zerstörten Komponente.

Auf einen einmaligen prozessweiten Initialisierungspfad umstellen: Selbstabmeldung nach erfolgreichem Handler oder statische Once-Grenze, keine Abmeldung notwendiger Events in `OnDestroy`, kein nur scheinbar erreichbarer Cleanup.
