# UCP-Features: KI-Wirtschaft, Häuser und Abrissregeln

## `ai_housing`: durch vorhandene Mods weitgehend ersetzt

UCP macht Schwellen für freien Wohnraum und untätige Bauern konfigurierbar und verhindert das kontraproduktive Löschen zusätzlicher Häuser.

`shcde-fixes` ersetzt die DE-Hausbauentscheidung über `OnAIShouldBuildHovel` und unterstützt global sowie pro Custom Lord freien Wohnraum, aktuelle/durchschnittliche Untätige und Mindestpopularität. `BugfixesAndQoL` verhindert mit `PreventHovelDeletion` den direkten KI-Hausabriss.

**Bewertung: funktional abgedeckt und flexibler als UCP.** Keine zweite Housing-Policy anlegen. Allenfalls die Einstellungen beider Mods später in einer gemeinsamen Oberfläche dokumentieren.

## `ai_nosleep`: durch `BugfixesAndQoL` abgedeckt

UCP verhindert, dass die KI Produktionsgebäude wegen kurzfristig fehlender Rohstoffe schlafen legt und dabei Prozess-/Lieferzustand verliert. `AIEconomyProtectionHook` patcht die DE-Synchronisation des Schlafzustands; `PreventAIPause` ist standardmäßig aktiv.

**Bewertung: abgedeckt.** Kein zusätzlicher UCP-Hook.

## `ai_demolish`: überwiegend abgedeckt

UCP kann drei Abrissgruppen deaktivieren: Wirtschaftsgebäude in der Notverkaufslogik, als unerreichbar eingestufte Gebäude und Befestigungen.

- Wirtschaft/Notabriss: `BugfixesAndQoL.PreventEmergencyDemolition` überspringt den gezielten DE-Notabrissblock.
- Unerreichbarkeit: `AIEconomyProtectionHook` ist durch seine optionale tatsächliche Erreichbarkeitsprüfung gleichwertig oder besser.
- Häuser: zusätzlich durch `PreventHovelDeletion` geschützt.
- Befestigungen: Eine separate, UCP-identische allgemeine Sperre wurde nicht nachgewiesen.

**Bewertung: für die problematischen Wirtschafts- und Accessibility-Fälle abgedeckt; Fortifikationsschalter offen.** Vor einem Fortifikationsschalter zuerst belegen, ob DE diesen Abrisspfad noch nutzt und ob er nicht für AIV-Reparatur notwendig ist.

## `ai_resources_rebuy`

UCP lässt die KI fehlendes Mehl, Eisen und Holz nach einstellbaren Wartezeiten erneut kaufen. **DE-Relevanz: hoch, falls der alte Deadlock noch besteht; keine Abdeckung gefunden.** Der Fixes-Mod korrigiert zwar die Verkaufskategorie von Weizen, aber nicht diese Nachkauf-Timer.

Die aktuelle Struktur liefert gute Messpunkte, aber noch keinen sicheren Schreibvertrag:

- `r_AISellOrBuyPhase` bei `0x2A6C`;
- Pending-Kaufmengen für Holz `0x2A74`, Eisen `0x2A88` und Mehl `0x2AB0`;
- gleichartig indizierte Sell-Suppression-Timer für Holz `0x4FD4`, Eisen `0x4FE8` und Mehl `0x5010`.

Diese Namen und Offsets stammen aus Script Extender 2.2.0; gemäß Baseline-Regel beweisen sie allein nicht, ob UCPs „Zeit ohne Ressource“ darin gespeichert wird. Zuerst pro Gut einen kontrollierten Mangel erzeugen und Phase, Pending-Menge, Suppression-Timer, Gold und Marktbestand über mehrere Kaufzyklen nur lesen. Danach den nativen Pfad ermitteln, der die erneute Pending-Menge aufbaut.

**Bevorzugte Umsetzung:** den bestätigten Retry-Entscheid beziehungsweise seinen Timer-Reset parametrisieren und Vanilla-Gold-, Markt- und Lagerprüfungen weiter ausführen lassen. Ein eigener `GameTimeManagerAPI.OnTick`-Sidecar darf höchstens den letzten erfolglosen Bedarf und die Wartezeit führen und anschließend denselben Vanilla-Requestpfad auslösen; er darf weder Güter gutschreiben noch blind Pending-Felder setzen. Alle Zeiten in Simulationsticks, Save/Load entweder aus dem Vanilla-Zustand fortsetzen oder mit dokumentierter konservativer Rücksetzung. `NetworkMode=1`.

## `o_freetrader`: über `BuildingCosts` erreichbar

UCP setzt die Kosten des Handelspostens auf null, damit Spieler oder KI ohne Holz nicht dauerhaft feststecken. `BuildingCosts` unterstützt `MAPPER_TRADEPOST` einschließlich Holz-, Stein-, Eisen-, Pech- und Goldkosten.

**Bewertung: bereits konfigurierbar.** Ein Preset mit allen Handelspostenkosten auf null bildet das UCP-Feature ab; kein neuer Mod nötig. Ob auch die KI die modifizierten Kosten in ihrer Bauentscheidung korrekt berücksichtigt, sollte einmal im Spiel bestätigt werden.

Die vollständige Evidenz- und Eingriffspunktmatrix steht in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

