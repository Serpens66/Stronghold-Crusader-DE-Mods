# Dependency Bootstrap Probe: Beobachtungsmodus nach Startfehler

## Aktueller Stand

Der Build vom 16.10.2026 um 17:06:49 verursachte einen schwarzen Bildschirm; nach Entfernen seines Patchers startete das Spiel wieder. Die genaue verursachende Operation innerhalb seiner frühen Vorbereitung ist nicht abschließend bewiesen. Die Desktop-Dateien unter `C:\Users\Serpens66\Desktop\Neuer Ordner\Neuer Ordner` bleiben unverändert als Beweismaterial erhalten. Diese Fassung nicht erneut installieren.

Der Ersatz entfernt sämtliche Hookvorbereitung aus Finish. Er startet standardmäßig im Modus **Observe**. Die Version bleibt während der Diagnose 0.1.0; die konkreten Builds werden anhand ihrer SHA256-Werte unterschieden. APIShared, Extender und deren Konfiguration werden nicht verändert. Es gibt noch keine Abhängigkeitskorrektur.

## Erster Spielversuch: Observe

1. Spiel schließen; `build.bat /nopause` prüft, baut, setzt ausschließlich die eigenen Testschalter auf Observe und installiert die zwei eigenen DLLs plus info.json. `/noinstall` verändert keine Spielinstallation oder Schalter.
2. Vollständig neu starten. Finish richtet nur Trace und Loglistener ein; keine Chainloader-Typinitialisierung, Methodenkopie, Delegate-Vorbereitung oder Hookinstallation.
3. Falls die bekannte Abhängigkeitswarnung in der aktuellen Modauswahl vorhanden ist, muss sie unverändert bleiben. Ohne ihre verursachenden Mods ist ihr Fehlen kein Testfehler. Hauptmenü öffnen und einige Sekunden warten.
4. Im Testmod-Verzeichnis `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Check-Run.ps1 -Mode Observe` ausführen. Standard ist ebenfalls Observe.
5. Zusätzlich das Hauptmenü bedienen und den aktuellen Unity-Player.log auf neue Initialisierungs-/Renderingfehler prüfen. Ein Marker-PASS allein bestätigt diese manuellen Kriterien nicht.

Die eigene Datei `BepInEx/DependencyBootstrapProbe.trace.log` enthält sofort geflushte Marker, unabhängig vom normalen BepInEx-Log. Jeder Start beginnt mit SESSION samt eindeutiger ID. Erwartet: FINISH mode=Observe → MODE → LISTENER_ATTACHED → READY_OBSERVED → PLUGIN_AWAKE → CHAIN_COMPLETE mit snapshot=False/extender=False/checks=0 → POST_CLEANUP_RENDER nach mindestens 120 verschiedenen Renderframes und zerstörter ursprünglicher Plugin-Komponente. Kein Hook-/JIT-/Kopiermarker darf erscheinen.

`Chainloader ready` kommt in der geprüften BepInEx-Fassung synchron am Ende von Initialize, nach ThreadingHelper und Logging, aber noch VOR der Zuweisung `_initialized=true`. Der Listener ändert keine Flags und ruft weder Initialize noch Start selbst auf. Fehlende, fremde und doppelte Meldungen aktivieren keine zusätzlichen Callback-Aufrufe; Fehler verlassen den Listener nicht.

## Erst nach erfolgreicher Beobachtung: Active

Nur nach bestandenem Observe-Logcheck, nutzbarem Menü und geprüftem Unity-Log bei geschlossenem Spiel:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Set-ProbeMode.ps1 -Mode Active -ConfirmMenuAndUnityLog

Der Treiber prüft den neuesten Beobachtungsstart gegen lokale und installierte Binäridentitäten und schreibt erst dann einen eigenen `.active`-Schalter mit dem Patcher-SHA256. Ohne passenden Schalter bleibt die Hookruntime unerreichbar. Ein erneuter Installationsbuild setzt den Modus wieder auf Observe. Ein fremder/veralteter Aktivierungshash wird als Fehler diagnostiziert und aktiviert keine Hooks.

Im folgenden vollständigen Spielstart ruft ausschließlich READY_OBSERVED die getrennte Hookruntime auf. Methodentransformation, Delegate-Erzeugung, JIT-Vorbereitung und Hookinstallation haben jeweils eigene Phasenmarker; beide Kopien werden vorbereitet, aber nicht aufgerufen. Die eigentliche SE-Prüfung bleibt unverändert. Danach mit `Check-Run.ps1 -Mode Active` prüfen. Erst dieser separate Spielversuch kann den späteren Hookzeitpunkt bestätigen. Bis dahin nicht als sicher oder erfolgreich darstellen.

## Kontrolllauf und Rückweg

Bei geschlossenem Spiel:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Set-ProbeMode.ps1 -Mode Disabled

Neustart, dann `Check-Run.ps1 -Mode Disabled`. Erwartet sind FINISH, DISABLED, LISTENER_ATTACHED, READY_OBSERVED, PLUGIN_DISABLED und CHAIN_COMPLETE mit inaktiven Zählern. Für reines Beobachten denselben Modustreiber mit `-Mode Observe` verwenden.

Für vollständige Entfernung ausschließlich unserer beiden Komponenten und ihrer Modusschalter:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Set-ProbeMode.ps1 -Mode Remove

`-WhatIf` zeigt den Vorgang ohne Änderungen. Es werden nur die zwei eigenen DLLs, das eigene info.json und die beiden eigenen Schalter gelöscht, keine Verzeichnisse rekursiv. Trace, fremde Plugins/Patcher und das Desktop-Beweismaterial bleiben erhalten. Bei erneutem schwarzem Bildschirm das Spiel schließen, die Probe entfernen und die Trace sowie Player.log sichern. Keine Wiederholung mit unveränderter fehlerhafter Fassung.

## Automatisierte Prüfungen und Grenzen

- Der isolierte Test kompiliert dieselben Runtime-Transformationsquellen und prüft echte DMD-Kopien von Chainloader und SE, Originalinstruktionen, Sprungziele, Exceptiongrenzen, Stackneutralität und sieben fehlerhafte Verträge. Keine Spielmethode wird ausgeführt, kein Hook installiert.
- Eine IL-Prüfung der frühen Typen schließt Unity-/MonoMod-/Chainloader-/HookRuntime-Typverweise aus. Die Listenerzustandsmaschine prüft fremde/fehlende/doppelte Meldungen und Callback-/Loggerfehler.
- Synthetische Logtests prüfen Observe, Active und Disabled einschließlich falscher Reihenfolge, alter Builds, unerlaubter Hookmarker und fehlender Abschlussnachweise. Synthetische Logs sind kein Spielnachweis.
- Desktop-.NET kann Unity-ECalls nicht vollständig JIT-vorbereiten. RuntimeHelpers.PrepareDelegate bleibt deshalb im späteren aktiven Spielpfad vorgeschrieben. Das neue Observe führt es überhaupt nicht aus.
- Keine JSON-Abhängigkeit, keine Assembly-CSharp-Referenz, kein APIShared-Verbraucher. Statische Hooks/Logger bleiben prozessweit; kein OnDestroy-/OnDisable-Teardown. Der Renderbeobachter hängt am statischen Unity-Event.

Status: Neue Spielabnahmen Observe, Active und Disabled stehen aus. Erst danach über Schatteninventar und spätere Indexergänzung entscheiden.

Vorbereitung abgeschlossen: Ersatzbuild gebaut und im Modus Observe installiert; lokale und installierte DLLs stimmen überein. Frühpfad-/Listenerprüfung, beide Transformationen, sieben Vertrags-Negativfälle und 23 Logauswerter-Fälle sowie gemeinsame Pflichtprüfungen bestanden. Spielabnahme weiterhin offen.

- BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll: SHA256 EA182E455F8E1F5D8630C0065690DC2AF55D92BDAD1522BA5EA9DC330AD83A26; Build 2026-10-16 18:00:51.

- BepInEx\plugins\DependencyBootstrapProbe_Serp\DependencyBootstrapProbe.dll: SHA256 C44E1B8E00976A5C458394513D6DE41970F6AA111916E76275F0F5473919F6B6; Build 2026-10-16 18:00:52.
