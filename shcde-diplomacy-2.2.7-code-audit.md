# Code-Audit: Stronghold Crusader Diplomatie V2 2.2.7

## Kurzfazit

Der Mod enthält die beschriebenen Kernsysteme tatsächlich: mehrstufige Beziehungen, Warenforderungen und -bitten, Waffenruhe- und Bündnislogik, Befehle, Unterwerfung, Tribut sowie eigene Speicherstände einschließlich V1-Import. Die Implementierung ist ungewöhnlich defensiv und besitzt für Teamwechsel und Gütertransfers Rücklese- und Rollbackpfade.

Die Beschreibung trifft den praktisch wirksamen Funktionsumfang aber in drei wichtigen Punkten nicht. Zu unseren produktiven Mods wurde nach der zweiten Prüfung keine praktisch auslösbare Inkompatibilität bestätigt. Insbesondere ist die gemeinsame Änderung des Verbündeten-HUDs durch `BugfixesAndQoL` in der aktuellen Script-Extender-Version kein Konflikt.

## Prüfgrundlage und Grenzen

- Geprüft wurde das ausgelieferte Paket unter `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\shcde-diplomacy-main\shcde-diplomacy-main`.
- Das Paket enthält keinen C#-Quellcode. Maßgeblich war deshalb die dekompilierte Release-DLL `StrongholdEuropeDiplomatieV2.dll`, Version 2.2.7, SHA-256 `A57896937F1628FD65BB707FF8700F9758030425797FF131863B9E3A9C238C80`.
- Die Dekompilierung liegt reproduzierbar unter `.inspect/DiplomacyAudit-A5789693/`.
- Verglichen wurde mit dem kanonischen lokalen Script Extender 2.3.0, Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`, und der aktuellen Native-Baseline `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Zusätzlich wurden die Zustandsautomaten für Beziehungen, Forderungen, Unterwerfung und Vasallentum, die Speicherpfade, Güter- und Teamtransaktionen, Lebenszyklus- und UI-Pfade sowie alle 1121 externen Memberreferenzen der DLL geprüft. Gegen die aktuell installierten Abhängigkeiten ließ sich jede dieser Referenzen auflösen.
- Es wurde kein mehrstündiger Spieltest durchgeführt. Als Befunde erscheinen daher nur deterministische Codepfade mit einer konkreten auslösenden Spielsituation, keine bloßen Absturz- oder Timingvermutungen.

## Bestätigte Probleme

### 1. Beziehungen sind bei gemeinsam gestarteten KI-Teams nicht pro Fürst, sondern pro Koalition

**Schweregrad: hoch – reproduzierbare Abweichung von der Hauptbeschreibung**

Die README verspricht für jeden KI-Fürsten unabhängig fortgeschriebene Werte für Vertrauen, Respekt und Groll. Der Code gruppiert beim Kartenstart jedoch alle KI-Spieler mit derselben positiven Teamnummer zu genau einer Koalition (`team:N`). Anschließend wird nur ein `RelationState` je Koalition angelegt. Sämtliche Mitglieder werden über `_playerCoalition` auf diesen gemeinsamen Zustand abgebildet.

Praktische Auslösung: Zwei oder mehr KI-Spieler starten im selben Team. Ein Geschenk, eine Forderung, ein Abkommen oder ein Vertragsbruch gegenüber einem Mitglied wirkt dann auf die gemeinsame Beziehung des gesamten Teams. Für die Persönlichkeitslogik wird nicht jedes Mitglied ausgewertet; der Code wählt einen einzigen Repräsentanten mit dem höchsten `send_goods_margin`, bei Gleichstand die niedrigste Player-ID. Einzelne Mitglieder eines solchen Teams können sich ausdrücklich nicht unterwerfen.

Belege:

- `DiplomacyFeature.CaptureRosterAndCoalitions`: Gruppierung über `team:<Teamnummer>` und Aufbau von `_playerCoalition` (`DiplomacyFeature.cs`, ca. Zeilen 2960–3025).
- `DiplomacyFeature.InitializeRelations`: genau ein `RelationState` je `CoalitionState` (ca. Zeilen 3040–3065).
- `DiplomacyFeature.SelectV2CoalitionPersonalitySignal`: Auswahl nur eines AIC-Repräsentanten (ca. Zeilen 9400–9445).
- `V2FealtyAvailabilityText`: ausdrückliche Ablehnung mit „Einzelne Teammitglieder können sich nicht unterwerfen“ (ca. Zeile 13238).

Erforderliche Korrektur: Entweder die Beschreibung klar auf koalitionsweite Beziehungen und eine repräsentative Persönlichkeit begrenzen oder Zustände und Persönlichkeitsauswertung wirklich pro KI-Spieler führen. Die Unterwerfungsbeschränkung gemeinsamer Teams muss in jedem Fall dokumentiert werden.

### 2. Der letzte feindliche KI-Verbund kann weder Bündnispartner noch Vasall werden

**Schweregrad: mittel bis hoch – undokumentierte, häufig erreichbare Funktionsgrenze**

`CanJoinAlliance` verlangt immer mindestens einen weiteren lebenden, nicht verbündeten KI-Verbund. Dieselbe Bedingung wird nicht nur für Bündnisse, sondern auch als Zulässigkeitsbedingung der Unterwerfung verwendet.

Praktische Auslösung:

- In einem 1-gegen-1-Gefecht ist ein Bündnis oder eine Unterwerfung des einzigen KI-Gegners grundsätzlich unmöglich.
- In größeren Partien kann auch der zuletzt verbliebene feindliche Verbund weder beitreten noch Vasall werden.

Das widerspricht zumindest dem uneingeschränkten Eindruck der Abschnitte „Bündnisverwaltung“ und „Lehen und Tribut“. Der Mod zeigt die Einschränkung erst im Spiel mit „Ein feindlicher KI-Verbund muss verbleiben“ beziehungsweise „Ein weiterer lebender Gegner muss verbleiben“ an; die README nennt sie nicht.

Belege:

- `DiplomacyFeature.CanJoinAlliance` zählt ausschließlich *andere* lebende feindliche Koalitionen und verlangt ein Ergebnis größer null (ca. Zeilen 4173–4179).
- `DiplomacyV2Submission.EligibilityBlocker` liefert bei derselben negativen Bedingung `NoRemainingOpponent` (ca. Zeilen 286–294).

Erforderliche Korrektur: Mindestens als zentrale Einschränkung dokumentieren. Soll das beschriebene Diplomatiesystem auch in 1-gegen-1-Partien vollständig funktionieren, muss die Endspielbehandlung so geändert werden, dass ein letzter Gegner sicher beitreten oder sich unterwerfen kann.

### 3. Echter Mehrspieler ist deaktiviert und nicht lediglich „ungetestet“

**Schweregrad: mittel – falsche Beschreibung des unterstützten Spielmodus**

Die README bezeichnet Mehrspieler als ungetestet. Der Code lässt Diplomatiemutationen in einer Netzwerkpartie aber nur zu, wenn kein anderer menschlicher Spieler vorhanden ist. Bei einem echten Host-/Client-Spiel wird die Karte mit `reason=not_offline_skirmish` deaktiviert. Eine zusätzliche Rosterprüfung verwirft jeden weiteren gültigen Nicht-KI-Spieler ausdrücklich mit `human-human diplomacy is not supported`.

Praktische Auslösung: Jede Netzwerkpartie mit mindestens einem weiteren Menschen. Der Mod kann dabei geladen sein, seine Diplomatiefunktion bleibt aber inaktiv.

Belege:

- `RuntimeSessionScopeDecision.AllowsLocalMutation`: bei `networked == true` nur `otherHumanPlayers == 0` (Zeilen 5–11).
- Kartenaktivierung in `DiplomacyFeature`: bei nicht erlaubtem Scope `_mapDisabled = true` und `status=inactive|reason=not_offline_skirmish` (ca. Zeilen 2529–2559).
- `CaptureRosterAndCoalitions`: ausdrückliche Ablehnung weiterer menschlicher Spieler (ca. Zeilen 2978–2987).

Erforderliche Korrektur: README und Metadaten müssen „Mehrspieler nicht unterstützt; Diplomatie wird dort deaktiviert“ sagen. Alternativ wäre eine echte hostautoritative und synchronisierte Implementierung erforderlich.

### 4. Die Kompatibilitätsanleitung widerspricht sowohl sich selbst als auch dem Code

**Schweregrad: niedrig – praktisch irreführende Diagnoseanleitung**

Der Alpha-Abschnitt und „Neu in 2.2.7“ sagen korrekt, dass unbekannte Spiel- und Script-Extender-Versionen trotz Warnung geladen werden. Im Abschnitt „Voraussetzungen“ steht dagegen weiterhin, andere Spielfassungen würden beim Start abgewiesen. Tatsächlich landen abweichende Datei-Hashes in `GameSideDrift`; sofern die Typ-/Memberprüfung erfolgreich ist, bleibt `CompatibilityReport.Compatible` wahr und der Mod lädt weiter.

Zusätzlich fordert die Installationskontrolle dazu auf, einen Kompatibilitätsstatus `degraded` zu beachten. Die Startprüfung emittiert jedoch nur `ok` oder `incompatible`. Andere, spätere Teilsysteme können zwar eigene `degraded`-Zeilen ausgeben, diese haben aber nicht die dort behauptete Bedeutung „Script-Extender passt nicht, Mod schaltet sich ab“.

Praktische Auslösung: Jeder Start auf einer nicht exakt gepinnten Version – darunter unsere Zielumgebung mit Script Extender 2.3.0 und einem vom 2.80-Pin abweichenden Native-Hash. Benutzer erhalten gleichzeitig Warnungen und einen möglichen `status=ok`, während die README an anderer Stelle eine Ablehnung verspricht.

Belege:

- `DiplomatieCompatibilityGuard.NoteUnattested` legt Hashabweichungen in `GameSideDrift` ab (ca. Zeilen 268–283).
- `CompatibilityReport.IdentityAttested` berücksichtigt `GameSideDrift` nicht (ca. Zeilen 45–56).
- `DiplomatiePlugin.Awake` protokolliert für die Startprüfung nur `ok` oder `incompatible` und lädt bei Drift ausdrücklich weiter (ca. Zeilen 58–75).

Erforderliche Korrektur: Die veralteten Absätze entfernen und einen einzigen, codegenauen Entscheidungsbaum dokumentieren: Member fehlt → `incompatible` und Abschaltung; nur Hash/Identität weicht ab → Warnung, aber Laden; Teilsystemfehler → jeweils den konkreten späteren Status auswerten.

## Eigenständig meldbare Laufzeitfehler

Die folgenden beiden Berichte können unabhängig vom übrigen Audit an den Autor übermittelt werden. Sie enthalten nur praktisch erreichbare Fehler im ausgelieferten Verhalten von Version 2.2.7. Dokumentationsfehler und lediglich verdächtige, aber nicht sicher belegbare Codepfade sind hier bewusst nicht enthalten.

### Fehlerbericht A: Gemeinsame Startteams teilen ungewollt eine einzige Diplomatiebeziehung

**Betroffene Version:** 2.2.7

**Reproduktion:** Eine Karte mit mindestens zwei KI-Fürsten starten, die bereits dieselbe positive Teamnummer besitzen. Danach mit einem dieser Fürsten interagieren, etwa durch ein Geschenk, eine Forderung, ein Abkommen oder einen Vertragsbruch.

**Tatsächliches Verhalten:** Beide Fürsten werden zu einer Koalition `team:N` zusammengefasst und teilen genau einen `RelationState`. Vertrauen, Respekt, Groll, Abkommen und Forderungen gelten dadurch für das gesamte Team. Für die Persönlichkeit wird nur ein Mitglied als Repräsentant ausgewählt. Einzelne Teammitglieder können sich außerdem nicht unterwerfen.

**Erwartetes Verhalten:** Wie in der README beschrieben, sollte jeder KI-Fürst seine drei Beziehungswerte und seine Persönlichkeitsentscheidung unabhängig besitzen. Alternativ muss die Funktion ausdrücklich als Diplomatie mit ganzen Startteams beschrieben und die Auswahl einzelner Mitglieder aus der Oberfläche entfernt beziehungsweise klar eingeschränkt werden.

**Ursache und Korrektur:** `CaptureRosterAndCoalitions` gruppiert alle KI-Spieler derselben Startteamnummer unter einem Schlüssel, `_playerCoalition` verweist jedes Mitglied auf diesen Schlüssel, und `InitializeRelations` erzeugt nur einen Zustand pro Schlüssel. Für fürstengenaue Diplomatie müssen die Beziehungszustände von der Teamkoalition getrennt werden. Falls das koalitionsweite Modell beabsichtigt ist, müssen mindestens README und UI diesem tatsächlichen Vertrag entsprechen.

### Fehlerbericht B: Unterwerfung des letzten Gegners wird durch eine Bündnisregel blockiert

**Betroffene Version:** 2.2.7

**Reproduktion:** Ein 1-gegen-1-Gefecht spielen oder in einer größeren Partie alle feindlichen KI-Verbünde bis auf einen beseitigen beziehungsweise verbünden. Beim letzten Gegner anschließend alle beschriebenen militärischen Unterwerfungsbedingungen erfüllen und eine Unterwerfung verlangen.

**Tatsächliches Verhalten:** Die Unterwerfung bleibt stets mit „Ein weiterer lebender Gegner muss verbleiben“ gesperrt. Damit kann der einzige beziehungsweise letzte feindliche KI-Verbund niemals Vasall werden. In einem 1-gegen-1 ist der gesamte beworbene Unterwerfungspfad unerreichbar.

**Erwartetes Verhalten:** Die militärischen und persönlichkeitsspezifischen Unterwerfungsbedingungen sollten entscheiden, ob der Gegner Vasall werden kann. Eine Regel für das Bilden eines gleichberechtigten Bündnisses darf die Unterwerfung nicht nebenbei sperren. Falls dieses Endspielverbot absichtlich besteht, muss es als zentrale Einschränkung dokumentiert werden.

**Ursache und Korrektur:** `DiplomacyV2Submission.EligibilityBlocker` erhält das Ergebnis von `CanJoinAlliance` und gibt bei `false` immer `NoRemainingOpponent` zurück. `CanJoinAlliance` verlangt mindestens einen weiteren lebenden, nicht verbündeten KI-Verbund. Für die Unterwerfung ist eine eigene Zulässigkeitsprüfung nötig; die Bündnisprüfung sollte dort nicht wiederverwendet werden.

**Ergebnis der erneuten Einzelprüfung:** Bericht A wurde nochmals vom Aufbau der Startkoalitionen über `_playerCoalition` bis zur Erzeugung und Verwendung des gemeinsamen `RelationState` verfolgt. Bericht B wurde nochmals vom UI-Aufruf über `DiplomacyV2Submission.Assess` und `EligibilityBlocker` bis zur Zählbedingung in `CanJoinAlliance` verfolgt. Beide Fehler sind in den genannten Spielsituationen deterministisch erreichbar. An den Aussagen war keine Korrektur erforderlich.

## Abgleich der übrigen Hauptversprechen

Bei den folgenden Punkten wurde in der statischen Prüfung keine konkrete Gegenabweichung gefunden:

- Die drei Beziehungswerte und die Stufen-/Abkommenslogik sind implementiert.
- Warenforderungen verwenden Bestände, Produktions- und Rekrutierungsbedarfe; Warenbitten prüfen den verfügbaren Überschuss.
- Waffenruhe, Angriffserkennung, Rückzugsversuche und Teamtransaktionen besitzen aktive Hook-/Eventpfade.
- Die Unterwerfungsprüfung enthält die beschriebenen zwölf Einheiten, örtliche und gesamte Mindeststärke sowie mehrtägige Druckprofile. Die Ratte gibt früher nach; Wolf, Saladin und Richard sind als niemals unterwerfbar hinterlegt.
- Tribut wird im Abstand von 20 Tagen neu bemessen, auf 25 bis 1000 Gold begrenzt und nach zwei versäumten Raten beendet.
- Eigene V2-Speicherdaten, Prüfsumme, Formatmigrationen und ein V1-Importpfad sind vorhanden.
- Die aktuelle Script-Extender-2.3.0-ID-/Indexsemantik wurde in den untersuchten Unit-/Building-Schleifen korrekt behandelt.

Das bestätigt die Existenz und innere Verdrahtung dieser Pfade, ersetzt aber nicht die im mitgelieferten `build-manifest.json` selbst noch als ausstehend markierte In-Game-Langzeitabnahme.


## Empfohlene Reihenfolge der Korrekturen

1. Koalitionsmodell und Beschreibung in Einklang bringen.
2. Verhalten gegenüber dem letzten feindlichen Verbund entscheiden und dokumentieren beziehungsweise erweitern.
3. Mehrspielerstatus und Kompatibilitätsdiagnose in der README korrigieren.

