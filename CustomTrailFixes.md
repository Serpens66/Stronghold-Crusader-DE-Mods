# Custom-Trail-Fixes: Nachprüfung und Lösungspläne

Stand: 20.10.2026. Dieses Dokument richtet sich an die Implementierung und das Review. Es korrigiert beziehungsweise präzisiert die acht zuvor im Chat genannten Punkte. Die beschriebenen Produktänderungen sind noch nicht umgesetzt. Ein geprüfter Lösungsansatz ist keine bestandene Spielabnahme.

## 1. Prüfstand und belastbare Ergebnisse

### Quellen und Eingaben

- Mod-Repository: `develop`, HEAD `a0575ecb038d529d91a882014669e0d0f464d8b2`. Releasevergleich: `SerpsMods/v1.0.28`, Commit `e4306050d32d65b375dbee63f7249cfe48f7795a`.
- APIShared: eigenständiger Checkout `APIShared`, Branch `dev/Serpens66`, HEAD `feaefcead116bdd24b55bb2fd862905d620c8209`, einschließlich vorhandener Arbeitsänderungen. Diese Angaben sind eine datierte Beobachtung; vor Umsetzung erneut prüfen. Die im lokalen Arbeitsindex geschützte ältere Arbeitskopie ist nicht automatisch der aktuelle Implementierungsstand.
- Eingaben: die vier Ordner `D:\DownloadsD\Trail`, `Trail2`, `Trail3`, `Trail4`, außerdem `D:\DownloadsD\CustomLords\CustomLords` und `D:\DownloadsD\Two Against the Marsh [8P][2v6 Survival][Unbalanced].map`.
- Native DLL: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; installierte Managed-Assembly: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Beide stimmen mit der aktuellen Baseline überein.
- Die zentralen Dateien `MissionAssetResolver`, `CoopTrailPackageExporter`, `TrailLordSelectionPolicy` sowie die beiden betroffenen Coop-/AIV-Komponenten in BugfixesAndQoL unterscheiden sich im geprüften Arbeitsstand nicht vom genannten Modpack-Release. Die jüngeren Änderungen an `TrailLordPackageRuntime` betreffen die Fehlerdiagnose, nicht die hier untersuchte Quellenpflicht.

### Erneut ausgeführte Prüfungen

Die erhaltenen Analysequellen, Logs und Ergebnisse liegen unter [`.work/a/ctfix/001`](.work/a/ctfix/001). Dieser Ordner ist ein Analyseartefakt, keine Entwicklungsarbeitskopie und kein installierbares Paket.

| Prüfung | Ergebnis und Grenze |
| --- | --- |
| Produktions-Resolver mit ersetzter externer Kartenlesegrenze | F1 reproduziert: tatsächlicher Lesepfad `map.map`, anschließend Multiplayer-Fehlermeldung. Keine Spiel-/Unity-Initialisierung ausgeführt. |
| Resolver-Prototyp mit genau einer Änderung: zweiter Pfadparameter ebenfalls absolut | Derselbe Fall erfolgreich; Produktionsquelle unverändert. |
| Produktions-Resolver für Custom-AIC ohne explizite AIV-Liste | Ergebnis `builtInLord=false`, `builtIn=false`, leere AIV-Liste; genau dieser Zustand erreicht die ablehnenden Coop-Prüfungen. |
| Unverändert extrahierter Produktionskörper von `CopySidecars` | `.modtrail.json` wird kopiert, `.lordrequirements.json` nicht. Dies prüft die Methode, nicht den vollständigen UI-Import. |
| Alle 40 Trails mit vorhandenem Container-/Restart-Codec | Alle lesbar, alle Restart-Version 60, alle Kartentyp 1. Quellen: 7 Built-in, 19 lokal, 14 Workshop. |
| Vorhandener Kartenextraktionsansatz auf allen 40 Dateien | Sektionsverzeichnis, Nutzdaten und Dateiende unverändert. Wiedereinsetzen des originalen Restart-Blocks ergibt bei 40/40 die ursprüngliche Datei bytegenau. Noch kein nativer Lade-/Spielnachweis. |
| Porträts | 81 belegte KI-Slot-Vorkommen mit eingebetteten Bildbytes, keine Aussage über 81 verschiedene Lords. Sir Otto: 4 AIVs, Porträt 53.777 Byte, Bild bytegleich zum Download. |
| Mission 4 gegen nachgereichte Originalkarte | Beide 800 × 800, je 175 Sektionen; 12 dekomprimierte Sektionen unterscheiden sich. Daraus folgt noch keine nachgewiesene Gameplay-Änderung. |
| Vorhandene ExtendedData-Tests | 74/74 bestanden beim unveränderten Gegenlauf außerhalb der Sandbox. Der erste Sandbox-Lauf scheiterte bei Datei-Move/Replace-Zugriffen und ist kein zusätzlicher Produktbefund. |
| Vorhandene APIShared.Content-Tests | 18/18 bestanden außerhalb der Sandbox. Der erste Lauf scheiterte an der Verbindung zwischen vstest und testhost; auch das ist kein Produktbefund. |

Details: [Beobachtungen](.work/a/ctfix/001/observed.json), [Pfad-Prototyp und Sidecar-Prüfung](.work/a/ctfix/001/prototype.json), [ExtendedData-Testlog](.work/a/ctfix/001/extendeddata-tests.log), [Content-Testlog](.work/a/ctfix/001/content-elevated.log) und [Prüfmetadaten](.work/a/ctfix/001/review.json). Die Beobachtungen enthalten SHA-256 der maßgeblichen Produktionsquellen. `Stubs.cs` ersetzt nur externe Grenzen; insbesondere behauptet der Resolver-Test keine reale Kartenlese- oder Bildladefunktion des Spiels.

### Vanilla-/Native-Verträge, auf denen die Pläne beruhen

1. `MapFileManager.GetFileInfoFromFileName(filePath, realFilePath, folderType, ...)` öffnet **realFilePath**. Dateiname, Dateipfad und Anzeige werden ebenfalls daraus abgeleitet. Die Methode gibt bei vielen Lese-/Formatfehlern nur `null` zurück. `mapType == 1` ist die gemeinsame Skirmish-/MP-Kategorie, kein zusätzlicher MP-Freigabeschalter innerhalb der vorliegenden Karten.
2. Bei `folderType == 4` decodiert die Methode den Restart-Block mit `customTrailMission=true`. Dabei darf die Originalkarte fehlen; `selectedHeader` kann trotz gültiger eingebetteter Missionsdaten null bleiben. Erst ExtendedDatas anschließende Pflichtprüfung blockiert diesen Zustand.
3. `CustomLordConfig` und `CustomAIV` serialisieren Nutzdaten, Namen und Checksums, aber keine verlässliche Herkunftsinstallation. Pfad und Workshop-Flag gehen beim Trail-Roundtrip verloren. Ein leerer Lordname mit Custom-AIC eines Standard-Lords ist zulässig. Gleichnamige lokale und Workshop-Pakete sind deshalb nicht ohne Zusatzprüfung austauschbar.
4. `EditorDirector.SaveCustomTrailMap` lädt die Originalkarte im Editor-Modus und ruft anschließend `CreateTrailMission` auf. Native `DLL_CreateTrailMission` bei RVA `0x816B0` (bestätigter Export) übernimmt Restart-Bytes in den Archivzustand und schreibt die geladene Karte. Die zwölf beobachteten Unterschiede können somit auch durch Laden/erneutes Schreiben entstehen. Sie beweisen weder einen defekten Trail noch absichtliche Kartenänderungen.
5. Die lokalen und Netzwerk-Coop-Starts rufen im Vanilla-Coop-Zweig den Standard-AIV-Upload auf und umgehen dabei die normale Custom-AIC-Übernahme. BugfixesAndQoL korrigiert diesen Aufrufpfad bereits über `UploadDefaultAivHook` und den vorbereiteten Start-Snapshot.
6. Bestätigte native Übergaben: Vorinitialisierung `0x86270`, AIV-Import `0x84850`, AIC-Übernahme `0x87350`, Kartenladen `0x84AC0`. Die Vorinitialisierung speichert Restart-Bytes; sie ersetzt die explizite AIV-/AIC-Übernahme nicht. AIV-Bankindizes sind 0-basiert, die AIC-Übergabe erwartet eine 1-basierte Spieler-ID. AIV-Import in einen bereits belegten Kandidaten ist kein sicherer allgemeiner Ersetzungsmechanismus; keine zweite Importphase auf bestehende Pointertabellen setzen.

Die relevante Kette umfasst Trail-Erzeugung, Header-/Restart-Lesen, Export, Assetauflösung, Auswahl/Snapshot, beide Startwege und die obigen nativen Übergaben. Für Kandidatenreihenfolge und Rotation gilt zusätzlich die bestehende [AIV-Baseline](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md). Neue native Hooks oder Offsets sind für die unmittelbaren Exportkorrekturen nicht erforderlich. Die native Ladeverträglichkeit extrahierter Karten und die fertige Multiplayer-Integration bleiben Spielabnahmekriterien.

## 2. Urteil über die acht ursprünglichen Punkte

| ID | Urteil nach Nachprüfung | Bedeutung |
| --- | --- | --- |
| F1 | Bestätigt und im Produktions-Resolver reproduziert | Falscher Lesepfad plus irreführende Meldung für gebündelte Karten. Das ist der stärkste direkte Erklärungsbefund für die gemeldete MP-Meldung. |
| F2 | Originalkarten-Abhängigkeit bestätigt; Gameplay-Folgerung eingeschränkt | Export verwendet die installierte Originalkarte und verlangt deren Header. Zwölf Sektionen Unterschied sind kein Beweis für einen spielerisch anderen Export. |
| F3 | Einschränkung bestätigt; pauschales Entfernen der Quellenprüfung wäre falsch | Eingebettete AIC/AIV werden nicht als Exportquelle genutzt. Zusatzpakete, `.modlord.json` und Fixes-Werte benötigen aber weiterhin nachgewiesene Herkunft oder vorhandene gespeicherte Anforderungen. |
| F4 | Bestätigt, zusätzlicher Codefall | Standard-Lord mit Custom-AIC und Standard-/Community-/Historical-Burgen kann wegen der AIV-/Namensprüfung scheitern. Kein Nachweis, dass gerade Mission 4 diese Variante verwendet. |
| F5 | Bestätigt | Import/Backup nehmen Lord-Anforderungen nicht mit; Reexport rekonstruiert sie aus der aktuellen Installation. Betrifft nicht unmittelbar die gelieferten Dateien, denn diese enthalten keine `.lordrequirements.json`. |
| F6 | Bestätigt; Wirkung liegt weiter in ExtendedData | Gleichnamiger Konfigurationswechsel bleibt als eingebettet klassifiziert. `PrepareTrail` kombiniert dann die aktuell gewählte Konfigurationsidentität mit den alten Zusatzwerten. Der neue APIShared-Leser kann diese falsche Zusammenstellung nicht nachträglich erkennen. |
| F7 | Bestätigt | Coop-Export/-Resolver erhalten eingebettete Porträts nicht. Optionale Medienanbieter sind kein Ersatz für bereits vorhandene Bildbytes. |
| F8 | Standalone-Lücke bestätigt; keine pauschale Aussage über das vollständige Modpack | BugfixesAndQoL besitzt Startkorrektur und zusätzliche AIV-Synchronisierung. APIShared hat diese Komponenten bislang nicht übernommen. ExtendedData deklariert BugfixesAndQoL nur optional. |

Zusätzliche Einschränkung der Datenauswertung: Die vier Downloads bilden zusammen die Missionsnummern 1–40. Die Sidecars für 21 und 32 liegen jeweils im benachbarten Downloadordner statt neben der zugehörigen `.trail`. Das ist eine Eigenschaft dieses Eingabestands, kein bewiesener Codefehler. Vor einem vollständigen Import die Zuordnung kontrolliert zusammenführen; die Originaldownloads erhalten.

## 3. Was bereits in APIShared liegt und was dorthin gehört

Maßgeblich sind [Architektur](APIShared/ARCHITECTURE.md), [Katalog](APIShared/docs/API_CATALOG.md), [Integration](APIShared/docs/THIRD_PARTY_GUIDE.md) und [Moddatenvertrag](APIShared/docs/MOD_DATA.md). Der ältere [Umzugsplan](ExtendedData-APIShared-Umzugsplan.md) ist Verlauf und Nachweis, keine zweite aktuelle Quelle für öffentliche Verträge.

### Bereits vorhanden: wiederverwenden

| Baustein | Zuständigkeit heute | Konsequenz für diese Fixes |
| --- | --- | --- |
| `APIShared.Content`: Namespace-Leser, `ModDataDocumentJson`, `MissionModSettingsJson` | Reine Daten-/Schemaalgorithmen ohne Spiel und Dateiauswahl | Keine Kopie und kein zweiter Parser in ExtendedData. |
| `ModDataAccess` | Aktuelles Kartenarchiv, ausdrücklich lokale Lord-Sidecar und ein optionaler bestätigter Auswahl-Anbieter | Kein AIC/AIV-Resolver, kein Host-/Auswahlersatz. ExtendedData bleibt für den Inhalt seines Anbieters verantwortlich; F6 gehört deshalb dort korrigiert. |
| `MissionModSettingsPreparation`, `MissionModSettingsSession`, `MissionModSettingsCreatorRules` | Aufbereitung, Anwendung und Bereinigung temporärer Einstellungen | Neue Export-/Importpfade an die vorhandenen Dienste anbinden; keine alten privaten Implementierungen wiederherstellen. |
| `SaveLoadDialogEvents` | Gemeinsamer Lade-/Speicherdialog | Keine zusätzlichen Dialoghooks für die geplanten Trail-Korrekturen. |
| `IMissionLifecycleCapability`, Herkunftsregistrierung | Zentrale Missionsgrenzen und gemeinsame Herkunftsauswertung | Für Startversuch, Abbruch, Restart und Bereinigung verwenden. Keine zweite Missionserkennung. |
| `LobbyPreparationOverride` | Einmalige Vorbereitung einer geöffneten Skirmish-Lobby | Kein Ersatz für den späteren AIV-/AIC-Import beim Spielstart. Nicht für F8 zweckentfremden. |

### Empfohlene zusätzliche gemeinsame Bausteine

**A. Kleiner Trail-Containerleser in APIShared.Content.** Er soll aus caller-eigenen Bytes beziehungsweise Streams den Restart-Block lesen und eine Karte ohne diesen Block extrahieren. Kein Zugriff auf installierte Maps/Lords, keine Unity-Bilder, kein Exportverzeichnis, keine Paket-/Hostentscheidung. Dafür gibt es zwei sinnvolle Verbraucher: ExtendedData und das unabhängige TrailEditor-Werkzeug. Den geprüften Algorithmus aus `Helpers/TrailEditor/TrailEditor.Core/TrailContainerCodec.cs` und die dafür nötigen begrenzten Preamble-Prüfungen übernehmen, nicht den gesamten MapParser, AICDecoder oder dessen CLI-/Serilog-/System.Text.Json-Abhängigkeiten.

**B. Gemeinsame Coop-AI-Startvorbereitung in APIShared, wiederverwendbarer Importkern.** Sie nimmt einen vollständig vorbereiteten, unveränderlichen Plan entgegen und führt die AIV-/AIC-Übernahme am bereits auditierten Startpunkt aus. Der Plan enthält Spieler-/Bankzuordnung, Standard- oder explizite Kandidatenquelle, deren Reihenfolge, Rotations-/Selektorwerte und gegebenenfalls AIC-Daten. Der Verbraucher muss Sitzung, Auswahl und Host-Autorität bereits bestätigt haben. Fehlende Bestätigung darf APIShared nicht durch eine lokale Datei ersetzen.

Die Hookstelle des Standard-AIV-Uploads bekommt genau einen gemeinsamen Besitzer. Die bestehenden Startstellen der Lifecycle-Capability koordinieren Beginn, Ende und Fehler eines Versuchs. BugfixesAndQoL und ExtendedData liefern Daten beziehungsweise nutzen denselben Kern, statt konkurrierende Hooks zu installieren. Doppelte Zuständigkeit für denselben Spieler ist ein Fehler und keine implizite Prioritätswahl. Vanilla ohne aktiven vorbereiteten Plan bleibt unverändert.

Die Netzwerkübertragung vollständiger Kandidatenlisten ist ein eigener Teil dieses Umzugs: Das bestehende Bestätigungs-/Digest-/Mehrkandidatenprotokoll aus BugfixesAndQoL darf nicht durch einen lokalen Import ersetzt werden. Für normale Lobbys bleibt sein bisheriger Verbraucher zunächst zuständig; für ExtendedData-Pakete kann der geprüfte, auf allen Peers identische Paketinhalt die Kandidatenquelle stellen. Bei Customize muss die aktuelle vollständige Auswahl bestätigt sein. Eine solche Quelle darf nicht aus Kandidat 0 oder aus nur dem Lordnamen geraten werden. Der Umzug ist erst fertig, wenn die Standalone- und Customize-Matrix aus Abschnitt 5 besteht.

**C. Extender-Kompatibilität nur im zugehörigen APIShared-Bereich zentralisieren.** Der für den Import nötige, geprüfte Backend-Zugriff aus `ShcdeSeCoarseGridBufferWorkaround` gehört beim gemeinsamen Import nach APIShared. Seine unbeteiligten Stone-Reserve-/Live-Village-Layoutzugriffe nicht mitverschieben. Ob der frühere Mono-TypeLoad-Fehler auf dem tatsächlich gewählten SDK noch auftritt, vor Umsetzung prüfen; keine historischen Strukturgrößen oder Offsets als aktuelle Voraussetzung übernehmen. Offizielle Extender-API bevorzugen; einen nachweislich noch nötigen Ersatz getrennt prüfen und dokumentieren.

### In ExtendedData beziehungsweise BugfixesAndQoL belassen

- ExtendedData: Coop-Paketschema, Katalog, Missionszuordnung, Fingerprints, atomare Veröffentlichung, Trail-Maker-Import/-Export, Sidecar-Lebenszyklus, Auswahl-/Herkunftspolitik, Lord-Paketäquivalenz, gespeicherte Zusatzwerte, lokalisierte Fehler und Benutzerentscheidungen. Der einzeilige Pfadfehler rechtfertigt keine allgemeine neue Map-API.
- BugfixesAndQoL: Custom-Partner-Auswahl, Coop-Historie, Porträt-Auswahloberfläche, Sichtbarkeit und Featurepräferenzen. Diese Bedienfunktionen gehören nicht in APIShared.
- Fixes-spezifische inhaltliche Korrekturen bleiben gemäß Workspace-Vertrag vorzugsweise in BugfixesAndQoL. Die vorhandene ExtendedData-Snapshotbrücke wird durch diesen Plan nicht als allgemeine APIShared-Fixes-Engine neu angelegt.

## 4. Konkrete Lösungspläne

### P1 — Pfad und Fehlerdiagnose korrigieren (F1)

1. Im gebündelten Kartenpfad beide Pfadargumente absolut übergeben: `GetFileInfoFromFileName(path, path, 0)`. `fileName`/Anzeige getrennt nach dem tatsächlichen Lesevertrag behandeln.
2. Einen fehlenden Header und einen gültig gelesenen falschen Kartentyp separat melden. Nicht die `mapType == 1`-Prüfung entfernen. Spieleranzahl weiterhin gegen die Karte prüfen.
3. Fehler um Paket, Missionsordinal, Assetpfad und Fehlerkategorie ergänzen. `Prepare` muss auch seine abschließende Resolver-Schleife mit Missionskontext kapseln; bisher erhält nur die frühe Definitionsbildung diesen Kontext.
4. Der Exportdialog zeigt die betroffene Mission und den konkreten Grund. Die vorhandene MP-Bestätigungsebene verwenden. Datei-/Ladefehler nicht als angebliche Unverträglichkeit mehrerer unbenannter Maps zusammenfassen.

**Nachweis:** Ein-Parameter-Prototyp besteht im isolierten Resolver. **Noch umzusetzen/zu testen:** Fehlermeldungen, beschädigte Datei, wirklich falscher Kartentyp, zu wenige Plätze, fremdes Arbeitsverzeichnis und zwei verschiedene gebündelte Dateien namens `map.map`.

### P2 — Gespeicherten Kartenstand als Quelle nutzen (F2)

1. Den Containerleser aus 3A hinzufügen. Er arbeitet ohne Spielinstallation, verwendet geprüfte Längen/Grenzen und gibt unabhängige Bytes zurück. Alle Blöcke außer Restart-Länge, Restart-Nutzdaten und dessen Terminator erhalten. Sektionsdaten und unbekanntes Dateiende nicht neu serialisieren.
2. `CreateDefinition` benötigt einen gültigen Container, Restart und Multiplayer-Setup, aber keinen auflösbaren `restart.selectedHeader`. Kartenmetadaten kommen aus dem Container. `selectedHeader` ist nur noch optionale Originalherkunft.
3. Jede exportierte Mission erhält eine aus ihrer `.trail` extrahierte gebündelte `.map`, auch wenn die ursprüngliche Karte Built-in war. Damit beeinflussen eine inzwischen geänderte oder fehlende Originalinstallation den Export nicht mehr.
4. DisplayName aus der gespeicherten Originalreferenz beziehungsweise vorhandener Anzeige ableiten; den technischen Exportnamen `map.map` nicht als Missionsnamen übernehmen. Ursprüngliche Quellenart und Originalname als Herkunft im Analyse-/Exportkontext erhalten.
5. Archiv-/Moddaten am Dateiende erhalten und mit dem echten Extender-Archivleser gegenprüfen. Ein erfolgreicher MapParser-Roundtrip allein beweist keine korrekten ZIP-/Archivzugriffe nach der Extraktion.

**Nachweis:** Der Extraktionsansatz besteht verlustfrei für 40/40 Eingaben. **Abnahme:** Native Ladepfade für extrahierte 800er-Karte, Built-in-Karte und Workshop-Karte, einschließlich Kartenmoddaten, Keep-Positionen und Editor. Die Semantik der zwölf Unterschiede nicht durch ein erwartetes Testresultat festschreiben.

### P3 — AIC/AIV und Bilder aus dem Trail erhalten, Zusatzanforderungen bewahren (F3/F7)

**Gewählter Ansatz:** Binäre Trail-Payloads erhalten. Kein Umweg über generierte `.lordjson`/`.aivjson`: Er könnte Namen, Checksums, Konfigurationsversionen, nicht öffentlich abbildbare AIC-Felder und Bilder verändern.

1. Das exportierte Missionsasset enthält zusätzlich eine unveränderte Kopie der jeweiligen Quell-`.trail`, gemeinsam für die KI-Slots dieser Mission. Die in P2 erzeugte `.map` bleibt der tatsächliche Kartenstartpfad.
2. Das aktuelle Coop-Missionsschema 4 wird um eine explizite neue Generation 5 erweitert. Geplante minimale Erweiterung: `LordReference.Source = "embeddedTrail"`, `File` zeigt auf die gebündelte `.trail`, eine additive Property `SourcePlayerId` bezeichnet den originalen 1-basierten Slot. `BaseLordId` bleibt die gespeicherte Laufzeit-Lordzuordnung; `Id` nicht als Quellslot zweckentfremden. Jeder KI-Spieler verweist so auf seinen vollständigen ursprünglichen `MPAIVInfo`-Zustand.
3. Für diese Quelle AIC, geordnete AIV-Liste, Flags, Rotation, Konfigurationsidentität und Bildbytes gemeinsam aus dem gespeicherten Slot lesen. Die vorhandene Vanilla-Decodierung mit `customTrailMission=true` kann runtime-seitig wiederverwendet werden; keine Originalkartenpflicht hinzufügen. Auf dem Main-Thread laden, Daten für spätere Mutationen unabhängig kopieren. Standard-Konfigurationstyp und dynamische Custom-Lordnummer nicht gleichsetzen.
4. `MissionLoader` validiert den neuen Dateityp, Quellslot, aktiven KI-Slot und die Übereinstimmung seiner gespeicherten Lordzuordnung. `ResolveBundledFiles`, Paketfingerprint und Workshop-Staging erfassen auch `.trail`. Widersprüchliche zusätzliche explizite AIV-Referenzen in einer `embeddedTrail`-Referenz ablehnen. Das bestehende `NativePreferredAiv` muss unverändert zum gespeicherten Setup passen; keine neue Kandidatenreihenfolge oder zufällige Ersatzwahl erzeugen.
5. Schema-4-Pakete bleiben mit ihrem bisherigen Lesepfad unterstützt. Schema 5 ist eindeutig versioniert und wird von älteren Lesern erkennbar abgelehnt. Kein stiller Wechsel der Bedeutung vorhandener Schema-4-Felder. Öffentliche Modellidentitäten erhalten; Erweiterungen additiv, beide erlaubten Schemata testen.
6. Vorhandene, an die Mission gebundene `.lordrequirements.json` ist die Quelle für Zusatzwerte und zusätzliche Installationspflichten. Nach erfolgreicher Identitätsprüfung ihre Slots übernehmen und für Coop umnummerieren; einen neuen Missionsdigest für die neue JSON-Datei berechnen. Keine gespeicherten Werte durch aktuelle lokale Werte überschreiben.
7. Fehlen gespeicherte Anforderungen, bleibt die reguläre Autor-Erfassung mit überprüftem Paket zulässig. Sie entscheidet nur über Zusatzdaten und Pflichtpakete; AIC/AIV selbst kommen weiterhin aus der `.trail`. Eine lokale Kopie mit gleichem Namen/Checksum beweist nicht automatisch gleiche Skripte oder Fixes-Werte.
8. Sind Zusatzdaten unbekannt und keine Quelle belegbar, standardmäßig mit genauer Missions-/Lordangabe abbrechen. Eine ausdrücklich gewählte Wiederherstellung „nur eingebettete Vanilla-Daten“ darf als separate Authoring-Entscheidung angeboten werden. Sie darf erst nach dieser Auswahl weitergehen, muss den Verzicht auf unbekannte Zusatzwerte erklären und als Exportnachweis speichern. Niemals aus „nicht gefunden“ automatisch `RequiresInstalledPackage=false` ableiten. Eine vorhandene, beschädigte oder widersprüchliche Anforderungsdatei nicht über diesen Weg stillschweigend ignorieren.
9. Porträtbytes aus dem eingebetteten Slot in den aufgelösten Lord und die vorhandene Lobbyübertragung übernehmen. Fehlerhafte optionale Bilder diagnostizieren und mit bestehender Standarddarstellung weiterarbeiten; AIC/AIV und Pflichtpakete davon unabhängig prüfen. Medienalias-Auflösung darf vorhandene eingebettete Bilder nicht löschen.

**Prüfungen:** Sir Otto ohne installierte Original-Lorddateien bei gültigen gespeicherten Anforderungen; alle vier AIVs in Reihenfolge und Bildhash gleich. Dazu Standard-Lord mit Custom-AIC und leerem Namen, lokale/Workshop-Aliase, zwei Konfigurationen desselben Lords, fehlendes Pflichtskript, unverfügbare optionale Medien und Legacy-Trail ohne Zusatznachweis. Die Standard-Extended-Lord-Pakete sind durch den nachgereichten `CustomLords`-Ordner nicht vollständig belegt; für sie keine Zusatzfreiheit behaupten.

### P4 — Custom-AIC und Standardburgen unabhängig behandeln (F4)

1. `builtInLord` beschreibt die AIC-Quelle; `builtIn`/`community`/`historical` beschreiben die Burgenquelle. Diese beiden Dimensionen weder im Resolver noch in den Startprüfungen miteinander gleichsetzen.
2. Eine gültige Custom-AIC eines bekannten Standard-Lordtyps darf ohne explizite AIV-Liste den regulären Standard-/Community-/Historical-Upload nutzen. Danach die Custom-AIC genau einmal vor dem Kartenladen übergeben. Für einen benannten Custom-Lord ohne gültige Burgquelle weiter abbrechen; keine passende Burg erfinden.
3. Im alten JSON-Pfad ohne AIV-Referenzen den dokumentierten Standard-AIV-Zustand ausdrücklich setzen; `MPAIVInfo.Init(nonEmptyName)` stellt ihn nicht automatisch her. Im eingebetteten Pfad alle gespeicherten Flags erhalten.
4. `ValidateSinglePlayerCoopAivs`, `ValidateCustomCoopLobby`, `BeginCoopStart` und den Uploadkern zusammen korrigieren. Leere Namen eines gültigen Standard-Lords mit Custom-AIC sind zulässig. Ein fehlendes AIC-Objekt oder eine ungültige explizite AIV bleibt ein Fehler.
5. Kandidatenreihenfolge, bevorzugte native AIV und Rotationssemantik erhalten; keine Materialisierung/Auswahl, die einen Selector versehentlich auf einen anderen Kandidaten bezieht.

**Prüfungen:** Custom-AIC × Standard-/Community-/Historical-/explizite Burgen, jeweils lokal und im echten MP; Negative für benannten Custom-Lord ohne Burgquelle. Importrekorder prüft AIV-Quelle, genau eine AIC-Übernahme, Spieler-/Bankbasis und Zeitpunkt vor `loadMultiplayerMap`.

### P5 — Sidecar-Lebenszyklus und Reexport erhalten (F5)

1. Import, Backup, Clear, Orphan-Bereinigung und Export behandeln `.modtrail.json` und `.lordrequirements.json` gemeinsam mit der zugehörigen `.trail`. Keine ungebundenen Zusatzdateien aus benachbarten Missionen übernehmen.
2. Vor dem Import Anforderungen gegen die Quellmission prüfen. Namenskollisionen und vorhandene uncommittete Nutzerinhalte unverändert erhalten. Ein Fehler darf nicht als erfolgreicher vollständiger Import angezeigt werden.
3. Beim Export dieselbe tatsächliche Quell-/Zielordinal-Zuordnung für Trail, Modsettings und Lord-Anforderungen nutzen. Maker-Sidecars behalten originale Spieler-IDs; die spielbare Coop-Datei erhält die bereits bestehende kompakte Umnummerierung nach Entfernung der zwei menschlichen Slots.
4. Einen vorhandenen gültigen Snapshot bewahren. Bei echten Inhaltsänderungen nicht nur dessen Digest „passend machen“: zuerst gebundene Konfigurations-/AIV-Identitäten prüfen und die passende Authoring-Erfassung auslösen oder gezielt abbrechen. Eine reine Dateiumbenennung verändert die Trail-Bytes und ihren Digest nicht.
5. Anforderungen und bearbeitbare `TrailMakerSource` im Staging vollständig vorbereiten, bevor das Ziel ersetzt wird. Der bestehende atomare Publisher schützt derzeit `CoopMissions` und Manifest, nicht automatisch sämtliche vorher geschriebenen Maker-Quelldateien. Seine Rollback-Grenze gezielt auf den tatsächlich publizierten Gesamtzustand erweitern; keine generelle Dateitransaktions-API dafür nach APIShared verschieben.

**Prüfungen:** Import → Backup → Clear → Wiederherstellung → Reexport, Nummernlücken, Slots mit deaktivierten Spielern, vorhandene unveränderte Zusatzwerte trotz abweichender lokaler Installation, beschädigter Digest, Kollision und injizierte Fehler vor/nach Veröffentlichung. Alte Daten und historische Nachweise dürfen nicht verschwinden.

### P6 — Konfigurationsidentität statt nur Lordname prüfen (F6)

1. Die internen Aufrufer verwenden eine vollständige Zuordnung aus gespeichertem Konfigurationstyp, Konfigurationsname und Checksum sowie der ursprünglichen Lordidentität. Medienalias allein identifiziert keine AIC. Gleiche Namen bei anderer Konfiguration ergeben eine ersetzte Auswahl.
2. Einen neuen ausreichend parametrisierten Vergleich beziehungsweise Overload hinzufügen. Alte öffentliche `TrailLordSelectionPolicy`-Overloads erhalten, aber für die sicherheitsrelevante Snapshotwahl nicht mehr verwenden. Ihre bisherigen Verträge nicht unbemerkt ändern.
3. Die Zuordnung sowohl auf Host als auch Client, im normalen Custom Trail, im Coop-Setup und beim Restart aktualisieren. `TryInspectTrail` prüft die vollständige Bindung nochmals unmittelbar vor der Snapshot-Erzeugung.
4. Bei einer ersetzten Auswahl alte Modlord-/Fixes-Werte und Paketanforderungen dieses Slots nicht weiterverwenden. Für die neue Auswahl den vorhandenen Host-Snapshot-/Bestätigungspfad verwenden. Eine bereits bestätigte andere Auswahl darf nicht mit dem alten Payload neu beschriftet werden.
5. `ModDataAccess` und sein einzelner bestätigter Anbieter bleiben unverändert in ihrer Zuständigkeit. Es gibt keinen zweiten ausgewählten-Lord-Leser in APIShared und keinen lokalen Ersatz bei fehlender Hostbestätigung.

**Prüfungen:** `Phoenix` → `Phoenix_cheat` unter demselben Lordnamen, Zurückwechseln, gleiche Konfiguration mit anderem Medienalias, Standard-Lord mit leerem Namen, späte Bestätigung einer alten Auswahl, Restart sowie zwei gleichnamige Lords mit unterschiedlichen Fixes-Werten. Die generischen Moddaten-Tests müssen weiterhin bestehen.

### P7 — Versteckte Startabhängigkeit auflösen (F8; gemeinsamer Anteil aus F4)

1. Den in 3B beschriebenen Importkern aus der bisherigen BugfixesAndQoL-Zuständigkeit nach APIShared ausgliedern. Die bereits vorhandene Lifecycle-Hookstelle verwenden; den Standard-AIV-Upload nur einmal gemeinsam intercepten. Keine zusätzliche native Speicher-/Patchlogik und kein zweiter Startdetektor.
2. Den Plan für einen Startversuch vor der Simulation vollständig validieren und unveränderlich erfassen. Nach Beginn keine UI-/Dateisuche im Importcallback. AIV-Banken resetten beziehungsweise importieren genau gemäß der bestehenden Reihenfolge; keinen weiteren Reset nach bereits erfolgtem Import hinzufügen.
3. Standardquelle: Vanilla-Burgen laden und bei Custom-AIC anschließend deren Konfiguration übergeben. Explizite Quelle: Kandidat 0 und Zusatzkandidaten genau einmal in deterministischer Reihenfolge importieren, AIC vor dem Laden übernehmen. Ablehnung des Backends oder fehlender bestätigter Plan bricht diesen Custom-Start ab; nicht unbemerkt Vanilla-Gegner einsetzen.
4. ExtendedData liefert seine bestätigten eingebetteten Paketdaten. BugfixesAndQoL behält zunächst UI und Transportadapter, delegiert aber denselben Importkern. Gemeinsame Installation und Standalone dürfen keine doppelte Anwendung erzeugen. Konflikte um einen Slot vor Ausführung erkennen.
5. Während des Umzugs vorhandene Protokoll-/Bereitschaftsprüfungen erhalten. Mehrkandidaten-Customize im MP ist erst freigegeben, wenn alle Peers dieselbe aktuelle vollständige Liste bestätigt haben. Der Einzelkandidat aus Vanillas Lobbyserialisierung ist dafür nicht ausreichend.
6. APIShared öffentlich und für fremde GUIDs ohne Serps-Modpack verwenden können. Alte öffentliche APIs erhalten; Mindestversionen erst nach fertig getesteter Implementierung konsistent anheben. Keine neue harte BugfixesAndQoL-Abhängigkeit als Ersatz für diesen Plan einführen.

**Freigabekriterium:** ExtendedData mit ausschließlich seinen deklarierten harten Abhängigkeiten kann eine vorbereitete Custom-Coop-Mission korrekt spielen. Mit BugfixesAndQoL kommt derselbe Kern zum Einsatz. Die vollständige Customize-/Mehrkandidaten-Matrix muss ebenfalls bestehen, bevor dieser Umzug als abgeschlossen gilt.

## 5. Reihenfolge, Kompatibilität und Abnahme

### Umsetzung in überprüfbaren Schritten

1. **P1 isoliert:** Pfad und konkrete Fehleranzeige; kein neues Dateiformat. Damit wird der aktuelle Hauptfehler schnell separat prüfbar.
2. **P5 und P6:** Vorhandene Zusatzdaten und Auswahlbindung sichern, bevor ein neuer portabler Exportpfad sie verwendet.
3. **Gemeinsamer Importkern/P4/P7:** Standalone und Standardburgen korrekt behandeln; danach die Portabilität nicht mehr von einem optionalen Mod abhängig machen.
4. **Containerleser und P2/P3:** Schema-5-Payloadreferenzen, Kartenextraktion und Bilder gemeinsam einführen. Keine halbfertige Generation ausgeben, die neue Quellen exportiert, aber nicht laden kann.
5. **Spielabnahme und Veröffentlichungsvorbereitung:** erst nach bestandener Matrix Versionen ändern. Keine Builds, Installation, Commits oder Veröffentlichung sind durch dieses Planungsdokument bereits ausgeführt.

APIShared-Änderungen erfordern einen binären, Quell- und Verhaltenskompatibilitätsvergleich, aktualisierte öffentliche Baseline und die betroffenen Beispiele/Katalogeinträge. Ein neuer positiver Pflichtschritt in bestehenden Interfaces ist zu vermeiden. Bestehende Spieler-/Indexbasis, Thread, Zeitpunkt, Vanilla-Aufrufzahl und permanenter Hookbesitz sind Verträge. ExtendedDatas vorhandene öffentliche Core-/Facade-Identitäten erhalten. Das Paketformat wird ausdrücklich versioniert; die ältere Generation darf nicht stillschweigend anders interpretiert werden.

### Automatisierte Prüfungen der Umsetzung

| Bereich | Erforderlicher Nachweis |
| --- | --- |
| APIShared.Content | Keine Spiel-/Unity-/Extender-/CLI-Abhängigkeit; Containerlängen, fehlender Restart, unbekannter Container, überlaufende Größen, unveränderte unbekannte Blöcke/Tails, 40er-Roundtrip. |
| Export/Loader | Fremdes Arbeitsverzeichnis, gleichnamige Assetdateien, fehlende Originalkarte, Typ-/Platzfehler, Schema 4 unverändert lesbar, Schema 5 vollständig geprüft, neue `.trail`-Assets im Fingerprint/Workshop-Staging. |
| Zusatzwerte | Digest-/Identitätsbindung, Pflichtskripte, gespeicherte Snapshots statt lokaler Ersatzwerte, explizite Vanilla-only-Entscheidung, richtige Slot-/Ordinalabbildung, transaktionale Fehlerfälle. |
| Lord-Auswahl | Gleicher Name bei anderer AIC, Aliaswechsel ohne AIC-Wechsel, leere Standardnamen, veraltete Bestätigungen, Host/Client/Restart. |
| Startkern | Echte Produktions-Calltrees mit Import-/Enginegrenzen als Testdoubles: Reset, Kandidatenreihenfolge, Standardquelle + Custom-AIC, genau einmalige Übernahme, Abbruch und Cleanup, kein Einfluss auf Vanilla ohne aktiven Plan. |
| Bestehende Integrationen | Content-/API-Baselines, bestehende ExtendedData-Tests, betroffene Preset-/Persistenzfälle sowie Moddaten-Anbieter ohne Verlust von Host-Autorität. Relevante Shared-/XAML-/Locale-Prüfungen auf die tatsächlichen Änderungen begrenzen. |

### Spielmatrix vor Freigabe

- Mission 4 mit extrahierter Karte und Sir Otto: AIC/AIV/Porträt korrekt, alle vier Kandidaten verfügbar, Keep-Positionen und Rotation unverändert. Zusätzlich eine ursprüngliche Built-in- und eine Workshop-Karte ohne ihre Originalinstallation.
- ExtendedData + APIShared + Script Extender ohne BugfixesAndQoL; anschließend dieselben Fälle mit vollständigem Modpack und mit dem externen Fixes-Mod. Keine stillschweigende Abhängigkeit von Test-/Diagnosemods.
- Lokaler Coop und echter Zwei-Personen-MP. Auf beiden Seiten Paketfingerprint, aktuelle Lord-Konfiguration und vollständige Kandidatenliste nachweisen; Menüanzeige oder ein Hostlog allein genügen nicht.
- Host konfiguriert vor Clientbeitritt, Client startet mit abweichenden persönlichen Defaults; große Daten/Bilder und späte Bestätigungen; Auswahlwechsel und Missionswechsel; normale sowie Customize-Starts; Neustart, Save/Laden und Rückkehr in die Lobby.
- Standard-Lord mit Custom-AIC und Standard-/Community-/Historical-Burgen; fehlendes Pflichtpaket blockiert gezielt, fehlende optionale Medien blockieren nicht. Keine falsche MP-Inkompatibilitätsmeldung bei Datei-/Pfadfehlern.
- Trail-Maker/Editor: Import, Teststart, Bearbeiten, Speichern und erneuter Export erhalten die gewählten Quellen und Zusatzdaten. Nicht vorschnell zusätzliche Startressourcen auf Saves oder im Editor erneut anwenden.

Offen bleiben bis zur Umsetzung die produktiven Integrationstests des neuen Schemas/Startkerns und die genannte Spielmatrix. Die vorliegenden Nachweise bestätigen die Fehlerklassen und den verlustfreien Containeransatz; sie sind keine Behauptung einer bereits funktionierenden neuen Runtime.
