# Forschungsübergabe: Geschwindigkeitssteuerung von Truppen

> **Historischer Hinweis (August 2026):** Der frühere Ctrl+Click-Code wurde
> entfernt. Alle Ctrl+Click-Abschnitte in diesem Dokument beschreiben nur
> frühere Versuche und nicht das aktuelle Verhalten von Bugfixes and QoL.

Letzter Erkenntnisstand: 29.07.2026

## Zweck dieses Dokuments

Dieses Dokument fasst die Ingame-Tests, Logauswertungen, statischen Analysen des
nativen Spielmoduls und die daraus entstandenen Mod-Ansätze zusammen. Es enthält
alle Informationen, die für einen vollständigen Neuaufbau benötigt werden.

Eine frühere, stärker eingreifende Implementierung erreichte das gewünschte
Ingame-Verhalten bereits. Der Neuaufbau soll dasselbe Ergebnis möglichst nah an
Vanilla und mit weniger dauerhaften Eingriffen erreichen.

Plugins mit den GUIDs `TroopMovementFix2_Serp` und `TroopMovementFix_Serp`
dürfen wegen überschneidender nativer Hooks nicht gleichzeitig aktiv sein.

### Eigenständigkeit

Dieses Dokument ist die einzige technische Grundlage für den Neuaufbau. Es
ist in sich vollständig. Der widerlegte Tribe-ID-Guard und die
übereinanderliegenden Selection-Restore-Schichten gelten ausdrücklich nicht als
bewährte Grundlage.

## Kurzfassung des aktuellen Stands

- Ein normaler Bewegungsbefehl soll unverändert durch Vanilla laufen.
- `Ctrl+Bewegungsbefehl` soll Vanillas bereits vorhandenen Modus verwenden, in
  dem jede Einheit ihre eigene Höchstgeschwindigkeit nutzt.
- Verbesserte Speerkämpfer müssen in einer gemischten synchronen Gruppe auf das
  Gruppentempo begrenzt werden.
- Eine reine Selection-Änderung soll die Bewegung einer bereits laufenden
  Gruppe nicht verändern.
- Der Ctrl-Pfad und der spezielle Speerkämpfer-Fix sind grundsätzlich
  verstanden.
- Das Selection-Problem ist noch nicht gelöst.
- Der untersuchte Tribe-ID-Guard ist nachweislich wirkungslos und darf nicht in
  den Neuaufbau übernommen werden.

## Beobachtetes Vanilla-Verhalten

### Normale Bewegungsbefehle

- Homogene Gruppen laufen häufig mit der für ihren Einheitentyp vorgesehenen
  schnellen Bewegung.
- Gemischte Gruppen werden von Vanilla normalerweise synchronisiert. Dabei
  bewegen sich alle Einheiten am Tempo der langsamsten Gruppeneinheit.
- Die sichtbare Bewegung entsteht nicht nur aus zwei globalen festen
  Geschwindigkeiten. Vanilla kombiniert mindestens:
  - eine typabhängige normale Höchstgeschwindigkeit,
  - eine aktuell berechnete effektive Geschwindigkeit beziehungsweise
    Bewegungsverzögerung,
  - einen Kadenz- oder Teilschrittbonus,
  - Geh-/Laufanimationszustände,
  - Tribe-Felder für synchronisierte Gruppenbewegung,
  - Terrain- und Zustandsmodifikatoren.
- Die kleinen Geschwindigkeitswerte sind Verzögerungsstufen: Ein kleinerer Wert
  bedeutet eine schnellere Einheit. Beispielsweise ist eine effektive
  Verzögerung `1` schneller als `4`.

### Vanillas bereits vorhandene freie Maximalbewegung

Vanilla besitzt bereits eine Funktion, bei der die Einheiten einer Gruppe nicht
an ein gemeinsames Tempo angepasst werden. Jede Einheit verwendet dann ihre
eigene typabhängige Höchstgeschwindigkeit.

Der zugehörige Vanilla-Wrapper ist sinngemäß
`giveUnitSelectionMoveInstructionNoMatchedSpeed`. Er übergibt nicht einfach
einen künstlichen Geschwindigkeitswert. Er setzt im Tribe das Feld
`freeUnitSpeeds` und verwendet danach weiterhin den normalen Bewegungspfad.
Dadurch bleiben Berechnung, Terrain-Effekte, Kadenz und Animation in Vanilla.

Dieses Verhalten ist die Grundlage für den Ctrl-Modus.

### Selection-Probleme in Vanilla

Folgende Probleme wurden ohne den Mod beziehungsweise mit möglichst
unverändertem Vanilla-Pfad reproduziert:

1. Ein Klick auf einen Einheitentyp in der unteren Selection-Leiste kann bereits
   laufende Einheiten auf freie beziehungsweise schnellere Bewegung umstellen.
2. Das erneute gemischte Anwählen einer bereits laufenden Gruppe kann sie wieder
   verlangsamen.
3. Die Geschwindigkeit kann sich auch beim Abwählen oder erneuten vollständigen
   Anwählen wieder ändern.
4. Bei einer langsam synchron laufenden Gruppe wurden zuletzt nur die erneut
   ausgewählten Einheiten schneller; der nicht ausgewählte Rest blieb langsam.
5. Beim erneuten Anwählen aller Einheiten wurden die zuvor beschleunigten
   Einheiten wieder langsamer.

Selection ist damit in Vanilla nicht rein visuell. Die Selection-Routinen
löschen, erzeugen oder befüllen interne Tribes neu und stoßen dabei erneut
typabhängige Bewegungsberechnungen an.

### Besonderheit verbesserter Speerkämpfer

Die erweiterte Einstellung für schnellere Speerkämpfer ist ein besonderer
Vanilla-Pfad:

- Mit der normalen Einstellung gehen Speerkämpfer und eine gemischte Gruppe
  meist korrekt synchron.
- Mit der Einstellung für schnellere Speerkämpfer setzt ihr typabhängiger
  Handler einen Laufbonus und Laufzustand spät erneut.
- Dadurch können Speerkämpfer in einer gemischten `DefaultInSync`-Gruppe die
  langsameren Einheiten überholen.
- In homogenen Speerkämpfergruppen und im freien Fast-/Ctrl-Modus soll die
  verbesserte Vanilla-Höchstgeschwindigkeit erhalten bleiben.

## Relevante Einheitenfelder

Die Namen stammen aus dem öffentlichen Script-Extender-Interop, soweit dort
vorhanden. Unbenannte Felder tragen weiterhin generierte Namen.

| Feld | Beobachtete Bedeutung |
| --- | --- |
| `GameUnit.r_TribeId` | Aktuelle Tribe-Zugehörigkeit. Während Selection-Rebuilds zeitweise `0`. |
| `GameUnit.r_CurrentSpeed` | Typ-/zustandsbezogene normale Höchstgeschwindigkeits-Verzögerung. Kleiner ist schneller. |
| `GameUnit.r_CurrentSpeed2` | Aktuell wirksame Bewegungsverzögerung. Enthält Gruppenanpassung und langsamere Terrain-/Zustandseffekte. |
| `GameUnit.r_SpeedBonus` | Kadenz-/Teilschrittbonus. Bei den bisher untersuchten Läufern typischerweise `0` beim Gehen und `1` beim Laufen. |
| `GameUnit.N000000F4` | Bewegungs-/Animationszustand. |
| `GameUnit.N000000AA` | Im Fix2-Log als Transition-Timer erfasst. |
| `GameUnit.r_AI_LastIssuedTribeCommand` | Letzter Tribe-Befehl; hilfreich, um echte Befehle von Selection-Folgen zu unterscheiden. |

Im nativen gemeinsamen Bewegungscode werden managerbezogene Offsets verwendet.
Der aktuelle `GameUnit` beginnt in diesem Kontext bei `R8 + 0x65C`.

| Nativer Manager-Offset | Relativ zu `GameUnit*` | Bedeutung |
| --- | --- | --- |
| `0x660` | `0x004` | Animation (`N000000F4`) |
| `0x916` | `0x2BA` | `r_SpeedBonus` |
| `0x9A2` | `0x346` | effektive Geschwindigkeitsverzögerung |
| `0x9A8` | `0x34C` | weiterer Zustand des gemeinsamen Bewegungspfads |

### Animationen

Die nativen Handler verwenden mehrere Animationsfamilien. Der Laufzustand ist
bei den gefundenen Familien der passende Gehzustand plus Bit `0x80`:

- `0x001 -> 0x081`
- `0x101 -> 0x181`
- `0x201 -> 0x281`

Diese Regel darf nicht blind auf jeden Einheitentyp angewendet werden. Die
bewährte Erkennung scannt den nativen Handler jedes Einheitentyps und verwendet
nur tatsächlich gefundene Übergänge. Das verhindert erfundene Animationen für
Einheitentypen ohne Laufanimation.

## Relevante Tribe-Felder

Der Script-Extender-Zeiger `GameTribe*` beginnt `0x2A` Bytes hinter dem Anfang
des vollständigen nativen Tribe-Datensatzes. Deshalb müssen rohe native Offsets
um `0x2A` reduziert werden, bevor über `GameTribe*` gelesen oder geschrieben
wird.

| Offset relativ zu `GameTribe*` | Bedeutung |
| --- | --- |
| `0x01E` | Leader speed seed 1 |
| `0x020` | Leader speed seed 2 |
| `0x022` | Leader speed seed 3 |
| `0x024` | Leader transition timer/seed |
| `0x542` | `freeUnitSpeeds` |
| `0x54C` | Minimum speed |
| `0x54E` | Movement/synchronized speed |
| `0x550` | Maximum speed |
| `0x552` | Movement state 1 (`uint`) |
| `0x556` | Movement state 2 |
| `0x558` | Patrol mode |
| `0x55A` | Movement state 3 |
| `0x55C` | Average speed |
| `0x55E` | Movement state 4 |

`freeUnitSpeeds` liegt im vollständigen nativen Datensatz bei `+0x56C`, also
relativ zu `GameTribe*` bei `+0x542`. Der rohe Offset `0x56C` darf nicht direkt
auf den verschobenen Script-Extender-Zeiger angewendet werden.

## Relevante native Bewegungspfade

### Typabhängiger Unit-Handler

Im vierten `updateUnits`-Pass wird über eine Dispatch-Tabelle der Handler des
jeweiligen Einheitentyps aufgerufen.

Verwendetes AOB-Muster:

    41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00

Die Handler schreiben unter anderem Laufanimation und `r_SpeedBonus`. Dadurch
können typabhängige Sonderfälle nach einer vorherigen Gruppenberechnung erneut
wirksam werden. Verbesserte Speerkämpfer sind das bestätigte Beispiel.

### Berechnung der effektiven Geschwindigkeit

Der bewährte Speed-Hook detouriert
`c_game_unit_calculate_movement_speed`.

Verwendetes AOB-Muster:

    48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 4C 8D 35 ?? ?? ?? ?? 48 63 C2 48 69 D8 90 04 00 00

Vanilla läuft zuerst vollständig über den Trampoline-Aufruf. Nur für Einheiten
mit einer Mod-Direktive wird danach eingegriffen.

### Gemeinsame Bewegungskadenz

Verwendetes AOB-Muster:

    41 0F BF 80 16 09 00 00 41 0F BF 88 A2 09 00 00 45 8B 90 A8 09 00 00

Hier fließen `r_SpeedBonus`, effektive Verzögerung und weitere
Bewegungszustände in die Schwelle und die Zahl der ausgeführten Teilschritte
ein. Nur die sichtbare Geschwindigkeit oder nur die Animation anzupassen reicht
daher nicht aus.

### Selection-Einstiegspunkte

Fix2 kennt zwei getrennte Selection-Helfer:

- Welt-/Maus-Selection:

      48 89 5C 24 08 48 89 7C 24 10 BF 01 00 00 00 48 63 DA 8B C7 45 33 C9 89 05 ?? ?? ?? ?? 4C 8B D1

- UI-/untere Selection-Leiste:

      48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 49 63 D8 4C 8D 49 74

Cdecl-Signaturen:

    void MouseSelectionChanged(
        NativePointer<GameUnitManager> unitManager,
        int selectedUnitCount,
        IntPtr selectedUnitIds)

    void UiSelectionChanged(
        NativePointer<GameUnitManager> unitManager,
        IntPtr selectedUnitIds,
        int selectedUnitCount)

Die Fix2-Callbacks laufen absichtlich vor dem Vanilla-Trampoline, damit der
Zustand vor dem destruktiven Tribe-Rebuild erfasst werden kann.

### Tribe-Rebuild-Helfer

`TribeSelectionSpeedHook` kennt drei native Funktionen:

- einzelne Einheit einem Tribe zuweisen und neu berechnen,
- passende Einheiten gesammelt zuweisen und neu berechnen,
- abschließend den Selection-Tribe-Zustand aus einem Template kopieren.

Alle drei Vanilla-Funktionen laufen zuerst unverändert. Fix2 beobachtet oder
kopiert danach Tribe-Zustände zurück. Diese Hookpunkte sind funktional gefunden
worden, reichen aber bisher nicht aus, um die per-unit Laufumschaltung zu
verhindern.

Die nutzbaren Signaturen und AOB-Muster lauten:

Einzelne Einheit zuweisen und Tribe neu berechnen:

    48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 41 8B C0 4D 63 F1 33 F6

Cdecl-Signatur:

    void AssignSingleAndRecalculate(
        NativePointer<GameTribeManager> tribeManager,
        int playerId,
        int unitId,
        int tribeId)

Passende Einheiten gesammelt zuweisen und Tribe neu berechnen:

    40 55 56 41 56 48 83 EC 30 44 8B 0D ?? ?? ?? ?? 45 33 D2 48 89 7C 24 58 45 8B F2

Cdecl-Signatur:

    void AssignMatchingAndRecalculate(
        NativePointer<GameTribeManager> tribeManager,
        int playerId,
        int tribeId,
        int matchContext)

Finalen Selection-Template-Zustand kopieren:

    48 83 EC 18 4D 63 C8 49 69 D1 88 06 00 00 83 B9 14 6B 73 00 00 0F 84 ?? ?? ?? ??

Cdecl-Signatur:

    void CopySelectionTribeState(
        NativePointer<GameTribeManager> tribeManager,
        int playerId,
        int tribeId)

Die Detours wurden mit `HookTransaction`,
`TransactionFailureMode.RollbackAndThrow` und Trampoline-zuerst-Verhalten
installiert. Bei einer fehlenden Signatur darf ein zusammengehöriger Fix nicht
teilweise aktiv bleiben.

## Nachweislich funktionierender stärkerer Referenzansatz

Ein bereits getesteter Ansatz belegt, dass sich das gewünschte Endergebnis
technisch erreichen lässt.

Sein Verhalten:

- normaler Klick: Vanilla-Verhalten,
- `Alt+Klick`: alle Einheiten am Maximum der langsamsten Einheit,
- `Ctrl+Klick`: jede Einheit an ihrer eigenen Höchstgeschwindigkeit,
- die gesetzte Direktive bleibt trotz späterer Selection-/Tribe-Änderung bis
  zum nächsten echten Befehl der Einheit erhalten.

Der Ansatz erreicht das durch bewusst stärkere Eingriffe:

1. Er speichert eine `UnitMovementDirective` pro betroffener Unit-ID.
2. Er lässt Vanillas Geschwindigkeitsberechnung zuerst laufen.
3. Im nativen Speed-Hook setzt er bei freier Bewegung
   `r_CurrentSpeed2 = r_CurrentSpeed`.
4. Für synchronisierte Bewegung erhöht er die effektive Verzögerung nur bis zum
   Wert der langsamsten Einheit. Langsamere Terrain-/Zustandseffekte werden
   dadurch nicht aufgehoben.
5. Im Kadenz-Hook korrigiert er `r_SpeedBonus` und die nachgewiesene
   Geh-/Laufanimation.
6. Der erste Lookup ist ein O(1)-Dictionary-Lookup nach Unit-ID; nicht
   betroffene Einheiten verlassen den Hot Path sofort.

Dieser Ansatz funktionierte auch im Test mit mehr als 1000 Einheiten und wurde
für eine Größenordnung bis 10000 Einheiten ausgelegt. Er ist jedoch genau die
Art dauerhafter per-unit Korrektur, die der Neuaufbau möglichst vermeiden soll.

## Gewünschte Vanilla-nahe Architektur

### Normaler Befehl

Bei einem echten normalen Bewegungsbefehl darf `args.MoveType` nicht verändert
werden. Der eingehende Vanilla-Modus bleibt erhalten.

Nach dem Post-Event werden Einheiten- und Tribe-Snapshots nur für
`DefaultInSync` und `Fast` erfasst. Ziel ist, spätere Selection-Folgen von einem
neuen echten Befehl unterscheiden zu können.

### Ctrl-Befehl

Bei einem neuen Bewegungsbefehl wird Ctrl über Unitys Legacy-Input gelesen.
Die Mod setzt vor Vanillas eigentlicher Verarbeitung ausschließlich
`GameTribe.freeUnitSpeeds = 1`. Der gewöhnliche MoveType-Parameter wird nicht
auf einen künstlichen Wert umgeschrieben.

Intern wird dieser Befehl als `TribeMoveType.Fast` gemerkt, damit keine
DefaultInSync-Fallbacks darauf angewendet werden.

Ingame funktionierte dieser schnelle Ctrl-Zustand zuletzt und wurde durch
nachträgliche Selection nicht mehr verändert. Dieser Teil sollte dennoch nach
jeder größeren Selection-Änderung erneut getestet werden.

`Shift+Ctrl+Klick` soll Vanillas Wegpunktfunktion behalten. Alt bleibt unbelegt.

### Verbesserte Speerkämpfer

Nur bei aktivierter verbesserter Speerkämpfer-Einstellung, mindestens zwei
Einheitentypen und `DefaultInSync` wird eine Direktive für Speerkämpfer
registriert.

- Die langsamste normale Gruppen-Höchstgeschwindigkeit ist der größte
  `r_CurrentSpeed`-Verzögerungswert.
- Unterstützen alle beteiligten Typen nachweislich eine Laufanimation, wird
  synchrones Laufen verwendet.
- Andernfalls wird synchrones Gehen erzwungen.
- Nur Speerkämpfer erhalten diese dauerhafte Sonderdirektive.
- Homogene Speerkämpfergruppen und Fast-/Ctrl-Bewegung bleiben Vanilla.

Dieser Fix hat sich in den Tests als notwendig erwiesen, weil der
Speerkämpfer-Handler seinen Bonus nach der Gruppenberechnung erneut setzen kann.

### Selection-Erhaltung

Folgende übereinander entstandenen Versuche wurden erprobt:

- per-unit Beobachtungen nach echten Befehlen,
- Tribe-Snapshots vor einer Selection,
- Erhaltung kompatibler bestehender Tribes,
- Rückschreiben kompatibler Zustände nach Einzel-/Sammelzuweisung,
- Rückschreiben beim finalen Selection-Template-Copy,
- einen nur bei Tribe-ID `0` aktiven Speed-/Kadenz-Fallback,
- einen Pre/Post-Tribe-ID-Guard um den typabhängigen Handler.

Diese Mechanismen sind komplexer geworden, ohne das Selection-Problem
vollständig zu lösen. Für das Minimalziel sollte der nächste Stand wieder
vereinfacht werden.

## Auswertung der bisherigen Versuche

### Nur einen vermuteten Selection-Marker erhalten

Ein früher Versuch wollte nur den vermeintlich entscheidenden
Selection-/Bewegungsmarker erhalten. Das verhinderte die tatsächliche
Geschwindigkeitsänderung nicht. Die Hypothese, ein einzelner bereits bekannter
Marker sei allein verantwortlich, wurde verworfen.

### Tribe-Geschwindigkeitsfelder nach Rebuild zurückschreiben

Das Rückschreiben von fünf und später vierzehn Tribe-Bewegungsfeldern war nur
teilweise erfolgreich:

- Bei vollständigen kompatiblen Rebuilds konnte der vorherige Tribe-Zustand
  tatsächlich vererbt werden.
- Bei partiellen Rebuilds waren häufig nicht alle Einheiten über die erwarteten
  Assignment-Events als getrackt erkennbar. Beispiele aus Logs:
  - 18 zugewiesene Einheiten, 7 getrackt, 11 ungetrackt,
  - 18 zugewiesene Einheiten, 1 getrackt, 17 ungetrackt.
- Solche Gruppen wurden aus Sicherheitsgründen als inkompatibel verworfen.
- Selbst bei einem erfolgreichen finalen Tribe-State-Copy änderten
  typabhängige Handler danach weiterhin `r_SpeedBonus` und Animation einzelner
  Einheiten.

Tribe-Zustand allein ist daher nicht die vollständige Ursache.

### Per-unit Werte nach Selection zurückschreiben

Ein Versuch stellte SpeedBonus und Animation aus dem Zustand unmittelbar vor
der Selection wieder her. Das konnte Beschleunigungen teilweise korrigieren,
verhinderte aber auch legitime spätere Vanilla-Verlangsamungen. Nach Deselection
blieben Archer dadurch zu schnell oder landeten in einem unerwünschten
Zwischenzustand. Der Ansatz wurde deshalb verworfen. Er widerspricht außerdem
dem Ziel, Selection-bedingte Änderungen direkt zu verhindern.

### Tribe-ID-0-Fallback

Ein enger Fallback stellt nur dann synchronisierte Speed-/Kadenzdaten bereit,
wenn eine konkrete beobachtete Einheit während des Selection-Rebuilds
vorübergehend `r_TribeId == 0` hat.

Das verbessert einzelne Übergänge, erklärt aber nicht alle Änderungen:

- Beschleunigungen treten auch bei gültiger und unveränderter Tribe-ID auf.
- Der Fallback kann nur die Auswirkungen nachträglich beeinflussen, nicht den
  auslösenden Vanilla-Zweig verhindern.

### Temporäre letzte gültige Tribe-ID für den Type-Handler

Der jüngste Ansatz hookt direkt vor und nach dem dynamischen Unit-Type-Handler.
Wenn eine beobachtete laufende Einheit gerade Tribe `0` hat, erhält sie nur für
diesen Handler-Aufruf ihre letzte gültige Tribe-ID. Direkt danach wird Tribe
`0` wiederhergestellt.

Analysierte Stellen des nativen Spielmoduls:

- vor dem Handler ungefähr VA `0x180183143`,
- nach dem Handler ungefähr VA `0x180183154`.

Die nachfolgend festgehaltenen AOB-Muster sind gegenüber diesen absoluten
Adressen die maßgebliche Referenz:

Vor dem indirekten Type-Handler-Aufruf:

    48 0F BF 84 19 E6 06 00 00 41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ??

Erste Instruktionen nach demselben Type-Handler-Aufruf:

    8B 15 ?? ?? ?? ?? 48 63 C2 48 69 C8 90 04 00 00 66 83 BC 19 E6 06 00 00 37

Am Pre-Hook enthielt `RDX` die einsbasierte Unit-ID. Der experimentelle Hook
verwendete den gecachten nativen Unit-Array-Zeiger, setzte bei einem lebenden
beobachteten Unit mit Tribe `0` vorübergehend dessen letzte gültige Tribe-ID und
stellte nach dem Handler Tribe `0` wieder her. Dieser komplette Mechanismus ist
als fehlgeschlagen anzusehen; die Muster bleiben nur für weitere Analyse des
Dispatch-Zeitpunkts dokumentiert.

Der Ingame-Test hat diese Hypothese widerlegt:

- Alle Speed-, Kadenz-, Selection-, Tribe-Copy- und Guard-Hooks wurden
  erfolgreich installiert.
- Der Guard griff je nach Selection bei 2, 9, 11 oder 17 Einheiten.
- Trotzdem änderte Vanilla unmittelbar danach weiterhin:
  - `speedBonus = 0 -> 1`,
  - `animation = 0x1 -> 0x81`.
- Die Änderung trat auch bei gültiger unveränderter Tribe-ID auf, zum Beispiel
  `tribe = 487 -> 487`.
- In mehreren Generationen änderten 9 oder 16 von 18 beobachteten Einheiten
  ihren Bewegungszustand, obwohl der Guard aktiv war.

Schlussfolgerung: Der vorübergehende Tribe `0` ist nicht die ausschlaggebende
Bedingung. Der Guard bringt zwei zusätzliche managed Hot-Path-Callbacks um
jeden Type-Handler-Aufruf, ohne das Problem zu lösen, und sollte entfernt
werden.

## Statische Hinweise zum Archer-Type-Handler

In der statischen Analyse des Archer-Handlers wurden vor dem Laufzweig
Bedingungen auf folgenden managerbezogenen Unit-Offets gesehen:

- `+0x8E6`, relativ zu `GameUnit*` ungefähr `+0x28A`,
- `+0x914`, relativ zu `GameUnit*` ungefähr `+0x2B8`,
- `+0xA64`, relativ zu `GameUnit*` ungefähr `+0x408`,
- außerdem Tribe raw `+0x582`.

Der Tribe-Offset raw `+0x582` entspricht wegen des `GameTribe*`-Versatzes von
`0x2A` dem bereits bekannten Feld `GameTribe* + 0x558`, also `PatrolMode`.
Der ursprüngliche und der temporär eingesetzte Tribe hatten in den relevanten
Fällen beide PatrolMode `0`. Das erklärt, weshalb der Tribe-Guard den Laufzweig
nicht verhindert.

Die drei noch nicht sicher benannten Unit-Felder bei relativ `+0x28A`,
`+0x2B8` und `+0x408` sind die aussichtsreichsten nächsten Diagnoseziele.
Insbesondere muss vor und nach Selection sowie direkt vor und nach dem
Type-Handler erfasst werden, welches davon sich ändert.

## Aktuelle Logbefunde und bekannter Diagnosefehler

Letzter ausgewerteter Lauf:

- Logzeitraum: 29.07.2026, etwa 13:36:51 bis 13:37:50
- Installation aller nativen Hooks erfolgreich
- keine direkt von `Troop Movement Fix 2` geloggte Warning oder Exception
- Selection-Diagnosen erreichten jeweils alle 18 beobachteten Einheiten

Es gab jedoch 69 Script-Extender-Fehler:

    GameTribeManagerAPI.TryGetTribeById:
    Tried to access tribe index that was out of range: [0/4500]

Die Ursache liegt sehr wahrscheinlich in
`OnSelectionChangedForDiagnostics()`. Dort wird
`TryCaptureTribeMovementSnapshot(unit->r_TribeId, ...)` auch aufgerufen, wenn
`r_TribeId == 0` ist. `TryCaptureTribeMovementSnapshot()` reicht die `0` ohne
Vorprüfung an `GameTribeManagerAPI.TryGetTribeById()` weiter.

Vor jedem Tribe-API-Lookup muss daher zuerst `tribeId > 0` geprüft werden.

## Effizienz

Positiv:

- Die Zustände sind nach Unit-ID beziehungsweise Tribe-ID in Dictionaries
  organisiert.
- Gruppenscans finden hauptsächlich bei echten Befehlen und Selection-Ereignissen
  statt.
- Es gibt kein dauerhaftes `Update()` und keinen Vollscan pro Frame.
- Detaillierte Selection-Logs sind auf 64 Zeilen begrenzt.
- Wiederholte Restore-Logs werden logarithmisch ausgegeben.
- Der bewährte Speed-/Kadenz-Hook verwirft nicht registrierte Units per
  O(1)-Lookup früh.

Negativ:

- Der wirkungslose Tribe-Guard führt zwei managed Callbacks an einem sehr
  häufigen Type-Handler-Pfad aus.
- Die inzwischen überlagerten Snapshot-, Restore-, Fallback- und Guard-Systeme
  machen die Korrektheit schwer nachvollziehbar.
- Info-Logging ist absichtlich noch sehr ausführlich. Es soll bis zur Lösung
  sichtbar bleiben, erzeugt bei großen Tests aber merkliche Datenmengen.
- Die untersuchten Tribe-Snapshots enthielten vierzehn Felder. Solange nicht feststeht,
  welche Felder wirklich ursächlich sind, ist dieses breite Rückschreiben für
  einen Minimalfix zu invasiv.

## Empfohlener nächster Arbeitsplan

1. Mit einer kleinen neuen Implementierung beginnen und den experimentell
   widerlegten Tribe-ID-Guard nicht übernehmen.
2. Vor jedem `GameTribeManagerAPI.TryGetTribeById()` sicherstellen, dass
   `tribeId > 0` ist, insbesondere beim Pre-Selection-Snapshot.
3. Ctrl/freeUnitSpeeds und den gemischten Improved-Spearman-Fix zunächst
   unverändert und getrennt vom Selection-Experiment lassen.
4. Enge Diagnostik für die drei unbekannten Unit-Felder relativ zu
   `GameUnit*` bei `0x28A`, `0x2B8` und `0x408` hinzufügen:
   - nach einem echten `DefaultInSync`-Befehl,
   - unmittelbar vor dem Selection-Trampoline,
   - direkt vor dem Type-Handler,
   - direkt nach dem Type-Handler,
   - nach Abschluss des Selection-Rebuilds.
5. Archer, Arabian Archer, normale Speerkämpfer, verbesserte Speerkämpfer,
   Swordsmen und Assassins getrennt vergleichen.
6. Den nativen Selection-Code rückwärts vom ersten geänderten Feld verfolgen
   und den genauen Branch oder Write identifizieren.
7. Nur diesen Selection-spezifischen bewegungsverändernden Branch
   unterdrücken. Die eigentliche Selection, UI-Aktualisierung,
   Tribe-Mitgliedschaft und alle echten Befehle müssen weiter durch Vanilla
   laufen.
8. Erst wenn der direkte Branch-Fix funktioniert, die älteren breiten
   Tribe-Restore- und per-unit Fallback-Schichten schrittweise entfernen.

Das Ziel ist nicht, SpeedBonus oder Animation nach jeder Selection
zurückzuschreiben. Der gewünschte Minimalfix verhindert, dass eine reine
Selection überhaupt einen neuen Bewegungszustand erzeugt.

## Implementierungsanforderungen für den Neuaufbau

### Plugin-Identität

Der neue Mod soll weiterhin ein eigenständiges Plugin sein:

- GUID: `TroopMovementFix2_Serp`
- Name: `Troop Movement Fix 2`
- harte BepInEx-Abhängigkeit: Script Extender GUID `000shcdese`
- Laufzeit-Inkompatibilität: Plugin-GUID `TroopMovementFix_Serp`
- Ziel-Framework: .NET Framework 4.8.1

Es werden keine Einstellungen, UI, Lokalisierungen oder Alt-Funktion benötigt.

### Benötigte Bibliotheken

Benötigt werden mindestens:

- `BepInEx`
- `UnityEngine`
- `UnityEngine.CoreModule`
- `UnityEngine.InputLegacyModule`
- `SHCDESE`
- `R3`
- `Zhuqiaomon`
- `PolyHook2.NET`
- `Iced`
- `System.Memory`
- `Microsoft.Extensions.Logging.Abstractions`

Die Paketmetadaten müssen die oben genannte Plugin-Identität, Abhängigkeit und
Inkompatibilität abbilden.

### Script-Extender-Events

Benötigte beziehungsweise bewährte Ereignisse:

- `TribeR3EventHooks.OnTribeIssueOrderMoveHere`
- `TribeR3EventHooks.OnTribeIssueOrderWithTarget`
- `TribeR3EventHooks.OnTribeAssignUnit`
- `UnitR3EventHooks.OnUnitDelete`
- `MapLoaderR3EventHooks.OnUnloadMap`

Wichtige Filter:

- Einen Bewegungsbefehl nur bearbeiten, wenn `IsNewOrder` wahr ist.
- `TribeMoveType.NoChange` ignorieren; dieser Wert wird häufig für interne
  Fortsetzungen, Tiere und KI verwendet.
- Neue Bewegung im Pre-Event vorbereiten und das endgültige Vanilla-Ergebnis
  bei Bedarf erst im Post-Event beobachten.
- Ziel-, Angriffs- und Stoppbefehle müssen gemerkten Bewegungszustand entfernen.
- Unit-Delete entfernt Unit-bezogene Daten.
- Map-Unload leert alle Maps und Dictionaries.

### Minimaler Ctrl-Pfad

Der gewünschte Ctrl-Pfad braucht keine per-unit Maximalgeschwindigkeiten:

1. Nur bei einem neuen echten Move-Here-Befehl Ctrl über
   `Input.GetKey(KeyCode.LeftControl/RightControl)` lesen.
2. Den Tribe über `GameTribeManagerAPI.Instance.TryGetTribeById()` beziehen.
3. Vorher zwingend `tribeId > 0` prüfen.
4. Über `GameTribe* + 0x542` das Vanilla-Feld `freeUnitSpeeds` auf `1` setzen.
5. `args.MoveType` nicht künstlich umschreiben.
6. Vanilla den gewöhnlichen Befehl vollständig ausführen lassen.
7. Intern darf der Befehl als Fast markiert werden, damit kein
   DefaultInSync-Speerkämpfer- oder Selection-Fallback darauf angewendet wird.

Das ist die bisher Vanilla-nächste bekannte Umsetzung. Ob Vanilla das
`freeUnitSpeeds`-Feld bei jedem relevanten Folgepfad selbst zurücksetzt, sollte
im neuen schlanken Stand erneut geloggt werden.

### Minimaler Speerkämpfer-Fix

Für einen engen nativen Speed-/Kadenz-Hook gilt:

1. Den Fix nur bei echtem `DefaultInSync`, aktivierter
   `GamePlayerManagerAPI.Instance.IsImprovedSpearman()`-Option und mindestens
   zwei lebenden Einheitentypen aktivieren.
2. Alle lebenden Tribe-Mitglieder einmal beim Befehl sammeln.
3. Den größten `r_CurrentSpeed`-Wert als langsamste normale
   Höchstgeschwindigkeits-Verzögerung bestimmen.
4. Nur verbesserte Speerkämpfer registrieren.
5. Im Speed-Hook Vanilla zuerst laufen lassen und danach nur dann
   `r_CurrentSpeed2` auf mindestens diesen Verzögerungswert begrenzen.
6. Im Kadenz-Hook den Speerkämpferbonus und die nachgewiesene Animation an die
   Gruppe anpassen.
7. Homogene Speerkämpfergruppen und Ctrl/Fast nicht registrieren.

Dieser Teil ist invasiver als reines Vanilla, aber auf einen bestätigten
Vanilla-Sonderfehler und nur die betroffenen Speerkämpfer begrenzt.

### Minimaler Selection-Forschungsstand

Zu Beginn sollte noch kein breites Restore-System eingebaut werden. Sinnvoller
Start:

1. Normal- und Ctrl-Befehle sowie den Speerkämpfer-Fix sauber voneinander
   trennen.
2. Die zwei bekannten Selection-Einstiegspunkte nur zur engen Diagnostik
   detourieren.
3. Die drei verdächtigen Unit-Felder `+0x28A`, `+0x2B8`, `+0x408` und die
   bekannten Felder vor/nach Selection erfassen.
4. Den ersten tatsächlichen Write oder Branch im Vanilla-Code identifizieren.
5. Erst danach einen transaktionalen nativen Patch ausschließlich für diesen
   Selection-Zweig erstellen.

Damit wird vermieden, die gescheiterten Fix2-Schichten beim Neustart
versehentlich wieder aufzubauen.

### Logging und Lifecycle

- Während der Forschung Info-Level verwenden.
- Jede eigene Logzeile erhält einen Zeitstempel mit Millisekunden.
- Keine teuren Unit-Listen oder String-Erzeugung in nativen Hot Paths.
- Fehler und Warnungen sichtbar lassen.
- Runtime und Hooks statisch beziehungsweise prozessweit am Leben halten.
- Beim frühen `BaseUnityPlugin.OnDestroy()` nichts abmelden oder disposen.
- Keine dauerhafte Funktion auf `Update()`, Coroutine oder die kurzlebige
  Plugin-Komponente stützen.
- Native Hookgruppen transaktional installieren und bei einer fehlenden
  Signatur vollständig zurückrollen.

## Erforderliche Regressionstests

- Homogene Archer: normaler Klick und spätere Selection.
- Homogene Speerkämpfer, Einstellung normales Gehen.
- Homogene Speerkämpfer, Einstellung schneller Speerkämpfer.
- Archer + Arabian Archer.
- Archer + Speerkämpfer.
- Archer + Speerkämpfer + Swordsmen.
- Archer + Assassins, da Assassins eine leicht langsamere eigene
  Höchstgeschwindigkeit besitzen.
- Normaler Klick muss exakt Vanilla bleiben.
- Bottom-Bar-Klick darf die bestehende Bewegung nicht verändern.
- Partielle Selection, Deselection und vollständige Wiederanwahl dürfen die
  bestehende Bewegung nicht verändern.
- Ein neuer echter normaler Befehl darf Geschwindigkeit und Animation wieder
  vollständig durch Vanilla berechnen.
- Ctrl muss jedem Typ seine eigene Höchstgeschwindigkeit geben; Assassins dürfen
  nicht auf Archer-Tempo angehoben werden und Archer dürfen nicht auf
  Assassin-Tempo begrenzt werden.
- Ctrl-Zustand muss Selection-Änderungen überstehen.
- `Shift+Ctrl+Klick` muss weiterhin Wegpunkte setzen.
- Verbesserte Speerkämpfer dürfen in gemischtem `DefaultInSync` keine
  langsamere Einheit überholen.
- Verbesserte Speerkämpfer müssen in homogener Gruppe und bei Ctrl ihre
  verbesserte Vanilla-Geschwindigkeit behalten.
- Angriff, Patrouille, Stop, Zielbefehl, Unit-Delete, Tribe-Wechsel und
  Mapwechsel müssen gespeicherte Mod-Zustände korrekt aktualisieren oder
  entfernen.
- Abschließend mit großen Gruppen bis zu 10000 Einheiten testen.

## Build, Installation und Logs

- Alle Textquellen vor dem Build auf CRLF prüfen.
- Den für den Workspace vorgesehenen erhöhten Build- und Installationsprozess
  verwenden.
- Erwartetes Ergebnis: null Buildfehler und erfolgreiche Installation des
  Plugins.
- Das BepInEx-Prozesslog im Installationsverzeichnis für jeden Ingame-Test
  vollständig auswerten.
- Alle eigenen Logzeilen enthalten Millisekunden-Zeitstempel.
- Während der Forschung werden operative Diagnosen auf Info-Level ausgegeben.

## Lifecycle-Hinweis

Das BepInEx-Manager-GameObject wird in dieser Spielumgebung bereits kurz nach
dem Chainloader-Start zerstört. Deshalb bedeutet `OnDestroy()` nicht, dass der
Mod wirklich entladen wurde.

Der Runtime-Zustand muss statisch gehalten werden; native Hooks sowie
Script-Extender-Abonnements bleiben bei diesem frühen `OnDestroy()` aktiv. Erst
bei `OnApplicationQuit()` wird explizit disposed. Die Lösung darf nicht von
`Update()`, Coroutines oder der Lebensdauer der kurzlebigen
`BaseUnityPlugin`-Komponente abhängen.
