# WorkshopLoadProbe – Testplan (Phase 0 aus `WorkshopLoad.md`)

Reiner Entwicklungs-Testmod. Er klärt, ob Workshop-Mods direkt aus
`steamapps/workshop/content/3024040` geladen werden können. APIShared und die Mods
bleiben unverändert. Nie Teil eines Releases.

## Bestandteile

| Teil | Ort im Spiel | Aufgabe |
|---|---|---|
| `patcher/` → `WorkshopLoadProbe.Patcher.dll` | `BepInEx/patchers` | Leitet beim Ereignis „Chainloader ready“ die Plugin-Suche in `Chainloader.Start` um (IL-Hook, nie zurückgenommen). Liest die `.acf` (T3), prüft das Inventar jedes APIShared-Items mit Hash-Cache (T5), wendet die Regel „lokal gewinnt“ an (P3) und lädt nur freigegebene Items (T1). Löst Hilfs-DLLs aus Workshop-Wurzeln auf (P2). |
| `observer/` → `WorkshopLoadProbe_Serp` | `BepInEx/plugins` | Weiche Abhängigkeit auf den Payload (Ladereihenfolge). Registriert geladene Workshop-Wurzeln nachträglich beim Script Extender (`RegisterAssetMod`) und misst die Wirkung (T4). |
| `payload/` → `WorkshopLoadProbePayload_Serp` | nur außerhalb von `plugins` | Test-Plugin mit Hilfs-DLL, `info.json`, Übersetzung, unsichtbarem XAML-Patch auf `FrontendMenus.xaml` und `Scripts/init.lua`. |

Ohne `BepInEx/config/WorkshopLoadProbe.txt` ist alles inaktiv (Logzeile `WLP INACTIVE`).
In Workshop-Ordner schreibt der Testmod nie. Er schreibt nur
`BepInEx/cache/WorkshopLoadProbe.hashes.tsv` und `BepInEx/cache/wlp_*_typeloader.dat`.

## Werkzeuge (`tools/`)

Alle mit `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <Skript>` aufrufen. Alle
außer den Schnappschüssen verweigern die Arbeit bei laufendem Spiel.

- `Set-ProbeConfig.ps1 -Mode off|observe|load [-Item <Ordner>] [-Load <Schlüssel>] [-Duplicates skip|bepinex] [-Resolver on|off] [-TypeCache on|off] [-HashCache on|off] [-ClearCaches]`
- `New-TestItem.ps1 [-Variant 1|2]` legt `.work/a/workshopload-item/<n>/` an. Darin liegen
  `source/` für den Upload und `simulated/` im veröffentlichten Aufbau (Ladeschlüssel `simulated`).
- `Check-Run.ps1 -Label <Name>` direkt nach jedem Spielstart ausführen. Das BepInEx-Log wird
  beim nächsten Start überschrieben. Die Ausgabe landet in `_inspect/WorkshopLoadProbe/runs/`.
- `Snapshot-Workshop.ps1 -ItemId <ID> -Label <Name>` und `Compare-WorkshopSnapshots.ps1 -Before <json> -After <json>`
  halten den Steam-Zustand fest (`.acf`, `content`, `downloads`, `temp`). Ablage:
  `_inspect/WorkshopLoadProbe/snapshots/`.
- `Set-LocalPayloadCopy.ps1 -State Present|Absent` legt den Payload zusätzlich in `plugins`
  bzw. entfernt nur genau diese Kopie wieder.
- `Remove-Probe.ps1` entfernt Patcher, Beobachter, Konfiguration und Caches aus dem Spiel.

## Vorbereitung

1. `build.bat /nopause` baut und installiert Patcher und Beobachter. Ohne Konfiguration sind
   beide inaktiv.
2. `tools/New-TestItem.ps1 -Variant 1`, den ausgegebenen `simulated`-Pfad merken.
3. Ruhige Testwelt: RandomEvents-Mod nicht aktiv. Spielzeitfenster mit anderen Sitzungen
   abstimmen. Für die Läufe A bis E genügt das Hauptmenü, Maus und Tastatur werden nicht gebraucht.

## Lokale Läufe (T1, T3, T4, T5)

| Lauf | Konfiguration | Erwartung / Frage |
|---|---|---|
| A kalt | `-Mode observe -Item <simulated> -ClearCaches` | `HOOK_INSTALLED`; `DECISION 3788821961 skip-local [...]` (lokale SerpsMods gewinnen); `DECISION simulated observe-only`; `INVENTORY ... hashed=<alle>`; `SCAN ... ms`; `ACF_ITEM`-Zeilen (T3). Zusätzlich die Patch-Anzahl von `FrontendMenus.xaml` als Vergleichswert notieren. |
| B warm | wie A ohne `-ClearCaches` | `INVENTORY ... hashed=0 cacheHits=<alle>`; `SCAN`-Zeit mit Typ-Cache. Für einen wirklich kalten Lauf A direkt nach einem Neustart von Windows wiederholen (T5). |
| C laden | `-Mode load -Item <simulated> -Load simulated` | `PAYLOAD_AWAKE info=...simulated...`; `RESOLVE resolved WorkshopLoadProbe.Helper`; `PAYLOAD_HELPER helper=...simulated...`; `ORDER payload loaded before observer=True`; `T4_REGISTER ... registered=True`; `T4_MODHASH` vorher ≠ nachher; `T4_PATCHES indexedAfter = indexedBefore+1`; `T4_LOCALES` en-US +1; `T4_LUA` enthält die Payload-GUID; Patch-Anzahl `FrontendMenus.xaml` = Wert aus A + 1 (wirkt die späte Registrierung noch?). Prüfen, ob `APIShared.DependencyBootstrap` Warnungen zum externen Plugin-Pfad meldet. Optional eine Partie starten: `MAP_START` und `LUA_INIT`. |
| D ohne Resolver | wie C mit `-Resolver off` | `RESOLVE would resolve ...` und `PAYLOAD_HELPER_FAILED` belegen, dass BepInEx Hilfs-DLLs außerhalb von `plugins` nicht selbst findet (P2). |
| E doppelt | `Set-LocalPayloadCopy -State Present`, dann wie C (`-Duplicates skip`), danach mit `-Duplicates bepinex` | skip: `DECISION simulated skip-local [WorkshopLoadProbePayload_Serp]`, der Payload kommt aus `plugins`. bepinex: Welche Kopie wählt BepInEx (Zeile `Skipping [...]`), welche das Extender-Asset-System, und woher wird die Hilfs-DLL aufgelöst? Danach `Set-LocalPayloadCopy -State Absent`. |

T4, Kompatibilitätsprüfung: Laut Quelle (`GameAssetModManager.PrepareAllAndCheckCompatibility`)
kennt die Startprüfung des Extenders nur Mods unter `plugins`. Eine lokale Abhängigkeit auf
einen reinen Workshop-Mod würde daher als fehlend gemeldet, mit Dialog. Ein Lauf dazu ist
nur bei Zweifel nötig, weil er einen Klick im Dialog verlangt.

## Steam-Läufe (T2, T3) – braucht den Nutzer

Ein Workshop-Upload ist eine Veröffentlichung und wird nur vom Nutzer ausgelöst.

1. Mit `APIShared/.local/WorkshopUploader/UploadMod.bat` den Ordner `source` von
   Variante 1 als **privates** neues Item hochladen und abonnieren.
2. Beim nächsten Spielstart bietet APIShared die Installation an. **Ablehnen**, sonst liegt
   der Payload in `plugins`.
3. `Snapshot-Workshop.ps1 -ItemId <ID> -Label t2-v1` aufnehmen.
4. Fremde Datei: `foreign.txt` in `content/3024040/<ID>/` anlegen (nur bei diesem Test-Item),
   dann Snapshot.
5. `New-TestItem.ps1 -Variant 2` erzeugen und deren `source` als Update auf dieselbe ID
   hochladen. Bei geschlossenem Spiel warten, bis Steam aktualisiert hat, dann Snapshot.
   Mit `Compare-WorkshopSnapshots` klären: Ist `only-in-variant-1.txt` gelöscht? Bleibt
   `foreign.txt` erhalten? Wie ändern sich `timeupdated`, `manifest` und `latest_*`?
6. Gesperrte DLL: `Set-ProbeConfig -Mode load -Load <ID>` und Spiel starten. Der Payload wird
   jetzt aus dem Workshop-Ordner geladen und ist gesperrt. Variante 1 erneut als Update
   hochladen. Snapshots während das Spiel läuft (`downloads`, `temp`, `NeedsUpdate`) und nach
   dem Beenden. Danach Spiel neu starten und `INVENTORY` prüfen.
7. Deabonnieren. Mehrere Snapshots (sofort, nach Steam-Neustart, später) zeigen, wann der
   Ordner und die `.acf`-Einträge verschwinden.

## Aufräumen

`tools/Remove-Probe.ps1`. Die Ordner `.work/a/workshopload-item/<n>` nach Abschluss
entfernen. Die Nachweise in `_inspect/WorkshopLoadProbe` bleiben erhalten. Das private
Test-Item löscht der Nutzer auf Steam.

## Stand (2026-11-18)

- Build und Quellprüfungen bestanden.
- Offline mit den gebauten Klassen geprüft:
  - Das Inventar des simulierten Items und des echten Items 3788821961 ist ok. Das echte Item
    hat 936 Dateien und 64 MB, warm in 94 ms gehasht.
  - Die `.acf` wird korrekt gelesen. Felder pro Item: `size`, `timeupdated` und `manifest`
    (installiert) sowie `subscribedby`, `timetouched`, `latest_timeupdated` und
    `latest_manifest` (Details).
- Kein Spielstart bisher.
