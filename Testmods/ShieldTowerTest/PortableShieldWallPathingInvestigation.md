# Untersuchung: Tragbare Schilde auf Mauern und Türmen

## Warnung und Herkunft

`ShieldTowerTest` ist ein unfertiger Forschungsmod und kann das Spiel nativ zum Absturz bringen. Er darf nicht als fertiger Fix oder für normale Multiplayer-Partien verwendet werden.

- Neuester rekonstruierbarer Quellstand: ExtraFeatures-Commit `3ec65b999d58bb92c60d68cd6ae9e62beabdf6a9` vom 25. August 2026.
- Entfernung und Sicherung der Erkenntnisse: Commit `272918ae2e5b9828fb52d2fcdea670c8bbc15ef5` vom 25. August 2026.
- Untersuchte und weiterhin kanonische `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Tragbarer Schild: `CHIMP_TYPE_PORTABLE_SHIELD`, Einheitentyp 60.

Der wiederhergestellte Code enthält den Tabellenpatch und den Detour von `canAUnitClimb` aus dem letzten erreichbaren Commit. Der später im ursprünglichen Bericht beschriebene Context-Hook am Bewegungsbefehl-Einstieg RVA `0x195E30` ist in keinem erreichbaren Commit enthalten und wurde nicht nachgebaut.

## Gesicherte Erkenntnisse

1. `setDestinationForUnit` liest einen typabhängigen Wert aus der während der Untersuchung als `DAT_UNIT_CLIMB` bezeichneten Tabelle.
2. Für Einheitentyp 60 ist dieser Wert in DE `0`, in HD und für gewöhnliche mauerfähige Einheiten dagegen `1`.
3. Der reversible Tabellenpatch `0 -> 1 -> 0` funktionierte technisch, reichte aber nicht aus, um einen gültigen Cursor oder Bewegungsbefehl für Mauerziele zu erhalten.
4. Das zweite Merkmal `DAT_ABLE_TO_CLIMB_TOWERS` und das Instanzfeld `GameUnit + 0x9B8` sind für Schilde auch in HD `0`.
5. `canAUnitClimb` bei RVA `0x18DC40` liefert für einen HD-Schild regulär `false`; ein Override entspricht daher nicht nachgewiesenem HD-Verhalten.
6. Die allgemeine Bereichsprüfung bei RVA `0xE2610` akzeptierte den untersuchten Zielbereich. Die verbleibende Ablehnung liegt später im Cursor-/Aktionspfad oder in einem parallelen Entscheidungszweig.

## Bekannte Probleme und ausgeschlossene Ansätze

- Änderungen einzelner Schildinstanzen sind unnötig: Neu gebaute Schilde lesen die Typdaten bei ihrer nativen Initialisierung.
- Das Setzen der zweiten Klettertabelle und des Instanzfelds auf `1` blieb wirkungslos und ist HD-fremd.
- Der `canAUnitClimb`-Override beseitigte die Cursor-Ablehnung nicht.
- Ein beobachtender Hook zeigte, dass die allgemeine Bereichsprüfung nicht der ablehnende Schritt ist.
- Der nicht mehr rekonstruierbare Context-Hook am Einstieg `0x195E30` wurde für jeden Bewegungsbefehl ausgeführt und führte danach bei beliebigen Einheiten zu nativen Abstürzen. Wahrscheinliche Ursache war eine fehlerhafte Windows-x64-Stackausrichtung vor dem verwalteten Callback.
- Der historische Commit iterierte `GetUnitsAsSpan()` fälschlich mit einer als `unitId` bezeichneten 0-basierten Variable ab Index 1. Die Wiederherstellung verwendet den bestätigten Vertrag `unitId = spanIndex + 1` und schließt Index 0 ein.

## Sinnvolle Fortsetzung

- Keinen Context-Hook direkt am Einstieg von RVA `0x195E30` installieren.
- Diagnosepunkte erst hinter einem validierten Prolog und mit nachgewiesener 16-Byte-Stackausrichtung verwenden.
- Die Cursorentscheidung ausgehend von einer gewöhnlichen mauerfähigen Einheit rückwärts verfolgen.
- Erst den konkret nachgewiesenen ablehnenden Zweig verändern.
- Keine zweite Klettertabelle, Instanzfelder oder allgemeinen Kletterergebnisse pauschal überschreiben.

Bis der konkrete DE-Unterschied nachgewiesen ist, existiert kein sicherer oder funktionsfähig bestätigter Fix.
