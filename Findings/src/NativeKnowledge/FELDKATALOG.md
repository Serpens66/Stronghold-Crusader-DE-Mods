# Feldkatalog: Kennungen, Bedeutung und Belegsicherheit

**Dokumentationsstand: 22.10.2026.** Diese Übersicht beschreibt den vorhandenen, unter Findings abgelegten C#-Katalog. Sie bestätigt keine neuen Feldbedeutungen und ändert keine Zugriffsfreigaben.

## Orientierung

| Inhalt | Umfang |
|---|---:|
| Erfasste Feldidentitäten | 88 |
| Bestätigte bevorzugte Bedeutungen (`Confirmed`) | 20 |
| Wahrscheinliche Bedeutungen (`Probable`) | 29 |
| Kandidaten / Quellhinweise (`Candidate`) | 39 |
| Speichervertrag zum Lesen freigegeben | 18 |
| Davon standardmäßig lesbar | 16 |
| Davon nur mit ausdrücklich zugelassener unsicherer Bedeutung | 2 |

Dazu kommt beim Todesmarker eine ausdrücklich **widerlegte historische Deutung**. Sie ist eine zusätzliche Bedeutung desselben Feldes und deshalb kein 89. Feldeintrag. Kennungslücken sind keine fehlenden Tabellenzeilen: Die neutralen IDs stammen aus dem bestehenden Erfassungsregister und werden hier nicht neu nummeriert.

### Was bedeutet „sicher“?

| Einstufung der Bedeutung | Bewertung für Menschen | Was daraus nicht folgt |
|---|---|---|
| `Confirmed` | Hohe Sicherheit **innerhalb des ausdrücklich beschriebenen Kontextes**; ein nativer Fach-Audit stützt die Aussage. | Nicht automatisch ein freigegebener Speicherzugriff, eine allgemeine Semantik für jeden Einheitentyp oder ein erfolgreicher Spieltest. |
| `Probable` | Mittlere Sicherheit; native Beziehungen oder ein begrenzter Kontext sprechen für die Deutung, aber Benennung oder Reichweite bleiben unvollständig geklärt. | Nicht stillschweigend wie eine bestätigte fachliche API behandeln. |
| `Candidate` | Niedrige Sicherheit der fachlichen Deutung; typischerweise Quellname, Platzhalter oder historische Deklaration ohne abgeschlossene eigene Prüfung. | Auch ein plausibler Upstream-Name beweist keine vollständige Vanilla-Semantik. |
| `Rejected` | Die konkrete Deutung ist durch Gegenbelege widerlegt. | Die Existenz des Feldes oder eine andere, zutreffende Deutung ist damit nicht widerlegt. |

**Bedeutung, Speichervertrag und Laufzeitnachweis sind drei getrennte Fragen.** Die Wörter „hoch/mittel/niedrig“ sind qualitative Erläuterungen der vorhandenen Katalogstufen, keine erfundenen Prozentwahrscheinlichkeiten und kein neuer unabhängiger Audit. Eine konservative Kandidateneinstufung ist auch kein Gegenbeweis gegen weitergehende Erkenntnisse außerhalb dieses Katalogs.

| Zugriff | Aussage im aktuellen Code |
|---|---|
| **Standard** | Speichervertrag ist für den erfassten Native-Hash freigegeben; bevorzugte Bedeutung ist bestätigt. |
| **Nur mit Opt-in** | Speichervertrag ist freigegeben, die Bedeutung bleibt wahrscheinlich. Der einzelne Aufruf muss `AllowUncertainMeaning` angeben. |
| **Gesperrt** | Keine vollständige Lesefreigabe im Katalog. Auch ein Opt-in hebt diese Sperre nicht auf. Typ-/Offsetangaben sind hier Wissensdaten, keine Zugriffsgarantie. |

„Standard“ bedeutet weiterhin: richtige Struktur, vollständige gültige Byteansicht, zum tatsächlichen Image passender Hash und korrekter Aufrufthread. Die Prüfung des Readers macht einen ungültigen nativen Pointer nicht sicher. Für den neuen Bereich gibt es noch keinen eigenen Spiel-/Multiplayer-Abnahmenachweis.

### Warum die Kennung wichtiger als der Name ist

`nkf-000054` identifiziert beispielsweise das heutige 16-Bit-Todeswort. Der C#-Bezeichner lautet `NativeFieldIds.F000054`. Verbesserte Namen und Bestätigungen ändern diese Identität nicht. Aliasse gelten nur für dieselben Rohbits; zusammengefasste oder aufgeteilte Felder sind verwandte, aber unterschiedliche Identitäten.

## Datengrundlage und Bezugsbasen

### Wo der Script Extender seine Kenntnisse aufführt

Der geprüfte Script-Extender-Quellstand verteilt die Informationen auf mehrere Stellen:

| Bereich | Inhalt und Aussagekraft |
|---|---|
| [Generierte API-Referenz am Beispiel GameUnit](https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.Interop.GameUnit.html) | Menschenlesbare Liste öffentlicher Strukturen und Felder samt Typen. Die veröffentlichte Website kann einen anderen Stand als unser erfasster Commit zeigen. |
| [C#-Interop-Strukturen](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop) | Konkrete Felddeklarationen, Datentypen, Layoutattribute und häufig Offsetkommentare. Hier liegen unter anderem GameUnit, GameBuilding, GamePlayerResources und GameTribe. |
| [Interop-Enums](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/Enums) | Benannte Domänen- und Zustandswerte. Ein Enum-Name ersetzt nicht den Audit, in welchem Kontext Vanilla diesen Wert verwendet. |
| [Native Header](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/ReverseEngineering/structs) | Native Struktur- und Enum-Beschreibungen für Reverse Engineering. Header können vom tatsächlich kompilierten Interop-Layout abweichen. |
| [API-Anleitungen](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/docs/guides) | Nutzung der Manager-APIs, Ereignisse und einzelner Fachbereiche; keine vollständige Feld-Evidenzsammlung. |

In den geprüften Dokumentations- und Quellbereichen wurde kein zu unserem Katalog gleichartiges Verzeichnis mit `Confirmed`/`Probable`/`Candidate`/`Rejected`, unabhängiger Speicherfreigabe und feldweiser Evidenzbewertung gefunden. Unsere Übersicht ergänzt diese Einordnung und verweist für übernommene Quellkenntnisse direkt auf Upstream.

**Linkregel:** Die Links „Script-Extender-Quelle“ und „Upstream-Permalink“ sind auf den erfassten Commit `4014ca67c96974a5e6d697665baf5351cbfebde6` und eine konkrete Quellzeile festgelegt. Sie belegen, wo Name bzw. Deklaration stehen; sie sind kein neuer nativer Bedeutungsnachweis. Die vorhandenen lokalen Belege bleiben erhalten. Einträge ohne konkret erfasste Extender-Quelldeklaration erhalten keinen erfundenen direkten Feldlink; insbesondere sind historische Halbwortfelder weiterhin von den neueren DWORD-Feldern getrennt.

**Dateihashes und Zeilenenden:** Die fünf verlinkten Quelltexte wurden gegen den Commit verglichen und stimmen nach Normalisierung der Zeilenenden und eines möglichen UTF-8-BOM textlich überein. Die hinterlegten SHA-256 beziehen sich unverändert auf die erfassten lokalen Dateibytes. Ein Git-Blob mit LF kann deshalb einen anderen Bytehash als eine lokale CRLF-Datei haben. Dieser Textvergleich bestätigt die Zuordnung zum Quellstand, nicht die fachliche Bedeutung des Feldes.


- Maßgeblicher Code: [Katalogdaten](NativeFieldCatalog.Data.cs), [Kennungen](NativeFieldIds.cs), [Verträge](NativeKnowledgeContracts.cs) und [Reader](NativeFieldReader.cs).
- Erfassungsdaten: [catalog-evidence.json](../../tests/NativeKnowledge/catalog-evidence.json). Technische Verwendung und spätere Übernahme: [INTEGRATION.md](INTEGRATION.md).
- Native-SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Erfasste Analyse: `sem/FBCB9319/analysis/g1E2FBFF53E33`, Digest `1E2FBFF53E3373D515E29BD27020DC7E75C7883847B276B84917F494388AE81C`.
- Generierte Baseline: Extender-Commit `10628df85cd432184742a50a1dee3969a66cf89e`; später separat erfasste Quelle: `4014ca67c96974a5e6d697665baf5351cbfebde6`. Der spätere Quellstand wird nicht rückwirkend als Herkunft älterer generierter Daten ausgegeben.
- Die referenzierten Belegdateien wurden beim Erstellen dieser Übersicht auf ihre hinterlegten vollständigen SHA-256 geprüft; keine Abweichungen. Auch der installierte Native-Hash stimmt überein. Das ist eine datierte Beobachtung, keine automatische Gültigkeitszusage für spätere Änderungen.

Alle Offsets sind hexadezimale **Byteabstände zum angegebenen Ansichtsursprung**. `UInt16` bedeutet zwei Bytes ohne Vorzeichen, `Int16` zwei Bytes mit Vorzeichen, `UInt32`/`Int32` entsprechend vier Bytes. `UInt8` ist ein Byte. Werte sind little-endian. Bei gesperrten Einträgen ist auch eine angezeigte Typdeutung nicht mit einer vollständigen technischen Freigabe gleichzusetzen. Arrayindizes im Reader sind 0-basiert innerhalb des Feldes; Spiel-IDs bleiben von diesen Indizes unabhängig.

| Struktur | Stabile Strukturkennung | Ansichtsursprung | Im Katalog geführte Ausdehnung |
|---|---|---|---|
| `GameUnit` | `nkr-000004` | Beginn der öffentlichen Interop-Struktur, nicht Managerbasis | `0x490` / 1168 Bytes |
| `GameBuilding` | `nkr-000001` | Beginn der öffentlichen Interop-Struktur, nicht Managerbasis | `0x32C` / 812 Bytes |
| `GamePlayerResources` | `nkr-000002` | Beginn der öffentlichen Interop-Struktur, nicht Managerbasis | `0x583C` / 22588 Bytes |
| `GameTribe` | `nkr-000003` | Beginn der öffentlichen Interop-Struktur, nicht Managerbasis | `0x688` / 1672 Bytes |
| `GameUnitManager` | `nkr-000005` | Unit-Managerbasis; kein einzelner Unit-Datensatz | Nicht bestätigt; Zugriff gesperrt |

Wichtige Stolperstellen: Der Spieler-Raidlisten-Ursprung wurde um `0x5C` korrigiert; die Tribe-Zielfelder sind native Bytebereiche über C#-Feldgrenzen hinweg; das alte Todes-DWORD ist nicht dasselbe wie sein heutiges unteres WORD. Die Detailtexte grenzen diese Fälle ein.

**Formale Beleggrenze:** Beim Quellergänzungsbeleg [E020](#e020) überschreitet das gespeicherte Zeilenende die tatsächliche Dateilänge um zwei Zeilen. Dies wird offengelegt, statt den Katalogverweis stillschweigend zu korrigieren. Die Quellinterpretationen bleiben Kandidaten; eine native Bestätigung ergibt sich daraus nicht.

## Felder nach Struktur

- [GameUnit](#gameunit) — 27 Einträge.
- [GameBuilding](#gamebuilding) — 19 Einträge.
- [GamePlayerResources](#gameplayerresources) — 8 Einträge.
- [GameTribe](#gametribe) — 33 Einträge.
- [GameUnitManager](#gameunitmanager) — 1 Eintrag.

<a id="gameunit"></a>
### GameUnit

| Kennung | Katalogname / verständliche Kurzbezeichnung | Bedeutung | Zugriff |
|---|---|---|---|
| [nkf-000047](#nkf-000047) | `CachedHealthBarBlocks` — Zwischengespeicherte Lebensbalken-Blöcke | Hoch / bestätigt | Standard |
| [nkf-000048](#nkf-000048) | `LifeStateRaw` — Lebens-/Verwaltungszustand als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000078](#nkf-000078) | `r_UnitChimp` — Upstream: Einheitentyp | Niedrig / Kandidat | Gesperrt |
| [nkf-000051](#nkf-000051) | `ControllingPlayerWord` — Vollständiges Kontrollspieler-Wort | Hoch / bestätigt | Standard |
| [nkf-000079](#nkf-000079) | `RecruitmentRallyField` — Rekrutierungs-/Sammelpunkt-bezogenes Feld | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000080](#nkf-000080) | `HealerWorldPositionX` — Heilerauswahl: Weltkoordinate X | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000081](#nkf-000081) | `HealerWorldPositionY` — Heilerauswahl: Weltkoordinate Y | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000052](#nkf-000052) | `StandingBaseHeightRaw` — Basiswert der Einheitshöhe | Hoch / bestätigt | Gesperrt |
| [nkf-000053](#nkf-000053) | `StandingHeightCorrection` — Vertikale Standhöhenkorrektur | Hoch / bestätigt | Gesperrt |
| [nkf-000054](#nkf-000054) | `DeathMarker` — Todesmerkmal, unabhängig von Projektilursache | Hoch / bestätigt | Standard |
| [nkf-000091](#nkf-000091) | `FormerCombinedDeathAndAdjacentWord` — Historisches gemeinsames Todes-/Nachbarwort | Niedrig / Kandidat | Gesperrt |
| [nkf-000055](#nkf-000055) | `CaptureEligibilityWord` — Wort für Eroberungs-/Auswahleignung | Mittel / wahrscheinlich | Nur mit Opt-in |
| [nkf-000092](#nkf-000092) | `NearestEnemyDistanceRaw` — Vermuteter Distanzrohwert zum nächsten Gegner | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000056](#nkf-000056) | `AiStateRaw` — Typabhängiger KI-Zustand | Hoch / bestätigt | Gesperrt |
| [nkf-000057](#nkf-000057) | `r_CanAutoEngageInMelee` — Upstream: selbstständiger Nahkampfbeginn | Niedrig / Kandidat | Gesperrt |
| [nkf-000082](#nkf-000082) | `WorkerWorkMode` — Arbeiter: Arbeits-/Unterhaltungsmodus | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000083](#nkf-000083) | `WorkerWorkshopId` — Arbeiter: zugeordnetes Produktionsgebäude | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000058](#nkf-000058) | `r_AttackingUnitGlobalId` — Upstream: Global-ID eines Angreifers | Niedrig / Kandidat | Gesperrt |
| [nkf-000084](#nkf-000084) | `WorkerWorkshopGlobalId` — Arbeiter: globale Workshop-Identität | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000085](#nkf-000085) | `WorkerAnimationVariant` — Arbeiter: Animationsvariante | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000086](#nkf-000086) | `FiremanTargetBuildingId` — Feuerwehr: Zielgebäude-Spiel-ID | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000087](#nkf-000087) | `FiremanTargetBuildingGlobalId` — Feuerwehr: globale Zielgebäude-ID | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000059](#nkf-000059) | `CurrentHealthRaw` — Aktuelle Lebenspunkte als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000060](#nkf-000060) | `MaximumHealthRaw` — Maximale Lebenspunkte als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000088](#nkf-000088) | `OutpostAiTribeRole` — Außenposten: KI-Gruppenrolle der Einheit | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000061](#nkf-000061) | `N000001FE` — Ungeklärtes Platzhalterfeld | Niedrig / Kandidat | Gesperrt |
| [nkf-000062](#nkf-000062) | `HealerTargetUnitId` — Vermutete Ziel-ID des Beduinenheilers | Mittel / wahrscheinlich | Gesperrt |


<a id="nkf-000047"></a>
#### nkf-000047 — CachedHealthBarBlocks

**Verständliche Bedeutung:** Zwischengespeicherte Lebensbalken-Blöcke. Vom Renderer verwendeter Blockzähler für den Lebensbalken. Schaden und Heilung aktualisieren den Zwischenspeicher. Der Wert ersetzt weder die eigentlichen Lebenspunkte noch die Identitäts- oder Lebensprüfung.

- **Speicherangabe:** `GameUnit + 0x34`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000047`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.CachedHealthBarBlocks`, `GameUnit.r_HealthBarBlocks`, `r_HealthBarBlocks`.
- **Belege:** [E037](#e037), [E038](#e038).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 30](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L30) (erfasster Commit).

<a id="nkf-000048"></a>
#### nkf-000048 — LifeStateRaw

**Verständliche Bedeutung:** Lebens-/Verwaltungszustand als Rohwert. Vorzeichenbehafteter AliveState-Rohwert. Während Todesanimation und Leichenphase kann IsAlive noch gesetzt sein. Deshalb allein kein Beweis, dass eine Einheit für Kampf oder Toreroberung als lebend zählt.

- **Speicherangabe:** `GameUnit + 0x88`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000048`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.LifeStateRaw`, `GameUnit.r_AliveState`, `r_AliveState`.
- **Belege:** [E039](#e039), [E040](#e040), [E041](#e041), [E042](#e042).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 55](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L55) (erfasster Commit).

<a id="nkf-000078"></a>
#### nkf-000078 — r_UnitChimp

**Verständliche Bedeutung:** Upstream: Einheitentyp. Upstream bezeichnet das Wort als Einheitentyp. Der automatisch übernommene Eintrag bleibt konservativ Kandidat: Die Erwähnung eines benannten Feldes allein schaltet hier keine eigenständige Bedeutungs- oder Zugriffsfreigabe frei.

- **Speicherangabe:** `GameUnit + 0x8A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000078`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnit.r_UnitChimp`, `r_UnitChimp`.
- **Belege:** [E083](#e083), [E084](#e084), [E085](#e085), [E086](#e086).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 56](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L56) (erfasster Commit).

<a id="nkf-000051"></a>
#### nkf-000051 — ControllingPlayerWord

**Verständliche Bedeutung:** Vollständiges Kontrollspieler-Wort. Vollständiges 16-Bit-Feld für den kontrollierenden Spieler. Manche nativen Leser erweitern es vorzeichenbehaftet; der Reader liefert die unveränderten vorzeichenlosen Bits. Keine Rückkehr zum früheren einzelnen Byte.

- **Speicherangabe:** `GameUnit + 0x92`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000051`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.ControllingPlayerWord`, `GameUnit.r_ControllableForPlayerId`, `r_ControllableForPlayerId`.
- **Belege:** [E043](#e043), [E044](#e044), [E045](#e045), [E046](#e046), [E047](#e047), [E048](#e048), [E049](#e049).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 60](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L60) (erfasster Commit).

<a id="nkf-000079"></a>
#### nkf-000079 — RecruitmentRallyField

**Verständliche Bedeutung:** Rekrutierungs-/Sammelpunkt-bezogenes Feld. Der dokumentierte Außenpostenpfad löscht dieses Feld beim Erzeugen seiner Einheiten. Seine allgemeine Befehls- oder Sammelpunktbedeutung ist damit nicht abschließend bestimmt.

- **Speicherangabe:** `GameUnit + 0xAC`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000079`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.N0000011E`, `GameUnit.RecruitmentRallyField`, `N0000011E`.
- **Belege:** [E087](#e087), [E088](#e088).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 68](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L68) (erfasster Commit).

<a id="nkf-000080"></a>
#### nkf-000080 — HealerWorldPositionX

**Verständliche Bedeutung:** Heilerauswahl: Weltkoordinate X. Im Heiler-Auswahlpfad als Weltposition verwendet. Nicht ohne Einheiten- und Skalierungsprüfung als gewöhnliche Kachelkoordinate behandeln.

- **Speicherangabe:** `GameUnit + 0xB2`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000080`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.HealerWorldPositionX`, `GameUnit.r_CurrentWorldPositionX`, `r_CurrentWorldPositionX`.
- **Belege:** [E089](#e089), [E090](#e090).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 71](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L71) (erfasster Commit).

<a id="nkf-000081"></a>
#### nkf-000081 — HealerWorldPositionY

**Verständliche Bedeutung:** Heilerauswahl: Weltkoordinate Y. Zweite Weltpositionskomponente im Heiler-Auswahlpfad. Nicht automatisch gleich der Kachel-Y-Koordinate.

- **Speicherangabe:** `GameUnit + 0xB4`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000081`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.HealerWorldPositionY`, `GameUnit.r_CurrentWorldPositionY`, `r_CurrentWorldPositionY`.
- **Belege:** [E091](#e091), [E090](#e090).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 72](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L72) (erfasster Commit).

<a id="nkf-000052"></a>
#### nkf-000052 — StandingBaseHeightRaw

**Verständliche Bedeutung:** Basiswert der Einheitshöhe. Eingangswert für die Standhöhe. Tatsächliche Höhe ergibt sich erst zusammen mit Korrektur, Oberflächenart und Sonderbewegungszweigen. Die im Katalog aufgeführte skalare Vorzeichenbehandlung ist nicht als freigegebener Lesevertrag zu verstehen.

- **Speicherangabe:** `GameUnit + 0xB6`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000052`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Gesperrt**.
  Die fachliche Aussage ist belegt; der vollständige technische Lesevertrag ist trotzdem nicht freigegeben.
- **Suchnamen / Aliasse:** `GameUnit.StandingBaseHeightRaw`.
- **Belege:** [E050](#e050).

<a id="nkf-000053"></a>
#### nkf-000053 — StandingHeightCorrection

**Verständliche Bedeutung:** Vertikale Standhöhenkorrektur. Vorzeichenbehaftete Korrektur bei der Standhöhenberechnung. Sie hängt unter anderem von Treppen, Mauern und erhöhten Gebäuden ab. Die Bedeutung ist belegt, der vollständige Speicherzugriff in diesem Katalog noch gesperrt.

- **Speicherangabe:** `GameUnit + 0xB8`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000053`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Gesperrt**.
  Die fachliche Aussage ist belegt; der vollständige technische Lesevertrag ist trotzdem nicht freigegeben.
- **Suchnamen / Aliasse:** `GameUnit.StandingHeightCorrection`.
- **Belege:** [E050](#e050).

<a id="nkf-000054"></a>
#### nkf-000054 — DeathMarker

**Verständliche Bedeutung:** Todesmerkmal, unabhängig von Projektilursache. Unteres 16-Bit-Todeswort. Auch Nahkampftod setzt es, während AliveState noch IsAlive sein kann. Das benachbarte Wort bei +0x29E ist anderes Speichergebiet. Der alte Name r_IsKilledByProjectile beweist keine Todesursache.

- **Speicherangabe:** `GameUnit + 0x29C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000054`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.DeathMarker`, `GameUnit.r_IsKilledByProjectile`, `r_IsKilledByProjectile`.
- **Verwandte Speicherbereiche:** [nkf-000091](#nkf-000091). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E051](#e051), [E052](#e052).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 209](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L209) (erfasster Commit).
- **Widerlegte Deutung:** `ProjectileDeathOnly` (`Rejected`). „Nur Projektiltod“ ist falsch: Der Nahkampfschreiber setzt dasselbe Todesmerkmal. Gegenbeleg: [E053](#e053).

<a id="nkf-000091"></a>
#### nkf-000091 — FormerCombinedDeathAndAdjacentWord

**Verständliche Bedeutung:** Historisches gemeinsames Todes-/Nachbarwort. Früheres N0000019A-DWORD fasste das heutige Todeswort und das unabhängige nächste Wort zusammen. Es besitzt eine eigene Kennung und ist kein Alias des heutigen 16-Bit-Todesmerkmals.

- **Speicherangabe:** `GameUnit + 0x29C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000091`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnit.FormerCombinedDeathAndAdjacentWord`, `N0000019A`, `historical:GameUnit.N0000019A`.
- **Verwandte Speicherbereiche:** [nkf-000054](#nkf-000054). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E103](#e103).

<a id="nkf-000055"></a>
#### nkf-000055 — CaptureEligibilityWord

**Verständliche Bedeutung:** Wort für Eroberungs-/Auswahleignung. Ein von null verschiedener Wert wird bei Toreroberung und beim Aufbau von Heiler-Kandidatenlisten geprüft. Die umfassende fachliche Klassifikation und alle Schreiber sind nicht geklärt. Insbesondere ist dies kein eigenständiges Lebensprädikat.

- **Speicherangabe:** `GameUnit + 0x2A0`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000055`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Nur mit Opt-in**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.CaptureEligibilityWord`, `GameUnit.r_SelectionAllowed`, `r_SelectionAllowed`.
- **Belege:** [E054](#e054), [E055](#e055).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 211](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L211) (erfasster Commit).

<a id="nkf-000092"></a>
#### nkf-000092 — NearestEnemyDistanceRaw

**Verständliche Bedeutung:** Vermuteter Distanzrohwert zum nächsten Gegner. Unterschiedliche native Erzeuger benutzen unterschiedliche Distanzmetriken. Vergleiche erfolgen teilweise als signed Int16 trotz öffentlicher UInt16-Ansicht. Keine universelle Distanzmetrik oder sichere arithmetische Interpretation wird zugesichert.

- **Speicherangabe:** `GameUnit + 0x2A2`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000092`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.NearestEnemyDistanceRaw`, `GameUnit.r_WorldDistanceToNearestEnemy`, `r_WorldDistanceToNearestEnemy`.
- **Belege:** [E104](#e104), [E105](#e105).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 212](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L212) (erfasster Commit).

<a id="nkf-000056"></a>
#### nkf-000056 — AiStateRaw

**Verständliche Bedeutung:** Typabhängiger KI-Zustand. KI-Zustände werden je Einheitentyp unterschiedlich verwendet. Beispielsweise ist der untersuchte Maceman-Zustand 114 eine Todesanimation. Daraus folgt kein allgemeines Zustands-Enum; der vollständige skalare Speichervertrag bleibt hier offen.

- **Speicherangabe:** `GameUnit + 0x2BC`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000056`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Gesperrt**.
  Die fachliche Aussage ist belegt; der vollständige technische Lesevertrag ist trotzdem nicht freigegeben.
- **Suchnamen / Aliasse:** `GameUnit.AiStateRaw`.
- **Belege:** [E056](#e056).

<a id="nkf-000057"></a>
#### nkf-000057 — r_CanAutoEngageInMelee

**Verständliche Bedeutung:** Upstream: selbstständiger Nahkampfbeginn. Neuerer Upstream-Name eines zuvor unbenannten Wortes. Er legt eine Erlaubnis zum selbstständigen Nahkampfbeginn nahe, bestätigt aber nicht sämtliche Verbraucher, Bedingungen oder Ausnahmen.

- **Speicherangabe:** `GameUnit + 0x2CC`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000057`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnit.r_CanAutoEngageInMelee`, `r_CanAutoEngageInMelee`.
- **Belege:** [E057](#e057), [E020](#e020).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 229](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L229) (erfasster Commit).

<a id="nkf-000082"></a>
#### nkf-000082 — WorkerWorkMode

**Verständliche Bedeutung:** Arbeiter: Arbeits-/Unterhaltungsmodus. Vom KI-Zustand getrennte Modusprüfung. Unterhaltungsbesuche können denselben KI-Zustand wie andere Abläufe verwenden; deshalb nicht allein vom AI-State auf Arbeitsbereitschaft schließen.

- **Speicherangabe:** `GameUnit + 0x2FA`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000082`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.WorkerWorkMode`, `GameUnit.r_WorkModeFlags`, `r_WorkModeFlags`.
- **Belege:** [E092](#e092), [E093](#e093).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 243](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L243) (erfasster Commit).

<a id="nkf-000083"></a>
#### nkf-000083 — WorkerWorkshopId

**Verständliche Bedeutung:** Arbeiter: zugeordnetes Produktionsgebäude. Spiel-ID des zugeordneten Workshops im untersuchten Arbeiterpfad. Ohne gepaarte globale Identität kann ein wiederverwendeter Gebäudeplatz das falsche Objekt bezeichnen.

- **Speicherangabe:** `GameUnit + 0x334`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000083`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.WorkerWorkshopId`, `GameUnit.r_LinkedProductionBuildingId`, `r_LinkedProductionBuildingId`.
- **Belege:** [E094](#e094), [E093](#e093).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 268](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L268) (erfasster Commit).

<a id="nkf-000058"></a>
#### nkf-000058 — r_AttackingUnitGlobalId

**Verständliche Bedeutung:** Upstream: Global-ID eines Angreifers. Neuerer Upstream-Name eines vorherigen Platzhalterfeldes. Welche Angriffsphase, Gültigkeitsdauer und Identitätsprüfung dazugehören, ist durch die Umbenennung allein nicht bestätigt.

- **Speicherangabe:** `GameUnit + 0x33C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000058`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnit.r_AttackingUnitGlobalId`, `r_AttackingUnitGlobalId`.
- **Belege:** [E058](#e058), [E020](#e020).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 272](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L272) (erfasster Commit).

<a id="nkf-000084"></a>
#### nkf-000084 — WorkerWorkshopGlobalId

**Verständliche Bedeutung:** Arbeiter: globale Workshop-Identität. Global-ID als Partner zur Workshop-Spiel-ID. Nur der dokumentierte Zuordnungskontext wird behauptet.

- **Speicherangabe:** `GameUnit + 0x364`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000084`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.WorkerWorkshopGlobalId`, `GameUnit.r_LinkedProductionBuildingGlobalId`, `r_LinkedProductionBuildingGlobalId`.
- **Belege:** [E095](#e095), [E093](#e093).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 288](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L288) (erfasster Commit).

<a id="nkf-000085"></a>
#### nkf-000085 — WorkerAnimationVariant

**Verständliche Bedeutung:** Arbeiter: Animationsvariante. Im untersuchten Arbeiterablauf als Animationsvariante gelesen. Der Extender-Name r_TicksSinceRestPulse wird dort wiederverwendet; nicht universell als Erholungszeit interpretieren.

- **Speicherangabe:** `GameUnit + 0x37C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000085`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.WorkerAnimationVariant`, `GameUnit.r_TicksSinceRestPulse`, `r_TicksSinceRestPulse`.
- **Belege:** [E096](#e096), [E093](#e093).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 297](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L297) (erfasster Commit).

<a id="nkf-000086"></a>
#### nkf-000086 — FiremanTargetBuildingId

**Verständliche Bedeutung:** Feuerwehr: Zielgebäude-Spiel-ID. Gebäude-ID des Feuerwehrziels. Lauf- und Löschzustände prüfen zusätzlich Global-ID und Brandzustand; ein gespeicherter Wert allein bedeutet keine erfolgreiche Zielannahme.

- **Speicherangabe:** `GameUnit + 0x39A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000086`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.FiremanTargetBuildingId`, `GameUnit.r_AI_ContextTargetUnitId`, `r_AI_ContextTargetUnitId`.
- **Belege:** [E097](#e097), [E098](#e098).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 310](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L310) (erfasster Commit).

<a id="nkf-000087"></a>
#### nkf-000087 — FiremanTargetBuildingGlobalId

**Verständliche Bedeutung:** Feuerwehr: globale Zielgebäude-ID. Globale Identität des Feuerwehrzieles als Partner zur Gebäude-Spiel-ID. Andere Einheitentypen können dasselbe Speichergebiet anders nutzen.

- **Speicherangabe:** `GameUnit + 0x39C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000087`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.FiremanTargetBuildingGlobalId`, `GameUnit.r_AI_ContextTargetUnitGlobalId`, `r_AI_ContextTargetUnitGlobalId`.
- **Belege:** [E099](#e099), [E098](#e098).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 311](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L311) (erfasster Commit).

<a id="nkf-000059"></a>
#### nkf-000059 — CurrentHealthRaw

**Verständliche Bedeutung:** Aktuelle Lebenspunkte als Rohwert. Aktuelle vorzeichenlose 32-Bit-Lebenspunkte. Schaden und Heilung aktualisieren weitere Zwischenspeicher; positive HP allein beweisen keinen gültigen, lebenden Datensatz.

- **Speicherangabe:** `GameUnit + 0x3C4`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000059`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.CurrentHealthRaw`, `GameUnit.r_CurrentHealth`, `r_CurrentHealth`.
- **Belege:** [E059](#e059), [E060](#e060).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 323](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L323) (erfasster Commit).

<a id="nkf-000060"></a>
#### nkf-000060 — MaximumHealthRaw

**Verständliche Bedeutung:** Maximale Lebenspunkte als Rohwert. Vorzeichenloses 32-Bit-HP-Maximum. Initialisierung setzt aktuelle und maximale HP typabhängig; dieser Reader verändert oder normalisiert nichts.

- **Speicherangabe:** `GameUnit + 0x3C8`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000060`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameUnit.MaximumHealthRaw`, `GameUnit.r_MaxHealth`, `r_MaxHealth`.
- **Belege:** [E059](#e059), [E061](#e061).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 324](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L324) (erfasster Commit).

<a id="nkf-000088"></a>
#### nkf-000088 — OutpostAiTribeRole

**Verständliche Bedeutung:** Außenposten: KI-Gruppenrolle der Einheit. Außenposten schreiben eine KI-Tribe-Rolle in dieses Feld. Die Eignung derselben Rolle für beliebige Spezialeinheiten ist damit nicht bestätigt.

- **Speicherangabe:** `GameUnit + 0x426`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000088`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.OutpostAiTribeRole`, `GameUnit.r_AITribeRole`, `r_AITribeRole`.
- **Belege:** [E100](#e100), [E088](#e088).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 357](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L357) (erfasster Commit).

<a id="nkf-000061"></a>
#### nkf-000061 — N000001FE

**Verständliche Bedeutung:** Ungeklärtes Platzhalterfeld. Im Quell- und Wissensbestand referenziertes 16-Bit-Feld. Der Platzhaltername liefert keine bestätigte fachliche Bedeutung. Es wird keine neue Benennung aus seiner bloßen Lage abgeleitet.

- **Speicherangabe:** `GameUnit + 0x42C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000061`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnit.N000001FE`, `N000001FE`.
- **Belege:** [E062](#e062), [E063](#e063).
- **Script-Extender-Quelle:** [GameUnit.cs, Zeile 360](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L360) (erfasster Commit).

<a id="nkf-000062"></a>
#### nkf-000062 — HealerTargetUnitId

**Verständliche Bedeutung:** Vermutete Ziel-ID des Beduinenheilers. Im untersuchten Heiler-Updater die 1-basierte Ziel-Unit-ID. Ziel-Global-ID, Verletzung, Reichweite und weitere Bedingungen werden separat geprüft. Die Nutzung durch andere Einheitentypen wird nicht daraus abgeleitet.

- **Speicherangabe:** `GameUnit + 0x450`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000062`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameUnit.HealerTargetUnitId`.
- **Belege:** [E064](#e064).

<a id="gamebuilding"></a>
### GameBuilding

| Kennung | Katalogname / verständliche Kurzbezeichnung | Bedeutung | Zugriff |
|---|---|---|---|
| [nkf-000001](#nkf-000001) | `RigidBuildingHeight` — Starre Gebäudehöhe | Hoch / bestätigt | Gesperrt |
| [nkf-000002](#nkf-000002) | `CurrentHealthRaw` — Aktuelle Lebenspunkte als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000003](#nkf-000003) | `MaximumHealthRaw` — Maximale Lebenspunkte als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000089](#nkf-000089) | `r_GateState` — Upstream: Torzustand als Byte | Niedrig / Kandidat | Gesperrt |
| [nkf-000005](#nkf-000005) | `SharedBuildingLinkWord` — Verknüpfungswert mehrteiliger Gebäude | Mittel / wahrscheinlich | Nur mit Opt-in |
| [nkf-000006](#nkf-000006) | `r_UsedInSiegeAttemptId` — Upstream: Verwendung bei Belagerungsversuch | Niedrig / Kandidat | Gesperrt |
| [nkf-000064](#nkf-000064) | `OnFireCounter` — Vermuteter Brandzähler | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000065](#nkf-000065) | `OutpostProfileMask` — Außenposten: Profilmaske | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000066](#nkf-000066) | `OutpostActiveTribeId` — Außenposten: aktuelle Gruppen-Spiel-ID | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000067](#nkf-000067) | `OutpostActiveTribeGlobalId` — Außenposten: globale Gruppenidentität | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000068](#nkf-000068) | `OutpostProductionCounter` — Außenposten: Produktionszähler | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000069](#nkf-000069) | `OutpostTargetGroupSize` — Außenposten: Zielgröße der Gruppe | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000070](#nkf-000070) | `OutpostActiveProfile` — Außenposten: aktuelles Produktionsprofil | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000071](#nkf-000071) | `OutpostSizeSetting` — Außenposten: Größeneinstellung | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000072](#nkf-000072) | `OutpostProductionDelay` — Außenposten: Produktionsverzögerung | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000073](#nkf-000073) | `OutpostCatapultFlag` — Außenposten: Katapult-bezogenes Feld | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000090](#nkf-000090) | `OutpostVicinityInitialized` — Außenposten: Umgebung initialisiert | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000075](#nkf-000075) | `OutpostGroupAcceleration` — Außenposten: Gruppenbeschleunigung | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000076](#nkf-000076) | `OutpostSpawnAcceleration` — Außenposten: Spawn-Beschleunigung | Mittel / wahrscheinlich | Gesperrt |


<a id="nkf-000001"></a>
#### nkf-000001 — RigidBuildingHeight

**Verständliche Bedeutung:** Starre Gebäudehöhe. Gemeinsame Höhenreferenz für starre Gebäudeteile und Einheiten auf Zugbrücken. Nicht mit der veränderlichen Kachelhöhe verwechseln. Die Bedeutung ist belegt; der vollständige skalare Lesevertrag, insbesondere die Vorzeichenbehandlung, ist im Katalog noch nicht freigegeben.

- **Speicherangabe:** `GameBuilding + 0xEC`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000001`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Gesperrt**.
  Die fachliche Aussage ist belegt; der vollständige technische Lesevertrag ist trotzdem nicht freigegeben.
- **Suchnamen / Aliasse:** `GameBuilding.RigidBuildingHeight`.
- **Belege:** [E001](#e001).

<a id="nkf-000002"></a>
#### nkf-000002 — CurrentHealthRaw

**Verständliche Bedeutung:** Aktuelle Lebenspunkte als Rohwert. Bei Gebäuden als vorzeichenbehaftetes 16-Bit-Wort gelesen; kein universeller Lebensnachweis.

- **Speicherangabe:** `GameBuilding + 0x10C`, `Int16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000002`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameBuilding.CurrentHealthRaw`, `GameBuilding.r_CurrentHealth`, `r_CurrentHealth`.
- **Belege:** [E002](#e002), [E003](#e003).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 95](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L95) (erfasster Commit).

<a id="nkf-000003"></a>
#### nkf-000003 — MaximumHealthRaw

**Verständliche Bedeutung:** Maximale Lebenspunkte als Rohwert. Im Gebäuderenderpfad vorzeichenlos gelesenes 16-Bit-Maximum. Die native Auswertung ist maßgeblich, nicht eine anders benannte C#-Ansicht.

- **Speicherangabe:** `GameBuilding + 0x10E`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000003`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameBuilding.MaximumHealthRaw`, `GameBuilding.r_MaxHealth`, `r_MaxHealth`.
- **Belege:** [E002](#e002), [E004](#e004).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 96](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L96) (erfasster Commit).

<a id="nkf-000089"></a>
#### nkf-000089 — r_GateState

**Verständliche Bedeutung:** Upstream: Torzustand als Byte. Aktuelles benanntes Byte bei +0x2A2. Historische Header können hiervon abweichen. Die Quellbenennung allein ersetzt keine eigene Prüfung aller Zustandswerte.

- **Speicherangabe:** `GameBuilding + 0x2A2`, `UInt8`; Elementabstand 1 Byte. C#-Kennung: `NativeFieldIds.F000089`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameBuilding.r_GateState`, `r_GateState`.
- **Belege:** [E101](#e101), [E063](#e063).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 214](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L214) (erfasster Commit).

<a id="nkf-000005"></a>
#### nkf-000005 — SharedBuildingLinkWord

**Verständliche Bedeutung:** Verknüpfungswert mehrteiliger Gebäude. Ein gemeinsamer, von null verschiedener 32-Bit-Wert verknüpft Gebäudeteile bei Zugänglichkeits- und Löschabläufen; auch Feuerwehrlogik nutzt eine solche Gruppierung. Der allgemeine Name bleibt eine Deutung: Daraus folgt weder universelle Gruppenidentität noch eine reine Belagerungs-ID.

- **Speicherangabe:** `GameBuilding + 0x2A8`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000005`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Nur mit Opt-in**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.N0000178A`, `GameBuilding.SharedBuildingLinkWord`, `N0000178A`.
- **Belege:** [E005](#e005), [E006](#e006).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 219](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L219) (erfasster Commit).

<a id="nkf-000006"></a>
#### nkf-000006 — r_UsedInSiegeAttemptId

**Verständliche Bedeutung:** Upstream: Verwendung bei Belagerungsversuch. Aktueller Extender-Name für das Feld bei +0x2AC. Historische Berichte verwendeten denselben Namen für +0x2A8. Das Nachbarfeld ist deshalb nicht allein wegen dieses Namens gleichzusetzen; eine vollständige eigene Bedeutungsprüfung fehlt.

- **Speicherangabe:** `GameBuilding + 0x2AC`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000006`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameBuilding.r_UsedInSiegeAttemptId`, `r_UsedInSiegeAttemptId`.
- **Belege:** [E007](#e007), [E008](#e008), [E009](#e009).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 220](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L220) (erfasster Commit).

<a id="nkf-000064"></a>
#### nkf-000064 — OnFireCounter

**Verständliche Bedeutung:** Vermuteter Brandzähler. Feuerwehr-Zielwahl prüft den von null verschiedenen Wert; Löschen setzt ihn auf null. Der native Bezug ist dokumentiert, aber die eigenständige skalare Zugriffsfreigabe dieses Katalogs steht aus.

- **Speicherangabe:** `GameBuilding + 0x2BE`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000064`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OnFireCounter`, `GameBuilding.r_OnFireTicks`, `r_OnFireTicks`.
- **Belege:** [E067](#e067), [E068](#e068).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 227](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L227) (erfasster Commit).

<a id="nkf-000065"></a>
#### nkf-000065 — OutpostProfileMask

**Verständliche Bedeutung:** Außenposten: Profilmaske. Wählt aktivierte Produktionsprofile. Nicht als prozentuale Aufteilung der Einheiten interpretieren. Bedeutung und Zugriff bleiben auf den dokumentierten Außenpostenpfad beschränkt.

- **Speicherangabe:** `GameBuilding + 0x300`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000065`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostProfileMask`, `GameBuilding.r_OutpostStateFlags`, `r_OutpostStateFlags`.
- **Belege:** [E069](#e069), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 254](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L254) (erfasster Commit).

<a id="nkf-000066"></a>
#### nkf-000066 — OutpostActiveTribeId

**Verständliche Bedeutung:** Außenposten: aktuelle Gruppen-Spiel-ID. Verweis auf die gerade aufgebaute Tribe-Gruppe. Eine Spiel-ID allein reicht bei wiederverwendeten Datensätzen nicht zur Identitätsprüfung.

- **Speicherangabe:** `GameBuilding + 0x302`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000066`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostActiveTribeId`, `GameBuilding.r_OutpostActiveTribeId`, `r_OutpostActiveTribeId`.
- **Belege:** [E071](#e071), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 255](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L255) (erfasster Commit).

<a id="nkf-000067"></a>
#### nkf-000067 — OutpostActiveTribeGlobalId

**Verständliche Bedeutung:** Außenposten: globale Gruppenidentität. Global-ID als Partner der aktuellen Tribe-Spiel-ID. Kein Nachweis, dass die Gruppe bereits vollständig produziert wurde oder einen Marschbefehl ausgeführt hat.

- **Speicherangabe:** `GameBuilding + 0x304`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000067`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostActiveTribeGlobalId`, `GameBuilding.r_OutpostActiveTribeGlobalId`, `r_OutpostActiveTribeGlobalId`.
- **Belege:** [E072](#e072), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 256](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L256) (erfasster Commit).

<a id="nkf-000068"></a>
#### nkf-000068 — OutpostProductionCounter

**Verständliche Bedeutung:** Außenposten: Produktionszähler. Im Produktionsablauf verwendeter Zähler. Seine Bedeutung hängt vom aktiven Profil und Zweig ab; nicht pauschal als verstrichene Echtzeit behandeln.

- **Speicherangabe:** `GameBuilding + 0x308`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000068`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostProductionCounter`, `GameBuilding.r_OutpostTickCounter`, `r_OutpostTickCounter`.
- **Belege:** [E073](#e073), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 257](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L257) (erfasster Commit).

<a id="nkf-000069"></a>
#### nkf-000069 — OutpostTargetGroupSize

**Verständliche Bedeutung:** Außenposten: Zielgröße der Gruppe. Zielzahl der Mitglieder für die aktuelle Produktion. Verzögerung, Profil und Abschlussbedingungen sind zusätzliche, getrennte Teile des nativen Ablaufs.

- **Speicherangabe:** `GameBuilding + 0x30A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000069`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostTargetGroupSize`, `GameBuilding.r_OutpostTargetGroupSize`, `r_OutpostTargetGroupSize`.
- **Belege:** [E074](#e074), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 258](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L258) (erfasster Commit).

<a id="nkf-000070"></a>
#### nkf-000070 — OutpostActiveProfile

**Verständliche Bedeutung:** Außenposten: aktuelles Produktionsprofil. Vermuteter aktiver Profilindex im dokumentierten Außenpostenpfad. Daraus wird kein allgemeines Profilformat für beliebige Gebäudetypen abgeleitet.

- **Speicherangabe:** `GameBuilding + 0x30C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000070`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.N00001849`, `GameBuilding.OutpostActiveProfile`, `N00001849`.
- **Belege:** [E075](#e075), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 259](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L259) (erfasster Commit).

<a id="nkf-000071"></a>
#### nkf-000071 — OutpostSizeSetting

**Verständliche Bedeutung:** Außenposten: Größeneinstellung. Einstellung, die die Gruppenproduktion beeinflusst. Nicht mit bereits vorhandener Mitgliederzahl oder endgültiger Zielgröße gleichsetzen.

- **Speicherangabe:** `GameBuilding + 0x30E`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000071`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostSizeSetting`, `GameBuilding.r_OutpostSize`, `r_OutpostSize`.
- **Belege:** [E076](#e076), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 260](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L260) (erfasster Commit).

<a id="nkf-000072"></a>
#### nkf-000072 — OutpostProductionDelay

**Verständliche Bedeutung:** Außenposten: Produktionsverzögerung. Verzögerungswert im Ablauf der Gruppenproduktion. Abschluss- und Freigabebedingungen müssen zusätzlich berücksichtigt werden.

- **Speicherangabe:** `GameBuilding + 0x310`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000072`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostProductionDelay`, `GameBuilding.r_OutpostDelay`, `r_OutpostDelay`.
- **Belege:** [E077](#e077), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 261](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L261) (erfasster Commit).

<a id="nkf-000073"></a>
#### nkf-000073 — OutpostCatapultFlag

**Verständliche Bedeutung:** Außenposten: Katapult-bezogenes Feld. Im Außenpostenbericht als Katapultflag eingeordnet. Eine universelle Boolean-Bedeutung oder ein geprüfter Schreibvertrag ist damit nicht bestätigt.

- **Speicherangabe:** `GameBuilding + 0x312`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000073`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.N00001797_2`, `GameBuilding.OutpostCatapultFlag`, `N00001797_2`.
- **Belege:** [E078](#e078), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 262](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L262) (erfasster Commit).

<a id="nkf-000090"></a>
#### nkf-000090 — OutpostVicinityInitialized

**Verständliche Bedeutung:** Außenposten: Umgebung initialisiert. Upstream als Merkmal zur Initialisierung naher Einheiten benannt. Die Quelle verwendet ein Byte; aus dem umgebenden Wort dürfen keine weiteren Bits als dasselbe Merkmal behandelt werden.

- **Speicherangabe:** `GameBuilding + 0x314`, `UInt8`; Elementabstand 1 Byte. C#-Kennung: `NativeFieldIds.F000090`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.OutpostVicinityInitialized`, `GameBuilding.r_OutpostNearbyUnitsInitialized`, `r_OutpostNearbyUnitsInitialized`.
- **Belege:** [E102](#e102), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 263](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L263) (erfasster Commit).

<a id="nkf-000075"></a>
#### nkf-000075 — OutpostGroupAcceleration

**Verständliche Bedeutung:** Außenposten: Gruppenbeschleunigung. Mit den Warte-/Abschlussbedingungen der Gruppenbildung verknüpft. Der native Bericht beschreibt vorzeichenbehaftete Auswertung; der hier geführte UInt16-Rohdatentyp ist keine abgeschlossene semantische Vorzeichenfestlegung.

- **Speicherangabe:** `GameBuilding + 0x316`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000075`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.N00001797_3`, `GameBuilding.OutpostGroupAcceleration`, `N00001797_3`.
- **Belege:** [E079](#e079), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 265](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L265) (erfasster Commit).

<a id="nkf-000076"></a>
#### nkf-000076 — OutpostSpawnAcceleration

**Verständliche Bedeutung:** Außenposten: Spawn-Beschleunigung. Im Produktionsablauf verwendeter Beschleunigungswert. Der Quelltyp ist UInt32; der Katalog hat daraus noch keinen eigenen vollständigen Lesevertrag abgeleitet.

- **Speicherangabe:** `GameBuilding + 0x318`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000076`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GameBuilding.N00001798`, `GameBuilding.OutpostSpawnAcceleration`, `N00001798`.
- **Belege:** [E080](#e080), [E070](#e070).
- **Script-Extender-Quelle:** [GameBuilding.cs, Zeile 266](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L266) (erfasster Commit).

<a id="gameplayerresources"></a>
### GamePlayerResources

| Kennung | Katalogname / verständliche Kurzbezeichnung | Bedeutung | Zugriff |
|---|---|---|---|
| [nkf-000077](#nkf-000077) | `PendingIncomingGoodsTable` — Tabelle ausstehender Wareneingänge | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000007](#nkf-000007) | `GoldRaw` — Goldbestand als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000008](#nkf-000008) | `LordUnitGameId` — Spiel-ID des Lords | Hoch / bestätigt | Standard |
| [nkf-000009](#nkf-000009) | `PlayerPauseWord` — Vermutetes Spieler-Pausenfeld | Mittel / wahrscheinlich | Gesperrt |
| [nkf-000010](#nkf-000010) | `WinLossStateRaw` — Sieg-/Niederlagenzustand als Rohwert | Hoch / bestätigt | Standard |
| [nkf-000011](#nkf-000011) | `RaidCandidateBuildingIds` — Gebäudekandidaten für Überfälle | Hoch / bestätigt | Standard |
| [nkf-000012](#nkf-000012) | `RaidCandidateBuildingCount` — Zahl der Überfall-Gebäudekandidaten | Hoch / bestätigt | Standard |
| [nkf-000013](#nkf-000013) | `r_AILastStandPhase` — Upstream: letzte KI-Verteidigungsphase | Niedrig / Kandidat | Gesperrt |


<a id="nkf-000077"></a>
#### nkf-000077 — PendingIncomingGoodsTable

**Verständliche Bedeutung:** Tabelle ausstehender Wareneingänge. Bezeichnet den Beginn der Tabelle, in die unter anderem besiegte Spielerbeute gebucht wird. Der Eintrag beschreibt derzeit nur die erste Quell-Skalarstelle; er erteilt keinen Arrayvertrag für die gesamte Tabelle.

- **Speicherangabe:** `GamePlayerResources + 0x46C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000077`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GamePlayerResources.PendingIncomingGoodsTable`, `GamePlayerResources.r_incomingNull`, `r_incomingNull`.
- **Belege:** [E081](#e081), [E082](#e082).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 303](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L303) (erfasster Commit).

<a id="nkf-000007"></a>
#### nkf-000007 — GoldRaw

**Verständliche Bedeutung:** Goldbestand als Rohwert. Vorzeichenbehafteter Goldbestand. Beute, Käufe, Verkäufe und Steuern besitzen eigene Schreibpfade. Ein gelesener Saldo beweist keinen abgeschlossenen Handel und wird nicht stillschweigend auf null begrenzt.

- **Speicherangabe:** `GamePlayerResources + 0x50C`, `Int32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000007`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GamePlayerResources.GoldRaw`, `GamePlayerResources.r_TotalGoodsGold`, `r_TotalGoodsGold`.
- **Belege:** [E010](#e010), [E011](#e011).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 343](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L343) (erfasster Commit).

<a id="nkf-000008"></a>
#### nkf-000008 — LordUnitGameId

**Verständliche Bedeutung:** Spiel-ID des Lords. 1-basierte Spiel-ID der Lord-Einheit. Aktuelle Identität und Lebenszustand müssen zusätzlich geprüft werden. Ein geladener Spielstand liefert zunächst Ausgangszustand, nicht automatisch ein neues Niederlagenereignis.

- **Speicherangabe:** `GamePlayerResources + 0x21F8`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000008`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GamePlayerResources.LordUnitGameId`, `GamePlayerResources.r_LordUnitId`, `r_LordUnitId`.
- **Belege:** [E012](#e012), [E013](#e013).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 1753](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1753) (erfasster Commit).

<a id="nkf-000009"></a>
#### nkf-000009 — PlayerPauseWord

**Verständliche Bedeutung:** Vermutetes Spieler-Pausenfeld. Im Extender r_IsPaused und im Niederlagenbericht erwähnt. Die vollständige Pausenbedeutung sowie der eigenständige Zugriff sind hier nicht abschließend auditiert.

- **Speicherangabe:** `GamePlayerResources + 0x2210`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000009`.
- **Sicherheit / Verwendung:** Mittel / wahrscheinlich (`Probable`); Zugriff **Gesperrt**.
  Die Belege stützen den beschriebenen Teilkontext; der Name oder seine allgemeine Gültigkeit bleibt eine Vermutung.
- **Suchnamen / Aliasse:** `GamePlayerResources.PlayerPauseWord`, `GamePlayerResources.r_IsPaused`, `r_IsPaused`.
- **Belege:** [E012](#e012), [E014](#e014).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 1759](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1759) (erfasster Commit).

<a id="nkf-000010"></a>
#### nkf-000010 — WinLossStateRaw

**Verständliche Bedeutung:** Sieg-/Niederlagenzustand als Rohwert. Der reguläre Simulationspfad aktualisiert dieses Zustandsfeld; Save-Laden ist ein weiterer Schreiber. Bestätigt ist der begrenzte Rohwertvertrag, nicht jede denkbare Bedeutung einzelner Zahlenwerte.

- **Speicherangabe:** `GamePlayerResources + 0x2244`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000010`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GamePlayerResources.WinLossStateRaw`, `GamePlayerResources.r_WinLossState`, `r_WinLossState`.
- **Belege:** [E012](#e012), [E015](#e015).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 1772](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1772) (erfasster Commit).

<a id="nkf-000011"></a>
#### nkf-000011 — RaidCandidateBuildingIds

**Verständliche Bedeutung:** Gebäudekandidaten für Überfälle. Speicher für 100 Gebäude-Spiel-IDs. Der aktuelle Zähler entscheidet, welche Einträge relevant sind. Der öffentliche Spielerpointer verlangt +0x35BC; die ältere Angabe +0x3560 bezog sich auf einen anderen Ursprung, der um 0x5C versetzt ist.

- **Speicherangabe:** `GamePlayerResources + 0x35BC`, `UInt16[100]`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000011`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GamePlayerResources.RaidCandidateBuildingIds`, `GamePlayerResources.r_BuildingIds`, `r_BuildingIds`.
- **Belege:** [E016](#e016), [E017](#e017).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 3028](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3028) (erfasster Commit).

<a id="nkf-000012"></a>
#### nkf-000012 — RaidCandidateBuildingCount

**Verständliche Bedeutung:** Zahl der Überfall-Gebäudekandidaten. Aktueller Zähler zur Kandidatenliste. Relativ zum öffentlichen Spielerpointer liegt er bei +0x3684, nicht +0x3628. Ein beobachteter Rohwert wird nicht automatisch begrenzt oder als Beweis gültiger Listeneinträge behandelt.

- **Speicherangabe:** `GamePlayerResources + 0x3684`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000012`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GamePlayerResources.RaidCandidateBuildingCount`, `GamePlayerResources.r_TrackedBuildingCount`, `r_TrackedBuildingCount`.
- **Belege:** [E016](#e016), [E018](#e018).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 3029](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3029) (erfasster Commit).

<a id="nkf-000013"></a>
#### nkf-000013 — r_AILastStandPhase

**Verständliche Bedeutung:** Upstream: letzte KI-Verteidigungsphase. Der neuere Extender-Name deutet auf eine letzte Verteidigungsphase der KI. Beschriebene Zustandswerte und Übergänge sind Upstream-Interpretation; ein unabhängiger vollständiger Vanilla-Zustandsaudit ist damit nicht erbracht.

- **Speicherangabe:** `GamePlayerResources + 0x38F0`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000013`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GamePlayerResources.r_AILastStandPhase`, `r_AILastStandPhase`.
- **Belege:** [E019](#e019), [E020](#e020).
- **Script-Extender-Quelle:** [GamePlayerResources.cs, Zeile 3167](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3167) (erfasster Commit).

<a id="gametribe"></a>
### GameTribe

| Kennung | Katalogname / verständliche Kurzbezeichnung | Bedeutung | Zugriff |
|---|---|---|---|
| [nkf-000014](#nkf-000014) | `LeaderUnitGameId` — Spiel-ID des Gruppenanführers | Hoch / bestätigt | Standard |
| [nkf-000015](#nkf-000015) | `StoredTargetBuildingGameId` — Gespeicherte Spiel-ID des Zielgebäudes | Hoch / bestätigt | Standard |
| [nkf-000016](#nkf-000016) | `StoredTargetBuildingGlobalId` — Globale Identität des Zielgebäudes | Hoch / bestätigt | Standard |
| [nkf-000017](#nkf-000017) | `FormerLowWord10` — Untere historische Hälfte von Angreifer-Slot 10 | Niedrig / Kandidat | Gesperrt |
| [nkf-000018](#nkf-000018) | `r_LastRangedAttackerGlobalId10` — Upstream: Fernkampf-Angreifer-Slot 10 | Niedrig / Kandidat | Gesperrt |
| [nkf-000019](#nkf-000019) | `FormerUpperWord10` — Obere historische Hälfte von Angreifer-Slot 10 | Niedrig / Kandidat | Gesperrt |
| [nkf-000020](#nkf-000020) | `FormerLowWord1` — Untere historische Hälfte von Angreifer-Slot 1 | Niedrig / Kandidat | Gesperrt |
| [nkf-000021](#nkf-000021) | `r_LastRangedAttackerGlobalId1` — Upstream: Fernkampf-Angreifer-Slot 1 | Niedrig / Kandidat | Gesperrt |
| [nkf-000022](#nkf-000022) | `FormerUpperWord1` — Obere historische Hälfte von Angreifer-Slot 1 | Niedrig / Kandidat | Gesperrt |
| [nkf-000023](#nkf-000023) | `FormerLowWord2` — Untere historische Hälfte von Angreifer-Slot 2 | Niedrig / Kandidat | Gesperrt |
| [nkf-000024](#nkf-000024) | `r_LastRangedAttackerGlobalId2` — Upstream: Fernkampf-Angreifer-Slot 2 | Niedrig / Kandidat | Gesperrt |
| [nkf-000025](#nkf-000025) | `FormerUpperWord2` — Obere historische Hälfte von Angreifer-Slot 2 | Niedrig / Kandidat | Gesperrt |
| [nkf-000026](#nkf-000026) | `FormerLowWord3` — Untere historische Hälfte von Angreifer-Slot 3 | Niedrig / Kandidat | Gesperrt |
| [nkf-000027](#nkf-000027) | `r_LastRangedAttackerGlobalId3` — Upstream: Fernkampf-Angreifer-Slot 3 | Niedrig / Kandidat | Gesperrt |
| [nkf-000028](#nkf-000028) | `FormerUpperWord3` — Obere historische Hälfte von Angreifer-Slot 3 | Niedrig / Kandidat | Gesperrt |
| [nkf-000029](#nkf-000029) | `FormerLowWord4` — Untere historische Hälfte von Angreifer-Slot 4 | Niedrig / Kandidat | Gesperrt |
| [nkf-000030](#nkf-000030) | `r_LastRangedAttackerGlobalId4` — Upstream: Fernkampf-Angreifer-Slot 4 | Niedrig / Kandidat | Gesperrt |
| [nkf-000031](#nkf-000031) | `FormerUpperWord4` — Obere historische Hälfte von Angreifer-Slot 4 | Niedrig / Kandidat | Gesperrt |
| [nkf-000032](#nkf-000032) | `FormerLowWord5` — Untere historische Hälfte von Angreifer-Slot 5 | Niedrig / Kandidat | Gesperrt |
| [nkf-000033](#nkf-000033) | `r_LastRangedAttackerGlobalId5` — Upstream: Fernkampf-Angreifer-Slot 5 | Niedrig / Kandidat | Gesperrt |
| [nkf-000034](#nkf-000034) | `FormerUpperWord5` — Obere historische Hälfte von Angreifer-Slot 5 | Niedrig / Kandidat | Gesperrt |
| [nkf-000035](#nkf-000035) | `FormerLowWord6` — Untere historische Hälfte von Angreifer-Slot 6 | Niedrig / Kandidat | Gesperrt |
| [nkf-000036](#nkf-000036) | `r_LastRangedAttackerGlobalId6` — Upstream: Fernkampf-Angreifer-Slot 6 | Niedrig / Kandidat | Gesperrt |
| [nkf-000037](#nkf-000037) | `FormerUpperWord6` — Obere historische Hälfte von Angreifer-Slot 6 | Niedrig / Kandidat | Gesperrt |
| [nkf-000038](#nkf-000038) | `FormerLowWord7` — Untere historische Hälfte von Angreifer-Slot 7 | Niedrig / Kandidat | Gesperrt |
| [nkf-000039](#nkf-000039) | `r_LastRangedAttackerGlobalId7` — Upstream: Fernkampf-Angreifer-Slot 7 | Niedrig / Kandidat | Gesperrt |
| [nkf-000040](#nkf-000040) | `FormerUpperWord7` — Obere historische Hälfte von Angreifer-Slot 7 | Niedrig / Kandidat | Gesperrt |
| [nkf-000041](#nkf-000041) | `FormerLowWord8` — Untere historische Hälfte von Angreifer-Slot 8 | Niedrig / Kandidat | Gesperrt |
| [nkf-000042](#nkf-000042) | `r_LastRangedAttackerGlobalId8` — Upstream: Fernkampf-Angreifer-Slot 8 | Niedrig / Kandidat | Gesperrt |
| [nkf-000043](#nkf-000043) | `FormerUpperWord8` — Obere historische Hälfte von Angreifer-Slot 8 | Niedrig / Kandidat | Gesperrt |
| [nkf-000044](#nkf-000044) | `FormerLowWord9` — Untere historische Hälfte von Angreifer-Slot 9 | Niedrig / Kandidat | Gesperrt |
| [nkf-000045](#nkf-000045) | `r_LastRangedAttackerGlobalId9` — Upstream: Fernkampf-Angreifer-Slot 9 | Niedrig / Kandidat | Gesperrt |
| [nkf-000046](#nkf-000046) | `FormerUpperWord9` — Obere historische Hälfte von Angreifer-Slot 9 | Niedrig / Kandidat | Gesperrt |


<a id="nkf-000014"></a>
#### nkf-000014 — LeaderUnitGameId

**Verständliche Bedeutung:** Spiel-ID des Gruppenanführers. 1-basierte Unit-ID des Tribe-Anführers. Native Berechnung benutzt einen vorgeschalteten Null-Datensatz; öffentliche Unit-Spans sind 0-basiert. Beim Wechsel zur Spanansicht ist genau einmal gameId−1 nötig, nicht bei Übergabe an eine ID-API.

- **Speicherangabe:** `GameTribe + 0x30`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000014`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameTribe.LeaderUnitGameId`, `GameTribe.r_LeaderUnitId`, `r_LeaderUnitId`.
- **Belege:** [E021](#e021), [E022](#e022), [E023](#e023), [E024](#e024).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 31](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L31) (erfasster Commit).

<a id="nkf-000015"></a>
#### nkf-000015 — StoredTargetBuildingGameId

**Verständliche Bedeutung:** Gespeicherte Spiel-ID des Zielgebäudes. Gespeicherte 1-basierte Zielgebäude-ID. Befehl 9 und andere Pfade schreiben sie. Ein bestehendes oder geändertes Ziel beweist weder einen gerade ausgeführten Befehl noch eine erfolgreiche neue Wegsuche.

- **Speicherangabe:** `GameTribe + 0x622`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000015`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameTribe.StoredTargetBuildingGameId`.
- **Belege:** [E025](#e025).

<a id="nkf-000016"></a>
#### nkf-000016 — StoredTargetBuildingGlobalId

**Verständliche Bedeutung:** Globale Identität des Zielgebäudes. Global-ID als Partner zur gespeicherten Gebäude-Spiel-ID. +0x626 ist strukturrelativ; die native Angabe +0x650 enthält den Managerpräfix. Die Ansicht umfasst native Bytes, die Grenzen benannter Extender-Felder überschreiten können.

- **Speicherangabe:** `GameTribe + 0x626`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000016`.
- **Sicherheit / Verwendung:** Hoch / bestätigt (`Confirmed`); Zugriff **Standard**.
- **Suchnamen / Aliasse:** `GameTribe.StoredTargetBuildingGlobalId`.
- **Verwandte Speicherbereiche:** [nkf-000017](#nkf-000017), [nkf-000018](#nkf-000018). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E025](#e025).

<a id="nkf-000017"></a>
#### nkf-000017 — FormerLowWord10

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 10. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x628`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000017`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk10`, `GameTribe.FormerLowWord10`.
- **Verwandte Speicherbereiche:** [nkf-000016](#nkf-000016), [nkf-000018](#nkf-000018). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000018"></a>
#### nkf-000018 — r_LastRangedAttackerGlobalId10

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 10. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x628`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000018`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId10`, `r_LastRangedAttackerGlobalId10`.
- **Verwandte Speicherbereiche:** [nkf-000016](#nkf-000016), [nkf-000017](#nkf-000017), [nkf-000019](#nkf-000019). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E027](#e027), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 443](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L443) (erfasster Commit).

<a id="nkf-000019"></a>
#### nkf-000019 — FormerUpperWord10

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 10. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x62A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000019`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId10`, `GameTribe.FormerUpperWord10`.
- **Verwandte Speicherbereiche:** [nkf-000018](#nkf-000018). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000020"></a>
#### nkf-000020 — FormerLowWord1

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 1. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x62C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000020`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk1`, `GameTribe.FormerLowWord1`.
- **Verwandte Speicherbereiche:** [nkf-000021](#nkf-000021). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000021"></a>
#### nkf-000021 — r_LastRangedAttackerGlobalId1

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 1. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x62C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000021`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId1`, `r_LastRangedAttackerGlobalId1`.
- **Verwandte Speicherbereiche:** [nkf-000020](#nkf-000020), [nkf-000022](#nkf-000022). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E028](#e028), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 444](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L444) (erfasster Commit).

<a id="nkf-000022"></a>
#### nkf-000022 — FormerUpperWord1

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 1. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x62E`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000022`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId1`, `GameTribe.FormerUpperWord1`.
- **Verwandte Speicherbereiche:** [nkf-000021](#nkf-000021). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000023"></a>
#### nkf-000023 — FormerLowWord2

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 2. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x630`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000023`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk2`, `GameTribe.FormerLowWord2`.
- **Verwandte Speicherbereiche:** [nkf-000024](#nkf-000024). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000024"></a>
#### nkf-000024 — r_LastRangedAttackerGlobalId2

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 2. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x630`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000024`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId2`, `r_LastRangedAttackerGlobalId2`.
- **Verwandte Speicherbereiche:** [nkf-000023](#nkf-000023), [nkf-000025](#nkf-000025). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E029](#e029), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 445](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L445) (erfasster Commit).

<a id="nkf-000025"></a>
#### nkf-000025 — FormerUpperWord2

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 2. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x632`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000025`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId2`, `GameTribe.FormerUpperWord2`.
- **Verwandte Speicherbereiche:** [nkf-000024](#nkf-000024). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000026"></a>
#### nkf-000026 — FormerLowWord3

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 3. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x634`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000026`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk3`, `GameTribe.FormerLowWord3`.
- **Verwandte Speicherbereiche:** [nkf-000027](#nkf-000027). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000027"></a>
#### nkf-000027 — r_LastRangedAttackerGlobalId3

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 3. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x634`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000027`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId3`, `r_LastRangedAttackerGlobalId3`.
- **Verwandte Speicherbereiche:** [nkf-000026](#nkf-000026), [nkf-000028](#nkf-000028). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E030](#e030), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 446](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L446) (erfasster Commit).

<a id="nkf-000028"></a>
#### nkf-000028 — FormerUpperWord3

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 3. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x636`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000028`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId3`, `GameTribe.FormerUpperWord3`.
- **Verwandte Speicherbereiche:** [nkf-000027](#nkf-000027). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000029"></a>
#### nkf-000029 — FormerLowWord4

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 4. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x638`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000029`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk4`, `GameTribe.FormerLowWord4`.
- **Verwandte Speicherbereiche:** [nkf-000030](#nkf-000030). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000030"></a>
#### nkf-000030 — r_LastRangedAttackerGlobalId4

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 4. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x638`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000030`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId4`, `r_LastRangedAttackerGlobalId4`.
- **Verwandte Speicherbereiche:** [nkf-000029](#nkf-000029), [nkf-000031](#nkf-000031). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E031](#e031), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 447](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L447) (erfasster Commit).

<a id="nkf-000031"></a>
#### nkf-000031 — FormerUpperWord4

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 4. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x63A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000031`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId4`, `GameTribe.FormerUpperWord4`.
- **Verwandte Speicherbereiche:** [nkf-000030](#nkf-000030). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000032"></a>
#### nkf-000032 — FormerLowWord5

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 5. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x63C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000032`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk5`, `GameTribe.FormerLowWord5`.
- **Verwandte Speicherbereiche:** [nkf-000033](#nkf-000033). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000033"></a>
#### nkf-000033 — r_LastRangedAttackerGlobalId5

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 5. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x63C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000033`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId5`, `r_LastRangedAttackerGlobalId5`.
- **Verwandte Speicherbereiche:** [nkf-000032](#nkf-000032), [nkf-000034](#nkf-000034). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E032](#e032), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 448](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L448) (erfasster Commit).

<a id="nkf-000034"></a>
#### nkf-000034 — FormerUpperWord5

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 5. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x63E`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000034`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId5`, `GameTribe.FormerUpperWord5`.
- **Verwandte Speicherbereiche:** [nkf-000033](#nkf-000033). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000035"></a>
#### nkf-000035 — FormerLowWord6

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 6. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x640`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000035`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk6`, `GameTribe.FormerLowWord6`.
- **Verwandte Speicherbereiche:** [nkf-000036](#nkf-000036). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000036"></a>
#### nkf-000036 — r_LastRangedAttackerGlobalId6

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 6. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x640`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000036`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId6`, `r_LastRangedAttackerGlobalId6`.
- **Verwandte Speicherbereiche:** [nkf-000035](#nkf-000035), [nkf-000037](#nkf-000037). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E033](#e033), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 449](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L449) (erfasster Commit).

<a id="nkf-000037"></a>
#### nkf-000037 — FormerUpperWord6

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 6. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x642`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000037`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId6`, `GameTribe.FormerUpperWord6`.
- **Verwandte Speicherbereiche:** [nkf-000036](#nkf-000036). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000038"></a>
#### nkf-000038 — FormerLowWord7

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 7. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x644`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000038`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk7`, `GameTribe.FormerLowWord7`.
- **Verwandte Speicherbereiche:** [nkf-000039](#nkf-000039). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000039"></a>
#### nkf-000039 — r_LastRangedAttackerGlobalId7

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 7. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x644`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000039`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId7`, `r_LastRangedAttackerGlobalId7`.
- **Verwandte Speicherbereiche:** [nkf-000038](#nkf-000038), [nkf-000040](#nkf-000040). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E034](#e034), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 450](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L450) (erfasster Commit).

<a id="nkf-000040"></a>
#### nkf-000040 — FormerUpperWord7

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 7. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x646`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000040`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId7`, `GameTribe.FormerUpperWord7`.
- **Verwandte Speicherbereiche:** [nkf-000039](#nkf-000039). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000041"></a>
#### nkf-000041 — FormerLowWord8

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 8. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x648`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000041`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk8`, `GameTribe.FormerLowWord8`.
- **Verwandte Speicherbereiche:** [nkf-000042](#nkf-000042). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000042"></a>
#### nkf-000042 — r_LastRangedAttackerGlobalId8

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 8. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x648`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000042`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId8`, `r_LastRangedAttackerGlobalId8`.
- **Verwandte Speicherbereiche:** [nkf-000041](#nkf-000041), [nkf-000043](#nkf-000043). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E035](#e035), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 451](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L451) (erfasster Commit).

<a id="nkf-000043"></a>
#### nkf-000043 — FormerUpperWord8

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 8. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x64A`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000043`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId8`, `GameTribe.FormerUpperWord8`.
- **Verwandte Speicherbereiche:** [nkf-000042](#nkf-000042). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000044"></a>
#### nkf-000044 — FormerLowWord9

**Verständliche Bedeutung:** Untere historische Hälfte von Angreifer-Slot 9. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x64C`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000044`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.unk9`, `GameTribe.FormerLowWord9`.
- **Verwandte Speicherbereiche:** [nkf-000045](#nkf-000045). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="nkf-000045"></a>
#### nkf-000045 — r_LastRangedAttackerGlobalId9

**Verständliche Bedeutung:** Upstream: Fernkampf-Angreifer-Slot 9. Upstream deutet das vollständige DWORD als globale Identität eines früheren Fernkampfangreifers. Bedeutung, Reihenfolge und zeitliche Gültigkeit der Slots sind nicht unabhängig neu bestätigt. In Extender 2.14.3 beginnt das Feld zwei Bytes früher und ersetzt zwei alte Halbwortdeklarationen; keine bloße Umbenennung.

- **Speicherangabe:** `GameTribe + 0x64C`, `UInt32`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000045`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameTribe.r_LastRangedAttackerGlobalId9`, `r_LastRangedAttackerGlobalId9`.
- **Verwandte Speicherbereiche:** [nkf-000044](#nkf-000044), [nkf-000046](#nkf-000046). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E036](#e036), [E020](#e020).
- **Script-Extender-Quelle:** [GameTribe.cs, Zeile 452](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L452) (erfasster Commit).

<a id="nkf-000046"></a>
#### nkf-000046 — FormerUpperWord9

**Verständliche Bedeutung:** Obere historische Hälfte von Angreifer-Slot 9. Historische 16-Bit-Deklaration aus Extender 2.14.2. Der neuere Quellstand fasst sie mit dem benachbarten Halbwort zu einem DWORD zusammen. Die frühere Benennung bestätigt keine unabhängige Bedeutung dieser Hälfte; kein Lesezugriff und keine automatische Umwandlung.

- **Speicherangabe:** `GameTribe + 0x64E`, `UInt16`; Elementabstand 2 Byte. C#-Kennung: `NativeFieldIds.F000046`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `2.14.2:GameTribe.r_LastRangedAttackerGlobalId9`, `GameTribe.FormerUpperWord9`.
- **Verwandte Speicherbereiche:** [nkf-000045](#nkf-000045). Überlappung bedeutet keine semantische Gleichheit.
- **Belege:** [E026](#e026).

<a id="gameunitmanager"></a>
### GameUnitManager

| Kennung | Katalogname / verständliche Kurzbezeichnung | Bedeutung | Zugriff |
|---|---|---|---|
| [nkf-000063](#nkf-000063) | `r_SelectedChimpsCount` — Upstream: Zähler ausgewählter Einheiten | Niedrig / Kandidat | Gesperrt |


<a id="nkf-000063"></a>
#### nkf-000063 — r_SelectedChimpsCount

**Verständliche Bedeutung:** Upstream: Zähler ausgewählter Einheiten. Als Zählerarray für ausgewählte Einheiten benannt und im Auswahl-/Abwahlkontext erwähnt. Dieser Eintrag liegt relativ zum Unit-Manager; seine vollständige Ausdehnung und der Lesevertrag sind hier nicht bestätigt.

- **Speicherangabe:** `GameUnitManager + 0x24`, `UInt32[9]`; Elementabstand 4 Byte. C#-Kennung: `NativeFieldIds.F000063`.
- **Sicherheit / Verwendung:** Niedrig / Kandidat (`Candidate`); Zugriff **Gesperrt**.
  Dies ist ein Such-/Quellhinweis. Der plausibel klingende Name erhöht die Einstufung nicht von selbst.
- **Suchnamen / Aliasse:** `GameUnitManager.r_SelectedChimpsCount`, `r_SelectedChimpsCount`.
- **Belege:** [E065](#e065), [E066](#e066).
- **Script-Extender-Quelle:** [GameUnitManager.cs, Zeile 36](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs#L36) (erfasster Commit).

## Belegverzeichnis

Jeder Beleg nennt seine konkrete Art, Quelle und Zeilen. Die vollständigen Dateihashes dienen der Reproduzierbarkeit; sie sind keine zusätzliche Bestätigung der Bedeutung. Links zeigen auf den vorhandenen Workspace-Stand. Werden die Dateien später separat nach APIShared kopiert, müssen diese Workspace-Links angepasst oder die erfassten Nachweise mitübernommen werden.

**Belegarten:** Ein nativer Fach-Audit stützt nur seine ausdrücklich dokumentierte Aussage. Eine Extender-Quelldeklaration oder Quellergänzung beweist zunächst Namen/Layout im erfassten Quellstand. Eine bloße Erwähnung im Wissensbericht ist kein zusätzlicher unabhängiger Nachweis. Belege aus demselben Bericht werden nicht als mehrere unabhängige Bestätigungen gezählt.


<a id="e001"></a>
### E001 — Nativer Fach-Audit

- **Quelle:** [DRAWBRIDGES.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/DRAWBRIDGES.md); Zeilen **16–35**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/DRAWBRIDGES.md`.
- **Datei-SHA-256:** `DEA5F7A3388368102BCD3056A5CD931B60FF6519254A98E0EBD248E007471DAE`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e002"></a>
### E002 — Nativer Fach-Audit

- **Quelle:** [HEALTH_BAR_RENDERING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md); Zeilen **5–13**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md`.
- **Datei-SHA-256:** `138CF3A231372D0EC132839DE1B8DEAC6804E72E2AA6785D71DC572FB3F920B3`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e003"></a>
### E003 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **95–95**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 95](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L95).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e004"></a>
### E004 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **96–96**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 96](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L96).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e005"></a>
### E005 — Nativer Fach-Audit

- **Quelle:** [AI_ECONOMY_OBSERVATION.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AI_ECONOMY_OBSERVATION.md); Zeilen **20–27**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/AI_ECONOMY_OBSERVATION.md`.
- **Datei-SHA-256:** `CFD031DF36644C48AB3BB7963AF5A7AA548843000FBF119646C6EEC94F03554B`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e006"></a>
### E006 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **219–219**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 219](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L219).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e007"></a>
### E007 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **220–220**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 220](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L220).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e008"></a>
### E008 — Erwähnung im Wissensbericht

- **Quelle:** [AIV_LOBBY_SELECTION.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md); Zeilen **893–893**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md`.
- **Datei-SHA-256:** `22EF5A167FC0D88B0ECA047F333FD55C7ED7552019C3DBC6CB0210C534A0A7BD`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e009"></a>
### E009 — Erwähnung im Wissensbericht

- **Quelle:** [AIV_LOBBY_SELECTION.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md); Zeilen **920–920**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md`.
- **Datei-SHA-256:** `22EF5A167FC0D88B0ECA047F333FD55C7ED7552019C3DBC6CB0210C534A0A7BD`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e010"></a>
### E010 — Nativer Fach-Audit

- **Quelle:** [DEFEAT_LOOT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/DEFEAT_LOOT.md); Zeilen **3–9**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/DEFEAT_LOOT.md`.
- **Datei-SHA-256:** `2D523B235DA6F1B611868E71EE291B22800953BC32B5A1400EAE224F8BB5E875`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e011"></a>
### E011 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **343–343**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 343](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L343).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e012"></a>
### E012 — Nativer Fach-Audit

- **Quelle:** [PLAYER_DEFEAT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PLAYER_DEFEAT.md); Zeilen **15–23**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/PLAYER_DEFEAT.md`.
- **Datei-SHA-256:** `2E806DA13409417995D3FF32F57A3A5D10C640C7E76784E4F676A5995E03A14C`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e013"></a>
### E013 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **1753–1753**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 1753](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1753).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e014"></a>
### E014 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **1759–1759**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 1759](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1759).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e015"></a>
### E015 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **1772–1772**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 1772](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L1772).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e016"></a>
### E016 — Nativer Fach-Audit

- **Quelle:** [RAID_RETARGET.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md); Zeilen **13–16**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md`.
- **Datei-SHA-256:** `4B56E8D37F874A07D6EA2F6B3075268F268655FC76EA41AA8E2FE0E921CA0DE6`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e017"></a>
### E017 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **3028–3028**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 3028](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3028).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e018"></a>
### E018 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **3029–3029**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 3029](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3029).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e019"></a>
### E019 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **3167–3167**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 3167](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L3167).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e020"></a>
### E020 — Extender-Quelle / Quellergänzung

- **Quelle:** [SE_2_14_3_INTEROP_ADDITIONS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md); Erfasster Zeilenbereich **15–50**; in der erfassten Datei vorhanden sind nur **15–48**. Das gespeicherte Zeilenende ist formal ungenau; der vollständige Dateihash stimmt überein. Dieser Quellhinweis bleibt ein Kandidatenbeleg und wird dadurch nicht zu einem nativen Audit.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md`.
- **Datei-SHA-256:** `B093C8FB6F6E2793C4283D5C4FF4EB0E0E5495E899267FAAFAC62613B7B4E7A8`.
- **Aussagekraft:** Spätere Quellergänzung; kein unabhängiger vollständiger Vanilla- oder Laufzeitaudit.

<a id="e021"></a>
### E021 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **31–31**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 31](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L31).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e022"></a>
### E022 — Erwähnung im Wissensbericht

- **Quelle:** [ENEMY_GATE_BUILDING_CONTEXT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md); Zeilen **63–63**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md`.
- **Datei-SHA-256:** `C35912CFA990572601EF75EAB8153072E3D0049682312CCF60EBAD248259371A`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e023"></a>
### E023 — Nativer Fach-Audit

- **Quelle:** [ENEMY_GATE_BUILDING_CONTEXT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md); Zeilen **59–72**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md`.
- **Datei-SHA-256:** `C35912CFA990572601EF75EAB8153072E3D0049682312CCF60EBAD248259371A`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e024"></a>
### E024 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **31–31**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 31](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L31).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e025"></a>
### E025 — Nativer Fach-Audit

- **Quelle:** [RAID_RETARGET.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md); Zeilen **7–18**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md`.
- **Datei-SHA-256:** `4B56E8D37F874A07D6EA2F6B3075268F268655FC76EA41AA8E2FE0E921CA0DE6`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e026"></a>
### E026 — Extender-Quelle / Quellergänzung

- **Quelle:** [SE_2_14_3_INTEROP_ADDITIONS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md); Zeilen **27–46**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md`.
- **Datei-SHA-256:** `B093C8FB6F6E2793C4283D5C4FF4EB0E0E5495E899267FAAFAC62613B7B4E7A8`.
- **Aussagekraft:** Historische Halbwortdeklaration; nicht identisch mit dem späteren vollständigen DWORD.

<a id="e027"></a>
### E027 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **443–443**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 443](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L443).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e028"></a>
### E028 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **444–444**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 444](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L444).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e029"></a>
### E029 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **445–445**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 445](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L445).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e030"></a>
### E030 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **446–446**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 446](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L446).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e031"></a>
### E031 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **447–447**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 447](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L447).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e032"></a>
### E032 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **448–448**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 448](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L448).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e033"></a>
### E033 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **449–449**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 449](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L449).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e034"></a>
### E034 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **450–450**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 450](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L450).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e035"></a>
### E035 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **451–451**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 451](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L451).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e036"></a>
### E036 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameTribe.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs); Zeilen **452–452**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameTribe.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 452](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameTribe.cs#L452).
- **Datei-SHA-256:** `B51B10DD9794C8AE53F904BF30C78F81F0E7289292E6F207201A1FDBBACE1127`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e037"></a>
### E037 — Nativer Fach-Audit

- **Quelle:** [HEALTH_BAR_RENDERING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md); Zeilen **13–17**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md`.
- **Datei-SHA-256:** `138CF3A231372D0EC132839DE1B8DEAC6804E72E2AA6785D71DC572FB3F920B3`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e038"></a>
### E038 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **30–30**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 30](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L30).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e039"></a>
### E039 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **55–55**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 55](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L55).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e040"></a>
### E040 — Erwähnung im Wissensbericht

- **Quelle:** [SE_2_14_1_ENGAGE_RANGE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md); Zeilen **9–9**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md`.
- **Datei-SHA-256:** `33AD033DAA57D0BF183DDDEF1081161CF80CCAF543C6C0B63360C2C8931F7BA9`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e041"></a>
### E041 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **10–21**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e042"></a>
### E042 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **55–55**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 55](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L55).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e043"></a>
### E043 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **60–60**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 60](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L60).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e044"></a>
### E044 — Erwähnung im Wissensbericht

- **Quelle:** [ENEMY_GATE_BUILDING_CONTEXT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md); Zeilen **313–313**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/ENEMY_GATE_BUILDING_CONTEXT.md`.
- **Datei-SHA-256:** `C35912CFA990572601EF75EAB8153072E3D0049682312CCF60EBAD248259371A`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e045"></a>
### E045 — Erwähnung im Wissensbericht

- **Quelle:** [FIREMAN_TARGETING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md); Zeilen **29–29**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md`.
- **Datei-SHA-256:** `8B5F6928C7F27C091063D8867F49A205FC52E3A12FA286F326023555E10F9B7B`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e046"></a>
### E046 — Erwähnung im Wissensbericht

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **61–61**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e047"></a>
### E047 — Erwähnung im Wissensbericht

- **Quelle:** [WORKSHOP_IDLE_DELAY.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md); Zeilen **55–55**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md`.
- **Datei-SHA-256:** `62BC8A4FA8207AD74BC2FB541A7FDA54A0368E243FEF8DE600FE203DBB0AD899`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e048"></a>
### E048 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **53–64**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e049"></a>
### E049 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **60–60**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 60](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L60).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e050"></a>
### E050 — Nativer Fach-Audit

- **Quelle:** [PLAGUE_HEIGHT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PLAGUE_HEIGHT.md); Zeilen **27–38**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/PLAGUE_HEIGHT.md`.
- **Datei-SHA-256:** `0FCFAC985AB423EA17197AB0D83A6CDAB3117BA7B6BFAEE1DD84F42D058F1275`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e051"></a>
### E051 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **7–21**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e052"></a>
### E052 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **209–209**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 209](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L209).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e053"></a>
### E053 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **49–58**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Nahkampf-Gegenbeleg zur ausschließlich auf Projektile beschränkten Deutung.

<a id="e054"></a>
### E054 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **7–18**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e055"></a>
### E055 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **211–211**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 211](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L211).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e056"></a>
### E056 — Nativer Fach-Audit

- **Quelle:** [OUTPOSTS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/OUTPOSTS.md); Zeilen **45–51**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/OUTPOSTS.md`.
- **Datei-SHA-256:** `72564C7AAB6D4C5AC4C7C223EE35D7A0563F466DAB0D6EFD0AED8EDC89960E9A`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e057"></a>
### E057 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **229–229**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 229](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L229).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e058"></a>
### E058 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **272–272**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 272](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L272).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e059"></a>
### E059 — Nativer Fach-Audit

- **Quelle:** [HEALTH_BAR_RENDERING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md); Zeilen **5–17**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md`.
- **Datei-SHA-256:** `138CF3A231372D0EC132839DE1B8DEAC6804E72E2AA6785D71DC572FB3F920B3`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e060"></a>
### E060 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **323–323**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 323](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L323).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e061"></a>
### E061 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **324–324**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 324](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L324).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e062"></a>
### E062 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **360–360**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 360](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L360).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e063"></a>
### E063 — Erwähnung im Wissensbericht

- **Quelle:** [ENEMY_GATE_ASSASSINS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md); Zeilen **59–59**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md`.
- **Datei-SHA-256:** `0444ED9B736455369C89A5A8CFEDE9F11822D2D69A8B4F2CA1933426D48A8849`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e064"></a>
### E064 — Nativer Fach-Audit

- **Quelle:** [BEDOUIN_HEALER_TARGETS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md); Zeilen **5–10**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md`.
- **Datei-SHA-256:** `2E553C38723A65C671E6D2BCAC7CE8E4CFB1BDB70F646520899A5CA4890C1608`.
- **Aussagekraft:** Nativer Fachbericht für den genannten Teilkontext. Spielabnahme und weitere Bedeutungen sind davon unabhängig.

<a id="e065"></a>
### E065 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnitManager.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs); Zeilen **36–36**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 36](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs#L36).
- **Datei-SHA-256:** `AD40F1D9A3B7EBC4A3118B7EEB8D13927294B2C8A18D33B848034104F4A5C8BC`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e066"></a>
### E066 — Erwähnung im Wissensbericht

- **Quelle:** [SURRENDER_SELECTION.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SURRENDER_SELECTION.md); Zeilen **11–11**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/SURRENDER_SELECTION.md`.
- **Datei-SHA-256:** `80C73D2BD397BDCE5448B20DCD9506A3DCF009393E67160BC8B8EEC548FF704E`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e067"></a>
### E067 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **227–227**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 227](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L227).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e068"></a>
### E068 — Nativer Fach-Audit

- **Quelle:** [FIREMAN_TARGETING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md); Zeilen **9–23**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md`.
- **Datei-SHA-256:** `8B5F6928C7F27C091063D8867F49A205FC52E3A12FA286F326023555E10F9B7B`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e069"></a>
### E069 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **254–254**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 254](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L254).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e070"></a>
### E070 — Nativer Fach-Audit

- **Quelle:** [OUTPOSTS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/OUTPOSTS.md); Zeilen **12–20**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/OUTPOSTS.md`.
- **Datei-SHA-256:** `72564C7AAB6D4C5AC4C7C223EE35D7A0563F466DAB0D6EFD0AED8EDC89960E9A`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e071"></a>
### E071 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **255–255**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 255](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L255).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e072"></a>
### E072 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **256–256**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 256](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L256).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e073"></a>
### E073 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **257–257**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 257](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L257).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e074"></a>
### E074 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **258–258**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 258](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L258).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e075"></a>
### E075 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **259–259**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 259](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L259).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e076"></a>
### E076 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **260–260**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 260](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L260).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e077"></a>
### E077 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **261–261**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 261](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L261).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e078"></a>
### E078 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **262–262**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 262](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L262).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e079"></a>
### E079 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **265–265**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 265](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L265).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e080"></a>
### E080 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **266–266**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 266](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L266).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e081"></a>
### E081 — Extender-Quelle / Quellergänzung

- **Quelle:** [GamePlayerResources.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs); Zeilen **303–303**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 303](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GamePlayerResources.cs#L303).
- **Datei-SHA-256:** `FA6B7B12789C088C8120FC4756CCC3A579494F5A220DE6EEAB1C3CD39A49D9DF`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e082"></a>
### E082 — Nativer Fach-Audit

- **Quelle:** [DEFEAT_LOOT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/DEFEAT_LOOT.md); Zeilen **5–9**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/DEFEAT_LOOT.md`.
- **Datei-SHA-256:** `2D523B235DA6F1B611868E71EE291B22800953BC32B5A1400EAE224F8BB5E875`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e083"></a>
### E083 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **56–56**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 56](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L56).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e084"></a>
### E084 — Erwähnung im Wissensbericht

- **Quelle:** [ENEMY_GATE_ASSASSINS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md); Zeilen **17–17**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/ENEMY_GATE_ASSASSINS.md`.
- **Datei-SHA-256:** `0444ED9B736455369C89A5A8CFEDE9F11822D2D69A8B4F2CA1933426D48A8849`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e085"></a>
### E085 — Erwähnung im Wissensbericht

- **Quelle:** [HEALTH_BAR_RENDERING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md); Zeilen **17–17**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/HEALTH_BAR_RENDERING.md`.
- **Datei-SHA-256:** `138CF3A231372D0EC132839DE1B8DEAC6804E72E2AA6785D71DC572FB3F920B3`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e086"></a>
### E086 — Erwähnung im Wissensbericht

- **Quelle:** [VANILLA_PEACE_TIME.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/VANILLA_PEACE_TIME.md); Zeilen **68–68**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/VANILLA_PEACE_TIME.md`.
- **Datei-SHA-256:** `4A17F3D4D1F638710118B4E6BD0F3ED2A17866B41E519FB7B865F81345C06473`.
- **Aussagekraft:** Erwähnung im Fachbericht; daraus entsteht weder eine neue Bedeutungsbestätigung noch eine Lesefreigabe.

<a id="e087"></a>
### E087 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **68–68**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 68](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L68).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e088"></a>
### E088 — Nativer Fach-Audit

- **Quelle:** [OUTPOSTS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/OUTPOSTS.md); Zeilen **16–31**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/OUTPOSTS.md`.
- **Datei-SHA-256:** `72564C7AAB6D4C5AC4C7C223EE35D7A0563F466DAB0D6EFD0AED8EDC89960E9A`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e089"></a>
### E089 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **71–71**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 71](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L71).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e090"></a>
### E090 — Nativer Fach-Audit

- **Quelle:** [BEDOUIN_HEALER_TARGETS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md); Zeilen **5–10**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/BEDOUIN_HEALER_TARGETS.md`.
- **Datei-SHA-256:** `2E553C38723A65C671E6D2BCAC7CE8E4CFB1BDB70F646520899A5CA4890C1608`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e091"></a>
### E091 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **72–72**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 72](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L72).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e092"></a>
### E092 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **243–243**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 243](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L243).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e093"></a>
### E093 — Nativer Fach-Audit

- **Quelle:** [WORKSHOP_IDLE_DELAY.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md); Zeilen **20–34**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/WORKSHOP_IDLE_DELAY.md`.
- **Datei-SHA-256:** `62BC8A4FA8207AD74BC2FB541A7FDA54A0368E243FEF8DE600FE203DBB0AD899`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e094"></a>
### E094 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **268–268**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 268](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L268).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e095"></a>
### E095 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **288–288**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 288](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L288).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e096"></a>
### E096 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **297–297**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 297](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L297).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e097"></a>
### E097 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **310–310**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 310](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L310).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e098"></a>
### E098 — Nativer Fach-Audit

- **Quelle:** [FIREMAN_TARGETING.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md); Zeilen **15–23**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/FIREMAN_TARGETING.md`.
- **Datei-SHA-256:** `8B5F6928C7F27C091063D8867F49A205FC52E3A12FA286F326023555E10F9B7B`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

<a id="e099"></a>
### E099 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **311–311**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 311](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L311).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e100"></a>
### E100 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **357–357**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 357](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L357).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e101"></a>
### E101 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **214–214**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 214](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L214).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e102"></a>
### E102 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameBuilding.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs); Zeilen **263–263**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameBuilding.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 263](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameBuilding.cs#L263).
- **Datei-SHA-256:** `2EADBC2EE4D8A42914B526969430FA88C4E93875B983C6188C9C1C37CF00C3D4`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e103"></a>
### E103 — Nativer Fach-Audit

- **Quelle:** [GATEHOUSE_LIVING_CAPTURE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md); Zeilen **49–58**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/GATEHOUSE_LIVING_CAPTURE.md`.
- **Datei-SHA-256:** `FB721038721AAF620F498BC377C2A3852F09DCABC081094F0179D48F5F860C95`.
- **Aussagekraft:** Historisches kombiniertes Feld; die native Kampfprüfung verwendet nur die unteren 16 Bits.

<a id="e104"></a>
### E104 — Extender-Quelle / Quellergänzung

- **Quelle:** [GameUnit.cs](../../../shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs); Zeilen **212–212**.
- **Quellkennung:** `extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`.
- **Upstream-Permalink:** [Datei am erfassten Commit, ab Zeile 212](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/GameUnit.cs#L212).
- **Datei-SHA-256:** `2ECFBD78D4FA63DD1D4F4DD07C535F14C629B52510DE3E585C678B91900DE3C8`.
- **Aussagekraft:** Erfasste Quelldeklaration bzw. Quelländerung; getrennt von der nativen Semantik zu bewerten.

<a id="e105"></a>
### E105 — Nativer Fach-Audit

- **Quelle:** [SE_2_14_1_ENGAGE_RANGE.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md); Zeilen **7–11**.
- **Quellkennung:** `baseline/sem/FBCB9319/knowledge/SE_2_14_1_ENGAGE_RANGE.md`.
- **Datei-SHA-256:** `33AD033DAA57D0BF183DDDEF1081161CF80CCAF543C6C0B63360C2C8931F7BA9`.
- **Aussagekraft:** Begrenzter nativer Offset-/Kontextnachweis; vollständige skalare Zugriffsfreigabe bleibt offen.

## Pflege dieser Übersicht

Die hier vergebenen sprachlichen Bewertungen erläutern die aktuellen Katalogstufen. Neue Erkenntnisse müssen zuerst im maßgeblichen Katalog und seinen Evidenzen verarbeitet werden; anschließend sind Tabellen und Detailtexte gemeinsam abzugleichen. Bedeutungsverbesserungen dürfen bestehende Kennungen nicht auf andere Rohwerte umlenken. Eine Übersicht mit geänderten Quellhashes ist zunächst als historische Momentaufnahme zu behandeln, nicht durch bloßes Ersetzen der Hashes zu „bestätigen“.
