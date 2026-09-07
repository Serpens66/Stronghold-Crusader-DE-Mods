# UCP-Features: KI-Wirtschaft, Häuser und Abrissregeln

## `ai_resources_rebuy`

UCP lässt die KI fehlendes Mehl, Eisen und Holz nach einstellbaren Wartezeiten erneut kaufen. **DE-Relevanz: hoch, falls der alte Deadlock noch besteht; keine Abdeckung gefunden.** Der Fixes-Mod korrigiert zwar die Verkaufskategorie von Weizen, aber nicht diese Nachkauf-Timer.

Die aktuelle Struktur liefert gute Messpunkte, aber noch keinen sicheren Schreibvertrag:

- `r_AISellOrBuyPhase` bei `0x2A6C`;
- Pending-Kaufmengen für Holz `0x2A74`, Eisen `0x2A88` und Mehl `0x2AB0`;
- gleichartig indizierte Sell-Suppression-Timer für Holz `0x4FD4`, Eisen `0x4FE8` und Mehl `0x5010`.

Diese Namen und Offsets stammen aus Script Extender 2.3.0; gemäß Baseline-Regel beweisen sie allein nicht, ob UCPs „Zeit ohne Ressource“ darin gespeichert wird. Zuerst pro Gut einen kontrollierten Mangel erzeugen und Phase, Pending-Menge, Suppression-Timer, Gold und Marktbestand über mehrere Kaufzyklen nur lesen. Danach den nativen Pfad ermitteln, der die erneute Pending-Menge aufbaut.

**Bevorzugte Umsetzung:** den bestätigten Retry-Entscheid beziehungsweise seinen Timer-Reset parametrisieren und Vanilla-Gold-, Markt- und Lagerprüfungen weiter ausführen lassen. Ein eigener `GameTimeManagerAPI.OnTick`-Sidecar darf höchstens den letzten erfolglosen Bedarf und die Wartezeit führen und anschließend denselben Vanilla-Requestpfad auslösen; er darf weder Güter gutschreiben noch blind Pending-Felder setzen. Alle Zeiten in Simulationsticks, Save/Load entweder aus dem Vanilla-Zustand fortsetzen oder mit dokumentierter konservativer Rücksetzung. `NetworkMode=1`.

