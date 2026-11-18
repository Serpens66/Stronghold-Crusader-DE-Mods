# WorkshopLoadProbe – Prototyp der sicheren Workshop-Installation

Reiner Entwicklungs-Testmod für den Plan in `WorkshopLoad.md`. APIShared und die Mods bleiben
unverändert. Nie Teil eines Releases.

## Bestandteile

| Teil | Aufgabe |
|---|---|
| `install/SafeInstall.cs` | Kern: Staging mit Hash-Prüfung, Keep-Liste mit Reset-Nummer, Umschalten per Ordner-Umbenennung mit Journal, Wiederherstellung. Ohne BepInEx-/Unity-Typen, damit Testprogramm und Patcher dieselbe Datei nutzen. |
| `patcher/` → `BepInEx/patchers/WorkshopLoadProbe.Patcher.dll` | Führt in `Finish` (vor dem Chainloader) Wiederherstellung, Anforderungen und den Selbstaustausch der eigenen DLL aus (`InstallRunner.cs`). |
| `observer/` → `BepInEx/plugins/WorkshopLoadProbe_Serp` | Nur für unbeaufsichtigte Läufe: schreibt das Log weg und beendet das Spiel nach `quitafter` Sekunden. |
| `payload/` → Test-Paket `WorkshopLoadProbePayload_Serp` | Wird nie von Hand installiert, nur über den Tausch. Meldet Variante, `settings.json`, `config/user.cfg` und ob `junk.log` noch existiert. |
| `tests/InstallHarness`, `tests/Run-InstallTests.ps1` | Offline-Tests: Szenarien, zufällige Prozessabbrüche, Zeitmessung mit dem echten SerpsMods-Item. |

Konfiguration `BepInEx/config/WorkshopLoadProbe.txt` (fehlt sie, ist alles inaktiv):
`install=on|off`, `quitafter=<Sekunden>`. Cache im Spiel: `BepInEx/cache/WLPI`
(`s` Staging, `b` Sicherung, `j` Journal, `r` Anforderungen, `p` Patcher).

## Werkzeuge (`tools/`)

Alle mit `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <Skript>` aufrufen. Sie
verweigern die Arbeit bei laufendem Spiel.

- `Set-ProbeConfig.ps1 [-Install] [-QuitAfter <s>]`
- `New-TestItem.ps1 -Variant 1|2 [-KeepRevision <n>]` erzeugt ein simuliertes Workshop-Item in
  `.work/a/workshopload-item/<n>/simulated`.
- `Stage-Install.ps1` bereitet einen Spieltest vor:
  - `-Variant 1|2 [-KeepRevision <n>]` stagt und fordert die Installation an.
  - `-AddPlayerFiles` verändert den installierten Ordner wie ein Spieler.
  - `-InterruptAt <Schritt>` tauscht sofort und bricht an diesem Schritt ab.
  - `-Patcher` stagt die frisch gebaute Patcher-DLL.
- `Invoke-ProbeRun.ps1 -Label <Name>` startet über Steam, wartet auf das selbstständige Beenden
  und ruft danach `Check-Run.ps1` auf, das die Logzeilen nach
  `_inspect/WorkshopLoadProbe/runs/` sichert.
- `Remove-Probe.ps1` entfernt Patcher, Beobachter, Konfiguration, Cache und den installierten
  Test-Payload.

## Ablauf der Tests

1. `tests/Run-InstallTests.ps1`, offline. Fixtures liegen in `.work/t/installtest` und werden
   bei Erfolg gelöscht.
2. `build.bat /nopause` installiert Patcher und Beobachter.
3. `Set-ProbeConfig.ps1 -Install -QuitAfter 20`
4. Spieltests, jeweils `Stage-Install.ps1 …`, dann `Invoke-ProbeRun.ps1 -Label …`:

| Test | Vorbereitung | Erwartung |
|---|---|---|
| G1 Erstinstallation | `-Variant 1` | `SWAPPED`; `PAYLOAD_VARIANT 'variant=1'`, `settings='default-1'` |
| G2 Update | `-Variant 2 -AddPlayerFiles` | `kept=2`; `variant=2`, `userFile='player config'`, `settings='player settings'`, `junk=False`; `only-in-variant-1.txt` fehlt |
| G3 Wiederherstellung | `-Variant 1 -InterruptAt old-moved` | `RECOVERED … rolled-forward`; `variant=1` mit Spielerdateien |
| G6 Reset | `-Variant 2 -KeepRevision 1` | `reset=1`; `settings='default-2'`, `config/` bleibt |
| G7 gleiche Nummer | Spieler ändert `settings.json`, `-Variant 1 -KeepRevision 1` | `reset=0`; Spielerversion bleibt |
| G4 Selbstaustausch | Patcher neu bauen (ohne Installation), `-Patcher` | `INSTALL_PATCHER_REPLACED`; nächster Start: neue `PATCHER_IDENTITY`, `.old` entfernt |

5. `Remove-Probe.ps1` und die Ordner `.work/a/workshopload-item/<n>` löschen.

## Ergebnisse (2026-11-18)

- **Offline:** 27/27 Prüfpunkte, 300 Abbrüche beim Umschalten und 40 beim Staging ohne
  Fehlschlag.
- **Zeit:** SerpsMods-Staging 1,4–4 s, Umschalten 8–430 ms.
- **Spiel:** G1, G2, G3, G6, G7 und G4 bestanden (Läufe `K-*` in
  `_inspect/WorkshopLoadProbe/runs/`).
- **Fehler, die die Tests im Prototyp gefunden haben** (behoben und mit Test abgesichert):
  - Der Elternordner der Sicherung fehlte (Spieltest).
  - Spielerdateien aus einem gescheiterten Versuch konnten veraltet übernommen werden.
- **Nicht geprüft:** Proton/Wine, mehrere Wurzeln pro Paket, Deinstallation.
