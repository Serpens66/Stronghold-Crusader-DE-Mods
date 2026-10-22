# AI Toolkit: DE-Formatwissen und Quellen für die Autoren

Stand: **22.10.2026**. Zweck: technische Übergabe für die Autoren von
[AI Toolkit](https://github.com/Schlossgespensty/AI-Toolkit). Dieses Dokument
beschreibt Dateiverträge, nachgewiesene Unterschiede und Prüfmaterial. Es
enthält keine Implementierung und setzt keinen bestimmten Architekturweg voraus.

**Orientierung:** Für eigenständige LORDJSON-Bearbeitung sind §4 bis §6 und
Anhang A zentral; für den vorhandenen Burgsupport §7. §8 bis §10 behandeln
Vorschaugrenzen und optionale Spiel-/Modintegration. §11 sammelt konkrete
Prüffälle. Die Tabellen sind für eine Weitergabe ohne Zugriff auf den lokalen
Workspace ausgeführt; lokale Beleglinks ergänzen die öffentlichen Quellen.

## 1. Leseschlüssel: Wie sicher ist eine Aussage?

| Kennzeichen | Sicherheit und Beleg | Was damit nicht behauptet wird |
| --- | --- | --- |
| **H · Assembly** | Hoch: direkt in der installierten Spiel-/Editor-/Extender-Assembly geprüft; gegebenenfalls mit Quellcode abgeglichen | Vollständige Spielwirkung eines Feldes |
| **H · Native** | Hoch für den genannten Vertrag und Native-Hash: vorhandener featurebezogener Baseline-Audit, abgeglichener Datenfluss | Gültigkeit für andere Spielversionen oder ununtersuchte Konstruktoren |
| **H · Test** | Hoch für den konkret beschriebenen Offline-Versuch | GUI-, Spiel- oder Multiplayerabnahme |
| **H · Quelle** | Hoch: das genannte Verhalten steht eindeutig im identifizierten Quellstand | Dass genau dieser Modstand installiert/aktiv ist |
| **M · Zuordnung** | Mittel: starke fachliche Entsprechung, noch kein vollständiger Konvertierungsnachweis | Fertige, verlustfreie HD↔DE-Konvertierung |
| **O · Offen** | Nicht bestätigt; zusätzliche Untersuchung oder echter Spiel-/Editortest erforderlich | Eine zulässige Annahme für stillschweigendes Umschreiben |
| **E · Empfehlung** | Vorgeschlagene Produkt-/Testentscheidung | Bestehender Spielvertrag |

Die Kennzeichen gelten jeweils für die konkrete Aussage, nicht pauschal für
eine ganze Datei. **Strukturtreue, Bedeutung und erfolgreiche Spielausführung
sind drei getrennte Nachweise.** Ein unbekannter Zahlenwert ist nicht schon
deshalb ungültig, weil der Editor keinen Namen dafür kennt.

## 2. Prüfstand und Quellenidentitäten

**H · Assembly/Quelle.** Am Berichtsdatum erneut ermittelt:

| Quelle | Identität |
| --- | --- |
| AI Toolkit, sauberer lokaler `main` | `0a12082582b63a7d531ae4683e7689382773f892`, Paketversion 0.11.0 |
| Kanonische installierte `CrusaderDE.dll` | SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` |
| Installierte Spiel-`Assembly-CSharp.dll` | SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789` |
| Offizieller Castle-&-CPU-Lord-Editor, `Assembly-CSharp.dll` | SHA-256 `3735EF057CAC15C7173B3CECB12801E2B76A9A913F87DC4131419B7AD5BF1B64` |
| Lokaler Script-Extender-Quellstand | `4014ca67c96974a5e6d697665baf5351cbfebde6` / v2.14.3 |
| Installierte `SHCDESE.dll` | Assemblyversion 2.14.3.0, SHA-256 `5CB1C33BE5463086505A17F5C48AAA357D733CFFF7240E101C821F0E3C668498` |
| Lokaler Fixes-Quellstand, sauber | `a90f96c176e9c8981af83534f5a8bb444838ae0a` |

**H · Quelle.** Die generierte Native-Datenbank hat noch Extender-Provenienz
2.14.2 (`10628df85cd432184742a50a1dee3969a66cf89e`). Die spätere
[2.14.3-Ergänzung](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SE_2_14_3_INTEROP_ADDITIONS.md)
ist separat dokumentiert. Frühere dort genannte Installationsstände sind
historisch; die obige Tabelle beschreibt die neue Prüfung. Native- und
Managed-Spielhash stimmen mit `CURRENT.json` und dem Datenbankmanifest überein.

### Quellenverzeichnis

Die Kürzel werden im weiteren Text verwendet. Lokale Verweise sind aus dem
Mod-Workspace heraus lesbar; öffentliche Extender-Verweise sind auf den
untersuchten Commit festgelegt.

| Kürzel | Nachprüfbare Quelle |
| --- | --- |
| **T1** | [Toolkit-DE-Dokumentation](https://github.com/Schlossgespensty/AI-Toolkit/blob/0a12082582b63a7d531ae4683e7689382773f892/docs/definitive-edition-integration.md), [gemergter DE-PR #5](https://github.com/Schlossgespensty/AI-Toolkit/pull/5) |
| **T2** | [Vorherige lokale Toolkit-Analyse](../_inspect/AI-Toolkit-DE/Analyse.md), [reproduzierbarer Dateiprobe-Code](../_inspect/AI-Toolkit-DE/inspect.cjs), [Ergebnisse](../_inspect/AI-Toolkit-DE/result.json) |
| **G1** | [Hashgebundener Spielcode: CustomisationFileManager](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/CustomisationFileManager.cs): `LordRoot`, `NewAIC`, `BuildFileLists`, `BuildCustomLordDirectory`, `BuildExtendedLordDirectory`, `ProcessExtendedLordFile` |
| **G2** | [Spielcode: AIVLoader](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/AIVLoader.cs): `SaveData.GetRawData` |
| **G3** | [Spielcode: EngineInterface](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/EngineInterface.cs): `CreateAILordConfigData`, `EncodeLordConfig`, `DecodeLordConfig` |
| **G4** | [Spielcode: ConfigSettings](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/managed/BC8B6A39/decompiled/ConfigSettings.cs): Pfade und `extendedLordPaths` |
| **E1** | [Offizieller Editor: JsonReadWrite](../_inspect/AI-Toolkit-DE/EditorJsonReadWrite.cs), [Lord-ViewModel](../_inspect/AI-Toolkit-DE/EditorLordViewModel.cs), [Frame](../_inspect/AI-Toolkit-DE/EditorFrame.cs), [FrameManager](../_inspect/AI-Toolkit-DE/EditorFrameManager.cs); aus der oben gehashten Editor-Assembly dekompiliert |
| **S1** | [Extender-AIC-Konverter](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.AICDecoder): `PublicAIC.cs`, `InternalAIC.cs`, `AICReader.cs`, `AICWriter.cs` |
| **S2** | [Extender-AIV-Konverter](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.AIVDecoder): `Models/SaveData.cs`, `AIVDecoder.cs`, `AIVEncoder.cs` |
| **S3** | [Extender-Enums](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/Enums.cs), [eStructs](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/src/SHCDESE.BepInEx/Interop/EStructs.cs), lokale zusätzliche [Interop-Enums](../shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums) |
| **S4** | [Extender Asset API](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/4014ca67c96974a5e6d697665baf5351cbfebde6/docs/guides/asset-api.md), [AIVLoader-Hook](../shcde-script-extender/src/SHCDESE.BepInEx/ManagedHooks/AIVLoader_Hooks.cs) |
| **S5** | [Extender GameAIManagerAPI](../shcde-script-extender/src/SHCDESE.BepInEx/API/GameAIManagerAPI.cs), [LordInfo](../shcde-script-extender/src/SHCDESE.BepInEx/API/Components/AI/LordInfo.cs) |
| **N0** | [Baseline-Einstieg](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md), [Identität](../_inspect/CrusaderDE-Native-Baseline/CURRENT.json), [Datenbankmanifest](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/DATABASE_INFO.json) |
| **N1** | [AIV-Import, Zelllöschung und Baureichweite](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_BUILD_RANGE.md) |
| **N2** | [AIV-Auswahl, Fit, Rotation und Markeradressierung](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md) |
| **N3** | [Lord-Identität und Trail-AIC-Export](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/TRAIL_CUSTOM_AIC_EXPORT.md) |
| **N4** | [Negative Verkaufsschwellen](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AI_SELL_THRESHOLDS.md) |
| **N5** | [Fixes-Verträge mit historischen Nachweisen](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIXES_COMPATIBILITY.md) |
| **W1** | [Workspace-AIV-Parser](../Helpers/AIVParser/AIVParser.Core), insbesondere `AivRawDataDecoder.cs`, `AivBlueprintParser.cs`, `AivCatalogs.cs`, `AivGridTransform.cs`, `AivWorldTransform.cs` |
| **W2** | [Workspace-Encoder](../CastlePlanner/src/AivRawDataEncoder.cs), [JSON-Reader](../CastlePlanner/src/AivJsonReader.cs), [Welt-/Keep-Abgleich](../CastlePlanner/src/AivWorldCoordinateTransform.cs) |
| **W3** | [Aktuelle Grenzen der Lobby-Prognose](AIVPlacement/STATUS.md), [Messreihen und Evidenzarchiv](AIVPlacement/EXPERIMENTS.md) |
| **F1** | Fixes-Quellstand aus Tabelle oben: `src/shcde-fixes/Config/CustomLordPreferencesEntry.cs`, `Preferences.cs`, `Events/FixesAIEvents.cs`, `Events/FixesMapEvents.cs`, `Detours/ExpandedGuardingSpotsDetours.cs`; [öffentliches Projekt](https://gitlab.com/rawra-stronghold-crusader/shcde-fixes) |

## 3. Welche DE-Unterstützung bereits im Toolkit steckt

**H · Quelle/Test (T1/T2).** `.aivjson` und der Toolkit-Alias `.aijson`
können bereits geöffnet und als JSON gespeichert werden. Der Editor kennt
`state.format = aiv/aivjson`, getrennte Normalisierung und einen DE-Export.
Er hält im geprüften JSON-Pfad Pausen, leere Frames, unbekannte Felder/IDs und
Markernummern fest. Es ist keine zweite Burgengine nötig.

**H · Quelle.** Besonders relevante Toolkit-Dateien:

| Datei/Gruppe | Zuständigkeit |
| --- | --- |
| `src/node/aiv-codec.mjs` | Klassischer binärer AIV-Codec mit Sektionsverzeichnis und PKWARE-Kompression; nicht DE-BAIV |
| `src/node/aiv-file.js` | Klassischer Export, Byteerhalt unveränderter Originale und Konvertierungsdiagnostik |
| `src/js/castle-format.js` | DE-JSON-Validierung/Serialisierung, `classicIssues` |
| `src/js/castle-editor.js` | Formatstatus, Normalisierung, Bearbeitung und Speichern |
| `src/desktop/documents.ts`, `src-tauri/src/storage.rs` | Aktiver Tauri-Dateipfad und Dateidialoge |
| `main.js`, `preload.js` | Weiter angebotener alternativer Electron-Pfad |
| `config/aiv_constants.json`, `aiv_categories.json`, `aiv_templates.json` | Itemkatalog, Kategorien und klassische Templates |
| `src/js/character-editor.js`, `config/template*.json`, Auswahlpools, Sections/Locales | Bisher UCP-Character-Modell |
| `src-tauri/src/library.rs`, `src/node/ucp-library.js`, `storage.rs` | UCP-Verzeichnis-/Projektverwaltung |

**H · Quelle.** Noch kein fertiger DE-Lord-Adapter: UCPs Character-Datei
enthält `lord`, `startTroops`, `aic`. Die DE-Datei enthält die AIC-Daten
dagegen unter `lord` mit anderen Schlüsseln und Arrays. Die vorhandene
`legacyKeyMap.json` benennt alte UCP-Schlüssel um; sie ist kein DE-Mapping.

## 4. Drei getrennte Ausgabeziele

| Ziel | Relevante Dateien | Sicherheit und Konsequenz |
| --- | --- | --- |
| Vanilla-Custom-/Extended-Lord | `.lordjson`, `.aivjson`, optional `avatar.png` | **H · Assembly**, G1/G4: kein Extender nötig, um diese Formate grundsätzlich anzubieten |
| Extender-Lordpaket | Zusätzlich `info.json`, `lordmeta.json`, ggf. `Override/` und Lua | **H · Quelle**, S5: separate Metadaten-/Assetregistrierung, kein Bestandteil der Vanilla-AIC-Struktur |
| Extender-Override eingebauter Lords | `Override/AIV/SK_RAT_0.aivjson`, `Override/AIC/SK_RAT.baic` als Beispiel | **H · Quelle**, S4: AIC wird in diesem Pfad binär erwartet; öffentliches LORDJSON ist dort kein Ersatz |

**E · Empfehlung.** Diese Ziele als Fähigkeiten unterscheiden. Eine Datei
separat öffnen/speichern zu können ist unabhängig von Bibliothekserkennung,
Workshopveröffentlichung und genauer Spielsimulation. Basissupport für
Vanilla-JSON braucht weder einen BepInEx-Pluginbuild noch einen nativen Hook.

### 4.1 Lokale Ordner und Lord-Identität

**H · Assembly (G1/G4).** Windows-Standarddatenwurzel:

    %USERPROFILE%\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition

Beispiel für einen eigenen Lord:

    CustomLords\MyLord\Economy.lordjson
    CustomLords\MyLord\Castle One.aivjson
    CustomLords\MyLord\Castle Two.aivjson
    CustomLords\MyLord\avatar.png

Dateinamen liefern Config-/AIV-Anzeigenamen. G1 scannt die unmittelbaren
Dateien des Lordordners, nicht beliebig verschachtelte Unterordner. Ein
benannter Custom Lord wird erst aufgenommen, wenn mindestens eine AIV und
eine Config erfolgreich geladen wurden. Mehrere Varianten sind möglich;
keine UCP-`castle_1`-Manifeststruktur voraussetzen.

**H · Assembly.** `avatar.png` muss für den geprüften Vanilla-Pfad 144×144
Pixel haben und kleiner als 80.000 Bytes sein; fehlende/abgewiesene Avatare
sind nicht dasselbe wie fehlende Gameplaydaten. Die `.data`-/`.ldata`-Dateien
signalisieren Workshop-Uploadinformationen und sind keine Lord-/Burgquelle.

**H · Assembly/Quelle (G4/S3/N3).** `ExtendedLords` ergänzt eingebaute
Lords. Ordnernamen kommen aus `ConfigSettings.extendedLordPaths`; dieses
Array hat aktuell 29 Einträge, ist aber kein dauerhaft festgelegter Umfang.
Die letzten Namen heißen derzeit `Baldwin`, `Bullseye`, `Surgeon`, `Baibars`;
Extender-Enum-/Override-Namen müssen damit nicht übereinstimmen.
Beispielsweise heißen die letzten beiden Enumplätze derzeit `SK_DLC4A/B`.
Ein Config-/AIV-Index, der nullbasierte Vanilla-Lordtyp, `AILords` mit
`SK_NULL` am Anfang und der Spielerplatz 1…8 sind unterschiedliche Domänen.

**H · Assembly (G1/N3).** Named Custom Lords haben `lordType = -1`; bei
Workshopquellen ist ihre interne Identität um die Workshop-Item-ID erweitert.
Bei Extended Lords werden Workshopconfigs dagegen an die gleiche
Lord-Sammlung angehängt. Namen/Typen allein beweisen keine Quellgleichheit.
Serialisierte Configs können Pfad und Workshopflag verlieren. Ein Editor
sollte die tatsächlich gewählte Quelldatei behalten, nicht später den ersten
gleichnamigen Lord suchen.

## 5. LORDJSON: öffentlicher Dateivertrag

**H · Assembly (G1/G3/E1).** Root ist ein Objekt mit Schlüssel `lord`.
Die bekannten Felder darin sind `Int32` oder `Int32[]`; etwaige Labels sind
Darstellung, nicht automatisch die gespeicherten Werte. Die vollständige
Feldinventur mit Arraygrößen und Transferoffsets steht in Anhang A.

Ungewöhnliche Schreibweisen sind verbindliche Schlüssel, insbesondere
`chance_of_harrasment`, `chance_of_seiging`, `harrasment_*`, `harrass_delay`,
`siege_herring_*` und `siege_assasin_amount`. Nicht beim Speichern korrigieren.

### 5.1 Arrays

**H · Assembly/Quelle (G1/G3/S1).** Im aktuellen Spielmodell initialisierte
Längen; der verwaltete Transfer liest diese Positionen direkt:

| Feld | Länge | Bedeutung der Einträge / Grenze des Nachweises |
| --- | ---: | --- |
| `farm_types` | 8 | Strukturtypen, nicht Mapper-IDs |
| `sell_all` | 15 | Waren-IDs; Reihenfolge und Nullplätze erhalten |
| `chance_of_defensive`, `chance_of_harrasment`, `chance_of_seiging` | je 3 | Drei Varianten; UCP-Default/Weak/Strong-Zuordnung vor Konvertierung gesondert prüfen |
| `troop_production_rate` | 3 | Ebenfalls drei Varianten; keine Echtzeitsekunden ableiten |
| `defensive_troops`, `harrasment_troops` | je 8 | Einheitentypen |
| `harrasment_machines` | 8 | Im offiziellen Editor Maschinen-Mapper |
| `siege_machines` | 8 | Im offiziellen Editor Maschinen-Einheitentypen |
| `siege_wall_troops` | 24 | Einheitentypen; nicht auf UCPs vier Haupttruppenslots verkürzen |
| `starting_troops_normal`, `starting_troops_deathmatch`, `starting_troops_crusader` | je 28 | Stückzahlen in fester, eigener Reihenfolge |

**H · Quelle.** S1 füllt beim Public→Internal-Konvertieren fehlende
Arraypositionen mit null auf. G3 greift dagegen direkt auf die Indizes zu.
Eine tolerante Konverterfunktion ist daher kein Nachweis, dass Vanilla
beliebig kurze Arrays akzeptiert. Neue spielkompatible Dokumente sollten
die vollständigen bekannten Arrays enthalten; vorhandene ungewöhnliche
Dateien separat diagnostizieren statt stillschweigend umzuschreiben.

### 5.2 Starttruppen: Index ist keine Unit-ID

**H · Assembly (E1).** Die folgende Beschriftung ist durch die Getter/Setter
des offiziellen Lord-Editors für die drei Modi belegt. Nicht aus
`eChimps`, `eTroops` oder der Reihenfolge von JSON-Schlüsseln berechnen.

| Nullbasierter Index | Einheit | Nullbasierter Index | Einheit |
| ---: | --- | ---: | --- |
| 0 | Archer | 14 | Horse Archer |
| 1 | Crossbowman | 15 | Arabian Swordsman |
| 2 | Spearman | 16 | Fire Thrower |
| 3 | Pikeman | 17 | Fire Ballista: Editor kennt einen Setter, setzt beim Laden aber 0; Spielwirkung offen |
| 4 | Maceman | 18 | Im geprüften Editor nicht zugeordnet; erhalten |
| 5 | Swordsman | 19 | Im geprüften Editor nicht zugeordnet; erhalten |
| 6 | Knight | 20 | Camel Lancer |
| 7 | Im geprüften Editor nicht zugeordnet; erhalten | 21 | Bedouin Healer |
| 8 | Engineer | 22 | Eunuch |
| 9 | Monk | 23 | Ambusher |
| 10 | Arabian Archer | 24 | Skirmisher |
| 11 | Slave | 25 | Heavy Camel |
| 12 | Slinger | 26 | Sapper |
| 13 | Assassin | 27 | Demolisher |

**O · Offen.** Die nicht beschrifteten Positionen haben hier absichtlich
keine erratene Bedeutung. Auch ein Speichern mit dem offiziellen Editor
ist wegen der beschriebenen Fire-Ballista-Normalisierung kein allgemeiner
Beweis für unveränderte Daten. Für Gameplay-Support solcher Slots fehlt ein
separater vollständiger Native-/Laufzeitnachweis.

### 5.3 Kleine Auswahltabellen mit großem Verwechslungspotenzial

**H · Assembly (E1), für die Zahlen zusätzlich S3, soweit benannt.**

| Feld | Im offiziellen Editor angebotene Werte |
| --- | --- |
| `farm_types[]` | None 0, Wheat 30, Hops 31, Apple 32, Dairy 33 (`eStructs`) |
| `blacksmiths_make` | Maces 21, Swords 22, Both **−999** |
| `fletchers_make` | Bows 17, Crossbows 18, Both **−999** |
| `poleturners_make` | Spears 19, Pikes 20, Both **−999** |
| `use_of_religion`, `use_of_ale` | 0, 1, 2 = none/low/high; keine Booleans |
| `lord_gfx_type` | Crusader 0, Arabic 1, Bedouin 2, Female Crusader 4, Female Arabic 6, Female Bedouin 7 |
| `who_to_pick_on` | Richest 0, Weakest 1, Closest 2, Any 3, Human 4 |
| `min_tax`, `max_tax` | 0…12 angebotene Stufen; 3 = keine Steuer, 0…2 = Bestechung |
| `lord_power_display_level` | 0…10 angebotene Anzeigestufen |

**H · Assembly.** Popularität wird im offiziellen Editor durch 100 geteilt
angezeigt und entsprechend gespeichert: 6500 entspricht 65. Das bedeutet
nicht, dass beliebige andere AIC-Zahlen ebenfalls Prozentwerte sind.

**H · Assembly/Native (E1/N4).** Signedness erhalten. Insbesondere darf
`Both = -999` nicht durch eine allgemeine Nichtnegativitätsprüfung zerstört
werden. Negative Verkaufsschwellen sind ebenfalls speicherbar; N4 belegt
allerdings einen Vanilla-Fehler mit Phantomgold in den acht Verkaufskategorien.
**E · Empfehlung:** vor dieser belegten Spielwirkung warnen, Originaldaten
erhalten und keine versteckte Korrektur vornehmen. Offline-Ausführungsbelege
aus N4 sind keine neue Ingameabnahme dieses Leitfadens.

### 5.4 UCP→DE: nützliche Kandidaten, keine fertige Konvertierungstabelle

**M · Zuordnung.** Die folgenden Entsprechungen verbinden Toolkit-Namen
mit DE-Strukturen und offiziellen Editorbeschriftungen. Sie helfen bei der
eigenen Umsetzung, beweisen aber nicht in jedem Fall identische Einheiten,
Wertebereiche, Defaults und gesamte Spielwirkung zwischen HD/UCP und DE.

| Toolkit-/UCP-Feld | DE-Feld |
| --- | --- |
| `CriticalPopularity`, `LowestPopularity`, `HighestPopularity` | `vlow_popularity`, `low_popularity`, `high_popularity` |
| `TaxesMin`, `TaxesMax` | `min_tax`, `max_tax` |
| `Farm1…8` | `farm_types[0…7]`, zusätzlich Struktur-ID-Abbildung |
| `PopulationPerFarm`, `PopulationPerWoodcutter`, `PopulationPerQuarry`, `PopulationPerIronmine`, `PopulationPerPitchrig` | `people_to_farm_ratio`, `extract_wood_ratio`, `extract_stone_ratio`, `extract_iron_ratio`, `extract_pitch_ratio` |
| `MaxQuarries`, `MaxIronmines`, `MaxWoodcutters`, `MaxPitchrigs`, `MaxFarms` | `max_quarries`, `max_mines`, `max_woodcutters`, `max_pitch_dugouts`, `max_farms` |
| `BuildInterval`, `ResourceRebuildDelay` | `build_rate`, `crushed_building_delay` |
| `MaxFood`, `MinimumApples/Cheese/Bread/Wheat/Hop` | `sell_food_at`, `buy_apples_at/buy_cheese_at/buy_bread_at/buy_wheat_at/buy_hops_at` |
| `TradeAmountFood`, `TradeAmountEquipment` | `buy_food_amount`, `buy_weapons` |
| `AIRequestDelay`, `MinimumGoodsRequiredAfterTrade`, `DoubleRationsFoodThreshold` | `pester_for_goods_delay`, `send_goods_margin`, `ration_boost` |
| `MaxWood`, `MaxStone`, `MaxResourceOther`, `MaxEquipment`, `MaxBeer`, `MaxResourceVariance`, `RecruitGoldThreshold` | `trade_wood_at`, `trade_stone_at`, `trade_resources_at`, `trade_weapons_at`, `trade_ale_at`, `trade_minimum`, `base_gold_reserves` |
| `BlacksmithSetting`, `FletcherSetting`, `PoleturnerSetting` | Produktionsmodusfelder aus §5.3 |
| `SellResource01…15` | `sell_all[0…14]`, zusätzlich Waren-ID-Abbildung |
| `DefTotal`, `DefWalls` | `defense_patrol_trigger_level`, `defensive_trigger_level` |
| `DefUnit1…8`, `RaidUnit1…8` | `defensive_troops`, `harrasment_troops` |
| `RaidUnitsBase`, `RaidUnitsRandom`, `RaidRetargetDelay` | `harrasment_trigger_level`, `harrasment_trigger_variance`, `harrass_delay` |
| `HarassingSiegeEngine1…8`, `HarassingSiegeEnginesMax` | `harrasment_machines`, `max_harrasment_machines` |
| `SiegeEngine1…8`, `CowThrowInterval`, `AttMaxEngineers` | `siege_machines`, `siege_cow_timer`, `siege_eng_amount` |
| `AttForceBase`, `AttForceRandom` | `siege_trigger_level`, `siege_trigger_variance` |
| `AttUnitMain1…4` | Nur ein Teil von `siege_wall_troops[0…23]`; keine verlustfreie Rückabbildung aller DE-Slots |
| `TargetChoice` | `who_to_pick_on`; Werte nicht anhand der Optionsreihenfolge übernehmen |
| `lord.Type` | Nur Teilmenge von `lord_gfx_type`; zusätzliche Fraktionen/Geschlechter |
| `lord.StrengthMultiplier`, `DotColour`, `DotCount` | Kein hier bestätigtes vollständiges Mapping zu DE-HP-/Flag-/Anzeige-Feldern |

**H · Quelle.** Zusätzlich zu diesen Kandidaten braucht DE unter anderem
`trade_flour_at`, `trade_pitch_at`, `use_improved_sieging`,
`siege_max_troops`, `siege_normal_wave_multiplier`,
`siege_high_gold_wave_multiplier` und die vollständigen zusätzlichen Arrays.
Unbekannte UCP-Felder `UnknownNNN` nicht rein positionsgleich in den
erweiterten DE-Datensatz kopieren.

**H · Quelle.** `AIOxTethers_*`, `RunningUnits_*`, `AIVTroops_*` im Toolkit
sind UCP-Erweiterungen. Vanilla-DE hat dadurch keinen gleichnamigen
LORDJSON-Vertrag. Mögliche Fixes-/andere Modäquivalente müssen eigens als
Abhängigkeit und eigenes Dateiformat behandelt werden.

## 6. ID-Domänen: konkrete Tabellen

**H · Assembly/Quelle.** `eChimps`, `eMappers`, `eStructs` wurden in der
installierten Extender-Assembly gelesen und gegen die echten Spiel-Enums
verglichen: 90/440/237 benannte Einträge, keine Abweichung. S3 enthält
die zugehörigen Symbole. Zusätzliche AIV-Klassen wurden gegen die installierte
Extender-Assembly gelesen; Editorwerte stammen aus E1/`LowerLevelItems`.

### 6.1 Rekrutierbare Truppen

**H · Assembly.** Die Spalte `eChimps` ist für die hier entsprechend
typisierten AIC-Felder relevant. Die AIV-Spalte ist ein eigener Markerraum.
Die Existenz einer Nummer garantiert nicht ihre Zulässigkeit in jedem AIC-Feld.

| Einheit | `eChimps` / AIC-Typ | AIV-Markerklasse | Starttruppenindex |
| --- | ---: | ---: | ---: |
| Archer | 22 | 6 | 0 |
| Crossbowman | 23 | 7 | 1 |
| Spearman | 24 | 8 | 2 |
| Pikeman | 25 | 9 | 3 |
| Maceman | 26 | 10 | 4 |
| Swordsman | 27 | 11 | 5 |
| Knight | 28 | 12 | 6 |
| Engineer | 30 | 1, speziell OilPourer/OilSmelterEngineer | 8 |
| Monk | 37 | Hier kein gewöhnlicher Burgmarker bestätigt | 9 |
| Arabian Archer | 70 | 16 | 10 |
| Slave | 71 | 13 | 11 |
| Slinger | 72 | 14 | 12 |
| Assassin | 73 | 15 | 13 |
| Horse Archer | 74 | 17 | 14 |
| Arabian Swordsman | 75 | 18 | 15 |
| Fire Thrower | 76 | 19 | 16 |
| Camel Lancer | 78 | 22 / JSON-Editorwert 9022 | 20 |
| Bedouin Healer | 79 | 23 / 9023 | 21 |
| Eunuch | 80 | 24 / 9024 | 22 |
| Ambusher | 81 | 25 / 9025 | 23 |
| Skirmisher | 82 | 26 / 9026 | 24 |
| Heavy Camel | 83 | 27 / 9027 | 25 |
| Sapper | 84 | 28 / 9028 | 26 |
| Demolisher | 85 | 29 / 9029 | 27 |

**H · Assembly.** Weitere unterschiedliche Zahlenräume: `eTroops`
enthält z. B. Archer 1, Crossbowman 4 und Camel Lancer 27. Die
`MAPPER_PEOPLE_BEDOUIN_*`-Werte 400…407 sind wiederum Platzierungsmapper,
nicht die obenstehenden AIV-Verteidigungsmarker. Alle vier Räume getrennt halten.

**H · Assembly.** Im offiziellen Editor heißen einige Beduinenwerte intern
noch `NomadEunich`, `NomadDervish`, `NomadWallBreaker`. Die geprüften
Zahlen 9024/9026/9029 passen zu Eunuch/Skirmisher/Demolisher; aus abweichenden
internen Namen keine weiteren Truppentypen erfinden.

### 6.2 Maschinen und Bauernhöfe

**H · Assembly (E1/S3).**

| Maschine | `siege_machines[]`: Einheitentyp | `harrasment_machines[]`: Mapper |
| --- | ---: | ---: |
| None | 0 | 0 |
| Catapult | 39 | 190 |
| Trebuchet | 40 | 191 |
| Siege Tower | 58 | 192 |
| Battering Ram | 59 | 193 |
| Portable Shield | 60 | 194 |
| Fire Ballista | 77 | 358 |

E1 bietet im `siege_machines`-Pool zusätzlich Mangonel 41 und Ballista 61.
Das beweist das Editorangebot, nicht jede spätere Verwendbarkeit.

| Gebäude | Strukturtyp für passende AIC-Felder | Mapper für AIV-Frames |
| --- | ---: | ---: |
| Wheat Farm | 30 | 70 |
| Hops Farm | 31 | 71 |
| Apple Farm | 32 | 72 |
| Dairy Farm | 33 | 73 |
| Bedouin Outpost | 2 | 53 |
| European Outpost | 106 | 178 |
| Arabian Outpost | 107 | 179 |
| Bedouin Stockade | 108 | 79 |

**H · Quelle.** Toolkit definiert Stockade mit 10×10 und die Outposts mit
5×5. **O · Offen:** Dass ein Gebäude im Katalog steht, ist keine allgemeine
Freigabe für jeden Lord-/KI-Baukontext. Für reale Footprints sind zusätzlich
S3, native Größentabellen und mapperabhängige Reservierungen relevant.

### 6.3 Waren

**H · Assembly/Quelle (E1/S3).** `eGoods`-Werte, nicht UCP-Optionsindizes:

| Wert | Ware | Wert | Ware |
| ---: | --- | ---: | --- |
| 0 | None | 13 | Apples / STORED_FOOD_FRUIT |
| 1 | Wood Logs, anderer Eintrag als normales Bauholz | 14 | Ale |
| 2 | Wood Planks, Editoroption Wood | 15 | Gold |
| 3 | Hops | 16 | Flour |
| 4 | Stone | 17 | Bows |
| 5 | Cow Hides | 18 | Crossbows |
| 6 | Iron | 19 | Spears |
| 7 | Raw Pitch | 20 | Pikes |
| 8 | Refined Pitch, eigener Enumwert | 21 | Maces |
| 9 | Wheat | 22 | Swords |
| 10 | Bread | 23 | Leather Armour |
| 11 | Cheese | 24 | Metal Armour |
| 12 | Meat | | |

S3s Sonderwert `_SE_REQUIRE_HORSE = -1` ist ausdrücklich kein normaler
Vanilla-Wareneintrag. Nicht als pauschale Option für `sell_all` anbieten.

## 7. AIVJSON und DE-BAIV

### 7.1 JSON-Struktur

**H · Assembly (G2/E1), H · Quelle (S2).** Beispiel eines strukturell
passenden Ausschnitts, **keine vollständig abgenommene Beispielburg**:

    {
      "pauseDelayAmount": 100,
      "frames": [
        {"itemType":61,"tilePositionOfsets":[5643],"shouldPause":false},
        {"itemType":25,"tilePositionOfsets":[5741,5742],"shouldPause":true}
      ],
      "miscItems": [
        {"itemType":9022,"positionOfset":5540,"number":0}
      ]
    }

Die Schreibweisen `tilePositionOfsets` und `positionOfset` sind exakt so
beizubehalten. JSON-Frame-`itemType` ist nicht das vorzeichenbehaftete
Binär-Countkennzeichen. Koordinaten sind Offsets im 100×100-Raster.

**H · Assembly (G2).** Zahlen werden beim Vanilla-Raw-Export in `short`
umgewandelt. Reines JSON-Pass-through darf unbekannte Werte erhalten;
eine zusätzliche Spielkompatibilitätsprüfung muss Überläufe gesondert melden.
Beispielsweise ist ein JSON-Testwert 99999 kein bestätigter ausführbarer Mapper.

### 7.2 Binärlayout: keine klassische AIV-Datei

**H · Assembly/Quelle (G2/S2), Vorzeichenzweig H · Native (N1/N2).**
BAIV ist eine Folge little-endian Int16-Wörter:

1. `pauseDelayAmount`.
2. Anzahl Pauseeinträge, dann Pauseeinträge. Vanilla beginnt mit Sentinel 0;
   Frameindex `k` im JSON entspricht Pausennummer **k+2**. Insgesamt maximal
   50 Einträge, also 49 gesetzte Framepausen; spätere Pausen werden im
   geprüften Writer nicht mehr eingetragen.
3. Frameanzahl.
4. Pro Frame: bei genau einer Position `[itemType, offset]`; bei gezählten
   Listen `[-itemType, count, offsets…]`. Der native Countzweig gilt
   ausschließlich bei **Typ < 0**, nicht bei Typ ≤ 0.
5. Anzahl Misc-Einträge; anschließend je `[type, offset, number]`.

**H · Assembly.** G2 zieht bei Misc-Typen **größer als 9000** einmal 9000
ab: 9022 wird 22. Niedrige und hohe Darstellung lassen sich danach nicht
immer eindeutig auseinanderhalten. Die Konverteroption `--restore9000` ist
eine Heuristik, kein allgemeiner verlustfreier Rekonstruktionsbeweis.
Eine JSON→JSON-Bearbeitung sollte die ursprüngliche Schreibweise erhalten.

**H · Native (N1).** `[0, offset]` ist eine Zelllöschung: Import schreibt
Mapper null und den aktuellen Frameindex ins Raster. Rotation erhält diesen
Wert; die Vorbereitung emittiert daraus kein Baurecord. Das ist kein bloßer
positionloser Warteschritt. Mehrere Positionen bei JSON-Typ null haben keine
entsprechende negative Countdarstellung, weil `-0` wieder 0 ist.

**O · Offen, wichtiger Korrekturhinweis zu T1/T2.** Dass das Toolkit `{}`
oder `{"shouldPause":true}` erhalten kann, bestätigt nicht deren problemlose
Verarbeitung durch Vanilla. In G2 hat ein Frame eine nicht initialisierte
`List<int>`, und `GetRawData` greift direkt auf `.Count` zu. Die konkrete
Unity-Deserialisierung fehlender Listen und die Spielakzeptanz dieser
Bestandsdateien wurden hier nicht ausgeführt. W1/W2 treffen eigene
Kompatibilitätsentscheidungen und sind dafür kein unabhängiger Vanilla-Beweis.
Diese Fälle getrennt als erhaltbare Bestandsdaten und Spielvalidierung behandeln.

### 7.3 Markergrenzen und Fixes-Erweiterung

**H · Native (N2).** Vanilla-Import `0x55320` löscht einen 320-Int-Puffer
und schreibt Misc-Positionen an `type * 10 + number`. `number ≥ 10`
wird nicht als unabhängiger elfter Platz behandelt, sondern adressiert
eine spätere Gruppe. Eine im Puffer liegende Adresse beweist keine korrekte
spätere Truppenzuweisung. Auch doppelte Einträge können überschreiben.

**H · Assembly.** AIV-Sonderklassen 20/21 sind Brazier/Flag im
`AivMiscItemType`-Raum; im defensiven Klassenenum sind dieselben Zahlen
reserviert. Derselbe Zahlenwert hat abhängig vom Verbraucher andere Rollen.

**H · Quelle (F1).** Der aktuelle Fixes-Stand besitzt
`GuardingSpotsPerUnitType`, Vanilla 10, erweiterter Bereich 11…30,
zusätzliche Puffer und einen eigenen Importhook. Die per-Lord-Präferenz
steht in `Override/fixes/preferences.json`, nicht in `.lordjson`.
Der neue Importpfad unterscheidet externe Speicherplätze ausdrücklich.
**O · Offen:** Diese Quelle ist keine Ingameabnahme der installierten
Fixes-Konfiguration. Ein Toolkit-Export mit 30 Markern braucht eine sichtbare
Abhängigkeitskennzeichnung; Vanilla darf nicht als gleichwertiger Verbraucher gelten.

### 7.4 Koordinaten, Footprints und Rotation

**H · Native (N1/N2), überprüfbar in W1/W2.** Für einen Datei-Offset
`p = 100*y + x` erzeugt `0x53CA0` den Importoffset
`p - 200*floor(p/100) + 9900`, also `(x, 99-y)`.
Danach wird das vollständige 100×100-Raster rotiert. Native Kardinalwerte
sind 0/2/4/6; W1 bezeichnet die passenden eigenen Drehungen als 0/90/180/270°.
Editorzeilen, Welt-Y und Kamerarotation nicht miteinander verwechseln.

**H · Quelle/Native.** Vor der Rotation expandiert der Import reguläre
quadratische Footprints in positives Raster-X/Y. Im ursprünglichen
JSON-Zeilenraum bedeutet das bei Größe `s`: Spalten `x…x+s−1`, Zeilen
`y−s+1…y`. Eine reine Ankerpunktrotation genügt deshalb nicht immer, um
den späteren minimalen Bau-Eckpunkt zu bestimmen.

**H · Native für die beschriebenen Verträge.** Spätere Frames können
Rasterzellen früherer Frames überschreiben. Einige Mapper reservieren
zusätzliche Zellen als `0xffff`. Eine aufsummierte Liste unabhängiger
Rechtecke ist daher kein vollständiger Nachbau des nativen vorbereiteten Plans.
Ein Keep im JSON, der native Keep-Referenzpunkt und die Baukoordinate sind
ebenfalls nicht automatisch derselbe Punkt. W1/W2 trennen diese Begriffe.

**E · Empfehlung.** Mit asymmetrischer Burg, versetztem Keep, beiden
Tororientierungen, mehrzelligen Mauern, überlagerten Frames und allen vier
Drehungen prüfen. Symmetrische Burgen verdecken Spiegelungs- und Originfehler.

## 8. Grenzen von Vorschau, Bauvalidierung und AIC-Hilfetexten

**H · Native (N1/N2), O für den vollständigen Bauausgang.** Ein positives
Candidate-Fit ist keine Garantie, dass später jedes Gebäude gebaut wird.
Der Pfad nach Vorbereitung prüft unter anderem Ressourcen, Erreichbarkeit,
Reichweite, Gelände, Belegung und modifizierbare Regeln.

| Thema | Gesicherte Teilinformation | Grenze |
| --- | --- | --- |
| Candidate-Fit | `0x57080 → 0x7B060`, in diesem Pfad Spieler 0 / Modus 0 | Keine vollständige Simulation der späteren Baukonstruktoren |
| Moat/Drawbridge | Spätere Vanilla-Höhenzweige verwenden Grenze 12; ExtraFeatures kann KI-Zweige ändern | Nicht allein aus dem Fit oder dem Ausgangsgelände auf Baufolgenschritte schließen |
| Tower/Gate-Reichweite | N1 prüft alle Footprintzellen relativ zur gespeicherten Keepreferenz; Chebyshev-Abstand; Verbündetenhilfe kann halbe Reichweite verwenden | Andere Platzierungsbedingungen bleiben bestehen |
| Kartenabhängige Standardreichweite | 160→45, 200→50, 300→60, 400→70, 500→80, 600→90, 700/800→100; Fallback 70 im untersuchten Stand | Extender-/Modoverrides möglich; keine universelle feste 70-Tile-Regel |
| Completed Castles | Vorherige Spieler können vor späteren Fits bereits den Kartenstand verändern | W3 hält spätere Ergebnisse ohne vollständiges Zustandsmodell bewusst offen |
| Outposts | Mapper-/Strukturidentitäten bestätigt | Kein allgemeiner Nachweis jeder KI-Platzierungsart |
| Defensive Patrouillen | [AIDefensePatrol](AIDefensePatrol.md) trennt `DefTotal`, `DefWalls`, Rekrutierung und bekannte Fehler | Eine gespeicherte Zielgröße garantiert nicht ihren fehlerfreien Vanilla-Vollzug |
| `use_improved_sieging` | [LordAttackChecks](AIAttacks/LordAttackChecks.md) trennt AIC-Einstellung, globale Optionen und UCP-Patches | Keine Gleichsetzung von UCP-Angriffswellenänderungen und DE-Lobbyoptionen |

**H · Quelle (T1).** Die vorhandenen Toolkit-Kosten-/Produktions-/Truppen-
und Kartenintegrationen sind überwiegend klassisch. Unter anderem verarbeiten
`src-tauri/src/game/` und `src/node/` klassische GM1-/MAP-/EXE-Daten sowie
UCP-Overrides. Ein anderer Installationspfad macht diese Datenquellen nicht
zu DE-Datenquellen. **E:** Anzeige nur als ausdrücklich benannte Schätzung
oder verifiziertes Spielprofil anbieten; offene Aussagen nicht als rote
Formatfehler behandeln.

### 8.1 Native-Auditindex für Vertiefungen

Alle RVAs beziehen sich ausschließlich auf den Native-Hash in §2. Die
automatischen Datenbankfunktionsnamen sind häufig nur `candidate`;
die hohe Sicherheit betrifft die einzeln belegten Verträge in N1/N2,
nicht einen pauschal bestätigten Funktionsnamen oder sämtliche Nebenwirkungen.

| RVA | Rolle im auditierten Pfad | Sicherheit |
| --- | --- | --- |
| `0x55320` | Raw-AIV-Import: Header, Vorzeichenbranch, Raster und Misc-Adressierung | **H · Native**, N1/N2 |
| `0x53CA0` | Offset-/Y-Transformation | **H · Native**, direkte Formel und Caller |
| `0x6A190` | Mappergröße; Import behandelt Moat gesondert und begrenzt nichtpositive Größen auf 1 | **H · Native** für Importbranch; keine universelle Zulässigkeitsprüfung |
| `0x56670` | Rotation des importierten Rasters und der Steps | **H · Native** für die Kardinalpfade |
| `0x53D00` | Gewählten Plan vorbereiten, null/negative Rasterzellen überspringen, überlebende Bauzellen auswerten | **H · Native** für beschriebene Teilverträge |
| `0x94350` | Geordneter Spielerstart und AIV-Auswahl | **H · Native** für Aufrufreihenfolge; Managed-Optionszuordnung teilweise **M** |
| `0x54DE0`, `0x54F60` | Festen Kandidaten prüfen bzw. Autoauswahl | N2 mit eigenen Branch-/Zustandsgrenzen |
| `0x55F50`, `0x51790` | Completed-Castle-/Schritt-Ausführung | N1/N2; kein vollständiges Offline-Modell aller Konstruktoren |
| `0x6AF00`, `0xEEF90` | Kartenabhängige Reichweite und Zellabstand | **H · Native** im dokumentierten Kontext |

Reproduzierbarer Einstieg vom Workspace-Root:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x55320
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callers 0x55320
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' callees 0x55320

Zuerst `CURRENT.json` und das dort referenzierte Manifest lesen und die
installierte DLL hashen. Keine alte Adresse auf eine andere DLL anwenden.

## 9. Konverter als Quellen verwenden, nicht als verlustfreien Universalpfad

**H · Quelle (S1/S2/G1/G3).** Besonders relevante Unterschiede:

1. DE-AIC-Binärstruktur: **1508 Bytes = 0x5E4 = 377 Int32**. Das wurde
   zusätzlich per Reflection/Marshal an der installierten Extender-Assembly
   geprüft. Öffentliche LORDJSON-Felder sind nicht einfach alle 377 Wörter.
2. Öffentliche JSON-Arrays werden im Transfer in nummerierte Felder
   (`farm_types1…8` usw.) aufgeteilt. `.aicjson` ist das interne flache
   Schema; `.lordjson` das öffentliche Wrapper-/Arrayschema.
3. Vanilla `NewAIC` besitzt `opponent_type_for_speech`. S1s `PublicAIC`
   lässt es weg und setzt es beim Rückkonvertieren auf `opponent_type`.
   G3 übernimmt dagegen den eigenständigen Quellwert. Diese beiden Wege
   sind nicht identisch; ein Editor muss das vorhandene Feld erhalten.
4. Interne Felder wie `extendedLordParent` und reservierte `free*`-Wörter
   sind nicht vollständig öffentlich abgebildet. BAIC→LORDJSON→BAIC ist
   deshalb kein zugesicherter bitidentischer Archivierungsweg.
5. Vanilla initialisiert die drei neuen Siege-Felder auf 200/5/7. S1s
   Public-Modell deklariert dafür keine gleichen Initializer. Fehlende
   Werte nicht ungeprüft aus einem generischen Null-Template erzeugen.
6. S1/S2 lesen JSON teilweise toleranter, etwa case-insensitiv, mit
   Kommentaren oder trailing commas. Das beweist keine gleiche Toleranz
   in Vanilla-`JsonUtility` oder im Toolkit. Spielausgaben als reguläres
   JSON mit exakter Schlüsselschreibung schreiben.

**E · Empfehlung.** Für die eigentliche LORDJSON-Bearbeitung den öffentlichen
Originalbaum behalten. Für optionale BAIC-Bearbeitung zusätzlich das komplette
Binär-/Internalschema erhalten. Unterschiedliche Round-trip-Zusagen benennen.

Nützliche CLI-Aufrufe der vorhandenen Extender-Werkzeuge, nicht hier ausgeführt:

    SHCDESE.AICDecoder.exe Input.baic Output.lordjson --format public-json
    SHCDESE.AICDecoder.exe Input.lordjson Output.baic --format binary
    SHCDESE.AIVDecoder.exe Input.baiv Output.aivjson
    SHCDESE.AIVDecoder.exe Input.aivjson Output.baiv --format binary

**H · Quelle (S4).** `DumpEmbeddedAIVs` und `DumpEmbeddedAICs` sind
Extender-Konfigurationsoptionen für `_EXPORT` im Spielordner. Das ist ein
Weg zu referenzierbaren eingebauten Daten; ein Export hängt vom jeweiligen
geladenen Spiel-/Modzustand ab und ist nicht automatisch pristine Vanilla.

## 10. Optionale Metadaten, Medien und Fixes-Präferenzen

**H · Quelle/Assembly (S5/G1).** Vanilla-Avatar und Vanilla-CustomMedia
sind getrennt von Extender-`lordmeta.json`. `LordInfo` enthält derzeit
lokalisierte Namen/Titel und Beschreibungstexte, FacePath,
JoinAudioPath, LeaveAudioPath, Messages und IncomingMessage. Die
Extender-Registrierung verarbeitet `info.json` und `lordmeta.json`;
`Override/`-Assets und optionales `init.lua` gehören zu diesem Zusatzprofil.
Kein Grund, sie in die Vanilla-`lord`-AIC-Struktur zu verschieben.

**H · Assembly.** G1 scannt Vanilla-`CustomMedia/<Name>/text.txt` und
verwendet eigene Tags, einschließlich Nicknames. UCP-`lines.json` ist
dadurch kein direktes DE-Medienformat. Eine Medienmigration braucht ein
gesondertes Tag-/Dateiformatmapping; diese Datei beschreibt keinen vollständigen
Audio-/Video-Konverter.

**H · Quelle (F1/N5), keine neue Laufzeitabnahme.** Beim optionalen
Erzeugen von Fixes-Präferenzen:

- `GuardingSpotsPerUnitType` separat behandeln (§7.3).
- `CustomMinPopularityRequiredForBadThings` benötigt auch
  `CustomBuildAttemptsRequiredForBadThings`, damit der dokumentierte Override
  aktiviert wird; Vanilla-Versuchszahl ist im auditierten Stand 100.
- Käufe als vollständige Paare `BuyWoodAt/BuyWoodAmount`,
  `BuyIronAt/BuyIronAmount`, `BuyAleAt/BuyAleAmount` erzeugen.
- Vorhandene fremde Werte nicht stillschweigend ergänzen, begrenzen oder
  löschen. Unbekannte Sidecar-Felder erhalten.
- Mapbezogene `byName`/`bySlot`-Präferenzen können Lorddefaults überlagern.
  Ein Paketdefault ist daher kein Beweis für den effektiven Spielwert.

Weitere Serp-spezifische Daten wie `.modlord.json` und ExtendedData-Snapshots
sind eigene Modverträge, keine Voraussetzung für grundlegenden DE-Support.
Die [ExtendedData-Lorddatenquellen](../ExtendedData/ExtendedData.Core/LordDataSnapshot.cs)
und N3 helfen bei optionaler späterer Paketportabilität.

## 11. Vorliegende Tests, konkrete Regressionsfälle und offene Abnahme

### 11.1 Bereits ausgeführte Prüfungen

**H · Test (T2), datierte Momentaufnahme vom 22.10.2026.** Im unveränderten
Toolkit bestanden 17 gezielte Tests aus `castle-format`, `aiv-format`,
`aiv-file`, `aiv-native-codec`, `character-aic-names`, `character-startup`.
Kein vollständiger Desktop-Build und kein Ingame-Test wurde durchgeführt.

Die zusätzliche Dateiprobe verwendet echte Upstream-Normalisierungsfunktionen:

| Eingaben / Ergebnis | Aussage |
| --- | --- |
| 134 lokale Dateien mit Endung `.aivjson` | Datenbestand in CustomLords/ExtendedLords, kein universeller Formatkorpus |
| 126 strukturell identische JSON-/Editor-Round-trips | Gut belegter Erhalt in diesem Lade-/Speicherpfad |
| 7 abgelehnte Pestdoktor-Burgen | Reine Pausenframes `{"shouldPause":true}` scheitern am Toolkit-Validator; Vanilla-Akzeptanz offen |
| 1 verbleibende Datei | ZIP-Signatur `PK`, irreführende Endung in `_LegacyMediaSource`; kein gültiges JSON |
| Unbekannter Toolkit-Framewert 160 | Erhalten; aktuell als `MAPPER_GARDEN1` in Spiel/Extender bestätigt, kein unbekannter DE-Wert mehr |
| Wolf-LORDJSON durch UCP-Normalisierung | `farm_types` wird Objekt statt Array; UCP-Defaults werden unter `aic`/`startTroops` ergänzt |

Der Wolf-Versuch ist kein echtes Öffnen/Speichern durch die GUI; er führt
die relevanten Funktionen im Speicher aus. Er belegt trotzdem direkt,
dass die bestehende UCP-Normalisierung keine sichere LORDJSON-Konvertierung ist.
Die Eingabedateien wurden nicht verändert.

### 11.2 Nützliche lokale Fixtures

**H · Quelle/Test.** Bezogen auf die Standarddatenwurzel aus §4.1:

- `ExtendedLords/Wolf/Wolf.lordjson`: öffentliche Arrays und nachvollziehbarer
  Character-Normalisierungsfehler; Original etwa `build_rate=3`, `min_tax=3`.
- `CustomLords/Pestdoktor/Plague Doctor1 1st Castle (Neutral).aivjson`:
  erster Validatorfehler bei Schritt 79 mit reinem Pausenframe.
- `ExtendedLords/Baibars/Nimrod1.aivjson`, `Nimrodwest.aivjson`,
  `Nimrodwest2.aivjson`: nachgewiesene Reichweiten-Grenzfälle aus N1.
- `Findings/AIVPlacement/EVIDENCE.zip`: datierte Fit-/Bautraces mit eigenen
  Metadaten; gezielt über `EXPERIMENTS.md` und den vorhandenen Restore-Helfer
  verwenden, nicht pauschal entpacken oder historische Nachweise ändern.

**E · Empfehlung.** Für Autoren/CI kleine selbst erstellte Fixtures mit
klarer Herkunft verwenden. Die lokalen Dateien sind Fundstellen und nicht
automatisch als weiterverteilbarer Testsatz freigegeben. Eine geänderte
Benutzerdatei ist kein Ersatz für den ursprünglichen hashgebundenen Nachweis.

### 11.3 Abnahmefälle für die Autoren

**E · Empfehlung; offene Punkte jeweils O.**

| Prüfung | Erwarteter Nachweis |
| --- | --- |
| Unverändertes LORDJSON öffnen/speichern | Arrays, unbekannte Felder, negative Werte, Zusatzfelder und bekannte Werte bleiben erhalten; keine UCP-Defaults |
| Bekanntes DE-Feld ändern | Nur das gewünschte Feld ändert sich; Originalwerte anderer Felder bleiben erhalten |
| Alle DE-Truppentypen | Richtiger ID-Raum pro Feld; alle drei Starttruppenarrays einschließlich unbeschrifteter Slots erhalten |
| Alle 24 Haupttruppenslots | Keine Kürzung auf vier UCP-Slots |
| Maschinen | Catapult 190 im Harassment-Pool und 39 im Siege-Pool getrennt prüfen |
| Werkstattproduktion | `Both=-999`, alle einzelnen Warenwerte erhalten |
| Missing/null/zu kurze Arrays | Diagnostik und explizite Normalisierungsentscheidung; kein späterer unbemerkter Datenverlust |
| Public↔BAIC | Gesonderter Nachweis für Speech-/Parent-/Reserved-Felder; keine falsche Bitidentitätszusage |
| AIV-Koordinaten | Asymmetrische Burg, versetzter Keep, alle Rotationen, Tore, echte Footprints |
| Überschriebene und Typ-null-Frames | Importreihenfolge und Zelllöschung erhalten; kein Countzweig für null |
| AIV-Pausen | Erste/letzte Pause, Grenze 49, zusätzliche Pausen, `{}` und reine Pausenframes getrennt prüfen |
| Marker | Niedrige/902x-Darstellung, Nummern, Duplikate, 10er-Vanillagrenze und ausdrücklich aktiviertes 30er-Fixesprofil |
| Dateiformatwahl | UCP, LORDJSON und AIVJSON nicht nur nach `.json` unterscheiden; falsche Endung/ZIP sauber melden |
| Speichern | Endung und tatsächliches Format stimmen überein, Abbruch/Fehler lässt Ziel unbeschädigt |
| Bibliothek | Named/Extended/Workshop-Identität, mehrere Varianten, Umlaute/Leerzeichen, lange Pfade und externe Quelldateien |
| Spielprüfung | Offizieller Editor und Spiel laden die Ausgabe; Baufolge und Truppenzuweisung beobachten, auch gewöhnlicher Bau und Completed Castles |
| Optionaler Modsupport | Vanilla, Extender und gewählter Fixes-Stand getrennt abnehmen; Abhängigkeiten offen anzeigen |
| Classic/UCP-Regression | Bestehende Import-/Exportpfade, unveränderte Binäroriginale und UCP-Projekte weiterhin prüfen |

## 12. Offene Fragen, die dieser Bericht absichtlich nicht errät

1. **O:** Vollständige HD/UCP↔DE-Spielsemantik jedes einzelnen AIC-Feldes,
   insbesondere alte `UnknownNNN`, Zeit-/Zählereinheiten und sämtliche
   Schwelleninteraktionen. Schema und ähnliche Bezeichnung genügen dafür nicht.
2. **O:** Vanilla-Akzeptanz fehlender `tilePositionOfsets` bei `{}` und reinen
   Pausenframes; ursprüngliche Schreibweise erhalten, gesondert prüfen.
3. **O:** Gameplaybedeutung der unbeschrifteten Starttruppenslots und
   Fire-Ballista-Starttruppen in der geprüften Editorversion.
4. **O:** Gültigkeit sämtlicher platzierbarer Outposts, unbekannter/erweiterter
   Mapper und mehr als zehn Marker in jeder Kombination von Vanilla und Mods.
5. **O:** Vollständiger DE-Karten-/Grafikimport und genaue Kosten-/Produktions-/
   Pathing-/Feueranalyse. Dafür sind zusätzliche Datenquellen und eigene Audits
   nötig; JSON-Support allein beantwortet das nicht.
6. **O:** Vollständige DE-Medien-/Workshopmigration und reale Multiplayerabnahme.

**E · Empfehlung zur Abgrenzung.** Ein eigenständiger, formatgetreuer
LORDJSON-Editor plus Nachbesserungen am vorhandenen AIVJSON-Support ist ein
sinnvoller erster Umfang. Lord-Bibliothek, Modmetadaten/Medien und exakte
DE-Spielanalyse lassen sich danach separat ergänzen. Die konkrete Umsetzung
und Architektur bleiben bei den Toolkit-Autoren.

## Anhang A: vollständige bekannte öffentliche DE-AIC-Feldinventur

**H · Assembly/Quelle (G1/G3/S1).** Automatisch aus der hashgebundenen
`NewAIC`-Deklaration erfasst und gegen die aktuelle installierte Spielklasse
abgeglichen. Alle Schlüssel gehören unter `lord`. Die Offsetspalte benennt
das erste zugehörige Int32-Wort im internen Transferlayout, nicht eine
JSON-Dateiposition. Bei Arrays folgen die Worte in der angegebenen Reihenfolge.

Abgleich: **118 öffentliche Felder, davon 14 Arrays; 118 erste Transferoffsets
gegen die installierte `InternalAIC`-Struktur geprüft, keine Abweichung.**

Die Initializer-Spalte dokumentiert C#-Deklarationen, **keine generelle
Zusage über das Deserialisieren fehlender JSON-Felder**. Für neue Dateien
geeignete vollständige Referenzexporte verwenden; unbekannte Werte erhalten.

| Schlüssel unter `lord` | Typ / Länge | C#-Initializer | Erstes Transferoffset |
| --- | --- | --- | --- |
| `opponent_type` | Int32 | 0 (implizit) | `0x0000` |
| `opponent_type_for_speech` | Int32 | 0 (implizit) | `0x0004` |
| `lord_gfx_type` | Int32 | 0 (implizit) | `0x0008` |
| `flag_type` | Int32 | 0 (implizit) | `0x000C` |
| `use_of_religion` | Int32 | 0 (implizit) | `0x0010` |
| `use_of_ale` | Int32 | 0 (implizit) | `0x0014` |
| `vlow_popularity` | Int32 | 0 (implizit) | `0x0018` |
| `low_popularity` | Int32 | 0 (implizit) | `0x001C` |
| `high_popularity` | Int32 | 0 (implizit) | `0x0020` |
| `min_tax` | Int32 | 0 (implizit) | `0x0024` |
| `max_tax` | Int32 | 0 (implizit) | `0x0028` |
| `farm_types` | Int32[8] | 8 Nullwerte | `0x002C` |
| `people_to_farm_ratio` | Int32 | 0 (implizit) | `0x004C` |
| `extract_wood_ratio` | Int32 | 0 (implizit) | `0x0050` |
| `extract_stone_ratio` | Int32 | 0 (implizit) | `0x0054` |
| `extract_iron_ratio` | Int32 | 0 (implizit) | `0x0058` |
| `extract_pitch_ratio` | Int32 | 0 (implizit) | `0x005C` |
| `max_quarries` | Int32 | 0 (implizit) | `0x0060` |
| `max_mines` | Int32 | 0 (implizit) | `0x0064` |
| `max_woodcutters` | Int32 | 0 (implizit) | `0x0068` |
| `max_pitch_dugouts` | Int32 | 0 (implizit) | `0x006C` |
| `max_farms` | Int32 | 0 (implizit) | `0x0070` |
| `build_rate` | Int32 | 0 (implizit) | `0x0074` |
| `crushed_building_delay` | Int32 | 0 (implizit) | `0x0078` |
| `sell_food_at` | Int32 | 0 (implizit) | `0x007C` |
| `buy_apples_at` | Int32 | 0 (implizit) | `0x0080` |
| `buy_cheese_at` | Int32 | 0 (implizit) | `0x0084` |
| `buy_bread_at` | Int32 | 0 (implizit) | `0x0088` |
| `buy_wheat_at` | Int32 | 0 (implizit) | `0x008C` |
| `buy_hops_at` | Int32 | 0 (implizit) | `0x0090` |
| `buy_food_amount` | Int32 | 0 (implizit) | `0x0094` |
| `buy_weapons` | Int32 | 0 (implizit) | `0x0098` |
| `pester_for_goods_delay` | Int32 | 0 (implizit) | `0x009C` |
| `send_goods_margin` | Int32 | 0 (implizit) | `0x00A0` |
| `ration_boost` | Int32 | 0 (implizit) | `0x00A4` |
| `trade_wood_at` | Int32 | 0 (implizit) | `0x00A8` |
| `trade_stone_at` | Int32 | 0 (implizit) | `0x00AC` |
| `trade_resources_at` | Int32 | 0 (implizit) | `0x00B0` |
| `trade_flour_at` | Int32 | 0 (implizit) | `0x00B4` |
| `trade_weapons_at` | Int32 | 0 (implizit) | `0x00B8` |
| `trade_ale_at` | Int32 | 0 (implizit) | `0x00BC` |
| `trade_pitch_at` | Int32 | 0 (implizit) | `0x00C0` |
| `trade_minimum` | Int32 | 0 (implizit) | `0x00C4` |
| `base_gold_reserves` | Int32 | 0 (implizit) | `0x00C8` |
| `blacksmiths_make` | Int32 | 0 (implizit) | `0x00CC` |
| `fletchers_make` | Int32 | 0 (implizit) | `0x00D0` |
| `poleturners_make` | Int32 | 0 (implizit) | `0x00D4` |
| `sell_all` | Int32[15] | 15 Nullwerte | `0x00D8` |
| `move_mobile_defenders` | Int32 | 0 (implizit) | `0x0114` |
| `max_mobile_groups` | Int32 | 0 (implizit) | `0x0118` |
| `buy_defense_machines_at` | Int32 | 0 (implizit) | `0x011C` |
| `buy_defense_machines_delay` | Int32 | 0 (implizit) | `0x0120` |
| `dog_release_timing` | Int32 | 0 (implizit) | `0x0124` |
| `dog_points_count` | Int32 | 0 (implizit) | `0x0128` |
| `chance_of_defensive` | Int32[3] | 3 Nullwerte | `0x012C` |
| `chance_of_harrasment` | Int32[3] | 3 Nullwerte | `0x0138` |
| `chance_of_seiging` | Int32[3] | 3 Nullwerte | `0x0144` |
| `economy_protection_number` | Int32 | 0 (implizit) | `0x0150` |
| `economy_protection_type` | Int32 | 0 (implizit) | `0x0154` |
| `bodyguard_number` | Int32 | 0 (implizit) | `0x0158` |
| `bodyguard_type` | Int32 | 0 (implizit) | `0x015C` |
| `moat_diggers` | Int32 | 0 (implizit) | `0x0160` |
| `moat_digger_type` | Int32 | 0 (implizit) | `0x0164` |
| `troop_production_rate` | Int32[3] | 3 Nullwerte | `0x0168` |
| `defense_patrol_trigger_level` | Int32 | 0 (implizit) | `0x0174` |
| `defense_patrols` | Int32 | 0 (implizit) | `0x0178` |
| `defense_patrol_style` | Int32 | 0 (implizit) | `0x017C` |
| `defense_patrol_delay` | Int32 | 0 (implizit) | `0x0180` |
| `defensive_trigger_level` | Int32 | 0 (implizit) | `0x0184` |
| `defensive_troops` | Int32[8] | 8 Nullwerte | `0x0188` |
| `harrasment_trigger_level` | Int32 | 0 (implizit) | `0x01A8` |
| `harrasment_trigger_variance` | Int32 | 0 (implizit) | `0x01AC` |
| `harrasment_troops` | Int32[8] | 8 Nullwerte | `0x01B0` |
| `harrasment_machines` | Int32[8] | 8 Nullwerte | `0x01D0` |
| `max_harrasment_machines` | Int32 | 0 (implizit) | `0x01F0` |
| `harrass_delay` | Int32 | 0 (implizit) | `0x01F4` |
| `siege_trigger_level` | Int32 | 0 (implizit) | `0x01F8` |
| `siege_trigger_variance` | Int32 | 0 (implizit) | `0x01FC` |
| `siege_troops_before_will_come_to_rescue` | Int32 | 0 (implizit) | `0x0200` |
| `siege_troops_on_site_percent` | Int32 | 0 (implizit) | `0x0204` |
| `siege_troops_at_home_percent` | Int32 | 0 (implizit) | `0x0208` |
| `siege_soften_up_delay` | Int32 | 0 (implizit) | `0x020C` |
| `siege_victory_delay` | Int32 | 0 (implizit) | `0x0210` |
| `percent_chance_waiting_for_joint_attack` | Int32 | 0 (implizit) | `0x0214` |
| `siege_machines` | Int32[8] | 8 Nullwerte | `0x0218` |
| `siege_cow_timer` | Int32 | 0 (implizit) | `0x0238` |
| `siege_eng_amount` | Int32 | 0 (implizit) | `0x023C` |
| `siege_moat_troop` | Int32 | 0 (implizit) | `0x0240` |
| `siege_moat_amount` | Int32 | 0 (implizit) | `0x0244` |
| `siege_herring_troop` | Int32 | 0 (implizit) | `0x0248` |
| `siege_herring_amount` | Int32 | 0 (implizit) | `0x024C` |
| `siege_assasin_amount` | Int32 | 0 (implizit) | `0x0250` |
| `siege_ladder_amount` | Int32 | 0 (implizit) | `0x0254` |
| `siege_tunnel_amount` | Int32 | 0 (implizit) | `0x0258` |
| `siege_storm_troop` | Int32 | 0 (implizit) | `0x025C` |
| `siege_storm_amount` | Int32 | 0 (implizit) | `0x0260` |
| `siege_storm_tribes` | Int32 | 0 (implizit) | `0x0264` |
| `siege_cover_troop` | Int32 | 0 (implizit) | `0x0268` |
| `siege_cover_amount` | Int32 | 0 (implizit) | `0x026C` |
| `siege_cover_tribes` | Int32 | 0 (implizit) | `0x0270` |
| `siege_shock_troop` | Int32 | 0 (implizit) | `0x0274` |
| `siege_shock_amount` | Int32 | 0 (implizit) | `0x0278` |
| `siege_reserve_troop` | Int32 | 0 (implizit) | `0x027C` |
| `siege_reserve_amount` | Int32 | 0 (implizit) | `0x0280` |
| `siege_reserve_tribes` | Int32 | 0 (implizit) | `0x0284` |
| `siege_wall_troops` | Int32[24] | 24 Nullwerte | `0x0288` |
| `siege_wall_amount` | Int32 | 0 (implizit) | `0x02E8` |
| `siege_wall_tribes` | Int32 | 0 (implizit) | `0x02EC` |
| `who_to_pick_on` | Int32 | 0 (implizit) | `0x02F0` |
| `use_improved_sieging` | Int32 | 0 (implizit) | `0x02F4` |
| `starting_troops_normal` | Int32[28] | 28 Nullwerte | `0x02F8` |
| `starting_troops_deathmatch` | Int32[28] | 28 Nullwerte | `0x0368` |
| `starting_troops_crusader` | Int32[28] | 28 Nullwerte | `0x03D8` |
| `lord_power_display_level` | Int32 | 0 (implizit) | `0x0448` |
| `lord_hps_percent` | Int32 | 0 (implizit) | `0x044C` |
| `siege_max_troops` | Int32 | 200 | `0x0454` |
| `siege_normal_wave_multiplier` | Int32 | 5 | `0x0458` |
| `siege_high_gold_wave_multiplier` | Int32 | 7 | `0x045C` |

Der interne Datensatz enthält darüber hinaus nicht vollständig öffentlich
abgebildete Parent-/Reserved-Wörter (§9). Diese Tabelle ist eine Feld- und
Typinventur; sie behauptet nicht für jeden Wert eine vollständig auditierte
Gameplaybeschreibung.

## Anhang B: Reproduktion und Änderungsumfang dieses Berichts

**H · Quelle.** Diese Arbeit hat Dokumentation erstellt und vorhandene
Quellen/Assemblies gelesen. Die zusätzlichen Editor-Dekompilate unter
`_inspect/AI-Toolkit-DE/` sind erhaltene Analyseartefakte. Toolkit-, Extender-,
Fixes- und Spielquellen/-installationen wurden nicht verändert. Kein Commit,
kein PR, kein Autorenkontakt und kein Spielstart wurden durchgeführt.

T2 lässt sich mit dem vorhandenen Node-Runtime gegen den identifizierten
Toolkit-Klon erneut ausführen. Seine `result.json` ist eine datierte
Momentaufnahme und wird durch eine Wiederholung neu erzeugt; für historische
Vergleiche vorher gezielt bewahren. Die obigen Ergebnisse stammen aus dem
vorherigen, konkret datierten Durchlauf, nicht aus einer neuen Spielabnahme.
