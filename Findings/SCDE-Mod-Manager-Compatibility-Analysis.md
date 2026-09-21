# SCDE Mod Manager: Kompatibilitätsanalyse

## Zweck und geprüfter Stand

Dieses Dokument ist die interne technische Analyse. Die versandfertige englische Änderungsanfrage an den Manager-Autor steht getrennt in `SCDE-Mod-Manager-Author-Request.md`.

Geprüft wurden:

- SCDE Mod Manager, Commit `918aa87`;
- offizieller Script Extender 2.8.0;
- Fixes 1.18.1;
- SerpsMods 1.0.14;
- die aktuell installierte `Assembly-CSharp.dll` mit SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.

Die im Manager gebündelte Script-Extender-Version 2.6.0 ist nur die Bootstrap- beziehungsweise Fallback-Ausgangsversion. `release-updates.js`, `se-core-package.js` und `se-core-updater.js` laden neuere offizielle Releases automatisch, bereiten sie separat vor und tauschen anschließend das installierte Manager-Paket aus. Der Early-Managed-IL-Guard wurde strukturell erfolgreich gegen die installierte SE-2.8.0-DLL geprüft: fünf Präfix-Instruktionen wurden ergänzt und der ursprüngliche Methodenkörper blieb erhalten.

Diese Arbeit betrifft Deployment, Paketierung und Managed-Interop. Eine Native-Vanilla-Analyse war dafür nicht erforderlich.

## Gegenwärtiges Kompatibilitätsurteil

Fixes 1.18.1 und SerpsMods 1.0.14 sind mit dem gegenwärtigen Importpfad kompatibel:

- `SerpsMods.map` wurde mit der angehängten-ZIP-Logik des Managers gelesen: 840 Archiveinträge, effektive verschachtelte SE-Mindestversion 2.7.1 und keine unzulässigen Manager-Systempfade.
- `fixes.map` wurde mit derselben Logik gelesen: 16 Archiveinträge, effektive SE-Mindestversion 2.5.0 und keine unzulässigen Manager-Systempfade.
- SE 2.8.0 besitzt weiterhin die vom Manager-Kompatibilitätsplugin reflektierten Verträge `MapModManager.TryUpdateModsFromRemote` und `GameAssetModManager.Instance.GetRegisteredAssetDirectories()`.
- Der aktuelle Managed-Hash entspricht dem vom geprüften Manager-Build erwarteten Spielstand.

Die konkrete Installation ist jedoch bereits modifiziert und enthält unter anderem Script Extender, Fixes, APIShared, SerpsMods-Plugins und SerpsModsHost. Dadurch trifft das nachfolgend beschriebene Stage-Problem auf die vorhandene Installation tatsächlich zu.

## Bereits vorhandene Schutzmechanismen

Folgende Funktionen sind schon implementiert und dürfen in einer Autorenanfrage nicht als vollständig fehlend dargestellt werden:

- `deployer.js:103-137` sichert `BepInEx/config` vor einem vollständigen Neuaufbau und behält die Sicherung bei einem Fehler.
- `deployer.js:163-204` erkennt normale Dateikonflikte. Identische Dateien werden nicht erneut kopiert; unterschiedliche Inhalte folgen bewusst der Modreihenfolge.
- `renderer/app.js:243-257` zeigt diese Konflikte an, einschließlich Gewinner und überschriebener Mod. Ein generelles Verbot normaler Modkonflikte wäre daher weder nötig noch mit dem bestehenden Ordnungsmodell vereinbar.
- `se-core-updater.js` besitzt bereits einen getrennten Download-/Ready-Bereich, einen protokollierten Verzeichnistausch, Backup, Wiederanlaufbehandlung, Rollback und Reapply.
- `EarlyManagedGuard.cs` prüft Identität und Version der SE-Assembly sowie die Form von `TryUpdateModsFromRemote`, schreibt in eine neue Datei und validiert den erzeugten IL-Präfix.
- `se-compatibility.js` prüft beim Start beziehungsweise manuellen Versionswechsel die SE-Grenzen des Pakets und verschachtelter `info.json`-Komponenten.
- Das Multiplayerprotokoll SCDEMM3 erfasst über `ScriptExtenderBridge.Read` bereits tatsächlich geladene BepInEx-Plugins und registrierte SE-Komponenten anhand GUID und Version; als client-only deklarierte SE-Assets werden aus dem strikten Profil ausgeschlossen.
- `.scdemod`-Importe werden in einem temporären Ordner vorbereitet und atomar gegen eine bestehende Paketversion ausgetauscht. Der Archivhash wird beim Installieren als `packageSha256` geschrieben.

## Bestätigte Probleme und sinnvollste Umsetzung

### 1. Modifizierte Quellen vererben nicht verwaltete Plugins

Bewertung: **beibehalten, hohe Priorität**.

`prepareStage` löscht die Stage und kopiert anschließend mit `fs.cp(gameDir, stageDir)` die gesamte ausgewählte Originalinstallation. Erst danach werden Manager-Pakete darübergelegt. Bereits vorhandenes `BepInEx`, Doorstop, Script Extender oder andere Plugins werden damit Teil der Stage, ohne in `enabledMods`, `activeFiles` oder dem Deploymentprofil aufzutauchen.

Das Problem besteht auch beim späteren Deaktivieren: `applyMods` stellt frühere aktive Pfade aus `gameDir` wieder her, falls sie dort existieren. Bei einer modifizierten Quelle kann deshalb gerade die nicht verwaltete Originaldatei erneut in die Stage gelangen.

Sinnvollste Umsetzung:

- Eine gemeinsame Funktion klassifiziert managerverwaltete Laufzeitpfade.
- Beim initialen Kopieren wird mindestens der vollständige Quellordner `BepInEx` ausgeschlossen.
- Root-Dateien werden nicht über eine unvollständige Handliste bestimmt, sondern soweit möglich aus den Payload-Inventaren der erforderlichen Systempakete abgeleitet. Dazu gehören aktuell `.doorstop_version`, `doorstop_config.ini`, `winhttp.dll`, `msvcp140.dll` und weitere zukünftig systemeigene Root-Dateien.
- Dieselbe Policy gilt beim Wiederherstellen früher aktiver Dateien: Ein managerverwalteter Laufzeitpfad wird entfernt beziehungsweise ausschließlich aus einem aktiven Manager-Paket hergestellt, niemals aus der Originalinstallation zurückkopiert.
- Optional kann der Manager eine modifizierte Quelle bereits bei der Auswahl deutlich melden. Eine vollständige Ablehnung ist eine sichere Alternative, aber weniger benutzerfreundlich als die bereinigte Kopie.
- Die Originalinstallation bleibt unangetastet.

Nur bekannte Doorstop-Dateien zu löschen wäre weniger langlebig als eine aus den Systempaketen abgeleitete Policy. Nur vor dem ersten Kopieren zu bereinigen wäre unvollständig, weil der Redeploy-Wiederherstellungspfad dasselbe Problem erneut erzeugen kann.

### 2. Persistente Daten außerhalb `BepInEx/config` gehen verloren

Bewertung: **beibehalten, hohe Priorität, Cache-Forderung korrigieren**.

Der vollständige Stage-Neuaufbau erhält derzeit nur `BepInEx/config`. Der Script Extender speichert Lobby-Einstellungen jedoch neben der jeweiligen Pluginassembly unter `LobbyModSettings/*.msgpack`. Fixes speichert `hopsFarmWhitelist.json` unter seinem Pluginordner in `data`.

`aobcache.json` ist dagegen ein regenerierbarer AOB-Cache. Er soll ausdrücklich nicht als Benutzerdatum migriert werden. Die frühere Forderung, ihn zu erhalten, war sachlich falsch und widersprach dem allgemeinen Cache-Ausschluss.

Sinnvollste Umsetzung:

- `LobbyModSettings/**/*.msgpack` als enge, eingebaute Persistenzkonvention erhalten.
- Für zusätzliche Daten eine versionierte Manifestangabe wie `persistentPaths` einführen. Ein Eintrag ist relativ zum paket eigenen Payload, normalisiert, frei von Traversal und auf ausdrücklich erlaubte paket eigene Wurzeln begrenzt.
- Für bereits existierende Fremdpakete ohne solche Metadaten eine eng begrenzte Migration vorsehen; im geprüften Fixes-Fall ist `data/hopsFarmWhitelist.json` relevant, `data/aobcache.json` nicht.
- Nur Laufzeitdaten sichern, nicht pauschal ganze Plugin- oder `data`-Ordner. Sonst würden statische SE-Daten, veraltete Paketdateien oder eingeschleuste DLLs als angebliche Nutzerdaten überleben.
- Paketinhalt und persistenter Inhalt dürfen nicht denselben Zielpfad beanspruchen. Die Wiederherstellung muss dann mit verständlicher Diagnose abbrechen, statt eine Seite still zu überschreiben.
- Das vorhandene Config-Sicherungsmodell kann erweitert werden: Backup vor dem Stage-Löschen, Wiederherstellung nach Paketdeployment, Sicherung bei Fehler behalten. Ein kompletter atomarer Austausch der gesamten Spielkopie wäre zwar stärker, ist wegen Größe und doppeltem Speicherbedarf aber keine notwendige Mindestforderung.

### 3. Normale Pakete können Systemkomponenten überschreiben

Bewertung: **beibehalten, hohe Priorität; nur Systemkonflikte hart blockieren**.

Der allgemeine `.scdemod`-Parser erlaubt Payloads im gesamten Stage-Namensraum. Die Systempakete werden in der Managerreihenfolge zuerst eingeordnet; ein später aktiviertes normales Paket kann daher Systemdateien überschreiben. Die speziellere `.map`-/`.semod`-Verarbeitung schützt einige Pfade, der allgemeine `.scdemod`-Pfad besitzt aber keine gleichwertige Reservierung.

Sinnvollste Umsetzung:

- Normale Pakete dürfen weder BepInEx-/Doorstop-Runtimepfade noch Payloadpfade erforderlicher Systempakete besitzen.
- Die reservierte Menge soll aus Systempaket-Inventaren und wenigen strukturellen Wurzeln wie `BepInEx/core` gebildet werden, damit neue Manager-Systemkomponenten automatisch geschützt sind.
- Beim Import früh ablehnen und unmittelbar vor Deployment erneut prüfen. Die zweite Prüfung schützt gegen beschädigte beziehungsweise manuell veränderte installierte Paketordner.
- Unterschiedliche Bytes an einem reservierten Pfad sind immer ein Fehler; identische Bytes sollten ebenfalls nicht als geteiltes Eigentum gewöhnlicher Mods akzeptiert werden, solange kein ausdrückliches Shared-File-Modell existiert.
- Normale Mod-zu-Mod-Konflikte bleiben reihenfolgebasiert und sichtbar. Das vorhandene Konfliktpanel ist dafür bereits die passende Semantik.

### 4. Script-Extender-Kandidatenprüfung

Bewertung: **präzisieren, mittlere Priorität**.

Der Updater ist bereits transaktional und behält eine vorige Version als Backup. Eine Forderung nach einem grundsätzlich neuen Transaktionsmodell wäre daher unbegründet.

Die tatsächliche Lücke ist enger: `EarlyManagedGuard` prüft nur den von ihm veränderten Updater-Einstieg. Das Manager-Kompatibilitätsplugin reflektiert später weitere Typen und Member. Ändern sich diese, wird die Inkompatibilität erst im Spiel erkannt. Ebenso sollte eine Kandidatenprüfung feststellen, ob die Assembly mit dem fest eingebundenen BepInEx-Runtimepaket und den Manager-Systemkomponenten auflösbar ist.

Sinnvoll ist eine Erweiterung des bestehenden Cecil-Helfers oder ein zweiter read-only Kandidatenprobe, der vor dem Swap prüft:

- alle reflektierten Typen, Properties und Methoden mit ihrer erwarteten statischen/instanzbezogenen Form;
- die für Manager-Plugins notwendigen Assemblyreferenzen gegen das gebündelte BepInEx-Runtimeverzeichnis;
- die Identitäten und Mindestverträge der Manager-Systemkomponenten.

Ein unbekannter Kandidat bleibt im Ready-Bereich und die aktive Version unverändert. Die bereits vorhandene Backup-/Rollbacklogik bleibt bestehen.

### 5. Spielbuild und Harmony-Patches

Bewertung: **revidieren; Strukturprüfung vor pauschaler Hash-Allowlist**.

Der bekannte `Assembly-CSharp.dll`-Hash ist wertvolle Provenienz. Eine ausschließliche Hash-Allowlist würde aber auch harmlose Spielupdates vollständig blockieren und bei jedem Rebuild manuelle Freigabe verlangen.

Die Manager-Patches sollten stattdessen pro Patchgruppe vor `PatchAll` ihre tatsächlichen Verträge prüfen:

- Zieltyp und Methodensignatur müssen eindeutig vorhanden sein.
- Transpiler müssen die erwartete Zahl und Form der Ersetzungen finden; insbesondere darf der Sparse-Key-Patch nicht still mit null oder mehreren unerwarteten Treffern fortfahren.
- Bei unbekannter Struktur wird nur die betroffene Patchgruppe deaktiviert beziehungsweise der Multiplayerstart fail-closed verhindert und eine konkrete Diagnose ausgegeben.
- Der Assemblyhash wird protokolliert und bekannten Testständen zugeordnet, bleibt aber nicht der einzige technische Kompatibilitätsbeweis.

### 6. Gleichversionige Inhaltsänderungen

Bewertung: **beibehalten, mittlere Priorität**.

`findWorkshopUpdates` vergleicht ausschließlich Versionen. Der beim Import erzeugte `packageSha256` wird durch `validateManifest` beim späteren Einlesen nicht in das normalisierte Modobjekt übernommen. Ein vom Publisher ersetztes Workshop-Paket mit unveränderter Version wird deshalb nicht als Aktualisierung erkannt.

Sinnvollste Umsetzung:

- `packageSha256` als validiertes Installationsmetadatum erhalten.
- Kandidaten beim Update-Scan begrenzt und abbrechbar hashen.
- Gleiche ID und Version bei anderem Hash als „content changed“ anzeigen und atomar neu importieren; nicht als semantisch neuere Version ausgeben.
- Unveränderte Hashes nicht erneut importieren.

### 7. Manifest- und Abhängigkeitsvertrag

Bewertung: **beibehalten, mittlere Priorität**.

Der `.scdemod`-Vertrag ist derzeit nur durch Implementierung und Tests definiert. Abhängigkeiten kennen eine exakte `version` oder keine Versionsbedingung. Das reicht insbesondere für APIShared-Verbraucher nicht aus, die eine Mindestversion, aber nicht exakt eine einzige Version benötigen.

Eine versionierte Manifestrevision sollte ergänzen:

- dokumentierte ID-Ableitung und stabile ID-Semantik;
- `minimumVersion` und `maximumVersion` mit derselben Versionsvergleichsfunktion wie SE-Grenzen;
- Rückwärtskompatibilität: `version` bleibt exakt, ein fehlendes Versionsfeld bleibt unbeschränkt;
- Eindeutige Validierung, dass exakte Version und Bereich nicht widersprüchlich kombiniert werden.

### 8. Multiplayer zwischen Manager und Standalone

Bewertung: **beibehalten, aber an SCDEMM3 anpassen**.

SCDEMM3 ist bereits runtimebewusster als die frühere Analyse erkennen ließ. `ScriptExtenderBridge.Read` inventarisiert geladene BepInEx-Plugins und registrierte SE-Komponenten. `RuntimeProfile.Merge` hängt diese Runtimeeinträge jedoch an die Manager-Paketzeilen aus `active-mods.lobby` an. Außerdem verweigert `RefreshRuntimeProfile` ohne geladenes Managerprofil die Arbeit. Deshalb erhalten Manager- und Standalone-Installationen trotz gleicher geladener Runtime nicht denselben Vertrag.

Sinnvollste Umsetzung für eine neue Protokollversion:

- Der Netzwerkfingerprint besteht aus der normalisierten geladenen Runtime: GUID, Version und vorhandene NetworkMode-/Client-only-Semantik.
- Manager-Paket-ID, Wrapperversion, Archivquelle und Modreihenfolge bleiben im Deployment-Receipt, aber nicht im Netzwerkfingerprint.
- Fehlt `active-mods.lobby`, arbeitet das separat installierbare Kompatibilitätsplugin im Runtime-only-Modus. Die Manager-spezifische Blockade des SE-Autoupdaters bleibt weiterhin nur bei erkanntem Managerprofil aktiv.
- Besitzen beide Seiten das neue Protokoll, werden die Runtimeprofile strikt verglichen.
- Fehlt das Protokoll auf einer Seite, soll nicht fälschlich vollständige Gleichheit behauptet werden. Die vorhandenen Script-Extender-Prüfungen bleiben maßgeblich; der zusätzliche BepInEx-Anteil wird sichtbar als ungeprüft gemeldet. Das ist bewusst weniger strikt, ermöglicht aber die gewünschte Interoperabilität mit normalen Installationen.
- Ein Dateidigest ist keine sofortige Pflicht. Eine spätere Protokollrevision kann einen optionalen normalisierten Digest ergänzen, sobald klar definiert ist, welche Assemblys, privaten Abhängigkeiten und Assets netzwerkrelevant sind und welche Nutzerdaten, Übersetzungen und Caches ausgeschlossen werden.

SerpsModsHost führt derzeit eigene SE-Lobbyhash-Warnungen aus und verwendet einen weiterleitenden Lobby-Lifecycle-Hook. Ein konkreter unauflösbarer Konflikt mit dem Manager wurde nicht nachgewiesen. Eine stabile Readiness-/Interop-API wäre langfristig hilfreich, ist aber keine notwendige Korrektur für den aktuellen Manager.

## Neubewertung der bisherigen Forderungen

| Bisheriger Punkt | Entscheidung | Begründung |
|---|---|---|
| Modifizierte Quelle bereinigen oder ablehnen | Beibehalten | Reproduzierbarer unmanaged Runtime-Eintrag; initialer Copy- und Restore-Pfad betroffen. |
| `LobbyModSettings` und Nutzerdaten erhalten | Beibehalten | Tatsächliche persistente Dateien liegen außerhalb `BepInEx/config`. |
| `aobcache.json` erhalten | Verwerfen | Regenerierbarer Cache, kein Nutzerdatum. |
| Alle unterschiedlichen Modkonflikte abbrechen | Verwerfen | Normale Last-wins-Reihenfolge ist dokumentierte Managerfunktion und wird bereits angezeigt. |
| Systempfade hart reservieren | Beibehalten | Normale `.scdemod`-Payloads können derzeit Systempakete überlagern. |
| SE-Update transaktional machen | Verwerfen als neue Forderung | Transaktion, Backup, Recovery und Rollback existieren bereits. |
| SE-Kandidatenverträge vor Swap prüfen | Beibehalten, enger formuliert | Reflektierte APIs und Runtimeauflösung werden vom IL-Guard noch nicht vollständig geprüft. |
| Unbekannten vollständigen Spielhash generell blockieren | Revidieren | Struktur- und Signaturprüfungen sind zielgenauer; Hash bleibt Provenienz. |
| Gleichversionigen Inhaltswechsel erkennen | Beibehalten | Updateerkennung ist derzeit rein versionsbasiert. |
| Manifest versionieren und Versionsbereiche ergänzen | Beibehalten | Ermöglicht langlebige APIShared- und ähnliche Abhängigkeiten. |
| SCDEMM3 sei rein paketbasiert | Korrigieren | Es enthält bereits Runtimeeinträge, mischt sie aber mit Paketwrappern. |
| Runtime-Dateihash sofort verpflichtend machen | Zurückstellen | Der kanonische Satz netzwerkrelevanter Dateien ist noch nicht sicher definiert. |
| SerpsModsHost-Interop zwingend verlangen | Zurückstellen | Kein konkreter Konflikt nachgewiesen; sinnvoll nur als optionale spätere API. |

## Unsere Releasehärtung

Die Standalone-Releasepipeline erzeugt zusätzlich zum bestehenden Thin-ZIP ein `.scdemod`:

- stabile ID nach der aktuellen Managerregel `se-` plus die ersten 32 Hexzeichen von SHA-256 über die kleingeschriebene GUID;
- Payload ausschließlich unter `payload/BepInEx/plugins/<GUID>/`;
- SE-Metadaten mit GUID, Versionscheck-URL, Minimum, Maximum und `metadataVersion: 2`;
- harte Managerabhängigkeit auf Script Extender und bei APIShared-Verbrauchern zusätzlich auf die stabile APIShared-Paket-ID;
- keine Umwandlung von BepInEx-Soft-Dependencies wie Fixes in Manager-Hard-Dependencies;
- Ausschluss lokaler `LobbyModSettings`, Logs, temporärer Dateien und MessagePack-Laufzeitdaten;
- `.scdemod`-Hash und Metadaten in derselben Provenienz wie das Thin-ZIP.

ZIP-, Nexus- und Workshop-Ausgaben bleiben unverändert. `SerpsMods.map` bleibt der primäre Komplettpaketpfad.

Der eigene Validator bildet den aktuellen Manifest-, ID- und Pfadvertrag des Managers nach. Der originale JavaScriptparser konnte lokal nicht direkt ausgeführt werden, weil Node und die `node_modules` des Managerprojekts fehlen; dies bleibt eine Umgebungsgrenze, kein behaupteter erfolgreicher Originalparser-Test.

## Referenz- und Akzeptanztests

Aktuell bestätigt:

- Early-Managed-Guard gegen SE 2.8.0 strukturell erfolgreich;
- reflektierte SE-Registry-Verträge vorhanden;
- bekannter Managed-Hash passend;
- SerpsMods 1.0.14 und Fixes 1.18.1 mit angehängter-ZIP-Logik lesbar;
- keine reservierten Managerpfade in diesen beiden Workshop-Paketen;
- Standalone-Release-, Nexus- und Steam-Pack-Regressionen erfolgreich.

Für Manageränderungen erforderlich:

1. Eine modifizierte Quelle mit altem BepInEx, SE, Fixes und Fremdplugin darf nichts davon unverwaltet in die Stage übertragen oder beim Redeploy wiederherstellen.
2. `LobbyModSettings` und `hopsFarmWhitelist.json` überleben einen vollständigen Neuaufbau; `aobcache.json` wird nicht migriert.
3. Ein normales Paket mit einem Systempfad wird beim Import und bei einer nachträglich manipulierten Installation vor Deployment abgelehnt.
4. Normale Modkonflikte bleiben reihenfolgebasiert und werden weiterhin angezeigt.
5. Ein SE-Kandidat mit fehlendem reflektiertem Member bleibt unapplied; die aktive Version und ihr Backup bleiben intakt.
6. Ein unbekannter Spielbuild mit passenden Patchverträgen kann gezielt zugelassen werden; ein veränderter Zielvertrag stoppt nur die betroffene Patchgruppe mit Diagnose.
7. Gleichversionige, aber inhaltlich unterschiedliche Workshop-Pakete werden als Inhaltswechsel erkannt.
8. Alte exakte und unversionierte Abhängigkeiten bleiben gültig; neue Minimum-/Maximum-Bereiche werden korrekt aufgelöst.
9. Manager- und Standalone-Installation mit identischer Runtime erzeugen im neuen Protokoll dasselbe Profil; eine GUID- oder Versionsabweichung wird strikt erkannt.
10. Fehlt das offene Protokoll, bleibt der nicht prüfbare BepInEx-Anteil ausdrücklich als ungeprüft sichtbar.
