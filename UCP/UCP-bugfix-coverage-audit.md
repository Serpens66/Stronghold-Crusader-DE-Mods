# Verbleibende UCP-Fixkandidaten für SHCDE

Stand: 8. September 2026

## Ergebnis der Einzelprüfung

Diese Datei enthält nur noch UCP-Einträge, deren zugrunde liegender Fehler für die aktuelle DE nicht sicher ausgeschlossen werden konnte. Bereits in DE integrierte, durch bekannte Mods abgedeckte, HD-protokollspezifische oder nach aktueller Native-Prüfung entfallene Einträge wurden gelöscht. Alle Native-Aussagen beziehen sich auf die installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; dieser Hash stimmt mit `CURRENT.json` überein.

| UCP-Schlüssel | Ergebnis der DE-Prüfung | Quellnachweis oder nächster Beleg |
| --- | --- | --- |
| `o_fix_ladderclimb` | offen, möglicher Altfehler | DE besitzt die Ladder-/Bewegungszustände und die Zielkoordinaten weiterhin; in den geprüften Übergängen wurde kein UCP-gleicher Zielpuffer nach dem Leiterabschluss belegt. Reproduktion bleibt erforderlich. |
| `ai_buywood` | offen | Das typisierte Pending-Holzkauffeld und die KI-Kaufphase existieren in 2.3.0; in Workspace-Mods und den geprüften Kaufpfaden wurde keine situationsgebundene Reserve von zwei Holz gefunden. Gleichzeitiges Bauen und Bogenproduktion reproduzieren. |
| `ai_assaultswitch` | offen, möglicher Altfehler | `r_AISiegePlayerIdTarget` und die Belagerungszustände bestehen fort; kein vorhandener Mod und kein belegter DE-Zweig bindet das Ziel ausdrücklich erst ab UCPs fortgeschrittener Angriffsphase. Zielwechsel kontrolliert provozieren. |
| `ai_rebuild` Rest | offen | Turmruinen sind entfernt, weil sie bereits abgedeckt sind. Für leicht beschädigte Mauern, verlorene Gilden-/Pechschmelzen-Sammelpunkte und Steinbruchplattformen bestehen weiterhin DE-AIV-/Repairpfade, aber keine gleichwertige Abdeckung. Die drei Fälle getrennt testen. |
| `ai_fix_laddermen_with_enclosed_keep` | offen | Leiterträgerplanung und `siege_ladder_amount` existieren; die alte Bool-Bedeutung lässt sich aus dem breiten Planungsautomaten nicht sicher ableiten. Snake-Testburg mit eingeschlossenem eigenem Keep verwenden. |
| `u_fix_lord_animation_stuck_movement` | offen, niedrige Priorität | Die Lord-Zustandsmaschine bei RVA `0x15A950` behandelt Bewegungs-/Angriffszustände und mehrere Animationsrücksetzungen, doch die beiden UCP-spezifischen Abschlusskanten konnten nicht eindeutig als vorhanden oder fehlend bewiesen werden. Bewegung und Gebäudeangriff getrennt testen. |
| `u_fix_applefarm_blocking` | offen | Die Apfelbauernfunktion bei RVA `0x12FC70` besteht fort; die UCP-Koordinatenkorrektur liegt wahrscheinlich in einer Hilfsfunktion und konnte keinem DE-Zweig sicher zugeordnet werden. Alle Randfelder systematisch blockieren. |
| `u_tanner_fix` | Fehlpfad statisch vorhanden; Laufzeitbeleg ausstehend | Die Gerberfunktion bei RVA `0x13E0B0` prüft Kuhziel, Generation und Alive-State; ein verlorenes Reservierungsrennen kann weiterhin ohne erfolgreiche Kuhübernahme zurückführen. Mehrere Gerber auf dieselben Kühe ansetzen. |
| `o_fix_baker_disappear` | Despawn-Pfad statisch vorhanden; Laufzeitbeleg ausstehend | Die Bäckerfunktion bei RVA `0x138850` kann nach fehlgeschlagenem Mehl-/Lagerlookup weiterhin `AliveState.MarkedForDeletion` setzen. Weil derselbe Zustand legitime Despawns abwickelt, muss der Missing-Flour-Rennfall noch gezielt korreliert werden. |

## Integrationsentscheidung nur nach positivem Test

| UCP-Schlüssel | Erstes DE-Instrument | Engste zulässige Korrektur |
| --- | --- | --- |
| `o_fix_ladderclimb` | öffentliche Unit-/Tribe-Order- und State-Events | Ziel nach 1-basierter Unit-ID plus Slotgeneration puffern und nur am bestätigten Ladder-Exit wiederherstellen |
| `ai_buywood` | Pending-Holzkauf `GamePlayerResources+0x2A74`, KI-Phase und Fletcher-State nur lesen | Vanilla-Kaufrequest ausschließlich in der belegten Bedarfssituation um zwei erhöhen |
| `ai_assaultswitch` | `r_AISiegePlayerIdTarget` `+0x2BD8` und Belagerungszustand | gültiges Ziel nur während der belegten fortgeschrittenen Belagerung binden |
| `ai_rebuild` Rest | Spawn/Delete/Repair/AI-Wall-Events | Mauer, Rally-Gebäude und Quarry-Plattform als getrennte Regeln behandeln |
| `ai_fix_laddermen_with_enclosed_keep` | Snake-AIV, `siege_ladder_amount`, eigener Zugang und Planungsresultat | nur den falschen Ausschluss durch den eigenen eingeschlossenen Keep ändern |
| `u_fix_lord_animation_stuck_movement` | native State- plus Unity-Visual-Events | visuelle und simulierte Störung zuerst trennen; nur die nachgewiesene Abschlusskante zurücksetzen |
| `u_fix_applefarm_blocking` | Apple-Pickup/-Dropoff und Unit-State | Zielkoordinate korrigieren, keinen Worker teleportieren |
| `u_tanner_fix` | State, Kuhziel, Generation und Reservierungsbesitz | ausschließlich den belegten Reservierungs-Fehlpfad erneut wählen oder warten lassen |
| `o_fix_baker_disappear` | Flour-Pickup, State und Unit-Delete gemeinsam korrelieren | nur den belegten Missing-Flour-Despawnzweig umleiten |

Ein ähnlicher Kontrollfluss oder ein generischer Funktionsname ist kein Laufzeitbeweis. Vor jedem Patch sind positiver und negativer Kontrollfall, aktueller Hashabgleich, vollständiger Datenfluss und Konfliktprüfung gegen Script Extender 2.3.0 sowie alle installierten Mods erforderlich.
