# Themenspezifische Agentenanweisungen

Ergänzungen zur Root-[`AGENTS.md`](../AGENTS.md). Diese Dateien werden nicht automatisch geladen; die Root-Datei legt in ihrer Routing-Tabelle fest, wann welche Datei Pflichtlektüre ist. Die Dateien gelten für alle Coding-Agenten (z. B. ChatGPT Codex, Claude Code) und verwenden deshalb nur normale Markdown-Links, keine toolspezifischen Importe.

| Datei | Inhalt |
|---|---|
| [native-analysis.md](native-analysis.md) | Kanonische DLL, Native-Baseline und `query.ps1`, verpflichtender Vanilla-Audit, ergebnisoffene Diagnosen, Rizin/Cutter, RVA/Pattern/`UpdateToNewDLL.md`, Diagnose- und Zustandsregeln |
| [native-hooks.md](native-hooks.md) | Permanente Hooklebensdauer, Überschreibspannen und eingehende Kanten, In-place-Patches, Funktionsziel vs. Call-Site, RedBird-Verträge |
| [runtime-lifecycle.md](runtime-lifecycle.md) | SHCDE-Startup-Cleanup, Verwurzelung, Dispatcher, Threads→UI, MainViewModel, JSON, publicized Assembly, Prechecks vor Runtime-Builds |
| [script-extender.md](script-extender.md) | Kanonischer Fork, Updates, bekannte Extender-Fehler, API-Verträge (XAML-Patches, Pathing, AIV, Gebäudeerzeugung u. a.) |
| [ids-enums.md](ids-enums.md) | 0-/1-basierte Unit-/Building-IDs, Tribe-Leader, Enums und native Konstanten |
| [modsettings-ui.md](modsettings-ui.md) | Presetsystem, Modsettings-Layout, Suche, Tooltips, Ingame-Popups, Lokalisierung, Pflichttests |
| [multiplayer-network.md](multiplayer-network.md) | Paket-Threadvertrag, MessagePack, Chores, PerPlayer-Sync, Autorität, Spielmodus-Freigaben, MP-Tests |
| [mission-lifecycle.md](mission-lifecycle.md) | APIShared-Missionslifecycle, OnTick- und Timeline-Einschränkungen, TimerEngine |
| [apishared.md](apishared.md) | Shared-/APIShared-Zuständigkeit, Nutzung und Erweiterung, Breaking Changes, Repo-Workflow, Doku, Mod-Abhängigkeiten |
| [external-components.md](external-components.md) | Umgang mit Fehlern in Script Extender und Fixes-Mod, Fixes-Kompatibilität |
| [build-release.md](build-release.md) | Build-Ablauf, CRLF, Hashprüfungen, Versionierung, Steam-Modpack |
| [shell-windows.md](shell-windows.md) | PowerShell-/CMD-Fallen, Treiberaufrufe, Prüfpfad-Tests, Sandbox |
| [performance.md](performance.md) | Mess- und Profiling-Werkzeuge, Messverfahren, Sampler-Auswertung, typische Kostenfallen in heißen Pfaden, Fallen der Messwerkzeuge |

## Pflege

- Neue dauerhafte Regeln in die thematisch passende Datei schreiben; die Root-`AGENTS.md` nur für immer gültige Regeln, Hard-Gate-Einzeiler und die Routing-Tabelle verwenden.
- Eine neue Themendatei immer in die Routing-Tabelle der Root-Datei und in diese Übersicht eintragen.
- Jede Themendatei beginnt mit „**Lesen, wenn:** …“, damit ihre Auslöser eindeutig sind.
- Regeln nicht in mehreren Dateien duplizieren; stattdessen verlinken.
