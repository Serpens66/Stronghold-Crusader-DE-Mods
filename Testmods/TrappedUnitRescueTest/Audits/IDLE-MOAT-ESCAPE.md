# Neuer Fluchtauftrag aus einem frisch fertiggestellten Graben

Geprüfter Arbeitsstand: 25.11.2026; native DLL SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Diese Notiz beschreibt den Testmod-Vertrag; sie behauptet keinen bestandenen Spieltest.

## Zulässiger Ausgangszustand

Der Mod besitzt keinen bisherigen Arbeits- oder Bewegungsauftrag. Die Einheit steht mit Flags0, Substep8, Current=Next und leerem740/742 auf ihrem alten gewöhnlichen Feld, das inzwischen Moat geworden ist. Ihr bisheriges Requested entspricht Current. Bewegungsklasse, Pause, Portal, Transport, Hidden-/Moat-Marker und Rekrutierungsanforderung schließen die Aufnahme aus, sobald sie besondere Behandlung verlangen. Identität, Tribe-Stempel, AI und die beobachtete Befehlssemantik müssen unverändert sein.

Zugelassen sind ausdrücklich bekannte Befehlstypen mit AI0 sowie der separat geprüfte Spearman-Leerlauf AI2. Tiere, Trebuchet und Engineer0 werden nicht adoptiert. Vorhandene Arbeiterrollen werden nicht in einen Fluchtauftrag umgedeutet. Siege benötigt vollständige unveränderte Crew und den vom Aufrufer bereits erbrachten Aliasnachweis. Der Dig-/Fill-Selektor ist `9F4` (`TribeAICommand.DigMoatTileId`/`Unknown7`); `956` ist ein getrenntes WorkMode-Feld und wird als Semantik beobachtet, aber nicht pauschal anhand derselben Zahlen abgelehnt.

Das Ziel stammt aus der vor der Mutation belegten lokalen Erreichbarkeit. Es unterscheidet sich vom Ausgangsfeld und ist jetzt leerer gewöhnlicher Boden mit physischem Ausgang. Die native Suche erhält dieses Ziel als **neuen eigenen Auftrag**. Ein Aufruf mit dem bisherigen Current-Ziel wäre kein Funktionsnachweis: `196280` liefert dafür1 bei Pfadlänge0.

## Native Belege und Widerlegungen

- `196840` liest Current72C und Terrainbit30; `196280` aktiviert dadurch die besondere Moat-Suche. Ein gewöhnlicher PCL0-Reachability-Test bildet diesen Weg nicht ab.
- `182B00` ruft nach dem Typhandler die Bewegungsroutine `1855A0` auf. Deren Flags2/4-Verbrauch setzt keinen erfundenen AI101-Zustand voraus. Pause8F8, physische Kanten und Sonderzustände können tatsächliches Weiterlaufen trotzdem verhindern. Eine veröffentlichte Route ist deshalb nur `InProgress`.
- `196280` ist keine schreibfreie Probe. Es schreibt Requested744/746,8EC,A4C; je nach Zweig außerdem900/92A und den1000-Byte-Pfadslot. Für Typ0x52 kann ein Suchfehlschlag A7E=100 setzen. Dieser Kontext wird mitbeobachtet; seine Änderung verhindert unsere Versetzung.
- `199CD0` kann AI110 setzen, wenn das aktuelle Feld WALL0x100 trägt. Der hier geprüfte neue Moat-Ausgang schließt WALL ausdrücklich aus. Eine Wandrettung ist durch diesen Nachweis nicht freigegeben.
- Selector1 ist kein neuer unabhängiger Suchlauf: `E32B0` arbeitet mit vorhandenen Such-/Rekonstruktionsdaten. Der Testmod benutzt Selector0.
- `19BA40` löscht Flags74E, DWORD740 und DWORD750=8, also Substep und Cursor, und führt die geprüfte Belegungs-Ummeldung aus. Requested, gespeicherter Moat-Marker und Pfadlänge werden nicht allgemein bereinigt. Das erlaubt keine Übertragung dieses Vertrags auf aktive Arbeiter oder andere Jobs.

Die vollständigen funktionsbezogenen Auszüge lagen bei der Prüfung unter `.work/a/rescue-impl/001/idle-196280.c`, `idle-196840.c`, `idle-182B00.c`, `idle-1855A0.c`, `idle-143400.c`, `idle-181890.c`, `idle-E32B0.c`, `idle-199CD0.c` und `relocate-function.json`; dauerhaft maßgeblich ist die hashgebundene Native-Baseline.

## Original-Grenze und Abschluss

Ein begrenzter verwalteter Receipt wird vor jeder nativen Aktion reserviert. Die gemeinsame Move-API unterscheidet tatsächliches Original0/1 von Veto, Umleitung, Identitätswechsel und Legacy-Post-Ergebnis. Nur exakt bestätigtes Original0 erlaubt einen anschließenden erneuten Zustandsnachweis für lokale Versetzung. Ausnahme, fehlendes Original, Umleitung oder unbewiesene Veröffentlichung erlauben sie nicht.

Der `IdleEscapeSourcePhase`-Observer bindet ausschließlich den eigenen endgültigen Original-Kontext einschließlich Session, Generation, Einheit und Ziel. Bis zu dessen passendem Completion bleibt die vollständige ursprüngliche Pose-/Semantikprüfung aktiv, insbesondere nach den übrigen Begin-Observern. Danach darf die eigene native Routine ihren eigenen angeforderten Zustand verändert haben; unveränderte Autorität und anschließende Zustandsprüfung bleiben erforderlich. Es gibt keine Annahme über eine feste Zahl von Guard-Aufrufen.

Original1 benötigt kohärente positive Route oder belegte tatsächliche Ankunft. Der Owner beobachtet danach mit festem Budget und Deadline; ein neuer Auftrag, AI-/Kontextwechsel oder Identitätsverlust gewinnt. Es gibt weder Wiederholung des alten Auftrags noch AI-Rückschreiben. Original0-Fallback erhält nur bei erneut bestätigtem unverändertem Ausgangszustand eine lokale Versetzung; Originalidentität, Gesundheit, Crew, Source-Belegung und tatsächliche Zielbelegung werden nachgeprüft.

Ein bereits zurückgegebenes Original1 bleibt auch nach einer Ausnahme in fremdem Legacy-Post als möglicher eigener nativer Auftrag erkennbar. Nur kohärente Live-Nachbedingungen gestatten dann die schreibfreie Ankunftsbeobachtung. Dagegen erlaubt Original0 bei einer solchen Ausnahme weiterhin keine zusätzliche Versetzung.

Der Owner besitzt keine Arrival-Barriere und plant nach erfolgreicher Veröffentlichung keine weiteren Schreibaktionen. Receipts dürfen daher bei Laden/Sessionwechsel verworfen werden. Die native Route selbst gehört zum normalen Spielzustand; eine zusätzliche Receipt-Persistenz oder ein Auftrag-Replay ist nicht erforderlich.

## Offline-Nachweis und verbleibender Spieltest

`IdleEscapeFixture` prüft Verhalten mit der produktiven Owner-Klasse sowie unveränderlichen SDK-Kontexten/-Ergebnissen: Reservierung, Reentranz, Kapazität, faire feste Budgets, Tick-Idempotenz und Wraparound-Deadline, Supersession, tatsächliche Ankunft, Native0-/1-Abgrenzung und die genaue Source-Phase. Sie ruft keine native Spielroutine auf. Der isolierte Build verwendet .NET Framework4.8.1 und die tatsächlichen Testmod-/SDK-Quellen.

Die physische Flucht mit allen zugelassenen Einheiten sowie Pausen, Feindkontakt, belegtem Ausgang und CastlePlanner-/Fixes-Kombinationen bleibt im Spiel zu testen. Ein Offline-Erfolg wird dafür nicht als Ersatz gewertet.
