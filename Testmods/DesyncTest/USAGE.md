# DesyncTest 0.1.0

Passiver Testmod für SHCDE. Noch keine reale Host-/Client-, Editor- oder Performance-Abnahme. Nicht als bereits im Spiel validiert behandeln. Keine Netzwerkpakete, Gameplay-Mutationen oder absichtlichen Desyncs.

## Build und Paket

Aus dem Modordner `build.bat /source:working /noinstall /nopause` ausführen. Der Treiber prüft die relevanten Quellen, den nativen Vertrag, C#-/Maschinentests und die Offline-Auswertung. Er baut ein lokales Paket unter `BepInEx/plugins/DesyncTest_Serp` dieses Modordners. **Dieser Treiber enthält keinen Installationsschritt.** Buildnachweis im Paket; ausführliche Nachweise unter dem darin genannten `.work/a/desync-build/...`.

Abhängigkeiten werden referenziert, nicht mitgeliefert oder installiert: BepInEx, Script Extender mindestens 2.14.3 und APIShared mindestens 0.16.3. Der initiale native Vertrag ist ausschließlich für SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` freigegeben. Andere Hashes deaktivieren den Kern mit Fehlermeldung; keine alten Datenadressen verwenden.

Eine spätere manuelle Installation erfolgt nur bei geschlossenem Spiel durch Übernahme des lokalen Pluginordners. Für diesen Auftrag wird nichts installiert. SerpsModsHost und weitere Gameplay-Mods sind keine Voraussetzung. Nur ein Teilnehmer darf den Rekorder haben; für Vergleiche Aufnahmen von Host und Client sammeln.

## Konfiguration

Die erste Ausführung erzeugt `BepInEx/config/DesyncTest_Serp.cfg`. `DesyncTest_Serp.cfg.example` im Paket zeigt alle Ausgangswerte. Änderungen gelten nach Spielneustart.

- `Mode=Light`: Standard. Begrenzte Vorgeschichte im RAM; Vorfälle, Forced Runs und Sitzungsende lösen Ausgabe aus. Nach einem Resync bleibt dessen Paket erhalten. Keine zusätzlichen regulären Welt-Scans.
- `Mode=Deep`: Für einen reproduzierbaren Desync vor dem Spielstart einschalten. Höchstens `DurationSeconds=300` monotone Echtzeitsekunden ab abgeschlossenem Missionsstart, einschließlich Pausen. Danach endet die intensive Erfassung automatisch und Light läuft weiter. Kein Spielabbruch, keine Pause und kein Hookabbau. Der vorab reservierte Speicher bleibt bis Prozessende vorhanden.
- `Mode=Off`: keine Aufzeichnung. Installierte Hooks bleiben als Weiterleitung aktiv; deshalb für Kostenmessungen zusätzlich eine Variante ganz ohne Hilfsmod verwenden.
- `IntervalTicks=10`: zusätzliche Deep-Hashes im Scheduler-Nachlauf alle zehn tatsächlich weitergeschalteten Simulationsticks. Nicht aus Renderframes abgeleitet.
- `FocusUnitIds=13,27`: optional zusätzliche vollständige Aufnahmen dieser nativen Spiel-IDs. Global-ID wird je Aufnahme neu gelesen; wiederverwendete Slots nicht als identisches Objekt behandeln.

Deep prüft alle 9999 vom nativen Prüfsummenpfad erfassten Slots, einschließlich nicht lebender Einheiten. An nativen Prüfsummenpunkten vergleicht es die nachgebildete Prüfsumme gegen Vanillas gespeicherten Wert und erfasst Rohrecords/Pfade. Abweichungen dieser Selbstprüfung deaktivieren weitere zusätzliche Einheitenproben und werden als Vertragsfehler ausgewiesen.

Grenzen: Light-Ring 32 MiB, Deep-Ring 256 MiB, Writer-Kopierpuffer 64 MiB, Light-Resync-Daten 128 MiB pro Versuch, Ausgabe 4 GiB pro Sitzung einschließlich 128 KiB reservierter Metadaten. Das sind Ausgangswerte, keine Performancegarantie. Bei Verlust oder Erschöpfung wird die Aufnahme unvollständig markiert. Keine automatische Löschung alter Pakete; den Ausgabeordner nach Sicherung selbst verwalten.

## Ausgabe und Auswertung

Unter dem Spielordner `BepInEx/DesyncLogs/<Datum-Prozess-Sitzung>` entstehen `manifest.json`, `events.bin`, `snapshots.bin` und `summary.txt`. Noch laufende oder abgebrochene Aufnahmen besitzen `complete=false`; ausschließlich vollständige Blöcke sind lesbar. Ein Absturz kann den noch nicht geschriebenen Light-Ring verlieren. Die begrenzte Textübersicht nennt Versuch, Zeit, Tick und Einheitenidentität; ausgelassene Übersichtseinträge werden gezählt. Wiederholte Resyncs stehen gemeinsam im Sitzungspaket und erhalten im Bericht die Identität ihres Start-Sequenzwerts. Sie überschreiben keine früheren Dateiblöcke.

Die Erfassung unterscheidet Eingang, Verpackung, Größenmessung und tatsächliche Handler-Ausführung. Rohpakete aus Transportcallbacks haben bewusst keinen angeblichen Simulationstick. Interne Mod-Warteschlangen und effektive, tatsächlich angewandte Fremdmod-Einstellungen sind ohne Anbieter nicht erfasst; das Manifest weist diese Grenze aus. Komponenten und menschliche/AI-Spielerzuordnung werden beim Missionsstart aufgenommen. Native Dispatch-Ereignisse liefern spätere Quellspieler; nicht beobachtbare Metadatenänderungen sind kein sicherer Hostwechselnachweis.

Normale Eingangsheader liefern Quellspieler, Befehlskennung und vorläufigen 24-Bit-Plantick; Sonder-/Kompressionspakete bleiben roh. Der Offlinebericht ordnet passende normale Eingänge den Ausführungen zu und zeigt lokale Zeitabstände. Diese Abstände sind keine gemessene Netzwerklatenz. Ein Export-Eingang bestätigt keine erfolgreiche FIFO-Einreihung; lokale Sender empfangen ihren eigenen Netzwerkrecord normalerweise nicht.

Für Einzelanalyse oder Vergleich Python außerhalb des Spiels verwenden; das Werkzeug benötigt nur dessen Standardbibliothek:

    python Analyze-Desync.py "<Sitzungsordner>"
    python Analyze-Desync.py "<Host-Sitzung>" "<Client-Sitzung>" --output "<neuer Bericht außerhalb der Pakete>.json"

Auf diesem Rechner ist Python direkt unter `D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe` verfügbar. Immer konkrete Sitzungsordner angeben; lokale Sitzungsnummern verschiedener Rechner nicht gleichsetzen. Fremde Pluginlisten dürfen sich bei rein lokalen Mods unterscheiden. Bereits die Einzelanalyse zeigt verfügbare Reparatur-Endpunkte, geänderte Wire-Offsets, Identitätswechsel und unvollständige Deep-Aufnahmeserien. Der Vergleich nennt Abweichungen, fehlende Abdeckung und FocusUnitIds für eine gezielte Wiederholung; er erklärt keinen Mod allein wegen zeitlicher Nähe zum Verursacher.

## Abdeckung und Grenzen

Der Prototyp erfasst den allgemeinen nativen Chore-/Resync-Ablauf, die 14 Kategorieaggregate und rohe Felder während Resync/Deep. Semantische Vorher-/Nachher-Aufnahmen sind zunächst auf Einheiten und gespeicherte Pfade begrenzt. Andere Reparaturlayouts werden nicht geraten.

Der native Übertragungsrecord beginnt vier Byte vor der Extender-GameUnit-Sicht. Im Wire-Record beginnt der Hash bei `0x8C`; maskiert werden `0x41C..0x423`. `r_UnitSelected` liegt dort bei `0xB2`, strukturrelativ bei `0xAE`. Wire-Felddifferenzen im Bericht tragen deshalb ausdrücklich ihre Basis. Rohrecords enthalten auch nicht verglichene lokale Felder; nur auditierte Prüfsummeneingaben fließen in Gameplay-Vergleiche ein.

Deep kann hohe CPU-/Schreiblast erzeugen und zeitabhängige Fehler beeinflussen. Vollständige Aufnahmen und Pakete bleiben budgetiert; eine abgebrochene Aufnahmeserie ist kein vollständiger Snapshot aller Slots. Gleiche Prüfsummen oder ein beendeter Resync beweisen weder vollständige Zustandsgleichheit noch eine Ursache. Hostdaten sind die Reparaturquelle, nicht automatisch der richtige Zustand.

Vor Nutzung als dauerhaftes Supportwerkzeug fehlen reale Startup-/Missionsmarker nach Cleanup, ein echter Host-/Client-Resync, ein Lauf mit nur einseitiger Installation sowie abwechselnde Messungen ohne Mod, Off, Light und Deep. Die Offline-Maschinenprüfungen belegen deren ausgeführte Verträge, nicht die gesamte Unity-Laufzeit.
