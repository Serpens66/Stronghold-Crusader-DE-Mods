# AIDefensePatrolTest – Vanilla-Analyse und Implementierungsübergabe

## 1. Zweck und Ergebnis in Kurzform

Dieses Dokument untersucht den Vanilla-Fehler, durch den äußere KI-Verteidigungspatrouillen das Wiederauffüllen der eigentlichen Burgverteidigung verhindern können. Es enthält noch keinen Mod-Code. Sein Zweck ist, einem neuen Chat beziehungsweise Entwickler genügend belegte Informationen für einen eigenständigen Testmod und einen späteren Fix zu geben.

Das zentrale Ergebnis lautet:

- Vanilla führt Burgverteidiger und äußere Patrouilleneinheiten in einem gemeinsamen Gesamtzähler.
- Dieser Gesamtzähler wird korrekt gegen `DefTotal` verwendet, um zu entscheiden, ob weitere Verteidiger rekrutiert werden müssen.
- Derselbe Gesamtzähler wird aber fälschlich auch gegen `DefWalls` verwendet, um die frisch rekrutierte Einheit entweder der Burgverteidigung oder einer äußeren Patrouille zuzuweisen.
- Dadurch enthält die Zuweisungsentscheidung keine Information darüber, ob gerade ein Burgverteidiger oder eine Patrouilleneinheit fehlt.
- Der minimale, risikoärmste Fix ist deshalb, nur diese Zuweisungsentscheidung durch eine tatsächliche Zählung der KI-Rolle 1 zu korrigieren. Die Rekrutierungsgrenze `DefTotal` sollte unverändert bleiben.

### 1.1 Vollständige Fehlerbeschreibung aus Spielersicht

Die defensive Truppensteuerung eines KI-Lords besitzt zwei zentrale Zahlenwerte:

- `DefWalls`: Wie viele defensive Einheiten die vorgesehenen Verteidigungspositionen der Burg besetzen sollen.
- `DefTotal`: Wie viele defensive Einheiten der Lord insgesamt unterhalten soll. Darin sind sowohl Burgverteidiger als auch äußere Schutzpatrouillen enthalten.

Ist `DefTotal` größer als `DefWalls`, verwendet Vanilla die Differenz als äußere Patrouille. Diese Einheiten werden nicht auf die eigentlichen Burgverteidigungspositionen gestellt, sondern in Gruppen um die Wirtschaftsgebäude außerhalb beziehungsweise am Rand der Burg geschickt. Das ist grundsätzlich nützlich, weil solche Patrouillen Überfälle abfangen und abgelegene Produktionsgebäude schützen können.

Beispiel:

- `DefWalls = 120`
- `DefTotal = 140`
- beabsichtigtes Ergebnis: 120 Burgverteidiger und 20 äußere Patrouilleneinheiten

Beim erstmaligen Aufbau der Armee entsteht dieser Split normalerweise korrekt. Das Problem beginnt, sobald Einheiten sterben:

1. Angreifer töten einige der 120 Burgverteidiger.
2. Die äußere Patrouille kann gleichzeitig vollständig oder größtenteils am Leben bleiben.
3. Weil die defensive Gesamtzahl nun unter `DefTotal` liegt, rekrutiert die KI korrekterweise Ersatztruppen.
4. Ein Teil oder alle dieser neuen Truppen werden jedoch der äußeren Patrouille zugeteilt, statt die freigewordenen Burgpositionen zu besetzen.
5. Die Patrouille wächst dadurch über ihre ursprünglich aus `DefTotal - DefWalls` abgeleitete Größe hinaus.
6. Sobald die gemeinsame Gesamtzahl wieder `DefTotal` erreicht, beendet die KI die Ersatzrekrutierung, obwohl weiterhin Burgverteidigungspositionen unbesetzt sind.

Bei wiederholten Angriffen kann sich der Zustand immer weiter verschlechtern: Die Burgverteidigung nimmt dauerhaft ab, während immer mehr der defensiven Armee in den äußeren Patrouillen gebunden ist. Besonders deutlich kann das nach einer Panic-/Notfallrekrutierung werden, weil dabei mehrere defensive Einheiten in kurzer Folge beziehungsweise innerhalb desselben Rekrutierungsablaufs entstehen und die Fehlzuweisung vervielfacht werden kann.

Der derzeitige AIC-seitige Workaround besteht darin, `DefWalls` mindestens so hoch wie `DefTotal` zu setzen. Dann bleibt keine positive Differenz für eine äußere Patrouille und Vanilla kann keine solchen Gruppen aufbauen. Dieser Workaround verhindert den Fehler, entfernt aber gleichzeitig die gewünschte und taktisch sinnvolle Patrouillenfunktion. Er ist daher nur ein Behelf und kein eigentlicher Fix.

Erwartetes Verhalten nach einem Bugfix:

- `DefTotal` begrenzt weiterhin den gesamten defensiven Bestand.
- Solange weniger als `DefWalls` tatsächliche Burgverteidiger leben, müssen neue defensive Rekruten zuerst diese Burgpositionen auffüllen.
- Erst wenn die Burgverteidigung wieder vollständig ist, dürfen weitere defensive Rekruten den für Patrouillen verbleibenden Anteil bis `DefTotal` bilden.
- Stirbt nur eine Patrouilleneinheit, soll ihr Ersatz weiterhin der Patrouille zugewiesen werden, sofern die Burgquote bereits erfüllt ist.
- Panic-Rekrutierung darf diese Priorität nicht umgehen.

### 1.2 Zusätzlich gewünschtes Feature: getrennte Patrouillenkomposition

Neben dem eigentlichen Bugfix ist langfristig eine eigenständige Konfiguration für äußere Schutzpatrouillen gewünscht. Gegenwärtig rekrutiert Vanilla Burgverteidiger und Patrouilleneinheiten aus derselben Liste `defensive_troops1..8`. AIC-Autoren können daher zwar über `DefTotal - DefWalls` ungefähr die gewünschte Patrouillenstärke bestimmen, aber nicht getrennt festlegen, aus welchen Einheitentypen diese Patrouille bestehen soll.

Das führt beispielsweise dazu, dass ein Lord mit Schwertkämpfern in seiner normalen Verteidigung auch langsame Schwertkämpfer in die äußere Patrouille einordnet. Nach der zugrunde liegenden Benutzerbeobachtung kann eine langsame Einheit die Bewegung der gesamten Patrouillengruppe bremsen. Für den Schutz weit auseinanderliegender Wirtschaftsgebäude wären dagegen häufig beweglichere Einheiten wie berittene Bogenschützen, Armbrustschützen oder Plänkler erwünscht. Die konkrete Geschwindigkeitsaggregation einer gemischten Gruppe ist in dieser Analyse noch nicht nativ bewiesen; das Konfigurationsproblem selbst besteht unabhängig davon.

Das gewünschte spätere Feature soll deshalb mindestens folgende fachliche Trennung ermöglichen:

- Burgverteidiger behalten `DefWalls` und ihre bestehende defensive Einheitenauswahl.
- Äußere Patrouillen erhalten eine eigene Einheitentyp-Auswahl, unabhängig von `defensive_troops1..8`.
- Die gewünschte Patrouillenstärke bleibt zunächst aus `DefTotal - DefWalls` ableitbar, sofern nicht später bewusst ein zusätzlicher eigener Größenwert eingeführt wird.
- AIC-/Lord-Autoren sollen die Patrouillenkomposition pro Lord bestimmen können.
- Der Bugfix muss auch ohne diese Erweiterung vollständig funktionieren; die getrennte Komposition ist ein nachgelagertes Feature und keine Voraussetzung für die Wiederauffüllung der Burgverteidigung.

Wichtig für die technische Umsetzung: Die bestehenden Felder `economy_protection_number` und `economy_protection_type` sind keine äußere Patrouillenkonfiguration. Sie steuern eine andere, reaktive KI-Kategorie. Eine spätere getrennte Patrouillenliste benötigt deshalb eine mod-eigene Erweiterung oder einen zusätzlichen Eingriff in die defensive Einheitenauswahl; ein vorhandenes Vanilla-Feld kann dafür nicht einfach umgedeutet werden.

## 2. Pfadkorrektur für die Vanilla-AIC-Dateien

Der ursprünglich angegebene Literalpfad

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\ActiveAIVDetector\BepInEx\plugins\ActiveAIVDetector\_Serp\VanillaAIC`

existiert nicht. Gemäß der Workspace-Regel für möglicherweise durch Markdown verfälschte Unterstriche wurde zusätzlich die Variante mit zusammengezogenem Unterstrich geprüft. Tatsächlich gefunden und für diese Analyse verwendet wurde:

`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\ActiveAIVDetector\BepInEx\plugins\ActiveAIVDetector_Serp\VanillaAIC`

## 3. Untersuchungsbasis und Gültigkeitsgrenze

### 3.1 Kanonische Binärdatei

- Spiel: Stronghold Crusader Definitive Edition
- Steam-Build: `24816905`
- Kanonische DLL: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Semantischer Kurzordner: `FBCB9319`

Der Hash der installierten DLL und `currentNativeHash` aus `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` stimmen überein. Alle nachfolgenden RVAs, VAs, Instruktionsfolgen und nativen Schlussfolgerungen sind ausdrücklich an diesen vollständigen Hash gebunden.

Bei einer anderen Spielversion dürfen die Adressen nicht ungeprüft verwendet werden. Ein späterer Mod muss bei abweichendem Hash entweder ein eindeutig validiertes semantisches Pattern auflösen oder fail-closed keinen Hook installieren.

### 3.2 Verwendete Quellen

- `_inspect/CrusaderDE-Native-Baseline/CURRENT.md`
- `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`
- `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/exports/semantic-decompiled-functions.c`
- die Roh- und semantischen Funktions-, Xref- und Disassembly-Daten derselben Baseline
- `shcde-script-extender/src/SHCDESE.BepInEx/Interop/InternalAIC.cs`
- `shcde-script-extender`-Definitionen von `GameUnit`, Unit-Manager und Hook-Infrastruktur
- lokales Vergleichsprojekt `..\Stronghold Crusader HD reversed`
- Vanilla-Lorddateien im oben korrigierten `VanillaAIC`-Ordner
- vorhandene lokale Hook-Muster, besonders `ExtraFeatures` und `BugfixesAndQoL`
- der direkte Tribe-Unassign-Adapter in `AIDefense`

Die Namen im Format `FUN_...` sind von der aktuellen Baseline vergebene native Kandidatennamen und keine bestätigten Firefly-Symbolnamen. Wo ein funktionales HD-Gegenstück bekannt ist, wird es gesondert genannt. Die beschriebenen Instruktionen und Datenflüsse sind für den genannten Hash direkt belegt; die semantische Benennung bleibt davon getrennt.

## 4. Relevante AIC- und lordjson-Felder

Die Definitive Edition verwendet im untersuchten `InternalAIC` folgende Felder:

| lordjson-/AIC-Feld | DE-Offset | Bedeutung |
|---|---:|---|
| `economy_protection_number` | `0x150` | Größe einer reaktiven Wirtschafts-Schutztruppe, KI-Rolle 6 |
| `economy_protection_type` | `0x154` | Einheitentyp dieser reaktiven Wirtschafts-Schutztruppe |
| `bodyguard_number` | `0x158` | Leibwächterzahl |
| `bodyguard_type` | `0x15C` | Leibwächtertyp, KI-Rolle 7 |
| `defense_patrol_trigger_level` | `0x174` | Gesamtziel für Burgverteidiger plus äußere Patrouillen; HD-Name `DefTotal` |
| `defense_patrols` | `0x178` | Anzahl äußerer Patrouillengruppen |
| `defense_patrol_style` | `0x17C` | Bewegungsstil äußerer Patrouillengruppen |
| `defense_patrol_delay` | `0x180` | Sammel-/Verzögerungswert der äußeren Patrouillen |
| `defensive_trigger_level` | `0x184` | gewünschte Zahl eigentlicher Burg-/Wallverteidiger; HD-Name `DefWalls` |
| `defensive_troops1..8` | `0x188–0x1A4` | gemeinsame Rekrutierungsliste für Burgverteidiger und äußere Patrouillen |

Im älteren HD-Aufbau lagen die entsprechenden Felder wegen eines leicht anderen vorangehenden Layouts typischerweise vier Bytes früher: `DefTotal` bei `0x170`, die drei äußeren Patrouillenfelder bei `0x174–0x17C`, `DefWalls` bei `0x180` und `DefUnit1..8` bei `0x184–0x1A0`. Für SHCDE sind ausschließlich die oben genannten DE-Offsets maßgeblich.

### 4.1 `economy_protection_*` ist keine Patrouillenkomposition

Die ähnlich klingenden Felder `economy_protection_number` und `economy_protection_type` dürfen nicht als bereits vorhandene Konfiguration der äußeren Patrouille interpretiert oder für einen Fix umgenutzt werden.

Die aktuelle native Funktion `FUN_180040430`, RVA `0x40430`, ist über den String `train_economy_protection_troops` zugeordnet. Sie erzeugt eine separate, reaktive Schutztruppe und weist deren Einheiten KI-Rolle 6 zu. Der Bedarf hängt neben dem AIC-Grundwert auch von jüngsten Angriffen auf Wirtschaftsgebäude beziehungsweise Arbeiter ab. Äußere Verteidigungspatrouillen verwenden dagegen Rolle 4.

Auch `bodyguard_*` ist separat: `FUN_180040230` weist Leibwächtern Rolle 7 zu.

### 4.2 Konkretes Vanilla-Beispiel

Die untersuchten Rat-Daten verwenden sinngemäß:

- `DefTotal = 30`
- `DefWalls = 20`
- `defense_patrols = 1`

Der beabsichtigte, beim erstmaligen Aufbau auch erreichte Split ist damit:

- 20 Einheiten als Burg-/Wallverteidiger, Rolle 1
- 10 Einheiten als äußere Patrouille, Rolle 4

## 5. Native Rekrutierungs- und Zuweisungslogik

### 5.1 Hauptfunktion

- Kandidatenname: `FUN_180040740`
- VA: `0x180040740`
- RVA: `0x40740`
- Größe: 1645 Bytes
- Funktionales HD-Gegenstück: `aiRecruitUnits`, im lokalen HD-Reverse-Engineering bei `0x004D3AE0`

Der aktuelle KI-Scheduler `FUN_18002AE40`, RVA `0x2AE40`, ruft diese Funktion im KI-Ablauf auf. Der ältere HD-Scheduler ist als `updateAIBehaviour` dokumentiert und ruft sein Gegenstück regelmäßig auf.

Für die defensive Rekrutierungsart liest `FUN_180040740` `defense_patrol_trigger_level` bei AIC-Offset `0x174`. Ist der gemeinsame Verteidigerbestand bereits mindestens so groß wie dieses Gesamtziel, wird keine weitere defensive Einheit benötigt. Diese Verwendung von `DefTotal` ist fachlich sinnvoll und nicht die zu korrigierende Stelle.

Die zu rekrutierende Einheit wird zyklisch aus `defensive_troops1..8`, beginnend bei AIC-Offset `0x188`, ausgewählt. Dieselbe Liste versorgt sowohl Rolle 1 als auch Rolle 4; Vanilla besitzt an dieser Stelle keine getrennte Einheitentyp-Liste für äußere Patrouillen.

### 5.2 Fehlerhafte Zuweisungsentscheidung

Nach erfolgreicher Rekrutierung entscheidet Vanilla anhand desselben gemeinsamen Bestandszählers:

```text
if (combinedRole1AndRole4Count < aic.defensive_trigger_level)
    assignNewUnitToCastleDefense();  // Rolle 1
else
    assignNewUnitToOuterPatrol();    // Rolle 4
```

Der relevante aktuelle Maschinenblock lautet vollständig:

```text
0x180040C97  mov rax,[rsp+38]
0x180040C9C  mov rcx,rbp
0x180040C9F  mov eax,[rax+rbp+0x184]
0x180040CA6  cmp dword [rsi+rdx+0x379deb8],eax
0x180040CAD  mov edx,ebx
0x180040CAF  jl 0x180040CB8
0x180040CB1  call 0x180029430
0x180040CB6  jmp 0x180040CE4
0x180040CB8  call 0x1800291F0
```

Interpretation:

- Der Load bei `0x180040C9F` liest `defensive_trigger_level`/`DefWalls` aus dem aktiven AIC-Datensatz.
- Der Compare bei `0x180040CA6` vergleicht diesen Sollwert mit dem gemeinsamen Spielerzähler.
- Der neue Unit-Identifier befindet sich an dieser Stelle in `EBX` und wird anschließend über `EDX` als zweites Argument übergeben.
- `jl` führt bei scheinbarem Unterbestand zur Burgverteidigung.
- Der Fall ohne Sprung ruft die äußere Patrouillenzuweisung auf.

### 5.3 Herkunft und Bedeutung des gemeinsamen Zählers

- Kandidatenname: `FUN_180182B00`
- RVA: `0x182B00`
- Größe: 9137 Bytes
- Funktionale Aufgabe: umfangreiche Unit-Aktualisierung beziehungsweise Zählerpflege

Der dekompilierte Switch über `r_AITribeRole` erhöht für Rolle 1 und Rolle 4 denselben spielerbezogenen Zähler. Rolle 6 und Rolle 7 erhöhen dagegen jeweils andere Zähler.

Die relevanten öffentlichen Unit-Felder sind:

- `r_ControllableForPlayerId` bei Unit-Offset `0x0092`
- `r_AITribeRole` bei Unit-Offset `0x0426`

GameUnit-IDs sind 1-basiert; direkte Unit-Spanindizes sind 0-basiert. Ein späterer Fix muss den im Workspace dokumentierten ID-/Indexvertrag strikt einhalten und darf die Basis nicht aus Wertebereichen erraten.

## 6. Exakte Fehlerursache

Vanilla kann den gewünschten Split nur beim monotonen Aufbau von null korrekt herstellen:

1. Solange der gemeinsame Zähler kleiner als `DefWalls` ist, gehen neue Einheiten in Rolle 1.
2. Danach gehen weitere Einheiten bis `DefTotal` in Rolle 4.
3. Damit entsteht zunächst der gewünschte Split.

Nach Verlusten geht die für die Zuweisung notwendige Information verloren:

1. Ein Burgverteidiger der Rolle 1 stirbt.
2. Der gemeinsame Zähler sinkt um eins; deshalb wird korrekt eine Ersatzrekrutierung ausgelöst.
3. Überlebende Patrouilleneinheiten der Rolle 4 bleiben aber im gemeinsamen Zähler enthalten.
4. Liegt dieser Gesamtwert weiterhin bei oder oberhalb `DefWalls`, wählt Vanilla erneut Rolle 4.
5. Der gemeinsame Bestand erreicht wieder `DefTotal`, aber der Rolle-1-Bestand bleibt unter Soll.
6. Bei Wiederholung kann sich die Zusammensetzung immer weiter von der Burgverteidigung zur äußeren Patrouille verschieben.

Wenn stattdessen eine Rolle-4-Einheit stirbt und die Rolle-1-Quote noch vollständig ist, funktioniert der Ersatz zufällig korrekt: Der gemeinsame Zähler liegt weiterhin bei oder oberhalb `DefWalls`, daher entsteht erneut Rolle 4.

Der Fehler ist somit kein falsches `DefTotal`, sondern die Verwendung von `role1 + role4` an einer Stelle, die den tatsächlichen Rolle-1-Bestand benötigt.

### 6.1 Verstärkung durch Batch- und Panic-Rekrutierung

Ein einzelner Aufruf kann abhängig vom KI-Zustand mehrere Einheiten rekrutieren. Normale Bedingungen erlauben mehrere Rekrutierungen; ein Nervous-/Panic-Zustand kann den Batch weiter erhöhen. Die Funktion verwendet innerhalb dieser Schleife teilweise die vor dem Batch ermittelten Zählerwerte, statt nach jeder frisch zugeteilten Einheit alle globalen Zähler neu zu berechnen.

Dadurch können Fehlzuweisungen und Überschreitungen innerhalb eines einzigen Aufrufs verstärkt werden. Ein Fix, der bei jeder Zuweisung den aktuellen Rollenbestand direkt aus den Unit-Daten zählt, kann diesen Verstärker nebenbei vermeiden.

## 7. Relevante Vanilla-Helfer

### 7.1 Burg-/Wallverteidigung

- Kandidatenname: `FUN_1800291F0`
- RVA: `0x291F0`
- Größe: 248 Bytes

Die Funktion setzt die neue Einheit auf KI-Rolle 1, sucht beziehungsweise erzeugt einen passenden Wall-/AIV-Verteidigungsposten und fügt die Einheit dem zugehörigen Tribe hinzu.

### 7.2 Äußere Patrouille

- Kandidatenname: `FUN_180029430`
- RVA: `0x29430`
- Größe: 119 Bytes

Die Funktion setzt KI-Rolle 4, wählt über `FUN_18002BAE0` eine äußere Patrouillengruppe und fügt die Einheit dem ausgewählten Tribe hinzu. In der aktuellen nativen Callgraph-Auswertung ist die beschriebene Rekrutierungsfunktion der relevante Aufrufer.

### 7.3 Patrouillengruppenauswahl

- Kandidatenname: `FUN_18002BAE0`
- RVA: `0x2BAE0`
- Größe: 348 Bytes

Die Funktion berücksichtigt die konfigurierte Zahl äußerer Patrouillengruppen, bevorzugt eine kleine beziehungsweise am wenigsten belegte Gruppe und kann bei Bedarf eine Gruppe erzeugen.

### 7.4 Tribe-Zuweisung

- Kandidatenname: `FUN_18011D370`
- RVA: `0x11D370`
- Größe: 172 Bytes

Die Funktion fügt eine Einheit einem Tribe hinzu und schreibt die Mitgliedschaftsdaten. Es ist nicht erkennbar, dass sie vorher automatisch eine vorhandene Mitgliedschaft sauber entfernt.

Folgerung: Eine bereits patrouillierende Rolle-4-Einheit darf nicht einfach nachträglich an `FUN_1800291F0` übergeben werden. Eine Zustandsreparatur muss sie zuerst über den korrekten nativen Vertrag aus dem alten Tribe entfernen.

## 8. Fixvarianten

### 8.1 Empfohlener erster Test: Vanilla-Zuweisungszweig korrigieren

Der kleinste Eingriff ist ein Context-Hook unmittelbar vor dem fehlerhaften Compare. Der vollständige 15-Byte-Kandidatenblock ist:

- Start-RVA: `0x40C97`
- End-RVA exklusiv: `0x40CA6`
- Enthaltene vollständige Instruktionen: 5 + 3 + 7 Bytes
- Verdrängte Wirkung: AIC-Basis laden, `RCX` vorbereiten und `DefWalls` nach `EAX` laden

Der Hook sollte `OverwrittenInstructionPlacement.BeforeCallback` verwenden. Bei dieser Platzierung werden die verdrängten Instruktionen vor dem Callback ausgeführt; im Callback enthält `EAX` deshalb bereits den ursprünglichen Vanilla-Wert `DefWalls`. `EBX` enthält die frisch rekrutierte Unit-ID.

Geplanter Callback:

1. Originales `EAX` sichern, damit ein Fehler auf Vanilla-Verhalten zurückfallen kann.
2. Unit-ID aus `EBX` lesen.
3. Die Einheit über eine ID-API auflösen und ihren Besitzer aus `r_ControllableForPlayerId` bestimmen.
4. Alle lebenden Einheiten dieses Besitzers ID-korrekt durchlaufen.
5. Tatsächliche Rolle-1- und Rolle-4-Einheiten zählen.
6. Wenn `role1Count < originalDefWalls`, den folgenden Compare sicher auf den Wall-Zweig lenken.
7. Andernfalls den Patrouillenzweig wählen lassen.

Vorgeschlagene Vergleichssteuerung:

- Wall erforderlich: `EAX = int.MaxValue`
- Wall vollständig: `EAX = int.MinValue`

Der native Gesamtzähler ist nicht negativ. Damit bleibt die beabsichtigte signed-`jl`-Entscheidung eindeutig, ohne den globalen Vanilla-Zähler zu verändern. Vor der Implementierung muss trotzdem nochmals statisch bestätigt werden, dass `EAX` nach dem Compare auf keinem Zweig anderweitig benötigt wird und dass keine nachfolgend benötigten Flags bereits vor dem Compare erhalten werden müssen.

Bei jeder Ausnahme oder ungültigen Unit-/Owner-Auflösung muss der Callback das originale `EAX` unverändert lassen. Vanilla läuft dann fail-open mit seinem bisherigen Verhalten weiter; der Hook darf das Spiel nicht wegen einer Diagnoseunsicherheit abbrechen.

Vorteile:

- Die neue Einheit ist noch nicht der falschen Patrouille zugeteilt.
- Beide vorhandenen Vanilla-Helfer bleiben unverändert zuständig für Rolle, Tribe und Zielposition.
- Es ist keine riskante nachträgliche Tribe-Migration erforderlich.
- Mehrere Rekrutierungen im selben Batch sehen nach jeder bereits erfolgten Zuweisung den aktualisierten Rolle-1-Bestand.
- Die korrekte `DefTotal`-Rekrutierungsgrenze bleibt erhalten.

Einschränkung:

Ein bereits vollständig fehlentwickelter Zustand wird nicht sofort repariert, wenn der Gesamtbestand bereits `DefTotal` erreicht hat. Der Zweigfix greift erst wieder, sobald Vanilla eine neue defensive Einheit rekrutiert.

### 8.2 Pattern- und Hookvalidierung

Der dokumentierte Maschinenblock darf nicht allein anhand der RVA gepatcht werden. Für die spätere Implementierung ist vorzusehen:

- Referenz-RVA nur bei exaktem bekanntem Hash akzeptieren.
- Zusätzlich mit `Shared.NativePatternResolver.ResolveUnique` ein semantisch enges Pattern in ausführbaren `.text`-Bereichen auflösen.
- RIP-relative globale Displacements und relative Call-Displacements im Fallbackpattern maskieren.
- Eindeutigkeit erzwingen: null oder mehrere Treffer deaktivieren das Feature.
- Nach Auflösung die Instruktionsgrenzen und die erwarteten Operanden validieren.
- Die beiden Call-Ziele als `FUN_180029430` und `FUN_1800291F0` beziehungsweise deren für die aktuelle Version validierte Gegenstücke bestätigen.
- Funktionsgrenze und sämtliche eingehenden Sprungziele prüfen; kein Sprung darf in die Mitte des überschriebenen Blocks führen.
- Vollständige Register-Liveness, Stackzustand, ABI-erhaltene Register und Flags über beide Ausgangspfade dokumentieren.
- Nur tatsächlich benötigte Register im `X64SmartCPUContextRegs`-Satz sichern; für die erste sichere Testversion kann ein breiterer Satz verwendet und später reduziert werden.
- Hookinstallation in einer Transaktion ausführen und bei jeder Validierungsabweichung vollständig unterlassen.

Der oben aufgeführte Block ist ein belastbarer Hookkandidat, aber noch keine Freigabe für einen ungeprüften In-place-Patch.

### 8.3 Alternative: Detour der Patrouillenzuweisung

Alternativ könnte `FUN_180029430` abgefangen werden. Beim Eintritt würde der Detour den tatsächlichen Rolle-1-Bestand mit dem aktiven `DefWalls` vergleichen und bei Unterbestand stattdessen `FUN_1800291F0` aufrufen.

Vorteil:

- Die fehlerhafte Vanilla-Entscheidung wird an einer semantisch klaren Grenze korrigiert.

Nachteile:

- Zusätzlicher nativer Funktions- und Calling-Convention-Vertrag.
- Das aktive AIC muss zuverlässig für Vanilla- und Custom-Lords aufgelöst werden.
- Trampolin, Rückkehrverhalten und direkter Aufruf des Wall-Helfers müssen vollständig validiert werden.
- Auch dieser Weg repariert keine bereits volle, falsch zusammengesetzte Verteidigung.

Diese Variante sollte nur gewählt werden, wenn sich der Context-Hook nicht stabil und versionssicher validieren lässt.

### 8.4 Optionale Reparatur bereits vergifteter Zustände

Eine zusätzliche periodische Reparatur könnte pro KI-Spieler Rolle 1 und Rolle 4 zählen und bei `role1 < DefWalls` deterministisch eine vorhandene Patrouilleneinheit zur Burgverteidigung verschieben.

Dafür gelten strengere Anforderungen:

- Auswahl deterministisch, beispielsweise niedrigste gültige Game-ID, damit Multiplayer-Zustände reproduzierbar bleiben.
- Einheit zuerst korrekt aus ihrem aktuellen Tribe entfernen.
- Erst danach den Vanilla-Wallhelfer verwenden.
- Fehler oder unklare Tribe-Zustände dürfen keine Doppelmitgliedschaft erzeugen.

Script Extender 2.2.0 korrigiert `GameTribeManagerAPI.UnassignUnit(tribeId, unitId)`. Mods für diese Zielversion müssen den öffentlichen Wrapper mit eigenen Vor- und Nachkontrollen verwenden und dürfen den nur für exakt 2.0.2 nötigen direkten nativen Adapter nicht übernehmen. Eine Runtime-Abhängigkeit zu `AIDefense` bleibt unzulässig.

Diese Reparatur ist invasiver und sollte als separat zuschaltbare zweite Stufe erst nach erfolgreichem Zweigfix entstehen.

### 8.5 Nicht empfohlene Variante: Vanilla-Zähler aufspalten

Man könnte theoretisch die Unit-Zählerpflege in `FUN_180182B00` verändern und einen separaten Rolle-1-Zähler einführen. Dafür ist jedoch kein nachweislich freies PlayerData-Feld bekannt. Ein externer Zähler oder mehrere gekoppelte Hooks wären nötig. Der Eingriff wäre größer, versionsanfälliger und schwieriger zu synchronisieren als die Korrektur direkt an der Zuweisungsentscheidung.

## 9. Späteres Feature: eigene Patrouillen-Einheitentypen

Das gewünschte Feature ist sinnvoll, aber vom Minimalfix getrennt zu behandeln.

Vanilla wählt den Einheitentyp aus `defensive_troops1..8`, bevor die frisch rekrutierte Einheit Rolle 1 oder Rolle 4 erhält. Ein bloßes Umleiten nach der Rekrutierung kann daher keine getrennte Patrouillenkomposition schaffen. Der Eingriff müsste bereits vor oder innerhalb der Auswahl- und Kaufentscheidung wissen, ob die nächste Einheit voraussichtlich Wallverteidiger oder Patrouille wird.

Empfohlene spätere Architektur:

- mod-eigene, pro Lord definierte `outer_patrol_troops`-Liste;
- Sidecar-Datei statt Änderung des festen `InternalAIC`-Layouts von `0x5E4` Bytes;
- Unterstützung sowohl der Vanilla-Lords als auch klar identifizierter Custom-Lords;
- Runtime-JSON ausschließlich über `Shared/DependencyFreeJson.cs`;
- fail-closed Schema-, Typ-, Pflichtfeld- und Wertebereichsprüfung im mod-eigenen Adapter;
- Auswahl aus der Patrouillenliste nur, wenn die tatsächliche Rolle-1-Quote bereits erfüllt ist;
- Vanilla-Liste als dokumentierter Fallback nur dann, wenn dies ausdrücklich als gewünschtes Verhalten entschieden wurde.

Vor einer Implementierung müssen gesondert untersucht werden:

- exakter Einheitenauswahlblock in `FUN_180040740`;
- zyklischer Auswahlindex und dessen Persistenz;
- Kaufbarkeit, Waffen, Gold und Produktionsbedingungen;
- Sonderfälle für Kavallerie und berittene Bogenschützen;
- Verhalten bei leeren oder vollständig nicht kaufbaren Patrouillenlisten;
- deterministische Auswahl im Multiplayer.

Die Aussage, dass eine gemischte Patrouillengruppe insgesamt mit der langsamsten enthaltenen Einheit läuft, ist derzeit eine Benutzerbeobachtung. Die Tribe-Gruppierung ist nativ belegt; die konkrete Geschwindigkeitsaggregation wurde in dieser Analyse noch nicht bewiesen und darf nicht als bestätigter nativer Vertrag behandelt werden.

## 10. Vorgaben für einen späteren Testmod

- Eigenständiger Mod ohne harte oder stille Abhängigkeit zu anderen Workspace-Mods.
- Keine überlappenden nativen Hooks mit `AIDefense`; dessen derzeitiger Zweck, geschützte Fernverteidiger in eigenen Tribes zu verwalten, ist von diesem Fehler verschieden.
- Zielvertrag ist Script Extender 2.2.0; bei späteren Extender-Updates sind die tatsächlichen Referenzen und Verträge erneut zu prüfen.
- Gameplayrelevanter Mod: `info.json` später mit `NetworkMode=1`.
- Langfristige Runtime und native Hooks nicht im normalen `BaseUnityPlugin.OnDestroy()` entfernen, da dieser Callback beim SHCDE-Startup vorzeitig auftreten kann.
- Simulationstätigkeit über Script-Extender-Ereignisse beziehungsweise `GameTimeManagerAPI.OnTick`, nicht über das `Update` einer früh erzeugten Plugin-Komponente.
- Logs immer mit Zeitstempel einschließlich Millisekunden.
- Empfohlene Diagnosedaten pro Entscheidung:
  - Spieler-ID;
  - frisch rekrutierte Unit-ID;
  - Unit-Typ;
  - tatsächlicher Rolle-1-Bestand;
  - tatsächlicher Rolle-4-Bestand;
  - ursprüngliches `DefWalls`;
  - gewählter Zweig;
  - Hash- oder Patternquelle der Hookauflösung.
- Ausführliche Entscheidungslogs begrenzen beziehungsweise nur im Diagnosemodus aktivieren, damit dauerhafte Rekrutierung keine unnötige Loglast erzeugt.

## 11. Akzeptanztests für die spätere Implementierung

### 11.1 Grundverhalten

1. `DefWalls = DefTotal`: Alle defensiven Rekruten und Ersatztruppen gehen auf Burgpositionen; es entsteht keine äußere Patrouille.
2. `DefWalls < DefTotal`: Der anfängliche Sollsplit entsteht unverändert.
3. Burgverteidiger sterben, Patrouille überlebt: Die nächsten defensiven Rekruten füllen Rolle 1 bis `DefWalls` auf.
4. Patrouilleneinheiten sterben, Wallquote ist erfüllt: Ersatz geht wieder in Rolle 4.
5. Beide Kategorien verlieren Einheiten: Rolle 1 wird zuerst bis zum Soll aufgefüllt, danach Rolle 4 bis `DefTotal`.

### 11.2 Batch und Panic

6. Mehrere Einheiten werden in einem Rekrutierungsaufruf angeworben: Jede einzelne Zuweisung berücksichtigt die bereits im selben Batch aktualisierten Rollen.
7. Panic-/Nervous-Rekrutierung überschreitet nicht aufgrund eines veralteten Rolle-1-Werts systematisch die gewünschte Patrouillenquote.

### 11.3 Spieler- und AIC-Trennung

8. Mehrere KI-Spieler mit verschiedenen AIC-Werten: Zählung und Entscheidung bleiben strikt pro Besitzer.
9. Custom-Lord: Der aktive Datensatz dieses Lords liefert `DefWalls`; kein hart codierter Vanilla-Lordindex.
10. Toter, ungültiger oder inzwischen wiederverwendeter Unit-Slot: Callback fällt sicher auf Vanilla zurück.

### 11.4 Kompatibilität und Sicherheit

11. Exakter Referenzhash: Hook löst auf, alle Instruktions- und Call-Target-Prüfungen bestehen.
12. Abweichender Hash mit eindeutigem kompatiblem Pattern: Hook wird nur nach vollständiger semantischer Validierung installiert.
13. Kein oder mehrdeutiger Patterntreffer: Feature bleibt deaktiviert und protokolliert den Grund eindeutig.
14. Gemeinsame Installation mit `AIDefense`: keine Hooküberlappung, keine Runtime-Abhängigkeit und keine Tribe-Kollision durch diesen Fix.
15. Startup-Lifecycle: Nach dem frühen Unity-Komponenten-Cleanup erscheint mindestens ein späterer mod-eigener Karten- oder Tick-Marker und der Hook arbeitet weiterhin.
16. Multiplayer-Test mit identischer Modkonfiguration: Host und Client bleiben synchron; die Auswahl beziehungsweise Zählung ist deterministisch.

### 11.5 Bereits beschädigter Zustand

17. Nur Zweigfix aktiv, Gesamtzahl bereits `DefTotal`: dokumentiert bestätigen, dass keine sofortige Migration stattfindet.
18. Falls die optionale Reparatur implementiert wird: Rolle-4-Einheiten werden ohne Doppelmitgliedschaft deterministisch aus dem alten Tribe entfernt und Rolle 1 zugewiesen.

## 12. Empfohlene Implementierungsreihenfolge für einen neuen Chat

1. Aktuellen DLL-Hash und Script-Extender-Zielversion erneut prüfen.
2. Maschinenblock, Funktionsgrenze, Call-Ziele, eingehende Sprünge und Register-Liveness mit der aktuellen Baseline erneut bestätigen.
3. Einen reinen Diagnosehook bauen, der `EAX`, `EBX`, Besitzer und Rollenbestände protokolliert, die Entscheidung aber noch nicht verändert.
4. Diagnose anhand eines reproduzierbaren `DefTotal > DefWalls`-Szenarios gegen beobachtete Unit-Rollen validieren.
5. Danach nur die Vergleichssteuerung für den minimalen Zweigfix aktivieren.
6. Statische Tests für Pattern, Blocklänge, Endadresse, Instruktionen und Call-Ziele ergänzen.
7. Alle Codekontrollen und CRLF-Prüfungen abschließen.
8. Erst danach einmal die mod-eigene `build.bat` mit den laut Workspace-Anweisung erforderlichen erhöhten Rechten ausführen.
9. Grund-, Verlust-, Batch-, Lifecycle- und Kompatibilitätstests durchführen.
10. Erst nach Erfolg entscheiden, ob eine optionale Zustandsmigration oder getrennte Patrouillen-Einheitentypen folgen sollen.

## 13. Offene Punkte und bewusst nicht behauptete Erkenntnisse

- Der 15-Byte-Block ist ein begründeter Hookkandidat, aber seine endgültige Patchfreigabe erfordert noch den vorgeschriebenen vollständigen Liveness- und Control-Flow-Nachweis.
- Das exakte stabile Fallbackpattern ist noch nicht festgeschrieben; es muss aus der dann aktuellen DLL abgeleitet und auf Eindeutigkeit getestet werden.
- Sofortige Reparatur bereits voller Fehlzustände gehört nicht zum minimalen Zweigfix.
- Getrennte Patrouillen-Einheitentypen benötigen einen weiteren Hookpunkt vor der Rekrutierung und sind kein bloßer zusätzlicher AIC-Wert.
- Die langsamste-Gruppeneinheit als Geschwindigkeitsgrenze ist noch nicht nativ verifiziert.
- Keine der hier genannten Kandidatenfunktionen ist allein wegen ihres `FUN_...`-Namens eine öffentliche oder versionsstabile API.

## 14. Abschlussbewertung

Der Fehler ist auf eine einzelne fachlich falsche Datenquelle in der Zuweisungsentscheidung eingegrenzt: Vanilla vergleicht `DefWalls` mit dem Gesamtbestand aus Rolle 1 und Rolle 4, obwohl dort ausschließlich der tatsächliche Rolle-1-Bestand relevant ist.

Der empfohlene erste Fix verändert weder Rekrutierungsziel, Einheitenauswahl noch Vanilla-Tribe-Helfer. Er korrigiert ausschließlich den Zweig für eine frisch rekrutierte, noch nicht zugewiesene Einheit. Damit ist er kleiner und risikoärmer als ein neuer Zähler, eine nachträgliche Tribe-Migration oder ein vollständiger Ersatz der Rekrutierungsfunktion.
