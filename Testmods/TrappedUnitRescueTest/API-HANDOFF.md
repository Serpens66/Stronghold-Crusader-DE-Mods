# Übergabe der isolierten APIShared-Entwicklung

Stand 25.11.2026. Aktive Entwicklung, noch kein Mergeauftrag und kein Installationspaket für Spieler. Die Testmod-Abnahme bleibt unabhängig von der späteren API-Integration.

## Erhaltenswerter Arbeitsstand

Workspace-relative API-Arbeitskopie: `.work/a/rescue-api/001`. Registrierter Git-Worktree, Branch `codex/rescue-observation`, Basis `681f57f36cdd349bc540a52651d81b3effad04a6`. Quellen einschließlich ungetrackter neuer Dateien erhalten. Kein Reset, Stash oder pauschales Bereinigen dieses Ordners; die Entwicklung ist noch nicht durch einen Commit gesichert. Der Schutzvermerk steht in `.work/INDEX.json` unter `RescueApiDevelopment20261125`.

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

1. Aktuellen kanonischen APIShared-Stand und diese uncommittierte Arbeitskopie vergleichen; ungetrackte Quellen gehören ausdrücklich zum Vergleich. Gleichzeitig entwickelte Dienste und Versionsangaben konfliktfrei zusammenführen.
2. Die vorhandenen Besitzer für AIV, HumanConstruction und AiMilitaryOrder beibehalten. Keinen zweiten nativen Hook an bereits besessenen Stellen erzeugen; semantische Änderungen an Original-/Mirror-/Wrappergrenzen erneut auditieren.
3. Vollständigen API-Build, öffentliche API-Baseline und die betroffenen tatsächlichen Verbraucher gegen den zusammengeführten Stand prüfen. Bisherige Fixtures sind Nachweise für diesen Entwicklungsstand, keine Blankofreigabe eines späteren Merges.
4. Erst bei einem eigenen Installationsauftrag Plugin und Bootstrap-Patcher aus demselben nachgewiesenen Build gemeinsam installieren. Dieser Auftrag installiert nichts.

Die spätere Migration der Rettung nach Pathfinding ist nicht Bestandteil dieses SDK-Merges. Bis dahin bleibt genau dieser Testmod der experimentelle Rettungsbesitzer; Spiel-/Save-/Editor-/Last-/MP-Prüfungen sind in INGAME-PENDING.md aufgelistet.
