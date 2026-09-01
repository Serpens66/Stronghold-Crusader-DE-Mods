# Assassin-Kampf-/Kletter-Fortsetzung: Analyseprotokoll

Diese Datei dokumentiert den Wissensstand des Testmods `AssassinCombatFix`. Sie trennt bestätigte Native-Verträge von noch ausstehenden Ingame-Nachweisen und verworfenen Ansätzen.

## Referenz und Abgrenzung

- Kanonische DLL: installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Native-Baseline: `_inspect/CrusaderDE-Native-Baseline`, einschließlich der semantischen Funktions- und Callgraph-Suche für denselben DLL-Hash.
- Der Testmod ist hart von `BugfixesAndQoL` abhängig und ergänzt ausschließlich dessen aktiviertes `EnableImprovedAssassinPathfinding`.
- Der gewichtete Assassin-Pathbuilder und die Korrekturen für reservierte Kletterflächen bleiben vollständig in `BugfixesAndQoL`.

## Bestätigte Erkenntnisse

### Reproduziertes Verhalten und Zustandsfolge

- Nach einem zufälligen Kampf auf einem Bewegungsweg mit Klettern bleibt der Assassin stehen, obwohl der grüne Zielmarker bestehen bleibt.
- Der passive Map-Editor-Trace zeigt für den reproduzierten Ablauf `101 → 106 → 101 → 0 → 1`.
- Beim Übergang aus Zustand `106` verschwindet der aktive Pfad im fehlerhaften Kletterfall. Das gespeicherte Ziel bleibt erhalten.
- Ein normaler Kontrollweg erzeugt nach demselben Kampf wieder einen Pfad und wird fortgesetzt.
- Die zuvor untersuchten Hooks in Zustand `107`, Zustand `122` und `FUN_180122800` wurden in diesem Ablauf nicht erreicht.

### Statisch bestätigter Zustand-106-Pfad

- Der Assassin-Zustandsautomat `FUN_18016CD70` behandelt Zustand `106` und ruft bei RVA `0x16DFD3` `FUN_1801853F0` auf.
- `FUN_1801853F0` ruft bei RVA `0x18540D` `FUN_1801976C0` auf. Die Rücksprung-RVA `0x185412` identifiziert diesen Kampfpfad gegenüber den anderen Aufrufern von `0x1976C0`.
- Der Prolog von `FUN_1801976C0` legt diese ursprüngliche Rücksprungadresse am Pfad-Callsite bei `RSP+0x38` ab.
- `FUN_1801976C0` löscht die bisherigen Pfadflags, stellt den gespeicherten AI-Zustand aus Unit-Offset `0x91E` wieder her und übernimmt die sekundären Zielkoordinaten aus `0x744/0x746`.
- Danach ruft es bei RVA `0x19772B` die gemeinsame Pfadroutine `FUN_180196280` auf und bei `0x197735` die Nachbearbeitung `FUN_180196810`.
- Am Call `0x19772B` enthält `RDI` den 0-basierten Unit-Index; `R8D/R9D` enthalten die gespeicherten Zielkoordinaten.
- Der aktuelle Ingame-Log enthält trotz korrekt installiertem, absturzfreiem Hook bei `0x197716` keinen Eintritt in diesen inneren Repath-Block. Der reproduzierte Ablauf wird daher vor dem Hook abgebrochen oder verwendet zusätzlich einen anderen Pfadaufrufer.
- Vor dem inneren Block existieren zwei relevante Schranken: `FUN_1801853F0` ruft `FUN_1801976C0` nur bei `r_AttackingUnitId == 0` auf; `FUN_1801976C0` verarbeitet die Einheit nur, wenn das niederwertige Word von `GameUnit.N0000019A` null und die Einheit nicht tot ist.

### Fehlender Assassin-Kontext

- Vor dem Call bei `0x19772B` setzt Vanilla das Assassin-Pfadkontextflag bei RVA `0x60AD6E8` nicht.
- `FUN_180196280` liest dieses Flag bei `0x1964EE` und löscht es auf beiden auditierten Ausgängen bei `0x196743` und `0x19676C`.
- Der Dispatcher wählt mit gesetztem Kontext bei `0xF4B27` den Assassin-Pathbuilder `FUN_1800D9C40`.
- Ein regulärer Assassin-Pfad im selben Zustandsautomaten setzt das Flag vor seiner Pfadanfrage, was den fehlenden Schreibzugriff im Zustand-106-Nachkampfpfad als Vanilla-Auslassung bestätigt.

## Aktuelle passive Diagnostik

- `X64InlineHook` benötigt für seinen absoluten Sprung immer mindestens 14 Byte. Die frühere Konfiguration mit Hooks bei `0x19772B` und `0x197730` war deshalb trotz deklarierter 5-Byte-Spannen überlappend: Der zweite Hook überschrieb einen Teil des ersten Sprungs einschließlich seiner Zieladresse und verursachte beim Kampfende einen nativen Absturz.
- Der unerreichte Verhaltenshook bei `0x197716` ist entfernt. Der Testmod verändert derzeit weder Register, Unit-Daten noch das Assassin-Kontextflag.
- Ein passiver Hook umfasst den vollständigen 16-Byte-Prolog `0x1853F0–0x1853FF`. Nach dessen Ausführung liegt die Aufruferadresse bei `RSP+0x28`; protokolliert werden insbesondere Kampfverknüpfung, Unit-Status und gespeicherter Zustand.
- Ein zweiter passiver Hook umfasst exakt 14 Byte bei `0x196280–0x19628D`. Diese Spanne enthält sechs Push-Instruktionen. Deshalb liegen die Aufruferadresse bei `RSP+0x30` und die fünfte Pfadoption bei `RSP+0x58`.
- Der Common-Path-Trace erfasst nur lebende Assassinen und zeigt für jede Pfadanfrage Aufrufer-RVA, Ziel, Option, aktuellen AI-Zustand und Assassin-Kontextflag.
- Beide Hooks werden transaktional installiert; bei einem Teilfehler bleiben beide inaktiv.
- Der passive `OnTick`-Beobachter bleibt während der Ingame-Validierung bestehen. Im Map Editor beginnt er mangels zuverlässigem `OnStartMap` beim ersten Simulationstick mit `GameModeHelper.IsMapEditor() == true`.
- Alle temporären Zeilen tragen `[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC]`. Native Ereignisse und Tick-Zustandszeilen sind jeweils auf 256 Einträge pro Karte begrenzt.

Erwartete Folge:

- `state-trace ... aiState=106`
- `combat-finish-entry ... returnRva=0x16DFD8`
- Kampfverknüpfung und beide Resume-Schranken unmittelbar beim Ende der Animation
- `common-path-entry` mit der tatsächlichen Aufrufer-RVA für den Kletter- und den normalen Kontrollfall
- Vergleich von Ziel, Pfadoption und Kontextflag beider Abläufe

## Getestete oder verworfene Ansätze

- Eine zunächst falsche 1-/0-basierte Unit-Auflösung wurde korrigiert, war aber nicht die alleinige Ursache.
- Zustand `122` ist ein Fallback für fehlgeschlagene Gruppenwege und kein allgemeiner Nachkampfzustand.
- Zustand `107` und seine beiden Aufrufe von `FUN_180122800` sind reale Vanilla-Pfade, wurden im reproduzierten Map-Editor-Ablauf aber nicht erreicht.
- `FUN_180122800` bleibt ein realer allgemeiner Wiederaufnahmeweg, ist für diesen Fehler jedoch nicht nachgewiesen und wird nicht mehr gehookt.
- Ein breiter Detour von `FUN_180196280` würde fremde Einheiten und Befehle unnötig erfassen und bleibt entfernt.
- Das Erzwingen eines vollständigen Repaths nach `FUN_1801946A0` adressiert den tatsächlich beobachteten Zustand-106-Ablauf nicht und ist entfernt.
- Eine normale manuelle Flagrestaurierung ist nicht nötig, weil `FUN_180196280` beide relevanten Ausgänge selbst bereinigt.
- Der Hook bei `0x197716` war nach Behebung seiner früheren Überlappung absturzfrei, wurde im reproduzierten Lauf jedoch nicht erreicht und ist deshalb als Verhaltenspatch entfernt.

## Reproduzierbare Ingame-Tests

1. Einen einzelnen Assassin vor einem Kletterweg in einen zufälligen Kampf laufen lassen und den Stillstand bestätigen.
2. Im selben Lauf einen Weg ohne Klettern als fortgesetzten Kontrollfall testen.
3. `combat-finish-entry`, Kampfverknüpfung, Resume-Schranken und sämtliche nach Zustand `106` auftretenden `common-path-entry`-Zeilen vergleichen.
4. Nach Identifikation der tatsächlichen Callsite Hoch- und Herunterklettern sowie mehrere Assassinen erneut prüfen.
