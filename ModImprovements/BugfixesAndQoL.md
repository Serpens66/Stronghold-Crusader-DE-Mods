# Bugfixes and QoL

## 1. Mittel: Per-Player-Getter fragen die Netzwerk-Spieler-ID in ungeeigneten Phasen wiederholt ab

### Beleg

- `BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs:93-185` löst für jeden Zugriff auf eine `[SyncPerPlayer]`-Property erneut `LocalPlayerIdOrOne` auf.
- `LocalPlayerIdOrOne` ruft an Zeile 290 immer `GameNetworkAPI.GetLocalPlayerId()` auf und ersetzt ungültige Werte mit Spieler 1.
- Im letzten BepInEx-Logabschnitt stehen 60 identische Warnungen, dass `EditorDirector` außerhalb der Lobby `-1` geliefert hat. Diese Getter sind der einzige hochfrequente direkte Workspace-Aufrufer dieser API; SpawnCastle besitzt nur zwei isolierte direkte Aufrufstellen. Damit ist BugfixesAndQoL der wahrscheinliche Hauptverursacher der Warnungsserie.

### Auswirkung

- Reflection, Preset-Capture, Bindings oder Property-Refresh können eine Warnung pro Getterzugriff erzeugen.
- Der stille Fallback auf Slot 1 kann in einem Kontext ohne gültige Netzwerk-ID den falschen Per-Player-Slot lesen oder schreiben.
- Die wiederholte native/Singleton-Abfrage ist unnötig.

### Fixvorschlag

1. Eine zentrale, phasenbewusste lokale Spielerauflösung verwenden.
2. In einer laufenden Karte die native lokale Spieler-ID aus `GamePlayerManagerAPI` verwenden; in einer echten Lobby die Netzwerk-ID. Außerhalb beider Phasen den zuletzt validierten Slot oder einen ausdrücklich als Initialdefault behandelten Slot nutzen.
3. Den gültigen lokalen Slot bei eindeutigen Lifecycle-Ereignissen aktualisieren, statt ihn in jedem Property-Getter neu zu ermitteln.
4. Ungültige Werte nicht still zu einem beliebigen aktiven Mitspieler umdeuten. Den Defaultfall klar von einem synchronisierten Spielerslot trennen.
5. Falls sinnvoll, die Auflösung als gemeinsamen Helper implementieren, damit andere Mods nicht eigene Varianten anlegen.

### Abnahme

- Menü, Lobby, Singleplayer und Multiplayer lesen/schreiben jeweils den erwarteten Per-Player-Slot.
- Preset-Capture außerhalb einer Karte erzeugt keine `GetLocalPlayerId`-Warnungsserie.
- HostClientPresetTests um mindestens zwei unterschiedliche lokale Player-IDs und einen „ID noch nicht verfügbar“-Fall ergänzen.

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

## 3. Niedrig: `OnApplicationQuit` ist auf der beim Start zerstörten Plugin-Komponente kein verlässlicher Cleanup-Pfad

`BugfixesAndQoLPlugin.cs:51-65` beschreibt Prozessende-Cleanup. Die BepInEx-Manager-Komponente wird jedoch bereits beim Start zerstört; eine zerstörte Komponente erhält später keinen normalen Quit-Callback mehr. Der aktuelle Runtime-Erhalt funktioniert über dauerhafte Registrierungen, nicht über die Plugin-Komponente.

Entweder den rein theoretischen Cleanup entfernen und den Prozesslebenszyklus ausdrücklich dokumentieren oder Cleanup an einen tatsächlich persistenten Besitzer hängen. Keine während der Prozesslaufzeit benötigten Hooks in `OnDestroy` lösen.
