# Desync- und Resync-Diagnose: Konzept für Light und Deep

Stand der Quellenprüfung: **27.10.2026**. Status: **Konzept und statische Befunde; kein Hilfsmod implementiert, keine neue Ingame-Abnahme und keine Performance-Messung durchgeführt.**

## 1. Ziel und Aussagegrenzen

Ein dauerhafter Hilfsmod soll Desyncs und Resyncs möglichst aussagekräftig dokumentieren, ohne selbst Spielzustand oder Netzwerkablauf zu verändern. Dieselbe Modfassung bietet zwei aktive Diagnose-Stufen: **Light** für den normalen Spielbetrieb und **Deep** für gezielte Untersuchungen. Der Standard ist Light; Off deaktiviert die Aufzeichnung logisch.

Das System soll sowohl Vanilla-verursachte als auch modverursachte Abweichungen im instrumentierten Spiel erfassen. Es kann damit noch nicht jeden Fehler automatisch einer bestimmten Codezeile zuordnen. In einer vollständig unmodifizierten Partie ohne Hilfsmod stehen nur die dort tatsächlich erzeugten Spiel-/Unity-Logs zur Verfügung.

Die drei wichtigsten Fragen sind getrennt zu beantworten:

1. **Was wurde beobachtet?** Fehlender Befehl, unterschiedliche Zustandsprüfsumme, Resync-Start, Reparatur, Abbruch oder Mod-Invariantenfehler.
2. **Was unterscheidet sich?** Befehlsausführung, Prüftick, Kategorie, Objekt, Feld oder modinterner Zustand.
3. **Was hat es verursacht?** Ein Zusammenhang oder eine reproduzierbar nachgewiesene Ursache; weder die Reparaturquelle noch der zuletzt geloggte Mod ist automatisch der Verursacher.

Gleiche Prüfsummen beweisen keine vollständige Zustandsgleichheit. Ein abgeschlossenes Resync-Protokoll beweist nicht, dass alle Vanilla- und Modzustände übereinstimmen. Zeitliche Nähe zu einer Modaktion beweist keine Kausalität.

## 2. Geprüfte Grundlage und Provenienz

### 2.1 Identitäten des untersuchten Stands

Die installierten Dateien wurden erneut gegen [CURRENT.json](_inspect/CrusaderDE-Native-Baseline/CURRENT.json) und das dort ausgewählte [Datenbankmanifest](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/DATABASE_INFO.json) geprüft.

| Bezug | Identität |
|---|---|
| Installierte CrusaderDE.dll, SHA-256 | `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` |
| Installierte Assembly-CSharp.dll, SHA-256 | `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` |
| Extender-Quellenwissen laut Baseline, Commit | `4014ca67c96974a5e6d697665baf5351cbfebde6` |
| Extender-Quellenwissen laut Baseline, Git-Tree | `372b43a9e9d614075c3b827d3df2a2346ec356fa` |

Die Extender-Provenienz bezeichnet die Wissensgrundlage der Baseline, nicht automatisch die aktuell geladene Extender-Assembly. Ein späterer Diagnosebericht muss die tatsächlich geladenen Komponenten separat identifizieren.

Alle nachfolgenden RVAs gehören zum genannten Native-Hash. Sie sind Analysebelege, **keine Freigabe zur ungeprüften Verwendung als Hook- oder Datenadresse**. Die exportierten Funktionsnamen sind teilweise nur `candidate`; die hier beschriebenen Rollen folgen aus den untersuchten Kontroll- und Datenflüssen. Ungeklärte Feldbedeutungen bleiben ungeklärt.

### 2.2 Rekonstruierter Kernablauf

| Phase | Befund im untersuchten Build | Evidenz / RVA |
|---|---|---|
| Simulationslauf | Der native Lauf erreicht den Spiel-/Simulationspfad und dessen Scheduler. | `DLL_RunTick` `0x86680`, `0xCDE60`, `0xD1AD0` |
| Reguläre Prüfung | Im entsprechenden Netzwerkmodus wird bei durch 200 teilbarem Simulationstick die Prüfsummenbildung angefordert. Interne Sperr-/Aufschubzustände können die Berechnung verhindern oder verzögern. | `0xD1AD0` → `0x22020` |
| Resync-Anforderung | Der Hostpfad berücksichtigt aktive Teilnehmer und vergleicht nicht nur Summen, sondern auch deren Prüftick. Eine passende festgestellte Abweichung kann Chore 54 auslösen. | `0x27C50` |
| Resync-Start | Der Ausführungszweig von Chore 54 setzt den nativen Resync-Zustand und fordert eine erneute Prüfsummenbildung an. | Handler `0x15170` → `0x22020` |
| Kategorienvergleich | Nach dem Start werden 14 Kategorieaggregate gesammelt/verglichen; Chore 100 überträgt die Aggregate und Chore 101 die Abweichungsflags. | `0x19870`, `0x27960`, `0x19980` |
| Detailanalyse | Für markierte Bereiche werden feinere Prüfsummen verglichen; mehrere Datensätze können abweichen. Die Vorermittlung ist von der anschließenden Übertragung zu unterscheiden. | `0x26D30` |
| Reparaturversand | Abweichende Datensätze werden blockweise übertragen. Der Pfad berücksichtigt ein Übertragungsbudget und Fortsetzungspositionen. | `0x251F0` |
| Einheiten-Reparatur | Der Empfangszweig von Chore 56 übernimmt einen Einheiten-Datensatz mit `0x490` Byte und einen separaten gespeicherten Pfad mit 1000 Byte. Dabei existiert eine lokale Feld-Erhaltungslogik; empfangene Bytes sind deshalb nicht pauschal identisch mit dem endgültigen lokalen Record. | Handler `0x15710` |
| Abschluss / Fehlerpfade | Der Ablauf enthält Bestätigungs-, Warte-, Timeout- und Teilnehmerausschlusspfade. Der Abschlussweg sendet Chore 67; dessen Handler setzt unter anderem den Resync-Zustand zurück. | `0x28160`, Handler `0x16E40`; Steuerung in `0xCDE60` |

Die Detailanalyse mit den 14 Kategorien folgt also bereits auf den Resync-Start. Ein Hook auf die sichtbare Fortschrittsanzeige wäre kein verlässlicher frühester Diagnosepunkt. Die verwaltete Empfangsverarbeitung ruft `EngineInterface.ReceiveChore` auf, bevor sie anschließend ihre Resync-Anzeigefelder aktualisiert. Eingang, Einreihung, native Ausführung und sichtbare UI sind unterschiedliche Ereignisse.

Der Befund deckt den untersuchten Kernablauf ab. Die vollständige fachliche Benennung aller 14 Bereiche, sämtlicher Reparaturlayouts und aller denkbaren Resync-Auslöser ist noch nicht abgeschlossen. Eine Implementierung darf diese Lücken nicht durch angenommene Feldnamen oder Speichergrenzen ersetzen.

### 2.3 Separater Befehls-Wartepfad

Der SyncEvent-Pfad prüft erwartete Befehlskennungen. Fehlen Befehle am relevanten Zeitpunkt, kann er die weitere Simulation zunächst anhalten. Nach ungefähr drei Sekunden kann er trotzdem fortfahren (`0x1BCF0`). Das ist ein eigenes Diagnoseereignis: ein Forced Run ist nicht automatisch derselbe Befund wie eine unterschiedliche Spielzustandsprüfsumme.

Die Call-Site enthält den Text `SyncEvent - Forced run`, aber das aufgerufene Ziel `0x1A4A50` besteht im geprüften Image nur aus `C2 00 00` (`RET 0`). **Dieser Aufruf erzeugt daher keine Logausgabe.** Höhere BepInEx-Logstufen beheben das nicht. Die Formulierung im älteren [Chore-Dossier](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/CHORE_SYSTEM.md), der Pfad logge die Meldung, muss für diesen Build entsprechend eingeschränkt gelesen werden. Das historische Dossier wird durch dieses Konzept nicht umgeschrieben.

## 3. Bereits vorhandene Logs

Die folgenden Pfade wurden für die lokale Installation geprüft. Sie sind eine Momentaufnahme und keine Zusicherung, dass jede Installation dieselben Einstellungen oder Archivierungsabläufe verwendet.

| Fundstelle | Nutzen und Grenzen |
|---|---|
| `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` | Plugin-/Modversionen, Modsettings, Exceptions und ausdrücklich implementierte Diagnosen. Kein allgemeiner feldweiser Desync-Bericht. |
| `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\logs` | Lokal vorhandene archivierte Logs, darunter frühere Aufgeben-Diagnosen. Archivierung nicht ohne Prüfung als automatische Vanilla-Funktion voraussetzen. |
| `C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Player.log` | Unity-/Spielmeldungen und Fehlerkontext; kein allgemeiner Vergleich der Zustände aller Teilnehmer. |
| Derselbe Ordner, `Player-prev.log` | Vorherige Unity-Logdatei; keine dauerhafte Historie aller Partien. |
| `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` | Bekannter Client-Logpfad für manuelle Untersuchungen. Zeitweise Nichterreichbarkeit ist kein Beweis für eine fehlende Datei. |

Am 27.10.2026 enthielt die lokale `BepInEx/config/BepInEx.cfg` unter anderem `AppendLog=false`, `UnityLogListening=true` und `WriteUnityLog=false`. Das aktuelle BepInEx-Log deshalb vor einem weiteren Spielstart sichern. Aus diesen getrennten Einstellungen folgt keine vollständige Spiegelung zwischen BepInEx- und Unity-Dateien.

Im geprüften verwalteten Resync-Sende-/Empfangspfad werden Zustände wie `resyncing`, Abschnitt und Fortschritt geführt, aber kein umfassender Zustandsvergleich in eine eigene Desync-Datei geschrieben. Im untersuchten nativen Timeoutpfad ist das Ausgabeziel leer. Daraus folgt nicht pauschal, dass das gesamte Spiel keinerlei andere Netzwerk- oder Fehlermeldungen besitzt.

APIShareds vorhandener Diagnose-Schalter kann zusätzliche dort implementierte Debugmeldungen aktivieren. Er ist kein allgemeiner Vanilla-Desync-Rekorder und ersetzt dieses System nicht.

## 4. Gemeinsame Architektur

### 4.1 Zuständigkeiten und Abhängigkeiten

- **Hilfsmod:** Modus und Filter, Aufzeichnung, Vorfallverwaltung, Ausgabe und Benutzerhinweise. Er funktioniert ohne weitere Gameplay-Mods mit seinen deklarierten Extender-/APIShared-Abhängigkeiten.
- **APIShared:** vorhandenen Missionslifecycle, passende öffentliche Beobachter und die native Stellenkoordination nutzen. Eine neue allgemeine Beobachtung nur dann dort ergänzen, wenn sie unabhängig wiederverwendbar ist. Aufzeichnung und modbezogene Konfiguration bleiben im Hilfsmod.
- **Optionale Modanbieter:** eigene synchronisierte Aktionen und interne Zustände mit stabiler Anbieterkennung bereitstellen. Ihr Fehlen darf die Vanilla-Aufzeichnung nicht verhindern.
- **Offline-Auswertung:** Pakete unterschiedlicher Rechner lesen, korrelieren und vergleichen. Sie läuft außerhalb des Spielprozesses.

Der Hilfsmod ist als rein lokale Beobachtung konzipiert; `NetworkMode=0` ist daher das vorgesehene Manifestmodell. Das setzt voraus, dass die spätere Implementierung tatsächlich keine Gameplay-Mutation und keine zusätzliche Spielprotokoll-Pflicht einführt. Andere Spieler müssen ihn nicht installieren, um mitzuspielen; die Qualität der gemeinsamen Diagnose steigt mit der Zahl erfasster Teilnehmer.

### 4.2 Beobachtung ohne Eingriff

Die erste Ausbaustufe versendet **keine zusätzlichen Diagnosepakete**, ersetzt keine Spielbefehle und erzwingt weder Pause noch Save, Resync oder Wiederholung. Sie verändert keine Zufallsfolge, Befehlsplanung oder Prüfsummenberechnung. Vanilla wird unabhängig von Diagnosefehlern genau nach seinem ursprünglichen Vertrag ausgeführt.

Vorhandene geeignete Ereignisse haben Vorrang vor Polling. Der geprüfte Extender-Netzwerkereigniskatalog enthält kein allgemeines fertiges Desync-Ereignis; Custom-Paketbeobachter decken nicht automatisch alle nativen Chores ab. Erforderliche ergänzende Beobachtungspunkte müssen deshalb gesondert auditiert werden.

Vor Installation sind Extender-, Fixes-, APIShared- und andere bekannte Hooks auf Überschneidungen zu prüfen. Es gibt einen Besitzer je gemeinsamer Stelle. Die modübergreifende Nutzung erfolgt über einen Beobachtervertrag, nicht über konkurrierende Hooks.

### 4.3 Lebensdauer und Threads

Hooks, Delegates, Logger und Ereignisregistrierungen bleiben prozessweit verwurzelt. Startup-`OnDestroy` oder `OnDisable` einer Plugin-Komponente ist kein Freigabesignal für diese Ressourcen. Veröffentlichte Hooks werden weder bei Moduswechsel noch beim Kartenende zurückgepatcht; Aktivierung erfolgt logisch.

Der bestehende APIShared-Missionslifecycle liefert Start, Initialisierungsphasen und Ende. Jeder Lade-/Startversuch erhält eine neue lokale Generation. Alte asynchrone Schreibaufträge behalten ihre eigene Sitzungszuordnung und dürfen nicht der nächsten Partie zugerechnet werden. Kartenende beendet die Aufzeichnung dieser Sitzung, nicht die prozessweiten Beobachtungspunkte.

Auf Netzwerkthreads nur Transportdaten und Absender kopieren. Live-Spielzustand wird ausschließlich am auditierten Ausführungspunkt im geschützten Spiel-/Simulationskontext kopiert. Ein Post auf den Main-Thread erzeugt allein noch keinen tickgenauen, gegen die Simulation geschützten Snapshot.

Hintergrundthreads dürfen nur eigene unveränderliche Kopien verarbeiten. Keine späteren Dereferenzierungen gespeicherter Spielpointer, keine Unity-/Noesis-Zugriffe und keine blockierenden Rückfragen an den Simulationsthread. BepInEx-/UI-Meldungen aus dem Simulationspfad werden nicht blockierend über den dafür vorgesehenen Präsentationsweg ausgegeben; die Dateiwartearbeit bleibt im eigenen Writer.

## 5. Konfiguration und Stufenvergleich

| Bereich | Off | Light, Standard | Deep, gezielt |
|---|---|---|---|
| Laufende Aufzeichnung | Keine fachliche Aufzeichnung | Kompakte begrenzte RAM-Vorgeschichte | Größere Vorgeschichte und begrenzte laufende Dateiausgabe |
| Vanilla-Prüfsummen | Keine Sammlung | Vorhandene Ergebnisse und Prüfticks übernehmen | Zusätzlich ausgewählte Zustände häufiger und feiner prüfen |
| Befehle | Keine Sammlung | Identität, Quelle, Ankunft, Planung und tatsächliche Ausführung, soweit validiert beobachtbar | Zusätzlich begrenzte Nutzdaten und gezielte Vorher-/Nachher-Beobachtung |
| Resync | Keine Sammlung | Beginn, Kategorien, Reparaturdaten, Fortschritt, Abschluss oder Abbruch | Zusätzlich ausführlichere Zustands-/Felddifferenzen |
| Moddaten | Keine Sammlung | Identitäten, effektive Einstellungen und günstige freiwillige Aktionsmarker | Ausgewählte Providerzustände, Warteschlangen, Zufallszustände und Invarianten |
| Laufende Mehrarbeit | Minimaler deaktivierter Hookpfad | Keine zusätzlichen vollständigen Weltprüfungen | Durch Filter und Budget beschränkte zusätzliche Prüfungen |

Off bedeutet nicht, dass bereits veröffentlichte Hooks entfernt werden. Anfangsinstallation, Versionsprüfung und der deaktivierte Weiterleitungspfad können weiterhin Kosten haben.

Modus, Deep-Filter und Prüfintervalle werden vor der Partie festgelegt; Änderungen während einer laufenden Partie wirken nicht rückwirkend. Ein Neustart ist erforderlich, wenn eine Änderung neue Initialisierung oder Instrumentierung verlangt. Es gibt kein stilles automatisches Hochschalten auf Deep mitten in der Partie.

Zu konfigurierende Größen sind RAM-Budgets, Warteschlangenbudget, Datenlimit pro Vorfall, Dateiaufbewahrung sowie bei Deep Beobachtungsfilter und Prüfintervall in Simulationsticks. Konkrete Werte sind **noch zu vermessende Defaults**, keine hier erfundenen Performancegarantien. Der Bericht enthält die tatsächlich verwendete Konfiguration und die erreichte Abdeckung.

Beide Stufen sind dauerhafte Supportfunktionen und können im Release-Hilfsmod verfügbar sein. Vorübergehende fallbezogene Experimente, zusätzliche Forschungs-Hooks und Entwicklungsdiagnosen bleiben getrennt über `Shared.DevDiag` und `/devdiag`; ein Laufzeitschalter ersetzt deren Buildgrenze nicht.

## 6. Light: Aufzeichnung für den normalen Spielbetrieb

### 6.1 Seltene Bestandsaufnahme

Bei Prozess-/Missionsstart die tatsächlichen Spiel-, Extender-, APIShared- und Modidentitäten aufnehmen: Versionen und möglichst Binärhashes, relevante Protokoll-/Schemas, Modus, bekannte Karten-/Save-Identität, menschliche Teilnehmer, Host und Spielerzuordnung. Hashes und statische Metadaten außerhalb heißer Pfade bestimmen und wiederverwenden.

Effektive Gameplay-Einstellungen nach ihrer Anwendung erfassen, soweit ein verlässlicher Anbieter existiert. Persönliche Darstellungseinstellungen nicht pauschal als Desync-Ursache behandeln. Nicht zugängliche Einstellungen oder AIC-/AIV-/Skriptidentitäten werden als fehlende Abdeckung vermerkt. Eine nicht bekannte Identität wird nicht durch einen Dateinamen als vermeintlich sicheren Inhaltsnachweis ersetzt.

### 6.2 Begrenzte Vorgeschichte

Ein vorab reservierter Ringpuffer speichert kompakte Ereignisse. Im normalen Ereignispfad keine Stringformatierung, LINQ, Reflection, Stacktraces oder zusätzlichen Objektlisten. Häufige Metadaten nur einmal aufnehmen und über lokale Kennungen referenzieren.

Die Ereignisse unterscheiden mindestens Transportankunft, native Einreihung/Planung und tatsächliche Ausführung. Befehlskennungen, Quelle und die jeweiligen Tickwerte erhalten; geänderte geplante Ticks nicht als neue Befehlsidentität interpretieren. Nicht jeder Kontroll-/Resync-Befehl muss denselben geplanten Ausführungspfad besitzen; fehlende Werte bleiben ausdrücklich unbekannt oder nicht anwendbar.

Vorhandene Vanilla-Prüfsummen und ihre Prüfticks nach Abschluss des Originalaufrufs übernehmen. Den Prüfsummenbuilder nicht zusätzlich aufrufen: Er maskiert vorübergehend Felder, stellt sie wieder her und kann im regulären Pfad selbst einen Chore erzeugen. Ein Snapshot während der Maskierung wäre irreführend.

Der Ring bewahrt eine durch Kapazität begrenzte Vorgeschichte. Er garantiert ohne Messung keine feste Zahl vergangener Sekunden oder Ticks. Er meldet ältesten/jüngsten enthaltenen Tick und verlorene Ereignisse.

### 6.3 Vorfall und Reparatur

Getrennte Ereignistypen vorsehen: festgestellte Prüfsummenabweichung, nativer Resync-Start, Forced Run, Resync-Fortschritt/-Ende/-Abbruch und Anbieter-Invariantenfehler. Ein Anbieterfehler darf nicht als bewiesener Vanilla-Desync ausgegeben werden. Das gemeinsame Anzeigeflag `resyncingOrSaving` allein unterscheidet Speichern und Resync nicht.

Bei einem Trigger die vorhandene Vorgeschichte für den Vorfall sichern und weiter erfassen. Begrenzte Pufferrotation beziehungsweise eine begrenzte Zahl eigener Snapshot-Puffer verhindert, dass ein laufender Writer neue Ereignisse blockiert. Zusammengehörige Trigger werden korreliert, weitere Resyncs erhalten unterscheidbare Versuche. Gesicherte Belege werden nicht durch ein erneutes Einfrieren überschrieben.

Für jedes bereits validierte Reparaturlayout unmittelbar vor der Anwendung den alten lokalen Bereich und die empfangenen Ersatzdaten kopieren. Wo erforderlich zusätzlich den tatsächlich resultierenden Zustand erfassen: lokale Erhaltungsregeln können Teile des Records bewahren. Vanilla wird dabei nicht zur Diagnose erneut ausgeführt.

Die Einheiten-Reparatur zeigt einen konkreten machbaren Ansatz mit Record und gespeichertem Pfad. Daraus folgt noch keine Freigabe für sämtliche anderen Reparaturbereiche. Bis zu deren Audit Rohtransportdaten erhalten und die fehlende semantische Abdeckung melden, statt unbekannte Zielpointer oder Größen zu erraten.

Große Reparaturen, wiederholte Vorfälle und ein langsamer Datenträger bleiben budgetiert. Bei Erschöpfung sind fehlende Bereiche, Byte-/Ereigniszahlen und Gründe zu protokollieren. Die Simulation wartet nicht auf freies Plattenbudget oder auf das Nachladen eines Writers.

### 6.4 Was Light leisten soll

Light soll typischerweise den betroffenen Bereich, auffällige Befehlsfolgen, das geprüfte Zeitfenster und die tatsächlich korrigierten Datensätze eingrenzen. Es besitzt normalerweise **keine vollständigen historischen Welt-Snapshots** zum letzten regulären Prüftick. Später gesicherter Reparaturzustand darf nicht als ursprünglicher Zustand dieses früheren Ticks ausgegeben werden.

## 7. Deep: gezielte Suche nach dem ersten abweichenden Schritt

Deep übernimmt Light und erweitert die Beobachtung entlang einer konkreten Fragestellung. Light-Berichte liefern dafür Filter wie Kategorie, Objekttyp, Objektidentität oder Anbieter. Ohne solche Eingrenzung steigt der Aufwand stark; zusätzliche Prüfungen bleiben trotzdem begrenzt und im Bericht sichtbar.

### 7.1 Häufigere und feinere Zustandsproben

Ausgewählte Bereiche an festen Simulationsticks und festen Phasen kopieren beziehungsweise prüfen. Optional die Prüfgranularität von Kategorien über Objekte bis zu Feldern verfeinern. Für unbekannte Layouts zunächst die belegten Rohgrenzen erhalten, ohne Vermutungen als Feldvertrag festzuschreiben.

Eine Zustandsaufnahme ist nur dann ein gemeinsamer Snapshot, wenn die gelesenen Daten aus demselben ausreichend geschützten Zustand stammen. Über mehrere Ticks verteilte Scans erhalten einzelne Zeit-/Phasenangaben und dürfen nicht zu einem vermeintlich gleichzeitigen Weltzustand zusammengefasst werden.

Simulationstick und Ausführungsphase sind Teil jeder Probe. Zusätzliche Prüfer dürfen keine Spielzufallswerte ziehen, keine mutierenden Spielqueries auslösen und keine nativen Caches oder temporär maskierten Zustände versehentlich als stabile Gameplay-Felder interpretieren.

### 7.2 Befehle und Modaktionen

Für ausgewählte Befehle begrenzte Roh-/Nutzdaten sowie Zustandsproben vor und nach tatsächlicher Ausführung erfassen. Bei synchronisierten Modaktionen optional interne Zustände vor/nach dem gemeinsamen Receiver sichern. Transportempfang und Aufruf einer öffentlichen Sendemethode beweisen keine tatsächliche Ausführung auf allen Teilnehmern.

Interessante Modzustände sind beispielsweise eigene Zufallsgeneratoren einschließlich Fortschritt, ausstehende Aktionen, deterministische Zähler, tatsächlich angewandte Einstellungen und persistierte Simulationsdaten. Invarianten können zusätzlich unerlaubte lokale Ausführung oder inkonsistente Warteschlangen sichtbar machen, soweit die jeweilige Mod einen belastbaren Vertrag besitzt.

Stack-/Aufruferinformationen nur an gezielt ausgewählten seltenen Stellen oder im begrenzten Fehlerfall erfassen. Eine flächendeckende Überwachung sämtlicher nativer oder fremder modseitiger Speicherwrites ist nicht Bestandteil dieses Entwurfs.

### 7.3 Vorgeschichte und Messbeeinflussung

Deep schreibt abgeschlossene Datenabschnitte begrenzt fortlaufend, damit mehr Vorgeschichte als im RAM-Ring verfügbar bleibt. Rotation und Löschgrenzen sind explizit; aktive beziehungsweise bereits gesicherte Vorfälle dürfen nicht still verschwinden. Ein Absturz kann den letzten noch nicht geschriebenen Abschnitt verlieren.

Deep muss vor der zu untersuchenden Reproduktion aktiv sein. Nach einem bereits geschehenen Vorfall aktivierte Detailprüfung rekonstruiert keine verlorene Vorgeschichte. In der ersten Ausbaustufe entsteht die feinere rechnerübergreifende Abweichungserkennung **offline**; ohne Diagnosepakete gibt es keinen automatischen Livevergleich zusätzlicher Deep-Prüfsummen.

Zusätzliche Instrumentierung kann zeitabhängige Fehler verändern oder verdecken. Jeder Bericht enthält deshalb die aktive Stufe und Instrumentierung. Ein in Deep ausbleibender Fehler widerlegt für sich keinen Light-Befund.

## 8. Ereignis- und Anbieter-Verträge

### 8.1 Gemeinsamer Kontext

Jeder Datensatz trägt die notwendigen Zuordnungen: lokale Prozess-/Sitzungskennung und Ladegeneration, beobachtete Spieler-/Hostzuordnung, Simulationstick, Ausführungsphase, lokale Sequenz und Ereignisart. UTC-Zeit mit Millisekunden und eine monotone lokale Zeit helfen bei Reihenfolge und Dauer; sie ersetzen keinen gemeinsamen Simulationstick.

Objekte mit Art, Spiel-ID/Slot und nach Möglichkeit Global-ID identifizieren. Slot-Wiederverwendung und Hostwechsel sichtbar behandeln. Unbekannte Tick- oder Identitätswerte dürfen nicht als Null oder geratenes Objekt erscheinen. Vorgeschlagene Plan-/Ausführungsticks sind getrennte Werte.

### 8.2 Optionale Anbieter

Ein Anbieter wird durch Mod-GUID, stabile lokale Anbieterkennung und Schema identifiziert. Der Vertrag beschreibt zulässigen Aufrufthread, Erfassungsphase, prozessweite Registrierung, logische Aktivierung, Fehlerbehandlung und Datenbudget. Eine neue öffentliche API muss additiv bleiben und vor Umsetzung mit vorhandenen APIShared-Verträgen abgeglichen werden; hier werden noch keine C#-Signaturen festgelegt.

Anbieter liefern eigene unveränderliche Daten oder schreiben in begrenzte eigene Aufzeichnungspuffer. Sie verändern zur Erfassung keine Simulation. Unbekannte oder fehlerhafte Anbieter werden isoliert; Vanilla und unabhängige Anbieter laufen weiter. Ein Zeitbudget kann einen blockierenden synchronen Fremdcallback nicht sicher abbrechen: Es braucht einen vertraglich begrenzten Erfassungspfad und die Abschaltung weiterer Aufrufe bei nachgewiesenen Problemen, keine Thread-Abbrüche im Spiel.

Kanonische Vergleichsdaten enthalten stabile Sortierung und eindeutige Typ-/Schemaangaben. Dictionaries nicht in zufälliger Reihenfolge hashen, keine Prozessadressen als Objektidentität und keine lokalen Darstellungswerte als gemeinsame Gameplay-Daten verwenden. Rohdaten für die Rekonstruktion können zusätzlich lokal vorhanden sein, dürfen aber nicht ungeprüft in einen rechnerübergreifenden Gleichheitsvergleich eingehen.

Ein fehlender Anbieter bedeutet unbekannten Zustand, nicht Gleichheit. Ein deaktiviertes Feature, eine legitime lokale Einstellung, ein Erfassungsfehler und ein tatsächlich abweichender Simulationswert sind verschiedene Berichtszustände.

## 9. Ausgabe, Grenzen und Fehlerverhalten

Vorgesehener Ausgabeort ist `BepInEx/DesyncLogs` mit kurzen eindeutigen Sitzungs-/Vorfallordnern. Vollständige Identitäten stehen im Manifest. Keine fremden Plugin-/Logordner pauschal bereinigen.

| Bestandteil | Zweck |
|---|---|
| Manifest | Formatversion, Identitäten, tatsächliche Konfiguration, Sitzung, Teilnehmer, Erfassungsabdeckung, Vollständigkeit und Dateiverzeichnis |
| Ereignisfolge | Kompakte versionierte Records mit Reihenfolge, Kontext und Verweisen auf Nutzdaten |
| Zustandsdaten | Binäre Kopien, Reparaturdaten und Deep-Proben mit Schema/Blockidentität und nachvollziehbaren Grenzen |
| Lesbare Zusammenfassung | Trigger, Kategorie, betroffene Objekte, Ablauf, Verluste und konkrete Hinweise zur Auswertung |

Formatierung, Prüfsummen der Ausgabedateien, optionale Kompression und Dateischreiben geschehen im Writer. Runtime-JSON verwendet ausschließlich den vorgeschriebenen quellverlinkten `Shared.DependencyFreeJson`; das Offline-Werkzeug darf seine eigene passende Bibliothek verwenden.

Dateien/Abschnitte sollen nach Unterbrechungen als vollständig, teilweise geschrieben oder beschädigt erkennbar sein. Ein Abschlussmarker ist erst nach erfolgreicher Ausgabe des zugehörigen Abschnitts gültig. Eine unvollständige Aufnahme bleibt lesbar, soweit ihre abgeschlossenen Blöcke reichen; ein Erfassungsfehler wird nicht als erfolgreicher Desync-Nachweis ausgegeben.

Budgetgrenzen gelten getrennt für RAM-Ring, eingefrorene Daten, Writer-Warteschlange, Vorfall und Aufbewahrung. Reservierte Metadaten/Zähler stellen sicher, dass auch bei Datenverlust noch erkennbar ist, was fehlt. Läuft der Writer hinterher, Daten begrenzt verwerfen oder die betroffene Zusatzaufzeichnung aussetzen; niemals unbegrenzt allokieren oder den Simulationspfad auf Dateiausgabe warten lassen.

Neue Vorfälle überschreiben keine schon gesicherten Belege desselben Laufs. Eine spätere Aufbewahrungsbereinigung entfernt nur abgeschlossene, nicht geschützte eigene Pakete nach dokumentierter Regel. Sind die Grenzen anders nicht einzuhalten, neue Aufnahme sichtbar begrenzen. Bei Schreibfehlern, vollem Datenträger oder unbekanntem Build bleiben Vanilla und unabhängige Diagnosebereiche aktiv.

Die Ausnahmebehandlung des Rekorders ist von Vanilla getrennt. Native Pointerfehler lassen sich nicht zuverlässig durch ein verwaltetes `try/catch` absichern; Speicherzugriffe benötigen vorher validierte Grenzen und Layouts. Unbekannte native Verträge deaktivieren nur den betroffenen Leser.

## 10. Offline-Vergleich und Ursachenbewertung

Das Auswertewerkzeug erhält die verfügbaren Pakete aller Teilnehmer. Es benötigt für die Dateiauswertung keinen Zugriff auf einen laufenden Spielprozess. Unterschiedliche lokale Diagnosekonfigurationen und fehlende Pakete werden als Abdeckungsunterschiede ausgewiesen, nicht automatisch als Gameplay-Abweichung.

Empfohlene Reihenfolge:

1. **Vergleichbarkeit prüfen:** Spiel-/Modidentitäten, Szenario beziehungsweise Save, Start-/Ladegeneration, Teilnehmer und Erfassungsschemas abgleichen. Lokale Sitzungsnummern verschiedener Rechner sind nicht automatisch gleichbedeutend. Mehrdeutige Zuordnungen explizit zur manuellen Auswahl vorlegen.
2. **Befehle korrelieren:** stabile Befehlsidentität und Absender verwenden; Transportankunft, geplante Ticks, tatsächliche Ausführung und Wiederholungen trennen. Bloße Ankunftszeitunterschiede sind noch kein Desync.
3. **Prüfungen ausrichten:** nur gleiche Prüfticks und kompatible Phasen/Bereiche vergleichen. Einen aktuellen lokalen Wert nicht gegen einen älteren entfernten Wert als Beweis verwenden.
4. **Abweichung eingrenzen:** Kategorie → Objekt → Feld, sofern die Aufnahme das unterstützt. Bei Deep die früheste beobachtete Abweichung zwischen vergleichbaren Proben suchen; bei Light ein gröberes Zeitfenster angeben.
5. **Reparatur auswerten:** alten lokalen Zustand, empfangene Daten und gegebenenfalls tatsächlich resultierenden Zustand vergleichen. Der Host ist die protokollseitige Reparaturquelle, nicht automatisch die fachlich fehlerfreie Instanz.
6. **Modbeiträge zuordnen:** effektive Einstellungen, Marker, Providerzustände und Invarianten heranziehen. Fehlende Daten, Komponentenausfälle und Pufferverluste in jede Schlussfolgerung einbeziehen.

Ergebnisse erhalten eine erkennbare Belegstufe:

- **Beobachtete Abweichung:** konkrete vergleichbare Daten unterscheiden sich.
- **Plausibler Zusammenhang:** ein bestimmter Ausführungsschritt oder eine Modaktion passt zum Befund, aber die verursachende Änderung ist nicht vollständig bewiesen.
- **Reproduzierbar nachgewiesene Ursache:** passende Reproduktion und Ausführungs-/Datenflussbelege zeigen den verursachenden Schritt; ein geeigneter Vergleichslauf bestätigt die Eingrenzung.

Die letzte Probe ohne erkannte Abweichung ist wegen begrenzter Abdeckung und möglicher Prüfsummenkollisionen kein Beweis, dass zu diesem Zeitpunkt jeder Zustand identisch war. Gleiche Befehle schließen lokale Zufalls-, Timing-, Konfigurations- oder direkte Speicheränderungen nicht aus. Modinterne Warteschlangen können nach einem Vanilla-Resync weiterhin abweichen, wenn Vanilla diese Zustände nicht repariert.

### Belegtes Beispiel: Aufgeben

Die vorhandene [Aufgeben-Analyse](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SURRENDER_SELECTION.md) verglich Lord-Einheiten auf Host und Client bei denselben Prüfticks. Dabei unterschied sich unter anderem `r_UnitSelected` am Record-Offset `0xAE`; dieses Feld liegt im gehashten Intervall. Vanilla identifizierte die entsprechende Einheit über Chore 56.

Das belegt, dass eine Eingrenzung bis auf ein relevantes Feld praktisch möglich ist. Die damalige Aufnahme enthielt jedoch nicht sämtliche zusätzlichen Einheiten-Prüfsummeneingaben, insbesondere nicht den vollständigen gespeicherten Pfad und alle drei zusätzlich einbezogenen Skalare. Der Befund ist deshalb kein Nachweis, dass ausschließlich dieses Feld abwich. Eine allgemeine Diagnose sollte solche Abdeckungslücken ausdrücklich vermeiden oder melden.

## 11. Abnahme und noch offene Implementierungsnachweise

### 11.1 Funktionale Szenarien

| Szenario | Erforderlicher Nachweis |
|---|---|
| Kontrolliert unterschiedliche Zustände bei gleicher Befehlsfolge | Prüftick, Kategorie und gesicherte Abweichung passen zusammen; Herkunft nicht allein aus letzter Modmeldung ableiten. |
| Verspäteter / fehlender Befehl | Warten, eventueller Forced Run und spätere Ausführung unterscheiden; kein erfundener Checksum-Desync. |
| Resync ohne bleibend feststellbaren Unterschied | Protokollereignis wird erfasst; Bericht behauptet keine ungemessene Ursache oder vollständige Gleichheit. |
| Mehrere Objekte / große Reparatur | Alle erfassten Blöcke und Fortsetzungen zuordnen; fehlende Daten ausdrücklich zählen. |
| Wiederholte Resyncs | Versuche und Objektidentitäten trennen; frühere Belege erhalten. |
| Save/Load, neue Partie, Editor, Abbruch, Hostwechsel | Sitzung/Generation und Autoritätszuordnung stimmen; Save nicht mit Resync verwechseln; keine alten Writerdaten in neuer Sitzung. |
| Mod mit / ohne Anbieter | Vanilla-Diagnose funktioniert in beiden Fällen; interne Modabdeckung korrekt ausgewiesen. |
| Rein lokaler Unterschied | Erwartete Darstellungs-/persönliche Werte erzeugen keinen falschen gemeinsamen Zustandsvergleich. |
| Fehler und Überlast | Ring-/Writerüberlauf, langsamer Datenträger, Schreibfehler und fehlerhafter Anbieter bleiben begrenzt; Vanilla wird nicht verhindert. |
| Unbekannter Native-Build / Hookkonflikt | Nicht validierte Leser bleiben aus; Diagnose nennt fehlende Capability statt alte Adressen zu benutzen. |

Echte Host-/Client-Läufe sind erforderlich. Modell-/Codec-Tests ersetzen weder den Empfang vor einem nativen Überschreiben noch die sichere Thread-/Lockposition im Spiel. Ein Readymarker beim Start reicht nicht; spätere Missions-/Ereignisaufrufe nach dem Startup-Cleanup sind nachzuweisen.

### 11.2 Performance und Nichtbeeinflussung

Off, Light und gezieltes Deep unter gleichen Einstellungen und vergleichbaren Spielsituationen abwechselnd messen. Eine Variante ohne Hilfsmod macht zusätzlich die Grundkosten bereits installierter deaktivierter Hooks sichtbar. Sinnvolle Messgrößen sind Simulationsdurchsatz, lange Ticks und Frame-Lücken, Allokationen/GC, Speicherverbrauch, Schreibmenge und Vorfallspitzen.

Für Light sowohl normale Partien als auch viele Befehle und große Einheiten-/KI-Szenarien betrachten. Separate Messung von laufender Aufzeichnung und dem seltenen Erfassen einer großen Reparatur. Hintergrundausgabe allein beweist keine Lagfreiheit: Schon das sichere Kopieren auf dem Simulationsthread hat Kosten.

Bei kontrollierten Läufen prüfen, dass Diagnose keine zusätzlichen Spielbefehle, Zufallsaufrufe oder Zustandsmutationen erzeugt. Ein Mod kann legitimerweise das Gameplay ändern; deshalb ähnliche Ticks/s oder unterschiedliche Weltzustände verschiedener Gameplay-Varianten nicht als alleinigen Rekorder-Kostennachweis verwenden. Noch existiert kein belegter Overheadwert für dieses Konzept.

### 11.3 Vor der Implementierung zu schließen

- Vollständige relevante Parameter-/Datenflüsse aller tatsächlich verwendeten Prüfsummenbereiche und Reparaturlayouts: aktive/ignorierte Felder, Maskierungen, Strides, ID-Basis und Speichergrenzen.
- Sichere Beobachtungspunkte für Erzeugung, Einreihung, tatsächliche Ausführung, Resync-Anwendung und Forced Run, einschließlich aller von der Implementierung unterstützten Alternativ-/Timeoutpfade.
- Tatsächliche Hookspannen, eingehende Sprünge, ABI, Register/Flags und Kompatibilität mit der dann installierten Extender-/Fixes-/APIShared-Fassung. RVAs/Patterns in der späteren `UpdateToNewDLL.md` dokumentieren.
- Sichtbarkeit und Signaturen verwendeter Spielmember gegen die echte `Assembly-CSharp.dll`, nicht allein gegen eine publicized Kompilierreferenz.
- Konkrete Provider-/Dateischemas sowie Fehler-/Overflow- und Wiederanlaufverträge implementieren und passend testen. Unbekannte oder partielle Aufnahmen müssen erkennbar bleiben.
- RAM-/Dateibudgets, Deep-Intervalle und produktive Defaults anhand tatsächlicher Erfassungsgröße und Laufzeitkosten bestimmen.

Diese offenen Nachweise verhindern keine Dokumentation des Konzepts, erlauben aber noch keinen produktiven nativen Leser auf Verdacht. Die spätere Umsetzung beginnt mit den validierten günstigen Beobachtungen und ergänzt Bereiche nur mit belegtem Vertrag. Dafür weder externe Quellbäume ändern noch historische Baseline-Nachweise umschreiben.

## 12. Quellen und reproduzierbare Nachprüfung

Primäre lokale Belege:

- [Aktuelle Baseline und Navigationsregeln](_inspect/CrusaderDE-Native-Baseline/CURRENT.md), [maschinenlesbare Auswahl](_inspect/CrusaderDE-Native-Baseline/CURRENT.json) und [Manifest](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/DATABASE_INFO.json).
- [Semantischer Native-Export](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c): die in Abschnitt 2 genannten Funktionen; Abfrage bevorzugt über `query.ps1 function <RVA>`.
- [Verwalteter Multiplayer-Code](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/Platform_Multiplayer.cs): `SendChores`, `processMessage` und Resync-Zustände.
- [Verwaltete Engine-Schnittstelle](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/EngineInterface.cs): `run`, `ReceiveChore` und ihre Lock-/Native-Aufrufgrenzen.
- [Chore-Dossier](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/CHORE_SYSTEM.md), mit der Einschränkung zum leeren Logger aus Abschnitt 2.3; [Aufgeben-Analyse](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SURRENDER_SELECTION.md).
- [Extender-Netzwerkereignisse](shcde-script-extender/src/SHCDESE.BepInEx/EventAPI/NetworkR3EventHooks.cs), [Netzwerk-API](shcde-script-extender/src/SHCDESE.BepInEx/API/GameNetworkAPI.cs) und [Chore-Transport](shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkChoreDetours.cs).
- [APIShared-Katalog](APIShared/docs/API_CATALOG.md), [Architektur](APIShared/ARCHITECTURE.md) und [Integrationsleitfaden](APIShared/docs/THIRD_PARTY_GUIDE.md), insbesondere Missionslifecycle, native Stellenkoordination und Diagnoseaktivierung.

Die folgenden lesenden Abfragen sind aus dem Workspace-Root reproduzierbar. Bei später geänderter Baseline zuerst deren aktuelle Auswahl und Identitäten prüfen. Die hier aufgeführten RVAs nur für den oben bezeichneten Native-Hash fachlich übernehmen.

```powershell
Get-Content -LiteralPath '_inspect/CrusaderDE-Native-Baseline/CURRENT.json'
Get-FileHash -Algorithm SHA256 -LiteralPath 'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
Get-FileHash -Algorithm SHA256 -LiteralPath 'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'

& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0xD1AD0
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x22020
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x27C50
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x27960
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x26D30
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x251F0
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x15710
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x1BCF0
& '_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1' function 0x1A4A50

Select-String -LiteralPath 'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/config/BepInEx.cfg' -Pattern 'AppendLog|UnityLogListening|WriteUnityLog'
```

Bei fehlender lokaler Datenbank gilt ausschließlich der dokumentierte Restore-Weg der aktuellen Baseline; keinen zufällig vorhandenen älteren Datenbankstand wählen. Datenbank- oder Analyseabfragen sind statische Nachprüfungen und keine Ingame-Abnahme.
