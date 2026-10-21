# Modsettings-Rework: Themenbereiche und Empfehlungen

## 1. Ziel und Grundlage

Die Modsettings sollen nach Variante B aufgebaut werden: **Client/Host → aufklappbarer Themenbereich → Fixes / QoL / Fixes? → Optionen**. Nutzer sollen zuerst ihr Thema finden und dort alle zugehörigen Einstellungen gemeinsam sehen. Die bisherige fachliche Einordnung bleibt innerhalb des Themas erhalten.

Diese Datei ist ein Umsetzungsplan mit Gestaltungsempfehlungen. Der erste konkrete Anwendungsfall ist BugfixesAndQoL; andere Mods können das Gestaltungsmuster später übernehmen. Dieser Auftrag verändert ausschließlich diese Planungsdatei.

### Bestandsaufnahme vom 21.10.2026

- Geprüft wurde der lokale Arbeitsstand auf `develop`, Basis-HEAD `a4373f6e8477d94ff5374370ae68a254506bbeea`, einschließlich der am 21.10.2026 gegen 17:04 Uhr MESZ vorhandenen uncommitteten Änderungen. Der Commit allein beschreibt diesen Stand deshalb nicht vollständig.
- Maßgeblich sind [Settings-XAML](BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml), [ViewModel](BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs) und [deutsche Texte](BugfixesAndQoL/Locales/de-DE.txt). Generierte oder installierte Kopien wurden nicht als Quelle verwendet.
- In der XAML stehen **100 Hauptoptionen**: 32 im Clientbereich und 68 im Hostbereich. Gezählt wurden fachliche CheckBox-, Slider- und ComboBox-Eingaben nach Beginn der Rollenbereiche; Verwaltung, Hotkey-Aktionen und Wareneditor wurden gesondert betrachtet.
- Zusätzlich gehören `HealthBarHotkey` und `MarketGoodsOrder` als bearbeitbare Detailwerte zu bestehenden Optionen. `ShowVanillaMapsInEditor` wird im Karten-/Dateidialog bedient und besitzt derzeit keine eigene Zeile auf dieser Modsettings-Seite.
- Die beiden neuen Optionen `EnableVanillaPerformanceOptimizations` und `EnableSpriteSheetMerge` sind bereits berücksichtigt. Die drei aktuellen Optionen für Aufgeben/Statistiken und Zuschauer sind ebenfalls noch Bestandteil dieser XAML. Ein anderer neuer Modordner ist für sich kein Beleg, dass sie hier bereits entfallen sind.
- Die Zuordnung unten ist eine datierte Bestandsaufnahme, keine dauerhafte Erwartung an Optionsnamen oder -anzahl. Vor der Umsetzung gegen den dann aktuellen Arbeitsstand abgleichen; bestehende parallele Änderungen erhalten.

## 2. Empfohlener optischer Aufbau

### Rollen und Klappbereiche

- Gemeinsamer Kopf mit Aktivierung, sichtbar beschrifteter Presetverwaltung, Zurücksetzen, Suche und Rollen-/Quellenhinweisen. Keine dieser Funktionen in einen fachlichen Klappbereich verlegen.
- Die beiden Rollenbereiche bleiben gleichzeitig sichtbar; ihre bestehenden grünen Client- und blauen Host-Akzente bleiben erhalten. Keine zusätzlichen Rollen-Tabs oder seitliche Navigation.
- Die Rolle ist eine feste Überschrift, darunter folgt genau **eine** aufklappbare Ebene. Mehrere Themen dürfen gleichzeitig geöffnet sein.
- Beim ersten Öffnen sind alle Themen geschlossen, sodass die gesamte Gliederung schnell erfassbar ist. Öffnungszustände während der Sitzung beim Wechsel der Modseite erhalten; für den ersten Schritt keine dauerhafte Speicherung über Spielneustarts.
- Neben der Themenüberschrift stehen ein eindeutiger Auf-/Zu-Pfeil und die Zahl der Hauptoptionen. Keine Gruppencheckbox: Ein Klick auf den Kopf öffnet oder schließt ausschließlich den Bereich.
- Pro Rollenbereich gibt es „Alle aufklappen“ und „Alle zuklappen“. Diese Befehle verändern keine Einstellungen.
- Zielgröße sind ungefähr **5–16 Hauptoptionen** je Thema, mit begründeten Ausnahmen für umfangreiche Editoren. Keine eigenständigen Klappen nur für Audio, Pest, Zuschauer oder eine einzelne Korrektur anlegen. Themen nicht durch sachfremde Optionen oder einzeln gezählte Editorbuttons künstlich vergrößern.
- Innerhalb eines Themas erscheinen die vorhandenen Kategorien in der Reihenfolge **Fixes, QoL, Fixes?**. Leere Kategorien entfallen; die übrigen sind einfache, dezente Zwischenüberschriften ohne weitere Klappe. Ein oder zwei Einträge unter solch einer Zwischenüberschrift sind unproblematisch, weil dafür kein zusätzlicher Navigationsschritt entsteht.
- Die Tabellen unten legen die Themenzuordnung fest. Innerhalb jeder Kategorie die Tabellenreihenfolge beibehalten. Die Kategorie wird im ersten Rework aus dem bisherigen Bestand übernommen, auch wenn der Propertyname anders klingt.
- Größere Editoren wie Warenreihenfolge und Hotkey-Zuweisung bleiben unmittelbar bei ihrer Option und werden mit dem Thema geschlossen. Kein zusätzlicher Scrollbereich innerhalb eines Klappbereichs.

### Optionszeilen und Markierungen

Einheitliches Zeilenraster: links das eigentliche Control mit umbrechender Beschriftung, rechts eine schmale Spalte für die Autorenempfehlung. Regler, Auswahllisten, Werttexte und Detailaktionen behalten ihren fachlichen Zusammenhang. Bei schmalen Fenstern darf die Empfehlung unter die Beschriftung rücken; sie darf den Titel nicht abschneiden.

| Kennzeichnung | Bedeutung | Darstellung |
| --- | --- | --- |
| ★ Empfohlen | Der Autor empfiehlt, diese Funktion normalerweise dauerhaft aktiviert zu lassen. | Dezentes Gold, stets mit Text. |
| ◇ Geschmackssache | Die Wahl hängt von gewünschter Darstellung, Bedienung oder Spielweise ab. | Neutrales Hellgrau, stets mit Text. |
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
      ▸ Menüs, Spielstände und Kreuzzugmärsche             8
      ▸ Markt und Gütertransfer                           4

    HOST-OPTIONEN                    Alle auf / Alle zu
      ▸ Partievorbereitung, Mehrspieler und Zuschauer    15
      ▸ KI, Burgaufbau und Wirtschaftsschutz             16
      ▾ Truppen, Befehle und Bewegung                    14
          Fixes
            [x] …                             ★ Empfohlen
          QoL
            [x] …                             ◇ Geschmackssache
      ▸ Gebäude, Wirtschaft und Versorgung               13
      ▸ Tore, Gräben und Belagerung                        9

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
| Host | H2 KI, Burgaufbau und Wirtschaftsschutz | 16 | Modusauswahl für Abrissschutz |
| Host | H3 Truppen, Befehle und Bewegung | 14 | Bestehende Beschreibung der Befehlsfunktionen |
| Host | H4 Gebäude, Wirtschaft und Versorgung | 13 | Pest/Apotheker als Teil der städtischen Versorgung |
| Host | H5 Tore, Gräben und Belagerung | 9 | Torverhalten, Wege und Belagerungsregeln |
| **Gesamt** | **Vier Client- und fünf Host-Themen** | **100** | **Zusätzlich Hotkey und Warenreihenfolge als Detailwerte** |

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

### H2 — KI, Burgaufbau und Wirtschaftsschutz · 16 Hauptoptionen

Alle KI-spezifischen Verhaltensoptionen zusammenhalten. Wirtschaftsschutz, Burgbau und Kampfzielauswahl sind hier unterschiedliche Aspekte derselben Zielgruppe. Eine weitere Klappebene würde den Nutzen der Zusammenfassung wieder verringern.

| Property | Option | Bisher |
| --- | --- | --- |
| `RememberAiAivSettings` | Gemeinsame AIV-/AIC-Auswahl merken | Q |
| `EnableAIQuarryPileTowardsKeep` | KI-Steinhaufen zum Bergfried ausrichten | Q |
| `EnableAiStoneReserveFix` | Stein für ausstehende KI-Burggebäude zurückhalten | F |
| `EnableAiRaidRetargetFix` | KI-Raidziele ohne Nahkampfzugang überspringen | F |
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

### H3 — Truppen, Befehle und Bewegung · 14 Hauptoptionen

Allgemeine Einheitensteuerung und Wegausführung zusammenführen. Assassinen bleiben hier, soweit es um allgemeine Bewegung geht; torhausspezifisches Klettern und Erobern steht in H5.

| Property | Option | Bisher |
| --- | --- | --- |
| `EnableExtendedShiftCommandQueue` | Erweiterte Shift-Befehlswarteschlangen | Q |
| `EnableMoveFormationEnhancements` | Aufstellungen und vollständige Zielmarker | Q |
| `EnableImprovedManualUnitCommands` | Verbesserte manuelle Einheitenbefehle | Q |
| `EnableImprovedMoatFilling` | Verbessertes Zuschütten von Gräben | Q |
| `EnableFastRecruitRallyMovement` | Rekruten laufen zu Sammelpunkten | Q |
| `EnableRallyTerrainSlowdownFix` | Geländeverlangsamung auf dem Weg zum Sammelpunkt korrigieren | Q |
| `EnableImprovedAssassinPathfinding` | Verbesserte Meuchelmörder-Wegfindung | Q |
| `EnableTroopMovementFix` | Truppen-Geschwindigkeitskorrektur | F |
| `EnableMountedStockpileMovementFix` | Stockpile-Bewegung berittener Einheiten | F |
| `EnableLadderAttackPathfindingFix` | Angriffe über Leiterzugänge ermöglichen | F |
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

### H5 — Tore, Gräben und Belagerung · 9 Hauptoptionen

Gemeinsamer Ort für Bau-/Eroberungsregeln und spezielle Wege an Befestigungen. Allgemeine Bewegungsverbesserungen stehen in H3, die lokalen Zielmarker in C1. Bestehende Tooltips können diese fachliche Abgrenzung kurz erwähnen; keine Optionen mehrfach anzeigen.

| Property | Option | Bisher |
| --- | --- | --- |
| `RequireReachableEnemyForAutomaticGateClosing` | Tore nur bei erreichbaren Feinden schließen | Q |
| `EnableCenteredGatehouseDistanceFix` | Torhaus-Schließdistanz von der Mitte messen | Q |
| `EnableFairSiegeAmmoRestock` | Belagerungsmunition gleichmäßig nachfüllen | Q |
| `EnableGatehouseLivingCaptureFix` | Tote Einheiten bei Torhauseroberung ignorieren | F |
| `EnableDrawbridgeDestructionFix` | Zugbrücken bei Schaden am noch lebenden Torhaus erhalten | F |
| `EnableAssassinGatehouseClimbFix` | Torhäuser direkt mit Assassinen erklimmen | F |
| `EnableTunnelPlacementDistanceFix` | Mindestabstand zu Feindbauten und -gräben korrigieren | F |
| `EnableEnemyGatePathfindingFix` | Feindliche Torhausdurchgänge vermeiden | F |
| `EnableAssassinCapturedGateProtectionFix` | KI-Assassinen greifen eroberte Torhäuser nicht erneut an | ? |

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
2. Die neun Themenbereiche entsprechend der Tabellen aufbauen. Kategoriezugehörigkeit, Optionstitel, Standardwerte, Bindings, Commands und gespeicherte Propertynamen zunächst erhalten.
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

- Geschlossen sind vier Client- und fünf Hostthemen überschaubar sichtbar. Es gibt keinen nur ein oder zwei Hauptoptionen umfassenden Themenbereich.
- Alle geöffneten Themen sind lesbar; lange deutsche Texte, schmale Fenster und die unterstützten UI-Skalierungen erzeugen keine überlappenden Controls oder abgeschnittenen Empfehlungskennzeichen.
- Checkboxen, Regler, ComboBoxen und Hotkeys behalten klare Titel und passende Einheiten. Die Empfehlung ist auch bei ausgeschalteter oder gesperrter Option verständlich.
- Wareneditor, Statistikvorschau, Steam-Blacklist-Aktion und Hotkey-Erfassung funktionieren nach Einklappen und erneutem Öffnen.
- Ein Stern bezeichnet die Autorenempfehlung, nicht „aktiv“ oder „technisch sicher“. Ohne bestätigte Einstufung wird keine Markierung erfunden.

### Technische Prüfung und Abschluss

- Für die geänderten Quellen die passenden XAML-, Tooltip-, Locale-Paritäts- und CRLF-Prüfungen auswählen; gemeinsame Prüfungen auf BugfixesAndQoL und tatsächlich berührte Abhängigkeiten begrenzen.
- Reiner Textplan: kein Runtime-Build und keine Gameplaytests erforderlich. Bei der späteren Implementierung erst die dann einschlägigen Runtime-/APIShared-/Buildanweisungen lesen und anwenden; neue öffentliche API oder Persistenzänderungen sind keine stillschweigende Voraussetzung dieses Plans.
- Vor Freigabe die tatsächliche Darstellung im Spiel prüfen. Ein erfolgreicher Build ersetzt die optische und interaktive Abnahme nicht.
- README-Erweiterung nach Fertigstellung gesondert erfragen. Keine Versionsänderung, Veröffentlichung oder Übertragung des Reworks auf andere Mods allein aufgrund dieser Planungsdatei.
