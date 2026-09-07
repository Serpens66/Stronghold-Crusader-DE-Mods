# Kleine und mittlere UCP-Fixes für KI-Wirtschaft und Belagerung

## `ai_tethers`: durch `shcde-fixes` abgedeckt

UCP setzt die maximale Zahl der Ochsengespanne pro KI-Lord auf zehn und verhindert damit zusätzliches Bauen bei weiteren Steinbrüchen.

Der lokale Fixes-Mod stellt über den Script Extender `AIMaxOxTethers` und ergänzend `AIStoneToOxenRatio` bereit. `AIMaxOxTethers=10` bildet die entscheidende UCP-Regel direkt ab; das Verhältnis erlaubt darüber hinaus eine DE-spezifische Feinsteuerung nach Steinrückstau und lebenden Ochsen.

**Bewertung: abgedeckt.** Kein eigener Hook in `BugfixesAndQoL`. Nur wenn der Fixes-Mod später bewusst ersetzt wird, sollte dieselbe öffentliche Global-Konfiguration übernommen werden.

## `ai_buywood`

UCP lässt die KI zwei zusätzliche Holzeinheiten kaufen, damit ein Bogenmacher nicht das gerade für ein Bauvorhaben beschaffte Holz verbraucht. **DE-Status: ungeklärt; keine identische Abdeckung gefunden.**

Mit einer KI testen, die gleichzeitig baut und Bögen produziert. Dabei `GamePlayerResources.r_AIPendingMarketPurchaseAmountWoodLogs` (`0x2A74`), `r_AISellOrBuyPhase`, Holzbestand, Bauziel und Fletcher-State nur beobachten. Bei Bestätigung die berechnete Kaufmenge am nativen Aufbau des Kaufrequests nur in dieser Bedarfssituation um zwei erhöhen. Weder global Holz schenken noch das Pending-Feld periodisch überschreiben; die Vanilla-Prüfung von Gold, Markt und Lagerkapazität muss erhalten bleiben. Gameplayrelevant, daher `NetworkMode=1`.

## `ai_towerengines`

UCP hebt das Vanilla-Limit von je drei Mangonellen und Ballisten auf Türmen auf. Das ist trotz UCP-Kategorie eher eine Balanceänderung. `shcde-fixes` bietet verwandte Belagerungs- und Wirtschaftsschwellen, aber keine gleichwertige Aufhebung dieses Limits.

**DE-Bewertung: optional, nicht als Standard-Bugfix.** `InternalAIC.buy_defense_machines_at` und `buy_defense_machines_delay` steuern Kaufzeitpunkt/-abstand, ersetzen das gesuchte Stücklimit aber nicht. Nach Reproduktion das Zählen bereits installierter Turmmaschinen und den Vergleich mit 3 lokalisieren. Einen synchronisierten Maximalwert anbieten; „unbegrenzt“ intern auf ein geprüftes Cap begrenzen, damit Unit-Limit und Rekrutierungsstillstand nicht ausgelöst werden.

## `ai_assaultswitch`

UCP verhindert, dass eine KI während einer fortgeschrittenen Belagerung auf einen anderen Lord umschwenkt. **DE-Status: möglicher Altfehler; weder Fixes-Mod noch Workspace decken ihn ab.**

Zielwahl, Angriffsstufe und `GamePlayerResources.r_AISiegePlayerIdTarget` (`0x2BD8`) instrumentieren. Nur während des belegten fortgeschrittenen Belagerungszustands die alte Player-ID binden; ausgeschiedene, verbündete oder ungültige Ziele müssen sofort neu gewählt werden. Die ID-Basis ist am konkreten Setter/Leser zu bestätigen und darf nicht aus dem Wertebereich geraten werden. `InternalAIC.who_to_pick_on` steuert die Auswahlstrategie, ist aber kein Schutz gegen einen Wechsel während laufender Belagerung. Deshalb mit `ai_attacktarget` koordinieren: AIC-Policy vor Angriffsbeginn, Assault-Lock erst nach eindeutigem Zustandsübergang. Zuerst die endgültigen Improved-Attacks-Hookpunkte prüfen, damit keine Überschneidung entsteht.

## `ai_fix_laddermen_with_enclosed_keep`

UCP ermöglicht besonders der Schlange den Einsatz von Leiterträgern, obwohl ihr eigener Bergfried vollständig ummauert ist. **DE-Status: ungeklärt; keine Abdeckung.**

Eine minimale Snake-AIV als Reproduktion erstellen und Leiterträgerzahl, `InternalAIC.siege_ladder_amount`, eigener Keep-Zugang und gewählte Belagerungsmaschinen protokollieren. Die offizielle Option `ImprovedLaddermen` ändert Einheitenwerte, belegt aber keine Korrektur dieser Planungsentscheidung. Bei Bestätigung nur die Bool-Bedingung korrigieren, welche die eigene eingeschlossene Burg fälschlich als Ausschlussgrund verwendet; Pfadfindung und gegnerische Zielburg bleiben unverändert. Ohne Reproduktion ist die übernommene HD-Bedeutung zu unsicher.

Weitere Baseline- und API-Grenzen stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

