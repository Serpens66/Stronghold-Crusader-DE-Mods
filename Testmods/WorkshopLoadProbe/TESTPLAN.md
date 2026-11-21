# WorkshopLoadProbe – Spieltests der sicheren Workshop-Installation

Reiner Entwicklungs-Testmod für die sichere Workshop-Installation von APIShared
(`APIShared/docs/WORKSHOP.md`, Abschnitte „Keep list“ und „Installation (preloader swap)“;
Plan und Entscheidungen in `WorkshopLoad.md`). Installiert wird ausschließlich durch APIShared
selbst; dieser Testmod steuert nur unbeaufsichtigte Spielstarts. Nie Teil eines Releases.

Die Offline-Tests der Installation liegen in APIShared: `tests/Core.Tests/WorkshopSafeInstallTests.cs`,
`WorkshopPackageTests.cs` und der Prozessabbruch-Test `tools/Validation/Test-WorkshopInstall.ps1`
(Teil des vollen APIShared-Builds).

## Bestandteile

| Teil | Aufgabe |
|---|---|
| `patcher/` → `BepInEx/patchers/WorkshopLoadProbe.Patcher.dll` | Reicht `quitafter` aus der Konfiguration an den Beobachter weiter. Installiert nichts. |
| `observer/` → `BepInEx/plugins/WorkshopLoadProbe_Serp` | Nur für unbeaufsichtigte Läufe: schreibt das Log weg und beendet das Spiel nach `quitafter` Sekunden. |
| `payload/` → Test-Paket `WorkshopLoadProbePayload_Serp` | Inhalt des Test-Items. Wird nie von Hand installiert, nur durch APIShared. Meldet Variante, `settings.json`, `config/user.cfg` und ob `junk.log` noch existiert. |

Konfiguration `BepInEx/config/WorkshopLoadProbe.txt` (fehlt sie, ist alles inaktiv): `quitafter=<Sekunden>`.

## Werkzeuge (`tools/`)

Alle mit `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <Skript>` aufrufen. Sie
verweigern die Arbeit bei laufendem Spiel.

- `Set-ProbeConfig.ps1 -QuitAfter <s>`
- `Stage-ApiSharedInstall.ps1 -ItemId <id> -Action Install|Update|Reinstall|Downgrade|Remove [-CrashAt <Schritt>]`
  plant und stagt ein abonniertes Workshop-Item mit den Klassen des installierten `APIShared.dll`;
  den Tausch macht der echte APIShared-Preloader beim nächsten Start. `-CrashAt
  journal|carried|old-moved|new-moved|receipt|backup-deleted` führt den Tausch sofort aus und
  beendet den Prozess an diesem Schritt; dafür das Skript als eigenen Prozess starten (`pwsh -File`).
- `New-TestItem.ps1 -Variant 1|2 [-KeepRevision <n>]` erzeugt aus dem gebauten Payload ein
  Workshop-Item-Layout mit Keep-Liste in `.work/a/workshopload-item/<n>/simulated`, etwa um eine
  neue Version des Test-Items hochzuladen.
- `Invoke-ProbeRun.ps1 -Label <Name>` startet über Steam, wartet auf das selbstständige Beenden
  und ruft danach `Check-Run.ps1` auf, das die Logzeilen nach `_inspect/WorkshopLoadProbe/runs/` sichert.
- `Remove-Probe.ps1` entfernt Patcher, Beobachter und Konfiguration. Das Test-Paket entfernt
  `Stage-ApiSharedInstall.ps1 -Action Remove` (APIShared löscht es wie jedes Workshop-Paket).

## Ablauf

1. APIShared normal bauen und installieren (Plugin und Preloader aus demselben Build).
2. `build.bat /nopause` installiert Patcher und Beobachter dieses Testmods.
3. `Set-ProbeConfig.ps1 -QuitAfter 20`
4. Je Test `Stage-ApiSharedInstall.ps1 …`, dann `Invoke-ProbeRun.ps1 -Label …` und Dateien bzw.
   Log prüfen (`INSTALLED`, `RECOVERED … rolled-forward`, `REMOVED` auf `APIShared.WorkshopInstall`).
5. Test-Paket mit `-Action Remove` entfernen, danach `Remove-Probe.ps1`.

## Ergebnisse

- 2026-11-18, Prototyp (inzwischen in APIShared aufgegangen und hier entfernt): 27/27
  Offline-Prüfpunkte, 300 Abbrüche beim Umschalten und 40 beim Staging ohne Fehlschlag;
  SerpsMods-Staging 1,4–4 s, Umschalten 8–430 ms; Spieltests G1–G7 bestanden (Läufe `K-*`).
- 2026-11-18, echtes APIShared (Item 3841874432): Erstinstallation, Neuinstallation mit
  Fremddateien, echter Prozessabbruch bei `old-moved` (Reparatur `rolled-forward`) und
  Deinstallation bestanden (Läufe `api-g*`).
- Nicht geprüft: Proton/Wine; Keep-Liste und mehrere Wurzeln nur offline.