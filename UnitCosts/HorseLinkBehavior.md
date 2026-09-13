# UnitCosts: Horse-Link-Vertrag

## Referenzstand

Diese Notiz beschreibt den untersuchten Vertrag für folgende Versionen:

- `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender: `2.6.0`, Commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`

Native RVAs und Verhaltensaussagen müssen nach einem Spiel- oder Script-Extender-Update erneut gegen die kanonische installierte DLL und die aktuelle Native-Baseline geprüft werden.

## Von UnitCosts angelegter Horse-Link

Wenn eine menschliche Einheit ein Pferd als Zusatzkosten benötigt, sucht UnitCosts beim erfolgreichen Rekrutierungsübergang einen verwendbaren Stall. Die Ställe werden nach ihrer 1-basierten Building-Game-ID sortiert; innerhalb eines Stalls wird der erste freie Slot in der Reihenfolge `0..3` gewählt. Die verfügbare Menge ist auf das Minimum aus `r_TotalHorses - r_UsedHorses` und der Zahl vollständig freier Slotpaare begrenzt.

UnitCosts ruft anschließend

```csharp
GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
    stableId,
    slot,
    unitId,
    unitGlobalId,
    bidirectional: true);
```

auf. Dadurch werden im Stall die Unit-ID und Unit-Global-ID des Slots sowie in der Einheit `r_LinkedStableBuildingId` und `r_LinkedStableGlobalId` gesetzt. Der Script Extender verändert dabei weder `r_TotalHorses` noch `r_UsedHorses`. Vanillas regelmäßiges Stall-Update zählt die gültigen Slotpaare anschließend neu.

Für alle beteiligten IDs gilt:

- `unitId` und `stableId` sind 1-basierte Game-IDs.
- Der Stallslot ist ein 0-basierter Index im Bereich `0..3`.
- Ein Slot ist nur dann frei, wenn sowohl seine Unit-ID als auch seine Unit-Global-ID `0` sind.
- Ein bestehender Link ist nur dann gültig, wenn Unit-ID und Global-ID gemeinsam zur aktuellen Einheit passen.

## Lebenszyklus

### Rekrutierung

Der Link wird im `Pre`-Ereignis des erfolgreichen Rekrutierungsübergangs gesetzt, bevor die übrigen Zusatzkosten abgezogen werden. Schlägt das Abbuchen danach fehl, löst der Rollback ausschließlich den gerade angelegten Link. Ein bereits bestehender oder fremder Link darf nicht verändert werden.

### Tod und endgültiges Löschen

Vanillas direkte Stallfreigabe bei RVA `0xC4110` wird für UnitCosts-verknüpfte Nicht-Ritter beim Tod nicht unmittelbar aufgerufen. Das Pferd wird dennoch über den nachgelagerten Identitäts- und Recount-Pfad verfügbar:

1. Die tote Einheit erreicht den terminalen `AliveState == 3`.
2. Der Unit-Manager ruft den Low-Level-Delete bei RVA `0x1869C0` auf.
3. Der Unit-Datensatz wird geleert; damit verschwindet auch die bisherige Global-ID.
4. Das regelmäßige Stall-Update bei RVA `0xA49E0` ruft den Recount bei RVA `0xC8D90` auf.
5. `0xC8D90` erkennt das gespeicherte Paar aus Unit-ID und alter Global-ID als ungültig, löscht den Slot und berechnet `r_UsedHorses` neu.

Die Freigabe beim Tod ist deshalb erwartetes Vanilla-Verhalten, erfolgt aber indirekt und gegebenenfalls erst beim nächsten Stall-Recount.

### Disband

Beim Auflösen wird die Einheit transformiert und nicht gelöscht. Unit-ID und Global-ID bleiben erhalten, weshalb RVA `0xC8D90` den Stallslot weiterhin als gültig betrachtet.

Der Disband-/Entfernungsweg bei RVA `0x186D10` ruft die direkte Vanilla-Stallfreigabe bei RVA `0xC4110` nur auf, wenn der bisherige Unit-Typ `0x1C` (`CHIMP_TYPE_KNIGHT`) ist. Für UnitCosts-verknüpfte Nicht-Ritter ergänzt der Mod deshalb eine eigene, streng validierte Freigabe.

Der Script Extender löst sein `UnitTransitionSource.Disband`-Ereignis bei RVA `0x186E7A` aus: nach Vanillas ritterbeschränkter Freigabe, aber bevor die weitere Transformation die benötigten Unit-Felder verändert. UnitCosts hält diese Ereignisregistrierung unabhängig vom aktuellen Aktivierungszustand der Kostenfunktion am Leben. Dadurch können auch Links aus einer laufenden Partie oder einem geladenen Savegame bereinigt werden, nachdem der Mod deaktiviert wurde.

Beim `Pre`-Ereignis validiert UnitCosts die 1-basierte Unit-ID, Unit-Global-ID, Owner-ID, Stall-ID, Stall-Global-ID und alle vier 0-basierten Slots. Nur wenn genau ein Slot vollständig zur Einheit und ihrem Backlink passt, wird das verknüpfte Pferd verbraucht: UnitCosts reduziert `r_TotalHorses` genau einmal und löst danach den Slot über `UnlinkStablesUnitIdLink(..., bidirectional: true)`.

Bei einem Gruppen-Disband werden die Übergänge synchron nacheinander verarbeitet, ohne dass Vanilla dazwischen zwingend einen Stall-Recount ausführt. `r_UsedHorses` darf deshalb vorübergehend größer als `r_TotalHorses` sein, beispielsweise `Total=3`, `Used=4` und drei vollständig belegte Slots nach dem ersten Disband aus einem vollen Stall. Maßgeblich ist in diesem Zeitraum die tatsächliche Slotstruktur. Jeder der vier Slots muss entweder vollständig frei oder mit Unit-ID und Unit-Global-ID vollständig belegt sein; halb belegte Slots werden abgelehnt. Außerdem darf die Zahl vollständig belegter Slots `r_TotalHorses` nicht überschreiten. Da ein erfolgreicher Disband sowohl den Gesamtbestand als auch die Zahl belegter Slots genau um eins reduziert, bleibt diese Invariante über die gesamte Disband-Serie erhalten.

Ein reines Unlink würde nur die Belegung entfernen. Nach Vanillas Recount wäre dasselbe Pferd wegen des unveränderten `r_TotalHorses` sofort wieder verfügbar. Die vorherige Reduzierung des Gesamtbestands sorgt stattdessen dafür, dass der Stall ein Ersatzpferd über seinen normalen Produktionspfad erzeugen muss.

Das regelmäßige Stall-Update bei RVA `0xA49E0` ruft zuerst den Recount bei RVA `0xC8D90` auf. Wenn `r_TotalHorses` kleiner als vier ist, erhöht Vanilla anschließend `r_HorseRechargeTimer`; nach Überschreiten von `0x226` wird `r_TotalHorses` um eins erhöht und der Timer auf null gesetzt. UnitCosts verändert weder `r_UsedHorses` noch `r_HorseRechargeTimer`. Ein bereits fast abgeschlossener Vanilla-Timer darf deshalb kurz nach dem Disband natürlich ein neues Pferd erzeugen.

Der Verbrauch wird als zusammengehöriger Übergang geprüft. Sind Ausgangszähler ungültig oder ist die Zuordnung fehlend, widersprüchlich oder doppelt, bleibt alles fail-closed unverändert. Scheitert das Unlink oder seine Nachprüfung, stellt der Rollback ausschließlich den gerade reduzierten Gesamtbestand und den gerade gelösten Link wieder her, sofern alle Identitäten und Vanilla-Zähler noch exakt zum erfassten Zustand passen.

### Savegames

Das aktuelle Saveformat ist `0xEC`. Beim Laden reicht RVA `0x71670` die geladene und die aktuelle Saveversion an RVA `0xC8380` weiter. Sind beide Versionen gleich, wird die dortige Migrationskette übersprungen. Die gespeicherten Stall-Slotfelder, `r_UsedHorses` und die Identität der lebenden Einheit bleiben dadurch erhalten; der Link gilt nach dem Laden weiterhin als belegt.

RVA `0xCA480` ist kein allgemeiner Lade-Recount. Die Funktion gehört zur Migration alter Saveformate, wird nur bei unterschiedlichen Saveversionen und für geladene Versionen vor `0xA7` erreicht und sucht beim Wiederaufbau nach Rittern des Typs `0x1C`. Daraus darf nicht abgeleitet werden, dass aktuelle UnitCosts-Links für Nicht-Ritter beim Laden verloren gehen. Das Verhalten historischer Saveformate vor `0xA7` ist kein abgesicherter UnitCosts-Vertrag.

Nach dem Laden greift beim späteren Tod derselbe Delete- und Recount-Pfad wie in einer nicht geladenen Partie.

## Sicherheitsregeln für Änderungen

- Vor Lesen, Freigeben oder Rollback stets Building-ID und Building-Global-ID sowie Unit-ID und Unit-Global-ID prüfen.
- Niemals anhand des Zahlenbereichs raten, ob ein Wert Game-ID oder Arrayindex ist. Die Umrechnung erfolgt genau einmal an der jeweiligen API-Grenze.
- `r_TotalHorses` darf ausschließlich beim vollständig validierten Disband-Verbrauch genau einmal reduziert und beim zugehörigen sicheren Rollback wiederhergestellt werden. Rekrutierung, Tod und Recount verändern den Wert modseitig nicht.
- `r_UsedHorses` nicht parallel zur Slotbelegung manuell fortschreiben; Vanillas Recount bei `0xC8D90` bleibt maßgeblich.
- Einen vorübergehend höheren Wert von `r_UsedHorses` während eines Gruppen-Disbands nicht als Beschädigung behandeln. Stattdessen vollständige Slotpaare zählen, halb belegte Slots ablehnen und `belegte Slots <= r_TotalHorses` verlangen.
- `r_HorseRechargeTimer` niemals modseitig verändern oder zurücksetzen; die natürliche Stallproduktion setzt den vorhandenen Timer fort.
- `GameBuildingManagerAPI.GetStablesUnitIdLink` nicht verwenden: Der Getter liest die vier `ushort`-Unit-ID-Felder fälschlich über einen `int*`. Bis zur Korrektur im Script Extender die vier Felder explizit und nur nach einer Slotbereichsprüfung lesen.
- Einen Link nur lösen, wenn Stall, Slot, Unit-ID und Global-IDs genau zur erwarteten Zuordnung passen.
- Ein Rollback darf ausschließlich die noch nicht veröffentlichte beziehungsweise gerade fehlgeschlagene eigene Belegung zurücknehmen.
- Native RVAs nicht als stabile API behandeln und ausschließlich nach erneutem Hashabgleich verwenden.

## Erforderliche Regressionstests

- Rekrutierung mit Pferdekosten belegt genau einen freien Slot und verhindert Doppelbelegung.
- Fehlgeschlagene Rekrutierung hinterlässt weder Stallslot noch Unit-Backlink.
- Tod durch Nahkampf und Projektil macht das Pferd nach dem Low-Level-Delete und Stall-Recount wieder verfügbar.
- Disband eines Nicht-Ritters gibt genau den vollständig validierten Link frei und reduziert `r_TotalHorses`; dasselbe Pferd ist nach dem Recount nicht sofort wieder verfügbar.
- Das gemeinsame Disbanden von bis zu vier UnitCosts-verknüpften Nicht-Rittern desselben vollen Stalls löst alle Links und reduziert `r_TotalHorses` für jede Einheit genau einmal, obwohl `r_UsedHorses` bis zum Recount veraltet bleibt.
- Der Stall erzeugt das Ersatzpferd später über Vanillas unveränderten Recharge-Timer.
- Save/Load während der Wiederaufladung erhält den reduzierten Bestand und den Vanilla-Timer.
- Disband eines Ritters verursacht nach Vanillas eigener Freigabe keine zweite modseitige Freigabe.
- Fehlende, widersprüchliche oder doppelte Links bleiben beim Disband unverändert.
- Speichern und Laden eines aktuellen `0xEC`-Savegames erhält Stallslot, Unit-/Global-ID-Paar, Backlink und Belegungsanzeige.
- Tod nach dem Laden gibt das Pferd über Delete und Recount wieder frei.
- Mehrere Ställe und Slots bleiben deterministisch sortiert und verwenden keine bereits teilweise oder vollständig belegten Slotpaare.
