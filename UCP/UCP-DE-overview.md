# Verbleibende UCP-Fixkandidaten für Stronghold Crusader DE

Stand: 8. September 2026

## Ergebnis

Nach dem Einzelabgleich enthält der Ordner nur noch Fixeinträge, deren Fehler in der aktuellen DE nicht sicher ausgeschlossen werden konnte. Entfernt wurden Einträge, die bereits durch bekannte Mods abgedeckt sind, als DE-Spieloption vorliegen, nur das alte HD-Protokoll betreffen, keine Fehlerkorrektur sondern eine Balanceänderung sind oder deren Korrektur im aktuellen Native-Code eindeutig vorhanden ist.

Die verbleibenden Kandidaten sind:

- Leiterziel nach dem Klettern;
- KI-Holzreserve beim gleichzeitigen Bauen und Produzieren;
- Zielwechsel während einer fortgeschrittenen KI-Belagerung;
- die drei noch offenen `ai_rebuild`-Teilverträge;
- Leiterträgerplanung bei einem eingeschlossenen eigenen Keep;
- Lord-Animationsabschluss nach Bewegung oder Gebäudeangriff;
- Apfelbauer-Zielkoordinate;
- Gerber-Kuhreservierungsrennen;
- Bäcker-Despawn bei zwischenzeitlich fehlendem Mehl.

Bei Gerber und Bäcker ist der zugrunde liegende problematische Kontrollpfad statisch weiterhin vorhanden, benötigt aber noch einen gezielten Laufzeitbeleg. Bei den übrigen Kandidaten reicht die aktuelle statische Evidenz weder zum Löschen noch zum sicheren Implementieren.

## Analysebasis

- kanonische installierte `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
- semantische Native-Baseline `FBCB9319`, gebunden an Script Extender 2.3.0, Commit `a0cd52993b44a6909d4f7f6a92f82fa5888a8e63`;
- lokale UCP2-/UCP3-Quellen und heruntergeladene Store-Pakete;
- Workspace-Quellcode und lokaler `shcde-fixes`-Quellcode.

Der installierte DLL-Hash wurde gegen `CURRENT.json` geprüft und stimmt überein. Alte UCP-Adressen und x86-Bytes dienen ausschließlich als Verhaltensreferenz.

## Berichte zu den verbleibenden Kandidaten

- [UCP-bugfix-coverage-audit.md](UCP-bugfix-coverage-audit.md)
- [UCP-AI-rebuild-and-access.md](UCP-AI-rebuild-and-access.md)
- [UCP-ladder-climb.md](UCP-ladder-climb.md)
- [UCP-AI-economy-and-siege.md](UCP-AI-economy-and-siege.md)
- [UCP-worker-state-fixes.md](UCP-worker-state-fixes.md)
- [UCP-small-placement-animation-ui-fixes.md](UCP-small-placement-animation-ui-fixes.md)
- [UCP-native-integration-audit.md](UCP-native-integration-audit.md)

## Feature-Berichte

Nicht als Bugfix eingestufte UCP-Funktionen bleiben in den separaten Feature-Berichten dokumentiert.

- [UCP-features-overview.md](UCP-features-overview.md)
- [UCP-features-AI-attacks-and-recruitment.md](UCP-features-AI-attacks-and-recruitment.md)
- [UCP-features-AI-economy.md](UCP-features-AI-economy.md)
- [UCP-features-units-and-balance.md](UCP-features-units-and-balance.md)
- [UCP-features-interface-and-gameplay.md](UCP-features-interface-and-gameplay.md)
- [UCP-store-feature-audit.md](UCP-store-feature-audit.md)
