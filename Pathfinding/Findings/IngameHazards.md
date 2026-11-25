# Feuer/Pest: erste Spielabnahme mit selbst erzeugter Testwelt

Momentaufnahme vom 22.11.2026. Sie belegt nur den hier genannten Stand und Ablauf und ersetzt weder T9 (MP) noch
T10 (Augentest).

## Identität

- Quellstand: Workspace `e19722c7b` („1195“) plus nicht committete Kommentar-/Testmodänderungen dieses Auftrags
  (Buildnachweis `.release-output/builds/Pathfinding/working.build.json`, erstellt 09:49:11 UTC).
- `Pathfinding.dll` SHA-256 `d937bdf28224d9c2…`, APIShared `dev` `d1bc1948` (sauber), `PathfindingHazardTest.dll`
  0.2.0 SHA-256 `637297a968aed608…`.
- Native DLL `CrusaderDE.dll` Hash `FBCB9319…31E2` (Baseline `CURRENT.json`).
- Lauf `2f168eeb588b40aab031db27f3f0a3b0` über `Shared/Tools/Automation/Invoke-InGameTest.ps1 -Scenario hazard-matrix
  -HiddenDesktop`; Plugin-Inventar und Einstellungen danach wiederhergestellt und geprüft.

## Aufbau

`test AI.map`, ein KI-Gegner (SK_RAT), Friedenszeit 60 min, Speed 250, RandomEvents aus, ExtraFeatures-Pestdauer
auf Faktor 1. Alle übrigen installierten Mods des Nutzers waren aktiv (u. a. BFQoL, Fixes, ExtraFeatures, APIShared).
Die produktive Sperre blieb aktiv (`mission options frozen; … avoidFire=False, avoidPlague=False`); die
Gefahrenoptionen galten nur in der privaten Optionskopie des Testmods während eines Falls
(`activated case=…`). Details: [ACCEPTANCE.md](../../Testmods/PathfindingHazardTest/ACCEPTANCE.md).

## Ergebnis (alle sieben Fälle PASS in einem Spielstart, keine Error-Zeilen im Log)

| Fall | Beobachteter Ausgang |
| --- | --- |
| `fire-escape` | Flucht, sichere Ankunft, Rückkehr, Ankunft am Ausgangspunkt, eigene Gruppe; KI-Speerkämpfer im selben Feuer nie gestoppt oder evakuiert (`escape-safe-return-arrival+ai-untouched`). |
| `plague-escape` | Flucht/Rückkehr wie oben vor acht echten Typ-22-Wolken. |
| `fire-wait-bounded` | Wartefall bei Feuer quer über dem Weg; Abbruch genau zur eigenen Frist (Tick ≥ `Until`), Feuer bestand noch, Stopp auf der Haltekachel. |
| `fire-wait-unlimited` | Wartefall überdauert die endliche Frist; nach Ende des Feuers weitergelaufen und angekommen, eigene Gruppe. |
| `plague-wait-bounded` | Wie `fire-wait-bounded` mit Pestsperre. |
| `plague-wait-unlimited` | Wie `fire-wait-unlimited` mit Pestsperre. |
| `plague-immune-crossing` | Lord quert die Pestsperre ohne Wartefall, nachweislich auf einer Pestkachel, und kommt an; der Kontroll-Speerkämpfer wartet vor der Sperre. Bestätigt die Immunitätsausnahme im Spiel. |

## Erkenntnisse aus den Vorläufen

- Die Replan-Zähler (`replanAborted` usw.) sind Zeitfenster-Metriken, die das Performance-Log periodisch
  zurücksetzt (`ResetReplanMetrics`). Sie taugen nicht als Testnachweis; der Testmod belegt Übergänge seither über
  den Zustand (Wartefall, Haltekachel, `replanSettled`). Kein Pathfinding-Fehler.
- Mit ExtraFeatures-Pestdauer ×4 (Nutzerprofil) treiben Wolken bis zu einem wartenden Soldaten und töten ihn, wenn
  die Flucht aus ist (so in den Wartefällen). Das ist korrektes Verhalten der gewählten Optionen; mit
  Standardeinstellung (`EscapePlague` an) flieht der Soldat. Nicht im Spiel abgenommen: Warten **mit** Flucht bei
  driftender Pest.
- `CHIMP_TYPE_HEALER` (34) ist der zivile Apotheker-Heiler (verschwindet sofort, wenn er ohne Arbeitsplatz gespawnt
  wird), der steuerbare `CHIMP_TYPE_BEDOUIN_HEALER` (79) ist laut `0x32BC40` nicht immun. Unter den Soldaten ist nur
  der Lord pestimmun; `IsPlagueImmune` bleibt unverändert, die Kommentare sind berichtigt.
- Mono kann eine Eigenschaft mit Zeigertyp nicht per `PropertyInfo.GetValue` lesen; der Testmod nutzt eine
  dynamische Methode.

## Grenzen

Nur Einzelspieler, flacher offener Boden, einzelne Soldaten in Einmanngruppen, Feuer über den Extender-Wrapper
(kein Pech, keine Feuerpfeile, kein Gebäudebrand), Pest über `0x9B2B0` wie Vanillas natürliche Pest. Nicht
abgedeckt: Höhen/Garnisonen (Q2, `EscapePlagueRaised`), Q4, Formationen/Queues (T5), Save/Load (T6), Last (T7),
Standalone ohne die übrigen Mods (T8), MP (T9), Sichtprüfung (T10), erneute Gefahr während der Rückkehr,
blockierte Rückkehr, Behandlung durch Apotheker.
