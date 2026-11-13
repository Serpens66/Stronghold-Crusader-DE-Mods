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

- Neue Tests oder Prüfscripte sind kein automatischer Bestandteil jeder Änderung. Vor ihrer Erstellung den konkreten zusätzlichen Nutzen bestimmen: Welche relevante Regression oder welcher verbindliche Vertrag ist durch bestehende Prüfungen noch nicht ausreichend abgesichert? Aufwand, Komplexität und spätere Wartung müssen zum Risiko passen. Vorhandene sinnvolle Tests bevorzugt ergänzen statt eine weitere Prüfdatei oder Testsuite einzuführen.
- Bei reiner Bereinigung, insbesondere dem Entfernen erledigter Entwicklungsdiagnose, zugehörige überflüssige Tests, Fixtures und Buildaufrufe mit entfernen. Nicht eigens neue Tests erstellen, die nur das Entfernen oder die Abwesenheit des alten Codes bestätigen. Ein zusätzlicher Test ist erst sinnvoll, wenn eine konkrete weiterhin relevante Fehlerklasse oder eigenständige Sicherheitsgrenze ihn begründet; bestehende allgemeine Sicherheitsprüfungen bleiben erhalten.
- Die Grundregel zu Verträgen und Momentaufnahmen aus der Root-AGENTS.md gilt auch für Tests, Buildtreiber, Konfigurationen und technische Markdown-Dateien. Verhalten, Ergebnisse und relevante Sicherheitsinvarianten prüfen; gleichwertige Umstrukturierungen sollen Tests nicht brechen.
- Exakte Quelltextfragmente, private Membernamen, Methodenaufteilungen oder interne Ordnerstrukturen nur als Prüfbedingung verwenden, wenn deren konkrete Form für den nachzuweisenden Vertrag erforderlich ist. Den Grund und die Grenzen der Prüfung am Test festhalten. Andernfalls geeignete Verhaltenstests, Metadaten oder strukturelle Prüfungen verwenden; keine bloßen Wortlautprüfungen als Sicherheitsnachweis ausgeben.
- Tests künftig nur für eine konkrete relevante Fehlerklasse schreiben: Eingaben, beobachtbares Ergebnis und eine plausible Regression benennen. Keine Tests auf Kommentare, beliebige Logformulierungen, frühere Versionsnummern oder selbst nachgerechnete Konstantenausdrücke. Testdoubles ersetzen externe Grenzen; eine nachgeschriebene Implementierung beweist nicht die Produktion. Quelltext-Heuristiken nur als ausdrücklich begrenzte Guardrails verwenden, niemals als Verhaltens- oder Laufzeitnachweis. Bei ihrer Ablösung muss die relevante Sicherheitsabdeckung erhalten bleiben.
- Buildprüfungen verwenden die tatsächlich ausgewählten SDK-/Spielreferenzen. Keine Abhängigkeit von unbeteiligten Modausgaben, pauschalen Workspace-Scans oder der Gesamtzahl vorhandener Features. Identische Prüfungen nicht mehrfach pro Build ausführen; unterschiedliche Backend-, Fehler- oder Prozesskontexte sind eigenständige Fälle und bleiben erhalten.
- Fixtures übernehmen relevante Eingaben und Abhängigkeiten aus der maßgeblichen Quelle bzw. einer bewusst festgelegten Testspezifikation. Eine exakte Pfadlänge ist beispielsweise im expliziten Grenzfalltest zulässig; andere Tests benötigen nur die für ihr Verhalten relevanten Eigenschaften. Ein absichtlicher Kompatibilitätsbruch oder eine reale Sicherheitsregression darf weiterhin fehlschlagen.
- Branch, HEAD, Dirty-Status, Verfügbarkeit und Buildfehler vor einer darauf beruhenden Entscheidung erneut feststellen. Zustandsberichte nennen Zeitpunkt, geprüften Quellstand und Nachweis/Prüfbefehl. Ein Commit nach einer früheren „uncommitted“-Beobachtung oder ein später erfolgreicher Build widerlegt keine historische Beobachtung und darf nicht durch diese blockiert werden.

## Build-Ablauf

- Wird APIShared installiert oder aktualisiert, müssen dessen Dateien unter `BepInEx/plugins` und `BepInEx/patchers` gemeinsam aus demselben gewählten Build beziehungsweise Paket aktualisiert werden; siehe [APIShared nutzen](apishared.md#apishared-nutzen).
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

## Entwicklungsdiagnose (DEVDIAG)

- Zweck: Entwicklungsdiagnosen bleiben im Quellbaum und sind ausschließlich im eigenen Diagnose-Build enthalten; normale Builds einschließlich `main` schließen ihre Arbeit beim Kompilieren aus. Messpunkte über `Shared.DevDiag` (`Shared/Runtime/Diagnostics/DevDiag.cs`); größere Implementierungen in `*.DevDiag.cs` oder vollständig mit `#if SERP_DEVDIAG` abgrenzen, einschließlich Vorbereitungen, Feldern, Subscriptions und Diagnosekonfiguration. Laufzeitschalter ersetzen die Buildgrenze nicht. Dauerhafte Supportdiagnosen, Fehlerwarnungen und öffentliche APIShared-Beobachter sind davon getrennt und bleiben nach ihrem Vertrag verfügbar.
- Alle `DevDiag`-Methoden sind `[Conditional("SERP_DEVDIAG")]`: ohne Symbol entfernt der Compiler Aufruf und Argumentauswertung; Call-Sites brauchen kein `#if`. Größere Diagnoseblöcke liegen in `src/**/*.DevDiag.cs` und werden nur mit Symbol kompiliert; funktionale Klassen binden sie über `partial void Diag…(…)`-Deklarationen an, deren Implementierung im `*.DevDiag.cs`-Teil liegt (ohne ihn entfernt der Compiler Aufrufe samt Argumenten). Nach dem Löschen der `*.DevDiag.cs`-Dateien auch die dann wirkungslosen `Diag…`-Deklarationen und -Aufrufe entfernen; die Inventur listet sie. Projekte mit Messpunkten verlinken `Shared/Runtime/Diagnostics/DevDiag.cs` ausdrücklich im csproj (wie `DebugLogHelper.cs`), damit auch csproj-lesende Quellprüfungen ihn sehen.
- Aktivierung nur ausdrücklich per `build.bat /devdiag` (setzt `SHCDE_DEVDIAG=1` nur für diesen Build; eine geerbte Variable wird verworfen). Für `main` verboten. Ausgabe: gedrosselte `DEVDIAG perf:`-Zeilen mit ms-Zeitstempel, nach `DevDiag.Attach(log)`.
- Sperren prüfen das erzeugte Artefakt: Eine DEVDIAG-Assembly trägt `AssemblyMetadata("SerpDevDiagnostics","true")` und `Shared.DevDiagRuntime`; `Release-Mod.ps1` und `Create-SteamModPack.ps1` brechen dann ab (`Assert-NoDevDiagnosticAssemblies`). Diagnosequellen sind auf allen Branches zulässig und blockieren einen normalen `main`-Build nicht. Inventur: `Shared/Tools/Validation/Test-DevDiagnostics.ps1 -ModDirectory <Mod>`; sie ist keine Entfernungsliste. `Test-DevDiagnosticsGate.ps1` prüft normale und Diagnose-Fixtures einschließlich Argumentauswertung, Partial-Aufrufen und der Binärsperre.
- Wiederverwendbare Diagnosequellen (Messpunkte) dürfen nach einer Untersuchung erhalten bleiben; ihre Entfernung ist keine Releasevoraussetzung. Ingame-Testszenarien gehören nicht dazu: sie liegen in eigenen Testmods unter `Testmods/`, und ein ausnahmsweise im Hauptmod angelegtes Szenario wird im selben Auftrag entfernt ([AutomatisierungTest.md](../AutomatisierungTest.md#neue-modbezogene-spieltests)). Veraltete Diagnose bei Bedarf gezielt bereinigen. DEVDIAG-Artefakte nie als normalen Build oder Release weitergeben.

## Hashprüfungen in Treibern

- Kritische Datei-/Paket-Hashprüfungen in Build-/Release-Treibern nutzen die vorhandenen gemeinsamen .NET-SHA-256-Helfer statt `Get-FileHash` (Verfügbarkeit/Modulauflösung nicht vorausgesetzt). Streams und Hashobjekte zuverlässig freigeben; bei Fehlern abbrechen, nie überspringen oder schwächer vergleichen.

## Temporäre Ausgaben und lokale Releasearchive

- Compilerverzeichnisse (`bin`, `obj`) und simulierte Testdaten sind erneut erzeugbar. Tests räumen eigene Fixtures bei Erfolg und Fehler auf; nach abgeschlossenen Untersuchungen kompakte Ergebnisse und notwendige Fehlerbelege statt vollständiger Compiler- oder Fixturebäume behalten. Nicht zuordenbare Quellen und echte Spielcaptures vorher prüfen und erhalten.
- Publisher-Aufrufe besitzen eigene temporäre Paketkopien. Nach Prozessende diese Kopien entfernen; Workshop-IDs, Versions-/Fingerprint-Zustand, Wiederanlaufmetadaten und kompakte Aufruf-/Changelog-Nachweise erhalten. Aktuelle eingefrorene Publisherdateien nicht verändern und aktive Aufrufe anderer Prozesse nicht bereinigen.
- Keine zusätzlichen lokalen Releasearchive auf Vorrat behalten. Nach erfolgreicher Vorbereitung können unreferenzierte Versionspakete, Download-/Entpackcaches und Staging-Daten entfernt werden. Aktuelle Uploadpakete, Publisher und alle tatsächlich von deren Vorbereitung benötigten Dateien bleiben erhalten. Historische Versionen bei Bedarf von GitHub beziehen oder aus dem passenden Quellstand neu erzeugen; ein Neubuild garantiert keine byteidentischen historischen Binärdateien.
- Installierte Analysewerkzeuge und Unity-Rücksetzdateien von heruntergeladenen Installern und Entpackresten unterscheiden. Aktuelle, manifestierte Analyseindizes und benötigtes Wissen erhalten; nicht mehr verwendete, erneut erzeugbare Indizes und abgeschlossene Checkpoints können nach Prüfung der Referenzen entfallen. Historische Metadaten und getrackte Quellen dabei nicht umschreiben.

## Lokale Mod-Buildcheckpoints

- Release-, Kompatibilitäts- und Hostbuilds mit explizitem `/noinstall` können vollständig erfolgreiche Builds samt Prüfungen über `Shared/Tools/BuildCache/ModBuildCheckpoint.ps1` wiederverwenden. Installierende Aufrufe führen ihre Buildaktion immer aus, weil vorhandene Paketdateien keine erfolgreiche Installation belegen. Dies überspringt keine aktuellen Git-, Abhängigkeits-, Versions-, Paket-, DEVDIAG- oder Veröffentlichungssperren. Teilweise bestandene Prüfungen eines fehlgeschlagenen Builds werden nicht als Erfolg gespeichert.
- Ein Treffer verlangt denselben vollständigen Commit, Quell-/Paketpfad, Argumente, geerbte Umgebung sowie unveränderte relevante Quellen, Tests, Quelllinks, Werkzeuge, SDK-, Spiel-/Native- und Compilerreferenzen. Die ausgewählte Native-Baseline bindet aktuelle Metadaten, Ressourcen und den manifestierten Datenbankdigest samt Dateistatus ein; keine erneute pauschale Gigabyte-Datenbankkopie oder -Hashprüfung.
- Zusätzlich muss der ursprüngliche Paketdateisatz vollständig vorhanden sein und alle SHA-256-Werte müssen stimmen. Fehlende oder veränderte Ausgaben erfordern einen echten Build; der Checkpoint stellt keine Binärdateien wieder her. Ursprünglicher Buildcommit und Buildzeit bleiben bei Wiederverwendung erhalten und werden in der Release-Provenance gekennzeichnet.
- Unter `.release-output/checkpoints/mod-builds` des koordinierenden Workspaces liegen höchstens 32 Erfolgsnachweise mit jeweils maximal 1 MiB, keine Binär-, Quell- oder Paketkopien. Jeder echte Neuversuch entfernt den bisherigen Nachweis vor Ausführung, auch wenn sein Fingerprint nicht mehr berechenbar ist. Unsichere Cachepfade werden nicht verwendet. Gezielt prüfen: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Shared/Tools/BuildCache/Test-ModBuildCheckpoint.ps1`.

## Versionen

- Während Tests/Debugging die Version nicht ändern, außer für einen Test wirklich sinnvoll oder ausdrücklich angefordert. Erst wenn ein Feature fertig oder ein Bug final behoben ist erhöhen; im Zweifel fragen, ob der Stand final ist.
- Versionsänderungen modweit atomar und einheitlich: vorher mit modweit begrenzter Suche alle Stellen der aktuellen Version als aktive Mod-, Assembly-, Paket-, Manifest-, Host- oder Buildversion ermitteln; dann Plugin-Konstanten, `info.json`, Manifeste und Build-/Releasekonfigurationen gemeinsam anpassen. Generierte/installierte Ausgaben sind kein Ersatz für Quelldateien; sie entstehen durch den Build neu.
- Danach vor dem Build erneut modweit nach alter und neuer Version suchen; verbleibende alte Vorkommen einzeln auf bewusste Historie (z. B. Changelog) prüfen, Rest korrigieren. Aktive Versionsangaben dürfen nicht abweichen. Ergebnis in der Abschlussmeldung nennen.

## Steam-Modpack „Serps Mods“

- `CreateSteamModPack.bat` ist der kanonische Einstieg. Mit Ziel „APIShared“ oder „Beide“ legt er ein fehlendes APIShared-Release nach Bestätigung aus dem gepinnten öffentlichen `main` an; andere Branches werden nie veröffentlicht. Nach einem PR-Merge auf GitHub spult die Vorbereitung den sauberen lokalen Release-Branch per Fast-Forward nach. `main` wird bei Bedarf in einem kleinen temporären Git-Worktree unter `.work/b/modpack-main` gebaut, damit der Workspace auf `develop` bleibt. Pakete, Publisher und Herkunftsnachweise liegen zentral unter `.release-output`; nach dem Einfrieren und Prüfen der Vorbereitung werden temporäre Quellen freigegeben. `UploadWorkshop.bat` im Hauptordner verwendet die zentrale Vorbereitung ohne vollständige Quellcheckouts. Ein neuer kompatibler Mod wird ausschließlich über die Liste `Mods` in `Shared/Tools/Steam/WorkshopConfiguration.ps1` aufgenommen.
- Host-GUID `SerpsMods_Serp`, veröffentlichte Child-GUIDs und alle ausgelieferten relativen Pfade sind unveränderlich: nicht umbenennen, wiederverwenden oder stillschweigend entfernen.
- Einmal ausgelieferte Pfade bleiben in jedem späteren Paket typgültig vorhanden: entfernte DLLs als gültige minimale No-op-Assembly mit gleichem Dateinamen und gleicher Plugin-GUID; JSON/XAML/strukturierte Dateien als formatspezifisch gültige neutrale Tombstones (keine leeren oder ungültigen Dateien).
- Stillgelegte Mods bleiben in `serps-modpack.json` mit `State: Retired` und erwarteten Tombstone-Hashes. Echte Pfadentfernung erst nach erfolgreichem Update-/Löschtest des verwendeten Extenders.
- Staging- und Ausgabeordner werden je Build frisch erstellt; die Bereinigung betrifft nur verifizierte Pfade unter `.release-output\SerpsMods` bzw. den exakten Workspace-Ausgabeordner `Serps Mods`.
- Child-Mods haben die Soft-Abhängigkeit `SerpsMods_Serp` und einen tatsächlich wirksamen Aktivierungsschalter; der Packager täuscht keine fehlende Runtime-Deaktivierung vor.
- Vor Einzel- und Packreleases ZIP-/Map-Hash, Provenance, GUID, Version, kompilierte Soft-Abhängigkeit und extrahierten Inhalt prüfen; ein Teilfehler ist kein erfolgreicher Packbuild.
- Host-eigene Fehler mit ms-Zeitstempel und Fehlerkennung ins BepInEx-Log; Error-/Fatal-Meldungen enthaltener Mods zusätzlich in der begrenzten Host-Diagnoseansicht, ohne sie rekursiv erneut zu loggen.
- Rückwärtskompatibilität modinterner Implementierungen ist nur für bereits als „SerpsMods“ auf Steam veröffentlichten Code nötig (APIShared: siehe [apishared.md](apishared.md)).

## Short development paths

Normale Entwicklungsbuilds (`/source:working`, auch `/workspace`, oder der automatische
Standard bei `/nopause`) nutzen den vorhandenen Arbeitsstand direkt. Keine vollständige
Quellkopie und keine Laufzeitanpassung von Quelltexten oder Buildtreibern. SDK-Auswahl,
Installation und Diagnose werden ausdrücklich als Parameter bzw. Prozessumgebung übergeben.
`/noinstall` bleibt unabhängig von der Quellauswahl; nicht mit `/branch` kombinieren.
Aufrufe ohne Argumente behalten das Branchmenü.

Pro Mod und notwendiger Konfiguration bleibt ein aktueller Ausgabestand. Ein erfolgreicher
Build entfernt überholte Paketdateien und ersetzt den bisherigen Stand; vorübergehendes
Staging ist zulässig, dauerhafte Buildbackups nicht. Fehlerhafte Builds sind kein gültiger
Erfolgsnachweis. Kompakte aktuelle Buildmetadaten reichen; kein wachsendes Binärarchiv.

Andere Branches verwenden bei Bedarf `.work/b/<branch>/<nummer>` mit nur den benötigten
Quellen und Abhängigkeiten. Vollständige Commits stehen in Metadaten, nicht im Ordnernamen.
Große `_inspect`-Daten werden nicht mitkopiert. Native Audits verwenden CURRENT.json und
sein Datenbankmanifest über SHCDE_NATIVE_BASELINE_ROOT; die Dataset-Identität bleibt geprüft.
Fehlende Datenbanken nur mit dem dokumentierten RestoreDatabase-Befehl wiederherstellen.

Isolierte Buildquellen und Test-Fixtures nach Abschluss auch im Fehlerfall aufräumen.
Aktive Nutzer, echte Entwicklungsänderungen und nicht integrierte Commits schützen;
einzelne einmalige Dateien gegebenenfalls mit überprüfbarem Wiederherstellungsnachweis
sichern, statt eine vollständige Arbeitskopie zu behalten. `/keepsource` verlangt einen
konkreten Bedarf und anschließende Bereinigung im selben Auftrag. Historische Altbestände
einzeln prüfen, nicht nach Alter oder Ordnername pauschal löschen. Bereits vorhandene
historische Nachweise bleiben unverändert; überholte Schutzvermerke aktuell berichtigen.

APIShared-SDKs je benötigtem Branch auf einen wiederverwendbaren Stand unter
`.work/c/apishared-<branch>` begrenzen. Wiederverwendung verlangt weiterhin die passende
vollständige Buildprüfung samt Quellen, Werkzeugen und Referenzen. Verbraucher desselben
Caches serialisieren. Registrierte Caches ausschließlich über Git entfernen; der nächste
Build erzeugt sie bei Bedarf neu. `/apishared:package` mit SHCDE_API_SHARED_DIR nutzt ein
geprüftes vorhandenes Paket ohne Neubuild. Standalone-APIShared-Arbeitsstandbuilds nutzen
den eigenen Checkout direkt; sein unabhängiger Branch-Workflow bleibt erhalten.

`/syncpackage` bleibt für Arbeitsstandbuilds zulässig: bei direkter Ausgabe ist keine
Quellkopie zurückzuspiegeln. Für `/branch` bleibt die Option unzulässig.

Tests und Analysen verwenden `.work/t` bzw. `.work/a` ausschließlich für notwendige
vorübergehende Eingaben. Nach Auswertung große erneut erzeugbare Rohdaten entfernen;
kompakte Resultate und notwendige Fehlerbelege erhalten. Eigene Testverzeichnisse auch
bei Fehlern mit dem sicheren Aufräumhelfer freigeben; Reparse Points nie verfolgen,
registrierte Git-Worktrees über Git entfernen. Ingame-Akzeptanznachweise und eigenständige
Entwicklungsquellen sind keine wegwerfbaren Fixtures.
