# Pathfinding: Verweise nach Abschluss der historischen Testserie

Bereinigt am **13.11.2026** gegen Modpack-Stand `e95da5315e2f7567d7ae8c8f6d24f75f3e7804b0`. Die frühere Ergebniswiederholung und inzwischen erledigte Empfehlungen wurden entfernt. Dieser Wegweiser erhält bestehende Verweise auf den Bericht.

- **Verbleibende Verbesserungen und Abnahmen:** [PathfindingFutureImprovements.md](PathfindingFutureImprovements.md).
- **Weiterhin nützliche Lehren aus verworfenen Versuchen:** [MoatMove-Migrationsnotizen](Pathfinding/migration/moatmove-notes.md#bewahrte-optimierungslehren).
- **Aktuelle Mod-/Hookverträge:** [Pathfinding/UpdateToNewDLL.md](Pathfinding/UpdateToNewDLL.md); native Fakten stehen in der [aktuellen Baseline](_inspect/CrusaderDE-Native-Baseline/CURRENT.md).

Die vollständige historische Fassung bleibt über Git lesbar (`git show e95da5315:PathfindingFinalReport.md`); der ursprüngliche Berichtstext liegt außerdem als Git-Blob `fba3f58d2a043ef418a72103da59ee4bcca4158f` vor. Vorhandene Testartefakte und geschützte Arbeitskopien bleiben erhalten. Historische Ergebnisse sind keine neue Abnahme des aktuellen Codes.

## 8. Feuerbewegung und Rückkehr – Nachtrag vom 02.11.2026

Dieser Abschnitt erhält den bisherigen Linkanker. Die weiterhin nützlichen nativen Bewegungsverträge und das isolierte Feuerexperiment einschließlich seiner Grenzen stehen in [FIRE_EFFECTS.md](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIRE_EFFECTS.md#isolated-fire-lifetime-reported-2026-11-02-preserved-2026-11-13-vanilla-facts).

Bewegungsaufnahme, getrennte Rückkehrarten und Fristen sowie gespeicherte Bewegungsfälle sind inzwischen implementiert. Die ausstehende vollständige Ingame-Abnahme und anschließende Freigabe sind in der [neuen Arbeitsliste](PathfindingFutureImprovements.md#41-feuer-pestvermeidung-vollständig-abnehmen) beschrieben; die alten Implementierungsempfehlungen und Fristwerte gelten nicht mehr als Arbeitsauftrag.
