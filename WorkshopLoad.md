# Workshop-Mods direkt aus dem Steam-Ordner laden (Plan)

Stand: Entwurf vom 2026-11-18, noch nichts umgesetzt. Aussagen über fremden Code
(BepInEx, Script Extender) sind Momentaufnahmen des lokalen Klons bzw. der installierten
Version und vor der Umsetzung erneut zu prüfen. Unbestätigte Annahmen sind als solche
markiert und haben einen Prüfschritt in Phase 0.

## Ziel

Abonnierte APIShared-Workshop-Pakete werden nicht mehr in `BepInEx/plugins` kopiert,
sondern direkt aus `<Steam-Bibliothek>/steamapps/workshop/content/3024040/<ID>/Payload/BepInEx/plugins/...`
geladen. Damit entfallen Installation, Transaktionen, Updater, Neustart und die
Fehlerklasse „unterbrochene Installation“. Steam übernimmt Aktualisieren und Entfernen.

Feste Anforderungen:

- **Lokale Installation gewinnt.** Liegt ein Mod eines Pakets bereits im `plugins`-Ordner
  des Spiels (z. B. eine ältere oder eigene Version), wird diese geladen und das
  Workshop-Paket übersprungen.
- Einstellungen und andere Nutzerdaten liegen nicht im Mod-Ordner, sondern unter
  Dokumente (wie bereits `PersonalSettingsPaths`/`PersonalDataFile`).
- Fällt das Laden aus dem Workshop aus, bleibt die manuelle Notlösung: `Payload/BepInEx`
  in den Spielordner kopieren; dann gewinnt diese lokale Kopie.

## Ausgangslage (geprüft am 2026-11-18)

- **BepInEx 5.4.23.5** (`BepInEx/core`): Der Chainloader sucht Plugins nur unter
  `Paths.PluginPath` (rekursiv). Weitere Ordner sind nicht vorgesehen. Preloader-Patcher
  in `BepInEx/patchers` laufen vorher und können den Chainloader per Harmony erweitern
  (HarmonyX liegt in `BepInEx/core`). Das BepInEx-Projekt „BepInEx.MultiFolderLoader“
  macht genau das für zusätzliche Mod-Ordner (aus Erfahrung, hier nicht geprüft) und taugt
  als Referenz.
- **APIShared hat bereits einen Preloader-Patcher**: `BepInEx/patchers/APIShared.DependencyBootstrap.dll`
  (paargebunden an `APIShared.dll`, siehe `APIShared/docs/DEPENDENCY_BOOTSTRAP.md`). Er
  korrigiert schon heute früh die Abhängigkeitsprüfung des Script Extenders für
  verschachtelte Plugins. Der Loader kann dort angesiedelt werden; Spieler müssen nichts
  zusätzlich installieren, weil APIShared selbst weiterhin als Extender-Map kommt.
- **Script Extender** (lokaler Klon `shcde-script-extender`):
  - `GameAssetModManager.RegisterAll()` registriert Asset-Mods (`info.json`, Assets,
    Lokalisierung, Lua) nur aus den direkten Unterordnern von `BepInEx/plugins`. Es läuft
    im `Awake` des Extenders (`Bootstrap/Plugin.cs`), vor `GameXAMLManagerAPI.Setup()`
    und vor allen anderen Plugins.
  - Es gibt eine öffentliche Einzelregistrierung (`Register(directory)`).
  - Die Startup-Kompatibilitätsprüfung (Abhängigkeiten aus `info.json`) sieht ebenfalls nur
    `BepInEx/plugins`.
  - Der Multiplayer-Abgleich (`GameNetworkAPI.ComputeActiveModHash`, Lobby-Metadaten)
    nutzt `Chainloader.PluginInfos` und die registrierten Asset-Mods. Aus dem Workshop
    geladene und registrierte Mods wären also automatisch enthalten.
- **Workshop-Ordner** `E:\ProgrammeE\Steam\steamapps\workshop\content\3024040` enthält
  sehr unterschiedliche Items (KI-Lords, Karten, Trails, Extender-`.map`-Mods).
  Unser Paket 3788821961 hat `apishared-workshop.json`, `Payload/` und `SerpsMods.map`
  (Bootstrap). Erkennen dürfen wir nur unser eigenes Kennzeichen (das Manifest).
- **Einstellungen**: Viele Daten liegen bereits unter Dokumente:
  - APIShared-Presets: `PersonalSettingsPaths`, `Dokumente/SHCDE/APIShared/<GUID>`
  - BugfixesAndQoL-Stores und CastlePlanner-Speicher: `Shared.PersonalDataFile`
  - ExtraFeatures (Feindnähe) über eine eigene Migration.

  Daneben gibt es Code, der relativ zum Plugin-Ordner liest (Locales, Assets, AIV,
  native DLLs). Lesen ist unkritisch. Ob irgendwo noch in den Plugin-Ordner geschrieben
  wird, ist nicht vollständig geprüft (Phase 1).

## Probleme und Lösungen

| # | Problem | Lösung |
|---|---|---|
| P1 | BepInEx lädt nur `BepInEx/plugins`. | Loader im APIShared-Preloader: Harmony-Hook auf die Plugin-Suche des Chainloaders (BepInEx 5: `TypeLoader.FindPluginTypes` bzw. `Chainloader.Start`), der die ausgewählten Workshop-Wurzeln ergänzt. Gebunden an die geprüfte BepInEx-Version; bei unbekannter Version kein Workshop-Laden (Fehler im Log, lokale Mods unberührt). |
| P2 | Abhängige DLLs eines Workshop-Mods (eigene Hilfs-DLLs) werden von BepInEx nur unter `plugins` aufgelöst. | Eigener `AppDomain.AssemblyResolve`-Handler, der nur in den ausgewählten Workshop-Wurzeln sucht, und zwar erst, nachdem die normale Auflösung gescheitert ist. Native DLLs laden die Mods selbst per Pfad (z. B. Pathfinding), das funktioniert unverändert. |
| P3 | Lokale Version soll gewinnen. | Vor dem Laden die GUIDs aller lokal vorhandenen Plugins ermitteln (`info.json` und `BepInPlugin`-GUIDs aus der BepInEx-Typsuche). Regel auf **Paketebene**: Kommt irgendeine GUID des Pakets (`ModGuids` im Manifest) lokal vor, wird das ganze Workshop-Paket übersprungen. So entsteht nie ein Mischstand innerhalb eines Pakets (z. B. SerpsMods-Host aus dem Workshop, ein Kind-Mod lokal). Der Bootstrap-Ordner des Pakets (reservierte eigene GUID) zählt nicht als lokale Installation. |
| P4 | Bisher von APIShared **kopierte** Installationen liegen ebenfalls lokal und würden nach P3 ewig gewinnen. | Unterscheiden: Eine Kopie mit gültiger APIShared-Quittung (Receipt), deren Dateien zu einer bekannten Version passen, ist „verwaltet“. Alles ohne Quittung oder verändert ist „manuell/Entwicklung“ und bleibt maßgeblich. Verwaltete Kopien werden einmalig nach Zustimmung entfernt (nur quittierte, hashgleiche Dateien; Einstellungen liegen dann schon unter Dokumente). Ab dem nächsten Start lädt der Loader die Workshop-Fassung. Siehe Entscheidung E3. |
| P5 | Steam könnte bei Updates zusätzliche Dateien im Item-Ordner löschen oder überschreiben. Nicht geprüft. | Kein Schreibzugriff auf Workshop-Ordner, weder durch APIShared noch durch Mods (Phase 1 und Vertrag in P11). Dann ist das Verhalten von Steam egal. |
| P6 | Halb heruntergeladene oder gerade aktualisierte Items. | Integritätsprüfung vor dem Laden: Alle Dateien im `Payload` müssen exakt zum Inventar in `apishared-workshop.json` passen (Pfad, Größe, SHA-256), ohne zusätzliche DLLs. Ein Cache unter `BepInEx/cache` (Pfad, Größe, Änderungszeit, Hash) vermeidet erneutes Hashen unveränderter Dateien. Passt etwas nicht, wird das Paket nicht geladen, und es gibt einen Hinweis (siehe P10). |
| P7 | Verwaiste Ordner deabonnierter Items. Ob und wann Steam sie entfernt, ist nicht geprüft. | Zusätzlich `steamapps/workshop/appworkshop_3024040.acf` lesen (VDF-Text). Nur Items, die dort als installiert und abonniert geführt werden und kein ausstehendes Update haben, gelten als Kandidaten. Welche Felder das zuverlässig zeigen, klärt Phase 0 (T3). Ist die Datei nicht lesbar, ist die Integritätsprüfung allein maßgeblich, mit Warnung. |
| P8 | Laufendes Spiel hält DLLs gesperrt; ein Steam-Update währenddessen könnte scheitern oder später nachgeholt werden. Nicht geprüft. | Das Verhalten wird in Phase 0 (T2) beobachtet. Ein halb aktualisierter Stand wird durch P6 beim nächsten Start erkannt und nicht geladen. Hinweis an den Spieler: Steam fertig aktualisieren lassen und neu starten. |
| P9 | Der Script Extender registriert Assets und prüft Abhängigkeiten nur unter `BepInEx/plugins`. | Bevorzugt: Feature-Anfrage an den Script Extender „zusätzliche Mod-Wurzeln“, z. B. eine registrierbare Liste, die `RegisterAll` und die Kompatibilitätsprüfung berücksichtigen. Wir ändern den Fork nicht. Übergangsweise: (a) APIShared ruft im eigenen `Awake` `Register(directory)` auf, sofern T4 zeigt, dass späte Registrierung für XAML-Patches, Locales, Lua und Overrides vollständig wirkt; oder (b) der vorhandene frühe Eingriff des DependencyBootstraps wird auf Workshop-Wurzeln erweitert (Präzedenzfall, aber an Extender-Versionen gebunden und daher fragil). |
| P10 | Der Preloader läuft vor Unity und Steam: keine Dialoge, keine Lokalisierung. | Der Preloader entscheidet nur und schreibt ein Protokoll mit Zeitstempel. Die Entscheidungen (geladen, übersprungen wegen lokaler Version, defekt, deaktiviert) legt er in einer Datei unter `BepInEx/cache` ab. APIShared zeigt sie zur Laufzeit an: in den Mod-Optionen „Geladen aus: Workshop / Spielordner“ und beim Start einen Hinweis für defekte oder übersprungene Pakete. |
| P11 | Fremde APIShared-Pakete könnten in ihren Ordner schreiben oder Pfade fest auf `BepInEx/plugins` annehmen. | **Opt-in pro Paket** im Manifest (z. B. `"LoadInPlace": true`). Nur damit gekennzeichnete Pakete werden direkt geladen; alle anderen laufen weiter über die bisherige Installation. Der Publisher fragt das Kennzeichen ab und dokumentiert den Vertrag: keine Schreibzugriffe im Mod-Ordner, Pfade nur relativ zu `Info.Location`. |
| P12 | Spieler wollen ein Workshop-Paket vorübergehend abschalten, ohne es zu deabonnieren. | Deaktivierungsliste in der APIShared-Konfiguration (`BepInEx/config`, für den Preloader als einfacher Text lesbar). Schalter in den Mod-Optionen, wirksam ab dem nächsten Start. |
| P13 | Linux/Proton. | Pfade relativ zum Spielordner ableiten (`<Spiel>/../../workshop/content/3024040`). Das bleibt innerhalb von Wine gültig. Die `.acf` liegt im selben Bibliotheksordner. Kein Host-Prozess nötig, das ist einfacher als der heutige Updater. Live-Test unter Proton erforderlich. |
| P14 | Multiplayer-Gleichstand: Steam liefert immer nur die neueste Version, kein Zurück. | Unverändert zu heute. Der Lobby-Abgleich des Extenders erkennt Versionsunterschiede (P9 vorausgesetzt). Wer eine bestimmte Version braucht, installiert sie lokal (P3). |
| P15 | Startzeit: Hashen eines großen Pakets bei jedem Start. | Hash-Cache (P6) und Messung in Phase 0 (T5). Nach den Vorgaben ist eine etwas längere Startzeit akzeptabel; Lags im Spiel entstehen dadurch nicht. |
| P16 | Doppelte GUIDs, z. B. lokale Kopie eines einzelnen Kind-Mods plus Workshop-Paket. | Durch P3 ausgeschlossen. SerpsModsHost meldet echte Duplikate weiterhin (`AuditDuplicateInstallations`). Die Prüfung muss die Paketwurzel im Workshop kennen (`packRoot` aus `Info.Location`, das passt bereits). |
| P17 | Das bisherige Installationssystem (Receipts, Transaktionen, Updater). | Bleibt für Pakete ohne Opt-in und für die Migration (P4) bestehen. Für Opt-in-Pakete gibt es keine Startup-Installationsangebote mehr. Ob es später ganz entfällt, wird nach Verbreitung entschieden (eigener Schritt mit Kompatibilitätsprüfung, APIShared-Vertrag). |

## Entscheidungen für den Nutzer

- **E1 Regel-Granularität**: Paketebene (empfohlen, siehe P3) oder pro Mod-GUID.
- **E2 Geltungsbereich**: Opt-in pro Paket (empfohlen, P11) oder alle APIShared-Pakete.
- **E3 Umgang mit alten verwalteten Kopien**: einmalig mit Zustimmung entfernen
  (empfohlen) oder liegen lassen. Liegen lassen hieße: Die alte Kopie gewinnt dauerhaft,
  bis der Spieler sie selbst löscht.
- **E4 Extender-Anbindung**: Feature-Anfrage an den Script Extender (empfohlen) mit
  Übergangslösung je nach Ergebnis von T4.

## Phasen

### Phase 0: Machbarkeit belegen (Prototypen, kein Produktivcode)

Alle Prototypen als eigener Testmod bzw. eigener Test-Patcher unter `Testmods/`. Weder
APIShared noch die Mods werden dafür geändert. Spieltests nach den Vorgaben: ruhige
Testwelt, schnelle Spielgeschwindigkeit, Spielzeitfenster mit anderen Sitzungen
abstimmen.

- **T1 BepInEx**:
  - Ein Test-Patcher lädt ein Test-Plugin aus einem Ordner außerhalb von `plugins`.
  - Zu prüfen: `Info.Location`, Abhängigkeitsauflösung, BepInEx-Typ-Cache,
    Ladereihenfolge mit `BepInDependency` auf Plugins in `plugins`.
  - Zu prüfen: Verhalten bei gleicher GUID in beiden Orten.
- **T2 Steam**, mit einem privaten Test-Item:
  - Löscht oder überschreibt Steam beim Update fremde Dateien im Item-Ordner?
  - Was passiert bei einem Update, während das Spiel läuft und DLLs gesperrt sind?
  - Wann verschwindet der Ordner nach dem Deabonnieren?
- **T3 `.acf`**: Welche Felder zeigen „abonniert“, „installiert“ und „Update ausstehend“
  zuverlässig an? Zustände vorher und nachher protokollieren.
- **T4 Script Extender**: Wirkt `GameAssetModManager.Register(dir)` aus dem `Awake` eines
  späteren Plugins vollständig? Zu prüfen: XAML-Patches, Locales, Lua, Asset-Overrides,
  Lobby-Mod-Hash, Mod-Liste. Zusätzlich: Wie reagiert die Kompatibilitätsprüfung auf
  Abhängigkeiten, die nur im Workshop liegen?
- **T5 Startzeit**: Hashen des SerpsMods-Pakets kalt und warm, mit und ohne Cache.

Ergebnis: kurzer Bericht mit Belegen. Danach Entscheidung, ob und mit welcher Variante
von P9 es weitergeht.

### Phase 1: Schreibzugriffe aus Mod-Ordnern entfernen

- Vollständiger Audit aller eigenen Mods und von APIShared auf Schreibzugriffe relativ
  zu `Info.Location`, `Assembly.Location` und `Paths.PluginPath`. Dazu gehören auch
  Logs, Caches, entpackte Dateien (z. B. CastlePlanner `VanillaAIV` prüfen),
  DevDiag-Ausgaben und alte `LobbyModSettings`-Pfade.
- Verbleibende Fälle auf `PersonalDataFile` bzw. `PersonalSettingsPaths` umstellen, mit
  Migration vom alten Ort. Die vorhandenen Migrationen in Pathfinding, Spectator und
  ExtraFeatures dienen als Vorlage.
- Dauerhafte Quelltextprüfung (z. B. in `Shared/Tools/Validation/Test-SharedBoundaries.ps1`):
  kein Schreiben unterhalb des Plugin-Ordners. Ausnahmen nur mit Begründung.
- Diese Phase lohnt sich auch ohne den Rest: Sie verhindert Datenverlust bei jeder Neuinstallation.

### Phase 2: Paketformat und Publisher

- Optionales Manifest-Feld für das Opt-in (E2) inklusive Validierung und Tests.
  Ältere APIShared-Versionen ignorieren das Feld; dafür die Kompatibilität des
  Manifest-Parsers prüfen.
- Publisher: Abfrage des Kennzeichens, Hinweis auf den Vertrag aus P11, Prüfung, dass das
  Inventar alle Payload-Dateien vollständig abdeckt (Grundlage für P6).

### Phase 3: Loader im APIShared-Preloader

- Ablauf beim Start:
  1. Workshop-Pfad bestimmen (P13).
  2. Kandidaten über das Manifest und die `.acf` ermitteln (P7).
  3. Deaktivierte Pakete aussortieren (P12).
  4. Lokale GUIDs ermitteln und die Regel aus P3 anwenden.
  5. Integrität prüfen, mit Cache (P6).
  6. Chainloader-Hook und Assembly-Auflösung setzen (P1, P2).
  7. Entscheidungsdatei für die Laufzeit schreiben (P10).
- Fehlertoleranz: Jeder Fehler betrifft nur das jeweilige Paket bzw. schaltet nur das
  Workshop-Laden ab. Lokale Plugins werden nie beeinträchtigt. Fehler werden als Error
  geloggt, mit Zeitstempel und Millisekunden.
- Bindung an die geprüfte BepInEx-Version bzw. den Methodenvertrag. Kein stilles
  Weiterlaufen bei unbekannter Version.
- Zusammenspiel mit der bestehenden Abhängigkeitskorrektur des DependencyBootstraps
  (gleiches Paar-Manifest, gemeinsame Tests).

### Phase 4: Script-Extender-Anbindung

Je nach T4 und der Antwort auf die Feature-Anfrage (E4):
- Asset-Registrierung der Workshop-Wurzeln.
- Kompatibilitätsprüfung für Abhängigkeiten aus dem Workshop.
- Prüfung des Lobby-Mod-Hashs.

Kompatibilität mit dem Fixes-Mod prüfen. Laut den Vorgaben ist das bei jeder Änderung
Pflicht.

### Phase 5: Laufzeit und Migration

- Anzeige der Ladequelle und der Hinweise in den Mod-Optionen; Schalter für die
  Deaktivierung.
- Erkennung verwalteter alter Kopien und deren Entfernung nach Zustimmung (P4/E3). Das
  läuft über den vorhandenen Updater, nur hashgeprüfte, quittierte Dateien, Einstellungen
  unberührt. Danach Neustart.
- Für Opt-in-Pakete entfallen Startup-Installationsangebote und die Vormerkung
  „Beim nächsten Start installieren“.

### Phase 6: Tests, Doku, Release

- Core-Tests für die Entscheidungslogik:
  - lokal gegen Workshop, Paketregel
  - `.acf`-Zustände
  - Integrität
  - Deaktivierung
- Fixture-Test mit künstlichem Workshop-Baum und Preloader-Lauf.
- Spieltest als Testmod-Szenario (APIShared-Test-API):
  - Laden aus dem Workshop
  - lokale Version gewinnt
  - defektes Paket wird übersprungen
  - Multiplayer-Hash
  - Karteneditor
- Live-Tests unter Windows und Proton.
- `docs/WORKSHOP.md`, `DEPENDENCY_BOOTSTRAP.md`, `ARCHITECTURE.md` aktualisieren. Die
  READMEs nur auf Nachfrage ändern; dort aus Spielersicht beschreiben, welche Mods von wo
  geladen werden und wie man eine bestimmte Version lokal festhält.

## Risiken

- **BepInEx-Interna:** Die Hook-Stelle ist intern. Ein BepInEx-Update kann sie
  verschieben. Gegenmaßnahme: Versionsbindung und Abschalten bei Abweichung (P1).
- **Extender-Abhängigkeit:** Ohne eine passende Extender-Schnittstelle (P9) bleibt nur
  ein früher Eingriff in Extender-Code, der an Versionen gebunden und wartungsintensiv ist.
- **Steam-Verhalten:** Das Verhalten von Steam ist nicht vertraglich festgelegt (T2/T3).
  Die Integritätsprüfung (P6) begrenzt das Risiko auf „Paket wird nicht geladen“ statt
  „defekter Stand wird geladen“.
- **Übergangszeit:** Zwei Wege (kopiert und direkt geladen) erhöhen den Testaufwand, bis
  P17 entschieden ist.
