# Untersuchung: Tragbare Schilde auf Mauern und Türmen

## Status

Die experimentelle ExtraFeatures-Option wurde entfernt. Keine der untersuchten Änderungen stellte das frühere HD-Verhalten in der Definitive Edition vollständig wieder her. Die zuletzt ergänzte Bewegungsbefehl-Diagnose verursachte außerdem einen nativen Absturz bei Bewegungsbefehlen für beliebige Einheiten.

Diese Datei bewahrt die gewonnenen Erkenntnisse für eine mögliche spätere, getrennte Untersuchung. Sie beschreibt keinen aktiven Modcode.

## Referenz

- Untersuchte DE-Bibliothek: installierte `CrusaderDE.dll`
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Tragbarer Schild: `CHIMP_TYPE_PORTABLE_SHIELD`, Einheitentyp 60
- Als Verhaltensreferenz diente zusätzlich die installierte HD-Version.

## Gesicherte Erkenntnisse

1. `setDestinationForUnit` liest einen typabhängigen Wert aus der Tabelle, die während der Untersuchung als `DAT_UNIT_CLIMB` bezeichnet wurde.
2. Für Einheitentyp 60 beträgt dieser direkte Zielberechtigungswert in der untersuchten DE-Version `0`, in HD dagegen `1`. Normale mauerfähige Einheiten verwenden ebenfalls `1`.
3. Ein reversibler Tabellenpatch `0 -> 1 -> 0` funktionierte technisch und war schon aktiv, bevor ein Engineer einen Schild fertigstellte. Neu gebaute Schilde benötigten daher keinen nachträglichen Instanzpatch.
4. Das zweite untersuchte typabhängige Klettermerkmal (`DAT_ABLE_TO_CLIMB_TOWERS`) hat für Schilde sowohl in HD als auch in DE den Wert `0`.
5. Das dazugehörige Instanzfeld bei `GameUnit + 0x9B8` bleibt auch im HD-Verhalten `0`.
6. `canAUnitClimb` bei DE-RVA `0x18DC40` liefert für einen HD-Schild regulär `false`. Dieses Ergebnis pauschal zu überschreiben entspricht daher nicht HD.
7. Die allgemeine Bereichsprüfung bei DE-RVA `0xE2610` lehnte den getesteten Übergang nicht ab. Im Log wurde der Zielbereich 361 als Ergebnis zurückgegeben (`vanillaResult=361`, `toArea=361`). `permitClimb=0` war dabei mit dem HD-Verhalten vereinbar.
8. Trotz des korrekten direkten Tabellenwerts blieb der Mauszeiger über Mauer-, Torhaus- und Turmzielen ungültig; ein Bewegungsbefehl konnte deshalb nicht erteilt werden. Die verbleibende Sperre liegt später im Cursor-/Aktionspfad oder in einem parallelen Entscheidungszweig.

## Getestete und ausgeschlossene Ansätze

### Nachträgliche Änderung einzelner Schildinstanzen

Eine frühe Diagnose nahm fälschlich an, Schildinstanzen müssten nach der Kartenerzeugung aktualisiert werden. Tatsächlich werden sie vom Spieler durch Engineers gebaut und lesen die Typendaten bei ihrer normalen nativen Initialisierung. Instanzscans, Cache-Refreshes und nachträgliche Änderungen an `GameUnit` sind unnötig und wurden entfernt.

### Zweite Klettertabelle und Instanzfeld

Der zweite Tabellenwert sowie das Instanzfeld `+0x9B8` wurden versuchsweise auf `1` gesetzt. Dadurch konnte `canAUnitClimb` positiv erscheinen, der Cursor blieb jedoch gesperrt. Da HD an beiden Stellen `0` verwendet, war dieser Ansatz wirkungslos und HD-fremd.

### Override von `canAUnitClimb`

Ein eng gedachter Override für reine eigene Schildauswahlen beseitigte die Cursor-Ablehnung nicht. Weil HD für Schilde ebenfalls `false` zurückgibt, wurde der Hook wieder entfernt.

### Diagnose der allgemeinen Bereichsprüfung

Ein rein beobachtender Hook zeigte, dass die Prüfung bei RVA `0xE2610` den Zielbereich erfolgreich zurückgab. Sie ist daher nicht der nachgewiesene Ablehnungsschritt und sollte nicht überschrieben werden.

### Cursor- und Bewegungsbefehl-Diagnose

Untersucht wurden zwei Cursorpfade um RVA `0x8D6F0` und `0x8F1A0`, ein Aktionszustandspfad um `0x8D87E` sowie der Bewegungsbefehl-Einstieg bei `0x195E30`. Die Hooks sollten nur beobachten und keine Register oder Vanilla-Ergebnisse verändern.

Der Context-Hook direkt am Funktionseinstieg `0x195E30` lief jedoch für jeden Bewegungsbefehl, noch bevor eine Schildauswahl im verwalteten Callback herausgefiltert werden konnte. Danach stürzte das Spiel beim Bewegen jeder beliebigen Einheit nativ ab. Im BepInEx-Log erschien keine verwaltete Exception.

Die wahrscheinlichste technische Ursache ist die Stack-Ausrichtung: Am Windows-x64-Funktionseinstieg ist `RSP` gegenüber einer Aufrufstelle um acht Bytes versetzt. Der verwendete `ContextAssemblyGenerator` reserviert 144 Bytes Kontext und 32 Bytes Shadow Space, gleicht diese acht Bytes vor seinem verwalteten Callback aber nicht aus. Derselbe Hookmechanismus kann an passenden Stellen innerhalb bereits eingerichteter Funktionsframes funktionieren, ist an diesem Prolog jedoch nicht sicher. Der Absturz korreliert exakt mit dem global aufgerufenen Bewegungsbefehl-Hook; der Typ-60-Tabellenpatch allein kann keinen Absturz bei allen Einheitentypen erklären.

## Ergebnis und mögliche spätere Fortsetzung

Der einzige klar belegte HD-Unterschied ist weiterhin der direkte, von `setDestinationForUnit` gelesene Typwert `1`. Dieser Wert allein reicht in DE nicht aus, um Cursor und Befehl für Mauerziele freizugeben.

Eine spätere Untersuchung sollte außerhalb des normalen ExtraFeatures-Mods erfolgen und folgende Grenzen einhalten:

- keinen Context-Hook direkt am Einstieg von RVA `0x195E30` verwenden;
- Diagnosepunkte erst nach einem validierten Prolog und nur bei nachgewiesener 16-Byte-Stackausrichtung setzen;
- Cursorentscheidung vom gültigen Referenzfall einer gewöhnlichen mauerfähigen Einheit rückwärts verfolgen;
- erst den konkret nachgewiesenen ablehnenden Zweig verändern;
- keine zweite Klettertabelle, kein Instanzfeld und kein `canAUnitClimb`-Ergebnis pauschal überschreiben;
- keine Schildinstanzen nach ihrer Erzeugung manipulieren.

Bis dieser konkrete DE-Unterschied nachgewiesen ist, existiert kein als sicher oder funktionsfähig bestätigter Fix.
