# Gebäude-Annäherung: Suchspieler und bewegender Spieler

Audit: 02.10.2026. Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Alle Adressen sind RVAs ausschließlich dieses Builds. Vertrauen: hoch für die
unten beschriebenen direkten nativen Datenflüsse; keine Zuordnung eines
konkreten Runtime-Aufrufs allein anhand gleicher Zahlenwerte.

Installierter Script Extender: 2.12.0.0, SHA-256
`DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`.
Lokaler Fork-Commit `f8d51730fcb54b25af43d3c9348d57db058e077f`,
Tree `657af449e1397c58e6c5ec054977d83198b68e66`; diese Provenienz stimmt
mit DATABASE_INFO.json überein. Installierte Feldtypen/-offsets wurden separat
über die echte SHCDESE.dll geprüft. Keine publicized Assembly verwendet.

## Vertrag

`0xDA020(pathManager, tribeId, buildingId, requestedResults, sourcePcl,
nativeQueryPlayer)` besitzt zwei direkte native Aufrufer. Das sechste Argument
ist kein einheitlicher Angreiferspieler und keine Bewegungsklasse.

| Aufrufweg | Quelle des sechsten Arguments | Tatsächlich bewegender Kontext |
| --- | --- | --- |
| Zielplaner `0x30620`, Call `0x30A33` | ausgewählter gegnerischer Spieler; außerhalb des Editors Ersatzwert 1 bei gespeichertem null | übergebener Tribe und dessen Anführer; unabhängig vom ausgewählten Gegner |
| Befehlsverteiler `0x11E960`, Call `0x11FF9A`, Befehle 9 und 38 | vorzeichenbehaftetes 16-Bit-Kontrollfeld des Anführers | derselbe Anführer/Tribe; Kontrollspieler und Tribe-Besitzer dürfen nicht ungeprüft gleichgesetzt werden |
| derselbe Gebäudezweig, Befehl 36 | null | derselbe bewegende Tribe; null ist eine absichtliche Vanilla-Abfragerolle |

Die Dispatchtabelle bei `0x121D4C` führt 9, 36 und 38 zum gemeinsamen
Gebäudehandler `0x11FC25`. Andere Befehle sind nicht pauschal Gebäudeabfragen:
Die Nullzuweisung am gemeinsamen Aufruf gilt für die dort ankommenden Befehle
außer 9/38. Sie ist kein Beleg, dass jeder andere Befehl DA020 aufruft.

## Vorgelagerte Zielplanung und Feldschreiber

`0x2A720(aiRoot, ownPlayer, tribeId)` sucht unter Spielern 1..8 einen lebenden,
nicht verbündeten Gegner. Die AIC-Auswahlzweige vergleichen Entfernung bzw.
Spielerwerte; ohne Treffer folgt die nächste geeignete Gegnerauswahl. Bleibt
auch sie erfolglos, schreibt der normale Spielpfad 1; im Editor kehrt er ohne
diesen Ersatz zurück. Die beiden direkten Schreiber bei `0x2A9C5` und
`0x2A9E6` schreiben ein WORD an
`module+0x7CC6D6A+tribeId*0x688`.

Der Tribe-Manager beginnt bei `module+0x7CC6720`, sein Array bei +0x2A.
Damit ist managerrelativ +0x64A gleich strukturrelativ +0x620. Die installierte
Interop benennt dort das UINT32-Feld `N00000580`; Vanilla liest/schreibt dessen
unteres WORD. Das benannte Feld `r_AttackTargetOwnerPlayerId` liegt hingegen
bei +0x61C und ist **nicht** die Quelle dieses Arguments.

`0x30620` liest das WORD bei `0x30682` vorzeichenbehaftet nach EBP. Es verwendet
denselben Wert zur Auswahl des gegnerischen Gebäudes über `0xCE570` und für
gegnerische Rückfallkoordinaten. Im direkten Gebäude-Annäherungszweig der
Speicherrollen 182/185 kopiert `0x30A09` EBP nach `[rsp+0x28]`; `0x30A33`
ruft DA020 auf. Bei fehlendem Anführer wird dieser Suchaufruf umgangen.
Eine fehlende geeignete Angriffskachel führt zu den gegnerischen
Rückfallkoordinaten; anschließend entscheidet `0x3E650` über Bewegung bzw.
`0x3E9A0`. Die anderen Planungszweige können direkt Gebäudeauftrag 9 oder
die vorgelagerte Graben-/Sonderzielwahl über `0xF09A0` auslösen. Ein späterer
Gebäudeauftrag beweist deshalb nicht, dass der direkte Planeraufruf lief.

## Anführer und ID-Basis

`0x11FF47` liest den Anführer aus managerrelativ +0x5A, also
`GameTribe.r_LeaderUnitId` bei +0x30. `0x11FF4D` multipliziert die unveränderte
1-basierte Game-ID mit Unitstride 0x490. Die Unitmanagerbasis hat bei +0x65C
den vorgeschalteten Sentinel/LastOrderedUnit. Die öffentliche Unitspan beginnt
erst hinter diesem Record und `TryGetUnitById(id)` verwendet dort `id-1`:

`manager+0x65C+id*0x490 == publicSpan+(id-1)*0x490`.

Die native Kontrollabfrage bei `0x11FF73` ist `MOVSX EAX,WORD [unitManager+
leaderId*0x490+0x6EE]`, entsprechend Unitpointer +0x92. In der installierten
Interop ist `r_ControllableForPlayerId` bei +0x92 ein **BYTE**, das nächste
unbenannte BYTE liegt bei +0x93. Beide zusammen bilden das native signed WORD.
Das obere Byte darf für eine zukünftige exakte Diagnose nicht stillschweigend
als null angenommen werden. `0x1185A0` aktualisiert beim Tribe-Kontrollwechsel
Tribe-Besitzer und dieses gesamte Unit-WORD für geeignete Mitglieder. Seine
Existenz belegt keinen unveränderlichen Gleichstand aller Einheiten/Tribes.

`0x11E986` setzt ESI auf null. Befehle 9/38 lesen das WORD, Befehl 36 behält
null; `0x11FF89` veröffentlicht EAX als sechstes Argument. Kein ID-Off-by-one
ist in diesem Zugriff nachgewiesen. Frühere Notizen mit „ushort“ werden durch
den hier bestätigten **signed WORD → int**-Vertrag präzisiert.

## Nachgelagerte Verwendung und terminale Folgen

DA020 reicht das sechste Argument unverändert als Spielerargument an E2610
weiter, sowohl im direkten Gebäudering als auch im erweiterten Suchring;
der Regionsmodus ist jeweils 0. E2610 akzeptiert gleiche PCL unmittelbar,
verwirft unterschiedliche Null-PCL und prüft sonst aktive Verbindungsrecords
mit Modus, Besitzer/Allianz und Eroberungswert. Spieler null überspringt in
dieser Recordauswahl die Spieler-/Allianzbedingung absichtlich. Der anschließende
Graphdurchlauf kann einen nächsten PCL liefern oder mit null scheitern.
Sein zweiter Durchlauf und die Scratch-/Ergebnisflags ändern nicht die Herkunft
des Spielerarguments.

DA020 besitzt davor eine eigene Same-PCL-Akzeptanz. Außerdem kann bei geeignetem
Tribeprofil (`0x117820`) `0xE2CA0` eine zuvor fehlgeschlagene Regionsprüfung
ersetzen. Diese alternative Suche hat **kein** Spielerargument: gleiche PCL,
zehn positive/negative PCL-Paarcacheplätze, sonst `0xD9C40`-Kachelsuche.
Ein vorhandener Cachetreffer führt nicht erneut durch die Regionsprüfung.
Ein Scope am E2610 allein deckt deshalb nicht jeden Gebäudezugang ab.

DA020 prüft außerdem Geometrie/Höhe und StructureGrid und erzeugt 12-Byte-
Annäherungs-/Angriffskandidaten in gemeinsamem Scratchspeicher. Nach dem
Order-Dispatcher-Aufruf sortiert/filtert `0x123090` anhand einer Suche ab der
Anführerposition: DB650 bei gesetzter Variante, sonst profilabhängig DA590
oder D9C40. Diese Suchen erhalten den sechsten DA020-Parameter nicht.
Der Verbraucher bei `0x11FFAC` verlangt eine geeignete erste Angriffskachel;
bei Fehlschlag wird die Einzelbewegungszuweisung übersprungen. Bei Erfolg
folgen Einzelbewegungen über `0x196280`. Die Angriffsliste ist kein permanenter
Auftrag und der gespeicherte Raid-Zielbezug beweist keinen erfolgreichen Weg.
Siehe ergänzend RAID_RETARGET.md für Sentinel, Unittypen und den bestehenden
BugfixesAndQoL-Raidfix.

## Konsequenz für den Testmod und verbleibende Grenze

Die bisherige Gleichheitsannahme des Gebäude-Resolvers ist zu streng:
Ein Suchwert 2 gegenüber Tribe-Besitzer 5 kann im Planer regulär sein. Sie
beweist dort keinen Spielerfehler. Ein Wertmatch allein beweist aber auch
keine Planerherkunft. Die bei 00:51:28 untersuchten zwei Aufrufe von Tribe
4423/global 2417968 hatten keinen kurzlebigen Auftragskontext; die bisherigen
Logs besitzen keine Call-Site-Kennung. Beide Ereignisse bleiben daher konkret
unaufgelöst und werden nicht nachträglich als bestätigte Normalfälle gewertet.
Die Datenreferenz bei `0x88F4AA4` auf DA020 sowie verwaltete Detour-/Trampolin-
Aufrufe sind zusätzliche Gründe, „zwei direkte native Aufrufer“ nicht als
Ausschluss sämtlicher anderer Aufrufquellen zu behandeln.

Diese Untersuchung ändert keine Runtime, Suchargumente, Masken, Hooks oder
Integritätswertung. Zusätzliche Runtime-Diagnose ist für den jetzt bewiesenen
Parametervertrag nicht nötig. Der nächste Resolverentwurf muss die zwei Rollen
explizit trennen: Vanillas Suchspieler für deren Abfrage und einen unabhängig
identitätsgeprüften bewegenden Kontext für die Gatepolicy. Vor einer Freigabe
müssen auch Same-PCL, alternative Suche, Cache und nachgelagerte Kandidatenfilter
betrachtet werden. Weder null noch der ausgewählte Gegner darf im Originalaufruf
pauschal durch den Tribe-Besitzer ersetzt werden. Erwartbare Rollenunterschiede
dürfen erst bei belegter Herkunft aus der dauerhaften Fehlerwertung herausfallen;
Identitätsfehler, unklare Herkunft und echte Scope-Konflikte bleiben getrennt.

## Reproduzierbare Prüfung und Regressionen

Unter `_inspect/EnemyGateBuildingContextAudit` liegen `audit.py`,
`installed-layout.ps1` und der vollständige Decompiler-/Maschinencodebeleg
`evidence.txt`. Die Prüfung leitet DB und installierte DLL aus CURRENT.json/
DATABASE_INFO.json ab, öffnet SQLite read-only, prüft Hash, Instruktionsgrenzen,
Argumenttransporte, Schreiber, direkte Caller, Dispatchfälle und Sentinelbasis.
Der Layouttest prüft acht öffentliche installierte Feldverträge und beide
Recordgrößen. Diese Prüfungen installieren keine Hooks und starten kein Spiel.

Für die spätere Resolveränderung erforderlich: normaler und Raid-Gebäudeangriff,
vorgelagerte Zielplanung gegen einen anderen Spieler, eigener/kontrollierter
Anführer, null-Abfrage, Same-PCL und Alternativsuche, eigener/eroberter Torzugang,
echter Umweg sowie BugfixesAndQoL/APIShared ohne Testmod. Keine neue Spielabnahme
oder Build wird durch die vorliegende reine Vertragsdokumentation behauptet.


## Umsetzung des Bewegungsscopes, 02.10.2026

Der Testmod bindet jetzt beide DA020-Einstiege an denselben Resolver. Er
validiert Snapshot-/Live-Tribeidentität und Anführerkontrolle und ordnet den
nativen Suchwert nur als kompatible Rolle ein. Bei passender Rolle verwendet
nur die zusätzliche Gate-Richtungsmaske den Bewegungsspieler; das originale
Suchargument bleibt unverändert. Ungültige Identitäten, Kontrollkonflikte und
unerklärte Rollen bleiben offen und Fehler. Callerzuordnung bleibt unobserved.
Planungsrolle null verwendet den Ersatzwert 1 nur außerhalb des Editors;
Editorstatus wird über den bestehenden Mission-Lifecycle geliefert.
Die früheren beiden Logereignisse bleiben historisch unaufgelöst.

Es gibt keine neuen Hooks, öffentlichen Schnittstellen oder Hauptmodänderungen.
Insbesondere werden keine weiteren Änderungen an Graph-/Same-PCL-/E2CA0-
Cachepfaden behauptet. Die Spielregression muss deren bisheriges Verhalten
bestätigen; zusätzliche NoRoute-Serien verhindern die Abnahme. Die aktuell
reproduzierbare Memberprüfung enthält nun zusätzlich beide Global-ID-Felder.


Technischer Nachweis der Umsetzung: build.bat des Testmods bestanden, 1824
Assertions und installierte RedBird-Capturer-Maschinentests, null Warnungen/
Fehler. Die unveränderte geschachtelte Scope-Restauration wurde statisch geprüft;
kein neuer nativer Building-Scope-Ausführungstest. Neue öffentliche Member und
Wrapper gegen die installierte SHCDESE.dll geprüft. Testmod lokal/installiert
SHA-256 69C349B4892F3B2850CDB351F21BA5DD3AD173CF7D01FDC2F5700B7832F57BB0.
Hauptmod/APIShared-DLLs blieben identisch. Spielabnahme ausstehend.


## Diagnosebereinigung und Spielbefunde, 02.10.2026

Die neue Gebäude-Scope-Fassung wurde in drei aktiven test_gates.sav-Epochen
beobachtet: 13:35:51.837–13:36:47.889 (KI-Zähler 11100, Rollenunterschiede 13),
13:36:51.958–13:37:56.844 (14151 / 14), 13:39:09.823–13:41:29.280 (8888 / 30).
Alle beobachteten Builder-Ergebnisse waren positiv, kein NoRoute, keine
Gebäude-Kontext-/Scope-/Snapshotfehler oder Exceptions; runtimeIntegrity=PASS.
Im zweiten Lauf enthielten die Ergebnisaggregate 14157 positive Beobachtungen,
also sechs mehr als der KI-Eintrittszähler. Das ist eine historische Zähldifferenz,
kein nachträglich aufgeklärter Einzelfall. Native Entry/Exit und Ergebnisobserver
verwendeten unterschiedliche KI-Abfragen; künftig bleibt die Entryklassifizierung
im QueryScope eingefroren und gilt für alle drei Stellen. Originalargumente,
Suchergebnisse, Scopemasken und Hookadressen bleiben unverändert.

Gemeinsame Suchaufrufe zählen Attack, BuildingApproach, BuildingConsumer,
CursorCommand und AI Builder jeweils einmal. BuildingConsumer bleibt im
Kantenzähler dieselbe Suchklasse wie bisher, erhält aber seinen eigenen
Aufrufzähler. Standalone CursorCommand erhält ebenfalls einen Aufrufzähler.

Gate-live-Zustände werden ohne Obergrenze nach Spieler, Building-ID, vollständigem
Zustand und vollständigem Detail intern gespeichert. Global-ID, Owner-/Capture-
Relation, Rohwerte, Policy, Ursachen und Brücken stehen in einer Definition
`Enemy-gate state: epoch=N,id=M,...`; Ergebniszeilen referenzieren `gateState=N/M`.
Definitionen erscheinen vor ihrer ersten Verwendung; Definitionen und aktive
Aggregate werden atomar ausgelesen. Identische Zustände behalten ihre Kennung über
Zehn-Sekunden-Fenster, Änderungen erhalten eine andere Kennung. Jede Observation,
erster/letzter Tribe-/Zielwert und bestehende Wechselzähler bleiben erhalten.
Reset erfolgt nur beim bestehenden Kartenstart; Epoche wird erhöht. Kein Unittracking.

Installierter SE 2.12.0, Tag v2.12.0, Commit f8d51730fcb54b25af43d3c9348d57db058e077f,
Tree 657af449e1397c58e6c5ec054977d83198b68e66 und Assemblyhash
DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF sind auditiert.
Die Mindestabhängigkeit bleibt unverändert; der Log-Versionsmatch ist weiterhin
ein Versionsvergleich und beweist keinen Laufzeit-Hashmatch.

Vanilla FBCB9319 liest Connection-Permissions in 0x181E00 mit 90 Int32 pro Zeile
(IMUL 0x5A, Zeilenabstand 0x168), Klassen 1–6 ab 0x32BDB0. Der aktuelle öffentliche
API-Konstantwert ist 89. Der flache Getter liefert 534 statt 540 Einträge;
unser Vergleich interpretiert den vorhandenen Prefix weiterhin korrekt mit
Native-Stride 90. Es fehlen Profileindex 89 und Permissionindizes 534–539
(Klasse 6, Unitindices 84–89). Alle gelesenen Werte der letzten Läufe stimmten.
Die Warnung bezeichnet jetzt unvollständige Abdeckung, keine Tabellenmutation;
Changed-Value-Warnungen bleiben getrennt. Der öffentliche Row-Getter verwendet
jedoch Stride 89 und adressiert Klassen 2–6 falsch; dies betrifft auch seinen
Can/Set-Verbraucher. Unsere Policy verwendet diese Getter/Setter nicht. Fork und
Hauptmods wurden für diesen Schritt nicht geändert. Vollständige Native-Reader-
Belege: _inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt.
Der englische Autorenreport liegt daneben als SCRIPT_EXTENDER_PATHFINDING_REPORT.md.

Logabnahmegrenzen: Alle Pre/Post-Paare und Aggregatsummen stimmten. Der dritte
Lauf beobachtete 12 gefilterte Gebäudekanten. Eroberung, nachgewiesene Cursorsperre
und Raid-Gebäudebefehl 9 wurden in diesen drei Läufen nicht beobachtet. Die
Spielbeobachtungen dieser Epochen sind noch nicht zugeordnet. Ein späterer Lauf
ohne Testmod ist ein eigener Hauptmodvergleich, kein Gatepolicy-Nachweis.
Offen: normaler/Raid-Gebäudeangriff, Eroberung/Rückeroberung, eigener Zugang nach
gesperrtem Cursorziel, mehrere Wellen und beobachteter Hauptmodvergleich. Keine
weitergehende KI-Verhaltensänderung ist aus diesen Logs gerechtfertigt.

Buildabnahme der Diagnosebereinigung: build.bat hat am 02.10.2026 die Fassung
mit 2847 bestandenen Assertions und null Warnungen/Fehlern gebaut und installiert.
Beide installierten RedBird-Capturer-Maschinentests bestanden. Die neuen Tests
prüfen 1500 Beobachtungen über 100 vollständige Torzustände und drei Zeitfenster,
Rekonstruktion, Epochenreset, eingefrorene KI-Zuordnung und unvollständige Tabellen.
Ein erster Testlauf stoppte vor der Installation wegen einer unbeabsichtigten
Änderung am direkten Cursorwrapper; dieser Pfad wurde vollständig wiederhergestellt.
Danach bestanden Tests und sämtliche Vorprüfungen erneut vor dem finalen Treiberlauf.
Lokale/installierte Testmod-DLL SHA-256:
B85CFC510B7912A4D1DF7D6F81ADB7EFB773FBE60B2F41E25853648CF6358C10.
BugfixesAndQoL und APIShared blieben gegenüber dem Stand vor diesem Build identisch
(00A0512B1F94C598B5417B6B5790A396E7B67228B27DEE2AB81E4BA466552CF7 /
02332AF3506C92C47DB066C1C2AC764A312D75E2CAFC144F1FB3B5E27617C6FF).
Version unverändert 0.1.5, README unverändert. Neue Spielabnahme der Ausgabe steht aus.
