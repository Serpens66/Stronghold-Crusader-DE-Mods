# Audit des veralteten AIFramework v1.13

## Ergebnis in Kurzform

Das AIFramework v1.13 war eine Kombination aus AIC-/AIV-Loader, Spawn- und
Balance-Editor, Trail-Editor, UI-/Asset-Override und mehreren fest
einprogrammierten nativen Verhaltenspatches für Spielversion 1.043/1.4.

Der größte Teil seiner Funktionalität ist heute durch den Script Extender oder
durch vorhandene Workspace-Mods besser und versionssicherer abgedeckt. Der alte
Code darf nicht in die aktuelle Installation übernommen werden: Seine absoluten
RVA-Angaben zeigen auf eine andere Binärversion, seine Laufzeit-Patches validieren
weder Signaturen noch Instruktionsgrenzen, und der Managed-Teil verwendet unter
anderem `System.Text.Json` und `YamlDotNet`, was nicht den heutigen Vorgaben für
Runtime-Mods entspricht.

Nur eine alte Idee bleibt als möglicher Untersuchungskandidat übrig:

1. Das Framework verkürzte beziehungsweise verschob Schwellwerte der
   Torhaus-Automatik. Die zugehörige Logik existiert nachweislich weiterhin in
   der aktuellen DLL. Der bestehende Farmer-Stuck-Fix des Fixes-Mods ist davon
   verschieden. Wegen möglicher Gameplay-Auswirkungen sollte dies höchstens als
   optionale Einstellung und erst nach Verhaltenstests umgesetzt werden.

Der alte Fix gegen KI-Panikabriss ist bereits durch `ExtraFeatures` abgedeckt.
Die neue Implementierung ist sogar enger und sicherer: Sie überspringt nur den
Notfallabrissblock und die konkrete Hüttenabrissentscheidung, während das alte
Framework eine komplette native Funktion durch ein sofortiges `RET` ersetzte.

Der alte Angriffsziel-Patch und die globale Angriffswellen-Konfiguration sind
inzwischen durch Vanilla überholt. Die aktuelle DLL besitzt für den
Angriffszielpfad sowohl globale Siege-Optionen als auch ein per Lord gesetztes
Bitfeld. Armee-Maximum und Wachstum stehen jetzt als drei AIC-/Lord-Parameter
zur Verfügung. Dafür ist kein zusätzlicher Workspace-Fix sinnvoll.

## Untersuchte Versionen und Quellen

### Altes Projekt

- Projekt: `AIFramework_v1.13_for_SHCDEv1.043`
- Managed Plugin: `BepInEx/plugins/AIFrameworkNET.dll`
- SHA-256 Managed Plugin:
  `086F98156EA372656B0BF1D4D7A5CB7B2E99E9C4520A7411CA8FEB4DABD30169`
- Native Plugin: `BepInEx/plugins/Native/AIFramework.dll`
- SHA-256 Native Plugin:
  `75E812F17A27D16D2372A07F551973339FFD6ED6C39FF159A52C86BE8FD17A79`
- Externe Adresstabelle bezeichnet sich als Version `1.043`.
- Das veröffentlichte Changelog wurde mit der lokalen Implementierung
  abgeglichen:
  <https://www.moddb.com/mods/ai-framework/downloads/aiframework>
- Fireflys offizieller Autumn-Update-Hinweis dokumentiert die nachträglich
  ergänzte globale verbesserte CPU-Belagerungslogik:
  <https://steamcommunity.com/app/3024040/discussions/0/599666256597257027/>
- Fireflys offizieller Winter-Update-Hinweis dokumentiert den aktuellen
  CPU-Lord-Editor:
  <https://fireflyworlds.com/2026/02/03/create-your-own-lord-the-winter-update-now-live/>
- Die offiziellen Patchnotes zu Version 2.61 nennen „Max Siege Size“ und
  „Siege Wave Size Multiplier“ ausdrücklich als editierbare Parameter für
  Custom CPU Lords:
  <https://steamcommunity.com/app/3024040/allnews/?l=english>

Der Managed-Code wurde mit ILSpy dekompiliert. Das native Plugin wurde über den
vorgeschriebenen Rizin-/Ghidra-Wrapper analysiert. Die dabei erzeugten
Analyseartefakte liegen unter `.inspect/AIFramework_v1.13_managed` und wurden
absichtlich nicht entfernt.

### Aktueller Vergleichsstand

- Kanonische DLL: installierte `CrusaderDE.dll`
- Größe: 3.450.880 Byte
- SHA-256:
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- PE-Buildzeit: 6. August 2026
- Zugehöriger Script-Extender-Stand: 1.42.0; dessen Changelog ordnet diese
  Generation Spielversion 2.8.0.1 zu.

Die historische Workspace-Kopie `x86_64/CrusaderDE.dll` wurde nicht als
maßgebliche DLL benutzt. Ihr SHA-256 lautet
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.

## Vollständige Funktionsübersicht

| Funktion/Fix des AIFrameworks | Alte Umsetzung | Heutiger Stand | Bewertung |
|---|---|---|---|
| AIC-Konfiguration pro Lord | Native JSON-Importer schreibt die komplette alte AIC-Struktur; `common.aicjson` ergänzt globale Werte | Script Extender bietet AIC-Array, `.baic`-/AIC-Override und Export; `VanillaAICExporter` ergänzt den Workflow | Abgedeckt; alten Loader nicht portieren |
| AIV-Konfiguration | Managed Patch ersetzt die AIV-Listen und fügt für fünf zusätzliche Lords je acht AIV-Dateien ein | Script Extender bietet AIV-Overrides; `ActiveAIVDetector`, `AIVPlacement`, `AIVPlacementLobby` und aktuelle Lord-Unterstützung decken den Bereich ab | Abgedeckt |
| Starttruppen | JSON-Dateien getrennt nach Spielmodus und Mensch/KI | `StartConditions` setzt aktuelle Starttruppen über validierte aktuelle Pfade | Abgedeckt |
| Startgold und Startgüter | Direkte Tabellen-/Nativeingriffe über feste RVAs | `StartConditions` deckt Ressourcen und Güter ab | Abgedeckt |
| Einheitenbalance | Native Handler-Tabellen werden aus JSON überschrieben | Script Extender stellt aktuelle Unit-Interop bereit; Workspace enthält `UnitCosts` und `UnitLimit`, jedoch keinen vollständigen Editor aller alten Kampfwerte | API-seitig weitgehend abgedeckt; ein universeller Balance-Editor wäre ein separates Feature, kein Fix |
| Gebäudebalance | Native Building-Handler, Kosten und Eigenschaften aus JSON | `BuildingCosts`, `BuildingLimit` und `GameBuildingManagerAPI` decken Kosten, Limits und wichtige Defaultdaten ab | Weitgehend abgedeckt |
| Trail-Konfiguration | Direkter Import der alten Trail-Missionstabellen und eigener UI-Pfad | `CustomCustomTrail` bietet das aktuelle, snapshot- und presetfähige System | Abgedeckt |
| Lordstärke/-leben | Direkter Eingriff in Lordtabellen und Anzeige | Aktuelle AIC-/Unit-Interop enthält `lord_power_display_level` und `lord_hps_percent` | API-seitig abgedeckt |
| Eigene Lordvideos | Harmony-Patch auf `SFXManager` plus eigene Dateinamen | Script Extender unterstützt Sound-/Asset-Overrides; aktuelle erweiterte Lords haben einen anderen Datenpfad | Alte Speziallösung nicht portieren |
| Eigene XAML, Bilder, Atlanten und Hintergründe | Harmony-Patches auf Noesis-Ladevorgänge; Export und Dateiersatz | Script Extender besitzt Asset-, XAML-, Sprite- und Noesis-APIs | Vollständig abgedeckt |
| Intro überspringen | Ersetzt Introvideo abhängig von der Vanilla-Konfiguration durch `dummy.webm` | Kein relevanter Fix mehr; die Lösung ist ein alter Asset-Hack | Nicht portieren |
| Spielgeschwindigkeit bis 1000 | Harmony-Patch hebt die 90er Grenze auf 1000 und ändert Schrittweiten | `ExtraFeatures` synchronisiert aktuelle Multiplayer-Geschwindigkeit, hält aber bewusst den aktuellen Bereich 10–90 ein | Nicht abgedecktes Extremfeature, aber ohne Leistungs-/Desync-Nachweis nicht empfehlenswert |
| Beobachtermodus | Falls kein menschlicher Lobbyeintrag dem lokalen Spieler entspricht, wird der lokale Player in einen Beobachterzustand gesetzt | Kein exakter Workspace-Nachbau gefunden | Fragiler alter Lobby-Hack; nur bei konkretem Bedarf neu entwerfen |
| Coop-Konfiguration exportieren/importieren | Schreibt `coop1.json` mit Karte, AIs, Spawns, Teams und AIVs | Aktuelle Lobby- und AIV-Datenmodelle sowie Synchronisation unterscheiden sich stark | Nicht direkt abgedeckt, aber veraltet und kein Bugfix |
| Mehr KI-Slots | Patcht UI-Grenze 19 auf 24 und ergänzt fünf alte Custom-Lords | Das aktuelle Spiel und der Script Extender haben eine erweiterte Lord-/AIC-Struktur | Obsolet |
| Kick-Button in Skirmish | Erzwingt Sichtbarkeit im `PlayerRow` | UI-Detail eines alten Builds | Keine Portierung ohne reproduzierbares aktuelles Problem |
| Coop-Crash mit KI | Ruft im gepatchten `FRONT_Multiplayer`-Konstruktor `InitCoopMissions()` erneut auf | Der Patch hing mit dem vom Framework veränderten alten Lobby-/Lordmodell zusammen; kein entsprechender aktueller Fehler belegt | Framework-spezifisch beziehungsweise obsolet |
| Falsche AIC-Parameter und ganzzahliger Siege-Engine-Wert | Parser-/Schemafixes in Versionen 1.03 bis 1.10 | Diese Fehler lagen im alten Framework-Importer, nicht nachweislich im Spiel | Nicht portieren |
| KI-Panikabriss verhindern | Native Funktion komplett per `RET` deaktiviert | `ExtraFeatures/AIEconomyProtectionHook.cs` verhindert gezielt Notfall- und Hüttenabriss | Bereits besser abgedeckt |
| Lord statt Wirtschaftsgebäude beim Sturm fokussieren | Sechs Bytes am festen `AttackAssignmentFocus`-RVA werden bedingungslos genoppt | Vanilla besitzt heute den globalen Schalter „More Aggressive AI Sieging“ und das entsprechende per-Lord-Bit in `use_improved_sieging` | Durch Vanilla abgedeckt; keinen zusätzlichen Fix portieren |
| Wachsende Angriffsarmeen konfigurieren | Patcht Inkrement und zwei Maximalwert-Immediates | Vanilla-Lorddaten besitzen `siege_max_troops`, `siege_normal_wave_multiplier` und `siege_high_gold_wave_multiplier`; der Script Extender exportiert und importiert alle drei | Vollständig und feiner abgestuft abgedeckt |
| Torhaus-Schließ-/Wiederöffnungslogik anpassen | Patcht zwei Immediates im nativen Gatehouse-Query-Code | Script Extender erkennt die aktuellen Werte; Fixes behebt separat steckenbleibende Farmer | Optionaler Untersuchungskandidat |

## Rekonstruktion der nativen alten Patches

Die Datei `StreamingAssets/AddressTable.json` enthält für Version 1.043 unter
anderem folgende RVAs:

| Name | Altes RVA |
|---|---:|
| `LordFractionTable` | `0x1B248` |
| `DestroyBuildingsByImportance` | `0x2A920` |
| `AttackAssignmentFocus` | `0x34ADA` |
| `AttackArmyMaxSize` | `0x2A6DE` |
| `GatehouseCloseDistance` | `0xAEA40` |
| `TrailMissions` | `0x2B8160` |
| `SkirmishDefaultSpawnGold` | `0x2C43A0` |
| `BuildingHandlers` | `0x2CDBB0` |
| `LordVideoMessages` | `0x3070A0` |
| `UnitHandlers` | `0x310520` |
| `LordManager` | `0x3FFFD10` |

Diese Werte sind ausschließlich für den alten Build dokumentiert. Sie dürfen
nicht gegen die aktuelle DLL verwendet werden.

### 1. KI-Gebäudeabriss

Die native Funktion `AIFramework.dll+0x65C40` berechnet
`CrusaderDEBase + DestroyBuildingsByImportance` und überschreibt das erste Byte
der Zielroutine mit `0xC3` (`RET`). Dadurch wird die komplette Routine sofort
verlassen. Der alte Code reserviert dafür pauschal eine 0x80-Byte-Speicherseite
mit Schreibrechten, prüft aber keine Originalbytes und hält keinen abschaltbaren
Trampolinpfad vor.

Das ist heute nicht mehr sinnvoll. `ExtraFeatures` arbeitet am aktuellen Hash
mit semantischen Signaturen und zwei engeren Eingriffen:

- Am aktuellen Referenz-RVA `0x2F454` wird nur die lokale
  `emergencyDemolitionRequested`-Entscheidung neutralisiert.
- Am aktuellen Referenz-RVA `0x3B1D0` wird nur die KI-Hüttenabrissroutine
  detouriert und bei aktiver Einstellung mit dem normalen „kein Abriss“-Ergebnis
  verlassen.
- Andere Löschpfade, Niederlagenbereinigung und normale Gebäudeaktionen bleiben
  erhalten.

Bewertung: vollständig und sicherer abgedeckt.

### 2. Angriffsziel Lord statt Wirtschaftsgebäude

Das alte Plugin überschreibt am RVA `AttackAssignmentFocus` exakt sechs Bytes
mit `NOP`. Das veröffentlichte Changelog beschreibt die Wirkung als Korrektur
des Verhaltens, bei dem die KI während des Sturms Wirtschaftsgebäude statt des
Lords fokussiert.

Die alte Binärdatei enthält weder die erwarteten Originalbytes noch eine
semantische Validierung. Ohne die zugehörige CrusaderDE-1.043-DLL lässt sich
daher aus dem Plugin allein nicht seriös bestimmen, welche konkrete
sechsbytegroße Instruktion entfernt wurde. Der alte Patch ist immer aktiv und
kennt weder eine globale noch eine per-Lord-Auswahl.

Die erneute Prüfung der aktuellen DLL widerlegt jedoch die frühere Einschätzung,
hier fehle eine Vanilla-Entsprechung. Version 2.8.0.1 besitzt zwei getrennte
globale Werte:

- `global_improved_sieging` bei VA `0x183664FB8`;
- `global_improved_sieging2`, vom Script Extender als
  `GlobalMoreAggressiveAISiegingBehaviourVA` bezeichnet, bei VA
  `0x183664FBC`.

Der Script Extender stellt dafür unter anderem
`IsGlobalMoreAggressiveSiegeBehaviour()` und
`SetGlobalMoreAggressiveSiegeBehaviour(...)` bereit. Zusätzlich enthält jede
aktuelle AIC am Offset `0x2F4` das Feld `use_improved_sieging`.

Die native Bitbelegung ist statisch nachgewiesen:

- Am aktuellen RVA `0x3B89B` wird Bit `1` des Lordfeldes geprüft. Ist es nicht
  gesetzt, wird alternativ der globale Wert `global_improved_sieging` geprüft.
- Am aktuellen RVA `0x3B4F9` wird Bit `2` geprüft. Ist es nicht gesetzt, wird
  zuvor am RVA `0x3B4F1` alternativ der globale Wert „More Aggressive AI
  Sieging“ geprüft.

`use_improved_sieging` ist damit kein einfacher Wahrheitswert, sondern ein
Bitfeld: `0` aktiviert keinen der neuen Pfade, `1` den normalen verbesserten
Pfad, `2` den aggressiveren Pfad und `3` beide. Das gilt unabhängig davon, ob
ein bestimmter Editor die Kombinationen als Checkboxen oder Auswahlwerte
darstellt.

Die Funktion ab aktuellem RVA `0x3B450` belegt auch die Wirkung des zweiten
Bits. Ohne den aggressiveren Schalter wird nur die klassische kleine
Direktzuweisung vorgenommen. Mit globalem Schalter oder Lord-Bit `2` wird aus
einer Siege-/Angriffsgröße größer als zwei `(size + 1) / 2` gebildet. Der
nachfolgende Zuweisungsloop darf dadurch zusätzliche Truppen direkt auf die
Koordinaten des gegnerischen Lords schicken, statt sie erneut über den normalen
Gebäude-/Zielpfad zu verteilen. Das entspricht der vom alten Framework
beschriebenen Korrektur, ist heute aber absichtlich konfigurierbar.

Firefly führte die globale verbesserte Belagerungslogik nach Veröffentlichung
des alten AIFrameworks offiziell ein. Die aktuelle Implementierung geht darüber
hinaus und erlaubt die Auswahl pro Lord. Dass Lords mit deaktiviertem Bit noch
den klassischen Pfad benutzen, ist daher kein fehlender Port des Fixes, sondern
die beabsichtigte Vanilla-Kompatibilitätsoption.

Bewertung: keinen eigenen nativen Fix erstellen. Ein bedingungsloser Port würde
die Vanilla- und Lordauswahl umgehen und lediglich bereits vorhandenes Verhalten
erzwingen. Für einen Lord, der das alte Fehlverhalten vermeiden soll, muss Bit
`2` gesetzt sein; wenn zugleich der normale Improved-Sieging-Pfad gewünscht ist,
ist der kombinierte Wert `3` zu verwenden. Alternativ kann die globale Option
„More Aggressive AI Sieging“ aktiviert werden.

### 3. `AttackArmyIncrement` und `AttackArmyMaxSize`

Das alte `common.aicjson` enthält standardmäßig:

    AttackArmyIncrement = 7
    AttackArmyMaxSize = 200

Das beigelegte Preset „SCDE Vanilla+ 1.43“ verwendet 35 und 650.

Die native Patchroutine liest beide Werte und verändert um den alten
`AttackArmyMaxSize`-Anker mehrere Stellen:

- bei `anchor - 0x1B`: zwei Bytes werden genoppt;
- bei `anchor - 0x14`: das Inkrement wird als ein Byte geschrieben und auf 255
  begrenzt;
- bei `anchor + 0x02`: der Maximalwert wird als 32-Bit-Wert geschrieben;
- bei `anchor + 0x10`: derselbe Maximalwert wird ein zweites Mal geschrieben.

Die zwei NOPs sind vermutlich Teil des im Changelog genannten Fixes für eine
fehlerhafte Ganzzahlbehandlung. Das lässt sich ohne Originalinstruktionen nicht
abschließend beweisen.

In aktuellen Spiel- und Lorddaten wurde dieses alte globale Wertepaar durch
drei per-Lord-Parameter ersetzt beziehungsweise verfeinert:

- `siege_max_troops` am AIC-Offset `0x454` entspricht funktional dem alten
  `AttackArmyMaxSize`;
- `siege_normal_wave_multiplier` am Offset `0x458` steuert das Wachstum bei
  normalem Goldstand;
- `siege_high_gold_wave_multiplier` am Offset `0x45C` steuert es bei hohem
  Goldstand.

Die beiden Multiplikatoren übernehmen damit die Rolle des alten
`AttackArmyIncrement`, unterscheiden aber zusätzlich nach der wirtschaftlichen
Lage des Lords. Der aktuelle Script Extender führt alle drei Felder sowohl in
`InternalAIC` als auch in `PublicAIC` und übernimmt sie in beide
Konvertierungsrichtungen. Sie können folglich bereits über `.lordjson` gesetzt
und mit den vorhandenen AIC-Werkzeugen exportiert/importiert werden.

Aktuelle lokale Lorddaten bestätigen die praktische Verwendung, beispielsweise
mit `siege_max_troops = 300`, `siege_normal_wave_multiplier = 4` und
`siege_high_gold_wave_multiplier = 12`. Firefly nennt im offiziellen Update zum
CPU-Lord-Editor entsprechend „Max Siege Size“ und „Siege Wave Size Multiplier“.

Bewertung: Ja, die alten Optionen sind durch Vanilla-`.lordjson` abgedeckt. Ein
Port der globalen Immediate-Patches wäre funktional gröber, versionsabhängig und
würde die heutige per-Lord-Konfiguration überschreiben. Kein zusätzlicher Mod
ist sinnvoll.

### 4. Torhaus-Automatik

Das alte Plugin schreibt relativ zu `GatehouseCloseDistance`:

- bei `anchor + 3` den 32-Bit-Wert 180;
- bei `anchor + 10` den 32-Bit-Wert 100.

Das Changelog nennt dies „adjusted duration of closed gatehouse“. Der Name in
der Adresstabelle ist ungenau, denn die Zielroutine enthält sowohl
Distanzvergleiche als auch einen Ergebnis-/Zeitwert.

Im letzten aktuellen Spielstart fand der Script Extender für die kanonische DLL:

- `gGateHouseCloseDistance = 200`;
- `gGateHouseReOpenDistance = 1200`;
- `gGateHouseUnkDistance = 1200`.

Die statische Prüfung des aktuellen Codes bestätigt am RVA `0xB7BC0` den
Vergleich mit 200, unmittelbar danach einen Pfad mit 1200, einen weiteren
Vergleich mit 140 und einen alternativen Ergebniswert 100. Die Logik ist also
noch vorhanden, aber Layout und Offsets entsprechen nicht dem alten Patch.

Der installierte Fixes-Mod hookt im selben größeren Gatehouse-Query-Ablauf einen
späteren Punkt, um steckenbleibende Farmer zu behandeln. Das ist keine
Entsprechung der alten 180/100-Anpassung.

Möglicher moderner Nutzen: eine optionale Einstellung für schnelleres
Wiederöffnen beziehungsweise eine andere Sicherheitsdistanz. Vorher muss mit
Tor offen/geschlossen, freundlichen Einheiten, Gegnern innen/außen und dem
Farmer-Stuck-Fix gemessen werden, welcher der aktuellen Werte tatsächlich Zeit
und welcher Entfernung repräsentiert. Änderungen müssen synchronisiert sein.

Bewertung: niedrige Priorität; eher Gameplay-/QoL-Tuning als Fehlerbehebung.

## Nicht zu portierende alte Implementationsmuster

- Keine absoluten RVAs aus `AddressTable.json` wiederverwenden.
- Keine Funktion durch ein einzelnes `RET` abschalten, wenn nur ein Zweig
  unerwünscht ist.
- Keine NOPs ohne Prüfung der Originalinstruktionen, der vollständigen
  Detourspanne und aller eingehenden Sprungziele schreiben.
- Keine vom alten Plugin verwendeten pauschalen 0x80-Byte-Schreibbereiche als
  Sicherheitsnachweis betrachten.
- Keine parallelen AIC-, AIV-, Trail-, Preset-, Netzwerk- oder Asset-Systeme
  neben den heutigen Shared-/Script-Extender-Pfaden anlegen.
- Runtime-JSON ausschließlich über `Shared/DependencyFreeJson.cs` verarbeiten.
- Kein `System.Text.Json` oder `YamlDotNet` in einen Unity-/BepInEx-Runtime-Mod
  übernehmen.

## Empfohlene Reihenfolge für mögliche Folgearbeiten

1. Torhauswerte nur dann weiter untersuchen, wenn das aktuelle Verhalten konkret
   als zu langsam oder fehlerhaft beobachtet wird.

Es wurde bewusst kein Codepatch erstellt und kein Mod gebaut: Der Auftrag war
eine Prüfung und Dokumentation. Der Angriffszielpfad und die Angriffswellenwerte
benötigen keinen zusätzlichen Fix; für den verbleibenden Torhaus-Kandidaten fehlt
noch der nach den Workspace-Regeln erforderliche aktuelle Laufzeitnachweis.
