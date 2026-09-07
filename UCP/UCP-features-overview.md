# UCP-Features: Gesamtübersicht für Stronghold Crusader DE

Stand: 7. September 2026

## Abgrenzung

Erfasst sind alle aktiv angebotenen Nicht-Bugfix-Funktionen aus UCP2/`ucp2-legacy`, einschließlich der nur dort sichtbaren Optionen `ai_resources_rebuy`, `o_fast_placing`, `u_arabxbow`, Randscrollen und Rechtsklickmenü. Auskommentierte Experimente werden separat genannt. Die 49 Store-Einträge stehen in [UCP-store-feature-audit.md](UCP-store-feature-audit.md).

## Wichtigste Ergebnisse

| Ergebnis | Features |
| --- | --- |
| Bereits in DE oder unseren Mods vorhanden | Improved Laddermen, Improved Spearmen, Speerträger-Laufen, Improved Arab Swordsmen als Nachfolger der beiden Arab-Swordsman-Buffs, Heiler, Zuschauergrundfunktion, frei wählbare Startressourcen/-truppen, kostenloser Handelsposten über `BuildingCosts`, erweiterte und synchronisierte Spielgeschwindigkeit, Marktgüterreihenfolge |
| Durch Fixes/BugfixesAndQoL weitgehend abgedeckt | `ai_housing`, `ai_nosleep`, große Teile von `ai_demolish` |
| Besonders sinnvolle neue DE-Features | prozentuale Angriffsskalierung `ai_addattack_alt`, Angriffslimit, Angriffsziel-Policy, Ressourcen-Nachkauf, responsive Tore, Pfad-Neuprüfintervall, Feuer-Immunitätszeit |
| Möglich, aber eher Komfort/Optik | Dauerplatzierung, sichtbare geplante Gräben, Spielerfarbe, arabische Ingenieurstimmen, Belagerungsgerät mittig, Bergfriedrotation |
| Nicht einfach als UCP-Port übernehmen | Strongholdify-Gesamtpreset, Extreme-Magieleiste entfernen, komplette HD-Identitätsmaske; DE hat dafür andere Systeme/Oberflächen |

## Priorität für neue Implementierungen

1. `ai_addattack_alt`: prozentuale statt absolute Vergrößerung der KI-Angriffsarmee. Dieses vom Nutzer gesuchte Verhalten fehlt als Einstellung weiterhin und ist ein Feature, kein Bugfix. Die Tiefenprüfung zeigt aber, dass es voraussichtlich ohne Berechnungshook über DEs AIC-Wellenmultiplikatoren umgesetzt werden kann.
2. `ai_attacklimit` und `ai_attacktarget`: zusammen mit der Skalierung als synchronisierte KI-Angriffsoptionen. DE besitzt dafür bereits `siege_max_troops` und `who_to_pick_on`; vor Freigabe sind Formel beziehungsweise Wertbedeutungen zu testen.
3. `ai_resources_rebuy`: nach Laufzeitbeleg, da es echte Wirtschaftsdeadlocks verhindert.


## Dokumente

- [UCP-features-AI-attacks-and-recruitment.md](UCP-features-AI-attacks-and-recruitment.md)
- [UCP-features-AI-economy.md](UCP-features-AI-economy.md)
- [UCP-features-units-and-balance.md](UCP-features-units-and-balance.md)
- [UCP-features-interface-and-gameplay.md](UCP-features-interface-and-gameplay.md)
- [UCP-native-integration-audit.md](UCP-native-integration-audit.md)
- [UCP-store-feature-audit.md](UCP-store-feature-audit.md)

