# Arbeitsanweisungen für SHCDE-Mods

Gilt für alle Coding-Agenten (z. B. ChatGPT Codex, Claude Code). Von diesen Regeln darf nur mit ausdrücklicher Erlaubnis des Nutzers abgewichen werden. Diese Datei enthält, was immer gilt; Spezialthemen stehen in [`AGENTS/`](AGENTS/README.md).

Buildbezogene Abläufe und Prüfungslisten in diesen Anweisungen sind Empfehlungen und Standardverhalten, keine unveränderlichen Buildregeln. Der ausdrückliche Nutzerauftrag bestimmt Quellstand, Branch, SDK, Prüfungsumfang, Buildweg und Installation. Auftragsbezogene Einschränkungen nie als allgemeine Verbote übernehmen. Technische Voraussetzungen und gültige Pfade bleiben erforderlich.

## 1. Themendateien: Pflichtlektüre nach Auslöser

Sobald ein Auslöser zutrifft, die genannte Datei **vor** dem ersten fachlichen Vorschlag, Codeeingriff, Build oder Test vollständig lesen; ihre Regeln sind dann genauso verbindlich wie diese Datei. Mehrere Auslöser → mehrere Dateien. Im Zweifel lieber lesen. Das Lesen einer Themendatei macht nicht automatisch sämtliche darin genannten Tests erforderlich; für alle Mods gilt die [Auswahl nach Relevanz](AGENTS/build-release.md#auswahl-der-prüfungen).

| Auslöser | Pflichtlektüre |
|---|---|
| Vanillas nativer Code ist für Analyse/Planung/Diagnose relevant; RVAs, Patterns, Offsets; Nutzer verlangt die Baseline | [AGENTS/native-analysis.md](AGENTS/native-analysis.md) |
| Nativer/verwalteter Laufzeithook, Byte-Patch, Trampolin, RedBird | [AGENTS/native-hooks.md](AGENTS/native-hooks.md) **und** native-analysis.md |
| Änderung oder Build eines C#-/BepInEx-Runtime-Mods (auch Testmods): Lifecycle, Events, Threads→UI, JSON, publicized Assembly | [AGENTS/runtime-lifecycle.md](AGENTS/runtime-lifecycle.md) |
| Script-Extender-APIs mit Sondervertrag (Pathing, AIV, Gebäudeerzeugung, XAML-Patches), Extender-Update | [AGENTS/script-extender.md](AGENTS/script-extender.md) |
| Unit-/Building-/Tribe-IDs, Arrayindizes, Enums und Domänenkonstanten | [AGENTS/ids-enums.md](AGENTS/ids-enums.md) |
| Modsettings, Presets, Modsettings-XAML, Tooltips, Locales | [AGENTS/modsettings-ui.md](AGENTS/modsettings-ui.md) |
| Custom-Pakete, Chores, `[SyncPerPlayer]`, Netzwerkautorität, Spielmodus-Freigaben, MP-Tests | [AGENTS/multiplayer-network.md](AGENTS/multiplayer-network.md) |
| Kartenstart, Save-Laden, Editor-Start, Missionsende, verzögerte Aktionen, Timeline | [AGENTS/mission-lifecycle.md](AGENTS/mission-lifecycle.md) |
| APIShared nutzen/erweitern/veröffentlichen, Dateien unter `Shared/`, Mod-Abhängigkeiten | [AGENTS/apishared.md](AGENTS/apishared.md) |
| Fehler in Script Extender oder Fixes-Mod; Fixes-bezogene Präferenzen, Presets, Belagerung | [AGENTS/external-components.md](AGENTS/external-components.md) |
| Build, CRLF-Konvertierung, Versionsänderung, Release, Steam-Modpack | [AGENTS/build-release.md](AGENTS/build-release.md) |
| PowerShell-/CMD-/BAT-Befehle oder -Skripte, Sandbox-/Berechtigungsfehler | [AGENTS/shell-windows.md](AGENTS/shell-windows.md) |
| FPS-/Lag-/Startzeit-/Effizienz-Untersuchung, Performance-Messung, neue Hooks/Callbacks/Suchen in heißen Pfaden (pro Tick, Frame, Einheit, Kachel) | [AGENTS/performance.md](AGENTS/performance.md) |

Verschachtelte `AGENTS.md` (z. B. `APIShared/`, `Shared/Tools/ScriptExtenderUpdate/`) gelten zusätzlich in ihrem Ordner.

## 2. Hard-Gates (nie überspringen)

Vor jedem Tool- oder Shellaufruf die einschlägigen Punkte prüfen; bereits dokumentierte Fehler nicht erst nach ihrem Auftreten erkennen. Details in den verlinkten Dateien.

- **Vanilla-Audit:** Hängt etwas von Vanillas nativem Verhalten ab, vor dem ersten Lösungsvorschlag einen vollständigen featurebezogenen Audit abschließen und als Zwischenmeldung zusammenfassen; Stichproben genügen nicht → [native-analysis.md](AGENTS/native-analysis.md).
- **Runtime-Precheck:** Vor der ersten Änderung an einem Runtime-Mod JSON-Abhängigkeiten, Teardownpfade und den langlebigen Aufrufweg nach dem SHCDE-Startup-Cleanup prüfen und melden; früh erzeugte Plugin-Komponenten werden beim Start zerstört → [runtime-lifecycle.md](AGENTS/runtime-lifecycle.md).
- **JSON:** in Runtime-Mods ausschließlich `Shared.DependencyFreeJson` per Quelllink; alle anderen Serializer verboten.
- **Publicized Assembly:** neue Spielmember vor dem Build gegen die echte `Assembly-CSharp.dll` auf Sichtbarkeit prüfen; ein erfolgreicher Build beweist nichts.
- **Hooks:** veröffentlichte Hooks nie zurück- oder umpatchen; Aktivierung nur logisch → [native-hooks.md](AGENTS/native-hooks.md).
- **XAML-Patches:** `<Content>` mit genau einem Wurzelelement.
- **Entwicklungsdiagnose:** neue, vor dem Release zu entfernende Diagnose nur über `Shared.DevDiag` und `build.bat /devdiag`; nie über Laufzeitschalter oder Release-Builds → [build-release.md](AGENTS/build-release.md#entwicklungsdiagnose-devdiag).
- **Events statt Polling:** immer prüfen, ob ein passendes Event existiert (z. B. `OnKeyDown` statt `onBeforeRender`).
- **Kompatibilität:** Jeder neue Code muss mit dem Script Extender und dem Fixes-Mod kompatibel sein (lokaler Klon zuerst, siehe §6); nach dem Fixes-Mod zu laden ist eine erlaubte Lösung.
- **Empfohlene Prüfungen vor Runtime-Builds:** `Shared/Tools/Validation/Test-SharedBoundaries.ps1` auf den zu bauenden Mod und seine relevanten Quellen begrenzen; weitere Prüfungen nach [Relevanz](AGENTS/build-release.md#auswahl-der-prüfungen) und den konkreten Auslösern der Themendateien auswählen.

## 3. Kommunikation und Entscheidungen

- Bei unklaren Anweisungen nachfragen, bevor eine möglicherweise falsche Richtung eingeschlagen wird.
- Gibt es bei einem echten Problem mehrere wesentlich unterschiedliche Lösungswege: Fortschritt sichern, Problem konkret beschreiben, Nutzer fragen.
- Für Fragen kein Zeitlimit; bleibt eine notwendige Antwort aus, sauber ohne Fortschrittsverlust anhalten und erst nach der Antwort weiterarbeiten.
- Vom Nutzer übergebene Pfade/URLs können durch Markdown verfälscht sein (`\_` statt `_`). Existiert der Literalpfad mit `\_` nicht, die Variante mit `_` prüfen und die korrigierte Interpretation ausdrücklich nennen; nicht ungeprüft korrigieren, wenn beide existieren oder plausibel sind.
- Ist eine vollständig in Markdown formatierte Antwort verlangt: genau ein äußerer Codeblock ab ` ```markdown ` bis ganz zum Ende, darin keine weiteren dreifachen Backticks (Code mit vier Leerzeichen einrücken).
- Lange Prozesse: Fortschritt beobachtbar machen (getrennte Logs, Zeitstempel, CPU, Loggröße, Zwischenstände); nicht wegen eines kurzen Shell-Timeouts als hängend behandeln. Bei voraussichtlich > 5 Minuten den Nutzer fragen, ob fortgesetzt werden soll; ein sicher laufender Prozess darf währenddessen weiterlaufen.

## 4. Dateien, Änderungen und Builds

- `README.md`-Dateien nur auf ausdrückliche Bitte ändern. Bekommt ein Mod ein neues Feature, fragen, ob es nach Fertigstellung in die README soll.
- Bestehende Nutzeränderungen erhalten; keine sachfremden Dateien ändern; Dateien gezielt bearbeiten.
- Für bereits Git-getrackte Dateien keine zusätzlichen Sicherungskopien anlegen; die versionierte Historie genügt. Noch nicht committete Nutzeränderungen sind dadurch nicht gesichert und müssen weiterhin erhalten bleiben.
- Analysewerkzeuge und Artefakte in `.inspect`, `.tools`, `.native-analysis` u. ä. nicht anschließend löschen.
- Textdateien verwenden CRLF (außer ausdrücklich für Linux); sicheres Konvertierungsverfahren → [build-release.md](AGENTS/build-release.md).
- Ersetzt neuer Code alten Code oder Dateien, keinen Fallback oder veraltete Version ungefragt parallel behalten; vorher fragen.
- Empfohlener Buildablauf: relevante Prüfungen gemäß [Prüfauswahl](AGENTS/build-release.md#auswahl-der-prüfungen), danach die passende `build.bat` mit `/nopause` und bei Bedarf erhöhten Rechten. Buildweg, Prüfungsumfang, Quellstand, SDK und Installation richten sich nach dem konkreten Nutzerauftrag; das ist keine allgemeine Einschränkung anderer Buildwege.
- Buildskripte bieten Standardabläufe; ausdrückliche Nutzerwünsche bestimmen Quellstand (einschließlich Arbeitsstand), Branch und SDK. Keine allgemeine Commit- oder Branchpflicht für Builds → [build-release.md](AGENTS/build-release.md).
- Versionen erst ändern, wenn ein Feature fertig oder ein Bug final behoben ist (im Zweifel fragen); dann modweit atomar und mit Konsistenzprüfung → [build-release.md](AGENTS/build-release.md).
- Commits und Veröffentlichungen nur auf ausdrücklichen Nutzerauftrag.

## 5. Allgemeine Mod-Entwicklung

- Ziel: C#-/BepInEx-Mods für **Stronghold Crusader Definitive Edition (SHCDE)** auf Basis des Script Extenders. Neue Mods am Aufbau vorhandener Workspace-Mods orientieren.
- `info.json` anlegen: echte lokale Client-Mods `NetworkMode=0` (Unterschiede in der Lobby ignorierbar), gameplaybeeinflussende `NetworkMode=1` (muss im MP übereinstimmen); `WorkshopUrl` optional. Plugins ohne `info.json` gelten beim Extender konservativ als gameplaybeeinflussend.
- Jeder Mod funktioniert allein mit seinen deklarierten harten Abhängigkeiten; andere Workspace-, Diagnose- oder Testmods nie stillschweigend voraussetzen. Die gemeinsame Installation aller Mods bleibt konfliktfrei: optionale Zusammenarbeit per Soft-Dependency mit Standalone-Pfad, nie überlappende native Hooks.
- Kurze Kommentare für nicht offensichtliche Entscheidungen.
- Logging immer mit Zeitstempel inkl. Millisekunden.
- Neue Features funktionieren standardmäßig auch im Karteneditor, außer der Nutzer schließt ihn aus (Modusregeln → [multiplayer-network.md](AGENTS/multiplayer-network.md)).
- Modsettings werden nur vor/nach einer Partie geändert, nie während einer laufenden, außer ausdrücklich angefragt.
- Fachliche Domänenwerte über Extender-Enums/-Konstanten, keine harten Zahlen → [ids-enums.md](AGENTS/ids-enums.md).
- Fallen bei Arbeit, Tests oder Diagnose ausreichend sichere neue Erkenntnisse für die Native-Baseline an, die Baseline ergänzen.
- Rückwärtskompatibilität modinterner Implementierungen ist nur für bereits als „SerpsMods“ auf Steam veröffentlichten Code nötig; APIShared ist ausgenommen und immer kompatibilitätspflichtig → [apishared.md](AGENTS/apishared.md).

## 6. Pfade, Quellen und Werkzeuge

- **Spiel:** `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition`
- **Kanonische native DLL** (für Builds und Analysen immer diese): `…\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`. Die Workspace-Kopie `x86_64\CrusaderDE.dll` ist nur eine historische Vergleichsquelle.
- **Native Baseline:** `_inspect\CrusaderDE-Native-Baseline\CURRENT.md` / `CURRENT.json` → [native-analysis.md](AGENTS/native-analysis.md).
- **BepInEx-Log:** `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` (AppendLog). Jeder Spielstart beginnt mit einer Zeile nach dem Muster `[Message:   BepInEx] BepInEx ... - Stronghold Crusader Definitive Edition` (keine konkrete BepInEx-Version voraussetzen); deren Uhrzeit ist falsch, daher die Zeitstempel unserer eigenen Logs verwenden. Alte Logs: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\logs`.
- **Client-Log:** `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`, rein lesend per PowerShell `Get-Content -LiteralPath` (evtl. erhöhter Zugriff nötig). Der Client ist nicht immer erreichbar; ein Fehlschlag beweist weder falschen Pfad noch fehlende Datei → Nichterreichbarkeit melden und später erneut versuchen.
- **Script Extender:** lokaler, nicht zu ändernder Fork `shcde-script-extender` (https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender, Docs unter `/-/tree/main/docs`); publicized Assembly z. B. `shcde-script-extender\deps\Assembly-CSharp-publicized.dll` → [script-extender.md](AGENTS/script-extender.md).
- **Fixes-Mod:** kanonischer lokaler Klon `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main` (https://gitlab.com/rawra-stronghold-crusader/shcde-fixes) → [external-components.md](AGENTS/external-components.md).
- **Beispielmods:** Fixes (oben), https://github.com/richardbinder/Stronghold-Crusader-DE-AI-Buff, https://gitlab.com/ensrick7/crusader-de-tweaker; ohne Script Extender: `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\AIFramework_v1.13_for_SHCDEv1.043`.
- **SHC-HD-Quellen:** https://github.com/sourcehold, https://github.com/UnofficialCrusaderPatch (teils lokal unter `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\HD sources\UCP`), https://github.com/gynt?tab=repositories&type=source, `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\HD sources\Stronghold Crusader HD reversed`.
- **KI-Lords:** `C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\CustomLords` und `ExtendedLords` enthalten `.lordjson` (AIC: grobes KI-Verhalten wie Wirtschaft und Militär) und `.aivjson` (AIV: Burgaufbau, Mauern, Gebäudeplatzierung). Editor: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor`.
- **Werkzeuge:** `git` (unter `C:/Program Files/Git/mingw64/libexec/git-core`), `dotnet`, `ilspycmd`; weitere Reverse-Engineering-Werkzeuge in `.tools` und `.native-analysis`.
- **Python:** nicht im PATH (`python`/`python3` schlagen fehl); portables WinPython unter `D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe` direkt aufrufen.

## Verträge, veränderliche Strukturen und Momentaufnahmen

Code, Tests und Dokumentation sollen sich auf ausdrücklich definierte Verträge und überprüfbare Eigenschaften stützen. Veränderliche Implementierungsdetails, Verzeichnisstrukturen, Namen, Versionen und Arbeitszustände dürfen nicht unbegründet als dauerhaft vorausgesetzt werden. Situative Aussagen müssen als Momentaufnahme mit Zeitpunkt, Bezug und Verifikationsweg erkennbar sein; sie dürfen spätere Arbeit nicht durch veraltete Bedingungen blockieren. Exakte Erwartungen sind zulässig, wenn gerade diese Eigenschaft Prüfgegenstand oder Bestandteil eines verbindlichen Vertrags ist. Den Grund dafür ausdrücklich dokumentieren.

Verbindliche Sicherheitsgrenzen, veröffentlichte Identitäten und hashgebundene native Verträge bleiben maßgeblich. Diese Regel erlaubt weder das Abschwächen notwendiger Prüfungen noch das Aktualisieren einer Erwartung allein, damit ein fehlgeschlagener Test besteht. Dauerhafte Arbeitsregeln von datierten Zustandsberichten trennen; historische Nachweise unverändert erhalten.

## Kurze Pfade und Long-Path-Unterstützung

Pfade möglichst kurz und übersichtlich halten, insbesondere für Build-, Test-, Analyse- und temporäre Arbeitsverzeichnisse. Unnötige Verschachtelung sowie lange Hashes oder GUIDs in Verzeichnisnamen vermeiden; vollständige Identitäten bei Bedarf in Metadaten speichern. Eigener Code und eigene Werkzeuge sollen lange Pfade unterstützen, soweit die verwendete Plattform dies ermöglicht. Kurze Pfade und Long-Path-Unterstützung ergänzen einander. Bei Pfadänderungen alle aktiven Referenzen, Prüfungen und Konfigurationen gemeinsam aktualisieren; historische Nachweise unverändert erhalten.

### Orientierung in `.work` für neue Chats

- `.work/b/<branch>/<laufnummer>` enthält Build-Arbeitskopien, `.work/t/<suite>/<laufnummer>` Test-Fixtures und `.work/a/<thema>/<laufnummer>` Analysen oder ausdrücklich angelegte Entwicklungsarbeitskopien. Die Laufnummer ist keine Commit- oder Versionskennung. `.work/c/apishared-<branch>` ist der jederzeit neu erzeugbare SDK-Cache der Buildtreiber (siehe [build-release.md](AGENTS/build-release.md#short-development-paths)).
- Beim Fortsetzen vorhandener Arbeit zuerst den lokalen Index `.work/INDEX.json` lesen, sofern vorhanden. Er benennt geschützte Entwicklungsarbeitskopien und zugehörige Nachweise. Aktiven Entwicklungsstand, Artefakte und abgeschlossene Nachweise unterscheiden; „Git-ignoriert“ bedeutet nicht „entbehrlich“.
- Identitäten aus benachbarten `*.run.json`, `*.build.json`, SDK-Auswahlnachweisen und Git bestimmen; nie aus der kurzen Ordnerbezeichnung raten. Vor Änderungen oder Bereinigung zusätzlich `git worktree list --porcelain`, Branch/HEAD und `git status --short` im betroffenen Repository prüfen. Indexangaben sind datierte Beobachtungen und können veralten; sie ersetzen diese Prüfungen nicht. Angaben wie „uncommitted“, „detached“ oder ein Buildfehler niemals als unveränderliche Voraussetzung behandeln. Veraltete Angaben nach Prüfung berichtigen, ohne historische Nachweise umzuschreiben.
- Neue Verzeichnisse mit den vorhandenen WorkPaths-/TestWorkDirectory-Helfern vergeben; keine Laufnummer selbst auswählen. Bei neuen geschützten Entwicklungsarbeitskopien oder deren Übergabe den lokalen Index aktualisieren. Vorhandene Ordner nicht für einen neuen Lauf wiederverwenden.
- `.work` niemals pauschal löschen. Uncommittete Quellen und Nachweise erhalten; registrierte Git-Worktrees ausschließlich über Git bzw. die zuständige Worktree-Verwaltung entfernen. Fehlende Metadaten sind keine Löschfreigabe. Ein inzwischen sauberer Arbeitsbaum beweist allein nicht, dass Arbeit integriert oder entbehrlich ist: auch noch nicht integrierte oder nicht übertragene Commits und benötigte Nachweise berücksichtigen. Schutzvermerke nach belegter Sicherung bzw. Integration und im Rahmen des autorisierten Auftrags überprüfen und aktualisieren; sie behaupten keinen dauerhaft unveränderten Git-Zustand.
