# Custom Custom Trail

- Fügt allen Custom Trails die Schaltfläche „Anpassen“ hinzu.
- Speichert passende Modsettings zusammen mit einzelnen Trailmissionen.
- Ermöglicht das Erstellen, Teilen und Spielen eigener Koop-Trails.

Custom Custom Trail erweitert den Traileditor und die Auswahl der Trails, ohne den gewohnten Ablauf des Spiels unnötig zu verändern. Normale Custom Trails erhalten eigene Modsettings pro Mission. Zusätzlich kannst du aus den Missionen deines Traileditors vollständige Koop-Trails für zwei menschliche Spieler erstellen.

## Modsettings in Trailmissionen

In den Modsettings von Custom Custom Trail findest du den Bereich „MOD SETTINGS IN CUSTOM TRAILS“. Dort wählst du aus, welche kompatiblen Mods beim Speichern einer Trailmission berücksichtigt werden sollen. Erkannte kompatible Mods sind zunächst aktiviert.

Gespeichert werden nur Einstellungen, die der Host für die jeweilige Mission vorgibt. Persönliche Einstellungen einzelner Spieler bleiben unberührt. So kann eine Mission beispielsweise mit einer bestimmten Einheitenbegrenzung, angepassten Kosten oder passenden Zufallsereignissen erstellt werden, sofern die jeweiligen Mods Custom Custom Trail unterstützen.

Beim Öffnen einer normalen oder einer Koop-Trailmission zeigt die Schaltfläche „Anpassen“ das gespeicherte Preset „Trail“. Im Traileditor kannst du diese Einstellungen bearbeiten. Beim normalen Spielen sind die Vorgaben der Mission geschützt, während die persönlichen Presets 1 und 2 der beteiligten Mods weiterhin unabhängig verwendet werden können.

Hat eine Mission keine gespeicherten Modsettings, bleibt sie vollständig spielbar. Custom Custom Trail verändert dann die aktuellen Einstellungen anderer Mods nicht.

## Einen normalen Custom Trail erstellen

Normale Custom Trails erstellst und exportierst du weiterhin mit den üblichen Funktionen des Traileditors:

1. Wähle in den Modsettings von Custom Custom Trail aus, welche kompatiblen Mods berücksichtigt werden sollen.
2. Lege deine Missionen an und speichere sie.
3. Exportiere den Trail wie gewohnt.

Die Modsettings werden passend zu jeder einzelnen Mission gespeichert. Beim Importieren, Sichern, Umbenennen oder Löschen eines Trails hält Custom Custom Trail diese zusätzlichen Missionsdaten automatisch mit den Trail-Dateien zusammen.

## Einen Koop-Trail erstellen

Ein Koop-Trail verwendet zwei menschliche Spieler und kann zusätzlich bis zu sechs KI-Gegner enthalten.

1. Richte jede Mission im Traileditor mit den gewünschten Spielern, Teams, Positionen und Einstellungen ein.
2. Speichere die Missionen wie gewohnt.
3. Öffne „Pfad exportieren“.
4. Aktiviere die Checkbox „Koop-Trail“.
5. Vergib einen Namen und exportiere den Trail.

Beim Export werden die Missionen diesen vier Koop-Trails zugeordnet:

- Mission 1–10 ersetzt Koop-Trail 1.
- Mission 11–20 ersetzt Koop-Trail 2.
- Mission 21–30 ersetzt Koop-Trail 3.
- Mission 31–40 ersetzt Koop-Trail 4.

Nur tatsächlich vorhandene Missionen werden übernommen; Lücken werden entfernt. Missionen nach Nummer 40 gehören zu keinem spielbaren Koop-Trail, bleiben aber in den bearbeitbaren Quelldateien des exportierten Pakets erhalten.

Die ersten beiden belegten Spielerslots werden Host und Gast. Ihre Positionen und Farben bleiben erhalten. Der Gast wird automatisch dem Team des Hosts zugeordnet. Ab dem dritten belegten Slot werden die Spieler als KI-Gegner übernommen. Fairness, Startgüter und Gebäudefreigaben stammen ebenfalls aus dem gespeicherten Lobbysetup.

Custom Maps, Custom Lords und AIV-Dateien, die von den Missionen benötigt werden, werden beim Export in das Koop-Paket aufgenommen. Dadurch kann das Paket vollständig weitergegeben werden, ohne dass andere Spieler die verwendeten Dateien einzeln zusammensuchen müssen.

Vor dem Export prüft der Mod alle Missionen und benötigten Dateien. Fehlen beispielsweise der zweite menschliche Spielerslot, eine Karte, ein Lord oder eine AIV-Datei, wird der Export abgebrochen und im Spiel eine verständliche Meldung angezeigt.

## Einen Koop-Trail bearbeiten

Exportierte Koop-Trails erscheinen im Traileditor sowohl unter „Pfad importieren“ als auch unter „Pfad exportieren“.

Über „Pfad importieren“ kannst du einen Koop-Trail wieder in den Traileditor laden und dort weiterbearbeiten. Die sichtbare Option zum Erstellen eines Backups funktioniert dabei wie bei normalen Trails. Beim erneuten Export kannst du das vorhandene Paket überschreiben oder einen neuen Namen vergeben.

Ein als Koop-Trail exportiertes Paket erscheint nur im Koop-Trail-Menü und nicht zusätzlich als normaler Custom Trail.

## Einen Koop-Trail spielen

Der Host wählt in den Modsettings von Custom Custom Trail entweder „Vanilla – kein eigenes Paket“ oder ein installiertes Koop-Trail-Paket aus. Gäste sehen die Auswahl des Hosts, können sie aber nicht selbst ändern.

Das ausgewählte Paket ersetzt nur die Koop-Trails und Missionen, für die es Inhalte besitzt. Nicht belegte Plätze bleiben unveränderte Vanilla-Koop-Trails.

Alle Teilnehmer benötigen Custom Custom Trail und dasselbe vollständige Koop-Paket. Der Mod prüft dies automatisch. Fehlt das Paket bei einem Spieler oder unterscheidet sich dessen Inhalt von der Version des Hosts, zeigt das Spiel eine passende Meldung mit dem betroffenen Spielernamen an. Eine ersetzte Mission kann erst gestartet werden, wenn die benötigten Pakete übereinstimmen.

Die Auswahl und Prüfung gelten nur für Missionen des gewählten Pakets. Vanilla-Missionen lassen sich weiterhin ohne dieses Paket starten.

## Einen Trail über den Steam Workshop teilen

Öffne nach dem Export die Ingame-Seite zum Hochladen eines Custom Trails und wähle den gewünschten Trail aus. Für normale und Koop-Trails zeigt Custom Custom Trail dort die Checkbox „Modsettings aufnehmen“ an. Sie ist bei jedem Öffnen der Seite aktiviert.

- Aktiviert: Die zur Mission gespeicherten Modsettings werden zusammen mit dem Trail hochgeladen.
- Deaktiviert: Der Trail wird ohne Modsettings hochgeladen.

Die Checkbox entscheidet nur, ob bereits gespeicherte Modsettings Teil des Uploads sind. Welche kompatiblen Mods beim Speichern einer Mission aufgenommen werden, legst du weiterhin im Bereich „MOD SETTINGS IN CUSTOM TRAILS“ fest.

Bei Koop-Trails bleibt die vollständige Paketstruktur mit Missionen und benötigten Dateien in beiden Fällen erhalten. Nur die gespeicherten Modsettings werden auf Wunsch weggelassen.

Nach dem Abonnieren muss das vollständige Paket bei allen Mitspielern installiert sein. Das Spiel überträgt Koop-Trail-Pakete nicht automatisch über die Mehrspieler-Lobby.

## Hinweise für Ersteller und Spieler

- Verwende vor dem Teilen am besten die Ingame-Exportfunktion, damit alle benötigten Dateien aufgenommen und geprüft werden.
- Teste jede Koop-Mission einmal mit den vorgesehenen Mods und Einstellungen.
- Teile den vollständigen exportierten Ordner, wenn du den Trail nicht über den Workshop verteilst.
- Achte darauf, dass Host und Gäste dieselbe Paketversion verwenden.
- Nicht installierte oder nicht kompatible Mods können von Custom Custom Trail nicht als Missionsvorgabe gespeichert werden.
