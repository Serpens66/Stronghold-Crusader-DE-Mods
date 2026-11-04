# MoatMove

Stand 03.11.2026. Dieses Dokument beschreibt nur den aktuellen Stand; der frühere Verlauf (Modi `precise`, `fast`, `FastNative`, Messläufe, Pläne) steht in der Git-Historie. Prüfbar über den Code unter `src/`, `native/` und die Tests unter `tests/`.

## Zweck

Addon zu BugfixesAndQoL (Testmod, nicht im Steam-Modpack):
- Einheiten queren eigene und verbündete fertige Burggräben.
- Gruppenbefehle von Spielern und KI bekommen gewichtete Routen aus einem gemeinsamen nativen Feld statt einer Vanilla-Suche je Einheit. Ein Graben wird genommen, wenn der Weg darüber günstiger ist.
- Optional: Einzelbefehle über denselben Kern (`AnchorSingleMoves`) und Ausweichen vor Feuer (`AvoidFire`).

MoatMove beeinflusst das Spielgeschehen (`NetworkMode = 1`).

## Abhängigkeiten und Zuständigkeiten

- **BugfixesAndQoL** besitzt alle nativen Befehls-Hooks (`BugfixesAndQoL/src/UnitCommands`). MoatMove registriert sich als Traversal-Provider (`FriendlyMoatTraversalProvider`, `UnitCommandPathAPI.RegisterTraversal`) und meldet `RequiredOnly = true`: Vanilla zuerst, Grabenwege nur, wo Kern oder Fast sie liefern.
- **APIShared** stellt die Routenregeln bereit (`RouteTileRules`, `RouteReplanPolicy`, `RouteSearchEvents`; Leitfaden `APIShared/docs/PATHFINDING_RULES.md`).
- **Script Extender** ab 2.14.0.
- **Native DLL:** an `CrusaderDE.dll` mit SHA-256 `FBCB9319…2831E2` gebunden. Verträge und Adressen: [UpdateToNewDLL.md](UpdateToNewDLL.md).
- Mit MoatMove läuft BugfixesAndQoLs Befehlslogik im Required-only-Ablauf. Dort ist BugfixesAndQoLs gewichtete Routenveröffentlichung aus (`TryCaptureBuilderWeightedScope`, einschließlich der gewichteten Auffüll-Optimierung); die gewichteten Gruppenrouten kommen aus dem Anker-Kern.

## Einstellungen

BepInEx-Konfiguration `BepInEx/config/MoatMove_Serp.cfg`, Abschnitt `[Movement]`. Änderungen wirken nach einem Neustart. Im Mehrspieler müssen alle Spieler dieselben Werte haben (keine Synchronisierung).

| Einstellung | Standard | Wirkung |
|---|---|---|
| `AnchorSingleMoves` | `false` | Einzelbefehle außerhalb von Gruppen nutzen den Kern (exaktes A*, Routenregeln) statt Vanillas Suche je Einheit. |
| `AvoidFire` | `false` | Feuer am Boden wird als teure Regelkachel eingetragen. Mit `AnchorSingleMoves` planen laufende Soldaten vor neuem Feuer um oder warten; ohne feuerfreien Weg wird die Bewegung abgebrochen. |
| `FireWaitSeconds` | `60` | Mit `AvoidFire`: Wartezeit vor Feuer bis zum Abbruch und Fenster, in dem ein geflohener Soldat sein altes Bewegungsziel fortsetzt (Sekunden Spielzeit, 1–1800). |
| `FireReturnSeconds` | `120` | Mit `EscapeFire`: Fenster, in dem ein Soldat, der im Feuer stand, an seine alte Position zurückkehrt; es beginnt an der ersten feuerfreien Kachel (Sekunden Spielzeit, 1–1800). |
| `FireDetourPercent` | `50` | Mit `AvoidFire`: erlaubter Umweg in Prozent der Restroute (0–1000). |
| `FireDetourMinTiles` | `10` | Mit `AvoidFire`: immer erlaubte zusätzliche Schritte (0–2000). |
| `EscapeFire` | `true` | Mit `AvoidFire`: Soldaten auf brennenden Kacheln gehen zur nächsten feuerfreien Kachel und kehren später zurück. |
| `AvoidPlague` | `false` | Kacheln, auf denen eine Pestwolke Einheiten Schaden machen würde, werden als teure Regelkacheln eingetragen; laufende Soldaten planen um oder warten und brechen ab wie beim Feuer. |
| `PlagueWaitSeconds` | `90` | Mit `AvoidPlague`: Wartezeit vor Pest bis zum Abbruch und Fenster, in dem ein Geflohener sein Bewegungsziel fortsetzt (1–1800). |
| `PlagueReturnSeconds` | `180` | Mit `EscapePlague`: Rückkehrfenster für Soldaten, die in der Pest standen, ab der ersten sicheren Kachel (1–1800). |
| `EscapePlague` | `true` | Mit `AvoidPlague`: Soldaten verlassen Kacheln, auf denen die Pest ihnen schadet. |
| `EscapePlagueRaised` | `false` | Mit `EscapePlague`: Auch Soldaten auf Mauer, Treppe, Tor, Turm oder Bergfried fliehen; sonst bleiben sie auf ihrem Posten. |

Nur im DEVDIAG-Build (`build.bat /devdiag`, Abschnitt `[Diagnostics]`): `RuleProbeTicks`, `MoatCostProbeTicks`, `TopologyProbeTicks` (Selbstprüfungen der Regeln und der Cursor-Topologie, 0 = aus).

Es gibt keine Moduswahl mehr. Ein alter Eintrag `Mode` in einer vorhandenen Konfiguration wird ignoriert.

## Ablauf

### Gruppenbefehle (Anker-Kern)

- **Einstieg:** `TribeR3EventHooks.OnTribeIssueOrderMoveHere` (Pre/Post um `0x11B520`), nur auf dem Simulationsthread, für menschliche Spieler und KI.
- **Masken:**
  - Je Spieler, der lebende Einheiten steuert, hält der Kern Kanten- und Grabenbits aus demselben Prädikat wie Fast (`LiveEdge`).
  - Die erste Spielerprüfung einer Karte (Kartenstart, Laden) baut ihre Masken im selben Tick vollständig, noch in der Ladephase. Die Logzeile `anchor masks built at map start` nennt Knoten und ms.
    - **Grund:** Solange eine Maske fehlt, bekommt jeder Gruppenbefehl dieses Spielers eine Vanilla-Suche je Mitglied. Bei 10.000 Einheiten und KI-Gruppen mit ~600 Mitgliedern waren das 2,7–3,7 s Simulationsstillstand je KI-Befehl in den ersten ~25 s.
    - **Kosten:** Der vollständige Bau für 8 Spieler auf 800×800 kostete in einem DevDiag-Build etwa 1,9 s. Eine längere Ladezeit ist dem Nutzer lieber (Entscheidung 02.11.2026).
  - Spätere Anforderungen bauen mit 4.096 Knoten je Tick, etwa ein Spieler, der erst später Einheiten bekommt. Neulesen nach Änderungen läuft mit 1.024 Knoten je Tick.
  - Spielerunabhängige Knoten teilen ihre Wörter.
  - Kachelmeldungen von BugfixesAndQoL lesen nur das ±2-Umfeld neu; Zugangsänderungen nur die Grundflächen betroffener Torgebäude.
- **Aufgaben je Befehl:** Das Pre erfasst Starts und Profil. Es gibt eine gewichtete Aufgabe für grabenfähige Mitglieder und eine reine Bodenaufgabe für alle übrigen. Das Feld entsteht bei der ersten Abfrage eines Mitglieds.
- **Abfrage je Einheit:**
  - Abstieg im Ankerfeld, dazu ein exaktes Endstück zum eigenen Platz (Rand 8).
  - Für Starts außerhalb des Hauptfelds gibt es Zusatzanker, höchstens 48. Darüber gilt die Abfrage als „über der Grenze“, und die Einheit behält Vanilla.
  - Formationsziele werden gebündelt beantwortet (`mma_query_batch`), mit denselben Antworten wie Einzelabfragen.
- **Veröffentlichung:**
  - Ein günstigerer Grabenweg qualifiziert die Einheit (Moduserkennung).
  - Eine Bodenentscheidung ersetzt Vanillas `F4930` über `TryReplaceUnsafeFallbackPath`.
  - Jede Route wird live geprüft: Identität, Start, Ziel, Puffer, alle Kanten und Eigentümer.
  - Innerhalb eines Gruppenbefehls merkt sich MoatMove bereits geprüfte Kanten (`AnchorEdgeMemo`): Die Mitglieder laufen durch dieselben Korridore, Treffer liegen bei ~98 %. Jede gemeldete Kartenänderung, Regeländerung, Markierung und jeder neue Befehl beginnt neu; DEVDIAG-Builds prüfen jeden Treffer live nach (`anchor-breakdown` `edgeMemo`).
- **Qualität:** Offline sind die Wege fast optimal; die Mehrkosten liegen im 95-%-Perzentil bei höchstens 6 % gegenüber einer exakten Suche je Einheit (GroupPathProbe).
- **Ausnahmen mit Fast bzw. Vanilla:**
  - Angriffe, Arbeitsaufträge, Wegpunkte und Patrouillen;
  - die Zeit, bis die Maske eines später angeforderten Spielers fertig ist, und Bündniswechsel;
  - Abfragen, die der Kern nicht beantworten kann.
- **Kern** (`native/MoatMoveAnchor.cpp`, ABI-Vertrag `0x0400000F`):
  - Threads: physische Kerne − 2, mindestens 1, höchstens 8; die Ergebnisse hängen nicht von der Threadzahl ab.
  - Speichergrenze 768 MiB, Wege höchstens 2.000 Schritte, Kantenkosten höchstens 255.

### Einzelbefehle (`AnchorSingleMoves`)

- Der Kern rechnet ein exaktes A* statt Vanillas Suche.
- Ein deterministisches Budget von 500.000 Knoten je Tick begrenzt den schlechtesten Fall.
- Über dem Budget behält ein Bodenbefehl Vanillas Weg. Assassinen werden angerechnet, aber nicht an Vanilla übergeben, weil deren Vanilla-Kette teurer ist.

### Wiederverwendung fehlgeschlagener Suchen

Beide Arten laufen nur mit geladenem Kern:
- **Bodensuchen** (`GroundFailureProofs`): Innerhalb einer Gruppenbewegung werden Starts aus einer bewiesen geschlossenen Fehlschlagmenge ohne `DAAC0`/`DA590` beantwortet.
- **Grabenmodus-Suchen** (`MoatFailureProofs`): Starts in der Komponente eines gescheiterten `DAFD0` werden ohne erneutes `DAFD0` beantwortet, solange sich die Randkacheln nicht geändert haben. Das wird bei jeder Nutzung über einen Fingerabdruck geprüft.

Beide sind an die geprüften Bytes gebunden und schalten sich bei Abweichungen ab.

### Fast (Rückfallebene)

- Gerichtete, ungewichtete Rückwärtsfelder (`FastRouteField`) in einem gemeinsamen Pool von 64 MiB. Ein Graben wird nur genommen, wenn kein Bodenweg existiert.
- Fast dient für Grabenarbeit (Ausheben, Auffüllen) und für die Cursor-Antwort beim Überfahren.
- Fehlt der Kern oder scheitert sein Start, übernimmt Fast auch die Gruppenbefehle. Dann werden Spielerbewegungen vor `0x196100` zurückgestellt und mit 8.192 Expansionen je Tick berechnet.

### Feuer (`AvoidFire`)

- `FireRouteRules` liest alle 4 Ticks Vanillas Effektmanager (Typ 9 im Zustand 2: brennendes Pech, Feuerpfeile und -töpfe, brennende Gebäude). Es trägt diese Kacheln für alle Spieler als Regelkacheln mit der größten Strafe ein und führt sie zusätzlich als Bitfeld für die Flucht. Neue Befehle umgehen Feuer, sobald irgendein Umweg existiert.
- **Nur Soldaten:** Umplanen, Warten und Flucht gelten nur für Einheiten mit `r_SelectionAllowed` (Militär), ohne Belagerungsgerät.
  - Ein gestoppter Feuerwehrmann löscht sein Gebäude aus der Ferne (`0x159C30` → `0xC3C80`).
  - Arbeiter werten ein erzwungenes Routenende als „Ziel erreicht“ (`0x188340`).
  - Beide behalten deshalb die Kern-Route mit Feuerstrafe.
- `RouteReplanWatch` (nur mit `AnchorSingleMoves`):
  - Laufende Soldaten planen 3 Schritte vor dem Feuer zum selben Ziel um. Erlaubt ist ein Umweg von `FireDetourPercent` % bzw. mindestens `FireDetourMinTiles` Schritten.
  - Ist kein solcher Umweg möglich, halten sie auf dem nächsten Feld und warten bis zu `FireWaitSeconds`. Findet eine Prüfung (alle 40 Ticks, Budget je Tick) einen feuerfreien Weg, laufen sie weiter.
  - **Weiterlaufen als Gruppenbefehl:** Der ursprüngliche Befehl geht als Vanilla-Gruppenbefehl (`IssueMoveHereCommand` → `0x11B520`) mit seinen eigenen Argumenten erneut an den ganzen Tribe, einmal je Tribe. Nur so setzt Vanilla wieder Laufzustand und Animation; `0x196280` allein veröffentlicht nur den Weg, die Einheit gleitet. Noch blockierte Mitglieder hält der Wächter erneut an.
    - MoatMove merkt sich pro Tribe den zuletzt beobachteten Bewegungs- oder Zielbefehl (Extender-Events im Simulationsthread). Wiederholt wird nur, solange kein neuerer Befehl kam und der Tribe (Global-ID, Besitzer) noch lebt.
    - **Einfache Befehle** (ein normaler Spielerbefehl über `0x196100`, nicht in die Warteschlange gestellt, keine Patrouille, zusätzlich noch Vanillas Zwei-Punkte-Befehl zu diesem Ziel; oder ein KI-Befehl) gehen als Gruppenbefehl erneut an den Tribe.
    - **Andere Spielerbefehle**, vor allem Formationen von BugfixesAndQoL (`EnableMoveFormationEnhancements`, sie rufen `0x11B520` direkt auf): Jedes wartende Mitglied läuft allein in einem eigenen Tribe per Vanilla-Befehl zu genau seinem bisherigen Platz und kommt bei Ankunft (oder bei einem neuen Befehl) in seine Gruppe zurück. So bleibt die Formation erhalten; ein Gruppenbefehl würde Vanillas Aufstellung erzeugen. Zähler `solo(resumed, rejoined, active)`.
    - **Befehle aus der Shift-Warteschlange** (Warteschlangen-Markierung im Bewegungstyp oder in `0x196100`) werden nicht wiederholt, weil das im Mehrspieler die Warteschlange verwirft. Interne Etappen der BugfixesAndQoL-Warteschlange sind von außen nicht als solche erkennbar und laufen wie Formationen einzeln weiter.
    - Gruppen, die nicht weiterlaufen, zählt `held`; `heldWhy(newer,group,points,refused,notMoving,queued)` nennt den Grund: neuerer Befehl, Gruppe weg oder Mitglied ausgetreten, kein Zwei-Punkte-Befehl mehr, Vanilla lehnte ab, Mitglied nicht im Laufzustand, Warteschlange.
  - Am Ende der Wartezeit wird die Bewegung abgebrochen: Die Einheit bleibt stehen (Zähler `aborted`) und läuft nicht durch das Feuer. Standard sind 60 Sekunden: Ein Pechgraben brennt je Kachel 22–27 Sekunden, durch Ausbreitung und Wiederentzündung oft länger.
  - Scheitert ein Umweg (`0x196280` liefert 0 und nullt den Routenpuffer), wird die Einheit sauber auf ihrem Bewegungsstart gestoppt und wartet (Zähler `moveFailures`).
- `FireEscape` (`EscapeFire`, braucht `AnchorSingleMoves` nicht):
  - Alle 8 Ticks werden Soldaten auf brennenden Kacheln gesucht; Kacheln, auf denen Vanilla keinen Feuerschaden macht (Flags `0xBA7001B1`: Mauer, erhöht, Zinnen, Furt usw.), sind ausgenommen.
  - **Ziel:** die nächste begehbare Kachel ohne Feuer im Umkreis von 12, die weder unverbranntes Pech (`TilePropertyFlag.PitchTrap`) ist noch an Feuer grenzt. Reihenfolge: so wenig Feuerkacheln auf dem Weg wie möglich, dann ein Ziel abseits der Feuerfront, dann so wenige Schritte wie möglich. Gibt es im Umkreis nur Ziele an der Feuerfront, gilt das nächste davon. So läuft der Soldat nicht vor sich ausbreitendem Pechfeuer her.
  - Der Weg hinaus führt zwangsläufig durch Feuer; fliehende Einheiten sind deshalb vom Wächter ausgenommen.
  - **Bewegung:** Der Soldat bekommt für die Flucht einen eigenen Tribe (`Create`, `Unassign`, `Assign` mit Rückabwicklung) und einen Vanilla-Gruppenbefehl wie ein normaler Spielerbefehl (`IssueMoveHereCommand(…, false, 1, DefaultInSync)`). Steht er allein in seinem Tribe, behält er ihn. Nimmt Vanilla ihn nicht in den Laufzustand (`r_AIState` 101 bzw. 122), gilt die Flucht als gescheitert.
  - **Ende:** Er kommt in seine Gruppe zurück, wenn diese noch existiert (gleiche Global-ID und Besitzer); der leere Hilfs-Tribe wird gelöscht. Ein KI-Soldat ohne Gruppe verlässt den Hilfs-Tribe, damit Vanillas Reparatur (`0x118660`) ihm einen KI-Tribe gibt; ein Spieler-Soldat ohne Gruppe behält ihn. Das gilt für Spieler- und KI-Soldaten. Die Haltung der Gruppe wird übernommen; eine aggressive Haltung kann den Soldaten zum Angriff bewegen, was die Flucht als neuen Befehl beendet.
  - **Rückkehr:** Stand der Soldat, geht er an seine alte Position (Fenster `FireReturnSeconds`); war er unterwegs oder wartete er vor Feuer, setzt er sein altes Ziel fort (Fenster `FireWaitSeconds`). Das Fenster beginnt an der ersten feuerfreien Kachel; ein erneutes Ausweichen startet es nicht neu. Zurück geht es, sobald das Ziel nicht mehr brennt und der Kern einen feuerfreien Weg innerhalb der Umweg-Regel findet. Nach dem Fenster bleibt er am Rand stehen.
  - Ein neuer Befehl (Spieler oder Kampf-KI) beendet die Flucht und sperrt eine neue für 40 Ticks; ebenso ein gescheiterter Fluchtversuch.
  - **Logzeile** `escape(...)`: `started` neue Fälle, `reescaped` erneutes Ausweichen, `returnIssued`/`returnArrived` Rückkehrbefehl und Ankunft, `stayed` Fenster abgelaufen, `endedOrder`/`endedGone` neuer Befehl bzw. Einheit weg (`endedOrderTribe`: davon Einheit nicht mehr im Hilfs-Tribe), `returnWaits(targetBurning,noRoute,tooLong,fire)` Rückkehrprüfungen, die noch nicht zurückführten (Ziel brennt, kein Weg, Umweg zu lang, Weg durch Feuer). In `replan(...)` zählt `probes(clear,noRoute,tooLong,fire)` die Prüfungen der Wartenden ebenso. `tribes(...)` zählt Abspaltungen, Rückführungen, freigegebene KI-Soldaten, Fehlschläge und nicht zugelassene Mitglieder (`notMoved`). Ein DevDiag-Build (`build.bat /devdiag`) schreibt zusätzlich je Ereignis eine Zeile `MOATMOVE_FIRE_CASE` (Flucht, Rückkehr, Fallende mit Grund, angehaltenes oder einzeln fortgesetztes Weiterlaufen) mit Ziel, Tribe, `r_AIState` und Weg der Einheit, höchstens 400 je Karte.
- Die Abbruch-Semantik gilt nur für MoatMoves eigene Feuer-Policy. Regeln anderer Mods mit `RouteReplanPolicy` setzen nach der Wartezeit wie im APIShared-Vertrag fort.

### Pest (`AvoidPlague`)

- `PlagueRouteRules` liest alle 4 Ticks die Pestwolken (Effekte vom Typ 22, `ProjectileType.Disease`, im Zustand 2; sie schaden bis zum Löschen, auch beim Ausblenden) aus demselben Effektmanager wie das Feuer.
- Eine Wolke schadet alle 31 Ticks im 5×5-Feld um ihre Kachel. `PlagueDamageModel` entscheidet je Kachel, ob eine dort stehende Einheit Schaden nähme:
  - Ohne APIShared-Höhenpolitik (Vanilla): Nachbarn und Ring nur bei Höhenunterschied < 120 (`0x6B910`); Mauer-, Treppen- und erhöhte Kacheln sowie Bergfried, Tore und Türme sind immun.
  - Mit aktiver Politik (z. B. ExtraFeatures `PlagueDamageWalls/Gates/Towers/Keeps`): Einordnung der Kachel wie im APIShared-Stub; eine abgeschaltete Kategorie bleibt immun, sonst zählt der Standhöhenunterschied gegen die Toleranz.
  - Die wirksame Politik liest MoatMove über `ModApiClient.TryGetPlagueHeightPolicy` (APIShared). Ohne aktiven Besitzer oder mit einem APIShared ohne diese Funktion gelten Vanilla-Regeln; die Logzeile zeigt `heightPolicy=APIShared` bzw. `Vanilla`.
- Diese Kacheln werden zu Regelkacheln (Strafe, eigene Policy mit `PlagueWaitSeconds`, Abbruch nach der Wartezeit wie beim Feuer). Die Kachel des nächsten Wolkenschritts wird mit eingetragen; ein 7×7-Rand um jede Wolke gilt bei der Fluchtzielwahl als Front, weil die Wolke wandert.
- Die Flucht (`EscapePlague`) läuft wie beim Feuer, mit den Pest-Fenstern; Fluchtziel und Rückweg müssen frei von Feuer und Pest sein. Ausgenommen sind der Lord und der Heiler (pestimmun) sowie ohne `EscapePlagueRaised` Soldaten auf erhöhten Kacheln.
- Entfernt ein Apotheker eine Wolke oder endet sie, verschwinden ihre Kacheln beim nächsten Scan; Wartende und Geflohene merken das an ihrer nächsten Prüfung (alle 40 Ticks).
- Logzeile: `plague(clouds, tiles, near, heightPolicy)`; in `escape(...)` zählt `startedPlague` die Pestfluchten.

### Regeln anderer Mods

- `RouteTileRules` (Sperre oder Strafe je Kachel und Spieler) und `RouteSearchEvents` (Kantenkosten einer Suche) wirken im Kern und in Fasts Kanten.
- Vanillas eigene Suchen kennen die Regeln nicht.

### Cursor und Grabenplatzierung

- **Cursor-Antwort beim Überfahren:** Liegen Start und Ziel in derselben positiven Bodenregion, ist die Antwort sofort positiv; sonst rechnen Fasts Cursor-Felder (eigener Pool, getrennt von der Simulation).
- **Grabenplatzierung:** nutzt die Cursor-Topologie (Regionsgraph über Graben- und Portalkanten). Sie wird tabellenbasiert gebaut und beim Kartenstart vorgebaut.

### Speichern und Laden

- Zurückgestellte Fast-Befehle werden im Spielstand gespeichert (`ModSaveDataAPI`, Schlüssel `MoatMove_Serp`).
- Masken und Felder entstehen nach dem Laden neu.
- Offene Bewegungsfälle (Feuer- und Pestfluchten, Solo-Fortsetzungen, Wartefälle samt beobachteten Tribe-Befehlen) speichert ein versionierter Datensatz über APIShared `VersionedSaveData` (`MoatMove_Serp.MovementCases`, siehe `src/MovementCaseSave.cs`).
- Nach dem Laden wird der Datensatz im ersten Simulationstick angewendet. Jeder Fall wird gegen die gespeicherten globalen IDs von Einheit und Tribes geprüft; ein Fall, der nicht weiterlaufen kann, endet sofort und führt den Soldaten in seine ursprüngliche Gruppe zurück.

## Mehrspieler

- Alle Entscheidungen hängen nur vom Simulationszustand, den Ticks und den Einheiten-IDs ab. Budgets zählen Knoten, nie Zeit.
- Die Threadzahl ändert keine Ergebnisse.
- Die Einstellungen werden nicht synchronisiert; alle Spieler brauchen dieselbe Konfiguration und denselben Build.

## Build, Tests und Messung

- **`build.bat`:**
  - Preflight: `Test-SharedBoundaries.ps1`, `Test-UnitCommandSplit.ps1 -ModDirectory Testmods/MoatMove`, `tests/Test-Preflight.ps1`;
  - Tests: `tests/MoatMove.Tests`;
  - MSBuild und der native Kern: `native/Build-MoatMoveAnchor.ps1` baut nach `.local/anchor/` und kopiert hashgeprüft ins Paket;
  - Installation: `tests/Install-Package.ps1`.
- **`tests/MoatMove.Tests`:** kompiliert die echten Quellen von MoatMove und der BugfixesAndQoL-Befehlslogik gegen Fixtures. Enthalten sind auch der Integrationstest mit der echten Kern-DLL (`AnchorIntegrationTests.cs`) und die Cursor-, Platzierungs- und Feld-Tests.
- **Kernverträge:** `Testmods/GroupPathProbe` mit `--anchor-core` prüft unabhängige Vergleiche, Thread-Unabhängigkeit und Batch-Gleichheit.
- **Messläufe im Spiel:** `tools/Run-PathfindingIteration.ps1` und `tools/Analyze-PathfindingRun.ps1` (mit PerfProbe); Ergebnisse unter `.local/runs/`. Übersicht über alle Läufe: `PathfindingRoadmap.md` und `PathfindingFinalReport.md` im Workspace.
- **Leistungszeilen im Log:** `stage=anchor-performance`, `stage=anchor-breakdown` (darin `replan(...)`, `escape(...)` und `tribes(...)`), `stage=fast-performance`. Die Anchor-Zeilen erscheinen bei Gruppenbefehlen und zusätzlich, solange Feuer brennt oder Warte- und Fluchtfälle offen sind.

## Offen

- Spieltest mit Augen (Gruppenwege, Feuer, Assassinen) und ein echter Mehrspielertest.
- Ingame-Modsettings mit Host-Synchronisierung statt der Konfigurationsdatei.
- Die BugfixesAndQoL-Mindestversion in `info.json` und im `BepInDependency`-Attribut (1.0.181) entspricht nicht dem benötigten Stand: Die interne Provider-Schnittstelle stammt aus dem aktuellen BugfixesAndQoL.
- Ziel auf einer gesperrten Kachel gilt als unerreichbar (Vanilla übernimmt); extreme Regeln (kartenweite Sperre) erzeugen noch einzelne Lücken von 120–130 ms.
