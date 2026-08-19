# Custom Custom Trail

`CustomCustomTrail` erweitert den eingebauten Traileditor um portable Koop-Trails und koordiniert missionsabhängige Einstellungen unterstützter Mods. Die aktuelle SHCDE-Version besitzt vier echte Koop-Trails mit jeweils zehn Plätzen; ein eigenes Paket kann daher bis zu 40 Missionen ersetzen.

Der Mod benötigt BepInEx und den SHCDE Script Extender. Über „Mod aktivieren“ kann jeder Teilnehmer sämtliche Funktionen lokal abschalten, ohne Dateien zu entfernen. Dabei werden bereits ersetzte Koop-Missionen sofort auf Vanilla zurückgestellt und Trail-Modsettings ignoriert.

## Koop-Trail ingame erstellen

1. Missionen wie gewohnt im Traileditor anlegen und speichern.
2. Im Exportdialog „Koop-Trail“ aktivieren.
3. Den Trail normal exportieren.

Nur vorhandene Missionen zählen; Lücken werden wie beim Vanilla-Export entfernt:

- Mission 1–10 ersetzt Koop-Trail 1.
- Mission 11–20 ersetzt Koop-Trail 2.
- Mission 21–30 ersetzt Koop-Trail 3.
- Mission 31–40 ersetzt Koop-Trail 4.
- Weitere Missionen werden nicht als Koop-Missionen verwendet, bleiben aber in den editierbaren Trail-Maker-Quelldateien erhalten.

Die ersten beiden belegten Spielerslots werden Host und Gast. Positionen und Farben bleiben erhalten, der Gast wird automatisch dem Team des Hosts zugeordnet. Ab dem dritten belegten Slot folgen die KIs. Fairness, Startgüter und Gebäudefreigaben stammen aus dem gespeicherten Lobbysetup; die gemeinsamen Gebäudefreigaben gelten identisch für Host und Gast.

Vor der Veröffentlichung wird das gesamte Koop-Paket geprüft. Eine Mission ohne zwei belegte Slots oder mit nicht auffindbaren Map-, Lord- oder AIV-Dateien bricht den Export mit einer Ingame-Meldung ab. Ein fehlendes `.modjson` ist zulässig und deaktiviert für diese Mission alle sieben unterstützten Trail-Mods.

Normale und Koop-Exporte verwenden dieselbe Modsettings-Quelle pro Mission. Ein unmittelbar vor Vanillas Speichervorgang erfasster Snapshot hat Vorrang vor dem vorhandenen `.modjson`; danach folgt der gespeicherte Sidecar. Nur wenn beides fehlt, werden die unterstützten Mods für diese Mission deaktiviert. Dadurch bleiben auch Exporte korrekt, die Vanilla noch innerhalb des laufenden Missionsspeicherns auslöst.

Einfache Werte bleiben direkt lesbar im JSON. Komplexe oder künftig neu hinzukommende `[SyncHostOnly]`-Propertytypen werden typisiert mit derselben MessagePack-Serialisierung abgelegt, die das gemeinsame Presetsystem verwendet. Damit benötigt `CustomCustomTrail` für neue unterstützte Preset-Propertytypen keine eigene JSON-Typerweiterung.

Ein aktivierter Koop-Export erscheint ausschließlich im Koop-Pfadmenü und nicht zusätzlich als normaler Custom Trail. Damit das Paket später trotzdem wieder in den Trail Maker importiert und bearbeitet werden kann, liegen die normalen `.trail`-Quelldateien nur im Unterordner `TrailMakerSource`. Der Mod ergänzt solche Pakete in Vanillas Ingame-Liste „Pfad importieren“ und leitet Vanillas originale Trail-Importmethode auf diesen Unterordner um. Der anschließende Import verwendet vollständig Vanillas Ablauf: Abhängig von der sichtbaren Backup-Checkbox wird zuerst ein Backup erstellt, danach leert Vanilla den aktuellen Trail-Maker-Arbeitsordner und importiert den gewählten Pfad. Zugehörige `.modjson`-Dateien werden erst danach und niemals über bereits vorhandene Sidecars kopiert.

Vorhandene Koop-Pakete erscheinen außerdem in Vanillas Liste „Pfad exportieren“. Ihre Auswahl übernimmt wie bei einem normalen Custom Trail den vorhandenen Paketnamen in das Export-Namensfeld; Bestätigung, optionales Backup und Überschreiben folgen weiterhin Vanillas Exportablauf.

## Installiertes Paket auswählen

Unter den Modsettings wählt der Host „Vanilla – kein eigenes Paket“ oder genau ein installiertes Koop-Trail-Paket. Gäste sehen die Hostauswahl schreibgeschützt. Das Dropdown wird beim Öffnen sowie nach Import und Export neu eingelesen.

Im Koop-Pfadmenü wird der beim Export gespeicherte Kartenname als Missionsname angezeigt. Für jeden durch das Paket belegten Koop-Trail ersetzt der Paketname außerdem die jeweilige Vanilla-Trailüberschrift; unbelegte Trails behalten ihren Vanilla-Namen.

Paket-ID und SHA-256-Inhaltsfingerprint werden synchronisiert. Jeder Teilnehmer prüft sein lokales Paket. Bei einem ersetzten Missionsplatz blockiert der Mod `Ready`, `ReadyLock` und beim Host `Play`, solange einem Teilnehmer das Paket fehlt, es beschädigt ist oder vom Hostinhalt abweicht. Nicht belegte Paketplätze bleiben Vanilla und benötigen das Paket für ihren Start nicht.

Das Paket wird nicht über das Spielnetz übertragen. Zum Verteilen muss der vollständige Custom-Trail-Ordner kopiert werden.

Beim Auswählen und direkten Start einer ersetzten Koop-Mission wird derselbe zentrale Trail-Preset-Kontext wie bei normalen Custom Trails aktiviert und über den Kartenwechsel beibehalten. „Anpassen“ öffnet denselben Missionssnapshot als sichtbares, editierbares Preset „Trail“ und wendet ihn vor einem anschließenden Start erneut an.

## Paketstruktur

Der Koop-Paketordner enthält:

    Mein Trail\
      cooptrail.json
      CoopMissions\
        01.coopmission.json
        ...
        40.coopmission.json
        Assets\
          01\
            map.map
            lord-3.lordjson
            aiv-3-1.aivjson
      TrailMakerSource\
        Trail_Mission_1.trail
        Trail_Mission_1.modjson

`cooptrail.json` enthält `schemaVersion`, eine beim Überschreiben stabil bleibende `packageId`, `displayName`, `missionCount` und den Fingerprint aller Missions-JSONs und gebündelten Dateien. Ordner mit ungültigem Manifest, ungültigen Missionen, falschem Fingerprint oder doppelter Paket-ID erscheinen nicht im Dropdown.

Das frühere Pluginlayout `BepInEx\plugins\CustomCustomTrail_Serp\CoopTrails\TrailN\NN.coopmission.json` wird nicht mehr geladen.

## Missionsformat

Das bestehende `coopmission.json`-Schema 2 bleibt erhalten. Eine Datei enthält:

- Namen und optionale Beschreibung;
- Mapreferenz;
- Fairness, Startgüter und getrennte Host-/Gast-Gebäudefreigaben;
- zwei menschliche sowie bis zu sechs KI-Spieler mit Team, Farbe und Keep-Position;
- Lord- und AIV-Referenzen;
- den missionsabhängigen `modSettings`-Snapshot.

Assets unterstützen weiterhin `builtIn`, `installed` und `bundled`. Der Ingame-Exporter verwendet für Custom Maps, Lords und AIVs bewusst `bundled`, sodass das Paket portabel bleibt. Relative Pfade dürfen ihren Paketordner nicht verlassen.

Ein vollständiges manuelles Missionsbeispiel und ein Manifestbeispiel liegen unter `Examples`. Der Fingerprint im Manifestbeispiel ist nur ein Platzhalter; produktive Pakete sollten über den Ingame-Exporter erzeugt werden.

## Modsettings der Trails

Normale Custom-Trail-Missionen speichern ausschließlich `[SyncHostOnly]`-Properties der folgenden Mods als gleichnamige `.modjson`:

- `BuildingCosts_Serp`
- `BuildingLimit_Serp`
- `ExtraFeatures_Serp`
- `RandomEvents_Serp`
- `StartConditions_Serp`
- `UnitCosts_Serp`
- `UnitLimit_Serp`

Speichern, Laden, Import, Export, Backup, Renummerierung und Löschen spiegeln diese Sidecars. Beim Koop-Export wird der jeweilige normalisierte Stand in die Mission eingebettet. Empfangene Hostwerte und persönliche Client-Einstellungen werden nicht in Trail-Dateien oder lokale Presets geschrieben.

Custom- und Koop-Trail-Missionen bieten vor dem Start „Anpassen“. Das gespeicherte Missionspreset `Trail` ist beim Spielen schreibgeschützt; im Traileditor bleibt es editierbar. Die lokalen Presets 1 und 2 der beteiligten Mods funktionieren unabhängig weiter. Eingebaute Vanilla-Trails besitzen keine eigenen gespeicherten Missions-Modsettings.

## Entwicklung

`build.bat` führt Core-, Struktur-, Host-/Client- und XAML-Prüfungen aus, baut das Paket und installiert es atomar in den Spieleordner. `/nopause` unterdrückt die Abschlussabfrage.
