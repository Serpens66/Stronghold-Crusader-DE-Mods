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
- Beim Übergang aus Zustand `106` wird der aktive Pfad gelöscht. Das gespeicherte Ziel bleibt erhalten, aber Vanilla erzeugt ohne Assassin-Kontext keinen verwendbaren Kletterweg.
- Die zuvor untersuchten Hooks in Zustand `107`, Zustand `122` und `FUN_180122800` wurden in diesem Ablauf nicht erreicht.

### Tatsächlicher Nachkampfpfad

- Der Assassin-Zustandsautomat `FUN_18016CD70` behandelt Zustand `106` und ruft bei RVA `0x16DFD3` `FUN_1801853F0` auf.
- `FUN_1801853F0` ruft bei RVA `0x18540D` `FUN_1801976C0` auf. Die Rücksprung-RVA `0x185412` identifiziert diesen Kampfpfad gegenüber den anderen Aufrufern von `0x1976C0`.
- Der Prolog von `FUN_1801976C0` legt diese ursprüngliche Rücksprungadresse am Pfad-Callsite bei `RSP+0x38` ab.
- `FUN_1801976C0` löscht die bisherigen Pfadflags, stellt den gespeicherten AI-Zustand aus Unit-Offset `0x91E` wieder her und übernimmt die sekundären Zielkoordinaten aus `0x744/0x746`.
- Danach ruft es bei RVA `0x19772B` die gemeinsame Pfadroutine `FUN_180196280` auf und bei `0x197735` die Nachbearbeitung `FUN_180196810`.
- Am Call `0x19772B` enthält `RDI` den 0-basierten Unit-Index; `R8D/R9D` enthalten die gespeicherten Zielkoordinaten.

### Fehlender Assassin-Kontext

- Vor dem Call bei `0x19772B` setzt Vanilla das Assassin-Pfadkontextflag bei RVA `0x60AD6E8` nicht.
- `FUN_180196280` liest dieses Flag bei `0x1964EE` und löscht es auf beiden auditierten Ausgängen bei `0x196743` und `0x19676C`.
- Der Dispatcher wählt mit gesetztem Kontext bei `0xF4B27` den Assassin-Pathbuilder `FUN_1800D9C40`.
- Ein regulärer Assassin-Pfad im selben Zustandsautomaten setzt das Flag vor seiner Pfadanfrage, was den fehlenden Schreibzugriff im Zustand-106-Nachkampfpfad als Vanilla-Auslassung bestätigt.

## Aktueller Patch und Diagnostik

- Der Pre-Hook ersetzt exakt den fünf Byte langen Call bei `0x19772B`. Der Callback läuft zuerst; anschließend wird der unveränderte Call ausgeführt.
- Der Kontext wird nur bei aktiven Mods/Settings, gültigem 0-basiertem Index, lebendem Assassin und ursprünglicher Rücksprung-RVA `0x185412` gesetzt.
- Ein bereits gesetztes Flag wird nicht überschrieben. Die gemeinsame Pfadroutine übernimmt dessen normale Bereinigung; es gibt keine manuelle Wiederherstellung im Erfolgsablauf.
- Der Post-Hook deckt exakt `mov edx,edi; mov rcx,rsi` bei `0x197730–0x197734` ab. Er protokolliert das Pfadergebnis und den erwarteten bereinigten Flagwert, bevor Vanilla `0x196810` aufruft.
- Beide Hooks werden atomar installiert und bei einem Teilfehler vollständig zurückgerollt.
- Der passive `OnTick`-Beobachter bleibt während der Ingame-Validierung bestehen. Im Map Editor beginnt er mangels zuverlässigem `OnStartMap` beim ersten Simulationstick mit `GameModeHelper.IsMapEditor() == true`.
- Alle temporären Zeilen tragen `[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC]`. Native Ereignisse sind auf 128 und Tick-Zustandszeilen auf 256 Einträge pro Karte begrenzt.

Erwartete Folge:

- `state-trace ... aiState=106`
- `post-combat-path-entry ... returnRva=0x185412 ... eligible=True`
- `post-combat-path-context ... contextSet=True ... flagForRequest=1`
- gewichteter Assassin-Pathbuilder aus `BugfixesAndQoL`
- `post-combat-path-result ... result=1 ... flagAfterVanilla=0`
- ein weiter fortschreitender Pfad bis zum gespeicherten Ziel

## Getestete oder verworfene Ansätze

- Eine zunächst falsche 1-/0-basierte Unit-Auflösung wurde korrigiert, war aber nicht die alleinige Ursache.
- Zustand `122` ist ein Fallback für fehlgeschlagene Gruppenwege und kein allgemeiner Nachkampfzustand.
- Zustand `107` und seine beiden Aufrufe von `FUN_180122800` sind reale Vanilla-Pfade, wurden im reproduzierten Map-Editor-Ablauf aber nicht erreicht.
- `FUN_180122800` bleibt ein realer allgemeiner Wiederaufnahmeweg, ist für diesen Fehler jedoch nicht nachgewiesen und wird nicht mehr gehookt.
- Ein breiter Detour von `FUN_180196280` würde fremde Einheiten und Befehle unnötig erfassen und bleibt entfernt.
- Das Erzwingen eines vollständigen Repaths nach `FUN_1801946A0` adressiert den tatsächlich beobachteten Zustand-106-Ablauf nicht und ist entfernt.
- Eine normale manuelle Flagrestaurierung ist nicht nötig, weil `FUN_180196280` beide relevanten Ausgänge selbst bereinigt.

## Reproduzierbare Ingame-Tests

1. Einen einzelnen Assassin vor dem Hochklettern in einen zufälligen Kampf laufen lassen.
2. Dasselbe vor einem Weg mit Herunterklettern testen.
3. Beide Fälle mit mehreren gleichzeitig befehligten Assassinen wiederholen.
4. Einen Weg ohne Klettern als Kontrollfall testen.
5. Klettern über den Assassin-Aktionsschalter deaktivieren; es darf keine Kletterkante erzwungen werden.
6. `EnableImprovedAssassinPathfinding` deaktivieren; Vanilla-Verhalten muss vollständig erhalten bleiben.
7. Den Lauf auf Host und Client wiederholen und Diagnose sowie erreichten Weg vergleichen.
