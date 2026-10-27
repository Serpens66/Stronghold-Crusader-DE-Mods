# Native Analyse, Vanilla-Audit und Diagnose

**Lesen, wenn:** Vanillas nativer Code für Analyse, Planung, Implementierung oder Diagnose relevant ist, der Nutzer die Baseline verlangt, RVAs/Patterns/Offsets verwendet werden oder eine Diagnose Vanilla-Daten erst ermitteln soll. Bei nativen Hooks zusätzlich [native-hooks.md](native-hooks.md) lesen. Gilt zusätzlich zur Root-`AGENTS.md`.

## Kanonische Spiel-DLL

- Für Builds, Kompatibilitätsprüfungen und neue Analysen immer die installierte DLL verwenden: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`.
- Die Workspace-Kopie `x86_64\CrusaderDE.dll` ist nur eine möglicherweise veraltete Vergleichsquelle; Erkenntnisse daraus nennen SHA-256 oder Spielversion.

## Baseline finden und verwenden

- Zuerst `_inspect\CrusaderDE-Native-Baseline\CURRENT.md` lesen. `CURRENT.json` ist die maschinenlesbare Quelle für vollständigen Native-Hash, kurzen Semantic-Ordner und Datenbankmanifest. Keine Hashordner oder Datenbankpfade aus früheren Chats raten oder hart codieren.
- Vor Verwendung eines RVA, einer VA, eines Patterns, einer Funktionsgrenze oder einer semantischen Schlussfolgerung den SHA-256 der installierten DLL mit `CURRENT.json` und dem `binaryHash` des Datensatzes vergleichen. Bei Abweichung nur als Vergleichshinweis nutzen und fail-closed keine alte Adresse oder Bytefolge anwenden.
- Script-Extender- und Native-Provenienz ausschließlich aus `CURRENT.json` und den referenzierten Datensätzen bestimmen. Bei einem Extender-Update Commit, Git-Tree, Quellenwissen, Index und Validierung erneuern. Betroffene Ghidra-Exporte bei Änderungen an Detours, AOBs, Interop, nativen Headern oder gepflegten semantischen Namen/Typen/Kommentaren erneuern; historische Exporte bei unverändertem Native-Hash nicht neu erzeugen. Ein unveränderter Spielhash beweist nicht, dass die Extender-gebundene Wissensschicht aktuell ist.
- Struktur (absichtlich getrennt):
  - `<vollständiger Native-SHA-256>\`: unveränderte Rohbaseline (Scanbericht, Rizin-/Ghidra-Projekte, Roh-Exporte).
  - `sem\<kollisionsgeprüfter 8-Zeichen-Hash>\`: semantische Baseline, Provenienz, Managed-/XAML-Verknüpfungen, JSONL-Exporte, semantisches Ghidra-Projekt, `DATABASE_INFO.json`, `knowledge\*.md`.
  - `diff\<alter Kurz-Hash>-<neuer Kurz-Hash>\`: Versionszuordnungen und Änderungsberichte.
  - `tools\semantic\`: reproduzierbare Pipeline und Nur-Lese-Abfrageoberfläche.
- **Vanilla- und Mod-Wissen trennen:** Rohbaseline, Exporte, Ghidra-/Rizin-Projekte, Managed-/XAML-Exporte und `diff\` beschreiben nur Vanilla. Die Dokumente unter `sem\<Kurz-Hash>\knowledge\` sind in `CURRENT.md` nach Scope gegliedert; die maschinenlesbare Zuordnung steht in `knowledge\document-scopes.json` (`vanilla`, `vanilla+integration`, `mod`, `external`). Für Vanilla-Fragen zuerst die Vanilla-Abschnitte nutzen. Dokumente mit Scope `mod` oder `external` sind allein kein Vanilla-Beleg; dort nur Abschnitte mit `[Vanilla facts]` als Vanilla-Wissen verwenden.
- Neue oder geänderte `knowledge`-Dokumente: H1 endet auf `[Scope: Vanilla]`, `[Scope: Vanilla + mod integration]`, `[Scope: Mod work, not Vanilla]` oder `[Scope: External component]`. Mod-spezifische Abschnitte in Vanilla-Audits stehen unter eigenen Überschriften mit `[mod integration]`, wiederverwendbare Vanilla-Abschnitte in Mod- oder Fremddokumenten tragen `[Vanilla facts]`. Neue Dokumente zusätzlich in `document-scopes.json` und in der passenden `CURRENT.md`-Kategorie eintragen. Markierungen in bestehenden Zeilen anhängen, keine Zeilen davor einfügen: `native-field-catalog.json` und Analyse-Dossiers verweisen per Zeilennummer auf diese Dokumente. Vor dem Markieren prüfen, ob die Dokumentbytes per Hash gepinnt sind, ohne dass der Validator die Git-Historie auflöst (`DATABASE_INFO.json`-Eingaben, `knowledge/chore-evidence.jsonl`, `tools/semantic/audited-models.json`). Solche Dokumente bleiben unmarkiert und stehen unter `markers.untagged` in `document-scopes.json`. Ein Pin wird nie nur neu belegt, damit eine Prüfung besteht.
- Erste Suche bevorzugt mit `query.ps1` aus dem Workspace-Root statt große Decompiler-/JSONL-Dateien ungezielt zu lesen:
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' stats`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' search <Text>`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function <Name|VA|RVA>`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callers <Funktion>`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callees <Funktion>`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' managed <Methode>`
  - `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' diff <alter vollständiger Hash> <neuer vollständiger Hash>`
- `function` ist der bevorzugte Einstieg (Name, Vertrauensstufe, Signatur, Provenienz, Pseudocode, Strings, Globals, Caller/Callees, Managed-Aufrufer, Typen, Versionsmatch). Danach gezielt die in `CURRENT.md` verlinkten Exporte oder das semantische Ghidra-Projekt prüfen.
- Fehlt nach frischem Clone die nicht getrackte SQLite-/FTS5-Datei, ausschließlich mit `& '_inspect\CrusaderDE-Native-Baseline\tools\semantic\Build-SemanticBaseline.ps1' RestoreDatabase` wiederherstellen. Keine ältere Datenbank wählen, `DATABASE_INFO.json` im Restore-Modus nicht ändern, SQLite und `-wal`/`-shm`/`-journal` nie committen.
- Rohbaseline einschließlich ihrer Ghidra-/Rizin-Projekte sowie historische Nachweise unverändert lassen. Das durch `CURRENT.json` ausgewählte semantische Ghidra-Projekt ist dagegen eine gepflegte Wissensschicht: belegte Namen, Typen und Kommentare dürfen dort kontrolliert ergänzt oder korrigiert werden. Vorher Native-Hash, Evidenz, vorhandene manuelle Annotationen und aktuellen Arbeitsstand prüfen; Bedeutungssicherheit und Speichervertrag getrennt erhalten. Nur betroffene Exporte, Suchindex und Manifest konsistent aktualisieren, ohne historische Analysegenerationen umzuschreiben. Änderungen auf den beabsichtigten Bereich begrenzen und die unveränderte Rohbaseline nachweisen.
- Experimente mit ungesicherten Strukturänderungen oder spekulativen Typzuordnungen zuerst in einer Kopie unter kurzem, hashgebundenem Pfad durchführen. Dokumentierte unsichere Wissensansichten sind im semantischen Projekt zulässig, wenn ihre Grenzen sichtbar bleiben und daraus keine unbewiesenen Pointer-/Funktionssignaturen entstehen. Keine Spiel-DLL in die Baseline kopieren.
- Vertrauensstufen: `confirmed` = Exporte oder eindeutige Evidenz, `probable` = starke Zuordnung, `candidate` = nur Suchhinweis. Pattern-Treffer, Callgraph-Reichweite, Stringnähe, XAML-Bindings und Pseudocode beweisen allein kein Laufzeitverhalten. Unsichere Erkenntnisse mit Hash, RVA/VA, Quelle und Evidenz dokumentieren, nicht als bestätigte API behandeln.
- Neue, ausreichend sichere Erkenntnisse aus Arbeit, Tests oder Diagnose in die Baseline übernehmen.
- Hinweise unter `shcde-script-extender\ReverseEngineering` beachten.

## Verpflichtender featurebezogener Vanilla-Audit (Hard-Gate)

- **Pflicht**, sobald korrekte Analyse, Planung, Implementierung oder Diagnose davon abhängt, wie Vanilla ein Verhalten ausführt, Daten aufbaut oder Parameter weitergibt, oder der Nutzer die Baseline verlangt — unabhängig davon, ob ein Hook oder nur verwalteter Code geplant ist.
- **Zeitpunkt:** abgeschlossen *vor* dem ersten fachlichen Lösungsvorschlag, der Implementierungsentscheidung, dem Codeeingriff oder einem darauf aufbauenden Test. Bis dahin unbekannte Vanilla-Verträge nicht durch Annahmen ersetzen. Stichproben einzelner Funktionen, Call-Sites oder vermuteter Fehlerstellen genügen nicht.
- **Umfang („vollständig featurebezogen“):** geschlossene Relevanzkette entlang Daten- und Kontrollfluss, bis Eingaben, Entscheidungen, Seiteneffekte und Folgen belastbar verstanden sind. Kein wahlloser DLL-Vollscan, aber mehr als eine Fundstelle.
  - Von allen relevanten Einstiegspunkten die vollständigen Caller-/Callee-Ketten und alle verhaltensrelevanten Branches einschließlich Alternativ-, Abbruch-, Fehler- und Fallbackpfaden untersuchen; nicht bei der ersten plausiblen Funktion stoppen.
  - Für alle relevanten Parameter Herkunft, Reihenfolge, nativen/verwalteten Typ, Einheit, Signedness, ID-/Indexbasis und jede Transformation bis zur Verwendung nachvollziehen; ebenso gelesene/geschriebene Felder, Manager- und Pointerbasen, Offsets, Rückgabe- und Out-Werte.
  - Vorgelagerte Erzeuger/Writer und nachgelagerte Leser, Writer, Normalisierungen und terminale Zustände einbeziehen. Managed-Aufrufer, Script-Extender-APIs und vorhandene Baseline-Erkenntnisse gegen den nativen Datenfluss abgleichen.
- **Nachweis:** kurze Audit-Zusammenfassung als Zwischenmeldung an den Nutzer: Native-Hash, untersuchte Funktionen mit RVA/VA und Vertrauensstufe, Parameter- und Zustandsfluss, relevante Branches und Folgezustände, verbleibende Unsicherheiten. Suchtrefferlisten oder Funktionsnamen allein genügen nicht.
- **Fail-closed:** Bei Lücken oder Widersprüchen zwischen Baseline, Disassembly, Managed-Code und Extender-Vertrag nicht auf Vermutung implementieren, sondern gezielt weiter untersuchen. Statisch nicht klärbar → Unsicherheit benennen und ergebnisoffene Diagnose planen; bei mehreren wesentlich verschiedenen Wegen den Nutzer fragen.
- **Nachträglich:** Erklärt ein späterer Testfehler sich durch eine zuvor nicht berücksichtigte Baseline-Erkenntnis, gilt das als unvollständiger Audit: vor dem nächsten Fix Umfang, Parameterfluss und ausgelassene Pfade erneut prüfen und die Zusammenfassung korrigieren.

## Ergebnisoffene Diagnosen

- Soll eine Diagnose erst ermitteln, wie Vanilla-Daten, Parameter oder Zustände aufgebaut sind, keine unbestätigten Annahmen als Abbruchbedingung einbauen; unbekannte oder unerwartete Werte sicher und ausreichend vollständig protokollieren.
- Nur nachgewiesene Speichergrenzen, Pointergültigkeit und unmittelbar nötige Schutzprüfungen dürfen das Auslesen begrenzen. Vermutete ID-Bereiche, Statuswerte oder Zähler sind bis zur Bestätigung Diagnoseergebnisse, keine Vorbedingungen.
- Kann ein Wert aus Sicherheitsgründen nicht dereferenziert werden, Rohwert und Kontext protokollieren statt ihn zu verwerfen oder unter unbestätigter Bedeutung abzulehnen.

## Rizin und Cutter

- Rizin nur über `.native-analysis\Run-Rizin-With-Ghidra.cmd`, Cutter nur über `.native-analysis\Run-Cutter-With-Ghidra.cmd` starten (setzen `SLEIGHHOME` prozesslokal). Nach Cutter-Updates ggf. `CUTTER_ROOT` in beiden Wrappern anpassen und den Decompiler an einer kleinen Funktion testen.
- Rizin 0.8 verarbeitet über `-i` weder mehrzeilige CRLF-Skripte noch Semikolon-Befehlszeilen zuverlässig → für nichtinteraktive Audits mehrere getrennte `-c`-Argumente. `-Q` existiert nicht; `-q`/`-qq` verwenden. Jeden Aufruf zuerst mit `-c "iI"` smoke-testen.
- Erst klein und gezielt: Exporte, eindeutige Signaturen, auf passende PE-Sektionen begrenzte Scans, ggf. kleine lokale PE-Scanner; dann nur die Funktion mit `s <Adresse>`, `af`, `pdr`, `pdg`. Bei großen Funktionen `pdr` statt `pdf`. Xrefs/`aa` nur ergänzend; `aaa` gilt als Vollscan und ist der letzte Schritt. Analysen möglichst mit laufender Zwischenausgabe, damit sie bei ausreichenden Ergebnissen früh abgebrochen werden können.
- Laden/Relokalisieren des großen virtuellen Abbilds kann 1–2 Minuten CPU kosten; nicht unnötig neu starten. Lange Audits verborgen mit getrennten stdout-/stderr-Logs, Start-/Endzeit, CPU, Loggröße, Fortschritt und ETA ausführen.
- `.native-analysis\TraceOverlapAnalyzer` erwartet Rotationen in Grad (`0/90/180/270`), nie native Orientierungswerte (`0/2/4/6`; native `2` = `90`°). Vorher README/Usage prüfen.

## RVA, Signaturen und Layouts

- Jeden direkten oder abgeleiteten nativen RVA-Zugriff (Hook-/Funktionseinstiege, Patchstellen, Call-/Branchziele, RIP-relative Referenzen, Tabellen, feste Datenziele) in `UpdateToNewDLL.md` des Mods dokumentieren (bei Bedarf anlegen): besitzendes Feature, Referenz-Hash, Referenz-RVA, semantische Signatur/Herleitung, Suchbereich, Fallback, Fehlerverhalten, Ableitungen, fehlende Fallbacks, Updateprüfungen.
- Produktive Code-/Instruktionsziele besitzen, soweit technisch möglich, Referenz-RVA **und** einen auf passende PE-Sektionen begrenzten eindeutigen semantischen Pattern-Fallback. Bei passendem Hash zuerst die Bytes am Referenz-RVA validieren; scheitert das (z. B. wegen eines früheren Hooks), trotzdem den Pattern-Fallback versuchen.
- Aus Signaturen abgeleitete Einstiege, Call-/Branchziele und RIP-relative Adressen nur aus validierten vollständigen Instruktionen berechnen, gegen Modul-/Sektionsgrenzen prüfen und dokumentieren. Keine unbekannte DLL über ein altes RVA ansprechen.
- Eine passende Funktionssignatur beweist keine Struktur-, Tabellen- oder Feldoffsets. Ohne belastbaren semantischen Fallback den Grund in `UpdateToNewDLL.md` festhalten und nur das abhängige Feature hashgebunden lassen.
- Scheitern RVA-Prüfung, Fallback oder nachgelagerte Semantikprüfung: Error-Log mit Zeitstempel (Feature, Ziel, Grund); nur dieses Feature fail-closed deaktivieren bzw. die Hooktransaktion vollständig zurückrollen. Vanilla und unabhängige Funktionen bleiben aktiv; modweiter Abbruch nur, wenn der Mod ausschließlich aus diesem Feature besteht.
- Erfolgreiche Auflösungen mit Methode (`reference-rva` oder Pattern-Fallback) und tatsächlichem RVA auf Info-Level loggen.
- **Prüfkosten (Zielverfahren für Hash- und Adressprüfung):** Adressprüfungen kosten nur dann Startzeit, wenn sie bei jedem Start scannen.
  - **Bekannter Native-Hash:** keine Scans, nur Bytevergleich der Referenzbytes an den Referenz-RVAs. Eine Eindeutigkeitsprüfung per Vollscan ist hier nicht nötig, der Hash pinnt das auditierte Image.
  - **Unbekannter Hash:** einmalig den auf passende Sektionen begrenzten Pattern- bzw. RIP-Fallback ausführen. Das Ergebnis persistent unter `BepInEx/cache` speichern, mit dem vollständigen Native-SHA-256 und der Modversion als Schlüssel. Spätere Starts validieren einen Cachetreffer wie eine Referenz: Code per Bytevergleich, Datenadressen durch erneutes Dekodieren der Anker-Instruktion. Ein fehlender, beschädigter oder widersprechender Cache führt nur zu einem Neuscan, nie zu einem Fehler.
  - **Datei-Hash:** höchstens einmal pro Prozess berechnen und zwischen den Features teilen. Persistent über Pfad, Größe und Änderungszeit cachen, damit ein unverändertes Spiel nicht neu gehasht wird.
  - **Datenadressen:** soweit möglich aus RIP-relativen Operanden validierter Code-Anker ableiten; das Ziel muss in einer Datensektion des Moduls liegen.
  - **Strukturfeld-Offsets, Strides und Record-Größen** dürfen bei unbekanntem Hash aus Displacements bzw. Immediates abgeleitet werden. Voraussetzung: Die Instruktion liegt an einer festen Position innerhalb einer per Pattern validierten Funktion, und nur ihr Displacement ist Platzhalter. Zusätzlich muss jeder Wert durch mindestens einen unabhängigen zweiten Beleg bestätigt sein, etwa einen zweiten Anker mit demselben Feld, feste Feldabstände einer Feldkarte (z. B. Anzahl = Index + 2) oder die Übereinstimmung mit einer `imul`-/Stride-Konstante. Wie Adressen wird das Ergebnis einmal ermittelt, gecacht und per Bytevergleich bestätigt.
  - **Keine Ableitung:** Ohne zweiten Beleg oder ohne belastbaren Anker bleibt das Feature hashgebunden, mit Begründung in `UpdateToNewDLL.md`. Gameplay-relevante Werte im Multiplayer werden nie aus einem einzelnen Beleg übernommen.
  - **Gates:** pro Feature oder Teilfeature, nie pauschal für einen größeren Verbund, damit ein vorhandener Fallback erreichbar bleibt.
  - **Managed-Code:** den IL-Vertrag an den geladenen Methoden prüfen (Signatur, Muster, Probe-Transformation), statt die Assembly-Datei zu hashen. Die Modulidentität (MVID) nur prüfen, wenn Bytes von der Platte analysiert werden.
- Feldoffsets nie allein aus einem Displacement übernehmen: zuerst Managerbasis, Slot-Adressformel, 0-/1-basierte IDs und Headerabstand zum Extender-Strukturzeiger bestimmen. Die Field-Map mit mehreren bekannten Feldern und plausiblen Laufzeitwerten validieren und im Initialisierungslog ausgeben.

## Diagnose- und Zustandsregeln

- Diagnose-Hooks am frühesten gemeinsamen Ausführungspunkt setzen, der alle verdächtigen Zustände sieht.
- Einmal `hook confirmed` mit plausibler Objekt-ID, Zustand und Position loggen; ungültige Hook-Kontexte oder nicht auflösbare IDs mindestens einmal als Fehler loggen.
- Diagnoseberechnungen bilden Vanillas Semantik exakt nach (Distanzmetrik, signed/unsigned, Vergleichsrichtung, `<` vs. `<=`); keine Ersatzformel (z. B. Manhattan), bevor der tatsächlich ausgewertete Rückgabewert der nativen Hilfsfunktion geprüft ist.
- Vorher-/Nachher-Messungen mit prüfbaren Invarianten (z. B. `eligible == changed + remaining`) versehen oder Abweichungen begründen.
- Zustandszuweisung ≠ erfolgreiche Entfernung: Slot, Global-ID, Typ, Alive-State, Phase und Folgezustand verfolgen; vor einem Fix den Pfad bis zum terminalen Zustand inkl. späterer Leser, Normalisierungen und Writer untersuchen.
- Vanilla-Aufruf, Verhaltenskorrektur und Diagnose in getrennten Fehlerpfaden kapseln: Vanilla läuft genau einmal; Capture-/Logfehler verhindern weder Vanilla noch einen unabhängigen Fix; Fix- und Diagnoseerfolg getrennt loggen.
- Diagnose-Logging selbstvalidierend und begrenzt: Initialisierung, erster echter Callback, einmalige Validierungsabweichungen, Operationsergebnisse mit Ein-/Ausgabezählern; identische Wiederholungsfehler drosseln.
- Vor Interpretation eines Testlaufs maschinell prüfen: erwartete Marker vorhanden, keine Callbackfehler, Invarianten erfüllt, Wiederholungen über stabile Identitäten getrennt.
- `IsAlive` ist meist richtig, aber nicht direkt beim Spawn (oft noch `NeedsInit`).
- Bei intermittierenden Zustandsfehlern den kleinsten stabilen Übergang korrigieren, nicht Symptome in Zielwahl oder Bewegung übersteuern. Der Folgezustand muss Vanillas regulärem Pfad entsprechen und darf im nächsten Update nicht normalisiert werden.
