# KI-Jäger-Test 0.1.0

Eigenständiger Testmod unter den Modoptionen als **AI Hunter Prey Test**. Benötigt APIShared ab 0.54.0 und den Script Extender ab 2.14.0. ImprovedHunters ist optional. Gameplay-Mod (`NetworkMode=1`): im Multiplayer auf allen Rechnern identisch installieren; Host-Einstellungen werden über APIShared synchronisiert. Ein echter Multiplayer-Spieltest steht noch aus.

Alle vier Schalter sind zunächst aktiv. Einstellungen vor der Partie ändern:

- **KI-Jäger-Test aktivieren:** gemeinsamer Hauptschalter.
- **AIV-Jäger nur bei Beute bauen:** sperrt normale KI-Bauversuche für Jäger ohne Beute. Menschliche Bauversuche bleiben unverändert.
- **Jäger auch ohne AIV-Auftrag bauen:** ergänzt auch bei Vanilla-Lords ohne Jäger im Burgbauplan eine Hütte. Grenze: eine lebende oder gerade entstehende Hütte je KI; vorhandene AIV-Hütten zählen mit. Normale Baukosten und Platzierungsregeln gelten. Sucht begrenzt nahe dem ersten Kornspeicher; ohne Platz, Ressourcen, funktionierenden Bergfried oder Kornspeicher wartet der Mod.
- **Leere KI-Jäger ohne Beute abreißen:** wartet auf abgeschlossene Verarbeitung und vollständige Fleischabgabe. Ein voller oder unerreichbarer Kornspeicher verzögert den Abriss unbegrenzt. Verwendet den normalen KI-Abriss mit Vanilla-Erstattung (im normalen Spiel 50 Prozent).

Der Scan läuft sofort beim Start und danach alle 4800 Kartenticks, ungefähr zwei Minuten bei Tempo 40. Pause hält die Prüfung an. Beute zählt kartenweit; die Entfernung und der Weg zur Beute werden nicht geprüft. Eigene Tiere zählen nur, wenn der registrierende Mod das ausdrücklich erlaubt. Frei sammelbare Kadaver zählen mit, wenn ImprovedHunters sie aktuell verwerten kann. Bereits von einem Jäger bearbeitete Tiere schützen dessen Hütte über die Arbeits- und Fleischprüfung.

Der APIShared-Katalog liefert auch neue, von anderen Mods registrierte Tierarten außerhalb des Extender-Enums. ImprovedHunters' deaktivierte Tierarten und sein Aktivierungszustand werden zusätzlich berücksichtigt. Diese optionale Integration liest einen Einstellungs-Snapshot und bindet für Kadaver dessen vorhandene private Abfrage. Ändert sich dieser Vertrag, setzt der Testmod seine Entscheidungen für die Partie aus und meldet `PREY_UNKNOWN`; er interpretiert einen Abfragefehler nicht als Beutemangel. Die übrigen Mods bleiben unverändert.

## Manuelle Prüffälle

1. Karte ohne Wild und ohne jagbare Modtiere: kein zusätzlicher Bau; ein AIV mit Jägerauftrag darf ebenfalls keine Hütte bauen. Derselbe AIV-Auftrag mit ausgeschalteter AIV-Option folgt wieder Vanilla.
2. Karte mit Beute und Vanilla-Lord ohne Jäger-AIV: bei ausreichend Ressourcen und Platz höchstens eine zusätzliche Hütte. Mit ausgeschalteter Automatik kein zusätzlicher Bau; AIV-Bauten bleiben davon unabhängig.
3. Mit ImprovedHunters ausschließlich Hasen, Kamele oder Hühner: deren Jagdeinstellung einschalten, dann Bau und tatsächliche Jagd prüfen. Jagd für diese einzige vorhandene Art vor einem weiteren Start ausschalten: kein zusätzlicher Bau. Auch ausschließlich Rehe mit ausgeschalteter Rehjagd prüfen.
4. Letztes Tier wird gejagt: Hütte bleibt während Rückweg, Verarbeitung und Fleischtransport bestehen. Kornspeicher voll machen: Hütte muss bestehen bleiben. Nach vollständiger Abgabe darf sie bei einer späteren Prüfung verschwinden.
5. Ausschließlich frische sammelbare Kadaver: Hütte bleibt erlaubt. Bereits verwertete/fadende Kadaver dürfen sie nicht dauerhaft erhalten.
6. Nach Beutemangel neue jagbare Tiere hinzufügen: nach der nächsten Prüfung darf wieder gebaut werden. Den Ablauf auch nach Speichern/Laden sowie einem Kartenwechsel prüfen.
7. Menschliche Hütte und Fleischtransport beobachten: kein Abriss durch den Testmod. Hauptschalter sowie die beiden Bauoptionen einzeln prüfen.

Für einen positiven Basistest genügt eine freie Baufläche nahe dem Kornspeicher auf derselben Wegkomponente wie der Bergfriedeingang. Die bewusst begrenzte Suche kann andere erreichbare Bauplätze übersehen. Es werden keine Tiere oder Rohstoffe hinzugefügt und keine bestehenden Gebäude verschoben.

## Build und Nachweise

`build.bat /nopause` baut den aktuellen Arbeitsstand gegen das geprüfte lokale APIShared-SDK und den installierten Script Extender, prüft den Mod und installiert ausschließlich dessen eigenen Pluginordner. Spiel vorher schließen. `build.bat /nopause /noinstall` erstellt nur den lokalen Paketordner `BepInEx/plugins/AIHunterPreyTest_Serp`. Den Treiber wegen der atomaren Preset-Persistenztests außerhalb einer Sandbox ausführen. Der bei dieser Vorbereitung geprüfte lokale APIShared-Build ist bytegleich mit der installierten DLL.

Supportmarker im BepInEx-Log: `AI_HUNTER READY`, danach `SESSION` und `SCAN`; `BUILD_BLOCKED`, `AUTOMATIC`, `RETIRED` zeigen konkrete Entscheidungen. `AUTOMATIC_WAIT` nennt fehlende Bauvoraussetzungen. `RETIREMENT_UNAVAILABLE`, `RETIREMENT_DISABLED`, `PREY_UNKNOWN` oder `FAILED` müssen vor der Auswertung geklärt werden. Jede Meldung enthält einen Zeitstempel mit Millisekunden.

Stand 15.11.2026, 12:18 Uhr: vollständiger Paketbuild ohne Installation erfolgreich; 49 Beute-/Fleischprüfungen und 88 fokussierte Presetprüfungen bestanden, dazu begrenzte Runtime-, Abhängigkeits-, ID- und Tooltip-Quellprüfungen. APIShared 0.54.0, ImprovedHunters 1.2.2; beide geprüften DLLs entsprechen den lokalen Buildausgaben. Auf Nutzerwunsch bleibt die Installation bis zur Meldung, dass das Spiel geschlossen ist, ausstehend. Kein Spiel gestartet, keine Maus-/Tastatursteuerung und kein bestandener Ingame-Test behauptet. Native Verträge und Updategrenzen stehen in `UpdateToNewDLL.md`.
