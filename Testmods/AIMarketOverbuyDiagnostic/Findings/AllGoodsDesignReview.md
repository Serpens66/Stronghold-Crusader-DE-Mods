# Erweiterung der Kaufmengenkorrektur: Architekturprüfung

## Spätere Präzisierung im selben Auftrag: Lieferungen bleiben angerechnet

Der Nutzer hat inzwischen ausdrücklich entschieden: Von fünf angeforderten Waren bleibt nach einer Lieferung von einer Ware ein Rest von vier, auch wenn diese eine Ware vor dem Marktkauf verbraucht wird. Die weiter unten beschriebene zwischenzeitliche Variante mit erneutem Mengenanstieg nach Verbrauch ist damit verworfen. Sie ist keine aktuelle Implementierungsvorgabe.

Der neue reine Mengenvertrag führt daher Originalpaket, gutgeschriebene Liefermenge und zuletzt beobachteten Bestand getrennt. Positive beobachtete Zugänge werden dauerhaft auf das Paket angerechnet; negative Änderungen aktualisieren nur den Beobachtungsstand. Eine Quelle mit Bestand zwölf und Paket zehn bleibt nach einer Lieferung von acht und anschließendem Verbrauch aller zwanzig Waren bei Rest zwei. Ein neuer Paketstart ist im reinen Vertrag ein ausdrücklich vom Aufrufer bestätigter neuer Auftrag. Der Vertrag selbst kann nicht beweisen, ob eine native erneute Zuweisung einen neuen Zweck oder nur die Aktualisierung desselben Zwecks darstellt.

Für die Runtime reicht der bisher geschlossene Nachweis aller positiven Bestandswege allein nicht aus: Eine Differenzrechnung muss auch sämtliche dazwischenliegenden negativen Änderungen beobachten. Sonst verdeckt ein nicht erfasster Verlust einen späteren echten Zugang. Diese zusätzliche negative Pfadprüfung und ihre Einbindung sind getrennte Voraussetzungen; der reine Verhaltenstest ersetzt sie nicht.

Der Nutzer hat zusätzlich den tatsächlichen Anlass der Anforderung betont. Daraus folgt keine allgemeine Gleichsetzung zwischen nativer Kaufcharge und Zahl zu rekrutierender Soldaten. `buy_weapons=5` ist eine ausdrücklich konfigurierte Einkaufscharge, kein Nachweis für fünf konkrete Soldaten. Bauanforderungen beruhen dagegen auf Kosten und können einen nativen Holzpuffer enthalten. Ein bereits gebautes Gebäude kann seinen Zweck erreicht haben, obwohl der Puffer noch nicht geliefert wurde. Ob dieser Puffer erhalten bleiben soll, ist eine eigene Quellenregel.

Ein besonders klares Gegenbeispiel für eine universelle Paketregel ist die Munitionsquelle D5710: Bei Steinbestand neun fordert sie eine feste Zehnercharge an. Eine weitere Lieferung von einem Stein ermöglicht die Auffüllung, die zehn Stein verbraucht und der Einheit zwanzig Munition gibt. Ein reiner Zehnerpaket-Ledger ließe danach neun Waren offen, obwohl der konkrete Auffüllvorgang abgeschlossen ist. Um diesen Rest sicher aufzuheben, müsste die Quelle als dieser Auffüllvorgang identifiziert und sein Abschluss nachgewiesen werden; dabei dürfen Anforderungen anderer Einheiten nicht gelöscht werden.

Ebenso beweist ein erneutes natives STORE derselben Menge keinen neuen unabhängigen Zweck. Derselbe Lebensmittel-, Rekrutierungs- oder Munitionspfad kann einen weiter bestehenden Bedarf erneut schreiben. Eine wiederholte Zuweisung darf deshalb nur nach einem zusätzlich festgelegten Quellenvertrag als neuer Paketstart gelten. Die momentan geprüfte reine Paketarithmetik ist ein Baustein; sie ist noch kein Nachweis einer allgemeinen Korrektur aller aktuellen Absichten.

Stand: 22.11.2026. Geprüft wurden der aktuelle Quellstand von AIMarketOverbuyDiagnostic, die verknüpften Verhaltenstests, der Buildtreiber und die bisherigen A/B-Nachweise unter `.work/a/market/001`. Die ergänzenden nativen Material- und Lebensmittel-/Rekrutierungsanalysen wurden im selben Auftrag unabhängig geprüft. Der anschließend ausgewählte reine Mengenvertrag ist in `src/MarketDemandTarget.DevDiag.cs` implementiert. Dieses Dokument belegt keine bereits abgeschlossene allgemeine Runtimeintegration oder Spielabnahme. Installation und Spielzustand wurden bei dieser Teilaufgabe nicht geändert.

## Belegter Ausgangsstand vor der Erweiterung

Der bisherige reine Mengenvertrag lag in `src/StaleEquipmentTarget.DevDiag.cs`; die Einbindung in `src/MarketStaleEquipmentFix.DevDiag.cs`. Er übernahm ausschließlich einen nachgewiesenen normalen Kampfrekrutierungswunsch aus Ausgangsbestand null. Ein einmal erfasstes Auffüllziel blieb erhalten. Vor der nativen Kaufentscheidung wurde die offene Menge höchstens verkleinert, niemals erhöht. Eine eigene Reduktion veränderte nicht das ursprüngliche Ziel. Der alte reine Vertrag und seine Tests wurden inzwischen durch den ausgewählten allgemeinen Restpaketvertrag ersetzt; die Runtimeintegration läuft getrennt davon.

Die Instrumentierung ist derzeit an eine ausdrücklich angeforderte kurze Kausaldiagnose gebunden. `AIMarketOverbuyDiagnosticRuntime.InstallTrace` installiert ohne passende Testanforderung keine nativen Handelsdetours. `EnableAfterGift` aktiviert die Korrektur ausschließlich im Szenario `market-backlog-fixed` nach dem dokumentierten Warenzugang. Der normale Modstart ist weiterhin beobachtend.

Der vorhandene A/B-Nachweis bestätigt einen veralteten Lederkauf nach zwischenzeitlicher Lieferung. Er bestätigt weder einen allgemeinen Abbruch überflüssig gewordener Bauabsichten noch alle Waren- und Erzeugerpfade. Die bisherigen Tests prüfen den tatsächlich verwendeten reinen Mengenvertrag, unter anderem Teilreduktion, spätere weitere Produktion, anschließenden Verbrauch, fremde Mengenänderung, Löschen mit gleichwertiger Neuerzeugung, AIC-/LastStand-Wechsel und Reset.

## Die fachliche Entscheidung

Eine gespeicherte Kaufmenge bedeutet nicht überall „so viel fehlt bis zum aktuellen Mindestbestand“. Lebensmittel- und Rekrutierungserzeuger schreiben feste Kaufchargen. Materialprüfungen können dagegen ein Defizit gegen erforderliche Baukosten schreiben und einen größeren schon offenen Wunsch beibehalten. Deshalb ist `max(0, Pending - aktueller Bestand)` als allgemeine Regel falsch.

Es gibt zwei unterschiedliche Ziele, die auch bei fehlerfreier Implementierung anderes Spielverhalten erzeugen:

| Regel | Beispiel: Waffenwunsch fünf bei Bestand null, später Bestand zwei | Bedeutung |
| --- | --- | --- |
| Ursprüngliches Auffüllziel erhalten | Noch drei kaufen | Bereits anderweitig gelieferte Waren decken einen Teil der ursprünglich angeforderten Charge. |
| Aktuelle Anwendbarkeit des Erzeugers prüfen | Den Wunsch gegebenenfalls ganz löschen | Wenn die aktuelle Rekrutierungsprüfung dieses Gut bei Bestand zwei nicht mehr als fehlend melden würde, entfällt ihr Kaufanlass. |

Entsprechend kann ein Lebensmittelwunsch nach der ersten Regel teilweise erhalten bleiben, obwohl der aktuelle Bestand inzwischen die Kaufschwelle erreicht hat. Die zweite Regel würde ihn dann vollständig verwerfen. Umgekehrt darf die zweite Regel bei weiterhin unterschrittener Kaufschwelle nicht ungeprüft aus einer nativen Fünfercharge ein Defizit von nur einem Stück machen. Die native feste Charge ist eine Einkaufsstrategie; ihr Umfang ist nicht schon deshalb falsch, weil eine kleinere Menge einen einzelnen Bedarf deckt.

Diese Entscheidung betrifft Vorratshaltung und Handelsverhalten. Sie ist keine reine Optimierung desselben Verhaltens. Der vorhandene Testfix folgt der ersten Regel. Im weiteren Auftrag hat der Nutzer die verbleibende Charge gewählt: Von fünf angeforderten und einer inzwischen anderweitig gedeckten Ware bleiben vier. Feste native Vorratschargen sollen damit erhalten bleiben. Dies ist die Grundlage der nächsten Implementierung; ein allgemeiner Abbruch aller heute nicht mehr bestehenden Einzelabsichten ist dadurch nicht beauftragt.

Eine weitere Grenze des alten Vertrags bleibt: Er erlaubt nur Reduktionen. Nach einer Teilreduktion kann zwischenzeitlicher Verbrauch den noch zu deckenden Anteil wieder vergrößern. Eine stets aktuelle Restmengenberechnung muss solche eigenen Mengen wieder anheben dürfen, solange der Auftrag noch positiv offen und seine Zuordnung nachgewiesen ist. Eine bereits vollständig erledigte oder anderweitig gelöschte Zelle bleibt dagegen beendet und darf nur durch einen neuen echten Erzeuger aktiviert werden. Das bisherige monotone Verhalten belegt keinen weiteren Vanilla-Fehler; es ist eine Einschränkung des bisherigen Testfixes.

## Warum eine einzelne Zahl pro Gut nicht alle aktuellen Absichten beschreibt

Die zusätzliche Materialprüfung zeigt mehrere Aufrufer und gleichzeitig mögliche Bauabsichten. Die gemeinsame Ressourcenprüfung kennt Mapper und Spieler, aber keinen eindeutigen Auftrag mit Position oder Lebensdauer. Sie erhöht eine Zelle bei Bedarf auf `max(alter Wunsch, aktuelles Defizit)`. Eine spätere erfolgreiche Einzelprüfung beweist deshalb nicht, dass alle anderen Bauabsichten dasselbe Gut nicht mehr benötigen.

Konkretes Gegenbeispiel für eine naive Ableitung: Ein Wunsch zehn entstand bei Bestand null. Später liegen fünf Waren vor; eine weitere Bauprüfung benötigt sieben und ermittelt Defizit zwei. Die native Max-Regel lässt den Wunsch zehn unverändert. Ein Beobachter darf daraus nicht das neue Ziel fünf plus zehn gleich fünfzehn ableiten. Er muss erforderliche Kosten und bereits bekannte Ziele unabhängig von der beibehaltenen Kaufzelle behandeln.

Beim Save-Laden fehlen außerdem die ursprünglichen Erzeuger und Ausgangsbestände älterer Wünsche. Gleichwertige gespeicherte Mengen können aus verschiedenen Erzeugern stammen. Die vorhandene Sitzungsrücksetzung verhindert eine falsche Übernahme alter Provenienz; sie rekonstruiert verlorene Provenienz nicht. Unbekannte Aufträge unverändert durchzulassen ist konservativ, erfüllt aber nicht den Anspruch, sofort jede bereits gespeicherte falsche Menge zu korrigieren.

Eine vollständige Korrektur aktueller Absichten benötigt zusätzlich erfüllte oder aufgegebene Bau-, Rekrutierungs- und Einheitenabsichten. Ein Einheitenpfad kann seine Waren bereits verbraucht und seinen Zustand geändert haben, während ein älterer Kaufwunsch noch offen ist. Nur aus dem nun wieder niedrigen Lagerbestand kann ein allgemeiner Bestandsdeckel diesen Unterschied nicht erkennen.

## Geeignete Integrationswege

Die gewählte Ausweitung arbeitet mit festen Datenfeldern je Spieler und Gut: erwartete offene Menge, bekanntes absolutes Ziel, ursprünglicher Paketumfang, Ursprung, Sitzungsidentität und erforderliche Politikwerte. Der native Audit hat einzelne tatsächliche Schreibstellen ergeben. Genau deren ausgeführter Store, sein tatsächlicher Mengenoperand und der zugehörige Lagerbestand erneuern das Paket: Ziel gleich Ausgangsbestand plus geschriebene Menge. Eine Max-Verzweigung ohne Store erneuert nichts. Damit wird auch eine echte erneute Zuweisung derselben Menge erfasst, die ein bloßer Vorher-/Nachher-Vergleich übersieht. Nicht ausgeführte oder nur vermutete Stores dürfen keine Zuordnung erzeugen.

Die verbleibende Menge ist der Abstand zum ursprünglichen Ziel, begrenzt auf null bis zur ursprünglich geschriebenen Paketgröße. Dadurch kann ein positiv offenes Paket nach Verbrauch wieder bis zur ursprünglichen Menge wachsen; früher vorhandener, inzwischen verbrauchter Vorrat vergrößert das ursprüngliche Paket nicht. Die native Holz-Zugabe von zwei gehört zum geschriebenen Paket und bleibt erhalten, auch wenn die zwischenzeitlich verfügbare Menge bereits zum Bauen ausreichen würde. Ein Abbruch nur aufgrund aktueller Baubarkeit wäre die ausdrücklich nicht gewählte Absichtsregel.

Eine vollständige Neubewertung aktueller Absichten ist ein größerer Eingriff. Sie braucht entweder eine reine, vollständige Auswertung der aktuell wirksamen Erzeugerzustände oder nachverfolgte Einzelabsichten einschließlich ihrer Erfüllung und Aufgabe. Für Einheiten gehören Besitzer, Spiel-ID, Global-ID und relevante Zustandsphase zur Identität. Bei Bauabsichten reicht der gemeinsame Ressourcenprüfungs-Einstieg allein nicht. Native Erzeuger zur Prüfung nochmals auszuführen ist ungeeignet, weil ihre Seiteneffekte dadurch ebenfalls wiederholt würden.

Beide Wege müssen die funktionale Korrektur von umfangreicher Diagnose trennen. Der derzeitige Korrekturaufruf hängt von einem erfolgreich aufgenommenen Diagnoseframe ab. Eine allgemeine Korrektur braucht eine eigene gültige Aktivierung und Fehlergrenze: Fehler beim Erfassen oder Formatieren dürfen ihre Ausführung nicht verhindern, und Fehler der Korrektur dürfen den genau einmaligen Vanilla-Aufruf nicht verhindern. Dauerhaft veröffentlichte Hooks bleiben installiert und werden logisch aktiviert.

Sitzungsende, Save-Laden, Kartenwechsel und Tickreset müssen alle Ziel-/Absichtsdaten verwerfen. Bei einer allgemeinen Aktivierung darf der derzeitige Reset auf „bis zum nächsten Testgeschenk deaktiviert“ nicht unverändert übernommen werden. Spät zugestellte aktuelle Sitzungsstarts müssen weiterhin funktionieren. Der vorhandene statisch verwurzelte Lifecycle-/Tickpfad und der getrennte Hintergrundschreiber sind dafür geeignete Grundlagen.

## Effizienz und Prüfung

Für eine konservative Mengenabstimmung sind feste numerische Felder ausreichend. Eine Prüfung vor der ohnehin stattfindenden KI-Kaufentscheidung vermeidet einen zusätzlichen vollständigen Tickscan. Im Simulationspfad sind keine Zeichenketten, Dateizugriffe oder dynamischen Sammlungen nötig. Bei einer vollständigen Absichtsverfolgung richtet sich der nötige Speicher zusätzlich nach der nachgewiesenen maximalen Zahl von Einheiten-/Bauabsichten; eine unbegrenzte Ereignishistorie ist nicht erforderlich.

Die Ausgabe muss weiterhin zusammengefasst bleiben: begrenzte erste Beispiele und Zähler pro Intervall, keine neue Einzelzeile für jede korrigierte Menge. Der vorhandene Achtstunden-Nachweis betrifft den Überblickspfad. Er belegt nicht automatisch Laufzeit oder Logwachstum zusätzlich aktivierter allgemeiner Erzeugerhooks und Korrekturen.

Die bestehenden TradeSession-Tests wurden um den tatsächlich verknüpften neuen Produktionsvertrag ergänzt. Geprüft sind native Paketgröße und Ausgangsbestand, eigene Teilreduktionen und Wiederanstieg nach Verbrauch, Obergrenze der ursprünglichen Charge, endgültige Erfüllung, echte erneute gleichwertige Zuweisung, unbekannter Save-Auftrag, fremde Menge, geänderte AIC-/Politikidentität, übersprungener nativer Max-Store, Defizit bei vorhandenem Bestand, bewahrter Holzpuffer, unabhängige feste Zellen und Überlaufgrenzen. Der Release-Build dieses Testprojekts und seine Ausführung bestanden mit null Warnungen und null Fehlern; die bestehenden verschachtelten und konkurrierenden Lifecyclefälle bestanden ebenfalls. Diese Tests ersetzen keinen Nachweis, dass jeder native Store richtig identifiziert und an den reinen Vertrag übergeben wird. Neue Hookstellen benötigen zusätzlich die vorhandenen ausgeführten Entry-/ABI-/Trampolin- und Fremdpatchprüfungen mit den jeweiligen echten Bytes.

Ein Spieltest muss den ausgewählten Vertrag prüfen: gültiger nativer Ursprung, zwischenzeitlich gedeckter Bedarf, ausreichendes Gold und Handelsfähigkeit sowie fortlaufend erzeugte kleine echte Käufe. Ein unveränderter Wunsch bei tatsächlich weiterhin bestehendem Bedarf darf nicht als Fehler zählen. Die vorhandene unsichtbare Desktop-Automation kann dafür wiederverwendet werden; globale Eingaben oder sichtbare Spielstarts sind nicht erforderlich. Diese Architekturprüfung hat keinen solchen neuen Lauf ausgeführt.

## Ergebnis vor Integration

Eine reine Erweiterung des Güterbereichs des bestehenden Testfixes erfüllt den Nutzerwunsch nicht zuverlässig. Die gewählte Restchargenregel erlaubt nun einen getrennt prüfbaren allgemeinen Mengenvertrag. Seine sichere Zuordnung zu allen nativen Erzeugern, Save-Aufträgen und konkurrierenden Materialanforderungen muss die Runtimeintegration weiterhin belegen. Eine universelle Korrektur ist mit dem hier geprüften Stand noch nicht implementiert oder nachgewiesen.
