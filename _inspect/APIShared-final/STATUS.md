# APIShared-Abhängigkeitsfix: abgeschlossen

17.10.2026: beide vereinbarten Spielnachweise bestanden. Getestetes Paket
wiederhergestellt, eigene Kontrollen entfernt. Kein weiterer Spielversuch erforderlich.

- Aktiv 22:07: gültiges Paar/Gewinner, beide Hooks/JIT, ein ergänzter Anbieter,
  unveränderter Originalindex, genau drei echte Fehler; Menü und Post-Cleanup bestätigt.
- Ohne Partner 22:24: keine Hooks/Korrektur, vier Originalfehler, erwartete abgefangene
  Paarablehnung; Chainloader und Post-Cleanup vorhanden, Durchführung bestätigt.
- 17 APIShared-Dateien hashgeprüft wiederhergestellt; vollständiges Paar geprüft.
- Vier Kontrolldateien und leere Ordner entfernt; 274 übrige Dateien unverändert.
- held-api aufgelöst; alte Probe bleibt gesichert/inaktiv. Kein Build/Release.

## Kompakte Ablage

- active-accepted.zip / orphan-accepted.zip: Logs und Identitäten der beiden Läufe.
- APIShared-development.zip / build-proof.json: getestetes Paket und historische Herkunft.
- result.json / orphan-state.json: Paket-/Kontrollhashes und abgeschlossener Übergang.
- build.log / checks.zip: historische Build- und gezielte Prüfnachweise.
- start1.zip: erster fehlgeschlagener Versuch; diagnostics-history.zip: gebündelte Altlogs.
- before.json / probe-backup.json / probe-backup: ursprüngliche Identitäten und Rückfallsicherung.
- test-contract-review.log: unabhängiger damaliger Hook-Publikationsbefund.

Grenzen: Die beiden Spieltests sind keine Abnahme späterer Quelländerungen oder
anderer Plattformen. Frühere Publisher-Prüfung scheiterte an fremder fixture.txt;
die spätere Installationsprüfung akzeptiert zusätzliche lokale Dateien. Historischen
Exitcode nicht umschreiben. Popularity, Fixes-Wheat und NativeHookPublication sind
unabhängige Aufgaben, deren aktuellen Stand andere Chats erneut prüfen müssen.
## Abschließende Codeprüfung

Die Bootstrap-Quellen entsprachen vor dieser Nachprüfung vollständig den im
Spiel getesteten Quellhashes. Früher Einstieg, Paar-/Gewinnerbindung, permanente
Hooklebensdauer, lokaler Indexwechsel und Fehlerpfade wurden erneut geprüft.
Dabei wurde ein zusätzlicher Randfall korrigiert: numerisches AssetMode 0/1 wird
vom internen JSON-Parser als Int32 geliefert und jetzt als gültig akzeptiert.
Der neue Verhaltenstest scheiterte vorher an 0 und besteht nach der Korrektur;
zwölf gültige/ungültige Schreibweisen sind zusätzlich abgedeckt.

Erneut bestanden: gesamte Bootstrap-CLI-Prüfung mit 73 Paarfällen, Anbieter- und
Indexfehlerpfaden, frühen Typ-/Listenergrenzen, echten DMD-Transformationen samt
Negativfällen und 75 Vergleichen mit der ursprünglichen SE-Prüfung. 289 lokale
Dokumentationsziele/Anker geprüft. Keine Hooks im Desktoptest installiert.

Entfernt: einmaliger Testmods/APISharedDependencyControl samt generierten Ausgaben
(14 Dateien) und unbenutzter DBP-Offline-Auswerter aus den APIShared-Tests.
Eigenständige Probe-/Regressionstests mit weiterhin verwendeten Referenzeingaben
bleiben erhalten; keine künstlichen Workshop-Adapter oder Kontrollverbraucher
werden mit APIShared ausgeliefert. Konflikterkennung der alten Probe bleibt nötig.

Wichtig: Die Int32-Korrektur ist quellseitig und durch den Desktoptest geprüft,
aber noch nicht im installierten Paket. Beide installierten DLL-Hashes sind
unverändert. Kein vollständiger Produktbuild und keine Installation in dieser
Nachprüfung; bei der nächsten regulären Paketierung wird die Ergänzung übernommen.
Die Dokumentation behauptet keinen Spielnachweis für diese spätere Quelländerung.
## Reduziertes Logging nach Nutzerauftrag

Erweiterte Bootstrap-Diagnose entfernt: keine separate Trace-Datei, Erfolgsmarker,
Inventardumps, Hashausgaben oder reinen IL-Einstiegstraces mehr. Nur echte Warnungen
und Fehler gehen mit Millisekundenzeitstempel in das normale BepInEx-Log.
Schutzprüfungen, Originalprüfung und permanente Hooklebensdauer bleiben erhalten.
Die Bootstrap-CLI-Prüfungen einschließlich 73 Paarfällen, 75 SE-Vergleichen und
beiden echten Methodentransformationen bestehen. Neue Loggingtests bestätigen
stille Initialisierung, Warn-/Fehlerstufen, Fehlerisolation und fehlenden Dateizugriff.
Dies ist der Quellstand auf dev/Serpens66, noch kein neu installiertes Paket.
Historische Trace-Nachweise bleiben gültige Belege der damaligen Testfassung.
