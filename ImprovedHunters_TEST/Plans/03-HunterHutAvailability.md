# Feature 03 – Jägerhütte ohne Vanilla-Wild freischalten

## Arbeitsauftrag

Die Jägerhütte soll auf Karten ohne Hirsch und Ziege baubar sein, wenn ImprovedHunters aktiv ist und mindestens eine zusätzliche Jagdbeute aktiviert ist. Karten- und Missionsregeln, die die Jägerhütte ausdrücklich verbieten, dürfen nicht umgangen werden.

Dieses Dossier ist eigenständig. Vor Arbeitsbeginn den gemeinsamen Stand und die Regeln in ../PLAN.md prüfen.

## Ursache des alten Problems

Die normale Building-Availability-API ändert die Kartenregel, kann aber eine zusätzliche native Ökologieprüfung nicht überschreiben. Das Spiel berechnet beim Kartenstart einen globalen Marker für vorhandenes Vanilla-Wild. Nachträglich erzeugte Hirsche ändern diesen Marker nicht. Deshalb helfen weder ein verzögertes EnableBuilding noch nachträglich gespawnte Tiere.

## Bestätigter nativer Befund

Referenz:

- DLL-SHA-256 33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469
- Export DLL_IsMapperAvailable RVA 0x084A20
- Export springt in die Implementierung RVA 0x0FFC20
- Mapper-ID der Jägerhütte: 78 beziehungsweise 0x4E
- relevante Instruktion bei VA 0x1800FFD56: cmp edx, 0x4e
- globaler Ökologiemarker: VA 0x1837EE97C im Referenzabbild

Für Mapper 0x4E und den relevanten Spielmodus liefert die Funktion früh false, wenn der globale Marker 0 ist. Dieser Rückgabepfad liegt vor beziehungsweise zusätzlich zur normalen Kartenverfügbarkeit.

GamePlayerManagerAPI.SetBuildingAvailability schreibt die Map-Rule r_EMNullAvailable[mapper] und aktualisiert die UI. Für die Jägerhütte liegt das entsprechende Feld nach bisherigem Layout bei MapRules +0x2C8. Diese API beseitigt die separate frühe Abfrage nicht.

Managed UI und Hotkeys verwenden MainViewModel.CanPlaceMapper und damit DLL_IsMapperAvailable. Eine Korrektur am nativen Ergebnis deckt daher die relevanten Eingabewege gemeinsam ab.

## Exakte Policy

Vanillas Ergebnis darf nur von false auf true geändert werden, wenn alle Bedingungen erfüllt sind:

1. mapper == 78
2. ImprovedHunters Hostfunktion ist aktiv
3. mindestens HuntRabbit, HuntCamel oder HuntChicken ist aktiv
4. die zugrunde liegende Karten-/Missionsregel erlaubt die Jägerhütte ausdrücklich
5. der aktuelle Mod- und Layoutzustand ist valide

HuntCow zählt nicht; Kühe sind keine Jagdbeute. Hirsch und Ziege brauchen keine Modkorrektur.

Ist die Kartenregel false, bleibt das Ergebnis false. Ein Missionsverbot darf niemals durch die Mod ausgehebelt werden.

## Geplante Architektur

Eine eigene Klasse HunterHutAvailabilityHook.cs detourt die exportierte Funktion DLL_IsMapperAvailable. Geeignet ist ein vollständiger MonoMod.RuntimeDetour.Hook mit exakt passender unmanaged Delegate-Signatur und Calling Convention.

Der Detour:

- ruft Vanilla exakt einmal auf
- gibt true unverändert zurück
- prüft nur bei Vanilla false die oben genannte Policy
- kapselt Vanilla-Aufruf, Policy und Diagnose getrennt
- fällt bei Fehlern auf das bereits ermittelte Vanilla-Ergebnis zurück
- bleibt prozessweit statisch referenziert
- wird nicht in OnDisable oder OnDestroy entfernt

Vor Implementierung sind Export-Signatur, Calling Convention und der sichere Zugriff auf die Kartenregel zu bestätigen. Der globale Ökologiemarker muss für die eigentliche Policy nicht geschrieben werden.

## Umsetzungsschritte

### 1. Präflight und Signatur

- installierten DLL-Hash prüfen
- Export und Weiterleitung erneut disassemblieren
- managed P/Invoke- oder Extender-Deklaration von DLL_IsMapperAvailable suchen
- Parameterbreite, Rückgabetyp und Calling Convention bestätigen
- feststellen, wie die aktive Spieler-/MapRules-Struktur sicher gelesen werden kann

### 2. Kartenregel validieren

Nicht allein das Offset +0x2C8 übernehmen. Bestätigen:

- Managerbasis
- aktiver Spieler beziehungsweise Regeln für die lokale Bauentscheidung
- Mapper-Indexierung
- Feldwert für mehrere bekannte erlaubte und verbotene Gebäude
- Verhalten in Skirmish, Trail und einer Karte mit ausdrücklichem Verbot

Die validierte Feldabbildung einmal im Initialisierungslog ausgeben.

### 3. Diagnostischer Detour

Zunächst Vanillas Ergebnis nicht verändern. Begrenzte Logs:

- hook confirmed
- mapper
- Vanilla-Ergebnis
- Jägerhütten-Kartenregel
- aktive zusätzliche Beutetypen
- GameModeHelper-Diagnose

Die Mod darf GameNetworkAPI.IsNetworkedEnvironment nicht allein als Modussignal verwenden. Shared/GameModeHelper.cs einbinden, wenn es für die Policy oder Diagnose benötigt wird.

### 4. Policy aktivieren

Nur den exakt beschriebenen false-zu-true-Fall zulassen. Kein Schreiben des globalen Markers und kein permanenter Byte-Patch.

UI-Aktualisierung prüfen, wenn Modsettings in der Lobby geändert werden. Falls die Vanilla-UI das Ergebnis cached, den vorhandenen sicheren Refreshweg verwenden; nicht den nativen Marker manipulieren.

### 5. Dokumentation

UpdateToNewDLL.md ergänzen um:

- Referenzhash
- Export-RVA und Implementierungs-RVA
- semantische Signatur
- Delegate-Signatur
- Kartenregelzugriff und Layoutvalidierung
- Verhalten bei abweichender DLL

## Laufzeittests

| Karte/Zustand | Erwartung |
|---|---|
| keine Hirsche/Ziegen, HuntRabbit an | Hütte baubar |
| keine Hirsche/Ziegen, HuntCamel an | Hütte baubar |
| keine Hirsche/Ziegen, HuntChicken an | Hütte baubar |
| keine zusätzliche Beute aktiv | Vanilla false bleibt false |
| Hütte in Missionsregeln verboten | bleibt verboten |
| Vanilla-Wild vorhanden | Vanilla true unverändert |
| Mod deaktiviert | Vanilla-Ergebnis |
| Settings wechseln | Anzeige und Hotkey stimmen überein |
| Save laden | identisches Ergebnis |

Sowohl Baupanel als auch zugehörigen Hotkey testen. Ein tatsächlicher Bauversuch muss funktionieren; ein nur optisch aktivierter Button reicht nicht.

## Abnahmekriterien

- Vanilla wird exakt einmal aufgerufen.
- Nur Mapper 78 kann korrigiert werden.
- Ausdrückliche Map-Rule false bleibt unangetastet.
- Mindestens eine echte zusätzliche Jagdbeute ist erforderlich.
- Kein globaler Ökologiemarker wird geschrieben.
- Hook ist persistent, fail safe und bei deaktivierter Mod wirkungslos.
- UI, Hotkey und tatsächlicher Bau sind getestet.

## Ergebnisse und offene Punkte

Noch nicht bearbeitet. Hier endgültige Delegate-Signatur, MapRules-Zugriff, Testkarten und Logmarker dokumentieren.

## Startprompt für einen neuen Chat

Arbeite Feature 03 aus ImprovedHunters/Plans/03-HunterHutAvailability.md vollständig ab. Lies zuerst ImprovedHunters/PLAN.md, prüfe DLL-Hash, Export-Signatur und den aktuellen Git-Stand. Implementiere einen fail-sicheren Detour von DLL_IsMapperAvailable, der Vanillas Ergebnis exakt einmal ermittelt und ausschließlich die zusätzliche Ökologie-Sperre für Mapper 78 überstimmt, niemals aber ein Karten- oder Missionsverbot. Aktualisiere Tests, UpdateToNewDLL.md und den Ergebnisabschnitt. Keine Script-Extender-Änderung in diesem Chat.
