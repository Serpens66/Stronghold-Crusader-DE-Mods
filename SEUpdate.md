# Script-Extender-Update: wiederverwendbare Checkliste

Diese Datei ist die allgemeingültige Arbeitsgrundlage für Updates des lokalen SHCDE Script Extenders und aller davon abhängigen Workspace-Mods. Release-spezifische Erkenntnisse gehören zusätzlich in einen eigenen Updateplan oder Abschlussbericht. Die Regeln aus `AGENTS.md` gelten immer und haben Vorrang.

Der bevorzugte Einstieg ist der idempotente Gesamt-Treiber:

    & 'Shared\ScriptExtenderUpdate\Invoke-ScriptExtenderUpdate.ps1' -OldVersion <alt> -NewVersion <neu> -OldTag <alter-tag> -NewTag <neuer-tag> -TargetCommit <commit> -VersionMode <Existing|Patch|Explicit>

Die feste Inventur liegt in `Shared\ScriptExtenderUpdate\mods.json`. `-Resume` setzt nach einem Fehler am letzten erfolgreichen Build fort. Ohne `-ExtenderDir` wird ausschließlich die installierte DLL in `BepInEx\plugins\000shcdese` verwendet; fehlt sie, muss ein alternativer Pfad ausdrücklich angegeben werden.

## Fast Path

- [ ] Alte Version/Tag: `<alt>` / `<alter-tag>`
- [ ] Neue Version/Tag: `<neu>` / `<neuer-tag>`
- [ ] Zielcommit: `<vollständiger-commit>`
- [ ] `origin/main`, `upstream/main`, lokaler Branch und Zieltag verglichen
- [ ] Kanonische `CrusaderDE.dll`: `<vollständiger-sha256>`
- [ ] `CURRENT.json` und semantische Baseline stimmen mit DLL und Extender-Commit überein
- [ ] Commitliste, Changelog und vollständiger Tag-Diff ausgewertet
- [ ] Öffentliche C#- und Lua-Verträge sowie native/RedBird-Verträge verglichen
- [ ] Betroffene Runtime-Mods: `<liste>`
- [ ] Notwendige Codeänderungen: `<liste-oder-keine>`
- [ ] Mindestversionen, Pluginabhängigkeiten, Modversionen und Changelogs konsistent
- [ ] JSON, CRLF, Lifecycle und Paketgrenzen geprüft
- [ ] Extender gebaut und installierte Version/Hashes bestätigt
- [ ] Mods in Abhängigkeitsreihenfolge gebaut und installiert
- [ ] Lokale und installierte Pakete per Dateiliste und SHA-256 identisch
- [ ] Tests/Abnahme: `<ergebnis>`

## 1. Präflight und maßgebliche Version

1. Workspace- und Extender-Arbeitsbaum mit `git status` prüfen. Bestehende Benutzeränderungen nicht überschreiben.
2. Remotes, Branch, Tags und Commit prüfen. `origin` ist der eigene Fork, `upstream` Rawras Originalprojekt.
3. Den offiziellen Upstream-Tag und `upstream/main` rein lesend mit den lokalen Referenzen vergleichen. Für ein echtes Update ausschließlich die lokale `shcde-script-extender\update.bat` verwenden; Quellbäume niemals per ZIP oder Dateikopie ersetzen.
4. Vollständigen Zielcommit notieren und kontrollieren, dass der Arbeitsbaum danach sauber auf diesem Commit steht.
5. Versionen von folgenden Artefakten getrennt prüfen, weil Quellbaum, Buildausgabe und Installation auseinanderlaufen können:
   - `src\SHCDESE.BepInEx\bin\net481\SHCDESE.dll`
   - `mod_output\000shcdese\SHCDESE.dll` und dessen `info.json`
   - installierte `BepInEx\plugins\000shcdese\SHCDESE.dll` und deren `info.json`
6. Die installierte DLL ist die kanonische Buildreferenz. Lokale `bin`- und `mod_output`-Artefakte werden trotzdem nach dem Extender-Build auf Version und Hash geprüft; sie werden niemals stillschweigend als Ersatz gewählt.

## 2. Änderungen zwischen den Releases analysieren

1. Changelog, chronologische Commitliste, Dateistatistik und vollständigen Diff von `<alter-tag>..<neuer-tag>` erfassen.
2. Änderungen nach Vertrag klassifizieren:
   - öffentliche C#-Methoden, Properties, Felder und Typen;
   - Signaturen, Rückgabewerte, Null-/Fallbackverhalten und Sichtbarkeit;
   - Enums, numerische Werte und umbenannte Member;
   - EventArgs, veränderbare Werte, Hookphasen und Rückgabe in den nativen Aufrufer;
   - Lua-Exporte, Namen, Argumente und Rückgabestrukturen;
   - Interop-Strukturen, Feldnamen, Offsets, Größen und Alignment;
   - ID-/Indexbasis und Konvertierungsgrenzen;
   - native Detours, Context-Hooks, AOBs, Funktionsziele, Call-Sites und RedBird/PolyHook-Verträge;
   - Assets, XAML, Modformate, Konfigurationsdefaults und paketierte Abhängigkeiten.
3. Reine Implementierungs- oder Performanceänderungen von echten Aufruferänderungen trennen.
4. Neue Features separat dokumentieren. Sie werden nicht automatisch in vorhandene Mods eingebaut.
5. Verdächtige Extender-Verträge im Extender-Quellcode und, falls nötig, gegen die kanonische native Analyse belegen. Den Extender-Fork nicht ändern; stattdessen einen kurzen englischen Markdown-Report für den Autor verfassen.

## 3. Workspace und Kompatibilität inventarisieren

1. Alle C#-Runtime-Mods, Lua-/Asset-Mods, Quellmanifeste, generierten Paketmanifeste, Projekte und `build.bat`-Treiber inventarisieren. Analyseprojekte und eigenständige CLI-Werkzeuge getrennt behandeln.
2. Für jeden Runtime-Mod erfassen:
   - maßgebliches Quellmanifest und generierte Kopien;
   - `PluginVersion`, Assembly-/Paketversionen und weitere aktive Versionsstellen;
   - `MinimumScriptExtenderVersion`, `MaximumScriptExtenderVersion` und `BepInDependency`;
   - direkte, reflektive und per String/Lua erfolgende Extender-Nutzung;
   - native Hooks, Pointer, Structfelder, RVAs/AOBs und RedBird-Typen;
   - harte und weiche Modabhängigkeiten sowie notwendige Buildreihenfolge.
3. Exakt nach entfernten und umbenannten Symbolen suchen. Zusätzlich die Klassen mit semantisch geänderten Methoden und alle betreffenden Lua-Namen durchsuchen.
4. Direkte Span-/Arrayzugriffe und ID-APIs erneut auf ihre dokumentierte 0-/1-Basis prüfen. Die Basis niemals aus erfolgreichen Einzelzugriffen erraten.
5. Bei API-Änderungen den kleinsten bestätigten Ersatz verwenden. Alte Adapter oder Fallbacks nur nach ausdrücklicher Entscheidung behalten.
6. Auch bei keinem Suchtreffer jeden Runtime-Mod gegen die neue Extender-DLL kompilieren; nur der echte Compiler deckt Signatur-, Assembly- und transitive Abhängigkeitsprobleme vollständig auf.

## 4. Native Baseline und Sicherheitsverträge

1. Vor jeder nativen Schlussfolgerung `_inspect\CrusaderDE-Native-Baseline\CURRENT.md` und `CURRENT.json` lesen.
2. SHA-256 der installierten kanonischen `CrusaderDE.dll` ermitteln und mit `CURRENT.json` sowie dem `binaryHash` der verwendeten Datensätze vergleichen.
3. Bei Hashabweichung fail-closed arbeiten: alte RVAs, VAs, AOBs und semantische Aussagen nur als historische Hinweise behandeln und keine alten Adressen anwenden.
4. Bei unverändertem Spielhash `Build-SemanticBaseline.ps1 UpdateForScriptExtender` mit Ziel- und Vorgängercommit verwenden. Der Modus erneuert Quellenwissen, AOB-Ergebnisse, Index und schnelle Validierung; aktuelles Ghidra läuft nur bei Änderungen an Detours, Interop oder nativen Headern.
5. Die Extender-Provenienz wird über vollständigen Commit, Git-Tree-Hash und einen sauberen getrackten Arbeitsbaum gesichert. Ignorierte lokale Buildartefakte gehören nicht zur Quellenidentität.
6. Neue oder geänderte native Hooks zusätzlich auf Funktionsgrenzen, Call-Site-vs.-Funktionsziel, Register-Liveness, Stack, Flags, ABI und vollständige Ersatzblocklänge prüfen.

### Prüfmatrix

| Änderung | Erforderliche Baseline-Schritte |
|---|---|
| Nur Managed API, Lua, Dokumentation oder Paketlogik; Native-Hash unverändert | Extender-Quellenwissen, AOB-Abgleich, Index, `ValidateFast` |
| Detours, AOBs, Interop oder native Header; Native-Hash unverändert | Zusätzlich aktueller Ghidra-Import/-Export; keine historische Neuerzeugung |
| `Assembly-CSharp.dll` geändert | Managed-Metadaten, Decompilation und Managed/native-Links erneuern |
| `sharedassets1.assets` oder Extraktionswerkzeug geändert | Ressourcen/XAML erneut extrahieren und indizieren |
| `CrusaderDE.dll` geändert | Vollständige neue hashgebundene Roh- und semantische Baseline; Fast Path gesperrt |

Historische Ghidra-Exporte werden bei reinen Extender-Updates nicht neu erzeugt. `Validate` öffnet beide Projekte vollständig; `ValidateFast` prüft Hashes, Git-Tree, kuratiertes Wissen, Datenbank, JSON und Adressverträge ohne Ghidra-Start.

## 5. Metadaten und Versionen anpassen

1. Das maßgebliche `info.json` ist die gebündelte Quelle für `MinimumScriptExtenderVersion` und `MaximumScriptExtenderVersion`. Der zentrale Treiber validiert jeden vorhandenen Grenzwert, prüft, ob die Zielversion im definierten Bereich liegt, und gleicht `BepInDependency` automatisch an ein vorhandenes Minimum an. Fehlen beide Felder oder sind beide leer, besteht für diesen Mod keine Extender-Versionsbedingung und die Versionsprüfung wird vollständig übersprungen. Modtests dürfen die Extender-Version nicht erneut als Literal führen, sondern müssen vorhandene Grenzen aus dem Manifest lesen und fehlende Grenzen ebenfalls ignorieren.
2. Vor einer Modversionsänderung modweit alle aktiven Vorkommen der alten Version suchen. Patchversionen nur erhöhen, wenn der Benutzer dies festgelegt hat oder die Anpassung final freigegeben ist.
3. Version atomar in Plugin-Konstanten, Quellmanifesten, Manifest-/Build-/Releasekonfigurationen und sonstigen aktiven Metadaten ändern.
4. Einen neuen obersten `SerpChangelog`-Eintrag mit derselben neuen Modversion anlegen. Historische Einträge nicht umschreiben.
5. Generierte oder installierte Paketmanifeste nicht als Ersatz für Quellen behandeln. Sie werden vom vorgesehenen Build neu erzeugt.
6. Nach den Änderungen erneut modweit nach alter und neuer Version suchen. Alte aktive Vorkommen korrigieren; bewusste historische Vorkommen einzeln klassifizieren.
7. README-Dateien nur auf ausdrücklichen Wunsch ändern.

## 6. Prüfungen vor dem ersten Build

1. Sämtliche geänderten JSON-Dateien parsen und Pflichtfelder, aktuelle Version, Mindestversion und obersten Changelog-Eintrag prüfen.
2. Alle geänderten Textdateien auf CRLF und auf versehentliche wörtliche `\\r\\n`-Sequenzen prüfen.
3. Runtime-Lifecycle statisch prüfen: kein Prozess-Teardown in Startup-`OnDestroy`, keine langfristige Logik auf einer früh zerstörbaren Plugin-Komponente und eine dokumentierte Runtime-Verwurzelung.
4. Projekt- und Paketreferenzen prüfen. SHCDESE-, RedBird-, R3- und zentral gelieferte Laufzeit-DLLs dürfen nicht unbeabsichtigt privat mitgeliefert werden.
5. Alle relevanten statischen Tests und Codekontrollen abschließen, bevor irgendeine Mod-`build.bat` ausgeführt wird.

## 7. Build, Installation und Abnahme

1. Zuerst die unveränderte Extender-`build.bat` direkt aus PowerShell mit erhöhten Rechten und `/nopause` ausführen. Nicht über `cmd /c` oder `Start-Process` verschachteln.
2. Extender-Buildausgabe, `mod_output` und Installation auf Assembly-, Datei-, Produkt- und Manifestversion sowie relevante Dateihashes prüfen. Bei Mods ist `PluginVersion` der bestehende aktive Versionsvertrag; eine nicht gepflegte Windows-`FileVersion` darf nicht mit der Modversion verwechselt werden.
3. Mod-Builds nacheinander in Abhängigkeitsreihenfolge ausführen. Harte Bibliotheks-/API-Anbieter vor Konsumenten bauen. Jeder Treiber übernimmt Build, Paketierung und Installation.
4. Bei einem Fehler den betroffenen Treiber, Exitcode und letzten erfolgreichen Stand festhalten. Eindeutige Infrastrukturfehler sicher wiederholen; bei mehreren fachlichen Lösungswegen anhalten und den Benutzer entscheiden lassen.
5. Nach allen Builds Quell- und Paketmanifeste erneut prüfen. Lokale und installierte Modpakete anhand relativer Dateiliste, Größe und SHA-256 vergleichen.
6. Prüfen, dass keine unerlaubten privaten Abhängigkeiten, veralteten Dateien oder abweichenden Manifestkopien im Paket verblieben sind.
7. Git-Status und Diff auf unerwartete oder sachfremde Änderungen kontrollieren.
8. Ein Spielstart gehört nur dann zur Abnahme, wenn er angefordert oder für den Vertrag erforderlich ist. Dann im aktuellen BepInEx-Log den neuen Startabschnitt und mod-eigene Marker nach dem Startup-Cleanup nachweisen.

## 8. Abschlussbericht

Der Abschlussbericht nennt mindestens:

- alte und neue Extender-Version, Tag und vollständigen Commit;
- Version und Hash der kanonischen Spiel-DLL;
- lokale und installierte Extender-Versionen/Hashes;
- geänderte und unveränderte API-, Lua-, Interop- und Native-Verträge;
- betroffene Mods und tatsächlich vorgenommene Codeänderungen;
- Modversionen, Mindestversionen und Ergebnis der atomaren Konsistenzprüfung;
- Tests, Buildstatus und Paket-/Installationsvergleich;
- verbleibende Risiken oder erforderliche Laufzeittests;
- neue Extender-Funktionen und konkrete, aber noch nicht automatisch implementierte Einsatzmöglichkeiten in den Workspace-Mods.
