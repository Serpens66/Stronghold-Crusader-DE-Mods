# Sichere Workshop-Installation (Plan)

Stand 2026-11-18. Prototyp und Tests: `Testmods/WorkshopLoadProbe` (`TESTPLAN.md`),
Logauszüge in `_inspect/WorkshopLoadProbe/`.

## Ziel

- Eine halb installierte Workshop-Installation darf nicht entstehen können, auch nicht bei
  Absturz, Stromausfall, beendetem Prozess, Virenscanner oder einem zu frühen Spielstart.
  BepInEx lädt immer entweder die vollständige alte oder die vollständige neue Version.
- Von uns entfernte Dateien verschwinden beim Spieler. Vom Spieler geänderte
  Konfigurationsdateien werden nie überschrieben, außer der Mod-Autor verlangt das ausdrücklich.
- Für Mod-Autoren einfach: höchstens eine kleine Textdatei.
- Installiert wird nach `BepInEx/plugins`. BepInEx und Script Extender laden ganz normal, ohne
  Eingriffe in ihren Ladevorgang.

## Ablauf

1. **Erkennen:** Das APIShared-Plugin erkennt ein neues oder geändertes abonniertes Paket über
   den Katalog. Ein ausstehendes Steam-Update zeigt die `.acf` über
   `WorkshopItemsInstalled.manifest ≠ WorkshopItemDetails.latest_manifest`.
2. **Zustimmung** des Spielers per Dialog beim Start oder in den Mod-Optionen (E2). Angewendet
   wird beim nächsten normalen Spielstart, ohne erzwungenen Neustart (E1).
3. **Staging** durch das Plugin, im Hintergrund, auch während einer Partie, ohne `plugins` zu
   berühren:
   - Das Paket wird aus dem Steam-Ordner nach `BepInEx/cache/<APIShared>/s/<Paket>/<Version>.tmp`
     kopiert.
   - Geprüft werden die **Kopien** gegen das Manifest (Größe und SHA-256). Was Steam
     währenddessen im Workshop-Ordner tut, ist damit egal.
   - Die Keep-Liste wird ebenfalls geprüft; eine fehlerhafte Liste weist das Paket hier zurück.
   - Danach wird die Fertig-Markierung geschrieben und der Ordner einmal in seinen endgültigen
     Namen umbenannt. Ein Abbruch hinterlässt nur einen `.tmp`-Ordner, der nie verwendet wird.
   - Zum Schluss legt das Plugin eine Installationsanforderung ab.
4. **Umschalten beim nächsten Spielstart** im APIShared-Preloader
   (`APIShared.DependencyBootstrap`, `Finish`, vor dem Chainloader, also bevor irgendetwas aus
   `plugins` geöffnet ist):
   1. Journal schreiben.
   2. Spielerdateien nach der Keep-Liste in das Staging übernehmen.
   3. `plugins/<Ordner>` in die Sicherung umbenennen.
   4. Den Staging-Ordner nach `plugins/<Ordner>` umbenennen.
   5. Sicherung und Journal löschen.

   Jede Umbenennung ist atomar. Staging, Sicherung und `plugins` liegen auf demselben Laufwerk.
5. **Wiederherstellung:** Ebenfalls im Preloader und vor dem Umschalten. Für jedes offene Journal
   entscheidet der tatsächliche Zustand im Dateisystem: Existieren Ziel, Sicherung und Staging?
   Danach wird die Umschaltung abgeschlossen oder zurückgenommen. Erst dann lädt BepInEx.
6. **Ergebnis:** Das Plugin zeigt Erfolg oder Fehler aus dem Preloader-Protokoll an.

## Keep-Liste für Mod-Autoren (`apishared-keep.txt`)

Nach einem Update enthält der Mod-Ordner **genau** die Dateien des neuen Pakets plus die in der
Keep-Liste genannten Spielerdateien. Alles andere verschwindet mit dem alten Ordner. Von uns
entfernte Dateien müssen dafür nirgends eingetragen werden.

```
# apishared-keep.txt (optional, in jedem Mod-Ordner des Pakets möglich, Pfade relativ dazu)
settings.json        # Datei, bleibt bei Updates erhalten
config/              # ganzer Ordner (Schrägstrich am Ende)
presets.json 2       # Reset-Nummer: erhöhen = einmalig auf die mitgelieferte Version zurücksetzen
```

- Beim Update wird jeder Eintrag aus der Liste **des neuen Pakets**, der im alten Ordner
  existiert, übernommen. Die Spielerversion gewinnt auch gegen eine mitgelieferte Datei gleichen
  Namens. Bei Ordnern werden die Spielerdateien über den mitgelieferten Ordner gelegt.
- **Reset-Nummer:** Ohne Zahl gilt 0. Das Installationsprogramm merkt sich in
  `.apishared-keep.state` (erzeugt, nie von Autoren gepflegt), welche Nummer angewendet wurde.
  - Ist die neue Nummer höher, gilt einmalig die mitgelieferte Version. Liefert das Paket keine
    mit, entfällt die Datei bzw. der Ordner.
  - Übersprungene Versionen bekommen den Reset trotzdem genau einmal.
  - Fehlt der Merkstand, gilt 0.
- **Deinstallation (`persist`):** Ein Eintrag mit dem Wort `persist` (`config/ persist`) bleibt
  auch erhalten, wenn das Paket deinstalliert wird (Nutzerentscheidung 2026-11-18: der Mod legt
  das selbst fest). Ohne `persist` wird der Eintrag bei der Deinstallation mit dem Mod-Ordner
  gelöscht.
  - Der Mod-Ordner verschwindet bei der Deinstallation immer ganz aus `plugins`.
  - `persist`-Einträge werden vorher in eine APIShared-Ablage außerhalb von `plugins` gerettet.
  - Bei einer späteren Neuinstallation desselben Pakets werden sie wie Spielerdateien
    übernommen; danach wird die Ablage gelöscht.
- **Ungültige Einträge** weist das Staging zurück, bevor etwas umgeschaltet wird:
  - absolute Pfade, `..`, Laufwerksangaben,
  - Platzhalter (`*`, `?`),
  - doppelte Einträge.
- **Wird ein Eintrag aus der Liste entfernt**, verschwindet die Datei beim nächsten Update.
- **Wiederholung nach einem gescheiterten Versuch:** Das Ergebnis hängt immer nur vom aktuellen
  Stand des installierten Ordners ab. Vor jedem Versuch wird die Übernahme des vorigen Versuchs
  rückgängig gemacht: Die überschriebenen Originale liegen in `<Version>.undo` neben dem Staging.
- **Pflicht vor dem Umstieg:** Für jeden unserer Mods prüfen, wohin er schreibt, und die Liste
  füllen. Die Empfehlung für alle Autoren: Einstellungen unter Dokumente speichern, dann ist
  keine Liste nötig. Ein vergessener Eintrag kostet den Spieler diese Datei beim nächsten
  Update, die Installation bleibt aber intakt.

## Weitere Regeln

- **Pakete mit mehreren Wurzeln:** ein Journal pro Paket, das alle Wurzeln aufführt. Die
  Wiederherstellung bringt vor dem Laden alle Wurzeln auf denselben Stand.
- **Deinstallation** (deabonniert): derselbe Weg ohne neues Staging. Den Ordner in die Sicherung
  umbenennen, dann löschen, mit Journal.
- **APIShared selbst:**
  - Der Plugin-Ordner wird wie jedes Paket getauscht.
  - Die Preloader-DLL in `BepInEx/patchers` ersetzt sich per `File.Replace` (atomar, mit
    `.old`-Datei). Sie wirkt ab dem nächsten Start, die `.old` wird dann gelöscht.
  - Die Paarbindung zwischen Plugin und Bootstrap muss diesen einen Übergangsstart zulassen.
- **Fehlschläge:**
  - Lässt sich der installierte Ordner nicht umbenennen (eine Datei wird gehalten), bleibt alles
    unverändert, und die Anforderung wartet auf den nächsten Start.
  - Scheitert das Hineinbenennen des Stagings, wird sofort zurückgenommen.
  - Scheitert das Löschen der Sicherung, gilt die Installation als erfolgreich. Das Journal
    bleibt, das Aufräumen folgt beim nächsten Start.
- **Ohne Bootstrap** (fehlt oder ist abgeschaltet): Es wird nichts umgeschaltet. Die Anforderung
  wartet, und das Plugin zeigt einen Hinweis. Kein Rückfall auf den alten Updater (E3).
- **Nie in Workshop-Ordner schreiben:** Steam räumt fremde Dateien dort nicht weg.

## Was im heutigen System entfällt

- der externe Updater (PowerShell und `.sh`, `flock`, Host-Prozess unter Proton),
- das Warten auf das Spielende und die Readiness-Übergabe,
- Dateitransaktionen mit Sicherung pro Datei, das Fortsetzen (Resume) und die
  Wiederherstellungsdialoge,
- das Fortschrittsfenster,
- für das Löschen entfernter Dateien: Inventare, `LegacyFiles` und Pfadhistorie.

Eine „Neuinstallation“ heißt nur noch: Staging aus dem Steam-Ordner neu aufbauen.

## Belege (Prototyp, 2026-11-18)

| Test | Ergebnis |
|---|---|
| Offline, 27 Prüfpunkte (`tests/InstallHarness`) | Alle bestanden. Geprüft wurden: Staging, Wiederverwendung, falsche Quelle, Quelle ändert sich während des Kopierens, Absturz beim Kopieren, ungültige Keep-Listen, Erstinstallation, Update mit Keep-Liste (auch in einem Unterordner), Absturz nach jedem Schritt und während der Wiederherstellung, gesperrte Datei samt späterer Wiederholung, verzögertes Aufräumen, alte Sicherung, unvollständiges Staging, Wiederholung mit inzwischen geändertem bzw. gelöschtem Spielerstand, Reset einmalig, Reset eines nicht mitgelieferten Ordners und ein aus der Liste entfernter Eintrag. |
| Offline, 300 zufällige Prozessabbrüche beim Umschalten | Nie ein halber Stand. Nach der Wiederherstellung war immer die neue Version vollständig installiert, ohne Reste. |
| Offline, 40 Abbrüche beim Staging des echten SerpsMods-Pakets | Nie ein unvollständiger Stand veröffentlicht. |
| Zeit, SerpsMods 1.0.38 (936 Dateien, 64 MB) | Staging mit Prüfung 1,4–4 s (Hintergrund). Umschalten beim Start 8–430 ms (inkl. Löschen der Sicherung). |
| Spiel G1 Erstinstallation | Umschalten in 13 ms. BepInEx und Extender laden normal aus `plugins`. |
| Spiel G2 Update | `settings.json` und `config/` des Spielers bleiben erhalten. Die nicht gelistete Datei und die vom Paket entfernte Datei sind weg. |
| Spiel G3 Abbruch mitten im Umschalten | Der nächste Start schließt vor dem Laden ab, mit Spielerdateien. |
| Spiel G6/G7 Reset-Nummer | Bei erhöhter Nummer gilt einmalig der Standard, `config/` bleibt. Beim nächsten Update mit gleicher Nummer bleibt die Spielerversion. |
| Spiel G4 Selbstaustausch des Preloaders | Neue DLL ab dem nächsten Start, `.old` entfernt. |

Steam-Verhalten (privates Test-Item 3841874432):
- Abonnieren lädt sofort herunter, auch bei laufendem Spiel.
- Updates folgen der Update-Einstellung des Spiels. Neue Versionen erkennt Steam nur verzögert
  über seinen Änderungsfeed.
- Bei Updates löscht Steam entfernte Dateien, fremde Dateien lässt es liegen.
- Nach dem Deabonnieren bleibt der Ordner, solange das Spiel läuft, und verschwindet etwa 3 s
  nach dem Spielende.
- Geladene verwaltete DLLs sind unter Mono nicht gesperrt. Für diesen Plan spielt das keine
  Rolle, denn umgeschaltet wird nur vor dem Laden, und das Staging liest nur aus dem
  Steam-Ordner.

## Entscheidungen (vom Nutzer bestätigt am 2026-11-18)

- **E1 Zeitpunkt:** Eine vorbereitete und bestätigte Version wird automatisch beim nächsten
  normalen Spielstart angewendet. Ein sofortiger Neustart ist nicht nötig.
- **E2 Bestätigung:** Updates und Installationen bestätigt der Spieler weiterhin per Dialog,
  weil sie im Multiplayer die Versionen ändern. Es gibt keine automatische Übernahme.
- **E3 Ohne Bootstrap:** Fehlt der Bootstrap oder ist er abgeschaltet, gibt es nur einen
  Hinweis. Kein Rückfall auf den alten externen Updater.

## Integration in APIShared (Entwurf vom 2026-11-18)

- **Geltungsbereich:** nur lose Pakete mit Wurzeln unter `BepInEx/plugins` (z. B. SerpsMods).
  APIShared selbst kommt als Extender-`.map` und wird vom Script Extender aktualisiert. Ein
  Selbstaustausch des Preloaders ist daher nicht nötig, und die Paarbindung bleibt unberührt.
- **Kern** `src/Workshop/Install/`, kompiliert in `APIShared.dll` und in den Preloader:
  - Staging, Keep-Liste, Tausch und Wiederherstellung, **für mehrere Wurzeln pro Paket in einer
    Transaktion**.
  - Die Quittung `_APIShared/Workshop/Receipts/<id>.json` wird am Ende derselben Transaktion
    geschrieben, abgesichert über das Journal.
  - Arbeitsordner (gleiches Laufwerk wie `plugins`): `_APIShared/I/` mit `s` (Staging), `b`
    (Sicherungen), `j` (Journale), `r` (Anforderungen), `k` (`persist`-Ablage).
- **Preloader:** Wiederherstellung und Anforderungen in `Finish` des DependencyBootstrap, vor dem
  Chainloader.
- **Plugin:**
  - Zustimmungsdialog beim Start bzw. „beim nächsten Start installieren“ in den Mod-Optionen.
  - Danach Staging im Hintergrund und eine Anforderung. Angewendet wird beim nächsten Start.
  - Kein Beenden und kein Neustart des Spiels.
- **Öffentliche API:** Die Signaturen von `PrepareInstallAsync`/`ApplyAsync` bleiben. `ApplyAsync`
  stagt und legt die Anforderung ab, `AwaitingRestart` bedeutet „wird beim nächsten Start
  angewendet“, und das Spiel wird nicht mehr beendet. Das wird im Changelog und im Katalog
  dokumentiert.
- **Altlasten:** Reste abgebrochener Transaktionen des alten Updaters werden erkannt. Für die
  betroffenen Pakete wird die Neuinstallation über den neuen Weg angeboten, danach werden die
  Reste gelöscht.

## Phasen für APIShared

Stand 2026-11-18: Phasen 1–6 umgesetzt (APIShared 0.57.0, SerpsModsHost 1.0.39 mit
`SerpsModsHost/apishared-keep.txt`); der Prototyp-Kern wurde danach aus dem Testmod entfernt,
dieser steuert nur noch Spieltests (`Testmods/WorkshopLoadProbe/TESTPLAN.md`). Offen: Proton-Test
und Release.

1. **Kern übernehmen:** Staging, Keep-Liste, Umschalten und Wiederherstellung aus
   `install/SafeInstall.cs` als APIShared-interne Bibliothek für Bootstrap und Plugin. Dazu die
   Szenario- und Abbruchtests in die APIShared-Testsuite und den Build. Kurze Pfade unter
   `BepInEx/cache`.
2. **Preloader:** Wiederherstellung und Anforderungen im `Finish` des DependencyBootstrap,
   Protokoll für das Plugin, Selbstaustausch und Übergangsstart bei der Paarbindung.
3. **Plugin:**
   - Startup-Ablauf und Mod-Optionen schreiben Staging und Anforderung statt den externen
     Updater zu starten.
   - Ergebnisanzeige.
   - Mehrere Wurzeln pro Paket und Deinstallation.
4. **Unsere Mods:** prüfen, wohin jeder Mod schreibt. Danach Einstellungen nach Dokumente
   verlegen oder `apishared-keep.txt` ergänzen. Publisher und Doku für Mod-Autoren um die
   Keep-Liste erweitern.
5. **Entfernen:** externer Updater, Dateitransaktionen, Resume, Fortschrittsfenster,
   Readiness-Übergabe und Wiederherstellungsdialoge. Die Kompatibilität für veröffentlichte
   Pakete prüfen (APIShared ist kompatibilitätspflichtig).
6. **Tests:**
   - Spieltests mit dem echten APIShared-Preloader (Stand 2026-11-18, Item 3841874432 über
     `Testmods/WorkshopLoadProbe/tools/Stage-ApiSharedInstall.ps1`): Erstinstallation,
     Neuinstallation mit Fremddateien, echter Prozessabbruch bei `old-moved` mit Reparatur beim
     nächsten Start und Deinstallation bestanden. Der alte `_APIShared/T`-Ordner wurde beim
     ersten Start entfernt. Manuell geprüft (gleicher Tag): Zustimmungsdialog beim Start (SerpsMods
     gelöscht bzw. eine Datei fehlte → Neuinstallation angeboten, „Ja“, beim nächsten Start
     installiert) und Mod-Optionen (vorbereiten, verwerfen, erneut vorbereiten, installiert). Dabei
     gefunden und behoben: Das Popup lag hinter dem Spiel, ohne sichtbaren und mit eingesperrtem
     Mauszeiger. Es gehört jetzt dem Spielfenster, Zeiger und Mausbereich werden währenddessen
     freigegeben. Offen: Keep-Liste mit einem echten Workshop-Item.
   - **Proton-Test** (Umbenennungen unter Wine) steht noch aus.
   - Doku (`APIShared/docs/WORKSHOP.md`).
7. **Release** über den normalen Ablauf.

## Risiken

- **Proton/Wine:** Atomares Umbenennen von Ordnern ist noch nicht live geprüft.
- **Dauerhaft blockierender Virenscanner:** Das Umschalten wird jeweils verschoben, Updates
  kommen dann nie an. Nach mehreren Fehlversuchen einen Hinweis zeigen.
- **Speicherplatz:** Staging und Sicherung brauchen kurzzeitig den Platz einer weiteren Kopie pro
  Paket.
- **Noch nicht gebaut:** mehrere Wurzeln pro Paket, Deinstallation und die Paarbindung des
  Bootstraps. Der Prototyp behandelt einen Ordner pro Paket.
