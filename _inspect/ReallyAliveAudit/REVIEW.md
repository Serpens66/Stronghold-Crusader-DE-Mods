# IsReallyAlive: Abschlussprüfung vom 08.10.2026

## Ergebnis

APIShared bietet als einzige neue öffentliche Runtime-API `UnitAccess.GetAllReallyAliveUnits(): int[]`. Die Funktion benutzt Script Extenders unveränderte `QueryUnits`-Abfrage, filtert mit `IsReallyAlive(in GameUnit)` und liefert 1-basierte Game-IDs. `GetAllAliveUnits` ist eine Script-Extender-Funktion und wurde nicht verändert. Die IDs sind eine Momentaufnahme; bestehende spätere Identitäts- und Lebensprüfungen bleiben erhalten.

Gezielte Umstellung von Zielwahl, Befehlen, Bewegung, Heilung, Rittertransformation, Nachschub, HUD-Auswahl und Lord-Prüfungen. Der finale Quelldiff enthält 152 Zeilen mit neuen strengeren Prädikataufrufen und zehn Zeilen mit dem neuen Abfragehelper einschließlich seiner Deklaration. Typ-, Eigentums-, Identitäts- und zusätzliche Gesundheitsbedingungen bleiben bestehen. Snapshot-Namen bleiben bestehen.

Gebaut und installiert: APIShared, BugfixesAndQoL, ExtraFeatures, ImprovedHunters, RandomEvents, StartConditions sowie AIDefenseTest, EnemyGatePathfindingTest, EngineerSiegeFixTest, FormationTest, MoatMove, OutpostTest, SpectatorEditorBuildTest, StockpileAccessFixTest und VirtualUnitsPrototype. Sämtliche Builds liefen über die jeweilige erhöhte `build.bat /nopause`, APIShared zuerst. Alle 15 installierten DLLs stimmen per SHA-256 mit ihren lokalen Paketen überein; siehe `installed-hashes.json`.

## Native Basis und Runtimeverträge

Installierte native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, aktuelle Baseline `sem/FBCB9319`. Der geprüfte Todes-, Dispatch-, Lösch-, Zielwahl- und Rekrutierungspfad bestätigt: AliveState allein kann sterbende Units einschließen; ausschlaggebend ist das Low Word bei GameUnit+0x29C. Das obere Wort ist kein Todeskriterium. Manager-relative Basis 0x65C, Unitstride 0x490, AliveState 0x6E4, Todesmarker 0x8F8. Bestehende RedBird-Verträge wurden gegen die installierte Implementierung geprüft.

Nur die fehlenden WORD-Prüfungen im Workshop-Arbeiterstub und in beiden Bewegungsstubs ergänzt. Cow-Prüfung bereits vorhanden; Hook-Grenzen, Backend, Register-/Flag-Erhaltung und vollständiger Vanilla-Replay bleiben erhalten. Keine neuen Hooks oder ausführbaren Laufzeitmutationen. EngineerSiegeFixTest installiert keine nativen Hooks: Seine vorhandenen Live-Proof-/Recovery-Schreibpfade prüfen nun Gerät und alle Engineers vor dem ersten Schreiben mit dem Helper. Dafür ist APIShared als Kompilier- und BepInEx-Abhängigkeit ergänzt.

JSON-, Lifecycle- und dauerhafte Callback-Verwurzelung vor Änderungen geprüft. Keine neuen Spielassembly-Mitglieder; alle 15 geänderten Runtimeprojekte maschinell gegen die echte Assembly-CSharp geprüft. Bekannte unveränderte HEAD-Zugriffe auf MissionLifecycleCapability.gameLocalPlayerID und FormationTest.MainViewModel.instance sind separat in `final-real-source.log` ausgewiesen. Keine neuen Sichtbarkeits-/Signaturfehler. Bestehende private Unsafe-Kontextfehler und zwei ungültige VirtualUnits-Ausdrucksstatements minimal korrigiert, um die vorgesehenen Builds zu ermöglichen.

## Bewusste Ausnahmen und zweite Prüfung

- Gebäude, Projektile und Gruppen behalten ihre eigenen Zustandsverträge. Die erneute Typprüfung hat insbesondere einen Projectile-`target` erkannt und dessen ursprüngliche Prüfung wiederhergestellt.
- UnitLimit-/BuildingLimit-Caches zählen belegte Slots einschließlich NeedsInit. StartConditions löscht weiterhin auch sterbende belegte Soldatenslots, zählt für neue Aktionen aber nur wirklich lebende Soldaten.
- Spawninitialisierung behält NeedsInit. Outpost-Rally und abschließende VirtualUnits-Statfreigabe verwenden für die fertige Unit den strengeren Zustand.
- Tote Jagdbeute bleibt verfügbar. Reine Hunter-Zustandsdiagnostik behält rohe Zustände; Diagnoseklassen, die tatsächlich Ziele oder Bewegung verändern, prüfen strenger.
- VirtualUnits erhält Leicheninstanzen für die Darstellung. Aktions-/HUD-Queries lehnen sterbende Units ab, ohne deren visuelle Registrierung zu löschen. Globale Identitätswechsel invalidieren weiterhin den alten Slot.
- Script Extender und fremder Fixes-Mod unverändert. README-Dateien, Modversionen und Manifeste unverändert.

`inventory.json` wurde mit dem finalen Quelldiff abgeglichen; Zeilennummern und `original` beziehen sich auf HEAD vor der Änderung. Zusätzliche numerische Engineer-Aktionsprüfungen und VirtualUnits-Spawnfreigabe sind oben dokumentiert. `migrate.py` ist das historische Analysewerkzeug und soll nicht erneut auf die bereits migrierten Quellen angewandt werden.

## Validierung und offene Spieltests

Bestanden: Lebenszustände, Low-/High-Word-Marker, NeedsInit, Nullpointer, Queryfilter und 1-basierte IDs; Identitätswechsel-/Slotwiederverwendung in bestehenden Command-/VirtualRegistry-Tests; JSON/Lifecycle/Hook/XAML/CRLF-Regressionsprüfungen; echter Workshop-Stub mit 1227 Checks; 1478 Command-Checks; 35 tatsächliche NativeX64-Indirect-Detours; vollständige produktive Bewegungs-Generatoren assembliert und vollständig dekodiert; Bewegungsparität einschließlich Todesmarker; 4554 Lord-/Session-Checks; 773 Engineer-Checks; modbezogene Build-Tests. Keine neuen Buildfehler. Vorhandene Warnungen über mehrfach eingebundene Shared-Typen und Assemblyversionen bleiben bestehen.

Noch im Spiel zu prüfen: Sterben während Befehlen, Rittertransformation und Heilung; Rally nach Spawn; tote Jagdbeute; VirtualUnits-Leichendarstellung; Workshop und Bewegung mit aktiven Einstellungen; Engineer-Besatzungs-Recovery. Automatisierte Prüfungen ersetzen diese Gameplaybeobachtung nicht. Alle Prüflogs liegen in diesem Ordner.
