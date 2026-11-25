# ImprovedHunters – TODO

- [ ] **Fleischsymbol im Baumenü der Gerberei ergänzen**, sobald APIShared eine passende Overlay-API für Baumenü-Buttons bietet. Neben dem Harnisch ein Fleischsymbol im Vanilla-Stil anzeigen, etwa halb so groß wie der Harnisch. Das Symbol soll nur sichtbar sein, wenn die Fleischproduktion wirksam aktiv ist. Dafür dieselbe Abfrage wie im Gerberei-Bedienfeld verwenden. Sie berücksichtigt den Mod-Schalter, `TannerMeatMode`, die Patch-Verfügbarkeit und die Multiplayer-Freigabe. [Aktivierungslogik](src/ImprovedHuntersRuntime.cs#L1116)

## Vorschlag für APIShared

`APIShared.Presentation.BuildMenuIcons` könnte zwei Methoden anbieten. Beide adressieren den vorhandenen HUD-Button über dessen Namen, hier `ButtonBuildTanner`. Die Signaturen sind ein Entwurf.

```csharp
bool TryRegisterOverlay(string ownerGuid, string id, string buttonName,
    Func<ImageSource> createImage, BuildMenuOverlayPlacement placement,
    out BuildMenuOverlayHandle handle, out string reason);

bool TryRegisterSpriteReplacement(string ownerGuid, string id, string buttonName,
    Func<BuildMenuSpritePair> createSprites,
    out BuildMenuSpriteHandle handle, out string reason);
```

- **Overlay:** APIShared hängt ein separates `Image` ohne Hit-Testing in die visuelle Struktur des Buttons ein. Das Bild folgt der Sichtbarkeit und Animation des Buttons. `placement` gibt Größe und Position relativ zum Button vor. ImprovedHunters registriert das Overlay einmal und steuert es über `handle.SetVisible(bool)` mit der bestehenden Aktivierungsabfrage. APIShared wendet den Zustand auf dem Unity-Hauptthread an und bindet das Overlay nach einer HUD-Neuerstellung erneut ein.
- **Sprite-Ersatz:** Wie im Rundturm-Test von SkinTest setzt die API lediglich `PropEx.Sprite1` und `PropEx.Sprite2` als Paar am vorhandenen Button. Dadurch kann auch derselbe Button ein anderes Grundbild und ein Overlay bekommen. APIShared koordiniert konkurrierende Ersetzungen pro Button. Beim Deaktivieren einer Registrierung gilt der nächste aktive Ersatz oder das ursprüngliche Bild, sofern die aktuellen Quellen noch der API gehören. [Rundturm-Test in SkinTest](../Testmods/SkinTest/src/SwordsmanSkinRuntime.cs#L878)
- **Abnahme im Spiel:** Fleischmodus an und aus prüfen, ebenso Hover, Klick, gesperrte Gerberei und die Rückkehr ins Baumenü. Mit einem gleichzeitigen Sprite-Ersatz am selben Button prüfen, dass Grundbild und Overlay unabhängig voneinander aktiviert und entfernt werden können.

## Trage- und Arbeitsanimationen

- [ ] **Gerber mit Fleisch auf dem Weg zum Kornspeicher anzeigen**, sobald APIShared eine passende API für kontextabhängigen Sprite-Austausch bietet. Körper, Hände und Harnisch sind in denselben Frames gezeichnet. Eine Fleischvariante der Gerber-Tragereihe erstellen und das Fleisch aus den Jägerframes als Vorlage für Größe und Aussehen verwenden. Griff und Perspektive an die Gerberpose anpassen. Den Ersatz nur für den identifizierten Gerber mit bestätigtem `meat-trip` seiner Gerberei und tatsächlich getragener Ladung anwenden. Die Einstellung der Gerberei allein sagt nicht, was der Gerber gerade trägt. [Transportzustand](src/TannerMeatState.cs)
- [ ] **Kadavertransport und Arbeit an der Jagdhütte visuell anpassen**, sobald sich Körper- und Gebäudeanimationssprites über APIShared austauschen lassen. Mit dem Kadavertransport anfangen. Vanilla verwendet eine Hirschreihe und eine gemeinsame Reihe für andere Beute. Für Kaninchen, Kamel und Huhn sind eigene Trageframes denkbar. Die vorhandene APIShared-Erfassung der Tierart bei der Kadaveraufnahme über eine Lese-API nutzen. Bei unbekannter Tierart, etwa nach dem Laden, genügt die normale Tragegrafik. Danach bei Bedarf die Bilder für Aufhängen, Zerlegen und Ausblenden an der Jagdhütte anpassen. [Hunter-Baseline](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HUNTER_PREY_RULES.md)

**APIShared-Anbindung.** [Paint-Broker-Entwurf in PR #26](https://github.com/SHCDE-APIShared/APIShared/pull/26) als Ausgangspunkt prüfen. Für Körperbilder den Renderer wie in [SkinTest](../Testmods/SkinTest/src/SwordsmanSkinRuntime.cs) seiner Einheit zuordnen. Für die Hüttenarbeit die Gebäudeanimation über ihre Objekt-ID zuordnen. Bei fehlenden Frames oder fremdem Sprite-Ersatz das vorhandene Bild beibehalten. Normale und alternative Trageframes, Fußposition, Teamfarbmaske und Transparenz zusammen berücksichtigen.

**Rein visuell.** Nur am Renderer austauschen. Native Animationszähler steuern unter anderem Schuss und Produktion und bleiben wie Waren, Wege, Arbeitsdauer, Save-Daten und Multiplayer-Synchronisierung unverändert. Im Spiel gemischte Harnisch- und Fleischlieferungen, volle oder entfernte Kornspeicher, Kameradrehungen und Laden mitten im Transport prüfen. Die abgelieferten Mengen mit und ohne Grafikaustausch vergleichen.

## Arbeitszeit in der Jägerhütte

- [ ] **Verarbeitungszeit pro Tierart einstellbar machen**, sobald APIShared eine passende API für den Arbeitszyklus der Jägerhütte anbietet. Die Option soll für Hirsch, Ziege, Kaninchen, Kamel und Huhn gelten. Standardwert ist jeweils das Vanilla-Tempo. Bei fehlender Tierzuordnung gilt ebenfalls das Vanilla-Tempo, etwa für einen laufenden Arbeitsgang aus einem älteren Spielstand.

- **APIShared-Anbindung.** Ein optionales Arbeitsintervall pro Tierart im gemeinsam abgestimmten Jagdkatalog vorsehen. Die API soll die tatsächlich eingesammelte Tierart aus der vorhandenen Kadaverzuordnung verwenden und für den aktiven Arbeitsgang den Abstand zwischen den Fortschrittsschritten der Hütte steuern. Damit bleiben Animation, alle sechs Fleischbuchungen und der Abschluss in ihrer Vanilla-Reihenfolge. `workTicks` in `WeaponProductionOptionDefinition` ist ein Vorbild für die Dauer je Arbeitsgang. Die Jägerhütte braucht eine eigene Anbindung. [Hunter-Baseline](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HUNTER_PREY_RULES.md#L555) · [Werkstattrezepte](../APIShared/docs/WEAPON_PRODUCTION.md#register-a-recipe)
- **Zielwahl.** Dieselbe wirksame Verarbeitungsdauer in die Kosten für lebende Beute und freie Kadaver einrechnen. Die feste Hüttenpauschale dabei ersetzen und den separaten Aufwand für Jagd, Wege und Lieferung erhalten. [Zielwahl](src/HunterTargetSelectionFeature.cs) · [Kadaverwahl](src/HunterCorpseChoiceFeature.cs)
- **Multiplayer.** Die bestehende APIShared-Reparatur der Kadaverzuordnung wiederverwenden, die ImprovedHunters für den Fleischertrag bereits über `HunterMeatYieldEvents.TryEnableMultiplayerResync` aktiviert. Das wirksame Intervall aus der reparierten Zuordnung ableiten. Chore 55 setzt das native Intervallfeld auf den Standardwert zurück. Chore 57 überträgt die beiden Taktzähler bereits. APIShared sollte bei aktiver Verarbeitung auch die für den nächsten Schritt maßgebliche Differenz in die Gebäudeprüfsumme einbeziehen, damit eine reine Taktabweichung eine Reparatur auslöst. [Bestehende Registrierung](src/HuntablePreyIntegration.cs) · [Resync-Befunde](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HUNTER_PREY_RULES.md#L555)
- **Abnahme.** Unterschiedliche Tierarten und einen freien Kadaver prüfen, außerdem Laden und einen echten Host-Client-Resync mitten in der Verarbeitung. Kadaverzuordnung und Hüttentakt jeweils getrennt auf Abweichungen prüfen. Beide Peers sollen den Arbeitsgang im selben Tick abschließen und dieselbe Fleischmenge liefern.
