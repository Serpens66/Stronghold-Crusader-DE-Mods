# Optionale AIV-Truppenverhaltensweisen für SHCDE

Stand: 8. September 2026

## Umfang

Dieses Dokument enthält nur die noch nicht umgesetzten, optionalen Verhaltensmodule aus UCP3 „AIV Troop Behaviour 0.2.1“. Der ehemals enthaltene, inzwischen durch einen bekannten Mod abgedeckte Bugfix wurde vollständig entfernt. Die hier verbleibenden Punkte sind KI-Features und keine notwendigen Fehlerkorrekturen.

Die UCP-Erweiterung selbst ist nicht binär mit SHCDE kompatibel. Übertragbar sind ausschließlich ihre Regeln und Testspezifikationen. Alte x86-Adressen oder Bytes dürfen nicht verwendet werden.

## Verbindliche Analysebasis

- kanonische installierte `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
- `_inspect/CrusaderDE-Native-Baseline/CURRENT.md` und `CURRENT.json` als alleiniger Einstieg in die Native-Baseline;
- Script Extender 2.3.0;
- lokale UCP-Quelle `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\UCP_aiv-troops-behaviour-0.2.1` als Verhaltensreferenz.

Vor jeder Native-Arbeit müssen DLL-Hash und `CURRENT.json` erneut übereinstimmen. Generische `FUN_...`-Namen und Callgraphnähe sind nur Suchanker.

## Offene optionale Module

### Start-/Szenariotruppen zuweisen

Die DE-Funktion bei RVA `0x29520` durchläuft beim KI-Start vorhandene, lebende, noch nicht gruppierte Einheiten. Die zugehörigen Entscheidungen bei RVA `0x291F0` und `0x29360` ordnen sie einer AIV-Verteidigungsgruppe oder einer Grabenarbeitergruppe zu.

Sinnvolle Optionen:

- vorhandene Starttruppen wie reguläre Verteidiger auf AIV-Positionen verteilen;
- geeignete Starttruppen wahlweise als Grabenarbeiter verwenden;
- Vanilla beibehalten, wenn kein gültiger Slot oder keine geeignete Grabenarbeit existiert.

Die Auswahl muss am nativen Entscheidungspunkt erfolgen. Eine nachträgliche vollständige Tribe-Neuorganisation über öffentliche APIs würde Vanilla-Buchhaltung duplizieren und ist nur für Diagnoseprototypen geeignet.

### Individuelle Position halten

Jede Verteidigungsgruppe kann ihrer eigenen AIV-Position zugeordnet bleiben, statt durch Vanilla auf andere freie Positionen verteilt zu werden. Kampfreaktionen, Rückzug, Sonderaufgaben und Wegfindung dürfen dadurch nicht blockiert werden.

Vor einer Implementierung sind Slotindex, Kapazität, Tribe-Eigentum und das Verhalten bei zerstörter oder unpassierbarer Position zu belegen. Der Hook muss auf Vanilla zurückfallen, sobald eine Voraussetzung fehlt.

### Zwischen AIV-Positionen patrouillieren

Verteidigungsgruppen können zeitgesteuert zwischen passenden AIV-Positionen wechseln. Die Bedeutung der vorhandenen Patrol-AIC-Werte ist noch nicht vollständig belegt; sie darf nicht allein aus Namen oder UCP-Feldern abgeleitet werden.

Erforderlicher Test:

1. genau zwei gültige Positionen derselben Reihe bereitstellen;
2. Slotwahl, Zielkoordinaten und Timer über mehrere Wechsel protokollieren;
3. Save/Load, Pause, hohe Geschwindigkeit und vorübergehend blockierte Ziele prüfen;
4. erst danach die Werte als Intervalle oder Wahrscheinlichkeiten typisieren.

### Eigene AIV-Reihe verwenden

Eine optionale Policy darf für bestimmte Einheitentypen eine andere AIV-Reihe wählen. Sie muss die vorhandene DE-Zuordnung selektiv am Mapper ändern und bei unbekannten Typen unverändert auf Vanilla zurückfallen. Ein pauschales Umschreiben importierter AIV-Dateien ist ungeeignet.

### Globale und lordabhängige Regeln

Die Konfiguration gehört in mod-eigene synchronisierte Einstellungen und optional ein lordabhängiges Sidecar. Zusätzliche Felder in `InternalAIC` oder BAIC dürfen nicht erfunden werden.

Empfohlene Priorität:

1. explizite lordabhängige Regel;
2. explizite truppenspezifische Regel;
3. globale Modulregel;
4. Vanilla-Fallback.

Alle beteiligten Spieler müssen dieselbe Policy verwenden; das Modul ist gameplayrelevant und benötigt `NetworkMode=1`.

## Beduineneinheiten: noch gesperrt

Die Zuordnung der neuen Beduineneinheiten zu AIV-Reihen ist nicht ausreichend bestätigt. Möglich sind Importverschiebungen, reservierte Reihen oder eine falsche bisherige Katalogannahme. Bis ein kontrollierter Laufzeittest ein eindeutiges Mapping liefert, verbleiben diese Einheiten auf Vanilla.

Der Test muss pro Kandidatenreihe genau eine markierte Position laden und anschließend tatsächlichen Einheitentyp, gewählte Reihe, Slot und Zielkoordinate gemeinsam protokollieren. Eine Zuordnung ist erst nach wiederholbaren positiven und negativen Kontrollen freizugeben.

## Architektur- und Sicherheitsvertrag

Ein eigener Mod `AIVTroopBehaviour` sollte alleiniger Eigentümer aller dynamischen Hooks sein. Die Hooks dürfen nicht zwischen `BugfixesAndQoL`, `ExtraFeatures` und einem weiteren Mod verteilt werden. Optionale Zusammenarbeit erfolgt nur über Soft-Dependencies und darf keine überlappenden Trampoline erzeugen.

Für jeden Context-Hook sind vorab zu dokumentieren:

- tatsächliche durch RedBird verdrängte Länge und vollständige Instruktionsgrenzen;
- Callback-Reihenfolge und alle Sprünge im verdrängten Bereich;
- Register-, Flags-, Stack- und SIMD-Liveness;
- eindeutiges Pattern, Originalbytes und fail-closed Rollback;
- korrekte 1-basierte Unit-/Tribe-IDs an öffentlichen API-Grenzen.

Die Runtime darf wegen des SHCDE-Startup-Lebenszyklus nicht im normalen `BaseUnityPlugin.OnDestroy()` abgebaut werden. Ein notwendiger Shutdownpfad muss nachweislich echt und idempotent sein.

## Akzeptanztests

- Vanilla-Verhalten bei deaktivierten Optionen bitgenau beziehungsweise zustandsäquivalent;
- mehrere Einheitentypen, leere und volle Positionslisten sowie zerstörte Ziele;
- Starttruppen mit und ohne bestehende Tribe-Zuordnung;
- Grabenarbeit mit gültigen, fehlenden und unpassierbaren Zielen;
- Hold und Patrol mit Kampfkontakt, Rückzug und Save/Load;
- identische Resultate für Host und Client;
- Beduineneinheiten bleiben bis zum abgeschlossenen Mapping-Test unverändert.

Die nächsten sinnvollen Schritte sind reine Diagnose-Hooks für Startentscheidung und Slotwahl, danach der Beduinen-Mappingtest und erst anschließend eine einzelne bewusst gewählte optionale Policy.
