# TrailEditor – portabler Windows-x64-Release

Dieser Ordner ist das weitergebbare Paket. Er benötigt weder eine installierte .NET-Runtime noch das .NET-SDK und darf an einen beliebigen beschreibbaren Ort verschoben werden.

## Verwendung

1. `.trail`-Dateien in `sources` ablegen.
2. `unpack-all-trails.bat` starten.
3. Dateien unter `unpacked` bearbeiten.
4. `repack-all-trails.bat` starten.
5. Die neuen Dateien unter `repacked` entnehmen.

Vorhandene Ausgabeordner und `.trail`-Dateien werden aus Sicherheitsgründen nicht überschrieben. Bei einem Fehler bleibt das BAT-Fenster offen und zeigt die Ursache sowie den Exitcode an.

Der Release ist für 64-Bit-Windows gebaut. Zum Bearbeiten oder erneuten Bauen des Quellcodes wird das vollständige Quellprojekt mit seinen in der Haupt-README dokumentierten Abhängigkeiten benötigt.
