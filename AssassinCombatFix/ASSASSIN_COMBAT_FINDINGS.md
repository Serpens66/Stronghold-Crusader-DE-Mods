# Assassin-Kampf-/Kletter-Fortsetzung: Analyseprotokoll

Diese Datei dokumentiert den Wissensstand des Testmods `AssassinCombatFix`. Sie trennt bestätigte Native-Verträge von noch ausstehenden Ingame-Nachweisen und verworfenen Ansätzen.

## Referenz und Abgrenzung

- Kanonische DLL: installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Native-Baseline: `_inspect/CrusaderDE-Native-Baseline/FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Der Testmod ist hart von `BugfixesAndQoL` abhängig und ergänzt ausschließlich dessen aktiviertes `EnableImprovedAssassinPathfinding`.
- Der gewichtete Assassin-Pathbuilder und die Korrekturen für reservierte Kletterflächen bleiben vollständig in `BugfixesAndQoL`.

## Bestätigte Erkenntnisse

### Reproduziertes Verhalten

- Nach einem zufälligen Kampf auf einem Bewegungsweg mit Klettern bleibt der Assassin stehen, obwohl der grüne Zielmarker bestehen bleibt.
- Ohne Kletteranteil setzt Vanilla den ursprünglichen Bewegungsbefehl normalerweise fort.
- Im letzten Map-Editor-Test waren die damaligen Zustand-122-Hooks installiert und Debug-Logging aktiv. Keine einzige ihrer Diagnosezeilen wurde erreicht.
- Auch mit den Hooks im allgemeinen Resume-Helper blieb der Assassin stehen; die bisherige Diagnose blieb leer, weil sie erst nach einer positiven Rücksprungadressen-Prüfung protokollierte. Damit war weder ein unerreichter Helper noch eine falsche Stack-Zuordnung bewiesen.

### Tatsächlicher Nachkampfpfad

- Der Assassin beendet den relevanten Kampfablauf im AI-Zustand `107`.
- Dieser Handler ruft den allgemeinen Resume-Helper `FUN_180122800` an den RVAs `0x16D599` und `0x16D642` auf.
- Die zugehörigen Rücksprungadressen `0x16D59E` und `0x16D647` identifizieren diese beiden Aufrufer innerhalb des gemeinsam genutzten Helpers eindeutig.
- `0x122800` erhält in `EDX` einen 0-basierten Unit-Index und adressiert das Unit-Array mit `index * 0x490`.

### Zwei Resume-Probleme

- `0x122800` ruft bei `0x122AF7` zunächst `FUN_1801946A0` auf. Ein Rückgabewert ungleich null überspringt den vollständigen Repath.
- Ein unterbrochener Kletterpfad kann damit als lokal wiederaufnehmbar gelten, ohne die notwendige Kletterbewegung tatsächlich neu zu aktivieren.
- Bei einem Rückgabewert null ruft Vanilla bei `0x122B0F` `FUN_180196280` auf, setzt vorher aber nicht den Assassin-Pfadkontext.
- Ohne diesen Kontext wählt der Dispatcher nicht den Assassin-Pathbuilder.
- Bei `0x122B14` überschreibt Vanilla das Ergebnis anschließend immer mit `1`; der Aufrufer kann einen fehlgeschlagenen Repath daher nicht erkennen.

### Pfadkontext und sichere Hookgrenzen

- `FUN_180196280` liest das Assassin-Flag bei RVA `0x60AD6E8` und löscht es auf beiden auditierten Ausgängen bei `0x196743` und `0x19676C`.
- Der Dispatcher wählt mit diesem Kontext bei `0xF4B27` den Assassin-Pathbuilder `FUN_1800D9C40`.
- Hook `0x122AFC`, Länge 4 Byte, ersetzt vollständig `test eax,eax` und den kurzen bedingten Sprung. Die Originalinstruktionen laufen nach dem Callback.
- Hook `0x122B14`, Länge 7 Byte, ersetzt vollständig `mov eax,1` und den anschließenden Sprung. Dadurch kann der Callback zuvor das echte Repath-Ergebnis lesen.
- Der Prolog von `0x122800` bewahrt die Rücksprungadresse an beiden Hookpunkten bei `RSP+0x58`.
- Im Zustand-107-Handler ruft `0x16D54D` die Zielprüfung `FUN_18007EB00` auf. Direkt danach liegen `test eax,eax; je ...` bei `0x16D552`; der neue passive Hook deckt genau diese vier Byte ab und verändert Vergleich oder Verzweigung nicht.

## Aktueller Patch und Diagnostik

- Innerhalb `0x122800` wird über die Rücksprungadresse ausschließlich einer der beiden bestätigten Assassin-Kampfaufrufer akzeptiert.
- Zusätzlich werden aktive Mods/Settings, Indexgrenzen, Lebenszustand und Assassin-Typ geprüft.
- Für einen zulässigen Aufruf wird das Ergebnis des kurzen Resumes protokolliert und auf `0` gesetzt. Dadurch führt Vanilla immer genau einen vollständigen Repath aus.
- Vor diesem Repath wird der Assassin-Kontext gesetzt; `0x196280` übernimmt und löscht ihn selbst.
- Vor Vanillas pauschalem Erfolgsergebnis werden der echte Repath-Rückgabewert und der bereinigte Flagwert protokolliert.
- Jeder aufgelöste lebende Assassin wird nun bereits beim Eintritt in `0x122AFC` protokolliert, noch bevor die Rücksprungadresse als bekannte Kampf-Callsite bewertet wird. Das Log enthält die absolute Adresse, RVA und die Rohwerte bei `RSP+0x50`, `+0x58` und `+0x60`.
- Der passive Hook bei `0x16D552` protokolliert den Rückgabewert der Zustand-107-Zielprüfung und den vollständigen Unit-Zustand, ohne Register oder Unit-Daten zu ändern.
- Ein passiver `OnTick`-Beobachter verfolgt lebende Assassins über 0-basierten Index plus `r_GlobalId`. Er protokolliert Zustands-/Pfadänderungen und begrenzte Stillstandsintervalle; Kartenstart und Kartenende verwerfen den kompletten Beobachterzustand.
- Der Map Editor erzeugt eine spielbare Simulation, ohne zuverlässig `OnStartMap` auszulösen. Nach `OnUnloadMap` eröffnet deshalb der erste persistente Simulationstick mit `GameModeHelper.IsMapEditor() == true` automatisch eine neue Diagnose-Epoche. Normale Partien verwenden weiterhin `OnStartMap(Post)`.
- Die untersuchten Script-Extender-Editorereignisse melden nur konkrete Bearbeitungsaktionen. Sie sind kein vollständiger Editor-Lifecycle und werden deshalb nicht als Startsignal verwendet.
- Alle temporären Zeilen tragen `[ASSASSIN_COMBAT_RESUME_DIAGNOSTIC]`. Native Ereignisse sind auf 128 und Tick-Zustandszeilen auf 256 Einträge pro Karte begrenzt.

Erwartete Folge:

- `state-trace ... aiState=...` vom Kampf bis zum Stillstand
- `state107-target-check ... result=...`, falls Zustand 107 diesen Zweig erreicht
- `raw-resume-entry ... stack50=... stack58=... stack60=... knownCombatCaller=...`
- `full-repath-forced ... flagForRequest=1`
- Gewichteter Assassin-Builder aus `BugfixesAndQoL`
- `full-repath-result ... fullRepathCalls=1 ... flagAfterVanilla=0`

## Getestete oder verworfene Ansätze

- Eine zunächst falsche 1-/0-basierte Unit-Auflösung wurde korrigiert, war aber nicht die alleinige Ursache.
- Ein alleiniger Entry-Detour von `0x122800` kann den erfolgreichen Kurzweg nicht dazu zwingen, den vollständigen Repath auszuführen.
- Ein breiter Detour von `0x196280` beeinflusst unnötig andere Einheitentypen und Befehle und bleibt entfernt.
- Zustand `122` ist kein allgemeiner Nachkampfzustand. Er wird beim fehlgeschlagenen Gruppen-Pathfinding als Fallbackzustand gesetzt; die früheren Hooks bei `0x16D2EA` und `0x16D304` adressierten deshalb nicht den reproduzierten Ablauf und sind entfernt.
- Eine manuelle normale Flagrestaurierung ist nicht nötig, weil `0x196280` beide relevanten Ausgänge selbst bereinigt.

## Reproduzierbare Ingame-Tests

1. Einen einzelnen Assassin vor dem Hochklettern in einen zufälligen Kampf laufen lassen.
2. Dasselbe vor einem Weg mit Herunterklettern testen.
3. Beide Fälle mit mehreren gleichzeitig befehligten Assassinen wiederholen.
4. Einen Weg ohne Klettern als Kontrollfall testen.
5. Klettern über den Assassin-Aktionsschalter deaktivieren; es darf keine Kletterkante erzwungen werden.
6. `EnableImprovedAssassinPathfinding` deaktivieren; Vanilla-Verhalten muss vollständig erhalten bleiben.
7. Den Lauf auf Host und Client wiederholen und Diagnose sowie erreichten Weg vergleichen.
