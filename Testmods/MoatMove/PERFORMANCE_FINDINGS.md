# MoatMove: Laufzeitbefund zu Test-lag.sav

## Messlauf vom 22.10.2026

Ausgewertet wurde der lokale Spielstart mit PerfProbe ab 18:23:38 bis zum
Spielende um 18:24:19 (Europe/Berlin). `Test-lag.sav` wurde um 18:23:56 geladen;
die Modsettings wurden um 18:23:58.204 protokolliert. Das Log nennt MoatMove
0.1.5, Bugfixes and QoL 1.0.195 und Script Extender 2.14.3.0.

Dieser Befund ist eine datierte Beobachtung dieses Laufs, keine Aussage über
spätere Builds oder die vollständige Ingame-Abnahme aller Bewegungsmodi.
Die älteren Extraktions- und Optimierungsnachweise bleiben unverändert gültig
für ihre jeweils dokumentierten Quellstände.

### Messaufbau und Nachweise

- PerfProbe 0.1.0 als DevDiag-Build; `PERFPROBE_READY` und Sampler-Start bestätigt.
- Main- und Simulationsthread, Intervall 1000 us, Aggregationsfenster 2 s.
- Sim-/Main-/Frame-Spikes ab 100 ms, Freeze-Markierung ab 400 ms;
  `EngineCalls = true`. Die genannten Suchfenster melden `dropped=0`.
- PerfProbe-Buildnachweis: `../../.work/b/working/294.build.json`.
  Installierte PerfProbe-DLL: SHA-256
  `2554E29BD3830ADF24FEE8EE97850C9B0E2AB59A20D2D21AE37C9F285338B6D1`.
- Originalquelle: `BepInEx/LogOutput.log` im installierten Spielordner.
  Der vollständige ausgewertete Logstand ist lokal unter
  [evidence/Test-lag-20261022-1823.log](evidence/Test-lag-20261022-1823.log)
  erhalten (838516 Bytes; SHA-256
  `8A5EFC51327D83DA967E58FCA3314932039E61661182D2992A48B45C08325715`).
  Der Live-Logpfad wird bei einem späteren Spielstart möglicherweise ersetzt.

## Bestätigt: lange Ticks durch gewichtete Burggraben-Wegsuche

| Beginn | Ende | Dauer EngineInterface.run |
|---|---|---:|
| 18:24:02.417 | 18:24:04.792 | 2374.7 ms |
| 18:24:04.792 | 18:24:07.555 | 2763.8 ms |
| 18:24:07.556 | 18:24:08.587 | 1031.7 ms |
| 18:24:08.595 | 18:24:10.055 | 1459.3 ms |
| 18:24:10.056 | 18:24:11.463 | 1406.8 ms |

Die Simulations-Samples zeigen folgende Aufrufkette:

    UnitCommandPathRuntime.EnableCompletedMoatModeForScopedMovement (BFQoL)
      -> FriendlyMoatTraversalProvider.ResolveScopedCompletedMoatModeTraversal (MoatMove)
      -> FriendlyMoatTraversalProvider.TryFindRequiredFriendlyRouteTraversal (MoatMove)
      -> WeightedMoatRoutePlanner.TryBuildCore / TryBuildCoreWithoutEvents (BFQoL)
      -> WeightedGridSearchKernel.Search
      -> WeightedGridSearchKernel.ReverseField.Settle

Weitere gemessene Einstiegspfade sind `FirstGroupUnitBarrier` über
`SelectOwnerSafeGroupMoatModeTraversal` sowie in geringerem Umfang
`PathBuilderBarrier`. Sowohl `TryProbeReachability` als auch
`TryBuildReachabilityEncoded` erscheinen unter der teuren Suche.

Die Summe der unmittelbar innersten Mod-Frames von `WeightedGridSearchKernel`
und `WeightedMoatRoutePlanner` beträgt in diesen Fenstern:

| Fenster endet | Suchframes / Simulations-Samples | Anteil |
|---|---:|---:|
| 18:24:04.651 | 1709 / 1716 | 99.6 % |
| 18:24:06.672 | 1724 / 1748 | 98.6 % |
| 18:24:08.672 | 1700 / 1715 | 99.1 % |

Auch die `self`-Sicht weist die tatsächliche Arbeit dort nach: insbesondere
`ReverseField.Pop`, `Settle`, `Before`, `TryCost`, die Zugriffe auf die
Suchwarteschlange und `WeightedMoatRoutePlanner.TryGetTraversalEdge`.
Es handelt sich damit nicht bloß um eine BFQoL-Assembly oder eine
`EngineInterface.run`-Harmony-Hülle irgendwo auf dem Stack. Die teure Suche
wird im beobachteten Lauf durch den MoatMove-Provider angefordert und im
gemeinsamen BFQoL-Suchkern ausgeführt.

## Bestätigt: zusätzlicher UI-Stillstand beim Merit-Zugriff

- `EngineInterface.GetMeritData` wartet von 18:24:05.001 bis 18:24:07.555
  insgesamt 2554.0 ms. Gleichzeitig läuft der 2763.8-ms-Simulationstick.
- `NoesisGUIUpdateChecksInGame` und `Director.Update` dauern entsprechend
  2554.2 bzw. 2554.3 ms. Die Bildlücke beträgt 2558.0 ms.
- Ein zweiter Merit-Zugriff dauert von 18:24:10.671 bis 18:24:11.463
  791.7 ms; die zugehörige Bildlücke beträgt 795.6 ms.

Gegen die installierte echte `Assembly-CSharp.dll` geprüft:
`EngineInterface.run` hält `threadLock` um `DLL_RunTick`; `GetMeritData`
verwendet dieselbe Sperre. Managed-SHA-256:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Damit erklärt die gemessene Sperrwartezeit den vollständigen Stillstand
einschließlich der fehlenden Menüreaktion. Ein langer Simulationstick allein
verhindert das Rendern nicht durchgehend: der erste 2374.7-ms-Tick meldet
beispielsweise `framesDuring=706`.

Die Merit-Cache-Optimierung meldet um 18:23:59.816 einen erfolgreichen Einsatz.
Der untersuchte Quellstand von `MeritDataLockOptimization` akzeptiert einen
Cache höchstens drei Sekunden lang und fällt sonst auf Vanilla zurück.
Das passt zum späteren Blockieren bei aufeinanderfolgenden langen Ticks;
welcher konkrete Cache-/Race-Zweig den jeweiligen Zugriff passieren ließ,
wird im Log nicht erfasst und ist deshalb nicht bewiesen.

## Abgrenzung und offene Fragen

- Nutzerbeobachtung: Abschalten des BFQoL-Host-Schalters beseitigt den Lag.
  Der Schalter deaktiviert nach bekanntem Fehlerstand zusätzlich andere
  Funktionalität; seine Korrektur wird in einem anderen Chat bearbeitet.
  Der Snapshot dieses Laufs zeigt `EnableMod=true`, aber fast alle einzelnen
  Host-Fixes ausgeschaltet, darunter `EnableTroopMovementFix` und
  `EnableImprovedManualUnitCommands`. Daraus keine korrekte Zuordnung zu
  einer einzelnen wirksamen Modsetting ableiten.
- Die Weglängenwarnung für Einheit 1621 um 18:24:01.988 (13 erwartet,
  12 beobachtet) liegt zeitlich davor; weitere Warnungen betreffen 1784 und
  2196. Sie sind kein Nachweis, dass diese Vertragsabweichung die teure Suche
  auslöst. Auch die RandomEvents-Ladewarnung ist nicht als Ursache belegt.
- Suchanfragen, expandierte Knoten, Cache-Invalidierungen und Suchbudgets
  fehlen im Messlog. Wiederholte Suchen, große unerreichbare Suchbereiche
  und unzureichende Wiederverwendung sind mögliche Untersuchungsrichtungen,
  keine bestätigten algorithmischen Ursachen.
- Es liegt hier kein gleich instrumentierter Gegenlauf mit abgeschaltetem
  Schalter oder ohne MoatMove vor. Der dominante Rechenpfad ist direkt
  nachgewiesen; die exakte Zusatzzeit gegenüber einer solchen Variante nicht.
  Sampler-Anteile sind Stichprobenanteile, und PerfProbe verursacht selbst
  Messaufwand. Überlappende Sim-, GUI- und Frame-Spikes nicht addieren.

Für eine weitere Ursachenanalyse die bereits identifizierten Suchanfragen
nach Anlass, Start/Ziel, Suchbudget, expandierten Knoten und Feldwiederverwendung
unterscheiden. Vor einer Verhaltensänderung gelten weiterhin der vollständige
featurebezogene Vanilla-Audit und die Vergleichstests der Suchergebnisse.
