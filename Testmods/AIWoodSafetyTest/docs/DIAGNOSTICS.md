# AIWoodSafetyTest – Einzelspieler-Testfix

Dieser separate Gameplay-Testmod betrifft nur neue KI-Holzfällerhütten und den
aktuellen Todesbeitrag eines KI-Holzfällers zum Hüttenzähler. Beide Regeln sind
standardmäßig aktiv, werden unabhängig voneinander zu Partiebeginn eingelesen
und gelten auch für neu geladene Einzelspieler-Saves. Editor, Replay, echtes
Multiplayer, Multiplayer-Saves sowie unbekannte/widersprüchliche Modi sind ausgeschlossen.

## Einstellungen

Datei nach späterer Installation: `BepInEx/config/AIWoodSafetyTest_Serp.cfg`.
Abschnitt `Safety`, Änderungen ausschließlich zwischen Partien:

- `ProtectNewHutsFromFortifications = true`
- `IgnoreDeadWorkersForCounter = true`

Der Testmod benötigt den **gemeinsam gebauten AIBuildDiagnoseTest**. Dessen Version
bleibt 0.2.1; ein älteres Paket derselben Versionsnummer enthält den neuen internen
Vertrag noch nicht. Das Buildpaket enthält deshalb beide Mods. APIShared, Extender,
BugfixesAndQoL und optional Fixes werden aus der vorhandenen Installation referenziert,
weder gebaut noch aktualisiert. CanarariWoodTest ist nicht erforderlich.

## Standortregel

Die größere effektive Reichweite von europäischem und arabischem Bogenschützen
kommt aus `GetUnitAttackRange`; im auditierten Vanilla-Abbild beträgt sie 54 Felder.
Eine unbesetzte feindliche Befestigung zählt genauso wie eine besetzte.
Alle nativen Turm- und Torhausvarianten zählen; zerstörte Turmtypen, eigene und
verbündete Gebäude nicht. Diplomatie folgt dem bestehenden Extender-Vertrag
`IsPlayerAlliedTo`: gleiche aktuelle Team-ID. Es werden keine Bogenschützen gesucht.

Abstand ist die kürzeste euklidische Entfernung zwischen den belegten Kartenfeldern
der vollständigen 3×3-Hütte und der vollständigen quadratischen Gebäudegrundfläche
aus `r_TilePositionXBegin/YBegin` und `r_OccupyTileGridSize`. Die Grenze ist inklusive.
Ein Anfangsscan erfasst Befestigungen; Spawn-/Abriss-/Eroberungsereignisse markieren
betroffene Slots. Vor einem Holzbaupass werden nur diese und bereits bekannte
Befestigungsidentitäten geprüft, einschließlich Typänderung zur Ruine. Besitzer,
Global-ID, Wiederaufbau, Diplomatie und wirksame Reichweiten gehen vor der Suche
neu ein. Ein Schadenereignis liefert keine Building-ID und wird hierfür nicht missbraucht.

Pro Besitzer wird eine referenzgezählte Gefahrenmaske für alle 800×800 möglichen
Hüttenursprünge vorbereitet (zusammen 11.520.000 Byte). Überlappende Befestigungen
bleiben beim Abriss einer einzelnen Befestigung korrekt gesperrt. Arbiträre AIV-
Koordinaten nutzen dieselbe Maske wie die auf Fünferkoordinaten arbeitende Suche.
Die Kandidatenprüfung liest höchstens acht Maskeneinträge, ohne Kartenscan,
Einheiten-/Baumsuche, neue native Abfrage oder Ersatzsuche. Der native Vorfilter
überspringt deaktivierte, fremde und ungefährliche Aufrufe ohne Managed-Übergang.
Der Callback liest die Masken nicht nochmals.

Nur innerhalb des synchronen KI-Holzbaukontexts wird die Kandidatenannahme von
Vanilla abgelehnt. Der vorhandene BFS reiht den Kandidaten dann normal ein und
setzt dieselbe Suche fort. Reihenfolge, Queue, Grenzen und Erschöpfung bleiben
unverändert. Andere Nutzer der gemeinsamen Suchfunktion erhalten keine neue Regel.
Eine zusätzliche `OnPlacementValidation`-Post-Prüfung setzt ausschließlich bei
gefährlicher KI-Holzhütte das vorhandene `IsPlacementBlocked`-Ergebnis. Sie läuft
nach Vanilla und nach dem globalen Extender-Override; feste AIV-Vorgaben folgen
damit dem normalen Fehler-/Wiederholungsweg. Andere Beobachter dürfen diese Sperre
nicht anschließend wieder aufheben. Der Testmod erzwingt keinen Ersatzbauplatz.

Die Regel schützt die Hütte. Arbeitswege und Bäume außerhalb dieser Grundfläche
können weiter unter Beschuss liegen. Bei vollständig gefährlichem Baubereich kann
die KI entsprechend weniger Holz produzieren; genau das gehört zur Spielabnahme.

## Todesbeitrag

Der allgemeine Diagnosemod behält den bestehenden Hook bei `0x12D1A2`. Nach dem
nativen WORD-ADD von zehn übergibt er synchron Sitzung, Einheitenslot/Global-ID,
Hüttenslot/Global-ID und den tatsächlichen Zielzähler. Der Testmod prüft die aktuellen
Identitäten erneut, Holzfällertyp, KI-Besitzer, Zustand 110, Markierung zur Löschung,
Gesundheit null, gesetzten nativen Todesmarker und die beidseitige Hüttenzuordnung.
Der missverständlich benannte Marker `r_IsKilledByProjectile` gilt im auditierten
Code auch für Nahkampftode. Ein fehlendes früheres Todesereignis ist kein Problem,
wenn diese synchronen Belege vollständig vorliegen. Widersprüche verhindern den Eingriff.

Die einzige Korrektur ist `counter = unchecked((short)(nativeAfter - 10))` am
tatsächlichen nativen Ziel. Sie entfernt genau den aktuellen Beitrag, auch wenn
der gespeicherte Altstand eine unbekannte Vorgeschichte hat. Altstände werden
weder zurückgesetzt noch nachträglich als Todesbeiträge interpretiert. Lebende
Rückkehr-/Erreichbarkeitsfehler und andere Abrissregeln bleiben wirksam. Eine
doppelte Meldung mit altem `nativeAfter` scheitert am unveränderten Erwartungswert.

Der Diagnosemod schreibt ohne Testmod weiterhin nichts. Seine allgemeine Ausgabe
trennt `nativeAdd=10`, `nativeAfter`, `remainingCounter`, `correctionConfirmed` und
`netAdd`. Korrigierte Beiträge zählen nicht als effektive Inkremente im Ursachenledger.
Unbestätigte Änderungen bleiben ausdrücklich unbekannt. Arbeitertod, Hüttenposition,
Arbeitsziel, tatsächliche Entfernung und Neubau behalten ihre getrennten Belege.

## Lebensdauer und Ausgaben

Alle neuen Zustände sind sitzungsbezogen. Ein BeforeLoad beendet die aktive Regel
auch bei anschließend fehlgeschlagenem Laden. Verschachtelte Holzbaukontexte werden
im finally wiederhergestellt; alte finally-Blöcke verändern keine neue Sitzung.
Veröffentlichte Hooks, Callback und Masken bleiben bis Prozessende verwurzelt.
Nur der Daten-Gate wird deaktiviert. Ein Fehler deaktiviert den betroffenen Regelteil;
Vanilla und die unabhängige andere Regel bleiben ausführbar.

`AI_WOOD_SAFETY_SESSION`, `INDEX_READY`, `CANDIDATE_READY`, die ersten drei
`CANDIDATE_REJECTED`/`PLACEMENT_REJECTED`/`DEATH_COUNTER_CORRECTED` und periodische
`SUMMARY` protokollieren Freigabe, Reichweite und Wirkung. Die Zusammenfassung nennt
native Kandidaten/Maskentreffer, Callbacks, Ablehnungen, Korrekturen, Fehler und
Auslassungen. Pro Sitzung gelten 1 MiB eigene Ausgabe, davon 64 KiB Abschlussreserve.
Die allgemeinen 56 MiB plus 8 MiB Wirtschaft sowie das Canari-Budget bleiben getrennt.

## Build und Abnahme

`build.bat /nopause /noinstall` baut ausdrücklich den aktuellen Arbeitsstand gegen
die installierten SDK-Dateien. Der Treiber prüft beide Mods, führt allgemeine,
Sicherheits- und Begleitmodtests aus und erzeugt ein gemeinsames ZIP unter
`.work/a/wood-safety-build/`. Er installiert nichts und ändert keine Version bestehender Mods.

Die neue Testsuite prüft Geometrie, Typen, Besitz/Bündnisse, Überlappung, Ruinen,
Reichweitenänderung, aktuelle Todeskorrektur, Altstände, lebende Rückkehrfehler,
widersprüchliche Identitäten, Doppelmeldung, Sitzungswechsel und Kontextfehler.
Layoutfehler werden je Regel isoliert, auch über Sitzungswechsel hinweg. Der
feste AIV-Platzierungsweg wird gegen Vorereignis, andere Mapper, menschlichen
Spieler, verbündete Befestigung und erlaubten Standort geprüft; er setzt nur eine Sperre.
Ein Maschinentest führt die vollständige echte Vanilla-BFS auf privaten Daten aus:
ein gesperrter Kandidat führt zum passenden Alternativplatz, vollständig gesperrte
Möglichkeiten zum regulären Misserfolg, mit identischer Queue wie im Vanilla-Vergleich.
Der produktive Generator samt beiden Fortsetzungen und nativem Vorfilter läuft
dabei unverändert; nur die CLR-Callbackgrenze wird durch einen nativen Testbody ersetzt.

**Spielabnahme bleibt separat offen.** Nach ausdrücklicher Installationsfreigabe
Original und Testfix aus demselben betroffenen Save, mit denselben Gameplay-Mods,
Einstellungen, Geschwindigkeit, Zoom und Beobachtungsdauer vergleichen. Mindestens
zwei neue Arbeiter-/Hüttenidentitäten verfolgen. Nachweisen: unterbundene Todesbeiträge,
tatsächliche Abrisse, Neubauten, Holzbestand/Hüttenbelegung und Laufzeitkosten.
Beide Regeln zusätzlich einzeln deaktiviert prüfen. Ein Build oder Scratch-Test
ersetzt diesen Save-Nachweis nicht; die aktuelle Gameplay-Modkonfiguration bestimmt
weiterhin, ob ein Vergleich unverfälschtes Vanilla darstellt.
