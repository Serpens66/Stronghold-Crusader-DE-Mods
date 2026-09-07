# UCP-Fixes für Arbeiter-Zustandsmaschinen

## Ergebnis

Die DE besitzt weiterhin native Updatefunktionen für Bäcker und Gerber. Für beide aktuellen RVAs liefert die Baseline nur generische Funktionsnamen mit Konfidenz `candidate`; bestätigt ist dagegen jeweils das eindeutige Versionsmatch zur vorherigen DE-DLL (`unique-normalized-hash-and-cfg`, Score 1,0). Die fachliche Rollenbezeichnung stammt aus der aktuellen Unit-VTable und dem Aufrufkontext. HD-Bytes sind nicht übertragbar. Im Workspace und in `shcde-fixes-main` wurde für Gerber und Bäcker keine identische Korrektur gefunden.

## `u_tanner_fix`

HD-Verhalten: Zwei Gerber wählen dieselbe Kuh; verliert einer das Rennen, kehrt er ohne Kuh zurück. UCP lässt ihn warten beziehungsweise erneut wählen.

**DE-Bewertung: zugrunde liegender Fehlpfad statisch weiterhin vorhanden; Laufzeitreproduktion ausstehend.** Die über die aktuelle Unit-VTable zugeordnete Gerber-Updatefunktion besteht in DE weiter (`FUN_18013E0B0`, RVA `0x13E0B0`). Ihr Kontrollfluss prüft Kuhziel, Slotgeneration und Alive-State; beim verlorenen Ziel-/Reservierungsrennen existiert weiterhin ein Rückweg ohne erfolgreiche Kuhübernahme. Ein identischer vorhandener Fix wurde nicht gefunden. Damit ist der Eintrag nicht sicher entbehrlich.

Umsetzung: `UnitR3EventHooks.OnUnitAIStateChange` für Gerber abonnieren und zusätzlich Unit-ID, Kuhziel sowie Zielbesitz beim Reservieren und Fehlschlag protokollieren. Das Event darf den beobachteten Zustand für Diagnoseläufe markieren, ersetzt aber nicht die Kuhreservierungsentscheidung. `FUN_18013E0B0` ist mit 5255 Bytes ein sehr breiter Updateautomat; ein Prefix/Postfix um die gesamte Funktion wäre unnötig riskant. Nach bestätigter Reproduktion den kleinsten fehlgeschlagenen Zielbesitz-Zweig bestimmen und dort erneut wählen/warten lassen. Slot-/Global-ID-Verwechslungen vermeiden; `UnitId` aus dem Event ist 1-basiert.

## `o_fix_baker_disappear`

HD-Verhalten: Erreicht der Bäcker ein Lager, in dem inzwischen kein Mehl mehr liegt, kann der fehlerhafte Zustandsübergang die Einheit verschwinden lassen. UCP entfernt den Despawn-Pfad und lässt den Arbeiter normal weitersuchen.

**DE-Bewertung: zugrunde liegender Despawn-Pfad statisch weiterhin vorhanden; Laufzeitreproduktion ausstehend.** Die über die aktuelle Unit-VTable zugeordnete Bäckerfunktion besteht in DE weiter (`FUN_180138850`, RVA `0x138850`). Nach dem Mehl-/Lagerlookup besitzt sie weiterhin einen Fehlschlagpfad, der den Alive-State auf `MarkedForDeletion` (`3`) setzt. Weil derselbe Zustand auch für legitimen Arbeiterabbau verwendet wird, muss ein Laufzeittest noch belegen, dass gerade der Missing-Flour-Rennfall dort endet. Kein gleichwertiger Fix wurde gefunden; der Eintrag bleibt deshalb erhalten.

Umsetzung: Den Lagerzugriff künstlich konkurrieren lassen und `UnitR3EventHooks.OnBakerPickUpFlour`, `OnUnitAIStateChange` sowie `OnUnitDelete` gemeinsam korrelieren. So ist unterscheidbar, ob fehlendes Mehl wirklich einen Despawn oder nur einen normalen Zustandswechsel erzeugt. `FUN_180138850` ist 4351 Bytes groß; nur den durch diesen Trace belegten Missing-Flour-Zweig umleiten. Der allgemeine legitime Arbeiterabbau darf nicht deaktiviert werden.

## Gemeinsame Testanforderungen

- Test mit hoher Spielgeschwindigkeit und mehreren gleichartigen Betrieben.
- Kartenstart, Save/Load und zerstörte/neu gesetzte Lagergebäude abdecken.
- Nach einem Hook Unit-Existenz, Arbeitsplatzbezug und Ressourcenbilanz prüfen.
- Beide Änderungen sind simulationsrelevant und benötigen `NetworkMode=1`.

Hash-, Größen- und Eventbelege sind in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) eingeordnet.

