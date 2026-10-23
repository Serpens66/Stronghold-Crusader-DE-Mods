# Modsettings-Rework: Themenbereiche und Empfehlungen

## 1. Ziel und Grundlage

Die Modsettings sollen nach Variante B aufgebaut werden: **Client/Host → aufklappbarer Themenbereich → Fixes / QoL / Fixes? → Optionen**. Nutzer sollen zuerst ihr Thema finden und dort alle zugehörigen Einstellungen gemeinsam sehen. Die bisherige fachliche Einordnung bleibt innerhalb des Themas erhalten.

Diese Datei beschreibt das Gestaltungsmuster und seine Umsetzung in BugfixesAndQoL. Andere Mods können es später übernehmen. Die historischen Bestandsaufnahmen bleiben als Nachweis erhalten; für die aktuelle Umsetzung gilt der folgende Abgleich.

### Umsetzungsstand vom 23.10.2026

Ausgangsstand: `develop`, HEAD `6c02c6ab08250d7126c177d954a3b704a7ac5d43`. Erneut gegen Quell-XAML und ViewModel abgeglichen: **107 Hauptoptionen, davon 35 Client und 72 Host**. Die zehn Themen besitzen aktuell die Größen C1/C2/C3/C4 = **12/9/10/4**, H1/H2/H3/H4/H5/H6 = **18/16/9/13/7/9**. Der zusammengehörige Partievorbereitungs-/Mehrspieler-/Zuschauerbereich bleibt mit 18 Optionen eine bewusste Ausnahme der Zielgröße.

- C3 ergänzt `EnableLordAndMapListEnhancements` und `EnableMissingMapSaveRecovery`; der Workshop-Auswahlfix ist bereits lokal eingeordnet.
- C1 verwendet nun `SpriteSheetMergeModeIndex` als UI-Adapter des gespeicherten `SpriteSheetMergeMode`.
- H1 ergänzt `EnableRandomOpponentSelectionEnhancements`, `EnableMultiplayerStartupStateFix` und `EnablePeaceTimeGameplayFixes`.
- H2 ergänzt `EnableAiSellThresholdFix` unter „Fixes?“.
- H6 ergänzt `EnableImprovedAssassinPathfindingAI` unmittelbar neben der menschlichen Assassin-Wegfindung. Damit neun statt acht Optionen.
- Die Zeitsteuerungsberechtigung und das maximale Spieltempo besitzen eigene Darstellungs-/Empfehlungszeilen. Der bisherige Suchschlüssel bleibt bei der Berechtigung; das Maximum erhält einen eigenen Suchschlüssel. Die gespeicherten Werte bleiben unverändert.

Die linke Symbolspalte und Themenklappen sind im Mod umgesetzt. Ansichtszustand ist ausschließlich lokal und gilt für die Lebensdauer des Settings-ViewModels. Suchübersteuerung verwendet den vorhandenen APIShared-Katalog und Matcher. Serp hat die vollständige [redaktionelle Empfehlungsliste](BugfixesAndQoL/ModsettingsRecommendations.md) am 23.10.2026 bestätigt: **47 Sterne „Empfohlen“ und 60 Rauten „Geschmackssache“**. Die nachfolgenden 100-Optionen-Tabellen dokumentieren weiterhin ausdrücklich den älteren Stand.

Nach optischer Rückmeldung werden die zehn Themenköpfe als schlichte goldene Überschriften ohne farbige Buttonfläche oder Rahmen dargestellt. Ein eigenes transparentes Button-Template erhält die ganze Zeile als Klickziel; Hover und Tastaturfokus hellen die Schrift dezent auf. Kleine gezeichnete Dreiecke zeigen den effektiven Öffnungszustand einschließlich Suchübersteuerung an; sie ersetzen die im Spiel fehlenden Schriftzeichen. Die Anzahl steht rechts in Grau, die Sammelschaltflächen bleiben unverändert.

Die bestätigten Empfehlungen werden ausschließlich über stabile Propertynamen im Mod zugeordnet. Begründungen kommen aus Locale-Keys, werden erst bei Anzeige aufgelöst und aktualisieren sich bei einer Katalog-/Sprachaktualisierung. Sie sind eigene 60000-ms-Tooltips der linken Marker; bestehende fachliche Control-Tooltips bleiben erhalten. Technische Getter-Platzhalter in der Empfehlungsliste wurden durch lesbare Texte ersetzt. Der empfohlene Regler für Abrissschutz nennt ausdrücklich „Nur vorübergehende Blockaden (1)“. Einstufungen und deren Anzeige ändern weder Spielwerte noch Presets.

Die Themen-, Rollen- und Legendenbreite folgt der sichtbaren Scrollfläche abzüglich des bestehenden Seitenrands. Das ist bei horizontalem Auto-Scroll erforderlich, damit lange Texte und die Warenliste mit begrenzter Breite umbrechen. Breitere bestehende Preseteditoren behalten den äußeren horizontalen Scrollweg.

### Bestandsaufnahme vom 21.10.2026

- Geprüft wurde der lokale Arbeitsstand auf `develop`, Basis-HEAD `a4373f6e8477d94ff5374370ae68a254506bbeea`, einschließlich der am 21.10.2026 gegen 17:04 Uhr MESZ vorhandenen uncommitteten Änderungen. Der Commit allein beschreibt diesen Stand deshalb nicht vollständig.
- Maßgeblich sind [Settings-XAML](BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml), [ViewModel](BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs) und [deutsche Texte](BugfixesAndQoL/Locales/de-DE.txt). Generierte oder installierte Kopien wurden nicht als Quelle verwendet.
- In der XAML stehen **100 Hauptoptionen**: 32 im Clientbereich und 68 im Hostbereich. Gezählt wurden fachliche CheckBox-, Slider- und ComboBox-Eingaben nach Beginn der Rollenbereiche; Verwaltung, Hotkey-Aktionen und Wareneditor wurden gesondert betrachtet.
- Zusätzlich gehören `HealthBarHotkey` und `MarketGoodsOrder` als bearbeitbare Detailwerte zu bestehenden Optionen. `ShowVanillaMapsInEditor` wird im Karten-/Dateidialog bedient und besitzt derzeit keine eigene Zeile auf dieser Modsettings-Seite.
- Die beiden neuen Optionen `EnableVanillaPerformanceOptimizations` und `EnableSpriteSheetMerge` sind bereits berücksichtigt. Die drei aktuellen Optionen für Aufgeben/Statistiken und Zuschauer sind ebenfalls noch Bestandteil dieser XAML. Ein anderer neuer Modordner ist für sich kein Beleg, dass sie hier bereits entfallen sind.
- Die Zuordnung unten ist eine datierte Bestandsaufnahme, keine dauerhafte Erwartung an Optionsnamen oder -anzahl. Vor der Umsetzung gegen den dann aktuellen Arbeitsstand abgleichen; bestehende parallele Änderungen erhalten.

### Ergänzung vom 23.10.2026: Performance sichtbar machen

Für Funktionen mit möglichem starkem Performance-Einfluss ist ein eigener Host-Themenbereich **„Performance Heavy“** vorgesehen. Sein erster Schwerpunkt ist **Wegfindung und Erreichbarkeit**. Die zunächst 13 Kandidaten wurden am 23.10.2026 vollständig im Code geprüft: **acht bleiben in diesem Bereich, fünf kehren in ihre fachlichen Themen zurück**, weil keine vergleichbare zusätzliche große Suche bestätigt ist. Die Ergebnisse und konkreten Optimierungsansätze stehen in [PerformanceFix.md](PerformanceFix.md). Die unten umgeordneten Tabellen verwenden weiterhin die vollständige 100-Optionen-Bestandsaufnahme vom 21.10.; sie sind kein vollständiger Neuabgleich aller inzwischen hinzugekommenen oder umbenannten Optionen.

Der Zweck ist, leistungsrelevante Entscheidungen schnell zu finden. **Nicht jede Wegfindungskorrektur ist nachweislich teuer.** Deshalb im Bereich eine kurze Erläuterung „Kann je nach Karte, Einheitenzahl und Befehlen zusätzliche Rechenzeit beanspruchen“ zeigen und pro Option den Belegstand unterscheiden. Ein pauschales Urteil „verursacht starke Lags“ wäre durch die vorhandenen Ergebnisse nicht gedeckt.

Auch außerhalb dieses Bereichs relevante Einflüsse kennzeichnen: beispielsweise Rendering/Sichtweite beim erweiterten Zoom und Grafikspeicherbedarf beim Spritesheet-Zusammenlegen. Performance-Optimierungen bleiben als solche erkennbar; eine Verbesserung der FPS gehört nicht unter eine pauschale Warnung vor hohen Kosten. Autorenempfehlung und Performance-Einfluss sind zwei unabhängige Informationen.

## 2. Empfohlener optischer Aufbau

### Rollen und Klappbereiche

- Gemeinsamer Kopf mit Aktivierung, sichtbar beschrifteter Presetverwaltung, Zurücksetzen, Suche und Rollen-/Quellenhinweisen. Keine dieser Funktionen in einen fachlichen Klappbereich verlegen.
- Die beiden Rollenbereiche bleiben gleichzeitig sichtbar; ihre bestehenden grünen Client- und blauen Host-Akzente bleiben erhalten. Keine zusätzlichen Rollen-Tabs oder seitliche Navigation.
- Die Rolle ist eine feste Überschrift, darunter folgt genau **eine** aufklappbare Ebene. Mehrere Themen dürfen gleichzeitig geöffnet sein.
- Beim ersten Öffnen sind alle Themen geschlossen, sodass die gesamte Gliederung schnell erfassbar ist. Öffnungszustände während der Sitzung beim Wechsel der Modseite erhalten; für den ersten Schritt keine dauerhafte Speicherung über Spielneustarts.
- Neben der Themenüberschrift stehen ein eindeutiger Auf-/Zu-Pfeil und die Zahl der Hauptoptionen. Keine Gruppencheckbox: Ein Klick auf den Kopf öffnet oder schließt ausschließlich den Bereich.
- Pro Rollenbereich gibt es „Alle aufklappen“ und „Alle zuklappen“. Diese Befehle verändern keine Einstellungen.
- Zielgröße sind ungefähr **5–16 Hauptoptionen** je Thema, mit begründeten Ausnahmen für umfangreiche Editoren. Keine eigenständigen Klappen nur für Audio, Pest, Zuschauer oder eine einzelne Korrektur anlegen. Themen nicht durch sachfremde Optionen oder einzeln gezählte Editorbuttons künstlich vergrößern.
- „Performance Heavy“ wird ein eigenständiges Thema auf derselben Klappebene. Darunter keine zusätzliche Pathfinding-Klappe bauen. Die vorhandenen Kategorien Fixes/QoL/Fixes? und die einzelnen Funktionen bleiben erhalten; kein gemeinsamer Schalter, der alle leistungsrelevanten Funktionen deaktiviert.
- Innerhalb eines Themas erscheinen die vorhandenen Kategorien in der Reihenfolge **Fixes, QoL, Fixes?**. Leere Kategorien entfallen; die übrigen sind einfache, dezente Zwischenüberschriften ohne weitere Klappe. Ein oder zwei Einträge unter solch einer Zwischenüberschrift sind unproblematisch, weil dafür kein zusätzlicher Navigationsschritt entsteht.
- Die Tabellen unten legen die Themenzuordnung fest. Innerhalb jeder Kategorie die Tabellenreihenfolge beibehalten. Die Kategorie wird im ersten Rework aus dem bisherigen Bestand übernommen, auch wenn der Propertyname anders klingt.
- Größere Editoren wie Warenreihenfolge und Hotkey-Zuweisung bleiben unmittelbar bei ihrer Option und werden mit dem Thema geschlossen. Kein zusätzlicher Scrollbereich innerhalb eines Klappbereichs.

### Optionszeilen und Markierungen

Einheitliches Zeilenraster: **ganz links eine feste, 28 UI-Einheiten breite Symbolspalte**, daneben das eigentliche Control mit umbrechender Beschriftung. Der Symbolplatz bleibt auch bei fehlender Markierung reserviert. Regler, Auswahllisten, Werttexte und Detailaktionen behalten ihren fachlichen Zusammenhang. Auch bei schmalen Fenstern bleibt das Symbol in seiner Spalte; nur der Optionsinhalt bricht um.

| Kennzeichnung | Bedeutung | Darstellung |
| --- | --- | --- |
| ★ Empfohlen | Der Autor empfiehlt, diese Funktion normalerweise dauerhaft aktiviert zu lassen. | Dezentes Gold; Symbol links, Bedeutung als Text in der Legende und Begründung im Tooltip. |
| ◇ Geschmackssache | Die Wahl hängt von gewünschter Darstellung, Bedienung oder Spielweise ab. | Neutrales Hellgrau; Symbol links, Bedeutung als Text in der Legende und Begründung im Tooltip. |
| Keine Markierung | Noch nicht redaktionell eingestuft. | Keine sichtbare Platzhalterbeschriftung; dieselbe Spalte bleibt im Raster. |

- Eine kurze Legende erklärt die beiden sichtbaren Markierungen einmal im Kopfbereich. Die Empfehlung ist unabhängig vom aktuellen Haken, vom Standardwert und von der Kategorie Fixes/QoL/Fixes?.
- Keine komplette Zeile einfärben und keine zusätzlichen grünen/blauen Bewertungsfarben verwenden; diese Farben kennzeichnen bereits die Rollen.
- Empfehlungen dürfen nichts automatisch aktivieren, keine Presets umschreiben und keine Pflichtoptionen erzeugen. Keine zusätzliche Schaltfläche „Alles Empfohlene aktivieren“ im ersten Rework.
- Die Zuordnung zu „Empfohlen“ oder „Geschmackssache“ ist eine redaktionelle Entscheidung von Serp. Diese Datei vergibt keine technischen Sicherheits- oder Qualitätsurteile aus Propertynamen, bisherigen Standardwerten oder dem Wort „Fix“. Ohne bestätigte Autorenempfehlung bleibt die Markierung leer.
- Zu einer bestätigten Empfehlung gehört ein kurzer lokalisierter Grund im bestehenden Tooltip. Bei Reglern muss eine konkrete Wertempfehlung ausdrücklich formuliert werden; ein Stern allein erklärt keinen sinnvollen Reglerwert. Ohne konkrete Wertempfehlung keinen Stern vergeben.
- „Fixes?“ erhält eine kurze Erklärung: Hier stehen Änderungen, deren Einordnung als Fehlerkorrektur gegenüber beabsichtigtem Vanilla-Verhalten nicht eindeutig ist. Daraus folgt keine Kennzeichnung als experimentell oder technisch instabil.
- Vorhandene fachliche Hinweise bleiben erhalten, insbesondere Einheiten an Reglern, der Neustarthinweis beim Spritesheet-Zusammenlegen und die zugehörigen Speicheranforderungen. Die Darstellung dieser Hinweise ist keine neue Performance-Bewertung.

### Schematische Ansicht

    Mod aktivieren: [Host] [Client]     Preset laden / speichern
    Zurücksetzen auf […]               Suche […]
    Rollen-/Quellenstatus
    ★ Empfohlen    ◇ Geschmackssache

    LOKALE CLIENT-OPTIONEN            Alle auf / Alle zu
      ▸ Anzeige, HUD, Audio und Leistung                 12
      ▸ Kamera, Bauen und Steuerung                       9
      ▸ Menüs, Spielstände und Kreuzzugmärsche            10
      ▸ Markt und Gütertransfer                           4

    HOST-OPTIONEN                    Alle auf / Alle zu
      ▸ Partievorbereitung, Mehrspieler und Zuschauer    18
      ▸ KI, Burgaufbau und Wirtschaftsschutz             16
      ▾ Truppenbedienung und Bewegungstempo               9
          Fixes
          ★ [x] …
          QoL
          ◇ [x] …
      ▸ Gebäude, Wirtschaft und Versorgung               13
      ▸ Tore und Belagerungsregeln                        7
      ▸ Performance Heavy                                9

Die Markierungen im Schema illustrieren ausschließlich das Zeilenraster und bewerten keine konkrete Funktion.

## 3. Vollständige Themenzuordnung für BugfixesAndQoL

**Legende:** Q = bisher QoL, F = bisher Fixes, ? = bisher Fixes?. Dies sind die aktuellen UI-Kategorien, keine neue Bewertung. Die Propertynamen ermöglichen einen eindeutigen Abgleich mit dem Quellstand; die Beschreibungen sind lesbare Kurzfassungen der vorhandenen Texte und keine fertigen neuen Locale-Texte.

### Übersicht und Größenprüfung

| Rolle | Themenbereich | Hauptoptionen | Zugehörige Details |
| --- | --- | ---: | --- |
| Client | C1 Anzeige, HUD, Audio und Leistung | 12 | Lebensbalken-Hotkey und Statistikvorschauen |
| Client | C2 Kamera, Bauen und Steuerung | 9 | Bestehende Beschreibungen und Tooltips |
| Client | C3 Menüs, Spielstände und Kreuzzugmärsche | 8 | Einladungs-Blacklist leeren |
| Client | C4 Markt und Gütertransfer | 4 | Vollständiger Wareneditor und HD-Wiederherstellung |
| Host | H1 Partievorbereitung, Mehrspieler und Zuschauer | 15 | Bestehende Rechte- und Geschwindigkeitsanzeigen |
| Host | H2 KI, Burgaufbau und Wirtschaftsschutz | 15 | KI-spezifische Bau-, Produktions- und Schutzregeln |
| Host | H3 Truppenbedienung und Bewegungstempo | 9 | Warteschlangen, Bewegungstempo und Bedienung |
| Host | H4 Gebäude, Wirtschaft und Versorgung | 13 | Pest/Apotheker als Teil der städtischen Versorgung |
| Host | H5 Tore und Belagerungsregeln | 7 | Torzustände, Abstände und Belagerungsregeln |
| Host | H6 Performance Heavy | 8 | Zusätzliche große Suchen und kumulative Erreichbarkeitsprüfungen |
| **Gesamt** | **Vier Client- und sechs Host-Themen** | **100** | **Zusätzlich Hotkey und Warenreihenfolge als Detailwerte** |

Die Verteilung wird gegenüber dem Bestand zu **33 Client- und 67 Host-Hauptoptionen**. Ursache ist ausschließlich der unten erklärte Wechsel des bereits lokalen Workshop-Lord-Auswahlfixes in den Clientbereich. Die Zahl der Spiel-/Modfunktionen wird nicht erweitert.

### C1 — Anzeige, HUD, Audio und Leistung · 12 Hauptoptionen

Alle rein darstellungsbezogenen Optionen zusammenhalten. „Audio“ und „Leistung“ bekommen trotz eigener Begriffe keine kleinen separaten Klappen. Innerhalb der Kategorien verwandte Zeilen unmittelbar aufeinander folgen lassen.

| Property | Option | Bisher |
| --- | --- | --- |
| `ShowSelectedUnitHealth` | Lebenspunkte ausgewählter Einheiten im Truppen-HUD | Q |
| `ShowForeignTroopHud` | Truppen-HUD für markierte fremde Truppen | Q |
| `EnableDamagedHealthBars` | Dauerhafte Lebensbalken beschädigter Objekte erlauben | Q |
| `ShowCountdownTimers` | Timer-Countdowns anzeigen | Q |
| `StatisticsTeamBadgeMode` | Teammarkierungen in Statistiken: Aus / Vanilla / Easy Read | Q |
| `PreserveDisplayResolution` | Gewählte Bildschirmauflösung beibehalten | F |
| `EnableGatehouseTargetMarkerHeightFix` | Höhe von Zielmarkern auf Torhäusern korrigieren | F |
| `EnableVanillaPerformanceOptimizations` | Lokale Vanilla-Leistungsoptimierungen | F |
| `EnableSpriteSheetMerge` | Einheiten-Spritesheets zusammenlegen | F |
| `EnableCompleteNotificationSkipOnClick` | Nachrichten per Rechtsklick vollständig überspringen | F |
| `EnableNotificationLastFrame` | Letztes Videobild während der Nachrichtenanzeige erhalten | ? |
| `ArabianIdleSoundVolumePercent` | Idle-Lautstärke arabischer Schwertkämpfer | ? |

`HealthBarHotkey` samt Zuweisen, Löschen und Anzeige der Tastenkombination bleibt direkt bei `EnableDamagedHealthBars`. Die Statistik-Iconvorschauen bleiben bei `StatisticsTeamBadgeMode`. Weder Detailbuttons noch Vorschaubilder werden als zusätzliche Hauptoptionen gezählt.

### C2 — Kamera, Bauen und Steuerung · 9 Hauptoptionen

Hier sucht man Änderungen an der eigenen Eingabe und an der Bau-/Bewegungsvorschau. Simulationsregeln für Tore oder Einheitengeschwindigkeit stehen weiterhin im Hostbereich.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableImprovedBuildPreview` | Bodenanforderungen beim Bauen anzeigen | Q |
| `EnableTroopHudMiddleClickCameraJump` | Per Mittelklick im Truppen-HUD zur Einheit springen | Q |
| `AllowMinimapWhilePlacingBuilding` | Minimap während der Gebäudeplatzierung bedienen | Q |
| `PreventMoveOrderOnPlacementCancel` | Beim Platzierungsabbruch keinen Bewegungsbefehl auslösen | Q |
| `AllowCameraMovementWithModifiers` | Kamera auch mit Strg/Alt bewegen | Q |
| `EnableResolutionAwareExtendedZoom` | Auflösungsabhängigen erweiterten Zoom verwenden | Q |
| `EnableMinimapCursorFollowFix` | Minimap-Mauszeiger korrigieren | F |
| `EnableDisbandedUnitControlGroupCleanup` | Aufgelöste Einheiten aus Kontrollgruppen entfernen | F |
| `EnableEnemyProximityBulldozeCursorFix` | Abreißcursor bei Feindnähe korrigieren | F |

### C3 — Menüs, Spielstände und Kreuzzugmärsche · 8 Hauptoptionen

Lokale Dialoge, Lobbydarstellung und vorbereitende Informationen gemeinsam bündeln. Einzelne Trail- oder Workshop-Korrekturen rechtfertigen keine eigene Klappe.

| Property | Option | Bisher |
| --- | --- | --- |
| `ShowLoadSaveDialogControls` | Zusätzliche Karten-, Spielstand- und Trailbuttons anzeigen | Q |
| `EnableSoloMultiplayerSaveLoading` | Multiplayer-Spielstände alleine laden | Q |
| `FixSkirmishLoadButtonVisibility` | Skirmish-Ladebutton sichtbar halten | Q |
| `ImproveYellowLobbyContrast` | Kontrastreicheres Gelb | Q |
| `EnableBriefingNoStartingGoldFix` | Korrekte „Kein Startgold“-Anzeige in der Missionsvorschau | Q |
| `EnableIngameSteamInvitePrompt` | Eingehende Steam-Einladungen im Spiel anzeigen | Q |
| `EnableCustomTrailExtremeGoldFix` | Startgold für eigene Kreuzzugmärsche korrigieren | F |
| `EnableWorkshopUploadLordSelectionFix` | Gewählten Lord nach Workshop-Upload beibehalten | Q |

**Bewusste Korrektur der Platzierung:** `EnableWorkshopUploadLordSelectionFix` steht aktuell optisch im Hostbereich, trägt im ViewModel jedoch bereits `[PresetLocal]`. Die Zeile gehört deshalb in diesen Clientbereich. Ihr Speichermodell und ihr Verhalten ändern sich dadurch nicht. Bei der Umsetzung ihre Bearbeitbarkeit aus dem Clientbereich beziehen und den bestehenden Setter-/Commandpfad beibehalten.

„Einladungs-Blacklist leeren“ bleibt als untergeordnete Aktion direkt bei `EnableIngameSteamInvitePrompt`, einschließlich der bestehenden Bestätigung. Die Aktion erhält keine eigene Empfehlungsbewertung.

### C4 — Markt und Gütertransfer · 4 Hauptoptionen plus Wareneditor

Die bewusste Ausnahme zur Richtgröße fünf ist sinnvoll: Der Wareneditor füllt bereits eine größere Fläche. Seine Einzelwaren und Pfeilbuttons werden nicht zur Schönung der Anzahl als weitere Hauptoptionen gezählt. Eine gemeinsame Klappe ist hier nützlicher als ein einzelner dauerhaft sichtbarer Großeditor.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableAllyGoodsAmountModifiers` | Mengen beim Gütertransfer mit Verbündeten anpassen | Q |
| `HdMarketView` | Konfigurierte Marktwaren-Reihenfolge verwenden | Q |
| `EnableMarketKeyMainMenuFix` | Markttasten im Hauptmenü korrigieren | F |
| `EnableAutoTradeSellZeroFix` | Auto-Verkaufsschwelle korrigieren | F |

`MarketGoodsOrder` wird über `MarketGoodsOrderItems` unmittelbar unter `HdMarketView` bearbeitet. Warenpositionen, Symbole, Vor-/Zurück-Pfeile und „HD-Reihenfolge wiederherstellen“ bleiben ein gemeinsamer Editor mit vorhandenen Bindings und Berechtigungen. Die hostgesteuerte Strg-Handelsfunktion bleibt aus Gründen der Autorität in H4; sie wird hier nicht dupliziert.

### H1 — Partievorbereitung, Mehrspieler und Zuschauer · 15 Hauptoptionen

Diese Gruppe bündelt den Ablauf einer Partie: Vorbereitung, Lobby, Zeitsteuerung, Verbindungsprobleme und Verhalten nach dem Ausscheiden. Eigene kleine Gruppen für Spielgeschwindigkeit oder Zuschauer sind damit überflüssig.

| Property / UI-Binding | Option | Bisher |
| --- | --- | --- |
| `ShowMpAdvancedSettingsInSingleplayer` | MP-Erweiterteinstellungen im Einzelspieler anzeigen | Q |
| `EnableCustomLordListEnhancements` | Auswahl- und Sortierlisten verbessern | Q |
| `EnableTrailCustomizationButtons` | Customize-Buttons für eigene und Koop-Kreuzzugmärsche | Q |
| `AllowFullAiMultiplayerLobby` | Alle Multiplayer-Plätze mit KI belegen | Q |
| `EnableReturnToMultiplayerLobby` | Nach Spielende gemeinsam in die Lobby zurückkehren | Q |
| `MultiplayerTimeControlPermissionIndex` | Wer Spielgeschwindigkeit und Pause steuern darf | Q |
| `MultiplayerGameSpeedMaximum` | Maximale Multiplayer-Spielgeschwindigkeit | Q |
| `EnableShiftGameSpeedSteps` | 25er-Schritte für Spielgeschwindigkeit | Q |
| `EnableSurrenderAndStatistics` | Aufgeben und laufende Statistiken ermöglichen | Q |
| `EnableEliminatedPlayersBecomeSpectators` | Ausgeschiedene Spieler werden Zuschauer | Q |
| `EnableSpectatorPerspective` | Erweiterte Zuschaueransicht | Q |
| `EnableAbruptHostMigrationFix` | Host-Wechsel nach abruptem Verbindungsabbruch reparieren | F |
| `EnableConnectionRecoverySave` | Sichern, bevor ein zurückliegender Spieler entfernt wird | F |
| `EnableResyncHostKick` | Getrennten Spieler und Host-Kick beim Resync anzeigen | F |
| `EnableCorruptLordDataSpawnFix` | Lord-Erzeugung bei beschädigten Kartendaten reparieren | F |

`MultiplayerTimeControlPermissionIndex` ist nur der UI-Adapter. Gespeicherte und synchronisierte Einstellung bleibt `EnableMultiplayerGameSpeedChanges`; keine zweite Einstellung aus dem Index machen. Die Lord-Erzeugungskorrektur steht hier wegen ihres Bezugs zum Karten-/Partiestart, nicht bei normalen Einheitenbefehlen.

Sollten Aufgeben/Zuschauer bis zur Umsetzung tatsächlich aus BugfixesAndQoL entfernt worden sein, die drei betroffenen Zeilen aus diesem Planabgleich herausnehmen und die Quellen dieses Umzugs prüfen. H1 enthält dann immer noch zwölf Hauptoptionen und benötigt keinen Ersatz zum Auffüllen.

### H2 — KI, Burgaufbau und Wirtschaftsschutz · 15 Hauptoptionen

KI-spezifische Bau-, Produktions- und Schutzregeln zusammenhalten. Die zusätzlichen nativen Raid-Ersatzaufträge stehen wegen ihres Performance-Bezugs in H6. Die Abrissschutzprüfung bleibt nach Codeprüfung in H2: kleiner begrenzter Portalgraph mit Tick-Caches, keine große Kachel-Suche. Eine weitere Klappebene innerhalb von H2 ist nicht erforderlich.

| Property | Option | Bisher |
| --- | --- | --- |
| `RememberAiAivSettings` | Gemeinsame AIV-/AIC-Auswahl merken | Q |
| `EnableAIQuarryPileTowardsKeep` | KI-Steinhaufen zum Bergfried ausrichten | Q |
| `EnableAiStoneReserveFix` | Stein für ausstehende KI-Burggebäude zurückhalten | F |
| `EnableAiDefensePatrolFix` | KI-Burgverteidigung wieder auffüllen | F |
| `EnableAiWallTargetingFix` | KI-Mauerzielauswahl verbessern | F |
| `EnablePrebuiltAiWorkshopBothFix` | Produktion vorgebauter KI-Werkstätten ausgleichen | F |
| `EnableAivDefenderPositionFix` | Ignorierte AIV-Verteidigungspositionen wiederherstellen | F |
| `FixAIPreplacedMapBuildings` | KI-Probleme durch vorplatzierte Kartengebäude beheben | F |
| `FixAITowerRepair` | KI-Turmwiederaufbau reparieren | F |
| `BetterAIOverbuildRules` | Bessere KI-Überbauregeln | F |
| `PreventAIPause` | KI-Schlafmodus bei Rohstoffmangel verhindern | F |
| `PreventEmergencyDemolition` | KI-Panikabriss verhindern | F |
| `PreventHovelDeletion` | KI-Hüttenabriss verhindern | F |
| `InaccessibleAIBuildingDemolitionProtection` | Umgang mit unerreichbaren KI-Gebäuden auswählen | F |
| `RemoveAIKeepRangeLimit` | KI-Baureichweite nicht begrenzen | ? |

Bei weiterem Wachstum zuerst die gewachsene tatsächliche Optionsmenge prüfen. Eine spätere Teilung in „KI-Burgbau und Wirtschaft“ sowie „KI-Militär“ erst empfehlen, wenn beide inhaltlich genügend Hauptoptionen besitzen; nicht jetzt für eine Gruppe mit nur wenigen militärischen Optionen teilen.

### H3 — Truppenbedienung und Bewegungstempo · 9 Hauptoptionen

Bedienung, Befehlswarteschlangen, Bewegungstempo und leichte Pfadkorrekturen zusammenführen. Zusätzliche potenziell große Suchen und häufige Graph-Wiederholungen stehen in H6. Stockpile-Zulässigkeit, Assassin-Wiederaufnahmekontext und Sammelpunktfreigabe bleiben hier: Im Fix selbst ist keine vergleichbare zusätzliche große Suche bestätigt. Dass eine Warteschlange später Bewegungsbefehle ausführt, macht sie ebenfalls noch nicht zu einem eigenen Pathfinding-Verfahren oder nachgewiesenen Performance-Hotspot.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableExtendedShiftCommandQueue` | Erweiterte Shift-Befehlswarteschlangen | Q |
| `EnableFastRecruitRallyMovement` | Rekruten laufen zu Sammelpunkten | Q |
| `EnableRallyTerrainSlowdownFix` | Geländeverlangsamung auf dem Weg zum Sammelpunkt korrigieren | Q |
| `EnableTroopMovementFix` | Truppen-Geschwindigkeitskorrektur | F |
| `EnableMountedStockpileMovementFix` | Stockpile-Bewegung berittener Einheiten | F |
| `EnableHealerAttackCommandFix` | Heiler aus Nahkampf-Angriffsgruppen heraushalten | F |
| `EnableAssassinCombatResumeFix` | Meuchelmörder-Bewegung nach Kämpfen fortsetzen | F |
| `EnableAssemblyPointPlacementFix` | Sammelpunkt-Platzierung korrigieren | F |
| `EnableLordUnitControls` | Lord-Steuerung im Truppen-HUD | Q |

### H4 — Gebäude, Wirtschaft und Versorgung · 13 Hauptoptionen

Gebäudebedienung, Produktionsabläufe und städtische Versorgung bündeln. Wasserträger und Apotheker gehören funktional hierher; die vier Pestoptionen benötigen keine separate Klappe. KI-spezifischer Abrissschutz bleibt in H2.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableSingleBuildingPause` | Einzelne Gebäude pausieren | Q |
| `EnableNearestWaterboyTargeting` | Nächsten Wasserträger zu jedem Brand schicken | Q |
| `EnableShiftRepairAllBuildings` | Alle Gebäude mit Shift reparieren | Q |
| `EnableQuarryPileRelocation` | Steinhaufen-Rotation | Q |
| `EnableCtrlSingleMarketTrade` | Mit Strg eine Markteinheit handeln | Q |
| `EnableKeepFlagRotationFix` | Hauptflaggen mit den Bergfrieden drehen | F |
| `EnableTanneryAnimationFix` | Gerberei-Animation einblenden | F |
| `EnableBakerMillerBreaks` | Pausen für Bäcker und Müller nach der Ablieferung | ? |
| `EnableWorkshopIdleDelayFix` | Warteverzögerung für Piekenmacher und Gerber entfernen | ? |
| `EnablePlaguePopularityFix` | Pest-Beliebtheit korrigieren | F |
| `EnablePlagueCloudRemovalFix` | Pestwolken-Entfernung korrigieren | F |
| `EnableStuckApothecaryFix` | Feststeckende Apotheker korrigieren | F |
| `EnablePlagueTargetReservationFix` | Apotheker-Zielreservierung korrigieren | F |

### H5 — Tore und Belagerungsregeln · 7 Hauptoptionen

Gemeinsamer Ort für Bau-/Eroberungsregeln an Befestigungen. Enemy-Gate-Zusatzsuchen und die häufige Torschließ-Erreichbarkeitsprüfung stehen in H6, die lokalen Zielmarker weiterhin in C1. Direktes Assassin-Torhausklettern bleibt hier, weil sein eigener Code nur Übergangs-/Endpunktregeln korrigiert und keine weitere A*-Suche startet. Bestehende Tooltips können diese fachliche Abgrenzung kurz erwähnen; keine Optionen mehrfach anzeigen.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableCenteredGatehouseDistanceFix` | Torhaus-Schließdistanz von der Mitte messen | Q |
| `EnableFairSiegeAmmoRestock` | Belagerungsmunition gleichmäßig nachfüllen | Q |
| `EnableGatehouseLivingCaptureFix` | Tote Einheiten bei Torhauseroberung ignorieren | F |
| `EnableDrawbridgeDestructionFix` | Zugbrücken bei Schaden am noch lebenden Torhaus erhalten | F |
| `EnableAssassinGatehouseClimbFix` | Torhäuser direkt mit Assassinen erklimmen | F |
| `EnableTunnelPlacementDistanceFix` | Mindestabstand zu Feindbauten und -gräben korrigieren | F |
| `EnableAssassinCapturedGateProtectionFix` | KI-Assassinen greifen eroberte Torhäuser nicht erneut an | ? |

### H6 — Performance Heavy · 8 Hauptoptionen

**Schwerpunkt: tatsächlich zusätzliche Such-/Prüfarbeit.** Der vollständige Codeabgleich der 13 Kandidaten vom 23.10.2026, einschließlich der hashgeprüften nativen Suchverträge, steht in [PerformanceFix.md](PerformanceFix.md). Sechs Optionen besitzen potenziell große Zusatzsuchen bzw. Planerproben; zwei weitere können durch zahlreiche begrenzte Graphprüfungen relevant werden. Neue Millisekunden- oder FPS-Versprechen sind damit nicht verbunden.

| Property | Option und Grund der Zuordnung | Bisher |
| --- | --- | --- |
| `EnableImprovedAssassinPathfinding` | Native Suche plus gewichtetes A* oder Existenzsuche; bei negativen Zielen kann eine große Fläche untersucht werden. | Q |
| `EnableEnemyGatePathfindingFix` | Gate-Cursorvalidierung kann bis zu zwei zusätzliche native Kachelsuchen mit großem Budget ausführen; dazu häufige Filter und Topologiepflege. | F |
| `EnableMoveFormationEnhancements` | BFS für erreichbare Formationsziele plus Ersatzslotzuordnung; Aufwand wächst mit Suchfläche und Auswahl. | Q |
| `EnableImprovedManualUnitCommands` | Native Planerproben mit rund 5 MiB Scratch-Rückkopie je tatsächlicher Probe; viele negative Mitgliederprüfungen können sich summieren. | Q |
| `EnableImprovedMoatFilling` | Zusätzliche gewichtete Bodenpräfixsuche beim passenden Grabenarbeitsweg, gegebenenfalls mit Verfeinerung; nicht nur Prüfung weniger Nachbarfelder. | Q |
| `EnableAiRaidRetargetFix` | Bis vier neue native Gebäudeangriffsaufträge je wartender Raidgruppe und Tick; kein globales Vier-Aufträge-Budget. | F |
| `RequireReachableEnemyForAutomaticGateClosing` | Kumulative Kategorie: viele Torabfragen mit begrenzter Portalgraphsuche, Distanzfilter und Tick-/Pass-Caches. Keine volle Kachel-BFS. | Q |
| `EnableLadderAttackPathfindingFix` | Kumulative Kategorie: negative Regionsabfrage kann eine weitere native Portalgraphsuche im Gruppenmodus auslösen; kein eigener Vollkarten-A*-Algorithmus. | F |

Die ersten sechs Einträge besitzen im Code bestätigtes Potenzial für große zusätzliche Arbeit. Die beiden letzten sind wegen möglicher hoher Wiederholungszahl aufgeführt; ihre Suche ist anders begrenzt. Bei allen Optionen Aufwand und Auslöser konkret erklären. Kategoriezugehörigkeit allein rechtfertigt weder eine pauschale Warnung „stark verlangsamend“ noch die Empfehlung, sämtliche Einträge abzuschalten. Cachehits, frühe Ablehnungen, bereits vorhandene Optimierungen und das tatsächliche Kartenszenario bleiben entscheidend.

Für den späteren Nutzerhinweis pro Option unterscheiden: **Simulation/CPU**, **situationsabhängige Suchspitzen**, **Rendering/GPU**, **Grafikspeicher** und **Start-/Ladezeit**. Nur tatsächlich passende Aspekte zeigen. Ein vorhandener Suchpfad begründet „potenziell aufwendig“, noch keine gemessene Größenordnung. Empfehlungen wie „sollte immer aktiv sein“ bleiben davon unabhängig.

**Bewusste Abgrenzung:** Gruppen-Geschwindigkeit, schnellere Rekruten und Geländeverlangsamung betreffen im Plan H3. Heiler aus Nahkampfgruppen ausschließen, KI-Mauerzielreservierungen, Wasserträger-Zuweisung, Apotheker-Zielreservierungen, Torhaus-Schließdistanz und Eroberungsregeln bleiben in ihren fachlichen Bereichen. Aus Bewegung, Zielauswahl oder einem Bestandteil der gemeinsamen X1PATH-Abschaltgruppe allein folgt noch keine einzeln belegte schwere Wegsuche. Werden für eine dieser Optionen relevante Kosten nachgewiesen, sie gezielt in „Performance Heavy“ verschieben oder am bisherigen Ort mit demselben Performance-Hinweis kennzeichnen; keine zweite Kopie des Schalters anlegen.

Die fünf ausgeschiedenen Kandidaten sind in H2/H3/H5 wieder zugeordnet. Ihr Codebefund ist in `PerformanceFix.md` einzeln erläutert. Insbesondere Stockpile-Cursorprüfungen können eine große Auswahl linear prüfen; „keine schwere Suche“ ist keine Behauptung „kostenlos“.

### Vorhandene Performance-Nachweise · gesichtet am 23.10.2026

Die PerfProbe-Ergebnisse sind im Workspace erhalten, überwiegend unter dem Git-ignorierten Analysepfad [.work/a/bfqol-perf/001/results](.work/a/bfqol-perf/001/results). Bei der Sichtung lagen dort 301 Logdateien und 14 Serienzusammenfassungen. Zugehörige Treiber, Originalsettings und Mess-DLLs liegen im übergeordneten Analyseordner. Diese Dateien erhalten; die folgenden Links sind lokale Nachweise, keine Zusicherung, dass sie in einem frischen Clone vorhanden sind.

| Nachweis | Aussage für die Einordnung | Grenze |
| --- | --- | --- |
| [E-Serie](.work/a/bfqol-perf/001/results/E-series.txt) | `EnableEnemyGatePathfindingFix=false` wurde gezielt getestet: E1/E2NOGATE mit 89,6/88,5 Ticks/s, E1BFQ mit 86,4 Ticks/s. Die FPS lagen in diesen drei Läufen eng beieinander, ungefähr 206–209. | Historisches szenarioabhängiges Signal für Simulationskosten; keine isolierte aktuelle CPU-Kostenmessung und kein Nachweis eines allgemeinen FPS-Verlusts. |
| [R1-Serie](.work/a/bfqol-perf/001/results/R1-series.txt), [S-Serie](.work/a/bfqol-perf/001/results/S-series.txt) | Eigene Varianten mit `RequireReachableEnemyForAutomaticGateClosing=false` sowie Baseline-/Modläufe existieren. | Ticks/s allein trennt Mehraufwand nicht sicher von verändertem Spielablauf; Streuung und Quellstand beachten. |
| [Lag-Auswertung vom 22.10.](.work/a/perf-lag/001/findings.json) und [zugehöriger Log](.work/a/perf-lag/001/LogOutput.log) | Tor-Erreichbarkeit war in zwei Simulationsspitzen an prominenten innersten verwalteten Samplestellen sichtbar. | Die Auswertung schließt ausdrücklich aus, daraus alleinige Ursache oder exakte CPU-Zeit abzuleiten; Sampler- und Sperrwartezeiten mitbetrachten. |
| [X1-Serie](.work/a/bfqol-perf/001/results/X1-series.txt) | `X1PATH` schaltet 13 Bewegungs-/Pathfinding-Optionen gemeinsam ab; zusätzlich existieren eine KI-Gruppe und eine Tor-Erreichbarkeitsvariante. | Ein Gruppenversuch ist keine Einzelmessung für jede der 13 Optionen. Der frühe `X1OFF`-Lauf hing und zählt nicht als Performance-Ergebnis. |
| [Neuere Laufdaten L1BFQ](.work/a/bfqol-perf/001/results/L1BFQ_LogOutput.log), [L2BFQD](.work/a/bfqol-perf/001/results/L2BFQD_LogOutput.log) | Weitere Sampler-/DEVDIAG-Rohdaten vom 23.10. sind vorhanden und können zur Prüfung späterer Optimierungsstände dienen. | Einzelne Rohlog-Samples sind noch kein abgeschlossener Vergleich oder Beleg eines heutigen unveränderten Kostenanteils. |
| [V-Serie](.work/a/bfqol-perf/001/results/V-series.txt) | `EnableVanillaPerformanceOptimizations=false` wurde separat getestet; die protokollierten FPS lagen deutlich unter dem dortigen Mod-Vergleichslauf. | Zeigt, warum leistungssteigernde Optionen nicht als „teuer, besser aus“ eingeordnet werden dürfen. Das ist keine neue Zusage gleicher Gewinne auf jeder Karte. |

Der in älteren Treibern und `AGENTS/performance.md` erwähnte Sammelbericht `BFQoLPerformace.md` wurde am Workspace-Root und in der gezielten Dateisuche nicht gefunden. Die oben verlinkten Serien und Rohlogs sind jedoch vorhanden; nicht aus dem fehlenden Bericht auf fehlende Messungen schließen. Keine neue Messreihe wurde für diese Planergänzung gestartet.

**Vorgehen vor endgültigen Kostenkennzeichen:** Vorhandene Nachweise zuerst nach Save, Laufvariante, gemessener Phase und damaliger Build-/DLL-Identität zuordnen. Spätere Optimierungen können frühere Kostenbefunde überholen. Danach nur offene Einzelzuordnungen bei Bedarf messen: gleiche Szene, Zoom, Geschwindigkeit und Diagnosekonfiguration, abwechselnde Vergleichsläufe sowie Simulations- und Renderkosten getrennt. Für Einzelkosten Sampler-/Zeitbefunde heranziehen, nicht bloß Ticks/s oder das Auftreten eines Modnamens im Gesamtstack. Historische Artefakte unverändert erhalten.

## 4. Weitere Einstellungen, Detailaktionen und Verwaltung

Diese Elemente werden bewusst nicht als zusätzliche Hauptoptionen einer Klappe gezählt. Damit sind auch die im ViewModel gespeicherten Detailwerte und die generischen Verwaltungscontrols berücksichtigt.

| Property / Element | Platzierung und Behandlung |
| --- | --- |
| `HealthBarHotkey` | Detailwert in C1 direkt bei den Lebensbalken; Zuweisen/Löschen und Tastenerfassung erhalten. |
| `MarketGoodsOrder` / `MarketGoodsOrderItems` | Detailwert/-editor in C4 bei `HdMarketView`; alle Waren und Nachbar-Tauschbefehle erhalten. |
| `ShowVanillaMapsInEditor` | Vorhandener Schalter im Karten-/Dateidialog bleibt dort; fachlich C3 zugeordnet. Im ersten Rework keine zusätzliche, doppelte Modsettings-Zeile anlegen. |
| `EnableMod` / `HostSettingsEnabled` | Bestehender Host-Aktivierungswert und seine UI-Anbindung im Aktivierungskopf; keine neue fachliche Option. |
| `EnableClientFeatures` / `ClientSettingsEnabled` | Bestehender persönlicher Aktivierungswert und seine UI-Anbindung im Aktivierungskopf. |
| `EnableMultiplayerGameSpeedChanges` | Tatsächlich gespeicherter Enumwert hinter der in H1 gezählten ComboBox `MultiplayerTimeControlPermissionIndex`. |
| `MultiplayerSafetyCompatibilityReport` | Interner synchronisierter Kompatibilitätsbericht mit `DoNotPersist`; kein Benutzerschalter und keine Empfehlungsmarkierung. |
| `ClearSteamInviteBlacklistCommand` | Untergeordnete Aktion in C3 direkt bei Steam-Einladungen. |
| `RestoreHdMarketOrderCommand`, `MovePreviousCommand`, `MoveNextCommand` | Untergeordnete Aktionen des Wareneditors in C4. |
| `AssignHealthBarHotkeyCommand`, `ClearHealthBarHotkeyCommand`, `HealthBarHotkeyInputCommand` | Untergeordnete Hotkey-Aktionen in C1; Tastenerfassung auch nach Einklappen/Seitenwechsel sauber beenden. |
| Preset laden | Presetauswahl, Beschreibung, Laden, Löschen, Abbrechen und zugehörige Bestätigungen bleiben im bestehenden Verwaltungsbereich. |
| Preset speichern | Ziel, Name, Beschreibung, Sammelmodus, individuelle Property-Modi und Speichern/Abbrechen bleiben im bestehenden Verwaltungsbereich. |
| Zurücksetzen | Quellenauswahl und Anwenden bleiben im Kopfbereich; kein themenweises Zurücksetzen ergänzen. |
| Suche | Suchtext, Tooltips einbeziehen, Ein-/Ausblenden, Leeren und „keine Treffer“ bleiben im Kopfbereich. |
| Status- und Quellenhinweise | Fehleranzeige mit Schließen, Presetbestätigung, Rollenstatus, Direktstart-/Trailhinweise und Legacy-Warnung bleiben außerhalb der Klappen. |

Formationsparameter wie Aufstellungsart, Dichte, Rollenmarker und gemerkte Reihen gehören zu ihrer bestehenden Ingame-Bedienung und sind keine zusätzlichen Lobby-Modsettings dieser XAML. Aus Locale-Einträgen allein keine weiteren Modsettings ableiten.

## 5. Umsetzungsempfehlung

### Schritt 1 — Bestand und Themenstruktur sichern

1. Vor Änderungen XAML und klassifizierte ViewModel-Properties erneut inventarisieren; den dann aktuellen Arbeitsstand einschließlich uncommitteter Änderungen verwenden. Neue oder inzwischen ausgelagerte Funktionen im Planabgleich berücksichtigen.
2. Die zehn Themenbereiche entsprechend der Tabellen aufbauen, einschließlich „Performance Heavy“ mit Schwerpunkt Wegfindung/Erreichbarkeit. Kategoriezugehörigkeit, Optionstitel, Standardwerte, Bindings, Commands und gespeicherte Propertynamen zunächst erhalten.
3. Den lokalen Workshop-Lord-Auswahlfix in C3 einordnen. Keine anderen Host-/Client-Zuordnungen aus der Themenähnlichkeit ableiten und keine Syncattribute verändern.
4. Gemeinsam verwendete Aktivierungs-, Preset- und Suchoberfläche erhalten. Das Rework betrifft die Organisation der bestehenden Optionen, nicht deren Spielverhalten.

### Schritt 2 — Klappverhalten, Suche und Rechte

- Den Themenkopf außerhalb des durch `CanEditHostSettings` bzw. `CanEditClientSettings` gesperrten Inhalts anordnen. Ein Client muss Hostthemen weiterhin öffnen, lesen und durchsuchen können, auch wenn er ihre Werte nicht ändern darf. Nur die fachlichen Eingaben mit den bestehenden Rechten sperren.
- Klappzustand ist reiner lokaler Ansichtsstatus. Nicht als Presetproperty klassifizieren, nicht synchronisieren und nicht in Missions-/Trailwerte aufnehmen. Änderungen ereignisbasiert behandeln; kein Polling für Klappzustände oder Empfehlungsanzeigen.
- Deklarativen Inhalt beim Schließen erhalten. Keine verzögerte Erzeugung verwenden, die geschlossene Themen aus dem Suchindex entfernt. Die Noesis-Unterstützung des konkreten Expander-/Templates und das Verhalten geschlossener Inhalte an einem Thema zuerst praktisch prüfen.
- Bei aktiver lokaler Suche oder einem exakten Treffer aus der globalen Modsettings-Suche Klappzustände vorübergehend übersteuern: passende Themen öffnen, Themen ohne sichtbare Treffer ausblenden und ihre passenden Kategorieüberschriften anzeigen. Ein Suchtreffer muss ohne weiteren Öffnungsklick sichtbar sein.
- Beim Leeren der Suche die zuvor manuell gewählten Klappzustände wiederherstellen. Während der Suche keine dauerhaften Änderungen an diesem Merkspeicher vornehmen; die Sammelbefehle zum Auf-/Zuklappen vorübergehend ausblenden.
- Vorhandene APIShared-Suche und die individuellen Suchschlüssel weiterverwenden. Neue Themen erhalten eindeutige rollenbezogene Section-Metadaten; gemeinsame neue Themenüberschriften müssen auffindbar sein. Alte Zwischenüberschriften nicht als verwaiste Suchziele zurücklassen. Keine separate Suchregistrierung im Mod anlegen.
- Dynamischen Wareneditor ausdrücklich mitprüfen: Treffer zu Waren und Wiederherstellungsaktion müssen C4 sichtbar machen. Ein bestandener Text-/XAML-Audit allein belegt diese Laufzeitfunktion nicht.

### Schritt 3 — Gestaltung und Autorenempfehlungen

- Zuerst Gliederung und Klappen im Spiel beurteilen, anschließend das einheitliche Zeilenraster und die beiden Markierungen ergänzen. Die Tabellen dieses Plans enthalten bewusst keine ungeprüften Sterne pro Funktion.
- Empfehlungsmetadaten an die eindeutige jeweilige Option knüpfen, unabhängig von Reihenfolge, geöffnetem Thema und aktuellem Wert. Nicht an übersetzten Titeltexten oder einer XAML-Zeilennummer erkennen.
- Für das erste Rework kann die Autorenklassifikation lokal im Mod bleiben; eine Erweiterung von APIShared ist nur bei nachgewiesenem Bedarf für mehrere Verbraucher sinnvoll. Eine neue öffentliche API wird durch diesen Plan nicht vorausgesetzt.
- Keine Kennzeichen in die eigentlichen Checkboxtitel einbauen, wenn dadurch automatische Suchtitel verunreinigt würden. Markierungen separat anzeigen; sie sind keine zusätzlichen Such-/Spieloptionen.
- Neue Texte für Themen, Klappbefehle, Legende und Empfehlungserklärungen in allen vorhandenen Sprachen ergänzen; bei fehlender Übersetzung Englisch verwenden. Die bestehenden deckenden Tooltips mit 60000 ms beibehalten und neue interaktive Klapp-/Sammelcontrols ebenfalls passend erklären.
- Äußeren ScrollViewer mit automatischen horizontalen und vertikalen Scrollleisten erhalten. Bei schmaler Ansicht Beschriftungen umbrechen lassen und Detaileditoren an den verfügbaren Platz anpassen, statt feste Mindestbreiten über den gesamten Themenbereich zu erzwingen.

## 6. Prüfungen und Abnahmekriterien für die spätere Umsetzung

### Vollständigkeit und Verhalten

- Alle zum Umsetzungszeitpunkt vorhandenen Hauptoptionen sind genau einem Thema zugeordnet. Für den hier dokumentierten Stand muss der Abgleich 100 eindeutige Hauptoptionen ohne Lücken oder Duplikate ergeben; diese Zahl ist kein Vertrag für spätere Featurestände.
- Hotkey, Warenreihenfolge, Dialogschalter und Verwaltungsfunktionen sind zusätzlich erhalten. Der Zeitsteuerungsindex erzeugt keine zweite persistierte Property.
- Öffnen, Schließen, Sammelbefehle und Navigation verändern keinen Spielwert. Mehrere offene Themen, Seitenwechsel und Wiederherstellung des Sitzungszustands funktionieren.
- Suchen funktioniert bei vollständig geschlossenen Themen, anhand von Titeln und Tooltips, über neue Themenüberschriften und über exakte globale Treffer. Anschließendes Leeren stellt die manuelle Ansicht wieder her. Auch die dynamischen Warencontrols prüfen.
- Als Nicht-Host können Hostthemen gelesen und aufgeklappt, ihre Werte aber weiterhin nicht verändert werden. Host-, Client-, Singleplayer- und bestehende Trail-Sperren bleiben wirksam.
- Keine Änderung von Propertynamen, Standardwerten, Syncattributen, Presetinhalten oder fachlichen Commands durch die reine Neuordnung. Bei Änderungen an der tatsächlichen Presetintegration die relevanten Host-/Client-Presettests gemäß Projektanweisungen ausführen.

### Optische Abnahme

- Geschlossen sind vier Client- und sechs Hostthemen überschaubar sichtbar. Es gibt keinen nur ein oder zwei Hauptoptionen umfassenden Themenbereich.
- Die neun aktuell zugeordneten Optionen mit potenziell großer Zusatzarbeit bzw. kumulativen Suchkosten einschließlich der getrennten KI-Assassin-Option sind in „Performance Heavy“ gemeinsam auffindbar. Die fünf leichteren Pfadkorrekturen bleiben in ihren fachlichen Themen. Performance-Hinweise und Autorenempfehlungen sind unabhängig erkennbar.
- Alle geöffneten Themen sind lesbar; lange deutsche Texte, schmale Fenster und die unterstützten UI-Skalierungen erzeugen keine überlappenden Controls oder abgeschnittenen Empfehlungskennzeichen.
- Checkboxen, Regler, ComboBoxen und Hotkeys behalten klare Titel und passende Einheiten. Die Empfehlung ist auch bei ausgeschalteter oder gesperrter Option verständlich.
- Wareneditor, Statistikvorschau, Steam-Blacklist-Aktion und Hotkey-Erfassung funktionieren nach Einklappen und erneutem Öffnen.
- Ein Stern bezeichnet die Autorenempfehlung, nicht „aktiv“ oder „technisch sicher“. Ohne bestätigte Einstufung wird keine Markierung erfunden.

### Technische Prüfung und Abschluss

- Für die geänderten Quellen die passenden XAML-, Tooltip-, Locale-Paritäts- und CRLF-Prüfungen auswählen; gemeinsame Prüfungen auf BugfixesAndQoL und tatsächlich berührte Abhängigkeiten begrenzen.
- Reiner Textplan: kein Runtime-Build und keine Gameplaytests erforderlich. Bei der späteren Implementierung erst die dann einschlägigen Runtime-/APIShared-/Buildanweisungen lesen und anwenden; neue öffentliche API oder Persistenzänderungen sind keine stillschweigende Voraussetzung dieses Plans.
- Vor Freigabe die tatsächliche Darstellung im Spiel prüfen. Ein erfolgreicher Build ersetzt die optische und interaktive Abnahme nicht.
- README-Erweiterung nach Fertigstellung gesondert erfragen. Keine Versionsänderung, Veröffentlichung oder Übertragung des Reworks auf andere Mods allein aufgrund dieser Planungsdatei.
