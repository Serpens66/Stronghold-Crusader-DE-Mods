# Feature 04 – Automatische Angriffe auf neutrale Tiere verhindern

## Arbeitsauftrag

Militär, Holzfäller, Jäger im Nahkampf und andere Einheiten sollen neutrale beziehungsweise Natur-Tiere nicht automatisch angreifen. Ein ausdrücklicher manueller AttackUnit-Befehl darf weiterhin ausgeführt und bei passenden Einheiten auch als Befehlsfolge fortgesetzt werden. Feindliche und spielereigene Tiere behalten ihr Vanilla-Kampfverhalten.

Dieses Feature braucht vor dem Fix gezieltes Reverse Engineering. Keine bekannte Adresse ist derzeit ausreichend bestätigt, um direkt eine produktive Verhaltensänderung zu implementieren.

## Verbindliche Policy

Geschützt werden nur:

- Besitzer Natur beziehungsweise neutral
- unterstützte Tierarten, mindestens Kaninchen, Kamel, Huhn und Kuh
- automatische Zielakquisition

Nicht geschützt werden:

- explizit per AttackUnit angegriffenes Einzeltier
- die nachvollziehbare Fortsetzung eines gültigen manuellen Angriffsbefehls, soweit Vanilla sie vorsieht
- feindliche Tiere
- spielereigene Tiere
- die dedizierte Hunter Query eines arbeitenden Jägers

Die Policy ist unabhängig davon, ob der betreffende Typ als Jagdbeute in den Modsettings aktiviert ist. Ein neutraler Hase soll nicht vom Holzfäller automatisch geprügelt werden, auch wenn HuntRabbit deaktiviert ist.

## Warum frühere Workarounds nicht genügen

Eine ältere Modfassung räumte nachträglich Ziele nicht jagender Fernkämpfer auf und ließ über eine kurze AttackUnit-Ausnahme manuelle Befehle zu. Das wurde wieder entfernt, weil Einheiten in Schussanimationen hängen blieben. Ein Ziel nach der Zuweisung zu löschen repariert den stabilen Übergang nicht.

Auch diese Ansätze sind ungeeignet:

- Damage- oder Projectile-Events: zu spät
- Tier nach einem Schuss sofort töten: behebt nur Jägerfehlschüsse, nicht Auto-Aggro
- wiederholtes Leeren des Zielslots: erzeugt Animations-/AI-Zustandsinkonsistenzen
- globaler Schutz aller Besitzer: würde feindliches Vanilla-Verhalten verändern

Der Eingriff muss am frühesten gemeinsamen Kandidatenfilter vor der automatischen Zielzuweisung erfolgen.

## Vorhandene Anhaltspunkte

- TribeR3EventHooks.OnTribeIssueOrderWithTarget existiert.
- TribeAICommand.AttackUnit hat den Wert 4.
- GameUnit besitzt r_AI_LastIssuedTribeCommand sowie Kontext-/Ziel-IDs.
- Damit lässt sich ein expliziter manueller Befehl grundsätzlich erfassen und mit stabiler Angreifer-/Zielidentität markieren.
- Ein bereits gefundener Kandidat um RVA 0x1827C0 scheint eher Cursor-/UI-Auswahl zu sein und ist nicht als Auto-Aggro-Selektor bestätigt. Er darf ohne Laufzeitbeweis nicht für den Fix verwendet werden.
- Der Script Extender bietet derzeit keinen frühen allgemeinen Auto-Acquisition-Callback.

## Reverse-Engineering-Plan

### 1. Kontrollierte Reproduktionsmatrix

Auf einer kleinen Testkarte Fälle einzeln reproduzieren:

- neutraler Hase neben Holzfäller
- neutraler Hase neben Nahkämpfer
- neutrales Huhn neben Fernkämpfer
- neutrale Kuh neben Fernkämpfer
- manuell befohlener Angriff derselben Einheit
- feindliches Tier
- spielereigenes Tier
- arbeitender Jäger mit regulärer Hunter Query

Je Fall Angreifer-ID, Ziel-ID, Unit-Typ, Besitzer, AI-State, letzten Tribe-Befehl, Zielslots und Animations-/Angriffsstatus zeitlich korrelieren.

### 2. Früheste Ziel-Writer finden

Ziel ist nicht irgendein später Leser, sondern der früheste gemeinsame Writer der automatischen Kampfziel-ID beziehungsweise dessen Kandidatenevaluator.

Vorgehen:

- bekannte GameUnit-Zielfelder und deren native Offsets aus öffentlicher Assembly und Extender-Feldlayout verifizieren
- in der kanonischen DLL gezielt nach Writern dieser Felder suchen
- Callgraph nur lokal um bestätigte Writer verfolgen
- automatischen Fall und manuellen AttackUnit-Fall vergleichen
- dedizierten Jägerpfad von generischer Kampfaquisition unterscheiden
- Kandidatenfunktion 0x1827C0 nur als Hypothese behandeln und durch echte Schreib-/Callbackkorrelation bestätigen oder verwerfen

Kein vollständiges aaa, solange zielgerichtete Xrefs, Signaturen und kleine Scanner ausreichen.

### 3. Diagnosehook vor Filter

Der erste Hook ist diagnostisch und ändert kein Verhalten. Er muss:

- alle problematischen Einheitentypen erreichen
- Kandidaten vor Zielzuweisung sehen
- hook confirmed auch in einem Vanilla-Nichttierfall melden
- expliziten AttackUnit-Kontext erkennbar machen
- dedicated hunter path erkennbar ausschließen oder markieren
- ungültige IDs mindestens einmal als Fehler melden
- Logs pro stabiler Angreifer-/Zielidentität drosseln

Erst wenn die Invariante Kandidatenentscheidungen = akzeptiert + abgelehnt für reproduzierbare Fälle stimmt, wird eine Policy aktiviert.

## Geplante Implementierung

Eine separate Klasse AutomaticAnimalTargetFilterHook.cs:

1. Vanilla-Kontext sicher erfassen.
2. Wenn Mod deaktiviert: unverändert.
3. Wenn kein unterstütztes Tier: unverändert.
4. Wenn Besitzer nicht neutral/Natur: unverändert.
5. Wenn dedizierter Hunter-Query-Pfad: unverändert.
6. Wenn gültige manuelle AttackUnit-Lineage für dieses Ziel vorliegt: unverändert.
7. Sonst Kandidat ablehnen, bevor ein Kampfziel geschrieben oder eine Angriffsanimation begonnen wird.

### Manuelle Befehlslineage

OnTribeIssueOrderWithTarget erfasst für AttackUnit:

- Zeitstempel mit Stopwatch.GetTimestamp
- Angreifer beziehungsweise Tribe/ausgewählte Units
- Zielslot und globale Ziel-ID
- Befehlssequenz oder einen anderen stabilen Kontext, falls verfügbar

Eine reine kurze Zeit-Gnadenfrist ist nur zulässig, wenn Tests belegen, dass sie keine automatische Folgeakquisition durchrutschen lässt. Besser ist eine explizite Lineage aus LastIssuedTribeCommand und Kontextziel. Alte Einträge bei neuer Order, ungültiger ID, Tod oder terminalem Zustandswechsel entfernen.

Die gewünschte Vanilla-Fortsetzung nach manuellem Befehl muss konkret getestet werden: Wenn mehrere Bogenschützen nach dem befohlenen ersten Tier weitere Tiere angreifen sollen, darf die Ausnahme nicht unbegrenzt jede spätere automatische Akquisition freigeben. Das genaue Produktverhalten ist im Testprotokoll festzuhalten.

## Laufzeittests

| Fall | Erwartung |
|---|---|
| neutraler Hase bei Nahkämpfer | kein Autoangriff |
| neutraler Hase bei Holzfäller | kein Autoangriff |
| neutrales Huhn/Kuh bei Fernkämpfer | kein Autoangriff, keine Schussanimation |
| manueller AttackUnit auf neutrales Tier | Angriff funktioniert |
| feindliches Tier | Vanilla-Autoangriff |
| spielereigenes Tier | Vanilla-Verhalten |
| arbeitender Jäger | reguläre Jagd nicht blockiert |
| Mod deaktiviert | Vanilla-Verhalten |
| Ziel verschwindet während manuellem Angriff | Einheit fällt sauber in Vanilla-Zustand zurück |

Nach jedem Fall prüfen:

- keine Einheit hängt in Angriff/Schuss
- kein unsichtbares Ziel bleibt reserviert
- kein ständig wiederholtes Ablehnungslog
- manuelle und automatische Lineage sind in den Logs unterscheidbar

## Abnahmekriterien

- Der Hookpunkt ist durch Writer-/Laufzeitkorrelation belegt.
- Neutrale Tiere werden vor Zielzuweisung abgelehnt.
- Manuelle AttackUnit-Befehle funktionieren.
- Jäger, Feind- und Spielereigentum bleiben korrekt.
- Keine nachträgliche Zielslot-Löschung als Hauptfix.
- UpdateToNewDLL.md dokumentiert Hash, RVA, Pattern, Register, Feldlayout und Testfälle.
- Mod aus entspricht Vanilla.

## Ergebnisse und offene Punkte

Noch nicht bearbeitet. Der erste Chat darf mit einem sauberen, dokumentierten Diagnoseergebnis enden, falls die produktive Hookstelle noch nicht zweifelsfrei bewiesen ist. Keine spekulative Verhaltensänderung erzwingen.

## Startprompt für einen neuen Chat

Bearbeite Feature 04 aus ImprovedHunters/Plans/04-NeutralAnimalAutoAttacks.md. Lies zuerst ImprovedHunters/PLAN.md. Beginne mit zielgerichtetem Reverse Engineering und einem diagnostischen Hook am frühesten gemeinsamen automatischen Zielwahlpunkt; implementiere keinen Fix an einer nur vermuteten Funktion. Belege die Trennung zwischen Auto-Aggro, manuellem AttackUnit und dedizierter Jägerlogik. Wenn der Hookpunkt sicher ist, implementiere die dort definierte Policy und Tests. Aktualisiere Ergebnisse und UpdateToNewDLL.md. Keine Script-Extender-Änderung in diesem Chat.
