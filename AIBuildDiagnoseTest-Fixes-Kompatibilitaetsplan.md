# Übergabe: AIBuildDiagnoseTest mit den installierten Release-Mods kompatibel machen

Stand: 16.10.2026. Dieser Auftrag ist unabhängig von der parallel geplanten APIShared-Korrektur sofort umsetzbar. Die zentrale Korrektur ist noch nicht als installiert oder getestet vorauszusetzen.

## Auftrag und verbindliche Grenzen

Nur Dateien unter `Testmods/AIBuildDiagnoseTest` ändern. Den nativen `trade-controller`-Detour bei RVA `0x3F270` vollständig aus der künftigen Installation entfernen, einschließlich ausschließlich dafür benötigter Verträge und Erwartungen. Andere Diagnosefunktionen und Kauf-/Verkaufsbeobachtungen erhalten.

Keine Änderungen an APIShared, BugfixesAndQoL, Shared, Script Extender, Fixes oder anderen Mods. Keine Manipulation von RDI oder anderen Registern, kein Ersatz-Hook, keine zusätzliche native Umleitung und keine Abhängigkeit von neuen APIShared-/BugfixesAndQoL-Entwicklungsständen. Keine Umschaltung eines bereits veröffentlichten Hooks: Die Entfernung gilt für den nächsten Prozessstart mit dem geänderten Diagnose-Mod; niemals einen laufenden Prozess zurückpatchen.

Bestehende Nutzeränderungen erhalten; den Workspace auf `develop` belassen. Geltende AGENTS.md und ausgelöste Themendateien vor Codeänderungen lesen. Runtime-/JSON-/Lifecycle-Inventur durchführen. Betroffene Textdateien verwenden CRLF. Keine Versionsänderungen, Commits oder Veröffentlichungen. In dieser Arbeitsphase keine Builds oder Installation; nur Prüfungen ohne Kompilierung ausführen. Spielabnahme und ein späterer Diagnose-Mod-Build benötigen einen gesonderten Auftrag.

## Ursache und Beweislage

Die bestätigte fehlerhafte Instruktion liegt im Weizenverkaufs-Hook des externen Fixes-Mods. Der Diagnose-Detour macht dessen ungültige Annahme über den Aufruferzustand sichtbar; die Diagnosebeobachtung ist deshalb mit dem vorhandenen Release-Fixes nicht sicher kompatibel.

Referenzstände:

| Bestandteil | Identität |
| --- | --- |
| CrusaderDE.dll, installierte kanonische DLL | SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` |
| Fixes, installierte Assembly 1.26.4.0 | SHA-256 `6A4ED4F492812BDA9342766AC432954F483732CC864C4CAAD67F9172A4F9E5BA` |
| Native Baseline | `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` und dessen referenzierte Generation |

Die bereits untersuchte relevante Kette ist: nativer KI-Aufrufer `0x2AE40` → Handelscontroller `0x3F270` → Verkaufsroutine `0x3EE10` → Fixes-Inline-Hook bei `0x3F0C9` → eigentlicher Verkauf `0x29700`. Der Handelscontroller prüft den Markt und wechselt zwischen Kauf- und Verkaufsverarbeitung; sein Verkaufspfad übergibt die tatsächliche Spieler-ID in EDX. Die Referenz gilt ausschließlich für den oben genannten Native-Hash.

Die Verkaufsroutine speichert EDX am Funktionseinstieg nach `[rsp+0x10]` und reserviert anschließend `0x58` Stackbytes. Damit liegt die Spieler-ID im Funktionskörper bei `[rsp+0x68]`. Der Fixes-Hook pusht RDX und R8; an seiner Ladeinstruktion liegt die ID folglich bei `[rsp+0x78]`. Er liest stattdessen `mov edx,[rsp+0x50]`. Dieser Slot enthält das gesicherte RDI des Aufrufers. Dessen Wert ist kein vertraglicher Spielerparameter.

Beim normalen nativen Aufruf hält der vorgelagerte Code die Spieler-ID zufällig auch in RDI. Im untersuchten verwalteten Diagnose-Aufrufweg wird RDI dagegen legitim für den Profilzeiger verwendet; die Argumente werden korrekt in RCX und RDX übergeben. Der Übergang ruft das Original genau einmal auf. Ein korrekt implementierter Fixes-Hook müsste mit beliebigen ABI-konformen RDI-Werten funktionieren.

### Konkreter Crash vom zweiten Prozessstart

Quellen im Spielverzeichnis `E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition`:

- `BepInEx/LogOutput.log` enthält die beiden Starts und Diagnosemarker.
- `BepInEx/crashdumps/SHCDESE-crash-2026-10-16-18-47-38-772-pid53724-tid54124.log`.
- `BepInEx/crashdumps/SHCDESE-crash-2026-10-16-18-47-38-777-pid53724-tid54124.dmp`.

Im Dump liegt der Fixes-Stub bei `0x7FFBCAAD5560`. Die fehlerhafte Flag-Abfrage liegt bei Stuboffset `+0x19`, also `0x7FFBCAAD5579`: `cmp byte ptr [r8+rdx],0`. R8 ist der Flag-Array-Zeiger `0x26746026040`, RDX ist `0x3E96C950`, der untere Teil des gesicherten Profilzeigers. Die daraus berechnete ungültige Leseadresse ist `0x26784992990`. Die tatsächliche Spieler-ID ist 1; RDI am Fehlerort ist 9, also Weizen. Der zusätzliche Push innerhalb des Flag-Helfers erklärt, warum die echte Spieler-ID zum Zeitpunkt des Fehlers bei Crash-RSP `+0x80` statt `+0x78` liegt.

Die Ladeinstruktion beginnt bei Stuboffset `+0x09`; ihr Displacement-Byte liegt bei `+0x0C`. Der geprüfte vollständige Stub umfasst 75 Byte und springt nach RVA `0x3F0DC` zurück. Diese Angaben sind Beweismaterial, kein Auftrag an den Diagnose-Mod, diesen Stub zu verändern.

Der Steinreserve-Hook von BugfixesAndQoL bei `0x3F156` überschneidet sich nicht mit der Fixes-Stelle. Sein untersuchter Wrapper erhält den Register-/Stackzustand; er erklärt den falschen Zugriff auf den gesicherten RDI-Slot nicht. Für diesen Crash ist die fehlerhafte Fixes-Spieler-ID-Ladeinstruktion direkt belegt. Daraus folgt keine pauschale Fehlerfreiheit aller übrigen Mods.

### Weshalb derselbe Save beim ersten Start weiterlief

Der erste untersuchte Lauf lud den Save um 18:38:25 und verarbeitete Tick 70 um 18:38:27.67 ohne diesen Prozessabbruch. Der zweite lud um 18:47:36 und brach bei Tick 70 um 18:47:38.77 ab. Ein erfolgreicher erster Lauf widerlegt den ungültigen Speicherzugriff nicht: Die Verwendung eines Pointerteils als Flag-Index kann je nach Prozesslayout unterschiedliche Adressen erreichen. Für den ersten Lauf fehlt ein entsprechender Register-/Speicherdump; seine konkrete falsche Adresse und deren Lesbarkeit sind deshalb nicht bewiesen. Speicherlayoutabhängigkeit ist eine plausible Erklärung, keine vollständig rekonstruierte erste Ausführung.

## Konkrete Änderungen am Diagnose-Mod

1. In `src/Diagnostics/EconomyNativeHooks.cs` die Installation `ActionPair(context, 0x3F270, "trade-controller")` entfernen. `ActionPair` und `PairAction` werden auch von `purchase-decision` und `ai-recruitment` verwendet und müssen erhalten bleiben.
2. In `src/Diagnostics/EconomyHookContract.cs` den ausschließlich für `0x3F270` vorhandenen Eintrag samt Fingerprint entfernen. Weitere ausschließlich diesem Detour dienende Vertrags-/Testdaten über eine modbegrenzte Suche ermitteln und bereinigen; gemeinsame Prüfungen behalten.
3. In `src/Diagnostics/EconomyDiagnostics.cs` die Sonderbehandlung und Stage-Erwartungen für `trade-controller` prüfen. Aktive Erwartungen entfernen, die weiterhin vollständige Handelscontroller-Beobachtung versprechen oder nach dessen Entfernung fehlende Callbacks als Fehler werten. Eine ausdrücklich als nicht verfügbar markierte Coverage-Kategorie darf zur Erklärung der Lücke bestehen bleiben.
4. Explizit melden, dass die Gesamtbeobachtung des Handelscontrollers aus Kompatibilitätsgründen nicht installiert wird. Vorhandenes Coverage-System verwenden und den Grund verständlich benennen. Fehlende Controller-Beobachtungen dürfen weder als „Handel wurde nicht aufgerufen“ noch als negative Markt-/Entscheidungsbelege erscheinen. Fehlende Evidenz bleibt unbekannt.
5. Beobachtungen `purchase-decision` bei `0x3EC90`, `ai-buy` bei `0x29650`, `ai-sell` bei `0x29700` sowie unabhängige Preis-/Extender-Ereignisse erhalten. Aus diesen Teilbeobachtungen keine vollständige Beobachtung des Markt-Gates, der Kauf-/Verkaufsalternierung oder aller abgebrochenen Verkaufsentscheidungen ableiten.
6. `UpdateToNewDLL.md` aktualisieren: `0x3F270` aus der Liste installierter Hookverträge nehmen; die native Funktion darf als Kontext und bewusst nicht instrumentierte Kompatibilitätslücke dokumentiert bleiben. `docs/DIAGNOSTICS.md` und betroffene Tests entsprechend anpassen. README-Dateien nicht ohne gesonderten Auftrag ändern.

## Prüfungen und Abnahmekriterien

Jetzt ohne Kompilierung durchführen und protokollieren:

- Modbegrenzte Suche nach `3F270`, `3f270` und `trade-controller`: kein installierbarer Detour und kein exklusiver Maschinenvertrag verbleibt. Bewusste Dokumentations-/Coverage-Erwähnungen einzeln begründen.
- Gemeinsame Helfer und verbleibende Hookverträge sind erhalten; Kauf-/Verkaufsbeobachtungen rufen das Original weiterhin genau einmal auf und kapseln Diagnosefehler wie zuvor.
- Keine neue native Mutation, Registerkorrektur, Ersatzinstrumentierung, Entwicklungsabhängigkeit oder Änderung außerhalb des Diagnose-Mod-Verzeichnisses.
- Vorhandene statische Prüfer nur ausführen, wenn der konkrete Aufruf weder kompiliert noch installiert. `verify.ps1` zuvor auf Build-/Testaufrufe prüfen; nicht ungeprüft starten.
- Regressionstests vorbereiten: Controller nicht im Installations-/Vertragsbestand; verbleibende Kauf-/Verkaufsstufen vorhanden; Coverage benennt die Lücke; fehlender Controller erzeugt keine falsche negative Evidenz. Vorhandene Backendtests so anpassen, dass sie den entfernten Vertrag nicht mehr erwarten. Kompilierte Tests noch nicht ausführen.
- Diff und CRLF prüfen. Im Abschluss klar zwischen bestandener statischer Prüfung, vorbereiteten Tests und ausstehender Spielabnahme unterscheiden.

### Spätere Spielabnahme nach gesonderter Buildfreigabe

Nur den geänderten Diagnose-Mod für die Abnahme bauen/installieren. APIShared, BugfixesAndQoL, Fixes und Script Extender bleiben auf den bisherigen installierten Release-Versionen; keine Entwicklungs-DLL als verdeckte Voraussetzung verwenden. Beim späteren Verbraucher-Build die Release-APIShared-Auswahl gemäß Workspace-Regeln festhalten und sicherstellen, dass sie zum getesteten Releasebestand passt.

1. Vorher installierte Versionen/Hashes, Fixes-Weizen-Einstellung und aktivierte Mods festhalten; die Einstellung für den reproduzierten Fehlerfall aktiv lassen.
2. Spiel vollständig beenden und frisch starten. `test_dusty.sav` laden und deutlich über Tick 70 hinaus laufen lassen. Coverage-Lücke, erhaltene Kauf-/Verkaufsmarker und fehlende Callbackfehler prüfen.
3. Mindestens drei getrennte Prozessstarts mit demselben Save wiederholen; zusätzlich wiederholtes Laden innerhalb eines Prozesses prüfen. Prozessstarts getrennt auswerten, weil das fehlerhafte Verhalten vom Speicherlayout abhängen kann.
4. Keine Abstürze sowie weiterhin plausible Handelsbeobachtungen dokumentieren. Fehlende Einzelmarker zunächst auf tatsächlich fehlende Handelsaktionen oder andere Coverage-Lücken prüfen; nicht ohne Beleg als Hookerfolg oder Fehlerfreiheit werten.
5. Logs/Crashartefakte erhalten. Ein erfolgreicher Test bestätigt die entschärfte Diagnose-Konfiguration, nicht die Beseitigung des zugrunde liegenden Fehlers im unveränderten Fixes-Mod für beliebige andere Aufrufer.

## Abgrenzung zur parallelen APIShared-Arbeit

Der andere Bearbeiter implementiert separat auf `APIShared/dev/Serpens66` eine eng validierte, einmalige Startkorrektur des Fixes-Displacements `0x50` auf `0x78`. BugfixesAndQoL bleibt auf `develop` und erhält keinen parallelen Runtimefix. Diese Arbeiten werden nicht gebaut oder installiert und sind keine Voraussetzung für diesen Sofortauftrag.

Auch nach einer späteren zentralen Korrektur den Controller-Detour nicht automatisch wieder einführen. Eine erneute Gesamtbeobachtung braucht einen eigenständigen Auftrag und Kompatibilitätsnachweis. Der aktuelle Diagnose-Auftrag endet mit dem entfernten Detour, erhaltenen Teilbeobachtungen und einer ehrlichen Dokumentation der verbleibenden Lücke.
