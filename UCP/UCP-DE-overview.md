# UCP-Bugfixes: Relevanz für Stronghold Crusader Definitive Edition

Stand: 7. September 2026

## Korrigiertes Gesamtergebnis

Der erneute Abgleich erfolgte gegen den tatsächlichen Quellcode von `BugfixesAndQoL`, die übrigen Workspace-Mods und `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\shcde-fixes-main`. Dadurch ändern sich mehrere frühere Bewertungen wesentlich:

- `ai_defense` ist in `BugfixesAndQoL` als `AiDefensePatrolFix` vollständig umgesetzt und standardmäßig aktiv. Der frühere Einzelbericht wurde deshalb gelöscht.
- `ai_access` ist durch `AIEconomyProtectionHook` funktional abgedeckt; dessen verbesserte Erreichbarkeitsprüfung ist sogar differenzierter als UCPs pauschales Unterdrücken des Abrisses.
- `ai_tethers` ist durch die Script-Extender-Globals `AIMaxOxTethers` und `AIStoneToOxenRatio` im Fixes-Mod konfigurierbar abgedeckt. Für UCPs Ergebnis „höchstens zehn“ genügt `AIMaxOxTethers=10`.
- Der Turmruinen-Teil von `ai_rebuild` steckt bereits in `AITowerRuinRepairFix`; verwandte Überbaukonflikte behandelt `BetterAIOverbuildRulesFix`. Die übrigen `ai_rebuild`-Teilfälle sind noch offen.
- Die Marktplatz-Waffenreihenfolge ist in `BugfixesAndQoL` frei konfigurierbar, einschließlich einer HD-Reihenfolge. Ein separater UCP-Port ist unnötig.
- `hopfarm-limit-fix` bleibt vollständig durch `shcde-fixes` abgedeckt.
- `o_fix_fletcher_bug` ist eine offizielle DE-Advanced-Option; `u_spearmen_run` ist über dieselbe DE-Optionsfamilie plus den Workspace-Patch umgesetzt.
- Der nachgewiesene Grundfix aus `aiv-troops-behaviour` ist inzwischen als `AivDefenderPositionFix` in `BugfixesAndQoL` umgesetzt; offen sind nur dessen optionale Verhaltensmodule.

Kein alter HD-Maschinencode darf in DE übernommen werden. UCP2 patcht 32-Bit-x86; DE verwendet eine andere 64-Bit-Bibliothek und neue verwaltete Oberflächen. Übertragbar sind Verhalten und Testspezifikation, nicht Adressen oder Bytes.

Die vertiefte Baseline-Prüfung hat außerdem die Integrationsplanung verbessert: Absolute Angriffsschritte, Angriffslimit, Ziel-Policy und Rekrutierungsrate besitzen bereits AIC-Felder in DE. Responsive Gates besitzen ein öffentliches Override-Event. Echte Native-Hooks bleiben damit vor allem für pro-Welle nicht abbildbare Formeln und nachweislich reproduzierte Worker-/Zustandsfehler nötig. Die vollständige Matrix steht in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

## Handlungsübersicht

| Gruppe | Ergebnis | Aktion |
| --- | --- | --- |
| Bereits abgedeckt | `ai_defense`, `ai_access`, `ai_tethers`, Hopfenfarm, Fletcher, Marktreihenfolge | keine Doppelimplementierung und keine überlappenden Hooks |
| Teilweise abgedeckt | `ai_rebuild`, Improved Attacks | nur die nachweislich fehlenden Teilpfade ergänzen |
| Wahrscheinliche Testkandidaten | Leiterziel, Bäcker, Gerber, Apfelbauer, Graben-Despawn, Feuerballista-Ziele, Pitch-Bogenschützen, Assault-Switch | zuerst deterministisch reproduzieren, dann eng hooken |
| Eher Balance als Bugfix | unbegrenzte Turm-Belagerungsmaschinen | höchstens als optionale Gameplay-Einstellung |
| HD-spezifisch beziehungsweise in DE ersetzt | Kartenversand-Workaround | nicht portieren, solange kein eigener DE-Fehler belegt ist |

Die vollständige Einzelprüfung aller 17 UCP2-`Bugfix`-Einträge, vier bugfixartigen Legacy-Optionen und zwei UCP3-Fixes steht in [UCP-bugfix-coverage-audit.md](UCP-bugfix-coverage-audit.md).

## Analysebasis

- kanonische installierte `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
- semantische Native-Baseline `FBCB9319`, gebunden an Script Extender 2.2.0, Commit `10d28f717d38166e5875c666f20fc5653ae44b0c`;
- UCP2 `Version.cs`, UCP3-Erweiterungen und die heruntergeladenen Store-Pakete;
- Workspace-Quellcode und separater lokaler `shcde-fixes-main`-Quellcode.

Der installierte DLL-Hash wurde gegen `CURRENT.json` geprüft und stimmt überein. „Offen“ bedeutet dennoch bewusst, dass ein ähnlicher Kontrollfluss allein den Fehler nicht beweist.

## Bugfix-Detailberichte

- [UCP-bugfix-coverage-audit.md](UCP-bugfix-coverage-audit.md)
- [UCP-AI-rebuild-and-access.md](UCP-AI-rebuild-and-access.md)
- [UCP-ladder-climb.md](UCP-ladder-climb.md)
- [UCP-AI-economy-and-siege.md](UCP-AI-economy-and-siege.md)
- [UCP-worker-state-fixes.md](UCP-worker-state-fixes.md)
- [UCP-combat-targeting-fixes.md](UCP-combat-targeting-fixes.md)
- [UCP-small-placement-animation-ui-fixes.md](UCP-small-placement-animation-ui-fixes.md)
- [UCP-system-and-multiplayer-fixes.md](UCP-system-and-multiplayer-fixes.md)
- [UCP-UCP3-fixes.md](UCP-UCP3-fixes.md)
- [UCP-native-integration-audit.md](UCP-native-integration-audit.md)

## Feature-Berichte

- [UCP-features-overview.md](UCP-features-overview.md)
- [UCP-features-AI-attacks-and-recruitment.md](UCP-features-AI-attacks-and-recruitment.md)
- [UCP-features-AI-economy.md](UCP-features-AI-economy.md)
- [UCP-features-units-and-balance.md](UCP-features-units-and-balance.md)
- [UCP-features-interface-and-gameplay.md](UCP-features-interface-and-gameplay.md)
- [UCP-store-feature-audit.md](UCP-store-feature-audit.md)
