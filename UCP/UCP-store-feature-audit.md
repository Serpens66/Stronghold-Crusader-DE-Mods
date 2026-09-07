# UCP-Store: Feature- und Inhaltspakete im DE-Abgleich

Stand: 7. September 2026

## Umfang und Lesart

Die Store-Rezeptdatei enthält 49 gepinnte Einträge. Manche sind eigenständige Funktionen, viele jedoch nur KI-/AIV-/Karten-/Grafikpakete oder „Applied“-Profile, die ein Basispaket aktivieren. Sie sind deshalb nicht 49 neue Enginefeatures. Alle Quellen liegen bereits unter `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\HD sources\UCP\Extensions`; direkte URLs stehen in [extension-downloads.csv](extension-downloads.csv).

## Technische Module

| Store-Eintrag | Funktion | DE-Abgleich und möglicher Weg |
| --- | --- | --- |
| `aiSwapper` | komplette KI-Slots samt AIC/AIV/Text/Grafik austauschen | DE unterstützt Custom Lords über Script Extender/Fixes-Infrastruktur; AIV-Seite zusätzlich über `CastlePlanner`. Kein Binärport, sondern DE-eigenes Custom-Lord-Paketformat verwenden beziehungsweise ausbauen |
| `aicloader` | AIC-Werte aus Dateien überschreiben | teilweise durch Script-Extender-AI-Strukturen und Custom-Lord-Unterstützung möglich; `VanillaAICExporter` liefert Referenzdaten. Für allgemeine Imports ist ein validierender DE-AIC-Loader noch sinnvoll |
| `aivloader` | AIV-Dateien laden/ersetzen | durch `CastlePlanner`, `AIVPlacement`, `AIVParser` und DE-AIVJSON-Workflow weitgehend abgedeckt; Konvertierung der enthaltenen HD-AIVs statt Modulport |
| `files` 1.1.0 und 1.2.0 | virtuelle Dateiüberlagerung für UCP-Pakete | DE hat andere Asset-/BepInEx-/Override-Pfade; keine eigene Gameplayfunktion. Ein generischer Port ist unnötig, solange jeder DE-Mod seine Assets installiert |
| `gmResourceModifier` | Bilder aus alten `.gm1`-Archiven zur Laufzeit ersetzen | HD-spezifisches Format/Rendering; DE-Assets sind Unity-Ressourcen. Nur konkrete Grafiken extrahieren und als DE-kompatible Assets neu einbinden |
| `graphicsApiReplacer` | DirectDraw durch DX11/OpenGL, Fenster-/VSync-Optionen | für DE nicht relevant: Unity/DE nutzt diesen alten DirectDraw-Ausgabepfad nicht |
| `maploader` | HD-Karten aus Paketen registrieren | DE besitzt neue Karten-/Workshop-Pfade; Karten konvertieren und über DE/Workshop laden, Modul nicht portieren |
| `startResources` | Startressourcen und Starttruppen | **durch `StartConditions` umfassender abgedeckt** |
| `textResourceModifier` | alte Textressourcen dynamisch ersetzen | DE-Lokalisierung läuft anders; Workspace nutzt `SerpLocalization` und XAML. Nur Inhalte portieren, kein alter Text-Hook |
| `winProcHandler` | Kette für klassische Win32-WindowProc-Hooks | für normale DE-Mods nicht nötig; Unity-Eingabe und verwaltete UI verwenden |
| `running-units` | Laufen für Streitkolbenkämpfer, Sklaven, Speerträger und Schleuderer, optional AIC-gesteuert | Speerträgerteil in `BugfixesAndQoL` abgedeckt. Andere Typen wären über denselben geprüften Movement-Cadence-Ansatz erweiterbar; als synchronisierte Bewegungsoption, nicht als HD-Port |
| `rebalancer` | generische Balancewerte überschreiben | teilweise durch `UnitCosts`, `BuildingCosts`, Limits und DE Advanced Options abgedeckt. Schaden/Rüstung/Tempo benötigen weitere typisierte DE-APIs oder validierte Native-Hooks |
| `citizens` | Verhalten von Zivilisten konfigurieren | kein gleichwertiger allgemeiner Workspace-Mod. Erst konkrete Unteroptionen erfassen; zu allgemein für einen sicheren pauschalen Port |

Die zwei `files`-Versionen sind getrennte Store-Revisionen für unterschiedliche Abhängigkeitsketten, keine zwei fachlich verschiedenen Features.

## KI-, AIV- und Karteninhalte

| Store-Eintrag(e) | Inhaltstyp | DE-Vorgehen |
| --- | --- | --- |
| `AI-Tournament-Maps` | Kartenpaket | Karten einzeln auf DE-Kompatibilität prüfen/konvertieren; keine Engineänderung |
| `Aggressive-AI-Behaviour`, `Aggressive-AI-Behaviour-Applied` | AIC-Verhaltensprofil plus Aktivierung | AIC-Werte nach DE mappen; Applied-Paket wird nach Import durch ein Preset ersetzt |
| `ApeX-AI-Behaviour`, `ApeX-AI-Behaviour-Applied` | AIC-Verhaltensprofil plus Aktivierung | wie oben; neue DE-Lords/Einheiten beim Mapping berücksichtigen |
| `Vanilla-Interpretation-Castles`, `Vanilla-Interpretation-Castles-Applied` | AIV-Satz plus Aktivierung | AIVs mit `AIVParser` prüfen und über `CastlePlanner`/DE-AIVJSON übernehmen |
| `Vanilla-Retraced-Unlocked` | Gesamtpaket aus AIs, AIVs, Karten, Grafik und UCP2-Regeln | nicht monolithisch portieren; Bestandteile einzeln über vorhandene DE-Mods/Importer abbilden |
| `ucp2-ai-files` | überarbeitete AIC/AIV-Dateien | Inhalte konvertieren; keine Binärpatches übernehmen |
| `ucp2-aic-patch` | Aktivierung der UCP2-AICs | in DE als Custom-Lord-/AIC-Preset, falls die Dateien portiert werden |
| `ucp2-evrey-aiv`, `ucp2-tatha-aiv`, `ucp2-vanilla-fixed-aiv` | alternative AIV-Auswahlen | AIVJSON-Konvertierung und Validierung; `CastlePlanner` ist Zielworkflow |
| `Kirito-Kun` | einzelnes KI-Paket | Custom-Lord-Inhalte manuell nach DE portieren |
| `Schlossgespenst-KI`, `Schlossgespenst-KI-files` | KI-Paket plus Dateien | AIC/AIV/Assets trennen und über DE-Custom-Lord-Workflow übernehmen |
| `Seraphine-and-Verehrer` | KI-Paket | wie oben; keine generische Enginefunktion |
| `AI-Tournament-Maps`, `Ascension-Maps` | Karten | DE-Konvertierung/Workshop; keine Hook-Portierung |

## Große Modpacks und Themenpakete

| Store-Eintrag(e) | Inhalt | DE-Bewertung |
| --- | --- | --- |
| `Legends-Of-The-Orient`, `Legends-Of-The-Orient-AI` | Gesamtmod plus KI | hoher manueller Portaufwand; AIC/AIV, Balance, Karten und Assets getrennt behandeln |
| `Fiery-Ladies`, `Fiery-Ladies-AI` | Gesamtmod plus KI | gleicher modularer Portweg; Abhängigkeiten auf Rebalancer/Running Units in vorhandene DE-Systeme übersetzen |
| `Ascension-AI`, `Ascension-AI-Balance`, `Ascension-Balance`, `Ascension-Multiplayer`, `Ascension-Maps`, `Ascension-AI-Tournament` | zusammenhängende Balance-, KI-, MP- und Kartenfamilie | nicht als einzelnes Feature bewerten. Kosten/Startbedingungen teilweise durch Workspace-Mods, AIC/AIV und weitere Balancewerte separat portieren |
| `Community-Paket`, `Community-Paket-Files` | kuratiertes Gesamtpaket und Assets/KI-Dateien | Inhalte einzeln inventarisieren; UCP2-/Grafikabhängigkeiten nicht direkt übertragbar |
| `Die-neuen-Herrscher`, `Die-neuen-Herrscher-KI-Files` | neue Lords samt KI/Assets | DE-Custom-Lord-Workflow; Audio/Grafik/Lokalisierung separat konvertieren |
| `ConqueringChristmas`, `ConqueringEurope` | Grafik-/Themenpakete | nur Assets portieren; alte `.gm1`-/Files-Infrastruktur ist DE-inkompatibel |

## UCP2-Kompatibilitätspakete und Fixes

| Store-Eintrag | Rolle | DE-Bewertung |
| --- | --- | --- |
| `ucp2-legacy` | enthält die in den Feature- und Bugfixberichten analysierten UCP2-Patches | nicht als Ganzes portieren; jede Funktion einzeln |
| `ucp2-legacy-defaults` | aktiviert ein UCP2-Standardprofil | nach Portierung einzelner Funktionen höchstens als DE-Preset nachbilden |
| `ucp3-fixes` / lokaler Quellordner | `aiv-troops-behaviour` und `hopfarm-limit-fix` | erster Fix separat analysiert; Hopfenfarm durch `shcde-fixes` abgedeckt |

## Vollständigkeitskontrolle

Die Tabellen nennen alle fachlichen Store-Pakete. Gezählt nach den 49 CSV-Zeilen sind enthalten: 14 technische Module und Revisionen, 18 KI-/AIV-/Karten-Einträge, 15 Einträge der großen Paketfamilien sowie zwei UCP2-Kompatibilitätspakete. `ucp3-fixes-main` und `extension-files-main` waren zusätzliche bereits vorhandene Arbeitskopien außerhalb der 49 gepinnten Downloadzeilen; sie ändern die Store-Zählung nicht.
