# Modsettings, Presets, XAML, Tooltips und Lokalisierung

**Lesen, wenn:** Lobby-Modsettings, Presets, Modsettings-XAML, Tooltips (Modoptionen oder Ingame), Locale-Dateien oder Vanilla-XAML-Patches bearbeitet werden. Für Multiplayer-Sync persönlicher Settings zusätzlich [multiplayer-network.md](multiplayer-network.md). Gilt zusätzlich zur Root-`AGENTS.md`.

## Gemeinsames Presetsystem

- Neue Lobby-Modsettings referenzieren APIShared, leiten von `APIShared.ModSettings.PresetLobbyModSettingsViewModel` ab und registrieren sich über `LobbyModSettingsPresetRegistration.Register(...)`. Kein paralleles Preset-, Rollen-, Persistenz-, Transport- oder Syncsystem. Eigene Mods dürfen `Shared/Runtime/Localization/SerpLocalization.cs` als Quelllink für Texte nutzen; externe Mods brauchen weder Shared-Quelllinks noch ExtendedData.
- Jede gespeicherte Property ausdrücklich klassifizieren: `[SyncHostOnly]` (hostkontrollierte Spielwerte), `[SyncPerPlayer]` (synchronisierte persönliche Werte), `[PresetLocal]` (rein lokal).
- Empfangene Host- oder Trail-Werte gelangen auf Clients nie in die lokale `.msgpack`; die Zusammenführung übernimmt das Presetsystem.
- Sperren nur über die berechneten Bindings: `CanEditHostSettings`, `CanEditClientSettings`, `CanChangePreset`, `CanResetSettings`, `PresetVisibility`, `HostReadOnlyNoticeVisibility`. Commands prüfen dieselben Rechte intern; `[SyncHostOnly]` bleibt die Netzwerk-Sicherheitsgrenze.
- Rollen- und Trail-Zustand sind unabhängig. Ein aktiver Trail sperrt nur, solange der dritte Preseteintrag „Trail“ gewählt ist (außerhalb der Trail-Bearbeitung). Auf Preset 1/2 gelten normale Singleplayer-/Host-/Clientrechte und Änderungen gehen ins lokale Preset; zurück auf „Trail“ stellt den unveränderten Snapshot und die Sperre wieder her.
- `CustomCustomTrail` erfasst in `.modjson` und Mission-Snapshots nur `[SyncHostOnly]`-Properties; fehlende Trail-Properties setzen persönliche Einstellungen nicht zurück. Bei Änderungen am Trail-Zusammenspiel `ExtendedData/src/TrailMissionSettingsCoordinator.cs` mitprüfen.
- Modsettings werden nur vor/nach einer Partie geändert, nie während einer laufenden (außer auf ausdrückliche Anfrage); das vorhandene Sync-System kann genutzt werden.

## Aufbau und Bedienbarkeit

- Äußerer `ScrollViewer` mit `HorizontalScrollBarVisibility="Auto"` und `VerticalScrollBarVisibility="Auto"`.
- Reihenfolge: „Mod aktivieren“, sichtbar beschriftetes „Preset“, „Zurücksetzen“, Rollenstatus; dann getrennte, visuell unterschiedliche Bereiche „HOST-OPTIONEN“ und „LOKALE CLIENT-OPTIONEN“. Rollenüberschriften in Großbuchstaben, Themenüberschriften normal; deutlich verschiedene Themen bekommen lokalisierte Zwischenüberschriften.
- Reine Client-Mods (z. B. SpawnCastle) legen alles in den Clientbereich.
- Gemischte Mods trennen Host- und Client-Aktivierung technisch: lokaler Schalter (für jeden bedienbar, nur Clientfunktionen) und eigener `[SyncHostOnly]`-Schalter direkt bei „HOST-OPTIONEN“ für Hostfunktionen. Nie einen client-editierbaren Wert als Runtime-Schranke für Hostfunktionen.
- Aktivierungsschalter mit `HostActivationBorder` bzw. `ClientActivationBorder` hervorheben.
- Sliderwerte mit fachlich eindeutiger Einheit (`%`, `x`, Monate, Gruppen, Felder); interne Stärkewerte ohne belastbare Einheit ohne Einheit.
- Vanilla-XAML wenn möglich per Patch statt Komplett-Override ändern (Einzelwurzel-Regel in [script-extender.md](script-extender.md)).

## Modsettings-Suche

- Die Suche kommt mit `PresetLobbyModSettingsViewModel` aus APIShared (`APIShared/src/ModSettings/UI/ModSettingsSearch.cs`) und erzeugt für jedes interaktive Setting automatisch einen Anker aus lokalisiertem Titel, Tooltip und Ziel-`FrameworkElement`. Sie traversiert deklarativen Content-/Panel-Baum und realisierten Visual Tree; dynamische DataTemplates werden best-effort ergänzt, sobald die Seite normal realisiert ist. Kein mod-eigenes Suchregister, kein paralleles Index-System, keine Shared-Kopie.
- Beim Öffnen des Hubs nie zur Vorab-Realisierung `SelectedTab` umschalten und keine nicht angehängten Noesis-Views messen/arrangieren (kann den nativen Noesis-Zustand reentrant beschädigen).
- Struktur für eindeutige Titel: CheckBoxen/Buttons tragen den Titel im `Content`; Slider, TextBoxen, ComboBoxen stehen mit ihrem Titel in derselben Grid-Zeile oder einem klar zugeordneten Container; zusammengehörige Eingaben teilen denselben Tooltip.
- Nur bei verbleibender Mehrdeutigkeit `shared:ModSettingsSearch.Key` (modintern stabil, eindeutig) und `shared:ModSettingsSearch.Title` (lokalisiert) setzen; der 60000-ms-Tooltip bleibt die durchsuchbare Beschreibung. Mods ohne diesen Export indexiert SerpsModsHost best-effort aus sichtbarem Text und Tooltips.
- Suchoberfläche selbst mit `shared:ModSettingsSearch.Exclude="True"` ausschließen.
- Direkt bedienbare Treffer verwenden ein neues Control mit sicherer Kopie des Original-Noesis-Bindings und gleichem DataContext (Setter, Commands, Persistenz, MP-Sync bleiben bei der Mod) und übernehmen die effektive Bearbeitbarkeit. Nie über `PropertyInfo.SetValue(...)` oder ein paralleles System ändern; nicht sicher klonbare Bindings bleiben Navigationstreffer.
- Treffer bleiben auf die Breite der SerpsModsHost-Modsettings begrenzt (kein horizontaler Ergebnis-Scroll); im Treffer nur Modname, lokalisierter Titel und möglichst originalgetreuer Editor, Beschreibung nur als umbrechender 60000-ms-Hover-Tooltip.
- Der XAML-Audit prüft, dass alle Preset-Projekte die Suche über APIShared erhalten und jedes interaktive Setting automatisch eindeutig zuordenbar ist oder explizite Suchmetadaten hat; keine pauschalen oder sachfremden Sammelmarkierungen.

## Tooltips in Modoptionen

- Jedes interaktive Element (Button, CheckBox, ComboBox, Slider, TextBox, editierbare Tabellenzelle) hat einen passenden, nichtleeren Tooltip mit exakt `ToolTipService.ShowDuration="60000"`.
- Einheitlicher `ToolTip`-Stil: deckender dunkler Hintergrund, kontrastreicher Rahmen, `FontSize="{x:Static shared:ToolTipPresentation.FontSize}"`, `MaxWidth="{x:Static shared:ToolTipPresentation.MaximumWidth}"`, `TextBlock` mit `TextWrapping="Wrap"`.
- `Shared/Runtime/UI/ToolTipPresentation.cs`: Referenz 1440p mit Schriftgröße 20, proportional skaliert für höhere Auflösungen (niedrigere behalten mindestens die 1440p-Werte); kurze Texte bleiben schmal. Keine festen Zeilenumbrüche in Locale-Texten.

## Ingame-Hovertexte und eigene Popup-Tooltips

- Eigene Popup-Tooltips nutzen den Modoptions-Stil: Hintergrund `#FF1D1710`, weiße Schrift, Rahmen `#FFF2D48A`, umbrechender Text, `seui:ToolTipResolutionScale.Enabled="True"`, 60000 ms. Die lokalen Modoptions-Templates sind die Vorlage; keinen neuen Stil erfinden.
- Der Vanilla-Popupstil aus `TOOLTIPS.xaml` (Löwen) ist für Mod-Tooltips verboten. Ein bloßes `ToolTip="..."`, `.ToolTip = ...` oder `ToolTipService.SetToolTip(...)` genügt nur, wenn unser Stil im tatsächlichen Ressourcen-/Templatekontext nachweislich gilt; sonst ausdrücklich zuweisen.
- Bewusste Vanilla-Erweiterungen nutzen die bestehende Vanilla-Hovertextstelle ohne zusätzliches Popup: HUD-Aktionsbuttons (Formation inkl. Überlaufpfeil), Rekrutierungsvarianten-Pfeile, Einheitentooltips, Gebäude-/Einheiten-Kosten und -Limits. Diese nicht auf unseren Stil umstellen.
- Bei unklarer Zuordnung einer weiteren Oberfläche vorher den Nutzer fragen. Bestehende korrekte Tooltips und unveränderte Vanilla-Oberflächen erhalten.
- Absichern mit `Shared/Tools/Validation/Test-IngameTooltips.ps1` und Verhaltenstests (Hover, Ausblendung, Fremd-HUD, HUD-Neuerstellung). Darstellung nicht aus einem bestandenen Build ableiten.

## Lokalisierung

- Neue Locale-Keys immer in allen Sprachen ergänzen; ohne bekannte Übersetzung den englischen Text verwenden.

## Pflichttests vor dem Build

- `_inspect/HostClientPresetTests` plus XAML-Audit (Tooltips, Locale-Key-Parität, CRLF). Aufruf wie in `_inspect/HostClientPresetTests/README.md` (MSBuild/EXE; `dotnet run` ist für das .NET-Framework-Projekt ungeeignet).
- Die Test-EXE immer mit erhöhten Rechten bzw. außerhalb der Sandbox ausführen: Sie prüft Persistenz mit atomarem `File.Replace`; in einer Sandbox kann das erste Anlegen der `.msgpack` gelingen, das Ersetzen aber mit `UnauthorizedAccessException` scheitern, was (wegen unterdrückter Logausgabe) nur als verlorener Presetwert erscheint. Ein Persistenz-/Restart-Fehler aus einem sandboxierten Lauf ist keine Produktregression; zuerst dieselbe EXE unverändert erhöht wiederholen, erst bei erneutem Fehlschlag fachlich untersuchen.
