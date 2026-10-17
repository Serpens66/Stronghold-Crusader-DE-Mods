# APIShared, Shared-Quellen und Mod-Abhängigkeiten

**Lesen, wenn:** APIShared genutzt, erweitert, getestet oder veröffentlicht wird, Dateien unter `Shared/` betroffen sind oder Mod-Abhängigkeiten/Mindestversionen geändert werden. Im APIShared-Checkout gelten zusätzlich dessen `AGENTS.md` und `CONTRIBUTING.md`. Gilt zusätzlich zur Root-`AGENTS.md`.

## Quellzuständigkeit

- `Shared/Runtime`: nur gemeinsam quellverlinkte Hilfen unserer Mods, nach Funktion gegliedert. `Shared/Adapters/APIShared`: modseitige Adapter für die öffentliche APIShared-Assembly. `Shared/Tools`: Workspace-Werkzeuge. Details: `Shared/Docs/SOURCE_OWNERSHIP.md`.
- APIShared bindet keine Workspace-Shared-Quellen ein; Runtime-Verbraucher kompilieren keine internen APIShared-Quellen (Tests dürfen beide Seiten prüfen).
- APIShared-interne Grundimplementierungen werden unabhängig gepflegt, nicht automatisch synchronisiert; Fehlerkorrekturen auf Relevanz für beide Seiten prüfen. Modspezifisches in modseitigen Hilfen/Adaptern ergänzen.
- Ordnerpfade sind keine C#-Namespaces; bei reinen Verschiebungen bleiben Namespaces und XAML-Typidentitäten unverändert. Aktive Pfade in Projekten, Treibern, Tests und Anweisungen gemeinsam migrieren; historische Nachweise nicht umschreiben.
- Vor jedem Runtime-Build zusätzlich `Shared/Tools/Validation/Test-SharedBoundaries.ps1` (Quelllinks, Runtime-JSON/Lifecycle/Scheduling, XAML-Wurzeln, permanente Hooks).
- Modgebundene Befehlsruntime: `BugfixesAndQoL/src/UnitCommands` (Namespace `BugfixesAndQoL.UnitCommands`); Formationen: `BugfixesAndQoL/src/Formations`. MoatMove nutzt den internen Anbieterweg und das lokale Paket der BugfixesAndQoL-Assembly, keine Runtime-Quelllinks auf diese Engine. Allgemeine Routensuchereignisse: `APIShared/src/Pathfinding/Routes`. Native Algorithmen, Hooks und Featureeinstellungen gehören nicht allein wegen eines gemeinsamen Addons nach APIShared.

## APIShared nutzen

- Vor Implementierungsentscheidungen [API-Katalog](../APIShared/docs/API_CATALOG.md), [Integrationsleitfaden](../APIShared/docs/THIRD_PARTY_GUIDE.md) und [Architektur](../APIShared/ARCHITECTURE.md) (Abschnitt „Finding and extending a feature“) prüfen. Passende Extender-Ereignisse zuerst nutzen. Von APIShared koordinierte Stellen nur über dessen Capability oder Registrierungsbroker erweitern; kein konkurrierender Hook, keine Kopie der Implementierung.
- Verbraucher referenzieren die öffentliche `APIShared.dll` mit `Private=false`, deklarieren die benötigte Mindestversion konsistent in Plugin und Manifest und liefern keine eigene APIShared-DLL mit. Capability-Zugriffe über `ApiShared.ForMod` mit eigener stabiler Mod-GUID; statische Broker mit eigener GUID und stabiler owner-lokaler Registrierungs-ID. Verfügbarkeit und Registrierungsdiagnostik prüfen; native Dienste erst nach deren Initialisierung. `WhenReady` ist kein Thread-Dispatcher und keine Verfügbarkeitsgarantie.
- Registrierungen und Hooks bleiben prozessweit verwurzelt; Features über logischen Aktivierungszustand steuern. Reihenfolge, Thread, privaten `State`, Abbruch, Fehler und Pre/Accepted/Post gemäß Vertrag behandeln. Ein verwalteter Post-Aufruf beweist keine native Befehlsausführung oder erfolgreiche Rekrutierung. HUD-Ersetzungen erst nach allen Pre-Callbacks, damit spätere Teilnehmer abbrechen können. MP-Autorisierung und deterministische Featureentscheidungen bleiben beim Verbraucher.

## APIShared erweitern

- In APIShared gehören wiederverwendbare Dienste, Beobachtungen, Algorithmen und Hookstellen, die mehrere unabhängige Mods sinnvoll teilen; modspezifische Einstellungen, Aktivierung, Texte und Befehlsengines bleiben im Mod. Ein nativer Hook allein rechtfertigt keine Übertragung. APIShared bleibt mit beliebigen fremden GUIDs ohne Modpack, Workspace-Quelllinks oder Nachbar-Repos nutzbar.
- Erweiterungen beim fachlichen Thema einordnen, möglichst additiv. Gemeinsame Hookstellen: zentraler Besitzer, öffentliche, wo sinnvoll abbrechbare Pre/Post-Verträge; Accepted nur, wo Vorbereitungen nach allen Pre-Vetos nötig sind. Unabhängige Fehler, Vanilla-Semantik und dauerhafte Hooklebensdauer erhalten. Verträge, Beispiele und Verhaltenstests gemeinsam aktualisieren; erst unabhängige APIShared-Prüfungen, dann betroffene Verbraucherprüfungen (`Shared/Tools/Validation/Test-APISharedConsumers.ps1`; nicht an interne MSTest-Klassen des öffentlichen Repos koppeln).
- **Vor jeder APIShared-Codeänderung Breaking-Change-Risiken prüfen** — binär, quell- und verhaltensbezogen: entfernte/geänderte öffentliche Typen, Member, Signaturen, Namespaces, Identitäten; neue Pflichtmember bestehender Interfaces; geänderte Defaults, Payload-/ID-Bedeutungen, Reihenfolge, Thread, Timing, Verfügbarkeit, Lebensdauer, Abbruch-/Fehlersemantik, Nebenwirkungen, persistierte Formate. Auch ohne Signaturwechsel können fremde Mods brechen.
- Serpens66 und Ensrick sind gleichberechtigte Maintainer und dürfen ohne Freigabe des anderen integrieren und veröffentlichen. Bei Breaking Changes oder erheblichem Risiko ist Rücksprache vorher empfohlen, nicht verpflichtend. Betroffene API, altes/neues Verhalten, Folgen für fremde Verbraucher und kompatible Alternativen konkret dokumentieren; Tests und ggf. Migrationshinweise bereitstellen. Versionssprung, eigene bestandene Tests oder unbekannte Verbraucher beweisen keine Kompatibilität. Breaking Changes nicht verstecken, keinen ungefragten parallelen Fallback. Agenten committen oder veröffentlichen nur auf Nutzerauftrag.
- Die Rückwärtskompatibilität von APIShared gilt unabhängig von einer Steam-Veröffentlichung.

## Repository, Branches, Releases

- `APIShared/` ist ein vom Mod-Repo ignorierter eigenständiger Checkout von https://github.com/SHCDE-APIShared/APIShared und bleibt auf `dev/Serpens66`. Persönliche dev-Branches: Experimente ohne Testpflicht; `develop`: fertige, testbereite Änderungen mit bestandenen Quellprüfungen und spielunabhängigen CLI-Tests; `main`: releasefertig mit bestandenen CLI-Tests und bei Runtime-Änderungen passenden Build-, Integrations- und Spieltests. Einfache Doku-Korrekturen dürfen direkt auf `main` und werden danach in `develop` integriert.
- Persönliche Branches während laufender Arbeit nicht bewegen. Workflow: `Shared/Tools/ApiSharedRepository/WORKFLOW.md`; gelöschte Status-/Aktualisieren-BATs nicht wiederherstellen. APIShared und Mod-Repo unabhängig committen; kein APIShared-Quellzeiger im Mod-Repo. Neue Workspaces mit `Shared/Tools/ApiSharedRepository/Initialize-ApiShared.ps1` einrichten; vorhandene Ordner nie automatisch überschreiben. Kein automatischer Branchwechsel, Stash, Reset oder Commit im Entwicklungs-Checkout.
- Normale Verbraucher-Builds verwenden APIShared derselben ausdrücklich gewählten Modbranch (`/branch:main|develop`), jeweils den letzten lokalen Commit ohne Checkoutwechsel. Abweichendes `/apishared:main|develop|dev` vorher mit dem Nutzer festlegen; `dev` verwendet das bereits gebaute persönliche Entwicklungspaket. Details: [build-release.md](build-release.md).
- Workshop-Vorbereitung und Upload behalten den festgehaltenen öffentlichen `main`-Stand (isolierte Release-Worktrees unter `.release-output/APIShared/main/<commit>/source` sind erlaubt). Veröffentlichte Mindestversionen bleiben unverändert.
- Gewünschte GitHub-Schutzpolitik: erfolgreiche CI auf `main`, keine verpflichtende Review-Freigabe; Doku-Korrekturen durch beide Maintainer direkt möglich. Das behauptet keine bereits erfolgte technische Aktivierung.
- Releases über das `release.bat` des eigenständigen Repos; keine neuen APIShared-Releases im alten Mod-Repo. Das APIShared-`build.bat` sucht keine Nachbardateien. Admin-Einladungen erst nach ausdrücklicher Auswahl des Nutzers.

## Dokumentationspflege (vom Nutzer autorisiert)

- Öffentliche Verträge direkt im Code erklären; bei API-Änderungen Katalog, Integrationsleitfaden und betroffene Beispiele aktualisieren. Neue Features an passender Stelle dokumentieren, die kurze README bei Bedarf als Einstieg mit Verweisen ergänzen. Keine separate Feature-Doku verlangen, wenn Kommentare und Leitfäden genügen.

## Allgemeine Mod-Abhängigkeiten

- Versionierte `BepInDependency`-Attribute deklarieren Mindestversionen, keine exakten Pins; BepInEx-Ladeschutz beibehalten.
- Erforderliche Mod-GUIDs in `info.json` unter `Dependencies` mit GUID und optional `MinimumVersion`/`MaximumVersion` (vom installierten Extender unterstützt). `MinimumScriptExtenderVersion`/`MaximumScriptExtenderVersion` für die Hostdiagnostik beibehalten. SoftDependencies bleiben optional, nicht als harte Manifest-Abhängigkeit.
- APIShared-Mindestversionen stammen aus dem jeweiligen `info.json`; keine zweite Versionstabelle in Release-Konfigurationen.
- Tests prüfen Verhalten, gültige Versionsbereiche und Übereinstimmung Manifest/Plugin, ohne konkrete aktuelle Versionsliterale.
- Vor Builds `Shared/Tools/Validation/Test-DependencyMetadata.ps1` bzw. die unabhängigen APIShared-Prüfungen ausführen.
