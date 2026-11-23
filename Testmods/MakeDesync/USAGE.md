# MakeDesync 0.1.0

MakeDesync löst auf dem Client gezielt einen Multiplayer-Resync aus. Für
die Probe brauchen beide Rechner MakeDesync 0.1.0, APIShared 0.67.0 und
Deposit Restoration 0.3.1. Verwende eine Kopie des Spielstands `test_stone`
mit übermauerten Stein- und Eisenvorkommen. Lass den Lord des Clients bei
voller Gesundheit und halte Kampf und Bauarbeiten während der Probe an.

| Kürzel auf dem Client | Erwartung |
| --- | --- |
| **Strg+Umschalt+F9** | Einmalig einen Lebenspunkt beim Lord abziehen. Vanilla stellt ihn wieder her. APIShared meldet `requestedRecords=0` und `transferredBytes=0`, wenn die lokalen Mod-Daten übereinstimmen. |
| **Strg+Umschalt+F10** | Einen gespeicherten Stein- oder Eiseneintrag unter einer Mauer auf dem Client löschen und dieselbe Lebenspunkt-Abweichung auslösen. Bei Deposit Restoration als einzigem registrierten Datensatz erwartet der Client `requestedRecords=1` und `transferredBytes=320804`. |

Pro Resync ist ein Versuch möglich. Warte auf `MAKE_DESYNC_RESYNC_COMPLETED`
und führe den zweiten Fall mit derselben unveränderten Spielstandkopie aus.
Entferne nach jedem abgeschlossenen Resync die Mauern und prüfe auf beiden
Rechnern, ob Stein und Eisen sichtbar werden.

## Logprüfung

Suche in `BepInEx/LogOutput.log` nach `MAKE_DESYNC_READY`. Ein
angenommenes Kürzel erzeugt `MAKE_DESYNC_ARMED`, der Schreibzugriff
`MAKE_DESYNC_APPLIED`. Bei fehlenden Voraussetzungen nennt
`MAKE_DESYNC_REJECTED` den Grund. Vergleiche
`MAKE_DESYNC_RESYNC_STARTED`, `MAKE_DESYNC_RESYNC_COMPLETED` und
`RESYNC_DATA_COMPLETED` für denselben Versuch. Entscheidend sind der
wiederhergestellte Lebenspunkt und beim zweiten Fall der wiederhergestellte
Vorkommenseintrag auf dem Client.
