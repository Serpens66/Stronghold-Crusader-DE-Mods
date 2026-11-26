# Zinnen bei KI-Belagerung prüfen

Der Test startet zwei lokale Spectator-Gefechte ohne menschlichen Spieler auf `4spielerkarte.map`. Die Schlange startet auf Platz 1 bei `(283,403)` mit `SK_SNAKE_4.aivjson` aus Fixes. Der passive DoNothing-Lord startet auf Platz 2 bei `(403,282)` mit `DoNothing.aivjson` und `DoNothing.lordjson`. Burgplätze, AIV-Auswahl und Drehung werden für beide Gefechte festgelegt. Der Runner prüft und protokolliert die SHA-256-Prüfsummen der Karte und der Lord-Dateien.

Die Fälle verwenden Host-Defaults mit **Human an**. Nur **AI (+Editor)** wechselt von aus auf an. Die drei Optionen zum Angleichen bereits vorhandener Zinnen sind aus. Die frisch gebaute Prüfwand muss in beiden Fällen dieselben Kacheln und Besitzer haben. Mindestens eine Zinne muss bei **AI (+Editor)** ihre Höhe ändern. Niedrige Zinnen müssen dann Wegkanten haben und hohe Zinnen dürfen keine haben.

APIShared weist 30 bei `(403,294)` aufgestellte Speerträger über dieselbe Schnittstelle wie Spectator der Rolle **SiegeWall** zu. Spectators Angriffswerkzeug gibt der Schlange den strategischen Angriffsbefehl. Der Test prüft die Gruppenzuweisung, die Belagerungsphase, die von der KI gewählten Ziele und den tatsächlichen Schaden. Die Zinnenreihe reicht von `(399,292)` bis `(408,292)`.

Zwei neue Sitzungen im Karteneditor prüfen den direkten Mauerschaden unabhängig von der KI-Zielauswahl. Drei Streitkolbenkämpfer erhalten jeweils einen direkten Auftrag auf dieselbe Zinne bei `(397,422)`. Der Test liest Schaden und Höhe vor und nach einem Treffer.

## Ausführung

Voraussetzungen sind eine laufende Steam-Sitzung bei geschlossenem Spiel, die lokal installierte Karte und DoNothing sowie das zur erwarteten Version passende installierte BugfixesAndQoL-, Spectator-, APIShared-, Script-Extender- und Fixes-Paket.

    dotnet msbuild Testmods/CrenelSiegeAcceptance/CrenelSiegeAcceptance.csproj /p:Configuration=Release
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Testmods/CrenelSiegeAcceptance/Run.ps1

Mit `-Scenario crenel-damage` läuft nur die Editorprobe. Der Runner führt die Serie in einem Spielstart auf einem unsichtbaren Desktop aus und stellt Plugins und persönliche Einstellungen anschließend wieder her. Das Log unter `.work/a/crenel-siege` enthält `CRENEL_SIEGE`, `TEST_ACTION` und `INGAME_TEST` als Auswertungsmarker.
