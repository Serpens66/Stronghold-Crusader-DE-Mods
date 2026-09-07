# Eigenständige Fixes aus `ucp3-fixes`

## `aiv-troops-behaviour`

Dieser Fix wurde bereits separat untersucht. Der Bericht wurde in den neuen UCP-Dokumentationsordner verschoben: [UCP-AI-behavior.md](UCP-AI-behavior.md).

## `hopfarm-limit-fix`

### Fehler

Hopfenfarmen werden in der Vanilla-KI nicht zum gemeinsamen Limit aktiver Farmen gezählt. Dadurch kann ein KI-Lord mehr Farmen als durch seine AIC-Grenze beabsichtigt errichten.

### DE-Relevanz und vorhandene Abdeckung

**Bestätigt vorhanden, aber bereits vollständig durch `shcde-fixes-main` abgedeckt.** Dessen `AIDetours.cs` hookt direkt `c_game_ai_count_active_farms`, erkennt den fehlerhaften Hopfenfarm-Ausschluss und kann die Korrektur global oder lordabhängig anwenden. `EnableHopsFarmFix` ist standardmäßig aktiv; zusätzlich existiert `ForceHopsFarmFix` sowie eine Lord-Whitelist/Preference.

Folgerung: Kein zweiter Fix in `BugfixesAndQoL`. Zwei Hooks am selben nativen Entscheidungspunkt würden unnötiges Konflikt- und Wartungsrisiko erzeugen. Voraussetzung ist lediglich, dass `shcde-fixes` installiert und die Option für den betreffenden Lord aktiv ist.

### Falls `shcde-fixes` später entfällt

Die vorhandene DE-Implementierung ist bereits die beste technische Referenz: signaturbasierter Inline-Hook, lordabhängige Einstellung und synchronisierte Gameplay-Konfiguration. Eine Neuimplementierung sollte nicht parallel, sondern nur als bewusst beschlossene Migration erfolgen.

## Umfang des `ucp3-fixes`-Repositories

Die untersuchte lokale Version enthält außer diesen beiden Modulen keine weiteren eigenständigen Bugfix-Module. Die übrigen Store-Erweiterungen sind überwiegend KI-Pakete, AIV/AIC-Dateien, Loader, Karten, Balance- oder Komfortmodule und wurden nicht allein wegen des Wortes „fix“ in Beschreibungen als Engine-Bugfix eingestuft.

