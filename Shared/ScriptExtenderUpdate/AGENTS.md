# Script-Extender-Update-Regeln

- Zielversion, Tag, Commit, Tree und installierte Assembly für jeden Lauf dynamisch prüfen. Diese flüchtigen Werte nicht als allgemeine Zielversion in die Root-`AGENTS.md` übernehmen.
- Historische Versionsidentitäten gehören ausschließlich in hash- beziehungsweise commitgebundene Kompatibilitätspläne, Auditberichte und Baseline-Provenienz.
- Vor jeder Manifest- oder Quellmutation den Runtime- und Release-Hook-Präflight vollständig ausführen. Native Änderungen am Script Extender benötigen einen passenden releaseweiten Hook-Audit; unbekannte Überschneidungen müssen fail-closed abbrechen.
- Ohne expliziten Kompatibilitätsplaneintrag weder Modversion, Mindestversion noch Changelog verändern. Nicht betroffene Mods behalten ihre nachweislich kompatible Mindestversion.
- Eine Mindestversion nur erhöhen, wenn der betreffende Mod tatsächlich eine neuere API oder einen versionsgebundenen Fix benötigt oder der Benutzer dies ausdrücklich verlangt.
- Bekannte Script-Extender-Fehler nur für die tatsächlich installierte Version dokumentieren. Sobald die installierte Version den Fehler behebt, den Hinweis vollständig entfernen; behobene Fehler nicht als allgemeine Arbeitsregel konservieren.
- Kompatibilitätspläne und Hook-Audits sind historische Provenienz und dürfen ihre konkreten Versions-, Commit- und Hashangaben behalten.
