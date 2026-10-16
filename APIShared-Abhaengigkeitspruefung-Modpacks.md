# APIShared: frühe Abhängigkeitsprüfung für Modpack-Module

Stand: 16. Oktober 2026. Korrigierter Arbeitsplan; die produktive Erweiterung ist noch nicht implementiert. Zielbranch für eine spätere Umsetzung: `dev/Serpens66`. Beim erneuten Prüfen während der Testmod-Erstellung steht APIShared auf `dev/Serpens66` (zuvor `main`); den vorhandenen Checkout weder ändern noch ungefragt umschalten. Bei paralleler Arbeit einen passenden isolierten Checkout verwenden. Keine Commits oder Veröffentlichungen ohne Nutzerauftrag.

## Problem und nachgewiesener Ablauf

Der separat installierte `AIBuildDiagnoseTest_Serp` 0.1.2 benötigt `BugfixesAndQoL_Serp` mindestens 1.0.181. Installiert ist BugfixesAndQoL 1.0.192 innerhalb von `BepInEx/plugins/SerpsMods_Serp/Mods/BugfixesAndQoL_Serp`, einschließlich gültiger `info.json` und DLL.

Script Extender 2.14.1 meldet trotzdem beim Start, dass BugfixesAndQoL nicht installiert sei. Seine frühe Asset-Mod-Suche berücksichtigt nur direkte Plugin-Unterordner und dortige `.semod`-Pakete. Die enthaltenen Modpack-Module registriert der SerpsMods-Host erst später. Der frühe Versionsindex kennt das enthaltene Modul deshalb noch nicht.

Im BepInEx-Log vom 16.10.2026 ist der Fehlalarm bestätigt:

- 15:26:40.272: vermeintlich fehlende Abhängigkeit BugfixesAndQoL.
- 15:26:41.596: Nutzer setzt mit „Ja“ fort.
- Danach laden APIShared 0.9.2, Serps Mods, BugfixesAndQoL 1.0.192 und AIBuildDiagnoseTest 0.1.2.
- 15:26:55.614: `AI_BUILD_DIAGNOSTIC_READY`, Observer, Scheduler- und Route-Hook bereit; allgemeine Suchhooks ebenfalls bereit.
- Der Chainloader schließt den Start ab. Das bestätigt den Fehlalarm dieser Abhängigkeitswarnung, nicht sämtliche Gameplay-Funktionen.

Die installierte BugfixesAndQoL-DLL enthält die benötigten internen Diagnoseschnittstellen und `InternalsVisibleTo("AIBuildDiagnoseTest")`. Ein bloßes Versionsproblem liegt hier nicht vor.

## Zeitliche Grenze und direkte Codekorrektur

Die Prüfung läuft bereits im `Awake()` des Script Extenders: `ConfirmModCompatibility()` ruft `GameAssetModManager.PrepareAllAndCheckCompatibility()` auf. Das normale APIShared-Plugin hängt vom Script Extender ab und startet danach. Eine Korrektur allein im bisherigen APIShared-`Awake()` kommt zu spät.

Auch `BepInEx.Bootstrap.Chainloader.PluginInfos` liefert zu diesem Zeitpunkt noch kein vollständiges Installationsinventar. In der installierten BepInEx-Version wird ein Eintrag jeweils unmittelbar vor dem `AddComponent()` des entsprechenden Plugins eingetragen. Später geladene Abhängigkeiten fehlen dort noch.

Script-Extender-Code kann grundsätzlich durch unsere Komponente im Speicher korrigiert werden, etwa mit einem verwalteten IL-Hook oder Detour. APIShared nutzt solche IL-Hooks bereits für andere Laufzeitkorrekturen. Der entscheidende zusätzliche Bedarf ist ein garantierter Einstieg **vor** dieser frühen Prüfung. Die originale `SHCDESE.dll` und der externe Quellbaum sollen dabei unverändert bleiben.

## Korrigierter Umsetzungsplan und Entscheidung

Aktualisiert am 16.10.2026 nach erneuter Prüfung. Das Ziel soll langfristig auch für reguläre Mods umgesetzt werden. Freigegeben sind zunächst isolierte Testmods und schrittweise Machbarkeitsnachweise. Änderungen an APIShared, Veröffentlichungen und eine produktive Integration sind damit noch nicht freigegeben.

Die Lösung ist technisch plausibel und für wiederkehrende Modpack-Abhängigkeiten sinnvoll. Sie erhält echte Warnungen und vermeidet falsche Fehlermeldungen. Der Preis sind ein empfindlicher früher Einstieg, versionsgebundene Wartung und aufwendigere Paket-/Updateverträge. Statische Quellprüfung beweist weder die Hookfähigkeit im tatsächlichen Unity-Mono-Prozess noch fehlerfreie Updates. Diese offenen Punkte sind explizite Testschranken.

### 1. Früher Einstieg

- Eigenständige BepInEx-Preloader-Begleitassembly: leere `TargetDLLs`, erforderliche leere `Patch(AssemblyDefinition)`. Finish richtet ausschließlich Trace und Listener ein. Die frühe Hookinstallation wurde nach dem Startfehler verworfen.
- Finish wurde als Beobachtungseinstieg nachgewiesen, nicht als sicherer Hookzeitpunkt. Neuer Testansatz: synchrones Chainloader-ready-Ereignis nach ThreadingHelper/Logging und vor regulärem Start; erst Observe ohne Hook, dann separat freigeschalteter aktiver Versuch.
- Kein zusätzlicher Patch des Unity-Einstiegspunkts. Kein Versuch, SHCDESE über `TargetDLLs` zu erreichen. Scheitert der Finish-Ansatz, neu bewerten statt automatisch einen anderen Startpfad einzubauen.
- Frühe Typen enthalten keine Unity-/MonoMod-/Chainloader-/HookRuntime-Typverweise. Spätes Setup nur bei expliziter, buildgebundener Aktivierung nach bestandener Beobachtung. NoInlining allein hat den Startfehler nicht verhindert.
- Einen permanent verwurzelten ILHook auf `Chainloader.Start` installieren. Vorher Binäridentität, Signaturen, vollständige Einfügepunkte, Stack und Kontrollfluss prüfen; bearbeitete Methodenkopie mit dem verwendeten Backend kompilieren, ohne sie auszuführen.
- Snapshot nach tatsächlicher Gewinnerauswahl und topologischer Sortierung. Den geprüften Übergang nach Assembly-Laden und vor dem Extender-`AddComponent` für den SE-Hook verwenden. Auch Assembly-Cache-Treffer müssen den Übergang durchlaufen.
- Nur den lokalen Installations-/Versionsindex von `PrepareAllAndCheckCompatibility` ergänzen. Originale Abhängigkeitsprüfung, Asset-Discovery, `_preparedCandidates`, Registrierung und Ausführung bleiben erhalten.
- Unbekannte Binärdateien, geänderter IL-Vertrag oder unsicherer Zustand: Ergänzung inaktiv lassen, ursprüngliche Prüfung weiterlaufen lassen. Hooks nie bei Unity-Cleanup zurückbauen; Deaktivierung logisch bzw. vor Installation beim nächsten Prozessstart.

### 2. Inventar und konservativer erster Funktionsumfang

- Tatsächlich ausgewählte BepInEx-Gewinner verwenden, keine zweite Dateisuche mit nachgebauten Auswahlregeln. `Chainloader.PluginInfos` allein ist zu früh unvollständig.
- BepInEx berücksichtigt Prozessfilter, Gewinner je GUID, Inkompatibilitäten, harte/optionale Abhängigkeiten und fehlgeschlagene frühere Plugins. Statische harte Abhängigkeitsketten sowie bereits bekannte Ladefehler müssen in die Eignungsentscheidung eingehen.
- Ein späteres erfolgreiches `Awake` lässt sich damit nicht garantieren. Die frühe Prüfung sagt nicht die vollständige Laufzeitfähigkeit voraus.
- Zunächst nur verschachtelte BepInEx-Plugins mit eigener gültiger `info.json` anerkennen. Vom DLL-Ordner aufwärts nur bis zum direkten Plugin-Unterordner suchen. Das nächste Manifest ist maßgeblich; ein ungültiges oder unpassendes Manifest nicht überspringen und durch ein Elternmanifest ersetzen.
- GUID und normalisierte Version müssen zur ausgewählten BepInEx-Metadateninstanz passen. Ungültige Versionen ausdrücklich ausschließen: Der SE-Kandidatenleser kann trotz ungültiger Version einen Kandidaten liefern.
- Den geprüften SE-Kandidatenleser für dessen Manifest-/AssetMode-/KillSwitch-Verträge verwenden, temporäre Quelle anschließend freigeben. Niemals Kandidaten in `_preparedCandidates` eintragen oder Assets vorregistrieren.
- Originale Einträge haben Vorrang. Nur bisher fehlende GUIDs ergänzen; Groß-/Kleinschreibungs-Konflikte ausschließen, Duplikate diagnostizieren. Keine Root-Version überschreiben.
- Ursprüngliche inklusive Min-/Max-Prüfung und Fehlersemantik erhalten. Keine pauschale Abschaltung, keine entfernten harten Abhängigkeiten, keine künstlichen PluginInfos-Einträge.
- Keine Unterstützung beliebiger verschachtelter reiner Asset-Mods in Stufe 1. Auch nicht ungefragt alle verschachtelten Manifeste zu zusätzlichen prüfenden Verbrauchern machen.
- Generisch für fremde GUIDs/Modpacks; keine SerpsMods-Sonderbehandlung.

### 3. Spätere APIShared-Integration und Paketierung

Erst nach bestandenen Testschranken, isoliert auf Basis des Entwicklungsstands. Vor Beginn tatsächlichen Branch/Commit neu prüfen; den vorhandenen Checkout nicht automatisch umschalten. Kein APIShared-Code wurde für den Machbarkeitsnachweis geändert.

- Begleitprojekt vom normalen `src/**/*.cs`-Compile-Glob trennen. Keine frühe Referenz auf die vollständige APIShared-Runtime und kein frühes Laden ihrer Assembly.
- Festes internes Paarmanifest: Schema, APIShared-Version, Runtime-SHA256 und Bootstrap-SHA256. Feste erlaubte Zielpfade; keine vom Manifest beliebig vorgegebenen Dateiziele. Manifest unter dem Pluginbaum normal inventarisieren.
- Nur bei übereinstimmendem ausgewähltem APIShared, Runtime, Bootstrap und Manifest aktivieren. Gemischte, verwaiste oder unklare Installation bleibt inaktiv. APIShared verwendet dafür seinen eigenen internen DependencyFreeJson-Parser, ohne Workspace-Quellabhängigkeit.
- Build, Installation, Release-Staging, Quell-/Binärnachweise, Offline-Publisher und beide Paketprüfer aktualisieren: `tools/WorkshopPackage.ps1` UND `src/Workshop/WorkshopMapPackage.cs`.
- Nur exakten zusätzlichen Bootstrap-Pfad erlauben. Öffentliche pluginrelative Inventory-v1-Semantik und allgemeine Verbraucherpaket-Regeln nicht aufweiten.
- Update-Löschung und Abonnement-Entfernung getrennt behandeln. Bestehende Ownership-Prüfung erwartet genau einen aktiven Eigentümer; für Abbestellen ist ein gesonderter Kontext nötig. Nur exakt eigene, hashbestätigte Bootstrap-Datei ohne konkurrierenden Eigentümer oder neue gestagte Fassung entfernen. Keine rekursive allgemeine Patcherbereinigung.
- Externe Paketverwaltung (`SCDEModManagerLaunchId`) respektieren. Unbekannte/veränderte Reste erhalten und inaktiv lassen; manuelle Entfernung dokumentieren.
- Der Extender-Updater kopiert Dateien sequenziell, ohne belegte Transaktion/Rollback. Gleichversionige Pakete werden nicht zuverlässig als Reparatur erneut installiert. Deshalb keine automatische Selbstheilung versprechen: Spiel schließen, vollständiges geprüftes Paket erneut installieren. Paarprüfung verhindert nur die Aktivierung einer gemischten Erweiterung, nicht sämtliche Folgen einer defekten Installation.
- Die Erweiterung wirkt erst nach vollständigem Neustart; beim Start vor ihrem Download kann der alte Fehlalarm noch auftreten.
- Windows-Update/Entfernung vollständig testen. Proton/Linux separat prüfen; nicht aus Windows-Ergebnissen ableiten.

### 4. Schrittweise Machbarkeitsnachweise

Jede Stufe hat ein eigenes Abnahmekriterium. Nach fehlgeschlagenem Nachweis keine nächste Eingriffsstufe aktivieren.

| Stufe | Versuch | Abnahme |
| --- | --- | --- |
| 1: Frühe Beobachtung | Separater Preloader + Diagnoseplugin. Finish, Chainloader-Kopie, tatsächliche Auswahl, Einstieg vor SE-AddComponent, Eintritt in Originalprüfung und Abschluss protokollieren. Keine Prüfergebnisse ändern. | Vollständiger neuer Spielstart, eindeutige Markerfolge, unveränderte Warnung, nutzbares Menü und Render-Marker nach nachgewiesener Zerstörung der ursprünglichen Plugin-Komponente. |
| 2: Inventar im Schattenbetrieb | Ausgewählte Plugins, Manifestzuordnung, statische harte Ketten und bereits bekannte Fehler auswerten; vorgeschlagene Ergänzungen nur protokollieren. | Kontrollierte Fixtures für Duplikate, Prozessfilter, Inkompatibilität, harte/optionale Abhängigkeiten, ungültige/nächste Manifeste und Versionen; Vergleich mit realem Loader. Originalzustand bleibt unverändert. |
| 3: Begrenzte Korrektur im Testmod | Ausschließlich den lokalen Prüfindex ergänzen, mit explizitem Testschalter. | Passende verschachtelte Abhängigkeit wird erkannt; fehlende, zu alte/zu neue, ungültige und kollidierende Abhängigkeiten bleiben Fehler. Min/Max-Grenzen und echte Ablehnung im Dialog prüfen. |
| 4: Laufzeit und Koexistenz | Mit/ohne Modpack, deaktiviert, unbekannte Binärdatei, vorhandene Patcher/KillSwitch, Fixes und APIShared. | Plugin-/Assetregistrierung genau einmal, normale Ladefolge, Missionsstart und späterer Verbraucher-Tick. Keine neuen Ausnahmen/Startfehler. |
| 5: Paketlebenszyklus | Isolierte Integrationspakete: Installation, Upgrade, Teilkopie, Versionsmischung, Abbestellen, externer Manager und manuelle Wiederherstellung. | Nur geprüfte Paare aktiv; keine fremden Dateien gelöscht; dokumentierter Wiederherstellungsweg praktisch bestanden. |

Stufe 1 wird unter `Testmods/DependencyBootstrapProbe` aufgebaut. Sie benötigt APIShared nicht und ersetzt dessen bestehende Hooks nicht. Die aktuelle frühe Diagnose ist kein fertiger Fix; die bekannte Warnung muss noch erscheinen. Details und Testbefehle stehen in `Testmods/DependencyBootstrapProbe/TESTPLAN.md`.

Vor jedem Build: lokale Struktur-/Vertragsprüfung, `Test-SharedBoundaries.ps1`, `Test-DependencyMetadata.ps1`, permanente Hookprüfung und gezielte Lebensdauer-/JSON-Prüfung des Testmods. Spielnachweise getrennt von statischen Prüfungen und erfolgreicher Kompilierung dokumentieren. Keine bestandenen Spieltests allein aus READY ableiten.

## Prüfgrundlage und relevante Quellen

- Installiertes BepInEx 5.4.23.5: Chainloader, Preloader/AssemblyPatcher wurden aus den installierten Assemblies untersucht.
- BepInEx.dll SHA256: `8255B28902886085C578B9E427D3073C97002DB85176D2090CDEDA90EF14CE70`.
- SHCDESE.dll 2.14.1 SHA256: `E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3`; lokaler Fork Commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, Tree `c238441ea12db35ee36c5c5668d3c8fce0897295`. Andere bereits für Workshop-Hooks bekannte Hashes sind nicht automatisch für diese Erweiterung freigegeben.
- `shcde-script-extender/src/SHCDESE.BepInEx/Bootstrap/Plugin.cs`: Awake und ConfirmModCompatibility.
- `shcde-script-extender/src/SHCDESE.BepInEx/API/Components/ModManager/GameAssetModManager.cs`: Kandidaten, Versionsindex und Fehlerprüfung.
- `APIShared/src/Core/APISharedPlugin.cs`, `src/Workshop/WorkshopCleanupRuntime.cs`, `WorkshopOwnership.cs`, `WorkshopMapPackage.cs` sowie `tools/Build.ps1`, `tools/Release/Release.ps1`, `tools/WorkshopPackage.ps1`.
- `SerpsModsHost/src/SerpsModsHostPlugin.cs`: spätere Modulregistrierung.
- `Testmods/StartupPerformanceDiagnostic/patcher/StartupTimingPatcher.cs`: vorhandenes Patcherbeispiel; kein Beweis für die neue Hookkette.
- Der BepInEx-Log ist appendend. Tests nur anhand des letzten vollständigen Startabschnitts auswerten, mit eigenen Millisekunden-Zeitstempeln.

Status am 16.10.2026: Beobachtung und aktive Hookkette im Unity-Mono-Spielstart bestanden (Details unten). Die Schattenstufe wird vorbereitet; produktive Korrektur und APIShared-Integration bleiben offen. Keine Extender- oder APIShared-Quelländerungen für diesen Testauftrag.
## Fortschritt des isolierten Machbarkeitsnachweises

Der erste Spielstart nach dem Erstbuild (16.10.2026, 16:32 Uhr) erreichte Preloader.Finish und den späteren Render-Callback nach Startup-Cleanup. Die Hookvorbereitung scheiterte vor Veröffentlichung am Cecil-Resolver für BepInEx. Das ist ein konkreter Implementierungsfehler der Probe; die Hookkette selbst ist noch nicht nachgewiesen.

Nächster korrigierter Teststand: resolverfreie Closure-Zuordnung über IL-Feldreferenzen plus Reflection gegen die geladene Assembly; direkte statische Beobachteraufrufe statt nicht unterstützter Inline-Delegate-Referenzen in isolierten ILContext-Kopien; identischer Transformationscode im Desktop-Test; beide Methodenkopien vor Installation im Spiel kompilieren/vorbereiten; SHA256-Buildidentitäten und separate aktive/deaktivierte Logabnahme. Details und offene Spielnachweise stehen im TESTPLAN des Testmods. Die Desktop-CLR kann Unity-ECalls nicht vollständig JIT-vorbereiten; dieser Nachweis bleibt ausdrücklich dem tatsächlichen Unity-Mono-Start vorbehalten. APIShared-Integration und Indexergänzung bleiben zurückgestellt.

## Korrektur nach schwarzem Bildschirm (16.10.2026)

Der aktive Patcher-Build von 17:06:49 verursachte einen Startfehler; Entfernen des Patchers stellte den Start wieder her. Der frühe Finish-Hookansatz ist damit praktisch nicht freigegeben. Er wird ersetzt: Finish richtet ausschließlich Trace und Listener ein. Standard ist Observe ohne Hookvorbereitung. Erst nach bestandener Beobachtung darf der separate aktive Versuch die Vorbereitung synchron bei Chainloader ready ausführen (nach ThreadingHelper/Logging, aber vor der abschließenden _initialized-Zuweisung; keine manuellen Initialisierungsaufrufe). Die ursprüngliche Fehleroperation ist noch nicht abschließend isoliert. Auch der spätere Zeitpunkt ist erst nach eigenem Spieltest freigegeben. Details, Buildbindung, Tests und gezielte Entfernung stehen im aktualisierten Testmod-TESTPLAN. Die verschobenen fehlerhaften Desktop-Dateien bleiben unverändert.

## Abgenommene Hookkette und Schatteninventar (16.10.2026)

Observe bestand um 18:34 und 18:47 Uhr, Active um 19:22 Uhr: beide Methodenkopien transformiert, Delegate erzeugt und in Unity Mono JIT-vorbereitet; 21 Gewinner vor SE-Start erfasst, genau ein Eintritt in die Originalprüfung, unveränderte zwei Warnungen, Chainloader-Abschluss und Post-Cleanup-Render. Nutzer bestätigt normalen Start. Für Stufe 1 sind nach Nutzerentscheidung weder Save-/Simulationstest noch ein zusätzlicher deaktivierter Kontrollstart erforderlich. Spätere Laufzeit-/Koexistenzprüfungen für die tatsächliche Korrektur bleiben davon unberührt.

Gesicherter Lauf samt Binäridentitäten: `_inspect/DependencyBootstrapProbe/Active-accepted-20261016-193043`. Die Abnahme gilt nur für diese geprüften Loaderidentitäten und beweist keine zukünftigen Awake-Erfolge aller Plugins.

Die gemeinsame Offline-/Runtime-Schattenauswertung verwendet die ausgewählten Gewinner, begrenzte nächste Manifestzuordnung, harte transitive Pluginabhängigkeiten und konservative Manifestabhängigkeiten. Offline bleiben geeignete Kandidaten ungeklärt hinsichtlich Live-Index, KillSwitch und früheren Ladefehlern. Aktueller Offlinebefund: beide betroffenen Testmods benötigen BugfixesAndQoL mindestens 1.0.192, ausgewählt ist 1.0.192; Manifestidentität und statische harte Ketten passen. Quelle: `shadow-offline.json` im gesicherten Analyseordner; aktuelle DLL-Metadaten sind kein historischer Hashnachweis für sämtliche Gewinner.

Der nächste Spielversuch vergleicht ausschließlich eine separate Kopie des tatsächlichen lokalen Extender-Index. Originaleinträge bleiben autoritativ. SE-Kandidatenleser prüft zusätzliche Quellen, temporäre Quellen werden freigegeben; keine Assetregistrierung oder Änderung an vorbereiteten Kandidaten. Die Original-Versionsprüfung bewertet beide Indizes, die reale Prüfung und Warnungen laufen danach unverändert. Einzelentscheidungen und Zählerinvarianten sind zwingende Logabnahme. Finish und Listener bleiben gegenüber dem bestandenen Build unverändert; bestehende Hash-/IL-/Stack-/Lebensdauerprüfungen werden erweitert, nicht abgeschwächt. Details im Testmod-TESTPLAN.
## Schattenstufe bestanden; begrenzte Korrektur im Testmod

Am 16.10.2026 um 19:46 Uhr wurde die Schattenstufe praktisch bestanden: 21 Gewinner, 10 geeignete zusätzliche Anbieter, 8 Anforderungen, erwartete Fehlerzahl 2 -> 0 bei unverändertem Originalindex. Beide tatsächlichen Warnungen blieben vorhanden, Start und Post-Cleanup-Render erfolgreich, keine gefundenen Startfehler. Gesicherte Evidenz: `_inspect/DependencyBootstrapProbe/Shadow-accepted-20261016-1946`. Weiterhin keine Garantie zukünftiger Awake-Erfolge beliebiger Plugins.

Nächster isolierter Versuch: Nur mit separatem, Patcher-hashgebundenem Korrekturschalter wird die vollständig vorbereitete Kopie als lokale Indexvariable vor der unveränderten SE-Prüfschleife eingesetzt. Das ursprüngliche Dictionary bleibt unverändert. Vorbereitung scheitert -> ursprünglicher Index; keine nachträgliche Ergebnisfilterung oder Dialogunterdrückung. Tatsächliche Übernahme und originale Rückgabe werden getrennt nachgewiesen.

Eine eigene leere Asset-Testkomponente liefert drei echte negative Anforderungen für denselben Start: fehlende Test-GUID sowie zu hohe Mindest-/zu niedrige Höchstversion des geprüften Anbieters. So müssen die zwei bisherigen Fehlalarme verschwinden, während genau drei Kontrollfehler bleiben. Bestehende Modmanifeste bleiben unverändert. Kontrollmanifestinstallation nur über den Buildtreiber; nach erfolgreicher Abnahme bei geschlossenem Spiel exakt und hashgeprüft entfernen. Details und Befehle im Testmod-TESTPLAN. Der neue Korrektur-Spielnachweis und die spätere APIShared-Integration sind noch offen.