# UCP-Zielklassifikationsfixes

## `u_fireballistafix`

UCP korrigiert zwei Zielklassifikationen, damit Feuerballisten Mönche und Tunnelgräber automatisch als gültige Gegner erkennen. Der HD-Patch passt sowohl Einheitenzuordnung als auch Zielattribute an.

**DE-Bewertung: plausibler Altfehler, statisch noch nicht bestätigt.** Im Workspace und in `shcde-fixes-main` gibt es keinen identischen Fix. Die grafische Behandlung von Feuerballisten in verwaltetem DE-Code sagt nichts über die native automatische Zielwahl aus.

Mögliche Umsetzung: Den nativen Auto-Target-Filter mit kontrollierten Einheitenpaaren instrumentieren. `UnitR3EventHooks.OnUnitAIStateChange`, `OnUnitTakeProjectileDamageEx` und `ProjectileR3EventHooks.OnProjectileSpawn` können Zustandswechsel und tatsächliche Schüsse korrelieren, bieten aber keinen öffentlichen Auto-Target-Entscheid. Erst wenn Mönch/Tunnelgräber ausschließlich im automatischen, nicht aber im manuellen Angriff fehlen, die konkrete Typ-/Attributprüfung im nativen Filter eng erweitern. Keine globale Feindklassifikation verändern, weil dieselben Attribute von anderen Projektilen verwendet werden können.

Die Eingriffspunkte sind auch in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) zusammengefasst.

