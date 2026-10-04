# GebÃ¤ude-AnnÃ¤herung: Suchspieler und bewegender Spieler

Audit: 02.10.2026. Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Alle Adressen sind RVAs ausschlieÃŸlich dieses Builds. Vertrauen: hoch fÃ¼r die
unten beschriebenen direkten nativen DatenflÃ¼sse; keine Zuordnung eines
konkreten Runtime-Aufrufs allein anhand gleicher Zahlenwerte.

Installierter Script Extender: 2.12.0.0, SHA-256
`DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`.
Lokaler Fork-Commit `f8d51730fcb54b25af43d3c9348d57db058e077f`,
Tree `657af449e1397c58e6c5ec054977d83198b68e66`; diese Provenienz stimmt
mit DATABASE_INFO.json Ã¼berein. Installierte Feldtypen/-offsets wurden separat
Ã¼ber die echte SHCDESE.dll geprÃ¼ft. Keine publicized Assembly verwendet.

## Vertrag

`0xDA020(pathManager, tribeId, buildingId, requestedResults, sourcePcl,
nativeQueryPlayer)` besitzt zwei direkte native Aufrufer. Das sechste Argument
ist kein einheitlicher Angreiferspieler und keine Bewegungsklasse.

| Aufrufweg | Quelle des sechsten Arguments | TatsÃ¤chlich bewegender Kontext |
| --- | --- | --- |
| Zielplaner `0x30620`, Call `0x30A33` | ausgewÃ¤hlter gegnerischer Spieler; auÃŸerhalb des Editors Ersatzwert 1 bei gespeichertem null | Ã¼bergebener Tribe und dessen AnfÃ¼hrer; unabhÃ¤ngig vom ausgewÃ¤hlten Gegner |
| Befehlsverteiler `0x11E960`, Call `0x11FF9A`, Befehle 9 und 38 | vorzeichenbehaftetes 16-Bit-Kontrollfeld des AnfÃ¼hrers | derselbe AnfÃ¼hrer/Tribe; Kontrollspieler und Tribe-Besitzer dÃ¼rfen nicht ungeprÃ¼ft gleichgesetzt werden |
| derselbe GebÃ¤udezweig, Befehl 36 | null | derselbe bewegende Tribe; null ist eine absichtliche Vanilla-Abfragerolle |

Die Dispatchtabelle bei `0x121D4C` fÃ¼hrt 9, 36 und 38 zum gemeinsamen
GebÃ¤udehandler `0x11FC25`. Andere Befehle sind nicht pauschal GebÃ¤udeabfragen:
Die Nullzuweisung am gemeinsamen Aufruf gilt fÃ¼r die dort ankommenden Befehle
auÃŸer 9/38. Sie ist kein Beleg, dass jeder andere Befehl DA020 aufruft.

## Vorgelagerte Zielplanung und Feldschreiber

`0x2A720(aiRoot, ownPlayer, tribeId)` sucht unter Spielern 1..8 einen lebenden,
nicht verbÃ¼ndeten Gegner. Die AIC-Auswahlzweige vergleichen Entfernung bzw.
Spielerwerte; ohne Treffer folgt die nÃ¤chste geeignete Gegnerauswahl. Bleibt
auch sie erfolglos, schreibt der normale Spielpfad 1; im Editor kehrt er ohne
diesen Ersatz zurÃ¼ck. Die beiden direkten Schreiber bei `0x2A9C5` und
`0x2A9E6` schreiben ein WORD an
`module+0x7CC6D6A+tribeId*0x688`.

Der Tribe-Manager beginnt bei `module+0x7CC6720`, sein Array bei +0x2A.
Damit ist managerrelativ +0x64A gleich strukturrelativ +0x620. Die installierte
Interop benennt dort das UINT32-Feld `N00000580`; Vanilla liest/schreibt dessen
unteres WORD. Das benannte Feld `r_AttackTargetOwnerPlayerId` liegt hingegen
bei +0x61C und ist **nicht** die Quelle dieses Arguments.

`0x30620` liest das WORD bei `0x30682` vorzeichenbehaftet nach EBP. Es verwendet
denselben Wert zur Auswahl des gegnerischen GebÃ¤udes Ã¼ber `0xCE570` und fÃ¼r
gegnerische RÃ¼ckfallkoordinaten. Im direkten GebÃ¤ude-AnnÃ¤herungszweig der
Speicherrollen 182/185 kopiert `0x30A09` EBP nach `[rsp+0x28]`; `0x30A33`
ruft DA020 auf. Bei fehlendem AnfÃ¼hrer wird dieser Suchaufruf umgangen.
Eine fehlende geeignete Angriffskachel fÃ¼hrt zu den gegnerischen
RÃ¼ckfallkoordinaten; anschlieÃŸend entscheidet `0x3E650` Ã¼ber Bewegung bzw.
`0x3E9A0`. Die anderen Planungszweige kÃ¶nnen direkt GebÃ¤udeauftrag 9 oder
die vorgelagerte Graben-/Sonderzielwahl Ã¼ber `0xF09A0` auslÃ¶sen. Ein spÃ¤terer
GebÃ¤udeauftrag beweist deshalb nicht, dass der direkte Planeraufruf lief.

## AnfÃ¼hrer und ID-Basis

`0x11FF47` liest den AnfÃ¼hrer aus managerrelativ +0x5A, also
`GameTribe.r_LeaderUnitId` bei +0x30. `0x11FF4D` multipliziert die unverÃ¤nderte
1-basierte Game-ID mit Unitstride 0x490. Die Unitmanagerbasis hat bei +0x65C
den vorgeschalteten Sentinel/LastOrderedUnit. Die Ã¶ffentliche Unitspan beginnt
erst hinter diesem Record und `TryGetUnitById(id)` verwendet dort `id-1`:

`manager+0x65C+id*0x490 == publicSpan+(id-1)*0x490`.

Die native Kontrollabfrage bei `0x11FF73` ist `MOVSX EAX,WORD [unitManager+
leaderId*0x490+0x6EE]`, entsprechend Unitpointer +0x92. In der installierten
Interop ist `r_ControllableForPlayerId` bei +0x92 ein **BYTE**, das nÃ¤chste
unbenannte BYTE liegt bei +0x93. Beide zusammen bilden das native signed WORD.
Das obere Byte darf fÃ¼r eine zukÃ¼nftige exakte Diagnose nicht stillschweigend
als null angenommen werden. `0x1185A0` aktualisiert beim Tribe-Kontrollwechsel
Tribe-Besitzer und dieses gesamte Unit-WORD fÃ¼r geeignete Mitglieder. Seine
Existenz belegt keinen unverÃ¤nderlichen Gleichstand aller Einheiten/Tribes.

`0x11E986` setzt ESI auf null. Befehle 9/38 lesen das WORD, Befehl 36 behÃ¤lt
null; `0x11FF89` verÃ¶ffentlicht EAX als sechstes Argument. Kein ID-Off-by-one
ist in diesem Zugriff nachgewiesen. FrÃ¼here Notizen mit â€žushortâ€œ werden durch
den hier bestÃ¤tigten **signed WORD â†’ int**-Vertrag prÃ¤zisiert.

## Nachgelagerte Verwendung und terminale Folgen

DA020 reicht das sechste Argument unverÃ¤ndert als Spielerargument an E2610
weiter, sowohl im direkten GebÃ¤udering als auch im erweiterten Suchring;
der Regionsmodus ist jeweils 0. E2610 akzeptiert gleiche PCL unmittelbar,
verwirft unterschiedliche Null-PCL und prÃ¼ft sonst aktive Verbindungsrecords
mit Modus, Besitzer/Allianz und Eroberungswert. Spieler null Ã¼berspringt in
dieser Recordauswahl die Spieler-/Allianzbedingung absichtlich. Der anschlieÃŸende
Graphdurchlauf kann einen nÃ¤chsten PCL liefern oder mit null scheitern.
Sein zweiter Durchlauf und die Scratch-/Ergebnisflags Ã¤ndern nicht die Herkunft
des Spielerarguments.

DA020 besitzt davor eine eigene Same-PCL-Akzeptanz. AuÃŸerdem kann bei geeignetem
Tribeprofil (`0x117820`) `0xE2CA0` eine zuvor fehlgeschlagene RegionsprÃ¼fung
ersetzen. Diese alternative Suche hat **kein** Spielerargument: gleiche PCL,
zehn positive/negative PCL-PaarcacheplÃ¤tze, sonst `0xD9C40`-Kachelsuche.
Ein vorhandener Cachetreffer fÃ¼hrt nicht erneut durch die RegionsprÃ¼fung.
Ein Scope am E2610 allein deckt deshalb nicht jeden GebÃ¤udezugang ab.

DA020 prÃ¼ft auÃŸerdem Geometrie/HÃ¶he und StructureGrid und erzeugt 12-Byte-
AnnÃ¤herungs-/Angriffskandidaten in gemeinsamem Scratchspeicher. Nach dem
Order-Dispatcher-Aufruf sortiert/filtert `0x123090` anhand einer Suche ab der
AnfÃ¼hrerposition: DB650 bei gesetzter Variante, sonst profilabhÃ¤ngig DA590
oder D9C40. Diese Suchen erhalten den sechsten DA020-Parameter nicht.
Der Verbraucher bei `0x11FFAC` verlangt eine geeignete erste Angriffskachel;
bei Fehlschlag wird die Einzelbewegungszuweisung Ã¼bersprungen. Bei Erfolg
folgen Einzelbewegungen Ã¼ber `0x196280`. Die Angriffsliste ist kein permanenter
Auftrag und der gespeicherte Raid-Zielbezug beweist keinen erfolgreichen Weg.
Siehe ergÃ¤nzend RAID_RETARGET.md fÃ¼r Sentinel, Unittypen und den bestehenden
BugfixesAndQoL-Raidfix.

## Konsequenz fÃ¼r den Testmod und verbleibende Grenze

Die bisherige Gleichheitsannahme des GebÃ¤ude-Resolvers ist zu streng:
Ein Suchwert 2 gegenÃ¼ber Tribe-Besitzer 5 kann im Planer regulÃ¤r sein. Sie
beweist dort keinen Spielerfehler. Ein Wertmatch allein beweist aber auch
keine Planerherkunft. Die bei 00:51:28 untersuchten zwei Aufrufe von Tribe
4423/global 2417968 hatten keinen kurzlebigen Auftragskontext; die bisherigen
Logs besitzen keine Call-Site-Kennung. Beide Ereignisse bleiben daher konkret
unaufgelÃ¶st und werden nicht nachtrÃ¤glich als bestÃ¤tigte NormalfÃ¤lle gewertet.
Die Datenreferenz bei `0x88F4AA4` auf DA020 sowie verwaltete Detour-/Trampolin-
Aufrufe sind zusÃ¤tzliche GrÃ¼nde, â€žzwei direkte native Aufruferâ€œ nicht als
Ausschluss sÃ¤mtlicher anderer Aufrufquellen zu behandeln.

Diese Untersuchung Ã¤ndert keine Runtime, Suchargumente, Masken, Hooks oder
IntegritÃ¤tswertung. ZusÃ¤tzliche Runtime-Diagnose ist fÃ¼r den jetzt bewiesenen
Parametervertrag nicht nÃ¶tig. Der nÃ¤chste Resolverentwurf muss die zwei Rollen
explizit trennen: Vanillas Suchspieler fÃ¼r deren Abfrage und einen unabhÃ¤ngig
identitÃ¤tsgeprÃ¼ften bewegenden Kontext fÃ¼r die Gatepolicy. Vor einer Freigabe
mÃ¼ssen auch Same-PCL, alternative Suche, Cache und nachgelagerte Kandidatenfilter
betrachtet werden. Weder null noch der ausgewÃ¤hlte Gegner darf im Originalaufruf
pauschal durch den Tribe-Besitzer ersetzt werden. Erwartbare Rollenunterschiede
dÃ¼rfen erst bei belegter Herkunft aus der dauerhaften Fehlerwertung herausfallen;
IdentitÃ¤tsfehler, unklare Herkunft und echte Scope-Konflikte bleiben getrennt.

## Reproduzierbare PrÃ¼fung und Regressionen

Unter `_inspect/EnemyGateBuildingContextAudit` liegen `audit.py`,
`installed-layout.ps1` und der vollstÃ¤ndige Decompiler-/Maschinencodebeleg
`evidence.txt`. Die PrÃ¼fung leitet DB und installierte DLL aus CURRENT.json/
DATABASE_INFO.json ab, Ã¶ffnet SQLite read-only, prÃ¼ft Hash, Instruktionsgrenzen,
Argumenttransporte, Schreiber, direkte Caller, DispatchfÃ¤lle und Sentinelbasis.
Der Layouttest prÃ¼ft acht Ã¶ffentliche installierte FeldvertrÃ¤ge und beide
RecordgrÃ¶ÃŸen. Diese PrÃ¼fungen installieren keine Hooks und starten kein Spiel.

FÃ¼r die spÃ¤tere ResolverÃ¤nderung erforderlich: normaler und Raid-GebÃ¤udeangriff,
vorgelagerte Zielplanung gegen einen anderen Spieler, eigener/kontrollierter
AnfÃ¼hrer, null-Abfrage, Same-PCL und Alternativsuche, eigener/eroberter Torzugang,
echter Umweg sowie BugfixesAndQoL/APIShared ohne Testmod. Keine neue Spielabnahme
oder Build wird durch die vorliegende reine Vertragsdokumentation behauptet.


## Umsetzung des Bewegungsscopes, 02.10.2026

Der Testmod bindet jetzt beide DA020-Einstiege an denselben Resolver. Er
validiert Snapshot-/Live-TribeidentitÃ¤t und AnfÃ¼hrerkontrolle und ordnet den
nativen Suchwert nur als kompatible Rolle ein. Bei passender Rolle verwendet
nur die zusÃ¤tzliche Gate-Richtungsmaske den Bewegungsspieler; das originale
Suchargument bleibt unverÃ¤ndert. UngÃ¼ltige IdentitÃ¤ten, Kontrollkonflikte und
unerklÃ¤rte Rollen bleiben offen und Fehler. Callerzuordnung bleibt unobserved.
Planungsrolle null verwendet den Ersatzwert 1 nur auÃŸerhalb des Editors;
Editorstatus wird Ã¼ber den bestehenden Mission-Lifecycle geliefert.
Die frÃ¼heren beiden Logereignisse bleiben historisch unaufgelÃ¶st.

Es gibt keine neuen Hooks, Ã¶ffentlichen Schnittstellen oder HauptmodÃ¤nderungen.
Insbesondere werden keine weiteren Ã„nderungen an Graph-/Same-PCL-/E2CA0-
Cachepfaden behauptet. Die Spielregression muss deren bisheriges Verhalten
bestÃ¤tigen; zusÃ¤tzliche NoRoute-Serien verhindern die Abnahme. Die aktuell
reproduzierbare MemberprÃ¼fung enthÃ¤lt nun zusÃ¤tzlich beide Global-ID-Felder.


Technischer Nachweis der Umsetzung: build.bat des Testmods bestanden, 1824
Assertions und installierte RedBird-Capturer-Maschinentests, null Warnungen/
Fehler. Die unverÃ¤nderte geschachtelte Scope-Restauration wurde statisch geprÃ¼ft;
kein neuer nativer Building-Scope-AusfÃ¼hrungstest. Neue Ã¶ffentliche Member und
Wrapper gegen die installierte SHCDESE.dll geprÃ¼ft. Testmod lokal/installiert
SHA-256 69C349B4892F3B2850CDB351F21BA5DD3AD173CF7D01FDC2F5700B7832F57BB0.
Hauptmod/APIShared-DLLs blieben identisch. Spielabnahme ausstehend.


## Diagnosebereinigung und Spielbefunde, 02.10.2026

Die neue GebÃ¤ude-Scope-Fassung wurde in drei aktiven test_gates.sav-Epochen
beobachtet: 13:35:51.837â€“13:36:47.889 (KI-ZÃ¤hler 11100, Rollenunterschiede 13),
13:36:51.958â€“13:37:56.844 (14151 / 14), 13:39:09.823â€“13:41:29.280 (8888 / 30).
Alle beobachteten Builder-Ergebnisse waren positiv, kein NoRoute, keine
GebÃ¤ude-Kontext-/Scope-/Snapshotfehler oder Exceptions; runtimeIntegrity=PASS.
Im zweiten Lauf enthielten die Ergebnisaggregate 14157 positive Beobachtungen,
also sechs mehr als der KI-EintrittszÃ¤hler. Das ist eine historische ZÃ¤hldifferenz,
kein nachtrÃ¤glich aufgeklÃ¤rter Einzelfall. Native Entry/Exit und Ergebnisobserver
verwendeten unterschiedliche KI-Abfragen; kÃ¼nftig bleibt die Entryklassifizierung
im QueryScope eingefroren und gilt fÃ¼r alle drei Stellen. Originalargumente,
Suchergebnisse, Scopemasken und Hookadressen bleiben unverÃ¤ndert.

Gemeinsame Suchaufrufe zÃ¤hlen Attack, BuildingApproach, BuildingConsumer,
CursorCommand und AI Builder jeweils einmal. BuildingConsumer bleibt im
KantenzÃ¤hler dieselbe Suchklasse wie bisher, erhÃ¤lt aber seinen eigenen
AufrufzÃ¤hler. Standalone CursorCommand erhÃ¤lt ebenfalls einen AufrufzÃ¤hler.

Gate-live-ZustÃ¤nde werden ohne Obergrenze nach Spieler, Building-ID, vollstÃ¤ndigem
Zustand und vollstÃ¤ndigem Detail intern gespeichert. Global-ID, Owner-/Capture-
Relation, Rohwerte, Policy, Ursachen und BrÃ¼cken stehen in einer Definition
`Enemy-gate state: epoch=N,id=M,...`; Ergebniszeilen referenzieren `gateState=N/M`.
Definitionen erscheinen vor ihrer ersten Verwendung; Definitionen und aktive
Aggregate werden atomar ausgelesen. Identische ZustÃ¤nde behalten ihre Kennung Ã¼ber
Zehn-Sekunden-Fenster, Ã„nderungen erhalten eine andere Kennung. Jede Observation,
erster/letzter Tribe-/Zielwert und bestehende WechselzÃ¤hler bleiben erhalten.
Reset erfolgt nur beim bestehenden Kartenstart; Epoche wird erhÃ¶ht. Kein Unittracking.

Installierter SE 2.12.0, Tag v2.12.0, Commit f8d51730fcb54b25af43d3c9348d57db058e077f,
Tree 657af449e1397c58e6c5ec054977d83198b68e66 und Assemblyhash
DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF sind auditiert.
Die MindestabhÃ¤ngigkeit bleibt unverÃ¤ndert; der Log-Versionsmatch ist weiterhin
ein Versionsvergleich und beweist keinen Laufzeit-Hashmatch.

Vanilla FBCB9319 liest Connection-Permissions in 0x181E00 mit 90 Int32 pro Zeile
(IMUL 0x5A, Zeilenabstand 0x168), Klassen 1â€“6 ab 0x32BDB0. Der aktuelle Ã¶ffentliche
API-Konstantwert ist 89. Der flache Getter liefert 534 statt 540 EintrÃ¤ge;
unser Vergleich interpretiert den vorhandenen Prefix weiterhin korrekt mit
Native-Stride 90. Es fehlen Profileindex 89 und Permissionindizes 534â€“539
(Klasse 6, Unitindices 84â€“89). Alle gelesenen Werte der letzten LÃ¤ufe stimmten.
Die Warnung bezeichnet jetzt unvollstÃ¤ndige Abdeckung, keine Tabellenmutation;
Changed-Value-Warnungen bleiben getrennt. Der Ã¶ffentliche Row-Getter verwendet
jedoch Stride 89 und adressiert Klassen 2â€“6 falsch; dies betrifft auch seinen
Can/Set-Verbraucher. Unsere Policy verwendet diese Getter/Setter nicht. Fork und
Hauptmods wurden fÃ¼r diesen Schritt nicht geÃ¤ndert. VollstÃ¤ndige Native-Reader-
Belege: _inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt.
Der englische Autorenreport liegt daneben als SCRIPT_EXTENDER_PATHFINDING_REPORT.md.

Logabnahmegrenzen: Alle Pre/Post-Paare und Aggregatsummen stimmten. Der dritte
Lauf beobachtete 12 gefilterte GebÃ¤udekanten. Eroberung, nachgewiesene Cursorsperre
und Raid-GebÃ¤udebefehl 9 wurden in diesen drei LÃ¤ufen nicht beobachtet. Die
Spielbeobachtungen dieser Epochen sind noch nicht zugeordnet. Ein spÃ¤terer Lauf
ohne Testmod ist ein eigener Hauptmodvergleich, kein Gatepolicy-Nachweis.
Offen: normaler/Raid-GebÃ¤udeangriff, Eroberung/RÃ¼ckeroberung, eigener Zugang nach
gesperrtem Cursorziel, mehrere Wellen und beobachteter Hauptmodvergleich. Keine
weitergehende KI-VerhaltensÃ¤nderung ist aus diesen Logs gerechtfertigt.

Buildabnahme der Diagnosebereinigung: build.bat hat am 02.10.2026 die Fassung
mit 2847 bestandenen Assertions und null Warnungen/Fehlern gebaut und installiert.
Beide installierten RedBird-Capturer-Maschinentests bestanden. Die neuen Tests
prÃ¼fen 1500 Beobachtungen Ã¼ber 100 vollstÃ¤ndige TorzustÃ¤nde und drei Zeitfenster,
Rekonstruktion, Epochenreset, eingefrorene KI-Zuordnung und unvollstÃ¤ndige Tabellen.
Ein erster Testlauf stoppte vor der Installation wegen einer unbeabsichtigten
Ã„nderung am direkten Cursorwrapper; dieser Pfad wurde vollstÃ¤ndig wiederhergestellt.
Danach bestanden Tests und sÃ¤mtliche VorprÃ¼fungen erneut vor dem finalen Treiberlauf.
Lokale/installierte Testmod-DLL SHA-256:
B85CFC510B7912A4D1DF7D6F81ADB7EFB773FBE60B2F41E25853648CF6358C10.
BugfixesAndQoL und APIShared blieben gegenÃ¼ber dem Stand vor diesem Build identisch
(00A0512B1F94C598B5417B6B5790A396E7B67228B27DEE2AB81E4BA466552CF7 /
02332AF3506C92C47DB066C1C2AC764A312D75E2CAFC144F1FB3B5E27617C6FF).
Version unverÃ¤ndert 0.1.5, README unverÃ¤ndert. Neue Spielabnahme der Ausgabe steht aus.

## Stored-route audit and passive observation (2026-10-04)

Full native hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 remains installed. bridge-stored-path-evidence.txt retains27 complete selected function bodies and complete installed-byte disassembly: all11 database functions referencing packed Unit buffers, all7 path-context buffer writers/readers, movement/current-position updates, builder/consume/reset helpers. A presumed1841A0 entry was absent and is not used. No new native hooks or competing owners are introduced.

Unit native base67E8400 + Game-ID*490 +65C maps public GameUnit: current tileC0/C2, stored startC8/CA, next tileDC/DE, plan flagsF2, substepF4, cursorF6 and lengthF8. The full490-byte native copy includes the preceding4-byte sentinel word; do not infer public record basis658 from that copy.196280 and197050 reset cursor, store start inC8/CA and publish builder length.185240/198620/198B60 independently reconstruct from that stored start;198620/198B60 can shorten or clear the plan.1869C0 clears the1000-byte slot;15710 restores it on load. Packed buffer is manager+B4FE78+Game-ID*1000, physical capacity2000 nibbles, low nibble first. E1640 backtracks then E4E90 reverses the transitions. Direction pairs at2D2E50 are interleaved Int32 dx/dy with8-byte stride, verified against installed bytes; apparent4-byte indexing in some decompiler output must not be copied. Unknown active nibbles/invalid coordinates/cursor/length are explicit incomplete decoding, not guessed directions.

1855A0 advances cursor BEFORE a tile step finishes.180230 conditionally copies next tileDC/DE into currentC0/C2 then1802A0 updates occupancy. The installed Extender movement wrapper (retained installed-unit-event-contract.cs) raises Pre, calls its original exactly once unless another observer skips it, then raises Post. Runtime subscribes to this existing static R3 publisher; Post absence is not a completed move. Original commands/results/buffers are untouched. Public GameUnit fields and public TryGetUnitPathPlanView/PackedBytes are validated by verify.ps1 against installed SHCDESE.dll. No new Assembly-CSharp access, public API, Extender fork change or detour exists.

Route tracking freezes unit/global/player/tribe/command/parent-event/consumer/military-root and last completed candidate plan. The latter remains explicitly chronological unless reservation evidence proves the link. Before/after command packed bytes distinguish preserved paths from changed paths, and raw command return is retained. Nested commands must not overwrite a newer command's return. Cached public pointer views are used only after fresh GlobalID validation. Same-header replacements are checked at least once per second; header changes and command returns inspect bytes immediately. This is bounded observation, not a claim to catch every transient replacement between events. Physical bridge toggles force reobservation of retained paths.

Exact moving deck fields come from the audited15-of25 orientation mapper, not every moat field or rectangular proximity. Parent-gate footprint geometry retains native-link versus adjacency-candidate ambiguity. Full relevant/undecodable packed bytes are logged for reconstruction. Stored deck intersection, gate-footprint intersection and actual tile-entry/exit observations are distinct; positive keep connectivity alone does not establish route dependency or justify blocking alternative routes. Any cross-thread header change invalidates the capture's atomicity claim.

Native contexts, aggregate stages/results and numeric player-group snapshots have shared definitions. Candidate contents can reuse an earlier definition while fresh planning and reservation contexts remain separate. Unscoped changed group commands keep bounded examples and exact counters; missing association is explicitly unknown. Session-end capture summary is immediate; session-delivered is emitted only after bounded draining empties the queue. File flush is not observed, so the analyzer separately rejects torn lines, missing references and absent delivery markers. No OnApplicationQuit/OnDestroy callback or unlimited shutdown drain is added.
