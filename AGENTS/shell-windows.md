# PowerShell, CMD, BAT und Sandbox

**Lesen, wenn:** PowerShell-/CMD-Befehle oder -Skripte geschrieben bzw. geändert werden, BAT-/CMD-Treiber gestartet werden oder ein Shellaufruf an Sandbox/Berechtigungen scheitert. Gilt zusätzlich zur Root-`AGENTS.md`.

## Allgemein

- Vor jedem PowerShell-Aufruf den vollständigen Befehl auf Syntax-, Quoting- und Pipelinefallen prüfen.
- PowerShell-Variablen nie `$HOME`, `$home` oder `$CODEX_HOME` nennen bzw. diese Systemvariablen zweckentfremden.
- Einen anweisungsbasierten `foreach (...) { ... }`-Block nie direkt mit `|` weiterleiten; erst sammeln (`$rows = @(foreach (...) { ... })`), dann `$rows | Sort-Object ...` bzw. `$rows | Format-Table ...`. Einen wegen `An empty pipe element is not allowed` gescheiterten Befehl nicht in derselben Pipelineform wiederholen.

## Treiber starten

- `build.bat` mit Leerzeichen im Pfad direkt über den Aufrufoperator: `& 'D:\Pfad mit Leerzeichen\build.bat' /nopause; exit $LASTEXITCODE`. Weder `cmd.exe /c` noch `Start-Process` dazwischen (zusätzliche Quote-Ebene kann `/nopause` verschlucken und den Build am `pause` festhalten).
- Muss ein `.cmd`-Treiber ausdrücklich über `Start-Process` und `cmd.exe` laufen, die äußere Argumentfolge als eine Zeichenfolge `/d /s /c ""<absoluter Treiberpfad>""` übergeben; `-ArgumentList @('/d', '/c', $driver)` verliert die nötige Quote. Nicht für `build.bat` verwenden.

## Prüfpfade testen

- Neue/geänderte PowerShell-Prüfpfade auch über den vorgesehenen BAT-/Unterprozess-Aufruf testen: frisches `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`, tatsächliches Arbeitsverzeichnis, vom Treiber vererbte Umgebung. Ein Test in PowerShell 7 oder einer durch Dot-Sourcing/Modulimporte vorbereiteten Sitzung genügt nicht.
- Benötigte Assemblies ausdrücklich laden; ZIP-Tests unter Windows PowerShell 5.1 brauchen ggf. `System.IO.Compression` und `System.IO.Compression.FileSystem`. Echte Veröffentlichungen durch simulierte Dienste oder reine Prüfmodi vermeiden.

## Sandbox und Berechtigungen

- Sichere, eindeutige Wiederholungen nach Infrastruktur- oder Sandboxfehlern sind erlaubt, wenn der Eskalationsweg feststeht und keine fachliche Entscheidung nötig ist.
- Pfade außerhalb des Workspace (z. B. die UNC-Freigabe des Client-Logs) können erhöhten Zugriff erfordern.
- **Codex-spezifisch:** Scheitert ein PowerShell-Aufruf schon beim Prozessstart mit `CreateProcessAsUserW failed: 5 (Zugriff verweigert)` mit Verweis auf die WindowsApps-Weiterleitung zu `pwsh.exe`, liegt kein Fehler im Nutzbefehl vor. Den unveränderten Aufruf einmal über den erhöhten Sandbox-/Berechtigungsweg wiederholen; nicht durch Umformulieren, andere Quotings oder Shellverschachtelungen umgehen. Scheitert auch das, anhalten und den Nutzer fragen.
