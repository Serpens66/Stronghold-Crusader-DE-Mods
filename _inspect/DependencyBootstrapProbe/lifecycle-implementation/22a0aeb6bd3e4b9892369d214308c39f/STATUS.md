# Unabhängiger Paketlebenszyklus — Abschluss 17.10.2026

Die Offline-Stufe ist bestanden. APIShared wurde weder bearbeitet noch gebaut; keine Installation und kein Start von Spiel oder Steam. Die installierte Korrekturprobe einschließlich Schaltern blieb unverändert.

## Implementierung

Eigenständige Komponenten unter Testmods/DependencyBootstrapProbe/package: Pair.ps1 (Paarprüfung und Aktivierungsentscheidung), Removal.ps1 (Receipt und erneute Eigentumsprüfung), Invoke-PostExitRemoval.ps1 (frischer Worker, standardmäßig Vorschau), VerifiedFileDeletion.cs (Hash und Löschmarkierung auf demselben gesperrten Windows-Dateihandle). CONTRACT.md beschreibt Zuständigkeiten und Grenzen.

Die aktiven Tests verwenden keinen APIShared-Import und kein zweites Paketmodell. Die früheren Modellquellen liegen im Wurzelverzeichnis dieses Archivs als Historie. Probe-GUID und feste Probe-Dateipfade sind die einzigen Ziele. Der Worker verweigert alle Wurzeln außerhalb des dedizierten Workspace-Testverzeichnisses.

## Nachweise

- 96 Fälle in Windows PowerShell 5.1: run-70ca4d2d1a0f45f98753eb3cb47b5246/results.json.
- Dieselben 96 Fälle in PowerShell 7: run-390ffe661e494cc0b33cef74c8a3fd72/results.json.
- Alle sechs Erstinstallations-/Update-Kopierreihenfolgen und Unterbrechungen; fehlende/veränderte/vertauschte Dateien; falscher Gewinner; konkurrierender Hookbesitzer und umbenannte identische Bootstrapdatei.
- Entfernung im frischen Prozess nach nachgewiesenem Prozessende; Vorschau, neue/alte Ownershipzustände, Staging, Dateisperren, geänderte Datei nach Planung, manipuliertes Paket, Hardlinks, Junction nach Planung, Wiederholung und vollständige Wiederherstellung.
- Tatsächliche bestehende DMD-/Inventar-/Negativtests bestanden: runtime-contract-tests.log. Kein Hook installiert und keine originale Prüfung aufgerufen.
- Bestehende Logprüfertests bestanden: log-checker-tests.log.
- Alle fünf installierten Dateien/Schalter stimmen vor und nach der Arbeit in SHA-256 überein: installed-before.json und installed-after.json.
- APIShared-Gitstatus war vorher leer und ist nachher leer (vorherige leere Pipeline erzeugte keine Datei). Keine APIShared-Schreibbefehle wurden ausgeführt.
- Quellstand und Hashes: probe-source-snapshot/ und probe-source-hashes.json. Jeder abschließende Testlauf enthält zusätzlich seine exakten Paketquellen und Paketbytes.

## Bewusst noch offen

Die Paar-/Aktivierungslogik ist eigenständiger ausführbarer Offline-Code, noch kein in Unity eingebauter Adapter. Auswahl, Abonnement und Ownership werden als explizite Fixture-Daten eingespeist. Der Worker ist kein aktivierter Wartungsdienst. Es gibt keinen automatischen Selbstreparatur-/Update-/Abbestellpfad im Spiel.

Der lokale Extender-Commit 10628df85cd432184742a50a1dee3969a66cf89e wurde nur gelesen (Quellhashes separat archiviert). Seine Abbestelllogik plant Pluginordner; sein Updater löscht zuerst, kopiert danach und startet Steam. Gleiche oder ältere Versionsnummern lösen kein reguläres Update aus. Diese Quellenprüfung ist kein Live-Nachweis.

Die Dateiidentität beim Löschen wird über dasselbe Handle geschützt; Änderungen der Abonnement-/Stagingzustände benötigen zusätzlich ein koordiniertes Live-Protokoll. Linux/Proton und die endgültigen APIShared-Assemblies sind nicht abgenommen. Paketrevision B besteht absichtlich aus synthetisch geänderten Bytes und ist keine ausführbare Modversion.

## Nächster sinnvoller Schritt

Einen rein beobachtenden Testmod-Adapter für echte Gewinner-, Abonnement-, konkurrierende Eigentümer- und Stagingdaten vorbereiten. Zuerst diese Eingaben gegen die erwarteten Entscheidungen prüfen; noch keine automatische Löschung freischalten. Vor einer Installation die funktionierende Probe inklusive Schaltern sichern. Danach getrennte Aktivierungs-, echte Update- und Post-Exit-Entfernungsversuche vorbereiten. Der Nutzer muss für diese abgeschlossene Offline-Stufe keinen Spielstart durchführen.
