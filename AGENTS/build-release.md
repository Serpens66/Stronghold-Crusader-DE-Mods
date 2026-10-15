# Builds, CRLF, Versionen und Releases

**Lesen, wenn:** ein Mod gebaut, eine Version geändert, Textdateien nach CRLF konvertiert, Hashprüfungen in Treibern geschrieben oder ein Release/Steam-Modpack vorbereitet wird. PowerShell-Aufrufdetails: [shell-windows.md](shell-windows.md). Gilt zusätzlich zur Root-`AGENTS.md`.

## Build-Ablauf

- Nach dem Erstellen oder Ändern eines Mods zuerst alle Prüfungen und Codekontrollen abschließen (z. B. `Test-SharedBoundaries.ps1`, `Test-DependencyMetadata.ps1`, `Test-PermanentNativeRuntimePatches.ps1`, mod-eigene `verify.ps1`, bei Modsettings die Tests aus [modsettings-ui.md](modsettings-ui.md)), damit unnötige Rebuilds entfallen.
- Danach einmal die passende `build.bat` direkt mit erhöhten Rechten (bzw. außerhalb der Sandbox) ausführen: `& '<Pfad>\build.bat' /nopause; exit $LASTEXITCODE`. Nicht selbst bauen oder kopieren; `build.bat` übernimmt Build und Installation in den Spieleordner. Nach Codeänderungen ist ein erneuter Lauf erlaubt.

## APIShared-Branch für Verbraucher-Builds (Pflichtabklärung)

- Vor jedem Build eines Mods, der APIShared referenziert, beim Nutzer abklären, gegen welche APIShared-Branch gebaut werden soll, sofern er es für diese Aufgabe nicht bereits ausdrücklich festgelegt hat. Nicht stillschweigend den Default annehmen. Die getroffene Wahl in der Abschlussmeldung nennen.
- Auswahl über das Argument der `build.bat`: `/apishared:main` baut gegen die öffentliche Release-Branch `main` (festgehaltener Commit in einem isolierten Worktree unter `.release-output/APIShared/main/<commit>/source`, bei Bedarf dort mit `/noinstall` gebaut; das installierte Spiel-APIShared und der Entwicklungs-Checkout bleiben unverändert). `/apishared:dev` baut gegen den lokalen Entwicklungs-Checkout `APIShared/` und dessen aktuell ausgecheckte Branch (normalerweise `dev/Serpens66`); dafür muss zuvor `APIShared/build.bat` für den aktuellen Stand gelaufen sein. Ohne Argument gilt `dev`, außer ein Release-Treiber hat das SDK bereits gepinnt. Skripte können dieselbe Wahl über `SHCDE_API_SHARED_BRANCH` treffen. Beispiel: `& '<Pfad>\build.bat' /nopause /apishared:main; exit $LASTEXITCODE`.
- Andere Branches (`develop`, fremde dev-Branches) werden vom Treiber nicht unterstützt; dafür nicht den Entwicklungs-Checkout umschalten, sondern beim Nutzer nachfragen. Gemeinsame Logik: `Shared/Tools/ApiSharedRepository/Select-ApiSharedBranch.ps1`/`.cmd`; neue Verbraucher-`build.bat` rufen sie unmittelbar vor `Test-ConsumerPackage.ps1` mit `%*` auf.

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
