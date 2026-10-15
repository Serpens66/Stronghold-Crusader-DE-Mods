# Externe Komponenten: Script Extender und Fixes-Mod

**Lesen, wenn:** ein Fehler im Script Extender oder im externen Fixes-Mod gefunden bzw. umgangen wird oder Fixes-bezogene Präferenzen, Presets, Merge-/Exportlogik oder Belagerungsfunktionen betroffen sind. Gilt zusätzlich zur Root-`AGENTS.md`.

## Umgang mit Fehlern externer Komponenten

- Der Autor von Script Extender und Fixes-Mod akzeptiert keine Änderungen mehr, auch keine Bugfixes. Deshalb **keine Autorenreports** für diese beiden Projekte; stattdessen untersuchen, wie wir den Fehler möglichst risikoarm in eigenen Mods oder APIShared korrigieren.
- Extender-Korrekturen gehören in APIShared: entweder SE-Code zur Laufzeit korrigieren oder korrigierte Kopien der betroffenen Funktionen in APIShared bereitstellen. Den Weg nach konkretem Fehler, Eingriffsrisiko und Kompatibilität wählen.
- Fixes-Mod-Korrekturen gehören vorzugsweise in BugfixesAndQoL. Ist das technisch nicht sinnvoll, vor einer anderen Zuordnung den Nutzer fragen und die Antwort abwarten.
- Die externen Quellbäume bleiben unverändert (Extender-Fork und Fixes-Klon sind Analysequellen). Native Audits, permanente Hooklebensdauer, Kompatibilitätsprüfungen und Rücksprache-Empfehlungen bei APIShared-Breaking-Changes gelten auch für diese Korrekturen.
- Statische Befunde nicht als nachgewiesene gamebreaking Bugs behandeln. Keine zusätzlichen Nachweise oder Testmods für die folgenden Hinweise ohne neuen Auftrag.

## Fixes-Kompatibilität

- Quelle: kanonischer lokaler Klon `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main` (https://gitlab.com/rawra-stronghold-crusader/shcde-fixes). Nach dem Fixes-Mod zu laden ist eine erlaubte Kompatibilitätslösung.
- Vor Fixes-bezogener Arbeit die aktuelle `knowledge/FIXES_COMPATIBILITY.md` im Semantic-Ordner aus `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` lesen; keinen alten Hashordner voraussetzen. Aktuell geprüfter Fixes-Quellstand: 1.25.1. Bei Fixes-Updates die Hinweise gegen den lokalen Quellcode erneut prüfen.
- `CustomMinPopularityRequiredForBadThings` benötigt `CustomBuildAttemptsRequiredForBadThings` (im Fixes-Mod dokumentiert). Eigene Einstellungen setzen bei gewünschter Grenze beide bewusst (Vanilla-Versuchszahl 100). Interne Popularität ist hundertfach skaliert (5000 = 50, 9000 = 90). Fremde Einstellungen nicht stillschweigend vervollständigen oder begrenzen.
- `EnableAIDistancedSiegeTents`: die 5×5-Abstandsprüfung funktioniert, aber die Zielprüfung kehrt bei gültigen Zielen vorzeitig zurück und ungültige scheitern am Ressourcen-Lookup → die Richtungsprüfung ist unerreichbar. Eigene Belagerungslogik darf sie nicht als Schutz voraussetzen. Eine dadurch verursachte Ingenieurblockade ist nicht nachgewiesen.
- Eigene Kaufpräferenzen enthalten vollständige Paare für Wood, Iron und Ale (`BuyWoodAt/BuyWoodAmount` ist im Fixes-Mod dokumentiert). Fremde unvollständige Paare nicht ergänzen; darstellbare Modderwerte nicht durch eigene Plausibilitätsgrenzen ersetzen.
