# Ingame-Tests per Code automatisieren

Stand: 20.10.2026. Untersucht wurden die installierten Spielassemblies, der lokale Script Extender sowie die Arbeitsquellen von BugfixesAndQoL und CastlePlanner.

## Was bereits möglich ist

| Aufgabe | Aufrufweg | Nachweis / Grenze |
| --- | --- | --- |
| Einzelspieler-Save laden | Vorhandener Testmod `AutoLoadSave`, konfigurierbar über `SavePath` | Am 19.10.2026 mit `test_dustynoattack.sav` im Spiel erfolgreich geprüft, einschließlich erstem Simulationstick. |
| Spielpause im Einzelspieler setzen/lösen | `EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, state, state)` | Öffentliche Spielmethode; Vanilla-Pausetaste und BugfixesAndQoL verwenden diesen Weg. |
| Multiplayer pausieren/entpausieren | Bestehende Paketwege in `MultiplayerGameSpeedRuntime` | Implementierung vorhanden, aber die Request-Methoden sind privat; ein unabhängiger Testmod braucht noch einen expliziten Zugriff über eine kleine öffentliche Brücke. |
| Kamera hinein-/herauszoomen | `PerfectPixelWithZoom.instance.adjustZoom(...)` | Öffentliche Spielmethode; positive Schritte zoomen hinein, negative heraus. |
| Kameraskalierung unmittelbar setzen | `PerfectPixelWithZoom.instance.SetZoomImmediate(scale)` | Öffentlich, aber mit Grenzen und internem Zoomzustand zu beachten; siehe unten. |
| Geskriptete Ereignisse im Messlauf (DEVDIAG-PerfProbe) | `Automation.Actions`, Einträge durch `\|` getrennt, Sekunden nach Beginn der Laufphase: `20:Attack:6:2:force` (KI 6 greift Spieler 2 an, über `0xD7580`), `15:IgnitePitch` (alle ruhenden Pechgräben zünden), `10:Spawn:3:600:CHIMP_TYPE_SPEARMAN` (600 Einheiten eines `eChimps`-Typs an der Keep-Tür von Spieler 3) | `Testmods/PerfProbe/src/PerfProbeAutomationActions.DevDiag.cs`; Angriff und Zündung am 29.10.2026 im Stress-Save geprüft, `Spawn` neu und noch ungeprüft. Ergebnisse stehen als `AUTOMATION action ...` im Log. |

Für diese Aktionen ist keine Mausautomation erforderlich. Dieses Dokument fügt keine neue Runtime-Funktion hinzu. Insbesondere bleibt AutoLoadSave beim einmaligen Laden des konfigurierten Saves.

## Voraussetzungen und Ausführungskontext

- Die folgenden Beispiele gehören **in den Spielprozess**, etwa in einen BepInEx-Testmod, nicht direkt in ein externes PowerShell-Skript.
- Missionsstart/-ende über `APIShared.IMissionLifecycleCapability` beobachten. `OnStart` bestätigt die vollständige verwaltete Initialisierung; zusätzlich auf `GameData.Instance.lastGameState`, erforderliche Kamera-/HUD-Objekte und für Simulationstests auf tatsächlichen Simulationsfortschritt warten. Ein Replay einer bereits laufenden Sitzung ist kein neuer Teststart.
- Native Extender-APIs erst nach deren erfolgreicher Initialisierung verwenden. Keine Singletons während `Awake` ungeschützt auslesen; `MainViewModel.viewModelLoaded` zuerst prüfen.
- Kamera, Unity und Noesis nur auf dem Unity-Main-Thread bedienen. Aus einem Simulationstick nur Ergebnisse/Zustandswechsel vormerken und Main-Thread-Arbeit über den geprüften Dispatcher oder einen statischen Render-Callback ausführen.
- Für Abläufe, die auch während einer Pause weiterlaufen müssen, einen statischen `Application.onBeforeRender`-Callback verwenden, auf einmal pro `Time.frameCount` begrenzt. Der Callback darf Arbeit nur für die aktuelle, freigegebene Mission ausführen.
- Früh erzeugte Plugin-Unity-Komponenten werden beim SHCDE-Startup-Cleanup zerstört. Daher keinen Ablauf auf Plugin-`Update`, `LateUpdate` oder einer dort gestarteten Coroutine aufbauen. Statische Zustände und langlebige Publisher verwenden; keine prozessweiten Subscriptions im Startup-`OnDestroy` entfernen.

## Einzelspieler: Spielpause setzen und lösen

Gemeint ist die Spielpause der normalen Pausetaste, nicht das Anhalten eines einzelnen Gebäudes:

```csharp
EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 1, 1); // Pause
EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0); // Weiter
```

Der erste Parameter ist der Spielbefehl; die beiden folgenden Integer entsprechen dem Vanilla-Aufruf. Nativ verwendet dieser Befehl den zweiten Integer nach dem Befehl als expliziten Zustand. Der optionale vierte Parameter bleibt 0. Der Rückgabewert von `GameAction` ist hier **keine Erfolgsbestätigung**: der native Pausenzweig gibt auch bei erfolgreicher Ausführung 0 zurück.

Den Zustand nach dem Aufruf über `SHCDESE.API.GamePlayerManagerAPI.Instance.IsLocalPaused()` prüfen. `GameData.Instance.lastGameState.game_paused` ist die über die Renderausgabe veröffentlichte Momentaufnahme und kann hinter dem direkten Zustand zurückliegen. Danach in einem begrenzten Beobachtungszeitraum prüfen, dass die relevanten Simulationscallbacks bei Pause ausbleiben und nach Entpause wieder eintreffen.

### Zwei verschiedene Arten von Pause

`Director.instance.SetPausedState(true/false)` und `Director.instance.Paused` steuern eine **andere, verwaltete Pause**, unter anderem für Menüs. Vanilla verweigert `SetPausedState(true)` im Multiplayer. Ein `false` für diesen Director-Zustand löst nicht automatisch die native Spielpause.

Für reproduzierbare Tests deshalb die native Spielpause über `Game_Paused` setzen. Den Director-Zustand zusätzlich protokollieren. Ein geöffnetes Menü oder Briefing kann den Test unabhängig von der nativen Pause beeinflussen; diese Zustände vor dem Test regulär verlassen, statt sie ungeprüft zu überschreiben. Keine `Time.timeScale = 0`-Ersatzpause verwenden: Sie bildet die native Pausensemantik nicht ab und würde Unity-basierte Abläufe beeinflussen.

CastlePlanner kann Entpausieren während einer aktiven Burgvorschau über `GameActionEvents.Pre` absichtlich ablehnen. Deshalb den öffentlichen Wrapper verwenden und den resultierenden Zustand prüfen. Weder den nativen P/Invoke direkt aufrufen noch das Pausefeld beschreiben, um solche Entscheidungen zu umgehen.

## Multiplayer: vorhandenen BugfixesAndQoL-Pfad verwenden

Die relevante Implementierung steht in [MultiplayerGameSpeedRuntime.cs](BugfixesAndQoL/src/MultiplayerGameSpeedRuntime.cs), insbesondere `QueueMultiplayerPausePress`, `TryQueueChange`, `TryBroadcastDirectUnpause`, `ProcessPacket` und `ApplyPauseState`.

Der Ablauf ist bewusst asymmetrisch:

1. **Pausieren:** Request mit `PauseAction`, `TargetSpeed = 0`, `PauseState = 1` über den synchronisierten Chore-Transport senden. Erst der gemeinsame Receiver setzt auf den Peers die native Pause; nicht bereits beim Senden lokal pausieren.
2. **Entpausieren:** denselben expliziten Zielzustand mit `PauseState = 0` als direktes Paket außerhalb der Simulations-Chore-Queue senden. Eine pausierte Simulation kann keinen wartenden Chore zum eigenen Aufwecken ausführen.
3. Direkte Empfangspakete werden auf den Unity-Main-Thread weitergereicht, gegen das aktuelle menschliche Spielerroster und den erwarteten Transport validiert und dann angewendet. Weil `SendPacketToAll` den Sender ausschließt, wendet der Sender den validierten Entpause-Request auch lokal an.

Die Berechtigung hängt an `EnableMod`, `EnableMultiplayerGameSpeedChanges` (`Disabled`, `OnlyHost`, `Everyone`), der aktuellen Host-/Rosterautorität und einer laufenden Multiplayer-Sitzung. Die vorhandene Behandlung eines Hostwechsels erhalten. Ein Testcontroller sollte für den normalen MP-Test auf dem autorisierten Host anfragen und auf jedem Teilnehmer den tatsächlich erreichten Zustand bestätigen lassen.

**Noch keine öffentliche Automations-API:** `TryQueueChange`, `TryBroadcastDirectUnpause` und die Runtime selbst sind nicht von einem fremden Testmod direkt aufrufbar. Die zweckmäßige nächste Erweiterung wäre eine kleine öffentliche Request-Brücke in BugfixesAndQoL, die die bestehende Runtime verwendet und dieselben Gates beibehält. Keine konkurrierende Paketregistrierung oder neue Pauseimplementierung im Testmod anlegen.

Innerhalb der bestehenden Runtime müsste diese Brücke sinngemäß nur folgende vorhandenen Aufrufe verbinden; dies ist ein **Ergänzungsvorschlag, keine bereits verfügbare Methode**:

```csharp
// Vorschlag innerhalb von MultiplayerGameSpeedRuntime:
internal bool RequestPauseForAutomation(bool pause)
{
    if (!CanRequestMultiplayerChange()) return false;
    return pause
        ? TryQueueChange(MultiplayerGameSpeedPolicy.PauseAction,
            0, "automation", pauseState: 1)
        : TryBroadcastDirectUnpause("automation");
}
```

Auch eine erfolgreiche Rückgabe dieses Requests bedeutet zunächst nur Anfrage/Versand, nicht bestätigte Ausführung auf allen Peers. Jeder Teilnehmer muss mit Sitzungskennung sein tatsächliches Ergebnis melden. Ein solo gestartetes MP-Spiel ist kein Nachweis für Host-/Client-Synchronisation.

## Kamera: hinein- und herauszoomen

```csharp
var cameraZoom = PerfectPixelWithZoom.instance;
float step = ConfigSettings.Settings_ExtraZoom ? 0.5f : 1f;
cameraZoom.adjustZoom(+step, loop: false); // hinein
// Erst nach Abschluss dieses Schritts einen weiteren Schritt auslösen:
cameraZoom.adjustZoom(-step, loop: false); // heraus
```

Das Beispiel zeigt zwei getrennte Aktionen; nicht beide unmittelbar hintereinander als sichtbaren Testschritt ausführen. `loop: false` verhindert ein Umspringen vom letzten zum ersten Zoomwert.

`adjustZoom` ist der geeignete Weg, wenn der Test das Verhalten eines normalen Zoomschritts prüfen soll. Es aktualisiert die interne Zoomposition und startet die Vanilla-Interpolation. Auf einem späteren Render-Frame `!cameraZoom.midZoom` abwarten und dann `cameraZoom.getZoom()` bzw. `currentZoomScale` lesen. Zusätzlich einen Timeout mit einer monotonen Uhr setzen; nicht nur eine feste Wartezeit von beispielsweise 200 ms annehmen. `getTargetZoom()` liefert während einer Interpolation `Min(aktueller Wert, Zielwert)` und eignet sich deshalb nicht allgemein als endgültiger Sollwert.

**Zoomen während der Spielpause:** Der direkte Aufruf von `adjustZoom` enthält keine Pause-Sperre; die Interpolation läuft in `PerfectPixelWithZoom.Update` über Unity-Zeit. Die Sperren der Tastatur-/Mausrad-Eingabe sitzen davor in `KeyManager` bzw. `CameraControls2D`. Ein Testmod kann daher direkt zoomen, obwohl die native Simulation pausiert ist, solange Unity weiter rendert. Für einen Test der Eingabesperren muss hingegen der Eingabepfad selbst untersucht werden; ein direkter Aufruf überspringt diese Sperren bewusst.

### Absolute Skalierung und reproduzierbare Bilder

```csharp
// Nur ohne laufende Zoominterpolation und nach Kamera-Initialisierung:
PerfectPixelWithZoom.instance.SetZoomImmediate(2f);
float actualScale = PerfectPixelWithZoom.instance.getZoom();
```

Dabei gelten folgende geprüfte Grenzen:

- Vanilla begrenzt den direkt angeforderten Wert zunächst auf 0,5 bis 4 und begrenzt zusätzlich das Herauszoomen anhand von Kartengröße, Auflösung und sichtbarer UI.
- `SetZoomImmediate` aktualisiert weder die interne Schrittposition `zoomPos` noch beendet es eine bereits laufende Interpolation. Ein alter Übergang kann den gesetzten Wert im nächsten `Update` wieder überschreiben; der nächste `adjustZoom`-Schritt kann von einer anderen Schrittposition ausgehen. Für Schrittfolgen daher `adjustZoom` verwenden. `ResetZoom()` setzt die Schrittposition zurück, sollte aber ebenfalls erst ohne laufende Interpolation erfolgen.
- BugfixesAndQoL hängt sich in die bestehenden öffentlichen Methoden ein. Bei aktivem `EnableClientFeatures` und `EnableResolutionAwareExtendedZoom` werden Skalierungen mit `max(1, Screen.height / 1080)` normalisiert und zusätzliche Zoomschritte freigegeben. Deshalb immer den **angewendeten** Wert lesen; ein Request von `2f` muss mit aktivem Mod nicht als `2f` zurückkommen.
- Direkte Änderungen an `Camera.orthographicSize` oder privaten Zoomfeldern würden den normalen Skalierungs-/Grenzpfad umgehen und können vom nächsten Update überschrieben werden. Für diese Tests sind sie nicht erforderlich.
- Zoom ist lokale Darstellung, auch im Multiplayer. Er muss nicht als Gameplay-Chore an andere Teilnehmer gesendet werden. Für vergleichbare Screenshots Auflösung, Kartengröße, UI-Sichtbarkeit, Kameraposition/-rotation und Zoom-Einstellungen je Teilnehmer festhalten.
- Im Tutorial löst `adjustZoom` zusätzlich einen Vanilla-`TutorialAction`-Aufruf aus. Ein allgemeiner Screenshot-Test sollte daher einen normalen Testspielstand verwenden, statt unbemerkt Tutorialfortschritt auszulösen.

## Kleine Helfer für einen Einzelspieler-Testmod

Diese Klasse illustriert direkt nutzbare öffentliche Aufrufe; sie registriert keine Callbacks und startet von sich aus keinen Test. Der Aufrufer gewährleistet die oben beschriebene Missions- und Extender-Bereitschaft und ruft sie auf dem Unity-Main-Thread auf.

```csharp
using CrusaderDE;
using SHCDESE.API;

internal static class AutomationActions
{
    private static bool Ready()
    {
        if (!MainViewModel.viewModelLoaded || Director.instance == null ||
            !Director.instance.SimRunning) return false;
        return GameData.Instance.lastGameState != null &&
            !MainViewModel.Instance.Show_HUD_Briefing;
    }

    public static bool TrySetSinglePlayerPause(bool paused)
    {
        if (!Ready() || Director.instance.MultiplayerGame ||
            Director.instance.Paused) return false;
        var players = GamePlayerManagerAPI.Instance;
        if (players.IsLocalPaused() != paused)
        {
            int state = paused ? 1 : 0;
            EngineInterface.GameAction(
                Enums.GameActionCommand.Game_Paused, state, state);
        }
        return players.IsLocalPaused() == paused;
    }

    public static bool TryRequestZoomStep(bool zoomIn)
    {
        if (!Ready()) return false;
        var zoom = PerfectPixelWithZoom.instance;
        if (zoom == null || CameraControls2D.instance == null || zoom.midZoom)
            return false;
        float step = ConfigSettings.Settings_ExtraZoom ? 0.5f : 1f;
        zoom.adjustZoom(zoomIn ? step : -step, loop: false);
        return true; // Request erfolgt; Abschluss/Zoomgrenze später prüfen.
    }
}
```

Der Pausehelfer verweigert vorsichtshalber jeden `Director.MultiplayerGame`-Kontext, einschließlich etwaiger lokaler MP-Varianten. Er ist keine Abkürzung für die Multiplayer-Brücke. Beim Zoom bedeutet eine erfolgreiche Anfrage nicht zwingend eine Wertänderung: eine erreichte Zoomgrenze ist ein reguläres Ergebnis.

## Ablauf für künftige automatisierte Tests

1. Spiel über Steam starten; `-skipvid` bzw. die bereits gesetzte Spieleinstellung `SkipIntro:True` überspringt das Intro. AutoLoadSave lädt den in `BepInEx/config/AutoLoadSave_Serp.cfg` angegebenen Spielstand einmal pro Prozess.
2. Der separate Testcontroller wartet auf die passende APIShared-Sitzung, ihre vollständige Initialisierung und mindestens einen relevanten Simulationstick. Eine eindeutige Lauf- und Sitzungskennung verwenden.
3. Auf dem Main-Thread native Pause anfordern und bestätigen. Noch ausstehende Simulation/Renderausgabe berücksichtigen; die Pause nicht als rückwirkende, atomare Momentaufnahme behandeln.
4. Bei Pause einen Zoomschritt anfordern, auf späteren Render-Frames den Abschluss beobachten und Kamera-/UI-Zustand erfassen. Dadurch sind wiederholbare Vergleichsbilder möglich, ohne die Welt während der Aufnahme weiterlaufen zu lassen.
5. Nach einer über `Stopwatch` gemessenen Pause entpausieren und den erneuten Simulationsfortschritt bestätigen. Das Entpausieren **nicht in einem späteren `OnTick` einplanen**: dieser Event läuft bei Spielpause gerade nicht.
6. Bei Missionsende die ausstehende Arbeit anhand der Sitzungskennung ungültig machen. Ein alter Timer darf keine neue Partie entpausieren oder deren Kamera verändern. Mehrere Requests als explizite Zustände statt Toggles behandeln.
7. PASS/FAIL einschließlich angefordertem und tatsächlichem Pause-/Zoomzustand, Tickbeobachtung, Timeout und Fehlermeldungen protokollieren. Kein Frame- oder Tickzähler des Hosts allein ist ein Beweis für unveränderte bzw. wieder laufende native Simulation.

Für schrittgenaues Simulieren einer festen Anzahl nativer Ticks wäre ein zusätzlicher Audit des Tick-Endes und des sicheren Pausenzeitpunkts erforderlich. Ein Pre-Tick-Event mit anschließendem Main-Thread-Request garantiert keine exakt tickgenaue Stoppstelle.

## Praxiserkenntnisse aus dem automatisierten Messlauf (PerfFixTest)

Stand: 20.10.2026, rund 40 vollständige Läufe mit `test_dustyFPS2.sav`. Referenzimplementierung damals im inzwischen stillgelegten PerfFixTest (Commit `a7978899f`). Seit 23.10.2026 liegt sie in PerfProbe: [PerfProbeAutomation.DevDiag.cs](Testmods/PerfProbe/src/PerfProbeAutomation.DevDiag.cs) und [Run-Automation.ps1](Testmods/PerfProbe/tools/Run-Automation.ps1).

### Im Spiel bestätigt

- **Laden ohne AutoLoadSave:** im Hauptmenü warten auf:
  - Extender bereit;
  - `MainViewModel.viewModelLoaded`;
  - `FatControler.currentScene == FrontEnd`;
  - `Show_Frontend_MainMenu` und kein Intro;
  - `HUDmain`/`HUDIngameMenu`, `Director.instance` und `EditorDirector.instance` vorhanden.

  Danach wie die Vanilla-Ladebestätigung:
  - Header über `MapFileManager.Instance.GetFileInfoFromFileName(path, path, 0, true)` prüfen;
  - `Platform_Multiplayer.Instance.gameMembers = null`;
  - `InitNewScene(MainGame)`, `Director.SetPausedState(false)`, `EditorDirector.stopGameSim()`, `loadSaveGame(...)`, `InitObjectiveGoodsPanelDelayed()`.

  Funktionierte in allen Läufen.
- **Missionsbereitschaft:** `Director.SimRunning`, `GameData.Instance.lastGameState`, `HUDmain`, kein Briefing, Kamera und Zoom vorhanden, dazu mindestens 80 beobachtete Ticks über `GameTimeManagerAPI.OnTick`. Der Tick-Callback läuft auf dem Sim-Thread und zählt nur (`Interlocked`).
- **Zoom:** `adjustZoom(-step, loop: false)`; vor jedem weiteren Schritt `!midZoom` abwarten. Die Zoomgrenze ist erreicht, wenn zwei abgeschlossene Schritte den Wert nicht mehr ändern. Bei 1080p und dieser Karte endet das nach 6 Schritten bei Zoom 0.25.
- **Pause:** Der Weg über `GameAction(Game_Paused, s, s)` mit Prüfung über `IsLocalPaused()` funktioniert zuverlässig. Nach dem Entpausieren treffen die ersten 5 Ticks nach ~0,1 s ein. In der Pause rendert Unity weiter, und `Application.onBeforeRender` läuft weiter. Eingriffe in die Darstellung (Renderer umschalten, Sprites tauschen) lassen sich dort fensterweise messen, weil die Simulation nichts zurücksetzt.
- **Ohne APIShared:** Für Leistungsmessungen ist eine Missionserkennung aus Spielzuständen und beobachteten Ticks ausreichend. Das hält die gemessene Installation minimal (Extender + Testmods). Das ist eine bewusste Ausnahme für Messmods, keine allgemeine Lifecycle-Regel.

### Gebäude und Partie für Spieltests vorbereiten

Stand 06.11.2026, DLL `FBCB9319…`, im Spiel geprüft mit `Testmods/UiAcceptance`.

- **Gebäude korrekt erzeugen:** `GameBuildingManagerAPI.CreatePrefab(player, x, y, mapper, BuildingScales.GetScale(mapper), 0x0F, true, bypass)`. Der sechste Wert (`a7`) ist die Grafikvariante: `0x0F` wählt wie Vanillas AIV-Bauweg die automatischen Sprites, `0` dagegen kamerabezogene Sprites und ergibt zerrissene Gebäudegrafiken. Für Hütte und Kornspeicher im Spiel bestätigt. Das ist nicht auf jeden Mapper übertragbar: Bei manchen Typen bedeutet der Wert Richtung oder Stil; die geprüfte Liste steht in `CastlePlanner/src/SupplementalPrefabVisualPolicy.cs`.
- **Rückgabewert ist kein Erfolg:** `CreatePrefab` kann auch bei abgelehntem Bauplatz einen Wert ungleich 0 liefern, etwa auf dem Startlager. Das Gebäude deshalb danach in der Gebäudeliste suchen und sonst den nächsten Platz versuchen. Ein Gebäude erscheint dort sofort. Die Startposition wechselt zwischen den Läufen, feste Abstände zum Lord treffen also manchmal belegte Felder.
- **Niederlage verhindern:** Auf hoher Spielgeschwindigkeit kann die KI den Lord schon nach wenigen Minuten töten. Danach steht das Spiel auf dem Defeat-Bildschirm, kein Schritt läuft mehr, und erst die Hängererkennung beendet den Prozess. Tests ohne Soldaten laufen deshalb mit Friedenszeit: Schritt `Lobby.PeaceTime` (Knopf `Setup`, Regler, `ApplySettings`) und danach `Hunt.ExpectPeace` als Nachweis. Friedenszeit bleibt aus, wenn Soldaten angreifen sollen.
- **Testmod installiert?** Fehlt `BepInEx/plugins/UiAcceptance_Serp`, bleibt das Spiel im Hauptmenü stehen. Vorher `Testmods/UiAcceptance/build.bat /nopause` ausführen.

### Tiere und Lager in Spieltests

Stand 06.11.2026, DLL `FBCB9319…`.

- **Ruhige Testwelt:** Szenarien mit Tieren oder Wirtschaft laufen mit Friedenszeit und ohne den RandomEvents-Mod (`EnableMod = false` auf `Settings` seines Plugins, Code unten). Sonst stören Wölfe, Brände, Seuchen oder Angriffe den Aufbau und machen Mengen und Ablauf unbrauchbar. Ohne Laufskript mit Rücksicherung bleibt der Schalter nach einem abgebrochenen Lauf aus und muss wieder eingeschaltet werden.
- **Kühe nur mit Rinderfarm:** Eine einzeln erzeugte Kuh löscht sich im ersten Update, weil sie zu einer Farm gehören muss. Im Test deshalb `MAPPER_CATTLEFARM` bauen. Auf Wüstenstarts braucht die Farm dafür den Bypass von `CreatePrefab`.
- **Echte Maus im Testlauf:** `NoesisView` schickt den echten Mauszeiger an die Oberfläche, sobald er sich bewegt. Wer während eines Laufs die Maus bewegt, zieht einen gerade gehaltenen Slider-Griff zum Zeiger; solche Schritte scheitern dann scheinbar zufällig. Schritte mit Ziehen oder Halten setzen deshalb `NGview.EnableMouse = false` und danach wieder `true`.
- **Tempo beim Messen:** Bei Geschwindigkeit 120 fragt ein Testschritt so selten ab, dass ein Jäger dazwischen einen ganzen Weg erledigt. Mengen, die während eines Wegs gelesen werden, deshalb bei normalem Tempo messen.
- **Fleischmengen messen:** Die Bevölkerung isst Fleisch aus dem Kornspeicher, das Lager allein beweist also keine Menge. Vorher Vanillas Schalter nutzen: `GameAction(SetFoodEaten, in_structure, 2)`. Der Zähler `0x87EEF64` hilft nicht, er zählt alle Lebensmittel einschließlich Käse.
- **Mauern im Test:** `CreateWall` baut nur auf zugelassenem Boden und nur solange Stein da ist; bei Mangel bricht die Linie ohne Fehler ab, und vier Ringe verbrauchen den Startvorrat. Vorher `SetLowWallCostMultiplier(0)` und `SetHighWallCostMultiplier(0)` setzen (`Hunt.Supply`). Ob eine Mauer steht, zeigt das Verbindungsraster (`GetPathComponentGrid`): Mauerkacheln wechseln dort die Komponente. Ein enger Ring ist nur mit einem Gebäude wieder zu öffnen, denn Mauern lassen sich per API nicht abreißen; eine Hütte am Rand (`MAPPER_HOVEL`) belegt ab ihrer Ankerkachel drei Felder, ein Turm lässt dagegen seine Eingangskachel offen.
- **Tiere als Testobjekte:** Einzelne Rehe gehorchen `MoveToTile` kaum und verschwanden in einem Lauf wenige Sekunden nach einem Versetzen. Hühner sterben, sobald der Vorrat über dem Sollwert der Kornkammer liegt, und bei einem Sollwert darüber bevorzugt der Jäger die nähere Kornkammer-Henne, sie taugen also nur für den einfachen Fall. Tierkadaver vergehen binnen Sekunden; wer Zeit braucht, setzt `+0x2C4` selbst auf 0.
- **Einheiten-IDs:** Das Zielfeld des Jägers behält die letzte ID, die inzwischen einer neuen Einheit gehören kann. Immer auch die Global-ID vergleichen.
- **Startpositionen:** Skirmish-Lords starten zufällig, und nicht von jeder Stelle ist dieselbe Richtung erreichbar. Testobjekte über die Komponente des Lords wählen, sonst scheitert derselbe Test nur bei manchen Starts.
- **Einstellungen:** Ein Testlauf ändert die persönlichen Einstellungen nicht dauerhaft, wenn das Laufskript sie vorher sichert und danach byteweise zurückschreibt (siehe „Laufskript“). Rücksetzschritte in einem Szenario sind dann wirkungslos, und ein Wert, den ein Szenario nicht setzt, kommt aus den persönlichen Einstellungen des Nutzers: Ein Szenario muss also jede Einstellung setzen, von der es abhängt. Beispiel: Ein dort stehender Fleischwert 37 machte einen Test mit Default-Erwartung unbrauchbar, und ein alter gespeicherter Wert `false` für eine erneut eingeführte Einstellung überschrieb deren neuen Standard. Eine Einstellung, deren Standard sich ändern soll und die schon gespeichert sein kann, braucht deshalb einen neuen Namen.
- **Offen:** In zwei Läufen kam `Tanner.Open` direkt nach `Skirmish → Spielstand laden` nicht durch (Klick nicht gelesen), wenn zuvor `Pause off` und `Speed` liefen. Auch mit `WaitTicks` als erstem Schritt trat es am 06.11.2026 einmal auf. Die Ursache ist nicht geklärt; `Tanner.Open` klickt deshalb bis zu viermal.

### Spiel beenden und Logs sichern

- `Application.Quit()` direkt nach dem Ergebnis verliert die letzten Sekunden in `LogOutput.log`. BepInEx 5.4.23 schreibt das Disk-Log zeitgesteuert. Lösung: `DiskLogListener.LogWriter.Flush()` über `BepInEx.Logging.Logger.Listeners` aufrufen und ~2 s warten, dann beenden.
- Bei `AppendLog=false` überschreibt jeder Start `LogOutput.log`. `Player.log` wird zu `Player-prev.log` verschoben. Beide müssen nach jedem Lauf gesichert werden, bevor das Spiel erneut startet.

### Spiel erneut starten

- Steam verweigert einen neuen Start, solange die vorige Instanz noch beendet wird; der Start geht dann ohne Fehlermeldung verloren. Vor dem nächsten Start deshalb warten, bis der Spielprozess **und** `UnityCrashHandler64` beendet sind, danach ~10 s Pause. Erscheint der Prozess nicht innerhalb von ~45 s, den Steam-Start einmal wiederholen.
- Start über `steam://rungameid/3024040`.
- **Startargumente** (z. B. `-force-d3d12`):
  - Ein Direktstart der exe wird über Steam neu gestartet, und dabei gehen die Argumente verloren.
  - Eine vorübergehende `steam_appid.txt` mit `3024040` im Spielordner ermöglicht den Direktstart. Die Datei danach wieder entfernen.
- **`boot.config`** (z. B. Graphics Jobs): vor dem Start ändern, die Originalbytes sichern und nach dem Lauf byte-genau wiederherstellen; Hash prüfen. `boot.config` wird nur beim Engine-Start gelesen.
- Ein Start mit `-force-d3d12` hat die gespeicherte Auflösung auf die native Monitorauflösung (3840×2160) umgestellt (PlayerPrefs `Screenmanager Resolution …`). Die Auflösung deshalb pro Lauf mitloggen (`Screen.width/height`) und Läufe nur bei gleicher Auflösung vergleichen.
- Neue Config-Schlüssel eines neu gebauten Testmods existieren erst nach dessen erstem Start. Ein Lauf-Skript muss fehlende bekannte Schlüssel samt Abschnitt anhängen können, statt abzubrechen.

### Messen und vergleichen

- Frame-Statistik selbst erfassen (Intervalle in `onBeforeRender`, Mittel/p50/p95/p99/Max). Die ersten ~2 s nach jedem Phasen- oder Umschaltwechsel verwerfen.
- **Streuung zwischen Läufen ist groß:** Identische Läufe lagen bei 133–173 FPS, weil Fremdlast auf dem PC (Browser, weitere Programme) mitwirkt. Belastbar sind:
  - Vergleiche **im selben Lauf**: Fenster in der Pause mit Kontrollfenstern am Anfang und Ende, Rückschalten genau der geänderten Objekte;
  - oder mehrere **abwechselnde** Läufe je Variante (A, B, A, B).
- **Pixelvergleiche:**
  - Nur zwischen Screenshots desselben Laufs sinnvoll; getrennte Läufe pausieren bei leicht verschiedenen Ticks.
  - Auch ohne Eingriff ändert sich die Uhr oben rechts (~450–700 Pixel), deshalb Kontrolllauf bzw. Kontrollfenster.
  - `ScreenCapture.CaptureScreenshot` schreibt asynchron; die Datei erst nach dem Spielende auswerten.
- **Grafik-API:** Unter D3D12 fehlt die Noesis-Oberfläche, und Vulkan ist nicht im Build. Automatisierte Tests mit Bildvergleich daher unter D3D11 durchführen.

## Einen eigenen Spieltest ohne vorhandene Testmods aufbauen

Stand 06.11.2026, DLL `FBCB9319…`. Alle Bausteine stammen aus einem im Spiel laufenden Testmod (rund 20 Szenarien mit Lobby, Karte, Einheiten und Oberfläche). Sie sind hier vollständig genug, um ihn neu zu schreiben. Die Spiel-APIs können sich ändern; Sichtbarkeit neuer Member gegen die echte `Assembly-CSharp.dll` prüfen. Schrittnamen wie `Lobby.PeaceTime` oder `Hunt.ExpectPeace` in anderen Abschnitten dieses Dokuments sind Handler solcher Testmods; was sie tun, steht hier.

### Aufbau

Vier Teile, jeder klein:

1. **Laufskript außerhalb des Spiels:** schreibt, welches Szenario laufen soll, startet das Spiel über Steam, wartet auf das Ende, sichert Logs und stellt Einstellungen wieder her.
2. **Testmod im Spiel (BepInEx-Plugin):** läuft nur, wenn in seiner Config `Enabled=true` steht, und setzt das sofort zurück, damit ein normaler Spielstart und ein Absturz keinen zweiten Lauf auslösen.
3. **Runner im Testmod:** arbeitet Schritte ab, einer nach dem anderen, einmal pro gerendertem Frame, auch in der Pause.
4. **Szenario als JSON:** Liste von Schritten, damit ein neuer Testfall keinen Neubau braucht.

Projektverweise (net481, `LangVersion latest`, `AllowUnsafeBlocks`, alle `Private=false`): `BepInEx`, `0Harmony`, `SHCDESE`, `RedBird.Core`, `System.Memory` (für `Span` aus dem Extender-Ordner), `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.ScreenCaptureModule`, `Assembly-CSharp` (die echte, nicht die publicized), `Noesis.NoesisGUI`, `com.rlabrecque.steamworks.net`, bei Bedarf `APIShared`.

### Runner

Ein Schritt ist ein `IEnumerable<Wait>`. Das hält lange Abläufe lesbar, ohne Coroutinen an Unity-Komponenten zu hängen, die der Startup-Cleanup zerstört.

```csharp
internal abstract class Wait { }
internal sealed class NextFrame : Wait { internal static readonly NextFrame Instance = new NextFrame(); }
internal sealed class Delay : Wait { internal readonly double Ms; internal Delay(double ms) { Ms = ms; } }
internal sealed class Until : Wait {
    internal readonly Func<bool> Condition; internal readonly string What;
    internal Until(string what, Func<bool> condition) { What = what; Condition = condition; }
}

// Einmal pro Frame aus einem statischen Render-Callback; guard begrenzt Schritte ohne Wartezeit.
internal static void Tick()
{
    if (scenario == null || lastFrame == Time.frameCount) return;
    lastFrame = Time.frameCount;
    try {
        for (int guard = 0; guard < 8; guard++) {
            if (finished || current == null && !BeginNext()) return;
            if (waiting != null && !Satisfied()) return;
            waiting = null;
            if (!current.MoveNext()) { CompleteStep(null); continue; }
            waiting = current.Current; waitClock = Stopwatch.StartNew();
            if (waiting is NextFrame) return;
        }
    }
    catch (Exception e) { CompleteStep(e is StepFailure ? e.Message : e.ToString()); }
}
```

`Satisfied()` wirft bei überschrittenem `step.TimeoutMs` (auf den ganzen Schritt gerechnet) einen Fehlschlag mit dem `What` des wartenden `Until`. Das spart viel Suche.

Antrieb: mit APIShared `ApiShared.ForMod(guid).Ui.TryRegisterBeforeRender("driver", Runner.Tick, out var frame, out string reason, 1000)` und danach `frame.SetContinuous(true)`, ohne APIShared ein statischer `Application.onBeforeRender`-Callback.

Szenario:

```json
{ "enabled": true, "series": "SP-beispiel", "role": "sp", "quitWhenDone": true,
  "steps": [ { "id": "menu", "do": "WaitMainMenu", "timeoutMs": 60000, "stopOnFail": true, "allowErrors": true },
             { "id": "x", "do": "Wait", "args": ["4000"], "timeoutMs": 15000,
               "expect": [ { "marker": "regex", "min": 1, "max": 1 } ] } ] }
```

Bewährt haben sich:

- **Ergebniszeilen:** jede mit Zeitstempel und eindeutig suchbar: `ACCEPT run=<label> step=<id> PASS|FAIL <was> ms=<dauer> :: <grund>`, am Ende `step=SUMMARY … passed= failed= skipped=`.
- **Log mitlesen:** Ein eigener `ILogListener` (`BepInEx.Logging.Logger.Listeners.Add`) hält alle Logereignisse im Speicher. So prüft ein Schritt Marker anderer Mods, ohne die Datei zu lesen. Die Datei bis zum Start einmal einlesen, weil früher geladene Mods schon geschrieben haben. Eigene `STEP`- und `ACCEPT`-Zeilen ausnehmen, sonst treffen sie die eigenen Suchmuster.
- **Fremde Fehler sind ein Fehlschlag:** Jeder Schritt schlägt fehl, wenn seit seinem Beginn eine fremde Quelle auf Error oder Fatal geloggt hat. Nur der erste Schritt und begründete Fälle setzen `allowErrors`. Getrennt danach `MOD_FAILURE` im Log suchen: Ein Hook, der an einer schon belegten Stelle scheitert (`Only N bytes could be decoded`), schaltet eine fremde Funktion stumm ab, während alles andere weiterläuft. Genau das ist hier einmal passiert, und die Spielschritte bestanden trotzdem.
- **Beenden:** nach der Zusammenfassung 2 s warten, `DiskLogListener.LogWriter.Flush()`, dann `Application.Quit()`.
- **Sichtbarkeit:** Zeiger (`GameUnit*`) sind in Iteratoren nicht erlaubt. Alles, was Einheiten anfasst, in eine gewöhnliche Hilfsmethode legen und aus dem Iterator unter der Sperre aufrufen.

### Zugriff auf die Simulation

Der Runner läuft auf dem Unity-Thread, die Simulation auf ihrem eigenen. Jeder Lese- und Schreibzugriff auf Einheiten und Gebäude läuft unter derselben Sperre wie die Simulation:

```csharp
FieldInfo f = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
object engine = f.GetValue(null);
lock (engine) { /* lesen und schreiben, kurz halten */ }
```

Tickzähler: `GameTimeManagerAPI.Instance.OnTick += tick => Interlocked.Increment(ref Ticks);`, und der Runner liest ihn mit `Volatile.Read`.

```csharp
int local = PlayerPerspectiveAPI.GetControlledPlayerId();
SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
GameUnit* unit = units.GetValuePointer(index);        // Einheiten-ID = index + 1
int id = (int)(unit - units._array) + 1;
bool used = unit->r_AliveState == AliveState.IsAlive; // auch Kadaver; lebend zusätzlich r_CurrentHealth > 0
int hunterState = unit->r_AIState;                    // dasselbe Feld wie +0x2BC
long newId = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, controller, x, y, height, eChimps.CHIMP_TYPE_DEER);
GamePlayerManagerAPI.Instance.GetLordUnitId(local);   // Start für alle Ortsangaben
```

Ein Mod-Objekt, das nicht öffentlich ist, per Reflection über einen Pfad wie `Namespace.Typ::statischesFeld.weiteres.Feld` auflösen. Damit lesen und setzen Schritte Einstellungen und auch interne Listen anderer Mods, ohne dass die Mods ein Testinterface brauchen.

### Partie vorbereiten

Alles über Vanillas eigene Befehle:

```csharp
// Hauptmenü bereit (nach dem Startup-Cleanup)
bool MenuReady() {
    if (!MainViewModel.viewModelLoaded || FatControler.currentScene != Enums.SceneIDS.FrontEnd) return false;
    var v = MainViewModel.Instance;
    return v.Show_Frontend_MainMenu && !v.Show_IntroSequence && v.HUDmain != null && v.HUDIngameMenu != null
        && Director.instance != null && EditorDirector.instance != null;
}

// Skirmish öffnen
MainViewModel.Instance.ButtonMainMenuFunction("Custom");
FRONT_Multiplayer lobby = MainViewModel.Instance.FRONTMultiplayer;
bool ready = lobby != null && lobby.currentLobby != null && lobby.panelActive
    && MainViewModel.Instance.Show_MPGameCreation && FRONT_Multiplayer.skirmishGame;

// Karte wählen: Zeile der privaten Liste `RefFileLists` (Noesis.ListView) per Dateiname suchen
list.SelectedItem = row; // row ist FileRow, Vergleich über Path.GetFileName(row.fileHeader.filePath)
// Danach das private Feld `selectedMPHeader` prüfen: Hat Vanilla sie angenommen?

lobby.SkirmishAIAddClick("1");     // KI zufügen, je Aufruf 250 ms warten
lobby.ButtonClicked("Setup");      // Friedenszeit: Panel öffnen, 250 ms warten
FRONT_Multiplayer_Setup.Instance.RefMP_Settings_Peacetime_Slider.Value = 60;
lobby.ButtonClicked("ApplySettings");
lobby.ButtonClicked("Play");       // danach auf den Missionsstart warten

// Briefing wegklicken, sobald es erscheint
MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
// Eine neue Partie kann pausiert beginnen
EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0);
Director.instance.SetEngineFrameRate(40);   // Spielgeschwindigkeit; 40 ist normal

// Nachweis, dass Friedenszeit läuft
APIShared.Simulation.PeaceTimeState.TryGetActive(out bool active);

// Zurück ins Menü
view.HUDIngameMenu.restartSkirmishMapInfo = null;   // sonst öffnet Vanilla die Lobby erneut
view.HUDmain.InGameOptions(null, null);             // je 300 ms warten
view.ButtonIngameMenuCommand.Execute("6");
view.ButtonConfirmationCommand.Execute("1");
```

Missionsbeginn zuverlässig: `APIShared.IMissionLifecycleCapability` (Beobachter registrieren, `notification.IsReplay` überspringen, Sitzungs-ID merken) und `FatControler.currentScene != FrontEnd`. Spielstand laden: siehe „Laden ohne AutoLoadSave“ oben; das klappt beliebig oft in einem Prozess. Der Pfad kommt aus `ConfigSettings.GetSavesPath()`.

Mod-Einstellungen nur im Menü setzen. Fremde Mods: `BepInEx.Bootstrap.Chainloader.PluginInfos[guid].Instance`, dessen Einstellungsobjekt und Eigenschaften per Reflection. Der Wert danach zurücklesen, denn ein Setter kann ablehnen.

### Oberfläche bedienen

Klicks gehen an Noesis, nicht an den echten Mauszeiger. Der Nutzer kann dabei weiterarbeiten. Das Spiel drängt sich aber oft in den Vordergrund:

```csharp
NoesisView view = FatControler.instance.NGview;
var element = GameXAMLManagerAPI.Instance.FindGlobalElement("ButtonBuildHunter") as Noesis.Button; // x:Name aus dem XAML
Noesis.Point c = element.PointToScreen(new Noesis.Point((float)element.ActualWidth / 2, (float)element.ActualHeight / 2));
view.MouseMove((int)c.X, (int)c.Y);
view.MouseButtonDown((int)c.X, (int)c.Y, Noesis.MouseButton.Left);
view.MouseButtonUp((int)c.X, (int)c.Y, Noesis.MouseButton.Left);
```

- **Bedingungen zuerst prüfen:** `element.IsVisible` und `IsEnabled` sind selbst Prüfgegenstand. Ein gesperrter Knopf lässt sich so ohne Klick nachweisen.
- **Bauleisten:** Eine Bauleiste öffnet `MainViewModel.Instance.HUDmain.SetupNewBuildScreen(id)`, danach 300 ms warten. Erst dann gibt es ihre Knöpfe.
- **Regler und Griffe:** `VisualTreeHelper` bis zum `Thumb` durchlaufen. Ein Griff bewegt sich erst, nachdem der Frame nach einem Mausereignis ihn neu angeordnet hat, und sein nächster Schritt wird von dort gemessen. Zwischen den Ereignissen deshalb 60 ms **und** drei gerenderte Frames warten, nicht nur Zeit. Das echte Mausziehen hat sich so von bloßem Wertsetzen unterschieden: Erst damit fielen Fehler wie zurückspringende Griffe auf.
- **Klick auf eine Kartenkachel:** Es gibt keine Maus auf der Karte, also per Harmony-Postfix auf `EditorDirector.preDLLCallActions` die Kachel (`ref int mouseOverX, ref int mouseOverY`) und auf `EditorDirector.getMouseStateForEngine` den Knopfzustand vorgeben: 1 für gedrückt, dann 3 für losgelassen, dann 0. Auf jeden Zustandswechsel warten, denn die Simulation liest nur einmal pro Durchlauf. So lässt sich ein Gebäude wie von Hand setzen, ein Beweis, dass der Knopf wirklich funktioniert.
- **Befehle statt Klicks:** Wo der Klick nicht Prüfgegenstand ist, gehen die öffentlichen `ICommand`-Eigenschaften von `MainViewModel` (`.Execute(parameter)`) schneller und robuster. Sie laufen über denselben Weg wie ein XAML-Knopf.
- **Screenshot:** `ScreenCapture.CaptureScreenshot(pfad)` und 500 ms warten. Mindestens einen je sichtbarem Zustand ablegen und mit dem Bildbetrachter wirklich ansehen; ein grüner Schritt beweist kein richtiges Aussehen.

### Testwelt zuverlässig bauen

Die meiste Arbeit steckte nicht in den Prüfungen, sondern darin, die Testwelt verlässlich zu machen:

- **Kein fester Ort:** Der Start wechselt. Jede Position relativ zum Lord oder zum eigenen Gebäude berechnen und vor dem Setzen prüfen, ob die Kachel dieselbe Komponente hat wie die Burg: `GamePathingManagerAPI.Instance.GetPathComponentGrid()[GameTileManagerAPI.Instance.GetTileId(x, y)]`. Sonst scheitert ein Test nur bei einem von vier Starts und sieht nach einem Modfehler aus.
- **Bauplätze:** Der Boden lehnt viele Stellen ab, besonders auf Wüstenkarten. Den Radius um den Zielpunkt schrittweise ausweiten und nach jedem `CreatePrefab` die Gebäudeliste prüfen. Der letzte Parameter `bypassGroundRules = true` setzt Gebäude auch auf eigentlich verbotenen Boden, etwa als Sichtblocker an genau einer Stelle. Für Mauern gibt es das nicht.
- **Mauerring als Sperre:** Vier `CreateWall`-Linien um einen Punkt. Danach prüfen, dass die Mauerkacheln die Komponente verlassen haben und die Innenkachel nicht mehr die des Jägers ist (kleine Flächen haben Komponente 0, auch das zählt). Weil der Boden oder Tiere auf einer Kachel einzelne Stücke ablehnen, werden mehrere Stellen versucht. Wichtig ist auch die Reihenfolge: erst den Ring schließen, dann die Einheit hineinsetzen.
- **Ring wieder öffnen:** Mauern lassen sich per API nicht abreißen. Lücke von drei Kacheln lassen, `MAPPER_HOVEL` so setzen, dass seine Ankerkachel am Lückenrand liegt, später `GameBuildingManagerAPI.Instance.DeleteBuildingSafe(id)`, die ID kommt aus `GetTileBuildingId`. Eine `CreateWall`-Linie quer über ein Gebäude hat im Test keine einzige weitere Kachel gebaut; die Linie deshalb nur bis zum Gebäude ziehen.
- **Tierherden:** `r_TribeId` einer Karten-Herde ist ihre Herde, der Datensatz liegt bei `0x7CC6720 + herde * 0x688`, Führer `+0x5A`, Flucht `+0x5F4`. Ein per `CreateUnitLocal` gesetztes Reh hat keine Herde und bleibt stehen, was als Testobjekt praktisch ist. Herden ziehen in Schüben: 800 ms ohne Bewegung des Führers abwarten, bevor etwas in ihrer Nähe gebaut wird. Bei hoher Geschwindigkeit verlassen sie einen Sektor in Sekundenbruchteilen.
- **Kadaver:** `KillUnit(id)` erzeugt einen echten Kadaver, im ersten Frame steht `r_AliveState` noch auf `NeedsInit`. Nach wenigen Sekunden ist er weg und die ID kann einer neuen Einheit gehören.
- **Reservierung `+0x448`** ist ein Countdown, kein Besitzer. Wer einen fremden Anspruch nachstellen will, muss ihn in jedem Frame erneuern; einmal gesetzt läuft er nach Sekunden aus und das Ergebnis täuscht.
- **Echte Pfeile:** `GameProjectileManagerAPI.CreateProjectile` mit `ArcherArrow` lieferte in zwei Versuchen 0 und erzeugte nichts (auch mit Koordinaten mal 8). Ein echter Pfeil lässt sich aber im Flug verändern: im Projektil-Array über `r_SourceUnitId` finden und `r_TargetUnidId` ändern. Fremdtreffer, also Tiere, die der Pfeil nicht anvisiert hat, kamen auf gestapelten Rehen in 150 s nicht von selbst vor.
- **Versetzen:** `SetCurrentLocalTilePosition` wirkt sofort, das versetzte Reh verschwand aber zweimal wenige Sekunden später. Für Schritte danach nicht darauf verlassen. Warum, ist nicht geklärt.
- **Kostenlose Mauern:** `SetLowWallCostMultiplier(0f)` und `SetHighWallCostMultiplier(0f)`. `TryAddGood` meldet ohne Lagerplatz `false`, das Lager lässt sich so nicht zuverlässig füllen.
- **Viele Testobjekte für Kostenmessungen:** Einheiten im Kreis um die Hütte setzen und Bauern (`CHIMP_TYPE_PEASANT`) dazu, sonst bleiben weitere Hütten unbesetzt.
- **Kartengröße:** `GameTileManagerAPI.Instance.GetCurrentMapSize()` im Spiel lesen. Die Dateigröße sagt wenig: `testanimals.map`, die zweitgrößte Datei im Kartenordner, ist nur eine 200er Karte. `4spielerkarte.map` und `2 Penninsula.map` haben Größe 400, `Another Close Call.map` 160. Für 800×800 einen entsprechenden Spielstand laden, hier `test_stresstest5.sav`.

### Damit ein Test wirklich etwas beweist

Mehrere Tests waren zuerst grün, ohne etwas zu prüfen:

- **Negative Tests brauchen einen Nachweis des Auslösers:** „Die Herde flieht nicht“ besagt nichts, wenn nichts sie hätte erschrecken können. Der Test zählt deshalb nur die Zeit, in der Vanillas eigener Sektorzähler (`0x3395E10 + sektor * 0xA0 + 0x48`) gesetzt ist, und das nur im richtigen Sektor. Ein Abschalten-Test mit deaktivierter Tierart bewies ebenfalls nichts: Dort galt die Tierart gar nicht als Ziel, und er bestand aus dem falschen Grund.
- **Aufbau selbst prüfen:** Ein Sichtblocker, der nie gebaut wurde (`placed=False`), ließ den Test bestehen. Jede Vorbedingung ist eine eigene Prüfung: Steht die Mauer, ist der Ring zu, ist das Gebäude an der richtigen Stelle.
- **Wiederverwendete IDs:** Felder wie das Ziel eines Jägers bleiben nach dessen Tod stehen, und die ID gehört bald einer neuen Einheit. Immer auch die Global-ID vergleichen.
- **Zeitfenster erzwingen statt hoffen:** Ein Fehler, der nur in einem Fenster von unter einer Sekunde auftritt, bleibt in normalen Läufen unsichtbar. Hier holten zwei Jäger denselben Kadaver, wenn einer von ihnen genau während des Ausblendens suchte; der vorhandene Zwei-Jäger-Test bestand trotzdem immer. Der Test setzt deshalb im Moment der Ankunft des ersten Jägers (Zustand 2) die untätigen Jäger 120 Frames lang auf Zustand 0. So trat der Fehler im ersten Lauf auf. Zuerst gegen den unkorrigierten Stand laufen lassen: Nur ein Test, der dort fehlschlägt, belegt die Korrektur.
- **Mengen vorhersagen:** `>= 37` wird auch nach mehreren Abschüssen wahr. Besser den genauen Wert aus Basiswert und Bonus erwarten (hier 6 mal 1,5 = 9), sonst fällt ein stiller Fehler nicht auf.
- **Einen Lauf nicht überinterpretieren:** Reihenfolge, Start und Zufall wechseln. Stellen, die einmal fehlschlagen, zwei- bis dreimal laufen lassen und bei jedem Fehlschlag die Ursache belegen, bevor Mod oder Test geändert wird. Hier waren fast alle Fehlschläge Testfehler, aber erst das Log eines jeden Falls zeigte, welcher echte Fehler (der belegte Hook, der unterwegs verschwindende Kadaver) dazwischen lag.
- **Szenario-IDs:** Die Schritt-`id` muss im Szenario eindeutig sein, sonst startet der Lauf gar nicht (`step=INIT FAIL`).
- **Fehlersuche im Test:** Beim Warten alle paar Sekunden den Zustand ins Log schreiben (Einheit, Zustand, Ziel, relevante Felder). Nach einem Timeout ist das fast immer der schnellste Weg zur Ursache; einen zusätzlichen Lauf spart es meist auch.
- **Wartende Einheit ist nicht frei:** Ein Jäger mit Fleisch in der Hütte sucht kein neues Ziel. Bei großen Mengen braucht er viele Wege, bis er wieder jagt, deshalb vor dem nächsten Fall auf Leerlauf warten (Tragmenge 0 und Zustand 0, 6 oder 7), statt eine feste Zeit zu warten.

### Laufskript

PowerShell, außerhalb des Spiels. Ablauf:

1. Abbrechen, wenn das Spiel läuft, und warten, bis `UnityCrashHandler64` fort ist; danach 10 s (siehe „Spiel erneut starten“).
2. Szenario nach `BepInEx/config/<Testmod>/` kopieren und in der Plugin-Config `[Run]` setzen: `Enabled = true`, `Scenario = <datei>`, `RunLabel = <label>`.
3. **Sichern:** `settings.cfg` des Spiels und alle Dateien unter `Dokumente\SHCDE\APIShared` byteweise in den Speicher lesen.
4. `BepInEx\LogOutput.log` löschen und mit `steam://rungameid/3024040` starten; nach 45 s ohne Prozess einmal wiederholen.
5. Warten, bis der Prozess weg ist. Wächter: Gesamtzeit und Stillstand (Logdatei seit `StallSeconds` nicht mehr gewachsen, 180 s haben gereicht). Bei beidem den Prozess beenden und das im Ergebnis ausweisen.
6. Im `finally` die gesicherten Dateien zurückschreiben, neu entstandene löschen, und danach byteweise vergleichen und das Ergebnis ausgeben.
7. `LogOutput.log`, `Player.log` und Screenshots in ein eigenes Ausgabeverzeichnis kopieren, Dateinamen mit Label.
8. Ergebnis: `ACCEPT run=`-Zeilen und `MOD_FAILURE` aus dem gesicherten Log.
9. Fehlschlag sichtbar machen: Als bestanden gilt nur genau eine Zeile `step=SUMMARY PASS … failed=0 skipped=0` dieses Laufs, kein Stillstand und bestätigt zurückgeschriebene Einstellungen; sonst wirft das Skript. Ein beendeter Prozess beweist nichts, er endet auch nach einem Absturz beim Start. Eine Schleife über mehrere Szenarien fängt das ab, sonst bricht die Serie beim ersten Fehlschlag ab.

Für Serien eine Schleife um das Skript, und bei einem Abbruch im Vorbereitungsschritt, der nur vom zufälligen Start abhängt (etwa „kein Platz“), automatisch neu starten. Ein Start braucht rund eine Minute, ein ganzer Lauf ein bis fünf Minuten.

### Echter Multiplayer

Zwei Rechner mit gleichem Mod-Stand. Bisher nur für Host/Client-Abläufe gebaut, die Ergebnisse dieser Prüfung stehen noch aus.

```csharp
MainViewModel.Instance.FrontEndMenu.ButtonClicked("Multiplayer");   // 1500 ms warten
lobby.ButtonClicked("Host");                                        // 800 ms warten
lobby.ButtonClicked("DoHost");
// Lobby-ID: lobby.currentLobby.id.m_SteamID; dem Client z. B. als Datei auf seiner Freigabe geben
Platform_Multiplayer.Instance.SetInviteLobbyID(id);                 // Client
Platform_Multiplayer.Instance.ResumeInvite();
```

Steam gibt private Lobbys über den Freundesstatus nicht preis, daher der Weg über die Datei. Beide Rechner treffen sich ohne eigenes Netzwerk über den Tickzähler seit Missionsbeginn, der ist auf allen Peers gleich. Ob es echter Multiplayer ist, im Modus der Mission prüfen: `realMultiplayer=True` und mindestens zwei `realNetworkGameMembers`. Ein Client muss mitspielen; ein solo gestartetes MP-Spiel beweist keine Synchronisation.

### Native Stellen vor dem Einbau prüfen

Vor einem Hook die Bytes lesen, nicht nur Dekompilat. Python ohne Zusatzaufwand (`capstone`, `pefile`):

```python
import pefile, capstone
pe = pefile.PE(dll, fast_load=True); data = pe.get_memory_mapped_image()
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
for i in md.disasm(data[a:b], a): print(f"{i.address:06X}: {i.mnemonic} {i.op_str}")
# Sprungziele in die Hook-Spanne suchen:
for i in md.disasm(data[start:end], start):
    if i.mnemonic.startswith("j"):
        try: t = int(i.op_str, 16)
        except ValueError: continue
        if s < t < e: print(hex(i.address), "->", hex(t))
```

- **Überlappung:** Prüfen, welche anderen Hooks (auch die eigenen) in der Nähe sitzen. Ein Hook verdrängt mindestens 14 Bytes und auf ganze Befehle gerundet; ein Hook am Anfang einer Sequenz kann also die Stelle dahinter schon mit belegen. Das Laden kann in Reihenfolge A, B funktionieren und B stumm fehlschlagen lassen.
- **Stubs einzeln ausführen:** Ohne das Spiel, im Testprogramm: die echten Bytes der Stelle in ausführbaren Speicher (`VirtualAlloc`, `PAGE_EXECUTE_READWRITE`) kopieren, den Stub darüber erzeugen, mit gewählten Registern aufrufen und das Ergebnis über eine kleine Rücksprung-Sequenz sichtbar machen. Jeder Zweig bekommt eine Prüfung. Das findet Registerfehler in Sekunden, die im Spiel als seltener Absturz enden würden.

## Prüfstand und Quellen

- Am 20.10.2026 wurden die öffentlichen Signaturen gegen die echte installierte `Assembly-CSharp.dll` geprüft. Die Helferklasse oben wurde unverändert aus diesem Dokument extrahiert und erfolgreich gegen echte Spiel-/Extender-Referenzen kompiliert; Nachweis unter `.work/t/automation/001`. Das ersetzt keinen Ingame-Test. Zusätzlich wurden der installierte Enumwert `Game_Paused = 1048` und der tatsächliche IL-Lesepfad von `IsLocalPaused()` geprüft.
- Laden, Zoom und Einzelspieler-Pause sind am 20.10.2026 in den automatisierten PerfFixTest-Läufen im Spiel ausgeführt und bestätigt (siehe Praxiserkenntnisse). Die Multiplayer-Brücke ist weiterhin nur statisch geprüft. Der AutoLoadSave-Lauf vom 19.10.2026 ist davon getrennte Evidenz: [Ladelog](.work/a/autoload/002/game-smoke.log).
- Momentaufnahme der Installation bei dieser Prüfung: Unter `BepInEx/plugins` wurden `000shcdese`, `PerfFixTest_Serp` und `PerfProbe_Serp` gefunden, aber keine dort installierten AutoLoadSave-, APIShared- oder BugfixesAndQoL-Assemblies. Vor einem späteren integrierten Test die tatsächlich installierten Abhängigkeiten erneut prüfen; für diese Dokumentation wurde nichts neu installiert.
- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Beide stimmen am Prüftag mit dem aktuellen Baseline-Manifest überein.
- Ladecode: [AutoLoadSavePlugin.cs](Testmods/AutoLoadSave/src/AutoLoadSavePlugin.cs). Multiplayer: [Runtime](BugfixesAndQoL/src/MultiplayerGameSpeedRuntime.cs), [Transportregeln](BugfixesAndQoL/src/MultiplayerGameSpeedPolicy.cs), [Berechtigungen](BugfixesAndQoL/src/MultiplayerTimeControlPermission.cs), [Rosterprüfung](BugfixesAndQoL/src/MultiplayerTimeControlRosterPolicy.cs).
- Zoom-Modifikation: [ResolutionAwareZoomHook.cs](BugfixesAndQoL/src/ResolutionAwareZoomHook.cs), [Zoomregeln](BugfixesAndQoL/src/ResolutionAwareZoomPolicy.cs). Entpause-Veto: `CastlePlanner/src/FreeCastlePreviewRuntime.cs`, `BeforeGameAction`.
- Vanilla-Nachweise liegen im über [CURRENT.json](_inspect/CrusaderDE-Native-Baseline/CURRENT.json) bestimmten Baseline-Datensatz: `KeyManager`, `Director`, `EngineInterface`, `PerfectPixelWithZoom`, `CameraControls2D` und der native Pause-/Simulationspfad. Alle RVAs dienen hier nur dem Nachweis; Automationscode soll die öffentlichen Wrapper benutzen.
