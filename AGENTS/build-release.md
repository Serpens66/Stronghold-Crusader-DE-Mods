# Builds, CRLF, Versionen und Releases

**Lesen, wenn:** ein Mod gebaut, eine Version geändert, Textdateien nach CRLF konvertiert, Hashprüfungen in Treibern geschrieben oder ein Release/Steam-Modpack vorbereitet wird. PowerShell-Aufrufdetails: [shell-windows.md](shell-windows.md). Gilt zusätzlich zur Root-`AGENTS.md`.

Buildbezogene Abläufe und Prüfungslisten in diesen Anweisungen sind Empfehlungen und Standardverhalten, keine unveränderlichen Buildregeln. Der ausdrückliche Nutzerauftrag bestimmt Quellstand, Branch, SDK, Prüfungsumfang, Buildweg und Installation. Auftragsbezogene Einschränkungen nie als allgemeine Verbote übernehmen. Technische Voraussetzungen und gültige Pfade bleiben erforderlich.

## Modpack-Branches

- Im Modpack-Repository ist `main` der releasefertige Stand; aktive und noch nicht releasefertige Arbeit gehört nach `develop`. Der normale Entwicklungs-Workspace bleibt auf `develop`.
- Release-Fixes auf `main` nur gezielt übernehmen und prüfen; danach nach `develop` zurückführen. Fertige Entwicklung regulär nach `main` mergen. Bestehende Release-Tags und veröffentlichte Historie nicht verschieben.
- APIShared bleibt ein unabhängiges Repository mit eigenem Branch-Workflow. Beide Modpack-Branches ignorieren dessen lokalen Checkout und enthalten keinen APIShared-Gitlink. Releases verwenden den festgehaltenen öffentlichen APIShared-`main`-Stand.

## Auswahl der Prüfungen

- Für alle Mods nur Prüfungen ausführen, die für den zu bauenden Mod, seine betroffenen Funktionen und den tatsächlichen Änderungsumfang relevant sind. Das Lesen einer Themendatei allein verpflichtet nicht zu sämtlichen dort genannten Tests; deren konkrete Auslöser beachten.
- Prüfungen möglichst auf das Modprojekt und seine relevanten Abhängigkeiten bzw. Quelllinks begrenzen (z. B. `Test-SharedBoundaries.ps1 -ModDirectory <Modpfad>`). Bei Änderungen an gemeinsam genutztem Code empfehlen sich zusätzlich die einschlägigen Prüfungen der dadurch betroffenen Verbraucher. Bietet ein Prüfer keine passende Eingrenzung, die relevanten Fälle separat prüfen; keine fachfremde Gesamtsuite allein wegen dieser technischen Einschränkung verlangen.
- Eine unveränderte APIShared-Referenz oder die gemeinsame Installation anderer Mods begründet allein keinen Gesamttest. Die fachlichen Auslöser für Preset- und UI-Prüfungen stehen in [modsettings-ui.md](modsettings-ui.md#pflichttests-vor-dem-build).
- Fachfremde Fehler blockieren den Build nicht und lösen keine ungefragten Reparaturen aus. Bei ausdrücklich angeforderten Gesamttests fachfremde Befunde separat ausweisen; Fehler im relevanten Prüfbereich sollten vor dem Build geklärt oder als bewusste Einschränkung ausgewiesen werden.
- Vor dem Build kurz festhalten, welche Prüfungen vorgesehen sind und warum.

## Belastbare Tests und Zustandsnachweise

- Die Grundregel zu Verträgen und Momentaufnahmen aus der Root-AGENTS.md gilt auch für Tests, Buildtreiber, Konfigurationen und technische Markdown-Dateien. Verhalten, Ergebnisse und relevante Sicherheitsinvarianten prüfen; gleichwertige Umstrukturierungen sollen Tests nicht brechen.
- Exakte Quelltextfragmente, private Membernamen, Methodenaufteilungen oder interne Ordnerstrukturen nur als Prüfbedingung verwenden, wenn deren konkrete Form für den nachzuweisenden Vertrag erforderlich ist. Den Grund und die Grenzen der Prüfung am Test festhalten. Andernfalls geeignete Verhaltenstests, Metadaten oder strukturelle Prüfungen verwenden; keine bloßen Wortlautprüfungen als Sicherheitsnachweis ausgeben.
- Tests künftig nur für eine konkrete relevante Fehlerklasse schreiben: Eingaben, beobachtbares Ergebnis und eine plausible Regression benennen. Keine Tests auf Kommentare, beliebige Logformulierungen, frühere Versionsnummern oder selbst nachgerechnete Konstantenausdrücke. Testdoubles ersetzen externe Grenzen; eine nachgeschriebene Implementierung beweist nicht die Produktion. Quelltext-Heuristiken nur als ausdrücklich begrenzte Guardrails verwenden, niemals als Verhaltens- oder Laufzeitnachweis. Bei ihrer Ablösung muss die relevante Sicherheitsabdeckung erhalten bleiben.
- Buildprüfungen verwenden die tatsächlich ausgewählten SDK-/Spielreferenzen. Keine Abhängigkeit von unbeteiligten Modausgaben, pauschalen Workspace-Scans oder der Gesamtzahl vorhandener Features. Identische Prüfungen nicht mehrfach pro Build ausführen; unterschiedliche Backend-, Fehler- oder Prozesskontexte sind eigenständige Fälle und bleiben erhalten.
- Fixtures übernehmen relevante Eingaben und Abhängigkeiten aus der maßgeblichen Quelle bzw. einer bewusst festgelegten Testspezifikation. Eine exakte Pfadlänge ist beispielsweise im expliziten Grenzfalltest zulässig; andere Tests benötigen nur die für ihr Verhalten relevanten Eigenschaften. Ein absichtlicher Kompatibilitätsbruch oder eine reale Sicherheitsregression darf weiterhin fehlschlagen.
- Branch, HEAD, Dirty-Status, Verfügbarkeit und Buildfehler vor einer darauf beruhenden Entscheidung erneut feststellen. Zustandsberichte nennen Zeitpunkt, geprüften Quellstand und Nachweis/Prüfbefehl. Ein Commit nach einer früheren „uncommitted“-Beobachtung oder ein später erfolgreicher Build widerlegt keine historische Beobachtung und darf nicht durch diese blockiert werden.

## Build-Ablauf

- Nach dem Erstellen oder Ändern eines Mods empfohlen: zunächst die nach der [Prüfauswahl](#auswahl-der-prüfungen) relevanten Prüfungen und Codekontrollen abschließen (z. B. modbezogene Shared-Boundary- und Abhängigkeitsprüfungen, mod-eigene `verify.ps1`, bei betroffenen Modsettings-/UI-Funktionen die einschlägigen Tests aus [modsettings-ui.md](modsettings-ui.md)), damit unnötige Rebuilds entfallen; der Nutzer kann einen anderen Prüfungsumfang wählen.
- Danach einmal die passende `build.bat` direkt mit erhöhten Rechten (bzw. außerhalb der Sandbox) und `/nopause` gemäß der Nutzerwahl ausführen. Standardmäßig übernimmt `build.bat` Build und Installation; andere ausdrücklich gewünschte Buildwege sind zulässig. Nach Codeänderungen ist ein erneuter Lauf erlaubt.

## Buildstand und SDK: Nutzerwahl vor Standard

- Ausdrückliche Nutzerwünsche bestimmen Quellstand (auch uncommittierter Arbeitsstand), Branch und SDK. Es gibt keine allgemeine Pflicht, vor einem Build zu committen, eine bestimmte Branch zu wählen oder ausschließlich deren letzten Commit zu bauen.
- Die vorhandenen Branchmenüs, Commit-Worktrees und zugehörigen SDK-Auswahlen in `build.bat` sind Standardabläufe für Aufrufe ohne abweichenden Nutzerauftrag. Sie sind keine Einschränkung ausdrücklich gewünschter Arbeitsstand- oder SDK-Builds. Fehlt dafür ein expliziter Treiberweg, ihn gezielt ergänzen; keine interne Release-Pinningvariable als versteckte Umgehung verwenden.
- Ohne abweichenden Auftrag bleiben vorhandene Release-Pins, Installationsvarianten und Bereinigungsgrenzen der Treiber erhalten. Ein gewählter Buildstand autorisiert weder Commits noch Branchwechsel, Stash, Reset oder eine Veröffentlichung.
- Unverändert verlangte SDKs und installierte Abhängigkeiten vor/nach dem Build prüfen und weder neu bauen noch ersetzen. Prüfungen sind der empfohlene Standard; technische Installationsfehler sind vom gewählten Quellstand unabhängig.

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

## Short development paths

New branch sources use `.work/b/<branch>/<number>`; full commits remain in build records.
`build.bat /source:working /nopause /noinstall` builds an isolated copy of current
tracked and untracked sources without committing or switching the checkout.
`/noinstall` is optional and independent of this source selection. Automated
`/nopause` calls without a source choice default to the working tree; calls without
arguments retain the branch menu.
Do not combine it with `/branch`. Mod consumers can select a previously validated
package explicitly with `/apishared:package` and `SHCDE_API_SHARED_DIR`; the package
is verified, never rebuilt. Test and analysis work uses `.work/t` and `.work/a`.
Historical evidence and published package paths retain their original locations.

Native audits resolve the database through CURRENT.json and its database manifest.
Branch builds pass SHCDE_NATIVE_BASELINE_ROOT for read-only access to local baseline
artifacts; audit code must verify that source and shared metadata identify the same
dataset. Missing databases use the documented RestoreDatabase command, never an
older guessed filename. Generated audit buffers belong in .work/a.
