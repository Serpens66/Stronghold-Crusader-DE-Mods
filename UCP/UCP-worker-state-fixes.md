# UCP-Fixes für Arbeiter-Zustandsmaschinen

## Ergebnis

Die DE besitzt weiterhin native Updatefunktionen für Bogenmacher, Bäcker und Gerber. Für alle drei aktuellen RVAs liefert die Baseline nur generische Funktionsnamen mit Konfidenz `candidate`; bestätigt ist dagegen jeweils das eindeutige Versionsmatch zur vorherigen DE-DLL (`unique-normalized-hash-and-cfg`, Score 1,0). Die fachliche Rollenbezeichnung stammt aus VTable-/Aufrufkontext und muss deshalb durch Laufzeitereignisse abgesichert werden. HD-Bytes sind nicht übertragbar. Im Workspace und in `shcde-fixes-main` wurde für Gerber und Bäcker keine identische Korrektur gefunden.

## `o_fix_fletcher_bug`

HD-Verhalten: Nach Ablage der fertigen Waffe läuft der Bogenmacher unnötig erst zur Werkstatt zurück, bevor er Holz holt. UCP setzt den Folgezustand so, dass er direkt zum Vorratslager geht.

**DE-Bewertung: bereits als Spieloption integriert.** In der als Bogenmacher-Update identifizierten DE-Funktion (`FUN_18012D230`, RVA `0x12D230`, Größe 4555 Bytes beim geprüften Hash) wird die Chore-Option `AdvOpt_ImprovedFletchers` ausgewertet; der alternative Pfad wählt den direkten Lagerweg abhängig vom Produkt. Script Extender 2.2.0 stellt `IsImprovedFletchers`/`SetImprovedFletchers` bereit.

Empfehlung: keinen nativen Doppelpatch bauen. Falls das Verhalten immer aktiv sein soll, die vorhandene Advanced Option über die öffentliche API und eine synchronisierte Host-Einstellung setzen. Vorher im Spiel einmal verifizieren, dass Bogen- und Armbrustproduktion beide korrekt reagieren.

## `u_tanner_fix`

HD-Verhalten: Zwei Gerber wählen dieselbe Kuh; verliert einer das Rennen, kehrt er ohne Kuh zurück. UCP lässt ihn warten beziehungsweise erneut wählen.

**DE-Bewertung: starker Testkandidat, nicht bestätigt.** Die Gerber-Updatefunktion besteht in DE weiter (`FUN_18013E0B0`, RVA `0x13E0B0`). Ein identischer vorhandener Fix wurde nicht gefunden.

Umsetzung: `UnitR3EventHooks.OnUnitAIStateChange` für Gerber abonnieren und zusätzlich Unit-ID, Kuhziel sowie Zielbesitz beim Reservieren und Fehlschlag protokollieren. Das Event darf den beobachteten Zustand für Diagnoseläufe markieren, ersetzt aber nicht die Kuhreservierungsentscheidung. `FUN_18013E0B0` ist mit 5255 Bytes ein sehr breiter Updateautomat; ein Prefix/Postfix um die gesamte Funktion wäre unnötig riskant. Nach bestätigter Reproduktion den kleinsten fehlgeschlagenen Zielbesitz-Zweig bestimmen und dort erneut wählen/warten lassen. Slot-/Global-ID-Verwechslungen vermeiden; `UnitId` aus dem Event ist 1-basiert.

## `o_fix_baker_disappear`

HD-Verhalten: Erreicht der Bäcker ein Lager, in dem inzwischen kein Mehl mehr liegt, kann der fehlerhafte Zustandsübergang die Einheit verschwinden lassen. UCP entfernt den Despawn-Pfad und lässt den Arbeiter normal weitersuchen.

**DE-Bewertung: starker Testkandidat, nicht bestätigt.** Die Bäckerfunktion besteht in DE weiter (`FUN_180138850`, RVA `0x138850`); kein gleichwertiger Fix wurde gefunden.

Umsetzung: Den Lagerzugriff künstlich konkurrieren lassen und `UnitR3EventHooks.OnBakerPickUpFlour`, `OnUnitAIStateChange` sowie `OnUnitDelete` gemeinsam korrelieren. So ist unterscheidbar, ob fehlendes Mehl wirklich einen Despawn oder nur einen normalen Zustandswechsel erzeugt. `FUN_180138850` ist 4351 Bytes groß; nur den durch diesen Trace belegten Missing-Flour-Zweig umleiten. Der allgemeine legitime Arbeiterabbau darf nicht deaktiviert werden.

## Gemeinsame Testanforderungen

- Test mit hoher Spielgeschwindigkeit und mehreren gleichartigen Betrieben.
- Kartenstart, Save/Load und zerstörte/neu gesetzte Lagergebäude abdecken.
- Nach einem Hook Unit-Existenz, Arbeitsplatzbezug und Ressourcenbilanz prüfen.
- Alle drei Änderungen sind simulationsrelevant und benötigen `NetworkMode=1`.

Hash-, Größen- und Eventbelege sind in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) eingeordnet.

