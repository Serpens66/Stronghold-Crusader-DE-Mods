# Offene UCP-Kandidaten für KI-Wirtschaft und Belagerung

## `ai_buywood`

UCP lässt die KI zwei zusätzliche Holzeinheiten kaufen, damit ein Bogenmacher nicht das gerade für ein Bauvorhaben beschaffte Holz verbraucht. **DE-Status: ungeklärt; keine identische Abdeckung gefunden.**

Mit einer KI testen, die gleichzeitig baut und Bögen produziert. Dabei `GamePlayerResources.r_AIPendingMarketPurchaseAmountWoodLogs` (`0x2A74`), `r_AISellOrBuyPhase`, Holzbestand, Bauziel und Fletcher-State nur beobachten. Bei Bestätigung die berechnete Kaufmenge am nativen Aufbau des Kaufrequests nur in dieser Bedarfssituation um zwei erhöhen. Weder global Holz schenken noch das Pending-Feld periodisch überschreiben; die Vanilla-Prüfung von Gold, Markt und Lagerkapazität muss erhalten bleiben. Gameplayrelevant, daher `NetworkMode=1`.

## `ai_assaultswitch`

UCP verhindert, dass eine KI während einer fortgeschrittenen Belagerung auf einen anderen Lord umschwenkt. **DE-Status: möglicher Altfehler; weder Fixes-Mod noch Workspace decken ihn ab.**

Zielwahl, Angriffsstufe und `GamePlayerResources.r_AISiegePlayerIdTarget` (`0x2BD8`) instrumentieren. Nur während des belegten fortgeschrittenen Belagerungszustands die alte Player-ID binden; ausgeschiedene, verbündete oder ungültige Ziele müssen sofort neu gewählt werden. Die ID-Basis ist am konkreten Setter/Leser zu bestätigen und darf nicht aus dem Wertebereich geraten werden. `InternalAIC.who_to_pick_on` steuert die Auswahlstrategie, ist aber kein Schutz gegen einen Wechsel während laufender Belagerung. Deshalb mit `ai_attacktarget` koordinieren: AIC-Policy vor Angriffsbeginn, Assault-Lock erst nach eindeutigem Zustandsübergang. Zuerst die endgültigen Improved-Attacks-Hookpunkte prüfen, damit keine Überschneidung entsteht.

## `ai_fix_laddermen_with_enclosed_keep`

UCP ermöglicht besonders der Schlange den Einsatz von Leiterträgern, obwohl ihr eigener Bergfried vollständig ummauert ist. **DE-Status: ungeklärt; keine Abdeckung.**

Eine minimale Snake-AIV als Reproduktion erstellen und Leiterträgerzahl, `InternalAIC.siege_ladder_amount`, eigener Keep-Zugang und gewählte Belagerungsmaschinen protokollieren. Die offizielle Option `ImprovedLaddermen` ändert Einheitenwerte, belegt aber keine Korrektur dieser Planungsentscheidung. Bei Bestätigung nur die Bool-Bedingung korrigieren, welche die eigene eingeschlossene Burg fälschlich als Ausschlussgrund verwendet; Pfadfindung und gegnerische Zielburg bleiben unverändert. Ohne Reproduktion ist die übernommene HD-Bedeutung zu unsicher.

Weitere Baseline- und API-Grenzen stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

