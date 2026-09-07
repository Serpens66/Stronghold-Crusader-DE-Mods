# UCP-Features: KI-Angriffe und Rekrutierung

## `ai_addattack`: absolute und prozentuale Angriffsskalierung

Vanilla vergrößert die Sollstärke jeder folgenden Hauptangriffswelle um einen festen Wert von fünf Einheiten. UCP erlaubt entweder einen anderen absoluten Zuwachs oder `ai_addattack_alt`: einen Faktor bezogen auf die ursprüngliche lordabhängige Angriffsstärke. Dadurch skaliert beispielsweise eine Anfangsarmee von 10 bei 50 Prozent mit 10, 15, 20, …, eine Anfangsarmee von 50 dagegen mit 50, 75, 100, ….

**DE-Relevanz: hoch; die relative Einstellung fehlt, der zugrunde liegende Mechanismus ist aber bereits in DE vorhanden.** Weder `BugfixesAndQoL` noch `shcde-fixes` bieten die relative Nutzeroption. Improved Attacks verändert Zielverteilung und Angriffsverhalten, nicht diesen Größenanstieg.

Die erneute Baseline-Prüfung korrigiert die frühere Hook-Empfehlung. DE erweitert `InternalAIC` um `siege_normal_wave_multiplier` (`0x458`, Default 5) und `siege_high_gold_wave_multiplier` (`0x45C`, Default 7). Dieselben Werte 5/7 verwendet UCP für normalen beziehungsweise hohen Goldstand. Die Felder werden durch das offizielle `lordjson`-Format und `EngineInterface` übertragen und in der nativen AIC-Initialisierung gesetzt. Das ist ein starker, aber noch durch einen Beobachtungstest abzuschließender Beleg für den direkten DE-Nachfolger der absoluten UCP-Regel.

Zusätzliche Evidenz liefern die exportierten DE-Lords selbst: Surgeon nutzt 120/3/7, Bullseye 300/4/12 und Baibars 500/5/20 für Limit, normalen und hohen Wellenzuwachs. Die Werte sind damit nachweislich lordabhängige Konfigurationsdaten und keine bloßen reservierten Felder. Der Laufzeittest bleibt nötig, um Formel, Angriffsindex und Goldschwelle statt nur Speicherung und Übertragung zu bestätigen.

**Empfohlene DE-Umsetzung:** AIC-Override statt Berechnungshook. Für jeden tatsächlich verwendeten Lord die ursprüngliche Anfangsstärke `siege_trigger_level` lesen und bei relativem Modus eine ganzzahlige Schrittweite berechnen:

    schritt_normal = round(siege_trigger_level * prozent / 100)
    ziel = min(siege_max_troops, siege_trigger_level + schritt_normal * angriffsindex)

`schritt_normal` wird in `siege_normal_wave_multiplier` geschrieben. Für hohen Goldstand muss das Preset ausdrücklich festlegen, ob derselbe Prozentwert oder die Vanilla-Relation 7:5 gelten soll. Dadurch skaliert ein lordabhängiger Grundwert ohne laufenden Hook proportional. Vor Freigabe sind Wellen 1 bis 4 mit zwei stark verschiedenen `siege_trigger_level`-Werten zu protokollieren, damit Indexbasis, Rundung und Goldpfad bestätigt werden.

Der Zugriff erfolgt beim bestätigten Kartenstart über `GameAIManagerAPI.GetAICArray()`, nachdem Vanilla- oder Custom-Lord-Daten vollständig geladen und den aktiven KI-Spielern zugeordnet sind. `AIR3EventHooks.OnAIProcessCustomLord` ist kein geeigneter Vorab-Schreibpunkt, da es erst nach dem verwalteten Einlesen ausgelöst wird. Nur die drei betroffenen Felder ändern, den übrigen `InternalAIC` unverändert zurückschreiben und Größe/Offsets statisch absichern. Beim öffentlichen `SetAICFromBytes` müssen AIC-Index und die exakte Länge `sizeof(InternalAIC)` vor dem Aufruf validiert werden; kein verkürztes Feldfragment übergeben. `r_AITargetAttackForceSize` (`GamePlayerResources+0x38A0`) dient zur Beobachtung, nicht zum periodischen Überschreiben. Erst wenn der Test widerlegt, dass die neuen AIC-Felder die Zielstärke steuern, ist ein enger Hook am einmaligen Schreibpunkt dieses Zustandsfelds nötig.

## `ai_attacklimit`

UCP ersetzt die Vanilla-Obergrenze 200 für rekrutierte Angriffstruppen durch einen einstellbaren Wert. **DE-Relevanz: sinnvoll und als Nutzeroption nicht abgedeckt; DE besitzt jedoch bereits das passende AIC-Feld.** `InternalAIC.siege_max_troops` liegt bei `0x454`, hat in DE den Default 200 und wird im offiziellen Lord-Format serialisiert. `UnitLimit` begrenzt Einheiten allgemein und ist kein Ersatz.

Deshalb dasselbe AIC-Override-Verfahren wie bei `ai_addattack` verwenden und keinen Cap-Vergleich patchen. Die erwartete Reihenfolge „Skalierung, danach Cap“ durch Beobachtung von `r_AITargetAttackForceSize` bestätigen. Hohe Werte können das globale Unit-Limit ausschöpfen; UI-Warnung und Mehr-KI-Test bleiben erforderlich.

## `ai_attackwave` / Improved Attacks

UCP verteilt mehr Angreifer auf Mauer-, Turm- und Wirtschaftsziele und schickt Resttruppen nach einem Durchbruch zum Lord. **Status: separat untersucht und teilweise in Arbeit.** `BugfixesAndQoL/src/AiWallTargetingFix.cs` deckt bereits den Teil ab, bei dem nicht nur eine Einheit pro Mauersegment angreift. Die übrigen Teile nicht hier duplizieren, sondern an die laufende Improved-Attacks-Arbeit anbinden.

## `ai_attacktarget`

UCP bietet nächster, reichster oder schwächster Gegner. **DE-Relevanz: optional und als globale Einstellung nicht abgedeckt; pro Lord existiert bereits `InternalAIC.who_to_pick_on` bei `0x2F0`.** Das Feld wird im offiziellen Lord-Format übertragen. Die exportierten DE-Lords verwenden Werte 0, 1, 2 und 4; ihre genaue Bedeutungszuordnung ist in Extender und Baseline aber nicht dokumentiert. Die alte UCP-Branch-Auswahl darf daher nicht ungeprüft als Enum übernommen werden.

Bevorzugte Umsetzung ist wiederum ein AIC-Override. Zuerst in einem Vier-Spieler-Test jede Ausprägung 0/1/2/4 mit kontrolliertem Abstand, Gold und Truppenstärke kartieren und Zielwechsel über `r_AISiegePlayerIdTarget` (`GamePlayerResources+0x2BD8`) protokollieren. Danach kann die validierte Zahl global oder pro Lord gesetzt werden. Ungültige, verbündete und besiegte Ziele bleiben der Vanilla-Auswahl überlassen. `PlayerR3EventHooks.OnPlayerAIEvaluateAttackOrder` ist nur ein Ereignis für die Bewertung von Hilfs-Angriffsbefehlen und kein belegter Ersatz für die Hauptbelagerungs-Zielwahl.

## `ai_recruitinterval`

UCP setzt alle Lords auf das schnellste Rekrutierungsintervall, vergleichbar mit Rat/Richard. **DE-Relevanz: Balanceoption, nicht abgedeckt.** Die „Fast recruit rally“-Pfade in `TroopMovementFix3` beschleunigen nur die Bewegung frisch rekrutierter Einheiten.

Der DE-Eingriffspunkt ist jetzt konkret: `InternalAIC.troop_production_rate1`, `2` und `3` liegen bei `0x168`, `0x16C` und `0x170` und werden als dreiteiliges `troop_production_rate` im Lord-Format serialisiert. UCP las genau drei lordabhängige Werte und ersetzte das ausgewählte Ergebnis durch 1. Daher beim Lord-Laden alle drei Werte auf 1 setzen; ein Countdown-Reset-Hook ist unnötig. Vor Freigabe jede der drei Wirtschafts-/Schwierigkeitslagen testen, weil ihre genaue Auswahlbedingung noch nicht benannt ist.

## `ai_recruitstate_initialtimer`

UCP konfiguriert die anfänglichen Monate, in denen die KI nur Verteidiger rekrutiert; Vanilla verwendet sechs und kodiert den Wert als `6 * 800` Simulationsticks. **DE-Relevanz: sinnvoll für Szenarien, nicht abgedeckt.** In `InternalAIC` wurde kein gleichwertiges typisiertes Feld gefunden.

Hier bleibt ein nativer Eingriff erforderlich, aber erst nach Instrumentierung des Kartenstarts: Rekrutierungszustand, Simulationstick und erste Offensivrekrutierung mehrerer Lords protokollieren, anschließend den einmaligen Initialisierungsschreibpunkt des Timers ermitteln. Der Mod sollte den Startwert dort in Ticks setzen, nicht einen eigenen parallelen Wall-clock-Timer betreiben. Save/Load muss den gespeicherten Vanilla-Zustand respektieren und darf den Initialtimer nicht erneut starten.

## `ai_recruitsleep`

Der Eintrag ist in UCP2 als `Balancing` auskommentiert und damit kein ausgeliefertes aktives Feature. Er wird nicht als Portkandidat geführt. Falls die Idee später wieder aufgenommen wird, muss zuerst aus älteren Revisionen rekonstruiert werden, welche Rekrutierungspause tatsächlich gemeint war.

## Gemeinsamer Lifecycle und Netzwerkvertrag

Alle AIC-Änderungen sind gameplayrelevant: `NetworkMode=1`, `[SyncHostOnly]`, identische Integerarithmetik auf allen Teilnehmern. Der Override wird pro Karte nach Custom-Lord-Verarbeitung einmal angewandt und bei einem neuen Karten-/Save-Kontext aus den dort geladenen Ausgangswerten neu berechnet. Nicht dauerhaft die eingebettete AIC-Tabelle über mehrere Sessions kumulativ verändern. Die Offset- und Formelbelege stehen gesammelt in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

