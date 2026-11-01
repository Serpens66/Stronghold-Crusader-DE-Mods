# APIShared: persönliche Werte und Missionskontexte

Stand: 1. November 2026. Umsetzung der beschlossenen Verhaltenskorrektur; lokale
Prüfergebnisse und verbleibende Spielabnahme stehen im Abschlussnachweis des Auftrags.

`[PresetLocal]` und `[SyncPerPlayer]` bleiben dauerhaft persönlich. Nur normale
`[SyncHostOnly]`-Werte gehören zur temporären Customize-/Missionsarbeitskopie.
Die frühere Sperre und Wiederherstellung persönlicher Werte war ein konzeptioneller
Fehler. Das bisherige Testverhalten wird bewusst entsprechend korrigiert.

Optional kennzeichnen Mods einzelne Hostwerte mit `[MissionIndependent]`. Diese
bleiben hostkontrolliert und synchronisiert, werden jedoch weder durch Map, Trail
noch Savegame vorgegeben oder wiederhergestellt. ExtendedData zeigt sie nicht zur
Einstufung an. Bestehende Hostproperties werden nicht automatisch umklassifiziert.

In Customize ändern normale Presets Hostwerte temporär und persönliche Werte
dauerhaft. Im gesperrten Missionskontext bleibt Preset-Laden gesperrt; persönliche
Einzelwerte sind von der Missionssperre unabhängig. Andere Änderungsregeln und
`RequiresRestart` gelten weiterhin.

Die öffentliche API, UI-Bindings und der additive Vertrag für getrennte persönliche
Backendpersistenz sind im APIShared-Integrationsleitfaden dokumentiert:
[Persönliche Werte](APIShared/docs/THIRD_PARTY_GUIDE.md#personal-values-and-mission-independent-host-settings).
Es gibt keine zweite persönliche Speicherdatei und keinen neuen Netzwerktransport.

Spectator speichert Anzeigepräferenzen direkt über die gemeinsame Settingsinstanz.
Der Zwischenpuffer für eine spätere Übernahme nach Missionsende entfällt. Die
unvollständige Tweaker-Anbindung in SerpsModsHost wird entfernt; allgemeine dynamische
APIShared-Provider und Backendverträge bleiben unabhängig davon verfügbar.
