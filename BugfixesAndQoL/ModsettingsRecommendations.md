# Bestätigte Modsettings-Empfehlungen

Am 23.10.2026 von Serp als vollständige Liste bestätigt: **47 empfohlene Optionen und 60 nach persönlichem Geschmack.** Die Einstufungen werden ausdrücklich als Metadaten im Mod hinterlegt. Diese Datei dokumentiert die Entscheidung und ist keine zur Laufzeit eingelesene Konfiguration.

„Empfohlen“ bedeutet: normalerweise eingeschaltet lassen. „Geschmackssache“ bedeutet: gewünschte Darstellung, Spielregeln oder Bedienung entscheiden. Die Gründe beruhen auf den aktuellen Optionstexten und der Themenzuordnung; sie sind keine neue Sicherheitszertifizierung oder Performance-Messung. Die Gründe werden in den Locale-Dateien lokalisiert und über stabile Propertynamen zugeordnet. Die Markierungen ändern keine Spielwerte.

Bei Schaltern bezeichnet „An“ den Vorschlag. Bei Reglern und Auswahllisten steht ein konkreter Wert, sofern eine allgemeine Empfehlung sinnvoll erscheint. Die API-Indexadapter erzeugen keine zusätzlichen gespeicherten Einstellungen.

## Anzeige, HUD, Audio und Leistung

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Ausgewählte Bildschirmauflösung beibehalten · `PreserveDisplayResolution` | Empfohlen | An | Schützt die aus settings.cfg geladene randlose Vollbildauflösung vor automatischen Änderungen während des Starts und bei Fokuswechseln. |
| Zielmarker auf Torhäusern korrigieren · `EnableGatehouseTargetMarkerHeightFix` | Empfohlen | An | Korrigiert die Höhe grüner Bewegungszielmarker auf Torhäusern, einschließlich Formationsvorschau. |
| Vanilla-Leistungsoptimierungen · `EnableVanillaPerformanceOptimizations` | Empfohlen | An | Gezielte lokale Leistungsoptimierungen; unabhängig von den zusätzlichen Suchen im Hostbereich beurteilen. |
| Einheiten-Spritesheets zusammenlegen (mehr FPS) · `SpriteSheetMergeModeIndex` | Geschmackssache | Automatisch | Rendering-Optimierung mit eigenem Speicherbedarf und Neustarthinweis; die passende Wahl hängt vom System ab. |
| Nachrichten per Rechtsklick vollständig überspringen · `EnableCompleteNotificationSkipOnClick` | Geschmackssache | Nach Vorliebe | Ein Rechtsklick auf das Video einer KI- oder Eventnachricht – beziehungsweise auf die Minimap, wenn die Nachricht kein Video besitzt – beendet ihren Ton und Text und wechselt sofort zur nächsten wartenden Nachricht. |
| Lebenspunkte ausgewaehlter Einheiten im Truppen-HUD anzeigen · `ShowSelectedUnitHealth` | Geschmackssache | Nach Vorliebe | Zeigt die aktuellen und maximalen Lebenspunkte eigener und markierter fremder Einheiten im Truppen-HUD an. |
| Markierte fremde Truppen im Truppen-HUD anzeigen · `ShowForeignTroopHud` | Geschmackssache | Nach Vorliebe | Zeigt Anzahl und Spielerfarbe markierter fremder Truppen. |
| Dauerhafte Lebensbalken beschädigter Objekte erlauben · `EnableDamagedHealthBars` | Geschmackssache | Nach Vorliebe | Erlaubt lokale Lebensbalken für sichtbare beschädigte Einheiten und Gebäude. |
| Timer-Countdowns anzeigen · `ShowCountdownTimers` | Geschmackssache | Nach Vorliebe | Zeigt die verbleibende Zeit für Missionsziele, Zeit bis zur Niederlage, Sieg-Timer und Friedenszeit an. |
| Teammarkierungen in Statistiken · `StatisticsTeamBadgeMode` | Geschmackssache | Easy Read (2) als Vorschlag | Persönliche Lesbarkeit der Teammarkierungen; Vanilla-Symbole und Aus bleiben legitime Alternativen. |
| Letztes Videobild wie in HD bis zum Sprachende anzeigen · `EnableNotificationLastFrame` | Geschmackssache | Nach Vorliebe | Zeigt nach dem Ende eines Minimap-Videos das letzte Bild weiter an, solange das Spiel auf Sprache wartet. |
| Arabische Schwertkaempfer: Idle-Lautstaerke · `ArabianIdleSoundVolumePercent` | Geschmackssache | 25 % als Ausgangspunkt | Reine persönliche Lautstärkepräferenz; der Standardwert ist keine allgemeine Empfehlung. |

## Kamera, Bauen und Steuerung

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Minimap-Mauszeigerfix · `EnableMinimapCursorFollowFix` | Empfohlen | An | Beim Ziehen auf der Minimap folgt die Kamera direkt dem Mauszeiger. |
| Aufgeloeste Einheiten aus Kontrollgruppen entfernen · `EnableDisbandedUnitControlGroupCleanup` | Empfohlen | An | Entfernt aufgeloeste Einheiten sofort aus deinen lokalen Kontrollgruppen, damit ihre Bauern oder spaeter daraus rekrutierte Soldaten die alte Gruppenzugehoerigkeit nicht erben. |
| Abreißcursorfix bei Feindnähe · `EnableEnemyProximityBulldozeCursorFix` | Empfohlen | An | Ändert das Abreißsymbol, wenn sich der Mauszeiger in der Nähe von Feinden befindet. |
| Bodenanforderungen beim Bauen anzeigen · `EnableImprovedBuildPreview` | Geschmackssache | Nach Vorliebe | Markiert ungeeigneten Boden rot und zeigt Flächenzahlen im Bautooltip. |
| Per Mittelklick im Truppen-HUD zur ausgewaehlten Einheit springen · `EnableTroopHudMiddleClickCameraJump` | Geschmackssache | Nach Vorliebe | Ein einfacher Klick mit der mittleren Maustaste auf das Symbol eines ausgewaehlten Einheitentyps zentriert die Kamera auf eine ausgewaehlte Einheit dieses Typs. |
| Minimap beim Platzieren von Gebaeuden erlauben · `AllowMinimapWhilePlacingBuilding` | Geschmackssache | Nach Vorliebe | Wenn aktiviert, bewegt ein Linksklick auf die Minimap die Kamera auch dann, wenn ein Gebaeude zum Platzieren ausgewaehlt ist. |
| Beim Abbrechen der Gebaeudeplatzierung keine Truppen bewegen · `PreventMoveOrderOnPlacementCancel` | Empfohlen | An | Im DE-Steuerungsmodus erteilt ein Rechtsklick, der die Gebaeudeplatzierung abbricht, den ausgewaehlten Truppen nicht gleichzeitig einen Bewegungsbefehl. |
| Kamerabewegung bei gedrueckter Strg- oder Alt-Taste erlauben · `AllowCameraMovementWithModifiers` | Geschmackssache | Nach Vorliebe | Wenn aktiviert, bewegen Tastatur- und Randscrollen die Kamera auch weiter, waehrend Strg oder Alt gedrueckt wird. |
| Aufloesungsabhaengigen erweiterten Zoom verwenden · `EnableResolutionAwareExtendedZoom` | Geschmackssache | Nach Vorliebe | Normiert den Kamerazoom anhand der vertikalen Aufloesung, damit hohe Aufloesungen dieselbe Nahansicht wie 1080p behalten, und fuegt 1,5-fach und 2-fach naehere Zoompositionen hinzu. |

## Menüs, Spielstände und Kreuzzugmärsche

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Startgold-Fix für eigene Kreuzzugmärsche · `EnableCustomTrailExtremeGoldFix` | Empfohlen | An | Behandelt den nicht nutzbaren Wert customisedExtremeTrail beim Lesen einer .trail-Datei immer als false. |
| Erweiterte Karten-, Spielstand- und Trail-Dialoge · `ShowLoadSaveDialogControls` | Geschmackssache | Nach Vorliebe | Ergänzt Dialogbuttons, Spielerzahlspalten und Sortierung, entfernt fehlende Einträge und behandelt geöffnete Originalkarten sicher. |
| Multiplayer-Spielstände alleine laden · `EnableSoloMultiplayerSaveLoading` | Geschmackssache | Nach Vorliebe | Erlaubt einem bereiten Host, Multiplayer-Spielstände ohne weitere menschliche Spieler zu laden. |
| Skirmish-Ladebutton sichtbar halten · `FixSkirmishLoadButtonVisibility` | Empfohlen | An | Stellt den Spielstand-laden-Button beim Betreten des Singleplayer-Skirmish nach einem Besuch in einer Multiplayer-Lobby wieder her. |
| Kontrastreicheres Gelb · `ImproveYellowLobbyContrast` | Geschmackssache | Nach Vorliebe | Verwendet dunkles Gold fuer das gelbe Spielerwappen im gesamten Spiel und fuer gelbe Teamzeilen in der Skirmish- und Multiplayer-Lobby. |
| Korrekte "Kein Startgold"-Anzeige · `EnableBriefingNoStartingGoldFix` | Empfohlen | An | Zeigt in der Missionsvorschau fuer menschliche Spieler 0 Gold an, wenn Vanillas erweiterte Option "Kein Startgold" aktiv ist. |
| Eingehende Steam-Einladungen im Spiel anzeigen · `EnableIngameSteamInvitePrompt` | Geschmackssache | Nach Vorliebe | Zeigt eine Ja/Nein-Abfrage nur, nachdem Steam eine Einladung von einem aktuellen Freund gemeldet und die eingeladene Multiplayer-Lobby erfolgreich aufgeloest hat. |
| Gewählten Lord nach Workshop-Upload beibehalten · `EnableWorkshopUploadLordSelectionFix` | Empfohlen | An | Behält nach einem erfolgreichen Upload einer erweiterten AIV-Burg oder CPU-Lord-Konfiguration den aktuell gewählten Lord bei, statt zur Uploadliste der Ratte zurückzukehren. |
| Such-, Sortier- und Merkhilfen für Listen · `EnableLordAndMapListEnhancements` | Geschmackssache | Nach Vorliebe | Persönliche Suche, Sortierung, Spalten, Porträts und gemerkte Kartenauswahl. |
| Spielstände ohne ursprüngliche Kartendatei laden · `EnableMissingMapSaveRecovery` | Empfohlen | An | Erlaubt neue Rettungsversuche, wenn die ursprüngliche Karten- oder Traildatei fehlt. |

## Markt und Gütertransfer

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Markttasten-Hauptmenüfix · `EnableMarketKeyMainMenuFix` | Empfohlen | An | Wird bei geöffnetem Markt die Markt-Tastenbelegung gedrückt, kehrt das Menü zum Haupthandelsmenü zurück. |
| Auto-Verkaufsschwellenfix · `EnableAutoTradeSellZeroFix` | Empfohlen | An | Erlaubt dem automatischen Markthandel, Waren mit einem Verkaufsschwellenwert größer als null zu verkaufen. |
| Mengen beim Guetertransfer mit Verbuendeten anpassen · `EnableAllyGoodsAmountModifiers` | Geschmackssache | Nach Vorliebe | Halte im Guetertransfer mit Verbuendeten Umschalt fuer die 5-fache oder Strg fuer die 0,2-fache angeklickte Menge. |
| Konfigurierte Marktwaren-Reihenfolge verwenden · `HdMarketView` | Geschmackssache | Nach Vorliebe | Verwendet die konfigurierte Reihenfolge in der detaillierten Marktansicht. |

## Partievorbereitung, Mehrspieler und Zuschauer

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Host-Wechsel nach abruptem Verbindungsabbruch reparieren · `EnableAbruptHostMigrationFix` | Empfohlen | An | Befördert den einzigen verbleibenden menschlichen Spieler zum neuen Host, wenn der bisherige Host die laufende Partie ohne Vanillas Abmeldepaket beendet, beispielsweise per Alt+F4. |
| Sichern, bevor ein zurückliegender Spieler entfernt wird · `EnableConnectionRecoverySave` | Empfohlen | An | Bevor Vanilla einen Spieler wegen zu großen Tick-Rückstands im Mehrspieler entfernt, erstellt der Host den synchronisierten Mehrspieler-Spielstand "Connection Recovery.msv", in dem noch alle Spieler enthalten sind. |
| Getrennten Spieler und Host-Kick beim Resync anzeigen · `EnableResyncHostKick` | Empfohlen | An | Zeigt dem Host während einer festgefahrenen Mehrspieler-Neusynchronisierung den menschlichen Spieler mit dem am längsten überfälligen Heartbeat und bietet einen bestätigten, autoritativen Kick an. |
| Lord-Erzeugung auf Karten mit beschädigten Lord-Daten reparieren · `EnableCorruptLordDataSpawnFix` | Empfohlen | An | Entfernt beim Start eines neuen Spiels ausschließlich veraltete Lord-Verweise auf vollständig leere Einheitenslots, damit Vanilla den fehlenden Lord erzeugen kann. |
| Veralteten Mehrspielerzustand vor dem Start verwerfen · `EnableMultiplayerStartupStateFix` | Empfohlen | An | Verwirft vor einem neuen Spiel den vorherigen Mehrspieler-Snapshot, solange die Simulation noch steht. |
| Friedenszeit in allen Spielmodi korrekt anwenden · `EnablePeaceTimeGameplayFixes` | Geschmackssache | Nach Vorliebe | Erweitert Friedenszeitregeln, Countdown, Starttruppen- und Tierkontaktprüfungen über Vanillas Mehrspielerpfade hinaus und verwendet eine feste Simulationsdauer. |
| Erweiterte Spieloptionen im lokalen Gefecht · `ShowMpAdvancedSettingsInSingleplayer` | Geschmackssache | Nach Vorliebe | Aktiviert den zusätzlichen lokalen Setupdialog, Presetaktionen, Keine-Hunde-Unterstützung und ergänzende Regelsteuerung. |
| Erweiterte Burgherren- und Burgauswahl · `EnableCustomLordListEnhancements` | Geschmackssache | Nach Vorliebe | Erlaubt bis zu 50 geordnete Burgkandidaten, AIV-/AIC-Auswahlpresets und zusätzliche Coop-Partner einschließlich notwendiger Mehrspielerübertragung. |
| Customize-Buttons für eigene und Koop-Kreuzzugmärsche · `EnableTrailCustomizationButtons` | Geschmackssache | Nach Vorliebe | Zeigt Customize bei eigenen Kreuzzugmärschen an und fügt den Button allen vier Koop-Kreuzzugmärschen hinzu. |
| Alle Multiplayer-Plätze mit KI belegen · `AllowFullAiMultiplayerLobby` | Geschmackssache | Nach Vorliebe | Erlaubt dem Host, in normalen Multiplayer-Lobbys auch den letzten freien Spielerplatz mit einer KI zu belegen. |
| Nach Spielende gemeinsam in die Lobby zurückkehren · `EnableReturnToMultiplayerLobby` | Geschmackssache | Nach Vorliebe | Erstellt nach dem Ende einer normalen Mehrspielerpartie eine neue Lobby für alle noch verbundenen Teilnehmer. |
| Mehrspieler-Steuerung fuer Spielgeschwindigkeit und Pause · `MultiplayerTimeControlPermissionIndex` | Geschmackssache | Nach Absprache | Legt fest, wer in der Partie die Zeit steuern darf; eine Gruppenentscheidung. |
| Maximales Spieltempo · `MultiplayerGameSpeedMaximum` | Geschmackssache | Nach Absprache, kein Stern | Die zulässige Höchstgeschwindigkeit richtet sich nach Mitspielern und gewünschtem Tempo. |
| 25er-Schritte fuer Spielgeschwindigkeit aktivieren · `EnableShiftGameSpeedSteps` | Geschmackssache | Nach Vorliebe | Druecke oder halte das normale Tastenkuerzel fuer schneller/langsamer. |
| Aufgeben / Statistiken ermöglichen · `EnableSurrenderAndStatistics` | Geschmackssache | Nach Vorliebe | Fügt aktiven Spielern einen Aufgeben-Button und Zuschauern einen Button für die Statistiken der laufenden Partie hinzu. |
| Ausgeschiedene Spieler werden Zuschauer · `EnableEliminatedPlayersBecomeSpectators` | Geschmackssache | Nach Vorliebe | Nachdem ihr Burgherr gestorben ist, erhalten ausgeschiedene Spieler Vanillas Zuschauersicht und KI-Informationen. |
| Erweiterte Zuschaueransicht · `EnableSpectatorPerspective` | Geschmackssache | Nach Vorliebe | Ursprüngliche Zuschauer können zwischen Spielern wechseln, alle Reports und Verbündeten einsehen sowie per Rechts- und Mittelklick zu Burgfried und Burgherr springen. |
| Erweiterte Zufallsgegner-Auswahl · `EnableRandomOpponentSelectionEnhancements` | Geschmackssache | Nach Vorliebe | Ergänzt Quellenfilter und hält die Zufallsgegner-Auswahl erreichbar. |

## KI, Burgaufbau und Wirtschaftsschutz

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Stein fuer ausstehende KI-Burggebaeude zurueckhalten · `EnableAiStoneReserveFix` | Empfohlen | An | Behaelt vor Steinverkaeufen Stein fuer das teuerste Burggebaeude zurueck, dessen Erstbau noch nicht abgeschlossen wurde. |
| KI-Burgverteidigung wieder auffuellen · `EnableAiDefensePatrolFix` | Empfohlen | An | Neu rekrutierte KI-Verteidiger fuellen zuerst wieder die konfigurierte Anzahl der Mauerverteidiger auf, bevor weitere Einheiten der aeusseren Patrouille zugewiesen werden. |
| KI-Mauerzielauswahl verbessern · `EnableAiWallTargetingFix` | Empfohlen | An | Erlaubt mehreren KI-Angreifern, dasselbe erreichbare Mauersegment gleichzeitig anzugreifen, statt ueberschuessige Einheiten warten zu lassen. |
| Produktion vorgebauter KI-Werkstätten ausgleichen · `EnablePrebuiltAiWorkshopBothFix` | Empfohlen | An | Korrigiert die Gutwahl vorgebauter Bogenmacher, Schmiede und Speermacher in vollständigen KI-Startburgen, wenn die Lord-Konfiguration auf „Both“ steht. |
| Ignorierte AIV-Verteidigungspositionen wiederherstellen · `EnableAivDefenderPositionFix` | Empfohlen | An | Stellt Verteidigungspositionen für Pikeniere, europäische Schwertkämpfer und arabische Schwertkämpfer in den mitgelieferten Standard-, Community- und Historical-AIV-Sets wieder her. |
| KI-Probleme durch vorplatzierte Kartengebäude beheben · `FixAIPreplacedMapBuildings` | Empfohlen | An | Verhindert, dass vorplatzierte Turmruinen den Baubeginn der KI verzögern, ermöglicht der KI Wirtschaftsbauten in Gebieten hinter eigenen oder verbündeten vorplatzierten Toren und aktualisiert den Wirtschaftszugriff nach einem echten Mauerdurchbruch. |
| KI-Turmwiederaufbau reparieren · `FixAITowerRepair` | Empfohlen | An | Erlaubt Vanilla, die eigene Turmruine einer KI zu entfernen, wenn sie den passenden Turm-Wiederaufbau blockiert, auch außerhalb von Vanillas Aufräumgrenze bei Manhattan-Distanz 20. |
| Bessere KI-Überbauregeln · `BetterAIOverbuildRules` | Empfohlen | An | Erlaubt der KI, beim Bau von Vorratslager, Markt, Kornspeicher und Waffenkammer Hindernisse zu beseitigen, wie sie es bereits bei Hütten und Kasernen tut. |
| KI-Schlafmodus bei Rohstoffmangel verhindern · `PreventAIPause` | Empfohlen | An | Hindert die KI daran, betroffene Produktionsgebaeude schlafen zu legen, wenn ein benoetigter Rohstoff fehlt. |
| KI-Panikabriss verhindern · `PreventEmergencyDemolition` | Empfohlen | An | Ueberspringt den KI-Notfallabriss zur Rohstoffrueckgewinnung, der sonst nuetzliche Gebaeude entfernen kann. |
| KI-Huettenabriss verhindern · `PreventHovelDeletion` | Empfohlen | An | Blockiert direktes Loeschen lebender KI-eigener Huetten, erlaubt aber weiterhin normale Zerstoerung durch Schaden. |
| Abriss unerreichbarer KI-Gebäude · `InaccessibleAIBuildingDemolitionProtection` | Empfohlen | Nur vorübergehende Blockaden (1) | Empfohlener Modus: Nur vorübergehende Blockaden (1). Schützt bei vorübergehender Torblockade; die stärkere dauerhafte Abrisssperre verändert das KI-Verhalten weitergehend. |
| Gemeinsame AIV-/AIC-Auswahl merken · `RememberAiAivSettings` | Geschmackssache | Nach Vorliebe | Wenn aktiviert, werden die zuletzt gewaehlte AIV-Liste, Drehung und benutzerdefinierten AIC-Einstellungen jedes KI-Lords gemeinsam fuer Singleplayer- und Multiplayer-Lobbys gespeichert. |
| KI-Steinhaufen zum Keep ausrichten · `EnableAIQuarryPileTowardsKeep` | Geschmackssache | Nach Vorliebe | Verschiebt den verknuepften Steinhaufen jedes neu gebauten KI-Steinbruchs auf die gueltige Vanilla-Position, die dem Keep dieses KI-Spielers am naechsten liegt. |
| KI-Baureichweite nicht begrenzen · `RemoveAIKeepRangeLimit` | Geschmackssache | Nach Vorliebe | Entfernt nur die Entfernungsprüfung zum eigenen oder verbündeten Keep für alle KI-Baupfade. |
| KI-Verkäufe auf vorhandene Waren begrenzen · `EnableAiSellThresholdFix` | Empfohlen | An | Verhindert laut vorhandenem Tooltip Verkäufe ohne Warenbestand durch negative KI-Verkaufsschwellen. |

## Truppenbedienung und Bewegungstempo

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Truppen-Geschwindigkeitskorrektur · `EnableTroopMovementFix` | Empfohlen | An | Korrigiert die synchronisierte Bewegung gemischter Truppengruppen: Bei einem normalen Bewegungsbefehl laufen alle Einheiten in der Geschwindigkeit der langsamsten Einheit der Gruppe. |
| Stockpile-Bewegung für berittene Einheiten · `EnableMountedStockpileMovementFix` | Empfohlen | An | Erlaubt Kavallerie Bewegungsbefehle auf passierbare Stockpile-Felder, die sie bereits durchqueren kann. |
| Heiler aus Nahkampf-Angriffsgruppen heraushalten · `EnableHealerAttackCommandFix` | Empfohlen | An | Verhindert, dass Beduinenheiler Nahkampfeinheiten in den Kampf folgen, wenn eine gemischte Auswahl den Befehl erhält, eine feindliche Einheit anzugreifen. |
| Meuchelmoerder-Bewegung nach Kaempfen fortsetzen · `EnableAssassinCombatResumeFix` | Empfohlen | An | Laesst Meuchelmoerder ihren urspruenglichen Bewegungsbefehl nach einem automatisch ausgetragenen Nahkampf fortsetzen, einschliesslich Wegen, die Klettern erfordern. |
| Sammelpunkt-Platzierungsfix · `EnableAssemblyPointPlacementFix` | Empfohlen | An | Erlaubt das Platzieren von Sammelpunktflaggen an jeder Stelle. |
| Erweiterte Shift-Befehlswarteschlangen aktivieren · `EnableExtendedShiftCommandQueue` | Geschmackssache | Nach Vorliebe | Verbindet Shift-Bewegungsbefehle und Angriffe auf Einheiten oder Gebaeude in einer Warteschlange mit bis zu 128 Befehlen und mehrseitigen Zielmarkierungen. |
| Rekruten laufen zu Sammelpunkten · `EnableFastRecruitRallyMovement` | Geschmackssache | Nach Vorliebe | Neu rekrutierte Einheiten menschlicher Spieler laufen mit ihrem eigenen maximalen Vanilla-Tempo und passender Animation zu ihrem Sammelpunkt; Gelände- und Zustandsmodifikatoren bleiben erhalten. |
| Geländeverlangsamung zum Sammelpunkt korrigieren · `EnableRallyTerrainSlowdownFix` | Geschmackssache | Nach Vorliebe | Einheiten auf dem Weg zum Sammelpunkt behalten die Vanilla-Verlangsamung in Sümpfen, Furten und beim Aufstieg. |
| Lord-Steuerung im Truppen-HUD aktivieren · `EnableLordUnitControls` | Geschmackssache | Nach Vorliebe | Zeigt beim Anklicken des eigenen Lords ein kompaktes Truppen-HUD. |

## Gebäude, Wirtschaft und Versorgung

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Hauptflaggen mit den Bergfrieden drehen · `EnableKeepFlagRotationFix` | Empfohlen | An | Behält die Hauptflagge relativ zum Eingang in derselben Ecke, wenn Vanilla, CastlePlanner oder Fixes einen menschlichen oder KI-Bergfried dreht. |
| Gerberei-Animation einblenden · `EnableTanneryAnimationFix` | Empfohlen | An | Lässt das Gerbgestell zu Beginn seiner Animation sichtbar einblenden, statt fast unsichtbar zu bleiben. |
| Pest-Beliebtheitsfix · `EnablePlaguePopularityFix` | Empfohlen | An | Gibt fuer jeden aktiven Pestherd genau -1 Beliebtheit und entfernt den Malus, sobald alle zugehoerigen Pestwolken natuerlich oder durch einen Apotheker verschwunden sind. |
| Pestwolken-Entfernungsfix · `EnablePlagueCloudRemovalFix` | Empfohlen | An | Stellt sicher, dass alle von einer Apothekerbehandlung erfassten Pestwolken in Vanillas Ausblendphase wechseln, statt wieder aktiv zu werden. |
| Fix für feststeckende Apotheker · `EnableStuckApothecaryFix` | Empfohlen | An | Vervollständigt Vanillas Gebäudeausgangs-Übergang, wenn ein Apotheker ein Pestziel findet, damit er nicht am Gebäude stehen bleibt. |
| Apotheker-Zielreservierungsfix · `EnablePlagueTargetReservationFix` | Empfohlen | An | Verhindert, dass verschiedene Apotheker Pestwolken auswählen, die von derselben erwarteten Flächenbehandlung erfasst werden. |
| Pausieren einzelner Gebaeude aktivieren · `EnableSingleBuildingPause` | Geschmackssache | Nach Vorliebe | Halte beim Umschalten des Pausenzustands eines Produktionsgebaeudes Strg gedrueckt, um nur das ausgewaehlte Gebaeude zu beeinflussen. |
| Naechsten Wassertraeger zu jedem Brand schicken · `EnableNearestWaterboyTargeting` | Geschmackssache | Nach Vorliebe | Weist jedem Brand genau einen Wassertraeger aus einem Brunnen oder Wassertrog zu. |
| Alle Gebaeude mit Shift reparieren · `EnableShiftRepairAllBuildings` | Geschmackssache | Nach Vorliebe | Halte beim Klicken auf den Reparieren-Button eines Gebaeudes Shift gedrueckt, um zuerst das ausgewaehlte Gebaeude und danach alle weiteren eigenen, beschaedigten und reparierbaren Gebaeude zu reparieren. |
| Steinhaufen-Rotation aktivieren · `EnableQuarryPileRelocation` | Geschmackssache | Nach Vorliebe | Fuegt ausgewaehlten Steinbruechen einen Button hinzu, der ihren verknuepften Steinhaufen im Uhrzeigersinn zur naechsten gueltigen Position verschiebt. |
| Mit Strg eine Markteinheit handeln · `EnableCtrlSingleMarketTrade` | Geschmackssache | Nach Vorliebe | Halte beim Kaufen oder Verkaufen Strg gedrueckt, um genau eine Einheit zu handeln. |
| Pausen für Bäcker und Müller nach der Ablieferung · `EnableBakerMillerBreaks` | Geschmackssache | Nach Vorliebe | Erlaubt Bäckern und allen drei Müllern, nach dem Abliefern gelegentlich Vanillas Pause bei einem positiven Angstfaktor zu machen, auch wenn noch Rohstoffe verfügbar sind. |
| Warteverzögerung für Piekenmacher und Gerber entfernen · `EnableWorkshopIdleDelayFix` | Geschmackssache | Nach Vorliebe | Überspringt die zusätzliche Warteanimation, wenn Holz, Häute in der Werkstatt oder eine geeignete Kuh verfügbar sind. |

## Tore und Belagerungsregeln

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Torhauseroberung: tote Einheiten ignorieren · `EnableGatehouseLivingCaptureFix` | Empfohlen | An | Tote Angreifer und Verteidiger zählen bei Vanillas regulärer Torhauseroberungsprüfung nicht mehr. |
| Zugbrücken bei Torhaus-Schaden erhalten · `EnableDrawbridgeDestructionFix` | Empfohlen | An | Verhindert, dass direkte Angriffe auf noch lebende Steintorhäuser ihre verbundenen Zugbrücken zerstören. |
| Assassinen können Torhäuser direkt erklimmen · `EnableAssassinGatehouseClimbFix` | Empfohlen | An | Ermöglicht Menschen und KI direkte Assassin-Kletterwege auf Torhäuser mit Vanillas Kletterausführung. |
| Mindestabstand zu Feindbauten und -gräben · `EnableTunnelPlacementDistanceFix` | Empfohlen | An | Verlangt eine Kachel Abstand zwischen allen von Menschen platzierten Gebäuden und ausgehobenen feindlichen Gräben. |
| Torhaus-Schließdistanz von der Mitte messen · `EnableCenteredGatehouseDistanceFix` | Geschmackssache | Nach Vorliebe | Verwendet die Mitte der vollständigen Torhausgrenzen statt einer Ecke, sodass die eingestellte Schließdistanz aus jeder Annäherungsrichtung symmetrisch ist. |
| Belagerungsmunition gleichmäßig nachfüllen · `EnableFairSiegeAmmoRestock` | Geschmackssache | Nach Vorliebe | Verwendet ein gemeinsames Munitionspaket für alle ausgewählten Katapulte und Triboke und verteilt es gleichmäßig, ohne vorhandene Munition zu verringern. |
| KI-Assassinen greifen keine eroberten Torhäuser mehr an · `EnableAssassinCapturedGateProtectionFix` | Geschmackssache | Nach Vorliebe | Verhindert, dass KI-Assassinen selbst oder verbündet eroberte steinerne Torhäuser angreifen. |

## Performance Heavy

| Option / Property | Vorschlag | Wert | Begründung |
| --- | --- | --- | --- |
| Feindliche Torhausdurchgänge vermeiden · `EnableEnemyGatePathfindingFix` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Spieler und KI planen keine Bodenwege durch feindliche, nicht berechtigt eroberte Torhäuser. |
| KI-Raidziele ohne Nahkampfzugang überspringen · `EnableAiRaidRetargetFix` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Nahkampf-Raidgruppen versuchen ein anderes Gebäude, wenn Vanilla keinen nutzbaren Angriffspunkt findet. |
| Angriffe ueber Leiterzugaenge ermoeglichen · `EnableLadderAttackPathfindingFix` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Erlaubt Einheiten, Vanillas Leiterwege zu verwenden, wenn sie eine feindliche Einheit oder ein Gebaeude hinter einer Mauer angreifen. |
| Verbesserte Meuchelmoerder-Wegfindung (menschliche Spieler) · `EnableImprovedAssassinPathfinding` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Waehlt Wege fuer die Meuchelmoerder menschlicher Spieler nach der erwarteten Dauer einschliesslich Kletteranimationen und aktiviert den spielerbezogenen Klettermodus-Button. |
| Verbesserte Meuchelmoerder-Wegfindung (KI-Lords) · `EnableImprovedAssassinPathfindingAI` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Waehlt Wege fuer die Meuchelmoerder von KI-Lords nach der erwarteten Dauer einschliesslich Kletteranimationen. |
| Aufstellung und vollständige Zielmarker aktivieren · `EnableMoveFormationEnhancements` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Wähle Zielaufstellung, Dichte und Rollenplatzierung im Truppen-HUD. |
| Verbesserte manuelle Einheitenbefehle · `EnableImprovedManualUnitCommands` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Erreichbare Mitglieder einer Auswahl führen manuelle Bewegungs-, Warteschlangen-, Patrouillen-, Angriffs- und Grabenarbeitsbefehle aus, auch wenn sie bereits im Graben stehen. |
| Verbessertes Zuschütten von Gräben · `EnableImprovedMoatFilling` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Beim Zuschütten feindlicher Gräben wählen Einheiten ein anderes freies, gültiges Randfeld, statt untätig zu werden, wenn die erste Position belegt ist. |
| Nur bei erreichbaren Feinden schließen · `RequireReachableEnemyForAutomaticGateClosing` | Geschmackssache | An bei gewünschter Funktion | Funktionsgewinn gegen zusätzliche Sucharbeit abwägen; abhängig von Karte, Einheiten und Befehlen. Wenn aktiviert, schließt ein Feind das Torhaus nur, wenn er per Wegfindung mindestens einen der beiden Toreingänge erreichen kann. |
