# Fixplan: neue Einheiten in bestehende KI-Gruppen aufnehmen

## Umsetzungsstand (18.11.2026, ersetzt den Plan weiter unten)

Umgesetzt wurde ein einfacherer Weg als der unten stehende Journal-/Hook-Plan. Der Nutzer hatte bessere Ideen ausdrücklich zugelassen.
Der ursprüngliche Plan bleibt unten als Analysebeleg erhalten. Seine Abschnitte „Umsetzung in APIShared“ und „Tests“ sind dadurch überholt.

**Audit (Hash `FBCB9319…31E2`, statisch, Details in `_inspect/.../knowledge/AI_MILITARY_ADMISSION.md` → „Controller handoff of joined recruits“):**
- Vanilla gibt eigenen Rekruten nie einen Befehl. Alle Klassifizierer (`291F0`, `292F0`, `29360`, `29430`, `294B0`) und `303E0`/`30310` schreiben nur Rolle und Mitgliedschaft (`11D370`).
- Den Befehl gibt der nächste reguläre KI-Durchlauf. Burgverteidigung (`2FE20` → `3D9E0/3DEE0/3DB30/3DD60` → `3E850/3E940/3E9A0` → `11DD10`) und Heimatgraben (`3E700`) laufen bei jedem Durchlauf, Patrouille (`3EA50`) alle `defense_patrol_delay` Durchläufe. Wirtschaftsschutz und Eingreifreserve (`3E070`/`3D840`) laufen bei jedem Durchlauf, erteilen unterhalb der AIC-Stärke aber keinen Befehl.
- `11DD10` markiert Mitglieder, die schon am Ziel sind; `11B520` überspringt markierte Mitglieder. Ein entfernter Neuzugang wird also hingeschickt, Altmitglieder am Posten bekommen nichts Neues.

**Code (APIShared `dev/Serpens66`, nicht committet):**
- `AiMilitaryService.Apply`: Hat eine bestehende Gruppe keinen beobachteten Auftrag und gibt es keinen Leergruppen-Startauftrag, werden CastleDefense, DefensePatrol, HomeMoat, EconomyProtection und Intervention wie ein Vanilla-Rekrut aufgenommen: nur Mitgliedschaft und Rolle, `Applied` mit `Detail`. Regel und Beleg stehen in `Orders/AiControllerHandoffPolicy.cs`.
- Harassment und Belagerungsrollen verlangen weiterhin einen beobachteten Auftrag. Ihre Ablehnung nennt jetzt Ledgergrund, Fallbackgrund und Zielgruppe (ID/GID/Besitzer/Mitglieder).
- Pending-Index (`pendingWork`): `Process` bearbeitet nur wartende Anfragen statt bei jedem Durchlauf alle verfolgten Einheiten. Das macht Mehrfachplatzierung (Shift) effizient.
- Doku `APIShared/docs/AI_MILITARY.md` angepasst. Kein neuer nativer Hook, keine Überwachung, kein Polling.

**Offline geprüft:** APIShared kompiliert. APISharedTests `AiMilitary*` (16), `AiMilitary.NativeTests`, `AiRecruitOrders.Tests`, `AiMilitary.AttackTests` und `MapRecordReadiness.Tests` sind grün.

### Noch offen
- [x] Spieltest 17.11.2026 22:28, Testmod `Testmods/AiGroupAdmissionTest` (Szenario `ai-groups`, `test AI.map`, KI Rat als Spieler 2, Tempo 250, Friedenszeit). Nachweis: `.work/t/aigroups/001/35723c59d98a/` (LogOutput.log, Test-Save). Ergebnis: alle Verteidigungsrollen (CastleDefense, DefensePatrol, HomeMoat, EconomyProtection, Intervention) in frischer Partie und nach dem Laden `Applied`, mit und ohne Übergabe; nach 3000 Ticks fast alle Neuzugänge in derselben Gruppe mit gleicher Rolle und bewegt. Ursprünglicher Prüfauftrag:
  Spieltest (Testmod unter `Testmods/`, Speed 250–300, Friedenszeit): Armbrustschützen in eine **bereits besetzte** Gruppe aufnehmen, für CastleDefense, DefensePatrol und EconomyProtection (Regular und Additional), in neuer Partie und mit geladenem `test_diagonal.sav` (Owner 7). Nachweisen: `Applied`, Mitgliedschaft und dass der Neuzugang beim nächsten Durchlauf zum Gruppenziel läuft, während Altmitglieder keinen neuen Befehl bekommen. Dabei die tatsächliche Durchlauffrequenz pro Spieler messen; statisch war sie nicht bestimmbar.
- [x] Mehrfachplatzierung (4 Bogenschützen in einem Erfassungsbereich, wie Shift) für CastleDefense, DefensePatrol und EconomyProtection: alle `Applied`, Burgverteidiger auf mehrere Gruppen verteilt.
- [ ] Im Spiel prüfen: Wird ein frisch platzierter Rekrut von `11DD10`/`1245B0` womöglich über `GameUnit+0xAC` markiert? Das würde Vanilla-Rekruten genauso betreffen, ist aber unbestätigt. Ebenso Fixes' erweiterte Verteidigungsslots (> 10).
- [x] (erledigt 18.11.2026, siehe unten) Harassment- und Belagerungsrollen ohne beobachteten Auftrag werden weiter abgelehnt. Eine Freigabe braucht einen eigenen Controller-Audit (`3E200` cacht Gebäudeziele, Belagerungs-Dispatch `122B40`).
  Spieltest bestätigt das als echte Lücke: Schon in einer frischen Partie wird der **zweite** Rekrut einer Harassment- oder Belagerungsgruppe (Moat, Assassins, Diversion, Ladders, Tunnelers, Cover, Reserve, Wall) mit `CurrentOrderUnknown` abgelehnt, weil der leere-Gruppen-Startauftrag des ersten Rekruten nicht als Gruppenauftrag im Ledger steht. Harassment nach dem Laden ging nur, weil die KI inzwischen selbst befohlen hatte.
- [x] Harassment/Belagerung, zweiter Rekrut derselben neuen Gruppe: behoben (APIShared `AiOrderLedger.RecordAdmissionOrder`, Startbefehl der leeren Gruppe gilt als Gruppenbefehl). Spieltests 17.11.2026 23:03 und 23:05 (`.work/t/aigroups/001/6e1a8d8cff2d`, `e2e6edcd172d`): keine Harassment-/Belagerungsablehnung mehr in frischer Partie.
- [x] (erledigt 18.11.2026, siehe unten) Harassment/Belagerung nach dem Laden weiter abgelehnt, wenn die KI der Gruppe inzwischen nicht nachspielbare Befehle gab (Einzelbewegungen, unbekannte Zielbefehle): Ledger korrekt leer (fail-closed). Freigabe braucht den Controller-Audit (Harassment `3E200` befiehlt die ganze Gruppe über `11DD10`/`11E960` mit Countdown 100, Belagerung `122B40` je Phase).
- [x] (erledigt 18.11.2026, siehe unten) Burgverteidigung, belegt im Lauf `e2e6edcd172d`: (1) Speerträger Klasse 8 mit 3 Positionen: `2BC40` liefert 0 (Fenster ≤ 0 durch `max_mobile_groups`), `GetOptions` meldet das nicht. (2) Weg zur Verteidigungsposition scheitert (`196280` raw=0): Bogenschützen zu Mauer-/Turmkachel 641,388 (Flags 0x40008000), Reiter/Kamele zu 629,409 (Flags 0x10008000). Vanilla prüft die Erreichbarkeit (`3E850` → `3E650`/`E2610`) und fällt auf Burg +3/+4 (`3E940`) zurück, APIShared nicht (Abweichung D2). (3) Kaskade: Eine gescheiterte Aufnahme lässt die neu belegte Gruppe im Zustand 3 zurück, `2BC40` gibt sie im selben Durchlauf weiter zurück, alle folgenden Rekruten dieser Klasse scheitern. Dazu D1 (Platz-1-Gruppen: Vanilla `3E9A0` statt Burg +3/+4).
- [x] (erledigt 18.11.2026, siehe unten) Burgverteidigung im Spieltest einzeln abgelehnt: Speerträger `NoAvailableGroup` („Native defensive allocation failed.“), Arabischer Schleuderer `AssignmentFailed` („The actual native recruit route did not succeed exactly once.“). Ursache klären (Slot-/Klassengrenze der Verteidigungsallokation bzw. Rekrutierungsweg).
- [x] Burgverteidigung (18.11.2026): (1) Ursache der Speerträger-Ablehnung: APIShared übergab `2BC40` den Spielermanager `366C210` statt des KI-Lord-Managers (AIC-Array `404C950`, `LEA RCX` bei `57452` vor `2AE40`); `max_mobile_groups` las dort 0. Korrigiert, `GetOptions` prüft zusätzlich das deterministische Fenster ≤ 0. (2)/(3) und D1/D2 umgesetzt: Startziel wie `2FE20` (`3E940`, Burgtür `3E9A0`, `3E850` mit Erreichbarkeit), Rekrut ohne Route tritt wie ein Vanilla-Rekrut bei, leere Zielgruppe wird nach gescheiterter Aufnahme wiederhergestellt. Spieltest `5ca1626871b8`: frische Runde 190/190 Applied.
- [x] Harassment/Belagerung nach dem Laden (18.11.2026): Ledger-Trace im Testmod zeigt, dass die Einträge schon vor dem Speichern durch normale Einzelrouten der Mitglieder (`196280` außerhalb des Gruppenbefehls) verloren gehen, nicht bei Capture/Restore. Controller-Audit: `3E200` befiehlt Harassment nur gruppenweit, `3C2E0` schickt bei Belagerungszustand 0 jede Belagerungsgruppe jeden Durchlauf heim (`3D560`) bzw. bewegt alle (`3D020`/`3D6E0`). `AiControllerHandoffPolicy` deckt daher Harassment immer und Belagerungsrollen bei `r_AISiegeState == 0` ab; Harassment-Ingenieure und laufende Belagerungen bleiben explizit. Spieltest `c87312021877` (18.11.2026 01:10): frisch 190/190, nach dem Laden 46/46 Applied (34 Übergaben), alle Übergabe-Rekruten nach 3000 Ticks in Gruppe/Rolle und von der KI bewegt; Testmod-Verdict PASS. Der Runner verlor beim Start die Prozesszuordnung (RESTORE_FAILED, Spiel lief weiter); Wiederherstellung von Konfiguration, Einstellungen, Log und Testmod manuell aus `SerpAutomationRollback` durchgeführt.
- [x] Harassment-Ingenieure (18.11.2026): Die Angabe oben, sie verlangten einen beobachteten Auftrag, war falsch. `Apply` nimmt sie wie Belagerungsingenieure und `None` ohne Ledgerauftrag auf; im Lauf `c87312021877` bestätigt. Kommentar in `AiControllerHandoffPolicy` und `docs/AI_MILITARY.md` berichtigt.
- [x] Belagerungsrollen während einer laufenden Belagerung (18.11.2026): Statischer Audit von `3C2E0` je Belagerungszustand (Baseline `AI_MILITARY_ADMISSION.md` → „Active siege controller“). Vanilla rekrutiert ab Zustand ≠ 0 keine Belagerungstruppen mehr (`40740`). Ein untätiger Neuzugang bekommt innerhalb weniger Durchläufe einen Befehl: Storm in 3–5 (`3CE40`), Shock in 4–5 (`3CDC0`/`122B40`), Moat/Assassins/Diversion/Cover/Reserve/Wall in 5. Diese Paare nimmt `AiControllerHandoffPolicy` wie Vanilla-Rekruten auf (übrige Zustände: siehe Nutzerentscheidung unten). Nur statisch belegt; die laufende Belagerung ist im Spieltest nicht abgedeckt (Testmod läuft mit Friedenszeit).
- [x] Nutzerentscheidung 18.11.2026: Platzierte Einheiten sollen spätestens bei der nächsten relevanten Phase aufgenommen werden. `AiControllerHandoffPolicy` nimmt Belagerungsrollen ohne beobachteten Auftrag daher in jedem Belagerungszustand 0–9 auf; wo `3C2E0` sie nicht innerhalb weniger Durchläufe erreicht, meldet `Detail` das Warten auf die nächste Phase, den Rückzug (`3D3E0`) oder die Heimkehr (`3D560`). Nicht im Spiel getestet, weil der Testmod mit Friedenszeit keine Belagerung erreicht. Regressions-Spieltest `.work/t/aigroups/001/51903af6ba0f` (18.11.2026 11:31, Belagerungszustand 0): frisch 190/190, nach dem Laden 46/46 Applied, Verdict PASS; der reparierte Runner stellte selbst wieder her.
- [ ] Ungeklärt: Platzierungen ohne erfasste primäre Einheit (`SPECTATOR_AI_PLACEMENT_EMPTY`). In keinem der Spieltestlogs `.work/t/aigroups/001/*` aufgetreten. Die Warnung bedeutet, dass die native Platzierung keine Einheit erzeugt hat (kein Gruppenfehler).
- [x] Changelogs und Mindestversion (17.11.2026, 22:06): Änderung in die unveröffentlichten Einträge APIShared 0.56.1 und Spectator 0.2.8 aufgenommen (letzte Releases: v0.56.0 bzw. v0.2.5), daher kein weiterer Versionssprung. Spectator verlangt jetzt APIShared 0.56.1 (`info.json` und `BepInDependency`). Beide mit vollständigen Tests gebaut und installiert (APIShared-Plugins und -Patcher gemeinsam).

---

## Ursprünglicher Plan (überholt, als Analysebeleg erhalten)

Stand: 17.11.2026. Auftrag: Analyse und Übergabeplan; die Umsetzung übernimmt ein anderer Chat.
Dieser Auftrag ändert ausschließlich diese TODO, keinen Runtime-Code und keine Installation.

## Ziel und verbindliche Entscheidungen

- Neu platzierte Einheiten müssen passende **bereits besetzte** KI-Gruppen ergänzen und deren wirksame Aufgabe übernehmen. Die Aufnahme darf nicht nur mit leeren Gruppen funktionieren.
- Der Kernfix gehört in APIShared. Spectator bleibt Verbraucher der öffentlichen Capability und zeigt den Aufnahmestatus an.
- Die vorhandenen Gruppenmitglieder erhalten durch die Aufnahme keinen neuen Gruppenbefehl. Nur der Neuzugang bekommt Mitgliedschaft, Rolle und seine eigene korrekte Initialisierung/Route.
- Regulär und zusätzlich behalten ihre bisherigen Bedeutungen. StartConditions, geschützte Platzierung, Rollenfreigaben und MP-Autorisierung bleiben erhalten. Keine neue UI für das Umgruppieren beliebiger vorhandener Einheiten.
- Nutzerentscheidung: Wenn insbesondere ein alter Save kein sicher bestimmbares Patrouillenziel enthält, darf die neue Einheit mit sichtbarem Wartestatus **begrenzt auf eine natürliche KI-Aktualisierung warten**. Keine erzwungene Neuberechnung des Gruppenauftrags.
- Nutzerentscheidung: **Keine Überwachung aller bis zu 10.000 Einheiten**, keine periodischen Welt-/Mitglieder-Scans zur Auftragsermittlung. Gruppenereignisse und gezielte Wartelisten verwenden.

## Bestätigter Befund und Grenzen

Aktueller Problembeleg: Spiel-Log vom 17.11.2026, Spectator 0.2.8, 18:37:25–18:37:47:

- 31 Ablehnungen von `CHIMP_TYPE_XBOWMAN`, Besitzer 7: Burgverteidigung 12, Verteidigungspatrouille 15, Wirtschaftsschutz 4; regulär und zusätzlich betroffen.
- Stets `CurrentOrderUnknown`, Detail `Initial orders apply only to a current, empty group.` Zwei weitere Versuche erzeugten bereits nativ keine erfasste primäre Einheit; deren Ursache ist damit nicht erklärt.
- `AiMilitaryService.Assignment.cs`: Nach erfolgreicher Gruppenauswahl verlangt `Apply` einen Ledgerauftrag. Bei Miss bleibt nur `TryGetInitialOrderForEmptyGroup`; dessen Prüfung scheitert bei einer besetzten oder nicht mehr aktuellen Gruppe. Der Fallback überschreibt den ursprünglichen Ledgergrund.
- Das Log enthält noch keine Zielgruppen-ID/GID und keinen ursprünglichen Ledgergrund. Daher ist **nicht** bewiesen, welche Invalidierung jeden der 31 Einzelversuche ausgelöst hat. Die grundsätzliche Lücke der Aufnahme ist dagegen aus dem Code und der nativen Kette bestätigt.
- `AiOrderLedger` rekonstruiert bei einem Cache-Miss nichts. Ein einzelner UnitMove außerhalb eines Gruppenbroadcasts/eigenen Aufnahme-Receipts verwirft bisher den Eintrag der gesamten Gruppe; die Ursache wird nicht gespeichert. Nicht blind diese Invalidierung entfernen oder nur vom Returnwert abhängig machen: native Seiteneffekte und echte individuelle Befehle weiterhin berücksichtigen.
- Save-Restore stellt ausschließlich gespeicherte Ledgeraufträge wieder her; fehlende/verworfene Aufträge bekommen keinen Backfill. Ein vorhandener `APIShared_AiMilitary`-Record beweist deshalb keinen vollständigen Auftragsbestand.
- Bisherige Live-Aufnahmetests erzeugten vor allem `None` und `SiegeEngineers`. Diese Pfade umgehen den fraglichen Ledgerzugriff. Frühere Membership-, Save-/Load- und UI-Erfolge sind kein Verhaltensnachweis für besetzte Verteidigungsgruppen.

### Native Auditbasis und belastbare Verträge

Die kanonische installierte DLL wurde gegen `CURRENT.json` und das Datenbankmanifest geprüft:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Vor Umsetzung erneut prüfen; die folgenden RVAs sind hashgebundene Auditnachweise, keine versionsunabhängigen Adressen.
Einstieg: [Baseline](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md), insbesondere `AI_MILITARY_ADMISSION.md`.

- Geschlossene defensive Aufrufkette: `2AE40 → 2FE20 → 3D9E0/3DEE0/3DB30/3DD60/3EA50`, Positionshilfen `3E850/3E650 → 11DD10 → 11B520/118E00`; Wirtschaftsschutz `3E070`, Eingreifreserve `3D840`, Rückzug `3E940/3E9A0`, Hilfen `303E0/30310`.
- `11D370` setzt Mitgliedschaft/Führungsreferenzen, **keinen** laufenden Auftrag. Bloßes Hinzufügen löst die Initialisierung nicht.
- `11DD10` trägt die semantische Halteposition. Wenn kein neuer Move erforderlich ist, liefert es `0` und ruft `11B520` nicht auf. Der gewünschte Halteauftrag besteht trotzdem weiter. Eine fehlende erfolgreiche Move-Beobachtung bedeutet deshalb nicht, dass die Gruppe keinen Auftrag hat.
- Defensive Aufrufe von `11B520` benutzen häufig `patrol=0, newOrder=0`: Die allgemeinen Patrol-/Zielfelder der Gruppe werden dabei nicht zuverlässig als Auftragsjournal aktualisiert.
- `11B520`, `118E00` und `11E960` haben maschinell bestätigte explizite 0/1-Rückgaben. Der Raw-0-Filter dieser Dispatcher ist nicht einfach ein Fehler durch einen zufälligen RAX-Wert. Eine 1 beweist dennoch nicht den Erfolg jeder Mitgliedsroute.
- Die vorhandenen SE-Hooks liegen auf den nativen Move-/Target-Einstiegen. Interne native Aufrufe über diese Einstiege werden mit erfasst. **Keine** pauschale Diagnose „Vanilla umgeht die verwalteten Wrapper“ verwenden.

| Rolle | Autoritative Zielquelle / notwendige Unterscheidung |
|---|---|
| Burgverteidigung | AIV-Verteidigungsklasse, tatsächlich gewählte Gruppenordinal und aktuelle Rückzugspolitik. Armbrustschützen: feste Klasse 7, Slotbasis 30. AIV-Tiles über die bevölkerte Ordinal auflösen, nicht einfach Arrayindex verwenden. |
| Mobile Burgverteidigung | Vanilla berücksichtigt den Rotationsstand des Besitzers und die AIC-Gruppenanzahl. Fixes' erweiterte Verteidigung hat eine abweichende Zuordnung ohne diese Vanilla-Rotation. Den vorhandenen `AiExpandedDefenseStorage`-Adapter verwenden/gezielt erweitern; nicht denselben Leser für beide Politiken annehmen. |
| Wirtschaftsschutz | `3E070`: `r_EconomyProtectionRequiredPositionTileX/Y`, Population/AIC und LastStand-/Keep-Bedingungen. Unterstärke bewirkt keinen neuen Auftrag, nicht automatisch Idle. Kein zusätzliches „Bedrohungstimer muss positiv sein“-Gate erfinden: Vanilla prüft für das Ziel positive X/Y. |
| Verteidigungspatrouille | `3EA50` wählt ein Gebäude/Zugangsziel und verwendet `11DD10`. Das ist nicht zwingend eine gewöhnliche Patrolroute mit gesetztem Patrolflag. `2D3E0` verändert einen gemeinsamen Auswahlcursor; nicht zur lesenden Rekonstruktion nochmals aufrufen. Der Ownercursor ist kein gespeichertes Ziel pro Gruppe. |

Das Patrouillenziel lässt sich auch nicht allgemein aus den Zielkoordinaten vorhandener Mitglieder rekonstruieren: Formation/Ausweichen erzeugen individuelle Ziele, Kampfreaktionen und bereits erreichte Ziele lassen andere Felder zurück. Keine Mehrheits-/Leaderheuristik oder fremde Pfadkopie.

## Umsetzung in APIShared

### 1. Semantische Gruppenabsicht erfassen statt nur letzte Bewegung merken

1. Den vorhandenen Order-Broker und die Prozess-/Weltgenerationen weiterverwenden. Zusätzlich die **Halteabsicht am nativen `11DD10`-Einstieg** erfassen: tatsächliche Gruppe, Owner/GID, Zielkoordinaten, Reihenfolge und Herkunft. Native Originalfunktion exakt einmal unverändert ausführen. Eine normale Rückkehr mit `0` darf die validierte Halteabsicht nicht verwerfen.
2. Vor diesem neuen gemeinsamen Beobachtungspunkt Eigentümer/Spannen in SE, Fixes und APIShared prüfen. Bei vorhandenem Eigentümer dessen Broker erweitern; keinen überlappenden Hook installieren. Falls eigener gemeinsamer Beobachter nötig: APIShared besitzt ihn prozessweit, einmalige geprüfte Installation mit logischem Demand-Gate; vollständige Instruktions-/ABI-/RIP-/Sprung- und Fremdpatchprüfung. Keine Live-Probe auf kopiertem Code beim Start, kein späteres Umpatchen.
3. Nested-Reihenfolge festlegen: Innerer Move während `11DD10` darf die abschließende Halteabsicht nicht in einen bloßen Move/Idle umwandeln. Spätere echte Target-/Strategic-/Stop-Befehle ersetzen/verwerfen ältere Absichten in derselben Ereignisordnung. Reentrante Weltwechsel und wiederverwendete Gruppen-IDs über GID/Generation absichern.
4. Journal pro exakter Gruppenidentität führen, nicht pro Einheit. Ein angekommenes Hold bleibt Hold; `MoveCompleted → Idle` darf dessen Ziel nicht verlieren. Einzelne Kampf-/Bewegungszustände von Mitgliedern sind kein universeller Gegenbeweis zur controllerseitigen Halteabsicht.
5. Individuelle Bewegungsinvalidierung mit Herkunft/Grund erfassen und anhand der nativen Semantik differenzieren. Eigene Recruit-Receipts bleiben ausgenommen. Echte fremde Auftragsänderungen weiterhin konservativ behandeln; im Zweifel frische autoritative Absicht verlangen.

### 2. Rollenbezogenen Aufnahmeplan bestimmen

Private Resolver-Schicht zwischen `TryResolveGroup` und `TryAdmit` einführen; keine neue Pflichtmethode am öffentlichen Capability-Interface.
Der interne Rückgabevertrag unterscheidet ausdrücklich `Resolved`, `AwaitControllerIntent` und `Rejected`; ein aufgelöster Plan enthält den Einzelunit-Auftrag, dessen Quelle und Gültigkeitsbezug. Warten darf nicht als boolescher Fehler durch `Finish` laufen.

- Exakte bestehende Zielgruppe, Owner, Speicherrolle/Ordinal und GID zuerst validieren. Gruppenauswahl nicht auf leere Ersatzgruppen umlenken, nur um den Fehler zu umgehen.
- Frische, weiterhin gültige semantische Journalabsicht verwenden. Bei fehlendem Journal die sicher lesbaren Controller-/AIV-Daten rollenbezogen auswerten; alle nativen Guards und Vanilla-/Fixes-Unterschiede berücksichtigen. Keine komplette KI-Controllerfunktion ausführen, um ein Ziel „abzufragen“.
- Guards mit bewusstem No-op bedeuten „bisherige Absicht bleibt maßgeblich“. Dort nicht aus aktuellen Ownerkoordinaten einen neuen Auftrag erfinden. Aktive Angriffs-/Strategieabsichten dürfen nicht durch ein statisches Wachziel überschrieben werden.
- Für Patrouillen weder `r_Patrol...` als alleinige Wahrheit verwenden noch den Auswahlcursor verändern. Fehlt eine belastbare Absicht, den Bootstrap-Wartepfad verwenden.
- Nach Auflösung vorhandene Einzelmitglied-Initialisierer, eigene Route und bestehenden transaktionalen Rollback verwenden. Keine Gruppenbefehle erneut senden; keine Aufträge, Aufgabenreservierungen oder Pfadpuffer eines Altmitglieds kopieren.
- Den Empty-Group-Fallback für tatsächlich leere Gruppen erhalten. Seine Beschränkung nicht entfernen, keine pauschale Idle-Zuweisung einführen.
- Geltungsbereich des Fixes: zuerst die drei belegten Rollen vollständig abnehmen. Für alle übrigen bereits unterstützten Rollen eine Fallübersicht über Journalquelle, aktuellen Gültigkeitsbeleg und Einzelinitializer erstellen und Regressionen prüfen. Keine Erweiterung auf bisher ausgeschlossene gebäudegebundene/Arbeitsrollen; keine unbelegte Zusage, damit sei jede Vanilla-Rolle live geprüft.

### 3. Alter Save / unbekanntes Ziel: begrenztes Warten

- Während der Aufnahme nur dann warten, wenn eine gültige besetzte Zielgruppe existiert, die Rolle grundsätzlich zulässig ist und ausschließlich die aktuelle Absicht fehlt. Ungültiger Owner/GID, ungeeigneter Typ, echte Pfad-/Jobfehler etc. bleiben unmittelbare Fehler.
- Die einmal gewählte Gruppenidentität festhalten und vor Anwendung erneut validieren. Nicht bei jedem Retry auf eine andere/leerere Gruppe wechseln. Wird die Gruppe ungültig, mit konkretem Grund beenden.
- Den Neuzugang währenddessen in einer eigenen geschützten Isolation halten; nicht vorzeitig der aktiven Gruppe beitreten lassen. Vorhandene Schutz-/Privatgruppenlogik wiederverwenden und alle eigens angelegten leeren Gruppen auf jedem terminalen Pfad bereinigen. Keine wiederholten Detach-/Allocation-Versuche pro Tick.
- `Queued` bleibt bis zum terminalen Ergebnis sichtbar. Spectators allgemeiner Wartetext soll sowohl Initialisierung als auch Warten auf den KI-Auftrag erklären. Ein spezieller Fortschritts-Callback ist nicht erforderlich: den vorhandenen Vertrag „Completion genau einmal“ erhalten; `TryGetState` darf den aktuellen Pending-Detailzustand zeigen.
- Timeout: separates Budget von **3000 fortgeschrittenen nativen Simulationsticks**, Beginn beim Eintritt in das Intent-Warten, unabhängig vom Initialisierungsbudget. Pause/Renderaufrufe verbrauchen nichts; Tick-Wrap/Weltwechsel wie beim bestehenden SimulationClock-Vertrag behandeln. Kein verstecktes endloses Warten und keine Controller-/Patrouillenbeschleunigung.
- Natürliche Zielpublikation weckt passende Wartende. Timeout/Defeat/Weltwechsel/Identitätswechsel beendet sie genau einmal mit lesbarer Ursache und vollständiger Bereinigung. Frische fehlgeschlagene Platzierungen nach bestehendem Vertrag entfernen; bestehende Einheiten bei gescheiterter API-Aufnahme unverändert wiederherstellen.

### 4. Effizienz: verbindliche Grenzen

- Keine Schleife über alle Einheiten, kein Sampling aller Truppenbewegungen, kein Pro-Frame-Abgleich sämtlicher Gruppen. Keine Leader-/Mehrheitszielsuche durch Gruppenmitglieder.
- Den vorhandenen UnitMove-Receipt-Broker für die eigene Aufnahmetransaktion beibehalten, aber daraus kein zusätzliches weltweites Bewegungsjournal bauen. Optionale Invalidierungsarbeit höchstens für relevante Gruppen und nur als konstante Ereignisarbeit; keine pro Einheit angelegten Subscriptions/Tracker und keine Speicherung aller UnitMove-Ereignisse. Gruppenabsicht nicht aus der Summe solcher Bewegungen ableiten.
- Beobachtung an wenigen vorhandenen Gruppenbefehlsstellen plus dem gemeinsamen Hold-Punkt. Pro Ereignis Gruppenidentität prüfen und einen kleinen Journaleintrag aktualisieren; keine Pfadsuche/Memberiteration im Beobachter.
- Relevante Journaleinträge an Group-ID/GID/Welt binden und auf die native Gruppenkapazität begrenzen. Demand vor optionaler verwalteter Arbeit prüfen. Bestehende Aufnahme-/Receipt-Sicherheitsfunktionen nicht pauschal abschalten, um Beobachtungskosten zu sparen.
- Wartende separat indexieren: `group identity → waiting requests`, deduplizierte Ready-Queue. `Process` darf für diese Erweiterung nicht fortlaufend alle `tracked.Values` durchsuchen. Unit-Settled-Ereignisse wecken nur zugehörige Requests; Zielpublikation nur Wartende dieser Gruppe.
- Timeouts über geordnete Deadlines und einen bestehenden Simulationstick-Publisher prüfen: pro Tick zuerst nur die früheste Deadline, abgelaufene Einträge proportional zu deren Anzahl abarbeiten. Kein eigenständiger Timer/Frame-Poller pro Neuzugang. Deadlines erst in der Ready-Queue außerhalb eines nativen Gruppenaufrufs anwenden.
- Resolver nur bei konkreter Aufnahme/Wiederaufnahme; begrenzte AIV-/Fixes-Tabellenabfragen sind dort zulässig. Die bereits erforderliche transaktionale Membership-/Bitsetprüfung nicht als „Performancefix“ abschwächen.
- Abnahme mit wenigen und mit vielen vorhandenen Einheiten: Anzahl optionaler Resolver-/Journal-/Warteschritte muss von relevanten Gruppenereignissen und pending Requests abhängen, nicht linear von der Weltpopulation. Leerlauf-Allokationen/Callbackzahlen messen, keine unbelegte FPS-Zusage.

### 5. Save-Vertrag, Diagnose und Verbraucher

- Semantische Hold-Absichten mit exakter Gruppe/GID/Owner und ihrem Gültigkeitsbezug speichern. Bestehende Auftragswerte nicht umnummerieren. Alte AIO1-/Save-Records weiter lesen; notwendige neue Journaldaten additiv/versioniert speichern und alte Bytes bei Fehlern erhalten. Fehlendes Journal ist ein Bootstrapfall, kein beschädigter Save.
- Beim Speichern wartender Requests auch die gebundene Zielgruppenidentität, den Wartegrund und das verbleibende Tickbudget sichern. Nach Laden Isolation und Zielbindung erneut prüfen und gezielt wieder einreihen; weder heimlich eine andere Gruppe wählen noch bei jedem Laden das volle Wartebudget erneuern. Fehlende neue Pending-Felder in alten Saves sind ein ausdrücklich zu migrierender Legacyfall.
- Beim Restore nur zur geladenen Welt passende Daten zulassen; Cache-Gültigkeit an aktuellen Gruppen-/Rollen-/Controllerzustand binden. Alte Befehle nicht auf neuere native Zustände zurückspielen. Map-Deklarationen weiterhin gegen endgültigen Owner/AIC prüfen, keinen Save-Cache blind in neue Maps übernehmen.
- Normale Fehlerlogs erhalten: Session, Unit-ID/GID/Typ, Owner, Rolle/Modus, Zielgruppen-ID/GID, validierter Füllstand/Leader soweit sicher verfügbar, Intentquelle/Sequenz, letzter Invalidierungsgrund. **Ursprünglichen Ledgergrund und Fallbackgrund getrennt erhalten**; ungültige Pointer nicht für Logging dereferenzieren.
- Bei Eintritt ins Warten einmal Anlass/Deadline loggen, bei Abschluss Ergebnis/verbrauchte native Ticks; keine wiederholte Warnung für denselben wartenden Request pro Tick. Jeder endgültige Fehlversuch bleibt im normalen Build nachvollziehbar.
- Große vorübergehende Messungen ausschließlich im separaten Testmod bzw. bei nachgewiesener Notwendigkeit über DevDiag; keine Szenarien im Hauptmod hinterlassen.
- Öffentliche Completion-, Thread-, Ownership- und Gruppenauswahlverträge kompatibel halten. Unterstütztes Verhalten und Grenzen in APIShareds AI-Military-Dokumentation/Updateverträgen nachziehen. Falls neue öffentliche Beobachtungen nötig sind, additiv und beim gemeinsamen Eigentümer veröffentlichen.

## Tests und überprüfbare Abnahme

### Offline vor dem Live-Test

Bestehende `AiMilitaryCoreTests`, `AiMilitaryBrokerTests`, `AiRecruitOrders.Tests`, `AiMilitary.NativeTests` und betroffene Save-/Map-Tests gezielt erweitern. Keine Quellwortlauttests als Ersatz für Verhalten.

1. Besetzte Gruppe ohne bisherigen Move-Ledger, gültiges Hold-Ziel: neuer Recruit erhält eigene Route; Altmitglieder unverändert. Native Hold-Rückkehr 0, echte Änderung 1 und nested Move/Hold-Reihenfolge prüfen.
2. Angekommener Hold bleibt nutzbar; neuer Recruit weit entfernt muss hingehen statt Idle zu bleiben. Geänderte Gruppenabsicht verwirft alten Cache.
3. Echte KI-Verteidigungspatrouille ohne gewöhnliches Patrolflag; keine Cursoränderung durch Lesen, keine abgeleiteten Member-/Leaderpfade.
4. Fixe/mobile Verteidigung, Rotation, nicht zusammenhängend belegte AIV-Tiles, LastStand/Keep-Rückzug und Fixes-Slots oberhalb 10. Native und Fixes-Mobilepolitik getrennt prüfen.
5. Wirtschaftsschutz mit gültigem Ziel trotz abgelaufenem Bedrohungstimer; Unterstärke-/No-op-Pfad erhält bisherige Absicht.
6. Wartende mit Zielereignis, ohne Ereignis bis Timeout, 5000 pausierten Pump-Aufrufen, Tick-Wrap, GID-Recycling, Weltende und reentrantem Completion-Callback. Genau ein terminales Ergebnis, keine Fremdeinheit/-gruppe verändern.
7. Legacy-Save ohne Journal, neuer Save mit Journal, erneutes Laden und Ergänzen derselben besetzten Gruppe. Kein Auftragsreplay über spätere Vanilla-Änderungen; Unknown-/Attack-/Strategie-/Dig-Gültigkeit nicht lockern.
8. Ereignis- und Warteschrittezähler bei großer Weltpopulation: kein Welt-Scan, kein Broadcast, kein neuer permanenter Unit-Poller. Inaktive Observer ohne optionale Allokationen.

### Ingame: minimal 12 positive Hauptfälle

Ein eigener spezialisierter Testmod unter `Testmods/` über APIShared.Automation; vorhandenes UiAcceptance-Setup/Mapper-Helfer wiederverwenden, sofern passend. Keine Testszenarien in Spectator/APIShared. Keine globalen Maus-/Tastatureingaben ohne ausdrückliche Freigabe; direkte Noesis-/API-Eingaben bevorzugen.

| Zielrolle | Neue Partie | Geladener Problemspielstand `test_diagonal.sav` |
|---|---|---|
| CastleDefense | Regular und Additional | Regular und Additional |
| DefensePatrol | Regular und Additional | Regular und Additional |
| EconomyProtection | Regular und Additional | Regular und Additional |

- Fixture garantiert passenden KI-Lord/AIC, Armbrustschützen und eine **schon vor dem Spawn besetzte tatsächlich ausgewählte Gruppe**. Im Problem-Save Owner 7 verwenden. Ein Ausweichen auf eine leere Gruppe ist kein bestandener Test.
- Reale Spectator-Auswahl und normaler Mapperpfad, keine künstliche Ledger-Vorbefüllung. Ausgewählte Gruppe vor/nach Aufnahme über genaue Identitäten nachweisen.
- `Applied`, Rolle, echte Membership und Quota-Marker prüfen; zusätzlich über aktive Simulationsticks Verhalten nachweisen: Wachziel erreichen/halten, aktuellen und nachfolgenden Patrouillenauftrag ausführen, Wirtschaftsschutzziel übernehmen. Passende Kampfreaktion als weiteres Verhalten, nicht allein Membership.
- Direkt am atomaren Aufnahmezeitpunkt Altmitglieder-Aufträge/Ziele/Pfadidentitäten vergleichen: keine vom Fix ausgelöste Neubefehligung. Spätere natürliche KI-Änderungen separat zuordnen.
- Mindestens ein Legacy-Patrouillenfall zeigt sichtbares Warten und Anwendung nach natürlichem Controllerziel, ein weiterer absichtlich unerfüllbarer Fall Timeout und Bereinigung. Pause darf kein Timeout erzeugen.
- Danach versionierten Save erzeugen, neu laden und nochmals in dieselbe weiterhin besetzte Gruppe aufnehmen. Im Editor deklarieren und beim Kartenstart in vorhandene Gruppen integrieren; tatsächlichen End-Owner/AIC berücksichtigen.
- Weitere Regressionen: leere Gruppen, None/Ingenieurpools, ungültige Gruppe/Zielidentität, unzugängliches Ziel, Angriff/Strategie-Supersession und einmalige Abschlussmeldung. Alle weiteren unterstützten Rollen mit passenden Einheiten/AIC in einer dokumentierten Fallübersicht nachprüfen; nicht ausgeführte Live-Fälle ausdrücklich offen lassen.
- Lokale Mutationen in echtem MP bleiben gesperrt. Falls neue persistierte simulationserhebliche Journaldaten die Map-/Save-Readiness berühren, passende reale Host-/Client-Abnahme mit übereinstimmenden und abweichenden Records einplanen.

## Reihenfolge und Übergabe

1. Aktuelle Quellen, installierte SDK-/SE-/Fixes-Stände und Native-Hash erneut feststellen; vorhandene Nutzeränderungen erhalten. Analysebasis dieses Plans: Mod-HEAD `58b84069eeaf0eead91861a7a2f56c6227dcd54d`, APIShared-HEAD `39a2093e57c36a0ddbed820e3f6b3f30e9683386` (Momentaufnahme, keine Buildpins).
2. Zunächst Ursachen-/Intentdiagnose und reproduzierbare gefüllte-Gruppen-Fixture. Bei Bedarf die 31 historischen Fälle mit zusätzlicher GID-/Invalidierungsdiagnose nachstellen; keine konkrete alte Gruppenidentität erfinden.
3. Gemeinsame Hold-Beobachtung, Journal/Resolver und gezielte Pending-/Timeoutsteuerung implementieren; Transaktion und Persistenz integrieren. Anschließend Spectator-Wartetext und Tests anpassen.
4. Relevante Offlineprüfungen, unabhängiges Review, anschließend Live-Matrix und Effizienznachweis. Auf dem bekannten Hash kein natives Vollscannen bei jedem Spielstart.
5. Erst nach fertigem Fix Versionen konsistent erhöhen. Spectators deklarierte APIShared-Mindestversion auf den tatsächlich benötigten Fixstand setzen. APIShared bei Installation immer mit passendem Plugin-/Patcher-Paar aktualisieren; keine fremde laufende Partie/Installation überfahren.
6. Einstellungen/temporäre Pakete nach Tests wiederherstellen, normale Builds ohne temporäre Diagnose prüfen/installieren. Reproduzierbare Fixtures/Kopien bereinigen; kompakte Ergebnisse und erforderliche Laufbelege behalten. README unverändert lassen; keine Commits/Veröffentlichung ohne Auftrag.

**Erledigt ist der Fix erst**, wenn besetzte Gruppen in der Pflichtmatrix tatsächlich verwendbare Neuzugänge erhalten, kein unzulässiger Gruppenbroadcast/Cursorlauf entsteht, Wartestatus/Timeout funktionieren und die Auftragsermittlung ohne Überwachung der gesamten Einheitenpopulation auskommt.
