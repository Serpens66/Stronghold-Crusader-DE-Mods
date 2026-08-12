# Unit Costs

## 1. Mittel: unveränderte Recruitment-Tooltips allokieren in jedem GUI-Frame

### Beleg

- `UnitCosts/src/RecruitmentAvailabilityUiHook.cs:50-61` hängt an `FatControler.NoesisGUIUpdateChecksInGame` und ruft den Refresh in jedem GUI-Update auf.
- `UnitCostsRuntime.RefreshRecruitmentUi` an `UnitCostsRuntime.cs:429-433` aktualisiert dabei auch jedes Mal den aktuellen Tooltip.
- `CreateRecruitmentCostEntries` an Zeile 435-459 erzeugt eine neue Liste, neue Entry-Objekte, formatierte Strings sowie Icon-/Ressourcenabfragen.
- `UnitRecruitmentCostTooltipViewModel.SetCosts` an `UnitRecruitmentCostTooltipViewModel.cs:33-46` kopiert die gerade erzeugte Liste nochmals in eine zweite `List`, bevor es feststellt, dass sich nichts geändert hat.

### Auswirkung

Solange ein Rekrutierungs-Tooltip aktiv ist, entstehen selbst bei unveränderten Kosten und Ressourcen pro GUI-Frame mehrere Listen, Entry-Objekte und Strings. Der späte `CostsMatch`-Vergleich verhindert nur ObservableCollection-Änderungen, nicht die vorausgehenden Allokationen.

### Fixvorschlag

1. Vor `CreateRecruitmentCostEntries` eine kompakte Signatur vergleichen: lokaler Spieler, Einheitentyp, Multiplikator, konfigurierte Kosten und aktuell verfügbare Ressourcen der tatsächlich verwendeten Güter.
2. Bei unveränderter Signatur sofort zurückkehren, bevor Entries, Icons und Strings erzeugt werden.
3. Dirty-Flags aus Hoverwechsel, Settingsänderung, Rekrutierungsmenge und Ressourcenereignissen verwenden. Falls Ressourcenereignisse nicht vollständig genug sind, einen niedrig frequentierten Poll als Sicherheitsnetz behalten.
4. `SetCosts` eine bereits materialisierte `IReadOnlyList` übergeben oder direkt primitive Werte vergleichen, damit keine zweite Liste entsteht.
5. Erst nach Messung entscheiden, ob auch die Button-Verfügbarkeitsprüfung gedrosselt werden muss; der bestätigte Hotspot ist der Tooltip-Aufbau.

### Abnahme

- Ein unveränderter, sichtbarer Tooltip erzeugt nach dem ersten Aufbau keine Entry-/Listen-/String-Allokationen pro Frame.
- Ressourcenänderung, Hoverwechsel, Recruit-Mengenänderung und Settingsänderung aktualisieren den Tooltip ohne sichtbare Verzögerung.
- Rekrutierungsblockierung und zusätzliche Kosten funktionieren für Human/AI und alle Güterslots unverändert.

## 2. Niedrig: redundante `CostsVisibility`-Benachrichtigung

`HasCosts` löst an `UnitRecruitmentCostTooltipViewModel.cs:20-28` bereits `PropertyChanged` für `CostsVisibility` aus. `SetCosts` meldet dieselbe Property an Zeile 45 noch einmal. Die letzte Meldung entfernen; wenn sich `HasCosts` nicht ändert, ändert sich auch die Visibility nicht.

## 3. Niedrig: widersprüchlicher Plugin-Lifecycle

`UnitCostsPlugin.cs:34-53` verwendet das gleiche frühe `OnDestroy`-/späte `OnApplicationQuit`-Muster wie BuildingCosts, BuildingLimit und StartConditions. Auf einmalige prozessweite Initialisierung umstellen und keine benötigte Library-Subscription im frühen `OnDestroy` lösen.
