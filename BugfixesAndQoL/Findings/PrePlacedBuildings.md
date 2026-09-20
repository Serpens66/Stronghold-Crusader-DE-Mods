# Vorplatzierte Gebäude und Vanilla-KI – finaler Wissensstand

- Stand: 20. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Bestätigte Laufzeit: Script Extender 2.8.0, APIShared 0.3.7, RedBird 1.3.2 und Fixes 1.17.1
- Spieler-IDs und Farben sind keine festen Rollen. Alle Besitzer und Testrollen werden je Kartenstart neu bestimmt.

## Ruinen-Startdelay

Auf der betroffenen Altformatkarte existieren fünf stabile vorplatzierte Records der Typen
`STRUCT_TOWER1_DESTROYED` bis `STRUCT_TOWER5_DESTROYED`. Der Altformatpfad `0xD4290`
erzeugt den störenden Wert nicht, sondern kopiert `crushed_building_delay=1` aus dem
serialisierten Record in den aktuellen Spielerrecord. Der Scheduler wartet deshalb ungefähr
49 Ticks, bevor die KI ihre AIV ausführt.

Der Fix normalisiert ausschließlich diesen belegten Transferfall:

- frische Altformatkarte, kein Savegame;
- stabiler Quellwert `1` und Zielwechsel `0→1` durch denselben Transfer;
- passende stabile Game-/Global-ID eines der fünf zerstörten Turmtypen;
- zuverlässige spätere Zuordnung des Besitzers zu einer KI;
- Timer bei der Anwendung weiterhin exakt `1`;
- keine zwischenzeitliche echte Schadensaktivierung.

Serialisierte Kartendaten, Gebäuderecords und Besitzer bleiben unverändert. Spätere echte
Gebäudeverluste behalten Vanillas Verhalten. Ein Langzeittest mit zwei Kampfverlusten blieb
ohne Crash. Der frühere Crash bei `0x7F07C` stammte aus einem inzwischen vollständig
entfernten Diagnosehook, in dessen überschriebenen Bereich Vanilla direkt springen konnte.

Dieser Fix ist fachlich vom vorhandenen `AITowerRuinRepairFix` getrennt: Jener behandelt
runtime-erzeugte Ruinen während der AIV-Platzierung, dieser ausschließlich den übernommenen
Starttimer vorplatzierter Altformatobjekte.

## Vorplatzierte Mauern und Tore

Vanilla baut das 160×160-Wirtschaftsraster in `0x50720` gegen die häufigste positive PCL
der gesamten Karte auf. `byte+04` einer 5×5-Zelle zählt Tiles außerhalb dieser globalen
Referenz. Freundliche Portalrouten sind dabei nicht spielerspezifisch berücksichtigt.

Die externen Wirtschaftsentscheidungen verwenden anschließend:

- Census `0x55FE0`;
- Farmen `0x575B0`;
- Stein, Eisen und Pech `0x57B80`;
- Holz `0x58020`;
- Nahsuche für Holz, Ochsenjoch und Steinbruch `0x58950`.

Der vorgelagerte Census ist entscheidend: Er setzt die zehn Verfügbarkeits- und
Cooldownfelder im Spielerrecord. Ohne korrigierten Census können Dispatcher bereits vor der
eigentlichen Suche abbrechen.

Bei vorplatzierten Torhäusern waren aktive, offene Portalrecords mit gültigen Endpunkten
vorhanden, ihr Ownerfeld `r_OwnerOrAccessPlayerId` bei `+0x1E4` enthielt aber noch einen alten
Besitzer. Vanilla korrigierte es erst Sekunden später. Der Fix synchronisiert das Feld bei
`OnStartMap Post` ausschließlich für eindeutig über Building-ID und Global-ID validierte,
lebende und vorplatzierte Portale einer frischen Karte. Saves, spätere AIV-Tore, zerstörte
oder uneindeutige Records bleiben unverändert.

Erreichbare PCLs bestimmt weiterhin Vanillas eigene Route mit
`PathConnectionQueryMode.ExcludeLadderClimb`; damit bleiben Diplomatie, feindliche Tore,
Leiterpfade und echte Isolation autoritativ. Nur `byte+04` wird für den Census und die
originale Suche temporär spielerspezifisch projiziert. `byte+16`, Queue, Cooldowns,
Ergebnisfelder und alle weiteren Zellbytes bleiben unangetastet. Das Original läuft genau
einmal, und alle geänderten Bytes werden in einem `finally`-Pfad exakt restauriert.

Eine torlose, geschlossene KI bleibt vollständig Vanilla. Relevante Damage-, Bulldoze- oder
Delete-Ereignisse markieren ihre Baseline nur als geändert. Erst der nächste echte
Wirtschaftslauf bestätigt einen Durchbruch aus zwei Bedingungen: Ein Baseline-Mauertile hat
seinen blockierenden Zustand verloren, und dessen stabile Innen-/Außenanker sind danach in
derselben positiven, von der Burg erreichbaren PCL verbunden. Schaden allein oder eine reine
PCL-Neunummerierung reichen nicht. Nach der Bestätigung erhält die KI denselben einmaligen
Re-Census und dasselbe temporäre Overlay.

Portal- und Durchbruchpfad wurden praktisch bestätigt. Torhaus-KIs bauten Farmen, Minen,
Steinbrüche, Ochsenjoche und Holzfäller außerhalb ihrer Einfassung. Eine torlose KI blieb bis
zum bestätigten Durchbruch gesperrt und baute danach ebenfalls externe Wirtschaftsgebäude
einschließlich Holzfällern. Alle beobachteten Overlays wurden exakt restauriert.

## Holzscore

`0x58020` initialisiert seinen lokalen Bestscore mit `-100` und übernimmt nur strikt bessere
Kandidaten. Sind formale Kandidaten vorhanden, aber alle Scores höchstens `-100`, liefert
Vanilla fälschlich kein Ergebnis. Der eng begrenzte Context-Hook bei `0x58057` setzt den
Startwert nur während eines berechtigten `ActivePortal`- oder `ActiveBreach`-Holzoverlays auf
`int.MinValue`. Traversierung, Prädikate, Scoring und Bestkandidatenauswahl bleiben Vanilla;
es gibt weder eine eigene Suche noch einen zweiten Durchlauf.

## Finaler Fixaufbau in BugfixesAndQoL

`BugfixesAndQoL 1.0.158` enthält zwei getrennte interne Fixkomponenten:

1. `LegacyRuinTimerFix` für den eng belegten Altformat-Timertransfer.
2. `PreplacedEconomyAccessFix` für Portalbesitzer, Durchbruchaktivierung, Re-Census,
   Wirtschaftsoverlay und den kontextgebundenen Holzscore.

Der Runtime-Orchestrator installiert nur zehn Funktionsdetours und einen Context-Hook:
`0x50680`, `0xD4290`, `0x50720`, `0x50F90`, `0x51270`, `0x51540`, `0x575B0`,
`0x57B80`, `0x58020`, `0x58950` sowie `0x58057`. `0x55FE0` wird über einen validierten,
statisch verwurzelten Delegate direkt aufgerufen.

Es gibt keine Unity-`Update`-/`LateUpdate`-/`FixedUpdate`-Methode, keinen Timer und kein
periodisches Polling. Gebäude- und Mauerdaten werden einmal im Pre-Callback von `0x50680`
unmittelbar vor der ersten AIV-Zuweisung erfasst. Das APIShared-`BeforeLoad`-Signal setzt nur
den Sitzungszustand zurück, weil die nativen Kartendaten dort noch nicht geladen sind. Fehlt
der `0x50680`-Checkpoint trotz vorhandener KI, bleibt der Wirtschaftspfad für diese Karte
fail-closed; eine verspätete Erfassung könnte bereits AIV-erzeugte Gebäude fälschlich als
vorplatziert einstufen. Cacheaufbau erfolgt nur bei Aktivierung oder echter Topologieänderung; normale
Suchaufrufe verwenden wiederverwendbare Arrays und ändern/restaurieren nur abweichende
Zellen. Vertrags-, Verschachtelungs- oder Restaurierungsfehler schalten den Wirtschaftspfad
prozessweit fail-closed auf Vanilla zurück.

Die hostverwaltete, standardmäßig aktive Einstellung `FixAIPreplacedMapBuildings` steuert
beide Fehlerbereiche gemeinsam und wird bei jedem Kartenstart fest übernommen. Änderungen
während einer Mission gelten ab der nächsten Karte. Der Wirtschaftszugriffsfix bleibt vom
vorhandenen `AIEconomyProtectionHook` getrennt, weil dieser nachgelagerte Schlaf-/Abriss- und
Accessibility-Folgen behandelt, nicht die vorgelagerten Census- und Suchentscheidungen.

Die Runtime ist prozessweit statisch verwurzelt und gehört ausdrücklich nicht zum normalen
`BugfixesAndQoLRuntime.Dispose()`-Pfad. Bei einem Fehler in Hash-, Signatur-, ABI-, Portal-,
Verschachtelungs- oder Restaurierungsverträgen fällt ausschließlich dieser Fixbereich
fail-closed auf Vanilla zurück; alle übrigen Funktionen des Mods bleiben aktiv.
