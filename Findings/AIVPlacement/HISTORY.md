# AIVPlacement: historische Native-Analyse

Der ursprüngliche Bericht `Findings/AICastlePlanner.md` beschrieb das native
KI-Burgsystem als Grundlage für CastlePlanners **menschlichen** Spawn-Castle-
Pfad. Seine untersuchte `CrusaderDE.dll` war Produktversion `2.7.0.1` mit
SHA-256
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.
Alle darin genannten RVAs sind **historisch** und dürfen nicht auf die aktuell
installierte DLL übertragen werden. Der Originalbericht ist unter seinem
ursprünglichen Pfad in [EVIDENCE.zip](EVIDENCE.zip) unverändert enthalten.

## Fortdauernd nützliche Unterscheidungen

- Die Lobbyoption „Completed enemy castles“ wird verwaltet als
  `advopt_pre_build`; die eigentliche Kandidatenwahl, Rotationsprüfung und
  Konstruktion geschehen in der nativen Startsequenz. Ein gesetztes Flag
  allein baut keine menschliche Burg.
- Vanillas nativer AIV-Baupfad unterscheidet zwischen menschlichem und
  KI-Besitzer. Die historische Untersuchung zeigte insbesondere, dass die
  menschliche Hovel-Variante den übergebenen Stil direkt nutzt, während der
  KI-Pfad eine eigene `0..6`-Folge bildet. CastlePlanners Human-Spawn-Pfad
  musste deshalb getrennt von der KI-Lobby-Prognose abgesichert werden.
- Der alte Bericht dokumentiert frühe Multiplayer- und Startup-Timingfallen:
  `Director.MultiplayerGame` und `Director.SkirmishModeGame` allein waren am
  synchronen `OnStartMap(Post)`-Punkt nicht ausreichend. Die späteren
  Implementierungsentscheidungen sind im Original nachvollziehbar.
- Native Adressen und Layouts, die der Bericht für eine ältere DLL nennt,
  sind keine Schnittstelle des Script Extenders. Für neue Änderungen gelten
  die installierte DLL, ihr Hash und die aktuelle
  [Native-Baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md).

## Entwicklung zur aktuellen Lobby-Prognose

Der ebenfalls archivierte ursprüngliche
`CastlePlanner/AIVPlacement_SOFORTSPAWN_FORSCHUNGSSTAND.md` dokumentiert die
Zwischenschritte der Offline-Prognose: Map-/AIV-Projektion, Oracle-Fits,
Startmarker und Drehung, Startgebäude, verknüpfte Records, Sofortbau sowie
geometrische Praxisbewertung. Viele ältere Abschnitte sind durch spätere
Messreihen bewusst eingeschränkt worden. Die verdichteten Ergebnisse stehen
in [EXPERIMENTS.md](EXPERIMENTS.md); für den aktuellen Funktionsumfang ist
[STATUS.md](STATUS.md) maßgeblich.

Die heute untersuchte Native-DLL trägt SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Ihr featurebezogener Daten- und Kontrollfluss ist in der aktuellen
[AIV-Lobby-Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md)
provenienzgebunden dokumentiert. Frühere Laufbelege bleiben über das Archiv
verfügbar, begründen aber keine allgemeine Freigabe für spätere KIs nach
Sofortbau.
