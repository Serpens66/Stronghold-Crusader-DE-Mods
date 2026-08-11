# Feature 05 – Fleischmodus pro Gerberei

## Arbeitsauftrag

Jede Gerberei erhält einen eigenen Produktionsmodus:

- Rüstung: vollständiges Vanilla-Verhalten
- Fleisch: Kuh wird wie bisher zur Gerberei geführt und dort verarbeitet; anschließend trägt der Arbeiter die konfigurierte Fleischmenge sichtbar zu einer geeigneten Kornkammer

Kühe werden nicht von Jägern erschossen oder zur Jägerhütte geführt. Die bestehende Gerber-Logik soll Kuhsuche, Heranführen und Verarbeitung so weit wie möglich weiterverwenden.

## Produktentscheidungen

- Modus ist pro Gebäude, nicht global.
- Neue Gerbereien starten mit Rüstung.
- Umschalten während eines laufenden Zyklus betrifft erst die nächste Kuh.
- Einstellung TannerMeatYield ist SyncHostOnly, Standard 6.
- HuntCow entfällt.
- CowMeat entfällt und wird ohne Rückwärtskompatibilitäts-Fallback durch TannerMeatYield ersetzt.
- Fleisch wird sichtbar zur Kornkammer getragen.
- Ziel ist die nächste erreichbare Kornkammer desselben Besitzers mit Kapazität.
- Ist keine Kornkammer verfügbar oder voll, behält der Arbeiter die Ladung und versucht später erneut.
- Wird das Ziel zerstört oder voll, wird neu gesucht.
- In echtem Multiplayer sind die Modus-Buttons sichtbar, aber deaktiviert; Tooltip erklärt den noch fehlenden zuverlässigen Netzwerksync.
- In Einzelspieler-Skirmish und Einzelspieler-Trail ist die Funktion verfügbar.

## Warum ein einfacher Eventhandler nicht reicht

Der Script Extender stellt folgende Tanner-Events bereit:

- OnTannerStoreCowHides
- OnTannerProduce
- OnTannerDropOffCowHides

Sie sind Benachrichtigungen mit UnitId. Sie können die native Transition nicht überspringen, Ausgabegut und Menge nicht zuverlässig ändern und kein Zielgebäude synchron bestimmen.

Die bestätigte native Tanner-Produce-Signatur lautet:

48 69 C8 ?? ?? ?? ?? 33 FF 66 42 89 BC 21 ?? ?? ?? ?? 66 42 89 9C 21

Treffer im Referenzabbild bei VA 0x18013EC9E. Die umgebende Zustandsmaschine liegt im Bereich 0x18013E... und verwendet die Unit-Schrittweite 0x490. Der bestehende Event-Hook liegt nach einer Produktionszustandsänderung und ist damit zu spät für ein sauberes Rerouting.

GameBuilding besitzt die serialisierten Felder:

- r_NextProducedGoodId
- r_ProducedGoodId

Sie liegen in der managed Definition unmittelbar nebeneinander. Die tatsächlichen nativen Offsets und die Serialisierung müssen für die installierte DLL validiert werden.

## Modulaufteilung

Die Implementierung soll nicht in ImprovedHuntersRuntime.cs anwachsen. Vorgesehene Module:

- TannerMeatRuntime.cs: Zustandsmaschine, Kuhverarbeitung, Payload und Lieferung
- TannerOutputViewModel.cs: ausgewählte Gerberei, Modus, Commands, Visibility/Enabled/Tooltip
- TannerNativeHook.cs oder klarer benannte Teilhooks: minimale native Übergänge
- Patches/Assets/GUI/XAMLResources/HUD_Buildings.xaml: additive UI-Erweiterung

Shared/GameModeHelper.cs wird in das Projekt gelinkt und ausschließlich für Moduserkennung verwendet. GameNetworkAPI.IsNetworkedEnvironment allein ist ungeeignet, weil auch lokaler Skirmish eine lokale gameMembers-Liste besitzen kann.

## UI-Konzept

Vanilla-Waffenwerkstätten in HUD_Buildings.xaml verwenden RadioButtons und ButtonChangeWorkshopOutputCommand. Diese Oberfläche dient als visuelles und interaktives Muster, nicht als Beweis für eine geeignete Netzwerkaktion.

ImprovedHunters verwendet das additive XAML-Patchsystem:

ImprovedHunters/Patches/Assets/GUI/XAMLResources/HUD_Buildings.xaml

Lokales Beispiel:
ExtraFeatures/Patches/Assets/GUI/XAMLResources/HUD_Buildings.xaml

Vorgesehen:

- Panel im generischen BuildingPanel ergänzen
- nur sichtbar, wenn die aktuell ausgewählte Struktur eine Gerberei ist
- zwei RadioButtons Rüstung und Fleisch
- passende vorhandene UI-Sprites verwenden
- Binding über GameXAMLManagerAPI.Instance.RegisterBinding registrieren
- Setup-/Selected-Building-Aktualisierung nach dem Muster von ExtraFeatures/QuarryPileRelocationRuntime.cs
- alle interaktiven Elemente mit nichtleerem lokalisiertem Tooltip und ToolTipService.ShowDuration 60000

Im echten Multiplayer:

- Panel sichtbar
- beide Buttons deaktiviert
- deutlicher Tooltip: Gerberei-Fleischmodus ist deaktiviert, weil per-Gebäude-Umschaltung noch nicht zuverlässig zwischen Teilnehmern synchronisiert wird
- prominentes TODO im Code und Dossier für späteren Netzwerksync

## Persistenz- und Zyklusmodell

r_NextProducedGoodId repräsentiert die UI-Auswahl für den nächsten Zyklus. Zu Beginn eines neuen Kuhzyklus wird die Auswahl in r_ProducedGoodId gelatcht. Ein Wechsel während der aktuellen Verarbeitung beeinflusst diese Kuh nicht.

Zu bestätigen:

- welche Good-ID sicher als internes Kennzeichen für Rüstung und Fleisch dienen kann
- ob beide Felder bereits savegame-serialisiert werden
- ob ein unbekannter Good-Wert Vanilla-Code an anderer Stelle stört

Falls eine freie Good-ID nicht sicher ist, darf keine willkürliche native ID gespeichert werden. Dann eine separate, stabil anhand der Building Global-ID serialisierte Modzustandsstruktur entwerfen. Diese muss Save/Load, Gebäudeabriss, ID-Wiederverwendung und Kartenwechsel behandeln. Der native Building-Feldweg ist nur zu bevorzugen, wenn die gesamte Leser-/Writer-Semantik validiert ist.

Neue Gebäude starten immer mit Rüstung. Beim Laden wird der gespeicherte Modus wiederhergestellt. Ungültige Werte fallen auf Rüstung zurück.

## Native Zustandsanalyse

Vor dem Eingriff einen vollständigen Vanilla-Zyklus diagnostizieren:

1. Gerber sucht Kuh.
2. Gerber führt Kuh zur Gerberei.
3. Kuh wird gespeichert beziehungsweise verarbeitet.
4. Produktionstransition.
5. Gerber trägt Rüstung.
6. Dropoff und nächster Zyklus.

Pro Arbeiter und Gebäude loggen:

- stabile Unit- und Building-ID
- AI-State und relevante Unterzustände
- Kuh-ID
- r_NextProducedGoodId / r_ProducedGoodId
- getragene Gut-ID und Menge
- Zielgebäude
- Position
- Ereignisse OnTannerStoreCowHides, OnTannerProduce und OnTannerDropOffCowHides

Jeder Writer und spätere Leser der verwendeten Zustände ist zu untersuchen. Eine einzelne Zuweisung gilt nicht als erfolgreiche Umleitung.

## Fleischzyklus

Die minimale stabile Abzweigung erfolgt unmittelbar bevor Vanilla die fertige Rüstung/Payload festlegt.

### Rüstungsmodus

- Vanilla exakt einmal
- keine Modmutation

### Fleischmodus

- Kuhsuche, Führen und Verarbeitung bleiben Vanilla, bis die Ausgabe bestimmt wird
- keine Rüstung erzeugen
- Payload = TannerMeatYield
- Arbeiter erhält einen validierten Trage-/Lieferzustand
- nächste erreichbare eigene Kornkammer mit freier Kapazität wählen
- visuell zur Kornkammer laufen
- am bestätigten Dropoff-Punkt Fleisch exakt einmal über die Vanilla-Güterlogik, bevorzugt TryAddGood oder den äquivalenten Building-Good-Pfad, gutschreiben
- Payload erst nach bestätigter Gutschrift löschen
- dann in Vanillas regulären Gerber-Folgezustand zurückkehren

Nicht direkt eine globale Fleischzahl erhöhen, wenn dadurch Laufweg, Kapazität oder sichtbare Lieferung umgangen würden.

### Keine Kornkammer oder keine Kapazität

- Payload behalten
- in einen stabilen Warte-/Neuversuchszustand gehen
- Suche gedrosselt wiederholen
- zerstörtes oder inzwischen volles Ziel verwerfen
- Gebäudeabriss, Arbeitertod und Kartenende ohne Doppelgutschrift behandeln

Eine feste Zielwahl darf keine unerreichbare Kornkammer dauerhaft blockieren. Für Reichweite und Wegbarkeit Vanillas tatsächliche Pfadsemantik beziehungsweise vorhandene Building-Query verwenden, nicht bloß Manhattan-Distanz.

## Modsettings-Migration

In ImprovedHuntersViewModel.cs:

- HuntCow-Property und Default entfernen
- CowMeat-Property und Default entfernen
- neue SyncHostOnly-Property TannerMeatYield, Default 6
- Preset- und Resetlogik anpassen

In ImprovedHuntersSettings.xaml:

- Kuh als Jägerbeute vollständig entfernen
- separaten Gerberei-Abschnitt mit TannerMeatYield anlegen
- Einheit für Slider/Anzeige eindeutig als Fleisch angeben
- alle Tooltips und Sperrbindings korrekt setzen

Lokalisierung:

- neue Schlüssel in allen 21 Source-Locale-Dateien
- Paketkopien über den vorgesehenen Build-/Contentpfad
- englischer Fallback, falls Übersetzung fehlt
- Shared/SerpLocalization-Fallbacks ergänzen, falls für frühe UI nötig

Rückwärtskompatibilität zu alten HuntCow-/CowMeat-Presets ist ausdrücklich nicht erforderlich. Keine versteckten Legacy-Properties behalten.

## Projektdatei und Paket

ImprovedHunters.csproj derzeit um Folgendes ergänzen:

- neue Source-Dateien
- Shared/GameModeHelper.cs als Link, falls noch nicht vorhanden
- Patches-Inhalte für das Buildartefakt
- eventuell MonoMod.RuntimeDetour- oder weitere Zhuqiaomon-Verweise, jedoch nur wenn wirklich benötigt

build.bat bleibt allein für Build und Installation zuständig.

## Testmatrix

### Funktion

| Fall | Erwartung |
|---|---|
| neue Gerberei | Modus Rüstung |
| Rüstungsmodus | identisches Vanilla-Verhalten |
| Fleischmodus, Kornkammer frei | Kuh verarbeitet, sichtbare Lieferung, exakt Yield Fleisch |
| Moduswechsel während Zyklus | wirkt erst auf nächste Kuh |
| mehrere Gerbereien mit unterschiedlichen Modi | Modi unabhängig |
| keine Kornkammer | Payload bleibt, kein Verlust/Duplikat |
| Kornkammer voll | gedrosselter Retry |
| Zielkornkammer zerstört | neues Ziel |
| Gerberei/Arbeiter zerstört | keine spätere Doppelgutschrift |
| Save/Load | Gebäudemodi und laufende sichere Zustände korrekt |
| Mod deaktiviert | Vanilla-Rüstung, keine Fleischabzweigung |

### Spielmodi

- Einzelspieler-Skirmish: aktiv
- Einzelspieler-Trail: aktiv
- echtes Multiplayer-Spiel als Host: Buttons sichtbar, deaktiviert
- echter Multiplayer-Client: Buttons sichtbar, deaktiviert
- GameModeHelper-Diagnose stimmt mit erwartetem Modus überein

### UI und Settings

- ausgewählte Gerberei zeigt richtigen Modus
- Wechsel zwischen zwei Gerbereien aktualisiert RadioButtons
- Nicht-Gerberei blendet Panel aus
- Tooltip-Audit
- Locale-Key-Parität aller 21 Sprachen
- HostClientPresetTests
- SyncHostOnly für TannerMeatYield
- CRLF aller geänderten Textdateien

### Invarianten

- verarbeitete Kühe = Rüstungszyklen + Fleischzyklen + sauber abgebrochene Zyklen
- Fleischpayload erzeugt = erfolgreich geliefert + aktuell getragen + explizit verworfen
- pro Zyklus höchstens eine Gutschrift
- keine Rüstung in Fleischmodus
- Vanilla läuft im Rüstungsmodus exakt einmal

## Abnahmekriterien

- Per-Gebäude-Modus ist persistent und zyklusfest.
- Rüstungsmodus ist Vanilla.
- Fleisch wird sichtbar und kapazitätsbewusst geliefert.
- Fehlerfälle duplizieren oder verlieren kein Fleisch unbemerkt.
- Echte Multiplayer-Sitzungen können die unsichere Umschaltung nicht auslösen.
- HuntCow/CowMeat sind vollständig entfernt, TannerMeatYield ist korrekt klassifiziert.
- Native Befunde und Updateschritte stehen in UpdateToNewDLL.md.
- UI-, Locale-, Preset-, CRLF- und Laufzeittests sind dokumentiert.

## Prominentes späteres TODO: Multiplayer

Per-Gebäude-Modus und laufender Payload benötigen eine explizite, stabile Netzwerkrepräsentation. Keine automatische Reflection-/Contractless-MessagePack-Serialisierung verwenden. Ein späteres Paket braucht numerische Keys und einen expliziten IMessagePackFormatter. Hostautorität, Join-in-progress, Save/Load und Building-ID-Wiederverwendung müssen definiert sein. Bis dahin bleibt die Funktion im echten Multiplayer deaktiviert.

## Ergebnisse und offene Punkte

Noch nicht bearbeitet. Aufgrund des Umfangs darf die Arbeit in weitere Chats für Diagnose, UI/Persistenz und Lieferzustandsmaschine aufgeteilt werden. Jeder Teilchat muss diesen Abschnitt mit exakten Dateien, bestätigten Zuständen und nächstem Einstiegspunkt aktualisieren.

## Startprompt für einen neuen Chat

Beginne Feature 05 anhand ImprovedHunters/Plans/05-TannerMeatMode.md und ImprovedHunters/PLAN.md. Prüfe zuerst Projektstand, DLL-Hash und vorhandene Benutzeränderungen. Arbeite phasenweise: zunächst Vanilla-Gerberzyklus und Building-Feldpersistenz diagnostizieren, dann per-Gebäude-UI und Modus, danach die Fleisch-Lieferzustandsmaschine. Keine spekulativen Feldoffsets und kein echter Multiplayer-Sync in diesem Feature; dort bleibt die UI sichtbar deaktiviert. Aktualisiere nach jedem Teil den Abschnitt Ergebnisse und offene Punkte, sodass der nächste Chat exakt fortsetzen kann.
