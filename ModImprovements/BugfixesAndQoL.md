# Bugfixes and QoL

## 2. Niedrig: das vom Script Extender erwartete Modlogo fehlt

### Beleg

Der Script Extender erwartet anhand der GUID die Datei:

`Override/Assets/GUI/Sprites/BugfixesAndQoL_Serp.png`

Sie ist weder im Workspace-Mod noch in der Installation vorhanden. Der letzte Logabschnitt enthält deshalb die entsprechende `GameAssetModManager`-Warning; auch ältere Starts zeigen sie wiederholt.

### Fixvorschlag

- Wenn der Mod in der Modliste ein Logo haben soll: passende PNG unter exakt diesem Pfad ergänzen und im Projekt beziehungsweise Release-Skript als Content kopieren.
- Wenn kein Logo gewünscht ist: zunächst prüfen, ob die verwendete Script-Extender-Version eine explizite „kein Logo“-Deklaration unterstützt. Nicht bloß die Warning global unterdrücken, solange andere Mods damit echte Paketierungsfehler sichtbar machen.

### Abnahme

Die gebaute Installation enthält entweder die korrekt benannte PNG oder eine ausdrücklich unterstützte No-Logo-Konfiguration; beim nächsten Start erscheint die Warning nicht mehr.
