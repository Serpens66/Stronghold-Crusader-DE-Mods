# Arbeitsplan: feststeckende Bauern entfernen und Soldaten befreien

**API-Integration, 27.11.2026:** Die Rettungs-API einschließlich Trampolinprüfung ist in `APIShared/`, Branch `dev/Serpens66`, Commit `421c1ed0457de7a8b7dfcbff160778a4f1f4d070` integriert. Dies ist derselbe getestete Quellstand; 1452 dateibasierte Build-Eingaben stimmen mit dem bisherigen vollständigen Buildnachweis überein. Buildproof, Buildschritte und TRX-Dateien sind unter `.work/a/api-consolidation/001/` erhalten. Die alte Rettungs-Arbeitskopie wurde nach erfolgreicher Integrationskontrolle mit `git worktree remove` vollständig entfernt. Ältere Pfade und Paketidentitäten unten bleiben historische Nachweise. Die Foreign-Control-Arbeitskopie ist ausdrücklich nicht integriert.

Verbindlicher Planstand: 27.11.2026. Zielintegration: **BugfixesAndQoL**. Ausführung: **abgeschlossen** nach belegter Produktionsintegration und Singleplayer-/Editor-Abnahme bis215. Keine Multiplayer-, Commit- oder Releasefreigabe.

## Vorrang und Ziel

Dieser aktive Teil ersetzt die früheren Vorgaben zur identitätserhaltenden Arbeiter-Fortsetzung und zur Migration nach Pathfinding. Der unten abgegrenzte Archivteil und ältere Checklisten dienen als Nachweise und technische Quellen; ihre offenen Aufgaben sind keine zusätzliche Abschlussbedingung. Bei jeder Fortsetzung zuerst diesen aktiven Teil und den tatsächlichen Quell-/SDK-Zustand prüfen.

Der gespeicherte übergeordnete Chat-Zieltext bleibt grundsätzlich geeignet: den Rettungsmod effizient finalisieren, in Singleplayer prüfen und anschließend in BugfixesAndQoL integrieren. Seine Ausführung richtet sich nach den nachfolgenden aktualisierten Regeln. Der ursprüngliche objective-Text wird nicht durch ein engeres Ziel ersetzt. Die nachfolgenden Anforderungen sind jetzt mit der [Abschlussabnahme](FINAL-ACCEPTANCE.md) belegt; es ist kein neues paralleles Ziel nötig.

## Verbindlicher Zieltext für die weitere Bearbeitung

> Finalisiere den Testmod nach diesem aktuellen Arbeitsplan: Nachweislich durch Bau- oder Geländeänderungen feststeckende Bauern und Wirtschaftsarbeiter regulär vollständig entfernen; Vanilla übernimmt Bevölkerungsausgleich und Jobneuvergabe. Das umfasst arbeitslose Lagerfeuerbauern und landwirtschaftliche Arbeiter. Transportierte Waren, bereits verbrauchte Inputs und verzögerte oder ausbleibende Neubesetzung sind als Nachteile akzeptiert; keine Warenkompensation oder künstlichen Ersatzbauern einführen. Soldaten erhalten, auf ein geprüftes nahes Feld befreien und ihren weiterhin gültigen Bewegungsauftrag fortsetzen. Rekrutierte Spezialisten, Belagerungsgeräte und Mannschaften separat erhalten und absichern. Erkennung ereignisbasiert über APIShared, mit festen Budgets und ohne dauernde Vollscans. Vorhandenen Code auf diesen einfacheren Ansatz umstellen und überflüssige Arbeiter-Fortsetzung samt ausschließlich dafür benötigten Tests entfernen. Erforderliche Singleplayer-, Editor-, Save-/Lifecycle-, Kompatibilitäts- und Effizienzprüfungen deterministisch durchführen, sobald das Ziel fortgesetzt wird: feste geeignete Karte, kontrollierte Testfälle, Friedenszeit, RandomEvents aus, DoNothing für nicht kämpfende Gegenproben, geprüfter Hintergrunddesktop ohne globale Eingaben. Aktuellen Fixes-Mod und die relevanten eigenen Mods einschließlich Pathfinding und CastlePlanner berücksichtigen; gemeinsame Hookbesitzer in APIShared erhalten. Nach bestandener Abnahme in BugfixesAndQoL integrieren, dort erneut prüfen und den ursprünglichen Runtime-Testmod entfernen; eigenständige Automation und notwendige Nachweise erhalten. Keine echte Multiplayerfreigabe. Bei einer zwingenden neuen Produktentscheidung zuerst alle unabhängigen Arbeiten abschließen, dann die Entscheidung vorlegen; die bereits akzeptierten Nachteile nicht erneut zur Freigabe stellen.

## Festgelegtes Verhalten und Grenzen

| Einheit / Zustand | Gewolltes Ergebnis | Entscheidender Schutz |
|---|---|---|
| Gewöhnlicher Bauer / Peasant, auch arbeitslos | Bei bewiesenem Feststecken native vollständige Entfernung | Keine Verwechslung mit laufender Soldaten-/Spezialistenrekrutierung anhand des vorübergehenden Peasant-Typs |
| Wirtschaftsarbeiter einschließlich sämtlicher Farmer | Native Entfernung, spätere natürliche Slotfreigabe und gegebenenfalls Neubesetzung | Alte ID/Global nicht nach Wiederverwendung anfassen; kein Arbeits-/Cargo-Replay |
| Gewöhnlicher Soldat | Dieselbe Einheit lokal befreien und gültige aktuelle Befehlsabsicht fortsetzen | HP/Owner/Verband/Belegung erhalten; neuer Auftrag und legitimer AI-Wechsel gewinnen |
| Ingenieur, Gerät und Mannschaft | Eigenen bestehenden Verbundvertrag erhalten | Kein Löschen aufgrund einer pauschalen Nicht-Soldaten-Regel; kein Crew-Neuspawn |
| Ox, arbeitsgebundene Tiere und sonstige Hilfseinheiten | Explizit geprüfte fachliche Behandlung | Ihre Erzeugung erfolgt nicht pauschal über Peasant-Zuzug; keine unbewiesene Ersatzgarantie |
| Gesunde Idle-/Hidden-/Arbeitsanimation, legaler Sonderweg | Keine eigene Aktion | Stillstand, fehlende sichtbare Belegung oder ein unerreichbares Ziel allein beweisen keine Falle |
| Tod, absichtliche Editorlöschung, Transientzustand oder unbekannte Identität | Keine Wiederbelebung oder blinde Entfernung | Native Lebens- und Ownershipgrenzen beibehalten |

Warenverlust und fehlende sofortige Neubesetzung sind **entschieden und akzeptiert**. Daraus folgt keine Freigabe für beschädigte Tilechains, dauerhaft falsche Mitarbeiterbindungen oder versehentlich gelöschte Soldaten. Menschen und KI erhalten dieselbe fachliche Behandlung innerhalb der bereits belegten Besitzer-/Ereignisgrenzen. Alte Saves ohne kausale Vorherdaten und fremde Besitzer werden nicht beiläufig durch neue Scans freigegeben.

## Aktive Arbeitspakete in Reihenfolge

| ID | Aufgabe | Abschlusskriterium | Status |
|---|---|---|---|
| N0 | Tatsächlichen Code-/SDK-/Paketstand und vorhandene Belege prüfen; veraltete Aufgaben außer Kraft setzen | Zuständigkeiten und Quellen eindeutig; alte 20-Rollen-Produktionsfortsetzung kein Freigabeziel | Abgeschlossen: aktueller Modstand und isoliertes APIShared0.78 einschließlich zusammengeführter kanonischer Änderungen geprüft; Quelle/Pakete/Belege in FINAL-ACCEPTANCE.md. |
| N1 | Entfernungspfad für Bauern/Arbeiter samt Klassifizierung und sicherem Simulationseinstieg schließen | Native Markierung bis endgültiger Entfernung, Belegung, Mitarbeiter-/Aktivslots, Weltwechsel und Originalaufrufbesitz geprüft; Rekrutierung geschützt | Abgeschlossen: vollständiger nativer Audit und Baseline;24 Typen/Klassifizierung/Rekrutierungsgrenzen; native Entfernung/Bindungsbereinigung und bedingte Neubesetzung in211 sowie Normal214/215 belegt. |
| N2 | Ereignisbasierte Erkennung mit getrennten Maßnahmen verbinden | Qualifizierte Bauern/Arbeiter werden entfernt, Soldaten verwenden die erhaltene sichere Befreiung; OFF verursacht keine eigene Maßnahme | Abgeschlossen: ereignisbasierte Erkennung und getrennter Entfernungspfad; CurrentSafe-/Next-/World-/Delete-Gegenproben211, tatsächliche Host-/Client-/Optionsgrenzen212/214. |
| N3 | Alten Arbeiter-Fortsetzungscode und ausschließlich zugehörige Tests ablösen | Keine Worker-Jobwiederherstellung, Crop730-Korrektur, Grunt-Wegpostprozessor-Nachbildung oder Rollenproduktions-Fortsetzung mehr nötig; weiterhin benötigte Command-/Crew-/Geometriebausteine erhalten | Abgeschlossen: neue menschliche Job-/Cargo-Fortsetzung gesperrt; Ox/gemeinsame Command-/Crewteile und notwendige historische Decoder erhalten;66 obsolete Prototypdateien/-ressourcen entfernt. |
| N4 | Neue funktionale Abnahme und negative Gegenproben | Entfernung/Belegung/Jobfreigabe statt gleicher Workeridentität und zwei geretteten Produktionschargen; Soldaten-/Gerätegarantien unverändert | Abgeschlossen im vereinbarten Scope:64 funktionale Produktionsfälle211 einschließlich Quellen, Gruppen, ursprünglicher Geräte/Crew und ziviler Entfernungs-/Gegenproben; normales Endpaket214/215 zusätzlich geprüft. |
| N5 | Lifecycle, Save/Load, Einstellungen und Fixes-Kompatibilität abnehmen | Kein doppelter Löschauftrag, kein alter Worker-Receipt-Replay, kein zurückgelassener eigener Flags5-Parkzustand; keine Hookkollision | Abgeschlossen: echte600er-Pending-Save/Loads211/214, Editor neu/geladen, Welt-/Identitätsgrenzen, fünf tatsächliche Aktivierungsprofile212/214; Fixes-Verträge/Live-Stubs/Updategegenfälle geprüft. |
| N6 | Relevante Last-/Bursts-/Normalbetriebsmessung abschließen | Ereignisarbeit und Queues begrenzt; keine leere Dauerarbeit/Vollscans; reale Messungen mit belegten Testwelten | Abgeschlossen im dokumentierten Scope:9999er-Offlinegrenzen, aktive/passive600er-Bursts; qualifiziertes9871/1000Walking-Profil213 mit8Fenstern/4Loads. Bekannte PitchLine-/PerfProbe-Diagnosegrenze und3unzugeordnete>50ms-Aufrufe ausdrücklich dokumentiert; keine allgemeine FPS-Behauptung. |
| N7 | In BugfixesAndQoL integrieren und dort abnehmen | Eine Runtime, eine neue gemeinsame Hosteinstellung unter EnableMod, konsistente UI/Defaults/Locales und SDK-Paarung; echte MP-Rettung aus | Abgeschlossen: eine BQoL-Runtime/eine default-AN Hostoption;27 Prüfgruppen/263WorkerGuard-/116Scannerchecks; normale Endabnahme214 Planner14 und215 Minimal4, keine Error/Fatal, SDK0.78-Notification- und normale PitchLine-READY. |
| N8 | Ersetzten Prototyp und entbehrliche Entwicklungsreste entfernen | Automation separat, notwendige Audits/Fehlerbelege erhalten, normale Releaseartefakte ohne Entwicklungsdiagnose | Abgeschlossen:66 obsolete Quellen/Ressourcen entfernt, separate Automation/25verlinkte Purefixtures/Audits erhalten, WorkerGuard nach BQoL verschoben. Normale Main-/Acceptance-Endpakete ohne Entwicklungsdiagnose oder zweiten Rettungs-/Scannerbesitzer; alle Spieltransaktionen restauriert. |

**Historischer Zwischenschritt, 27.11.2026, 02:15 Uhr:** Der neue Entfernungspfad ist implementiert und offline geprüft. Teilabnahmen166/168 bestätigen tatsächliche Arbeiter-/Bauernentfernung und freie Ketten;166/171 außerdem natürliche Farmneubesetzung.170 bestätigt menschliche/KI-Gruppen und originale Geräte/Crew;171 den echten Native0-Fall im Minimalprofil;172 echte historische Waiting-Parks nach Save/Load bei Rettung OFF, Command/Ox erhalten. 173 belegt die bereits gesetzte originale Fixes-Löschmarkierung;174 prüft deren Vorrang ohne zweite eigene Entfernung erfolgreich im normalen Build. Anschließend gezielte übrige Hilfsbindungen/Quellen/AIworker-/Lifecycle-/Effizienzfälle und Produktionsintegration. Fehlgeschlagene Serien165–169 bleiben korrekt getrennte Belege.

## Aktueller Checkpoint: Abschluss nach Normalabnahme215

Alle aktiven Arbeitspakete N0–N8 sind im vereinbarten Scope abgeschlossen.211 besteht alle64 funktionalen Fälle gemeinsam;212 bestätigt die fünf tatsächlichen Default-/Host-/Client-/Optionsprofile. Der dabei gefundene APIShared-Trampolinprüffehler ist eng korrigiert: deterministische RedBird/Iced-Neuencodierung statt falscher Bytekopieannahme,37 gezielte Backendtests und vollständiger offizieller SDK-Build bestanden.213,214 und215 bestätigen tatsächlich `NOTIFICATION_IDENTITY_READY`.

213 besteht die integrierte künstliche9871er-/1000Walking-Abnahme mit8Fenstern,5Welt-/Ready-Handoffs und4Endpunktprüfungen. Der aktuelle181-Checks-Logprüfer weist die bekannte frühe PerfProbe/PitchLine-Kompositionsgrenze ausdrücklich aus; daraus entsteht weder eine volle Pitch-/Normalweltfreigabe noch eine kausale Performanceverbesserung. Drei native Aufrufe über50ms (max71.220ms) im letzten ON-Walkingfenster bleiben unzugeordnet, keiner über100ms.

Die normalen Main- und Acceptance-Endpakete bestehen die vollständigen vorgesehenen Prüfungen und die NoDevGate-/Typprüfung.214/0714d5b63904 (Planner14 Fälle) und215/29cd44bd7f5b (Minimal4 Fälle ohne Fixes/Pathfinding/CastlePlanner) bestehen ohne Error-/Fatal-Zeilen, mit Notification- und PitchLine-READY. Alle Spieltransaktionen sind restauriert, alle Testdesktops geschlossen. Der ursprüngliche Runtime-/Settings-/EarlyProvider-Prototyp ist entfernt; die verbleibende Automation besitzt keinen zweiten Rettungskern.

Nutzerentscheidungen bleiben verbindlich: `EnableTrappedUnitRescue` ist standardmäßig AN; allgemeine Rettung und separat entfernbare Reiterkorrektur folgen `EnableMod && EnableTrappedUnitRescue`. Der Clientmaster sperrt diese Hostregeln nicht. OFF führt keine eigene historische Arbeiterbereinigung aus; eigene Legacyarchive bleiben erhalten. Bauern/Arbeiter werden ohne Warenkompensation entfernt, Soldaten und ursprüngliche Besatzungen erhalten. Keine echte MP-Freigabe, kein neuer Vollscan für alte Fallen ohne kausalen Vorherbeleg. APIShared0.78 liegt als geschützte eigenständige Entwicklung unter `.work/a/rescue-api/001`; der kanonische Checkout bleibt unverändert. Kein Commit, Push, Release oder permanente Installation wurde durch diesen Auftrag vorgenommen.

Nachweise und finale Paketidentitäten: [FINAL-ACCEPTANCE.md](FINAL-ACCEPTANCE.md), [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md), `.work/a/rescue-sp/001/n8-final-normal-summary.json` und `results.json`.

## Historischer Zwischenschritt, 27.11.2026, 03:42 Uhr

Erfolgreiche Teilabnahmen reichen inzwischen bis183: neue zivile Entfernung samt Arbeitsplatz-/Heartbeatbereinigung für Peasant/Apple/Miller/Mine, KI-Arbeiter, echte Pending-Saves, historische Parkmigration, externe Entfernung/Worldchange, CurrentSafe-Next, vier Editorwelten und acht Bau-/Dig-/Fill-Regressionen. Aktuelle4000er-ABAB-Messung179 ist qualifiziert innerhalb der dokumentierten künstlichen Profil-/Profilergrenzen; keine allgemeine Lösch-/Burstkostenfreigabe. Alle Spieltransaktionen bis183 restauriert, kein Spieltest aktiv. Nächste zwei konkrete Arbeiten: (1) rein im Aux-Testsetup den vollständig auditierten tatsächlichen Cattle-Farm-Bauvertrag verwenden, statt der alten100%-Grün-Feldhülle, danach echtenTanner/Cow-Fall abnehmen; (2) tatsächliche Burst-/Warteschlangengrenzen gegen Kapazität/Ablauf/Fairness prüfen. Keine erneute20-Rollen- oder kartesischePeasantNext-Pflichtquote. Danach Produktionsaufteilung N7/N8 und normale Pakete/Ausschlussprüfung; aktuell nochDEVDIAG.

## Historischer Checkpoint 27.11.2026, 04:07 Uhr

Gerber-/Kuhbindung185 und Korrekturregression186 bestanden; normalesPaket/volleSuite/NoDevGate undechteNormalpaket-Serie187 bestanden. Die bestätigteRecovery-Reentry-Lücke wurde gezielt behoben und offline konkret abgesichert; keine Frist-/Capänderung dabei. AlleSpieltransaktionen bis187 restauriert, keine aktiveTestinstanz. Produktions-/SDK-Quellen unverändert, keinCommit/Release. **Nächster maßgeblicherArbeitsblock ist N6 nach BURST-BACKLOG-PLAN.md:** kompaktevollständigeVorhernachweise/fairebudgetierteQueue, korrekteInvalidierung undSavegröße, dann echteBursttests. Der vorherigeCheckpoint03:42 und ältereDatumsblöcke sind historischeZwischenstände; Tanner ist jetzt abgeschlossen. N7/N8 bleibenoffen. NativeBaseline strukturgerecht ergänzt/gelintet.

## Checkpoint 27.11.2026, 02:30 Uhr

Normales Paket und vollständige Offline-Suite bestanden, Entwicklungsdiagnose im normalen Paket ausgeschlossen. Spielbelege bis174 sind archiviert und alle Transaktionen restauriert. Keine Produktion/SDK-Quellen geändert, kein Commit oder Release. Der neue Hauptpfad ist implementiert; die Gesamtaufgabe bleibt aktiv und unvollständig. Nächster Arbeitsblock sind die unten verlangten weiteren Hilfsbindungen, KI-/Quellen-, Pending-Save-/Editor-/Lifecycle- und aktuellen Effizienzprüfungen; anschließend erst N7/N8.

## Checkpoint 27.11.2026, 03:10 Uhr

KI-Arbeiter OFF/ON175 und sichtbarer Müller OFF/ON177 erreichten mit echter kausaler Änderung die neuen Entfernungs-/Bereinigungsziele. Echter Pending-Save/Load OFF175 und ON178 bestanden einschließlich identischer v5-Daten, genau einer eigenen Life3-Markierung und tatsächlicher späterer Ketten-/Arbeitsplatzbereinigung ON.175/177 bleiben wegen nachfolgender Fixturefehler korrekt Teilbelege;176 wurde wegen eines belegten unerreichbaren alten Job-Testguards abgebrochen und restauriert. Ein vollständig geprüfter geometrischer Aux-Testhelper ersetzt den überholten Jobguard. Mine-Setup benötigt noch die korrekte Unterscheidung zwischen akzeptiertem Next und inaktivem packedNext-Cache; keine nativen Cachewrites. Aktuelle4000-nojesters-Messung179 läuft, keine allgemeine Performancefreigabe daraus vorwegnehmen. Neue normative N4-Audits zu Auxiliary-Bindungen und KI-Anstellung sind in RECRUIT_TRANSFORMATION sowie den Vanilla-Zusammenfassungen units/economy eingetragen; Mod-/Testbelege bleiben in der Testdokumentation. Aktuelles Paket ist DEVDIAG; nach Ende der Serie normal neu bauen und Ausschlussprüfung wiederholen. Keine Produktions-/SDK-Quelländerung, kein Commit oder Release.

## Historische N6-Ergänzung: damalige Ereignisspitzen

Der Codeaudit bestätigt begrenzt verlustbehaftete512er-Erfassung/Batch-/Pendinggrenzen. Die4000er-Normalbetriebsmessung179 ersetzt keinen Burstabnahmenachweis. Der konkrete, noch nicht implementierte Entwurf und seine offenen Sicherheitsgates stehen in [BURST-BACKLOG-PLAN.md](BURST-BACKLOG-PLAN.md). Vor einer Fristverlängerung insbesondere spätere Geometrie zwischen Quelle und Bank vollständig prüfen; Capture/Frist/Speicherung nicht unabhängig voneinander hochsetzen.

## Aktuelle Integrationskarte N7, abgeschlossen am 27.11.2026

- BQoL besitzt `src/TrappedUnitRescue` und den vorhandenen Bootstrapadapter; kein zweites Plugin. Die bestehende `Lord.HomeTeleportGuard`-Runtime bleibt alleiniger19BA40-Besitzer, Rescue nutzt den geprüften Call-only-Einstieg.
- Genau eine default-AN Hosteinstellung `EnableTrappedUnitRescue` mit `EnableMod`-Masterbindung, Reset, Host-XAML und21Locales. Main und Acceptance deklarieren konsistent APIShared mindestens0.78.0.
- APIShared besitzt gemeinsame WorldMutation-/UnitMove-/Tick-/Lifecycle-/Save-Verträge. Native Trampoline werden aus auditierten Originalinstruktionen deterministisch neu encodiert und vollständig geprüft; keine Live-Probe und kein neuer konkurrierender Relocationhook.
- Produktions-Saveowner ist `BugfixesAndQoL_Serp`, Identifier `BugfixesAndQoLTrappedUnitRescue`. Kein automatischer Import experimenteller `TrappedUnitRescueOwnedWorkers`-Archive; bei OFF keine eigene Legacybereinigung.
- Die entfernbare Mounted-Scanner-Insel folgt derselben Hostoption; OFF lässt den ursprünglichen Fixes-Aufruf bestehen. Relevante IL-/Helfer-/Live-Stubverträge statt pauschaler Fixes-DLL-Hashbindung; unbekannte relevante Änderungen bleiben fail-closed.
- Native-/Core-/Saveprüfungen laufen gegen Produktionsquellen. Testmod, Fixtures, Editorbrushes und künstliche Performancewelt bleiben separat; das Acceptancepaket enthält nur DLL/PDB/info und keinen eigenen Runtime-/EarlyProviderbesitzer.

## Entfernung und Wegfall alter Komplexität

Gemeinsame Ereignisbesitzer, tatsächliche Vorher-/Nachhergeometrie, Current-/akzeptierte-Next-Erfassung, Lebensidentität, Deduplizierung und feste Budgets weiterverwenden. Einen Bauern nicht schon wegen eines einzigen fehlgeschlagenen Wegauftrags entfernen; es braucht den bestätigten zur Änderung gehörenden Feststeckfall. Maßnahmen erst nach dem vollständigen äußeren Bauvorgang am erlaubten Simulationspunkt durchführen.

Den Arbeiterzweig so früh trennen, dass seine Entfernung nicht mehr von erfolgreichem Job-/Feld-/Vegetationssnapshot, einer eigenen Bewegungsprobe, einer Bankkachelsuche oder alter Worker-Reissue-Policy abhängt. Erforderliche Typ-/Lebens-/Belegungs- und Wirkungskontrollen bleiben bestehen. Insbesondere nicht dieselbe vollständige Workerfortsetzung als Aufnahmebedingung behalten und nur am Ende die Maßnahme austauschen. Für allgemeine Beobachtung/Entfernung einen belegten bestehenden APIShared-/Extenderpfad nutzen; keine unnötigen neuen nativen Detours.

WorkerTravelPolicy/Job-/Journey- und NativeWorkerResumeWorld-/Supervisor-/Save-Anteile nach tatsächlichen Verbrauchern prüfen. Gemeinsame Command-/Crew-Funktionen zuerst sauber lösen; anschließend ausschließlich überholten Code, Worker-Fortsetzungsfixtures und Buildaufrufe entfernen. Bestehende relevante Tests aktualisieren; alte Erwartungen nicht nur auf Erfolg umbiegen. Bereits gespeicherte experimentelle Pending-/Parkzustände erhalten vor einem etwaigen Verwerfen einen sicheren belegten Übergang, damit kein eigener Parkzustand dauerhaft zurückbleibt. Keine automatische Übernahme alter Prototyp-Saves in den Hauptmod erfinden.

Entfernung ist keine Produktionskompensation: Waren, Yield, AI, Tierreservierungen und Jobstadien nicht künstlich nachspielen. Bei Cow-/Tanner- und Mine-/Millerfällen die tatsächliche native Bereinigung der Bindungen beobachten; ein normaler nativer Tier-Timeout ist von einer neu verursachten dauerhaften Fehlbindung zu unterscheiden.

## Neue Abnahmekriterien

- Bauern/Arbeiter: echter reproduzierbarer Trap, eigene Aktion nur ON, ursprüngliche Identität endgültig entfernt, keine restliche Current-/Next-Belegung und keine Aktion auf eine wiederverwendete ID. Arbeitsplatzslots werden nach dem nativen Vertrag freigegeben; unter kontrolliert zulässigen Bedingungen wird natürliche Neubesetzung mit neuer Identität nachgewiesen. Keine sofortige Ersetzung verlangen.
- Auch arbeitslose Bauern, Landwirtschaft, normale Input-/Outputreisen und unterschiedliche tatsächliche Mengen/Fracht prüfen. Repräsentative Sonderbindungen gezielt absichern: Tanner/Cow, aktive Mine-/Millerplätze sowie die gesonderte Ox-/Hilfseinheitenpolitik. Die Klassifizierung muss alle gewollten Familien erfassen; das ist keine erneute Forderung, jeden Arbeitsweg/Produktionszyklus wieder aufnehmen zu können.
- Negative Fälle: OFF, No-op, abgelehnter Writer, legaler Ausgang, bloß unerreichbares Ziel, regulär Hidden/Idle, Rekrutierung, Tod, Slotreuse, Weltwechsel und bereits erfolgte Vanilla-/Fixes-Entfernung. Unzureichende Vorbedingungen bleiben fehlgeschlagene Tests, kein Zufalls-PASS.
- Soldaten: Identität/HP/Verband erhalten, tatsächlich zulässige lokale Zielkachel und Bewegung nach der Befreiung; KI-Leader/-Mitglied, neue Befehle, stehende/begonnene Schritte, Angriffs-/Kletter-/Sondergrenzen und fehlender sicherer Zielort gezielt prüfen. Geräte und originale Ingenieure unverändert erhalten.
- Lifecycle/Save: vor/nach einer Maßnahme, ausstehende Kandidaten, Tod/Slotreuse, Laden/Editor/Ende und eigene überholte Worker-Parkfälle. Keine halb abgeschlossenen nativen Aktionen serialisieren oder wiederholen.
- Performance: vorhandene verwertbare Capture-Messungen als datierte Teilbelege erhalten. Belastungen und tatsächlich erforderliche Grenzwerte vor dem jeweiligen Lauf festlegen; historisch vorbereitete Varianten nicht ungeprüft vollständig abarbeiten. Funktionale Tests und passive Messungen getrennt qualifizieren.

Die bisherigen **13/20 Workerrollen** und Zweichargenbelege beschreiben den alten Erhaltungsansatz. Sie sind keine Pflichtquote und kein Abnahmenachweis für den neuen Entfernungspfad. Neue Belege ausdrücklich mit Maßnahme und erwartetem Resultat kennzeichnen.

## Kompatibilität, Migration und Abschluss

APIShared bleibt Besitzer gemeinsamer WorldMutation-/UnitMove-/SimulationTick-/Lifecycle-/Save-Verträge. Fixes darf reguläre eigene Entfernungen weiter ausführen; seine Unterdrückung für die entfallende Workerfortsetzung überprüfen und entfernen beziehungsweise auf nachweislich weiterhin nötige eigene Soldatenaktionen begrenzen. Keine fremden Einstellungen verändern. Die getrennte entfernbare Mounted-Scanner-Korrektur bleibt an ihren relevanten Upstreamvertrag gebunden. Gemäß neuer Nutzerentscheidung wird sie gemeinsam mit allgemeiner Rettung durch `EnableMod && EnableTrappedUnitRescue` gesteuert; OFF führt den ursprünglichen Fixes-Aufruf aus. Unbekannte relevante Änderungen lassen die eigene Korrektur inaktiv.

Die BQoL-Migration des vereinfachten Kerns ist mit den oben genannten Endbelegen abgeschlossen. Die alte Migrationskarte unten ist keine Anweisung, WorkerRecoveryService samt alten Worker-Savedaten und Feldkorrekturen blind zu übernehmen. Lord-/Relocation- und andere native Hookbesitzer erhalten; keine zweiten Hooks oder Broker. Genau eine neue Hosteinstellung mit Masterbindung, vollständiger UI/Locale-/Resetverdrahtung, geprüftem Default und konsistenter SDK-Version. Prototyp-Testsettings sind keine bereits festgelegten Produktionsdefaults.

Der umgestellte Mod, die abgenommene Integration in BugfixesAndQoL und die Entfernung der ersetzten Runtime sind jetzt belegt. Keine Ingame-Testlogik in Produktionsmods. Kein Commit/Push/Release ohne eigenen Auftrag. Bei notwendiger Nutzerentscheidung zuerst unabhängig mögliche Arbeit fertigstellen und den konkreten verbleibenden Punkt nennen; keine erneute Grundsatzdiskussion über akzeptierte Warenverluste oder Neubesetzungsverzögerung.

## Nachweise und Fortsetzungseinstiege

- [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md): historischer Laufnachweis und künftige ausdrücklich neue Abnahmeergebnisse.
- [INGAME-PENDING.md](INGAME-PENDING.md): historische Restlisten; für neue Läufe gelten die Kriterien oben.
- [WORKER-GAME-MATRIX.md](WORKER-GAME-MATRIX.md): alte Worker-Fortsetzungsabdeckung; kein aktiver Arbeitsvorrat.
- [IMPLEMENTATION.md](IMPLEMENTATION.md), [API-HANDOFF.md](API-HANDOFF.md), [TESTING.md](TESTING.md): vorhandene technische Quellen und überprüfbare Build-/SDK-/Testwege; ihre Zustandsangaben vor Nutzung erneut prüfen.
- Baseline über [CURRENT.md](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md): bestätigte native Entfernung, Migration/Jobvergabe, Belegung, Command- und Produktionsgrenzen.
- `.work/INDEX.json` und `.work/a/rescue-sp/001/results.json`: aktueller Arbeitsstand. `.work/a/rescue-sp/001/PAUSED.md` bleibt historischer Pausenbeleg. Lauf164 bleibt USER_PAUSED_PARTIAL und restauriert; Lauf165 ist eine neue Abnahme des Entfernungspfads.

## Archiv: früherer Arbeitsplan und datierte Nachweise

**Der gesamte folgende Teil ist Historie.** Aussagen wie „nächster Schritt“, offene P0–P10-Kästchen, Worker-Identitätserhalt und Pathfinding-Zielmigration gelten nur für ihren damaligen Stand. Der aktive Plan oben ersetzt diese Handlungsanweisungen; Vanilla-Audits, Quellen und ehrlich begrenzte historische Testergebnisse bleiben erhalten.

## Plan: allgemeine Einheitenrettung und spätere Integration in Pathfinding

Erstellt: 17.11.2026. Fortschritt aktualisiert: 26.11.2026. Arbeitsplan und fortlaufendes Abnahmeprotokoll.

### Neubewertung und akzeptierte Nachteile, 27.11.2026

Die laufende Gesamtaufgabe und Spieltests wurden vom Nutzer pausiert. Lauf164 ist unterbrochen und vollständig wiederhergestellt; abgeschlossene Einzelschritte sind kein Gesamt-PASS. Die nachfolgenden Fortsetzungs- und Abnahmepläne dokumentieren den bisherigen Ansatz und sind noch keine Freigabe seiner unveränderten Weiterführung.

Der Nutzer schlägt vor, nachweislich feststeckende Wirtschaftsarbeiter regulär vollständig zu entfernen und Vanilla die Neubesetzung zu überlassen; Soldaten sollen erhalten und lokal befreit werden. **Ausdrücklich akzeptiert:** Verlust transportierter Waren beziehungsweise bereits verbrauchter Inputs sowie verzögerte oder ausbleibende Neubesetzung unter ungünstigen Vanilla-Bedingungen. Diese Nachteile sind kein erneut vorzulegender Entscheidungspunkt. Die Empfehlung lautet deshalb, die komplexe rollenspezifische Arbeitsfortsetzung durch einen engeren Entfernungspfad für echte Wirtschaftsarbeiter zu ersetzen. Eine Runtimeumstellung wurde in diesem Analyseauftrag noch nicht durchgeführt.

**Ausdrückliche Ergänzung des Nutzers:** Auch feststeckende Bauern werden entfernt. Dazu zählen gewöhnliche Peasants einschließlich arbeitsloser Lagerfeuerbauern sowie landwirtschaftliche Arbeiter. Eine bereits gestartete Rekrutierung eines Soldaten oder rekrutierten Spezialisten darf dabei nicht allein wegen des vorübergehend noch sichtbaren Peasant-Typs als gewöhnlicher Bauer klassifiziert werden; die dokumentierte gleichbleibende Rekrutierungsidentität und der gewünschte Zieltyp bleiben maßgeblich.

Bestehende ereignisbasierte Erkennung, echte Geometrieänderung, ID/Global-/Lebensprüfung, feste Budgets und APIShared-Hookbesitzer bleiben nützlich. Entfernung darf keine bloß untätigen, regulär verborgenen, bereits sterbenden oder absichtlich gelöschten Einheiten treffen. Soldaten, rekrutierte Ingenieure, Belagerungsgeräte und deren Mannschaften dürfen nicht allein wegen einer Nicht-Soldaten-Klassifizierung in diesen Pfad geraten. Ox und andere vom Gebäude erzeugte Hilfseinheiten brauchen ihren eigenen geprüften Ersatzvertrag.

Vor einer Umsetzung ist der gemeinsame Entfernungseinstieg mit Simulationstiming und vorhandenen Fixes-Unterdrückungen abzugleichen; danach braucht es gezielte Tests für endgültige Entfernung, Slotfreigabe, belegte Kacheln und natürliche Neubesetzung unter zulässigen Bedingungen. Kuhreservierung und aktive Arbeitsplatzslots sind ausdrücklich zu prüfen. Waren-/Jobkompensation oder künstliche Sofortersatzbauern sind nicht aus dieser Entscheidung abzuleiten. Allgemeine Spieltests werden hiermit nicht erneut gestartet.

Die zugehörigen gesicherten Vanilla-Erkenntnisse sind in der Baseline ergänzt: `RECRUIT_TRANSFORMATION.md` (Entfernung/Recount/Migration/Jobvergabe), `WORKER_PRODUCTION_BONUS.md`, `WEAPON_WORKSHOP_FIELDS.md`, `HUNTER_PREY_RULES.md` und `TRAPPED_UNIT_RESCUE_AUDIT.md`. Offene alternative Disband-/Recyclingpfade und der genaue KeepDoor-zu-Lagerfeuer-Weg bleiben als solche gekennzeichnet.

### Aktueller Auftrag vom 26.11.2026

Der Nutzer autorisiert jetzt die erforderlichen **Singleplayer-Ingame-Tests**, weitere Fehlerkorrekturen und Effizienzprüfung. Die anfängliche Wartebedingung auf den Chat „Mehrfachplatzierung erweitern“ wurde erfüllt; die inzwischen ausgeführten Läufe und ihre Wiederherstellung sind im Abnahmeprotokoll belegt. Die Testfälle werden kontrolliert erzeugt, mit Friedenszeit, festgelegter geeigneter Karte und deaktivierten RandomEvents. Ausführungs- und Abnahmeplan: [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md).

**Neue Zielentscheidung:** Nach erfolgreicher Finalisierung erfolgt die Übernahme in **BugfixesAndQoL** und die Entfernung des ursprünglichen Testmods. Die nachfolgenden älteren Pathfinding-Migrationsabschnitte beschreiben die frühere Planung; ihre technischen Kompatibilitäts- und Ownershipanforderungen bleiben relevant, der Zielmod ist durch diesen neuen Auftrag ersetzt. Reale Multiplayerfreigabe gehört nicht zu diesem Auftrag.

### Abnahmefortschritt am 26.11.2026, 23:34 Uhr

- [x] Vorhandenen DoNothing-Lord über normale Lobbybefehle einbinden und tatsächliche geladene native Identität sowie AIC-/AIV-Fingerprints prüfen; gezielt für die neuen Arbeiterfälle, keine Nullaktivitätsgarantie.
- [x] Natürlicher Fletcher-R7-Bowfall OFF/ON einschließlich beider vollständiger Waffenlieferungen und neuem Produktionszyklus: Lauf159 PASS.
- [x] Natürlicher Woodcutter-R1-Fall mit demselben Baum, drei Stämmen, tatsächlichem Yield18 und vollständiger Lagerbuchung: Lauf160 PASS. Vorherige Fixturefehler157/158 bleiben separat dokumentiert.
- [x] Bestehende fünf Geräte-/Besatzungsfälle mit aktuellem Capture-Optimierungsstand wiederholen: Lauf161 PASS; synthetische Blockierung, keine automatische Quellenfreigabe daraus ableiten.
- [x] Miner1 R2 mit ursprünglichem Arbeitsplatz und zwei echten Eisenbuchungen; Hunter R5 mit zwei verschiedenen natürlichen Beutetieren und vollständigen Fleischchargen: Lauf162 PASS.
- [ ] Übrige sieben Arbeiterrollen und bislang offene Phasen-/Produktvarianten akzeptieren; aktuell13/20 Rollen mit mindestens einem positiven natürlichen Fall. Hunter-, Farmer- sowie Grunt-/Pitchman-Verträge werden vor neuen Fixtures vollständig auditiert.
- [ ] Große Populationen, Quellenbursts, Budgets/Tails und restliche Lifecycle-/P7-/Geometriefälle abschließen; anschließend Produktionsintegration und ursprünglichen Prototyp entfernen. Keine reale MP-Freigabe.

Alle Läufe159–162 wurden auf dem getrennten Desktop ausgeführt und vollständig wiederhergestellt. Vollständige Belege und Grenzen: [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md), aktuelle Rollenübersicht: [WORKER-GAME-MATRIX.md](WORKER-GAME-MATRIX.md). Dies ersetzt keine noch offene Gesamtfreigabe.

### Konkrete BQoL-Migrationskarte (26.11.2026)

Dies ist die vorbereitete Quellzuordnung für die bereits festgelegte Zielintegration in **BugfixesAndQoL**, keine durchgeführte Migration oder abgeschlossene Gesamtfreigabe. Erst nach der verbleibenden Singleplayer-/Effizienzabnahme wird integriert und anschließend der Prototyp entfernt. Die älteren Pathfinding-Zielabschnitte unten bleiben historische Planung. Durchführung und Grenzen stehen in [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md), SDK-Verträge in [API-HANDOFF.md](API-HANDOFF.md).

Ein featureeigener Bereich `BugfixesAndQoL/src/TrappedUnitRescue` genügt. Thematische Unterordner können bei Bedarf entstehen; eine tiefe Verzeichnishierarchie ist keine Integrationsvoraussetzung.

| Quellen des Prototyps | Zuordnung im Feature |
|---|---|
| `RescueRuntime`, `RescueDiscovery`, `RescueMutationFootprints`, `RescueCleanupProjection`, `RescueCandidateQueue`, Geometrienachweise und Pending-/Geometriecodecs | Rettungskern, kausale Nachweise und begrenzte Verarbeitung |
| `GeneralRescueObserver` einschließlich `Replans`, `RescueOrderObserver`, `RescueReplanIntent` | Beobachtung über die bestehenden APIShared-Besitzer |
| Worker-Policies, Binding-/Journey-Snapshots, Ledger, Supervisor und `WorkerRecoveryService`; Command-Policies/-Snapshot und `IdleEscapeOwner` | Qualifizierte Fortsetzung, neuer Idle-Fluchtauftrag und eigene Savedaten |
| `NativeRelocation`, `NativeMutationFootprints`, `NativeVegetationAccess`, `NativeWorkerResumeWorld`, `NativeIdleEscapeWorld` | Bestehende auditierte native Adapter und Readbacks |
| `FixesIntegration` und `FixesManagedContract` | Optionale schmale Fixes-Kompatibilität mit logisch begrenzter Unterdrückung |
| Vier Dateien `FixesMountedScanner*.cs` | Gesondert vollständig entfernbarer Kompatibilitätsbereich; [aktueller Runtime-Vertrag](Audits/FIXES-MOUNTED-RUNTIME.md) |

**Quellausschlüsse:** `RescueRuntime` vor Übernahme von `BeginMutation`/`MutationScope` samt tatsächlichem Testcensus über IDs 1..9999 sowie `AppleMoatReceipt` und ausschließlich dafür benötigten Helfern trennen. Test-`Plugin`, Testsettings und `Metadata`, alle `Fixture.*`, `EditorBrush`/`EditorAcceptanceBrushes`, frühe Provider-/Mono-Testassets, Census, NoJesters und fixturespezifische Performance-/Diagnoseteile gehören nicht in BQoL. Zugehörige Diagnoseaufrufe gemeinsam entfernen; keine fehlenden privaten Diagnoseklassen oder Testabhängigkeiten im Produktions-Diagnosebuild hinterlassen. Geeignete allgemeine `Shared.DevDiag`-Messpunkte können nach ihrem bestehenden Compilegate erhalten bleiben.

**Runtime und gemeinsame Besitzer:** Ein Adapter wie `BugfixesAndQoLRuntime.TrappedUnitRescue.cs` hält genau einen prozessweit verwurzelten Rettungsverbund. Er bindet Initialisierung an den bestehenden LibraryLoaded-Pfad, Einstellungsübernahme an `ApplySettings` und Sitzungsbeginn/-ende an die vorhandenen Gameplay-/Mission-Lifecycle-Publisher. `WorldMutationEvents`, `UnitMoveEvents`, `SimulationTickEvents` und `VersionedSaveData` bleiben ihre gemeinsamen APIShared-Besitzer; keine zusätzlichen Entry-Detours, kopierten Broker oder Fixtureflags übernehmen. Capability-/Registrierungsfehler sperren den betroffenen Rettungspfad. Ohne aktive Einstellung bzw. ohne Arbeit bleiben lokale Erfassung und eigene native Rettungsaufrufe aus.

**Native Ownership ausdrücklich erhalten:** [LordTeleportGuardRuntime](../../BugfixesAndQoL/src/LordTeleportGuard/LordTeleportGuardRuntime.cs) bleibt unter `Lord.HomeTeleportGuard` exklusiver Besitzer der **16 Byte ab 19BA40 bis 19BA50**. Seine Initialisierung geht dem Rettungsadapter voraus; dieser nutzt den geprüften Einstieg einschließlich vorhandenem Guard und dessen unterstützte Call-only-Registrierung. **Kein zweiter 19BA40-Detour**, kein Bypass über ein fremdes Trampolin und keine Änderung des bestehenden Lord-Emitters oder seiner ABI. Der bereits auditierte separate Fixes-Worker-Guard des Prototyps behält seine eigene disjunkte Spanne und wird nur während eigener qualifizierter Rettungs-/Fortsetzungsaktionen logisch aktiviert. Veröffentlichte Hooks bleiben verwurzelt; unbekannte oder überschriebene relevante Spannen werden nicht zurückgepatcht.

**Genau eine neue Hosteinstellung:** Eine `[SyncHostOnly]`-Property, Arbeitsname `EnableTrappedUnitRescue`, kommt unter den vorhandenen BQoL-Master `EnableMod`. Setter, konsistenter Default/Reset, `ModSettingsPresentation.Topics`, Host-Fixes-XAML einschließlich Suche/Tooltip und Locale-Schlüssel werden gemeinsam ergänzt. Der ausgeschaltete experimentelle Testmoddefault ist keine bereits beschlossene Produktionsvoreinstellung. Originale Fixes-Einstellungen werden nicht verändert; ohne Fixes funktioniert die allgemeine Rettung weiterhin. Keine neue harte Abhängigkeit von Fixes, Pathfinding oder einem Testmod.

**SDK, Save und Modi:** Plugin/Manifest benötigen mindestens APIShared 0.74.0 mit den tatsächlich geprüften Diensten; Runtime und Bootstrap werden aus demselben ausdrücklich gewählten Paket verwendet, die Bootstrap-Referenz bleibt `Private=false`. Die Versionsnummer allein ersetzt keinen Paket-/Vertragsnachweis oder die spätere SDK-Übernahme. `WorkerRecoveryService` bekommt BQoL als Save-Owner-GUID und einen stabilen Produktions-Feature-Identifier. Atomare Capture-Sperren, `SaveGamesAndMaps`, Pending-/Geometriearchive, Alter/Versuchsverbrauch sowie Welt-/Order-Rebinding vor dem ersten zugelassenen Takt bleiben erhalten. Keine automatische Übernahme unpublizierter Testmod-Saves oder Reparatur alter Fallen ohne eigenen Nachweis erfinden. Singleplayer und Editor bleiben zugelassen; echte Multiplayer-Rettung bleibt im aktuellen Auftragsumfang deaktiviert. Host-Synchronisierung allein gibt MP nicht frei.

**Produktionsbezogene Prüfungen und Ablösung:** Die weiterhin relevanten Core-/Native-/Persistenzfixtures nach `BugfixesAndQoL/tests/TrappedUnitRescue` übertragen und gegen die dortigen echten Produktionsquellen verdrahten. Die 66 Scanner-Verträge, vorhandene Lord-Guard-Tests einschließlich gemeinsamer Einstiegseigentümerschaft und betroffene Modsettings-/Präsentations-/Reset-/Localeprüfungen erhalten bzw. erweitern; BQoL-Projekt und Buildtreiber binden diese Prüfungen und Quellen ein. Während der Integrationsabnahme darf ein Testmod nur automatisieren/beobachten, nicht parallel einen zweiten Rettungsbesitzer veröffentlichen. Nach bestandener BQoL-Abnahme Prototyp und ersetzte Runtimeverdrahtung entfernen, notwendige kompakte historische Nachweise erhalten. Keine Ingame-Automation in den Hauptmod verschieben.

**Ausdrücklich noch offener Produktpunkt:** Soll die separate Fixes-Scannerkorrektur auch bei **BQoL-Master-OFF** wirken? Das ist bisher nicht entschieden und wird bei der Migration nicht stillschweigend festgelegt. Davon getrennt ist bereits verbindlich: **General-Rescue-OFF darf die originale Fixes-Reiterrettung bzw. deren Scannerkorrektur nicht abschalten.** Dafür wird keine zusätzliche Modsetting eingeführt.

### 1. Ziel, Auftrag und Zuständigkeit

Einheiten sollen nach einer Bau- oder Geländeänderung zuverlässig erkannt werden, wenn sie ihren Standort nicht mehr regulär verlassen können. Die Rettung erhält dieselbe Einheit, Gesundheit, Waren, Arbeitsplatz, Besatzung und die weiterhin gültige Befehlsabsicht. Ein unerreichbares Ziel oder längeres Stillstehen allein berechtigt zu keiner Versetzung.

Der Testmod bleibt bis zur fachlichen Abnahme der Entwicklungs- und Testort. Das spätere Produktziel ist auf ausdrücklichen Nutzerwunsch **Pathfinding**. Das ersetzt für dieses Rettungssystem den früher erwogenen Zielort BugfixesAndQoL. Wiederverwendbare Beobachtungsverträge können nach APIShared gehören; Rettungsregeln, Einstellungen und Befehlsfortsetzung gehören zum Rettungssystem und später zu Pathfinding.

Dieser Auftrag erstellt den Plan auf Grundlage des aktuellen Arbeitsstands. Er installiert oder veröffentlicht nichts und beginnt noch keine Migration in den Hauptmod. Produktionscode, fremde Quellen und vorhandene Nutzeränderungen bleiben dabei erhalten. Die vorhandenen Änderungen an `Pathfinding/src/TODO.md` und `Pathfinding/UpdateToNewDLL.md` sind kein Auftrag, sie umzuschreiben.

#### Was „allgemein“ hier bedeutet

- Menschen und KI werden anhand derselben fachlichen Rettungsregeln behandelt. Die aktuell vom Menschen ausgewählte Einheit ist keine Voraussetzung.
- Bau- und Bewegungsquellen sowie Einheitenfamilien werden über eine ausdrückliche Abdeckungsmatrix erschlossen. Ein erfolgreicher Speerträgertest gibt keine Arbeiterzustände frei.
- Legale Sonderbewegungen bleiben legal. Ein Graben, eine erhöhte Fläche oder eine Mauer ist nicht für jeden Einheitentyp ein ungültiger Standort.
- Fehlt ein sicherer Zielort oder ein belegter Zustandsvertrag, bleibt die Einheit unverändert und der Fall wird als nicht gerettet ausgewiesen. Unbekannte Speicherbeschädigung wird nicht durch blindes Teleportieren behandelt.
- Neue Fehler verhindern und vorhandene Fehler aus alten Saves reparieren sind unterschiedliche Aufgaben. Ohne Vorherdaten darf der zweite Fall nicht die Beweisregeln des ersten übernehmen.

### 2. Fortschritt und Arbeitsweise

Statuswerte: **offen**, **in Arbeit**, **teilweise geprüft**, **abgenommen**. Ein Kästchen wird erst mit benanntem Nachweis abgeschlossen. „Nicht ausgeführt“ und „nicht unterstützt“ sind kein PASS.

| ID | Arbeitspaket | Status | Abhängigkeit / Ergebnis |
|---|---|---|---|
| P0 | Vorhandene Rettung, Pathfinding und Schnittstellen einordnen | Abgenommen für die dokumentierte Bestandsaufnahme | Abschnitt 3; vorhandene Nachweise in Abschnitt 4 |
| P1 | Erzeuger, Zustandsübergänge und Beobachtungsverträge auditieren | Statisch geschlossen und implementiert für die freigegebenen Quellen | F1 aus APIShared0.73.4 übernommen; WorldMutation- und finaler UnitMove-Broker in eigener API-Kopie vollständig offline geprüft |
| P2 | Gemeinsames Kandidatenmodell, Nachbeobachtung und Budgets | Implementiert und integriert offline geprüft | Queue, lokale Current/Next-Discovery, Coordinator, kausale Save-Daten und feste Budgets; keine permanenten Vollscans |
| P3 | Bau- und Geländeänderungen automatisch anbinden | Implementiert; offline geprüft; Spielabnahme offen | 15 gemeinsame Quellen, vollständige native Hüllen einschließlich Compound/Keep-Rotation und Cleanup |
| P4 | Gewöhnliche Soldaten und KI-Gruppen im Normalbetrieb absichern | Typ-/Phasenmatrix und Befehlsbesitz offline geprüft; neue Gruppen-Spielabnahme offen | Stationäre Semantik, aktive Fortsetzung und neue fremde Befehle getrennt; ursprünglicher Verband bleibt erhalten |
| P5 | Begonnene Schritte, Next-Belegung und Sonderflächen | Gewöhnliche Schritte implementiert/offline geprüft; Sondermodi bleiben gesondert | Ein zuvor erfasster Schritt darf fortschreiten; legale Sonderbewegungen werden nicht als normale Blockade behandelt |
| P6 | Zivile Arbeit, Grabenfälle und Besatzungen integrieren | Automatische Anbindung implementiert und offline geprüft; neue Spielabnahme offen | 20 Rollen, qualifizierte Command-Reisen, eigener untätiger Moat-Fluchtauftrag und Crew-/Cargo-Schutz |
| P7 | Bewegungsfehler als weitere Kandidatenquelle | Kausale Geometrie und frische Absicht implementiert; integrierte Offline-Abnahme | Nur mit zuvor beobachteter Vorhergeometrie; keine Rettung allein aufgrund nativer Ablehnung |
| P8 | Lifecycle, Saves, Editor und Multiplayer | Lifecycle und binäre Persistenz offline geprüft; echte Abnahme offen | Atomare Phasengates, Weltwechsel, Alters-/Retryerhalt; MP bleibt gesperrt |
| P9 | Normalbetrieb, Last und Kompatibilität abnehmen | Statische Budgets und Vertragsablehnung offline geprüft; reale Messung offen | Operationszähler sind keine Simulations-/FPS-Abnahme |
| P10 | Abgenommenen Kern nach Pathfinding überführen | Offen | Testmod-Abnahme und genau ein aktiver Rettungsbesitzer |

**Nächster konkreter Arbeitsschritt:** Gesondert autorisierte Spielabnahme nach INGAME-PENDING.md vorbereiten. Der Offlineauftrag einschließlich überschneidender Folgegeometrie, vollständigem Paketbuild und SDK-Übergabenachweis ist abgeschlossen;64101 Testmodprüfungen sowie1787 SDK-Tests bestanden. Finale UnitMove-Argumente, native Rückgaben, Common-Owner-Anbindung, Save-/Ordergrenzen sowie gewöhnliche und untätige Moat-Fortsetzung sind implementiert und integriert offline geprüft. Fortschritt und noch ausstehende Spielprüfungen stehen in [IMPLEMENTATION.md](IMPLEMENTATION.md) und [INGAME-PENDING.md](INGAME-PENDING.md).

#### Offline-Fortschritt vom 25.11.2026

Die relevanten ausführbaren Offline-Prüfungen des vorhandenen Prototyps und des aktuellen Pathfinding-Arbeitsstands bestehen. Einzelheiten, Quellidentitäten, verworfene Laufversuche, Vertragsmatrix und offene Grenzen stehen in [OFFLINE-CHECKS.md](OFFLINE-CHECKS.md). Geprüft wurden der Testmod-Build mit 1.807 Einzelprüfungen plus Fixes-Verträgen, NativePatchGuard, 105 ausgewählte APIShared-Tests, 62 Bauzustandsprüfungen, Quell-/XAML-/Locale-Prüfungen und sämtliche vom normalen Pathfinding-Build ausgewählten Offline-Suiten. Eine fehlende explizite Tooltip-Stilzuweisung wurde im Testmod korrigiert; die betroffenen Prüfungen und sein Build wurden erfolgreich wiederholt.

Es wurde nichts installiert und kein Spiel gestartet. Die Abnahme vom 17.11. bleibt historisch. Der aktuelle Referenzstand ist Extender 2.14.4, Fixes 1.31.0 und APIShared 0.73.3; Pathfinding-Quellversion 0.1.16. Der native Hash ist unverändert. Die ältere Interop-Provenienz des Baseline-Datasets muss vor zusätzlichen nativen Integrationen relevant validiert werden. Die bekannte veränderliche Scanner-Capture im Fixes-Reiterinstaller ist statisch weiterhin vorhanden; ihre aktuelle Spielwirkung wurde nicht neu geprüft.

P1 ist durch die dokumentierten AIV-/Bau-/Spiegel-/Editor-/Bewegungsverträge vorangekommen, aber noch nicht geschlossen. P2–P10 erhalten durch erfolgreiche Tests des bestehenden Codes keine pauschale Freigabe. Weiter offen sind insbesondere zusätzliche endgültige Änderungsabschlüsse, alte Ausweichbereiche, Next-/Schrittzustände, automatische Arbeiter-/Besatzungspfade und die spätere echte Spiel-/MP-/Lastabnahme. Bis zur nächsten Umsetzung zuerst diese P1-Lücken für die ausgewählte Quelle schließen.

#### Anschließender vollständiger statischer Audit vom 25.11.2026

Der spätere ausdrückliche Auditauftrag schließt die zuvor genannten statischen P1-Lücken. Einstieg und verbindliche Teilberichte: [Audits/AUDIT.md](Audits/AUDIT.md). Untersucht wurden sämtliche 23 Creator-Dispatchfamilien des gemeinsamen Baupfads, AIV/Prebuild, Menschen-/KI-Mauern, finale Original-/Mirroraufrufe, terminales Dig/Fill, alle sieben äußeren Editorwriter, terminaler Abriss, gewöhnliche und besondere Current-/Next-/Belegungsfolgen, Tunnel/Assassinen/Portal-/Ladderzustände, alle 20 Wirtschaftsrollen, Geräte/Crew sowie Lifecycle, Save, Resync und aktuelle Hookbesitzer. Sichere Nichtfreigaben sind mit konkreten legitimen beziehungsweise schädlichen Folgezuständen begründet; nicht durch pauschales „unbekannt“ ersetzt.

Der Audit beschreibt den additiven Brokervertrag einschließlich eines äußeren Mirror-Batchscopes, effektiver Childinvocations, nicht werfender Fehlerbehandlung und begrenzter innerer nativer Arbeit. Er belegt eine Current-Occupancy-Discovery mit einem Tile Halo für gewöhnliche Next-Schritte und trennt sie ausdrücklich von entfernten Portal-/Transportendpunkten. Nominale BuildingScale-Quadrate sind für Compoundbauten nicht allgemein ausreichend. Die Worker-Matrix beschreibt rollenabhängige Reise-/Kontakt-/Cargo-/Retryverträge; sie aktiviert keine zusätzlichen Runtimezustände.

**Bestätigter Zusatzbefund:** [F1](Audits/AUDIT-FINDINGS.md): Ein erneut werfendes `Exception.ToString()` im AIV-Fehlerreporter kann Original oder weitere Receipts verhindern beziehungsweise eine Originalexception maskieren. Drei Gegenbeispiele und ein normaler Kontrollfall wurden am tatsächlichen Workspace-Produktionsdispatch offline reproduziert, unabhängig wiederholt und mit Assembly-SHA/MVID dokumentiert. F1 wurde in diesem Auditauftrag nicht korrigiert. Das ist ein seltener bestätigter Fehlerpfad, kein neu beobachteter Spielschaden.

Native/Managed-Hashes wurden erneut geprüft; die Extenderstände von Dataset, installierter Referenz und aktuellem Quellbaum2.14.6 wurden getrennt abgeglichen. Keine Installation, kein Spielstart, keine Runtimequellenänderung. Die wiederverwendbaren neuen nativen Fakten stehen zusätzlich im bestehenden Baseline-Dossier `TRAPPED_UNIT_RESCUE_AUDIT.md`. P2–P10 bleiben Implementierungs-/Abnahmearbeit; Altsave-/Besitzer-/Persistenzentscheidungen bleiben ausdrücklich offen.

#### Aktueller Implementierungsauftrag nach dem Audit

Der anschließende Auftrag umfasst sämtliche sinnvoll ausführbaren Offline-Schritte im Testmod und einer isolierten APIShared-Arbeitskopie. Kandidatenkern, lokale Current/Next-Erkennung, automatische Brücken-/weitere Bau-/Geländequellen, gewöhnliche Soldaten, qualifizierte Arbeiterreisen, Besatzungen und kausaler Replan sind implementiert. Die F1-Korrektur aus dem anderen Chat ist vollständig übernommen. Aktuelle Quellen und Nachweise: [IMPLEMENTATION.md](IMPLEMENTATION.md), [API-HANDOFF.md](API-HANDOFF.md).

Die folgenden P0–P10-Checklisten sind die vollständige Funktionsabnahme einschließlich Spiel und späterer Migration. Ein noch offenes Kästchen kann deshalb eine bereits offline bewiesene Teilanforderung enthalten; es behauptet nicht automatisch fehlenden Code. Die detaillierten neuen Spielprüfungen stehen in [INGAME-PENDING.md](INGAME-PENDING.md). Alte Abschnitte mit älteren SDK-Versionen und damaligen P1-Lücken sind datierte Historie und werden durch diesen aktuellen Status ergänzt, nicht als heutige Sperre wiederverwendet.

Nicht Bestandteil des Offlineabschlusses sind Installation, reale Spiel-/Editor-/Save-/Last-/MP-Abnahme, Reparatur historischer Fälle ohne Vorherdaten, eine neue Politik für fremde Besitzer und die spätere Migration nach Pathfinding. Die vorhandene Besitzerpolitik wird ausdrücklich beibehalten; ein enger Beleg wird nicht als allgemeine Freigabe weiterer Zustände ausgegeben.

#### Bei jeder Fortsetzung

1. Diesen Plan, `TESTING.md`, relevante Updateverträge und `.work/INDEX.json` lesen; Branch, HEAD, Arbeitsstand und referenzierte Nachweise erneut prüfen.
2. Ein Arbeitspaket auswählen und als „in Arbeit“ markieren. Geänderte Annahmen und Entscheidungen im Änderungsprotokoll festhalten.
3. Vor neuen nativen Eingriffen den vollständigen featurebezogenen Audit einschließlich Abbruch-/Fehlerpfaden, nachgelagerten Lesern, Fremdpatches und Parameterdomänen abschließen. Die unten genannten Suchorte sind keine Freigabe für neue Hooks.
4. Kleine, überprüfbare Implementierung im Testmod beziehungsweise eine notwendige allgemeine APIShared-Erweiterung vornehmen. Betroffene vorhandene Tests im selben Schritt pflegen.
5. Relevante Offline-, Integrations- und Spielprüfungen durchführen. Erst dann Status, Abdeckung, Grenzen und Belegpfade aktualisieren.
6. Historische Fehlerbelege nicht überschreiben. Reproduzierbare temporäre Kopien aufräumen, Originalinstallation verifiziert wiederherstellen. Keine Commits oder Veröffentlichungen ohne Auftrag.

### 3. Geprüfter Ausgangsstand von Pathfinding

Die folgende Tabelle ist eine Momentaufnahme des lokalen Quellstands, kein dauerhafter Vertrag über Dateinamen oder Versionen. Ausgangs-HEAD des Mod-Repositories: `9dcd28b4384b8ff7efb5b2852a38832993cfc9db`, Branch `develop`, einschließlich vorhandener uncommitteter Änderungen. `Pathfinding/info.json` bezeichnet den Quellstand als Version `0.1.10`, mit APIShared-Mindestversion `0.53.0`.

Die installierte kanonische native DLL wurde für diese Bestandsaufnahme erneut gehasht und stimmt mit `CURRENT.json` überein:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Vor späterer Adressverwendung erneut gegen den dann aktuellen Baseline-Verweis prüfen. Es wurden für diesen Plan keine neuen Spiel- oder Buildläufe ausgeführt.

| Bereich / Quelle | Vorhandene Funktion | Bedeutung und Grenze für die Rettung |
|---|---|---|
| [NativeMovementRecovery.cs](../../Pathfinding/src/UnitCommands/Movement/NativeMovementRecovery.cs), `InstallConnectivityAndRecovery` | Besitzt native Beobachter für Kachelaktualisierung, Graben-Schreiben/-Löschen und Masken-Neuaufbau | Keine konkurrierenden Hooks an diesen Stellen. Meist Nachher-Invalidierung; kein vollständiger Vorher-/Nachher-Bauvorgang und keine alten Ausweichfelder. |
| Dieselbe Datei, `InvalidateMovementSearchData` | Erhöht `placementRevision`, verwirft Such-/Gruppencaches und invalidiert Kacheln, Richtungen oder ganze Daten | `placementRevision` steigt auch bei `terrain:false` in verschachtelten Bewegungsaufrufen. Nicht ungeprüft als reine Geländeänderung oder peerübergreifend identische Rettungsrevision benutzen. |
| Dieselbe Datei, `TryRecoverBeforeBuilder` | Eng begrenzte Wiederherstellung eines belegten freundlichen Grabenroutenfalls vor dem Builder | Kein allgemeiner Teleport oder beliebiger ungültiger Start. Benötigt vorhandenen UnitMoveFrame, passenden Plan/Modus/Ziel und validierte kodierbare Kanten. |
| [UnitMovementContext.cs](../../Pathfinding/src/UnitCommands/Movement/UnitMovementContext.cs) | `DispatchUnitMoveEvent` / `ObserveUnitMoveOrder` ordnen Pre/Post-Aufrufe mit Epoch, Tick und verschachteltem UnitMoveFrame zu | Nützlich für echte abgefangene Bewegungsaufträge, aber nicht jede native Suche oder physische Schrittablehnung. Post enthält ursprüngliche Argumente; übersprungene Originals haben kein Post. |
| [ManualGroupAdmission.cs](../../Pathfinding/src/UnitCommands/Runtime/ManualGroupAdmission.cs) | Behandelt manuelle Gruppen mit gemischt erreichbaren Mitgliedern | `BeginManualGroupAdmission` schließt KI-Besitzer außerhalb des Editors ausdrücklich aus. `EnableImprovedManualUnitCommands` schützt damit nicht allgemein KI-Gruppen. |
| [FastMovementScheduler.cs](../../Pathfinding/src/Engine/FastMovementScheduler.cs) | Gemeinsame Simulationstaktung, Gruppen-/Einzelbefehle und Route-Veröffentlichung | Fast-Gruppenrouting hat auch KI-Pfade. Das ist keine physische Rettung und keine Aufhebung der manuellen KI-Ausschlüsse. |
| [RouteReplanWatch.cs](../../Pathfinding/src/Engine/RouteReplanWatch.cs) | Begrenzte Routenvorschau, Kandidatenlisten, Wartezustände, Auftragssequenzen, Fortsetzung | Gute Vorbilder für Identität, neuere Befehle und deterministische Budgets. An `AnchorSingleMoves`, geeignete Soldaten und relevante Routenregeln gebunden; nicht unverändert für alle Arbeiter nutzbar. |
| [FireEscape.cs](../../Pathfinding/src/Engine/FireEscape.cs) | Gefahrenflucht und erneute Versuche | Eigene aktive Gefahrensteuerung schließt KI ausdrücklich aus. Diese Grenze nicht versehentlich in die neue allgemeine Rettung übernehmen oder pauschal entfernen. |
| [MovementSurfaces.cs](../../Pathfinding/src/Engine/MovementSurfaces.cs), [TraversalCommandState.cs](../../Pathfinding/src/Engine/TraversalCommandState.cs) | Oberflächenklassen, Revisionen und begrenzte Invalidierung | Wiederverwenden, soweit Vertrag/Profil/Thread passen; frisch validierte Live-Zielbedingungen bleiben erforderlich. |
| [GroundFailureProofs.cs](../../Pathfinding/src/Engine/GroundFailureProofs.cs), [MoatFailureProofs.cs](../../Pathfinding/src/Engine/MoatFailureProofs.cs), [AnchorRouting.cs](../../Pathfinding/src/Engine/AnchorRouting.cs) | Fehlernachweise, Such- und Veröffentlichungsbesitzer | Ein Fehlschlag zu einem Ziel belegt keinen eingeschlossenen Quellort. Integration innerhalb vorhandener Besitzer prüfen, keinen zweiten Builder-Detour installieren. |
| [SoldierTribeMoves.cs](../../Pathfinding/src/Engine/SoldierTribeMoves.cs) | Kontrollierte temporäre Einzelverbände und Rückkehr zu Gruppen | Befehlssemantik studieren; nicht blind für KI, Arbeiter oder Rettung übernehmen. Bevorzugt ursprünglichen Verband unverändert lassen. |
| [MovementCaseSave.cs](../../Pathfinding/src/Engine/MovementCaseSave.cs), [EngineHost.cs](../../Pathfinding/src/Engine/EngineHost.cs) | Versionierte Bewegungsfälle, Wiederherstellung, prozessweit verwurzelter Host und Missionsoptionen | Spätere Integrationsorte. Ausstehende Rettung ist keine gespeicherte native Pointeransicht und darf beim Laden nicht blind wiederholt werden. |

#### Bestehende Kostenmodelle richtig einordnen

Pathfinding ist nicht vollständig ereignisbasiert. `RouteReplanWatch` verteilt unter Bedarf Prüfungen über ungefähr ein Vierzigstel der IDs je Tick, kurzzeitig ein Achtel, plus eine Kandidatenliste und begrenzte Routenvorschau. Die Gefahrenflucht kann bei aktiven Gefahren alle IDs in ihrem Acht-Tick-Takt prüfen. Anchor-Spielererfassung und Oberflächenaktualisierung besitzen weitere verteilte Prüfungen.

Das sind vorhandene, bedingte Verfahren. Der Rettungsmod soll nicht noch einen unabhängigen permanenten Einheitenscan hinzufügen. Ebenso wenig werden diese bestehenden Verfahren ohne gesonderten Nachweis als Teil der Rettungsarbeit umgebaut. Neue Budgets werden am zusätzlichen Aufwand und am gesamten Simulationstakt gemessen.

#### Öffentliche API ist nicht mit interner Engine gleichzusetzen

- Die wesentlichen Pathfinding-Runtimeklassen und `UnitCommandPathAPI` sind intern. Der eigenständige Testmod verlinkt oder kopiert nicht die gesamte Engine. Notwendige Zusammenarbeit erhält einen kleinen belegten Vertrag; Tests dürfen interne Ergebnisse gezielt per Reflection prüfen.
- [RouteSearchEvents](../../APIShared/src/Pathfinding/Routes/RouteSearchEvents.cs) beobachten zusätzliche gewichtete Suchen teilnehmender Herausgeber. Sie sind kein allgemeiner nativer Fehlerkanal. Ihr Callback-Vertrag verbietet Befehle und Weltmutationen. Der Kontext enthält keine allgemeine stabile Einheitenidentität; alleinige Zuordnung über Endpunkte reicht nicht.
- `RouteTileRules` und deren Journal betreffen veröffentlichte Routenregeln. Das Journal ist kein vollständiges Protokoll aller nativen Geländeänderungen.
- `EnemyBridgeDiagnosticBridge` und `TemporaryGateRouteAcceptanceBridge` sind Diagnose-/Beobachtungsverträge. Aus ihrer Verfügbarkeit folgt keine Erlaubnis für funktionale Rettung darin.
- [PathReachability](../../APIShared/src/Pathfinding/Reachability/PathReachability.cs) ist ein grober spieler-/modusabhängiger Komponentenfilter. `Connected` beweist keinen ausführbaren Detailweg; ein unerreichbares Ziel beweist keinen feststeckenden Start. Die Abfrage verändert native Suchbuchhaltung und ist nicht beliebig nebenwirkungsfrei verschachtelbar.
- Der gelesene API-Katalog führt `PathReachability` ab APIShared `0.54.0`; Pathfinding deklariert derzeit mindestens `0.53.0`. Eine spätere Nutzung muss SDK, Laufzeitverfügbarkeit und Mindestversion tatsächlich klären, statt Quellpräsenz mit installierter API gleichzusetzen.

### 4. Bereits vorhandene Rettungsbelege und ihre Grenzen

Verbindlicher Ausgangsnachweis: [TESTING.md](TESTING.md), [AIV-Observer-Audit.md](AIV-Observer-Audit.md), [UpdateToNewDLL.md](UpdateToNewDLL.md), [zusammengefasste Laufdaten](../../.work/a/rescue-next/001/result.json).

- Automatische KI-AIV-Zugbrückenrettung für zugelassene aktuelle Soldaten-/Reiterbelegungen: echte Bauausführung, No-op-Gegenprobe, Bewegung danach, auch ohne optionale Hauptmods bestanden. Andere Bau-/Editorpfade und Next-only-Eintritte sind dadurch nicht abgenommen.
- Kontrollierte Brücken-, Gruppen-, KI-Mauer-, Besatzungs- und Apfelarbeitszyklen bestanden. Noch kein allgemeiner automatischer Einheiten-/Arbeitsbeobachter.
- Der gesonderte kausale Grabenfall rettete einen natürlich arbeitenden Apple-AI5-Arbeiter ohne Ladung nach tatsächlich abgelehnter Wegveröffentlichung; er verließ den Graben und erreichte sein ursprüngliches Arbeitssegmentziel. Beladener Apple-AI7 und vollständiger Arbeits-/Lieferzyklus nach Grabenrettung bleiben offen.
- Ein natürlich beladener Ochse wurde gerettet und lieferte alle zwölf ursprünglichen Steine ab. Die Einschlussgeometrie dieses Falls ist eine ausdrücklich künstliche Wand unter der real arbeitenden Einheit, kein Beleg für jeden natürlichen Baupfad.
- Die Bergfriedmatrix mit korrigierter Fixes-Testkopie bestand mit eigener Rettung aus/an: jeweils48 stehende und49 begonnene Fälle einschließlich Gegenproben und blockiertem Ausgang. Die tatsächlichen Gebäuderichtungen wurden geprüft; beliebige menschliche CastlePlanner-Startauswahlen sind damit nicht vollständig getestet.
- Ein zusätzlicher belegter Aux-Belegungszustand wurde nicht natürlich erreicht. Seine Bereinigung darf nicht als geprüft ausgegeben oder durch künstliche Unitfelder hergestellt werden.
- Letzter festgehaltener Offline-Stand:1538 Regel-/Zustandsprüfungen,263 native Guard-Prüfungen, sechs Produktions-Receipt-Verträge sowie relevante Managed-/Cecil-Verträge. Das ist keine aktuelle Vollabnahme aller Pathfinding-Funktionen.

#### Fixes-Korrektur getrennt halten

Der Fehler des geprüften Fixes1.30.0 und seine Korrektur sind unabhängig vom allgemeinen Rettungsalgorithmus belegt: ursprüngliche Läufe046/047 mit aufgestellten echten Reitern scheitern; korrigierte Läufe073/074 bestehen auch bei ausgeschalteter eigener Rettung. Der Fehler betrifft den verzögert ausgewerteten veränderlichen Scanner.

**Aktueller Ersatz vom 26.11.2026:** Die frühere `TemporaryFixes`-CLI samt EXE-Projekt, Build-/Selbsttestaufrufen, Runner-DLL-Korrektur und `-OriginalFixes` ist entfernt. Vier getrennte Runtimequellen unter `src/FixesMountedScanner*.cs` erhalten den ursprünglichen Feldtyp und verwenden nach genau einem Originalscan einen unabhängig gebundenen Scanner. Der gemeinsame deklarative Bootstrap-Provider installiert vor Fixes-Aktivierung einen verwurzelten `SafeIlHook`; kein zusätzlicher nativer Besitzer oder Dateipatch entsteht. Die fremde Mounted-Option bleibt maßgeblich, auch wenn unsere allgemeine Rettung OFF ist. Eine spätere BQoL-Masterbindung ist damit nicht entschieden.

Die 66 regulären Offline-Prüfungen verlinken die vier aktiven Quellen und prüfen das explizit ausgewählte APIShared-/Bootstrap-Paar. Lauf 149 mit unveränderter Fixes-1.31-DLL besteht einschließlich OFF/ON-Matrix und 104 tatsächlichen CALL187110-/Originalfortsetzungsprüfungen; Grenzen wie die nicht belegte dritte Aux-Belegung bleiben offen. Nachweise: [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md#nachtrag-149-runtime-scannerkorrektur-ohne-fixes-dateikopie-bestanden), Aufbau: [Audits/FIXES-MOUNTED-RUNTIME.md](Audits/FIXES-MOUNTED-RUNTIME.md).

Nach einem Upstream-Update den unveränderten Mod und die tatsächlichen Aufrufziele erneut prüfen; eigene Rettung OFF schaltet diese Kompatibilitätskorrektur nicht aus. Bekannte skalare Captures werden übersprungen, unbekannte relevante Verträge nicht verändert. Erst bei bestätigter Upstream-Korrektur die vierteilige Insel samt gewöhnlicher Testverdrahtung vollständig entfernen. Keine pauschale Fixes-DLL-Hash-/Versionssperre; vollständige relevante IL-/Helper-/Feldzugriffe und echte installierte Stubs prüfen. Die Native-Hash-Bindung bleibt davon getrennt.

### 5. Zielarchitektur und unveränderliche Sicherheitsregeln

Die folgenden Namen beschreiben geplante Verantwortlichkeiten, keine bereits veröffentlichte API.

    Änderungsquelle oder belegter Bewegungsfehler
        -> kleine Menge betroffener Einheiten
        -> kausaler Nachweis / sicherer Kandidat
        -> begrenzte Nachbeobachtung am sicheren Simulationspunkt
        -> profilspezifische Entscheidung
        -> native Rettung und zulässige Befehlsfortsetzung
        -> physischer Erfolgsnachweis oder ausdrückliches Fehlerergebnis

#### Kandidat und kausaler Nachweis

Ein Kandidat enthält nur benötigte Werte: Sitzung, Unit-ID und Global-ID, Besitzer, Typ/Bewegungsprofil, Quellort und aktuelle/Next-Belegung, Änderungsursache, relevante Befehls-/Arbeitsidentität und zuvor belegte lokale Erreichbarkeit. Keine nativen Pointer, Spans oder unveränderlich angenommenen Objektreferenzen über verzögerte Arbeit hinweg.

Die Ursache braucht eine eigene Bedeutung und Gültigkeit. Eine Cacheinvalidierung oder lokale Suchrevision ist nicht automatisch ein bestätigter Weltumbau. Verschachtelte Änderungen, Teilkonstruktion und mehrere Quellen derselben Änderung müssen koalesziert werden. Bei weiteren Änderungen darf ein alter Nachweis nicht unbemerkt durch eine nachträglich berechnete Erreichbarkeit ersetzt werden.

#### Nachbeobachtung und Grenzen

- Nur konkrete Kandidaten nachbeobachten; kein dauerhaftes Fortschrittsregister aller Einheiten.
- Kandidaten anhand Sitzung und stabiler Identität deduplizieren; neue Ereignisse kontrolliert zusammenführen. Tod, ID-Wiederverwendung, Kartenwechsel und ungültiger Nachweis beenden den Fall.
- Stillstand, Zeitablauf oder wiederholter Suchfehlschlag sind Hinweise, kein Rettungsbeweis. Ein abgelaufenes Budget rechtfertigt keinen Teleport.
- Begrenzte Wiederholungen mit deterministischer Verteilung. Obergrenzen für Kandidaten, Kachel-/Kantenbesuche, Zielsuche und Versuche festlegen und messen. Keine Laufzeit-Millisekunden als MP-entscheidendes Abbruchkriterium.
- Neue Befehle respektieren: keine alte Bewegung, Warteschlange oder Arbeitsabsicht gegen einen inzwischen gültigen neueren Auftrag wiederherstellen.

#### Eingriff und Erfolgsbegriff

- Zuerst belegen, ob die Einheit ihren Standort im tatsächlichen Modus regulär verlassen kann. Boden-Komponenten, Kachelflags und Routenfehler sind jeweils nur Teile dieses Nachweises.
- Die Rettung erweitert keine Bewegungsrechte und erlaubt kein Überspringen unverwandter Mauern, geschlossener Tore oder fremder Sperren. Zielorte müssen für die konkrete Einheit aktuell legal und sicher belegbar sein.
- Bei kausal beobachteter Änderung bevorzugt zuvor erreichbare lokale Ausweichfelder. Kein pauschaler Bergfriedteleport.
- Mehrere Rettungen berücksichtigen bereits belegte beziehungsweise im selben Ablauf reservierte Ziele; Auswahl und Verarbeitungsreihenfolge sind deterministisch.
- Native Wegveröffentlichung ist keine harmlose Probe. Im belegten Grabenfall löschte eine fehlgeschlagene Veröffentlichung die Wegbytes trotz alter Flags/Länge. Deshalb keine Diagnose durch mutierende „Testbefehle“ mit angenommener Rückrollbarkeit.
- Begonnene Schritte niemals durch ungeprüftes Nullsetzen von AI, Substep, Portal-, Ziel- oder Belegungsfeldern abbrechen. Benötigte native Bereinigung erst nach vollständigem Zustandsaudit freigeben.
- Nicht nur Rückgabewert oder Zielkoordinaten prüfen: aktuelle Position, alte/neue Belegung, stabile Identität und tatsächliche weitere Bewegung beziehungsweise Arbeit messen.
- Fehler nach einer bereits ausgeführten Versetzung ausdrücklich von Ablehnung vor jedem Eingriff unterscheiden. Keine atomare Rückrollbarkeit behaupten: fehlgeschlagene Auftragsfortsetzung kann eine bereits bewegte Einheit hinterlassen. Nicht blind an den ungültigen Quellort zurücksetzen oder denselben Rettungsaufruf wiederholen.

#### Ein Besitzer je Eingriff

Der Testmod und Pathfinding dürfen niemals dieselben nativen Spannen konkurrierend belegen. Vorhandene Pathfinding- und APIShared-Besitzer bleiben maßgeblich. Eine notwendige öffentliche Beobachtung wird dort additiv und fehlerisoliert angeboten, nicht über kopierte Hooks oder funktionale Nutzung einer Diagnose-API.

Originale laufen genau einmal. Beobachtungsfehler dürfen weder Vanilla noch andere Teilnehmer verhindern. Funktionale Eingriffe erfolgen erst an einem dafür auditierten sicheren Punkt. Fixes bleibt überall aktiv, wo unser System den Fall nicht vollständig übernimmt. Bloße Kandidateneignung vor dem Bau ist kein Beleg, dass die spätere eigene Rettung gelingt.

### 6. Arbeitspakete mit Abnahmekriterien

#### P1 — Erzeuger- und Vertragsmatrix

- [x] Alle relevanten Quellen aufführen: KI-AIV-Brücke, KI-Mauer-/Gebäudebau, menschlicher Bau einschließlich Spiegel-/Serienpfaden, Editorbau/-gelände, Grabenfortschritt und tatsächlicher Abschluss, Rückbau/Zerstörung sowie externe Bau-APIs. Siehe vier Teilaudits.
- [x] Je Quelle native/verwaltete Erzeuger, effektive Parameter, Footprint, betroffene Nachbarschaft, Thread, Reihenfolge, Abbrüche, Ausnahmen und stabilen Abschluss bis zur späteren Bewegung auditieren; nicht allgemein rettbare Zustände mit konkretem Vertrag abgrenzen.
- [x] Vorhandene Extender-Ereignisse und APIShared-/Pathfinding-Hookbesitzer zuordnen. Post-only-Kachelinvalidierung ausdrücklich von Vorher-/Nachher-Transaktion getrennt.
- [x] Relevante Fixes-Reaktionen und deren Reihenfolge geprüft; ursprüngliche Prävention und Fixtureisolation getrennt, Keep-Reiterrettung bleibt eigenständig.
- [x] Additiven funktionalen Brokervertrag entworfen: Quelle/Invocation, äußerer Mirrorrahmen, effektive Originaldaten, einmaliger Abschluss, Originalfehler, Thread/Lebensdauer und vollständige Callback-/Diagnosefehlerisolation. Siehe `Audits/AUDIT.md` Abschnitt3; noch keine APIänderung.
- [x] Native-/SDK-/Extender-Provenienz und offline relevante Einstiegsspannen einschließlich Dig/Fill-Innenreferenzen belegt. Keine neue Hookveröffentlichung.
- [ ] F1 gezielt korrigieren; danach die drei Fehlergegenbeispiele als erfolgreiche Isolation und Originalerhaltung prüfen.
- [ ] Tatsächliche Live-Spannen erst beim später autorisierten Start prüfen. Die Offline-PE-Analyse ersetzt keinen Live-Fremdpatchnachweis; keine neuen RVA-Adapter ungeprüft aus dem Plan ableiten.

**Abgenommen wenn:** Für jeden anzubindenden Pfad ist belegt, wie betroffene Einheiten vor der Änderung erfasst und nach dem vollständigen Umbau geprüft werden. Nicht belegte Pfade stehen sichtbar als offen in der Abdeckungsmatrix. Ein getesteter Pfad darf dabei bereits inkrementell weitergehen.

#### P2 — Kandidatenkern und begrenzte Verarbeitung

- [x] Bestehende Testmod-Snapshots/Ergebnisse so strukturieren, dass Erkennung, Nachweis, Planung, native Ausführung und Beobachtung getrennte Fehlerpfade besitzen.
- [x] Kandidatenliste mit Identität, Ursache, Sitzung, Deduplizierung, Ablauf und begrenzten Wiedervorlagen implementieren. Vorherdaten nur so lange wie fachlich nötig halten.
- [x] Vorhandene Simulationstaktung benutzen; bei leerer Arbeit sofort zurückkehren. Nachbeobachtung vorhandener Kandidaten ist erlaubt, permanentes Scannen aller Einheiten für den neuen Dienst nicht.
- [x] Bereits vorhandene Pathfinding-Revisions- und Auftragsmuster nach Vertragsprüfung anpassen. Keine Engine-Quellkopie in den Testmod aufnehmen.
- [x] Reine Verhaltenstests: doppelte/nested Ereignisse, ID-Wiederverwendung, neue Befehle, mehrere Umbauten, Sitzungstausch, Budgeterschöpfung, fehlender Zielort und deterministische Reihenfolge.

**Abgenommen wenn:** Ohne Ereignisse keine Einheiten-/Wegprüfungen; ein Verdachtsfall verursacht begrenzte Arbeit; abgelaufene oder ersetzte Fälle können keine Mutation mehr auslösen.

#### P3 — Weitere automatische Bau-/Geländepfade

- [x] Vorhandenen echten KI-AIV-Zugbrückenpilot und dessen No-op-/Off-/On-Belege als Ausgangspunkt dokumentiert.
- [ ] Den Pilot in den gemeinsamen Kandidatenpfad überführen, ohne ursprüngliche Bauaktionen zu unterdrücken oder die bestehende Gegenprobe zu verlieren.
- [ ] In belegter Reihenfolge weitere KI-Bauten und danach Menschen-/Editorpfade anbinden. Keine pauschale Callback-Ausführung je Footprintkachel.
- [x] Tatsächlichen Grabenabschluss separat anbinden; ein Eintrag/Schreibaufruf allein gilt nicht als fertiger unpassierbarer Graben.
- [ ] Teilbau, abgelehnte Platzierung, unverändertes Gelände, Rückbau und wiederholte Bauversuche als Gegenproben aufnehmen.
- [x] Bounded Tile-Occupancy-Discovery für aktuelle Betroffene; Next-only-Kandidaten erst nach P5 belastbar ergänzen. Keine versprochene vollständige Erkennung durch die bisherige Current-Kette.

**Abgenommen wenn:** Echte normale Bauvorgänge lösen die Erkennung aus, ohne dass die Fixture ausdrücklich `BeginMutation` oder die Rettung aufrufen muss. Kontrollierte native Bauaufrufe und autonome KI-Abläufe werden im Nachweis getrennt.

#### P4 — Soldaten, Besitzer und KI-Gruppen

- [ ] Stehende gewöhnliche Soldaten zuerst über neue automatische Quellen abnehmen: Einzelunit, mehrere Betroffene, tatsächlicher Gruppenführer und anderes Mitglied.
- [ ] KI-Gruppen mit regulärem Marsch-, Angriffs- und gegebenenfalls Patrouillenauftrag prüfen; gesunde Mitglieder müssen weiter funktionieren. Keine universelle Gruppenblockade voraussetzen.
- [ ] Bestehende manuelle Admission-Logik erhalten. Ihre KI-Sperre nicht als beiläufige Rettungskorrektur entfernen. Etwaige allgemeine KI-Gruppenkorrektur als eigenen belegten Fall behandeln.
- [ ] Ursprünglichen Verband bevorzugt beibehalten; fortgesetzter Auftrag darf keine neuere Order, Formation oder Shift-Warteschlange zerstören. `SoldierTribeMoves` nicht ungeprüft zur universellen Fortsetzung machen.
- [ ] Besitzerpolitik festhalten: bisheriger automatischer Pilot verlangt Besitzer = Bauherr. Alliierte, neutrale und feindliche Betroffene sind gesonderte Fälle. Ein globaler Rettungsschalter darf nicht heimlich nur dem lokalen Menschen helfen.
- [ ] Normales Warten, Kampf, befohlenes Halten und unerreichbares Ziel ohne eingeschlossenen Start als zwingende Negativtests.

**Abgenommen wenn:** Physische Rettung und tatsächliche sinnvolle Auftragsfortsetzung bei Mensch und KI belegt sind; das übrige Gruppenverhalten und nicht betroffene Einheiten bleiben erhalten.

#### P5 — Begonnene Schritte und Sonderflächen

- [ ] Vollständige Writer-/Consumer-Kette für Current, Next, Interpolation, Verbindungs-/Portalzustand und Belegungslisten je freizugebender Bewegungsfamilie auditieren.
- [ ] Beide Hauptfälle reproduzieren: aktueller Standort wird ungültig; nur das bereits angenommene nächste Feld wird ungültig. Auch Änderung um die Mittelpunktübernahme herum prüfen.
- [ ] Sicheres Warten bis zu einer zulässigen Grenze und eine gegebenenfalls nötige native Bereinigung getrennt qualifizieren. Keine unbewiesene Rückkehr zu Idle erzwingen.
- [ ] Reiter/Large-Profil, Fußsoldaten, Assassinen sowie erhöhte und durch Gebäude verbundene Flächen getrennt abnehmen.
- [ ] Legales Klettern, Treppen, Tore, geschlossene/eroberte Tore und Brückenstellungen gegen falsche Rettungen prüfen. Tatsächliche Richtung/Höhe lesen, einschließlich CastlePlanner-Fällen.
- [ ] Natürliche Current-/Next-Belegung und alte/neue Listen prüfen. Zusätzliche Aux-Fälle nur bei belegtem natürlichem Erzeuger abnehmen, sonst offen ausweisen.

**Abgenommen wenn:** Der ursprüngliche „Einheit läuft während des Baus über das Feld“-Fehler über automatische Quellen zuverlässig behoben wird; weder doppelte Belegung noch zweite Versetzung oder Wiederaufnahme des alten ungültigen Schritts entstehen.

#### P6 — Arbeiter, Grabenmodus und Besatzungen

- [ ] Die getesteten Apple-/Ox-/Besatzungsabläufe in dieselben automatischen Quellen integrieren; erfolgreiche direkte Fixture-Aufrufe nicht als automatische Abnahme zählen.
- [ ] Apple-AI5 ohne Ladung als Regression erhalten; beladenen Apple-AI7-Grabenfall und vollständigen anschließenden Ernte-/Lieferzyklus hinzufügen.
- [ ] Weitere Arbeiterfamilien anhand ihrer tatsächlichen Jobzustände qualifizieren. Erzwungenes Wegende darf nicht als aus der Ferne erreichtes Arbeitsziel wirken; insbesondere keine unkontrollierte Übernahme von Soldaten-Stoppregeln.
- [ ] Natürlich beladenen Ochsen erneut über echte Änderung erfassen und vollständige bestehende Ladung am tatsächlichen Lager nachweisen. Keine künstlichen Waren, AI-Zustände oder überlebenden Testgebäude voraussetzen.
- [ ] Gerät mit Besatzung, ursprüngliche Ingenieure, unmittelbares Absitzen und spätere Eigenbewegung getrennt prüfen. Verdeckte/montierte Ingenieure nicht als unabhängige Rettungskandidaten versetzen.
- [ ] Native Grabenbewegung zulassen, wenn sie legal funktioniert. Gesonderte kausale Grabenrettung nur bei vollständig belegtem Modus-/Ausgangs-/Arbeitsvertrag freigeben.

**Abgenommen wenn:** Für jede freigegebene Familie der konkrete Job beziehungsweise die Gerätefunktion nach echter Rettung weiterläuft und Identität, Ladung, Arbeitsplatz und Besatzung erhalten bleiben. Nicht qualifizierte Zustände bleiben sichtbar außerhalb der Freigabe.

#### P7 — Bewegungsfehler und fehlende Bauereignisse

- [ ] Abdeckung von `OnUnitMoveHere`, vorhandenen Builder-/Recovery-Besitzern und tatsächlichen Schrittablehnungen vollständig unterscheiden und auditieren. Globale Auftragsbeobachtung ist kein vollständiger Bewegungssensor.
- [ ] Verlässliche Unit-ID/Global-ID, tatsächlichen Besitzer, effektiven Ziel-/Moduskontext und nativen Ausgang einem Aufruf zuordnen. Endpunktgleichheit oder aktuelle UI-Auswahl sind keine Identität.
- [x] Skipped Original, veränderte Pre-Argumente, fehlendes Post bei Ausnahme und verschachtelte Aufrufe abnehmen. Keine neue instabile LIFO-Kopie neben bestehenden Besitzern.
- [ ] Aus Fehlermeldungen nur Kandidaten erzeugen. Keine Weltmutation in `RouteSearchEvents` oder in einem laufenden fremden Suchkontext.
- [ ] Ohne Vorherdaten erst einen eigenständigen Nachweis für aktuell fehlende legale Ausgänge und einen sicheren Zielort entwickeln. Nicht einfach den kausalen alten-Bank-Nachweis erfinden.
- [ ] Häufig wiederholte Fehler deduplizieren und zeitlich begrenzen. Vor einem verwalteten Callback an einem sehr heißen nativen Pfad dessen Eintrittskosten und ein natives Bedarfs-/Fehlergate prüfen.

**Abgenommen wenn:** Ein Fehler außerhalb der bereits beobachteten Bauquellen erkannt werden kann, ohne dass ein unerreichbares Ziel, Warten oder fremde Routenregeln unerlaubte Teleports auslösen. Ein nicht identifizierbarer Fehler bleibt ohne Eingriff.

#### P8 — Sitzungen, Save-Laden, Editor und MP

- [ ] Zentrale Missions-/Editor-Lifecycle-Verträge verwenden; aktive Fälle auf Ende, Ersetzung und Fehlstart ungültig machen. Startup-Cleanup darf die Runtime nicht zerstören.
- [ ] Kartenwechsel, Neustart, Save-Laden und Laden desselben Saves mit wiederkehrenden IDs prüfen. Native Pointer/alte Beweissnapshots nicht über die Sitzung retten.
- [ ] Bewusst entscheiden, welche eigenen Warte-/Fortsetzungsfälle gespeichert werden müssen. Bestehendes `MovementCaseSave`-Schema kompatibel erweitern; keine formatlosen zusätzlichen Serialisierer.
- [ ] Für schon defekte alte Saves optional eine getrennte, einmalige verteilte Bestandsaufnahme oder angeforderte Prüfung entwickeln. Untätige Altfälle ohne Ereignis werden sonst nicht gefunden. Fehlende Vorherdaten und sichere Rückkehrseite bleiben ausdrücklich ein eigener Nachweis.
- [ ] Normalen Editorbau/-gelände und spätere Simulation testen; Editor-Created und Editor-Loaded nicht mit Skirmishstart gleichsetzen.
- [ ] Vor MP-Aktivierung Autorität festlegen: identische native Simulationsereignisse und deterministische Entscheidung oder belegter synchroner Chore-Weg. Keine Mutation nur auf dem Host oder bei jedem Peer zusätzlich zur empfangenen Mutation.
- [ ] Gleiche Einstellungen, Kandidatenordnung, Tick-/Arbeitsbudgets und Zielwahl auf Host/Client prüfen. Keine Wandzeit, Thread-Reihenfolge, lokale UI-Auswahl oder lokale Diagnose-/Suchrevision als gemeinsame Spielentscheidung.
- [ ] Reales Host-/Client-Spiel mit Bau, mehreren gleichzeitigen Rettungen, KI-Gruppen, Arbeitern und anschließendem Save/Laden abnehmen. Bis dahin bleibt der jetzige MP-Ausschluss aktiv.

**Abgenommen wenn:** Keine veraltete/doppelte Rettung über Lifecyclegrenzen und identisches Verhalten in den ausdrücklich freigegebenen Modi belegt ist. Ein lokaler Zweispieler-Ersatz ist keine echte MP-Abnahme.

#### P9 — Last, Fehlertoleranz und Kompatibilität

- [ ] Auf großer Karte mit annähernd10.000 Einheiten Leerlauf, wenige Änderungen und Massenbau/gleichzeitige Betroffene vergleichen. Keine Pflicht, hierfür alle Kacheln pro Tick zu betrachten.
- [ ] Zusatzkosten messen: native→verwaltete Übergänge, Kandidaten/Versuche, Kanten-/Kachelbesuche, Queue-Spitze, Allokationen, gesamter Sim-Tick und Lags. Durchschnitt allein reicht nicht.
- [ ] Abwechselnde Vergleichsläufe, gleiche Szenarien und zusätzliche Kosten zu vorhandener Pathfinding-Taktung ausweisen. Keine unbelegte feste Millisekundengarantie in Einstellungen oder Dokumentation.
- [ ] Journalausfall, überlaufende Kandidatenmenge, ungültige Kette, fehlende Capability und Callbackfehler prüfen. Begrenztes Aufschieben/Verwerfen mit sichtbarem Ergebnis; nie stumme Rettung aufgrund verlorener Daten.
- [ ] Allein mit deklarierten Pflichtabhängigkeiten sowie mit Fixes, Pathfinding, BugfixesAndQoL und CastlePlanner testen. Pro Stelle eindeutige Ownership und Live-Spannen vor Veröffentlichung sowie bei Missions-/Editorstart prüfen.
- [ ] Relevante fremde Änderungen ablehnen; harmlose Versions-/DLL-Änderungen ohne Vertragsänderung akzeptieren. Originale und unabhängige Features bleiben aktiv; keine fremden Einstellungen umschreiben.

**Abgenommen wenn:** Keine zusätzliche permanente Vollprüfung aller Einheiten entsteht, Lastspitzen begrenzt sind und Ablehnung/Überlast weder Vanilla noch bestehende Features beschädigen. Release-Diagnose ist klein; temporäre Entwicklungsdiagnose läuft ausschließlich über `Shared.DevDiag`.

#### P10 — Überführung nach Pathfinding

- [ ] Abdeckungsmatrix und offene Fälle gemeinsam bewerten. Einen begrenzten MVP nicht als vollständig allgemeine Rettung umbenennen.
- [ ] Rettungskern, native Ausführung, modinterne Policy und nötige allgemeine Beobachtungsschnittstellen mit eindeutigen Besitzern zuordnen. APIShared bleibt unabhängig von Pathfinding-Interna und fremden GUIDs nutzbar.
- [ ] Neue eigene Rettungsoption in Pathfinding konsistent übernehmen. Master-Aus deaktiviert sie; eine ausgeschaltete manuelle Verbesserung, schnelle Einzelsuche oder Gefahrenflucht darf nicht unbeabsichtigt die allgemeine Rettung sperren. Tatsächliche technische Voraussetzungen ausdrücklich in Vertrag/UI darstellen.
- [ ] Globaler Host-/Missionssnapshot, Presets, Locales und Modusfreigaben korrekt einbinden. Keine zusätzliche Einstellung speziell für die temporäre Fixes-Korrektur einführen.
- [ ] Den produktiven Runtimebesitzer genau einmal installieren. Beim Wechsel in einem neuen Spielprozess die eigenständige Runtime des Testmods ablösen; keine parallel aktiven alten Hooks oder ungefragte zweite Fallbackimplementierung behalten.
- [ ] Fixtures und Automationsszenarien bleiben außerhalb des Hauptmods. Der Testmod prüft danach den tatsächlichen Pathfinding-Code über geeignete öffentliche Grenzen beziehungsweise gezielte Test-Reflection.
- [ ] Bestehende Pathfinding-Tests nach tatsächlicher Betroffenheit pflegen und ausführen: manuelle Admission, native Recovery/Reject, Anchor-Integration und Veto, Oberflächen, Warte-/Save-Fälle, Formationsautorität, Topologieveröffentlichung und Gate-Kompatibilität.
- [ ] Anschließend dieselben Rettungsszenarien gegen den integrierten Hauptmod wiederholen; Erfolg des vorherigen eigenständigen Testmods ersetzt diese Prüfung nicht.
- [ ] Erst nach Fertigstellung Versionen konsistent ändern. README-Ergänzung gesondert abstimmen. Paketinstallation, Commit und Veröffentlichung richten sich nach dem dann ausdrücklichen Auftrag.

**Abgenommen wenn:** Pathfinding enthält genau eine getestete Rettungsruntime, der Testmod nur die Tests dafür, und die freigegebenen Modi/Familien/Quellen stimmen mit der Dokumentation überein.

### 7. Verbindliche Abnahmematrix

Für jede relevante Kombination einen Eintrag mit Status, Quelle, tatsächlichem Zustand, Trigger, Originalverhalten, eigenem Verhalten und Beleg führen. Keine bloße Aufzählung potenziell vorhandener Tests.

| Achse | Mindestfälle |
|---|---|
| Ursache | AIV-Brücke, weitere KI-Bauten, Menschen-/Editorbau, Grabenabschluss, Rückbau, Bewegungsfehler, Altsave getrennt |
| Bewegung | Stehend, aktuelle Kachel betroffen, nur Next betroffen, mehrere natürliche Teilschritte vor/nach Mittelpunkt, legale Sonderbewegung |
| Einheit | Fußsoldat, Assassine, vier geprüfte Reitertypen, Arbeiter ohne/mit Ware, Ochse, Gerät samt Ingenieuren; weitere Familien einzeln |
| Befehl | Kein Auftrag, Einzelmove, Gruppenführer/-mitglied, Angriff/Patrouille, Formation, Queue, neuerer Auftrag während Beobachtung, Arbeitsplatz |
| Besitzer | Mensch, KI, mehrere KI; Alliierte/Neutral/Feind nur nach festgelegter Policy |
| Geometrie | Freie Ausweichbank, mehrere Kandidaten, alle Ziele blockiert, enger Einschluss, Höhenwechsel, gültiger Kletterausgang, Tore/Brückenstellungen, tatsächliche Keep-Rotation |
| Zeit / Modi | Sofortiger Bauabschluss, verschachtelte Änderung, mehrere Betroffene, Kartenwechsel, Save/Reload, Editor, echter MP |
| Kompatibilität | Pflichtabhängigkeiten allein, ursprüngliches und gezielt korrigiertes Fixes getrennt, übrige Hauptmods, fremder Hook, relevante/unabhängige Updates |

Ein positiver Test umfasst: dieselbe globale Identität und Gesundheit, korrekte Belegung und keine zweite Versetzung, gültigen Zielort und tatsächliche Bewegung/Arbeit danach. Bei Arbeit zusätzlich reale Waren-/Arbeitsplatzbelege; bei Gruppen Befehls- und Mitgliedschaftsbelege. Negative Tests umfassen No-op, legalen Ausgang, nicht eingeschlossenen Start, unerreichbares Ziel, fehlenden Zielort und unbekannten Zustand.

### 8. Offene Entscheidungen, ohne stille Vorannahmen

| ID | Entscheidung | Bis zur Klärung |
|---|---|---|
| D1 | Besitzerpolitik für Alliierte, Neutral und Feinde bei fremdem Bau | Bisherigen Besitzer=Bauherr-Pilot nicht stillschweigend erweitern |
| D2 | Automatische Reparatur alter Saves oder ausdrücklich angeforderte Prüfung | Neue Ereignisse behandeln; keine versprochene rückwirkende Rettung |
| D3 | Erledigt: kausale Kandidaten, Geometrie und eigene Fortsetzungen getrennt speichern | Implementierte strikte binäre Codecs; Originalalter/Versuche bleiben erhalten, transiente Phasen blockieren atomare Saves |
| D4 | Erledigt: WorldMutationEvents und finaler UnitMoveEvents-Broker | APIShared0.74.0 aus isolierter Arbeitskopie; bestehende gemeinsame Originalbesitzer, keine funktionale Diagnose-API |
| D5 | MP-Ausführung und Autorität | Bestehender Testmod bleibt im echten MP gesperrt |
| D6 | Umfang erster produktiver Freigabe und Standardwert der Rettungsoption | Testmod bleibt explizit experimentell und standardmäßig aus |

Technische Details können innerhalb belegter Verträge entschieden werden. Mehrere wesentlich verschiedene Verhaltensoptionen werden vor ihrer Umsetzung konkret mit Folgen und Nachweisen zur Entscheidung vorgelegt; fehlende Antwort ist keine Freigabe.

### 9. Änderungs- und Abnahmeprotokoll

| Datum | Paket | Ergebnis / nächste Arbeit | Nachweise |
|---|---|---|---|
| 17.11.2026 | P0 / Planerstellung | Aktuellen Pathfinding-Quellstand, menschliche/KI-Grenzen, vorhandene Taktung, Hookbesitzer und öffentliche Vertragsgrenzen eingeordnet. Zielmigration ausdrücklich Pathfinding. Nächster Schritt P1. Keine neuen Runtimeänderungen, Builds oder Spieltests für diesen Plan. | Quellen in Abschnitt3; vorhandene Rettungsnachweise in Abschnitt4 |
| 25.11.2026 | Offline-Abnahme des vorhandenen Stands / P1 teilweise | Testmod, kompletter Pathfinding-Offline-Build und ausgewählte API-Verträge PASS. Testmod-Tooltip-Stil korrigiert; Nachprüfung PASS. Sandbox-Testhost-Startfehler durch unveränderte erfolgreiche Läufe außerhalb abgegrenzt. Keine Installation/Spieltests; keine neue allgemeine Rettungsquelle implementiert. Nächster Schritt: relevante Baseline-Provenienz validieren und P1 für die nächste Quelle vollständig schließen. | [OFFLINE-CHECKS.md](OFFLINE-CHECKS.md), `.work/a/rescue-offline/001/result.json` |
| 25.11.2026 | Vollständiger statischer Featureaudit | Bau-/Gelände-/Bewegungs-/Job-/Lifecycleketten und sichere Ausschlüsse geschlossen; alle20 Wirtschaftsrollen und besondere Tunnel-/Climb-/Portalzustände ergänzt. Neue native Bytebelege unabhängig gegengeprüft. F1 durch3 Gegenbeispiele+Kontrolle am Produktionsdispatch reproduziert; keine Runtimekorrektur. Keine Installation/Spieltests. Nächster Schritt F1-Korrektur, dann P2/Brokerimplementierung. | [Audits/AUDIT.md](Audits/AUDIT.md), [F1](Audits/AUDIT-FINDINGS.md), `.work/a/rescue-audit/001/` |

| 25.11.2026 | Offlineimplementierung P2–P9 / P7-Abschluss | Kandidatenkern,15 gemeinsame Bau-/Geländequellen, gewöhnliche Current/Next-Zustände,20 Workerrollen und Command-Reisen, Idle-Moat, Lebens-/Vegetationsbindung, eigene Fortsetzungen, kausaler Replan und binäre Persistenz integriert. Überschneidende Folgegeometrie und fehlgeschlagene Compound-Captures als Regression korrigiert. Vollständiger Testmodbuild64101 Prüfungen plus Fixes-Verträge; isoliertes SDK1787 Tests und Buildproof bestanden. Keine Installation/Spielstarts/Produktionsmigration; angeforderte lokale Sicherungscommits ausgeführt. | [IMPLEMENTATION.md](IMPLEMENTATION.md), [API-HANDOFF.md](API-HANDOFF.md), `.work/a/rescue-impl/001/offline-completion.json`, [INGAME-PENDING.md](INGAME-PENDING.md) |

Für spätere Einträge mindestens Datum, tatsächlichen Quell-/Buildstand, geänderte Dateien/Verträge, bestandene und fehlgeschlagene Prüfungen, verbleibende Grenzen sowie den nächsten konkreten Schritt ergänzen. Bei neuen Erkenntnissen diesen Plan anpassen; erledigte technische Annahmen nicht als dauerhafte Sperren konservieren.

## N7: unabhängige Übernahmevorbereitung

Nach Abschluss von N6 wurden31 Kernquellen nach `BugfixesAndQoL/src/TrappedUnitRescue` übernommen.27 enthalten nur die Namespaceänderung. Adapter: injizierter Fixes-Owner, neuer Save-Identifier `BugfixesAndQoLTrappedUnitRescue`, ausschließlich vorhandener Lord.HomeTeleportGuard als19BA40-Besitzer, Entfernung der alten Vollzensus-MutationScope-Testinsel. Reine Shared.DevDiag-Capturemessung bleibt nur im Diagnosebuild. Der bisherige Testmod bleibt bis N8 als validierter Prototyp erhalten; die neue Runtime ist noch nicht aktiviert.

BQoL-Projekt und beide SDK-Mindestmetadaten sind auf die tatsächlichen0.74-Verträge vorbereitet; Modversion unverändert. Der erste Compile deckte im bestehenden AreaConstruction-Rollback einen vom aktuellen Compiler als void gebundenen Reverse-Aufruf auf; die explizite LINQ-Enumeration erhält die beabsichtigte rückwärtige Rollbackreihenfolge. Danach Hauptassembly-Compile bestanden. Neues `BugfixesAndQoL/tests/TrappedUnitRescue.Tests` verlinkt30 echte Produktionsquellen und25 vorhandene reineFixtures; alle bestanden, keine alte Runtimekopie. Host und DevDiag sind aus diesem reinen Kernelprojekt ausgeschlossen. Die neue Save-ID-Registrierung benötigt weiterhin die integrierte Host-/Spielabnahme.

Nachweise: `.work/a/rescue-sp/001/n7-core-compile.log`, `n7-kernel-build.log`, `n7-kernel-tests.log`. Kein Installieren/Veröffentlichen/Commit, keine endgültige Produktvoreinstellung, noch keine Scanner-Insel-Migration. Die Fragen zu Default und BQoL-MasterOFF bleiben offen; die davon unabhängige Übernahme wird vorbereitet.

### N7-Hostvorbereitung abgeschlossen, Aktivierung bleibt offen

Der interne normale C#-Host `TrappedUnitRescueFeature` ist vorbereitet und kompiliert: injizierte Owner-/Enable-/Logfunktionen, eine Generalruntime, gemeinsame Broker und neues Save-Register als letzter fallibler Initialisierungsschritt, Ready-gesteuerte Teilinitialisierung, bestehende SP-/Editorgrenzen und stufenbezogene Budgets. BeginSession/EndSession/ApplySettings werden später vom vorhandenen BQoL-Bootstrap aufgerufen; keine zusätzliche Lifecyclequelle, kein Fixture-Schalter, keine Produktvoreinstellung. Fehler erscheinen als Error mit Millisekundenzeit. Die neue Hostdatei ist aus den reinen Kerneltests ausdrücklich ausgeschlossen.

Die vorbereitete Hauptassembly kompiliert normal und besteht NoDevGate. Das ersetzt keine aktivierte Host-/Save-ID-/Einstellungs-/Scanner-/Spielabnahme. Nachweise: `n7-host-compile.log`, `n7-kernel-tests.log`, `n7-boundaries.log` im vorhandenen Evidenzordner. Der ursprüngliche Testmod wird erst nach dieser integrierten Abnahme abgelöst. Die beiden gestellten Produktpräferenzen bleiben erforderlich; keine automatische Auswahl aufgrund verstrichener Zeit.
