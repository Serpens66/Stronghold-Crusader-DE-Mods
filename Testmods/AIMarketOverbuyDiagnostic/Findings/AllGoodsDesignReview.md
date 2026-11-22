# AI-Markt: Mengenverträge und Diagnose

Stand: 22.11.2026, aktueller implementierter Arbeitsstand. Native Referenz ist `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; Aktualisierungs- und Hookverträge stehen in `../UpdateToNewDLL.md`. Dieses Dokument beschreibt den Codevertrag. Build-, Laufzeit- und Spielabnahmen sind eigene Nachweise und werden nicht aus bestandenen reinen Tests abgeleitet.

## Gemeinsame Grundlage

Vanilla speichert eine Kaufmenge pro Spieler und Gut. Ein späterer Erzeuger kann diese Zelle überschreiben; es gibt hier keine chronologische Liste unabhängiger Bestellungen. Die native Kaufentscheidung verarbeitet den gespeicherten Wert, ohne jeden ursprünglichen Zweck allgemein neu zu berechnen. Zwischenzeitlich gelieferte Waren können deshalb einen alten Kaufwunsch überholen.

Der Testmod beobachtet die tatsächlich ausgeführten nativen Schreibstellen. Er interpretiert keinen unveränderten Snapshot als neue Anforderung. Quellenfamilie, AIC-Identität, erwarteter Zellwert und ursprüngliche Provenienz werden getrennt erfasst. Unbekannte gespeicherte Aufträge, fremde Mengenänderungen und nicht sicher zuordenbare Quellen bleiben unowned; der Mod erfindet dafür keinen Zweck. Ein ausgeführter Store ist ein Mengenbeleg, aber nicht automatisch ein unabhängiges neues Ziel.

Im `/devdiag`-Entwicklungsbuild ist die Korrektur auch in normal gestarteten Partien aktiv; der normale Build bleibt beobachtend. Der Editor ist auf ausdrücklichen Nutzerwunsch ausgeschlossen. Eigene Vergleichsszenarien können die Korrektur zunächst deaktivieren und trotzdem Quellen erfassen. Frühe Unity-Komponenten sind kein dauerhafter Host: statisch verwurzelte Library-/Missions-/Tick-Publisher und permanente native Relays tragen die Runtime. Native Hooks werden nicht zurückgepatcht. Diagnosepuffer, Schreibtempo und Diagnosefehler entscheiden nie über die Gameplay-Aktivierung.

## Mission, Multiplayer und Speicherung

APIShared ist Besitzer des Missionslifecycle, der nativen Simulationsticks und der vier gemeinsamen Markteinstiege. Der Fix registriert seinen Kaufentscheidungs-Callback unabhängig von der optionalen Handelsdiagnose. `BeforeLoad` sperrt die alte Welt und setzt einen neuen Versuch genau einmal zurück. Erst der native Startpunkt unter der Engine-Sperre, die bestätigte rohe Weltwiederherstellung oder ein fertiger Sitzungsstart erlaubt wieder Quellenbeobachtung. Ein späterer Start derselben Sitzung löscht keine bereits wiederhergestellten Gutschriften. Eine nachträgliche Startzustellung ist zulässig. Alle Gameplay-Modi benutzen diesen Pfad; der Editor bleibt gesperrt.

Einzelspieler benötigt keine Peer-Zustimmung. Echter Multiplayer verlangt zusätzlich die öffentliche erforderliche `SimulationAgreement`-Freigabe samt geprüfter Resync-Grenzen. Ein tatsächlicher Korrekturfehler meldet einen gemeinsamen terminalen Weltfehler. Die gemeinsamen ausgeführten Repair-Start-/Endgrenzen sperren die Beobachtung und verwerfen die privaten Zuordnungen auf allen Peers; native Kaufmengen bleiben dabei unverändert. Innerhalb einer Reparatur wird kein Ledger gespeichert.

Neue Spielstände speichern höchstens 184 geordnete Ledger-Zeilen in einem expliziten binären Schema (maximal 16.936 Nutzbytes). Das enthält Mengenverträge und stabile Munitionsidentität, keine Pointer, lokale Sitzungskennungen, Uhren oder Ereignissequenzen. Der öffentliche atomare Save-Vertrag friert diese Metadaten unter derselben Engine-Sperre wie den nativen Stand ein. Legitimer Verlust von Eigentums-/Politikbeweisen wird beim Speichern als Verwerfen dieser privaten Zuordnung behandelt und verhindert kein normales Speichern.

`Prepared` dekodiert ausschließlich private Kandidaten. Nach der rohen nativen Wiederherstellung, vor dem ersten Simulationsfold, werden sie gegen die passende Sitzung, echte Pending-/Bestandsfelder, AIC-/Politikwerte und gegebenenfalls die Einheit geprüft und gemeinsam übernommen. Ein späteres `OnStart` leert sie nicht erneut. Widersprüchliche geladene Datensätze lehnen den Ladevorgang ab. Alte Spielstände ohne diesen Datensatz beginnen ausdrücklich mit leerem Ledger; vorhandene alte Kaufwünsche werden nicht rückwirkend rekonstruiert oder repariert. Neue sicher belegte Quellen werden danach normal erfasst.

## B: Feste Beschaffungspakete

Eine feste Charge verlangt insgesamt ihre ursprünglich angeforderte Warenmenge. Bereits gelieferte Waren bleiben angerechnet, auch wenn sie inzwischen verbraucht wurden. Beispiel: Paket fünf, Lieferung eins, anschließend Verbrauch eins ergibt weiterhin Rest vier. Der zuletzt beobachtete Bestand wird bei positiven und negativen Änderungen aktualisiert; nur positive Zugänge erhöhen die gedeckelte Liefergutschrift. Derselbe Snapshot darf nicht zweimal zählen.

Ein Matching-Refresh aus derselben Quellenfamilie mit gleicher Originalmenge, relevanter Politik und AIC-Identität behält die Gutschrift. Dafür muss unmittelbar vor dem nativen Store auch der eigene erwartete Zellwert noch vorliegen. Vanilla darf dabei nochmals fünf schreiben; der Nach-Callback korrigiert anschließend unmittelbar wieder auf vier. Beobachtetes Löschen, abgeschlossene Beschaffung oder ein Wechsel von Menge, Politik beziehungsweise Quellenfamilie beginnt einen neuen Vertrag. Verschiedene Rekrutierungspfade können dieselbe konfigurierte Waffencharge aktualisieren.

`buy_weapons=5` ist eine Kaufcharge, kein Nachweis für fünf konkrete Soldaten und kein kontinuierliches Lagerziel fünf. Der normale Waffenwunsch wird beim nachgewiesenen Ressourcenmangel erzeugt. Der Mod macht daraus keine regelmäßige Auffüllung eines Bestands von eins bis vier auf fünf.

## M: Materialkosten aus CC420

Die fünf CC420-Stores enthalten aktuelle Defizite. Ihr Kostenbestand wird aus Ausgangsbestand plus tatsächlich geschriebener Menge minus nativem Holzaufschlag abgeleitet. Solange die eigentlichen Kosten fehlen, gilt Kosten minus aktuellem Bestand plus Aufschlag. Sobald die Kosten verfügbar sind, endet dieser Wunsch einschließlich des dann überflüssigen Aufschlags. Ein Verlust vor Erfüllung kann den Bedarf dagegen wieder erhöhen.

Beispiel: Bestand zwölf, Baukosten zwanzig, nativer Holzauftrag zehn einschließlich zwei Aufschlag. Eine Lieferung von acht ermöglicht den Bau; der alte Wunsch wird null. Der spätere Verbrauch aller zwanzig Waren reaktiviert ihn nicht. Während noch nicht erfüllter Materialanforderungen bleibt der größte bereits belegte Kostenbestand erhalten; dominiert der ältere Kostenbestand, bleibt auch dessen ursprüngliche Provenienz erhalten. Ein andersartiger neuer Writer ersetzt den Vertrag, statt fremde Ziele heimlich zusammenzuführen.

Ein unbekannt aufgegebener Bauplan wird nicht automatisch erkannt. Die Resource-Prüfung besitzt keine vollständige Identität und Lebensdauer aller Bauabsichten. Dieser Mengenvertrag ist kein Ersatz für Vanillas gesamte Planverwaltung.

## A: Automatische Munitionsanforderung

D5710 ist keine fraktionierte Beschaffung von zwanzig einzelnen Schüssen: Die Auffüllung verbraucht atomar zehn Stein. Nur die fünf auditierten AUTO-Aufrufstellen und eine anfangs leere, lebende eigene Einheit werden übernommen. Spiel-ID, Global-ID, Erzeugungstick, Eigentümer, Alive-/Death-Zustand und der native vorzeichenbehaftete Ammo-WORD sichern die Identität.

Für dieselbe noch leere AUTO-Einheit ist die Warenmenge `max(0, 10 − aktueller Steinbestand)`. Bei neun Stein fehlt somit einer. Ist die Einheit inzwischen nicht mehr leer, tot, gewechselt oder nachgewiesen ersetzt, endet der alte Auslöser. Ein nicht sicher lesbarer Kontext berechtigt dagegen zu keiner Änderung. Positive Munitionsänderungen werden nicht als additive Liefergutschrift modelliert. Manuelle oder unbekannte D5710-Aufrufer bleiben Vanilla und verwerfen eine davor bestehende Steinprovenienz; ihr möglicher Zweck wird nicht geraten.

## Beobachtungs- und Eingriffspunkte

Alle Quellenrelays werden vorbereitet, validiert und verwurzelt, bevor sie veröffentlicht werden. Bei den 22 nativen Mengen-Stores erfasst ein Vor-Callback den alten Zellwert und die Quelle. Der originale einzelne MOV läuft genau einmal; ein gated Nach-Callback prüft Sitzung und tatsächlich gespeicherte Menge und veröffentlicht gegebenenfalls die Korrektur. Der strukturell validierte Fixes-Zusatz nutzt seinen gesonderten Quellenvertrag.

Abgeschlossene C1CE0-/B97B0-Bestandsrechnungen führen Zugänge und Verluste nach und korrigieren geänderte Wünsche unmittelbar. Der normale Sonderverlust B8122 publiziert anschließend sicher null; sein Vor-Callback verbucht diesen geplanten Nullbestand ausschließlich als Metadaten, statt einen transient negativen Istwert zu verwenden. Die zusätzliche negative Pfadprüfung ist geschlossen: normale gezählte Lagerverluste laufen durch diese Grenzen. Fremde direkte Datenmanipulationen oder abweichende rohe API-Verwendung sind dadurch nicht allgemein zertifiziert.

Vor dem nativen Käufer werden alle eigenen Wünsche nochmals geprüft. Während seines Originals verhindert ein eigener Tiefenzähler, dass dessen bezahlter Warenzugang als vermiedener Kauf gezählt wird. Danach beendet die native Zelllöschung die Zuordnung. Vanilla läuft unabhängig von Diagnosefehlern genau einmal; Fehler deaktivieren logisch und werden begrenzt gemeldet.

Politikstempel enthalten nur relevante Werte: effektive Rekrutierungscharge, eigene Lebensmittelschwelle und -charge, jeweilige Literalquelle oder validierte Fixes-Kanalwerte. Die gewöhnliche LastStand-Abzählung acht bis eins erzeugt kein neues Ziel. Bodyguard-/Wirtschaftsrekrutierung behält ihren AIC-Wert; nur der Kampfpfad verwendet bei positivem LastStand fünf. Ein Wechsel der Lebensmittelcharge eins auf acht verwirft deshalb keinen Waffenrest vier. Die frühere Voll-AIC-Hash-Abhängigkeit ist entfernt.

## Aufwand, Ausgabe und Grenzen der Nachweise

Der Simulationspfad benutzt feste Zahlenfelder, kleine begrenzte Schleifen und direkte geprüfte Objektzugriffe. Es gibt keine laufende Stringformatierung, Enum-Namensumwandlung, Dateizugriffe oder unbeschränkte Ereignishistorie. Ein statisch verwurzelter Hintergrundschreiber verarbeitet nur kopierte numerische Daten. Einzelbeispiele sind begrenzt; alle weiteren Vorgänge erscheinen als Zähler einer kompakten Prozesszusammenfassung etwa alle zehn Sekunden. Sitzungsende fordert auch das verbleibende Teilintervall an.

Die Ausgabe kennzeichnet B mit Paket/Gutschrift, M mit Kosten/Aufschlag und A mit stabiler Einheitenidentität, atomarem Steinbedarf, gelesenem Ammo-WORD und Ergebnis. Vier getrennte Laufzeit-Histogramme messen Quellenbeobachtung, Vor- und Nachbereitung der Kaufentscheidung sowie Bestandsbeobachtung; die Dauer des nativen Originals zählt nicht als Modaufwand. Die reine Formatterprüfung verwendet die tatsächlichen Produktionsfunktionen und konservative Prefix-/Zeilenaufschläge. Der kombinierte Achtstunden-Nachweis stammt aus dem bestehenden Simulationsharness und ersetzt keine Ingame-Performance- oder Multiplayerabnahme.

Die reinen Tests prüfen Liefergutschrift nach Verbrauch, echte Refreshes, Quellen-/Politikwechsel, Materialziele und deren Wachstum, atomare AUTO-Anforderungen, fehlende Provenienz, Zahlenränder und null verwaltete Allokationen nach Aufwärmen. Die ausgeführten nativen Fixtures prüfen MOV-Einmalausführung, Vor-/Nach-Zustand, Trampoline, Register/Flags/XMM und die Caller-/Layout-Verträge. Reale wirtschaftliche Wirkung und tatsächlicher Laufzeitaufwand werden anschließend getrennt im Spiel geprüft.

## Historischer Nachweis

Der frühere A/B-Nachweis unter `.work/a/market/001` bestätigt einen veralteten Ausrüstungskauf nach zwischenzeitlicher Lieferung und einen damaligen eng begrenzten 40740-Testfix. Er ist historische Evidenz; seine damalige Aktivierung nur nach einem Testgeschenk und seine rein monotone Reduktionsformel sind keine Vorgaben des aktuellen Vertrags. Seine Aussage wird nicht nachträglich auf alle heutigen Quellen oder Spielmodi erweitert.
