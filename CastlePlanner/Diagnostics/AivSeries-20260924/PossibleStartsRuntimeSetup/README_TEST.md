# Laufzeitprüfung der möglichen Startzustände

Native-DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die produktive CastlePlanner-Bibliothek und beide Diagnosemods wurden am
24.09.2026 bei beendetem Spiel mit ihren vorgesehenen `build.bat` installiert.
Die vorherige abgeschlossene Serienkonfiguration und ihr Fortschritt liegen
als `Previous-*.json` in diesem Ordner.

Die aktive Serie `aiv-possible-starts-regression-20260924` beginnt bei
`nextIndex=0` und umfasst in dieser Reihenfolge `CL-Early-off`,
`CL-Early-on`, `CL-Middle-off`, `CC-Early-off`, `CC-Middle-off`,
`CC-Middle-on`. Jeder Eintrag verwendet die geprüften sieben KI-Spieler,
Keep-Slots, AIV-Dateien und Map-Hashes aus der früheren Acht-Match-Serie.
Die zwei Sofortspawn-Paare prüfen, dass die erste KI weiterhin bewertet
werden kann und spätere KIs ohne vollständige Bauzustandsrekonstruktion
grau bleiben. Die vier Läufe ohne Sofortspawn prüfen die neue Freigabe
nur bei identischen Ergebnissen aller möglichen früheren Starts.

Pro Lauf eine neue lokale Skirmish-Lobby öffnen, die automatisch gesetzten
Spieler und Optionen unverändert lassen und die Karte bis zum sichtbaren
Spielbeginn starten. Auf den regulären späteren KI-Bau muss nicht gewartet
werden. Ein bestätigter Start rückt die Serie weiter. Die Auswertung
vergleicht anschließend Runtime-Farben und Gründe, Native-Fits und
Starttraces; eine Abweichung verhindert die allgemeine Freigabe.
