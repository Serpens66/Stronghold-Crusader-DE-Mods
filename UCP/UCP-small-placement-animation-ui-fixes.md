# Kleine UCP-Fixes: Platzierung, Animation, Graben und UI

## `u_fix_applefarm_blocking`

UCP verschiebt die vom Apfelbauern verwendete Zielkoordinate, damit ein Gebäude links oben am Apfelgarten den Arbeiter nicht blockiert. **DE-Status: ungeklärt; keine vorhandene Abdeckung.** Die DE-Apfelfarm-Updatefunktion allein beweist den Pfad nicht, weil die problematische Koordinatenwahl in einer Hilfsfunktion liegen kann. Test: Gebäude systematisch an allen Randfeldern platzieren und `UnitR3EventHooks.OnAppleFarmerPickUpApple`, `OnAppleFarmerDropOffApple` sowie `OnUnitAIStateChange` vergleichen. Erst wenn nur die problematische Ecke den Zyklus stoppt, den Koordinatenoffset in der Zielwahl ändern; keinen Worker teleportieren.

## `fix_apple_orchard_build_size`

UCP verkleinert die Platzierungs-/Vorschaufläche auf die tatsächlich belegte Größe. **DE-Status: ungeklärt.** Das ist teilweise QoL und kann Auswirkungen auf AIV-Platzierung haben. `BuildingR3EventHooks.OnPlacementValidation` und `OnBuildStructure` erlauben, Vorschauentscheidung und autoritativen Bauversuch zu vergleichen. Vor einem Patch die DE-Maske an jeder Kante neben Hindernissen messen. Lehnt bereits die native Validierung freie Felder ab, muss die Mapper-/Gebäudegrößenmaske korrigiert werden; eine reine XAML-/Renderänderung wäre falsch. Akzeptiert die Simulation den Bau und nur die Vorschau ist zu groß, genügt eine lokale Visualkorrektur.

## `u_fix_lord_animation_stuck_movement`

UCP setzt nach Bewegung beziehungsweise Gebäudeangriff mehrere Lord-Animations-/Zustandsfelder zurück, damit der Lord nicht in einer Pose hängen bleibt. **DE-Status: möglicher Altfehler, niedrige Priorität; keine Abdeckung gefunden.** `OnUnitAIStateChange`, `OnUnitMovement` und die Unity-Visual-Spawn/Interpolate-Events können bestimmen, ob nur der Chimp-Animator oder auch der native Lordzustand hängt. Bei rein visueller Abweichung im Chimp-/Animator-Pfad korrigieren (`NetworkMode=0`); nur bei steckender Simulation den engen nativen Zustandsabschluss ändern (`NetworkMode=1`).

## `o_fix_moat_digging_unit_disappearing`

UCP verhindert einen fehlerhaften Despawn während des Graben-Aushebens. Eine im UCP-Text genannte Restkante bleibt: Ein Befehl von einem unpassierbaren Ausgangsfeld kann weiterhin problematisch sein. **DE-Status: ungeklärt.** Eure Funktionen für verbesserte Grabenarbeit und freundliche Grabenbewegung sind fachlich nicht derselbe Fix; sie dürfen deshalb nicht als Abdeckung gezählt werden.

Test: einzelne und gruppierte Einheiten, unterbrochene Befehle, unpassierbare Startfelder sowie Save/Load. `OnBuildPitchDitch`, `OnRemovePitchDitch`, `OnUnitAIStateChange` und `OnUnitDelete` gemeinsam protokollieren. Nur wenn dieselbe 1-basierte Unit-ID nach dem Grabenübergang wirklich im Low-Level-Delete endet, den spezifischen Zustandszweig korrigieren; ein versteckter oder transformierter Chimp ist kein Despawnbeweis.

## `o_armory_marketplace_weapon_order_fix`

UCP2 ordnet Waffen und Icons zwischen Arsenal und Marktplatz um. Der Patch adressiert HD-spezifische Tabellen/UI-Bytes und ist als `Other`, standardmäßig aus, klassifiziert.

**Korrigierte DE-Bewertung: funktional bereits durch `BugfixesAndQoL` abgedeckt.** `HdMarketViewHook` und `MarketGoodsOrderDefinition` erlauben die vollständige lokale Sortierung der Marktgüter; die HD-Reihenfolge kann ausdrücklich wiederhergestellt werden. Damit lässt sich das sichtbare UCP-Ergebnis ohne alten Binärtabellenpatch herstellen.

Der Arsenalteil wird dadurch nicht umsortiert. Nur wenn Markt und Arsenal in einer konkreten DE-Konfiguration weiterhin sichtbar auseinanderlaufen, sollte dieselbe lokale Einstellung zusätzlich auf das Arsenal-ViewModel angewandt werden. Dafür ist kein simulationsrelevanter Native-Hook nötig.

Die übergreifenden Baseline- und Eventregeln stehen in [UCP-native-integration-audit.md](UCP-native-integration-audit.md).

