# UCP-Features: Einheiten und Balance

## Offizielle DE-Nachfolger

| UCP-Feature | DE-/Workspace-Status | Empfehlung |
| --- | --- | --- |
| `u_laddermen` – Fernkampfrüstung und Kosten 20 | DE besitzt `ImprovedLaddermen`; Script Extender bietet `Is/SetImprovedLadderman` | offizielle Option verwenden; exakte Zahlen nur bei Bedarf gegen UCP vergleichen |
| `u_spearmen` – bessere Fernkampfrüstung | DE besitzt `ImprovedSpearmen`; öffentliche API vorhanden | offizielle Option verwenden |
| `u_spearmen_run` – standardmäßig laufen | `BugfixesAndQoL/SpearmanMovementPatch` stellt bei aktivem `ImprovedSpearmen` die normale Archer-Laufentscheidung her | **abgedeckt**, keinen zweiten Patch installieren |
| `u_arabwall` und `u_arabxbow` | DE bündelt Verbesserungen in `ImprovedArabSwordsmen`; öffentliche API vorhanden | als offiziellen Nachfolger behandeln; numerische Gleichheit separat testen, falls UCP-exakte Balance gewünscht ist |
| `o_healer` | DE besitzt Advanced Option `Healers`; `IsBetterHealers`/`SetBetterHealers` ist öffentlich | **in DE integriert**; Workspace-Heilerfixes betreffen zusätzliche Randfehler und sind kein Ersatz für die Aktivierung |

Die Advanced Options sind Teil der synchronisierten Spielregeln. Falls ein eigenes Preset sie erzwingen soll, ausschließlich die öffentlichen Script-Extender-Setter verwenden und keine parallelen Damage-/Chore-Hooks installieren.

## `o_restore_arabian_engineer_speech`

UCP wählt bei einem arabischen menschlichen Lord die vorhandenen arabischen Ingenieuraufnahmen für Auswahl, Kauf und Belagerungsgerät. **DE-Relevanz: nur nach Hörtest.** Die Baseline bestätigt, dass DE die vollständige `AEngineer_*`-Familie bereits in `SFXManager` führt. Native Kandidaten bei RVA `0x3770` und `0x3BC0` referenzieren ausdrücklich arabische Ingenieur-Tabellen; ein weiterer Soundpfad bei RVA `0xD6030` enthält westliche Engineer-Auswahlstrings. Das Vorhandensein beweist jedoch noch nicht die richtige Auswahl für den menschlichen arabischen Lord.

Zuerst Auswahl, Rekrutierung, Bemannung jedes Belagerungsgeräts, Graben, Öl und Auflösung mit europäischem/arabischem Lord gegentesten. Falls die Kulturwahl falsch ist, den kleinsten Dispatch-Zweig korrigieren, der Lordkultur und `CHIMP_TYPE_ENGINEER` zusammenführt; keine Sounds nachträglich zusätzlich abspielen, weil sonst Doppelwiedergabe und falsche Varianten entstehen. Keine Audiodateien aus HD kopieren: DE besitzt die Assets bereits. Lokal hörbares Verhalten kann `NetworkMode=0` sein, sofern der Hook nur die Sound-ID auswählt und keinen Simulationszustand verändert.

## `o_shfy` / Strongholdify

UCP passt Bierpopularität, Religionspopularität, Bauern-Spawnrate und gelieferte Ressourcenmengen an Stronghold 1 an. **DE-Relevanz: reines alternatives Balancepreset, nicht abgedeckt.** `BuildingCosts`, `UnitCosts` und `StartConditions` überschneiden sich nur am Rand und ersetzen diese vier Mechaniken nicht.

Eine DE-Portierung sollte vier getrennte `[SyncHostOnly]`-Optionen statt eines undurchsichtigen Gesamtpatches anbieten. Die erneute API-Prüfung liefert bereits zwei bessere Eingriffspunkte:

- Bier/Inn-Abdeckung kann über `PlayerR3EventHooks.OnPlayerCalculateDrunkPercentage` an der bestehenden Berechnung verändert werden; das Event besitzt einen schreibbaren `ReturnValue`.
- zusätzliche Arbeiter-Liefermenge kann über `UnitR3EventHooks.OnCalculateBonusYield` am dafür vorgesehenen Bonuspfad verändert werden. Zuerst prüfen, ob UCP den Grund- oder Bonusyield meint; das Event darf nicht versehentlich jede Lieferung doppeln.

Für Religionspopularität gibt es bislang keinen gleichwertigen öffentlichen Formelhook. `SetPlayerPopularity` ist ungeeignet, weil periodisches Setzen alle anderen Popularitätsanteile überschreiben würde. Die Bauern-Spawnraten liegen zwar als drei Tabellen für hohe, niedrige und normale Popularität vor, sind in `GamePlayerManagerAPI` aber privat gekapselt; hier ist eine öffentliche Extender-Konfiguration oder ein konfliktgeprüfter Tabellenoverride dem Patchen des kompletten Keep-Updates vorzuziehen. Alle vier Optionen getrennt testen und mit KI-Profilen abstimmen.


Die Einordnung der öffentlichen und nativen Eingriffspunkte steht gesammelt in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).
