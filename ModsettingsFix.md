# BugfixesAndQoL: Modsettings korrigieren

Stand: 22.10.2026. Entwurf und erneut geprüfte Bestandsaufnahme; die beschriebenen Runtime-Fixes sind noch nicht implementiert.

## 1. Ziel und verbindliches Verhalten

Der Host-Schalter deaktiviert ausschließlich die eigenen hostkontrollierten Funktionen von BugfixesAndQoL. Der Client-Schalter deaktiviert ausschließlich die persönlichen Funktionen, einschließlich aller rein lokalen Optionen. Die Zuordnung richtet sich nach der Wirkung und Autorität, nicht danach, auf welchem Rechner ein Callback läuft oder ob die Funktion einen Button besitzt.

- Host: gemeinsame Spielregeln, Simulation, Start-/Lobbykonfiguration und gemeinsame Informationsfreigaben; `[SyncHostOnly]`.
- Client: persönliche Anzeige und lokale Bedienung; bestehende `[SyncPerPlayer]`- und `[PresetLocal]`-Klassifizierungen gehören beide in diesen Bereich.
- Ein Host bleibt auch Benutzer seiner persönlichen Client-Einstellungen. Ein Multiplayer-Client darf die eigenen lokalen Optionen bedienen, ohne Hostrechte zu erhalten.
- Technische Autoritätsprüfungen wie `lobby.isHost` bleiben erforderlich, auch innerhalb einer Clientfunktion. Sie sind kein Ersatz für einen Aktivierungsschalter und machen eine lokale Lade-/Dialogpräferenz nicht automatisch zur Host-Modsetting.

Die gespeicherten Unteroptionen bleiben beim Ausschalten eines Bereichs erhalten. Der Hauptschalter wirkt als logische Schranke, nicht als Reset. Kein wechselseitiges Setzen von Host-/Clientwerten und kein Abschalten durch Hook-Uninstallation.

Bezeichnungen im weiteren Dokument:

- `H = EnableMod`: bestehende hostkontrollierte Property; Name wegen Presetsystem, gespeicherten Daten und Integrationen beibehalten.
- `C = EnableClientFeatures`: bestehende persönliche Property.
- `O`: jeweilige Unteroption; weitere fachliche Voraussetzungen gelten zusätzlich.

| Vergleich bei gleichem Ausgangszustand | Gefordertes Ergebnis |
| --- | --- |
| `H=false` gegenüber `H=true` und allen Host-Unteroptionen fachlich aus | Gleiche eigenen Hostwirkungen; identische Clientwirkungen |
| `C=false` gegenüber `C=true` und allen Client-Unteroptionen fachlich aus | Gleiche eigenen Clientwirkungen; identische Hostwirkungen |
| Host und Client aus | Keine neu ausgelösten eigenen optionalen Funktionen; notwendige Bereinigung und die sichere Fortführung bereits erzeugter Zustände bleiben möglich |
| Bereich wieder an | Unveränderte gespeicherte Unteroptionen werden wieder wirksam |

Die Vergleiche betreffen effektives Verhalten, nicht identische gespeicherte Werte oder die Zahl installierter Hooks. Vergleichsläufe benötigen frische gleichartige Sitzungen; bei Sprite-Zusammenführung auch einen frischen Prozess. Bereits ausgeführte Spielaktionen, gespeicherte Spielregeln, eine aktivierte Zuschauerrolle oder bereits gerettete Spielstände lassen sich durch einen Schalter nicht rückwirkend ungeschehen machen.

## 2. Erneut geprüfte Befunde und konkrete Korrekturen

### 2.1 Zehn Client-Optionen verwenden den Host-Schalter

Die erste Analyse ist für diese zehn sichtbaren Einstellungen bestätigt. Die ersten sieben verlangen bisher `H && C && O`; bei den letzten drei fehlt `C` vollständig.

| Property | Bisherige Kopplung / Beleg | Empfohlener Fix |
| --- | --- | --- |
| `ImproveYellowLobbyContrast` | [LobbyYellowContrastFeature.IsActive](BugfixesAndQoL/src/LobbyYellowContrastFeature.cs), [Policy.IsEnabled](BugfixesAndQoL/src/LobbyYellowContrastPolicy.cs): `H && C && O` | Nur `C && O`; vorhandene Ressourcenprüfung und Wiederherstellung beibehalten |
| `EnableDamagedHealthBars` | [DamagedHealthBarsRuntime](BugfixesAndQoL/src/DamagedHealthBarsRuntime.cs): Hotkey und Abschaltpfad prüfen zusätzlich `H` | Beide Wege nur an `C`, Unteroption, Hotkey und tatsächlichen Anzeigezustand binden |
| `EnableBriefingNoStartingGoldFix` | [BriefingNoStartingGoldFixRegistration.Adjust](BugfixesAndQoL/src/BriefingNoStartingGoldFixRegistration.cs), zugehörige Policy: `H && C && O` | Hostargument aus der modinternen Aktivierungsentscheidung entfernen; fachliche Goldberechnung unverändert |
| `StatisticsTeamBadgeMode` | [SurrenderFeature.StatisticsTeamBadgesEnabled](BugfixesAndQoL/src/SurrenderFeature.cs): `H && C` | Nur `C`; Modus `Off` bleibt die Unteroption. Unabhängig von `EnableSurrenderAndStatistics` betreiben |
| `PreventMoveOrderOnPlacementCancel` | [PlacementCancelMoveSuppressionFeature](BugfixesAndQoL/src/PlacementCancelMoveSuppressionFeature.cs), [PlacementCancelRightClickPolicy](BugfixesAndQoL/src/PlacementCancelRightClickPolicy.cs): `H && C && O` | Nur `C && O`; zusammengehörigen RightDown-/RightUp-Zustand sicher abschließen |
| `EnableCompleteNotificationSkipOnClick` | [NotificationSkipFeature](BugfixesAndQoL/src/NotificationSkipFeature.cs): sowohl Klickentscheidung als auch Audioklassifizierung verwenden `H` | Beide Aktivierungswege auf `C && O` umstellen; laufende Nachrichten-/Audiobereinigung erhalten |
| `EnableNotificationLastFrame` | [NotificationLastFrameFeature.Enabled](BugfixesAndQoL/src/NotificationLastFrameFeature.cs): `H && C && O` | Nur `C && O`; eigenen Text-/Videostatus korrekt zurücksetzen |
| `ShowLoadSaveDialogControls` | [VanillaMapEditorHook](BugfixesAndQoL/src/VanillaMapEditorHook.cs) und [TrailDeletionViewModel.Enabled](BugfixesAndQoL/src/TrailDeletionViewModel.cs): `H && O` | `C && O`; alle Sichtbarkeiten, Commands und Bestätigungs-Callbacks einschließen |
| `EnableSoloMultiplayerSaveLoading` | [LobbySaveLoadingFeature.ResolveSoloLoad](BugfixesAndQoL/src/LobbySaveLoadingFeature.cs): `H && O` | `C && O`; echte lokale Hostautorität, Lobbytyp und Rosterprüfungen unverändert erhalten |
| `FixSkirmishLoadButtonVisibility` | [LobbySaveLoadingFeature.AfterSetup](BugfixesAndQoL/src/LobbySaveLoadingFeature.cs): `H && O` | `C && O`; ursprünglichen Setup-Aufruf weiter genau einmal ausführen |

Bei diesen Änderungen auch sämtliche Settings-Listener, Snapshotveröffentlichungen und UI-Refreshs anpassen. Nur die zentrale Bool-Bedingung zu ändern genügt beispielsweise bei Lebensbalken, Statistikmarkierungen und Dialogbuttons nicht. Eine Hoständerung darf keine lokale Darstellung löschen; eine Clientänderung muss sie zuverlässig aktualisieren.

`ShowVanillaMapsInEditor` ist eine vorhandene lokale Unterpräferenz des Dialogbereichs. Sie braucht keinen weiteren Hauptschalter: effektiv `C && ShowLoadSaveDialogControls && ShowVanillaMapsInEditor`.

### 2.2 Workshop-Option falsch einsortiert

`EnableWorkshopUploadLordSelectionFix` ist bereits `[PresetLocal]`, steht aber im [Host-XAML-Bereich](BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml) und verwendet [zur Laufzeit](BugfixesAndQoL/src/WorkshopUploadLordSelectionFix.cs) `H && O`.

Empfehlung: in den Clientbereich unter „Editor und Dateien“ verschieben; `C && O` verwenden. Attribut und gespeicherten Propertynamen erhalten. Der umgebende Editor muss `CanEditClientSettings` benutzen, damit auch ein Multiplayer-Client seine lokale Präferenz bearbeiten kann. Keine neue Option nötig.

Präzisierung zur ersten Zwischenmeldung: Es gibt im untersuchten XAML genau eine ausdrückliche lokale Property im Hostbereich. Die drei Lade-/Dialogoptionen stehen bereits im Clientbereich; deren Fehler ist die Runtime-Kopplung.

### 2.3 Lordlisten-Option enthält zwei unterschiedliche Kategorien

`EnableCustomLordListEnhancements` ist `[SyncHostOnly]`. Der [Tooltip](BugfixesAndQoL/Locales/de-DE.txt) und die Verbraucher bündeln persönliche Suche/Sortierung, Kartenlisten-Merker, bis zu 50 geordnete AIV-Kandidaten, Coop-Partnerauswahl und die MP-Übertragung dieser Kandidaten.

Empfehlung: die bestehende Hostproperty behalten und ihren sichtbaren Titel auf „Erweiterte Burgherren- und Burgauswahl“ präzisieren. Zusätzlich eine lokale Option `EnableLordAndMapListEnhancements` schaffen, sichtbar als „Such-, Sortier- und Merkhilfen für Listen“.

| Teilfunktion | Zielzuständigkeit |
| --- | --- |
| Suche, Darstellungssortierung, Spalten, Tastaturfokus und Porträtkorrekturen in Lord-/AIV-/AIC-Listen | `C && EnableLordAndMapListEnhancements` |
| Herkunftssortierung, Größenanzeige klassischer Karten, gemerkte Kartensortierung/-auswahl | Dieselbe lokale Option; Wiederherstellen der gewählten Karte weiterhin nur mit tatsächlicher Lobbyautorität und außerhalb fremder Vorbereitung |
| Erhöhtes AIV-Limit, Hinzufügen/Entfernen/Ordnen der tatsächlich ausgewählten Kandidaten, AIV-/AIC-Auswahlpresets | `H && EnableCustomLordListEnhancements`; echte Host-/Auswahlrechte erhalten |
| Zusätzliche Coop-Partner und deren Start-/Fortschrittsintegration | Bestehende Hostproperty |
| Übertragung optionaler erweiterter Kandidatenlisten | Bestehende Hostproperty; keine lokale Client-Abschaltung |

Wichtig: [AiCastleSettingsListEnhancementHook](BugfixesAndQoL/src/AiCastleSettingsListEnhancementHook.cs) darf nicht einfach komplett auf die neue Clientproperty umgestellt werden. `ShowHook` verändert unter anderem `mpMode`, `AddSelectedHook` verändert `info.aivs`, und der inaktive Weg kürzt die Auswahl auf Vanillagrenzen. Anzeige und Datenmutation benötigen getrennte Freigaben. Das Abschalten persönlicher Suchhilfen darf niemals Kandidaten kürzen oder die Hostauswahl verändern. Bei `C=false, H=true` bleibt eine funktionsfähige, einfachere Oberfläche für die erlaubte erweiterte Auswahl erhalten.

Die technische Aufteilung muss die Indexabbildung zwischen gefilterter Anzeige und kanonischen Listen sowie die autorisierte Änderung ihrer Reihenfolge erhalten. Suche/Sortierung verändert nur die Ansicht; Auswahlaktionen verändern die Lobbydaten ausschließlich mit Hostfreigabe.

### 2.4 Fehlende Einzelschalter: empfohlene Zuordnung

| Bisher nur implizit aktivierte Funktion | Entscheidung | Begründung und vorgeschlagenes Verhalten |
| --- | --- | --- |
| Gefilterte Zufallsgegner, Quellenfilter und dauerhaft erreichbare Zufallsauswahl | **Neue Hostoption** `EnableRandomOpponentSelectionEnhancements`, „Erweiterte Zufallsgegner-Auswahl“, Default `true` | [SkirmishAiSelectionMemoryHook](BugfixesAndQoL/src/SkirmishAiSelectionMemoryHook.cs) verändert tatsächlich die KI-Lobbybelegung. Passt weder zum bloßen Merken noch zur Kapazitätsoption `AllowFullAiMultiplayerLobby`. Alle direkten `H`-Zweige dieser Funktion durch `H && neueOption` ersetzen; vorhandene Filterpräferenzen behalten. „Aus“ reicht erkannte Zufallskommandos wieder an Vanilla durch; bei Fehlern nach begonnener Mutation weiterhin kein zweiter Vanilla-Aufruf. |
| Editor-Spielerzahlspalte/-sortierung, Entfernen fehlender Karten-/Savezeilen und zusammengehörige Dialogergänzungen | **Vorhandene Clientoption erweitern** `ShowLoadSaveDialogControls`; sichtbarer Titel „Erweiterte Karten-, Spielstand- und Trail-Dialoge“ | Gleicher Arbeitsablauf wie die vorhandenen Zusatzbuttons. Keine weitere kleinteilige Option. [VanillaMapEditorHook.FeatureEnabled](BugfixesAndQoL/src/VanillaMapEditorHook.cs) auf `C && ShowLoadSaveDialogControls` beziehen; vorhandene Vanilla-Kartencheckbox bleibt Unterpräferenz. Tooltip um Spalten, Listenbereinigung und sichere Behandlung geöffneter Originalkarten erweitern. |
| Verwerfen eines alten `lastGameState` unmittelbar vor erneutem MP-Start | **Neue Hostoption** `EnableMultiplayerStartupStateFix`, „Veralteten Mehrspielerzustand vor dem Start verwerfen“, Default `true` | [AbruptHostMigrationFix.StartMultiplayerGameHook](BugfixesAndQoL/src/AbruptHostMigrationFix.cs) prüft bisher nur `H`. Es handelt sich um Startzustandshygiene, nicht um die Beförderung eines neuen Hosts oder das Recovery-Save. Nicht künstlich an eines dieser beiden Features koppeln. Bestehende Prüfungen `!SimRunning` und vorhandener Zustand bleiben; neue Freigabe `H && neueOption`. |
| AIV-Synchronisierung bei `coopGame`, obwohl die Lordlisten-Hostoption aus ist | **Vorhandene Hostoption verwenden**, keine separate „Synchronisierung an/aus“-Option | [MultiplayerAivSyncRuntime.IsFeatureActive](BugfixesAndQoL/src/MultiplayerAivSyncRuntime.cs) enthält `H && (LordlistenOption \|\| coopGame)`. Den Spielmodus nicht als Ersatz für die optionale Featurefreigabe verwenden. Wer die erweiterte Auswahl erlaubt, erhält zwingend auch deren Synchronisierung. Vorschau, Manifest, Hoststart und Clientannahme müssen dieselbe Freigabe verwenden. |
| Spielstandrettung bei fehlender Originalkarte/-Trailquelle | **Neue lokale Option** `EnableMissingMapSaveRecovery`, „Spielstände ohne ursprüngliche Kartendatei laden“, Default `true` | [InitializeNetwork](BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs) übergibt bisher nur `C` als `RecoveryEnabled`. Die Funktion betrifft andere Fehlerfälle als Solo-MP-Laden oder einen fehlenden Ladebutton; eigene Option im Clientbereich unter „Laden und Dateien“. Neue Rettungen nur bei `C && neueOption`. |

Für die beiden sicherheitsrelevanten Folgezustände gelten besondere Grenzen:

1. **Bereits gerettete Spielstände:** [SavegameFix](BugfixesAndQoL/src/SavegameMissingMap/SavegameFix.cs) trennt `RecoveryEnabled` von Identitätserhalt, Serialisierung und Neustartsperre. Diese Trennung erhalten. Ausschalten verhindert neue Rettungen, darf aber weder den serialisierbaren Kartenbezug eines schon geretteten Zustands verlieren noch einen Neustart ohne echte Kartendatei freigeben. Keine zweite Option für notwendige Null-/Identitätsschutzmaßnahmen.
2. **Bereits geöffnete Originalkarten/Bestätigungen:** Beim Abschalten keine neue Modaktion beginnen. Bereits übernommene Schutzverantwortung für ein geöffnetes Original oder eine begonnene Transaktion sicher abschließen; nicht durch Abschalten einen gefährlichen Schreibpfad freigeben. Löschbestätigungen prüfen Rechte und aktuellen Zustand erneut.

Die neue MP-Startoption wird auf allen Peers aus dem Hostwert ausgewertet. Die erneute Prüfung der echten Managed-Assembly bestätigt: `Director.StartMultiplayerGame` aktiviert MP und ruft `clearMPMessages`; es leert `lastGameState` nicht. `UpdateMultiplayer` erreicht `monitorForLostPlayers`, das vorhandene `lastGameState.mpkick`-Einträge zum Entfernen menschlicher Spieler auswertet. Das belegt die getrennte Zuständigkeit des Bereinigungspfads; ein reproduzierter Ingame-Fehlkick wird hier nicht behauptet.

### 2.5 Zusätzlich gefunden: Skirmish-Runtime und permanente Gameplay-Patches

Die erste Analyse war an dieser Stelle nicht vollständig: [SkirmishGameOptionsAccessViewModel](BugfixesAndQoL/src/SkirmishGameOptionsAccessViewModel.cs) berücksichtigt `H && ShowMpAdvancedSettingsInSingleplayer` für den Button. [SkirmishGameOptionsRuntime](BugfixesAndQoL/src/SkirmishGameOptionsRuntime.cs) bekommt dagegen gar kein Settingsobjekt/-Prädikat und bearbeitet lokale `Setup`-/Apply-/Presetkommandos unabhängig von diesem Aktivierungswert.

Außerdem installiert [BugfixesAndQoLPlugin](BugfixesAndQoL/src/BugfixesAndQoLPlugin.cs) die [VanillaPeaceTimeGameplayPatch](BugfixesAndQoL/src/VanillaPeaceTimeGameplayPatch.cs) und [NoDogsNativePatch](BugfixesAndQoL/src/NoDogsNativePatch.cs) abhängig von technischer Kompatibilität, aber ohne Host-/Einzeloptionszustand. Installation allein wäre zulässig; problematisch ist, dass die geänderten Instruktionspfade selbst keine Modsettings-Aktivierung besitzen. Wirkung setzt weiterhin passende Vanilla-Spielzustände voraus; nicht jeder Lauf zeigt dadurch einen sichtbaren Unterschied.

**Empfehlung zur Skirmish-Bedienung:** Das vorhandene Hostsetting `ShowMpAdvancedSettingsInSingleplayer` für Button und neue eigene Dialog-/Presetaktionen verwenden; den sichtbaren Titel zu „Erweiterte Spieloptionen im lokalen Gefecht“ präzisieren. Die zusätzliche „Keine Hunde“-Unterstützung dieses lokalen Setups gehört ebenfalls dazu. Bereits gespeicherte Vanilla-Setupdaten nicht beim Verbergen der Oberfläche löschen.

**Empfehlung zur Friedenszeit:** Eine weitere Hostoption `EnablePeaceTimeGameplayFixes`, „Friedenszeit in allen Spielmodi korrekt anwenden“, Default `true`. Sie besitzt die bisherigen Änderungen der `VanillaPeaceTimeGameplayPatch`: feste Simulationsdauer pro Minute, erweiterte Gameplay-/Countdownpfade, Starttruppen und Tierkontaktkorrektur. Diese Wirkung umfasst auch Modi außerhalb des lokalen Setupdialogs und passt deshalb nicht vollständig zu dessen Sichtbarkeitsoption. Keine zusätzlichen Schalter je Patchstelle oder Vanilla-Spielregel. Im lokalen Setup bei fehlender effektiver Friedenszeit-Unterstützung die Auswahl passend sperren/kennzeichnen, ohne gespeicherte Werte zu löschen. Die reine Setupoberfläche und der Gameplayfix bleiben getrennt abschaltbar.

Die Umsetzung der nativen Abschaltbarkeit ist ein eigener fachlicher Arbeitsschritt: permanente, einmal installierte bedingte Stubs/Detours statt Laufzeit-Rückpatchen. Bei deaktivierter eigener Erweiterung den vollständigen ursprünglichen Pfad ausführen; reguläre Vanilla-MP-Regeln und legitim von einer fremden aktiven Funktion benötigte Verarbeitung erhalten. Nicht einfach das gesamte Friedenszeitflag oder die Vanilla-Spielregel abschalten.

Hier liegt **noch kein fertig auditierter neuer Maschinenkode-Vertrag** vor. Der vorhandene Friedenszeit-Audit deckt die bestehende Timer-/Gameplaykette ab; die neue logische Verzweigung und insbesondere der vollständige „Keine Hunde“-Kontext benötigen vor Implementierung einen frischen featurebezogenen Audit samt echten Spannen, eingehenden Sprüngen und deaktivierter Originalsemantik. Der Aktivierungsfehler ist durch die Quellen bestätigt; ein sicherer Ersatz darf nicht allein aus den bisherigen zwei Patchbytes abgeleitet werden.

### 2.6 Gemeinsame Dienste und fremde Featurebesitzer

Die erste Analyse muss außerdem zwischen eigener optionaler Wirkung und dem Betrieb gemeinsamer Infrastruktur unterscheiden:

- [TrailCustomizationFeature](BugfixesAndQoL/src/TrailCustomizationFeature.cs) verwendet bereits `(H && eigeneOption) || fremderProviderAktiv`. Ein weiterhin sichtbarer Customize-Button von ExtendedData ist deshalb nicht automatisch ein Bug. Die fremde Funktion folgt ihren eigenen Settings.
- [GateRoutingFeature](BugfixesAndQoL/src/GateRouting/GateRoutingFeature.cs) erlaubt Fremdregistrierungen trotz ausgeschalteter eigener Toroption, sperrt aber bisher den Gesamtdienst mit `H`. Der Betriebsbedarf muss aus eigener aktiver Torregel **oder** tatsächlich benötigten Fremdbeiträgen stammen. `H` darf nur die eigene Regel abschalten. Alle Start-/Tick-/Refresh-/Fehlerpfade konsistent berücksichtigen.
- [MovementOptionsSnapshot.Capture](BugfixesAndQoL/src/UnitCommands/Runtime/MovementOptionsSnapshot.cs) koppelt fremde Traversal-Anbieter bisher ebenfalls an `H`. Eigene manuelle Erweiterung und fremden Traversal-Bedarf trennen; jede vom Fremdanbieter benötigte gemeinsame Befehlsverarbeitung nach dessen validierter Freigabe betreiben. Keine neue BugfixesAndQoL-Option für MoatMove erfinden.
- Bereits bestehende [Movement-Cadence-Anbieter](BugfixesAndQoL/src/TroopMovementFix3Runtime.cs) zeigen dieses Muster: eigene Synchronisierung/Terrainkorrektur abschaltbar, fremder Callbackbedarf getrennt. Dieses Verhalten erhalten.
- Bei Coop-Importen müssen notwendige Verarbeitung und Validierung einer tatsächlich angeforderten fremden Startfunktion erhalten bleiben. Das betrifft auch die vorgeschaltete Custom-Coop-Validierung in `HostStartGameHook`, die schon vor `IsFeatureActive()` läuft. Keine pauschale Freigabe nur wegen `coopGame`; stattdessen den tatsächlichen Start-/Providerbedarf feststellen und Vorschau sowie Ausführung darauf abstimmen. Sicherheitsprüfungen nicht ersatzlos streichen.

Für Gleichwertigkeitstests alle fremden Settings und Registrierungen konstant halten und ihre Wirkungen getrennt zählen. Die beiden BugfixesAndQoL-Hauptschalter dürfen keine fremden Optionen ersetzen. Vor Änderungen veröffentlichter Anbieterwege deren Vertrag und Verbraucher prüfen; keine stillschweigende Änderung einer öffentlichen API einführen.

## 3. Sortierung und Umfang der neuen Optionen

### 3.1 Empfohlener sichtbarer Aufbau

Host und Client bleiben optisch getrennt, mit berechneten Rollenrechten und eigener Aktivierung. Titel und Tooltips müssen die tatsächliche Wirkung beschreiben.

| Bereich | Themen und Zuordnung |
| --- | --- |
| Host / Lobby und Spielstart | Erweiterte lokale Spieloptionen, volle KI-Lobby, KI-/AIV-Auswahl merken, erweiterte AIV-/Coop-Auswahl, neue Zufallsgegner-Option, Customize-Start |
| Host / Multiplayer und Zuschauer | Spieltempo, neue MP-Startzustandskorrektur, Migration, Recovery-Save, Resync-Kick, Lobbyrückkehr, Aufgeben/Statistiken, Zuschauerfreigaben |
| Host / Befehle und Wirtschaft | Formationen, Warteschlangen, manuelle Befehle, Graben-/Sammelpunktfunktionen, Markt-Einzelhandel, Gebäudepause/-reparatur, Wasserträger, Steinbruch-/Torregeln |
| Host / Fehlerbehebungen | KI, Gebäude, Bewegung, Pest und neue Friedenszeitoption; die bestehenden Themen und optionalen „Fixes?“-Abgrenzungen erhalten |
| Client / Anzeige und HUD | Gelb, Lebensbalken, Gesundheits-/Fremdtruppen-HUD, Countdown, Statistik-Teammarkierungen, Goldanzeige, Markerhöhe |
| Client / Bedienung | Kamera, Minimap, Platzierungsabbruch, Marktanzeige/-tasten, Warenmengen, HUD-Kamerasprung, Kontrollgruppen, Steam-Einladung |
| Client / Listen, Laden und Dateien | Neue Listenpräsentationsoption, erweiterte Dialoge, Solo-MP-Laden, Ladebutton, neue Missing-Map-Rettung, Workshop-Lordauswahl beibehalten |
| Client / Audio und Performance | Nachrichtenoptionen, arabische Idlelautstärke, Auflösung, Vanilla-Optimierungen, Sprite-Zusammenführung |

`RememberAiAivSettings` bleibt Host: Es speichert zwar lokal, setzt aber beim Hinzufügen einer KI deren tatsächliche AIV-/AIC-/Rotationsauswahl. Lokale Speicherung allein begründet keine Clientklassifizierung. `EnableSpectatorPerspective` bleibt ebenfalls Host: Es erlaubt zusätzliche Informationen und teilweise Aktionen, nicht nur eine kosmetische Ansicht. Buttons für gemeinsame Aktionen gehören weiterhin zur Hostfunktion; `C=false` darf sie nicht abschalten.

`EnableTanneryAnimationFix` und `EnableKeepFlagRotationFix` vorerst Host belassen. Die aktuelle Gerbereiimplementierung verändert das native Gebäude-Transparenzfeld im Simulationsupdate; die Flaggenkorrektur verändert native Projektilkoordinaten. Rein optischer Zweck beweist keine sichere unabhängige Ausführung oder Persistenz auf einzelnen Peers. Ein späterer Umbau ausschließlich in der Präsentationsschicht wäre ein separates Featurevorhaben, keine Attributkorrektur in diesem Fix.

### 3.2 Neue Properties und Bestandskompatibilität

Empfohlen sind genau **fünf** neue Settings, alle Default `true`, damit bisher eingeschaltete Funktionen bei der üblichen aktiven Standardkonfiguration verfügbar bleiben:

| Neue Property | Klassifizierung | Zweck |
| --- | --- | --- |
| `EnableLordAndMapListEnhancements` | `[PresetLocal]` | Persönliche Such-/Sortier-/Merkhilfen aus der bisherigen Mischoption herauslösen |
| `EnableRandomOpponentSelectionEnhancements` | `[SyncHostOnly]` | Bisher nur am Host-Hauptschalter hängende Zufallsgegner-Erweiterung |
| `EnableMultiplayerStartupStateFix` | `[SyncHostOnly]` | Eigenständig abschaltbare Bereinigung des alten MP-Zustands |
| `EnableMissingMapSaveRecovery` | `[PresetLocal]` | Neue Rettungen ohne ursprüngliche Kartendatei unabhängig erlauben |
| `EnablePeaceTimeGameplayFixes` | `[SyncHostOnly]` | Bisher nicht über Modsettings abschaltbare Friedenszeit-Gameplaykorrekturen |

Keine zusätzliche Sync-An/Aus-Option, kein neuer Editor-Spaltenschalter und keine separate Option für notwendige Zustandsbereinigung. Vorhandene Settingsnamen und Klassifizierungen beibehalten; die Host-/Client-Hauptproperties nicht umbenennen. Insbesondere `[SyncPerPlayer]`-Optionen nicht nebenbei zu `[PresetLocal]` migrieren: Die vorhandenen Companion-Arrays und Netzverträge sind ein anderes Thema.

Neue Properties in Defaults, Reset, Presets, XAML, Suche und sämtlichen Locales integrieren; nur die neuen Hostproperties gehören in Host-/Trail-Snapshots. Fehlende neue Werte alter lokaler Presets nach dem bestehenden registrierten Defaultvertrag behandeln. Neue persönliche Defaults niemals aus empfangenen Host-/Trailwerten erzeugen. Die neue lokale Listenoption startet unabhängig mit `true`; sie übernimmt nicht den alten Host-Hauptschalter als persönliche Präferenz. Host aus muss auch bei fehlenden neuen Hostkeys jede eigene optionale Hostwirkung sperren. Explizit gespeicherte neue Werte bleiben maßgeblich.

Alle sichtbaren Umbenennungen betreffen Locale-Texte und Suchbeschreibungen; gespeicherte Propertynamen bleiben stabil. Bestehende Named-AIV-Presets, Filterpräferenzen, Kartenmerker und andere separate Nutzdaten nicht löschen. Modsettings bleiben vor/nach der Partie editierbar; der Entwurf schafft keine neuen Ingame-Settingswechsel.

### 3.3 Vollständiges Bestandsinventar

Die folgenden Tabellen listen die 106 bei der Prüfung ausdrücklich klassifizierten Properties. Das ist eine datierte Bestandszählung, keine dauerhafte Testvorgabe. XAML-Proxyindizes, Commands und Companion-Arrays sind keine zusätzlichen Settings. Für die Umsetzung die Live-Inventur erneut erzeugen.

#### Hostbestand: 68 Properties

| Property | Bestehende Klassifizierung | Ziel / Maßnahme |
| --- | --- | --- |
| `EnableTrailCustomizationButtons` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableMod` | `SyncHostOnly` | Host-Hauptschalter; gespeicherten Namen erhalten |
| `AllowFullAiMultiplayerLobby` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `ShowMpAdvancedSettingsInSingleplayer` | `SyncHostOnly` | Host behalten; Runtime und lokale No-Dogs-Unterstützung anbinden (2.5) |
| `EnableAiStoneReserveFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableEnemyGatePathfindingFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAiRaidRetargetFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableDrawbridgeDestructionFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableGatehouseLivingCaptureFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAssassinCapturedGateProtectionFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAssassinGatehouseClimbFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAiDefensePatrolFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAiWallTargetingFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `RemoveAIKeepRangeLimit` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableWorkshopIdleDelayFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableBakerMillerBreaks` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAivDefenderPositionFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `FixAIPreplacedMapBuildings` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `FixAITowerRepair` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `BetterAIOverbuildRules` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `RememberAiAivSettings` | `SyncHostOnly` | Host behalten; tatsächliche Lobbyänderungen nur mit Autorität |
| `EnableCustomLordListEnhancements` | `SyncHostOnly` | Hostteil erhalten; persönliche Listenhilfen herauslösen (2.3) |
| `EnableTroopMovementFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableExtendedShiftCommandQueue` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableMoveFormationEnhancements` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableImprovedManualUnitCommands` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableImprovedMoatFilling` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableMountedStockpileMovementFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableLadderAttackPathfindingFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableHealerAttackCommandFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableFastRecruitRallyMovement` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableRallyTerrainSlowdownFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableSingleBuildingPause` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableNearestWaterboyTargeting` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableShiftRepairAllBuildings` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableQuarryPileRelocation` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAIQuarryPileTowardsKeep` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `RequireReachableEnemyForAutomaticGateClosing` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableCenteredGatehouseDistanceFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `PreventAIPause` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `PreventEmergencyDemolition` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `PreventHovelDeletion` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `InaccessibleAIBuildingDemolitionProtection` | `SyncHostOnly` | Host; fachlich aus = Vanilla |
| `EnableImprovedAssassinPathfinding` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAssassinCombatResumeFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnablePlaguePopularityFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnablePlagueCloudRemovalFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableStuckApothecaryFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnablePlagueTargetReservationFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAssemblyPointPlacementFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableKeepFlagRotationFix` | `SyncHostOnly` | Host behalten; native Projektilkoordinaten (3.1) |
| `EnableTanneryAnimationFix` | `SyncHostOnly` | Host vorerst behalten; native Gebäudefelder (3.1) |
| `EnableCorruptLordDataSpawnFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnablePrebuiltAiWorkshopBothFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableTunnelPlacementDistanceFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableFairSiegeAmmoRestock` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableSurrenderAndStatistics` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableLordUnitControls` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableEliminatedPlayersBecomeSpectators` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableSpectatorPerspective` | `SyncHostOnly` | Host behalten; Informations-/Aktionsfreigabe |
| `EnableAbruptHostMigrationFix` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableConnectionRecoverySave` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableResyncHostKick` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableReturnToMultiplayerLobby` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableCtrlSingleMarketTrade` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableMultiplayerGameSpeedChanges` | `SyncHostOnly` | Host; fachlich aus = Disabled |
| `MultiplayerGameSpeedMaximum` | `SyncHostOnly` | Hostgrenzwert der erlaubten Tempoänderungen; kein An/Aus-Schalter |
| `EnableShiftGameSpeedSteps` | `SyncHostOnly` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |

#### Clientbestand einschließlich lokal: 37 Properties

| Property | Bestehende Klassifizierung | Ziel / Maßnahme |
| --- | --- | --- |
| `EnableMinimapCursorFollowFix` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableCompleteNotificationSkipOnClick` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `EnableNotificationLastFrame` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `EnableMarketKeyMainMenuFix` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableAutoTradeSellZeroFix` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableEnemyProximityBulldozeCursorFix` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableIngameSteamInvitePrompt` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `ShowSelectedUnitHealth` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `ShowForeignTroopHud` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `ArabianIdleSoundVolumePercent` | `PresetLocal` | Lokal; 100 % entspricht ursprünglicher Lautstärke |
| `EnableImprovedBuildPreview` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableDamagedHealthBars` | `PresetLocal` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `HealthBarHotkey` | `PresetLocal` | Lokale Unterpräferenz der erlaubten Lebensbalken |
| `ShowCountdownTimers` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `ImproveYellowLobbyContrast` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `EnableBriefingNoStartingGoldFix` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `StatisticsTeamBadgeMode` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `EnableTroopHudMiddleClickCameraJump` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableDisbandedUnitControlGroupCleanup` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableClientFeatures` | `SyncPerPlayer` | Client-Hauptschalter einschließlich PresetLocal |
| `EnableCustomTrailExtremeGoldFix` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableSoloMultiplayerSaveLoading` | `PresetLocal` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `FixSkirmishLoadButtonVisibility` | `PresetLocal` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `ShowLoadSaveDialogControls` | `PresetLocal` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `ShowVanillaMapsInEditor` | `PresetLocal` | Lokale Unterpräferenz der erweiterten Dialoge (2.1/2.4) |
| `PreserveDisplayResolution` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableGatehouseTargetMarkerHeightFix` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableVanillaPerformanceOptimizations` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `SpriteSheetMergeMode` | `PresetLocal` | Eigene lokale Ladeoption; Neustartgrenze beachten |
| `EnableWorkshopUploadLordSelectionFix` | `PresetLocal` | In Client-XAML verschieben; C statt H (2.2) |
| `EnableAllyGoodsAmountModifiers` | `PresetLocal` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `AllowMinimapWhilePlacingBuilding` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `PreventMoveOrderOnPlacementCancel` | `SyncPerPlayer` | Hostkopplung entfernen; nur C und eigene Unteroption (2.1) |
| `AllowCameraMovementWithModifiers` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `EnableResolutionAwareExtendedZoom` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `HdMarketView` | `SyncPerPlayer` | Kategorie erhalten; eigene Unteroption und passenden Hauptschalter prüfen |
| `MarketGoodsOrder` | `SyncPerPlayer` | Persönliche Warenreihenfolge; wirksam innerhalb der HD-Marktansicht |

#### Technischer Teilnehmerstatus: eine Property

| Property | Bestehende Klassifizierung | Ziel / Maßnahme |
| --- | --- | --- |
| `MultiplayerSafetyCompatibilityReport` | `SyncPerPlayer, SHCDESE.API.Components.ModManager.DoNotPersist` | Kein Benutzer-Schalter; nicht persistierter Kompatibilitätsbericht für Recovery, nicht durch C als Protokollregistrierung abschalten |


## 4. Umsetzung und Abnahme

### 4.1 Reihenfolge und technische Grenzen

1. Die Zielzuordnung und alle effektiven Aktivierungsentscheidungen als modinternen Vertrag festlegen. Der bestehende APIShared-Presetcontroller weist die Hauptschalter korrekt zu; kein paralleles Preset-/Syncsystem und kein pauschaler Controllerumbau.
2. Die zehn Clientkopplungen und die Workshop-Platzierung korrigieren; Listener, Commands, UIzustand und Cleanup gemeinsam anpassen.
3. Die Mischoption aufteilen, fünf neue Optionen registrieren und bisher implizite Pfade an die empfohlenen Eigentümer binden. Anzeigeaktionen und Lobby-/Simulationsmutationen getrennt prüfen.
4. Fremdanbieterbedarf von eigenen Featuregates trennen. Danach Skirmish-Runtime und native Abschaltbarkeit anhand eines abgeschlossenen, hashgebundenen Audits ergänzen. Nicht mit reinem Ausblenden des Buttons als erledigt betrachten.
5. Tests und Locales aktualisieren; erst nach fertiger Fehlerbehebung Versionsänderung und Build/Installation im dann beauftragten Umfang. Dieser Dokumentauftrag verändert keine Runtime, README, Version oder Installation.

Vor Runtimeänderungen die vorgeschriebene JSON-/Lifecycle-Inventur und den langlebigen Aufrufweg nach Startup-Cleanup durchführen. Neue Settings-Gates dürfen keine veröffentlichte Hookinstallation rückgängig machen. Native Fehler-/Abschaltpfade erhalten die Originalsemantik; technische Registrierung und Paket-IDs bleiben auf allen Peers unabhängig von den Settings gleich.

### 4.2 Erforderliche Verhaltenstests

- **Vollständige Gate-Matrix:** Für jede Kategorie `H/C/O` kombinieren. Clientwirkung ist unabhängig von `H`, Hostwirkung unabhängig von `C`. Die echte Produktionsentscheidung prüfen, nicht dieselbe Formel im Test nachbauen.
- **Gleichwertigkeit:** Frische identische Starts für „Host aus“ vs. „Host an, alle Hostfunktionen aus“ und entsprechend für Client. Beide Richtungen mit aktiver Gegenkategorie. Abweichende gespeicherte Werte dürfen bestehen; Wirkungen müssen gleich sein.
- **Fachlich aus:** Multiplayer-Zeitsteuerung `Disabled`, Erreichbarkeitsschutz `Vanilla`; bei Clientoptionen Statistikmodus `Off`, Idlelautstärke `100 %` für Originalverhalten, Sprite-Modus `Off`. Der Geschwindigkeits-Maximalwert ist ein konfigurierter Grenzwert, kein eigener An/Aus-Schalter.
- **Gemischte Listenoption:** Alle Kombinationen aus persönlicher Darstellung und Host-Erweiterung. Suche aus darf keine AIVs kürzen; Suche an darf ohne Hostfreigabe keine erhöhte Kandidatenzahl oder Schreibrechte freischalten. Gefilterte Listen dürfen niemals falsche AIV-/AIC-Indizes anwenden.
- **Echtes MP mit zwei Menschen:** Host aus und lokale Darstellung an; Client aus bei erlaubten Hostfunktionen. Lokale Gelb-/Statistik-/Dialogwerte bleiben unabhängig. Hostoptionen vor und nach Beitritt, frischer Client mit abweichenden Defaults, Sync vor Kartenstart und Lobbywechsel prüfen. Client bearbeitet lokale Workshopoption; keine eigene Änderung fremder Hostwerte.
- **Presets/Trail:** Preset 1/2, Reset, Neustart, alte fehlende neue Keys und neue explizite Werte; Trail sperrt nur nach bestehendem Vertrag. Remote Host-/Trailwerte landen nie im persönlichen Preset. Unteroptionen bleiben nach Hauptschalter aus/an erhalten.
- **Zustandswechsel vor/nach einer Partie:** UI-Neuerstellung, Lobbyrückkehr, wiederholter Start und Editorstart. Keine veralteten Badges, Goldressourcen, Tastaturfoki oder Löschcommands. Keine versehentliche Abmeldung langlebiger Runtimepfade.
- **Recovery und Dateien:** Neue Missing-Map-Rettung wird durch `C` und Einzeloption gestoppt. Schon geretteter Zustand bleibt speicherbar, Label/Identität erhalten, fehlende Karte bleibt für Neustart ungeeignet. Keine Löschung oder Originalkartenüberschreibung durch abgeschaltete Schutzoberfläche.
- **MP-Startbereinigung:** Staler Snapshot mit gesetztem `mpkick`, noch nicht laufende Simulation und anschließender frischer Zustand; neue Option aus belässt Vanilla, an verwirft nur den alten Zustand. Migration und Recovery-Save unabhängig davon einzeln prüfen.
- **Skirmish/native Optionen:** Setupfreigabe und neue Friedenszeitoption unabhängig kombinieren. Eigene Erweiterung aus entspricht unveränderten Originalpfaden für alle betroffenen Spielmodi. An-Fälle für Friedenszeit, Ablauf, Tiere, Starttruppen und „Keine Hunde“ erhalten. Native Maschinenverträge, Flags/Register, Verzweigungen und vollständige deaktivierte Pfade testen; Spieltests mit/ohne Fixes ergänzen. Vorhandene Setupdaten nicht durch das Ausschalten der Mod löschen.
- **Fremdanbieter:** ExtendedData-Customize, Fremd-Torregeln, MoatMove/Traversal und Cadenceanbieter jeweils an/aus bei konstanten BugfixesAndQoL-Settings. Eigene Regeln aus, notwendiger fremder Dienst weiterhin wirksam. Ohne echten Bedarf keine versteckte eigene Wirkung.
- **Startgebundene Performancefunktion:** Sprite-Zusammenführung wird beim Laden aufgelöst. Gleichwertigkeit nach Neustart prüfen und Neustarthinweis auch beim Client-Hauptschalter verständlich machen; keine vermeintlich sofortige Entflechtung geladener Atlanten zusagen.

Die bisherigen [Gelb- und Statistiktests](BugfixesAndQoL/tests/Program.cs) schreiben teilweise die falsche Host-Abhängigkeit fest. Diese Erwartungen durch Gegenbeispiele für `H=false, C=true, O=true` ersetzen. [LobbySaveLoading.Tests](BugfixesAndQoL/tests/LobbySaveLoading.Tests/Program.cs) verwendet ein Settingsdouble ohne `EnableClientFeatures`; es muss die reale neue Entscheidungsgrenze abbilden. Vorhandene Sicherheitsabdeckung erhalten, nicht nur störende Quelltextprüfungen entfernen.

Bei späterer Implementierung relevant: modbezogene Shared-Boundary-Prüfung, einschlägige HostClientPresetTests, mod-eigene Verhaltens-/Hooktests, XAML-/Tooltip-/Localeprüfung sowie CRLF. Persistenz-EXE entsprechend den Arbeitsanweisungen erhöht ausführen. Ein erfolgreicher Build ersetzt weder den tatsächlichen Multiplayer- noch den Darstellungsnachweis.

## 5. Prüfstand, Evidenz und verbleibende Grenzen

Geprüft wurden die klassifizierten Properties des ViewModels, sämtliche direkten/proxygebundenen Settings der XAML-Seite, die modweiten Verwendungen von `EnableMod`/`EnableClientFeatures`, die genannten Runtimepfade und einschlägige vorhandene Tests. Alle zehn Clientkopplungen wurden erneut im Quelltext verfolgt; keine davon erfordert die Annahme eines bestimmten nativen Gameplayverhaltens.

Die tatsächlich installierte Gelb-Policy wurde isoliert über Reflection mit allen acht Boolkombinationen aufgerufen. Ergebnis: nur `H=true, C=true, O=true` liefert `true`. Ladefunktions-Gates und der Coop-Bypass wurden zusätzlich aus der installierten Mod-DLL dekompiliert. Dies sind statische/isolierte Nachweise, keine neue Ingame-Reproduktion.

| Bezug am 22.10.2026 | Identität / Nachweis |
| --- | --- |
| Mod-Repository | Branch `develop`, HEAD `f55021711a9c62793adc18087301e43cdd3b8914`, mit parallelen uncommitteten Änderungen; kein eingefrorener Buildstand |
| Installierte Mod | Assemblyversion `1.0.195.0`; SHA-256 `1C1CC8FEAF00F60239992B0703FB07CD04DE33842CCE68E01690E16E136219B9` |
| Installierte Managed-Spielassembly | SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`; Start-/Monitorpfad mit ilspycmd gelesen |
| Installierte native DLL | SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; identisch mit CURRENT |
| Geprüfte Quelle `BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs` | SHA-256 `97919F037DBBF5079C71E3D9CBDFB82A3B987C7DB44F17CBB7CC0624CB4C4DDB` |
| Geprüfte Quelle `BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml` | SHA-256 `4602554781791841DF286BD08F5FFDB3BC6C37916813403E2AFE6F283C309DAA` |

Der Native-Hash wurde gegen [CURRENT.json](_inspect/CrusaderDE-Native-Baseline/CURRENT.json) verifiziert. Für die vorsichtige Beibehaltung der Gerberei-/Flaggenzuständigkeit wurden die vollständigen vorhandenen Audits [TANNER_ALPHA](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/TANNER_ALPHA.md) und [KEEP_FLAG_ROTATION](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/KEEP_FLAG_ROTATION.md) mit den aktuellen Modquellen abgeglichen: Gebäudeupdate liest/schreibt Transparenz, restauriert Vanilla-Eingaben und reicht das Ergebnis an drei Renderer weiter; Flaggenkorrektur arbeitet an den vom Keep-Spawn erzeugten stationären Projektilkoordinaten. Eine abweichende lokale Simulation/Persistenz ist dadurch nicht als sicher nachgewiesen.

Für die zusätzliche Skirmishlücke wurde [VANILLA_PEACE_TIME](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/VANILLA_PEACE_TIME.md) vollständig gelesen: Timerinitialisierung, Countdown/Ablauf, 36 Flagreferenzen, modeabhängige Gameplaykonsumenten, Starttruppen und korrigierter Tierkontaktpfad. Dieser bestehende Audit belegt die Reichweite der aktuellen Erweiterung; er ersetzt keinen Audit neu geplanter Abschaltstubs. Die neue „Keine Hunde“-Ersatzstrategie ist ausdrücklich noch nicht vollständig nativ auditiert.

Keine Spiel-/MP-Läufe, keine neuen Runtime-Builds und keine Installation im Rahmen dieser Datei. Die neuen Propertynamen und Gruppierungen sind Empfehlungen dieses Entwurfs. Vor Umsetzung die Quellen erneut prüfen; gleichzeitig laufende Arbeit kann Arbeitsbaum und installierte DLL ändern. Historische Hashes und Beobachtungen sind keine zukünftigen Buildvoraussetzungen.
