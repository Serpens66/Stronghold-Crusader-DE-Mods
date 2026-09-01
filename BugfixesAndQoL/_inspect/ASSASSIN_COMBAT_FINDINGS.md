# Assassin-Kampf-/Kletter-Fortsetzung: Analyseprotokoll

## Referenz und Abgrenzung

- Kanonische DLL: installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Native-Baseline: `_inspect/CrusaderDE-Native-Baseline` für denselben DLL-Hash.
- Der Fix ist seit `BugfixesAndQoL` 1.0.115 direkt in diesem Mod enthalten und wird über die standardmäßig aktive Host-Einstellung `EnableAssassinCombatResumeFix` gesteuert.
- Die Einstellung ist unabhängig von `EnableImprovedAssassinPathfinding`: Allein aktiviert verwendet sie Vanillas Assassin-Builder; gemeinsam aktiviert verwendet sie den gewichteten Builder und dessen Korrekturen für reservierte Kletterflächen.

## Bestätigte Ursache

- Der reproduzierte Nachkampfpfad lautet `Assassin-Zustand 106 → 0x1853F0 → 0x1976C0 → Pfadanfrage 0x196280`.
- `0x1853F0` ruft bei RVA `0x18540D` `0x1976C0` auf. Die Rücksprung-RVA `0x185412` grenzt diesen Ablauf gegenüber anderen Aufrufern ein.
- `0x1976C0` löscht den alten Pfad, stellt gespeicherten Zustand und sekundäres Ziel wieder her und fordert bei RVA `0x19772B` einen neuen Pfad an.
- Vanilla setzt davor das Assassin-Pfadkontextflag bei RVA `0x60AD6E8` nicht. Dadurch wird der normale statt des Assassin-Pathbuilders verwendet und ein Kletterweg nicht wiederhergestellt.
- Am Fixpunkt enthält `RDI` eine 1-basierte Unit-Game-ID. Für `GetUnitsAsSpan()` gilt ausschließlich `spanIndex = unitId - 1`; die frühere direkte Verwendung als Spanindex untersuchte die benachbarte Einheit und ließ den Fix still ausfallen.

## Finaler Patchaufbau

- Ein einzelner `X64InlineHook` liegt bei RVA `0x197716` über exakt 14 Byte.
- Die überschriebenen Vanilla-Instruktionen laufen vollständig vor dem Callback. Der Zustandswrite bei `0x197724` und die Calls bei `0x19772B` und `0x197735` bleiben unverändert im nativen Code.
- Der Hook setzt das Kontextflag nur bei aktivem `BugfixesAndQoL`, aktiver eigener Kampf-Fortsetzungsoption, gültiger 1-basierter Unit-ID, lebendem Assassin, Zustand `106` und Rücksprung-RVA `0x185412`.
- Das Setzen des Flags ist die letzte Callback-Operation. `0x196280` liest das Flag und löscht es auf beiden auditierten Ausgängen selbst; eine manuelle Wiederherstellung ist weder nötig noch erwünscht.
- R3 wird nicht mehr benötigt. Iced bleibt nur als nicht kopierte transitive Compile-Referenz bestehen, weil Zhuqiaomons öffentliche Context-Hook-Signatur `Iced.Instruction` enthält; der Mod verwendet Iced nicht direkt.
- DLL-Hash, Callkette, Hookbytes, Instruktionsgrenzen, Pfad-Call, Flagzugriffe und Auswahl des Assassin-Builders werden vor Installation geprüft. Bei einer Abweichung bleibt Vanilla fail-closed aktiv.

## Erfolgreiche Ingame-Validierung

- Der Map-Editor-Test bestätigte wiederholt `unitId=6 → spanIndex=5`, `callerRva=0x185412` und einen zulässigen Zustand-106-Resume.
- Das Kontextflag wechselte unmittelbar vor `MoveHere` von `0` auf `1`; der Pfadaufruf war erfolgreich und Vanilla setzte es anschließend wieder auf `0`.
- Nach mehreren Kämpfen wurden jeweils neue aktive, kürzere Kletterpfade erzeugt, unter anderem `11 → 9 → 7` sowie `8 → 3 → 2`.
- Der Assassin setzte seinen ursprünglichen Bewegungsbefehl ingame sichtbar fort. Es traten keine Hookfehler, Ausnahmen oder verbliebenen Kontextflags auf.
- Nach dieser Bestätigung wurden State-Machine-Clone, MoveHere-/Kill-Events, OnTick-/Editor-Trace und sämtliche temporären Diagnoseausgaben entfernt.

## Verworfene Ansätze und Sicherheitslehren

- Zustände `107` und `122` sowie `0x122800` gehören zu realen Vanilla-Abläufen, wurden im reproduzierten Fehlerpfad aber nicht erreicht.
- Ein breiter Detour von `0x196280` beeinflusst unnötig andere Pfadanfragen und kollidiert zudem mit dem vorhandenen Script-Extender-Detour.
- Zwei Hooks bei `0x19772B` und `0x197730` überlappten wegen der effektiven Mindestüberschreibung von 14 Byte und verursachten einen nativen Absturz.
- Ein früher Diagnosehook am Einstieg von `0x196280` verletzte wegen seiner Position die Windows-x64-Stackausrichtung vor dem Managed Callback.
- Native Hookspannen müssen daher neben vollständigen Instruktionsgrenzen auch Mindestüberschreibung, Überlappungen, Register-Liveness und ABI-Stackausrichtung erfüllen.

## Regressionstests

1. Kampf vor einem Weg mit Hochklettern und vor einem Weg mit Herunterklettern auslösen.
2. Einzelne und mehrere Assassinen sowie einen normalen Weg ohne Klettern kontrollieren.
3. Beide Assassin-Einstellungen einzeln und gemeinsam prüfen: Der Kampf-Fix muss mit Vanillas sowie mit dem verbesserten Assassin-Builder funktionieren.
4. Bei deaktiviertem Kampf-Fix muss Vanillas Nachkampfverhalten unverändert bleiben; deaktiviertes Klettern darf keine Kletterkante erzwingen.
5. Host und Client müssen denselben wiederaufgenommenen Weg bestimmen.
