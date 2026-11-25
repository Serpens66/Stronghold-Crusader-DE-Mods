# MakeDesync 0.2.2

MakeDesync löst auf dem Client gezielt einen Multiplayer-Resync aus. Für
die Probe brauchen beide Rechner MakeDesync 0.2.2, APIShared ab 0.69.0 und
BugfixesAndQoL ab 1.0.240. Aktiviere für F10 im Hauptmod die Wiederherstellung
von Stein und Eisen. Verwende eine Kopie des Spielstands `test_stone`
mit übermauerten Stein- und Eisenvorkommen. Lass den Lord des Clients bei
voller Gesundheit und halte Kampf und Bauarbeiten während der Probe an.

| Kürzel auf dem Client | Erwartung |
| --- | --- |
| **Strg+Umschalt+F9** | Einmalig einen Lebenspunkt beim Lord abziehen. Vanilla stellt ihn wieder her. APIShared meldet `requestedRecords=0` und `transferredBytes=0`, wenn die lokalen Mod-Daten übereinstimmen. |
| **Strg+Umschalt+F10** | Einen gespeicherten Stein- oder Eiseneintrag unter einer Mauer auf dem Client löschen und dieselbe Lebenspunkt-Abweichung auslösen. Wenn nur die Vorkommenshistorie abweicht, erwartet der Client `requestedRecords=1` und `transferredBytes=320804`. |
| **Strg+Umschalt+F11** | Eine fertig ausgehobene, unbelegte Grabenkachel im nativen Kachelraster und den dazugehörigen privaten Eintrag in BugfixesAndQoL verändern. Der Lord bleibt unverändert. Auf dem Client müssen `divergedAtStart=True`, `tileRestored=True` und `historyRestored=True` erscheinen. |
| **Strg+Umschalt+F8** | Nach dem Verfüllen derselben F11-Grabenkachel den Bodentyp und das Ende der Historie auf dem Client prüfen. Erwartet `MAKE_DESYNC_MOAT_FILL ... restored=True`. |

Pro Resync ist ein Versuch möglich. Warte auf `MAKE_DESYNC_RESYNC_COMPLETED`
und führe den zweiten Fall mit derselben unveränderten Spielstandkopie aus.
Entferne nach jedem abgeschlossenen Resync die Mauern und prüfe auf beiden
Rechnern, ob Stein und Eisen sichtbar werden.

Für F11 in einer privaten Testpartie auf ruhiger Karte genau **eine** Kachel
auf Gras oder einem anderen Boden ausheben, dessen Bodentyp sich beim Graben
ändert. Die Option für die Wiederherstellung des ursprünglichen Grabenbodens muss
aktiv sein. Ausheben lassen, bis die Arbeitseinheit die Kachel verlassen hat.
Dann am Client F11 drücken. Erst nach dem bestätigten Abschluss dieselbe
Kachel normal verfüllen. Sobald sie verfüllt ist, am Client F8 drücken.
Bei `MAKE_DESYNC_REJECTED` vor der Änderung Voraussetzungen korrigieren und
erneut versuchen. Wenn eine bereits angewandte Abweichung keinen bestätigten
Abschluss ergibt, die Testpartie verlassen und die Ausgangskopie neu laden.
Die Testpartie nur nach einem bestätigten Abschluss weiterspielen oder speichern.

## Logprüfung

Suche in `BepInEx/LogOutput.log` nach `MAKE_DESYNC_READY`. Ein
angenommenes Kürzel erzeugt `MAKE_DESYNC_ARMED`, der Schreibzugriff
`MAKE_DESYNC_APPLIED`. Bei fehlenden Voraussetzungen nennt
`MAKE_DESYNC_REJECTED` den Grund. Vergleiche
`MAKE_DESYNC_RESYNC_STARTED`, `MAKE_DESYNC_RESYNC_COMPLETED` und
`RESYNC_DATA_COMPLETED` für denselben Versuch. Entscheidend sind der
wiederhergestellte Lebenspunkt und beim zweiten Fall der wiederhergestellte
Vorkommenseintrag auf dem Client.
