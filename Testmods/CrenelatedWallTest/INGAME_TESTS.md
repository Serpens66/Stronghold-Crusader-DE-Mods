# Zinnen-Testmod: Ingame-Prüfung der ersten Fassung

Version 0.1.0. APIShared wird nur über bestehende öffentliche APIs verwendet und wurde nicht verändert oder gebaut. Der Testmod installiert keine nativen Hooks. Seine Aktivierung wird als Host-Einstellung synchronisiert und beim Beginn einer Partie festgehalten.

## Einstieg und Nachweis

1. Spiel nach der Installation neu starten. In den Lobby-Modoptionen **Crenelated Wall Test** aktivieren.
2. Auf einer Testkarte eine gewöhnliche Steinmauer errichten und daneben Zinnen bauen. Mit genügend Stein beginnen; anschließend dieselben Versuche mit wenig Stein wiederholen.
3. In `BepInEx/LogOutput.log` im aktuellen Spielstartabschnitt prüfen: `CRENEL_READY`, `CRENEL_NATIVE_READ`, `CRENEL_SESSION`, `CRENEL_RUNTIME_AFTER_CLEANUP` und nach dem ersten erfolgreichen Zinnenbau `CRENEL_HOOK_CONFIRMED`.
4. Erfolgreiche Bauten protokollieren `CRENEL_BUILD` mit `eligible = changed + unchanged`. Ablehnungen protokollieren `CRENEL_REJECT` und `CRENEL_REJECT_VERIFIED` mit `added=0` und unverändertem Steinbestand.
5. `CRENEL_CALLBACK_ERROR`, `CRENEL_POST_UNRESOLVED` oder unerwartetes `CRENEL_LOAD_SKIPPED` mit Screenshot und dem aktuellen Logabschnitt melden. Ein fehlender Start-/Tickmarker bedeutet, dass kein Laufzeitnachweis vorliegt.

Die Vorschau bleibt Vanilla: Ein Bauzug kann grün angezeigt und bei seiner synchronisierten Ausführung trotzdem abgelehnt werden. Abgelehnt wird der gesamte noch mögliche Bauzug, nicht ein nachträglich abgerissenes Teilstück. Normale Kosten, Schäden, Grundhöhen und Eigentümer verwaltet weiterhin Vanilla. Bei gesunkenem Steinbestand prüft der Mod den tatsächlich möglichen Präfix mit Vanillas eigener Verfügbarkeitsabfrage.

## Alle Sonderfälle

| Versuch | Erwartung der ersten Fassung | Worauf achten |
|---|---|---|
| Eine Zinne einzeln setzen | Hohes Endstück. | Normale Endgrafik, unveränderte Kosten. |
| Zweite daneben setzen | Beide Endstücke hoch. | Zulässige Zweierfolge. |
| Dritte daneben setzen | Hoch–niedrig–hoch. | Das zuvor einzeln gebaute Mittelstück muss niedrig werden. |
| Gerade Reihen mit 4, 5, 6 und 11 Feldern | Endstücke hoch, gerade Mitte möglichst wechselnd; höchstens eine notwendige Zweierfolge im geraden Abschnitt. | Gerade Längen können in der Mitte zwei niedrige Stücke brauchen. Einzelbau und Ziehen dürfen unterschiedliche gleichwertige Phasen erhalten. |
| Reihen in Gegenrichtung oder an beiden Enden verlängern | Dieselbe Wechselregel. | Alle vier Kartenrichtungen und alle Kamerarotationen testen. |
| Getrennte Reihen mit einer Lücke verbinden | Angeschlossenen eigenen Zug neu zuordnen. | Keine Dreierfolge nach dem Schließen; entfernte andere Mauern unverändert. |
| Kurzes L aus genau drei Feldern | Ablehnung des dritten Feldes. | Die drei Vanilla-Sonderstücke wären alle hoch. |
| L mit längeren Armen | Hohe Ecke; gerade Nachbarstücke niedrig. | Eckgrafik erhalten, keine Hochfolge um die Ecke. |
| Kurzes U, dichtes Zickzack oder kurze Verzweigung | Ablehnung, wenn feste Sonderstücke eine Dreierfolge erzwingen. | Ablehnungsgrund und Koordinaten im Log; diese Fälle sind bewusst konservativ. |
| T-Kreuzung mit längeren Armen | Hohe Verzweigung, angrenzende veränderbare Stücke niedrig. | Kurze zusätzliche Arme können abgelehnt werden. |
| Geschlossener Ring mit ausreichend langen Seiten | Hohe Eckstücke, Wechsel zwischen den Ecken. | Seiten mit gerader/ungerader Länge und das letzte Schließen testen. |
| Tatsächliche Diagonale im Kartenraster | Lange Dreierfolgen aus hohen Sonderstücken werden verhindert. | Die übliche isometrische Bildschirmdiagonale einer geraden Kartenachse ist davon nicht betroffen. |
| Zweite parallele Reihe neben einer vorhandenen | Gesamten Bauzug ablehnen, sobald ein neues gefülltes 2×2 entsteht. | Kein Steinverlust. Ein einzelnes Eckfeld ohne 2×2 ist noch keine verbotene Doppelreihe. |
| 2×2 über mehrere Einzelklicks vervollständigen | Letztes vervollständigendes Feld ablehnen. | Keine Umgehung durch Einzelbau oder Bau in Gegenrichtung. |
| An vorhandene Doppelreihe anbauen | Bestand nicht löschen; problematische Erweiterung ablehnen. | Der Mod repariert keine alten Doppelreihen beim Laden. |
| Anschluss an Tor oder Turm | Gebäudeflächen erhalten; angrenzende eigene Zinnen nur bei erfüllbaren Regeln korrigieren. | Grafik, Grundhöhe, Einheitengang und Anschluss kontrollieren. |
| Unterschiedliche Boden-/Mauerhöhen | Native Höhen erhalten; nur Zinnenmarkierung wechseln. | Keine abgesenkten Mauerkörper oder versetzten Einheiten. |
| Anschluss an fremde/alliierte Mauern | Fremde Flags und Eigentümer erhalten. | Fremde Nachbarn können die eigene Platzierung begrenzen. |
| Zinnen über beziehungsweise neben niedrigen Mauern | Native Höhenwahl erhalten. | Die Korrektur ändert nicht Vanillas Wahl der Grundhöhe. |
| Abriss einer Mitte oder Ecke | Betroffene gespeicherte Einträge entfernen und erreichbare eigene Nachbarn erneut prüfen. | Neue Endstücke dürfen hoch werden; unvermeidbare Sonderformen werden gemeldet, nicht automatisch abgerissen. |
| Beschuss und anschließender Wiederaufbau | Beschädigte Grafik erhalten; intakte neue Bauten erneut zuordnen. | Projektile und Nahkampfschaden testen; die begrenzte Tickprüfung kann entfernte Änderungen verzögert erkennen. |
| Bau mit knappem Steinbestand | Tatsächlich möglichen Präfix prüfen; einen problematischen kurzen Präfix vollständig ablehnen. | Auch mehrere schnell hintereinander abgeschickte Bauzüge testen. |
| Alter Save ohne Moddaten | Keine automatische Kartenkorrektur. | Erst ein neuer menschlicher Bau darf den angeschlossenen eigenen Zug ändern. |
| Save mit korrigierten Mauern | Zuordnung nach Neustart/Laden erhalten. | `CRENEL_SAVE` und `CRENEL_LOAD`; keine Änderung ungespeicherter anderer Mauern. |
| Mod für die nächste Partie deaktivieren | Neue Bauten verwenden Vanilla. | Aktivierungsänderungen beeinflussen die laufende Partie nicht. |
| KI baut oder erweitert ihre Burg | KI-Mauern bleiben Vanilla. | Keine eigenständigen KI-Korrekturen oder KI-Bauverbote. |

## Multiplayer

Auf Host und Client denselben Testmod und dieselben erforderlichen Abhängigkeiten verwenden. Nur der Host stellt die Aktivierung ein. Abwechselnd auf beiden PCs Einzelstücke, Linien, Anschlüsse und verbotene Doppelreihen bauen; auf beiden dieselben Entscheidungen und Zinnenformen erwarten. Auch eigene Mauern beider Spieler, alliierte Anschlüsse, knappen Steinbestand, Kartenwechsel sowie einen Multiplayer-Save nach komplettem Neustart testen. `LoadContext.IsSaveFile` allein ist keine zuverlässige Save-Erkennung; der Mod verwendet dafür den vorhandenen APIShared-Missionskontext.

Die automatisierten Zwei-Peer- und Save-Tests sind Modell-/Codecprüfungen. Eine reale Netzwerkpartie, Resynchronisation, Grafik und Einheitenhöhe gelten erst nach diesen Ingame-Versuchen als bestätigt. Editor-, Script-Extender-Debug-/Spiegelplatzierungen und direkte Fremdmod-Speicherwrites sind kein unterstützter menschlicher Baupfad dieser Testfassung.
