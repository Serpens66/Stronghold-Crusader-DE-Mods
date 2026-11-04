# Spectator — Entwicklerhinweise

## Aufbau

- `SpectatorPlugin` hält den Runtime-Einstieg und initialisiert unabhängige Features.
- `SpectatorPerspectiveRuntime`, Report-/Ally-Hooks, Save-Policy und Marker übernehmen
  die Zuschauerfunktionen ohne Abhängigkeit von BugfixesAndQoL.
- `PlayerSelectionController` ist die gemeinsame Auswahlgrenze; `SpectatorRuntime`
  verbindet Sitzung, Platzierung und native Testaktionen mit der Oberfläche.
- `GateHudLifecycle` und `StatisticsPreviewController` begrenzen die jeweilige HUD-Übernahme.
- `CreditAmountInput`, die Aktionsrichtlinien und optionale Anbieteradapter kapseln
  Eingabevalidierung, Einmaligkeit und modabhängige Verträge.

Spectator ist Teil des Steam-Modpacks „Serps Mods“ (`release-projects.json`,
`WorkshopConfiguration.ps1`); die Modversion steht in `SpectatorPlugin.PluginVersion` und
`info.json`. Es gibt keine Anwesenheitssperre für BugfixesAndQoL oder den früheren Testmod.
BugfixesAndQoL enthält nach der Auslagerung keine Zuschauer-Auswahl, Berichte, Save-Wiederherstellung, Statistikvorschau oder
Ausscheidensübergänge mehr. Aufgeben, Lobby-Rückkehr, allgemeines fremdes Truppen-HUD
und Statistikabzeichen bleiben dort. Beide Mods nutzen APIShareds gemeinsame
Endansicht-Ereignisse. Der alte Testmod gehört nicht zur gemeinsamen Testinstallation.

Test- und Cheatwerkzeuge (`EnableTestTools`, `EnableSingleplayerTestMode`) sind standardmäßig
aus. Der Karteneditor hat weder Spectator-Leiste noch Werkzeugsitzung. Der Frame-Callback
`OnBeforeRender` ist nur angemeldet, solange eine Mission eine anwendbare Funktion hat
(Zuschauer, ausgeschiedener Teilnehmer, aktive Werkzeuge außerhalb von echtem Multiplayer)
oder Präsentations-/Logarbeit wartet. Andere Threads melden ihn über Unitys
Synchronisationskontext an; Rollenwechsel prüft der `GameData.setGameState`-Postfix. Platzierungsdiagnose liegt in `SpectatorRuntime.Placement.DevDiag.cs`
(nur `build.bat /devdiag`).

## Build

`Spectator/build.bat` nutzt den gemeinsamen Branch-Dispatch der Packmods (Arbeitsstand,
Branch oder festgehaltener Release-Commit; SDK-Auswahl über `/apishared:…`). Ohne
`/noinstall` installiert der Treiber bei geschlossenem Spiel; danach schreibt er den lokalen
Buildnachweis. Releases laufen über `Spectator/release.bat` bzw. das Modpack.

## Nachweise vom 21.10.2026 (historisch, Entwicklungsstand vor der Pack-Aufnahme)

- APIShared: vollständiger Treiber erfolgreich; 700 Tests bestanden, zwei optionale
  Umgebungstests übersprungen. Öffentliche Änderungen ausschließlich additiv.
- Alle 1.477 geprüften installierten APIShared-Member-/Typreferenzen auflösbar.
- Aktueller BugfixesAndQoL-Quellstand kompiliert gegen dieses SDK in ein separates
  Testverzeichnis. Zuschauer-Preflight und Save-Prüfungen unverändert bestanden.
  Vorhandene Warnungen aus dessen Assassin-Code separat im Kompilierungslog.
- Spectator: `verify.ps1` und Buildtreiber bestanden. Produktionsrichtlinien und
  Controller, Eingabefokus, optionale Adapter, Bindungen, native Instruktionsverträge,
  Locale-Parität, XAML und CRLF geprüft. Paket nicht installiert.
- Die genauen Buildhashes und Logs liegen in `.local/`; APIShared führt zusätzlich
  seinen eigenen SDK-Buildnachweis. Änderungen an Quellen erfordern neue Nachweise.

## Noch offene Ingame-Abnahme

Nach Bereinigung der überlappenden Zuschauerfunktionen: Tor → Gebäude → Einheit →
Platzierung; alle acht Kräfte für verschiedene KIs bis zur tatsächlichen Wirkung;
Güterlieferreserve und spätere Einlagerung; ExtraFeatures-Automatik/Fest;
CheatMod/ExtremePowers einzeln und gemeinsam; Pause, Editor, Save-Laden, Ausscheiden,
Multiplayer-Zuschauerrechte und 1080p/1440p/4K. Automatisierte Tests und ein erfolgreicher
Build belegen weder diese Laufzeitwirkung noch die endgültige Darstellung.
