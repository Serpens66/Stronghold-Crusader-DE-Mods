# Builds, CRLF, Versionen und Releases

**Lesen, wenn:** ein Mod gebaut, eine Version geändert, Textdateien nach CRLF konvertiert, Hashprüfungen in Treibern geschrieben oder ein Release/Steam-Modpack vorbereitet wird. PowerShell-Aufrufdetails: [shell-windows.md](shell-windows.md). Gilt zusätzlich zur Root-`AGENTS.md`.

## Modpack-Branches

- Im Modpack-Repository ist `main` der releasefertige Stand; aktive und noch nicht releasefertige Arbeit gehört nach `develop`. Der normale Entwicklungs-Workspace bleibt auf `develop`.
- Release-Fixes auf `main` nur gezielt übernehmen und prüfen; danach nach `develop` zurückführen. Fertige Entwicklung regulär nach `main` mergen. Bestehende Release-Tags und veröffentlichte Historie nicht verschieben.
- APIShared bleibt ein unabhängiges Repository mit eigenem Branch-Workflow. Beide Modpack-Branches ignorieren dessen lokalen Checkout und enthalten keinen APIShared-Gitlink. Releases verwenden den festgehaltenen öffentlichen APIShared-`main`-Stand.

## Auswahl der Prüfungen

- Für alle Mods nur Prüfungen ausführen, die für den zu bauenden Mod, seine betroffenen Funktionen und den tatsächlichen Änderungsumfang relevant sind. Das Lesen einer Themendatei allein verpflichtet nicht zu sämtlichen dort genannten Tests; deren konkrete Auslöser beachten.
- Prüfungen möglichst auf das Modprojekt und seine relevanten Abhängigkeiten bzw. Quelllinks begrenzen (z. B. `Test-SharedBoundaries.ps1 -ModDirectory <Modpfad>`). Änderungen an gemeinsam genutztem Code erfordern zusätzlich die einschlägigen Prüfungen der dadurch betroffenen Verbraucher. Bietet ein Prüfer keine passende Eingrenzung, die relevanten Fälle separat prüfen; keine fachfremde Gesamtsuite allein wegen dieser technischen Einschränkung verlangen.
- Eine unveränderte APIShared-Referenz oder die gemeinsame Installation anderer Mods begründet allein keinen Gesamttest. Die fachlichen Auslöser für Preset- und UI-Prüfungen stehen in [modsettings-ui.md](modsettings-ui.md#pflichttests-vor-dem-build).
- Fachfremde Fehler blockieren den Build nicht und lösen keine ungefragten Reparaturen aus. Bei ausdrücklich angeforderten Gesamttests fachfremde Befunde separat ausweisen; Fehler im relevanten Prüfbereich bleiben vor dem Build zu klären.
- Vor dem Build kurz festhalten, welche Prüfungen erforderlich sind und warum.

## Build-Ablauf

- Nach dem Erstellen oder Ändern eines Mods zuerst alle nach der [Prüfauswahl](#auswahl-der-prüfungen) relevanten Prüfungen und Codekontrollen abschließen (z. B. modbezogene Shared-Boundary- und Abhängigkeitsprüfungen, mod-eigene `verify.ps1`, bei betroffenen Modsettings-/UI-Funktionen die einschlägigen Tests aus [modsettings-ui.md](modsettings-ui.md)), damit unnötige Rebuilds entfallen.
- Danach einmal die passende `build.bat` direkt mit erhöhten Rechten (bzw. außerhalb der Sandbox) und der zuvor gewählten Branch ausführen, z. B.: `& '<Pfad>\build.bat' /branch:develop /nopause; exit $LASTEXITCODE`. Nicht selbst bauen oder kopieren; `build.bat` übernimmt Build und Installation in den Spieleordner. Nach Codeänderungen ist ein erneuter Lauf erlaubt; uncommittete Quellen gehören nicht zum ausgewählten Branchbuild.

## APIShared-Branch für Verbraucher-Builds (Pflichtabklärung)

- Lokale `build.bat` bieten ohne Argument ein Menü für `main` und `develop`. Automatisierte Aufrufe benötigen `/branch:main` oder `/branch:develop`; `/nopause` ohne Auswahl bricht ab. Beide Optionen bauen ausschließlich den letzten lokalen Commit in einem eigenen Worktree unter `.release-output/branch-builds/`, ohne Fetch, Branchwechsel oder Übernahme uncommitteter Projekt-/Runtime-Änderungen. Aktuelle Buildsteuerung passt nur Buildskripte und Installationspfade an; die Anpassungen stehen im Buildnachweis neben dem Worktree.
- Die gewählte Modbranch legt zugleich die APIShared-Branch fest (`main` → `main`, `develop` → `develop`). Damit ist die Standard-SDK-Wahl autorisiert; eine abweichende Wahl zuvor mit dem Nutzer klären und im Abschluss nennen. `/apishared:main|develop` beziehungsweise `SHCDE_API_SHARED_BRANCH` wählen ausdrücklich einen anderen lokalen Branchstand. Das SDK wird isoliert mit `/noinstall` gebaut; das installierte APIShared bleibt unverändert.
- `/apishared:dev` bleibt die ausdrückliche Ausnahme für das bereits gebaute Paket des lokalen persönlichen Checkouts (gewöhnlich `dev/Serpens66`); dessen Buildnachweis muss aktuell sein. Der normale Branchdialog bietet diesen persönlichen Arbeitsstand nicht an.
- `develop` installiert Plugins und zusätzliche Patcher-/Werkzeugordner mit `_dev`; Bereinigung und Einstellungen betreffen nur die gewählte Variante. Die andere Installation wird manuell entfernt. `/noinstall` verändert keine Spielinstallation. Bestehende Installationssperren gelten weiterhin.
- Release-Treiber und deren Unterbuilds behalten ihren bereits geprüften Commit und ihr gepinntes SDK ohne neues Auswahlmenü. Sie setzen `SHCDE_BRANCH_PINNED` nur für den geprüften Aufruf; diese interne Variable ist kein Ersatz für die Branchwahl bei normalen Builds.
- Beispiel: `& '<Pfad>\build.bat' /branch:develop /nopause; exit $LASTEXITCODE`. Gemeinsame Steuerung: `Shared/Tools/BranchBuild/`; APIShared enthält dieselben eigenständig nutzbaren Buildwerkzeuge unter `tools/BranchBuild/`.

## CRLF

- Textdateien verwenden CRLF (Ausnahme: ausdrücklich für Linux gedachte Dateien). Vor einem Mod-Build alle betroffenen Textdateien prüfen.
- Nie Git-/MSYS-Perl oder shellübergreifend gequotete Aufrufe wie `perl -pi -e 's/\r?\n/\r\n/g'` verwenden (fehlerhafte Argumentübergabe kann normale `n`-Zeichen beschädigen).
- In .NET/PowerShell `"\r\n"` nie als Ersatzzeichenfolge an `Regex.Replace` übergeben (PowerShell schreibt sonst die vier sichtbaren Zeichen `\r\n`). Zeilenumbrüche nur über `[Environment]::NewLine`, ``"`r`n"`` oder `[char]13`/`[char]10` bilden und danach prüfen, dass keine wörtlichen `\r\n`-Sequenzen zurückbleiben.
- CRLF nur mit einer eng begrenzten .NET-Dateioperation mit expliziten Zielpfaden herstellen: erwarteten Text im Speicher bilden, schreiben, ordinal vergleichen; danach je Datei CRLF-Anzahl, verbleibende nackte LF und repräsentativen Inhalt kontrollieren.

## Hashprüfungen in Treibern

- Kritische Datei-/Paket-Hashprüfungen in Build-/Release-Treibern nutzen die vorhandenen gemeinsamen .NET-SHA-256-Helfer statt `Get-FileHash` (Verfügbarkeit/Modulauflösung nicht vorausgesetzt). Streams und Hashobjekte zuverlässig freigeben; bei Fehlern abbrechen, nie überspringen oder schwächer vergleichen.

## Versionen

- Während Tests/Debugging die Version nicht ändern, außer für einen Test wirklich sinnvoll oder ausdrücklich angefordert. Erst wenn ein Feature fertig oder ein Bug final behoben ist erhöhen; im Zweifel fragen, ob der Stand final ist.
- Versionsänderungen modweit atomar und einheitlich: vorher mit modweit begrenzter Suche alle Stellen der aktuellen Version als aktive Mod-, Assembly-, Paket-, Manifest-, Host- oder Buildversion ermitteln; dann Plugin-Konstanten, `info.json`, Manifeste und Build-/Releasekonfigurationen gemeinsam anpassen. Generierte/installierte Ausgaben sind kein Ersatz für Quelldateien; sie entstehen durch den Build neu.
- Danach vor dem Build erneut modweit nach alter und neuer Version suchen; verbleibende alte Vorkommen einzeln auf bewusste Historie (z. B. Changelog) prüfen, Rest korrigieren. Aktive Versionsangaben dürfen nicht abweichen. Ergebnis in der Abschlussmeldung nennen.

## Steam-Modpack „Serps Mods“

- `CreateSteamModPack.bat` ist der kanonische Einstieg. Ein neuer kompatibler Mod wird ausschließlich über den nächsten freien `STEAM_MOD_XX`-Eintrag im Kopfbereich aufgenommen.
- Host-GUID `SerpsMods_Serp`, veröffentlichte Child-GUIDs und alle ausgelieferten relativen Pfade sind unveränderlich: nicht umbenennen, wiederverwenden oder stillschweigend entfernen.
- Einmal ausgelieferte Pfade bleiben in jedem späteren Paket typgültig vorhanden: entfernte DLLs als gültige minimale No-op-Assembly mit gleichem Dateinamen und gleicher Plugin-GUID; JSON/XAML/strukturierte Dateien als formatspezifisch gültige neutrale Tombstones (keine leeren oder ungültigen Dateien).
- Stillgelegte Mods bleiben in `serps-modpack.json` mit `State: Retired` und erwarteten Tombstone-Hashes. Echte Pfadentfernung erst nach erfolgreichem Update-/Löschtest des verwendeten Extenders.
- Staging- und Ausgabeordner werden je Build frisch erstellt; die Bereinigung betrifft nur verifizierte Pfade unter `.release-output\SerpsMods` bzw. den exakten Workspace-Ausgabeordner `Serps Mods`.
- Child-Mods haben die Soft-Abhängigkeit `SerpsMods_Serp` und einen tatsächlich wirksamen Aktivierungsschalter; der Packager täuscht keine fehlende Runtime-Deaktivierung vor.
- Vor Einzel- und Packreleases ZIP-/Map-Hash, Provenance, GUID, Version, kompilierte Soft-Abhängigkeit und extrahierten Inhalt prüfen; ein Teilfehler ist kein erfolgreicher Packbuild.
- Host-eigene Fehler mit ms-Zeitstempel und Fehlerkennung ins BepInEx-Log; Error-/Fatal-Meldungen enthaltener Mods zusätzlich in der begrenzten Host-Diagnoseansicht, ohne sie rekursiv erneut zu loggen.
- Rückwärtskompatibilität modinterner Implementierungen ist nur für bereits als „SerpsMods“ auf Steam veröffentlichten Code nötig (APIShared: siehe [apishared.md](apishared.md)).
