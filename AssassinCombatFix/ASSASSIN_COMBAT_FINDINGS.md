# Assassin-Kampf-/Kletter-Fortsetzung: Analyseprotokoll

Diese Datei dokumentiert den Wissensstand des Testmods `AssassinCombatFix`. Sie trennt nachgewiesene Native-Verträge von noch offenen Vermutungen und bereits verworfenen Ansätzen, damit spätere Analysen nicht dieselben Umwege wiederholen.

## Referenz und Abgrenzung

- Kanonische DLL: installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Native-Baseline: `_inspect/CrusaderDE-Native-Baseline/FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Der Testmod ist hart von `BugfixesAndQoL` abhängig und ergänzt nur dessen aktiviertes `EnableImprovedAssassinPathfinding`.
- Der gewichtete Assassin-Pathbuilder und die Korrekturen für reservierte, begehbare Kletterflächen bleiben vollständig im Mod `BugfixesAndQoL`. Dieser Testmod besitzt diese Hooks nicht.

## Bestätigte Erkenntnisse

### Reproduziertes Verhalten

- Nach einem zufälligen Kampf auf einem Bewegungsweg mit Klettern blieben Assassinen stehen, obwohl der grüne Zielmarker bestehen blieb.
- Ohne Kletteranteil wurde der ursprüngliche Bewegungsbefehl nach dem Kampf normal fortgesetzt.
- Frühere Testversionen mit Hooks der vollständigen Funktionen `0x122800` und `0x196280` änderten das beobachtete Verhalten nicht zuverlässig.

### Tatsächlicher Nachkampfpfad

- Der Assassin-Updater `FUN_18016cd70` behandelt AI-Zustand `122` über Remap-Index `13` und den Handler bei RVA `0x16D21C`.
- Der Handler liest den 0-basierten nativen Unit-Index, berechnet `index * 0x490` und lädt das gespeicherte Ziel.
- Bei RVA `0x16D2FF` ruft er direkt `FUN_180196280` auf. Der Registervertrag an diesem Call ist:
  - `RCX`: Unit-Arraybasis.
  - `RDX`: 0-basierter nativer Unit-Index.
  - `R8D`: Ziel-X.
  - `R9D`: Ziel-Y.
  - fünfter Stackparameter: Pfadoption `0`.
- Unabhängig vom Rückgabewert wechselt Vanilla danach in Zustand `101`.

### Fehlender Assassin-Pfadkontext

- Der funktionierende Assassin-Zweig derselben Updatefunktion setzt bei RVA `0x16CFE2` das globale Flag bei RVA `0x60AD6E8` und ruft bei `0x16CFEE` dieselbe Pfadroutine `0x196280` auf.
- Im Zustand-122-Zweig fehlt diese Flagsetzung unmittelbar vor `0x16D2FF`.
- `FUN_180196280` liest das Flag bei `0x1964EE`. Das Flag liegt im Pfadkontext an Offset `0x88`, den der Dispatcher `FUN_1800f4930` auswertet.
- Ist das Kontextfeld ungleich null, ruft der Dispatcher bei `0xF4B27` den Assassin-Pathbuilder `FUN_1800d9c40` auf.
- Vanilla löscht das globale Assassin-Flag selbst:
  - Erfolgs-/regulärer Ausgang bei `0x196743`.
  - Fehler-/Kurz-Ausgang bei `0x19676C`.
- Eine zusätzliche Restaurierung des Flags durch den Mod ist deshalb weder nötig noch erwünscht.

### Sichere Hookgrenzen

- Pre-Hook RVA `0x16D2EA`, Länge 18 Byte: exakt zwei vollständige 9-Byte-Instruktionen zum Laden von Ziel-Y und Ziel-X. Der Call bei `0x16D2FF` wird nicht überschrieben.
- Post-Hook RVA `0x16D304`, Länge 14 Byte: exakt zwei vollständige 7-Byte-Instruktionen zur erneuten Unit-Indexadressierung.
- Die Bereiche überlappen weder einander noch den fünf Byte langen Call.

## Vermutungen und ausstehende Nachweise

- Die fehlende Kontextsetzung ist nach der Native-Analyse die engste Erklärung für den Unterschied zwischen normalen und kletternden Nachkampfwegen. Der neue Callsite-Patch muss noch ingame bestätigen, dass der Assassin anschließend tatsächlich weiterläuft.
- Für Hoch- und Herunterklettern wird derselbe gewichtete Builder erwartet. Beide Richtungen müssen separat getestet werden.
- Die Simulation wird als gleichthreadig erwartet. Die Diagnose korreliert Pre- und Post-Hook dennoch threadlokal und verschachtelungssicher.
- Host und Client sollten aufgrund desselben deterministischen Callsite-Eingriffs denselben Weg erzeugen; dies ist noch in einem echten Multiplayer-Lauf zu bestätigen.

## Getestete oder verworfene Ansätze

### Falsche 1-/0-basierte Unit-Auflösung

Der Parameter von `0x122800` wurde zunächst an eine 1-basierte `TryGetUnitById`-API übergeben. Vanilla verwendet ihn jedoch direkt als 0-basierten Arrayindex mit `index * 0x490`. Dieser Fehler wurde erkannt und korrigiert, löste den beobachteten Zustand-122-Ablauf aber nicht.

### Alleiniger Hook von `0x122800`

`FUN_180122800` ist ein realer allgemeiner Wiederaufnahmeweg. Er stellt gespeicherte Befehlsdaten wieder her, probiert zuerst `FUN_1801946a0` und ruft nur bei Bedarf `FUN_180196280` auf. Für den reproduzierten Assassin-Nachkampfpfad wurde sein Aufruf jedoch nicht beobachtet; Zustand `122` ruft `0x196280` direkt auf. Der Hook war daher für diesen Fehler zu breit und zugleich am tatsächlichen Callsite vorbei. `0x122800` wird nicht mehr gehookt.

### Detour der vollständigen gemeinsamen Pfadroutine `0x196280`

Diese Funktion hat sehr viele Aufrufer für unterschiedliche Einheitentypen und Befehle. Ein archiviertes Log meldete bereits beobachtbare Effekte des gemeinsamen Detours auf Nicht-Assassinen. Die vollständige Funktion zu detouren ist für eine einzelne fehlende Flagsetzung unnötig riskant und wurde entfernt.

### Manuelle Flagrestaurierung

Frühere Wrapper sicherten den Flagwert und restaurierten ihn in `finally`. Die vollständige Vanilla-Analyse belegt zwei eigene Löschpfade in `0x196280`. Der neue Patch setzt das Flag nur von `0` auf `1` und überlässt die garantierte Bereinigung Vanilla.

## Aktueller Patch und Diagnostik

- Der Pre-Hook prüft die beiden `BugfixesAndQoL`-Settings, Indexgrenzen, lebenden Assassin, Zustand `122` und Flagwert `0`.
- Nur bei vollständiger Eligibility setzt er das Flag auf `1`; alle anderen Fälle behalten Vanilla-Verhalten.
- Der Post-Hook protokolliert den ursprünglichen Rückgabewert aus `RAX` sowie den von Vanilla bereinigten Flagwert.
- Alle temporären Zeilen tragen `[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC]` und sind auf 64 relevante Ereignisse pro Karte begrenzt.
- Erwartete Folge für einen relevanten Lauf:
  - `callsite-pre ... aiState=122 ... eligible=True, flagBefore=0`
  - `callsite-post injected=True, result=..., flagAfterVanilla=0`

## Reproduzierbare Ingame-Tests

1. Einen einzelnen Assassin auf einen Weg schicken, auf dem er vor dem Hochklettern einen Gegner bekämpft.
2. Dasselbe vor einem Weg mit Herunterklettern testen.
3. Beide Fälle mit mehreren gleichzeitig befehligten Assassinen wiederholen.
4. Einen Weg ohne Klettern als Kontrollfall testen.
5. Klettern über den Assassin-Aktionsschalter deaktivieren; es darf keine Kletterkante erzwungen werden.
6. `EnableImprovedAssassinPathfinding` deaktivieren; Vanilla-Verhalten muss vollständig erhalten bleiben.
7. Den Lauf auf Host und Client wiederholen und Diagnosen sowie erreichten Weg vergleichen.
