> Current action status, 2026-10-13 after format10: the quarry layout and rejection are now observed, 286 Raid routes were checked, and physical recapture is confirmed. Earlier requests below for screenshots or another quarry run are historical and withdrawn. See the final consolidated review below and Testmods/EnemyGatePathfindingTest/TARGETED_TESTS.md. Both local diagnostic options were set to false and read back successfully; restart applies them. No runtime source or package changed in this review.

# Raid / Gatepolicy: technische Abnahme vom 13.10.2026

## Ergebnis

Keine neue funktionale Korrektur belegt. Keine Runtimequelle, kein Hook, keine öffentliche Schnittstelle und kein Paket geändert. Hauptmodintegration bleibt gesperrt, bis die verbleibende native Erreichbarkeits-/Raid-Weglücke geklärt ist.

Die vom Nutzer bestätigten Fälle sind bestanden und werden nicht erneut angefordert:
- test_gates_around: KI nimmt mit Tormod den echten Umweg statt des feindlichen Torhausdurchgangs; Kontrolllauf mit demselben Hauptmodsatz ohne Tormod.
- Editor: Assassinen kletterten vor der Eroberung sichtbar aufs Dach; keine beobachtete unerlaubte Bodenpassage.
- Tatsächliche Rückeroberung durch Einheiten, danach neuer Bodenbefehl für normale Soldaten des ursprünglichen Besitzers: Zugang funktioniert.
Historische nicht zugeordnete native Einzelwege bleiben ungeklärt. Diese Klarstellungen ersetzen die älteren offenen Rückfragen, nicht deren damaligen Informationsstand.

## Produktive Kette

AiRaidRetargetFixRuntime bewertet den frisch beobachteten Kandidatenpuffer des Gebäudeauftrags. AttackBuilding (9) kann bei NoAttackPoint und geeigneter Nahkampfgruppe den vorhandenen Retry auslösen. ForceAttackBuilding (36) wird diagnostisch klassifiziert, löst dort aber keinen Retry aus. Gebäudeannäherung (38), bloßer gespeicherter Zielwert und erfolgreiche Auftragsrückgabe ersetzen diesen Nachweis nicht.

Die Gebäudesuche läuft über ObserveBuildingApproachBuilder und die vorhandene optionale Gate-Registrierung. Vanillas Suchargumente bleiben erhalten; ein geprüfter Bewegungsspieler bestimmt den Scope. Ohne Anbieter bleibt der native Kern unverändert einmal aufgerufen. Ein gültiger Angriffspunkt wird erhalten, auch wenn das Ziel das Tor selbst ist. Besitz-/Allianz-/Eroberungsfreigaben kommen aus der bestehenden Policy, nicht aus einem zweiten Raid-Filter.

ProcessRaidRetries prüft aktuelle Gruppen-/Zielidentität, gibt AttackBuildingEx aus und verlangt einen frischen gültigen Angriffspunkt plus tatsächlich gespeichertes Ziel. Frisch negative Kandidaten werden übersprungen; unklare Ergebnisse beenden den Versuch vorsichtig. Ein späterer negativer Einzelweg ist kein automatischer neuer Auslöser dieses Fixes. Die mögliche Lücke Angriffspunkt vorhanden -> später NoRoute bleibt deshalb offen, solange der konkrete native Fall nicht beobachtet ist.

Gleiche PCL bedeutet keine vollständig geprüfte Route. Auch unterschiedliche PCL und positiver Regionsvergleich beweisen keinen fertigen Weg. Der bestehende Same-PCL-Kurzschluss wird durch die bloße Scope-Registrierung nicht ersetzt. Die kontrollierten Tests geben native Kandidaten/Wege vor; sie beweisen nicht, dass Vanilla bei ausschließlich gesperrtem Torweg bereits eine leere Kandidatenliste liefert.

Sechs Harassment-Rollen sind identifizierbar. Rollen 1/2 besitzen zusätzlich einen Bewegungszweig; auch andere Zustände können ohne Gebäudeauftrag laufen. Rollenzugehörigkeit allein beweist keinen command-9-Retargetpfad. Keine zweite Retarget-Implementierung ergänzt.

## Vorhandene Spielbelege

Log_292.log: test_Raid_Retarget_Delay tatsächlich um 01:30:35.545 geladen. Abschluss 11.10.2026 01:34:12.761, Session4: attacks14, valid5, negative9, unknown0, retries9, selected9, aborted0, pending0. Tormod geladen, ausführlicher TEMP-Routenbeobachter nicht aktiv. Beispiel Spieler2/Rolle0/Tribe4402/63445: erstes Ersatzziel195/9698 frisch negativ, zweites204/19080 gültiger Angriffspunkt. Kein vollständig zugeordneter anschließender Torweg belegt; keine Aussage zur Torstellung oder Weggeometrie erfinden.

Log_277.log enthält einen echten Start, aber attacks0. Andere Save-Nennungen ohne Load-/Kartenstartmarker zählen nicht als Läufe. Historische erfolgreiche Raid-Schäden/Retargets ohne belegten Torbezug sind weiterhin allgemeine Raid-Regressionsbelege.

## Neue ausführbare Prüfung

_inspect/TemporaryGateAcceptanceRuntimeTests/RaidChainFixture.cs wird vom vorhandenen Roslyn-Prüfer geladen. Direkt aus aktuellen Quellen stammen:
- RaidAttackFieldEvaluation (vollständig);
- ProcessRaidRetries einschließlich seiner drei Datentypen;
- ObserveBuildingApproachBuilder, BeginEnemyGateSearch, EndEnemyGateSearch und ResolveEnemyGateBuildingPlayer;
- vorhandene produktive Wegausgabe-Diagnose und Tormod-Beobachter.

Kontrollierte Grenzen: Native-Building-Ergebnis, Kandidatenreihenfolge, SDK-Identitäten, Zulässigkeit der Nahkampfgruppe und native Wegbytes. Das Einreihen eines bestätigten negativen Auftrags ist Fixture-Eingabe; der vollständige native Planer und LogAttackCandidates werden hier nicht nachgebaut. Die vorhandene Raid-Suite prüft die produktiven Frische-/Framehelfer zusätzlich.

273 neue Ketten-Assertions und insgesamt751 Diagnosepfad-Assertions bestanden. Fälle: sechs Rollen, Scope an/aus, zwei unverändert weitergereichte Quellregionwerte, Angriffspunkt plus erlaubter Umweg, Angriffspunkt plus spätere negative Route, negative Suche plus gültiges Ersatzziel, negatives erstes/positives zweites Ersatzziel, veraltete Daten, fehlende Befehlsausführung, abweichendes gespeichertes Ziel, wiederverwendete Tribe-ID und erschöpfte Kandidaten. Der simulierte Umweg führt geometrisch um die gesperrte gerichtete Kante; er wird durch den tatsächlichen Ausgabebeobachter vollständig dekodiert. Ein Angriffspunkt zählt niemals als geprüfte Route. Ein Native-Aufruf pro Wrapperaufruf und wiederhergestellter Scope werden geprüft.

Die bestehende Hauptmod-Raid-Suite besteht mit1945 Klassifizierungs-,89 Native-/Assembler-,15 Aktivierungs- und35 Runtime-Helferprüfungen. Ihre vorhandene Assembly-Unification-Warnung bleibt vom Spielergebnis getrennt.

Die Scopeprüfung mit Anbieter an/aus beweist unveränderte Argumente, Aufrufzahlen und Weitergabe der vorgegebenen nativen Ergebnisse. Sie beweist keine Gleichheit der tatsächlichen Vanilla-Ergebnisse bei aktiver Torfilterung; diese dürfen sich bestimmungsgemäß unterscheiden. Eigene/verbündete/eroberte Masken bleiben durch die bestehenden Torpolicytests und die dokumentierten Spielbelege abgesichert; die neue Raidfixture erfindet keine neue Besitzregel.

Vollständiger installierter Native-Audit:60 Such-/Kletterfunktionen,16960 Instruktionsbereiche und21 Planungs-/Ausgabefunktionen erneut bestätigt. Aktuelle Raid-Baseline einschließlich Rollen-, Befehls-, Kandidaten- und Rückgabeverträgen berücksichtigt. Native-Hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

## Restlücke und nächste Aktion

Die Instrumentierung kann Angriffspunkte und materialisierte Raid-Wege getrennt prüfen. Nur synchron belegte Kontexte beziehungsweise geprüfte Gruppen-/Einheitenidentitäten werden übernommen. Zeitlich spätere Wege erhalten keine erfundene Zuordnung zu einem früheren Ersatzziel. Ein Rollentest im Fixture beweist nicht, dass jede Rolle im Spiel denselben Befehlszweig nimmt.

Für den nächsten Spielnachweis fehlt der bestätigte Aufbau von test_Raid_Retarget_Delay. Deshalb zuerst den Save pausiert ansehen und einen Überblick über angreifende Raidgruppe, offene Torhäuser und angegriffene Gebäude liefern; noch keinen weiteren Zufallslauf. Danach kann ein konkret geeigneter Gebäudeangriff mit echtem Umweg oder ausschließlich Torzugang ausgewählt werden. Keine PCL-Konstruktion durch den Nutzer verlangen. Brücken ausgeschlossen.

DetailedDiagnostics=true und MacemenDiagnostics=false sind lokal bereits gesetzt. Keine neue Konfigurationsänderung nötig. Keine Runtimeänderung, daher kein Modbuild und keine Installation nötig. Versionen, Mindestabhängigkeiten, README und Saves unverändert.

## 2026-10-13: consolidated technical review after quarry acceptance

Full installed-native audit passed again: 60 search/climb bodies, 16,960 instruction ranges and 21 planning/output bodies, hash FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Entire DA020, 123090 and DBC60 bodies reviewed against the source wrappers. DA020 creates coarse approach candidates. 123090 executes DA590 (ordinary), D9C40 (Assassin), or DB650 for its alternative variant, replaces unreachable scores with 10,000,000 and removes them before the command9 first-building-field guard. Same-PCL acceptance in DA020 is therefore not proof that an unreachable ordinary approach survives the consumer.

Both standalone and shared BuildingConsumer wrappers keep the validated Tribe-player scope around that call and restore it in finally. Existing direction adapters cover DA783, D9EA6 and DB650 sites. The friendly-moat candidate fallback uses BuildingCandidateEdge -> WeightedMoatRoutePlanner.TryGetTraversalEdge, which rejects a disallowed Gatepolicy direction before accepting ordinary/moat traversal. This is functional mainmod compatibility, not testmod-only diagnostic work. No registered Gatepolicy means the optional gate check is inactive.

The ladder-specific restoration is a distinct path: it retains candidates based on a positive group-mode region result and a legal building pair, not a full final route. This review does not promote that proof into an unconditional individual reachability guarantee. No failing gate example for that branch was established, so its gameplay behavior was not changed.

Verification: 273 actual Raid-chain assertions and 756 actual observer assertions passed; Raid suite 1,945 classification +89 native/assembler +15 activation +35 helpers passed; Assassin suite 16,257 assertions passed, including actual post-native validation, mode94 guard, cache and observer/no-observer equivalence. Native candidate outputs are controlled inputs in the managed Raid fixture, not execution of the native planner. An initial direct Assassin test invocation omitted its workspace argument and failed path resolution; rerunning with the required root argument passed. The test source was not altered to hide that invocation error.

Format10 evidence is retained from Log_313 / original LogOutput SHA256 d3790c7eb99a2d20f5d423770d1eb0ab16893d7a18831159a5410840f1681ec0. No new functional defect was reproduced. Historical native output486 (Log_311) stays movement-unproven; exact weighted output782 has its separately validated transition evidence. No route or command semantics, hooks, public interfaces, versions, README or saves changed. No production build is required for these documentation/configuration changes.
