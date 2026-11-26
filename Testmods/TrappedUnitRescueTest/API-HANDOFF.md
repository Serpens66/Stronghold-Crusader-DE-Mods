# Übergabe der isolierten APIShared-Entwicklung

## Aktueller Teststand vom 26.11.2026

Für die laufende autorisierte Singleplayer-Abnahme wird die isolierte SDK-Arbeitskopie auf Branch `codex/rescue-observation`, Sicherungscommit `25a9b35bca8a30de74937856c5f4bfa28fab3aa9`, verwendet. Der Checkout war nach dem vollständigen Build sauber; vor Übernahme Git-Zustand und Buildproof erneut prüfen. Der kanonische Checkout bleibt unverändert.

Der vollständige Callback-SDK-Build bestand mit1794 Tests und einem bestehenden optionalen Skip. Er enthält die korrigierte AILords→Lobby-ID-Konvertierung und die additive vierparametrige StartSkirmish-Überladung mit synchroner Vorbereitung nach fertigem Lobbyaufbau. Die bisherige Überladung bleibt erhalten. Fingerprint `1e5cbfc487e3b96eae6653a2c37d1fdf32c4f31c5e2ab23f28d08aebd5911cb3`; APIShared.dll SHA256 `252F2B633F39C61AE45A23B4399D0C803C9400A739D56CD9FD79FABC8AFCC905`; Bootstrap SHA256 `8DDAB4E337DEBCE6D7D1AD74F6AE5FD58F7296FFC21F22DA7378DE4573AB8FD9`. Nachweis `.work/a/rescue-api/001/.local/build-proof.json`, Buildlog `.work/a/rescue-impl/001/sdk-beforelaunch-full.log`. Öffentliche API gegenüber der kanonischen Entwicklungsbasis +113/-0. Ältere Fingerprints unten sind historische Checkpoints.

Die Zielintegration ist **BugfixesAndQoL**; Pathfinding bleibt Kompatibilitätspartner. Der aktuelle Verbraucherbuild `.work/a/rescue-sp/001/miller-off-build.log` besteht mit64637 Offlineprüfungen. Echte Spielbelege und offene Lücken sind in SP-ACCEPTANCE.md und WORKER-GAME-MATRIX.md getrennt dokumentiert. Vollständige SP-Abnahme, Produktionsintegration und Rückbau des Prototyps sind noch offen; Multiplayer ist außerhalb des aktuellen Testauftrags.

Stand 25.11.2026. Aktive Entwicklung, noch kein Mergeauftrag und kein Installationspaket für Spieler. Die Testmod-Abnahme bleibt unabhängig von der späteren API-Integration.

## Erhaltenswerter Arbeitsstand

Workspace-relative API-Arbeitskopie: `.work/a/rescue-api/001`. Registrierter Git-Worktree, Branch `codex/rescue-observation`, Basis `681f57f36cdd349bc540a52651d81b3effad04a6`. Die API-Entwicklung ist seit dem angeforderten Sicherungscommit vom 25.11.2026 vollständig versioniert: `3eee4849a3bb81f7098415a670f457aa8dfa689d`. Der Commit bleibt auf dem eigenen Entwicklungsbranch und ist kein Merge oder Release. Quellen und die Arbeitskopie für die weitere Integration erhalten. Der Schutzvermerk steht in `.work/INDEX.json` unter `RescueApiDevelopment20261125`.

Der kanonische Checkout `APIShared` wurde zuletzt sauber auf `dev/Serpens66`, HEAD `855516b19236e9c0cd809e182b17b74aa69d0123`, geprüft. Das ist eine Momentaufnahme; vor jeder Integration Branch, HEAD, Diff und weitere Worktrees erneut prüfen. Der Originalcheckout bleibt durch diesen Auftrag unverändert.

Die vollständige Callback-Fehlerisolierung aus diesem Commit ist bereits in der Arbeitskopie enthalten, einschließlich AIV, VisibleMap, Lobby und UnitHud mit ihren Tests. Ein späterer Merge darf sie weder doppelt einführen noch durch ältere eigene Fassungen ersetzen.

## Additive Verträge

- `APIShared.WorldMutations`: 15 qualifizierte Quellen, effektive Originalargumente, private Receipts, äußere Batches/Mirrors, Weltidentität und isolierte Fehlerpfade. Vorhandene Bau-/AIV-Besitzer werden genutzt; terminale Moatwriter sind separat auditiert.
- `APIShared.Units.UnitMoveEvents`: finaler effektiver Originalaufruf am vorhandenen AI-Military-/SE-Besitzer, unveränderliche Native0/1-Ergebnisse, eigenes gebundenes Origin gegenüber unabhängigen fremden Pre-/Post-Kindaufrufen, guardierte eigene Aufrufe und Live-Patchprüfung von Entry samt indirektem Slot.
- Keine Rettungsstrategie, kein Teleport und keine zusätzliche Modsetting im SDK. Gameplayentscheidungen und begrenzte Retrys gehören zum Testmod beziehungsweise später zu Pathfinding.

Öffentliche API gegenüber der Entwicklungsbasis: +112/-0. Details und Callbackgrenzen stehen in `docs/WORLD_MUTATIONS.md`, `docs/UNIT_MOVES.md`, API-Katalog und Updatevertrag der Arbeitskopie.

## Geprüftes Entwicklungs-SDK

Version0.74.0; vollständiger SDK-Build mit1787 bestandenen Tests, einem optional übersprungenen Consumer-Rescan und zusätzlichen nativen/Packaging-Fixtures. Nachweise:

- `.work/a/rescue-api/001/.local/build-proof.json`
- `.work/a/rescue-impl/001/api-full-build.log`
- Fingerprint `9c7432d8b3bb98634d7518d1bfa61065d92ae52b378525d4e1a58d976f29046c`
- `APIShared.dll` SHA256 `67451CB66F4A117D51CF1475709B0E1457F5875D6B598611DB8C246EB84FE764`

Der vorhandene Prüfer `tools/BuildProof.ps1`, Funktion `Assert-ApiBuildProof`, bestätigt aktuelle Quell-/Werkzeug-/Referenzeingaben und den vollständigen Plugin-/Patcher-/Publisherdateisatz. Er wurde nach dem erfolgreichen Build erneut ausgeführt. Beim späteren Weiterarbeiten denselben Prüfer erneut nutzen; keine handgeschätzte Versions-/Dateipaarung.

## Vor einer später autorisierten Übernahme

1. Aktuellen kanonischen APIShared-Stand und diesen gesicherten Entwicklungsbranch vergleichen; gegebenenfalls später hinzugekommene uncommittierte oder ungetrackte Quellen gehören ausdrücklich zum Vergleich. Gleichzeitig entwickelte Dienste und Versionsangaben konfliktfrei zusammenführen.
2. Die vorhandenen Besitzer für AIV, HumanConstruction und AiMilitaryOrder beibehalten. Keinen zweiten nativen Hook an bereits besessenen Stellen erzeugen; semantische Änderungen an Original-/Mirror-/Wrappergrenzen erneut auditieren.
3. Vollständigen API-Build, öffentliche API-Baseline und die betroffenen tatsächlichen Verbraucher gegen den zusammengeführten Stand prüfen. Bisherige Fixtures sind Nachweise für diesen Entwicklungsstand, keine Blankofreigabe eines späteren Merges.
4. Erst bei einem eigenen Installationsauftrag Plugin und Bootstrap-Patcher aus demselben nachgewiesenen Build gemeinsam installieren. Dieser Auftrag installiert nichts.

Die spätere Migration der Rettung nach Pathfinding ist nicht Bestandteil dieses SDK-Merges. Bis dahin bleibt genau dieser Testmod der experimentelle Rettungsbesitzer; Spiel-/Save-/Editor-/Last-/MP-Prüfungen sind in INGAME-PENDING.md aufgelistet.

## P7-Zwischenstand beim Sicherungscommit

Der Rettungs-Testmod enthält inzwischen begonnene P7-Coordinator- und Persistenzänderungen. Der letzte vollständige grüne Gesamtbuild mit61407 Prüfungen liegt vor diesen Änderungen. Die neuen reinen Geometrie-/Intent-Bausteine wurden getrennt geprüft; die Gesamtintegration und der begonnene Persistenztest sind noch nicht abschließend kompiliert oder getestet. Der Sicherungscommit behauptet keine Abnahme dieses Zwischenstands.

## Fortschritt nach dem Sicherungscheckpoint

Der oben beschriebene unvollständige Zustand ist der historische Stand des ersten Sicherungscommits. Inzwischen wurden die integrierten P7-Quellen unabhängig kompiliert und alle tatsächlichen Offline-Fixtures ausgeführt: Geometrie209, frische Absicht928, Persistenz637 und Coordinator853 Prüfungen bestanden; zusätzliche bestehende Suiten ebenfalls bestanden. Nachweis: `.work/a/rescue-impl/001/replan-review-build.log` und `replan-review-tests.log`. Der anschließende Überschneidungsfix einschließlich früher/später fehlgeschlagener Batchteiloperationen ist mit Coordinator919 Prüfungen und dem vollständigen Normalbuild abgeschlossen:64101 Einzelprüfungen plus Fixes-Verträge bestanden. Aktueller Nachweis: `.work/a/rescue-impl/001/offline-completion.json` und `p7-final-build.log`; Umsetzung und Spielgrenzen stehen in IMPLEMENTATION.md. Die SDK-Quellen werden durch diese testmodinterne Ergänzung nicht verändert; der vollständige SDK-Buildnachweis bleibt gegen seine aktuellen Eingaben prüfbar.

Die angeforderten Sicherungscommits im Hauptrepository sind `be28405a4` und `3f8244061`. Das sind lokale Arbeitsstandsicherungen und keine Freigabe des Testmods oder späterer Änderungen.
