# ImprovedHunters – Masterplan

Stand: 11. August 2026

Dieses Dokument ist der Einstiegspunkt für die weitere Entwicklung. Die eigentliche Arbeit ist in eigenständige Feature-Dossiers unter Plans aufgeteilt. Jedes Dossier enthält genug Projektkontext, bestätigte technische Befunde, konkrete Arbeitsschritte und Abnahmekriterien, um in einem neuen Chat ohne Kenntnis früherer Gespräche zu beginnen.

## Zielbild

ImprovedHunters soll die Jagd verlässlich auf Kaninchen, Kamele und Hühner erweitern, vorhandene Kadaver sinnvoll verwerten und die Jägerhütte auch auf Karten ohne Hirsch oder Ziege verfügbar machen. Kühe werden ausdrücklich nicht mehr von Jägern gejagt. Stattdessen erhält jede Gerberei einen lokalen Produktionsmodus, der eine angelieferte Kuh wahlweise zu Rüstung oder zu Fleisch verarbeitet.

Dabei gelten folgende Produktentscheidungen:

- Hühner aller Besitzer dürfen optionale Jagdziele sein. Sie werden nicht länger zwangsläufig auf Besitzer Natur umgeschrieben.
- Nur neutrale beziehungsweise der Natur gehörende Tiere werden vor unerwünschten automatischen Angriffen geschützt. Manuelle AttackUnit-Befehle bleiben möglich. Feindliche und spielereigene Tiere behalten das Vanilla-Kampfverhalten.
- Ein toter, nicht reservierter und abholbarer Beutekörper hat für Jäger absolute Priorität vor lebender Beute. Innerhalb derselben Prioritätsstufe bleibt die Fleisch-pro-Zeit-Bewertung maßgeblich.
- Kühe sind keine Jagdbeute. Jede Gerberei kann im Einzelspieler zwischen Rüstung und Fleisch umgeschaltet werden. Neue Gerbereien starten mit Rüstung.
- Der Fleischwert der Gerberei ist eine hostgesteuerte Modsetting-Einstellung namens TannerMeatYield mit Standardwert 6.
- Der Gerbereimodus ist im echten Multiplayer vorerst sichtbar, aber deaktiviert. Ein klarer Tooltip erklärt, dass die zuverlässige Synchronisation noch fehlt.
- Neue native Hooks werden zuerst eigenständig im Mod implementiert und im Spiel validiert. Erst danach werden allgemeine, policy-neutrale APIs auf einem separaten Script-Extender-Branch vorbereitet.
- Eine veröffentlichte Modversion bleibt zunächst mit dem offiziellen Script Extender kompatibel. Interner Mod-Hook und Extender-Testhook dürfen niemals gleichzeitig dieselbe Stelle hooken.

## Reihenfolge und Dossiers

1. [01 – Spielereigene Hühner als Jagdziele](Plans/01-HunterQuery-OwnedAnimals.md)
   Ersetzt die erzwungene Neutralisierung von Hühnern durch einen frühen, eigenen Kandidatenfilter. Das ist zugleich die technische Grundlage für die Kadaverfunktion.

2. [02 – Tote, nicht reservierte Beute einsammeln](Plans/02-DeadPreyCollection.md)
   Erweitert den frühen Filter um validierte Kadaver und führt die feste Prioritätsstufe vor lebender Beute ein.

3. [03 – Jägerhütte ohne Vanilla-Wild freischalten](Plans/03-HunterHutAvailability.md)
   Korrigiert die zusätzliche native Verfügbarkeitsprüfung, ohne Karten- oder Missionsverbote zu umgehen.

4. [04 – Automatische Angriffe auf neutrale Tiere verhindern](Plans/04-NeutralAnimalAutoAttacks.md)
   Benötigt zunächst gezielte native Diagnose und Reverse Engineering am frühesten gemeinsamen Zielwahlpunkt.

5. [05 – Fleischmodus pro Gerberei](Plans/05-TannerMeatMode.md)
   Größtes Feature: UI, persistenter Gebäudemodus, Kuhverarbeitung und sichtbare Lieferung an eine Kornkammer.

6. [06 – Allgemeine Hooks in den Script Extender überführen](Plans/06-ScriptExtender-Upstream.md)
   Erst nach erfolgreichen Laufzeittests der jeweiligen Modfunktionen. Enthält Branch-, API- und Merge-Request-Regeln.

Die Reihenfolge 1 vor 2 ist technisch zwingend, weil beide Funktionen dieselbe frühe Hunter-Query-Erweiterung verwenden. Die Punkte 3 und 4 sind davon weitgehend unabhängig. Punkt 5 sollte erst begonnen werden, wenn die kleineren Jagdfunktionen stabil sind. Punkt 6 folgt jeweils erst nach belegten Laufzeittests.

## Verbindlicher Ausgangszustand

- Projekt: ImprovedHunters, aktuell Version 1.1.25.
- Kanonische Spiel-DLL:
  E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll
- Geprüfter Steam-Build: 24651686.
- Dateigröße: 3.450.880 Bytes.
- SHA-256: 33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469.
- Die DLL unter x86_64 im Workspace ist nur eine möglicherweise veraltete historische Vergleichsquelle und darf nicht als Build- oder Analysebasis dienen.
- Kanonischer lokaler Script-Extender-Fork: shcde-script-extender.
- Zuletzt geprüfter Extender-Commit: 368124119be230306f3f2593efa2a270b0e3dfb1, Release 1.40.0 vom 5. August 2026.
- origin zeigt auf den eigenen GitLab-Fork, upstream auf Rawras Originalprojekt.
- Das BepInEx-Log liegt unter:
  E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log

Vor Beginn eines Features sind Hash, Spiel-Build, Modversion und Extender-Commit erneut zu prüfen. Weichen sie ab, gelten feste RVAs und Strukturlayouts nicht automatisch weiter.

## Aktuelle Architektur des Mods

Die Hauptlogik liegt in ImprovedHunters/src/ImprovedHuntersRuntime.cs. Apply registriert derzeit unter anderem:

- UnitHunterQueryTarget Pre
- CalculateBonusYield Pre
- UnitCreate Pre
- Hunter-Pickup und Hunter-Dropoff
- Projectile-Spawn
- Map-Start
- Bewegungs- beziehungsweise Visual-Interpolate-Ereignisse als Taktgeber des nativen Scans

Der Runtime-Zustand ist absichtlich statisch und persistent. OnDisable oder OnDestroy der kurzlebigen BaseUnityPlugin-Instanz bedeuten bei SHCDE nicht das Prozessende. Dort dürfen dauerhafte Events oder native Hooks nicht abgemeldet werden. Ebenso darf die Funktion nicht von Update, Coroutines oder UnityMainThreadDispatcher als dauerhaftem Frame-Host abhängig gemacht werden.

Wichtige vorhandene Rohfelder eines Unit-Slots:

| Bedeutung | Offset relativ zum vom Mod verwendeten Rohslot |
|---|---:|
| AliveState | +0x88 |
| Unit-Typ | +0x8A |
| Flags beziehungsweise Besitz-/Kontrollstatus | +0x92 |
| Position X/Y | +0xC0 / +0xC2 |
| Corpse-Flag | +0x29C |
| AI-Zustand | +0x2BC |
| Timer-/Transform-Feld | +0x2C4 |
| Wanderziel | +0x370 |
| Jäger-Zielkoordinaten | +0x39A / +0x39C |
| Reservierung | +0x448 |

Diese Offsets sind keine allgemeine API-Garantie. Jede neue native Verwendung muss Managerbasis, Slotformel, ID-Basis und Unit-Strukturgröße bestätigen. Die aktuelle native Unit-Struktur verwendet nach bisheriger Analyse eine Schrittweite von 0x490.

Bekannte Zustände:

- CorpsePickupState = 0x6E
- CorpseFreshState = 0x6F

TryGetPreyEligibility akzeptiert aktuell nur IsAlive, Reservation 0, geeignete Flags sowie Corpse-Flag 0 oder AI-Zustand 0x6E. IsRuntimeHuntingEnabled schließt Kühe ausdrücklich aus. Die Owner-Hilfsfunktionen lassen derzeit faktisch alle Besitzer zu. OnUnitCreate und NeutralizePlayerOwnedChicken schreiben Hühner aktuell auf Spieler/Farbe 0 um; genau das wird durch Feature 01 entfernt.

## Einstellungen und Oberfläche

ImprovedHuntersViewModel.cs klassifiziert die Jagdwerte aktuell als SyncHostOnly. HuntCow und CowMeat sind noch vorhanden, obwohl die Kuhjagd zur Laufzeit ausgeschlossen ist. Im Zuge des Gerberei-Features werden beide ohne Rückwärtskompatibilitäts-Fallback entfernt beziehungsweise ersetzt:

- HuntCow entfällt vollständig.
- CowMeat wird durch TannerMeatYield ersetzt.
- TannerMeatYield ist SyncHostOnly und hat Standardwert 6.
- Die Kuhzeile verschwindet aus der Jagdbeute-Tabelle.
- Der Gerbereiwert erhält einen eigenen klar benannten Abschnitt.

Die Modsettings-XAML liegt unter:
ImprovedHunters/BepInEx/plugins/ImprovedHunters_Serp/Override/ScriptExtenderUI/ImprovedHuntersSettings.xaml

Lokalisierungen existieren in 21 Sprachen sowohl unter ImprovedHunters/Locales als auch in der Paketstruktur. Neue Schlüssel müssen in allen Sprachen existieren; ist keine Übersetzung bekannt, wird der englische Text verwendet. Jedes interaktive XAML-Element benötigt einen nichtleeren Tooltip und ToolTipService.ShowDuration 60000. Der vorhandene einheitliche Tooltip-Stil ist beizubehalten.

## Native Entwicklungsregeln

Für jedes neue native Feature gelten zwingend:

- Analyse ausschließlich gegen die installierte kanonische DLL.
- Adressen immer als SHA-256 plus RVA plus semantische Signatur dokumentieren.
- Bei passendem Referenzhash direktes bekanntes RVA verwenden und die lokalen Bytes beziehungsweise Semantik prüfen. Auf diesem Pfad keine vollständige Pattern-Suche.
- Bei abweichendem Hash nur einen eindeutigen, auf geeignete PE-Sektionen begrenzten Pattern-Fallback verwenden.
- Shared.NativePatternResolver benutzen und ImprovedHunters/UpdateToNewDLL.md um Hook, Hash, RVA, Pattern, Auflösung und Updateprüfung ergänzen.
- Rizin nur über .native-analysis\Run-Rizin-With-Ghidra.cmd starten. Zuerst ein kleiner iI-Smoke-Test, danach nur relevante Funktionen mit s, af, pdr und gegebenenfalls pdg untersuchen. Kein vollständiges aaa außer als letzter Ausweg.
- Ein Displacement allein beweist kein Feldlayout. Managerbasis, 0-/1-basierte IDs, Headerabstand, Slotformel und mehrere plausible Laufzeitwerte validieren.
- Diagnosehook am frühesten gemeinsamen Punkt setzen. Einmal hook confirmed mit plausibler Objekt-ID, Zustand und Position loggen. Ungültige Kontexte mindestens einmal ausdrücklich melden und Wiederholungen drosseln.
- Vanilla exakt einmal ausführen. Capture, Korrektur und Diagnose in getrennten Fehlerpfaden kapseln.
- Bei deaktivierter Mod muss jeder Eingriff vollständig inaktiv sein.
- Keine dauerhaften Byte-Patches für zur Laufzeit abschaltbare Funktionen verwenden.

Für neue Inline-Hooks sind die lokalen Zhuqiaomon-Hooks das Grundmuster: `Iced.Intel`, `Zhuqiaomon.Extensions`, `Zhuqiaomon.Hooks`, `HookTransaction`, `HookRef<X64InlineHook>` und atomarer Rollback. Vor der Wahl zwischen `AddInline` und `AddContextHook` müssen insbesondere die x64-Mindestüberschreibung von 14 Byte, Instruktionsgrenzen und Hot-Path-Kosten geprüft werden. Für vollständige exportierte Funktionsdetours kann `MonoMod.RuntimeDetour.Hook` verwendet werden, sofern Projektverweise und Calling Convention sauber geprüft sind.

## Qualitäts- und Abschlussregeln pro Feature

Jeder Feature-Chat endet erst, wenn folgende Punkte erledigt oder ausdrücklich als noch offener Laufzeittest dokumentiert sind:

1. Vorherigen Zustand und fremde Benutzeränderungen prüfen; keine sachfremden Dateien anfassen.
2. Statische Analyse und Hooksemantik dokumentieren.
3. Implementierung modular halten; große neue Funktionen in eigene Quelldateien legen.
4. Kurze Kommentare erklären nicht offensichtliche Entscheidungen.
5. Logs mit Zeitstempel einschließlich Millisekunden über die gemeinsamen Logging-Helfer ausgeben.
6. Mod aus = Vanilla-Verhalten.
7. UpdateToNewDLL.md und gegebenenfalls info.json aktualisieren.
8. Alle geänderten Textdateien auf CRLF prüfen.
9. Bei Modsettings- oder XAML-Änderungen HostClientPresetTests, Tooltip-Audit, Locale-Key-Parität und CRLF prüfen.
10. Erst nach allen jeweils möglichen Vorprüfungen die passende `ImprovedHunters/build.bat` mit erhöhten Rechten ausführen. `build.bat` übernimmt Build und Installation; nicht manuell bauen oder kopieren. Scheitert ein Build oder macht ein Test eine Code-/Projektkorrektur nötig, darf und soll `build.bat` nach der Korrektur erneut ausgeführt werden, bis Build und Installation erfolgreich sind. Identische Wiederholungen ohne neue Diagnose oder Änderung vermeiden. Diese Wiederholungsregel gilt für jeden Featureplan, auch wenn dessen eigener Testabschnitt den Build nur verkürzt erwähnt.
11. Testprotokoll aus dem BepInEx-Log mit erwarteten Markern, fehlenden Callbackfehlern und relevanten Invarianten auswerten.

Analyseartefakte in .inspect, .tools oder .native-analysis werden nicht ungefragt gelöscht.

## Statusübersicht

| Feature | Implementierung | Laufzeittest | Extender-Port |
|---|---|---|---|
| Spielereigene Hühner | Neutral/Feind bleiben im bewiesenen nativen Pfad; Reservation und Orderredirect gelten nur für exakten Eigenbesitz; Build 1.1.30 installiert | Neutral- und Feindhuhn vollständig erfolgreich; 1.1.30 bestätigt `AliveState=3` beim Delete und deckt einen Tile-/Weltkoordinaten-Mischvergleich auf, weshalb kein aktiver Schadensversuch lief; Same-Owner-`KillUnit` blieb korrekt aus | Aktive Distanz mit `r_CurrentTileX/Y * 8` beziehungsweise gleichskaligen Werten korrigieren und erneut testen |
| Kadaverpriorität | offen | offen | offen |
| Jägerhütten-Verfügbarkeit | offen | offen | offen |
| Autoangriffsschutz | Reverse Engineering offen | offen | offen |
| Gerberei-Fleischmodus | offen | offen | optional, erst später |

Diese Tabelle soll nach jedem Feature-Chat aktualisiert werden. Zusätzlich pflegt jedes Dossier einen eigenen Abschnitt Ergebnisse und offene Punkte, damit der nächste Chat nicht auf Gesprächsverlauf angewiesen ist.
