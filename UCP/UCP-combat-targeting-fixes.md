# UCP-Zielklassifikationsfixes

## `u_fireballistafix`

UCP korrigiert zwei Zielklassifikationen, damit Feuerballisten Mönche und Tunnelgräber automatisch als gültige Gegner erkennen. Der HD-Patch passt sowohl Einheitenzuordnung als auch Zielattribute an.

**DE-Bewertung: plausibler Altfehler, statisch noch nicht bestätigt.** Im Workspace und in `shcde-fixes-main` gibt es keinen identischen Fix. Die grafische Behandlung von Feuerballisten in verwaltetem DE-Code sagt nichts über die native automatische Zielwahl aus.

Mögliche Umsetzung: Den nativen Auto-Target-Filter mit kontrollierten Einheitenpaaren instrumentieren. `UnitR3EventHooks.OnUnitAIStateChange`, `OnUnitTakeProjectileDamageEx` und `ProjectileR3EventHooks.OnProjectileSpawn` können Zustandswechsel und tatsächliche Schüsse korrelieren, bieten aber keinen öffentlichen Auto-Target-Entscheid. Erst wenn Mönch/Tunnelgräber ausschließlich im automatischen, nicht aber im manuellen Angriff fehlen, die konkrete Typ-/Attributprüfung im nativen Filter eng erweitern. Keine globale Feindklassifikation verändern, weil dieselben Attribute von anderen Projektilen verwendet werden können.

## `ai_fix_crusader_archers_pitch`

UCP sorgt dafür, dass europäische KI-Bogenschützen Pech entzünden können wie arabische Bogenschützen. Dazu wird im relevanten Prüfpunkttyp beziehungsweise Attributpfad der passende Bogenschützentyp berücksichtigt.

**DE-Bewertung: plausibler Altfehler, noch nicht bestätigt.** Keine vorhandene Mod-Abdeckung gefunden.

Mögliche Umsetzung: Eine AIV mit Pechgräben und ausschließlich europäischen Bogenschützen gegen ein gültiges Ziel testen und `OnSpawnFire`, Unittyp sowie Eigentümer protokollieren. Bei Bestätigung sollte ausschließlich die KI-Pechentzündungs-Typprüfung um `eChimps.CHIMP_TYPE_ARCHER` erweitert werden, ohne Spielerbefehle oder andere Fernkämpfer zu verändern. Ein allgemeiner Projectile-/Fire-Hook, der nachträglich Feuer erzeugt, wäre schlechter, weil er Zielprüfung, Munitions-/Animationszustand und Netzwerkablauf umgeht.

## Priorität und Netzwerk

Beide Änderungen sind klein, aber gut beobachtbar. Sie eignen sich nach reproduzierbarem Test für enge native Hooks oder – falls die Klassifikation künftig öffentlich verfügbar wird – für Script-Extender-APIs. Beide beeinflussen Kampfentscheidungen und benötigen `NetworkMode=1`.

Die Eingriffspunkte sind auch in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) zusammengefasst.

