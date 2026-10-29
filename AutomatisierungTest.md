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

## Prüfstand und Quellen

- Am 20.10.2026 wurden die öffentlichen Signaturen gegen die echte installierte `Assembly-CSharp.dll` geprüft. Die Helferklasse oben wurde unverändert aus diesem Dokument extrahiert und erfolgreich gegen echte Spiel-/Extender-Referenzen kompiliert; Nachweis unter `.work/t/automation/001`. Das ersetzt keinen Ingame-Test. Zusätzlich wurden der installierte Enumwert `Game_Paused = 1048` und der tatsächliche IL-Lesepfad von `IsLocalPaused()` geprüft.
- Laden, Zoom und Einzelspieler-Pause sind am 20.10.2026 in den automatisierten PerfFixTest-Läufen im Spiel ausgeführt und bestätigt (siehe Praxiserkenntnisse). Die Multiplayer-Brücke ist weiterhin nur statisch geprüft. Der AutoLoadSave-Lauf vom 19.10.2026 ist davon getrennte Evidenz: [Ladelog](.work/a/autoload/002/game-smoke.log).
- Momentaufnahme der Installation bei dieser Prüfung: Unter `BepInEx/plugins` wurden `000shcdese`, `PerfFixTest_Serp` und `PerfProbe_Serp` gefunden, aber keine dort installierten AutoLoadSave-, APIShared- oder BugfixesAndQoL-Assemblies. Vor einem späteren integrierten Test die tatsächlich installierten Abhängigkeiten erneut prüfen; für diese Dokumentation wurde nichts neu installiert.
- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Beide stimmen am Prüftag mit dem aktuellen Baseline-Manifest überein.
- Ladecode: [AutoLoadSavePlugin.cs](Testmods/AutoLoadSave/src/AutoLoadSavePlugin.cs). Multiplayer: [Runtime](BugfixesAndQoL/src/MultiplayerGameSpeedRuntime.cs), [Transportregeln](BugfixesAndQoL/src/MultiplayerGameSpeedPolicy.cs), [Berechtigungen](BugfixesAndQoL/src/MultiplayerTimeControlPermission.cs), [Rosterprüfung](BugfixesAndQoL/src/MultiplayerTimeControlRosterPolicy.cs).
- Zoom-Modifikation: [ResolutionAwareZoomHook.cs](BugfixesAndQoL/src/ResolutionAwareZoomHook.cs), [Zoomregeln](BugfixesAndQoL/src/ResolutionAwareZoomPolicy.cs). Entpause-Veto: `CastlePlanner/src/FreeCastlePreviewRuntime.cs`, `BeforeGameAction`.
- Vanilla-Nachweise liegen im über [CURRENT.json](_inspect/CrusaderDE-Native-Baseline/CURRENT.json) bestimmten Baseline-Datensatz: `KeyManager`, `Director`, `EngineInterface`, `PerfectPixelWithZoom`, `CameraControls2D` und der native Pause-/Simulationspfad. Alle RVAs dienen hier nur dem Nachweis; Automationscode soll die öffentlichen Wrapper benutzen.
