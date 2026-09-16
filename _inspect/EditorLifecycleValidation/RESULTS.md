# Gemeinsamer Editor-Lifecycle – Validierung

Stand: 2026-09-16. Implementierung und automatisierte Prüfungen abgeschlossen; interaktive Spieltests stehen aus.

## Vertrag und Integration

- APIShared stellt `IEditorMapLifecycleCapability` bereit: unveränderliche Ready-/Ended-Benachrichtigungen, prozessweite Sitzungsnummern, Created/Loaded, Dateipfad, Endgrund und gekennzeichnetes Replay.
- Managed-Hooks umfassen vollständige Editor-Erstellung/Ladung, den verschachtelten Erstellungsrückgabewert und tatsächliche Bildschirmwechsel. Keine zusätzlichen nativen Detours und keine periodische Kartenstarterkennung.
- Shared unterscheidet EditorCreated/EditorLoaded von NewMap/LoadedSave. Der Editor-Gate-Callback läuft vor Fach-Subscribern, auch bei synchroner Registrierung mit Replay. Das frühe native Editor-OnLoadSave veröffentlicht keine zweite Sitzung.
- CastlePlanner, ExtraFeatures und BugfixesAndQoL nutzen die gemeinsame Quelle. Ersetzte Editor-Startdetektoren sind entfernt; HUD-, Spieler- und Gebäudebereitschaft bleiben featurebezogen.
- Alle 19 Shared-Lifecycle-Verbraucher besitzen APIShared-Projektverweise, harte Pluginabhängigkeiten und passende Release-/Updateinventare. AIAttackTest wurde ohne Aktivierung automatischer Updatebuilds im Inventar ergänzt.
- AGENTS.md und die semantische Baseline enthalten den Vertrag. README-Dateien, Mod-/Assemblyversionen, ElevatedMoat-Dateien und der Script-Extender-Fork wurden nicht verändert.

## Prüfungen

- APIShared-Tests bestanden: Erstellung und Laden, alle vier Created/Loaded-Folgen, identische Eingaben, genau ein Ready, fehlgeschlagene Rückgabe, unveränderte Vanilla-Exception, genau ein Originalaufruf, verschachtelte/redundante Unloads, abgebrochene Operation, isolierte Beobachterfehler, späte/reentrante Registrierung, doppelte Registrierungs-ID, kein Replay beendeter Sitzungen, geordnete reentrante Veröffentlichungen sowie Menü/Gameplay bei stehengebliebenem Editorflag.
- Shared-/HostClientPresetTests bestanden: Gate vor Fach-Subscriber, getrennte Editorarten ohne künstliche Gameplay-Argumente, kein frühes/doppeltes Editor-Save-Ereignis, Replay, Ende und bestehende normale Save-/Spielstartabläufe.
- Release-Status-Tests und die vorhandenen modbezogenen Tests der Buildtreiber bestanden. MoatMove-Standaloneprüfungen inklusive historischer Quellverträge und installiertem RedBird-Decoder bestanden; keine Testhooks installiert.
- Maschinenprüfungen für JSON-/Lifecycleverbote, CRLF, Projektstruktur, Abhängigkeiten, Inventarabdeckung und entfernte Startdetektoren bestanden. `git diff --check` ohne Befund.
- Zweite Codeprüfung umfasste insbesondere Hook-Signaturen, Initialisierung/Rollback und Verwurzelung, Fehler-/Exceptionpfade, Gate-Reihenfolge, Reentranz, Sitzungstausch, Replay, modseitige Startressourcen sowie verbleibende direkte Kartenereignisse. Ein Szenenwechsel beendet die Sitzung auch dann, wenn nach dem Wechsel weitere Vanilla-Initialisierung fehlschlägt.

## Builds und Installation

Alle Runtime-Builds liefen ausschließlich über die jeweiligen erhöht ausgeführten `build.bat /nopause`, APIShared zuerst. Vor den Builds lief der maschinelle Präflight. Alle 20 Runtime-Projekte wurden erfolgreich gebaut.

17 installierte Pakete sind per DLL-SHA-256 mit dem jeweiligen lokalen Build abgeglichen: APIShared, AIAttackTest, ActiveAIVDetector, BuildingCosts, BuildingLimit, CastlePlanner, CheatMod, ExtendedData, ExtraFeatures, ExtremePowers, ImprovedHunters, RandomEvents, SerpsModsHost, StartConditions, UnitCosts, UnitLimit und BugfixesAndQoL.

EnemyGatePathfindingTest, MoatMove und SkinTest wurden mit dem neu ergänzten `/noinstall` gebaut. Ihre Pluginordner im Spielverzeichnis bleiben nachweislich nicht vorhanden. Build- und Testlogs liegen neben diesem Bericht.

Es bleiben Compilerwarnungen außerhalb des neuen Lifecycle-Codes, insbesondere der Mono.Cecil-Referenzkonflikt in BugfixesAndQoL und unbenutzte Felder/Nullable-Anmerkungen bei MoatMove. Keine Buildfehler.

## Noch im Spiel zu bestätigen

Die automatisierten Zustands- und Quelltests ersetzen keinen interaktiven Unity-/Mono-Test der tatsächlich installierten Managed-Hooks. Optionsdialoge kehren laut geprüftem Vanilla-Managedcode vor GoToScreen zurück; dieses Verhalten wurde noch nicht interaktiv ausgeführt.

1. Neue Karte erstellen, erneut mit gleicher Größe und Spieler-ID erstellen; jede erfolgreiche Initialisierung erhält genau eine neue Sitzungsnummer.
2. Neu → Laden, Laden → Neu und Laden → Laden durchführen, einschließlich derselben Datei. Gate-Log muss vor der jeweiligen Featureinitialisierung erscheinen.
3. Fehlerhafte/nicht ladbare Karte wählen: kein Ready und keine weiter aktive vorherige Sitzung.
4. Editor → Menü und Editor → Gameplay testen: genau ein Ended für die alte Sitzung. Optionen und überlagerte Dialoge öffnen/schließen: kein Ended.
5. Blueprint-/Gatehouse-/Bugfix-Funktionen nach Erstellung und Laden sichtbar prüfen; dabei auch Editor-Spieler wechseln und auf den ersten HUD-/GameState-Aufbau warten.
6. Normalen Gameplay-Neustart und Save-Laden prüfen: keine zusätzlichen Startressourcen oder Burgenstarts durch Editorereignisse.

Für die Auswertung den neuen BepInEx-Startabschnitt sowie `EditorMapReady`, `EditorMapEnded`, Sitzungsnummern und `gameplay-mod gate` korrelieren. Der Native-/Managed-/Extender-Audit ist in der verlinkten semantischen Baseline dokumentiert.
