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
- Im jüngsten fehlerhaften Kletterfall bleibt der alte Pfad bei Position/Länge `6/12`, verliert seine aktiven Flags und wird anschließend nicht mehr verfolgt. Das gespeicherte Ziel bleibt erhalten.
- Ein normaler Kontrollweg ersetzt den unterbrochenen Pfad nach mehreren Kämpfen dagegen schrittweise durch kürzere Pfade (`14 → 8 → 7 → 5`) und erreicht sein Ziel.
- Die zuvor untersuchten Hooks in Zustand `107`, Zustand `122` und `FUN_180122800` wurden in diesem Ablauf nicht erreicht.

### Statisch bestätigter Zustand-106-Pfad

- Der Assassin-Zustandsautomat `FUN_18016CD70` behandelt Zustand `106` und kann nach abgeschlossenem Animationszweig bei RVA `0x16DFD3` `FUN_1801853F0` aufrufen.
- `FUN_1801853F0` ruft bei RVA `0x18540D` `FUN_1801976C0` auf. Die Rücksprung-RVA `0x185412` identifiziert diesen Kampfpfad gegenüber den anderen Aufrufern von `0x1976C0`.
- Der Prolog von `FUN_1801976C0` legt diese ursprüngliche Rücksprungadresse am Pfad-Callsite bei `RSP+0x38` ab.
- `FUN_1801976C0` löscht die bisherigen Pfadflags, stellt den gespeicherten AI-Zustand aus Unit-Offset `0x91E` wieder her und übernimmt die sekundären Zielkoordinaten aus `0x744/0x746`.
- Danach ruft es bei RVA `0x19772B` die gemeinsame Pfadroutine `FUN_180196280` auf und bei `0x197735` die Nachbearbeitung `FUN_180196810`.
- Am Call `0x19772B` enthält `RDI` die 1-basierte Unit-Game-ID; `R8D/R9D` enthalten die gespeicherten Zielkoordinaten. Für `GetUnitsAsSpan()` muss exakt einmal `spanIndex = unitId - 1` gerechnet werden.
- Der vorherige Hook bei `0x197716` und die Script-Extender-Events wurden durch eine falsche direkte Verwendung dieser Game-ID als Spanindex still verworfen. Der jüngste Log belegt denselben Vertrag mit `rawAttacker=7` für den Assassin am Spanindex `6`.
- Vor dem inneren Block existieren zwei relevante Schranken: `FUN_1801853F0` ruft `FUN_1801976C0` nur bei `r_AttackingUnitId == 0` auf; `FUN_1801976C0` verarbeitet die Einheit nur, wenn das niederwertige Word von `GameUnit.N0000019A` null und die Einheit nicht tot ist.

### Fehlender Assassin-Kontext

- Vor dem Call bei `0x19772B` setzt Vanilla das Assassin-Pfadkontextflag bei RVA `0x60AD6E8` nicht.
- `FUN_180196280` liest dieses Flag bei `0x1964EE` und löscht es auf beiden auditierten Ausgängen bei `0x196743` und `0x19676C`.
- Der Dispatcher wählt mit gesetztem Kontext bei `0xF4B27` den Assassin-Pathbuilder `FUN_1800D9C40`.
- Ein regulärer Assassin-Pfad im selben Zustandsautomaten setzt das Flag vor seiner Pfadanfrage, was den fehlenden Schreibzugriff im Zustand-106-Nachkampfpfad als Vanilla-Auslassung bestätigt.

## Aktuelle passive Diagnostik

- `X64InlineHook` benötigt für seinen absoluten Sprung immer mindestens 14 Byte. Die frühere Konfiguration mit Hooks bei `0x19772B` und `0x197730` war deshalb trotz deklarierter 5-Byte-Spannen überlappend: Der zweite Hook überschrieb einen Teil des ersten Sprungs einschließlich seiner Zieladresse und verursachte beim Kampfende einen nativen Absturz.
- Der sichere Einzelhook bei `0x197716` ist mit korrekter Unit-ID-Konvertierung wieder aktiv. Er setzt ausschließlich für den bestätigten Zustand-106-Aufrufer bei `0x185412` das fehlende Assassin-Kontextflag.
- Seine exakt 14 Byte lange Spanne enthält nur `mov [rbx+0x74E],cx`, `mov [rsp+0x20],ecx` und `mov rcx,rsi`. Diese Instruktionen laufen vollständig vor dem Callback; `RDI` bleibt als Unit-ID erhalten. Zustandswrite `0x197724` und Calls `0x19772B/0x197735` bleiben im nativen Block unverändert.
- Der Context-Hook sichert alle Register und Flags. Sein einziger Spielzustandsschreibzugriff betrifft das globale Assassin-Kontextflag; Zielkoordinaten, Pfadoption, Stack und Unit-Felder bleiben unverändert.
- Die späteren passiven Hooks bei `0x1853F0` und `0x196294` sind ebenfalls entfernt: Der erste wurde im reproduzierten Fehler nicht erreicht; `0x196280` wird bereits vom Script Extender für `OnUnitMoveHere` detourt, sodass ein zusätzlicher Hook innerhalb des ursprünglichen Funktionskörpers kein verlässlicher Beobachtungspunkt ist.
- Der erste Common-Path-Diagnosehook bei `0x196280–0x19628D` war trotz vollständiger Instruktionsgrenzen nicht ABI-sicher: Er führte vor dem Managed Callback nur sechs Pushes aus. Der Hook-Generator subtrahiert anschließend ausschließlich Vielfache von 16 und korrigiert das dadurch verbleibende `RSP mod 16 == 8` nicht. Der Prozess stürzte deshalb beim ersten Pfadaufruf vor der Callback-Ausgabe ab.
- Die Native-Baseline bestätigt den Assassin als `eChimps`-Index `73`, dessen VTable-Eintrag bei RVA `0x321EF8` auf `FUN_18016CD70` zeigt. Die Funktion ist exakt 5625 Byte lang und enthält 19 vollständige Schreibinstruktionen auf das AI-State-Feld `GameUnit+0x918`.
- Ein einzelner `X64FunctionCloneHook` instrumentiert genau diese 19 bestätigten Schreibstellen. Jede Diagnose enthält die ursprüngliche Schreib-RVA sowie alten und vorgeschlagenen Zustand; die originale Schreibinstruktion wird unverändert ausgeführt.
- Vor der Installation werden DLL-Hash, VTable-Zuordnung, Live-Eintrittsbytes, Funktionsgröße und die vollständige Liste der 19 Schreib-RVAs geprüft. Jede Abweichung lässt den Testmod fail-closed inaktiv.
- Pfadanfragen werden konfliktfrei über `UnitR3EventHooks.OnUnitMoveHere` in Pre und Post erfasst. Zusätzlich korreliert `OnUnitKilledByMelee` den Todeszeitpunkt mit dem Assassin-Zustand.
- Clone- und Verhaltenshook werden transaktional installiert; Event-Abonnements werden erst nach erfolgreichem Commit aktiviert. Beide Hookobjekte müssen zusätzlich tatsächlich aktiv sein.
- Der passive `OnTick`-Beobachter bleibt während der Ingame-Validierung bestehen. Im Map Editor beginnt er mangels zuverlässigem `OnStartMap` beim ersten Simulationstick mit `GameModeHelper.IsMapEditor() == true`.
- Alle temporären Zeilen tragen `[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC]`. Native Ereignisse und Tick-Zustandszeilen sind jeweils auf 256 Einträge pro Karte begrenzt.

Erwartete Folge:

- `state-trace ... aiState=106`
- `melee-kill ...` mit Kampfverknüpfung und vollständigem Unit-Zustand
- `post-combat-path-context ... unitId=7, spanIndex=6, callerRva=0x185412, eligible=True`
- `state-write ... siteRva=... oldState=106, proposedState=101`, falls der Assassin-Handler selbst den Zustand wechselt
- `move-here phase=Pre/Post ...` mit Ziel, Pfadoption, Rückgabewert und Kontextflag `1 → 0`
- Vergleich der exakten Schreib-RVA und der MoveHere-Folge zwischen Kletter- und normalem Kontrollfall

## Getestete oder verworfene Ansätze

- Frühere Diagnosen behandelten 1-basierte Unit-Game-IDs fälschlich als 0-basierte Spanindizes. Heuristische Doppelauflösungen sind verworfen; es gilt ausschließlich `spanIndex = unitId - 1`.
- Zustand `122` ist ein Fallback für fehlgeschlagene Gruppenwege und kein allgemeiner Nachkampfzustand.
- Zustand `107` und seine beiden Aufrufe von `FUN_180122800` sind reale Vanilla-Pfade, wurden im reproduzierten Map-Editor-Ablauf aber nicht erreicht.
- `FUN_180122800` bleibt ein realer allgemeiner Wiederaufnahmeweg, ist für diesen Fehler jedoch nicht nachgewiesen und wird nicht mehr gehookt.
- Ein breiter Detour von `FUN_180196280` würde fremde Einheiten und Befehle unnötig erfassen und bleibt entfernt.
- Das Erzwingen eines vollständigen Repaths nach `FUN_1801946A0` adressiert den tatsächlich beobachteten Zustand-106-Ablauf nicht und ist entfernt.
- Eine normale manuelle Flagrestaurierung ist nicht nötig, weil `FUN_180196280` beide relevanten Ausgänge selbst bereinigt.
- Die frühere Schlussfolgerung, der Hook bei `0x197716` werde nicht erreicht, beruhte auf dieser falschen ID-Auslegung und ist verworfen.
- Der Common-Path-Einstiegshook bei `0x196280` ist wegen der nicht ausgerichteten Managed-Callback-Stacklage verworfen. Vollständige Instruktionsgrenzen allein reichen für Context-Hooks nicht.
- Auch der spätere Hook bei `0x196294` ist verworfen, weil der Script Extender bereits den Funktionseinstieg von `0x196280` detourt und über `OnUnitMoveHere` den dafür vorgesehenen konfliktfreien Beobachtungspunkt bereitstellt.

## Reproduzierbare Ingame-Tests

1. Einen einzelnen Assassin vor einem Kletterweg in einen zufälligen Kampf laufen lassen und den Stillstand bestätigen.
2. Im selben Lauf einen Weg ohne Klettern als fortgesetzten Kontrollfall testen.
3. `melee-kill`, `post-combat-path-context`, `state-write` und sämtliche nach Zustand `106` auftretenden `move-here`-Zeilen vergleichen.
4. Nach erfolgreicher Fortsetzung Hoch- und Herunterklettern sowie mehrere Assassinen erneut prüfen.
